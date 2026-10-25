"""Scoped owner-requested prototype patches, preserving the raw review file.

Duiker: small skinned backing caps inside the two fenestrated pinnae.
Aardvark: right-paw chain confinement, rigid distal footprint and bounded roll.
Not a universal contact/collision or natural-motion acceptance gate.
"""
import argparse,hashlib,json,math,sys
from pathlib import Path
import bpy,numpy as np
from mathutils import Quaternion
repo=Path(__file__).resolve().parent.parent
ap=argparse.ArgumentParser();ap.add_argument('--slug',required=True,choices=['duiker','aardvark'])
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=repo/'artifacts/fauna-overnight-20261024'/a.slug
choice=json.loads((repo/'docs/fauna/OWNER_SELECTIONS.json').read_text())['animals'][a.slug];assert choice['selected_models']==[2]
source=root/'Animal - Raw Audition.blend';out=root/'Animal - Owner Corrected.blend';assert not out.exists(),'Keep an existing owner correction intact'
source_hash=hashlib.sha256(source.read_bytes()).hexdigest();bpy.ops.wm.open_mainfile(filepath=str(source));s=bpy.context.scene;entries=json.loads(s['deer_four_rigs']);e=entries[1]
body=bpy.data.objects[e['body']];rig=bpy.data.objects[e['rig']];rig.animation_data_clear();rig.data.pose_position='REST';s.frame_set(0)
p=np.array([v.co[:] for v in body.data.vertices]);faces=np.array([f.vertices[:] for f in body.data.polygons]);names=e['joint_names'];w=np.zeros((len(p),len(names)))
for v in body.data.vertices:
    for g in v.groups:w[v.index,names.index(body.vertex_groups[g.group].name)]=g.weight
evidence={'slug':a.slug,'candidate':2,'source_sha256':source_hash,'owner_requests_no_repeat_review':True,'natural_motion_approved':False}
def smooth(lo,hi,x):
    t=np.clip((x-lo)/(hi-lo),0,1);return t*t*(3-2*t)
if a.slug=='duiker':
    caps=[]
    for bone_id in [20,21]:
        region=(w[:,bone_id]>.30)&(p[:,2]>1.42);points=p[region];assert len(points)>20
        centre=np.median(points,axis=0);_,_,axes=np.linalg.svd(points-points.mean(0),full_matrices=False);long=axes[0];long*=1 if long[2]>0 else -1;width=axes[1];normal=np.cross(long,width);normal/=np.linalg.norm(normal)
        if normal@np.array([np.sign(centre[0])*.5,.8,0])<0:normal=-normal;width=-width
        verts=[];polys=[];segments=8;centre-=normal*.003
        for sign in [1,-1]:
            verts.append(centre+normal*sign*.004)
            for i in range(segments):
                angle=math.tau*i/segments;verts.append(centre+long*(.095*math.cos(angle))+width*(.065*math.sin(angle))+normal*sign*.006)
        for i in range(segments):
            j=(i+1)%segments;polys.extend([(0,1+i,1+j),(9,10+j,10+i),(1+i,10+i,10+j),(1+i,10+j,1+j)])
        mesh=bpy.data.meshes.new('Duiker ear backing');mesh.from_pydata(verts,[],polys);mesh.update();ob=bpy.data.objects.new('Duiker ear backing',mesh);s.collection.objects.link(ob);ob.matrix_world=body.matrix_world.copy();mesh.materials.append(body.data.materials[0])
        skin=w[region].mean(0);keep=np.argsort(skin)[-4:];skin=skin[keep]/skin[keep].sum()
        for index,value in zip(keep,skin):ob.vertex_groups.new(name=names[index]).add(list(range(len(verts))),float(value),'REPLACE')
        for poly in mesh.polygons:poly.use_smooth=True
        bpy.ops.object.select_all(action='DESELECT');body.hide_set(False);body.select_set(True);ob.select_set(True);bpy.context.view_layer.objects.active=body;bpy.ops.object.join()
        caps.append({'ear_bone':names[bone_id],'centre':centre.tolist(),'triangles':len(polys),'method':'Thin backing inside existing closed ear tunnel; no global remesh'})
    actual=np.array([v.co[:] for v in body.data.vertices]);assert np.array_equal(actual[:len(p)],p)
    evidence.update(ear_caps=caps,original_vertices_unchanged=True,added_triangles=64)
