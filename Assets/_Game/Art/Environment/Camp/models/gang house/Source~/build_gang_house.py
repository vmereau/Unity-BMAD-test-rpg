"""Area 1 gang house: the lodge on the lake shore (one room on a rubble base a storey high on the lake side), its
camp ramp, the balcony and its lean-to roof, the deck on piles at the base's foot, the walkway down the east end,
the pier and the net frame, built piece by piece.

Run in Blender (5.x):  blender -b -P build_gang_house.py
or from a session:      runpy.run_path(r"...\\build_gang_house.py", run_name="__main__")

Writes  ../GangHouse.fbx, ../GangHouse.json (colliders, walk surfaces, Not Walkable volumes, lights, the ground
        rectangle to lower and the anchors GangHouseBuilder and the tests read) and GangHouse.blend (the built
        objects only: this script is the source).

The mesh helpers, the materials and the FBX export are the warehouse's: this script loads
../../warehouse/Source~/build_warehouse.py as a library (Camp/CLAUDE.md -> One wood, by construction).

Frame (the prefab's, which stands turned 205 degrees in the scene): x along the house toward its west end (the
chimney), y up, z toward the camp (-z is the lake); origin on the main floor's top at the house's centre. Nothing
is fitted to a ground capture: the base, the piles and the ramp's feet are modelled deeper than the ground, which
the heights below describe (measured on the site, GANG_HOUSE_CONCEPTS.md -> Build brief).
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
WOOD, FRAME, SHINGLE, PALE, RUBBLE, IRON, ROPE, TAR, EARTH = (_LIB[n] for n in ("WOOD", "FRAME", "SHINGLE", "PALE", "RUBBLE", "IRON", "ROPE", "TAR", "EARTH"))
MAX_BOARD = _LIB["MAX_BOARD"]

# --- Build brief ----------------------------------------------------------------------------------
CENTRE = (43.5, 53.5)                # world x, z of the origin (layout: gang-house)
YAW = 205.0                          # the root's turn in the scene
FLOOR_WORLD_Y = 2.15                 # the main floor's top
HX, HZ = 4.5, 3.0                    # half footprint: 9 x 6 m, long axis along x
WALL = 2.6                           # top of the wall plates above the floor
PITCH = math.radians(34.0)           # ridge along x at 4.6 above the floor (world y 6.8)
EAVE_OVERHANG, GABLE_OVERHANG = 0.3, 0.35
CAMP_DOOR = (-2.55, -1.45, 2.1)      # camp wall (+z): x from, x to, head
LAKE_DOOR = (0.55, 1.65, 2.1)        # lake wall (-z), onto the balcony
CAMP_GROUND = -1.55                  # the flat ground in front of the house (world y 0.6)
DECK_Y = -2.65                       # deck, pier and basement floor (world y -0.5)
WATER = -4.15                        # world y -2
BASE_OUT = 0.05                      # the rubble base stands this far proud of the footprint
BASE_WALL = 0.6
BASE_TOP = -0.3                      # under the floor's joists
RAMP = (1.4, 4.3)                    # camp ramp: width, plan length
BALCONY_Z = -4.5                     # its outer edge
LEAN = (2.3, 2.02)                   # underside of the balcony roof's boards: at the wall, at the posts
DECK = (-5.8, 4.8, -6.5)             # deck: min x, max x, lake edge (its camp edge is the base)
WALKWAY = (-5.8, -4.65, 0.6, -3.6)   # east end: min x, max x, head z (on the ground), foot z (on the deck)
WALKWAY_HEAD_Y = -1.85               # the ground at its head (world y 0.3)
PIER = (0.0, 2.4, -15.0)             # min x, max x, far end
NETS = (0.9, 3.3, -4.0)              # net frame in front of the hidden door: min x, max x, z
HIDDEN_DOOR = (1.45, 2.59, 2.14)     # lake side of the base, at deck level: min x, max x, height (the door prefab's leaf is 1.1 m wide)
DOOR_SCALE_Y = 1.06                  # Poor small door is 2.01 m tall
GROUND_CUT = (-6.3, 5.3, -7.5, -3.0, -3.05)   # the bank under the deck is lowered: min x, max x, min z, max z, to y (world -0.9)
# ... and the ground under the basement's lake side with it, inside the footprint only (no bank round it: a wider
# cut lowers the ground under the gang fence, and its panels with it, into the tunnel):
GROUND_CUT_INSIDE = (-4.4, 4.4, -3.0, -1.2, -3.05)
# --- the basement (under a terrain hole) and the tunnel
ROOM = (-3.0, 3.0, -2.4, 1.5)        # inside faces: min x, max x, min z (the lake wall), max z. The camp wall and both
                                     # end walls are 1.5 m thick: they hide the ragged edge of the terrain hole
HOLE = (-4.45, -2.95, 4.45, 2.95)    # terrain hole cells must lie wholly inside: min x, min z, max x, max z (this frame, turned)
TUNNEL_WORLD_Y = -1.7                # its floor under the gang fence's footings and into the room (the water is at -2):
                                     # the fence's logs end at world y 0.65 there, the tunnel's roof 0.62
TUNNEL_W, TUNNEL_H = 1.3, 2.1        # clear width between the lagging, clear height under the caps
STEP = 0.2                           # the well's risers, from the tunnel's floor up to the room's
TREAD = 0.45
# the tunnel's line on the floor, in the WORLD (x, z, floor y): from the outer face of the warehouse cellar's side
# room (its south wall), east under the terrace, north under the wilderness gateway, east again into the west wall
TUNNEL_PATH = ((22.3, 52.3, 0.1), (22.3, 51.6, 0.1), (23.0, 51.6, 0.1), (30.0, 51.6, -1.55), (32.4, 51.6, TUNNEL_WORLD_Y),
               (32.4, 54.2, TUNNEL_WORLD_Y))   # ... and on along +x to the room's west face
FRAME_STEP = 1.8
WALL_BOX = 0.24

# --- Construction ---------------------------------------------------------------------------------
POST = 0.2
BOARD = 0.04
INNER = POST
PLATE = 0.16
TAN, COS, SIN = math.tan(PITCH), math.cos(PITCH), math.sin(PITCH)
CAMP_POSTS = (-4.4, -2.65, -1.35, 0.6, 2.5, 4.4)
LAKE_POSTS = (-4.4, -2.2, 0.45, 1.75, 3.2, 4.4)
GABLE_POSTS = (-2.9, -1.0, 1.0, 2.9)
TIES = (-1.5, 1.5)
HEARTH = (3.25, 4.3, -0.8, 0.8)      # against the west gable: min x, max x, min z, max z
CHIMNEY = (3.6, 4.3, -0.4, 0.4)
CHIMNEY_TOP = 5.3                    # world y 7.45 (limit 10)
BALCONY_POSTS = (-4.35, -1.45, 1.45, 4.35)
X, Y, Z = (1.0, 0.0, 0.0), (0.0, 1.0, 0.0), (0.0, 0.0, 1.0)


_COS_YAW, _SIN_YAW = math.cos(math.radians(YAW)), math.sin(math.radians(YAW))


def to_local(x, y, z):
    """A world point in the prefab's frame."""
    dx, dz = x - CENTRE[0], z - CENTRE[1]
    return [dx * _COS_YAW - dz * _SIN_YAW, y - FLOOR_WORLD_Y, dx * _SIN_YAW + dz * _COS_YAW]


def roof_plane(z):
    """Top of the roof boards above local z."""
    return WALL + (HZ - abs(z)) * TAN


