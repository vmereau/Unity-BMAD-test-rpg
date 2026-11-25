"""Summarise CSV logs written by action_root_motion_play_measure.cs (spec-action-root-motion-unarmed-combo) and
sword_combo_play_measure.cs (spec-sword-three-hit-combo: same columns plus the blade tip and the off-hand distances).

    python Tools/Blender/action_root_motion_analyse.py <run.csv> [more.csv ...]

Per run: travel of each attack state (root forward between its enter and the next enter / the end), total travel and
sideways drift, root speed inside each attack -> attack crossfade (a dropped or doubled travel shows as a speed of 0 or a
jump), planted-foot slide (a foot within 2 cm of its lowest height and slower than 0.3 m/s for at least 0.1 s: how far
it moves horizontally meanwhile) and the largest frame-to-frame velocity jump of hips, hands and feet, inside and outside
attack -> attack crossfades (a pose pop shows as a jump far above the clip's own). With blade columns: per attack, the
blade tip when it is furthest ahead of the root, and the smallest left hand -> right hand, left hand -> blade and
off-hand item -> blade distances of the run.
"""
import csv
import math
import sys

PLANT_HEIGHT = 0.02   # m above the foot's lowest point
PLANT_MIN_TIME = 0.1  # s
PLANT_SPEED = 0.3     # m/s: faster than this the foot is stepping, not planted


def load(path):
    with open(path, newline="") as f:
        return [{k: float(v) for k, v in row.items()} for row in csv.DictReader(f)]


def planted_slides(rows, foot):
    """Intervals where the foot is low and slower than PLANT_SPEED: (t0, t1, horizontal slide, over a crossfade)."""
    low = min(r[foot + "Y"] for r in rows)
    down = [False]
    for i in range(1, len(rows)):
        dt = rows[i]["t"] - rows[i - 1]["t"]
        speed = math.hypot(rows[i][foot + "F"] - rows[i - 1][foot + "F"], rows[i][foot + "S"] - rows[i - 1][foot + "S"]) / dt if dt > 0 else 0.0
        down.append(rows[i][foot + "Y"] <= low + PLANT_HEIGHT and speed < PLANT_SPEED)
    out, start = [], None
    for i in range(len(rows) + 1):
        on = i < len(rows) and down[i]
        if on and start is None:
            start = i
        elif not on and start is not None:
            seg = rows[start:i]
            if seg[-1]["t"] - seg[0]["t"] >= PLANT_MIN_TIME:
                fs, ss = [x[foot + "F"] for x in seg], [x[foot + "S"] for x in seg]
                slide = math.hypot(max(fs) - min(fs), max(ss) - min(ss))
                out.append((seg[0]["t"], seg[-1]["t"], slide, any(x["next"] > 0 and x["t"] > 0.3 for x in seg)))
            start = None
    return out


