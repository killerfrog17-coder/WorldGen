"""Bounded offline diagnostics; does not certify motion/art quality."""
import json
from pathlib import Path
import sys
import bpy
import numpy as np


def geometry(mesh):
    return np.array([v.co[:] for v in mesh.data.vertices]),np.array([l.uv[:] for l in mesh.data.uv_layers.active.data])


def skin(mesh):
    names={g.index:g.name for g in mesh.vertex_groups}
    return [[(names[g.group],g.weight) for g in v.groups] for v in mesh.data.vertices]


source,control,output=[Path(p).resolve() for p in sys.argv[sys.argv.index('--')+1:]]
bpy.ops.wm.open_mainfile(filepath=str(source))
mesh=bpy.data.objects['Locally generated deer']
original=geometry(mesh)
original_skin=skin(mesh)
original_images={i.name:np.array(i.pixels[:]) for i in bpy.data.images if i.has_data and i.size[0]>512}
bpy.ops.wm.open_mainfile(filepath=str(control))
mesh=bpy.data.objects['Locally generated deer']
assert all(np.array_equal(a,b) for a,b in zip(original,geometry(mesh)))
assert original_skin==skin(mesh)
assert len([o for o in bpy.context.scene.objects if o.type=='ARMATURE'])==1
assert not bpy.data.objects.get('Deer'), 'Donor geometry accidentally retained'
for name,pixels in original_images.items():
    assert np.array_equal(pixels,np.array(bpy.data.images[name].pixels[:]))
scene=bpy.context.scene
rig=bpy.data.objects['AI Deer Rig']
checks=[]
for name in scene['review_clips'].split('|'):
    action=bpy.data.actions[name]
    rig.animation_data.action=action
    if action.slots:
        rig.animation_data.action_slot=action.slots[0]
    first=None
    previous=None
    largest_step=0
    low=100
    for f in range(int(action.frame_range[0]),int(action.frame_range[1])+1):
        scene.frame_set(f)
        coords=np.array([v.co[:] for v in mesh.evaluated_get(bpy.context.evaluated_depsgraph_get()).data.vertices])
        assert np.isfinite(coords).all()
        assert np.max(np.abs(coords))<5, 'Exploded mesh'
        low=min(low,float(coords[:,2].min()))
        if first is None:
            first=coords
        if previous is not None:
            largest_step=max(largest_step,float(np.linalg.norm(coords-previous,axis=1).max()))
        previous=coords
    checks.append(dict(clip=name,frames=int(action.frame_range[1]-action.frame_range[0])+1,
        lowest=low,largest_adjacent_step=largest_step,
        loop_max_vertex_delta=float(np.linalg.norm(coords-first,axis=1).max())))
report=dict(unchanged_mesh_uv_skin_texture=True,no_donor_geometry=True,checks=checks,
    limitations='Not gait, self-intersection, contact lock, animation blending or runtime acceptance')
output.write_text(json.dumps(report,indent=2))
print(json.dumps(report))
