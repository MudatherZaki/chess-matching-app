"""Measure accuracy on real games from the ChessReD dataset (TU Delft, CC BY-NC-SA 4.0).

ChessReD photographs a physical board after every move of 100 real games, with the
true position for each photo. The ChessReD2K subset also has the board corners,
which we use instead of corner detection. A photo per move is what the video
pipeline's still-period step produces, so this tests everything after that step.

Usage:
    python eval/chessred_eval.py DATA_DIR [--games 0,6,19]

DATA_DIR holds annotations.json and the unzipped chessred2k/ folder from
https://data.4tu.nl/datasets/99b5c721-280b-450b-b058-b2900b69a90f
The dataset is for non-commercial use, so it is only used for testing, never shipped.
"""
from __future__ import annotations

import argparse
import collections
import json
import os
import sys

import chess
import cv2
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from video_to_lichess.board import homography, warp  # noqa: E402
from video_to_lichess.pipeline import read_board_images  # noqa: E402
from video_to_lichess.squares import SquareReader, board_classes  # noqa: E402

CATEGORY_TO_PIECE = {}


def load(data_dir: str):
    ann = json.load(open(os.path.join(data_dir, "annotations.json")))
    for c in ann["categories"]:
        if c["name"] != "empty":
            colour, kind = c["name"].split("-")
            CATEGORY_TO_PIECE[c["id"]] = chess.Piece(
                chess.PIECE_NAMES.index(kind), colour == "white"
            )
    images = {i["id"]: i for i in ann["images"]}
    corners = {c["image_id"]: c["corners"] for c in ann["annotations"]["corners"]}
    pieces = collections.defaultdict(list)
    for p in ann["annotations"]["pieces"]:
        pieces[p["image_id"]].append(p)
    games = collections.defaultdict(list)
    for img_id in corners:
        games[images[img_id]["game_id"]].append(images[img_id])
    for g in games.values():
        g.sort(key=lambda i: i["move_id"])
    return images, corners, pieces, games


def true_board(piece_anns) -> chess.Board:
    board = chess.Board(None)
    for p in piece_anns:
        if p["category_id"] in CATEGORY_TO_PIECE:
            board.set_piece_at(chess.parse_square(p["chessboard_position"]), CATEGORY_TO_PIECE[p["category_id"]])
    return board


def true_moves(boards: list[chess.Board]) -> list[chess.Move] | None:
    """Recover the move list from consecutive true positions."""
    game = chess.Board()
    if game.board_fen() != boards[0].board_fen():
        return None
    moves = []
    for nxt in boards[1:]:
        if nxt.board_fen() == game.board_fen():
            continue
        found = [m for m in game.legal_moves if _after(game, m) == nxt.board_fen()]
        if not found:
            return None
        moves.append(found[0])
        game.push(found[0])
    return moves


def _after(board: chess.Board, move: chess.Move) -> str:
    board.push(move)
    fen = board.board_fen()
    board.pop()
    return fen


def warped(data_dir: str, image, corner) -> np.ndarray:
    path = os.path.join(data_dir, "chessred2k", image["path"])
    if not os.path.exists(path):
        path = os.path.join(data_dir, image["path"])
    frame = cv2.imread(path)
    pts = np.array(
        [corner["top_left"], corner["top_right"], corner["bottom_right"], corner["bottom_left"]],
        dtype=np.float32,
    )
    return warp(frame, homography(pts))


def evaluate_game(data_dir, game_imgs, corners, pieces, recalibrate=True):
    boards = [true_board(pieces[i["id"]]) for i in game_imgs]
    truth = true_moves(boards)
    if truth is None:
        return None
    imgs = [warped(data_dir, i, corners[i["id"]]) for i in game_imgs]
    reader = SquareReader.calibrate(imgs[0], chess.Board())
    readings = [reader.read(img) for img in imgs[1:]]
    square_acc = np.mean(
        [np.mean(r.argmax(1) == board_classes(b)) for r, b in zip(readings, boards[1:])]
    )
    moves = read_board_images(imgs, auto_orient=False, recalibrate=recalibrate).moves
    prefix = 0
    for a, b in zip(moves, truth):
        if a != b:
            break
        prefix += 1
    return {
        "half_moves": len(truth),
        "found": len(moves),
        "correct_prefix": prefix,
        "exact": moves == truth,
        "square_acc": float(square_acc),
        "camera": game_imgs[0]["camera"],
    }


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("data_dir")
    ap.add_argument("--no-recalibrate", action="store_true", help="calibrate on the first image only")
    ap.add_argument("--games", help="comma-separated game ids (default: all with corners)")
    args = ap.parse_args()
    _, corners, pieces, games = load(args.data_dir)
    ids = [int(g) for g in args.games.split(",")] if args.games else sorted(games)
    for gid in ids:
        r = evaluate_game(args.data_dir, games[gid], corners, pieces, not args.no_recalibrate)
        if r is None:
            print(f"game {gid}: skipped (annotations don't form a legal game from the start)")
            continue
        print(
            f"game {gid:3d} [{r['camera']}]: squares read right by the first-image reader {r['square_acc']:.1%}, "
            f"moves right until move {r['correct_prefix']}/{r['half_moves']}"
            f"{' (exact)' if r['exact'] else ''}",
            flush=True,
        )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
