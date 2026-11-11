"""Fit the immutable Meshy Company grunt to the canonical player skeleton.

Run run() through the connected Blender. All work occurs in a temporary scene;
source assets, the active scene and canonical head/body remain untouched.
"""
from pathlib import Path
import json
import math
import runpy
import tempfile
import os
import bpy
import bmesh
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform

ROOT = Path(__file__).resolve().parents[2]
FOLDER = ROOT / 'Assets/_Game/Art/Characters/Humanoids/models/Company/Grunt armor'
SOURCE = FOLDER / 'Meshy_AI_Armored_Medieval_Guard_20261111_texture.fbx'
CANONICAL = ROOT / 'Assets/_Game/Art/Characters/Humanoids/models/test base hero/base hero canonical split.fbx'


def interpolate(x, points):
    for (a, va), (b, vb) in zip(points, points[1:]):
        if x <= b:
            return va + (vb-va)*max(0, min(1, (x-a)/(b-a)))
    return points[-1][1]


def conforming_neck(source, profile, destination):
    """Cover the complete posed skin stump with its own canonical influences.

    A single sampled ring cannot cover the shoulder-weighted lower neck during
    overhead attacks. This cloth shell follows the actual canonical skin mesh,
    while keeping all skin and the unchanged exposed seam on the player head.
    """
    previous = bpy.context.window.scene
    scene = bpy.data.scenes.new('CompanyConformingCollar')
    bpy.context.window.scene = scene
    try:
        bpy.ops.import_scene.fbx(filepath=str(source))
        armor = next(o for o in scene.objects if o.type == 'MESH')
        rig = next(o for o in scene.objects if o.type == 'ARMATURE')
        rig.data.pose_position = 'REST'
        before = set(scene.objects)
        bpy.ops.import_scene.fbx(filepath=str(profile))
        imported = set(scene.objects)-before
        neck = next(o for o in imported if o.type == 'MESH' and '_head' in o.name)
        matrix = neck.matrix_world.copy()
        inverse = matrix.inverted()
        bm = bmesh.new()
        try:
            bm.from_mesh(neck.data)
            # Higher behind/along the neck, lower under the chin. This covers
            # the skin's shoulder blend above the old exposed join without
            # extending cloth onto the front jaw.
            bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),
                plane_co=inverse@Vector((0,0,.01606)),plane_no=matrix.to_3x3().transposed()@Vector((0,-.233,1)),
                clear_outer=True,dist=1e-9)
            bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),
                plane_co=inverse@Vector((0,0,.01490)),plane_no=matrix.to_3x3().transposed()@Vector((0,0,1)),
                clear_inner=True,dist=1e-9)
            uv = bm.loops.layers.uv.active
            for vertex in bm.verts:
                p=matrix@vertex.co
                radius=math.hypot(p.x,p.y)
                if radius>0:
                    p.x*=1+.00004/radius
                    p.y*=1+.00004/radius
                vertex.co=inverse@p
            # Cloth UVs occupy one inspected charcoal patch of the source atlas.
            for face in bm.faces:
                face.material_index=0
                face.smooth=True
                for loop in face.loops:
                    p=matrix@loop.vert.co*100
                    loop[uv].uv=(.352+.018*(math.atan2(p.y,p.x)/math.tau+.5),.84+.018*(p.z-1.49)/.14)
            bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
            # Round the visible cut edge with an inward return, carrying the
            # exact same skin influences so no pale sawtooth shows at the rim.
            deform=bm.verts.layers.deform.active
            upper_edges=[e for e in bm.edges if e.is_boundary and
                all((matrix@v.co).z>.0157 for v in e.verts)]
            upper_vertices={v for e in upper_edges for v in e.verts}
            inner={}
            for vertex in upper_vertices:
                p=matrix@vertex.co
                radius=math.hypot(p.x,p.y)
                if radius>0:
                    p.x*=1-.000025/radius;p.y*=1-.000025/radius
                p.z-=.000015
                new=bm.verts.new(inverse@p)
                for group,weight in vertex[deform].items():new[deform][group]=weight
                inner[vertex]=new
            for edge in upper_edges:
                a,b=edge.verts
                face=bm.faces.new((a,b,inner[b],inner[a]))
                face.smooth=True
                for loop in face.loops:
                    p=matrix@loop.vert.co*100
                    loop[uv].uv=(.352+.018*(math.atan2(p.y,p.x)/math.tau+.5),.855)
            bm.to_mesh(neck.data)
        finally:
            bm.free()
        neck.data.materials.clear()
        neck.data.materials.append(armor.data.materials[0])
        if neck.data.has_custom_normals:
            neck.data.normals_split_custom_set([(0,0,0)]*len(neck.data.loops))
        neck_matrix=neck.matrix_world.copy()
        neck.parent=rig
        neck.matrix_world=neck_matrix
        for modifier in neck.modifiers:
            if modifier.type=='ARMATURE':modifier.object=rig
        for obj in imported:
            if obj!=neck:
                bpy.data.objects.remove(obj,do_unlink=True)
        bpy.ops.object.select_all(action='DESELECT')
        armor.select_set(True);neck.select_set(True)
        bpy.context.view_layer.objects.active=armor
        bpy.ops.object.join()
        armor.name=armor.data.name='CompanyGrunt'
        # Re-export with the immutable authority, avoiding accumulated FBX
        # rest-axis round-trip drift in the skeleton's bind transforms.
        before=set(scene.objects)
        bpy.ops.import_scene.fbx(filepath=str(CANONICAL))
        canonical_objects=set(scene.objects)-before
        final_rig=next(o for o in canonical_objects if o.type=='ARMATURE')
        final_rig.data.pose_position='REST'
        if final_rig.animation_data:final_rig.animation_data.action=None
        for bone in final_rig.pose.bones:bone.matrix_basis.identity()
        armor_matrix=armor.matrix_world.copy()
        armor.parent=final_rig;armor.matrix_world=armor_matrix
        for modifier in armor.modifiers:
            if modifier.type=='ARMATURE':modifier.object=final_rig
        for obj in canonical_objects:
            if obj!=final_rig:bpy.data.objects.remove(obj,do_unlink=True)
        bpy.data.objects.remove(rig,do_unlink=True)
        rig=final_rig
        bpy.ops.object.select_all(action='DESELECT')
        armor.select_set(True);rig.select_set(True)
        bpy.context.view_layer.objects.active=rig
        bpy.ops.export_scene.fbx(filepath=str(destination),use_selection=True,
            object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=False,
            use_mesh_modifiers=False,mesh_smooth_type='OFF',path_mode='STRIP')
        return len(armor.data.vertices)
    finally:
        bpy.context.window.scene=previous
        for obj in list(scene.objects):bpy.data.objects.remove(obj,do_unlink=True)
        bpy.data.scenes.remove(scene)


