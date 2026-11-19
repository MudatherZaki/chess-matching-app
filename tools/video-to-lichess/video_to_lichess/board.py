"""Board geometry: warp a camera frame to a top-down board and cut it into squares.

The warped image always has a8 in the top-left corner and h1 in the bottom-right,
whatever side of the frame White sat on.
"""
from __future__ import annotations

import cv2
import numpy as np

WARP_SIZE = 640  # pixels, so each square is 80x80
SQUARE = WARP_SIZE // 8


def parse_corners(text: str) -> np.ndarray:
    """Parse "x1,y1;x2,y2;x3,y3;x4,y4" into a 4x2 float array.

    The order is the board's a8, h8, h1, a1 corners as they appear in the frame.
    """
    points = [tuple(float(v) for v in p.split(",")) for p in text.split(";") if p.strip()]
    if len(points) != 4 or any(len(p) != 2 for p in points):
        raise ValueError("expected four corners as 'x,y;x,y;x,y;x,y' (a8, h8, h1, a1)")
    return np.array(points, dtype=np.float32)


def homography(corners: np.ndarray) -> np.ndarray:
    dst = np.array(
        [[0, 0], [WARP_SIZE - 1, 0], [WARP_SIZE - 1, WARP_SIZE - 1], [0, WARP_SIZE - 1]],
        dtype=np.float32,
    )
    return cv2.getPerspectiveTransform(corners.astype(np.float32), dst)


def warp(frame: np.ndarray, matrix: np.ndarray) -> np.ndarray:
    return cv2.warpPerspective(frame, matrix, (WARP_SIZE, WARP_SIZE))


def square_index(file: int, rank: int) -> int:
    """python-chess square index (a1 = 0, h8 = 63)."""
    return rank * 8 + file


def square_patch(board_img: np.ndarray, file: int, rank: int, inset: float = 0.2) -> np.ndarray:
    """Central part of one square in the warped image (rank 7 is the top row)."""
    row = 7 - rank
    pad = int(SQUARE * inset)
    y0, x0 = row * SQUARE + pad, file * SQUARE + pad
    return board_img[y0 : y0 + SQUARE - 2 * pad, x0 : x0 + SQUARE - 2 * pad]


def is_light_square(file: int, rank: int) -> bool:
    return (file + rank) % 2 == 1
