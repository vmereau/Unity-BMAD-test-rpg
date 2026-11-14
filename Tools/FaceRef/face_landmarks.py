# /// script
# requires-python = ">=3.12,<3.13"
# dependencies = [
#     "mediapipe>=0.10.14,<0.11",
#     "numpy",
#     "pillow",
# ]
# ///
"""Face landmarks for NPC portrait faces (spec-npc-portrait-faces). Runs OUTSIDE Blender, in an isolated uv env:

    uv run Tools/FaceRef/face_landmarks.py <front.png> <side.png> --out <landmarks.json>

Front image: MediaPipe Face Landmarker, 478 points (468 mesh + 10 iris), pixel coordinates (x right, y down).
Exactly one face must be found, or the tool exits non-zero naming the image and writes nothing.
Side image (profile facing LEFT, plain background): silhouette front edge per row + profile features
(nose tip, brow, nasion, chin, crown); make_head_variant scales the profile crown -> nose tip (the nasion is
for the overlay only). Exits non-zero on a profile facing right, or a crop touching the top / left edge found on that edge.

Output JSON: {"front": {"image", "size": [w, h], "points": [[x, y, z], ...], "crown_row"},
              "side":  {"image", "size": [w, h], "edge": [[row, x], ...], "features": {name: [row, x]}}}
A debug overlay is written next to the JSON (<out>_debug_front.png / _side.png) to check the fit by eye.
The MediaPipe model is downloaded once to Temp/FaceRef/.
"""
import argparse
import json
import sys
import urllib.request
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

PROJECT = Path(__file__).resolve().parents[2]
MODEL_URL = ("https://storage.googleapis.com/mediapipe-models/face_landmarker/face_landmarker/float16/1/"
             "face_landmarker.task")
MODEL = PROJECT / "Temp/FaceRef/face_landmarker.task"


def _model():
    if not MODEL.exists():
        MODEL.parent.mkdir(parents=True, exist_ok=True)
        tmp = MODEL.with_suffix(".part")
        urllib.request.urlretrieve(MODEL_URL, tmp)
        tmp.replace(MODEL)
    return str(MODEL)


def _rgb(path):
    img = Image.open(path)
    if img.mode in ("RGBA", "LA", "PA") or (img.mode == "P" and "transparency" in img.info):
        # transparent background -> light grey, like the prompt's background (make_head_variant._load_rgb does the same)
        img = img.convert("RGBA")
        bg = Image.new("RGBA", img.size, (200, 200, 200, 255))
        img = Image.alpha_composite(bg, img)
    return img.convert("RGB")


def front_landmarks(path):
    import mediapipe as mp
    from mediapipe.tasks.python import BaseOptions, vision

    img = _rgb(path)
    w, h = img.size
    opts = vision.FaceLandmarkerOptions(base_options=BaseOptions(model_asset_path=_model()),
                                        running_mode=vision.RunningMode.IMAGE, num_faces=2,
                                        min_face_detection_confidence=0.3, min_face_presence_confidence=0.3)
    with vision.FaceLandmarker.create_from_options(opts) as lm:
        res = lm.detect(mp.Image(image_format=mp.ImageFormat.SRGB, data=np.asarray(img).copy()))
    faces = res.face_landmarks
    if len(faces) != 1:
        raise SystemExit(f"{path}: expected exactly one face, MediaPipe found {len(faces)}")
    pts = [[p.x * w, p.y * h, p.z * w] for p in faces[0]]
    if len(pts) != 478:
        raise SystemExit(f"{path}: expected 478 landmarks (with iris), got {len(pts)}")
    # Crown: top of the head silhouette between the temples (the side profile is scaled crown -> nose tip).
    fg = _silhouette(img)
    lo = int(np.clip(min(pts[234][0], pts[454][0]), 0, w - 1))
    hi = int(np.clip(max(pts[234][0], pts[454][0]), 0, w - 1))
    rows = np.where(fg[:, lo:hi + 1].any(axis=1))[0]
    if len(rows) == 0:
        raise SystemExit(f"{path}: no head silhouette above the face (plain background expected)")
    if rows[0] <= 2:
        raise SystemExit(f"{path}: the head touches the top edge (crown cropped): regenerate with margin above the hair")
    return {"image": str(path), "size": [w, h], "points": pts, "crown_row": int(rows[0])}


def _silhouette(img):
    a = np.asarray(img).astype(np.int32)
    border = np.concatenate([a[0], a[-1], a[:, 0], a[:, -1]])
    bg = np.median(border, axis=0)
    fg = np.abs(a - bg).max(axis=2) > 28
    m = Image.fromarray((fg * 255).astype(np.uint8))
    m = m.filter(ImageFilter.MinFilter(5)).filter(ImageFilter.MaxFilter(5))  # opening: drops speckles
    m = m.filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.MinFilter(5))  # closing: fills small holes
    return np.asarray(m) > 127


