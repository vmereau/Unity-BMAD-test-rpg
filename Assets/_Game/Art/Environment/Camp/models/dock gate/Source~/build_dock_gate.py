"""Area 1 dock gate: a palisade gateway with two leaves of lashed logs standing open, the guard's plank
lean-to outside it and an iron fire basket.

Run in Blender (5.x):  blender -b -P build_dock_gate.py
or from a session:      runpy.run_path(r"...\\build_dock_gate.py", run_name="__main__")

Writes  ../DockGate.fbx, ../DockGate.json (colliders, Not Walkable volumes, the fire's light point and the sizes
        DockGateBuilder and the tests read) and DockGate.blend (the built objects only: this script is the source).

The mesh helpers, the materials and the FBX export are the warehouse's: this script loads
../../warehouse/Source~/build_warehouse.py as a library (Camp/CLAUDE.md -> One wood, by construction). In Unity the
round logs' material slot (Wh_Frame) is remapped to the palisade's wood for this model, so the gate reads as part
of the wall it stands in.

Frame (the prefab's): x east (out of the camp, along the bank path), y up, z north (the river); origin on the
lane in the middle of the opening. The lane is flat there; the south post stands on the bank, so both posts are
modelled deep. Sizes the game depends on (QUAY_CONCEPTS.md -> Build brief) are the constants of the first block.
"""
import json
import math
import os
import random
import runpy

import bpy

_HERE = os.path.dirname(os.path.abspath(__file__))
_LIB = runpy.run_path(os.path.join(os.path.dirname(os.path.dirname(_HERE)), "warehouse", "Source~", "build_warehouse.py"),
                      run_name="warehouse_lib")
Mesh, norm, cross, materials, export, to_blender, _normal = (_LIB[n] for n in ("Mesh", "norm", "cross", "materials", "export", "to_blender", "_normal"))
WOOD, FRAME, IRON, ROPE, TAR = (_LIB[n] for n in ("WOOD", "FRAME", "IRON", "ROPE", "TAR"))
MAX_BOARD = _LIB["MAX_BOARD"]
LOG = FRAME                           # round logs: remapped to the palisade's wood in the model importer

# --- Build brief ----------------------------------------------------------------------------------
CENTRE = (24.5, 59.0)                # world x, z of the origin (layout: dock-gate)
WIDTH = 3.0                          # between the posts' axes; the layout's gate width
POST_R = 0.25
POST_TOPS = (5.6, 4.9)               # south (bank side) and north (river side)
POST_DEEP = 2.2                      # below the lane: the river post stands in the falling bank
CLEAR = 3.2                          # under the lintel
LINTEL_R = 0.17
TOP_LOGS = 9                         # above the lintel, stepping down to the river side
TOP_R = 0.15
TOP_TOPS = (5.75, 5.05)              # first and last log's point
LEAF = (1.42, 3.0)                   # one leaf: width, height
LEAF_SWING = 6.0                     # degrees off the path's line: standing open, not quite flat
LEAN = (1.8, 4.1, -2.9, -1.4)        # guard lean-to: min x, max x, min z (back), max z (open front). East of x 4.2 the
                                     # bank rises, and rocks stand at z -2.8: it fits the flat between them
LEAN_FRONT, LEAN_BACK = 2.3, 1.8     # underside of its roof
BRAZIER = (2.9, 1.85)                # across the lane from the lean-to, past the north leaf: clear of the gateway's width
BRAZIER_TOP = 0.82


