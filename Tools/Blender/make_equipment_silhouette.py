"""
Inventory paper-doll silhouette (spec-inventory-equipment-paper-doll).

Renders the base hero (canonical head + body split) front-on (orthographic, Workbench flat white, transparent background) in a relaxed A-pose
and turns the mask into one flat sprite: fill #352B21 with a thin #5E4C38 outline, at 2x the on-screen rect
(260x700 -> 520x1400). The figure is placed so the leader lines of DESIGN.md land on it: top of the head at y 14,
feet at the bottom, hand centres HAND_DX px either side of the centre line (run() reports where they land).

Runs in its own scene, so whatever is open in Blender is left alone:
    exec(open("Tools/Blender/make_equipment_silhouette.py").read()); result = run(REPO)
or  blender -b --factory-startup --python-expr "exec(open('Tools/Blender/make_equipment_silhouette.py').read()); run('.')"
"""
import math
import os

import bpy
import numpy as np
from mathutils import Matrix, Vector

FBX = "Assets/_Game/Art/Characters/Humanoids/models/test base hero/base hero canonical split.fbx"  # body + canonical head, one rig
OUT = "Assets/_Game/Art/UI/Sprites/UI_EquipmentSilhouette.png"

SCALE = 2                       # render px per on-screen px
RECT_W, RECT_H = 260, 700       # on-screen silhouette rect (DESIGN.md)
HEAD_TOP_Y = 14                 # rect-local px
FEET_Y = 690                    # rect-local px
HAND_DX = 100                   # hand centre, rect-local px from the centre line (screen x ~188 / ~388)
OUTLINE_PX = 2.0                # on-screen outline width
FILL = (0x35, 0x2B, 0x21)
EDGE = (0x5E, 0x4C, 0x38)

SCENE = "EquipmentSilhouette"


_STATE = {}


def _view_layer():
    # bpy.context.view_layer still points at the previously open scene inside one MCP call: use our scene's own layer.
    return _STATE["scene"].view_layers[0]


def _update():
    _view_layer().update()


def _new_scene():
    old = bpy.data.scenes.get(SCENE)
    if old is not None:
        for ob in list(old.objects):
            data = ob.data
            bpy.data.objects.remove(ob, do_unlink=True)
            if data is not None and data.users == 0:
                for coll in (bpy.data.meshes, bpy.data.armatures, bpy.data.cameras):
                    if data.name in coll and coll[data.name] == data:
                        coll.remove(data)
                        break
        bpy.data.scenes.remove(old)
    scene = bpy.data.scenes.new(SCENE)
    if bpy.context.window is not None:
        bpy.context.window.scene = scene
    _STATE["scene"] = scene
    return scene


def _rotate_bone_world(arm_ob, pb, world_axis, angle):
    """Rotates a pose bone about a world axis through its head."""
    axis = (arm_ob.matrix_world.inverted().to_3x3() @ world_axis).normalized()
    head = pb.head.copy()
    rot = Matrix.Translation(head) @ Matrix.Rotation(angle, 4, axis) @ Matrix.Translation(-head)
    pb.matrix = rot @ pb.matrix
    _update()


def _world(arm_ob, pb, tail=False):
    return arm_ob.matrix_world @ (pb.tail if tail else pb.head)


def _mesh_bounds(meshes):
    deps = _view_layer().depsgraph
    deps.update()
    pts = []
    for ob in meshes:
        ev = ob.evaluated_get(deps)
        me = ev.to_mesh()
        mw = ob.matrix_world
        pts.extend(mw @ v.co for v in me.vertices)
        ev.to_mesh_clear()
    xs = [p.x for p in pts]
    zs = [p.z for p in pts]
    return min(xs), max(xs), min(zs), max(zs)


def _hand_point(arm_ob, side):
    """Centre of the hand: middle-finger base, falling back to the hand bone tail."""
    bones = arm_ob.pose.bones
    for name in (f"mixamorig:{side}HandMiddle1", f"mixamorig:{side}Hand"):
        pb = bones.get(name)
        if pb is not None:
            return _world(arm_ob, pb, tail=name.endswith("Hand"))
    raise RuntimeError(f"no {side} hand bone")


def pose_a(arm_ob, meshes):
    """
    Lowers both arms from the T-pose until each hand centre is HAND_DX on-screen px from the body centre line: the 260 px
    rect only fits arms hanging almost straight. A real arm puts the hand centre at ~48 % of the height, above the
    mockup's long-armed figure: run() reports where the hands land and the hand / ring leader lines are aimed there.
    """
    bones = arm_ob.pose.bones
    x_min, x_max, z_min, z_max = _mesh_bounds(meshes)
    centre_x = (x_min + x_max) / 2
    target_dx = HAND_DX / (FEET_Y - HEAD_TOP_Y) * (z_max - z_min)
    lo, hi, applied = 0.0, math.radians(88), 0.0

    def set_angle(angle):
        nonlocal applied
        # The character faces -Y: rotating about world Y swings the arm in the front view.
        for side in ("Left", "Right"):
            _rotate_bone_world(arm_ob, bones[f"mixamorig:{side}Arm"], Vector((0, 1, 0)),
                               (angle - applied) * _down_sign(arm_ob, side))
        applied = angle

    for _ in range(30):  # bisection: the spread shrinks monotonically as the arm lowers towards vertical
        mid = (lo + hi) / 2
        set_angle(mid)
        if abs(_hand_point(arm_ob, "Left").x - centre_x) > target_dx:
            lo = mid
        else:
            hi = mid
    set_angle((lo + hi) / 2)
    return math.degrees(applied)


