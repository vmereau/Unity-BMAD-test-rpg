"""Canonical head-owned neck and fitted leather collar. Run run(config_path) in Blender.

This finishes an already fitted/weighted outfit; it does not auto-rig arbitrary
characters. Source assets are immutable. Dimensions are game metres (FBX / 100).
"""
import json
import math
from pathlib import Path
import runpy
import shutil
import tempfile
import os

import bpy
import bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree
from mathutils.geometry import barycentric_transform


def reset(rig):
    if rig.animation_data:
        rig.animation_data.action = None
    for bone in rig.pose.bones:
        bone.matrix_basis.identity()
    rig.data.pose_position = 'REST'


def export(path, objects, rig):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True,
        object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False, bake_anim=False,
        use_mesh_modifiers=False, mesh_smooth_type='OFF', path_mode='STRIP')


def head_neck(config, source, destination):
    """Preserve original skin UVs and head silhouette; inset the hidden neck cup.

    The original body conceals the inset skin when unequipped. Its surface and
    its exposed join with the original head remain unchanged.
    """
    original_scene = bpy.context.window.scene
    original_scenes = set(bpy.data.scenes)
    module = runpy.run_path(str(Path(__file__).with_name('split_head_body.py')))
    try:
        module['split'](str(source), str(destination), config['neck_bottom_m'], .10, 2.)
        scene = bpy.context.scene
        head = next(o for o in scene.objects if o.type == 'MESH' and '_head' in o.name)
        body = next(o for o in scene.objects if o.type == 'MESH' and '_body' in o.name)
        rig = next(o for o in scene.objects if o.type == 'ARMATURE')
        before = set(scene.objects)
        bpy.ops.import_scene.fbx(filepath=str(source))
        added = [o for o in scene.objects if o not in before]
        original_head = next(o for o in added if o.type == 'MESH' and '_head' in o.name)
        original_body = next(o for o in added if o.type == 'MESH' and '_body' in o.name)
        scale, seam = config['world_to_m'], config['original_seam_m']
        ring = [(original_head.matrix_world @ v.co) * scale for v in original_head.data.vertices
                if abs((original_head.matrix_world @ v.co).z * scale - seam) < .00001]
        if len(ring) < 3:
            raise ValueError('Canonical source has no neck ring at the configured height.')
        inverse = head.matrix_world.inverted()
        saved_normals = [n.vector.copy() for n in head.data.corner_normals]
        for vertex in head.data.vertices:
            p = (head.matrix_world @ vertex.co) * scale
            if p.z >= seam - .000001:
                continue
            angle = math.atan2(p.y, p.x)
            nearest = min(ring, key=lambda q: abs(math.atan2(
                math.sin(math.atan2(q.y, q.x)-angle), math.cos(math.atan2(q.y, q.x)-angle))))
            limit = math.hypot(nearest.x, nearest.y) * (
                1 - config['neck_inset'] * min((seam-p.z)/.025, 1))
            radius = math.hypot(p.x, p.y)
            if radius > limit:
                p.x *= limit/radius
                p.y *= limit/radius
            vertex.co = inverse @ (p/scale)
        # Geometry edits change Blender's encoded custom-normal basis. Restore
        # the source vectors explicitly, and snap near-plane cut fragments to
        # the canonical interface (including every UV-split corner).
        reference = KDTree(len(original_body.data.vertices))
        for i,vertex in enumerate(original_body.data.vertices):
            reference.insert(original_body.matrix_world@vertex.co*scale,i)
        reference.balance()
        body_loops = {}
        for loop in original_body.data.loops:
            body_loops.setdefault(loop.vertex_index,[]).append(loop.index)
        source_uv = original_body.data.uv_layers.active.data
        source_normals = original_body.data.corner_normals
        uv = head.data.uv_layers.active.data
        matched = {}
        for vertex in head.data.vertices:
            p = head.matrix_world@vertex.co*scale
            if abs(p.z-seam) > .00001:
                continue
            position,index,distance = reference.find(p)
            if distance > .0001:
                raise ValueError('Head skin join lies outside canonical seam tolerance.')
            vertex.co = inverse@(position/scale)
            for group in head.vertex_groups:
                group.remove([vertex.index])
            for influence in original_body.data.vertices[index].groups:
                name = original_body.vertex_groups[influence.group].name
                head.vertex_groups[name].add([vertex.index],influence.weight,'REPLACE')
            matched[vertex.index] = index
        for loop in head.data.loops:
            if loop.vertex_index not in matched:
                continue
            index = matched[loop.vertex_index]
            candidates = body_loops[index]
            nearest_loop = min(candidates,key=lambda j:(source_uv[j].uv-uv[loop.index].uv).length_squared)
            uv[loop.index].uv = source_uv[nearest_loop].uv
            saved_normals[loop.index] = source_normals[nearest_loop].vector.copy()
        head.data.normals_split_custom_set(saved_normals)
        # Close the hidden neck stump. Looking down inside a loose collar must
        # reveal skin, never the back of an open head mesh.
        bm = bmesh.new()
        try:
            bm.from_mesh(head.data)
            layer = bm.loops.layers.float_vector.new('_head_cap_normals')
            ns = [n.vector.copy() for n in head.data.corner_normals]
            for face in bm.faces:
                for loop in face.loops:
                    loop[layer] = ns[loop.index]
            seam_vertices = [v for v in bm.verts if abs((head.matrix_world@v.co).z*scale-seam)<.00001]
            bmesh.ops.remove_doubles(bm, verts=seam_vertices, dist=.000001/scale)
            lower_edges = [e for e in bm.edges if e.is_boundary and
                max((head.matrix_world@v.co).z*scale for v in e.verts)<seam+.00001]
            # UV seams are already split at the original interface; only the
            # new lower component is capped, not those coincident upper edges.
            caps = bmesh.ops.holes_fill(bm, edges=lower_edges, sides=0)['faces']
            triangles = bmesh.ops.triangulate(bm, faces=list(caps))['faces']
            triangles = set(triangles) | {face for face in caps if face.is_valid}
            uv_layer = bm.loops.layers.uv.active
            down = (head.matrix_world.to_3x3().transposed()@Vector((0,0,-1))).normalized()
            for face in triangles:
                face.smooth = False
                for loop,uv_value in zip(face.loops,((.70,.68),(.701,.68),(.70,.681))):
                    loop[layer] = down
                    loop[uv_layer].uv = uv_value
            bm.to_mesh(head.data)
        finally:
            bm.free()
        attribute = head.data.attributes['_head_cap_normals']
        head.data.normals_split_custom_set([v.vector.normalized() for v in attribute.data])
        head.data.attributes.remove(attribute)
        # Restore the complete original body. Below the old join the additional
        # neck is inside this body, not a second coincident exposed skin surface.
        body.data = original_body.data.copy()
        body.matrix_world = original_body.matrix_world.copy()
        for obj in added:
            bpy.data.objects.remove(obj, do_unlink=True)
        head.name = head.data.name = 'base_hero_head'
        body.name = body.data.name = 'base_hero_body'
        export(destination, (head, body, rig), rig)
        return {'canonical_ring_vertices': len(ring), 'head_vertices': len(head.data.vertices)}
    finally:
        bpy.context.window.scene = original_scene
        for scene in set(bpy.data.scenes)-original_scenes:
            for obj in list(scene.objects):
                bpy.data.objects.remove(obj, do_unlink=True)
            bpy.data.scenes.remove(scene)