def cone(mesh, x, z, y, top, radius, mat, sides=8, lean=(0.0, 0.0)):
    """An axe-cut point on an upright log: a ring at `y`, the apex at `top` (a little off the axis)."""
    base = len(mesh.verts)
    for s in range(sides):
        a = 2.0 * math.pi * s / sides
        mesh.verts.append(to_blender((x + radius * math.cos(a), y, z + radius * math.sin(a))))
    mesh.verts.append(to_blender((x + lean[0], top, z + lean[1])))
    centre = to_blender((x, y, z))
    for s in range(sides):
        tri = [base + s, base + (s + 1) % sides, base + sides]
        p = [mesh.verts[i] for i in tri]
        n = _normal(p[0], p[1], p[2])
        if sum(n[k] * (p[0][k] + p[1][k] - 2.0 * centre[k]) for k in range(3)) < 0:
            tri.reverse()
        mesh.faces.append(tri)
        mesh.uvs.append([(0.3 + 0.1 * s / sides, 0.35), (0.3 + 0.1 * (s + 1) / sides, 0.35), (0.35, 0.45)])
        mesh.mats.append(mat)
        mesh.smooth.append(False)


class DockGate:
    def __init__(self, seed=83):
        self.rng = random.Random(seed)
        self.parts = {name: Mesh(self.rng) for name in ("Gate", "Leaves", "LeanTo", "Brazier")}
        self.colliders, self.not_walkable = [], []
        self.leaves = []

    def collider(self, name, group, centre, size, euler=(0.0, 0.0, 0.0), into=None):
        (self.colliders if into is None else into).append({
            "name": name, "group": group, "centre": [round(c, 4) for c in centre],
            "size": [round(c, 4) for c in size], "euler": [round(c, 3) for c in euler]})

    def gate(self):
        g, rng = self.parts["Gate"], self.rng
        half = WIDTH * 0.5
        for name, z, top in (("Post_South", -half - 0.05, POST_TOPS[0]), ("Post_North", half + 0.05, POST_TOPS[1])):
            g.log((0.0, -POST_DEEP, z), (rng.uniform(-0.02, 0.02), top, z + rng.uniform(-0.02, 0.02)), POST_R, LOG, sides=10, rings=7)
            self.collider(name, "Gate", (0.0, (top - POST_DEEP) * 0.5, z), (POST_R * 2.0, top + POST_DEEP, POST_R * 2.0))
        ly = CLEAR + LINTEL_R
        g.log((0.0, ly, -half - 0.45), (0.0, ly + 0.03, half + 0.45), LINTEL_R, LOG, sides=10, rings=5)
        # pointed logs standing on the lintel, pressed together, stepping down to the river side
        base = ly + LINTEL_R - 0.05
        span = WIDTH - 2.0 * POST_R - 0.1
        for i in range(TOP_LOGS):
            t = i / (TOP_LOGS - 1.0)
            z = -span * 0.5 + span * t
            top = TOP_TOPS[0] + (TOP_TOPS[1] - TOP_TOPS[0]) * t + rng.uniform(-0.08, 0.08)
            r = TOP_R * rng.uniform(0.9, 1.08)
            point = rng.uniform(0.27, 0.36)
            x = rng.uniform(-0.02, 0.02)
            g.log((x, base, z), (x, top - point, z), r, LOG, sides=8, rings=4)
            cone(g, x, z, top - point - 0.005, top, r * 0.98, LOG, lean=(rng.uniform(-0.04, 0.04), rng.uniform(-0.04, 0.04)))
        # a pole on each face ties them, lashed at its ends
        for side in (-1, 1):
            y = base + 0.75
            g.log((side * (TOP_R + 0.05), y, -half + 0.1), (side * (TOP_R + 0.05), y + 0.04, half - 0.1), 0.055, LOG, sides=6, rings=3)
        for z in (-half + 0.3, half - 0.3):
            for k in range(3):
                g.box((-TOP_R - 0.12, base + 0.69 + k * 0.035, z - 0.012 + k * 0.03), (TOP_R + 0.12, base + 0.72 + k * 0.035, z + 0.012 + k * 0.03), 0, ROPE)
        top = max(TOP_TOPS) + 0.1
        self.collider("Lintel", "Gate", (0.0, (CLEAR + top) * 0.5, 0.0), (0.44, top - CLEAR, WIDTH))

    def leaf(self, name, hinge_z, swing):
        """A leaf of upright logs on two cross poles and a diagonal, hung on the post at `hinge_z`, standing open
        along the path (+x), `swing` degrees off it."""
        lv, rng = self.parts["Leaves"], self.rng
        width, height = LEAF
        a = math.radians(swing)
        along = (math.cos(a), 0.0, math.sin(a))
        side = (-math.sin(a), 0.0, math.cos(a))        # across the leaf, toward +z
        face = 1.0 if hinge_z < 0 else -1.0            # the cross poles are on the face toward the lane
        hinge = (POST_R + 0.06, 0.1, hinge_z)
        count = 7
        step = width / count

        def at(s, y, out=0.0):
            return [hinge[0] + along[0] * s + side[0] * out * face, hinge[1] + y, hinge[2] + along[2] * s + side[2] * out * face]

        for i in range(count):
            s = step * (i + 0.5)
            r = step * 0.5 * rng.uniform(0.92, 1.04)
            top = height + rng.uniform(-0.12, 0.1)
            lv.log(at(s, 0.0), at(s + rng.uniform(-0.01, 0.01), top), r, LOG, sides=7, rings=4, wobble=0.006)
        for y in (0.55, height - 0.6):
            lv.log(at(-0.05, y, 0.14), at(width + 0.05, y + rng.uniform(-0.03, 0.03), 0.14), 0.05, LOG, sides=6, rings=3, wobble=0.004)
            for s in (step * 0.5, width - step * 0.5):   # rope round the pole and the end log
                for k in range(3):
                    c = at(s, y - 0.035 + k * 0.035, 0.05)
                    lv.framed(c, along, (0.0, 1.0, 0.0), side, (step * 0.5 + 0.03, 0.014, 0.17), 0, ROPE)
        lv.log(at(0.05, 0.6, 0.2), at(width - 0.05, height - 0.65, 0.2), 0.045, LOG, sides=6, rings=3, wobble=0.004)
        mid = at(width * 0.5, height * 0.5, 0.05)
        self.collider(name, "Leaves", mid, (width + 0.1, height + 0.2, 0.34), (0.0, -swing, 0.0))
        self.collider(name, "NotWalkable", mid, (width + 0.3, height + 0.2, 0.7), (0.0, -swing, 0.0), into=self.not_walkable)
        self.leaves.append({"name": name, "hinge": [round(c, 3) for c in hinge], "swing": swing, "width": width, "height": height})

    def lean_to(self):
        lt, rng = self.parts["LeanTo"], self.rng
        x0, x1, z0, z1 = LEAN
        P = 0.12
        # posts: two tall ones at the open front, two short ones at the back
        for x in (x0 + P * 0.5, x1 - P * 0.5):
            lt.box((x - P * 0.5, -0.3, z1 - P), (x + P * 0.5, LEAN_FRONT, z1), 1, WOOD)
            lt.box((x - P * 0.5, -0.3, z0), (x + P * 0.5, LEAN_BACK, z0 + P), 1, WOOD)
            self.collider("Post", "LeanTo", (x, LEAN_FRONT * 0.5, z1 - P * 0.5), (P, LEAN_FRONT, P))
        # back wall and the east side: horizontal planks on the posts' outer face, ragged
        for ya, yb in lt.rows(0.04, LEAN_BACK - 0.05, 0.15):
            lt.box((x0 - rng.uniform(0.0, 0.08), ya, z0 - 0.03), (x1 + rng.uniform(0.0, 0.08), yb - rng.uniform(0.004, 0.02), z0), 0, WOOD)
        tan = (LEAN_FRONT - LEAN_BACK) / (z1 - z0)
        for ya, yb in lt.rows(0.04, LEAN_FRONT - 0.1, 0.15):
            reach = z0 + (yb - LEAN_BACK + 0.12) / tan if yb > LEAN_BACK - 0.12 else z0   # the planks stop under the roof's slope
            if z1 - reach < 0.4:
                break
            lt.box((x1, ya, max(z0, reach) - 0.02), (x1 + 0.03, yb - rng.uniform(0.004, 0.02), z1 - 0.2 + rng.uniform(-0.1, 0.12)), 2, WOOD)
        self.collider("Back", "LeanTo", ((x0 + x1) * 0.5, LEAN_BACK * 0.5, z0), (x1 - x0, LEAN_BACK, 0.16))
        self.collider("Side", "LeanTo", (x1, LEAN_BACK * 0.5, (z0 + z1) * 0.5), (0.16, LEAN_BACK, z1 - z0))
        # roof: two plates, planks down the slope, overhanging front and back
        slope = math.atan(tan)
        cos, sin = math.cos(slope), math.sin(slope)
        down = (0.0, -sin, -cos)                       # from the front down to the back
        normal = (0.0, cos, -sin)
        length = (z1 - z0 + 0.45) / cos
        front = z1 + 0.1                               # the eave stops short of the gateway's line
        top = (0.0, LEAN_FRONT + 0.1 * tan + 0.07, front)
        for z, y in ((z1 - P * 0.5, LEAN_FRONT), (z0 + P * 0.5, LEAN_BACK)):
            lt.box((x0 - 0.15, y, z - 0.05), (x1 + 0.15, y + 0.09, z + 0.05), 0, WOOD)
        for xa, xb in lt.rows(x0 - 0.22, x1 + 0.22, 0.16):
            ln = length + rng.uniform(-0.12, 0.1)
            o = [top[k] + down[k] * ln * 0.5 + normal[k] * 0.015 + (xa + xb) * 0.5 * (1.0 if k == 0 else 0.0) for k in range(3)]
            lt.framed(o, down, normal, (1.0, 0.0, 0.0), (ln * 0.5, 0.014, (xb - xa) * 0.5 - 0.003), 0, WOOD)
        mz = front - length * cos * 0.5
        self.collider("Roof", "LeanTo", ((x0 + x1) * 0.5, top[1] - length * sin * 0.5, mz), (x1 - x0 + 0.4, 0.12, length), (-math.degrees(slope), 0.0, 0.0))
        # bench along the back: a plank on two blocks
        bx0, bx1, bz0, bz1 = x0 + 0.4, x1 - 0.6, z0 + 0.14, z0 + 0.5
        lt.box((bx0, 0.4, bz0), (bx1, 0.45, bz1), 0, WOOD)
        for x in (bx0 + 0.25, bx1 - 0.25):
            lt.box((x - 0.06, -0.05, bz0 + 0.04), (x + 0.06, 0.4, bz1 - 0.04), 1, WOOD)
        self.collider("Bench", "LeanTo", ((bx0 + bx1) * 0.5, 0.225, (bz0 + bz1) * 0.5), (bx1 - bx0, 0.45, bz1 - bz0))
        self.collider("Bench", "NotWalkable", ((bx0 + bx1) * 0.5, 0.5, (bz0 + bz1) * 0.5), (bx1 - bx0 + 0.2, 1.2, bz1 - bz0 + 0.2), into=self.not_walkable)
        # three spears leaning in the back corner
        for i in range(3):
            fx, fz = x1 - 0.2 - i * 0.09, z0 + 0.62 + i * 0.07
            tx, tz = x1 - 0.06, z0 + 0.12 + i * 0.1
            ty = 1.86 + i * 0.05
            lt.log((fx, 0.0, fz), (tx, ty, tz), 0.016, WOOD, sides=5, rings=2, wobble=0.0)
            ax = norm((tx - fx, ty, tz - fz))
            tip = [tx + ax[0] * 0.2, ty + ax[1] * 0.2, tz + ax[2] * 0.2]
            lt.beam((tx, ty, tz), tip, 0.035, 0.008, IRON, extend=0.0)

    def brazier(self):
        br, rng = self.parts["Brazier"], self.rng
        x, z = BRAZIER
        top = BRAZIER_TOP
        radius = 0.26
        for k in range(3):                              # three legs, splayed
            a = 2.0 * math.pi * k / 3.0 + 0.5
            br.log((x + 0.3 * math.cos(a), -0.3, z + 0.3 * math.sin(a)), (x + 0.2 * math.cos(a), top - 0.3, z + 0.2 * math.sin(a)), 0.024, IRON,
                   sides=5, rings=2, wobble=0.0)
        for y, r in ((top - 0.32, radius * 0.78), (top - 0.16, radius * 0.93), (top, radius)):   # hoops
            br.ring((x, y, z), r, 0.018, IRON, segs=12, sides=4)
        for k in range(10):                             # bars between the hoops
            a = 2.0 * math.pi * k / 10.0
            br.log((x + radius * 0.76 * math.cos(a), top - 0.33, z + radius * 0.76 * math.sin(a)), (x + radius * math.cos(a), top + 0.01, z + radius * math.sin(a)),
                   0.012, IRON, sides=4, rings=2, wobble=0.0)
        br.log((x, top - 0.34, z), (x, top - 0.31, z), radius * 0.76, IRON, sides=10, rings=2, wobble=0.0)   # the pan
        for k in range(4):                              # charred wood
            a = rng.uniform(0.0, math.pi)
            lift = top - 0.26 + k * 0.045
            br.log((x - 0.17 * math.cos(a), lift, z - 0.17 * math.sin(a)), (x + 0.17 * math.cos(a), lift + rng.uniform(0.0, 0.06), z + 0.17 * math.sin(a)),
                   0.035, TAR, sides=6, rings=2, wobble=0.0)
        self.collider("Brazier", "Brazier", (x, top * 0.5, z), (0.6, top, 0.6))
        self.collider("Brazier", "NotWalkable", (x, 0.6, z), (0.9, 1.4, 0.9), into=self.not_walkable)
        return [round(x, 3), round(top + 0.12, 3), round(z, 3)]

    def build(self):
        self.gate()
        half = WIDTH * 0.5
        self.leaf("Leaf_South", -half - 0.05, -LEAF_SWING)
        self.leaf("Leaf_North", half + 0.05, LEAF_SWING)
        self.lean_to()
        fire = self.brazier()
        return {"centre": list(CENTRE), "opening": [round(-half + POST_R, 3), round(half - POST_R, 3), CLEAR], "postTops": list(POST_TOPS),
                "leaves": self.leaves, "leanTo": list(LEAN), "fire": fire}


