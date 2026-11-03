#!/usr/bin/env python3
"""Regenerate the calibration reference pattern with wider cross arms.

The ring layout of the original 2400x2400 pattern is kept unchanged. Only the
two full-length arms (rows and columns centred on 1199.5) are widened, from
10 px to ARM_WIDTH px, so the radial lines survive on dye discs where a 0.5 mm
line is too faint to see.

Usage: python tools/make_wide_calibration_target.py [--arm-width 40]
Prints the new SHA-256 values to paste into CalibrationReferencePattern.cs.
"""
import argparse
import gzip
import hashlib
import io
from pathlib import Path

import numpy as np
from PIL import Image

PATTERNS = Path(__file__).resolve().parent.parent / "src/DiscImageStudio.Core/Calibration/Patterns"
SIZE = 2400
CENTRE = (SIZE - 1) / 2.0  # 1199.5


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--arm-width", type=int, default=40)
    width = parser.parse_args().arm_width
    if width <= 0 or width % 2:
        raise SystemExit("arm width must be a positive even number so the arms stay centred on 1199.5")

    gray = np.array(Image.open(PATTERNS / "target_slim.png").convert("L"), dtype=np.uint8)
    if gray.shape != (SIZE, SIZE):
        raise SystemExit("unexpected source size")
    lo = int(CENTRE - width / 2 + 0.5)
    hi = lo + width
    gray[lo:hi, :] = 0
    gray[:, lo:hi] = 0

    buffer = io.BytesIO()
    Image.fromarray(gray).convert("RGB").save(buffer, format="PNG", optimize=True)
    png = buffer.getvalue()
    (PATTERNS / "target_slim.png").write_bytes(png)
    with open(PATTERNS / "target_slim.gray.gz", "wb") as raw:
        with gzip.GzipFile(filename="", mode="wb", fileobj=raw, compresslevel=9, mtime=0) as gz:
            gz.write(gray.tobytes())

    print("arm width px :", width, f"(columns/rows {lo}..{hi - 1})")
    print("PngSha256    :", hashlib.sha256(png).hexdigest())
    print("GraySha256   :", hashlib.sha256(gray.tobytes()).hexdigest())


if __name__ == "__main__":
    main()
