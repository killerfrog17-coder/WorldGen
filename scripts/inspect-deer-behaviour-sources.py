"""Inspect CC0 motion skeletons and our own head landmarks, offline only."""
import json
from pathlib import Path
import sys
import bpy
import numpy as np


root=Path(sys.argv[-1]).resolve()
for source_name in ('horse','fox'):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.fps=24
    bpy.ops.import_scene.gltf(filepath=str(root/'sources'/f'{source_name}-animations.glb'))
    rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
    rig.animation_data_clear()
    bpy.context.view_layer.update()
    records=[]
    for b in rig.data.bones:
        records.append(dict(name=b.name,parent=b.parent.name if b.parent else None,
            head=list(rig.matrix_world@b.head_local),tail=list(rig.matrix_world@b.tail_local)))
    actions=[dict(name=a.name,frames=list(a.frame_range)) for a in bpy.data.actions]
    report=dict(bones=records,actions=actions,world_matrix=[list(row) for row in rig.matrix_world])
    (root/f'{source_name}-inspection.json').write_text(json.dumps(report,indent=2))
    print(source_name,json.dumps(report),flush=True)
source=root.parent/'deer-realvis-20261023/Deer - Game Asset Review v2.blend'
bpy.ops.wm.open_mainfile(filepath=str(source))
mesh=bpy.data.objects['Locally generated deer']
rig=bpy.data.objects['AI Deer Rig']
rig.data.pose_position='REST'
vertices=np.array([v.co[:] for v in mesh.data.vertices])
groups={g.name:g.index for g in mesh.vertex_groups}
weights=np.zeros((len(vertices),len(groups)))
for v in mesh.data.vertices:
    for g in v.groups:
        weights[v.index,g.group]=g.weight
head=weights[:,[groups[n] for n in ('Head','Jaw','Muzzle_R','Muzzle_L')]].sum(1)
image=next(n.image for n in mesh.data.materials[0].node_tree.nodes if n.type=='TEX_IMAGE' and n.image.colorspace_settings.name=='sRGB')
pixels=np.array(image.pixels[:]).reshape(image.size[1],image.size[0],4)
uvs=np.zeros((len(vertices),2))
for loop in mesh.data.loops:
    uvs[loop.vertex_index]=mesh.data.uv_layers.active.data[loop.index].uv
xy=np.clip((uvs*np.array(image.size)).astype(int),0,np.array(image.size)-1)
colour=pixels[xy[:,1],xy[:,0],:3]
dark=(colour[:,:3].max(1)<.23)&(head>.65)
centres={}
for n in ('Head','Jaw','Muzzle_R','Muzzle_L','Neck3'):
    w=weights[:,groups[n]]
    centres[n]=((vertices*w[:,None]).sum(0)/max(w.sum(),1e-8)).tolist()
report=dict(centres=centres,dark_head_points=vertices[dark].tolist(),
    bones=[dict(name=b.name,head=list(b.head_local),tail=list(b.tail_local)) for b in rig.data.bones])
(root/'target-inspection.json').write_text(json.dumps(report,indent=2))
print('TARGET',json.dumps(dict(centres=centres,dark_head_count=int(dark.sum()),dark_head_centroid=vertices[dark].mean(0).tolist())),flush=True)
