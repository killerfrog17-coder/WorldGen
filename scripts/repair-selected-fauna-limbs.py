"""Explicit selected-animal limb repairs, in separate native copies.

Remove non-limb skin leakage and replace the unstable axial frame with the
shortest swing of each actual rest bone. Preserve sampled world joint centres.
These are scoped prototype repairs, not an automatic motion acceptance gate.
"""
import argparse, hashlib, json, sys
from pathlib import Path
import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector

repo = Path(__file__).resolve().parent.parent
# Each limb lists its real neural hierarchy, the start of the rigid hand/foot,
# and the highest vertex eligible for the distal rigid patch (metres).
SPECS = {
    'hedgehog': dict(limbs=[([7,8,9,10],9,.12),([11,12,13,14],13,.12)], clips=['Walk','Run']),
    'howler-monkey': dict(limbs=[([6,7,8,9,10,11,12],9,.29),([13,14,15,16,17,18,19],16,.29)], clips=['Run']),
    'koala': dict(limbs=[([11,12,13],13,.18),([14,15,16],16,.18)], clips=['Walk','Run'], ear_head=6),
    'lemur': dict(limbs=[([12,13,14,15],15,.27)], clips=['Run']),
    'long-nosed-bandicoot': dict(limbs=[([15,16,17,18,19,20],18,.22),([21,22,23,24,25,26],24,.22)], clips=['Run']),
    'macaque': dict(limbs=[([17,18,19,20],19,.29),([21,22,23,24],23,.29)], clips=['Run']),
    'gorilla': dict(limbs=[([6,7,8,9,10,11,12],9,.34),([13,14,15,16,17,18,19],16,.34),([20,21,22,23],22,.23),([24,25,26,27],26,.23)], clips=['Run']),
    'guereza': dict(limbs=[([10,11,12,13],13,.60)], clips=['Run'], minimum_y=.56, minimum_x=.17),
    'common-brushtail-possum': dict(limbs=[([9,10,11,12],12,.20),([13,14,15,16],16,.20),([18,19,20,21,22],21,.22),([23,24,25,26,27],26,.22)], clips=['Run']),
    'cormorant': dict(limbs=[([19,20,21,22],21,.10),([23,24,25,26],25,.10)], clips=['Walk'], wings=list(range(7,17))),
    'pangolin': dict(limbs=[([19,20,21,22,23],22,.25),([24,25,26,27,28],27,.25),([29,30,31,32,33],32,.28),([34,35,36,37,38],37,.28)],clips=['Walk','Run'],head_amplitude={i:.75 for i in range(11,19)}),
    'penguin': dict(limbs=[([31,32,33,34,35],33,.10),([40,41,42,43,44],42,.10)],clips=['Walk']),
    'platypus': dict(limbs=[([8,9,10,11,12],10,.30),([13,14,15,16,17],15,.30)],clips=['Run'],palm_pitch_limit=.55,broad_forefoot_min_y=.25),
    'polar-bear': dict(limbs=[([4,5,6,7],7,.36),([8,9,10,11,12],11,.36)],clips=['Run'],palm_pitch_limit=.65,broad_forefoot_min_y=.15),
    'puffin': dict(limbs=[([21,22,23,24,25],23,.15)],clips=['Walk']),
    'shorebird': dict(limbs=[([18,19,20,21,22,23],20,.17),([33,34,35,36,37,38],35,.17)],clips=['Walk']),
    'shrew': dict(limbs=[([10,11,12,13,14],13,.34),([15,16,17,18,19],18,.34)],clips=['Walk','Run']),
    'tern': dict(limbs=[([22,23,24,25,26],24,.14),([27,28,29,30,31],29,.14)],clips=['Walk'],flight_forward_nodes=(2,6)),
}
ap=argparse.ArgumentParser(); ap.add_argument('--slug',required=True,choices=SPECS)
ap.add_argument('--replace-unpublished',action='store_true')
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]); spec=SPECS[a.slug]
root=repo/'artifacts/fauna-overnight-20261024'/a.slug
choice=json.loads((repo/'docs/fauna/OWNER_SELECTIONS.json').read_text())['animals'][a.slug]
number,=choice['selected_models']; source=root/'Animal - Raw Audition.blend'; out=root/'Animal - Owner Corrected.blend'
assert not out.exists() or (a.replace_unpublished and not choice.get('manual_fix_blend')),'Preserve a published correction'
digest=hashlib.sha256(source.read_bytes()).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(source)); scene=bpy.context.scene
entries=json.loads(scene['deer_four_rigs']); entry=entries[number-1]
body=bpy.data.objects[entry['body']]; rig=bpy.data.objects[entry['rig']]
names=entry['joint_names']; parents=np.asarray(entry['parents']); joints=np.asarray(entry['generated_joint_heads'])
rig.animation_data_clear(); rig.data.pose_position='REST'
vertices=np.array([v.co[:] for v in body.data.vertices]); weights=np.zeros((len(vertices),len(names)))
for v in body.data.vertices:
    for g in v.groups: weights[v.index,names.index(body.vertex_groups[g.group].name)]=g.weight
