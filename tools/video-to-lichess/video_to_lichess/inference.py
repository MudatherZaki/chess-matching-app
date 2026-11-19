"""Turn a sequence of noisy square readings into the moves that were played.

Each reading is a 64x3 array: for every square, how likely it is to be empty,
hold a white piece, or hold a black piece. We keep a small set of candidate games
(a beam). For each new reading we extend every candidate by zero, one or two legal
moves and score how well the resulting position explains the reading. Legal-move
rules do most of the error correcting: a misread square rarely matches any legal
move, so it gets outvoted.

Two moves per step covers a still period we missed (for example a quick reply
while the other player's hand was still in view). Readings that match nothing
well (a hand resting on the board, a bumped piece) can be skipped at a cost.
"""
from __future__ import annotations

from dataclasses import dataclass, field

import chess
import numpy as np

from .squares import BLACK, EMPTY, WHITE


@dataclass
class Settings:
    beam: int = 24
    move_cost: float = 2.0          # nats per move, prefers fewer moves when the reading is ambiguous
    skip_cost: float = 25.0         # nats to ignore a reading entirely
    max_moves_per_step: int = 2
    deep_beam: int = 6              # how many top candidates may jump two moves at once


@dataclass(order=True)
class _Candidate:
    cost: float
    board: chess.Board = field(compare=False)


class _CostTable:
    """Cost of a position under one reading, computed from the piece bitboards."""

    def __init__(self, log_probs: np.ndarray):
        self.base = float(-log_probs[:, EMPTY].sum())
        self.white = (log_probs[:, EMPTY] - log_probs[:, WHITE]).tolist()
        self.black = (log_probs[:, EMPTY] - log_probs[:, BLACK]).tolist()

    def __call__(self, board: chess.Board) -> float:
        cost = self.base
        for sq in chess.scan_forward(board.occupied_co[chess.WHITE]):
            cost += self.white[sq]
        for sq in chess.scan_forward(board.occupied_co[chess.BLACK]):
            cost += self.black[sq]
        return cost


class MoveSearch:
    """Beam search over legal moves, fed one reading at a time."""

    def __init__(self, start: chess.Board | None = None, settings: Settings | None = None):
        self.s = settings or Settings()
        self.start = start.copy() if start is not None else chess.Board()
        self.beam = [_Candidate(0.0, self.start)]

    @property
    def best_board(self) -> chess.Board:
        return self.beam[0].board

    def moves(self) -> list[chess.Move]:
        return list(self.best_board.move_stack[len(self.start.move_stack):])

    def step(self, probs: np.ndarray) -> None:
        s = self.s
        table = _CostTable(np.log(probs))
        extended: dict[tuple, _Candidate] = {}

        def offer(cost: float, board: chess.Board) -> None:
            # Merge candidates that reach the same position; keep the cheaper path.
            key = board._transposition_key()
            if key not in extended or cost < extended[key].cost:
                extended[key] = _Candidate(cost, board.copy(stack=True))

        def expand(board: chess.Board, base_cost: float, depth: int, max_depth: int) -> None:
            offer(base_cost + depth * s.move_cost + table(board), board)
            if depth == max_depth:
                return
            for move in list(board.legal_moves):
                if move.promotion not in (None, chess.QUEEN):
                    continue  # colours can't tell promotion pieces apart; assume a queen
                board.push(move)
                expand(board, base_cost, depth + 1, max_depth)
                board.pop()

        for rank, cand in enumerate(self.beam):
            # Ignore this reading altogether.
            offer(cand.cost + s.skip_cost, cand.board)
            # Two-move jumps only from the best candidates, to keep the search fast.
            max_depth = s.max_moves_per_step if rank < s.deep_beam else min(1, s.max_moves_per_step)
            expand(cand.board.copy(stack=True), cand.cost, 0, max_depth)

        self.beam = sorted(extended.values())[: s.beam]


def infer_moves(
    readings: list[np.ndarray],
    start: chess.Board | None = None,
    settings: Settings | None = None,
) -> list[chess.Move]:
    """Most likely move list from the start position through all readings."""
    search = MoveSearch(start, settings)
    for probs in readings:
        search.step(probs)
    return search.moves()
