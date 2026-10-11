"""Demo mode: replays recorded results as if participants were passing right now.

Useful to rehearse a presentation, or to check the display without a headset.
Like the Unity experience, each participant file is first written with one scent,
then rewritten with one more scent after each "booth".
"""

import json
import tempfile
from pathlib import Path

from PySide6.QtCore import QObject, QTimer


class DemoReplayer(QObject):
    """Writes the example files, one booth at a time, into a temporary folder."""

    def __init__(self, examples_dir: Path, seconds_per_booth: float, parent: QObject | None = None):
        super().__init__(parent)
        self._temp_dir = tempfile.TemporaryDirectory(prefix="olfactoasis_demo_")
        self.output_dir = Path(self._temp_dir.name)

        # Every step to replay: (file name, participant data, number of scents to write)
        self._steps = []
        for path in sorted(examples_dir.glob("*.json")):
            data = json.loads(path.read_text(encoding="utf-8"))
            for scent_count in range(1, len(data["Scents"]) + 1):
                self._steps.append((path.name, data, scent_count))

        self._timer = QTimer(self)
        self._timer.timeout.connect(self._write_next_step)
        self._timer.start(int(seconds_per_booth * 1000))

    def _write_next_step(self) -> None:
        if not self._steps:
            self._timer.stop()
            return
        name, data, scent_count = self._steps.pop(0)
        partial = dict(data, Scents=data["Scents"][:scent_count])
        (self.output_dir / name).write_text(json.dumps(partial, indent=4), encoding="utf-8")
