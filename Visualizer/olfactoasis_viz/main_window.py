"""Main window: a title bar, the grid of result cards, and a footer.

Keyboard (also shown in the footer):
    F11 / F         toggle full screen (double-click works too)
    Échap           leave full screen
    + / -           more / fewer cards per page
    ← / →           older / newer results (also Page Up / Page Down)
    Début (Home)    back to the latest results
"""

import math
import time

from PySide6.QtCore import Qt
from PySide6.QtWidgets import QGridLayout, QHBoxLayout, QLabel, QMainWindow, QVBoxLayout, QWidget

from . import config
from .headset import HeadsetState
from .result_card import ResultCard
from .results import ScentResult

STATUS_COLORS = {
    HeadsetState.CONNECTED: config.COLOR_STATUS_OK,
    HeadsetState.UNAUTHORIZED: config.COLOR_STATUS_WARNING,
    HeadsetState.ADB_MISSING: config.COLOR_STATUS_WARNING,
    HeadsetState.DISCONNECTED: config.COLOR_STATUS_OFF,
}


def best_grid(count: int, width: float, height: float, aspect: float) -> tuple[int, int]:
    """Returns (columns, rows) giving the largest cards of the given aspect ratio."""
    best, best_area = (1, count), 0.0
    for columns in range(1, count + 1):
        rows = math.ceil(count / columns)
        card_width = min(width / columns, height / rows * aspect)
        if card_width * card_width / aspect > best_area:
            best, best_area = (columns, rows), card_width * card_width / aspect
    return best


def shared_y_max(results: list[ScentResult]) -> float:
    """Pleasantness range shown by every card: all curves fit, and they can be compared."""
    largest = max((abs(p.pleasantness) for r in results for p in r.points), default=0.0)
    # Never below the usual largest traces (~1.75), so the scale does not jump while
    # the first results of the day arrive; with headroom, rounded up to 0.25.
    return math.ceil(max(largest, 1.75) * 1.15 * 4) / 4


