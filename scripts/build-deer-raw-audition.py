"""Fresh generated mesh, automatic donor weights and unchanged authored rig/motion.

Raw audition only: no sculpt/paint, joint fitting, motion repair or contact bake.
"""
import argparse
import json
from pathlib import Path
import sys
import time
import bpy
import numpy as np
from mathutils import Matrix,Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform
sys.path.insert(0,str(Path(__file__).parent))
from deer_behaviour_core import blend_pose,smooth

ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True)
ap.add_argument('--source',type=Path,required=True);ap.add_argument('--fur',type=Path,required=True)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=a.root.resolve();started=time.perf_counter()
out=root/'Deer - Raw Generated Motion Audition.blend'
if out.exists():raise FileExistsError(out)
bpy.ops.wm.open_mainfile(filepath=str(a.source.resolve()));s=bpy.context.scene
info=json.loads(s['own_rig_review']);rig=bpy.data.objects[info['rig']];donor=bpy.data.objects[info['mesh']]
rig.animation_data_clear();rig.data.pose_position='REST'
forward=Vector((-.91514678,-.40312079,0)).normalized();right=Vector((.40312079,-.91514678,0)).normalized()
C=Matrix((-right,forward,Vector((0,0,1))));inverse=C.inverted()
donor_points=[C@(donor.matrix_world@v.co) for v in donor.data.vertices]
donor_faces=[tuple(f.vertices) for f in donor.data.polygons]
tree=BVHTree.FromPolygons(donor_points,donor_faces)
bone_names=[b.name for b in rig.data.bones];lookup={n:i for i,n in enumerate(bone_names)}
donor_weights=np.zeros((len(donor_points),len(bone_names)))
for v in donor.data.vertices:
    for g in v.groups:
        name=donor.vertex_groups[g.group].name
        if name in lookup:donor_weights[v.index,lookup[name]]=g.weight
before=set(bpy.data.objects);bpy.ops.import_scene.gltf(filepath=str(root/'deer-raw.glb'))
objects=[o for o in set(bpy.data.objects)-before if o.type=='MESH']
bpy.ops.object.select_all(action='DESELECT')
for o in objects:o.select_set(True)
bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();body=bpy.context.object
body.parent=None;bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
p=np.array([v.co[:] for v in body.data.vertices]);p[:,2]-=p[:,2].min();p*=1.65/p[:,2].max()
torso=p[(p[:,2]>.7)&(p[:,2]<1.2)];head=p[p[:,2]>1.35]
_,axes=np.linalg.eigh(np.cov(torso[:,:2].T));f=axes[:,-1]
if (head[:,:2].mean(0)-torso[:,:2].mean(0))@f<0:f=-f
r=np.array([f[1],-f[0]]);p[:,:2]=p[:,:2]@np.stack([r,f],axis=1)
target=np.array(donor_points);dt=target[(target[:,2]>.7)&(target[:,2]<1.2)]
torso=p[(p[:,2]>.7)&(p[:,2]<1.2)]
for axis in (0,1):
    p[:,axis]+=(np.quantile(dt[:,axis],.02)+np.quantile(dt[:,axis],.98)-np.quantile(torso[:,axis],.02)-np.quantile(torso[:,axis],.98))*.5
for v,co in zip(body.data.vertices,p):v.co=inverse@Vector(co)
dec=body.modifiers.new('Automatic 6000 triangle audition budget','DECIMATE');dec.ratio=min(1,6000/len(body.data.polygons));dec.use_collapse_triangulate=True
bpy.ops.object.modifier_apply(modifier=dec.name)
tri=body.modifiers.new('Triangles','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=tri.name)
for face in body.data.polygons:face.use_smooth=True
weights=np.zeros((len(body.data.vertices),len(bone_names)));distances=[]
for v in body.data.vertices:
    point=C@v.co;nearest,normal,face_index,distance=tree.find_nearest(point);face=donor_faces[face_index]
    if len(face)!=3:raise ValueError('Weight donor must be triangulated')
    bary=barycentric_transform(nearest,*[donor_points[j] for j in face],Vector((1,0,0)),Vector((0,1,0)),Vector((0,0,1)))
    row=np.clip(np.array(bary),0,1);row/=max(row.sum(),1e-9)
    w=row@donor_weights[list(face)];indices=np.argsort(w)[-4:];w2=w[indices];w2/=max(w2.sum(),1e-9)
    weights[v.index,indices]=w2;distances.append(distance)
body.vertex_groups.clear()
for column,name in enumerate(bone_names):
    group=body.vertex_groups.new(name=name)
    for index in np.flatnonzero(weights[:,column]>1e-7):group.add([int(index)],float(weights[index,column]),'REPLACE')
modifier=body.modifiers.new('Automatic transferred skin','ARMATURE');modifier.object=rig;modifier.use_deform_preserve_volume=False
body.parent=rig;body.matrix_parent_inverse=rig.matrix_world.inverted()
mat=bpy.data.materials.new('Existing fur - automatic box mapping');mat.use_nodes=True
nodes=mat.node_tree.nodes;links=mat.node_tree.links;bsdf=nodes.get('Principled BSDF');bsdf.inputs['Roughness'].default_value=.85
tex=nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(a.fur.resolve()));tex.image.pack();tex.projection='BOX';tex.projection_blend=.25
coord=nodes.new('ShaderNodeTexCoord');scale=nodes.new('ShaderNodeVectorMath');scale.operation='SCALE';scale.inputs[3].default_value=8
links.new(coord.outputs['Generated'],scale.inputs[0]);links.new(scale.outputs[0],tex.inputs['Vector']);links.new(tex.outputs['Color'],bsdf.inputs['Base Color'])
body.data.materials.clear();body.data.materials.append(mat)
donor.name='Old deer weight donor - temporary';body.name='Locally generated deer';info['mesh']=body.name
for o in list(s.objects):
    if o.type=='MESH' and o!=body:bpy.data.objects.remove(o,do_unlink=True)