def outfit(config, source, canonical, profile, destination):
    profile_height = config['collar_profile_m']
    max_profile_radius = config['collar_max_profile_radius_m']
    clearance = config['collar_clearance_m']
    if (not all(math.isfinite(value) for value in (profile_height, max_profile_radius, clearance))
            or not (config['collar_cut_m'] < profile_height
                    and profile_height + .001 < config['original_seam_m'])
            or clearance <= 0 or max_profile_radius <= clearance):
        raise ValueError('Collar profile must stay below the canonical skin seam, above the cut, '
                         'with finite positive clearance and a larger radius limit.')
    previous = bpy.context.window.scene
    scene = bpy.data.scenes.new('ArmorNeckPreparation')
    try:
        bpy.context.window.scene = scene
        bpy.ops.import_scene.fbx(filepath=str(source))
        meshes = [o for o in scene.objects if o.type == 'MESH']
        rigs = [o for o in scene.objects if o.type == 'ARMATURE']
        if len(meshes) != 1 or len(rigs) != 1:
            raise ValueError('Expected one fitted outfit mesh and one rig.')
        armor, old_rig = meshes[0], rigs[0]
        reset(old_rig)
        before = set(scene.objects)
        bpy.ops.import_scene.fbx(filepath=str(canonical))
        added = [o for o in scene.objects if o not in before]
        rig = next(o for o in added if o.type == 'ARMATURE')
        reset(rig)
        # Always export with the authoritative rig, not another FBX round trip.
        matrix = armor.matrix_world.copy()
        armor.parent = rig
        armor.matrix_world = matrix
        for modifier in armor.modifiers:
            if modifier.type == 'ARMATURE':
                modifier.object = rig
        for obj in added:
            if obj != rig:
                bpy.data.objects.remove(obj, do_unlink=True)
        bpy.data.objects.remove(old_rig, do_unlink=True)
        before = set(scene.objects)
        bpy.ops.import_scene.fbx(filepath=str(profile))
        profile_objects = [o for o in scene.objects if o not in before]
        head = next(o for o in profile_objects if o.type == 'MESH' and '_head' in o.name)
        scale = config['world_to_m']
        points = [(head.matrix_world @ v.co)*scale for v in head.data.vertices]
        head.data.calc_loop_triangles()
        head_triangles = list(head.data.loop_triangles)
        surface = BVHTree.FromPolygons(points, [t.vertices[:] for t in head_triangles], all_triangles=True)
        kd = KDTree(len(points))
        for i, p in enumerate(points):
            kd.insert(p, i)
        kd.balance()
        bm = bmesh.new()
        try:
            bm.from_mesh(armor.data)
            layer = bm.loops.layers.float_vector.new('_outfit_normals')
            normals = [n.vector.copy() for n in armor.data.corner_normals]
            for face in bm.faces:
                for loop in face.loops:
                    loop[layer] = normals[loop.index]
            remove = [f for f in bm.faces
                if (matrix @ f.calc_center_median()).z*scale > config['collar_cut_m']
                and math.hypot((matrix @ f.calc_center_median()).x,
                    (matrix @ f.calc_center_median()).y)*scale < config['collar_cut_radius_m']]
            if not remove:
                raise ValueError('No damaged collar found inside the configured fitting region.')
            removed_count = len(remove)
            bmesh.ops.delete(bm, geom=remove, context='FACES')
            bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
            bm.to_mesh(armor.data)
        finally:
            bm.free()
        attribute = armor.data.attributes['_outfit_normals']
        armor.data.normals_split_custom_set([v.vector.normalized() for v in attribute.data])
        armor.data.attributes.remove(attribute)
        # Find the exterior opening by its winding around the neck axis. Small
        # pre-existing folded islands have zero winding and must not define fit.
        bm = bmesh.new()
        try:
            bm.from_mesh(armor.data)
            uv = bm.loops.layers.uv.active
            deform = bm.verts.layers.deform.active
            nl = bm.loops.layers.float_vector.new('_collar_normals')
            ns = [n.vector.copy() for n in armor.data.corner_normals]
            for face in bm.faces:
                for loop in face.loops:
                    loop[nl] = ns[loop.index]
            remaining = {e for e in bm.edges if e.is_boundary}
            openings = []
            while remaining:
                seed = remaining.pop()
                edges, vertices, todo = {seed}, set(seed.verts), list(seed.verts)
                while todo:
                    vertex = todo.pop()
                    for edge in vertex.link_edges:
                        if edge in remaining:
                            remaining.remove(edge)
                            edges.add(edge)
                            for other in edge.verts:
                                if other not in vertices:
                                    vertices.add(other)
                                    todo.append(other)
                if min((matrix@v.co).z*scale for v in vertices) < config['collar_cut_m']-.02:
                    continue
                winding = 0
                for edge in edges:
                    loop = next(l for l in edge.link_faces[0].loops if l.edge == edge)
                    a, b = matrix@loop.vert.co, matrix@loop.link_loop_next.vert.co
                    winding += math.atan2(a.x*b.y-a.y*b.x,a.x*b.x+a.y*b.y)
                openings.append((abs(winding/math.tau), edges, vertices))
            candidates = [o for o in openings if o[0] > .9]
            if len(candidates) > 1 and config.get('concentric_collar_openings', False):
                candidates = [max(candidates, key=lambda opening: sum(
                    math.hypot((matrix@v.co).x, (matrix@v.co).y)
                    for v in opening[2])/len(opening[2]))]
            if len(candidates) != 1:
                raise ValueError('Expected one exterior neck opening, found '+str(len(candidates)))
            _, edges, vertices = candidates[0]
            # Close incidental folded holes beneath the collar with their own
            # adjacent UVs; the new exterior loft does not use those islands.
            for winding, hole_edges, hole_vertices in openings:
                if hole_edges == edges:
                    continue
                uv_by_vertex = {}
                for edge in hole_edges:
                    for loop in edge.link_faces[0].loops:
                        if loop.vert in hole_vertices:
                            uv_by_vertex[loop.vert] = loop[uv].uv.copy()
                caps = bmesh.ops.holes_fill(bm, edges=list(hole_edges), sides=0)['faces']
                bm.normal_update()
                for face in caps:
                    outward = matrix@face.calc_center_median()
                    if face.normal.dot(matrix.to_3x3().inverted()@Vector((outward.x,outward.y,0))) < 0:
                        face.normal_flip()
                    face.smooth = True
                    for loop in face.loops:
                        p = matrix@loop.vert.co*scale
                        loop[uv].uv = (.4+p.x*.1,.85+p.y*.1)
                        loop[nl] = face.normal
            base = {v:(matrix@v.co)*scale for v in vertices}
            inverse = matrix.inverted()
            neighbors = {v:[e.other_vert(v) for e in edges if v in e.verts] for v in vertices}
            if any(len(n) != 2 for n in neighbors.values()):
                raise ValueError('Exterior neck opening is branched.')
            first = min(vertices,key=lambda v:math.atan2(base[v].y,base[v].x))
            ordered = [first]
            previous_vertex = None
            current = first
            while True:
                following = next(v for v in neighbors[current] if v != previous_vertex)
                if following == first:
                    break
                ordered.append(following)
                previous_vertex,current = current,following
            # Remove the one-triangle sawtooth from the cut edge while keeping
            # the surrounding source garment. Two local smoothing passes.
            for _ in range(2):
                base = {v:base[v]*.5+(base[neighbors[v][0]]+base[neighbors[v][1]])*.25 for v in vertices}
            for vertex in vertices:
                vertex.co = inverse@(base[vertex]/scale)
            lengths = [(base[ordered[(i+1)%len(ordered)]]-base[v]).length for i,v in enumerate(ordered)]
            total = sum(lengths)
            winding = sum(math.atan2(base[a].x*base[b].y-base[a].y*base[b].x,
                base[a].x*base[b].x+base[a].y*base[b].y)
                for a,b in zip(ordered,ordered[1:]+ordered[:1]))
            sign = 1 if winding > 0 else -1
            arc, cursor = {},0
            for vertex,length in zip(ordered,lengths):
                arc[vertex] = sign*math.tau*cursor/total
                cursor += length
            phases = [math.atan2(base[v].y,base[v].x)-arc[v] for v in vertices]
            phase = math.atan2(sum(math.sin(p) for p in phases),sum(math.cos(p) for p in phases))
            # A clothing rim follows the neck, not the jaw. The original high
            # profile hit the chin and transferred mostly Head influence,
            # producing a flap which lifted and twisted with the face.
            neck_groups = [group.index for group in armor.vertex_groups
                           if group.name.rsplit(':', 1)[-1] == 'Neck']
            if len(neck_groups) != 1:
                raise ValueError('Outfit requires one unambiguous canonical Neck group.')
            neck_group = neck_groups[0]
            target, top_weights = {}, {}
            max_sampled_radius = 0
            for vertex in vertices:
                p = base[vertex]
                theta = phase+arc[vertex]
                direction = Vector((math.cos(theta),math.sin(theta),0))
                hit,_,triangle_index,_ = surface.ray_cast(Vector((0,0,profile_height)), direction, .25)
                if hit is None:
                    raise ValueError('Canonical head does not cover collar profile.')
                sample_radius = math.hypot(hit.x, hit.y)
                max_sampled_radius = max(max_sampled_radius, sample_radius)
                if sample_radius > max_profile_radius:
                    raise ValueError('Collar profile exceeds its neck radius limit '
                                     f'({sample_radius:.4f} m > {max_profile_radius:.4f} m); '
                                     'lower the profile or author a compatible neck interface.')
                target[vertex] = hit + direction*clearance
                triangle = head_triangles[triangle_index]
                factors = barycentric_transform(hit,*[points[i] for i in triangle.vertices],
                    Vector((1,0,0)),Vector((0,1,0)),Vector((0,0,1)))
                influences = {}
                for source_vertex,factor in zip(triangle.vertices,factors):
                    for group in head.data.vertices[source_vertex].groups:
                        name = head.vertex_groups[group.group].name
                        if name.rsplit(':', 1)[-1] == 'Head':
                            continue
                        bone = armor.vertex_groups.find(name)
                        influences[bone] = influences.get(bone,0)+max(0,factor)*group.weight
                if -1 in influences:
                    raise ValueError('Head and outfit use incompatible bone names.')
                total = sum(influences.values())
                top_weights[vertex] = ({bone:weight/total for bone,weight in influences.items()}
                                       if total > 1e-8 else {neck_group: 1.0})
            # t, inward offset, vertical offset: outside loft, rounded rim,
            # then a short inner lining whose lower edge is hidden by the neck.
            row_specs = [(0,0,0),(.3,0,0),(.65,0,0),(1,0,0),
                         (1,-.0015,.001),(1,-.003,0),(1,-.003,-.012),
                         (1,0,config['neck_bottom_m']-profile_height+.005)]
            rows = [{v:v for v in vertices}]
            for t,inward,dz in row_specs[1:]:
                row = {}
                for vertex in vertices:
                    p = base[vertex].lerp(target[vertex],t)
                    direction = Vector((p.x,p.y,0)).normalized()
                    p += direction*inward
                    p.z += dz
                    if len(rows) == len(row_specs)-1:
                        # Bury the lining's lower rim inside the closed skin
                        # stump instead of leaving a visible slot from above.
                        p.x *= .4
                        p.y *= .4
                    new = bm.verts.new(inverse@(p/scale))
                    for bone in set(vertex[deform].keys()) | set(top_weights[vertex]):
                        new[deform][bone] = (1-t)*vertex[deform].get(bone,0)+t*top_weights[vertex].get(bone,0)
                    row[vertex] = new
                rows.append(row)
            new_faces = []
            for edge in edges:
                loop = next(l for l in edge.link_faces[0].loops if l.edge == edge)
                a,b = loop.vert,loop.link_loop_next.vert
                uv0 = {a:loop[uv].uv.copy(),b:loop.link_loop_next[uv].uv.copy()}
                normal0 = {a:loop[nl].copy(),b:loop.link_loop_next[nl].copy()}
                edge_uv = uv0[b]-uv0[a]
                step = Vector((-edge_uv.y,edge_uv.x)).normalized()*.025
                center_uv = sum((l[uv].uv for l in loop.face.loops),Vector((0,0)))/len(loop.face.loops)
                if step.dot(center_uv-(uv0[a]+uv0[b])*.5) > 0:
                    step.negate()
                for ri in range(len(rows)-1):
                    face = bm.faces.new((rows[ri][b],rows[ri][a],rows[ri+1][a],rows[ri+1][b]))
                    face.smooth = True
                    new_faces.append(face)
                    for k,(corner,original) in enumerate(zip(face.loops,(b,a,a,b))):
                        t = row_specs[ri if k<2 else ri+1][0]
                        # New collar uses one contiguous leather atlas patch;
                        # extrapolating each Meshy island produces long stripes.
                        ua = (arc[a]/math.tau) % 1
                        ub = (arc[b]/math.tau) % 1
                        if ub-ua > .5:
                            ua += 1
                        elif ua-ub > .5:
                            ub += 1
                        u = ua if original == a else ub
                        v = (ri if k<2 else ri+1)/(len(rows)-1)
                        origin = config.get('collar_uv_origin', [.32, .80])
                        span = config.get('collar_uv_span', [.24, .13])
                        corner[uv].uv = (origin[0]+span[0]*u, origin[1]+span[1]*v)
                        corner[nl] = normal0[original]
            bm.normal_update()
            for face in new_faces:
                for loop in face.loops:
                    if loop.vert not in vertices:
                        loop[nl] = loop.vert.normal
            bm.to_mesh(armor.data)
            collar_quads = len(new_faces)
        finally:
            bm.free()
        attribute = armor.data.attributes['_collar_normals']
        armor.data.normals_split_custom_set([v.vector.normalized() for v in attribute.data])
        armor.data.attributes.remove(attribute)
        # Lock the triangle diagonals before UV validation; FBX/Unity may choose
        # another diagonal for nonplanar quads and recreate a collapsed island.
        bm = bmesh.new()
        try:
            bm.from_mesh(armor.data)
            layer = bm.loops.layers.float_vector.new('_triangle_normals')
            normals = [n.vector.copy() for n in armor.data.corner_normals]
            for face in bm.faces:
                for loop in face.loops:
                    loop[layer] = normals[loop.index]
            bmesh.ops.triangulate(bm, faces=list(bm.faces))
            bm.to_mesh(armor.data)
        finally:
            bm.free()
        attribute = armor.data.attributes['_triangle_normals']
        armor.data.normals_split_custom_set([v.vector.normalized() for v in attribute.data])
        armor.data.attributes.remove(attribute)
        # A few original Meshy triangles and hidden fold caps carry collapsed
        # UVs. Repair only such triangles in the neck; keep valid source UVs.
        armor.data.calc_loop_triangles()
        uv = armor.data.uv_layers.active.data
        repaired_uvs = 0
        for _ in range(2):
            for triangle in armor.data.loop_triangles:
                ps = [matrix@armor.data.vertices[i].co*scale for i in triangle.vertices]
                center = sum(ps,Vector())/3
                if not (1.48<=center.z<=1.62 and math.hypot(center.x,center.y)<.17):
                    continue
                a,b,c = [uv[i].uv.copy() for i in triangle.loops]
                ab,ac = b-a,c-a
                if abs(ab.x*ac.y-ab.y*ac.x)*.5 >= 1e-9:
                    continue
                normal = (ps[1]-ps[0]).cross(ps[2]-ps[0])
                if normal.length_squared < 1e-16:
                    continue
                axis = max(range(3),key=lambda i:abs(normal[i]))
                axes = [i for i in range(3) if i!=axis]
                # A stable nonzero local island even for sub-millimetre faces.
                size = max((ps[1]-ps[0]).length,(ps[2]-ps[0]).length)
                for index,p in zip(triangle.loops,ps):
                    uv[index].uv = (.4+(p[axes[0]]-ps[0][axes[0]])*.005/size,
                                    .85+(p[axes[1]]-ps[0][axes[1]])*.005/size)
                repaired_uvs += 1
        armor.name = armor.data.name = config.get('outfit_name', 'leather_armor_neck')
        export(destination, (armor,rig), rig)
        return {'removed_faces': removed_count, 'collar_quads': collar_quads,
                'repaired_uv_triangles': repaired_uvs, 'max_profile_radius_m': max_sampled_radius}
    finally:
        bpy.context.window.scene = previous
        for obj in list(scene.objects):
            bpy.data.objects.remove(obj, do_unlink=True)
        bpy.data.scenes.remove(scene)


