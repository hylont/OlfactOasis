import argparse
import csv
import os
import sys

import matplotlib.pyplot as plt

PLEASANTNESS_SIGN = {"Negative": -1, "NeutralUndecided": 0, "Positive": 1}


def parse_args():
    parser = argparse.ArgumentParser(
        description="Plot pleasantness (agreableness) as a function of intensity for one odor, "
                    "from a ScentEvaluation CSV exported by the OlfactOasis Unity project."
    )
    parser.add_argument("csv_path", help="Path to the <ID>.csv file exported by Unity.")
    parser.add_argument("scent_name", help="Name of the odor to plot (must match a ScentName value in the CSV).")
    return parser.parse_args()


def load_rows(csv_path, scent_name):
    with open(csv_path, newline="", encoding="utf-8") as csv_file:
        return [
            row for row in csv.DictReader(csv_file)
            if row["ScentName"].strip().lower() == scent_name.strip().lower()
        ]


def pleasantness_score(row):
    sign = PLEASANTNESS_SIGN.get(row["WasPleasant"], 0)
    magnitude = float(row["ResponseMagnitude"])
    # The drawn response curve (magnitude) refines how pleasant/unpleasant the reaction was;
    # fall back to the plain +1/0/-1 answer when no curve was drawn (e.g. neutral answers).
    return sign * magnitude if magnitude > 0 else float(sign)


def main():
    args = parse_args()
    scent_name = args.scent_name.strip()

    if not os.path.isfile(args.csv_path):
        sys.exit(f"CSV file not found: {args.csv_path}")

    rows = load_rows(args.csv_path, scent_name)
    if not rows:
        sys.exit(f"No evaluation found for odor '{scent_name}' in {args.csv_path}")

    points = sorted(
        ((float(row["Strength"]), pleasantness_score(row)) for row in rows),
        key=lambda point: point[0],
    )
    intensities, scores = zip(*points)

    fig, ax = plt.subplots()
    ax.plot(intensities, scores, marker="o")
    ax.axhline(0, color="gray", linewidth=0.8, linestyle="--")
    ax.set_xlim(0, 1)
    ax.set_xlabel("Intensité")
    ax.set_ylabel("Agréabilité")
    ax.set_title(f"Agréabilité en fonction de l'intensité - {scent_name}")
    fig.tight_layout()

    output_path = f"{os.path.splitext(args.csv_path)[0]}_{scent_name}.png"
    fig.savefig(output_path)
    print(f"Graph saved to {output_path}")

    plt.show()


if __name__ == "__main__":
    main()
