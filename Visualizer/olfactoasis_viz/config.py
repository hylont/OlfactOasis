"""Shared settings of the visualizer: where the results live, how they look.

Everything a demo operator may want to tweak is gathered here, so the rest of the
code never contains a magic value.
"""

from pathlib import Path

# --------------------------------------------------------------------------- #
# Files
# --------------------------------------------------------------------------- #

VISUALIZER_DIR = Path(__file__).resolve().parent.parent

# Local copy of every result pulled from the headset. It survives restarts, so the
# results stay visible even after the headset has been unplugged.
DATA_DIR = VISUALIZER_DIR / "Data"

EXAMPLES_DIR = VISUALIZER_DIR / "Examples"

# --------------------------------------------------------------------------- #
# Headset (Meta Quest, seen by the PC through adb)
# --------------------------------------------------------------------------- #

# Android "applicationIdentifier" of the Unity build (Project Settings > Player).
ANDROID_PACKAGE = "com.Lohan.OlfactOasisExperience"

# Unity's Application.persistentDataPath on Android, followed by the folder written by
# ScentCalibrationResults.cs. "{package}" is replaced by ANDROID_PACKAGE.
REMOTE_RESULTS_DIR = "/sdcard/Android/data/{package}/files/ScentCalibrationResults"

# How often (seconds) the headset is looked for, and its results compared with ours.
HEADSET_POLL_SECONDS = 2.0

# How often (seconds) the local result folders are scanned for new or modified files.
FOLDER_SCAN_SECONDS = 1.0

# --------------------------------------------------------------------------- #
# Scents: display name and color
# --------------------------------------------------------------------------- #

# Keys are the names written in the JSON (EScentName in upper snake case).
# Colors evoke the scent itself, and are bright enough to stand out on the dark
# background from the back of the room. A scent is never identified by its color
# alone: every card also writes its name in large letters.
SCENTS = {
    "VANILLA":        ("Vanille",       "#f2d27e"),
    "ROSE":           ("Rose",          "#f585c4"),
    "BUTANOL":        ("Butanol",       "#62a8ff"),
    "DARK_CHOCOLATE": ("Chocolat noir", "#c4834f"),
    "LAVENDER":       ("Lavande",       "#a78bfa"),
    "STRAWBERRY":     ("Fraise",        "#f0505e"),
    "ORANGE":         ("Orange",        "#ff9a3c"),
    "MINT":           ("Menthe",        "#3fd6a0"),
}
UNKNOWN_SCENT = ("Odeur inconnue", "#c3c2b7")


def scent_style(json_name: str) -> tuple[str, str]:
    """Returns (display name, hex color) for a scent name read from the JSON."""
    return SCENTS.get(json_name, UNKNOWN_SCENT)


# --------------------------------------------------------------------------- #
# Theme
# --------------------------------------------------------------------------- #

COLOR_BACKGROUND = "#101216"
COLOR_CARD = "#1b1e25"
COLOR_CARD_BORDER = "#2a2e38"
COLOR_TEXT = "#ffffff"
COLOR_TEXT_SECONDARY = "#c3c2b7"
COLOR_TEXT_MUTED = "#8a8d96"
COLOR_GRID = "#3a3f4b"
COLOR_PLEASANT_ZONE = "#3fd6a0"    # faint tint of the "pleasant" half of each plot
COLOR_UNPLEASANT_ZONE = "#f0505e"  # faint tint of the "unpleasant" half of each plot
COLOR_STATUS_OK = "#3fd6a0"
COLOR_STATUS_WARNING = "#f2b84b"
COLOR_STATUS_OFF = "#8a8d96"

FONT_FAMILY = "Segoe UI"

# --------------------------------------------------------------------------- #
# Grid
# --------------------------------------------------------------------------- #

# Number of results shown at once. Few large cards read better from afar; the
# operator can change it live with the + and - keys.
DEFAULT_CARDS_PER_PAGE = 6
MIN_CARDS_PER_PAGE = 1
MAX_CARDS_PER_PAGE = 16

# Width / height ratio a card tries to keep when the grid picks its columns.
CARD_ASPECT_RATIO = 4 / 3

# Newly arrived results glow for this long (seconds).
HIGHLIGHT_SECONDS = 10.0

# Only results written by the headset less than this long ago (minutes) are
# highlighted: plugging in a headset holding days of results does not light up
# every card. The sync keeps each file's date as written by the headset.
HIGHLIGHT_MAX_AGE_MINUTES = 30
