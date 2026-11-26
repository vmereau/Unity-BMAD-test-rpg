"""Area 1 canteen: the cookhouse (kitchen + store), its cook porch, the mess shelter, a trestle table set and
the back alley's fence, built piece by piece.

Run in Blender (5.x):  blender -b -P build_cookhouse.py
or from a session:      runpy.run_path(r"...\\build_cookhouse.py", run_name="__main__")

Writes  ../Cookhouse.fbx, ../Cookhouse.json (colliders, walk surfaces, lights, table poses and the anchors
        CookhouseBuilder places things at) and Cookhouse.blend (the built objects only: this script is the source).

The mesh helpers (boards mapped onto one plank of T_ModularWood, round logs, rubble stones), the materials and
the FBX export are the warehouse's: this script loads ../../warehouse/Source~/build_warehouse.py as a library,
so the two buildings share one wood by construction (Camp/CLAUDE.md -> One wood, by construction).

Frame (the prefab's): x east, y up, z north, origin on the floor's top at the building's centre. The ground is
flat, GROUND below the floor: the site is levelled by Tools/World/Place Area 1 Canteen before the prefab lands.
Sizes the game depends on (CANTEEN_CONCEPTS.md -> Build brief) are the constants of the first block.
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
Mesh, norm, cross, materials, export = _LIB["Mesh"], _LIB["norm"], _LIB["cross"], _LIB["materials"], _LIB["export"]
# the library's material slots: taken from it, so a reorder there cannot swap materials here
WOOD, FRAME, SHINGLE, PALE, RUBBLE, IRON = (_LIB[n] for n in ("WOOD", "FRAME", "SHINGLE", "PALE", "RUBBLE", "IRON"))
MAX_BOARD = _LIB["MAX_BOARD"]

# --- Build brief ----------------------------------------------------------------------------------
CENTRE = (-15.75, 20.0)              # world x, z of the origin
GROUND = 0.15                        # the levelled ground lies this far below the floor's top
HX, HZ = 2.75, 4.5                   # half footprint: 5.5 x 9 m, long axis north-south
PART_Z = 0.8                         # partition: kitchen south of it, store north (posts 0.8..1.0, boards behind)
WALL = 2.6                           # top of the wall plates above the floor
PITCH = math.radians(30.0)
EAVE_OVERHANG, GABLE_OVERHANG = 0.35, 0.4
HATCH = (-1.4, 0.8, 0.95, 2.0)       # east wall: z from, z to, sill, head
KITCHEN_DOOR = (0.75, 1.85, 2.1)     # south gable: x from, x to, head
INNER_DOOR = (1.15, 2.15, 2.1)       # partition
DELIVERY_DOOR = (2.0, 3.6, 2.2)      # west wall: z from, z to, head
PORCH_DEPTH = 3.0                    # lean-to south of the kitchen
PORCH_HIGH, PORCH_LOW = 2.55, 2.2    # underside of its boards at the wall and at the posts: under the main roof's eave line
MESS = (4.25, 10.25, -10.0, -5.5)    # mess shelter: min x, max x, min z (palisade side), max z
MESS_HIGH, MESS_LOW = 3.0, 2.4       # above the ground, south (high) and north (low)
ALLEY_X = (-6.25, -HX)               # the back alley between the huts and the west wall
FENCE_Z = 5.0                        # its fence and gate, on the arrival lane
WALL_BOX = 0.24                      # wall collider thickness

# --- Construction ---------------------------------------------------------------------------------
POST = 0.2
BOARD = 0.04
INNER = POST                         # footprint line to the boards' outer face
PLATE = 0.16
TAN, COS, SIN = math.tan(PITCH), math.cos(PITCH), math.sin(PITCH)
EAST_POSTS = (-4.4, -2.9, -1.5, 0.9, 2.7, 4.4)
WEST_POSTS = (-4.4, -2.6, -0.8, 0.9, 1.9, 3.7, 4.4)
SOUTH_POSTS = (-2.65, -1.0, 0.65, 1.95, 2.65)
NORTH_POSTS = (-2.65, -0.9, 0.9, 2.65)
PART_POSTS = (-2.65, 1.05, 2.25, 2.65)
TIES = (-2.9, 2.7)                   # tie beams across the rooms (the partition's plate is the third)
HEARTH = (-1.75, 0.05, -0.2, 0.8)    # fire bench against the partition: min x, max x, min z, max z
CHIMNEY = (-1.25, -0.45, 0.1, 0.8)
CHIMNEY_TOP = 4.8
# The two rows under the shelter stand 2.5 m apart: 0.88 m between their benches, enough for a walker (0.6 m).
TABLES = ((-10.0, 11.0, 0.0), (-7.0, 11.0, 0.0), (-10.0, 13.5, 0.0), (-7.0, 13.5, 0.0),   # world x, z, yaw
          (-9.6, 16.9, 12.0), (-6.9, 16.3, -8.0))


def roof_plane(x):
    """Top of the roof boards above local x."""
    return WALL + (HX - abs(x)) * TAN


class Cookhouse:
    def __init__(self, seed=47):
        self.rng = random.Random(seed)
        self.parts = {name: Mesh(self.rng) for name in
                      ("Frame", "Walls", "Floor", "Roof", "Doors", "Hearth", "Rubble", "Iron", "Furniture", "Shelters",
                       "Yard", "TableSet")}
        self.colliders, self.walk, self.lights, self.lanterns, self.anchors = [], [], [], [], {}

    # -- parts file --------------------------------------------------------------------------------
    def collider(self, name, group, centre, size, euler=(0.0, 0.0, 0.0)):
        self.colliders.append({"name": name, "group": group, "centre": [round(c, 4) for c in centre],
                               "size": [round(abs(c), 4) for c in size], "euler": [round(c, 3) for c in euler]})

    def box_collider(self, name, group, lo, hi):
        self.collider(name, group, [(lo[i] + hi[i]) * 0.5 for i in range(3)], [hi[i] - lo[i] for i in range(3)])

    def anchor(self, name, *values):
        self.anchors[name] = [round(v, 3) for v in values]

    # -- walls -------------------------------------------------------------------------------------
    def wall(self, name, runs_x, line, out, a, b, posts, openings=(), sill_stones=True, gable=False):
        """One framed wall: squared posts with their outer face on `line`, a sill beam, a girt and a top plate
        between them, horizontal boards on the posts' inner face, cut round the openings
        (from, to, sill, head along the wall). `out` is +1 / -1: the side of `line` that is outside."""
        frame, walls, rng = self.parts["Frame"], self.parts["Walls"], self.rng
        grain = 0 if runs_x else 2
        centre = line - out * POST * 0.5
        for o in openings:   # an opening stands between two posts' faces: anything else would leave a post in a doorway
            if not (any(abs(o[0] - (q + POST * 0.5)) < 1e-6 for q in posts) and any(abs(o[1] - (q - POST * 0.5)) < 1e-6 for q in posts)) \
                    or any(o[0] < q < o[1] for q in posts):
                raise ValueError("%s: the opening %s does not stand between two posts" % (name, (o[0], o[1])))

        def p(along, across, y):
            return (along, y, across) if runs_x else (across, y, along)

        def box(mesh, a0, a1, c0, c1, y0, y1, g, mat, **kw):
            mesh.box(p(a0, c0, y0), p(a1, c1, y1), g, mat, **kw)

        for at in posts:
            box(frame, at - POST * 0.5, at + POST * 0.5, centre - POST * 0.5, centre + POST * 0.5, -0.05, WALL - PLATE, 1, FRAME)
        box(frame, a, b, centre - POST * 0.5, centre + POST * 0.5, WALL - PLATE, WALL, grain, FRAME)
        for pa, pb in zip(posts, posts[1:]):
            lo, hi = pa + POST * 0.5, pb - POST * 0.5
            cut = [o for o in openings if o[0] < hi - 0.01 and o[1] > lo + 0.01]
            if not any(o[2] < 0.01 for o in cut):       # no sill across a doorway
                box(frame, lo, hi, centre - 0.08, centre + 0.08, -0.04, 0.1, grain, FRAME)
            if not cut:
                box(frame, lo, hi, centre + out * 0.02, centre + out * 0.1, 0.95, 1.06, grain, FRAME)
            for o in cut:
                box(frame, lo - 0.02, hi + 0.02, centre - 0.08, centre + 0.08, o[3], o[3] + 0.12, grain, FRAME)
                if o[2] > 0.01:
                    box(frame, lo, hi, centre - 0.08, centre + 0.08, o[2] - 0.1, o[2], grain, FRAME)
        # boards
        inner = line - out * INNER
        edges = sorted({0.0, WALL - 0.02} | {y for o in openings for y in (o[2], o[3]) if 0.0 < y < WALL - 0.02})
        for ya, yb in zip(edges, edges[1:]):
            spans, x = [], a + POST
            for o in sorted(openings):
                if o[2] < ya + 1e-3 and o[3] > yb - 1e-3:
                    spans.append((x, o[0]))
                    x = o[1]
            spans.append((x, b - POST))
            for r0, r1 in walls.rows(ya, yb):
                for s0, s1 in spans:
                    if s1 - s0 < 0.05:
                        continue
                    for q0, q1 in walls.pieces(s0, s1):
                        d = rng.uniform(0.0, 0.005)
                        box(walls, q0 + 0.002, q1 - 0.002, inner - out * (BOARD + d), inner - out * d, r0, r1 - 0.003, grain, WOOD)
        if gable:   # vertical boards up to the roof, standing a little proud of the wall boards
            for r0, r1 in walls.rows(a + 0.02, b - 0.02, 0.16):
                d = rng.uniform(0.0, 0.005)
                c0, c1 = inner - out * (BOARD + d) + out * 0.045, inner - out * d + out * 0.045

                def skew(c, lift=rng.uniform(-0.01, 0.0)):
                    return [c[0], roof_plane(c[0]) - 0.03 + lift, c[2]] if c[1] > WALL else c

                walls.box(p(r0 + 0.002, c0, WALL - 0.07), p(r1 - 0.002, c1, WALL + 1.0), 1, WOOD, skew=skew)
        # a single course of rubble under the sill, outside
        if sill_stones:
            rubble, x = self.parts["Rubble"], a - 0.05
            while x < b + 0.05:
                n = min(rng.uniform(0.28, 0.5), b + 0.05 - x)
                if n < 0.12:
                    break
                mid = x + n * 0.5
                if not any(o[2] < 0.01 and o[0] - 0.05 < mid < o[1] + 0.05 for o in openings):
                    h = rng.uniform(0.17, 0.22)
                    c = p(mid, centre + out * rng.uniform(0.0, 0.05), -GROUND - 0.08 + h * 0.5)
                    rubble.stone(c, (n * 1.04, h, rng.uniform(0.26, 0.32)) if runs_x else (rng.uniform(0.26, 0.32), h, n * 1.04), yaw=0.0)
                x += n
        # colliders: solid runs between the openings, a header over each, a sill box under the hatch
        mid_c = line - out * WALL_BOX * 0.5
        x = a
        for o in sorted(openings):
            if o[0] - x > 0.02:
                self.box_collider(name, "Walls", p(x, mid_c - WALL_BOX * 0.5, -GROUND), p(o[0], mid_c + WALL_BOX * 0.5, WALL))
            self.box_collider(name + "_Header", "Walls", p(o[0], mid_c - WALL_BOX * 0.5, o[3]), p(o[1], mid_c + WALL_BOX * 0.5, WALL))
            if o[2] > 0.01:
                self.box_collider(name + "_Sill", "Walls", p(o[0], mid_c - WALL_BOX * 0.5, -GROUND), p(o[1], mid_c + WALL_BOX * 0.5, o[2]))
            x = o[1]
        if b - x > 0.02:
            self.box_collider(name, "Walls", p(x, mid_c - WALL_BOX * 0.5, -GROUND), p(b, mid_c + WALL_BOX * 0.5, WALL))
        if gable:
            # narrow enough to stay under the roof: at 0.85 m from the ridge the boards are 1.1 m above the plates
            self.box_collider(name + "_Gable", "Walls", p(a + 1.9, mid_c - WALL_BOX * 0.5, WALL), p(b - 1.9, mid_c + WALL_BOX * 0.5, WALL + 1.0))

    def shell(self):
        frame = self.parts["Frame"]
        self.wall("Wall_East", False, HX, 1, -HZ, HZ, EAST_POSTS, [HATCH])
        self.wall("Wall_West", False, -HX, -1, -HZ, HZ, WEST_POSTS, [(DELIVERY_DOOR[0], DELIVERY_DOOR[1], 0.0, DELIVERY_DOOR[2])])
        self.wall("Wall_South", True, -HZ, -1, -HX, HX, SOUTH_POSTS, [(KITCHEN_DOOR[0], KITCHEN_DOOR[1], 0.0, KITCHEN_DOOR[2])], gable=True)
        self.wall("Wall_North", True, HZ, 1, -HX, HX, NORTH_POSTS, gable=True)
        self.wall("Partition", True, PART_Z, -1, -HX, HX, PART_POSTS, [(INNER_DOOR[0], INNER_DOOR[1], 0.0, INNER_DOOR[2])],
                  sill_stones=False, gable=True)
        for z in TIES:
            frame.box((-HX + POST, WALL - PLATE, z - 0.08), (HX - POST, WALL - 0.01, z + 0.08), 0, FRAME)
        # knee braces at the outer corners, under the plates
        for x, dx in ((-HX + POST * 0.5, 1.0), (HX - POST * 0.5, -1.0)):
            for z, dz in ((-HZ + POST, 1.0), (HZ - POST, -1.0)):
                frame.beam((x - dx * 0.16, WALL - PLATE - 0.7, z), (x - dx * 0.16, WALL - PLATE, z + dz * 0.7), 0.09, 0.1, FRAME,
                           up=(1.0, 0.0, 0.0), extend=0.03)

    def floor(self):
        floor = self.parts["Floor"]
        for x0, x1 in floor.rows(-HX + INNER, HX - INNER, 0.16):
            for z0, z1 in floor.pieces(-HZ + INNER, HZ - INNER, 2.2, 3.8):
                d = self.rng.uniform(0.0, 0.004)
                floor.box((x0 + 0.002, -0.045, z0 + 0.002), (x1 - 0.002, -d, z1 - 0.002), 2, WOOD, skip=((1, 0),))
        self.walk.append({"name": "Floor", "centre": [0.0, -0.1, 0.0], "size": [HX * 2 - 0.1, 0.2, HZ * 2 - 0.1], "euler": [0.0, 0.0, 0.0]})
        # a plank doorstep outside each outer door
        steps = (("Step_Kitchen", (KITCHEN_DOOR[0] - 0.1, -HZ - 0.55), (KITCHEN_DOOR[1] + 0.1, -HZ - 0.02), 0),
                 ("Step_Delivery", (-HX - 0.6, DELIVERY_DOOR[0] - 0.05), (-HX - 0.02, DELIVERY_DOOR[1] + 0.05), 2))
        for name, lo, hi, grain in steps:
            floor.box((lo[0], -GROUND - 0.02, lo[1]), (hi[0], -0.075, hi[1]), grain, FRAME)
            self.walk.append({"name": name, "centre": [(lo[0] + hi[0]) * 0.5, -0.125, (lo[1] + hi[1]) * 0.5],
                              "size": [hi[0] - lo[0], 0.1, hi[1] - lo[1]], "euler": [0.0, 0.0, 0.0]})

    def roof(self):
        roof, frame, rng = self.parts["Roof"], self.parts["Frame"], self.rng
        length = (HZ + GABLE_OVERHANG) * 2.0
        slope = (HX + EAVE_OVERHANG) / COS
        peak = (0.0, roof_plane(0.0), 0.0)
        ridge = (0.0, 0.0, 1.0)
        for side in (-1, 1):
            down = (side * COS, -SIN, 0.0)
            normal = (side * SIN, COS, 0.0)
            for za, zb in roof.rows(-length * 0.5, length * 0.5, 0.17):   # boards seen from inside
                o = [peak[k] + down[k] * slope * 0.5 - normal[k] * 0.0125 + ridge[k] * (za + zb) * 0.5 for k in range(3)]
                roof.framed(o, down, normal, ridge, (slope * 0.5, 0.0125, (zb - za) * 0.5 - 0.002), 0, WOOD, skip=((1, 1),))
            n = 9
            for i in range(n):
                z = -HZ + POST * 0.5 + i * (2.0 * (HZ - POST * 0.5)) / (n - 1)
                o = [peak[k] + down[k] * (slope * 0.5 - 0.05) - normal[k] * 0.09 + ridge[k] * z for k in range(3)]
                frame.framed(o, down, normal, ridge, (slope * 0.5 - 0.05, 0.065, 0.045), 0, FRAME)
            for end in (-1, 1):   # barge boards
                o = [peak[k] + down[k] * (slope * 0.5 - 0.02) - normal[k] * 0.05 + ridge[k] * end * (length * 0.5 + 0.016) for k in range(3)]
                roof.framed(o, down, normal, ridge, (slope * 0.5 + 0.02, 0.09, 0.016), 0, FRAME)
            # shingles, course by course from the eave up
            exposure, shingle_len, thick = 0.31, 0.5, 0.022
            tilt = math.atan2(thick, exposure)
            ax = norm([down[k] * math.cos(tilt) + normal[k] * math.sin(tilt) for k in range(3)])
            ay = norm([normal[k] * math.cos(tilt) - down[k] * math.sin(tilt) for k in range(3)])
            courses = int((slope + 0.03) / exposure) + 1
            pale = [(rng.randrange(1, courses - 2), rng.uniform(-length * 0.5 + 0.5, length * 0.5 - 1.6), rng.randrange(1, 3), rng.uniform(0.5, 1.1))
                    for _ in range(3)]
            for c in range(courses):
                butt = slope + 0.03 - c * exposure
                if butt < 0.12:
                    break
                z = -length * 0.5 - 0.03 + rng.uniform(0.0, 0.1)
                while z < length * 0.5 + 0.03:
                    w = min(rng.uniform(0.15, MAX_BOARD), length * 0.5 + 0.03 - z)
                    if w < 0.05:
                        break
                    gap = rng.uniform(0.003, 0.009)
                    roll = rng.random()
                    # none inside the chimney
                    px = side * (butt - shingle_len * 0.5) * COS   # butt is measured down the slope from the ridge
                    in_chimney = CHIMNEY[0] + 0.05 < px < CHIMNEY[1] - 0.05 and CHIMNEY[2] + 0.05 < z + w * 0.5 < CHIMNEY[3] - 0.05
                    if roll > 0.006 and not in_chimney:
                        rag = rng.uniform(-0.03, 0.035) + (rng.uniform(0.03, 0.07) if roll < 0.03 else 0.0)
                        ln = min(shingle_len, butt + rag - 0.02)
                        mid = butt + rag - ln * 0.5
                        lift = rng.uniform(0.0, 0.006)
                        o = [peak[k] + down[k] * mid + normal[k] * (0.004 + lift + (ln * 0.5) * math.sin(tilt)) + ay[k] * thick * 0.5
                             + ridge[k] * (z + w * 0.5) for k in range(3)]
                        mat = SHINGLE
                        if any(pc <= c < pc + ph and pz <= z < pz + pw for pc, pz, ph, pw in pale) or rng.random() < 0.012:
                            mat = PALE
                        skew = rng.uniform(-0.012, 0.012)
                        ends = [(skew if (i >> 2) & 1 else -skew) if i & 1 else 0.0 for i in range(8)]
                        roof.framed(o, ax, ay, ridge, (ln * 0.5, thick * 0.5, (w - gap) * 0.5), 0, mat, ends, skip=((1, 0), (0, 0)))
                    z += w
            self.collider("Roof_%s" % ("West" if side < 0 else "East"), "Roof",
                          (side * HX * 0.5, WALL + HX * TAN * 0.5, 0.0), (HX / COS + 0.7, 0.2, HZ * 2.0 + 0.8),
                          (0.0, 0.0, -side * math.degrees(PITCH)))
        top = peak[1]
        frame.box((-0.06, top - 0.28, -HZ - GABLE_OVERHANG + 0.05), (0.06, top - 0.1, HZ + GABLE_OVERHANG - 0.05), 2, FRAME)
        for side in (-1, 1):
            down = (side * COS, -SIN, 0.0)
            normal = (side * SIN, COS, 0.0)
            for za, zb in roof.pieces(-length * 0.5 - 0.04, length * 0.5 + 0.04, 2.6, 3.8):
                o = [peak[k] + down[k] * 0.085 + normal[k] * (0.062 + (0.012 if side > 0 else 0.0)) + ridge[k] * (za + zb) * 0.5 for k in range(3)]
                roof.framed(o, ridge, normal, down, ((zb - za) * 0.5 - 0.004, 0.012, 0.1), 0, FRAME)

    # -- doors and hatch ---------------------------------------------------------------------------
    def leaf(self, hinge, along, out, width, height, planks=5, name="Leaf"):
        """A ledged plank door standing open: `along` runs from the hinge to the free edge, `out` is its face."""
        doors, iron, rng = self.parts["Doors"], self.parts["Iron"], self.rng
        up = (0.0, 1.0, 0.0)
        x = 0.01
        for _ in range(planks):
            w = (width - 0.02) / planks
            o = [hinge[k] + along[k] * (x + w * 0.5) + up[k] * (height * 0.5 + rng.uniform(-0.008, 0.008)) + out[k] * 0.018 for k in range(3)]
            doors.framed(o, up, along, out, (height * 0.5, w * 0.5 - 0.002, 0.018), 0, WOOD)
            x += w
        for y in (0.3, height - 0.3):
            o = [hinge[k] + along[k] * width * 0.5 + up[k] * y + out[k] * 0.052 for k in range(3)]
            doors.framed(o, along, up, out, (width * 0.5 - 0.03, 0.06, 0.016), 0, FRAME)
            o = [hinge[k] + along[k] * min(0.3, width * 0.4) + up[k] * y + out[k] * 0.072 for k in range(3)]
            iron.framed(o, along, up, out, (min(0.3, width * 0.4), 0.024, 0.004), 0, IRON, mode="iron")
        a = [hinge[k] + along[k] * 0.06 + up[k] * 0.37 + out[k] * 0.052 for k in range(3)]
        b = [hinge[k] + along[k] * (width - 0.06) + up[k] * (height - 0.37) + out[k] * 0.052 for k in range(3)]
        doors.beam(a, b, 0.1, 0.03, FRAME, up=out)
        c = [hinge[k] + along[k] * width * 0.5 + up[k] * height * 0.5 + out[k] * 0.02 for k in range(3)]
        self.collider(name, "Doors", c, (0.08, height, width - 0.06), (0.0, math.degrees(math.atan2(along[0], along[2])), 0.0))

    def doors(self):
        doors, iron, frame, furniture = self.parts["Doors"], self.parts["Iron"], self.parts["Frame"], self.parts["Furniture"]

        def swing(base_deg, open_deg):
            a = math.radians(base_deg + open_deg)
            return (math.sin(a), 0.0, math.cos(a))

        def face(along):
            return (along[2], 0.0, -along[0])

        y0 = 0.02
        # kitchen door: hinged on the east jamb, standing open into the porch
        along = swing(180.0, -12.0)
        # hung on the jamb post's outer face, 6 cm clear of the opening, like the inner door
        self.leaf((KITCHEN_DOOR[1] + 0.06, y0, -HZ - 0.03), along, face(along), KITCHEN_DOOR[1] - KITCHEN_DOOR[0], KITCHEN_DOOR[2] - 0.06, name="Leaf_Kitchen")
        # inner door: hinged on the east jamb, standing open into the store against the east wall
        along = swing(0.0, 8.0)
        self.leaf((INNER_DOOR[1] + 0.06, y0, PART_Z + POST + BOARD + 0.02), along, face(along), INNER_DOOR[1] - INNER_DOOR[0], INNER_DOOR[2] - 0.06,
                  name="Leaf_Store")
        # delivery door: two leaves standing open against the west wall
        half = (DELIVERY_DOOR[1] - DELIVERY_DOOR[0]) * 0.5
        along = swing(180.0, 14.0)
        self.leaf((-HX - 0.03, y0, DELIVERY_DOOR[0]), along, face(along), half, DELIVERY_DOOR[2] - 0.06, planks=4, name="Leaf_Delivery_S")
        along = swing(0.0, -14.0)
        self.leaf((-HX - 0.03, y0, DELIVERY_DOOR[1]), along, face(along), half, DELIVERY_DOOR[2] - 0.06, planks=4, name="Leaf_Delivery_N")

        # serving hatch: counter through the wall, shutter hinged at the head and propped open like an awning
        z0, z1, sill, head = HATCH
        for (xa, xb) in ((HX - INNER - 0.3, HX - 0.12), (HX - 0.12, HX + 0.42)):
            furniture.box((xa, sill, z0 + 0.015), (xb - 0.004, sill + 0.05, z1 - 0.015), 2, WOOD)
        for z in (z0 + 0.25, (z0 + z1) * 0.5, z1 - 0.25):
            frame.beam((HX + 0.01, sill - 0.38, z), (HX + 0.36, sill - 0.005, z), 0.06, 0.06, FRAME, up=(0.0, 0.0, 1.0))
        self.box_collider("Counter", "Furniture", (HX - INNER - 0.3, -GROUND, z0), (HX + 0.42, sill + 0.05, z1))
        self.anchor("counterTop", HX - 0.2, sill + 0.05, (z0 + z1) * 0.5)
        tilt = math.radians(18.0)
        s = (math.cos(tilt), -math.sin(tilt), 0.0)
        n = (math.sin(tilt), math.cos(tilt), 0.0)
        hinge = (HX + 0.03, head + 0.03, 0.0)
        length = head - sill + 0.05
        zs = doors.rows(z0 - 0.03, z1 + 0.03, 0.16)
        for za, zb in zs:
            o = [hinge[k] + s[k] * length * 0.5 + n[k] * 0.016 for k in range(3)]
            o[2] = (za + zb) * 0.5
            doors.framed(o, s, n, (0.0, 0.0, 1.0), (length * 0.5 + self.rng.uniform(-0.01, 0.01), 0.016, (zb - za) * 0.5 - 0.002), 0, WOOD)
        for t in (0.18, 0.86):
            o = [hinge[k] + s[k] * length * t - n[k] * 0.016 for k in range(3)]
            o[2] = (z0 + z1) * 0.5
            doors.framed(o, (0.0, 0.0, 1.0), n, s, ((z1 - z0) * 0.5, 0.016, 0.05), 0, FRAME)
        for z in (z0 + 0.3, z1 - 0.3):
            o = [hinge[k] + s[k] * 0.3 + n[k] * 0.036 for k in range(3)]
            o[2] = z
            iron.framed(o, s, n, (0.0, 0.0, 1.0), (0.3, 0.004, 0.024), 0, IRON, mode="iron")
        for z in (z0 + 0.08, z1 - 0.08):
            tip = [hinge[k] + s[k] * (length - 0.06) - n[k] * 0.03 for k in range(3)]
            doors.log((HX + 0.38, sill + 0.05, z), (tip[0], tip[1], z), 0.022, FRAME, sides=6, rings=2, wobble=0.0)
        centre = [hinge[k] + s[k] * length * 0.5 for k in range(3)]
        centre[2] = (z0 + z1) * 0.5
        self.collider("Shutter", "Doors", centre, (length, 0.06, z1 - z0),
                      (0.0, 0.0, -math.degrees(tilt)))

        # lantern bracket on the hatch's north jamb post, and the tally board beside it
        bz, by = EAST_POSTS[3], 2.2
        iron.box((HX, by - 0.012, bz - 0.012), (HX + 0.42, by + 0.012, bz + 0.012), 0, IRON, mode="iron")
        iron.beam((HX, by - 0.24, bz), (HX + 0.27, by - 0.01, bz), 0.018, 0.018, IRON, up=(0.0, 0.0, 1.0))
        iron.box((HX + 0.37, by - 0.07, bz - 0.008), (HX + 0.386, by, bz + 0.008), 1, IRON, mode="iron")
        self.lanterns.append({"name": "HatchLantern", "position": [round(HX + 0.378, 3), round(by - 0.07 - 0.27, 3), round(bz, 3)],
                              "out": [1.0, 0.0, 0.0]})
        tx = HX - INNER
        for ya, yb in furniture.rows(1.2, 1.85, 0.16):
            furniture.box((tx, ya, 1.35), (tx + 0.025, yb - 0.003, 2.25), 2, FRAME)

    # -- hearth ------------------------------------------------------------------------------------
    def masonry(self, lo, hi):
        """Rough rubble laid in courses filling a box: a plain core, faced with separate stones."""
        mesh, rng = self.parts["Hearth"], self.rng
        mesh.box([lo[i] + 0.05 for i in range(3)], [hi[i] - 0.05 for i in range(3)], 0, RUBBLE, mode="stone")
        along_x = (hi[0] - lo[0]) >= (hi[2] - lo[2])
        la, lb = (lo[0], hi[0]) if along_x else (lo[2], hi[2])
        ca, cb = (lo[2], hi[2]) if along_x else (lo[0], hi[0])
        y = lo[1]
        while hi[1] - y > 0.03:
            h = rng.uniform(0.15, 0.22)
            if hi[1] - y - h < 0.1:
                h = hi[1] - y
            rows = [(ca, cb)] if cb - ca < 0.5 else [(ca, ca + (cb - ca) * rng.uniform(0.42, 0.58)), None]
            if rows[-1] is None:
                rows[-1] = (rows[0][1], cb)
            for r0, r1 in rows:
                x = la
                while lb - x > 0.02:
                    n = rng.uniform(0.24, 0.44)
                    if lb - x - n < 0.14:
                        n = lb - x
                    c_along, c_across = x + n * 0.5, (r0 + r1) * 0.5
                    size = (n * 1.03, h * 1.03, (r1 - r0) * 1.03)
                    centre = (c_along, y + h * 0.5, c_across) if along_x else (c_across, y + h * 0.5, c_along)
                    mesh.stone(centre, size, yaw=0.0 if along_x else math.pi * 0.5)
                    x += n
            y += h

    def hearth(self):
        mesh, frame, iron = self.parts["Hearth"], self.parts["Frame"], self.parts["Iron"]
        x0, x1, z0, z1 = HEARTH
        bench = 0.42
        self.masonry((x0, -0.02, z0), (x1, bench, z1))                      # the raised fire bench
        for a, b in ((x0, x0 + 0.34), (x1 - 0.34, x1)):                       # cheeks carrying the hood
            self.masonry((a, bench, z0 + 0.25), (b, 1.42, z1))
        self.masonry((x0 + 0.34, bench, z1 - 0.16), (x1 - 0.34, 1.42, z1))   # fire back
        frame.box((x0 - 0.04, 1.42, z0 + 0.2), (x1 + 0.04, 1.56, z0 + 0.36), 0, FRAME)   # mantel beam
        # the hood: clay over rubble, tapering to the chimney
        c0, c1, cz0, cz1 = CHIMNEY
        lo_y, hi_y = 1.56, 2.35
        corners, local = [], []
        for i in range(8):
            top = (i >> 1) & 1
            x = (c1 if i & 1 else c0) if top else (x1 if i & 1 else x0)
            z = (cz1 if (i >> 2) & 1 else cz0) if top else (z1 if (i >> 2) & 1 else z0 + 0.2)
            y = hi_y if top else lo_y
            corners.append([x, y, z])
            local.append([x - (x0 + x1) * 0.5, y - (lo_y + hi_y) * 0.5, z - (z0 + z1) * 0.5])
        mesh.hexa(corners, local, 0, RUBBLE, mode="stone")
        self.masonry((c0, hi_y, cz0), (c1, CHIMNEY_TOP, cz1))
        # pot hook: a bar across the cheeks and a hanging hook
        iron.box((x0 + 0.2, 1.3, (z0 + z1) * 0.5 + 0.1), (x1 - 0.2, 1.325, (z0 + z1) * 0.5 + 0.125), 0, IRON, mode="iron")
        hx = (x0 + x1) * 0.5
        iron.box((hx - 0.008, 0.95, (z0 + z1) * 0.5 + 0.105), (hx + 0.008, 1.3, (z0 + z1) * 0.5 + 0.12), 1, IRON, mode="iron")
        self.box_collider("Hearth", "Furniture", (x0, -GROUND, z0), (x1, 1.56, z1))
        self.box_collider("Chimney", "Furniture", (c0, 1.56, cz0), (c1, CHIMNEY_TOP, cz1))
        self.anchor("hearthPot", hx, 0.6, (z0 + z1) * 0.5 + 0.11)
        self.lights.append({"name": "HearthFire", "position": [round(hx, 3), 0.75, round(z0 + 0.35, 3)]})

    # -- furniture ---------------------------------------------------------------------------------
    def furniture(self):
        f, iron, frame = self.parts["Furniture"], self.parts["Iron"], self.parts["Frame"]
        # work table in the middle of the kitchen
        cx, cz, hw, hd, top = 0.15, -1.9, 0.9, 0.4, 0.84
        for z0, z1 in f.rows(cz - hd, cz + hd, 0.17):
            f.box((cx - hw, top - 0.06, z0 + 0.002), (cx + hw, top, z1 - 0.002), 0, WOOD)
        for sx in (-1, 1):
            for sz in (-1, 1):
                f.box((cx + sx * (hw - 0.14), -0.0, cz + sz * (hd - 0.1) - 0.05), (cx + sx * (hw - 0.04), top - 0.06, cz + sz * (hd - 0.1) + 0.05), 1, FRAME)
            f.box((cx + sx * (hw - 0.12), 0.22, cz - hd + 0.12), (cx + sx * (hw - 0.06), 0.3, cz + hd - 0.12), 2, FRAME)
        f.box((cx - hw + 0.1, 0.23, cz - 0.04), (cx + hw - 0.1, 0.29, cz + 0.04), 0, FRAME)
        self.box_collider("WorkTable", "Furniture", (cx - hw, -GROUND, cz - hd), (cx + hw, top, cz + hd))
        self.anchor("workTable", cx, top, cz)
        # two shelves on the kitchen's west wall
        wx = -HX + INNER + BOARD
        for y in (1.1, 1.55):
            f.box((wx, y, -2.45), (wx + 0.3, y + 0.035, -0.95), 2, WOOD)
            for z in (-2.3, -1.1):
                f.beam((wx + 0.01, y - 0.26, z), (wx + 0.27, y - 0.005, z), 0.04, 0.04, FRAME, up=(0.0, 0.0, 1.0))
        self.anchor("kitchenShelf", wx + 0.15, 1.135, -1.7, 1.585, 1.5)   # x, low y, z centre, high y, length
        # store: shelves along the north wall, floor to head height
        nz = HZ - INNER - BOARD
        sx0, sx1, depth = -1.9, 2.45, 0.45
        levels = (0.32, 0.82, 1.32, 1.82)
        for x in (sx0, (sx0 + sx1) * 0.5, sx1):
            for z in (nz - depth + 0.035, nz - 0.035):
                f.box((x - 0.035, 0.0, z - 0.035), (x + 0.035, 1.95, z + 0.035), 1, FRAME)
        for y in levels:
            for z0, z1 in f.rows(nz - depth, nz, 0.2):
                f.box((sx0 - 0.06, y, z0 + 0.002), (sx1 + 0.06, y + 0.035, z1 - 0.002), 0, WOOD)
        self.box_collider("StoreShelves", "Furniture", (sx0 - 0.06, -GROUND, nz - depth), (sx1 + 0.06, 1.95, nz))
        self.anchor("storeShelves", sx0, sx1, nz - depth * 0.5, *[y + 0.035 for y in levels])
        # hooks under the store's tie beam
        hooks = []
        for x in (-0.7, 0.0, 0.7):
            iron.box((x - 0.006, WALL - PLATE - 0.16, TIES[1] - 0.006), (x + 0.006, WALL - PLATE, TIES[1] + 0.006), 1, IRON, mode="iron")
            hooks += [x, WALL - PLATE - 0.16, TIES[1]]
        self.anchor("hooks", *hooks)
        # a rail of pegs over the hearth side for pans
        frame.box((-HX + INNER + BOARD, 1.9, -0.6), (-HX + INNER + BOARD + 0.03, 1.98, 0.6), 2, FRAME)

    # -- lean-to roofs -----------------------------------------------------------------------------
    def lean_roof(self, name, x0, x1, z_high, z_low, y_high, y_low, rafter_step=0.75):
        """Rough boards on pole rafters, falling from z_high to z_low; y values are the boards' underside."""
        roof, frame, rng = self.parts["Shelters"], self.parts["Frame"], self.rng
        d = norm((0.0, y_low - y_high, z_low - z_high))
        n = norm(cross(d, (1.0, 0.0, 0.0)))
        if n[1] < 0:
            n = [-c for c in n]
        over_low, over_high = 0.3, 0.12
        run = math.sqrt((y_low - y_high) ** 2 + (z_low - z_high) ** 2)
        length = run + over_low + over_high
        start = (0.0, y_high - d[1] * over_high, z_high - d[2] * over_high)
        count = int((x1 - x0 - 0.3) / rafter_step) + 1
        for i in range(count + 1):
            x = x0 + 0.15 + i * (x1 - x0 - 0.3) / count
            a = (x, start[1] - n[1] * 0.055, start[2] - n[2] * 0.055)
            b = (x, a[1] + d[1] * length, a[2] + d[2] * length)
            frame.log(a, b, 0.05, FRAME, sides=6, rings=4, wobble=0.01)
        k = 0
        for xa, xb in roof.rows(x0 - 0.15, x1 + 0.15, 0.16):
            lift = 0.022 if k % 2 else 0.0
            rag_low, rag_high = rng.uniform(-0.12, 0.05), rng.uniform(-0.03, 0.03)
            ln = length + rag_low + rag_high
            mid = -rag_high + ln * 0.5
            o = [(xa + xb) * 0.5, start[1] + d[1] * mid + n[1] * (0.012 + lift), start[2] + d[2] * mid + n[2] * (0.012 + lift)]
            w = (xb - xa) * 0.5 + (0.012 if k % 2 else -0.004)
            roof.framed(o, d, n, (1.0, 0.0, 0.0), (ln * 0.5, 0.012, w), 0, WOOD)
            k += 1
        for t in (0.3, 0.72):   # bark poles holding the boards down
            c = [0.0, start[1] + d[1] * length * t + n[1] * 0.1, start[2] + d[2] * length * t + n[2] * 0.1]
            roof.log((x0 - 0.3, c[1], c[2]), (x1 + 0.3, c[1], c[2]), 0.055, FRAME, sides=7, rings=6, wobble=0.015)
        mid_y, mid_z = (y_high + y_low) * 0.5, (z_high + z_low) * 0.5
        pitch = math.degrees(math.atan2(y_high - y_low, abs(z_high - z_low))) * (1.0 if z_low > z_high else -1.0)
        self.collider(name, "Roof", (0.5 * (x0 + x1), mid_y + 0.05, mid_z), (x1 - x0 + 0.3, 0.14, length), (pitch, 0.0, 0.0))

    def post(self, name, x, z, top, radius=0.085):
        frame = self.parts["Frame"]
        frame.log((x, -GROUND - 0.3, z), (x, top, z), radius, FRAME, sides=8, rings=5, wobble=0.01)
        self.box_collider(name, "Structure", (x - radius, -GROUND, z - radius), (x + radius, top, z + radius))

    def porch(self):
        frame, walls, rng = self.parts["Frame"], self.parts["Walls"], self.rng
        z_low = -HZ - PORCH_DEPTH
        plate_y = PORCH_LOW - 0.11
        for i, x in enumerate((-2.62, -0.9, 0.9, 2.62)):
            self.post("PorchPost_%d" % i, x, z_low + 0.1, plate_y - 0.07)
        frame.log((-HX - 0.2, plate_y, z_low + 0.1), (HX + 0.2, plate_y, z_low + 0.1), 0.075, FRAME, sides=8, rings=6)
        frame.box((-HX + 0.05, PORCH_HIGH - 0.2, -HZ - 0.07), (HX - 0.05, PORCH_HIGH - 0.06, -HZ), 0, FRAME)   # ledger on the gable
        self.lean_roof("Roof_Porch", -HX, HX, -HZ - 0.02, z_low, PORCH_HIGH, PORCH_LOW)
        # wind wall on the west side: vertical boards on two rails
        wx = -HX + 0.06
        for y in (0.45, 1.5):
            frame.box((wx + 0.02, y, z_low + 0.15), (wx + 0.09, y + 0.09, -HZ - 0.02), 2, FRAME)
        for za, zb in walls.rows(z_low + 0.12, -HZ - 0.02, 0.16):
            t = (za - z_low) / PORCH_DEPTH
            top = PORCH_LOW + (PORCH_HIGH - PORCH_LOW) * t - 0.16 + rng.uniform(-0.05, 0.0)
            walls.box((wx - 0.025, -GROUND - 0.03, za + 0.003), (wx + 0.02, top, zb - 0.003), 1, WOOD)
        self.box_collider("WindWall", "Walls", (wx - 0.06, -GROUND, z_low + 0.1), (wx + 0.08, PORCH_LOW - 0.1, -HZ))
        # chopping block at the porch's open east end
        bx, bz = 1.55, z_low + 0.75
        frame.log((bx, -GROUND - 0.05, bz), (bx, -GROUND + 0.45, bz), 0.24, FRAME, sides=10, rings=2, wobble=0.0)
        self.box_collider("ChoppingBlock", "Furniture", (bx - 0.22, -GROUND, bz - 0.22), (bx + 0.22, -GROUND + 0.45, bz + 0.22))
        self.anchor("choppingBlock", bx, -GROUND + 0.45, bz)
        self.anchor("station", -0.85, -GROUND, -HZ - 1.6)
        # the tripod fire has no light of its own: the third warm light of the night view
        self.lights.append({"name": "PorchFire", "position": [-0.85, round(-GROUND + 0.45, 3), round(-HZ - 1.6, 3)]})
        self.anchor("porch", -HX, HX, z_low, -HZ)

    def mess(self):
        frame = self.parts["Frame"]
        x0, x1, z0, z1 = MESS
        high, low = MESS_HIGH - GROUND, MESS_LOW - GROUND
        for row, (z, top) in enumerate(((z0 + 0.12, high - 0.18), (z1 - 0.12, low - 0.18))):
            for i, x in enumerate((x0 + 0.1, (x0 + x1) * 0.5, x1 - 0.1)):
                self.post("MessPost_%d%d" % (row, i), x, z, top - 0.08, 0.09)
            frame.log((x0 - 0.25, top, z), (x1 + 0.25, top, z), 0.08, FRAME, sides=8, rings=7)
        self.lean_roof("Roof_Mess", x0, x1, z0 + 0.12, z1 - 0.12, high - 0.045, low - 0.045)
        for x in (x0 + 0.1, x1 - 0.1):   # a brace at each end, post to plate
            frame.beam((x, high - 1.0, z0 + 0.2), (x, high - 0.3, z0 + 0.95), 0.07, 0.07, FRAME, up=(1.0, 0.0, 0.0))

    # -- table set (its own object, at its own origin: the builder places copies) ------------------
    def table_set(self):
        t = self.parts["TableSet"]
        half, top = 1.1, 0.76
        for z0, z1 in t.rows(-0.38, 0.38, 0.17):
            ln = half + self.rng.uniform(-0.03, 0.03)
            t.box((-ln, top - 0.045, z0 + 0.003), (ln, top, z1 - 0.003), 0, WOOD)
        for x in (-0.8, 0.8):
            t.box((x - 0.04, top - 0.125, -0.34), (x + 0.04, top - 0.045, 0.34), 2, FRAME)
            for s in (-1, 1):
                t.beam((x, top - 0.125, s * 0.2), (x, 0.0, s * 0.36), 0.07, 0.06, FRAME, up=(1.0, 0.0, 0.0), extend=0.02)
            t.box((x - 0.03, 0.26, -0.3), (x + 0.03, 0.32, 0.3), 2, FRAME)
        t.box((-0.8, 0.265, -0.03), (0.8, 0.315, 0.03), 0, FRAME)
        for s in (-1, 1):
            z = s * 0.68
            t.box((-half + 0.02, 0.4, z - 0.13), (half - 0.02, 0.44, z + 0.13), 0, WOOD)
            for x in (-0.8, 0.8):
                t.box((x - 0.03, 0.0, z - 0.11), (x + 0.03, 0.4, z + 0.11), 1, FRAME)

    # -- back alley --------------------------------------------------------------------------------
    def yard(self):
        yard, rng = self.parts["Yard"], self.rng
        ground = -GROUND

        def panel(a, b, hinge=None, swing=0.0, name="Fence"):
            """Stakes woven on two rails from a to b along x; a gate leaf is the same thing turned round a hinge."""
            cs, sn = math.cos(swing), math.sin(swing)

            def at(x, y, dz=0.0):
                if hinge is None:
                    return (x, y, FENCE_Z + dz)
                r = x - hinge
                return (hinge + r * cs - dz * sn, y, FENCE_Z + r * sn + dz * cs)

            lift = 0.0 if hinge is None else 0.08
            for y in (0.4, 1.05):
                yard.log(at(a, ground + y + lift), at(b, ground + y + lift), 0.032, FRAME, sides=6, rings=3, wobble=0.008)
            x, k = a + 0.05, 0
            while x < b - 0.03:
                h = rng.uniform(1.25, 1.5)
                dz = 0.04 if k % 2 else -0.04
                foot = at(x, ground - (0.2 if hinge is None else -lift), dz)
                tip = at(x + rng.uniform(-0.03, 0.03), ground + h, dz)
                yard.log(foot, tip, rng.uniform(0.027, 0.036), FRAME, sides=5, rings=3, wobble=0.006)
                x += rng.uniform(0.1, 0.15)
                k += 1
            c = at((a + b) * 0.5, ground + 0.75)
            self.collider(name, "Fence", c, (b - a, 1.5, 0.14), (0.0, -math.degrees(swing), 0.0))

        a, b = ALLEY_X
        gate = 1.3
        panel(a, a + 0.45)
        panel(b - 0.45, b)
        for i, x in enumerate((a + 0.45, b - 0.45)):
            yard.log((x, ground - 0.3, FENCE_Z), (x, ground + 1.75, FENCE_Z), 0.07, FRAME, sides=8, rings=4)
            self.box_collider("GatePost_%d" % i, "Fence", (x - 0.07, ground, FENCE_Z - 0.07), (x + 0.07, ground + 1.75, FENCE_Z + 0.07))
        # two leaves standing open: the west one into the alley, along the huts (the lane's ground rises north of
        # the fence and would bury it), the east one out toward the lane, clear of the delivery door's leaves
        panel(a + 0.52, a + 0.52 + gate, hinge=a + 0.45, swing=math.radians(-100.0), name="Gate_W")
        panel(b - 0.52 - gate, b - 0.52, hinge=b - 0.45, swing=math.radians(-82.0), name="Gate_E")
        # fish rack at the far end of the alley
        rx0, rx1, rz, top = a + 0.9, a + 2.5, -HZ - 2.2, ground + 1.75
        for x in (rx0, rx1):
            yard.log((x, ground - 0.25, rz), (x, top + 0.1, rz), 0.04, FRAME, sides=6, rings=4)
        yard.log((rx0 - 0.15, top, rz), (rx1 + 0.15, top, rz), 0.03, FRAME, sides=6, rings=4)
        self.box_collider("FishRack", "Fence", (rx0 - 0.05, ground, rz - 0.06), (rx1 + 0.05, top, rz + 0.06))
        self.anchor("fishRack", rx0 + 0.2, rx1 - 0.2, top - 0.03, rz)
        self.anchor("alley", a, b, -HZ - PORCH_DEPTH, FENCE_Z)

    def build(self):
        self.shell()
        self.floor()
        self.roof()
        self.doors()
        self.hearth()
        self.furniture()
        self.porch()
        self.mess()
        self.table_set()
        self.yard()
        # the store room, inside its walls: the restricted volume
        z0, z1 = PART_Z + POST + BOARD, HZ - INNER - BOARD
        # it reaches 0.5 m under the floor: trespass tests the player's feet, which rest exactly on the floor's plane
        self.anchor("storeBox", 0.0, WALL * 0.5 - 0.25, (z0 + z1) * 0.5, (HX - INNER - BOARD) * 2.0, WALL + 0.5, z1 - z0)
        self.anchor("bed", -1.55, 0.0, -HZ + INNER + BOARD + 0.6)
        self.anchor("behindCounter", HX - INNER - 0.75, 0.0, (HATCH[0] + HATCH[1]) * 0.5)
        self.anchor("frontOfHatch", HX + 1.7, -GROUND, HATCH[1] + 0.9)
        self.anchor("byTheHearth", HEARTH[1] + 0.9, 0.0, -0.6)   # between the hearth and the store's door
        self.anchor("storeDoor", -HX, DELIVERY_DOOR[0], DELIVERY_DOOR[1])
        self.anchor("storeFloor", -HX + INNER + BOARD, HX - INNER - BOARD, z0, z1)
        self.anchor("kitchenFloor", -HX + INNER + BOARD, HX - INNER - BOARD, -HZ + INNER + BOARD, PART_Z)
        self.anchor("hatch", *HATCH)
        # openings, for the tests: from, to, head, then the wall's line (x for the delivery door, z for the others)
        self.anchor("kitchenDoor", KITCHEN_DOOR[0], KITCHEN_DOOR[1], KITCHEN_DOOR[2], -HZ + POST * 0.5)
        self.anchor("innerDoor", INNER_DOOR[0], INNER_DOOR[1], INNER_DOOR[2], PART_Z + (POST + BOARD) * 0.5)
        self.anchor("deliveryDoor", DELIVERY_DOOR[0], DELIVERY_DOOR[1], DELIVERY_DOOR[2], -HX + POST * 0.5)


