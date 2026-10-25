"""Owner-requested low, alternating flipper walk on the selected walrus.

Own procedural prototype animation, not borrowed dog motion or walrus mocap.
Keeps the selected sculpt/rest rig and existing aquatic clips.
"""
import hashlib,json,math,sys
from pathlib import Path
import bpy,numpy as np
from mathutils import Quaternion,Vector
repo=Path(__file__).resolve().parent.parent;sys.path.insert(0,str(repo/'scripts'))
from deer_behaviour_core import blend_pose,smooth
root=repo/'artifacts/fauna-overnight-20261024/walrus';source=root/'Animal - Raw Audition.blend';out=root/'Animal - Owner Corrected.blend'
choice=json.loads((repo/'docs/fauna/OWNER_SELECTIONS.json').read_text())['animals']['walrus']
assert not out.exists() or ('--replace-unpublished' in sys.argv and not choice.get('manual_fix_blend'))
digest=hashlib.sha256(source.read_bytes()).hexdigest();bpy.ops.wm.open_mainfile(filepath=str(source));scene=bpy.context.scene;entries=json.loads(scene['deer_four_rigs']);entry=entries[0]
body=bpy.data.objects[entry['body']];rig=bpy.data.objects[entry['rig']];names=entry['joint_names'];parents=entry['parents'];rest=[rig.data.bones[n].matrix_local.copy() for n in names];local=[rest[p].inverted()@m if p>=0 else m for p,m in zip(parents,rest)]
rig.animation_data_clear();rig.data.pose_position='POSE';v=np.array([p.co[:] for p in body.data.vertices]);patches=[]
for foot,side,front in [(2,1,True),(4,-1,True),(15,1,False),(18,-1,False)]:
    mask=(v[:,0]*side>.33)&(v[:,2]<(.37 if front else .46))&((v[:,1]>.72) if front else (v[:,1]<-.90))
    ids=np.flatnonzero(mask);assert len(ids)>10
    for group in body.vertex_groups:group.remove(ids.tolist())
    body.vertex_groups[names[foot]].add(ids.tolist(),1.,'REPLACE');patches.append({'bone':names[foot],'rigid_vertices':ids.tolist()})
def basis_from_global(g):
    values=[]
    for i,p in enumerate(parents):
        relative=g[p].inverted()@g[i] if p>=0 else g[i];basis=local[i].inverted()@relative;values.append((np.array(basis.translation),np.array(basis.to_quaternion())))
    return np.array([p[0] for p in values]),np.array([p[1] for p in values])
def pose(phase,walking):
    g=[]
    for i,p in enumerate(parents):
        m=g[p]@local[i] if p>=0 else rest[i].copy()
        if p<0:
            q=Quaternion((0,1,0),.018*math.sin(phase) if walking else 0)@rest[i].to_quaternion();m=q.to_matrix().to_4x4();m.translation=rest[i].translation+Vector((0,0,.025+(.018*(1-math.cos(phase*2)) if walking else 0)))
        elif i in (1,3,14,17):
            offset={1:0,3:math.pi,14:math.pi,17:0}[i];amplitude=.28 if i in (1,3) else .19
            q=Quaternion((1,0,0),amplitude*math.sin(phase+offset) if walking else 0)@rest[i].to_quaternion();position=m.translation.copy();m=q.to_matrix().to_4x4();m.translation=position
        elif i in (2,4,15,18):
            position=m.translation.copy();m=rest[i].copy();m.translation=position
        g.append(m)
    value=basis_from_global(g)
    for name,loc,q in zip(names,*value):rig.pose.bones[name].location=loc;rig.pose.bones[name].rotation_mode='QUATERNION';rig.pose.bones[name].rotation_quaternion=q
    bpy.context.view_layer.update();ev=body.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh();floor=min(p.co.z for p in mesh.vertices);ev.to_mesh_clear();lift=max(0,.006-floor)
    assert lift<.20,'Walk needs anatomical reconsideration rather than large floor lift'
    if lift:
        for m in g:m.translation.z+=lift
        value=basis_from_global(g)
    return value,lift