_DOWN = {}


def _down_sign(arm_ob, side):
    """+1 / -1 so that a positive step lowers that arm (decided once by a probe)."""
    if side in _DOWN:
        return _DOWN[side]
    pb = arm_ob.pose.bones[f"mixamorig:{side}Arm"]
    before = _world(arm_ob, pb, tail=True).z
    _rotate_bone_world(arm_ob, pb, Vector((0, 1, 0)), 0.05)
    after = _world(arm_ob, pb, tail=True).z
    _rotate_bone_world(arm_ob, pb, Vector((0, 1, 0)), -0.05)
    _DOWN[side] = 1.0 if after < before else -1.0
    return _DOWN[side]


def render_mask(scene, meshes, tmp_png):
    x_min, x_max, z_min, z_max = _mesh_bounds(meshes)
    w, h = RECT_W * SCALE, RECT_H * SCALE
    px_per_m = (FEET_Y - HEAD_TOP_Y) * SCALE / (z_max - z_min)
    ortho = h / px_per_m  # ortho_scale spans the larger (vertical) dimension

    cam_data = bpy.data.cameras.new("SilhouetteCam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = ortho
    cam = bpy.data.objects.new("SilhouetteCam", cam_data)
    scene.collection.objects.link(cam)
    centre_x = (x_min + x_max) / 2
    # Image centre (h/2 px from the top) sits (h/2 - HEAD_TOP_Y*SCALE) px below the head top.
    centre_z = z_max - (h / 2 - HEAD_TOP_Y * SCALE) / px_per_m
    cam.location = (centre_x, -10.0, centre_z)
    cam.rotation_euler = (math.radians(90), 0, 0)  # looks along +Y at the front of the figure
    scene.camera = cam

    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x, scene.render.resolution_y = w, h
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.display.shading.light = "FLAT"
    scene.display.shading.color_type = "SINGLE"
    scene.display.shading.single_color = (1, 1, 1)
    scene.display.render_aa = "32"
    scene.view_settings.view_transform = "Standard"
    scene.render.filepath = tmp_png
    bpy.ops.render.render(write_still=True, scene=scene.name)
    return px_per_m, centre_x, centre_z


def _dilate(alpha, radius):
    out = alpha.copy()
    r = int(math.ceil(radius))
    for dy in range(-r, r + 1):
        for dx in range(-r, r + 1):
            d = math.hypot(dx, dy)
            if d > radius + 0.5:
                continue
            weight = min(1.0, radius + 0.5 - d)
            shifted = np.roll(np.roll(alpha, dy, axis=0), dx, axis=1) * weight
            np.maximum(out, shifted, out=out)
    return out


def compose(tmp_png, out_png):
    img = bpy.data.images.load(tmp_png, check_existing=False)
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
    bpy.data.images.remove(img)
    a = px[..., 3]
    d = _dilate(a, OUTLINE_PX * SCALE)
    fill = np.array(FILL, dtype=np.float32) / 255.0
    edge = np.array(EDGE, dtype=np.float32) / 255.0
    rgb = fill[None, None, :] * a[..., None] + edge[None, None, :] * (1.0 - a[..., None])
    out = np.concatenate([rgb, d[..., None]], axis=2)
    res = bpy.data.images.new("UI_EquipmentSilhouette", w, h, alpha=True)
    res.colorspace_settings.name = "sRGB"
    res.pixels = out.ravel()
    res.filepath_raw = out_png
    res.file_format = "PNG"
    res.save()
    bpy.data.images.remove(res)
    rows = np.where(a.max(axis=1) > 0.5)[0]  # Blender pixel rows run bottom-up
    return {"size": [w, h], "figure_rows_2x_from_top": [int(h - 1 - rows.max()), int(h - 1 - rows.min())]}


def run(repo):
    repo = os.path.abspath(repo)
    scene = _new_scene()
    _DOWN.clear()
    # The importer links into the context scene, which stays the previous one inside an MCP call and in `blender -b`:
    # move whatever it created into our scene.
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(repo, FBX))
    for ob in set(bpy.data.objects) - before:
        for coll in list(ob.users_collection):
            coll.objects.unlink(ob)
        scene.collection.objects.link(ob)
    arm_ob = next(o for o in scene.objects if o.type == "ARMATURE")
    meshes = [o for o in scene.objects if o.type == "MESH"]
    lowered = pose_a(arm_ob, meshes)
    tmp = os.path.join(bpy.app.tempdir or os.environ.get("TEMP", "."), "equip_silhouette_mask.png")
    px_per_m, cx, cz = render_mask(scene, meshes, tmp)

    # Report where the hands landed (rect-local, on-screen px) so the fit can be checked against DESIGN.md.
    _, _, _, z_max = _mesh_bounds(meshes)
    hands = {}
    for side in ("Left", "Right"):
        p = _hand_point(arm_ob, side)
        hands[side] = [round(RECT_W / 2 + (p.x - cx) * px_per_m / SCALE, 1),
                       round(HEAD_TOP_Y + (z_max - p.z) * px_per_m / SCALE, 1)]
    info = compose(tmp, os.path.join(repo, OUT))
    info.update({"arm_lowered_deg": round(lowered, 1), "hands_rect_px": hands, "out": OUT})
    return info
