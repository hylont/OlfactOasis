"""Automatic copy of the results from the VR headset to the PC.

The Meta Quest is an Android device: once plugged in (with developer mode on), the
PC sees it through adb, the Android Debug Bridge, which ships with Unity's Android
module and with Meta Quest Developer Hub.

HeadsetSync runs in a background thread so the interface never freezes while adb
works. Every HEADSET_POLL_SECONDS it:
  1. looks for a plugged-in headset (`adb devices`),
  2. lists the result files on it, with their size and modification date,
  3. pulls the ones that are new or differ from the local copy.
The interface does not need to know about it: ResultsLibrary sees the pulled files
appear in the local folder.
"""

import glob
import logging
import os
import shutil
import subprocess
import sys
from enum import Enum
from pathlib import Path

from PySide6.QtCore import QThread, Signal

from . import config

log = logging.getLogger(__name__)

# Prevents a console window from flashing on every adb call on Windows.
_NO_WINDOW = subprocess.CREATE_NO_WINDOW if sys.platform == "win32" else 0


class HeadsetState(Enum):
    """What the status bar tells the operator. Values are the displayed texts."""

    ADB_MISSING = "adb introuvable : synchronisation du casque impossible"
    DISCONNECTED = "Casque non branché"
    UNAUTHORIZED = "Casque branché : autorisez le débogage USB dans le casque"
    CONNECTED = "Casque connecté"


def find_adb() -> str | None:
    """Returns the path of an adb executable, or None if none is installed.

    Looks in the PATH first, then in the usual install folders of the Android SDK,
    Meta Quest Developer Hub and Unity's Android module (newest Unity first).
    """
    on_path = shutil.which("adb")
    if on_path:
        return on_path

    exe = "adb.exe" if sys.platform == "win32" else "adb"
    candidates = [
        Path(os.environ[var]) / "platform-tools" / exe
        for var in ("ANDROID_HOME", "ANDROID_SDK_ROOT") if var in os.environ
    ]
    candidates += [
        Path(os.environ.get("LOCALAPPDATA", "")) / "Android" / "Sdk" / "platform-tools" / exe,
        Path(r"C:\Program Files\Meta Quest Developer Hub\resources\bin") / exe,
    ]
    unity_adbs = glob.glob(
        r"C:\Program Files\Unity\Hub\Editor\*\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe"
    )
    candidates += [Path(p) for p in sorted(unity_adbs, reverse=True)]

    return next((str(p) for p in candidates if p.is_file()), None)


class HeadsetSync(QThread):
    """Background thread copying the headset's result files into a local folder."""

    # (state, detail text, e.g. the number of results on the headset)
    status_changed = Signal(object, str)

    def __init__(self, adb_path: str | None, local_dir: Path, package: str = config.ANDROID_PACKAGE):
        super().__init__()
        self._adb = adb_path
        self._local_dir = local_dir
        self._remote_dir = config.REMOTE_RESULTS_DIR.format(package=package)
        self._last_status: tuple[HeadsetState, str] | None = None

    # ------------------------------------------------------------------ thread

    def run(self) -> None:
        if self._adb is None:
            self._set_status(HeadsetState.ADB_MISSING)
            return

        self._local_dir.mkdir(parents=True, exist_ok=True)
        while not self.isInterruptionRequested():
            try:
                self._poll_once()
            except (OSError, subprocess.SubprocessError) as error:
                log.warning("Headset synchronization failed: %s", error)
                self._set_status(HeadsetState.DISCONNECTED, "erreur de communication")
            self._sleep(config.HEADSET_POLL_SECONDS)

    def stop(self) -> None:
        """Asks the thread to finish and waits for it."""
        self.requestInterruption()
        self.wait()

    def _sleep(self, seconds: float) -> None:
        """Sleeps in small steps, so that closing the app is never delayed."""
        for _ in range(int(seconds * 10)):
            if self.isInterruptionRequested():
                return
            self.msleep(100)

    # ------------------------------------------------------------------ steps

    def _poll_once(self) -> None:
        serial, state = self._find_headset()
        if serial is None:
            self._set_status(state)
            return

        remote_files = self._list_remote_files(serial)
        for name, (size, mtime) in remote_files.items():
            if not self._is_up_to_date(name, size, mtime):
                self._pull(serial, name)

        count = len(remote_files)
        self._set_status(HeadsetState.CONNECTED, f"{count} participant{'s' if count > 1 else ''} dans le casque")

    def _find_headset(self) -> tuple[str | None, HeadsetState]:
        """Returns (serial of the first usable device or None, state to display)."""
        output = self._adb_run("devices")
        # Lines look like "1WMHH123456789\tdevice" (or "unauthorized", "offline").
        devices = [line.split("\t") for line in output.splitlines()[1:] if "\t" in line]
        for serial, device_state in devices:
            if device_state == "device":
                return serial, HeadsetState.CONNECTED
        if any(device_state == "unauthorized" for _, device_state in devices):
            return None, HeadsetState.UNAUTHORIZED
        return None, HeadsetState.DISCONNECTED

    def _list_remote_files(self, serial: str) -> dict[str, tuple[int, int]]:
        """Returns {file name: (size in bytes, modification time in seconds)} on the headset."""
        # Quoted for the headset's shell, where "|" would otherwise mean a pipe.
        command = f"stat -c '%n|%s|%Y' {self._remote_dir}/*.json"
        output = self._adb_run("-s", serial, "shell", command, check=False)

        files = {}
        for line in output.splitlines():
            parts = line.strip().rsplit("|", 2)
            if len(parts) == 3 and parts[1].isdigit() and parts[2].isdigit():
                files[parts[0].rsplit("/", 1)[-1]] = (int(parts[1]), int(parts[2]))
        return files  # empty when the folder does not exist yet

    def _is_up_to_date(self, name: str, size: int, mtime: int) -> bool:
        """True if the local copy has the same size and date as the headset's file."""
        local = self._local_dir / name
        if not local.is_file():
            return False
        stat = local.stat()
        return stat.st_size == size and int(stat.st_mtime) == mtime

    def _pull(self, serial: str, name: str) -> None:
        """Copies one file from the headset, keeping its date (-a).

        The copy goes to a temporary file first, then replaces the local file in one
        go: ResultsLibrary never reads a half-copied file.
        """
        final_path = self._local_dir / name
        temp_path = final_path.with_name(name + ".part")
        self._adb_run("-s", serial, "pull", "-a", f"{self._remote_dir}/{name}", str(temp_path), timeout=30)
        os.replace(temp_path, final_path)
        log.info("Pulled %s from the headset", name)

    # ------------------------------------------------------------------ helpers

    def _adb_run(self, *args: str, check: bool = True, timeout: float = 10) -> str:
        completed = subprocess.run(
            [self._adb, *args], capture_output=True, text=True, timeout=timeout,
            check=check, creationflags=_NO_WINDOW,
        )
        return completed.stdout

    def _set_status(self, state: HeadsetState, detail: str = "") -> None:
        """Emits the status only when it changes, to avoid repainting for nothing."""
        if self._last_status != (state, detail):
            self._last_status = (state, detail)
            self.status_changed.emit(state, detail)
