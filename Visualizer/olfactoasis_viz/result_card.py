"""One card of the grid: the pleasantness curve of one scent for one participant.

    ● Chocolat noir                         Optimum 55 %
    12 oct. · 09:10 · participant 8e7a
    ┌──────────────────────────────────────────────────┐
    │ Agréable        ___★                             │
    │ Neutre  ___o___/    `---o                        │
    │ Désagréable                                      │
    └─────3──────7──────20──────55──────100────────────┘
                     Intensité (%)

Everything (fonts, line widths, markers) is sized from the card's own size, so the
cards stay readable from afar whatever the number of cards in the grid.
"""

import math

import pyqtgraph as pg
from PySide6.QtCore import QEasingCurve, QRectF, Qt, QVariantAnimation
from PySide6.QtGui import QColor, QFont, QFontMetrics, QPainter, QPen
from PySide6.QtWidgets import QFrame, QHBoxLayout, QLabel, QSizePolicy, QVBoxLayout, QWidget

from . import config
from .results import ScentResult

MONTHS = ["janv.", "févr.", "mars", "avr.", "mai", "juin", "juil.", "août", "sept.", "oct.", "nov.", "déc."]

# Intensities are spread on a logarithmic axis: the tested strengths (3, 7, 20, 55, 100)
# are roughly evenly spaced on it, so the low intensities do not get squeezed together.
AXIS_INTENSITIES = (3, 7, 20, 55, 100)


def to_axis(intensity: float) -> float:
    """Position of an intensity on the (logarithmic) horizontal axis."""
    return math.log10(max(intensity, 1.0))


def with_alpha(hex_color: str, alpha: int) -> QColor:
    color = QColor(hex_color)
    color.setAlpha(alpha)
    return color


def blend(color_a: str, color_b: str, t: float) -> str:
    """Color between color_a (t = 0) and color_b (t = 1), as hex."""
    a, b = QColor(color_a), QColor(color_b)
    mixed = QColor(
        round(a.red() + (b.red() - a.red()) * t),
        round(a.green() + (b.green() - a.green()) * t),
        round(a.blue() + (b.blue() - a.blue()) * t),
    )
    return mixed.name()


