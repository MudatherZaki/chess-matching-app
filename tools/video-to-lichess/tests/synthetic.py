"""Render a fake over-the-board game video, for end-to-end tests.

The board is drawn top-down with simple pieces, then warped into a tilted camera
view. During each move a "hand" blob sweeps over the board, so the pipeline has to
find the still periods just as it would in a real video.
"""
from __future__ import annotations

import chess
import cv2
import numpy as np

SIZE = 640
SQ = SIZE // 8
LIGHT, DARK = (181, 217, 240), (99, 136, 181)  # BGR wood colours
FRAME_W, FRAME_H = 960, 720
# Where the board's a8, h8, h1, a1 corners land in the camera frame (a tilted view).
CORNERS = np.array([[250, 120], [720, 110], [850, 640], [120, 650]], dtype=np.float32)


def draw_board(board: chess.Board, rng: np.random.Generator) -> np.ndarray:
    img = np.zeros((SIZE, SIZE, 3), np.uint8)
    for rank in range(8):
        for file in range(8):
            y, x = (7 - rank) * SQ, file * SQ
            img[y : y + SQ, x : x + SQ] = LIGHT if (file + rank) % 2 else DARK
    for sq, piece in board.piece_map().items():
        cx = chess.square_file(sq) * SQ + SQ // 2 + int(rng.integers(-4, 5))
        cy = (7 - chess.square_rank(sq)) * SQ + SQ // 2 + int(rng.integers(-4, 5))
        body, rim = ((235, 235, 230), (60, 60, 60)) if piece.color else ((35, 30, 30), (90, 90, 90))
        r = 22 + piece.piece_type  # bigger pieces for higher types
        cv2.circle(img, (cx, cy), r, body, -1, cv2.LINE_AA)
        cv2.circle(img, (cx, cy), r, rim, 2, cv2.LINE_AA)
        cv2.putText(img, piece.symbol().upper(), (cx - 8, cy + 8), cv2.FONT_HERSHEY_SIMPLEX, 0.7, rim, 2)
    return img


def camera_view(top: np.ndarray, rng: np.random.Generator) -> np.ndarray:
    src = np.array([[0, 0], [SIZE - 1, 0], [SIZE - 1, SIZE - 1], [0, SIZE - 1]], np.float32)
    m = cv2.getPerspectiveTransform(src, CORNERS)
    frame = np.full((FRAME_H, FRAME_W, 3), (70, 80, 90), np.uint8)
    frame = cv2.warpPerspective(top, m, (FRAME_W, FRAME_H), dst=frame, borderMode=cv2.BORDER_TRANSPARENT)
    noise = rng.normal(0, 3, frame.shape)
    return np.clip(frame + noise, 0, 255).astype(np.uint8)


def add_hand(frame: np.ndarray, t: float) -> np.ndarray:
    out = frame.copy()
    x = int(100 + t * 700)
    y = int(650 - 300 * np.sin(np.pi * t))
    cv2.ellipse(out, (x, y), (90, 60), 30, 0, 360, (120, 160, 210), -1)  # skin-ish blob
    cv2.rectangle(out, (x - 40, y + 30), (x + 40, FRAME_H), (120, 160, 210), -1)  # arm
    return out


def render_game(moves: list[str], path: str, fps: int = 12, still_s: float = 1.2, move_s: float = 0.8, seed: int = 0) -> chess.Board:
    rng = np.random.default_rng(seed)
    board = chess.Board()
    writer = cv2.VideoWriter(path, cv2.VideoWriter_fourcc(*"mp4v"), fps, (FRAME_W, FRAME_H))

    def still() -> None:
        frame = camera_view(draw_board(board, rng), rng)
        for _ in range(int(still_s * fps)):
            writer.write(np.clip(frame + rng.normal(0, 1.5, frame.shape), 0, 255).astype(np.uint8))

    still()
    for san in moves:
        before = camera_view(draw_board(board, rng), rng)
        board.push_san(san)
        after = camera_view(draw_board(board, rng), rng)
        n = int(move_s * fps)
        for i in range(n):
            writer.write(add_hand(before if i < n // 2 else after, i / max(1, n - 1)))
        still()
    writer.release()
    return board


# Morphy's "Opera Game" (1858): castling, captures, a queen sacrifice and mate.
OPERA_GAME = (
    "e4 e5 Nf3 d6 d4 Bg4 dxe5 Bxf3 Qxf3 dxe5 Bc4 Nf6 Qb3 Qe7 Nc3 c6 Bg5 b5 "
    "Nxb5 cxb5 Bxb5+ Nbd7 O-O-O Rd8 Rxd7 Rxd7 Rd1 Qe6 Bxd7+ Nxd7 Qb8+ Nxb8 Rd8#"
).split()
