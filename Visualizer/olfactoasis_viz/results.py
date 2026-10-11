"""Reading the result files written by the Unity experience.

One JSON file = one participant (see ScentCalibrationResults.cs). It holds every
scent calibrated so far, and Unity rewrites it after each booth.

One *result* = one scent of one participant: the curve of how pleasant the scent
was rated at each intensity, plus the intensity the system kept as optimal.

ResultsLibrary keeps the list of every result found in a few local folders, and
notifies the interface whenever a file appears or changes.
"""

import json
import logging
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path

from PySide6.QtCore import QObject, QTimer, Signal

from . import config

log = logging.getLogger(__name__)

# Values of EUserResponse, as written in the JSON.
POSITIVE = "POSITIVE"
NEGATIVE = "NEGATIVE"


@dataclass(frozen=True)
class CurvePoint:
    """Rating of one diffusion: pleasantness is > 0 when pleasant, < 0 when unpleasant."""

    intensity: float
    pleasantness: float
    perceived: bool


@dataclass(frozen=True)
class ScentResult:
    """Pleasantness of one scent according to its intensity, for one participant."""

    result_id: str           # unique and stable: "<file name>#<scent index>"
    scent_name: str          # as written in the JSON, e.g. "DARK_CHOCOLATE"
    participant_id: str
    start_date: datetime     # start of the participant's session
    booth_index: int         # order in which the scent was calibrated
    saved_at: float          # when the file was last written (Unix time), kept by the headset sync
    points: tuple[CurvePoint, ...]  # sorted by intensity
    optimal_intensity: float | None  # None when no intensity was found pleasant

    @property
    def optimal_point(self) -> CurvePoint | None:
        """The curve point the optimal marker is drawn on."""
        if self.optimal_intensity is None:
            return None
        return next((p for p in self.points if p.intensity == self.optimal_intensity), None)


def pleasantness_score(evaluation: dict) -> float:
    """Turns one evaluation into a signed pleasantness value.

    - not perceived, or neutral answer         -> 0
    - pleasant (thumb up) / unpleasant (down)  -> +/- the length of the "how much"
      trace drawn by the participant (ResponseMagnitude), or +/- 1 if no trace was drawn.
    """
    if evaluation.get("WasPerceived") != POSITIVE:
        return 0.0
    sign = {POSITIVE: 1.0, NEGATIVE: -1.0}.get(evaluation.get("WasPleasant"), 0.0)
    magnitude = float(evaluation.get("ResponseMagnitude", 0.0))
    return sign * magnitude if magnitude > 0 else sign


def build_curve(evaluations: list[dict]) -> tuple[CurvePoint, ...]:
    """One point per tested intensity, sorted. Repeated intensities are averaged."""
    by_intensity: dict[float, list[dict]] = {}
    for evaluation in evaluations:
        intensity = float(evaluation["Parameters"]["Strength"])
        by_intensity.setdefault(intensity, []).append(evaluation)

    points = []
    for intensity, group in sorted(by_intensity.items()):
        scores = [pleasantness_score(e) for e in group]
        perceived = any(e.get("WasPerceived") == POSITIVE for e in group)
        points.append(CurvePoint(intensity, sum(scores) / len(scores), perceived))
    return tuple(points)


def parse_results_file(path: Path) -> list[ScentResult]:
    """Reads one participant file. Raises ValueError/KeyError if it is not valid (yet)."""
    saved_at = path.stat().st_mtime
    data = json.loads(path.read_text(encoding="utf-8"))
    start_date = datetime.fromisoformat(data["StartDate"])

    results = []
    for booth_index, scent in enumerate(data["Scents"]):
        points = build_curve(scent.get("Evaluations", []))
        if not points:
            continue  # booth started but nothing rated yet

        # Unity leaves Strength at 0 when no intensity was rated pleasant.
        optimal = float(scent.get("OptimalParameters", {}).get("Strength", 0))

        results.append(ScentResult(
            result_id=f"{path.name}#{booth_index}",
            scent_name=scent.get("Name", "UNKNOWN"),
            participant_id=data.get("ParticipantId", ""),
            start_date=start_date,
            booth_index=booth_index,
            saved_at=saved_at,
            points=points,
            optimal_intensity=optimal if optimal > 0 else None,
        ))
    return results


class ResultsLibrary(QObject):
    """Watches result folders and keeps every result they contain, newest first.

    The folders are simply scanned every FOLDER_SCAN_SECONDS: there are only a few
    dozen small files, and scanning works the same whether a file was pulled from the
    headset, copied by hand, or replayed by the demo mode.
    """

    # (all results newest first, ids of the results that just appeared)
    results_changed = Signal(list, set)

    def __init__(self, folders: list[Path], parent: QObject | None = None):
        super().__init__(parent)
        self._folders = folders
        self._known_files: dict[Path, float] = {}       # path -> modification time read
        self._results_by_file: dict[Path, list[ScentResult]] = {}
        self._is_first_scan = True

        self._timer = QTimer(self)
        self._timer.timeout.connect(self.scan)
        self._timer.start(int(config.FOLDER_SCAN_SECONDS * 1000))

    @property
    def results(self) -> list[ScentResult]:
        """Every result, newest participant first, then latest booth first."""
        all_results = [r for results in self._results_by_file.values() for r in results]
        return sorted(all_results, key=lambda r: (r.start_date, r.booth_index), reverse=True)

    def scan(self) -> None:
        """Reloads new or modified files, forgets deleted ones, then notifies if anything changed."""
        previous_ids = {r.result_id for r in self.results}
        changed = False

        current_files = {
            path: path.stat().st_mtime
            for folder in self._folders if folder.is_dir()
            for path in folder.glob("*.json")
        }

        for path in set(self._known_files) - set(current_files):
            del self._known_files[path]
            self._results_by_file.pop(path, None)
            changed = True

        for path, mtime in current_files.items():
            if self._known_files.get(path) == mtime:
                continue
            self._known_files[path] = mtime
            try:
                self._results_by_file[path] = parse_results_file(path)
            except (OSError, ValueError, KeyError, TypeError) as error:
                # Most likely a file still being written: keep the previous version.
                # Finishing the write changes the modification time, so it gets retried.
                log.warning("Could not read %s (%s)", path.name, error)
                continue
            changed = True

        if changed or self._is_first_scan:
            new_ids = set() if self._is_first_scan else {r.result_id for r in self.results} - previous_ids
            self._is_first_scan = False
            self.results_changed.emit(self.results, new_ids)
