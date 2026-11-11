"""Split a Mixamo-rigged character into a Head mesh and a Body mesh on the same skeleton.

Cuts the skinned mesh with a level plane (world height in metres), separates the two sides into
`<name>_head` / `<name>_body`, gives both the smooth vertex normals of the uncut mesh (so the seam
has no lighting crease once Unity imports normals) and exports one FBX: armature + both meshes,
no animation, no leaf bones.

Run inside Blender (Blender MCP `execute_blender_code`: exec this file after setting SRC / DST / CUT_M),
or from the command line:
    blender -b -P split_head_body.py -- <src.fbx> <dst.fbx> <cut_height_m>

Base hero (2026-11-03): CUT_M = 1.585 (between Neck 1.55 m and Head 1.63 m, just under the jaw).
Mixamo FBX import puts the armature at scale 0.01, so world units are "metres / 100" — handled below.
"""
import math
import re
import sys

import bmesh
import bpy
from mathutils import Vector

SCENE = "HeadBodySplit"


def _import(path, scene):
    before = set(scene.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in scene.objects if o not in before]
    arms = [o for o in new if o.type == 'ARMATURE']
    meshes = [o for o in new if o.type == 'MESH']
    if len(arms) != 1 or not meshes:
        raise ValueError(f"{path}: expected 1 armature and at least 1 mesh")
    arm, mesh = arms[0], meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.select_all(action='DESELECT')
        for obj in meshes:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = mesh
        bpy.ops.object.join()
    if arm.animation_data:
        arm.animation_data.action = None
    for bone in arm.pose.bones:
        bone.matrix_basis.identity()
    arm.data.pose_position = 'REST'
    bpy.context.view_layer.update()
    return arm, mesh, new


def _world_unit(arm):
    # Mixamo FBX → Blender: armature scaled 0.01, so 1 m of character = 0.01 world units.
    return arm.matrix_world.to_scale().x


def _keep_side(obj, head_faces, keep_head):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if (f.index in head_faces) != keep_head], context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()


def _boundary_edges(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    n = sum(1 for e in bm.edges if e.is_boundary)
    bm.free()
    return n


def split(src, dst, cut_m, neck_radius_m=None, neck_slope=2.0):
    if bpy.context.window is not None:  # interactive (MCP): work in a dedicated scene
        scene = bpy.data.scenes.new(SCENE)
        bpy.context.window.scene = scene
    else:  # background (blender -b): import/export only reach the context scene
        scene = bpy.context.scene
    for o in list(scene.objects):
        bpy.data.objects.remove(o, do_unlink=True)

    arm, body, _ = _import(src, scene)
    base_name = re.sub(r"_(head|body)$", "", re.sub(r"\.\d{3}$", "", body.name))

    # Preserve face-corner normals as interpolated BMesh loop data. This follows the
    # actual cut edge, including UV/hard-normal splits, rather than averaging two
    # spatially nearest vertices (which may belong to unrelated triangles).
    if not body.data.has_custom_normals:
        body.data.shade_smooth()
    corner_normals = [n.vector.copy() for n in body.data.corner_normals]

    # Bisect in mesh-local space.
    A = body.matrix_world
    plane_co = A.inverted() @ Vector((0, 0, cut_m * _world_unit(arm)))
    plane_no = (A.to_3x3().transposed() @ Vector((0, 0, 1))).normalized()
    bm = bmesh.new()
    bm.from_mesh(body.data)
    normal_layer = bm.loops.layers.float_vector.new('_split_source_normal')
    for f in bm.faces:
        for loop in f.loops:
            loop[normal_layer] = corner_normals[loop.index]
    res = bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], dist=1e-7,
                                 plane_co=plane_co, plane_no=plane_no)
    planes = [(plane_co, plane_no)]
    if neck_radius_m is not None:
        # A convex cup keeps the neck/upper chest with the head, without taking
        # the shoulders and arms along when lowering the bottom plane. The cut
        # is underneath the garment. Every plane is expressed in mesh space.
        for i in range(16):
            angle = i * math.tau / 16
            outward = Vector((math.cos(angle), math.sin(angle), 0))
            world_co = Vector((0, 0, cut_m * _world_unit(arm))) + outward * (neck_radius_m * _world_unit(arm))
            local_co = A.inverted() @ world_co
            local_no = (A.to_3x3().transposed() @ Vector((-outward.x * neck_slope, -outward.y * neck_slope, 1))).normalized()
            candidates = [f for f in bm.faces if all((f.calc_center_median() - co).dot(no) >= -1e-9 for co, no in planes)]
            geom = set(candidates)
            for f in candidates:
                geom.update(f.edges)
                geom.update(f.verts)
            bmesh.ops.bisect_plane(bm, geom=list(geom), dist=1e-9, plane_co=local_co, plane_no=local_no)
            planes.append((local_co, local_no))
    bm.faces.ensure_lookup_table()
    head_faces = {f.index for f in bm.faces if all((f.calc_center_median() - co).dot(no) > -1e-9 for co, no in planes)}
    bm.to_mesh(body.data)
    bm.free()
    body.data.update()

    head = body.copy()
    head.data = body.data.copy()
    scene.collection.objects.link(head)
    head.name = head.data.name = base_name + "_head"
    body.name = body.data.name = base_name + "_body"
    _keep_side(head, head_faces, True)
    _keep_side(body, head_faces, False)

    for obj in (head, body):
        obj.data.shade_smooth()
        attribute = obj.data.attributes['_split_source_normal']
        obj.data.normals_split_custom_set([value.vector.normalized() for value in attribute.data])
        obj.data.attributes.remove(attribute)

    report = {"head_verts": len(head.data.vertices), "body_verts": len(body.data.vertices),
              "head_ring_edges": _boundary_edges(head), "body_ring_edges": _boundary_edges(body),
              "cut_m": cut_m, "neck_radius_m": neck_radius_m, "neck_slope": neck_slope}

    bpy.ops.object.select_all(action='DESELECT')
    for o in (arm, head, body):
        o.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(filepath=dst, use_selection=True, object_types={'ARMATURE', 'MESH'},
                             add_leaf_bones=False, bake_anim=False, mesh_smooth_type='OFF',
                             use_mesh_modifiers=False, path_mode='STRIP')
    return report


if __name__ == "__main__" and "--" in sys.argv:
    a = sys.argv[sys.argv.index("--") + 1:]
    print(split(a[0], a[1], float(a[2])))
