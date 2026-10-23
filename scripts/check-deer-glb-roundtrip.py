"""Reimport the actual GLB and compare skinned surface positions to the blend."""
import json
from pathlib import Path
import sys
import bpy
import numpy as np

root=Path(sys.argv[sys.argv.index('--')+1]).resolve()
blend=sys.argv[sys.argv.index('--')+2]
samples=[('Idle',0),('Walk',8),('Run',7),('Graze',36),('Rest',12),('Turn Left',111),('Death',20)]


def positions(obj):
    bpy.context.view_layer.update()
    evaluated=obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    mesh=evaluated.to_mesh()
    data=np.array([evaluated.matrix_world@v.co for v in mesh.vertices],dtype=np.float32)
    evaluated.to_mesh_clear()
    return data


def select(rig,name,frame):
    action=next(a for a in bpy.data.actions if a.name==name or a.name.endswith('|'+name))
    rig.animation_data_create().action=action
    if action.slots:
        rig.animation_data.action_slot=action.slots[0]
    bpy.context.scene.frame_set(frame)


bpy.ops.wm.open_mainfile(filepath=str(root/blend))
rig=bpy.data.objects['AI Deer Rig']
mesh=bpy.data.objects['Locally generated deer']
reference={}
for name,f in samples:
    select(rig,name,f)
    reference[name]=positions(mesh)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(root/'game-assets/Deer-LOD0.glb'))
bpy.context.scene.render.fps=24
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
mesh=next(o for o in bpy.context.scene.objects if o.type=='MESH' and
          any(m.type=='ARMATURE' for m in o.modifiers))
result={}
for name,f in samples:
    select(rig,name,f)
    actual=positions(mesh)
    expected=reference[name]
    # UV seam splits can reorder/duplicate vertices, so compare nearest points.
    chosen=expected[np.linspace(0,len(expected)-1,160,dtype=int)]
    distances=[]
    for points in np.array_split(chosen,10):
        d=points[:,None,:]-actual[None,:,:]
        distances.extend(np.sqrt(np.einsum('ijk,ijk->ij',d,d).min(axis=1)))
    maximum=float(max(distances))
    result[name]={'maximum_sampled_vertex_distance_m':maximum,
                  'reference_vertices':len(expected),'imported_vertices':len(actual)}
    print('ROUNDTRIP',name,maximum,flush=True)
    assert maximum<.002,(name,maximum)
(root/'glb-roundtrip-validation.json').write_text(json.dumps(result,indent=2))
print(json.dumps(result,indent=2),flush=True)
