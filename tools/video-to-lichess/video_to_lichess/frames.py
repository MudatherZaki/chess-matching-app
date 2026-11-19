"""Sample frames from a video and keep only the moments when the board is still.

A move shows up as a burst of motion (a hand over the board) between two still
periods. We read the position once per still period.
"""
from __future__ import annotations

from dataclasses import dataclass
from typing import Iterable, Iterator

import cv2
import numpy as np

from .board import warp


@dataclass
class StillFrame:
    time_s: float
    board: np.ndarray  # warped, top-down BGR image


def sample_frames(path: str, fps: float = 4.0) -> Iterator[tuple[float, np.ndarray]]:
    cap = cv2.VideoCapture(path)
    if not cap.isOpened():
        raise IOError(f"could not open video: {path}")
    native = cap.get(cv2.CAP_PROP_FPS) or 30.0
    step = max(1, int(round(native / fps)))
    index = 0
    try:
        while True:
            ok = cap.grab()
            if not ok:
                break
            if index % step == 0:
                ok, frame = cap.retrieve()
                if not ok:
                    break
                yield index / native, frame
            index += 1
    finally:
        cap.release()


def _motion(a: np.ndarray, b: np.ndarray) -> float:
    ga = cv2.GaussianBlur(cv2.cvtColor(a, cv2.COLOR_BGR2GRAY), (5, 5), 0)
    gb = cv2.GaussianBlur(cv2.cvtColor(b, cv2.COLOR_BGR2GRAY), (5, 5), 0)
    return float(np.mean(cv2.absdiff(ga, gb)))


def still_frames(
    frames: Iterable[tuple[float, np.ndarray]],
    matrix: np.ndarray,
    motion_threshold: float = 2.5,
    min_still: int = 3,
) -> list[StillFrame]:
    """Return one representative warped board per still period.

    A still period is at least `min_still` consecutive samples whose frame to frame
    motion is below `motion_threshold` (mean grey level difference, 0 to 255).
    The representative is the per-pixel median of the period, which removes
    short flickers and noise.
    """
    out: list[StillFrame] = []
    run: list[tuple[float, np.ndarray]] = []
    prev = None

    def flush() -> None:
        if len(run) >= min_still:
            stack = np.stack([b for _, b in run])
            out.append(StillFrame(run[len(run) // 2][0], np.median(stack, axis=0).astype(np.uint8)))

    for t, frame in frames:
        board = warp(frame, matrix)
        if prev is not None and _motion(prev, board) > motion_threshold:
            flush()
            run = []
        run.append((t, board))
        prev = board
    flush()
    return out
