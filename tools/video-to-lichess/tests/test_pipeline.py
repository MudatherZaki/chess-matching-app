import os
import sys

import chess
import numpy as np
import pytest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from synthetic import CORNERS, OPERA_GAME, render_game  # noqa: E402
from video_to_lichess.inference import infer_moves  # noqa: E402
from video_to_lichess.lichess import analysis_url, build_pgn  # noqa: E402
from video_to_lichess.pipeline import read_video  # noqa: E402
from video_to_lichess.squares import board_classes  # noqa: E402


def sans(moves):
    board, out = chess.Board(), []
    for m in moves:
        out.append(board.san(m))
        board.push(m)
    return out


def noisy_reading(board, rng, flip=0.0, conf=0.9):
    """Simulated square reader output, with a share of squares misread."""
    classes = board_classes(board)
    probs = np.full((64, 3), (1 - conf) / 2)
    for sq in range(64):
        c = classes[sq]
        if rng.random() < flip:
            c = (c + rng.integers(1, 3)) % 3
        probs[sq, c] = conf
    return probs


def readings_for(game, rng, **kw):
    board, out = chess.Board(), []
    for san in game:
        board.push_san(san)
        out.append(noisy_reading(board, rng, **kw))
    return out


def test_clean_readings_give_the_game():
    rng = np.random.default_rng(1)
    assert sans(infer_moves(readings_for(OPERA_GAME, rng))) == OPERA_GAME


def test_misread_squares_are_corrected_by_legal_moves():
    rng = np.random.default_rng(2)
    assert sans(infer_moves(readings_for(OPERA_GAME, rng, flip=0.04))) == OPERA_GAME


def test_missed_still_periods_and_duplicates():
    rng = np.random.default_rng(3)
    readings = readings_for(OPERA_GAME, rng)
    # Drop every 5th reading (a quick reply never seen still) and repeat some others.
    thinned = []
    for i, r in enumerate(readings):
        if i % 5 == 4 and i != len(readings) - 1:
            continue
        thinned.append(r)
        if i % 7 == 0:
            thinned.append(r)
    assert sans(infer_moves(thinned)) == OPERA_GAME


def test_garbage_reading_is_skipped():
    rng = np.random.default_rng(4)
    readings = readings_for(OPERA_GAME, rng)
    readings.insert(10, np.full((64, 3), 1 / 3) * 0 + rng.dirichlet([1, 1, 1], 64))
    assert sans(infer_moves(readings)) == OPERA_GAME


def test_castling_and_en_passant():
    game = "e4 Nf6 e5 d5 exd6 exd6 Nf3 Be7 Bc4 O-O O-O".split()
    rng = np.random.default_rng(5)
    assert sans(infer_moves(readings_for(game, rng))) == game


def test_pgn_has_no_players_or_result():
    board = chess.Board()
    moves = [board.push_san(s) for s in ["e4", "e5"]]
    pgn = build_pgn(moves)
    assert '[White "?"]' in pgn and '[Black "?"]' in pgn and '[Result "*"]' in pgn
    assert pgn.strip().endswith("1. e4 e5 *")


def test_analysis_url():
    board = chess.Board()
    moves = [board.push_san(s) for s in ["e4", "e5", "Qh5", "Nc6", "Bc4", "Nf6", "Qxf7#"]]
    assert analysis_url(moves) == "https://lichess.org/analysis/pgn/e4_e5_Qh5_Nc6_Bc4_Nf6_Qxf7%23"


@pytest.mark.parametrize("rotate", [False, True])
def test_end_to_end_synthetic_video(tmp_path, rotate):
    path = str(tmp_path / "game.mp4")
    render_game(OPERA_GAME, path)
    corners = CORNERS.copy()
    if rotate:
        # Give the corners starting from a different board corner: orientation must be detected.
        corners = np.roll(corners, 2, axis=0)
    result = read_video(path, corners)
    assert sans(result.moves) == OPERA_GAME