else:
    mask=(p[:,0]>.10)&(p[:,1]>.40)&(p[:,1]<1.20)&(p[:,2]<.43);ids=np.flatnonzero(mask);assert len(ids)>30
    chain=[12,13,14];new=w.copy();distal=np.flatnonzero(mask&(p[:,2]<.22));assert len(distal)>20
    for vertex in ids:
        row=np.zeros(w.shape[1]);row[chain]=w[vertex,chain]
        if row.sum()<1e-8:row[14]=1
        row/=row.sum();pin=float(1-smooth(.22,.43,p[vertex,2]));row*=1-pin;row[14]+=pin;new[vertex]=row
        for group in body.vertex_groups:group.remove([int(vertex)])
        for index in np.flatnonzero(row>1e-9):body.vertex_groups[names[index]].add([int(vertex)],float(row[index]),'REPLACE')
    evidence.update(changed_vertices=len(ids),rigid_distal_vertices=len(distal),tail_weight_removed=float(w[ids,30].sum()),outside_paw_weights_unchanged=True,geometry_unchanged=True)
    actions=[e['actions']['Run'],*e['demos'].values()];roll=[]
    for action_name in actions:
        action=bpy.data.actions[action_name];rig.data.pose_position='POSE';rig.animation_data_create().action=action;rig.animation_data.action_slot=action.slots[0]
        first,last=action.frame_range;times=np.unique(np.r_[np.arange(first,last+.01,.25),last]);samples={key:[] for key in ['Joint_13','Joint_14']};angles={key:[] for key in samples}
        for frame in times:
            s.frame_set(int(frame),subframe=float(frame-int(frame)))
            for key in samples:
                q=rig.pose.bones[key].rotation_quaternion.copy();swing,twist=q.to_swing_twist('Y');limit=.35 if key=='Joint_13' else .22;bounded=float(np.clip(twist,-limit,limit));corrected=swing@Quaternion((0,1,0),bounded)
                if samples[key] and corrected.dot(Quaternion(samples[key][-1]))<0:corrected.negate()
                samples[key].append(tuple(corrected));angles[key].append(abs(float(twist)))
        bags=[bag for layer in action.layers for strip in layer.strips for bag in strip.channelbags]
        for key,values in samples.items():
            path=f'pose.bones["{key}"].rotation_quaternion'
            for bag in bags:
                for curve in list(bag.fcurves):
                    if curve.data_path==path:bag.fcurves.remove(curve)
            for frame,q in zip(times,values):rig.pose.bones[key].rotation_quaternion=q;rig.pose.bones[key].keyframe_insert('rotation_quaternion',frame=float(frame),group=key)
            for bag in bags:
                for curve in bag.fcurves:
                    if curve.data_path==path:
                        for point in curve.keyframe_points:point.interpolation='LINEAR'
            roll.append({'action':action_name,'bone':key,'max_original_axial_roll_rad':max(angles[key]),'bounded_roll_rad':.35 if key=='Joint_13' else .22,'swing_retained':True})
    evidence['roll_corrections']=roll
    rig.animation_data_clear();rig.data.pose_position='REST';assert np.array_equal(np.array([v.co[:] for v in body.data.vertices]),p)
e['triangles']=sum(len(poly.vertices)-2 for poly in body.data.polygons);e['manual_repairs']=True;e['owner_manual_patch']=evidence
s['deer_four_rigs']=json.dumps(entries);s.frame_set(0);rig.animation_data_clear();rig.data.pose_position='REST';bpy.context.preferences.filepaths.save_version=0
assert hashlib.sha256(source.read_bytes()).hexdigest()==source_hash
bpy.ops.wm.save_as_mainfile(filepath=str(out));evidence.update(output=str(out.relative_to(repo)),output_sha256=hashlib.sha256(out.read_bytes()).hexdigest(),native_checks_pending=True)
(root/'owner-fix.json').write_text(json.dumps(evidence,indent=2)+'\n');print('OWNER PATCH SAVED',a.slug,e['triangles'],'native/visual checks pending',flush=True)
