"""Command line: python -m video_to_lichess VIDEO --corners "x,y;x,y;x,y;x,y" [--import]"""
from __future__ import annotations

import argparse
import sys

import cv2

from .board import parse_corners
from .lichess import analysis_url, build_pgn, import_game
from .pipeline import read_video


def main(argv: list[str] | None = None) -> int:
    p = argparse.ArgumentParser(prog="video_to_lichess", description=__doc__)
    p.add_argument("video")
    p.add_argument(
        "--corners",
        help="board corners in the frame as 'x,y;x,y;x,y;x,y', clockwise from any corner. "
        "Orientation is detected from the starting position.",
    )
    p.add_argument("--first-frame", metavar="PNG", help="save the first frame (to read off corners) and exit")
    p.add_argument("--fps", type=float, default=4.0, help="frames sampled per second (default 4)")
    p.add_argument("--motion", type=float, default=2.5, help="motion threshold for a still board (default 2.5)")
    p.add_argument("--import", dest="do_import", action="store_true", help="also import the game on Lichess (public)")
    args = p.parse_args(argv)

    if args.first_frame:
        cap = cv2.VideoCapture(args.video)
        ok, frame = cap.read()
        cap.release()
        if not ok:
            print("could not read the video", file=sys.stderr)
            return 1
        cv2.imwrite(args.first_frame, frame)
        print(f"saved {args.first_frame} ({frame.shape[1]}x{frame.shape[0]})")
        return 0
    if not args.corners:
        p.error("--corners is required (use --first-frame to find them)")

    result = read_video(args.video, parse_corners(args.corners), args.fps, args.motion)
    pgn = build_pgn(result.moves)
    print(f"# {result.still_periods} still periods, {len(result.moves)} half-moves", file=sys.stderr)
    print(pgn)
    print(f"\nAnalysis link (nothing uploaded): {analysis_url(result.moves)}")
    if args.do_import:
        print(f"Imported on Lichess: {import_game(pgn)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