body.hide_set(False);body.hide_render=False;rig.hide_set(True);rig.data.pose_position='POSE'
s['own_rig_review']=json.dumps(info)
clips={};fps=info['transferred_fps']
rig.data.pose_position='POSE'
for label in ('Idle','Walk','Trot','Run','Eating','Sleep','lay_to_idle'):
    action=bpy.data.actions[info['transferred'][label]];rig.animation_data_clear();rig.animation_data_create().action=action;rig.animation_data.action_slot=action.slots[0]
    array=[]
    for frame in range(int(action.frame_range[0]),int(action.frame_range[1])+1):
        s.frame_set(frame)
        array.append((np.array([rig.pose.bones[n].location[:] for n in bone_names]),np.array([rig.pose.bones[n].matrix_basis.to_quaternion()[:] for n in bone_names])))
    if label!='lay_to_idle' and all(np.max(np.abs(x-y))<1e-5 for x,y in zip(array[0],array[-1])):array=array[:-1]
    clips[label]=array
def sample(label,seconds):
    array=clips[label];time=seconds*fps
    if label=='lay_to_idle':time=min(time,len(array)-1)
    else:time%=len(array)
    first=int(time);return blend_pose(array[first],array[min(first+1,len(array)-1)] if label=='lay_to_idle' else array[(first+1)%len(array)],time-first)
def demo(name,parts):
    poses=[];previous=None;duration_before=0;offset_before=0
    for label,duration in parts:
        offset=0
        if previous in ('Walk','Trot','Run') and label in ('Walk','Trot','Run'):
            offset=((duration_before+offset_before)*fps/len(clips[previous])%1)*len(clips[label])/fps
        for frame in range(round(duration*fps)):
            t=frame/fps;pose=sample(label,t+offset)
            if previous and t<.5:pose=blend_pose(sample(previous,duration_before+offset_before+t),pose,smooth(t/.5))
            poses.append(pose)
        previous=label;duration_before=duration;offset_before=offset
    for frame in range(round(.5*fps)):poses.append(blend_pose(poses[-1],sample('Idle',0),smooth((frame+1)/(.5*fps))))
    action=bpy.data.actions.new(name);action.use_fake_user=True;slot=action.slots.new(id_type='OBJECT',name=rig.name)
    bag=action.layers.new('Raw preview blend').strips.new(type='KEYFRAME').channelbag(slot,ensure=True)
    loc=np.array([p[0] for p in poses]);rot=np.array([p[1] for p in poses])
    for frame in range(1,len(rot)):rot[frame,np.sum(rot[frame]*rot[frame-1],axis=1)<0]*=-1
    times=np.arange(len(poses),dtype=np.float32)
    for index,bone in enumerate(bone_names):
        for path,values in [('location',loc[:,index]),('rotation_quaternion',rot[:,index])]:
            for axis in range(values.shape[1]):
                curve=bag.fcurves.new(data_path=f'pose.bones["{bone}"].{path}',index=axis);curve.keyframe_points.add(len(poses))
                curve.keyframe_points.foreach_set('co',np.c_[times,values[:,axis]].astype(np.float32).ravel())
                for key in curve.keyframe_points:key.interpolation='LINEAR'
                curve.update()
    return action.name
demos={}
demos['Showcase']=demo('Raw behaviour showcase',[('Idle',2),('Walk',3),('Trot',2),('Run',3),('Walk',2),('Idle',2),('Eating',5),('Idle',2),('Sleep',4),('lay_to_idle',len(clips['lay_to_idle'])/fps),('Idle',2)])
demos['Locomotion']=demo('Raw locomotion loop',[('Idle',2),('Walk',3),('Trot',2),('Run',3),('Trot',2),('Walk',3),('Idle',2)])
s['raw_review_demos']=json.dumps(demos)
action=bpy.data.actions[info['transferred']['Walk']];rig.animation_data_create().action=action;rig.animation_data.action_slot=action.slots[0]
s.render.fps=info['transferred_fps'];s.frame_start=int(action.frame_range[0]);s.frame_end=int(action.frame_range[1]);s.frame_set(s.frame_start)
report={'stage':'raw automatic motion audition','source':str(a.source.resolve()),'triangles':len(body.data.polygons),'bones':len(rig.data.bones),
    'rig_and_motion':'Existing accepted rig/rest and authored actions unchanged; automatic closest-surface top4 weight transfer',
    'material':'Existing fur source with automatic box mapping; no painting','seconds':round(time.perf_counter()-started,2),
    'nearest_surface_max_m':max(distances),'manual_pose_or_geometry_corrections':False,'art_checked':False,'game_changed':False}
s['raw_audition']=json.dumps(report);s.render.engine='CYCLES';s.cycles.device='CPU'
bpy.data.orphans_purge(do_recursive=True)
bpy.ops.wm.save_as_mainfile(filepath=str(out));(root/'raw-audition.json').write_text(json.dumps(report,indent=2));print(report,flush=True)
