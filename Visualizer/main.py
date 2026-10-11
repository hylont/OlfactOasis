"""OlfactOasis Visualizer - live display of the scent calibration results.

Shows, in a grid, how pleasant each scent was rated according to its intensity, for
each participant, with a star on the intensity the system kept as optimal. Results
are pulled automatically from the VR headset as soon as it is plugged into this PC.

Usage examples:
    python main.py                          # live: headset results (and Data/)
    python main.py --fullscreen --screen 1  # full screen on the second monitor
    python main.py --examples               # also show the recorded examples
    python main.py --demo                   # replay the examples as a live session

See README.md for the full documentation.
"""

import argparse
import logging
import sys
from pathlib import Path

import pyqtgraph as pg
from PySide6.QtWidgets import QApplication

from olfactoasis_viz import config
from olfactoasis_viz.demo import DemoReplayer
from olfactoasis_viz.headset import HeadsetSync, find_adb
from olfactoasis_viz.main_window import MainWindow
from olfactoasis_viz.results import ResultsLibrary


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Affiche en direct les résultats de calibration olfactive d'OlfactOasis.")
    parser.add_argument("--fullscreen", action="store_true", help="démarrer en plein écran")
    parser.add_argument("--screen", type=int, default=None,
                        help="numéro de l'écran où ouvrir la fenêtre (0 = principal, 1 = second écran...)")
    parser.add_argument("--folder", type=Path, action="append", default=[],
                        help="dossier de résultats supplémentaire à afficher (option répétable)")
    parser.add_argument("--examples", action="store_true", help="afficher aussi les résultats du dossier Examples")
    parser.add_argument("--demo", type=float, nargs="?", const=4.0, default=None, metavar="SECONDES",
                        help="rejouer les exemples comme une session en direct (une odeur toutes les N secondes, 4 par défaut)")
    parser.add_argument("--no-headset", action="store_true", help="ne pas chercher le casque")
    parser.add_argument("--adb", default=None, help="chemin de adb.exe (trouvé automatiquement sinon)")
    parser.add_argument("--package", default=config.ANDROID_PACKAGE, help="identifiant Android de l'application Unity")
    return parser.parse_args()


def place_window(window: MainWindow, app: QApplication, screen_index: int | None, fullscreen: bool) -> None:
    """Moves the window to the requested monitor, then shows it (full screen or not)."""
    screens = app.screens()
    if screen_index is not None and 0 <= screen_index < len(screens):
        geometry = screens[screen_index].availableGeometry()
        window.move(geometry.topLeft())
        window.resize(geometry.width() * 9 // 10, geometry.height() * 9 // 10)
    elif screen_index is not None:
        logging.warning("Screen %d does not exist (%d screen(s) found).", screen_index, len(screens))

    if fullscreen:
        window.showFullScreen()
    else:
        window.show()


def main() -> int:
    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(name)s: %(message)s")
    args = parse_args()

    app = QApplication(sys.argv)
    pg.setConfigOptions(antialias=True)

    # Which folders to display
    folders = [config.DATA_DIR, *args.folder]
    if args.examples:
        folders.append(config.EXAMPLES_DIR)
    demo = None
    if args.demo is not None:
        # The demo shows only its own replayed results, as if the day had just started.
        demo = DemoReplayer(config.EXAMPLES_DIR, seconds_per_booth=args.demo)
        folders = [demo.output_dir]

    use_headset = not args.no_headset and demo is None
    window = MainWindow(show_headset_status=use_headset)

    library = ResultsLibrary(folders)
    library.results_changed.connect(window.set_results)

    headset = None
    if use_headset:
        headset = HeadsetSync(args.adb or find_adb(), config.DATA_DIR, args.package)
        headset.status_changed.connect(window.set_headset_status)
        headset.start()

    library.scan()
    place_window(window, app, args.screen, args.fullscreen)
    exit_code = app.exec()

    if headset is not None:
        headset.stop()
    return exit_code


if __name__ == "__main__":
    sys.exit(main())
