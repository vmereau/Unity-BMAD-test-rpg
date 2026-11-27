"""Area 1 wilderness gate: the cart gateway in the palisade beside the gang house, on the way to the bridge.
It is the dock gate's gateway (two log posts, a lintel with pointed logs on it, two leaves of lashed logs standing
open) without the guard's lean-to and the fire basket.

Run in Blender (5.x):  blender -b -P build_wilderness_gate.py
or from a session:      runpy.run_path(r"...\\build_wilderness_gate.py", run_name="__main__")

Writes  ../WildernessGate.fbx, ../WildernessGate.json (colliders, Not Walkable volumes, the opening) and
        WildernessGate.blend (the built objects only: this script is the source).

Every size is the dock gate's: this script loads ../../dock gate/Source~/build_dock_gate.py as a library and
builds its `gate` and `leaf` parts with another seed, so a change there changes both gates. Frame (the prefab's):
x along the path, the way the leaves stand open; y up; z across the opening; origin on the ground in the middle
of the opening. In the scene the root is turned so that +x points into the camp (layout: wilderness-gate, yaw).
"""
import json
import os
import runpy

import bpy

_HERE = os.path.dirname(os.path.abspath(__file__))
_GATE = runpy.run_path(os.path.join(os.path.dirname(os.path.dirname(_HERE)), "dock gate", "Source~", "build_dock_gate.py"),
                       run_name="dock_gate_lib")
DockGate, materials, export = _GATE["DockGate"], _GATE["materials"], _GATE["export"]
WIDTH, POST_R, CLEAR, POST_TOPS, LEAF_SWING = (_GATE[n] for n in ("WIDTH", "POST_R", "CLEAR", "POST_TOPS", "LEAF_SWING"))

CENTRE = (31.95, 52.0)               # world x, z of the origin (layout: wilderness-gate)
YAW = 90.0                           # the root's turn: +x into the camp, +z east along the palisade's line
PARTS = ("Gate", "Leaves")


def main():
    out = os.path.dirname(_HERE)
    gate = DockGate(seed=97)
    gate.gate()
    half = WIDTH * 0.5
    gate.leaf("Leaf_South", -half - 0.05, -LEAF_SWING)
    gate.leaf("Leaf_North", half + 0.05, LEAF_SWING)
    meshes = {name: gate.parts[name] for name in PARTS}

    name = "WildernessGate"
    old = bpy.data.collections.get(name)
    kept = set(old.objects) if old else set()
    for part in meshes:   # checked before anything is deleted
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
    objects = [mesh.to_object(part, mats, col) for part, mesh in meshes.items()]
    export(objects, os.path.join(out, name + ".fbx"))
    triangles = {part: mesh.triangles() for part, mesh in meshes.items()}
    # the parts file goes out with the FBX, before anything else can fail: the two must never be a version apart
    parts = {"version": 1, "triangles": triangles, "total": sum(triangles.values()), "objects": list(meshes),
             "colliders": gate.colliders, "notWalkable": gate.not_walkable, "centre": list(CENTRE), "yaw": YAW,
             "opening": [round(-half + POST_R, 3), round(half - POST_R, 3), CLEAR], "postTops": list(POST_TOPS),
             "leaves": gate.leaves}
    with open(os.path.join(out, name + ".json"), "w") as f:
        json.dump(parts, f, indent=1)
    for o in objects:
        o.name = name + "_" + o.name
    bpy.data.libraries.write(os.path.join(_HERE, name + ".blend"), set(objects) | set(mats), fake_user=True)
    return {"triangles": triangles, "total": parts["total"]}


if __name__ == "__main__":
    result = main()
    print("wilderness gate:", result)
