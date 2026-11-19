# Video to Lichess (prototype)

Turns a video of an over-the-board chess game into its moves, and shows the game
on Lichess. This is an internal prototype to measure accuracy before we build a
backend service around it. It is not wired into the API or the app.

The app does not track games: the PGN has no player names, ratings or result,
nothing is saved, and the default output is a Lichess analysis link that uploads
nothing.

## How it works

1. **Board:** you give the four board corners in the frame (any corner first,
   clockwise). The frame is warped to a top-down board.
2. **Still periods:** frames are sampled (4 per second) and a move shows up as a
   burst of motion. Each still period between bursts gives one image of the board.
3. **Square reading:** every square is read as empty, white piece or black piece.
   The reader calibrates itself on the first still frame, which must show the
   starting position. It also works out which side White is sitting on.
4. **Move inference:** a beam search over legal moves finds the game that best
   explains the readings. Misread squares, missed still periods (two moves at once)
   and junk frames (a hand resting on the board) are tolerated.
5. **Output:** a PGN, a `lichess.org/analysis/pgn/...` link, and optionally an
   import via `POST https://lichess.org/api/import` (that makes the game public).

Piece types are never recognised: from the starting position, the colour on each
square is enough to tell which legal move was played. The one gap is
underpromotion, which is always read as a queen.

## Run it

```bash
cd tools/video-to-lichess
python -m venv .venv && . .venv/bin/activate
pip install -r requirements.txt -r requirements-dev.txt

python -m video_to_lichess game.mp4 --first-frame first.png   # find the corners
python -m video_to_lichess game.mp4 --corners "250,120;720,110;850,640;120,650"
python -m video_to_lichess game.mp4 --corners "..." --import  # also import on Lichess
```

Best results: phone fixed on a stand, all four corners in view, camera as high
above the board as possible, the video starting from the initial position.

## Tests

```bash
python -m pytest -q tests
```

The tests render a synthetic game video (Morphy's Opera Game, with a hand sweeping
over the board on every move) and check the full pipeline reads it back exactly,
from either side of the board. Other tests feed noisy readings into the move search.

## Accuracy on real games

`eval/chessred_eval.py` runs everything after the still-period step on real
photos from [ChessReD](https://data.4tu.nl/datasets/99b5c721-280b-450b-b058-b2900b69a90f)
(TU Delft, CC BY-NC-SA 4.0, used for testing only, never shipped):

```bash
python eval/chessred_eval.py /path/to/chessred --games 0,6,19
```

Results on all 20 ChessReD2K games (about 100 half-moves each), 2026-11-19:

| | |
|---|---|
| Games read exactly, start to finish | 7 of 20 |
| Games right except the very last half-move | 6 more |
| Median share of a game read before the first mistake | 99% |
| Worst games | first mistake after 45 to 64 half-moves (4 games) |

Two things to know when reading these numbers:

* ChessReD moves the camera for every photo, so the square reader has to
  re-learn the board each time. A fixed phone on a stand should do better.
  On the other hand, ChessReD has clean photos with no hands and hand-marked
  corners, so the still-period step and corner finding are not tested here.
* After the first mistake, the rest of the game is usually wrong too. The
  product will need a way to fix a move (or the board) and carry on.

## Not done yet

* Automatic corner detection (corners are given by hand).
* Testing on real videos with hands, glare and bumped pieces.
* A backend endpoint, job queue and upload flow.
