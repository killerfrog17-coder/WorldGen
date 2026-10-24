"""Export anatomical guides and a non-destructive live material-review copy."""
import argparse
import hashlib
import json
from pathlib import Path
import sys
import bpy
import numpy as np
from mathutils import Vector

sys.path.insert(0,str(Path(__file__).parent))
from deer_behaviour_core import F,R,Z

ap=argparse.ArgumentParser();ap.add_argument('--source',type=Path,required=True)
ap.add_argument('--root',type=Path,required=True)
ap.add_argument('--replace-unopened',action='store_true')
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=a.root.resolve();root.mkdir(parents=True,exist_ok=True)
out=root/'Deer - Masked Coat Working Review.blend'
if out.exists() and not a.replace_unopened:raise FileExistsError(out)
bpy.ops.wm.open_mainfile(filepath=str(a.source.resolve()));s=bpy.context.scene
info=json.loads(s['own_rig_review']);mesh=bpy.data.objects[info['mesh']];rig=bpy.data.objects[info['rig']]
rig.animation_data_clear();rig.data.pose_position='REST'
for pb in rig.pose.bones:pb.matrix_basis.identity()
s.frame_set(0)
verts=np.array([mesh.matrix_world@v.co for v in mesh.data.vertices],np.float32)
norms=np.array([mesh.matrix_world.to_3x3()@v.normal for v in mesh.data.vertices],np.float32)
faces=np.array([p.vertices[:] for p in mesh.data.polygons],np.int32)
uv=np.array([u.uv[:] for u in mesh.data.uv_layers.active.data],np.float32).reshape(-1,3,2)
weights=np.zeros((len(verts),len(rig.data.bones)),np.float32);names=[b.name for b in rig.data.bones]
groups={g.index:g.name for g in mesh.vertex_groups}
for v in mesh.data.vertices:
    for g in v.groups:
        if groups[g.group] in names:weights[v.index,names.index(groups[g.group])]=g.weight
basis=np.array([R,F,Z],np.float32)
anchors={b.name:dict(head=list(basis@np.array(rig.matrix_world@b.head_local)),
    tail=list(basis@np.array(rig.matrix_world@b.tail_local))) for b in rig.data.bones}
np.savez_compressed(root/'anatomy.npz',vertices=verts@basis.T,normals=norms@basis.T,
    faces=faces,uv=uv,weights=weights,bone_names=names,basis=basis)
(root/'anchors.json').write_text(json.dumps(anchors,indent=2))
old=mesh.data.materials[0];old.name='Accepted previous coat (preserved)'
old.use_fake_user=True
trial=bpy.data.materials.new('Masked deer coat - work in progress');trial.use_nodes=True
nodes=trial.node_tree.nodes;links=trial.node_tree.links;bsdf=nodes.get('Principled BSDF')
bsdf.inputs['Base Color'].default_value=(.22,.13,.065,1);bsdf.inputs['Roughness'].default_value=.8
for role,name,space in [('colour','coat-colour.png','sRGB'),('roughness','coat-roughness.png','Non-Color'),
                         ('normal','coat-normal.png','Non-Color')]:
    n=nodes.new('ShaderNodeTexImage');n.name='Coat '+role;n.label=name
    n.location=(-600,200 if role=='colour' else -20 if role=='roughness' else -220)
    if role=='normal':
        norm=nodes.new('ShaderNodeNormalMap');norm.inputs['Strength'].default_value=1.0
        norm.name='Coat normal mapping'
        links.new(norm.outputs['Normal'],bsdf.inputs['Normal'])
mesh.data.materials[0]=trial
s['masked_coat_review']=json.dumps(dict(root=str(root),mesh=mesh.name,rig=rig.name,
    previous=old.name,trial=trial.name,source_sha256=hashlib.sha256(a.source.read_bytes()).hexdigest()))
for o in s.objects:
    if o.type=='MESH' and o!=mesh:
        o.hide_set(True);o.hide_render=True
floor=bpy.data.objects.get('Studio floor (not part of animal)')
if floor:floor.hide_set(False);floor.hide_render=False
s.render.engine='CYCLES';s.cycles.device='CPU';s.cycles.samples=16
s.render.threads_mode='FIXED';s.render.threads=3
s.render.resolution_x=900;s.render.resolution_y=760;s.render.resolution_percentage=100
s.camera.data.type='ORTHO'
head=rig.data.bones['Head'];center=rig.matrix_world@(head.head_local+head.tail_local)*.5
views={}
for name,direction,target,scale in [('head-right',R,center,.62),('head-left',-R,center,.62),
    ('head-front',F,center,.62),('body',R*3.5+F*.25+Z*.2,Vector((0,0,.83)),2.7)]:
    s.camera.location=target+direction.normalized()*3
    s.camera.rotation_euler=(target-s.camera.location).to_track_quat('-Z','Y').to_euler()
    s.camera.data.ortho_scale=scale
    s.render.filepath=str(root/(name+'-clay.png'));bpy.ops.render.render(write_still=True)
    views[name]=dict(camera_matrix=[list(row) for row in s.camera.matrix_world],scale=scale,
        size=[s.render.resolution_x,s.render.resolution_y])
(root/'guide-cameras.json').write_text(json.dumps(views,indent=2))
for screen in bpy.data.screens:
    for ar in screen.areas:
        if ar.type=='VIEW_3D':
            sp=ar.spaces.active;sp.shading.type='MATERIAL';sp.shading.use_scene_world=False
            sp.overlay.show_overlays=False;sp.show_region_ui=True
            sp.region_3d.view_location=(0,0,.83);sp.region_3d.view_distance=3.4
            sp.region_3d.view_rotation=(R*3.5+F*.4+Z*.25).to_track_quat('Z','Y')
(root/'progress.json').write_text(json.dumps({'stage':'Mapping eyes, nose and anatomical regions','ready':False}))
bpy.ops.wm.save_as_mainfile(filepath=str(out))
print('Masked-coat working review prepared.',flush=True)