walk=[];lifts=[]
for frame in range(61):
    value,lift=pose(math.tau*frame/60,True);walk.append(value);lifts.append(lift)
idle,idle_lift=pose(0,False)
def bake(label,poses):
    act=bpy.data.actions.new(rig.name+' | '+label);act.use_fake_user=True;slot=act.slots.new(id_type='OBJECT',name=rig.name);bag=act.layers.new('Own procedural flipper walk').strips.new(type='KEYFRAME').channelbag(slot,ensure=True)
    locations=np.array([p[0] for p in poses]);rotations=np.array([p[1] for p in poses]);times=np.arange(len(poses),dtype=np.float32)
    for k in range(1,len(poses)):rotations[k,np.sum(rotations[k]*rotations[k-1],axis=1)<0]*=-1
    for i,name in enumerate(names):
        for path,values in [('location',locations[:,i]),('rotation_quaternion',rotations[:,i])]:
            for axis in range(values.shape[1]):
                curve=bag.fcurves.new(data_path=f'pose.bones["{name}"].{path}',index=axis);curve.keyframe_points.add(len(times));curve.keyframe_points.foreach_set('co',np.c_[times,values[:,axis]].astype(np.float32).ravel())
                for key in curve.keyframe_points:key.interpolation='LINEAR'
    return act.name
original_idle=entry['actions']['Idle'];entry['actions']['Aquatic idle']=original_idle;entry['actions']['Idle']=bake('Ground Idle',[idle,idle]);entry['actions']['Walk']=bake('Own ground Walk',walk)
def sample(label,t):
    array=walk if label=='Walk' else [idle,idle];x=t*30%(len(array)-1);i=int(x);return blend_pose(array[i],array[min(i+1,len(array)-1)],x-i)
sequence=[('Idle',2.),('Walk',6.),('Idle',2.)];demo=[];previous=None
for label,duration in sequence:
    for frame in range(round(duration*30)):
        t=frame/30;current=sample(label,t)
        if previous and t<.5:current=blend_pose(sample(previous,t),current,smooth(t/.5))
        demo.append(current)
    previous=label
entry['demos']={key:bake('Ground '+key,demo) for key in ('Showcase','Locomotion')};entry['locomotion_sequence']=[p[0] for p in sequence];entry['locomotion_durations_s']=[p[1] for p in sequence]
evidence={'slug':'walrus','candidate':1,'source_sha256':digest,'geometry_unchanged':True,'rest_skeleton_unchanged':True,'owner_requests_no_repeat_review':True,'natural_motion_approved':False,'method':'Own procedural alternating fore/hind flipper walk, small low-body weight shift, rigid palms and sampled support clearance. No dog gait or claimed walrus mocap. Existing aquatic clips retained.','limbs':[{'rigid_vertices':p['rigid_vertices'],'bone':p['bone']} for p in patches],'walk_frames':61,'maximum_sampled_floor_lift_m':max(lifts),'ground_idle_floor_lift_m':idle_lift,'source_aquatic_actions_preserved':True,'native_checks_pending':True}
entry['manual_repairs']=True;entry['owner_manual_patch']=evidence;scene['deer_four_rigs']=json.dumps([entry]);rig.animation_data_clear();rig.data.pose_position='REST';scene.frame_set(0);bpy.context.preferences.filepaths.save_version=0
assert np.array_equal(v,np.array([p.co[:] for p in body.data.vertices]));bpy.ops.wm.save_as_mainfile(filepath=str(out));evidence.update(output=str(out.relative_to(repo)),output_sha256=hashlib.sha256(out.read_bytes()).hexdigest());(root/'owner-fix.json').write_text(json.dumps(evidence,indent=2)+'\n');print('WALRUS OWN WALK SAVED',max(lifts),'sampled floor lift',flush=True)
