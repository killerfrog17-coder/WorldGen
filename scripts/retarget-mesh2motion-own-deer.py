"""Transfer native Mesh2Motion rotations onto the existing deer rig/weights.

This is an isolated comparison, not re-rigging or an animation acceptance test.
No live IK, per-frame floor correction, new skin weights, or mesh edits.
"""
import argparse
import hashlib
import json
from pathlib import Path
import sys
import bpy
import numpy as np
from mathutils import Matrix, Vector

sys.path.insert(0, str(Path(__file__).parent))
from deer_behaviour_core import Skeleton, F, R, Z, matrix
import importlib.util
spec=importlib.util.spec_from_file_location('deer_library',Path(__file__).with_name('build-deer-behaviour-library.py'))
library=importlib.util.module_from_spec(spec);spec.loader.exec_module(library)


def digest(mesh,rig):
    h=hashlib.sha256()
    for v in mesh.data.vertices:
        h.update(np.array(v.co,dtype='<f4').tobytes())
        for g in v.groups:h.update(np.array([g.group,g.weight],dtype='<f8').tobytes())
    h.update(np.array([p.vertices[:] for p in mesh.data.polygons],dtype='<i4').tobytes())
    h.update(np.array([u.uv[:] for u in mesh.data.uv_layers.active.data],dtype='<f4').tobytes())
    h.update(json.dumps(library.metadata(rig),sort_keys=True).encode())
    return h.hexdigest()