class ResultCard(QFrame):
    """Displays one ScentResult. `y_max` is shared by all cards so that they compare."""

    def __init__(self, result: ScentResult, y_max: float, highlight: float = 0.0, parent: QWidget | None = None):
        """`highlight` (0 to 1) is how much of the "new result" glow is left to play."""
        super().__init__(parent)
        self.result = result
        self._y_max = y_max
        self._name, self._color = config.scent_style(result.scent_name)
        self._border_color = config.COLOR_CARD_BORDER

        # The grid alone decides the card size. Without this, the text (sized from the
        # card) would ask for a larger card, which would enlarge the text, and so on.
        self.setSizePolicy(QSizePolicy.Policy.Ignored, QSizePolicy.Policy.Ignored)

        self._build_header()
        self._build_plot()

        layout = QVBoxLayout(self)
        layout.addLayout(self._header)
        layout.addWidget(self._plot, stretch=1)
        self._layout = layout

        self._apply_style()
        self._start_highlight(highlight)

    # ------------------------------------------------------------------ building

    def _build_header(self) -> None:
        r = self.result
        self._dot = QLabel("●")
        self._title = QLabel(self._name)
        self._new_badge = QLabel("NOUVEAU")
        self._optimum_text = (f"Optimum {r.optimal_intensity:g} %" if r.optimal_intensity is not None
                              else "Aucune intensité agréable")
        self._optimum = QLabel(self._optimum_text.replace("Optimum ", "Optimum <b>") + "</b>"
                               if r.optimal_intensity is not None else self._optimum_text)
        self._meta = QLabel(
            f"{r.start_date.day} {MONTHS[r.start_date.month - 1]} · {r.start_date:%H:%M}"
            f" · participant {r.participant_id[:4]}"
        )

        title_row = QHBoxLayout()
        self._title_row = title_row
        title_row.addWidget(self._dot)
        title_row.addWidget(self._title)
        title_row.addWidget(self._new_badge)
        title_row.addStretch(1)
        title_row.addWidget(self._optimum)

        self._header = QVBoxLayout()
        self._header.setSpacing(0)
        self._header.addLayout(title_row)
        self._header.addWidget(self._meta)

    def _build_plot(self) -> None:
        self._plot = pg.PlotWidget(background=config.COLOR_CARD)
        item = self._plot.getPlotItem()
        item.hideButtons()
        item.setMenuEnabled(False)
        item.setMouseEnabled(x=False, y=False)
        self._plot.setFocusPolicy(Qt.FocusPolicy.NoFocus)
        self._plot.setFrameShape(QFrame.Shape.NoFrame)

        # Fixed view: every card shows the same intensities and pleasantness range.
        x_min, x_max = to_axis(2.4), to_axis(125)
        item.setXRange(x_min, x_max, padding=0)
        item.setYRange(-self._y_max, self._y_max, padding=0)

        # Faint green half above zero (pleasant), faint red half below (unpleasant).
        for low, high, color in ((0, self._y_max, config.COLOR_PLEASANT_ZONE),
                                 (-self._y_max, 0, config.COLOR_UNPLEASANT_ZONE)):
            zone = pg.LinearRegionItem((low, high), orientation="horizontal", movable=False,
                                       brush=with_alpha(color, 22), pen=pg.mkPen(None))
            zone.setZValue(-20)
            item.addItem(zone)

        self._zero_line = pg.InfiniteLine(pos=0, angle=0)
        self._zero_line.setZValue(-10)
        item.addItem(self._zero_line)

        # Optimal intensity: a vertical guide and a star on the curve.
        optimal = self.result.optimal_point
        self._optimal_line = None
        if optimal is not None:
            self._optimal_line = pg.InfiniteLine(pos=to_axis(optimal.intensity), angle=90)
            self._optimal_line.setZValue(-5)
            item.addItem(self._optimal_line)

        xs = [to_axis(p.intensity) for p in self.result.points]
        ys = [p.pleasantness for p in self.result.points]
        self._curve = pg.PlotDataItem(xs, ys, fillLevel=0, fillBrush=with_alpha(self._color, 55))
        item.addItem(self._curve)

        # Filled dots where the scent was perceived, hollow ones where it was not.
        self._perceived_dots = pg.ScatterPlotItem(
            [x for x, p in zip(xs, self.result.points) if p.perceived],
            [y for y, p in zip(ys, self.result.points) if p.perceived])
        self._unperceived_dots = pg.ScatterPlotItem(
            [x for x, p in zip(xs, self.result.points) if not p.perceived],
            [y for y, p in zip(ys, self.result.points) if not p.perceived])
        item.addItem(self._perceived_dots)
        item.addItem(self._unperceived_dots)

        self._star = None
        if optimal is not None:
            self._star = pg.ScatterPlotItem([to_axis(optimal.intensity)], [optimal.pleasantness], symbol="star")
            self._star.setZValue(10)
            item.addItem(self._star)

        bottom, left = item.getAxis("bottom"), item.getAxis("left")
        bottom.setTicks([[(to_axis(i), str(i)) for i in AXIS_INTENSITIES]])
        third = self._y_max * 0.62
        left.setTicks([[(third, "Agréable"), (0, "Neutre"), (-third, "Désagréable")]])

    # ------------------------------------------------------------------ sizing

    def resizeEvent(self, event) -> None:
        super().resizeEvent(event)
        self._apply_style()

    def _scale(self) -> float:
        """1.0 for a 520 x 340 card, more for bigger cards, less for smaller ones."""
        return max(0.55, min(2.4, self.width() / 520, self.height() / 340))

    def _apply_style(self) -> None:
        s = self._scale()
        px = lambda size: max(9, round(size * s))  # noqa: E731 - font size helper

        border = max(2, round(3 * s))  # keep the content inside the border drawn by paintEvent
        self._layout.setContentsMargins(px(18) + border, px(12) + border, px(14) + border, px(8) + border)
        self.setStyleSheet(f"QLabel {{ background: transparent; font-family: '{config.FONT_FAMILY}'; }}")
        # Title row: as large as the card allows, but shrunk when it would not fit its width.
        fit = self._title_row_fit(s, border)
        hpx = lambda size: max(9, round(size * s * fit))  # noqa: E731
        self._title_row.setSpacing(hpx(10))
        self._dot.setStyleSheet(f"color: {self._color}; font-size: {hpx(30)}px;")
        self._title.setStyleSheet(f"color: {config.COLOR_TEXT}; font-size: {hpx(30)}px; font-weight: 700;")
        self._new_badge.setStyleSheet(
            f"color: {config.COLOR_BACKGROUND}; background: {self._color}; font-size: {hpx(13)}px;"
            f"font-weight: 800; border-radius: {hpx(6)}px; padding: {hpx(2)}px {hpx(7)}px;")
        self._optimum.setStyleSheet(f"color: {config.COLOR_TEXT}; font-size: {hpx(24)}px;")
        self._meta.setStyleSheet(f"color: {config.COLOR_TEXT_MUTED}; font-size: {px(16)}px;")

        # Plot marks
        self._curve.setPen(pg.mkPen(self._color, width=4.5 * s))
        self._perceived_dots.setSize(px(15))
        self._perceived_dots.setBrush(pg.mkBrush(self._color))
        self._perceived_dots.setPen(pg.mkPen(config.COLOR_CARD, width=2.5 * s))
        self._unperceived_dots.setSize(px(14))
        self._unperceived_dots.setBrush(pg.mkBrush(config.COLOR_CARD))
        self._unperceived_dots.setPen(pg.mkPen(config.COLOR_TEXT_MUTED, width=2.5 * s))
        self._zero_line.setPen(pg.mkPen(config.COLOR_GRID, width=1.5 * s))
        if self._star is not None:
            self._star.setSize(px(40))
            self._star.setBrush(pg.mkBrush(config.COLOR_TEXT))
            self._star.setPen(pg.mkPen(config.COLOR_CARD, width=3 * s))
            self._optimal_line.setPen(pg.mkPen(config.COLOR_TEXT_SECONDARY, width=2 * s, style=Qt.PenStyle.DashLine))

        # Axes
        tick_font = QFont(config.FONT_FAMILY)
        tick_font.setPixelSize(px(19))
        item = self._plot.getPlotItem()
        for name in ("bottom", "left"):
            axis = item.getAxis(name)
            axis.setStyle(tickFont=tick_font, tickLength=0, tickTextOffset=px(8))
            axis.setTextPen(config.COLOR_TEXT_SECONDARY)
            axis.setPen(config.COLOR_GRID)
        item.getAxis("bottom").setLabel("Intensité (%)", color=config.COLOR_TEXT_MUTED, **{"font-size": f"{px(17)}px"})

    def _title_row_fit(self, s: float, border: int) -> float:
        """Factor (at most 1) by which the title row must shrink to fit in the card width."""
        def text_width(text: str, size: float) -> int:
            font = QFont(config.FONT_FAMILY)
            font.setPixelSize(max(9, round(size * s)))
            font.setBold(True)  # measured bold: a slight overestimate keeps a safety margin
            return QFontMetrics(font).horizontalAdvance(text)

        needed = text_width("●", 30) + text_width(self._name, 30) + text_width(self._optimum_text, 24)
        needed += (3 * 10 + 18 + 14 + 20) * s + 2 * border  # spacings, margins, minimum gap
        if self._new_badge.isVisibleTo(self):
            needed += text_width("NOUVEAU", 13) + 14 * s
        return min(1.0, self.width() / needed)

    # ------------------------------------------------------------------ highlight

    def _start_highlight(self, remaining: float) -> None:
        """Border glowing in the scent's color, fading back to normal; "NOUVEAU" badge meanwhile."""
        self._new_badge.setVisible(remaining > 0)
        if remaining <= 0:
            return
        self._glow = QVariantAnimation(self)
        self._glow.setStartValue(remaining)
        self._glow.setEndValue(0.0)
        self._glow.setDuration(int(remaining * config.HIGHLIGHT_SECONDS * 1000))
        self._glow.setEasingCurve(QEasingCurve.Type.InQuad)
        self._glow.valueChanged.connect(self._on_glow)
        self._glow.finished.connect(self._end_highlight)
        self._glow.start()

    def _end_highlight(self) -> None:
        self._new_badge.hide()
        self._apply_style()  # the title row may grow back now that the badge is gone

    def _on_glow(self, value: float) -> None:
        self._border_color = blend(config.COLOR_CARD_BORDER, self._color, value)
        self.update()  # repaints the border only

    def paintEvent(self, event) -> None:
        """Rounded card background and border (drawn by hand: cheaper to animate than a style sheet)."""
        s = self._scale()
        border = max(2.0, 3 * s)
        painter = QPainter(self)
        painter.setRenderHint(QPainter.RenderHint.Antialiasing)
        painter.setPen(QPen(QColor(self._border_color), border))
        painter.setBrush(QColor(config.COLOR_CARD))
        inner = QRectF(self.rect()).adjusted(border / 2, border / 2, -border / 2, -border / 2)
        painter.drawRoundedRect(inner, 18 * s, 18 * s)