def run():
    previous = bpy.context.window.scene
    scene = bpy.data.scenes.new('CompanyGruntPreparation')
    bpy.context.window.scene = scene
    try:
        bpy.ops.import_scene.fbx(filepath=str(CANONICAL))
        rig = next(o for o in scene.objects if o.type == 'ARMATURE')
        body = next(o for o in scene.objects if o.type == 'MESH' and '_body' in o.name)
        rig.data.pose_position = 'REST'
        if rig.animation_data:
            rig.animation_data.action = None
        for bone in rig.pose.bones:
            bone.matrix_basis.identity()
        bpy.context.view_layer.update()
        before = set(scene.objects)
        bpy.ops.import_scene.fbx(filepath=str(SOURCE))
        armor = next(o for o in scene.objects if o not in before and o.type == 'MESH')
        source_matrix = armor.matrix_world.copy()
        # Source is centered at the waist, in metres. Canonical import world is
        # 1/100 game metres. Align feet and modestly shorten the generated body.
        for vertex in armor.data.vertices:
            p = source_matrix @ vertex.co
            p.z += .9510740042
            p *= .985
            ax = abs(p.x)
            if p.z > 1.15:
                blend = max(0, min(1, (ax-.19)/.10))
                p.z += interpolate(ax, [(0,0),(.2,-.02),(.3,-.01),(.45,.025),(.6,.04),(.72,.065),(.95,.04)]) * blend
                p.y += .03*blend
            vertex.co = p/100
        armor.matrix_world = Matrix.Identity(4)
        # Cut off only the generated neck/head. The shoulder mantle survives.
        bm = bmesh.new()
        try:
            bm.from_mesh(armor.data)
            geom = {v for v in bm.verts if math.hypot(v.co.x,v.co.y)<.0016}
            edges = [e for e in bm.edges if all(v in geom for v in e.verts)]
            faces = [f for f in bm.faces if all(v in geom for v in f.verts)]
            bmesh.ops.bisect_plane(bm, geom=list(geom)+edges+faces, dist=1e-8,
                plane_co=(0,0,.0155), plane_no=(0,0,1), clear_outer=True)
            bm.to_mesh(armor.data)
        finally:
            bm.free()
        # Transfer barycentric body weights in common world space. The nearest
        # triangle interpolation avoids the discontinuities of nearest vertices.
        body.data.calc_loop_triangles()
        positions = [body.matrix_world@v.co for v in body.data.vertices]
        triangles = [tuple(t.vertices) for t in body.data.loop_triangles]
        tree = BVHTree.FromPolygons(positions, triangles, all_triangles=True)
        groups = {g.index: armor.vertex_groups.new(name=g.name).index for g in body.vertex_groups}
        for vertex in armor.data.vertices:
            q, normal, face, distance = tree.find_nearest(vertex.co)
            ids = triangles[face]
            a,b,c = [positions[i] for i in ids]
            bary = barycentric_transform(q,a,b,c,Vector((1,0,0)),Vector((0,1,0)),Vector((0,0,1)))
            weights = {}
            for index, factor in zip(ids,bary):
                for influence in body.data.vertices[index].groups:
                    weights[influence.group] = weights.get(influence.group,0)+max(0,factor)*influence.weight
            # Garment collar must not follow the chin through Head influence.
            if vertex.co.z > .0149 and math.hypot(vertex.co.x,vertex.co.y)<.0016:
                head = body.vertex_groups.get('mixamorig:Head')
                neck = body.vertex_groups.get('mixamorig:Neck')
                if head and neck:
                    weights[neck.index] = weights.get(neck.index,0)+weights.pop(head.index,0)
            weights = sorted(weights.items(),key=lambda x:x[1],reverse=True)[:4]
            total = sum(w for _,w in weights)
            if total <= 0:
                raise ValueError('Unweighted outfit vertex')
            for group, weight in weights:
                if weight/total > 1e-6:
                    armor.vertex_groups[groups[group]].add([vertex.index],weight/total,'REPLACE')
        armor.parent = rig
        armor.matrix_world = Matrix.Identity(4)
        modifier = armor.modifiers.new('CanonicalArmature','ARMATURE')
        modifier.object = rig
        armor.name = armor.data.name = 'CompanyGrunt'
        bpy.ops.object.select_all(action='DESELECT')
        armor.select_set(True)
        rig.select_set(True)
        bpy.context.view_layer.objects.active = rig
        output = FOLDER/'CompanyGrunt_Fitted.fbx'
        # Use the existing measured neck finishing on the fitted outfit only.
        # This does not regenerate or overwrite the canonical player's head.
        with tempfile.TemporaryDirectory(dir=str(ROOT/'Temp')) as staging:
            fitted = Path(staging)/'fitted.fbx'
            finished = Path(staging)/'finished.fbx'
            bpy.ops.export_scene.fbx(filepath=str(fitted),use_selection=True,
                object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=False,
                use_mesh_modifiers=False,mesh_smooth_type='OFF',path_mode='STRIP')
            config = json.loads((ROOT/'Tools/Blender/configs/leather_neck.json').read_text(encoding='utf-8-sig'))
            config.update(outfit_name='CompanyGrunt',collar_cut_radius_m=.105,
                collar_uv_origin=[.535,.652],collar_uv_span=[.058,.058],
                concentric_collar_openings=True)
            profile = ROOT/'Assets/_Game/Art/Characters/Humanoids/models/test base hero/base hero split.fbx'
            report = runpy.run_path(str(ROOT/'Tools/Blender/prepare_armor_neck.py'))['outfit'](
                config, fitted, CANONICAL, profile, finished)
            staged_final=Path(staging)/'final.fbx'
            report['with_conforming_sleeve_vertices']=conforming_neck(finished,profile,staged_final)
            os.replace(staged_final,output)
        return {'output':str(output),'vertices_before_collar':len(armor.data.vertices),
                'groups':len(armor.vertex_groups),'source_preserved':True,'collar':report}
    finally:
        bpy.context.window.scene = previous
        for obj in list(scene.objects):
            bpy.data.objects.remove(obj,do_unlink=True)
        bpy.data.scenes.remove(scene)