class GangHouse:
    def __init__(self, seed=61):
        self.rng = random.Random(seed)
        self.parts = {name: Mesh(self.rng) for name in
                      ("Frame", "Walls", "Floor", "Roof", "Doors", "Hearth", "Base", "Iron", "Furniture", "Shelters",
                       "Decks", "Piles", "Nets", "Basement", "BasementWalls", "Tunnel", "TunnelTimber")}
        self.colliders, self.walk, self.not_walkable, self.lights, self.lanterns, self.anchors = [], [], [], [], [], {}
        self.door, self.tunnel_nodes = None, []
        self.goods = []

    # -- parts file --------------------------------------------------------------------------------
    def collider(self, name, group, centre, size, euler=(0.0, 0.0, 0.0), into=None):
        (self.colliders if into is None else into).append({
            "name": name, "group": group, "centre": [round(c, 4) for c in centre],
            "size": [round(abs(c), 4) for c in size], "euler": [round(c, 3) for c in euler]})

    def box_collider(self, name, group, lo, hi, into=None):
        self.collider(name, group, [(lo[i] + hi[i]) * 0.5 for i in range(3)], [hi[i] - lo[i] for i in range(3)], into=into)

    def walk_box(self, name, lo, hi):
        self.box_collider(name, "Walk", lo, hi, into=self.walk)

    def anchor(self, name, *values):
        self.anchors[name] = [round(v, 3) for v in values]

    # -- walls (the cookhouse's framing: posts outside, boards on their inner face) -----------------
    def wall(self, name, runs_x, line, out, a, b, posts, openings=(), gable=False):
        frame, walls, rng = self.parts["Frame"], self.parts["Walls"], self.rng
        grain = 0 if runs_x else 2
        centre = line - out * POST * 0.5
        for o in openings:
            if not (any(abs(o[0] - (q + POST * 0.5)) < 1e-6 for q in posts) and any(abs(o[1] - (q - POST * 0.5)) < 1e-6 for q in posts)) \
                    or any(o[0] < q < o[1] for q in posts):
                raise ValueError("%s: the opening %s does not stand between two posts" % (name, (o[0], o[1])))

        def p(along, across, y):
            return (along, y, across) if runs_x else (across, y, along)

        def box(mesh, a0, a1, c0, c1, y0, y1, g, mat, **kw):
            mesh.box(p(a0, c0, y0), p(a1, c1, y1), g, mat, **kw)

        for at in posts:
            box(frame, at - POST * 0.5, at + POST * 0.5, centre - POST * 0.5, centre + POST * 0.5, BASE_TOP, WALL - PLATE, 1, FRAME)
        box(frame, a, b, centre - POST * 0.5, centre + POST * 0.5, WALL - PLATE, WALL, grain, FRAME)
        box(frame, a, b, centre - POST * 0.5 - 0.02, centre + POST * 0.5 + 0.02, BASE_TOP, BASE_TOP + 0.2, grain, FRAME)   # sill beam on the base
        for pa, pb in zip(posts, posts[1:]):
            lo, hi = pa + POST * 0.5, pb - POST * 0.5
            cut = [o for o in openings if o[0] < hi - 0.01 and o[1] > lo + 0.01]
            if not cut:
                box(frame, lo, hi, centre + out * 0.02, centre + out * 0.1, 1.0, 1.11, grain, FRAME)
            for o in cut:
                box(frame, lo - 0.02, hi + 0.02, centre - 0.08, centre + 0.08, o[3], o[3] + 0.12, grain, FRAME)
        inner = line - out * INNER
        edges = sorted({-0.1, WALL - 0.02} | {y for o in openings for y in (o[2], o[3]) if -0.1 < y < WALL - 0.02})
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
                        d = rng.uniform(0.0, 0.006)
                        box(walls, q0 + 0.002, q1 - 0.002, inner - out * (BOARD + d), inner - out * d, r0, r1 - 0.003, grain, WOOD)
        # a few rough boards nailed over gaps, outside, between two posts
        for _ in range(2):
            i = rng.randrange(len(posts) - 1)
            lo, hi = posts[i] + POST * 0.5, posts[i + 1] - POST * 0.5
            if any(o[0] < hi and o[1] > lo for o in openings) or hi - lo < 0.9:
                continue
            y = rng.uniform(0.3, 1.9)
            ln = rng.uniform(0.7, min(1.4, hi - lo))
            s = rng.uniform(lo, hi - ln)
            lift = rng.uniform(-0.05, 0.05)
            box(walls, s, s + ln, inner + out * 0.004, inner + out * 0.03, y, y + 0.17, grain, FRAME,
                skew=lambda c, s=s, ln=ln, lift=lift: [c[0], c[1] + lift * ((c[0] if runs_x else c[2]) - s) / ln, c[2]])
        if gable:
            for r0, r1 in walls.rows(a + 0.02, b - 0.02, 0.16):
                d = rng.uniform(0.0, 0.005)
                c0, c1 = inner - out * (BOARD + d) + out * 0.045, inner - out * d + out * 0.045

                def skew(c, lift=rng.uniform(-0.012, 0.0)):
                    return [c[0], roof_plane(c[2]) - 0.03 + lift, c[2]] if c[1] > WALL else c

                walls.box(p(r0 + 0.002, c0, WALL - 0.07), p(r1 - 0.002, c1, WALL + 1.0), 1, WOOD, skew=skew)
        mid_c = line - out * WALL_BOX * 0.5
        x = a
        for o in sorted(openings):
            if o[0] - x > 0.02:
                self.box_collider(name, "Walls", p(x, mid_c - WALL_BOX * 0.5, BASE_TOP), p(o[0], mid_c + WALL_BOX * 0.5, WALL))
            self.box_collider(name + "_Header", "Walls", p(o[0], mid_c - WALL_BOX * 0.5, o[3]), p(o[1], mid_c + WALL_BOX * 0.5, WALL))
            x = o[1]
        if b - x > 0.02:
            self.box_collider(name, "Walls", p(x, mid_c - WALL_BOX * 0.5, BASE_TOP), p(b, mid_c + WALL_BOX * 0.5, WALL))
        if gable:
            self.box_collider(name + "_Gable", "Walls", p(a + 1.6, mid_c - WALL_BOX * 0.5, WALL), p(b - 1.6, mid_c + WALL_BOX * 0.5, WALL + 0.9))

    def shell(self):
        frame = self.parts["Frame"]
        self.wall("Wall_Camp", True, HZ, 1, -HX, HX, CAMP_POSTS, [(CAMP_DOOR[0], CAMP_DOOR[1], 0.0, CAMP_DOOR[2])])
        self.wall("Wall_Lake", True, -HZ, -1, -HX, HX, LAKE_POSTS, [(LAKE_DOOR[0], LAKE_DOOR[1], 0.0, LAKE_DOOR[2])])
        self.wall("Wall_East", False, -HX, -1, -HZ, HZ, GABLE_POSTS, gable=True)
        self.wall("Wall_West", False, HX, 1, -HZ, HZ, GABLE_POSTS, gable=True)
        for x in TIES:
            frame.box((x - 0.08, WALL - PLATE, -HZ + POST), (x + 0.08, WALL - 0.01, HZ - POST), 2, FRAME)

    def floor(self):
        floor = self.parts["Floor"]
        for z0, z1 in floor.rows(-HZ + INNER, HZ - INNER, 0.16):
            for x0, x1 in floor.pieces(-HX + INNER, HX - INNER, 2.2, 3.8):
                d = self.rng.uniform(0.0, 0.004)
                floor.box((x0 + 0.002, -0.045, z0 + 0.002), (x1 - 0.002, -d, z1 - 0.002), 0, WOOD, skip=((1, 0),))
        for x in (-3.6, -2.4, -1.2, 0.0, 1.2, 2.4, 3.6):   # joists, seen from the basement
            floor.box((x - 0.07, BASE_TOP, -HZ + 0.3), (x + 0.07, -0.045, HZ - 0.3), 2, FRAME)
        self.walk_box("Floor", (-HX + 0.05, -0.2, -HZ + 0.05), (HX - 0.05, 0.0, HZ - 0.05))

    def roof(self):
        roof, frame, rng = self.parts["Roof"], self.parts["Frame"], self.rng
        length = (HX + GABLE_OVERHANG) * 2.0
        slope = (HZ + EAVE_OVERHANG) / COS
        peak = (0.0, roof_plane(0.0), 0.0)
        ridge = X
        for side in (-1, 1):
            down = (0.0, -SIN, side * COS)
            normal = (0.0, COS, side * SIN)
            for xa, xb in roof.rows(-length * 0.5, length * 0.5, 0.17):
                o = [peak[k] + down[k] * slope * 0.5 - normal[k] * 0.0125 + ridge[k] * (xa + xb) * 0.5 for k in range(3)]
                roof.framed(o, down, normal, ridge, (slope * 0.5, 0.0125, (xb - xa) * 0.5 - 0.002), 0, WOOD, skip=((1, 1),))
            n = 9
            for i in range(n):
                x = -HX + POST * 0.5 + i * (2.0 * (HX - POST * 0.5)) / (n - 1)
                o = [peak[k] + down[k] * (slope * 0.5 - 0.05) - normal[k] * 0.09 + ridge[k] * x for k in range(3)]
                frame.framed(o, down, normal, ridge, (slope * 0.5 - 0.05, 0.065, 0.045), 0, FRAME)
            for end in (-1, 1):
                o = [peak[k] + down[k] * (slope * 0.5 - 0.02) - normal[k] * 0.05 + ridge[k] * end * (length * 0.5 + 0.016) for k in range(3)]
                roof.framed(o, down, normal, ridge, (slope * 0.5 + 0.02, 0.09, 0.016), 0, FRAME)
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
                x = -length * 0.5 - 0.03 + rng.uniform(0.0, 0.1)
                while x < length * 0.5 + 0.03:
                    w = min(rng.uniform(0.15, MAX_BOARD), length * 0.5 + 0.03 - x)
                    if w < 0.05:
                        break
                    gap = rng.uniform(0.003, 0.009)
                    roll = rng.random()
                    pz = side * (butt - shingle_len * 0.5) * COS
                    in_chimney = CHIMNEY[0] - 0.05 < x + w * 0.5 < CHIMNEY[1] + 0.3 and CHIMNEY[2] + 0.05 < pz < CHIMNEY[3] - 0.05
                    if roll > 0.012 and not in_chimney:
                        rag = rng.uniform(-0.035, 0.04) + (rng.uniform(0.03, 0.08) if roll < 0.05 else 0.0)
                        ln = min(shingle_len, butt + rag - 0.02)
                        mid = butt + rag - ln * 0.5
                        lift = rng.uniform(0.0, 0.007)
                        o = [peak[k] + down[k] * mid + normal[k] * (0.004 + lift + (ln * 0.5) * math.sin(tilt)) + ay[k] * thick * 0.5
                             + ridge[k] * (x + w * 0.5) for k in range(3)]
                        mat = SHINGLE
                        if any(pc <= c < pc + ph and px <= x < px + pw for pc, px, ph, pw in pale) or rng.random() < 0.015:
                            mat = PALE
                        skew = rng.uniform(-0.014, 0.014)
                        ends = [(skew if (i >> 2) & 1 else -skew) if i & 1 else 0.0 for i in range(8)]
                        roof.framed(o, ax, ay, ridge, (ln * 0.5, thick * 0.5, (w - gap) * 0.5), 0, mat, ends, skip=((1, 0), (0, 0)))
                    x += w
            # mended with plain planks: three or four long boards nailed down the slope over the shingles
            for _ in range(rng.randrange(3, 5)):
                x = rng.uniform(-length * 0.5 + 0.6, min(length * 0.5 - 0.6, CHIMNEY[0] - 0.5))
                a = rng.uniform(0.3, slope * 0.5)
                ln = rng.uniform(0.9, 1.5)
                o = [peak[k] + down[k] * (a + ln * 0.5) + normal[k] * 0.045 + ridge[k] * x for k in range(3)]
                roof.framed(o, down, normal, ridge, (ln * 0.5, 0.012, rng.uniform(0.07, 0.095)), 0, PALE if rng.random() < 0.5 else FRAME)
            self.collider("Roof_%s" % ("Lake" if side < 0 else "Camp"), "Roof",
                          (0.0, WALL + HZ * TAN * 0.5, side * HZ * 0.5), (HX * 2.0 + 0.7, 0.2, HZ / COS + 0.6),
                          (side * math.degrees(PITCH), 0.0, 0.0))
        top = peak[1]
        frame.box((-HX - GABLE_OVERHANG + 0.05, top - 0.28, -0.06), (HX + GABLE_OVERHANG - 0.05, top - 0.1, 0.06), 0, FRAME)
        for side in (-1, 1):
            down = (0.0, -SIN, side * COS)
            normal = (0.0, COS, side * SIN)
            for xa, xb in roof.pieces(-length * 0.5 - 0.04, length * 0.5 + 0.04, 2.6, 3.8):
                o = [peak[k] + down[k] * 0.085 + normal[k] * (0.062 + (0.012 if side > 0 else 0.0)) + ridge[k] * (xa + xb) * 0.5 for k in range(3)]
                roof.framed(o, ridge, normal, down, ((xb - xa) * 0.5 - 0.004, 0.012, 0.1), 0, FRAME)

    # -- doors -------------------------------------------------------------------------------------
    def leaf(self, hinge, along, out, width, height, planks=5, name="Leaf"):
        """A ledged plank door standing open: `along` runs from the hinge to the free edge, `out` is its face."""
        doors, iron, rng = self.parts["Doors"], self.parts["Iron"], self.rng
        x = 0.01
        for _ in range(planks):
            w = (width - 0.02) / planks
            o = [hinge[k] + along[k] * (x + w * 0.5) + Y[k] * (height * 0.5 + rng.uniform(-0.008, 0.008)) + out[k] * 0.018 for k in range(3)]
            doors.framed(o, Y, along, out, (height * 0.5, w * 0.5 - 0.002, 0.018), 0, WOOD)
            x += w
        for y in (0.3, height - 0.3):
            o = [hinge[k] + along[k] * width * 0.5 + Y[k] * y + out[k] * 0.052 for k in range(3)]
            doors.framed(o, along, Y, out, (width * 0.5 - 0.03, 0.06, 0.016), 0, FRAME)
            o = [hinge[k] + along[k] * min(0.3, width * 0.4) + Y[k] * y + out[k] * 0.072 for k in range(3)]
            iron.framed(o, along, Y, out, (min(0.3, width * 0.4), 0.024, 0.004), 0, IRON, mode="iron")
        a = [hinge[k] + along[k] * 0.06 + Y[k] * 0.37 + out[k] * 0.052 for k in range(3)]
        b = [hinge[k] + along[k] * (width - 0.06) + Y[k] * (height - 0.37) + out[k] * 0.052 for k in range(3)]
        doors.beam(a, b, 0.1, 0.03, FRAME, up=out)
        c = [hinge[k] + along[k] * width * 0.5 + Y[k] * height * 0.5 + out[k] * 0.02 for k in range(3)]
        self.collider(name, "Doors", c, (0.08, height, width - 0.06), (0.0, math.degrees(math.atan2(along[0], along[2])), 0.0))

    def doors(self):
        iron = self.parts["Iron"]

        def swing(deg):
            a = math.radians(deg)
            return (math.sin(a), 0.0, math.cos(a))

        def face(along):
            return (along[2], 0.0, -along[0])

        # camp door: hinged on its west jamb, standing open inward against the wall
        along = swing(98.0)
        self.leaf((CAMP_DOOR[1] + 0.06, 0.02, HZ - POST - BOARD - 0.03), along, face(along), CAMP_DOOR[1] - CAMP_DOOR[0], CAMP_DOOR[2] - 0.06, name="Leaf_Camp")
        # balcony door: hinged on its west jamb, standing open inward
        along = swing(80.0)
        self.leaf((LAKE_DOOR[1] + 0.06, 0.02, -HZ + POST + BOARD + 0.03), along, face(along), LAKE_DOOR[1] - LAKE_DOOR[0], LAKE_DOOR[2] - 0.06, name="Leaf_Lake")
        # lantern bracket outside, east of the camp door (left of it, seen from the camp)
        bx, by = CAMP_POSTS[2], 2.15
        iron.box((bx - 0.012, by - 0.012, HZ), (bx + 0.012, by + 0.012, HZ + 0.42), 2, IRON, mode="iron")
        iron.beam((bx, by - 0.24, HZ), (bx, by - 0.01, HZ + 0.27), 0.018, 0.018, IRON, up=X)
        iron.box((bx - 0.008, by - 0.07, HZ + 0.37), (bx + 0.008, by, HZ + 0.386), 1, IRON, mode="iron")
        self.lanterns.append({"name": "DoorLantern", "position": [round(bx, 3), round(by - 0.07 - 0.27, 3), round(HZ + 0.378, 3)], "out": [0.0, 0.0, 1.0]})

    # -- rubble ------------------------------------------------------------------------------------
    def masonry(self, mesh, lo, hi, core=True):
        """Rough rubble laid in courses filling a box: a plain core, faced with separate stones."""
        rng = self.rng
        if core:
            mesh.box([lo[i] + 0.05 for i in range(3)], [hi[i] - 0.05 for i in range(3)], 0, RUBBLE, mode="stone")
        along_x = (hi[0] - lo[0]) >= (hi[2] - lo[2])
        la, lb = (lo[0], hi[0]) if along_x else (lo[2], hi[2])
        ca, cb = (lo[2], hi[2]) if along_x else (lo[0], hi[0])
        y = lo[1]
        while hi[1] - y > 0.03:
            h = rng.uniform(0.17, 0.27)
            if hi[1] - y - h < 0.12:
                h = hi[1] - y
            x = la
            while lb - x > 0.02:
                n = rng.uniform(0.28, 0.56)
                if lb - x - n < 0.16:
                    n = lb - x
                c_along, c_across = x + n * 0.5, (ca + cb) * 0.5
                size = (n * 1.03, h * 1.03, (cb - ca) * 1.03)
                centre = (c_along, y + h * 0.5, c_across) if along_x else (c_across, y + h * 0.5, c_along)
                mesh.stone(centre, size, yaw=0.0 if along_x else math.pi * 0.5)
                x += n
            y += h

    def mouth(self):
        """Where the tunnel (coming along world +x) meets the basement's west face: its axis there (z), the
        opening's two sides on that face, and how far the opening has moved along the wall at its outer face."""
        q = to_local(*[TUNNEL_PATH[-1][k] for k in (0, 2, 1)])
        dx, dz = _COS_YAW, _SIN_YAW                       # world +x in this frame
        t = (q[0] - ROOM[1]) / -dx
        zi = q[2] + dz * t
        half = (TUNNEL_W * 0.5 + 0.07) / abs(dx)
        reach = abs(dz / dx) * (HX + BASE_OUT - ROOM[1])
        return zi, zi - half, zi + half, reach

    def base(self):
        """The rubble base: four walls under the house's, faced with stones outside where they are seen and inside
        round the basement. The lake wall is 0.65 m thick and holds the hidden door; the camp wall and the end
        walls are 1.5 m thick (ROOM), because the terrain's hole stops short of the footprint in whole cells."""
        base, walls, frame = self.parts["Base"], self.parts["BasementWalls"], self.parts["Frame"]
        ox, oz = HX + BASE_OUT, HZ + BASE_OUT
        rx0, rx1, rz0, rz1 = ROOM
        face = 0.3
        deep = WATER - 0.4
        seen_lake = DECK_Y - 0.35           # below the deck only the plain core shows
        camp_foot = CAMP_GROUND - 0.4
        da, db, dh = HIDDEN_DOOR
        head = DECK_Y + dh
        # camp wall, outside
        base.box((-ox + face, camp_foot, oz - BASE_WALL), (ox - face, BASE_TOP, oz - face), 0, RUBBLE, mode="stone")
        self.masonry(base, (-ox, camp_foot, oz - face), (ox, BASE_TOP, oz), core=False)
        # lake wall: plain below the deck, stones above it either side of the door
        base.box((-ox + 0.02, deep, -oz + 0.02), (ox - 0.02, seen_lake, rz0 - 0.02), 0, RUBBLE, mode="stone")
        for a, b in ((-ox, da - 0.1), (db + 0.1, ox)):
            base.box((a + 0.05, seen_lake, -oz + 0.27), (b - 0.05, BASE_TOP, rz0 - 0.27), 0, RUBBLE, mode="stone")
            self.masonry(base, (a, seen_lake, -oz), (b, BASE_TOP, -oz + face), core=False)
        base.box((da - 0.1, seen_lake, -oz + 0.02), (db + 0.1, DECK_Y - 0.06, rz0 - 0.02), 0, RUBBLE, mode="stone")   # under the sill
        # door frame: jambs, lintel beam through the wall, a plank sill
        for x in (da - 0.1, db):
            frame.box((x, DECK_Y - 0.06, -oz - 0.02), (x + 0.1, head, rz0 + 0.02), 1, FRAME)
        frame.box((da - 0.32, head, -oz - 0.04), (db + 0.32, BASE_TOP, rz0 + 0.04), 0, FRAME)
        frame.box((da, DECK_Y - 0.06, -oz - 0.03), (db, DECK_Y, rz0 + 0.03), 0, FRAME)
        # end walls, outside: the ground falls along them from the camp to the lake. The tunnel comes in through
        # the west wall's camp half, under the ground: nothing of that half is drawn below the tunnel's head.
        _, m0, _, _ = self.mouth()
        tunnel_head = TUNNEL_WORLD_Y - FLOOR_WORLD_Y + TUNNEL_H + 0.45
        for side in (-1, 1):
            x0, x1 = (side * ox, side * (ox - face)) if side < 0 else (side * (ox - face), side * ox)
            cx0, cx1 = sorted((side * (ox - face), side * (ox - BASE_WALL)))
            split = 0.0 if side < 0 else m0 - 0.15          # where the lake half ends
            foot = camp_foot - 0.6 if side < 0 else tunnel_head
            base.box((cx0, deep, -oz + face), (cx1, BASE_TOP, split), 0, RUBBLE, mode="stone")
            base.box((cx0, deep if side < 0 else tunnel_head, split), (cx1, BASE_TOP, oz - face), 0, RUBBLE, mode="stone")
            base.box((min(x0, x1) + 0.02, deep, -oz + face), (max(x0, x1) - 0.02, seen_lake, split), 0, RUBBLE, mode="stone")
            self.masonry(base, (x0, seen_lake, -oz + face), (x1, BASE_TOP, split), core=False)
            self.masonry(base, (x0, foot, split), (x1, BASE_TOP, oz - face), core=False)

        # colliders: the thick walls, the lake wall round the door, the ground under the sill
        self.box_collider("Base_Camp", "Base", (-ox, deep, rz1), (rx1, BASE_TOP, oz))   # the west wall's own boxes take the corner
        self.box_collider("Base_East", "Base", (-ox, deep, -oz), (rx0, BASE_TOP, oz))
        self.box_collider("Base_Lake_East", "Base", (-ox, DECK_Y, -oz), (da, BASE_TOP, rz0))
        self.box_collider("Base_Lake_West", "Base", (db, DECK_Y, -oz), (ox, BASE_TOP, rz0))
        self.box_collider("Base_Lake_Header", "Base", (da, head, -oz), (db, BASE_TOP, rz0))
        self.box_collider("Base_Lake_Foot", "Base", (-ox, deep, -oz), (ox, DECK_Y, rz0))
        self.anchor("hiddenDoor", da, db, dh, -oz, DECK_Y)
        self.anchor("basement", rx0, rx1, rz0, rz1, DECK_Y, BASE_TOP)
        self.door = {"position": [round(da, 3), round(DECK_Y, 3), round(-oz + 0.1, 3)], "yaw": 0.0, "scaleY": DOOR_SCALE_Y,
                     "openAngle": -90.0, "opening": [da, db, dh]}

    def inner_face(self, lo, hi, holes=()):
        """Stones on a room face of the base (a box 0.3 m deep, from the floor to the joists), a plain sheet of
        rubble behind them, skipping `holes`: (from, to, head) along the face, from the floor."""
        walls = self.parts["BasementWalls"]
        along_x = (hi[0] - lo[0]) >= (hi[2] - lo[2])
        a, b = (lo[0], hi[0]) if along_x else (lo[2], hi[2])
        cuts = sorted(holes)
        spans, at = [], a
        for h0, h1, _ in cuts:
            spans.append((at, h0, lo[1]))
            at = h1
        spans.append((at, b, lo[1]))
        spans += [(h0, h1, h_top) for h0, h1, h_top in cuts]          # over each opening, from its head
        for s0, s1, y0 in spans:
            if s1 - s0 < 0.12 or hi[1] - y0 < 0.08:
                continue
            if along_x:
                self.masonry(walls, (s0, y0, lo[2]), (s1, hi[1], hi[2]))
            else:
                self.masonry(walls, (lo[0], y0, s0), (hi[0], hi[1], s1))

    def basement(self):
        """The gang's store under the house: rubble on every side, the main floor's joists overhead on a beam and
        two posts, packed earth, and at the west end a well of five steps down to the
        tunnel's mouth. Returns where the tunnel's axis meets the west face."""
        b, walls, frame, iron, rng = self.parts["Basement"], self.parts["BasementWalls"], self.parts["Frame"], self.parts["Iron"], self.rng
        rx0, rx1, rz0, rz1 = ROOM
        da, db, dh = HIDDEN_DOOR
        fy = DECK_Y
        ty = TUNNEL_WORLD_Y - FLOOR_WORLD_Y
        zi, m0, m1, reach = self.mouth()                  # the tunnel's mouth on the west face
        treads = int(round((fy - ty) / STEP)) - 1
        top = rx1 - 0.3                                   # the landing's edge: the treads climb east from it
        well = (top - treads * TREAD, rx1, m0 - 0.1, rz1)   # min x (the top riser), max x, min z, max z
        mouth_head = ty + TUNNEL_H - 0.1
        # faces
        self.inner_face((rx0, fy - 0.05, rz0 - 0.3), (rx1, BASE_TOP, rz0), holes=((da - 0.1, db + 0.1, BASE_TOP),))     # lake
        self.inner_face((rx0, fy - 0.05, rz1), (rx1, BASE_TOP, rz1 + 0.3))                                                 # camp
        self.inner_face((well[0] - 0.3, ty - 0.1, rz1), (rx1, fy - 0.05, rz1 + 0.3))                                       # ... and down the well
        self.inner_face((rx0 - 0.3, fy - 0.05, rz0), (rx0, BASE_TOP, rz1))                                                 # east
        self.inner_face((rx1, ty - 0.1, rz0), (rx1 + 0.3, BASE_TOP, rz1), holes=((m0, m1, mouth_head + 0.2),))            # west
        # the west wall's colliders, round the tunnel's slanting way through it
        ox, oz, deep = HX + BASE_OUT, HZ + BASE_OUT, WATER - 0.4
        self.box_collider("Base_West_South", "Base", (rx1, deep, -oz), (ox, BASE_TOP, m0 - 0.05))
        self.box_collider("Base_West_North", "Base", (rx1, deep, m1 + reach + 0.05), (ox, BASE_TOP, oz))
        self.box_collider("Base_West_Header", "Base", (rx1, mouth_head + 0.3, m0 - 0.05), (ox, BASE_TOP, m1 + reach + 0.05))
        self.box_collider("Base_West_Foot", "Base", (rx1, deep, m0 - 0.05), (ox, ty - 0.2, m1 + reach + 0.05))
        # the mouth's frame: two posts and a cap, rough
        for z in (m0 - 0.02, m1 + 0.02):
            frame.log((rx1 + 0.05, ty, z), (rx1 + 0.05, mouth_head + 0.05, z), 0.075, FRAME, sides=7, rings=3)
        frame.log((rx1 + 0.05, mouth_head + 0.1, m0 - 0.25), (rx1 + 0.05, mouth_head + 0.1, m1 + 0.25), 0.08, FRAME, sides=7, rings=4)
        # floor: packed earth round the well, a few flat stones
        for name, lo, hi in (("Basement", (rx0, well[2]), (well[0], rz1)), ("Basement_Lake", (rx0, rz0), (rx1, well[2]))):
            b.box((lo[0] - 0.05, fy - 0.3, lo[1] - 0.05), (hi[0] + 0.05, fy, hi[1] + 0.05), 0, EARTH, mode="stone")
            self.box_collider(name, "Walk", (lo[0], ty - 0.25, lo[1]), (hi[0], fy, hi[1]), into=self.walk)
        for fx, fz in ((-1.9, -0.9), (-0.4, -1.6), (1.9, -1.5), (-2.3, 0.6)):
            b.stone((fx, fy - 0.012, fz), (rng.uniform(0.4, 0.6), 0.07, rng.uniform(0.32, 0.48)))
        # the well: a landing at the tunnel's level, plank treads on earth, boards holding its south side
        b.box((well[0], ty - 0.3, well[2] - 0.05), (rx1 + 0.05, ty - 0.008, rz1 + 0.05), 0, EARTH, mode="stone")
        for k in range(treads):
            x1 = top - k * TREAD
            y = ty + (k + 1) * STEP
            b.box((x1 - TREAD - 0.02, ty - 0.02, well[2]), (x1, y - 0.04, rz1), 0, EARTH, mode="stone")
            b.box((x1 - TREAD - 0.03, y - 0.045, well[2] + 0.02), (x1 + 0.03, y, rz1 - 0.02), 2, WOOD)
        b.box((well[0] - 0.05, fy - 0.045, well[2] + 0.02), (well[0] + 0.07, fy, rz1 - 0.02), 2, WOOD)   # the top nosing
        for ya, yb in b.rows(ty - 0.05, fy - 0.02, 0.17):
            b.box((well[0] + 0.02, ya, well[2] - 0.04), (rx1 - 0.02, yb - 0.004, well[2]), 0, WOOD)
        run = treads * TREAD
        rise = fy - ty
        length = math.hypot(run, rise)
        pitch = math.degrees(math.atan2(rise, run))
        # a smooth ramp over the treads, climbing toward -x (yaw 270)
        self.collider("BasementSteps", "Walk", (top - run * 0.5, ty + rise * 0.5 - 0.1, (well[2] + rz1) * 0.5), (well[3] - well[2], 0.2, length + 0.1),
                      (-pitch, 270.0, 0.0), into=self.walk)
        self.box_collider("BasementLanding", "Walk", (top - 0.02, ty - 0.2, well[2]), (rx1 + 0.35, ty, rz1), into=self.walk)
        # ceiling: boards closing the joists from below (the floor's boards have no underside), a beam on two posts
        # (over the whole footprint: the band between the walls' top and the floor would show the sky)
        for za, zb in b.rows(-HZ + 0.02, HZ - 0.02, 0.16):
            b.box((-HX + 0.02, -0.07, za + 0.002), (HX - 0.02, -0.046, zb - 0.002), 0, WOOD, skip=((1, 1),))
        bz = -0.45
        frame.box((rx0, BASE_TOP - 0.22, bz - 0.11), (rx1, BASE_TOP, bz + 0.11), 0, FRAME)
        for i, x in enumerate((-1.3, 1.3)):
            frame.box((x - 0.1, fy - 0.05, bz - 0.1), (x + 0.1, BASE_TOP - 0.22, bz + 0.1), 1, FRAME)
            b.stone((x, fy + 0.02, bz), (0.42, 0.1, 0.42), yaw=rng.uniform(-0.3, 0.3))
            self.box_collider("BasementPost_%d" % i, "Structure", (x - 0.1, fy, bz - 0.1), (x + 0.1, BASE_TOP, bz + 0.1))
        # a lantern on the east post: it always burns (no window down here)
        hx, hook = -1.3 + 0.1, fy + 1.9
        iron.box((hx, hook, bz - 0.01), (hx + 0.16, hook + 0.02, bz + 0.01), 0, IRON, mode="iron")
        self.lanterns.append({"name": "BasementLantern", "position": [round(hx + 0.15, 3), round(hook - 0.27, 3), round(bz, 3)],
                              "out": [1.0, 0.0, 0.0], "always": True})
        # a slatted plank panel leaning on the west face beside the mouth, as if to stand in front of it
        px = rx1 - 0.12
        za, zb = m0 - 1.25, m0 - 0.25
        for k in range(5):
            z = za + 0.08 + k * (zb - za - 0.16) / 4.0
            frame.beam((px - 0.3, fy, z), (px, fy + 1.75, z), 0.13, 0.025, WOOD, up=X)
        for y in (0.45, 1.3):
            off = 0.3 * (1.0 - y / 1.75)
            frame.box((px - off - 0.04, fy + y, za), (px - off - 0.01, fy + y + 0.1, zb), 2, FRAME)
        self.box_collider("Panel", "Structure", (px - 0.32, fy, za), (rx1, fy + 1.75, zb))
        self.anchor("well", well[0], well[1], well[2], well[3], ty)
        self.anchor("mouth", m0, m1, mouth_head, rx1, ty)
        return zi

    def tunnel(self, zi):
        """The smuggling tunnel: a drift shored with pole frames and split planks, from the outer face of the
        warehouse cellar's side room to the basement's west face. Its line and depths are TUNNEL_PATH (world)."""
        floor, timber, rng = self.parts["Tunnel"], self.parts["TunnelTimber"], self.rng
        hw = TUNNEL_W * 0.5
        nodes = [to_local(x, y, z) for x, z, y in TUNNEL_PATH]
        nodes.append([ROOM[1], TUNNEL_WORLD_Y - FLOOR_WORLD_Y, zi])
        self.anchor("tunnelStart", *nodes[0])

        def flat(v):
            return norm((v[0], 0.0, v[2]))

        dirs = [norm([b[k] - a[k] for k in range(3)]) for a, b in zip(nodes, nodes[1:])]
        turns = [0.0]                                    # at each inner node: + turns left, - right, 0 straight
        for da, db in zip(dirs, dirs[1:]):
            fa, fb = flat(da), flat(db)
            c = fa[2] * fb[0] - fa[0] * fb[2]
            turns.append(0.0 if abs(c) < 0.05 else (1.0 if c > 0 else -1.0))
        turns.append(0.0)
        legs = []
        frame_at = 0.35
        for i, (a, b) in enumerate(zip(nodes, nodes[1:])):
            d = dirs[i]
            f = flat(d)
            side = norm(cross(Y, f))                     # to the left of the way
            n = norm(cross(d, side))
            if n[1] < 0:
                n = [-c for c in n]
            length = math.sqrt(sum((b[k] - a[k]) ** 2 for k in range(3)))
            last = i == len(dirs) - 1
            lap = 0.08                                  # stretches that meet in line overlap: no seam between them
            start = hw if turns[i] else (-lap if i else 0.0)   # a leg leaving a corner starts at the corner's far side
            end = length + (hw + 0.12 if turns[i + 1] else (0.0 if last else lap))
            # the last stretch meets the wall's face on the slant: one side reaches it sooner than the other
            slant = -side[0] / d[0] if last else 0.0    # extra length per metre to the side

            def at(s, across=0.0, up=0.0, a=a, d=d, side=side):
                return [a[k] + d[k] * s + side[k] * across + Y[k] * up for k in range(3)]

            mid, half = (start + end) * 0.5, (end - start) * 0.5
            yaw = math.degrees(math.atan2(f[0], f[2]))
            pitch = -math.degrees(math.asin(max(-1.0, min(1.0, d[1]))))
            # floor: trodden earth, two lines of boards
            reach = abs(slant) * (hw + 0.3)             # floor and roof run on to the face's far corner
            floor.framed([at(mid + reach * 0.5)[k] - n[k] * 0.06 for k in range(3)], d, n, side, (half + reach * 0.5, 0.06, hw + 0.3), 0, EARTH, mode="stone")
            for across in (-0.2, 0.2):
                s = start + 0.1
                while s < end - 0.3:
                    ln = min(rng.uniform(1.3, 2.1), end - 0.1 - s)
                    floor.framed([at(s + ln * 0.5, across + rng.uniform(-0.03, 0.03))[k] + n[k] * 0.018 for k in range(3)], d, n, side,
                                 (ln * 0.5, 0.018, rng.uniform(0.12, 0.16)), 0, WOOD)
                    s += ln + rng.uniform(0.03, 0.12)
            self.collider("Tunnel_%d" % i, "Walk", [at(mid)[k] - n[k] * 0.1 for k in range(3)], (TUNNEL_W + 0.3, 0.2, half * 2.0), (pitch, yaw, 0.0), into=self.walk)
            self.collider("TunnelRoof_%d" % i, "Tunnel", [at(mid)[k] + n[k] * (TUNNEL_H + 0.3) for k in range(3)], (TUNNEL_W + 0.5, 0.2, half * 2.0), (pitch, yaw, 0.0))
            # walls: split planks on edge behind the props, dark earth behind them; the side a corner opens is left out there
            for sign in (1.0, -1.0):
                w0, w1 = start, end + slant * sign * (hw + 0.13)
                if turns[i + 1] and turns[i + 1] * sign > 0:
                    # the next leg leaves through this wall. Its own wall starts where this one stops, and the
                    # earth behind the two crosses at the corner: neither may run on, or it stands in the way
                    w1 = length - hw
                if w1 - w0 < 0.1:
                    continue
                wm, wh = (w0 + w1) * 0.5, (w1 - w0) * 0.5
                # packed earth behind the planks, 0.2 m of it
                timber.framed([at(wm, sign * (hw + 0.21))[k] + n[k] * (TUNNEL_H * 0.5 + 0.1) for k in range(3)], d, n, side,
                              (wh + 0.05, TUNNEL_H * 0.5 + 0.3, 0.1), 0, EARTH, mode="stone")
                for y0 in (0.06, 0.5, 0.94, 1.38, 1.8):
                    s = w0
                    while s < w1 - 0.05:
                        ln = min(rng.uniform(1.5, 2.3), w1 - s)
                        hgt = rng.uniform(0.26, 0.34) if y0 < 1.7 else 0.24
                        timber.framed([at(s + ln * 0.5, sign * (hw + 0.07 + rng.uniform(0.0, 0.03)))[k] + n[k] * (y0 + hgt * 0.5) for k in range(3)],
                                      d, n, side, (ln * 0.5 - 0.01, hgt * 0.5, 0.02), 0, WOOD)
                        s += ln
                self.collider("TunnelWall_%d" % i, "Tunnel", [at(wm, sign * (hw + 0.17))[k] + n[k] * (TUNNEL_H * 0.5) for k in range(3)],
                              (0.2, TUNNEL_H + 0.6, wh * 2.0 + 0.1), (pitch, yaw, 0.0))
            if turns[i + 1]:                             # the corner's end wall
                for y0 in (0.06, 0.5, 0.94, 1.38, 1.8):
                    timber.framed([at(end - 0.05)[k] + n[k] * (y0 + 0.15) for k in range(3)], side, n, d, (hw + 0.1, 0.15, 0.02), 0, WOOD)
                timber.framed([at(end + 0.08)[k] + n[k] * (TUNNEL_H * 0.5 + 0.1) for k in range(3)], side, n, d, (hw + 0.4, TUNNEL_H * 0.5 + 0.3, 0.1), 0, EARTH, mode="stone")
                self.collider("TunnelEnd_%d" % i, "Tunnel", [at(end + 0.08)[k] + n[k] * (TUNNEL_H * 0.5) for k in range(3)], (TUNNEL_W + 0.5, TUNNEL_H + 0.6, 0.2), (pitch, yaw, 0.0))
            # roof: planks across on the caps, earth over them
            timber.framed([at(mid + reach * 0.5)[k] + n[k] * (TUNNEL_H + 0.26) for k in range(3)], d, n, side, (half + reach * 0.5, 0.08, hw + 0.3), 0, EARTH, mode="stone")
            s = start
            while s < end + reach - 0.05:
                w = min(rng.uniform(0.2, 0.3), end + reach - s)
                timber.framed([at(s + w * 0.5)[k] + n[k] * (TUNNEL_H + 0.13 + rng.uniform(0.0, 0.02)) for k in range(3)], side, n, d,
                              (hw + 0.12, 0.018, w * 0.5 - rng.uniform(0.005, 0.03)), 0, WOOD)
                s += w
            # frames: two pole props and a cap pole
            while frame_at < length - 0.2:
                if frame_at > start - 0.3:
                    tops = []
                    for sign in (1.0, -1.0):
                        foot = at(frame_at, sign * (hw - 0.06))
                        top = [foot[0] + rng.uniform(-0.02, 0.02), foot[1] + TUNNEL_H + d[1] * 0.0, foot[2] + rng.uniform(-0.02, 0.02)]
                        timber.log(foot, top, rng.uniform(0.06, 0.075), FRAME, sides=7, rings=3, wobble=0.008)
                        tops.append(top)
                    capa = [at(frame_at, hw + 0.1)[k] + Y[k] * (TUNNEL_H + 0.06) for k in range(3)]
                    capb = [at(frame_at, -hw - 0.1)[k] + Y[k] * (TUNNEL_H + 0.06) for k in range(3)]
                    timber.log(capa, capb, rng.uniform(0.065, 0.08), FRAME, sides=7, rings=3, wobble=0.008)
                    legs.append((i, frame_at, at(frame_at, hw - 0.06, TUNNEL_H - 0.25), side))
                frame_at += FRAME_STEP
            frame_at -= length
        # lanterns on three props: they always burn
        for name, pick in (("TunnelLantern_West", 2), ("TunnelLantern_Middle", len(legs) // 2), ("TunnelLantern_East", len(legs) - 2)):
            i, _, point, side = legs[max(0, min(len(legs) - 1, pick))]
            hook = [point[k] - side[k] * 0.16 for k in range(3)]
            self.lanterns.append({"name": name, "position": [round(hook[0], 3), round(hook[1] - 0.27, 3), round(hook[2], 3)],
                                  "out": [round(-side[0], 3), 0.0, round(-side[2], 3)], "always": True})
        # agents never take the tunnel: a Not Walkable slab across its second leg
        a, b = nodes[3], nodes[4]
        f = flat([b[k] - a[k] for k in range(3)])
        mid = [(a[k] + b[k]) * 0.5 for k in range(3)]
        self.collider("TunnelStrip", "NotWalkable", (mid[0], mid[1] + 1.0, mid[2]), (TUNNEL_W + 1.0, 3.0, 1.2),
                      (0.0, math.degrees(math.atan2(f[0], f[2])), 0.0), into=self.not_walkable)
        self.tunnel_nodes = [[round(c, 3) for c in node] for node in nodes]

    def hearth(self):
        mesh, frame = self.parts["Hearth"], self.parts["Frame"]
        x0, x1, z0, z1 = HEARTH
        bench = 0.4
        self.masonry(mesh, (x0, BASE_TOP, z0), (x1, bench, z1))
        for a, b in ((z0, z0 + 0.32), (z1 - 0.32, z1)):
            self.masonry(mesh, (x0 + 0.2, bench, a), (x1, 1.4, b))
        self.masonry(mesh, (x1 - 0.18, bench, z0 + 0.32), (x1, 1.4, z1 - 0.32))
        frame.box((x0 + 0.14, 1.4, z0 - 0.04), (x0 + 0.3, 1.54, z1 + 0.04), 2, FRAME)   # mantel beam
        c0, c1, cz0, cz1 = CHIMNEY
        lo_y, hi_y = 1.54, 2.3
        corners, local = [], []
        for i in range(8):
            top = (i >> 1) & 1
            x = (c1 if i & 1 else c0) if top else (x1 if i & 1 else x0 + 0.14)
            z = (cz1 if (i >> 2) & 1 else cz0) if top else (z1 if (i >> 2) & 1 else z0)
            y = hi_y if top else lo_y
            corners.append([x, y, z])
            local.append([x - (x0 + x1) * 0.5, y - (lo_y + hi_y) * 0.5, z - (z0 + z1) * 0.5])
        mesh.hexa(corners, local, 0, RUBBLE, mode="stone")
        self.masonry(mesh, (c0, hi_y, cz0), (c1, CHIMNEY_TOP, cz1))
        self.box_collider("Hearth", "Furniture", (x0, 0.0, z0), (x1, 1.54, z1))
        self.box_collider("Chimney", "Furniture", (c0, 1.54, cz0), (c1, CHIMNEY_TOP, cz1))
        self.lights.append({"name": "HearthFire", "position": [round(x0 + 0.45, 3), 0.75, 0.0]})
        self.anchor("chimneyTop", (c0 + c1) * 0.5, CHIMNEY_TOP, 0.0)

    # -- furniture ---------------------------------------------------------------------------------
    def furniture(self):
        f = self.parts["Furniture"]
        # table and two benches, west of the room's middle (Brann and his chest keep the east half)
        cx, cz, hw, hd, top = 1.35, 0.7, 0.85, 0.4, 0.8
        for z0, z1 in f.rows(cz - hd, cz + hd, 0.17):
            f.box((cx - hw, top - 0.05, z0 + 0.002), (cx + hw, top, z1 - 0.002), 0, WOOD)
        for sx in (-1, 1):
            for sz in (-1, 1):
                f.box((cx + sx * (hw - 0.14), 0.0, cz + sz * (hd - 0.1) - 0.05), (cx + sx * (hw - 0.04), top - 0.05, cz + sz * (hd - 0.1) + 0.05), 1, FRAME)
        f.box((cx - hw + 0.1, 0.24, cz - 0.04), (cx + hw - 0.1, 0.3, cz + 0.04), 0, FRAME)
        self.box_collider("Table", "Furniture", (cx - hw, 0.0, cz - hd), (cx + hw, top, cz + hd))
        for s in (-1, 1):
            z = cz + s * 0.72
            f.box((cx - hw + 0.05, 0.4, z - 0.13), (cx + hw - 0.05, 0.44, z + 0.13), 0, WOOD)
            for x in (cx - 0.6, cx + 0.6):
                f.box((x - 0.03, 0.0, z - 0.11), (x + 0.03, 0.4, z + 0.11), 1, FRAME)
            self.box_collider("Bench", "Furniture", (cx - hw + 0.05, 0.0, z - 0.13), (cx + hw - 0.05, 0.44, z + 0.13))
        self.anchor("table", cx, top, cz)
        # two bunks along the east gable
        bx0, bx1 = -HX + INNER + BOARD + 0.02, -HX + INNER + BOARD + 0.92
        for i, (z0, z1) in enumerate(((-2.72, -0.8), (0.8, 2.72))):
            for x in (bx0 + 0.04, bx1 - 0.04):
                for z in (z0 + 0.04, z1 - 0.04):
                    f.box((x - 0.04, 0.0, z - 0.04), (x + 0.04, 0.62, z + 0.04), 1, FRAME)
            for xa, xb in f.rows(bx0, bx1, 0.17):
                f.box((xa + 0.002, 0.36, z0), (xb - 0.002, 0.4, z1), 2, WOOD)
            for x in (bx0, bx1 - 0.03):
                f.box((x, 0.4, z0), (x + 0.03, 0.56, z1), 2, FRAME)
            self.box_collider("Bunk_%d" % i, "Furniture", (bx0, 0.0, z0), (bx1, 0.6, z1))
            self.anchor("bunk%d" % i, (bx0 + bx1) * 0.5, 0.4, (z0 + z1) * 0.5, z1 - z0)
        # a shelf on the camp wall, west of the door
        wz = HZ - INNER - BOARD
        f.box((0.9, 1.5, wz - 0.28), (2.3, 1.535, wz), 0, WOOD)
        for x in (1.05, 2.15):
            f.beam((x, 1.24, wz - 0.01), (x, 1.495, wz - 0.25), 0.04, 0.04, FRAME, up=X)
        self.anchor("shelf", 1.6, 1.535, wz - 0.14, 1.4)

    # -- lean-to roof over the balcony (the cookhouse's: rough boards on pole rafters) ---------------
    def lean_roof(self, name, x0, x1, z_high, z_low, y_high, y_low, rafter_step=0.8):
        roof, frame, rng = self.parts["Shelters"], self.parts["Frame"], self.rng
        d = norm((0.0, y_low - y_high, z_low - z_high))
        n = norm(cross(d, X))
        if n[1] < 0:
            n = [-c for c in n]
        over_low, over_high = 0.3, 0.0
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
            rag_low = rng.uniform(-0.12, 0.05)
            ln = length + rag_low
            o = [(xa + xb) * 0.5, start[1] + d[1] * ln * 0.5 + n[1] * (0.012 + lift), start[2] + d[2] * ln * 0.5 + n[2] * (0.012 + lift)]
            w = (xb - xa) * 0.5 + (0.012 if k % 2 else -0.004)
            roof.framed(o, d, n, X, (ln * 0.5, 0.012, w), 0, WOOD)
            k += 1
        c = [0.0, start[1] + d[1] * length * 0.6 + n[1] * 0.1, start[2] + d[2] * length * 0.6 + n[2] * 0.1]
        roof.log((x0 - 0.3, c[1], c[2]), (x1 + 0.3, c[1], c[2]), 0.055, FRAME, sides=7, rings=6, wobble=0.015)
        mid_y, mid_z = (y_high + y_low) * 0.5, (z_high + z_low) * 0.5
        pitch = math.degrees(math.atan2(y_high - y_low, abs(z_high - z_low))) * (1.0 if z_low > z_high else -1.0)
        self.collider(name, "Roof", (0.5 * (x0 + x1), mid_y + 0.05, mid_z), (x1 - x0 + 0.3, 0.14, length), (pitch, 0.0, 0.0))

    def planks_across(self, mesh, x0, x1, z0, z1, top, rag=0.04, thick=0.05):
        """Deck planks lying along z, side by side along x."""
        rng = self.rng
        for xa, xb in mesh.rows(x0, x1, 0.16):
            d = rng.uniform(0.0, 0.006)
            mesh.box((xa + 0.004, top - thick, z0 - rng.uniform(0.0, rag)), (xb - 0.004, top - d, z1 + rng.uniform(0.0, rag)), 2, WOOD)

    def pile(self, name, x, z, top, foot, radius=0.11, collide=None):
        piles = self.parts["Piles"]
        tar_to = WATER + 0.45
        if foot < tar_to < top:
            piles.log((x, foot, z), (x, tar_to, z), radius * 1.03, TAR, sides=8, rings=3)
            piles.log((x, tar_to - 0.02, z), (x, top, z), radius, FRAME, sides=8, rings=3)
        else:
            piles.log((x, foot, z), (x, top, z), radius, FRAME, sides=8, rings=3)
        if collide is not None:
            self.box_collider(name, "Structure", (x - radius, collide, z - radius), (x + radius, top, z + radius))

    def balcony(self):
        decks, frame, piles = self.parts["Decks"], self.parts["Frame"], self.parts["Piles"]
        z_out = BALCONY_Z
        for z in (-HZ - 0.5, z_out + 0.1):   # bearers: the inner one on brackets, the outer on the posts
            frame.box((-HX, -0.19, z - 0.06), (HX, -0.05, z + 0.06), 0, FRAME)
        for x in (-4.3, -2.9, -1.45, 0.0, 1.45, 2.9, 4.3):
            frame.beam((x, -0.75, -HZ - 0.03), (x, -0.2, -HZ - 0.5), 0.07, 0.07, FRAME, up=X)
        for xa, xb in decks.rows(-HX, HX, 0.16):
            d = self.rng.uniform(0.0, 0.005)
            decks.box((xa + 0.003, -0.05, z_out - self.rng.uniform(0.0, 0.04)), (xb - 0.003, -d, -HZ - 0.01), 2, WOOD)
        self.walk_box("Balcony", (-HX, -0.2, z_out), (HX, 0.0, -HZ))
        plate = LEAN[1] - 0.1
        for i, x in enumerate(BALCONY_POSTS):   # squared posts from the deck up to the lean-to's plate
            frame.box((x - 0.08, DECK_Y, z_out + 0.02), (x + 0.08, plate, z_out + 0.18), 1, FRAME)
            self.box_collider("BalconyPost_%d" % i, "Structure", (x - 0.08, DECK_Y, z_out + 0.02), (x + 0.08, plate, z_out + 0.18))
        frame.log((-HX - 0.2, plate + 0.06, z_out + 0.1), (HX + 0.2, plate + 0.06, z_out + 0.1), 0.07, FRAME, sides=8, rings=6)
        frame.box((-HX + 0.05, LEAN[0] - 0.16, -HZ - 0.07), (HX - 0.05, LEAN[0] - 0.03, -HZ), 0, FRAME)   # ledger on the wall
        self.lean_roof("Roof_Balcony", -HX, HX, -HZ - 0.02, z_out - 0.05, LEAN[0], LEAN[1])
        # one pole rail along the front and across both ends
        rail = 0.95
        piles.log((-HX, rail, z_out + 0.1), (HX, rail, z_out + 0.1), 0.04, FRAME, sides=6, rings=7, wobble=0.012)
        self.box_collider("BalconyRail", "Rails", (-HX, 0.0, z_out + 0.04), (HX, rail + 0.1, z_out + 0.16))
        for side in (-1, 1):
            x = side * (HX - 0.08)
            piles.log((x, rail, z_out + 0.1), (x, rail, -HZ), 0.035, FRAME, sides=6, rings=3, wobble=0.008)
            self.box_collider("BalconyRail_End", "Rails", (x - 0.06, 0.0, z_out), (x + 0.06, rail + 0.1, -HZ))
        # the deck's lantern hangs on the second post, over the deck
        bx = BALCONY_POSTS[1]
        iron = self.parts["Iron"]
        iron.box((bx - 0.012, DECK_Y + 2.2, z_out - 0.3), (bx + 0.012, DECK_Y + 2.224, z_out + 0.02), 2, IRON, mode="iron")
        iron.box((bx - 0.008, DECK_Y + 2.13, z_out - 0.29), (bx + 0.008, DECK_Y + 2.2, z_out - 0.274), 1, IRON, mode="iron")
        self.lanterns.append({"name": "DeckLantern", "position": [round(bx, 3), round(DECK_Y + 2.13 - 0.27, 3), round(z_out - 0.282, 3)], "out": [0.0, 0.0, -1.0]})

    def deck(self):
        decks, frame = self.parts["Decks"], self.parts["Frame"]
        x0, x1, z0 = DECK
        z1 = -HZ - BASE_OUT
        self.planks_across(decks, x0, x1, z0, z1 - 0.01, DECK_Y)
        for z in (z0 + 0.2, (z0 + z1) * 0.5, z1 - 0.2):
            frame.box((x0 + 0.05, DECK_Y - 0.21, z - 0.07), (x1 - 0.05, DECK_Y - 0.05, z + 0.07), 0, FRAME)
        n = 5
        for i in range(n):
            x = x0 + 0.2 + i * (x1 - x0 - 0.4) / (n - 1)
            for z in (z0 + 0.2, (z0 + z1) * 0.5):
                self.pile("DeckPile", x, z, DECK_Y - 0.05, WATER - 2.6)
        self.walk_box("Deck", (x0, DECK_Y - 0.2, z0), (x1, DECK_Y, z1))
        self.anchor("deck", x0, x1, z0, z1, DECK_Y)
        self.anchor("groundCut", *GROUND_CUT)
        self.anchor("groundCutInside", *GROUND_CUT_INSIDE)

    def walkway(self):
        decks, frame, piles, rng = self.parts["Decks"], self.parts["Frame"], self.parts["Piles"], self.rng
        x0, x1, z_head, z_foot = WALKWAY
        run = z_head - z_foot
        drop = WALKWAY_HEAD_Y - DECK_Y
        d = norm((0.0, -drop, -run))             # down the walkway, toward the lake
        n = norm(cross(X, d))
        if n[1] < 0:
            n = [-c for c in n]
        length = math.sqrt(run * run + drop * drop)
        head = (0.0, WALKWAY_HEAD_Y, z_head)
        s = -0.35                                # it starts a little way into the ground
        while s < length:
            w = min(rng.uniform(0.16, MAX_BOARD), length - s)
            if w < 0.05:
                break
            o = [(x0 + x1) * 0.5, head[1] + d[1] * (s + w * 0.5) - n[1] * 0.025, head[2] + d[2] * (s + w * 0.5) - n[2] * 0.025]
            decks.framed(o, X, n, d, ((x1 - x0) * 0.5 + rng.uniform(-0.03, 0.03), 0.025, w * 0.5 - 0.004), 0, WOOD)
            s += w
        for x in (x0 + 0.12, x1 - 0.12):         # stringers
            a = (x, head[1] - n[1] * 0.12, head[2] - n[2] * 0.12)
            b = (x, a[1] + d[1] * length, a[2] + d[2] * length)
            frame.beam(a, b, 0.09, 0.14, FRAME, extend=0.2)
        pitch = math.degrees(math.atan2(drop, run))
        mid = [(x0 + x1) * 0.5, (WALKWAY_HEAD_Y + DECK_Y) * 0.5 - 0.1, (z_head + z_foot) * 0.5]
        self.collider("Walkway", "Walk", mid, (x1 - x0, 0.2, length + 0.5), (-pitch, 0.0, 0.0), into=self.walk)
        # piles and a pole rail on the outer (east) side; a pile pair midway carries the stringers
        rail = 0.95
        tops = []
        for i, t in enumerate((0.05, 0.5, 0.97)):
            z = z_head - run * t
            y = WALKWAY_HEAD_Y - drop * t
            self.pile("WalkwayPost_%d" % i, x0 - 0.02, z, y + rail + 0.08, WATER - 1.2, radius=0.06, collide=y - 0.3)
            tops.append((x0 - 0.02, y + rail, z))
            if i:
                self.pile("WalkwayPile", x1 - 0.1, z, y - 0.12, WATER - 1.2, radius=0.08)
        for a, b in zip(tops, tops[1:]):
            piles.log(a, b, 0.035, FRAME, sides=6, rings=3, wobble=0.008)
        self.collider("WalkwayRail", "Rails", (x0 - 0.02, (WALKWAY_HEAD_Y + DECK_Y) * 0.5 + rail * 0.5, (z_head + z_foot) * 0.5),
                      (0.12, rail + 0.1, length), (-pitch, 0.0, 0.0))
        self.anchor("walkway", x0, x1, z_head, z_foot, WALKWAY_HEAD_Y)

    def ramp(self):
        decks, frame, rng = self.parts["Decks"], self.parts["Frame"], self.rng
        width, run = RAMP
        cx = (CAMP_DOOR[0] + CAMP_DOOR[1]) * 0.5
        x0, x1 = cx - width * 0.5, cx + width * 0.5
        drop = -CAMP_GROUND
        d = norm((0.0, -drop, run))
        n = norm(cross(d, X))
        if n[1] < 0:
            n = [-c for c in n]
        length = math.sqrt(run * run + drop * drop)
        head = (0.0, 0.0, HZ + 0.02)
        s, k = 0.0, 0
        while s < length + 0.3:
            w = min(rng.uniform(0.16, MAX_BOARD), length + 0.3 - s)
            if w < 0.05:
                break
            o = [cx, head[1] + d[1] * (s + w * 0.5) - n[1] * 0.025, head[2] + d[2] * (s + w * 0.5) - n[2] * 0.025]
            decks.framed(o, X, n, d, (width * 0.5 + rng.uniform(-0.03, 0.03), 0.025, w * 0.5 - 0.004), 0, WOOD)
            s += w
            k += 1
        t = 0.5
        while t < length:                        # grip battens (visual, 2 cm)
            o = [cx, head[1] + d[1] * t + n[1] * 0.01, head[2] + d[2] * t + n[2] * 0.01]
            decks.framed(o, X, n, d, (width * 0.5 - 0.06, 0.01, 0.03), 0, FRAME)
            t += 0.55
        for x in (x0 + 0.15, x1 - 0.15):
            a = (x, head[1] - n[1] * 0.13, head[2] - n[2] * 0.13)
            b = (x, a[1] + d[1] * length, a[2] + d[2] * length)
            frame.beam(a, b, 0.1, 0.16, FRAME, extend=0.15)
            z = HZ + run * 0.5
            frame.box((x - 0.06, CAMP_GROUND - 0.3, z - 0.06), (x + 0.06, -drop * 0.5 - 0.14, z + 0.06), 1, FRAME)
        pitch = math.degrees(math.atan2(drop, run))
        self.collider("Ramp", "Walk", (cx, -drop * 0.5 - 0.1, HZ + run * 0.5), (width, 0.2, length + 0.3), (pitch, 0.0, 0.0), into=self.walk)
        self.anchor("ramp", x0, x1, HZ, HZ + run, CAMP_GROUND, pitch)

    def pier(self):
        decks, frame = self.parts["Decks"], self.parts["Frame"]
        x0, x1, z_end = PIER
        z_start = DECK[2]
        for za, zb in decks.rows(z_end, z_start, 0.16):   # cross planks
            d = self.rng.uniform(0.0, 0.006)
            decks.box((x0 - self.rng.uniform(0.0, 0.05), DECK_Y - 0.05, za + 0.004), (x1 + self.rng.uniform(0.0, 0.05), DECK_Y - d, zb - 0.004), 0, WOOD)
        for x in (x0 + 0.25, x1 - 0.25):
            frame.box((x - 0.07, DECK_Y - 0.21, z_end + 0.05), (x + 0.07, DECK_Y - 0.05, z_start), 2, FRAME)
        stations = (z_start - 2.6, z_start - 5.4, z_end + 0.25)
        for i, z in enumerate(stations):
            for side, x in ((0, x0 + 0.02), (1, x1 - 0.02)):
                stub = side == 0 or i == 2          # mooring stubs on the boat's side and at the end
                top = DECK_Y + (0.42 if stub else -0.05)
                self.pile("PierPost_%d%d" % (i, side), x, z, top, WATER - 4.6, radius=0.12, collide=DECK_Y if stub else None)
        self.walk_box("Pier", (x0, DECK_Y - 0.2, z_end), (x1, DECK_Y, z_start))
        self.anchor("pier", x0, x1, z_end, z_start, DECK_Y)
        self.anchor("boat", x0 - 1.15, WATER, z_end + 2.6, 0.0)   # beside the pier's east side: x, y, z, yaw off the pier's line

    def nets(self):
        nets, piles, rng = self.parts["Nets"], self.parts["Piles"], self.rng
        x0, x1, z = NETS
        bar = DECK_Y + 2.12
        for x in (x0, x1):
            piles.log((x, DECK_Y, z), (x + rng.uniform(-0.03, 0.03), bar + 0.1, z), 0.04, FRAME, sides=6, rings=3, wobble=0.006)
            piles.log((x, DECK_Y, z - 0.55), (x, DECK_Y + 1.1, z - 0.02), 0.028, FRAME, sides=5, rings=2, wobble=0.0)   # a stake propping it
        piles.log((x0 - 0.15, bar, z), (x1 + 0.15, bar, z), 0.035, FRAME, sides=6, rings=4, wobble=0.006)
        # two layers of net, knotted cord: uprights and rows, hanging to the deck, sagging between the poles
        for layer, dz in enumerate((-0.05, 0.05)):
            count = 33
            for i in range(count):
                x = x0 + 0.08 + i * (x1 - x0 - 0.16) / (count - 1) + (0.05 if layer else 0.0)
                low = DECK_Y + rng.uniform(0.0, 0.1)
                nets.beam((x, bar + 0.02, z), (x + rng.uniform(-0.04, 0.04), low, z + dz * 2.0), 0.012, 0.012, ROPE, up=X)
            rows = 24
            for k in range(rows):
                t = (k + 1) / (rows + 1.0)
                y = bar + (DECK_Y + 0.05 - bar) * t
                sag = 0.04 * math.sin(math.pi * t)
                nets.beam((x0 + 0.06, y, z + dz * 2.0 * t), (x1 - 0.06, y - sag, z + dz * 2.0 * t), 0.012, 0.012, ROPE)
        self.collider("NetFrame", "Structure", ((x0 + x1) * 0.5, DECK_Y + 1.1, z), (x1 - x0 + 0.3, 2.2, 0.2))
        self.collider("NetFrame", "NotWalkable", ((x0 + x1) * 0.5, DECK_Y + 1.0, z - 0.2), (x1 - x0 + 0.5, 2.0, 0.9), into=self.not_walkable)
        self.anchor("nets", x0, x1, z, bar)

    def kit_goods(self):
        """Kit props the Sites tool stands on the deck and in the room (prefab frame): this script owns where."""
        def put(kit, x, y, z, yaw=0.0, scale=1.0, solid=True, lay=0.0):
            self.goods.append({"kit": kit, "position": [round(x, 3), round(y, 3), round(z, 3)], "euler": [lay, yaw, 0.0],
                               "scale": scale, "solid": solid})

        crate, cask, sack, sack2 = "SM_Crate_B_01", "SM_WoodBarrel_01a", "SM_Sack_01", "SM_Sack_02"
        base = -HZ - BASE_OUT
        # the deck: two casks against the base east of the lantern post, crates beside the nets, a sack
        put(cask, -3.75, DECK_Y, base - 0.45, 20.0)
        put(cask, -3.0, DECK_Y, base - 0.5, 140.0)
        put(crate, NETS[1] + 0.75, DECK_Y, base - 0.5, 6.0, 1.0)
        put(crate, NETS[1] + 0.7, DECK_Y + 0.66, base - 0.48, -14.0, 0.85, solid=False)
        put(crate, 3.9, DECK_Y, DECK[2] + 0.6, 30.0, 0.9)
        put(sack, -2.1, DECK_Y, base - 0.4, 70.0, solid=False)
        put(sack2, -1.75, DECK_Y, base - 0.75, 160.0, solid=False)
        # the room: a cask by the lake wall, a crate and sacks in the camp-side corner by the hearth
        put(cask, -0.35, 0.0, -HZ + INNER + BOARD + 0.42, 50.0)
        put(crate, 3.75, 0.0, HZ - INNER - BOARD - 0.45, -8.0, 0.9)
        put(sack, 2.95, 0.0, HZ - INNER - BOARD - 0.38, 100.0, solid=False)
        # the basement: casks against the lake wall east of the door, crates and sacks along the east wall
        rx0, rx1, rz0, rz1 = ROOM
        for x, yaw in ((rx0 + 0.6, 10.0), (rx0 + 1.4, 130.0), (rx0 + 2.2, 250.0)):
            put(cask, x, DECK_Y, rz0 + 0.55, yaw)
        put(crate, rx0 + 0.5, DECK_Y, rz1 - 0.5, 5.0, 1.0)
        put(crate, rx0 + 1.3, DECK_Y, rz1 - 0.48, -12.0, 0.95)
        put(crate, rx0 + 0.75, DECK_Y + 0.66, rz1 - 0.5, 20.0, 0.85, solid=False)
        put(sack, rx0 + 0.45, DECK_Y, 0.2, 30.0, solid=False)
        put(sack2, rx0 + 0.5, DECK_Y, -0.35, 120.0, solid=False)
        put(sack, rx0 + 0.95, DECK_Y, -0.05, 80.0, solid=False)

    def build(self):
        self.shell()
        self.floor()
        self.roof()
        self.doors()
        self.base()
        self.tunnel(self.basement())
        self.hearth()
        self.furniture()
        self.balcony()
        self.deck()
        self.walkway()
        self.ramp()
        self.pier()
        self.nets()
        self.kit_goods()
        # openings, for the tests: from, to, head, then the wall's line
        self.anchor("campDoor", CAMP_DOOR[0], CAMP_DOOR[1], CAMP_DOOR[2], HZ - POST * 0.5)
        self.anchor("lakeDoor", LAKE_DOOR[0], LAKE_DOOR[1], LAKE_DOOR[2], -HZ + POST * 0.5)
        self.anchor("room", -HX + INNER + BOARD, HX - INNER - BOARD, -HZ + INNER + BOARD, HZ - INNER - BOARD, WALL)
        self.anchor("ridge", roof_plane(0.0) + 0.1)


def main():
    out = os.path.dirname(_HERE)
    house = GangHouse()
    house.build()

    name = "GangHouse"
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
    # the parts file goes out with the FBX, before anything else can fail: the two must never be a version apart
    parts = {"version": 2, "centre": list(CENTRE), "yaw": YAW, "floorWorldY": FLOOR_WORLD_Y, "triangles": triangles,
             "total": sum(triangles.values()), "objects": list(house.parts), "walk": house.walk,
             "colliders": house.colliders, "notWalkable": house.not_walkable, "lanterns": house.lanterns,
             "lights": house.lights, "goods": house.goods, "anchors": house.anchors,
             "basement": {"room": list(ROOM), "floorY": DECK_Y, "hole": list(HOLE), "door": house.door},
             "tunnel": {"nodes": house.tunnel_nodes, "width": TUNNEL_W, "height": TUNNEL_H, "worldPath": [list(p) for p in TUNNEL_PATH]}}
    with open(os.path.join(out, name + ".json"), "w") as f:
        json.dump(parts, f, indent=1)
    for o in objects:
        o.name = name + "_" + o.name
    bpy.data.libraries.write(os.path.join(_HERE, name + ".blend"), set(objects) | set(mats), fake_user=True)
    return {"triangles": triangles, "total": parts["total"]}


if __name__ == "__main__":
    result = main()
    print("gang house:", result)