height=float(np.ptp(vertices[:,2])); rest=[rig.data.bones[n].matrix_local.copy() for n in names]
local=[rest[int(p)].inverted()@r if p>=0 else r for p,r in zip(parents,rest)]
evidence={'slug':a.slug,'candidate':number,'source_sha256':digest,'geometry_unchanged':True,
          'rest_skeleton_unchanged':True,'owner_requests_no_repeat_review':True,'natural_motion_approved':False,
          'method':'Explicit limb-chain skin confinement, rigid distal footprint, shortest rest-to-posed swing without axial flips', 'limbs':[]}
changed=set()
def set_weight(vi,row):
    for group in body.vertex_groups: group.remove([int(vi)])
    keep=np.argsort(row)[-4:]; row=row.copy(); row[[i for i in range(len(row)) if i not in keep]]=0
    row/=row.sum()
    for i in np.flatnonzero(row>1e-9): body.vertex_groups[names[i]].add([int(vi)],float(row[i]),'REPLACE')
    changed.add(int(vi))

distances=[]
for chain,foot,ceiling in spec['limbs']:
    assert all(parents[c]==p for p,c in zip(chain,chain[1:]))
    start=joints[chain[:-1]]; delta=np.diff(joints[chain],axis=0)
    t=np.clip(np.sum((vertices[:,None,:]-start)*delta,axis=2)/np.maximum(np.sum(delta*delta,axis=1),1e-8),0,1)
    distances.append(np.linalg.norm(vertices[:,None,:]-(start+t[:,:,None]*delta),axis=2).min(1))
nearest=np.argmin(distances,axis=0)
for k,(chain,foot,ceiling) in enumerate(spec['limbs']):
    distance=distances[k]; elbow_height=max(joints[chain[min(1,len(chain)-1)],2],ceiling)+height*.06
    mask=(nearest==k)&(vertices[:,2]<elbow_height)&(distance<height*.14)&((weights[:,chain].sum(1)>.18)|(distance<height*.045))
    if 'broad_forefoot_min_y' in spec:
        # Entire low paw, including peripheral vertices with wholly wrong
        # hind-leg weights. A distance cutoff through the same paw tears it.
        mask|=(nearest==k)&(vertices[:,2]<ceiling+height*.10)&(vertices[:,1]>spec['broad_forefoot_min_y'])
    if 'minimum_y' in spec:mask&=vertices[:,1]>spec['minimum_y']
    if 'minimum_x' in spec:mask&=vertices[:,0]>spec['minimum_x']
    ids=np.flatnonzero(mask); rigid=[]; leakage=0
    for vi in ids:
        old=weights[vi]; row=np.zeros(len(names)); row[chain]=old[chain]
        leakage+=float(old.sum()-row.sum())
        if row.sum()<1e-9: row[chain[int(np.argmin(np.linalg.norm(joints[chain]-vertices[vi],axis=1)))]]=1
        row/=row.sum()
        blend=float(np.clip((ceiling+height*.10-vertices[vi,2])/(height*.10),0,1));blend=blend*blend*(3-2*blend)
        row*=1-blend;row[foot]+=blend
        if vertices[vi,2]<ceiling:rigid.append(int(vi))
        set_weight(vi,row)
    assert len(ids)>5 and len(rigid)>3,(a.slug,chain,len(ids),len(rigid))
    evidence['limbs'].append({'chain':chain,'rigid_bone':foot,'changed_vertices':len(ids),'rigid_vertices':rigid,'foreign_weight_removed':leakage})