def main():
    out = os.path.dirname(_HERE)
    house = Cookhouse()
    house.build()

    name = "Cookhouse"
    old = bpy.data.collections.get(name)
    kept = set(old.objects) if old else set()
    for part in house.parts:   # checked before anything is deleted
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
    objects = [mesh.to_object(part, mats, col) for part, mesh in house.parts.items()]
    export(objects, os.path.join(out, name + ".fbx"))
    triangles = {part: mesh.triangles() for part, mesh in house.parts.items()}
    tables = [{"position": [round(x - CENTRE[0], 3), -GROUND, round(z - CENTRE[1], 3)], "yaw": yaw} for x, z, yaw in TABLES]
    parts = {"centre": list(CENTRE), "floorAboveGround": GROUND, "triangles": triangles,
             "total": sum(triangles.values()) + triangles["TableSet"] * (len(TABLES) - 1),
             "objects": list(house.parts), "walk": house.walk, "colliders": house.colliders, "lanterns": house.lanterns,
             "lights": house.lights, "tables": tables, "anchors": house.anchors}
    with open(os.path.join(out, name + ".json"), "w") as f:
        json.dump(parts, f, indent=1)
    for o in objects:
        o.name = name + "_" + o.name
    bpy.data.libraries.write(os.path.join(_HERE, name + ".blend"), set(objects) | set(mats), fake_user=True)
    return {"triangles": triangles, "total": parts["total"]}


if __name__ == "__main__":
    result = main()
    print("cookhouse:", result)
