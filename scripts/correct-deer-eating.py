"""Offline eating-pose correction on the original deer rig, with planted feet.

Changes the eating action only; bind mesh/rig/weights and other actions persist.
The flat review plane is an explicit target, not a game terrain controller.
"""
import argparse
import hashlib
import importlib.util
import json
import math
from pathlib import Path
import sys
import bpy
import numpy as np
from mathutils import Matrix,Quaternion,Vector

sys.path.insert(0,str(Path(__file__).parent))
from deer_behaviour_core import Skeleton,F,R,Z,matrix
spec=importlib.util.spec_from_file_location('retarget',Path(__file__).with_name('retarget-mesh2motion-own-deer.py'))
tools=importlib.util.module_from_spec(spec);spec.loader.exec_module(tools)


def main():
    ap=argparse.ArgumentParser();ap.add_argument('--source',type=Path,required=True)
    ap.add_argument('--root',type=Path,required=True);ap.add_argument('--pilot',action='store_true')
    a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=a.root.resolve();root.mkdir(parents=True,exist_ok=True)
    out=root/'Deer - Eating Correction Review.blend'
    if out.exists():raise FileExistsError(out)
    source_hash=hashlib.sha256(a.source.read_bytes()).hexdigest()
    bpy.ops.wm.open_mainfile(filepath=str(a.source.resolve()))
    scene=bpy.context.scene;info=json.loads(scene['own_rig_review'])
    rig=bpy.data.objects[info['rig']];mesh=bpy.data.objects[info['mesh']]
    signature=tools.digest(mesh,rig);skel=Skeleton(tools.library.metadata(rig));rig.data.pose_position='POSE'
    def pose(action,frame):
        rig.animation_data_clear();rig.animation_data_create().action=action
        rig.animation_data.action_slot=action.slots[0];scene.frame_set(frame)
        return [rig.pose.bones[n].matrix.copy() for n in skel.names]
    src=bpy.data.actions[info['transferred']['Eating']]
    frames=[pose(src,f) for f in range(round(src.frame_range[0]),round(src.frame_range[1])+1)]
    standing=pose(bpy.data.actions[info['transferred']['Idle']],0)
    vertices=np.array([v.co for v in mesh.data.vertices]);hom=np.c_[vertices,np.ones(len(vertices))]
    weights=np.zeros((len(vertices),len(skel.names)))
    group={g.index:g.name for g in mesh.vertex_groups}
    for v in mesh.data.vertices:
        for g in v.groups:
            if group[g.group] in skel.index:weights[v.index,skel.index[group[g.group]]]=g.weight
    def skin(g,ids=None):
        vv=hom if ids is None else hom[ids];ww=weights if ids is None else weights[ids]
        m=np.array([p@inv for p,inv in zip(g,skel.inverse)])
        return np.einsum('vb,bij,vj->vi',ww,m[:,:3,:],vv,optimize=True)
    head_ids=np.flatnonzero(weights[:,[skel.index[n] for n in ('Head','Jaw','Muzzle_L','Muzzle_R')]].sum(1)>.65)
    feet=[];standing_vertices=skin(standing)
    for name,chain in [('ForeHoof_L',['UpperFore_L','Elbow_L','Wrist_L','ForeHoof_L']),
        ('ForeHoof_R',['UpperFore_R','Elbow_R','Wrist_R','ForeHoof_R']),
        ('HindHoof_L',['Thigh_L','Knee_L','Hock_L','HindHoof_L']),
        ('HindHoof_R',['Thigh_R','Knee_R','Hock_R','HindHoof_R'])]:
        idx=skel.index[name];ids=np.flatnonzero(weights[:,idx]>.5)
        low=standing_vertices[ids,2].min();sole_ids=ids[standing_vertices[ids,2]<low+.018]
        target=Vector(standing_vertices[sole_ids].mean(0))
        target.z=.004+float(standing_vertices[sole_ids,2].mean()-low)
        if name.startswith('Fore'):
            target+=F*.065
            # Spread a little, rather than folding both forelegs into the chest.
            side=1 if (standing[idx].translation-standing[skel.index['Chest']].translation).dot(R)>0 else -1
            target+=R*(side*.015)
        sole_local=standing[idx].inverted()@Vector(standing_vertices[sole_ids].mean(0))
        feet.append(dict(bone=name,index=idx,chain=chain,sole_local=sole_local,target=target,ids=ids.tolist()))
    def correct(original):
        g=[m.copy() for m in standing]
        # A small forward feeding stance; the ribcage and supporting limbs stay
        # standing instead of copying the donor's deeply folded front quarters.
        skel.translate_all(g,(0,0,-.025))
        for name in ('Neck1','Neck2','Neck3','Head'):
            i=skel.index[name];p=skel.parents[i]
            g[i]=matrix(original[i].to_quaternion(),g[p]@skel.local[i].translation)
        for name in ('Ear_L','Ear_R','Jaw','Muzzle_L','Muzzle_R'):
            i=skel.index[name];g[i]=g[skel.parents[i]]@skel.local[i]
        for foot in feet:
            i=foot['index'];q=g[i].to_quaternion()
            goal=foot['target']-q@foot['sole_local']
            skel.solve_chain(g,foot['chain'],goal,iterations=32)
            skel.rotate_branch(g,i,q@g[i].to_quaternion().inverted())
        # Raise/lower the existing feeding neck smoothly as a chain. Solve the
        # actual skinned muzzle surface, not a misplaced inferred head joint.
        base=[m.copy() for m in g]
        def bent(angle):
            gg=[m.copy() for m in base]
            for name,share in (('Neck1',.45),('Neck2',.35),('Neck3',.20)):
                skel.rotate_branch(gg,skel.index[name],Quaternion(R,angle*share))
            return gg
        def error(angle):return float(skin(bent(angle),head_ids)[:,2].min())-.025
        candidates=[(abs(error(t)),t) for t in np.linspace(-1.1,1.1,45)]
        angle=min(candidates)[1];left=max(-1.1,angle-.075);right=min(1.1,angle+.075)
        # Locally monotone in this feeding range; no frame-to-frame root lift.
        for _ in range(22):
            middle=(left+right)/2
            if error(left)*error(middle)<=0:right=middle
            else:left=middle
        if error(left)*error(right)<=0:angle=(left+right)/2
        return bent(angle),angle
    solved=[];reports=[]
    for frame,original in enumerate(frames):
        g,angle=correct(original);vv=skin(g)
        soles=[(g[f['index']]@f['sole_local']-f['target']).length for f in feet]
        reports.append(dict(frame=frame,muzzle_min=float(vv[head_ids,2].min()),
            lowest=float(vv[:,2].min()),max_sole_error=max(soles),neck_correction_deg=math.degrees(angle)))
        solved.append(g)
    action=bpy.data.actions.new('Corrected | Eating');action.use_fake_user=True
    rig.animation_data_clear();rig.animation_data_create().action=action
    previous=None
    for f,g in enumerate(solved):
        loc,rot=skel.basis(g)
        if previous is not None:
            for i in range(len(rot)):
                if np.dot(rot[i],previous[i])<0:rot[i]*=-1
        previous=rot.copy()
        for i,name in enumerate(skel.names):
            pb=rig.pose.bones[name];pb.rotation_mode='QUATERNION';pb.location=loc[i]
            pb.rotation_quaternion=rot[i];pb.scale=(1,1,1)
            pb.keyframe_insert('location',frame=f);pb.keyframe_insert('rotation_quaternion',frame=f)
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for curve in bag.fcurves:
                    for key in curve.keyframe_points:key.interpolation='LINEAR'
    info['uncorrected_eating']=info['transferred']['Eating'];info['transferred']['Eating']=action.name
    scene['own_rig_review']=json.dumps(info);scene['eating_correction']=True
    scene.render.fps=30;scene.frame_start=0;scene.frame_end=len(solved)-1;scene.frame_set(0)
    # Visible floor makes both muzzle clearance and planted hooves inspectable.
    floor=bpy.data.objects.get('Studio floor (not part of animal)')
    if floor:
        floor.hide_set(False);floor.hide_render=False;floor.location.z=0
    scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=16
    scene.render.threads_mode='FIXED';scene.render.threads=3
    scene.render.resolution_x=850;scene.render.resolution_y=650;scene.render.resolution_percentage=100
    center=Vector((0,0,.65));scene.camera.location=center+R*3.5+F*.5+Vector((0,0,.45))
    scene.camera.rotation_euler=(center-scene.camera.location).to_track_quat('-Z','Y').to_euler()
    scene.camera.data.type='ORTHO';scene.camera.data.ortho_scale=2.85
    for f in (0,len(solved)//2):
        scene.frame_set(f);scene.render.filepath=str(root/f'corrected-eating-{f}.png');bpy.ops.render.render(write_still=True)
    report=dict(source_sha256=source_hash,frames=reports,geometry_rig_weights_unchanged=tools.digest(mesh,rig)==signature,
        source_unchanged=hashlib.sha256(a.source.read_bytes()).hexdigest()==source_hash,
        changed_action_only=True,live_ik=False,game_launched=False)
    (root/'correction-checks.json').write_text(json.dumps(report,indent=2))
    assert report['geometry_rig_weights_unchanged'] and report['source_unchanged']
    if not a.pilot:
        scene.frame_set(0);bpy.ops.wm.save_as_mainfile(filepath=str(out))
    print('Eating correction ready.',flush=True)


if __name__=='__main__':main()
