"""Read each square as empty, white piece or black piece.

We never need to know *which* piece stands on a square: starting from a known
position, the colour of every square is enough to pick out the legal move that
was played (see inference.py). Telling three classes apart is far easier than
telling thirteen apart, and it lets us calibrate on the video itself.

The reader is calibrated on the first still frame, where the position is known
(normally the starting position). It learns what an empty light square, an empty
dark square, a white piece on each, and a black piece on each look like in this
video, under this light, with this set. No pre-trained model is needed.

A trained detector (YOLO or similar) can replace this class later, as long as it
returns the same per-square probabilities.
"""
from __future__ import annotations

from dataclasses import dataclass

import chess
import cv2
import numpy as np

from .board import is_light_square, square_index, square_patch

EMPTY, WHITE, BLACK = 0, 1, 2
CLASS_NAMES = ("empty", "white", "black")


def board_classes(board: chess.Board) -> np.ndarray:
    """64-long array of EMPTY / WHITE / BLACK for a python-chess board."""
    out = np.zeros(64, dtype=np.int8)
    for sq, piece in board.piece_map().items():
        out[sq] = WHITE if piece.color == chess.WHITE else BLACK
    return out


def _features(patch: np.ndarray) -> np.ndarray:
    lab = cv2.cvtColor(patch, cv2.COLOR_BGR2LAB).reshape(-1, 3).astype(np.float32)
    grey = cv2.cvtColor(patch, cv2.COLOR_BGR2GRAY)
    edges = cv2.Canny(grey, 60, 160)
    return np.concatenate([lab.mean(0), lab.std(0), [edges.mean() / 2.55]])


def all_features(board_img: np.ndarray) -> np.ndarray:
    feats = np.zeros((64, 7), dtype=np.float32)
    for rank in range(8):
        for file in range(8):
            feats[square_index(file, rank)] = _features(square_patch(board_img, file, rank))
    return feats


@dataclass
class _Gaussian:
    mean: np.ndarray
    var: np.ndarray

    def loglik(self, x: np.ndarray) -> np.ndarray:
        return -0.5 * np.sum((x - self.mean) ** 2 / self.var + np.log(self.var), axis=-1)


class SquareReader:
    """Per-video three-class square classifier (one model per square colour)."""

    def __init__(self, models: dict[tuple[bool, int], _Gaussian], temperature: float = 4.0):
        self.models = models
        self.temperature = temperature

    @classmethod
    def calibrate(
        cls,
        board_img: np.ndarray,
        known: chess.Board,
        prior: "SquareReader | None" = None,
        prior_weight: float = 16.0,
    ) -> "SquareReader":
        """Learn the six square appearances from an image whose position is `known`.

        With a `prior` reader, each class model is a blend of this image and the
        prior, weighted as if the prior were `prior_weight` extra squares. That keeps
        late-game classes with only one or two pieces (and any square the move just
        changed) from swinging the model.
        """
        feats = all_features(board_img)
        labels = board_classes(known)
        light = np.array([is_light_square(s % 8, s // 8) for s in range(64)])
        spread = feats.std(0) + 1e-3
        models = {}
        for is_light in (True, False):
            for cls_id in (EMPTY, WHITE, BLACK):
                rows = feats[(light == is_light) & (labels == cls_id)]
                if prior is not None:
                    pm = prior.models[(is_light, cls_id)]
                    n, w = len(rows), prior_weight
                    mean = (rows.sum(0) + w * pm.mean) / (n + w)
                    second = ((rows**2).sum(0) + w * (pm.var + pm.mean**2)) / (n + w)
                    var = np.maximum(second - mean**2, (0.15 * spread) ** 2)
                    models[(is_light, cls_id)] = _Gaussian(mean, var)
                    continue
                if len(rows) == 0:
                    raise ValueError(
                        f"calibration position has no {CLASS_NAMES[cls_id]} on a "
                        f"{'light' if is_light else 'dark'} square"
                    )
                # Variance floor: a share of the spread across the whole board, so a
                # class seen on few squares does not get an overconfident model.
                var = np.maximum(rows.var(0), (0.15 * spread) ** 2)
                models[(is_light, cls_id)] = _Gaussian(rows.mean(0), var)
        return cls(models)

    def fit_score(self, board_img: np.ndarray, known: chess.Board) -> float:
        """Mean log-probability of the known labels (used to find the board's orientation)."""
        probs = self.read(board_img)
        labels = board_classes(known)
        return float(np.mean(np.log(probs[np.arange(64), labels])))

    def white_is_lighter(self) -> bool:
        lw = np.mean([self.models[(l, WHITE)].mean[0] for l in (True, False)])
        lb = np.mean([self.models[(l, BLACK)].mean[0] for l in (True, False)])
        return lw > lb

    def read(self, board_img: np.ndarray) -> np.ndarray:
        """64x3 array of probabilities for EMPTY, WHITE, BLACK."""
        feats = all_features(board_img)
        out = np.zeros((64, 3), dtype=np.float64)
        for sq in range(64):
            is_light = is_light_square(sq % 8, sq // 8)
            ll = np.array([self.models[(is_light, c)].loglik(feats[sq]) for c in (EMPTY, WHITE, BLACK)])
            ll = ll / self.temperature
            p = np.exp(ll - ll.max())
            p /= p.sum()
            out[sq] = p
        # Never fully certain: leaves room for the move search to overrule a misread.
        out = np.clip(out, 0.01, 0.98)
        return out / out.sum(1, keepdims=True)


def orient_and_calibrate(board_img: np.ndarray, known: chess.Board) -> tuple[int, SquareReader]:
    """Try the four rotations of the warped board; return (k, reader) for np.rot90(img, k).

    A wrong quarter turn puts pieces on the wrong squares and fits badly. A half turn
    fits just as well with the colours swapped, so between those two we pick the one
    where the "white" pieces really are the lighter ones.
    """
    scored = []
    for k in range(4):
        img = np.rot90(board_img, k).copy()
        reader = SquareReader.calibrate(img, known)
        scored.append((reader.fit_score(img, known), k, reader))
    scored.sort(key=lambda s: s[0], reverse=True)
    best = scored[0]
    pair = [s for s in scored if (s[1] - best[1]) % 2 == 0]
    for _, k, reader in pair:
        if reader.white_is_lighter():
            return k, reader
    return best[1], best[2]
