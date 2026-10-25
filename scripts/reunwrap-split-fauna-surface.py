"""Repair tiny isolated UV islands using a temporary geometrically joined copy.

Only UVs and sampling normals change on the selected original. No vertex,
polygon, skin group, rest joint or action is welded or otherwise edited there.
"""
import argparse,hashlib,json,sys
from pathlib import Path
import bpy,numpy as np
from mathutils.kdtree import KDTree
repo=Path(__file__).resolve().parent.parent
ap=argparse.ArgumentParser();ap.add_argument('--slug',required=True)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=repo/'artifacts/fauna-painted-20261024'/a.slug
state=json.loads((root/'paint-state.json').read_text());assert not state['appearance_approved']
bpy.ops.wm.open_mainfile(filepath=str(root/'Animal - Painted Species Review.blend'))
entry=json.loads(bpy.context.scene['deer_four_rigs'])[0];body=bpy.data.objects[entry['body']]
original=body.data;points=np.array([v.co[:] for v in original.vertices],np.float32)
polygons=[p.vertices[:] for p in original.polygons]
def key(mesh,poly):return tuple(sorted(tuple(round(x,6) for x in mesh.vertices[v].co) for v in poly.vertices))
temp=original.copy();body.data=temp
bpy.ops.object.select_all(action='DESELECT');body.hide_set(False);body.select_set(True);bpy.context.view_layer.objects.active=body
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.remove_doubles(threshold=.000001)
bpy.ops.uv.smart_project(angle_limit=1.151917,island_margin=.012,area_weight=.1,correct_aspect=True,scale_to_bounds=True)
bpy.ops.object.mode_set(mode='OBJECT')
tree=KDTree(len(temp.vertices))
for v in temp.vertices:tree.insert(v.co,v.index)
tree.balance();indices=[]
for p in points:
    _,index,distance=tree.find(p);assert distance<.000003
    indices.append(index)
table={tuple(sorted(poly.vertices)):{temp.loops[i].vertex_index:temp.uv_layers.active.data[i].uv[:] for i in poly.loop_indices} for poly in temp.polygons}
layer=original.uv_layers.active
for poly in original.polygons:
    face_key=tuple(sorted(indices[v] for v in poly.vertices))
    if face_key not in table:
        coords=points[list(poly.vertices)]
        assert len(coords)==3 and np.linalg.norm(np.cross(coords[1]-coords[0],coords[2]-coords[0]))<1e-7,'Nondegenerate sampling polygon missing'
        continue  # Preserve collapsed source triangles; they have no visible area.
    lookup=table[face_key]
    for i in poly.loop_indices:layer.data[i].uv=lookup[indices[original.loops[i].vertex_index]]
body.data=original;bpy.data.meshes.remove(temp)
assert np.array_equal(points,np.array([v.co[:] for v in original.vertices],np.float32))
assert polygons==[p.vertices[:] for p in original.polygons]
original.calc_loop_triangles();data=dict(np.load(root/'paint-anatomy.npz'))
data['uv']=np.array([[layer.data[i].uv[:] for i in t.loops] for t in original.loop_triangles],np.float32)
normal=data['normals'].copy();groups={}
for i,p in enumerate(np.round(points,6)):groups.setdefault(tuple(p),[]).append(i)
for ids in groups.values():
    n=normal[ids].sum(0);normal[ids]=n/max(float(np.linalg.norm(n)),1e-8)
data['normals']=normal;assert np.isfinite(data['uv']).all()
np.savez_compressed(root/'paint-anatomy.npz',**data)
state['uv_second_pass']='Temporary coincident-position joined sampling mesh; original geometry and skin retained exactly, transferred corner UVs and averaged sampling normals.'
(root/'paint-state.json').write_text(json.dumps(state,indent=2)+'\n')
bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(root/'Animal - Painted Species Review.blend'))
print('UV surface joined for sampling only',a.slug,len(points),'unchanged original vertices',len(table),'sampling polygons')