def side_profile(path):
    img = _rgb(path)
    w, h = img.size
    fg = _silhouette(img)
    rows = np.where(fg.any(axis=1))[0]
    if len(rows) < h * 0.3:
        raise SystemExit(f"{path}: no head silhouette found (plain background expected)")
    edge = {int(r): int(np.argmax(fg[r])) for r in rows}  # first foreground pixel from the left = front of the face
    top = int(rows[0])
    if top <= 2:
        raise SystemExit(f"{path}: the head touches the top edge (crown cropped)")
    r = np.array(sorted(edge))
    x = np.array([edge[i] for i in r])

    def pick(lo, hi, fn):
        sel = (r >= lo) & (r <= hi)
        if not sel.any():
            raise SystemExit(f"{path}: profile feature window {lo}-{hi} is empty")
        i = np.where(sel)[0][fn(x[sel])]
        return [int(r[i]), int(x[i])]

    # Nose tip: most forward point in the upper part of the image (below the crown, above the shoulders).
    tip = pick(top + 0.15 * (h - top), top + 0.62 * (h - top), np.argmin)
    if tip[1] <= 2:
        raise SystemExit(f"{path}: the nose touches the left edge (cropped profile): regenerate with margin")
    L = tip[0] - top  # crown -> nose tip, the profile's length unit
    # Facing check: a nose tip is a sharp point (the edge recedes >= ~5 mm within ~1.5 cm above and below it); the
    # back of a skull (what a right-facing profile shows on the left) curves away by ~1 mm over the same span.
    def recede(r0):
        sel = np.abs(r - r0) <= 2
        return float(x[sel].mean()) - tip[1] if sel.any() else 0.0
    if min(recede(tip[0] - 0.08 * L), recede(tip[0] + 0.08 * L)) < 0.03 * L:
        raise SystemExit(f"{path}: no nose point on the left edge: the profile may face RIGHT (mirror it) or be cropped")
    # Nasion: deepest point between the brow ridge (most forward point above the eyes) and the nose tip. A plain
    # "deepest point above the tip" lands on the sloping forehead instead.
    # The dip can be a few pixels deep, so look for local extrema on a smoothed edge, scanning up from the tip.
    xs = np.convolve(np.pad(x.astype(np.float64), 4, mode="edge"), np.ones(9) / 9, mode="valid")
    lo, hi = tip[0] - 0.55 * L, tip[0] - 0.08 * L
    cand = [i for i in range(8, len(r) - 8) if lo <= r[i] <= hi
            and xs[i] >= xs[i - 8:i + 9].max() - 1e-9]
    if cand:
        i = max(cand)  # the local recess closest above the nose tip
        nasion = [int(r[i]), int(x[i])]
    else:  # straight forehead-to-nose line: fall back to the base hero's proportion
        j = int(np.argmin(np.abs(r - (tip[0] - 0.27 * L))))
        nasion = [int(r[j]), int(x[j])]
    brow = pick(nasion[0] - 0.25 * L, nasion[0] - 1, np.argmin)
    feats = {
        "nose_tip": tip,
        "brow": brow,
        "nasion": nasion,
        "chin": pick(tip[0] + 0.3 * L, tip[0] + 0.65 * L, np.argmin) if r[-1] >= tip[0] + 0.3 * L else None,  # overlay only
        "crown": [top, int(edge[top])],
    }
    return {"image": str(path), "size": [w, h], "edge": [[int(i), int(edge[i])] for i in r], "features": feats}


def _debug(front, side, out):
    if front:
        img = _rgb(front["image"])
        d = ImageDraw.Draw(img)
        for x, y, _ in front["points"]:
            d.ellipse([x - 1.5, y - 1.5, x + 1.5, y + 1.5], fill=(255, 40, 40))
        img.save(out.with_name(out.stem + "_debug_front.png"))
    if side:
        img = _rgb(side["image"])
        d = ImageDraw.Draw(img)
        d.line([(x, r) for r, x in side["edge"]], fill=(40, 200, 255), width=2)
        for name, f in side["features"].items():
            if f is None:
                continue
            r, x = f
            d.ellipse([x - 5, r - 5, x + 5, r + 5], outline=(255, 40, 40), width=2)
            d.text((x + 8, r - 6), name, fill=(255, 40, 40))
        img.save(out.with_name(out.stem + "_debug_side.png"))


def main(argv):
    ap = argparse.ArgumentParser()
    ap.add_argument("front", help="front portrait (or base head front render)")
    ap.add_argument("side", help="side profile facing left (or base head side render)")
    ap.add_argument("--out", required=True)
    args = ap.parse_args(argv)
    for p in (args.front, args.side):
        if not Path(p).is_file():
            raise SystemExit(f"{p}: file not found")
    front = front_landmarks(args.front)
    side = side_profile(args.side)
    out = Path(args.out)
    out.parent.mkdir(parents=True, exist_ok=True)
    rel = lambda p: (Path(p).resolve().relative_to(PROJECT).as_posix() if Path(p).resolve().is_relative_to(PROJECT)
                     else Path(p).resolve().as_posix())  # absolute outside the project (Blender joins it to PROJECT)
    front["image"], side["image"] = rel(front["image"]), rel(side["image"])
    out.write_text(json.dumps({"front": front, "side": side}, indent=1), encoding="utf-8")
    front["image"], side["image"] = str(PROJECT / front["image"]), str(PROJECT / side["image"])
    _debug(front, side, out)
    print(f"{out}: {len(front['points'])} front landmarks, profile {len(side['edge'])} rows, "
          f"features {side['features']}")


if __name__ == "__main__":
    main(sys.argv[1:])