def run(config_path):
    if bpy.context.window is None:
        raise RuntimeError('Neck finishing requires an interactive Blender MCP session.')
    with open(config_path, encoding='utf-8-sig') as stream:
        config = json.load(stream)
    root = Path(__file__).resolve().parents[2]
    paths = {key: (root/config[key]).resolve() for key in ('canonical','outfit_source','head_output','outfit_output')}
    if any(not path.is_relative_to(root) for path in paths.values()):
        raise ValueError('Neck input and output paths must stay inside the project.')
    if len(set(paths.values())) != 4:
        raise ValueError('Sources and derived outputs must be different files.')
    existing = [path for path in paths.values() if path.exists()]
    if any(os.path.samefile(a,b) for i,a in enumerate(existing) for b in existing[i+1:]):
        raise ValueError('Sources and outputs must not alias the same file.')
    for key in ('canonical','outfit_source'):
        if not paths[key].is_file():
            raise ValueError('Missing immutable source: '+str(paths[key]))
    # Reject bad generation before replacing either imported production asset.
    # The adjacent .meta files (and therefore all prefab references) stay put.
    temporary_root = root/'Temp'
    temporary_root.mkdir(exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='neck-build-', dir=temporary_root) as staging:
        head_path = Path(staging)/'head.fbx'
        armor_path = Path(staging)/'armor.fbx'
        report = head_neck(config, paths['canonical'], head_path)
        report.update(outfit(config, paths['outfit_source'], paths['canonical'], head_path, armor_path))
        replacements = [(head_path,paths['head_output']), (armor_path,paths['outfit_output'])]
        backups = {}
        for i,(_,destination) in enumerate(replacements):
            backup = Path(staging)/('original-'+str(i)+'.fbx') if destination.exists() else None
            if backup is not None:
                shutil.copyfile(destination,backup)
            backups[destination] = backup
        try:
            for generated,destination in replacements:
                shutil.copyfile(generated,destination)
        except Exception as failure:
            rollback_failures = []
            for destination,backup in backups.items():
                try:
                    if backup is None:
                        destination.unlink(missing_ok=True)
                    else:
                        shutil.copyfile(backup,destination)
                except Exception as rollback_failure:
                    rollback_failures.append((destination, rollback_failure))
            if rollback_failures:
                details = '; '.join(str(path)+': '+str(error) for path,error in rollback_failures)
                raise RuntimeError('Neck export failed; rollback also failed: '+details) from failure
            raise
    return report