if spec.get('ear_head') is not None:
    # Owner's koala right ear was sharing distant limb influences. Both outer
    # pinnae follow the actual head rigidly; no new expressive ear motion.
    mask=np.zeros(len(vertices),dtype=bool)
    for centre in [joints[8],joints[10]]:
        mask|=np.sum(((vertices-centre)/np.array([.25,.22,.29]))**2,axis=1)<1
    ids=np.flatnonzero(mask); assert len(ids)>20
    for vi in ids:
        row=np.zeros(len(names)); row[spec['ear_head']]=1; set_weight(vi,row)
    evidence['ear_vertices_pinned_to_head']=len(ids)

targets=set(n for chain,_,_ in spec['limbs'] for n in chain[1:])
flight_landmarks=None
if spec.get('flight_forward_nodes'):
    # The learned bird graph does not reach both physical tail tips. Use the
    # actual skinned beak/tail surface, not a misleading short internal bone.
    lo,hi=np.quantile(vertices[:,1],[.07,.93]);mid=float(np.median(vertices[:,0]))
    head_ids=np.flatnonzero((vertices[:,1]<lo)&(abs(vertices[:,0]-mid)<.32));tail_ids=np.flatnonzero((vertices[:,1]>hi)&(abs(vertices[:,0]-mid)<.42))
    assert len(head_ids)>4 and len(tail_ids)>4
    ids=np.r_[head_ids,tail_ids];flight_landmarks=(ids,len(head_ids),np.c_[vertices[ids],np.ones(len(ids))],weights[ids])
