"""Build a PGN and show it on Lichess.

Two ways to show a game:

* analysis_url(): a lichess.org/analysis link with the moves in the URL. Nothing is
  uploaded or stored anywhere; Lichess just opens the analysis board.
* import_game(): POST /api/import, which creates an imported game on Lichess and
  returns its page (with server-side analysis available). That game is public.

The PGN carries no player names, ratings or result, in line with the app's
"no game tracking" rule.
"""
from __future__ import annotations

import json
import os
import urllib.parse
import urllib.request

import chess
import chess.pgn

LICHESS = "https://lichess.org"


def build_pgn(moves: list[chess.Move], start: chess.Board | None = None, event: str = "Over-the-board game") -> str:
    start = start or chess.Board()
    game = chess.pgn.Game()
    game.headers["Event"] = event
    for key in ("Site", "Date", "Round", "White", "Black"):
        game.headers[key] = "?"
    game.headers["Result"] = "*"
    if start.fen() != chess.STARTING_FEN:
        game.setup(start)
    node = game
    for move in moves:
        node = node.add_variation(move)
    return str(game.accept(chess.pgn.StringExporter(headers=True, variations=False, comments=False)))


def analysis_url(moves: list[chess.Move]) -> str:
    """lichess.org/analysis link that replays the moves from the starting position.

    Nothing is uploaded or stored; Lichess reads the moves from the URL
    (underscores stand for spaces).
    """
    board = chess.Board()
    sans = []
    for move in moves:
        sans.append(board.san(move))
        board.push(move)
    return f"{LICHESS}/analysis/pgn/{urllib.parse.quote('_'.join(sans), safe='_')}"


def import_game(pgn: str, token: str | None = None, timeout: float = 30.0) -> str:
    """Import the PGN on Lichess and return the game's URL.

    Works without a token (rate limited). With LICHESS_TOKEN set, the game is
    imported under that account.
    """
    token = token or os.environ.get("LICHESS_TOKEN")
    data = urllib.parse.urlencode({"pgn": pgn}).encode()
    req = urllib.request.Request(f"{LICHESS}/api/import", data=data, method="POST")
    req.add_header("Accept", "application/json")
    if token:
        req.add_header("Authorization", f"Bearer {token}")
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        body = json.loads(resp.read().decode())
    return body["url"]