def main():
    source = _HERE
    out = os.path.dirname(source)
    gate = DockGate()
    facts = gate.build()

    name = "DockGate"
    old = bpy.data.collections.get(name)
    kept = set(old.objects) if old else set()
    for part in gate.parts:   # checked before anything is deleted
        clash = bpy.data.objects.get(part)
        if clash and clash not in kept:
            raise RuntimeError("An object called '%s' already exists in this Blender session; rename it first" % part)
    if old:
        for o in list(old.objects):
            bpy.data.objects.remove(o, do_unlink=True)
        bpy.data.collections.remove(old)
    col = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(col)
    mats = materials()
    objects = [mesh.to_object(part, mats, col) for part, mesh in gate.parts.items()]
    export(objects, os.path.join(out, name + ".fbx"))
    triangles = {part: mesh.triangles() for part, mesh in gate.parts.items()}
    # the parts file goes out with the FBX, before anything else can fail: the two must never be a version apart
    parts = {"version": 1, "triangles": triangles, "total": sum(triangles.values()), "objects": list(gate.parts),
             "colliders": gate.colliders, "notWalkable": gate.not_walkable}
    parts.update(facts)
    with open(os.path.join(out, name + ".json"), "w") as f:
        json.dump(parts, f, indent=1)
    for o in objects:
        o.name = name + "_" + o.name
    bpy.data.libraries.write(os.path.join(source, name + ".blend"), set(objects) | set(mats), fake_user=True)
    return {"triangles": triangles, "total": parts["total"], "facts": facts}


if __name__ == "__main__":
    result = main()
    print("dock gate:", result)
