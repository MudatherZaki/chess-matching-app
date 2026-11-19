"""Video in, moves out."""
from __future__ import annotations

from dataclasses import dataclass

import chess
import numpy as np

from .board import homography
from .frames import sample_frames, still_frames
from .inference import MoveSearch, Settings
from .squares import SquareReader, orient_and_calibrate


@dataclass
class Result:
    moves: list[chess.Move]
    still_periods: int
    rotation: int


def read_board_images(
    images: list[np.ndarray],
    start: chess.Board | None = None,
    auto_orient: bool = True,
    settings: Settings | None = None,
    recalibrate: bool = True,
    prior_weight: float = 16.0,
) -> Result:
    """Moves from a list of warped, top-down board images (the first shows `start`)."""
    if not images:
        raise ValueError("no still frames found")
    start = start or chess.Board()
    if auto_orient:
        k, reader = orient_and_calibrate(images[0], start)
    else:
        k, reader = 0, SquareReader.calibrate(images[0], start)
    search = MoveSearch(start, settings)
    for img in images[1:]:
        img = np.rot90(img, k).copy()
        if recalibrate:
            # Re-learn what the squares look like on this image, labelled with the
            # position we believe was on the board just before it. At most four
            # squares change per move, so the labels are nearly all right, and the
            # reader keeps up with changing light or a moved camera.
            # Blending with the last reader covers classes with few or no squares left.
            reader = SquareReader.calibrate(img, search.best_board, prior=reader, prior_weight=prior_weight)
        search.step(reader.read(img))
    return Result(search.moves(), len(images), k)


def read_video(
    path: str,
    corners: np.ndarray,
    fps: float = 4.0,
    motion_threshold: float = 2.5,
    start: chess.Board | None = None,
    settings: Settings | None = None,
) -> Result:
    matrix = homography(corners)
    stills = still_frames(sample_frames(path, fps), matrix, motion_threshold)
    return read_board_images([s.board for s in stills], start, True, settings)