def main():
    ap=argparse.ArgumentParser();ap.add_argument('--source',type=Path,required=True)
    ap.add_argument('--motion',type=Path,required=True);ap.add_argument('--root',type=Path,required=True)
    ap.add_argument('--replace-unopened',action='store_true')
    a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=a.root.resolve();root.mkdir(parents=True,exist_ok=True)
    out=root/'Deer - Original Rig Motion Review.blend'
    if out.exists() and not a.replace_unopened:raise FileExistsError(out)
    bpy.ops.wm.open_mainfile(filepath=str(a.source.resolve()))
    scene=bpy.context.scene;rig=bpy.data.objects['AI Deer Rig'];mesh=bpy.data.objects['Locally generated deer']
    before_digest=digest(mesh,rig);source_hash=hashlib.sha256(a.source.read_bytes()).hexdigest()
    rig.animation_data_clear();rig.parent=None;rig.matrix_world.identity()
    for pb in rig.pose.bones:pb.matrix_basis.identity()
    for obj in scene.objects:
        if obj.type=='MESH':obj.hide_set(obj!=mesh);obj.hide_render=obj!=mesh
    own_actions={}
    for action in list(bpy.data.actions):
        name=action.name;action.name='Existing | '+name;action.use_fake_user=True;own_actions[name]=action.name
    skel=Skeleton(library.metadata(rig))
    old_objects=set(bpy.data.objects);old_actions=set(bpy.data.actions)
    # glTF seconds are converted to the current scene's frame rate on import.
    scene.render.fps=30;scene.render.fps_base=1
    bpy.ops.import_scene.gltf(filepath=str(a.motion.resolve()))
    imported=set(bpy.data.objects)-old_objects
    donor=next(o for o in imported if o.type=='ARMATURE');donor.animation_data_clear()
    source_actions={x.name:x for x in set(bpy.data.actions)-old_actions}
    canonical=Matrix((R,-F,Z)).transposed().to_quaternion()
    mapping={'Pelvis':'hips','Spine1':'spine_1','Spine2':'spine_2','Chest':'spine_3',
        'Neck1':'spine_4','Neck2':'spine_5','Neck3':'spine_6','Head':'head','Tail1':'tail_1','Tail2':'tail_2'}
    # Old skin's suffixes do not consistently denote anatomical left/right.
    fore_center=sum((skel.rest[skel.index['UpperFore_'+s]].translation for s in ('L','R')),Vector())/2
    hind_center=sum((skel.rest[skel.index['Thigh_'+s]].translation for s in ('L','R')),Vector())/2
    for side in ('L','R'):
        upper=skel.rest[skel.index['UpperFore_'+side]].translation
        ss='l' if (upper-fore_center).dot(R)>0 else 'r'
        mapping.update({f'Shoulder_{side}':f'front_scapula_{ss}',
            f'UpperFore_{side}':f'front_humerus_{ss}',f'Elbow_{side}':f'front_leg_upper_{ss}',
            f'Wrist_{side}':f'front_leg_lower_{ss}',f'ForeHoof_{side}':f'front_leg_ankle_{ss}'})
        thigh=skel.rest[skel.index['Thigh_'+side]].translation
        ss='l' if (thigh-hind_center).dot(R)>0 else 'r'
        mapping.update({f'Thigh_{side}':f'back_leg_upper_{ss}',f'Knee_{side}':f'back_leg_lower_{ss}',
            f'Hock_{side}':f'back_leg_ankle_{ss}',f'HindHoof_{side}':f'back_leg_foot_{ss}'})
    assert mapping['UpperFore_L']!=mapping['UpperFore_R']
    assert mapping['Thigh_L']!=mapping['Thigh_R']
    rest={n:(donor.matrix_world@donor.data.bones[n].matrix_local).copy() for n in set(mapping.values())}
    scale=skel.rest[skel.index['Pelvis']].translation.z/rest['hips'].translation.z
    retarget={};checks=[];inherited={'Ear_L','Ear_R','Jaw','Muzzle_L','Muzzle_R'}
    scene.render.fps=30;scene.render.fps_base=1
    for clip,src in sorted(source_actions.items()):
        if clip=='Rest_Pose':continue
        donor.animation_data_create().action=src;donor.animation_data.action_slot=src.slots[0]
        start,end=src.frame_range;frames=[];previous=None;max_face_basis=0
        for frame in range(round(start),round(end)+1):
            scene.frame_set(frame)
            delta={n:canonical@((donor.matrix_world@donor.pose.bones[n].matrix).to_quaternion()
                @rest[n].to_quaternion().inverted())@canonical.inverted() for n in rest}
            shift=canonical@((donor.matrix_world@donor.pose.bones['hips'].matrix).translation-rest['hips'].translation)*scale
            globals_m=[]
            for i,name in enumerate(skel.names):
                p=skel.parents[i]
                if name=='MotionRoot':globals_m.append(skel.rest[i].copy());continue
                if name in inherited:
                    globals_m.append(globals_m[p]@skel.local[i]);continue
                q=delta[mapping[name]]@skel.rest[i].to_quaternion()
                pos=globals_m[p]@skel.local[i].translation if p>=0 else skel.rest[i].translation
                if name=='Pelvis':pos=skel.rest[i].translation+shift
                globals_m.append(matrix(q,pos))
            loc,rot=skel.basis(globals_m)
            if previous is not None:
                for j in range(len(rot)):
                    if np.dot(rot[j],previous[j])<0:rot[j]*=-1
            previous=rot.copy();frames.append((loc,rot))
        action=bpy.data.actions.new('Mesh2Motion | '+clip);action.use_fake_user=True
        rig.animation_data_create().action=action
        for f,(loc,rot) in enumerate(frames):
            for i,name in enumerate(skel.names):
                pb=rig.pose.bones[name];pb.rotation_mode='QUATERNION';pb.location=loc[i]
                pb.rotation_quaternion=rot[i];pb.scale=(1,1,1)
                # Mouth and ears follow Head rigidly; no horse jaw deformation.
                if name in inherited:
                    pb.matrix_basis.identity();max_face_basis=max(max_face_basis,float(np.max(np.abs(loc[i]))))
                pb.keyframe_insert('location',frame=f);pb.keyframe_insert('rotation_quaternion',frame=f)
        for layer in action.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    for curve in bag.fcurves:
                        for key in curve.keyframe_points:key.interpolation='LINEAR'
        retarget[clip]=action.name
        checks.append(dict(clip=clip,frames=len(frames),face_local_translation_error=max_face_basis))
    for obj in imported:bpy.data.objects.remove(obj,do_unlink=True)
    for action in source_actions.values():bpy.data.actions.remove(action)
    assert digest(mesh,rig)==before_digest,'Existing asset changed'
    scene['own_rig_review']=json.dumps(dict(rig=rig.name,mesh=mesh.name,original=own_actions,
        transferred=retarget,original_fps=24,transferred_fps=30))
    scene['review_status']='Original rig/skin. Authored motion transfer comparison, not accepted gait.'
    # Start in the actual unchanged bind pose so pausing cannot hide a deformation.
    rig.animation_data_clear();rig.data.pose_position='REST'
    for pb in rig.pose.bones:pb.matrix_basis.identity()
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                space=area.spaces.active;space.overlay.show_overlays=False;space.show_region_ui=True
                space.shading.type='MATERIAL';space.shading.use_scene_world=False
                space.region_3d.view_location=(0,0,.8);space.region_3d.view_distance=3.5
                space.region_3d.view_rotation=Vector((3.4,-4.6,1.1)).to_track_quat('Z','Y')
    bpy.ops.wm.save_as_mainfile(filepath=str(out))
    checks=dict(source_sha256=source_hash,unchanged_geometry_uv_weights_rest_digest=before_digest,
        original_bones=len(rig.data.bones),mapping=mapping,clips=checks,
        original_actions_retained=True,new_rig_or_weights=False,live_ik=False,
        source_unchanged=hashlib.sha256(a.source.read_bytes()).hexdigest()==source_hash)
    (root/'retarget-checks.json').write_text(json.dumps(checks,indent=2))
    print('Own-rig motion comparison saved.',flush=True)


if __name__=='__main__':main()