class MainWindow(QMainWindow):
    def __init__(self, show_headset_status: bool = True):
        super().__init__()
        self.setWindowTitle("OlfactOasis – Résultats")
        self.resize(1600, 900)

        self._results: list[ScentResult] = []
        self._highlight_until: dict[str, float] = {}  # result id -> end of its glow (time.monotonic)
        self._cards_per_page = config.DEFAULT_CARDS_PER_PAGE
        self._page = 0
        self._cards: list[ResultCard] = []

        self._build_ui(show_headset_status)
        self._refresh_page()

    # ------------------------------------------------------------------ layout

    def _build_ui(self, show_headset_status: bool) -> None:
        font = f"font-family: '{config.FONT_FAMILY}';"
        central = QWidget()
        central.setStyleSheet(f"background: {config.COLOR_BACKGROUND};")
        self.setCentralWidget(central)

        title = QLabel(
            f"<span style='color:{config.COLOR_TEXT}; font-weight:800'>OlfactOasis</span>"
            f"<span style='color:{config.COLOR_TEXT_SECONDARY}'>&nbsp;&nbsp;·&nbsp;&nbsp;"
            f"Quelle intensité pour quelle odeur&nbsp;?</span>")
        title.setStyleSheet(f"{font} font-size: 34px;")

        self._status = QLabel()
        self._status.setStyleSheet(f"{font} font-size: 20px; color: {config.COLOR_TEXT_SECONDARY};")
        self._status.setVisible(show_headset_status)

        header = QHBoxLayout()
        header.addWidget(title)
        header.addStretch(1)
        header.addWidget(self._status)

        self._grid_host = QWidget()
        self._grid = QGridLayout(self._grid_host)
        self._grid.setContentsMargins(0, 0, 0, 0)
        self._grid.setSpacing(20)

        self._empty = QLabel()
        self._empty.setAlignment(Qt.AlignmentFlag.AlignCenter)
        self._empty.setStyleSheet(f"{font} font-size: 40px; color: {config.COLOR_TEXT_MUTED};")
        self._empty.setText("En attente des premiers résultats…<br>"
                            "<span style='font-size:26px'>Branchez le casque à ce PC.</span>")

        self._page_label = QLabel()
        self._page_label.setStyleSheet(f"{font} font-size: 18px; color: {config.COLOR_TEXT_SECONDARY};")
        legend = QLabel(
            f"<span style='color:{config.COLOR_TEXT}'>●</span> odeur perçue &nbsp;&nbsp; "
            f"<span style='color:{config.COLOR_TEXT}'>○</span> non perçue &nbsp;&nbsp; "
            f"<span style='color:{config.COLOR_TEXT}'>★</span> intensité optimale choisie par le système")
        legend.setStyleSheet(f"{font} font-size: 18px; color: {config.COLOR_TEXT_SECONDARY};")
        hints = QLabel("F11 plein écran  ·  + / − taille de la grille  ·  ← / → pages")
        hints.setStyleSheet(f"{font} font-size: 15px; color: {config.COLOR_TEXT_MUTED};")
        footer = QHBoxLayout()
        footer.addWidget(self._page_label)
        footer.addStretch(1)
        footer.addWidget(legend)
        footer.addStretch(1)
        footer.addWidget(hints)

        root = QVBoxLayout(central)
        root.setContentsMargins(28, 20, 28, 14)
        root.setSpacing(16)
        root.addLayout(header)
        root.addWidget(self._grid_host, stretch=1)
        root.addWidget(self._empty, stretch=1)
        root.addLayout(footer)

    # ------------------------------------------------------------------ data in

    def set_results(self, results: list[ScentResult], new_ids: set[str]) -> None:
        """Called by ResultsLibrary whenever results appear or change (newest first)."""
        self._results = results
        if new_ids:
            self._page = 0  # show the newcomers
            recent = time.time() - config.HIGHLIGHT_MAX_AGE_MINUTES * 60
            end = time.monotonic() + config.HIGHLIGHT_SECONDS
            self._highlight_until.update(
                {r.result_id: end for r in results if r.result_id in new_ids and r.saved_at > recent})
        self._refresh_page()

    def set_headset_status(self, state: HeadsetState, detail: str) -> None:
        text = state.value + (f"  ·  {detail}" if detail else "")
        self._status.setText(f"<span style='color:{STATUS_COLORS[state]}'>●</span>&nbsp; {text}")

    # ------------------------------------------------------------------ grid

    def _page_count(self) -> int:
        return max(1, math.ceil(len(self._results) / self._cards_per_page))

    def _refresh_page(self) -> None:
        """Rebuilds the cards of the current page."""
        for card in self._cards:
            card.deleteLater()
        self._cards = []

        self._page = min(self._page, self._page_count() - 1)
        start = self._page * self._cards_per_page
        y_max = shared_y_max(self._results)
        now = time.monotonic()
        for result in self._results[start:start + self._cards_per_page]:
            remaining = (self._highlight_until.get(result.result_id, 0) - now) / config.HIGHLIGHT_SECONDS
            self._cards.append(ResultCard(result, y_max, highlight=max(0.0, remaining)))

        has_results = bool(self._results)
        self._grid_host.setVisible(has_results)
        self._empty.setVisible(not has_results)
        count = len(self._results)
        self._page_label.setText(
            f"Page {self._page + 1} / {self._page_count()}  ·  {count} résultat{'s' if count > 1 else ''}"
            if has_results else "")
        self._place_cards()

    def _place_cards(self) -> None:
        """Puts the cards in the grid, choosing the number of columns that makes them largest."""
        while self._grid.count():
            self._grid.takeAt(0)
        for i in range(self._grid.columnCount()):
            self._grid.setColumnStretch(i, 0)
        for i in range(self._grid.rowCount()):
            self._grid.setRowStretch(i, 0)
        if not self._cards:
            return

        # Lay out the grid for a full page, so cards keep the same size on the last page.
        slots = max(len(self._cards), min(self._cards_per_page, len(self._results)))
        columns, rows = best_grid(slots, self._grid_host.width() or self.width(),
                                  self._grid_host.height() or self.height(), config.CARD_ASPECT_RATIO)
        for index, card in enumerate(self._cards):
            self._grid.addWidget(card, index // columns, index % columns)
        for column in range(columns):
            self._grid.setColumnStretch(column, 1)
        for row in range(rows):
            self._grid.setRowStretch(row, 1)

    def resizeEvent(self, event) -> None:
        super().resizeEvent(event)
        self._place_cards()

    # ------------------------------------------------------------------ controls

    def toggle_fullscreen(self) -> None:
        if self.isFullScreen():
            self.showNormal()
        else:
            self.showFullScreen()

    def keyPressEvent(self, event) -> None:
        key = event.key()
        if key in (Qt.Key.Key_F11, Qt.Key.Key_F):
            self.toggle_fullscreen()
        elif key == Qt.Key.Key_Escape and self.isFullScreen():
            self.showNormal()
        elif key in (Qt.Key.Key_Plus, Qt.Key.Key_Equal):
            self._set_cards_per_page(self._cards_per_page + 1)
        elif key in (Qt.Key.Key_Minus, Qt.Key.Key_Underscore):
            self._set_cards_per_page(self._cards_per_page - 1)
        elif key in (Qt.Key.Key_Right, Qt.Key.Key_PageDown):
            self._set_page(self._page + 1)
        elif key in (Qt.Key.Key_Left, Qt.Key.Key_PageUp):
            self._set_page(self._page - 1)
        elif key == Qt.Key.Key_Home:
            self._set_page(0)
        else:
            super().keyPressEvent(event)

    def mouseDoubleClickEvent(self, event) -> None:
        self.toggle_fullscreen()

    def _set_cards_per_page(self, count: int) -> None:
        count = max(config.MIN_CARDS_PER_PAGE, min(config.MAX_CARDS_PER_PAGE, count))
        if count != self._cards_per_page:
            first_shown = self._page * self._cards_per_page
            self._cards_per_page = count
            self._page = first_shown // count  # keep the same results in view
            self._refresh_page()

    def _set_page(self, page: int) -> None:
        page = max(0, min(self._page_count() - 1, page))
        if page != self._page:
            self._page = page
            self._refresh_page()