actions=[entry['actions'][c] for c in spec['clips'] if c in entry['actions']]+list(entry['demos'].values())
if spec.get('wings') or spec.get('head_amplitude') or spec.get('flight_forward_nodes'): actions=list(entry['actions'].values())+list(entry['demos'].values())
actions=list(dict.fromkeys(actions)); action_evidence=[]
for action_name in actions:
    action=bpy.data.actions[action_name]; rig.data.pose_position='POSE'; rig.animation_data_create().action=action
    rig.animation_data.action_slot=action.slots[0]; first,last=action.frame_range
    times=np.unique(np.r_[np.arange(first,last+.01,.5),last]); sampled=[]; previous=[None]*len(names)
    for frame in times:
        scene.frame_set(int(frame),subframe=float(frame-int(frame))); bpy.context.view_layer.update()
        matrices=[rig.pose.bones[n].matrix.copy() for n in names]
        for i in targets:
            original=rest[i].to_3x3().col[1].normalized(); posed=matrices[i].to_3x3().col[1].normalized()
            q=original.rotation_difference(posed)@rest[i].to_quaternion()
            m=q.to_matrix().to_4x4(); m.translation=matrices[i].translation; matrices[i]=m
        if spec.get('palm_pitch_limit'):
            for chain,i,ceiling in spec['limbs']:
                raw_q=rig.pose.bones[names[i]].matrix.to_quaternion();delta=raw_q@rest[i].to_quaternion().inverted()
                pitch=float(np.clip(delta.to_euler('XYZ').x,-spec['palm_pitch_limit'],spec['palm_pitch_limit']))
                q=Quaternion((1,0,0),pitch)@rest[i].to_quaternion();m=q.to_matrix().to_4x4();m.translation=matrices[i].translation;matrices[i]=m
        if spec.get('wings'):
            # Reduce only downward wing excursion with FK, keeping real lengths.
            # Upstroke retains 85%, inward/downward stroke 55% of local swing.
            for i in spec['wings']:
                pb=rig.pose.bones[names[i]]; basis=pb.matrix_basis.copy()
                down=rig.pose.bones[names[i]].tail.z<rig.pose.bones[names[i]].head.z
                q=Quaternion().slerp(basis.to_quaternion(),.55 if down else .85)
                basis=q.to_matrix().to_4x4(); basis.translation=pb.matrix_basis.translation
                matrices[i]=matrices[int(parents[i])]@local[i]@basis
        for i,factor in spec.get('head_amplitude',{}).items():
            pb=rig.pose.bones[names[i]]; basis=pb.matrix_basis.copy();q=Quaternion().slerp(basis.to_quaternion(),factor)
            basis=q.to_matrix().to_4x4();basis.translation=pb.matrix_basis.translation*factor
            matrices[i]=matrices[int(parents[i])]@local[i]@basis
        if spec.get('flight_forward_nodes'):
            ids,head_count,points,skin=flight_landmarks
            transforms=np.array([m@r.inverted() for m,r in zip(matrices,rest)])
            q=(np.einsum('bij,kj->bki',transforms,points)*skin.T[:,:,None]).sum(0)[:,:3]
            forward=q[:head_count].mean(0)-q[head_count:].mean(0)
            theta=float(np.arctan2(forward[2],-forward[1]))
            label=next((key for key,value in entry['actions'].items() if value==action_name),'Demo')
            if label in ('Flap','Glide'):factor=1.
            elif label=='Demo':
                factor=float(np.clip((abs(theta)-np.deg2rad(50))/np.deg2rad(20),0,1));factor=factor*factor*(3-2*factor)
            else:factor=0.
            turn=Quaternion((1,0,0),theta*factor).to_matrix().to_4x4();pivot=matrices[0].translation.copy()
            for i,matrix in enumerate(matrices):
                position=pivot+turn.to_3x3()@(matrix.translation-pivot);matrices[i]=turn@matrix;matrices[i].translation=position
        values=[]
        for i,p in enumerate(parents):
            relative=matrices[int(p)].inverted()@matrices[i] if p>=0 else matrices[i]
            basis=local[i].inverted()@relative; q=basis.to_quaternion()
            if previous[i] is not None and q.dot(previous[i])<0: q.negate()
            previous[i]=q.copy(); values.append((basis.translation[:],q[:]))
        sampled.append(values)
    bags=[bag for layer in action.layers for strip in layer.strips for bag in strip.channelbags]
    # Bone matrices only. Other object curves, if any, stay intact.
    for bag in bags:
        for curve in list(bag.fcurves):
            if curve.data_path.startswith('pose.bones['): bag.fcurves.remove(curve)
    for frame,values in zip(times,sampled):
        for i,(location,q) in enumerate(values):
            pb=rig.pose.bones[names[i]]; pb.location=location; pb.rotation_mode='QUATERNION'; pb.rotation_quaternion=q
            pb.keyframe_insert('location',frame=float(frame),group=names[i]); pb.keyframe_insert('rotation_quaternion',frame=float(frame),group=names[i])
    for bag in bags:
        for curve in bag.fcurves:
            for key in curve.keyframe_points: key.interpolation='LINEAR'
    action_evidence.append({'action':action_name,'samples':len(times),'world_joint_centres_preserved':not bool(spec.get('wings'))})

rig.animation_data_clear(); rig.data.pose_position='REST'; scene.frame_set(0)
assert np.array_equal(vertices,np.array([v.co[:] for v in body.data.vertices]))
assert hashlib.sha256(source.read_bytes()).hexdigest()==digest
evidence.update(changed_vertices=len(changed),actions=action_evidence,native_checks_pending=True)
entry['owner_manual_patch']=evidence; entry['manual_repairs']=True
scene['deer_four_rigs']=json.dumps(entries); bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(out))
evidence.update(output=str(out.relative_to(repo)),output_sha256=hashlib.sha256(out.read_bytes()).hexdigest())
(root/'owner-fix.json').write_text(json.dumps(evidence,indent=2)+'\n')
print('SELECTED LIMB PATCH SAVED',a.slug,number,len(changed),'vertices; native/visual checks pending',flush=True)