def analyse(path):
    rows = load(path)
    if not rows:
        print("== %s: empty log" % path)
        return
    print("== %s (%d frames, %.0f fps)" % (path.replace("\\", "/").split("/")[-1], len(rows), len(rows) / max(rows[-1]["t"], 1e-6)))
    attack = [r for r in rows if r["attacking"] > 0]
    if not attack:
        print("   no attack recorded")
        return
    # attack state entries: the destination state changes, or a new transition into an attack state starts
    entries, last, last_blending = [], 0, False
    for i, r in enumerate(rows):
        blending = r["next"] > 0
        if r["state"] > 0 and (r["state"] != last or (blending and not last_blending)):
            entries.append(i)
        last, last_blending = r["state"], blending
    if not entries:
        print("   attacking, but no attack state was entered (press refused or trigger not taken)")
        return
    end = rows.index(attack[-1])
    bounds = entries + [end]
    for n in range(len(entries)):
        a, b = rows[bounds[n]], rows[bounds[n + 1]]
        print("   attack %d (state %d) enter t=%.2f  travel %.3f m  sideways %+.3f m" %
              (n + 1, int(a["state"]), a["t"], b["rootF"] - a["rootF"], b["rootS"] - a["rootS"]))
    print("   total travel %.3f m (peak %.3f), sideways end %+.3f m, sideways peak %.3f m, rootMotion owner on %d / %d attack frames" %
          (rows[-1]["rootF"], max(r["rootF"] for r in rows), rows[-1]["rootS"], max(abs(r["rootS"]) for r in rows),
           sum(1 for r in attack if r["rootMotion"] > 0), len(attack)))
    # root speed through attack -> attack crossfades
    speeds = []
    for i in range(1, len(rows)):
        dt = rows[i]["t"] - rows[i - 1]["t"]
        if dt > 0:
            speeds.append((i, (rows[i]["rootF"] - rows[i - 1]["rootF"]) / dt))
    blend_runs, run = [], []
    for i, v in speeds:
        if rows[i]["next"] > 0 and i > entries[0] and rows[i]["t"] - rows[entries[0]]["t"] > 0.3:
            run.append((i, v))
        elif run:
            blend_runs.append(run)
            run = []
    if run:
        blend_runs.append(run)
    for run in blend_runs:
        vs = [v for _, v in run]
        jumps = [abs(vs[k] - vs[k - 1]) for k in range(1, len(vs))]
        zero = sum(1 for v in vs if abs(v) < 0.005)
        print("   crossfade t=%.2f-%.2f: root speed min %.2f / max %.2f m/s, frames at 0: %d of %d, largest frame-to-frame change %.2f m/s, travel inside %.3f m" %
              (rows[run[0][0]]["t"], rows[run[-1][0]]["t"], min(vs), max(vs), zero, len(vs), max(jumps) if jumps else 0.0,
               rows[run[-1][0]]["rootF"] - rows[run[0][0] - 1]["rootF"]))
    for foot, name in (("lf", "left"), ("rf", "right")):
        slides = [s for s in planted_slides(rows[:end + 1], foot) if s[1] > rows[entries[0]]["t"]]
        if slides:
            worst = max(slides, key=lambda s: s[2])
            in_blend = [s[2] for s in slides if s[3]]
            print("   %s foot planted %d times, worst slide %.3f m (t=%.2f-%.2f), worst over a crossfade %.3f m" %
                  (name, len(slides), worst[2], worst[0], worst[1], max(in_blend) if in_blend else 0.0))
    for bone in ("hips", "lh", "rh", "lf", "rf"):
        worst = {False: (0.0, 0.0), True: (0.0, 0.0)}
        prev = None
        for i in range(entries[0] + 1, end + 1):
            dt = rows[i]["t"] - rows[i - 1]["t"]
            if dt <= 0:
                continue
            v = [(rows[i][bone + a] - rows[i - 1][bone + a]) / dt for a in "FSY"]
            if prev is not None:
                jump = math.sqrt(sum((v[k] - prev[k]) ** 2 for k in range(3)))
                key = rows[i]["next"] > 0 and rows[i]["t"] > 0.3
                if jump > worst[key][0]:
                    worst[key] = (jump, rows[i]["t"])
            prev = v
        print("   %-4s largest velocity jump between frames: %.2f m/s in a crossfade (t=%.2f), %.2f m/s outside (t=%.2f)" %
              (bone, worst[True][0], worst[True][1], worst[False][0], worst[False][1]))
    if "tipF" in rows[0]:
        armed = [r for r in rows[entries[0]:end + 1] if r["lhRh"] >= 0]
        for n in range(len(entries)):
            seg = [r for r in rows[bounds[n]:bounds[n + 1] + 1] if r["lhRh"] >= 0]
            if not seg:
                continue
            far = max(seg, key=lambda r: r["tipF"] - r["rootF"])
            print("   attack %d blade tip furthest ahead at stateTime %.2f s: %.2f m ahead of the root, %+.2f m sideways, %.2f m high" %
                  (n + 1, far["stateTime"], far["tipF"] - far["rootF"], far["tipS"] - far["rootS"], far["tipY"]))
        if armed:
            a, b = min(armed, key=lambda r: r["lhRh"]), min(armed, key=lambda r: r["lhBlade"])
            print("   off-hand: left hand -> right hand min %.2f m (t=%.2f), left hand -> blade min %.2f m (t=%.2f)" %
                  (a["lhRh"], a["t"], b["lhBlade"], b["t"]))
            held = [r for r in armed if r["offBlade"] >= 0]
            if held:
                c = min(held, key=lambda r: r["offBlade"])
                print("   off-hand item -> blade min %.2f m (t=%.2f, state %d at %.2f s)" %
                      (c["offBlade"], c["t"], int(c["state"]), c["stateTime"]))


if __name__ == "__main__":
    for arg in sys.argv[1:]:
        analyse(arg)
