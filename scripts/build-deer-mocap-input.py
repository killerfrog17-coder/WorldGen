"""Export consistently Y-up, metre-scale MoCap inputs from our native deer.

Upstream's Max/centimetre FBX assumptions do not hold for this Blender asset.
This adapter exports the evaluated native rig, not a replacement character.
Run in motion-eval-venv (bpy), CPU only.
"""
import argparse
import json
import os
from pathlib import Path
import shutil
import sys


def main():
    ap=argparse.ArgumentParser()
    ap.add_argument('--root',type=Path,required=True)
    ap.add_argument('--repo',type=Path,required=True)
    ap.add_argument('--source',type=Path,required=True)
    a=ap.parse_args();root=a.root.resolve();repo=a.repo.resolve()
    os.environ.update(CUDA_VISIBLE_DEVICES='',OMP_NUM_THREADS='3',PYTHONIOENCODING='utf-8')
    sys.path.insert(0,str(repo))
    import bpy
    import numpy as np
    from mathutils import Vector
    from utils.bvh import save_dict
    from utils.transforms3d import matrix_to_euler_angles
    import torch
    bpy.ops.wm.open_mainfile(filepath=str(a.source.resolve()))
    rig=bpy.data.objects['AI Deer Rig'];mesh=bpy.data.objects['Locally generated deer']
    rig.animation_data_clear();rig.parent=None;rig.matrix_world.identity()
    for pb in rig.pose.bones: pb.matrix_basis.identity()
    bpy.context.view_layer.update()
    names=[b.name for b in rig.data.bones if b.name!='MotionRoot']
    parents=np.array([names.index(rig.data.bones[n].parent.name)
        if rig.data.bones[n].parent and rig.data.bones[n].parent.name in names else -1 for n in names])
    B=np.array([[.40312079,-.91514678,0],[0,0,1],[-.91514678,-.40312079,0]])
    # Remove rounding in the basis rather than introducing a scale/shear.
    B[0]/=np.linalg.norm(B[0]);B[2]/=np.linalg.norm(B[2])
    rest=np.array([rig.data.bones[n].matrix_local for n in names])
    rp=rest[:,:3,3]@B.T
    offsets=rp.copy()
    for j,p in enumerate(parents):
        if p>=0: offsets[j]-=rp[p]
    zoo=root/'mocap-work/zoo';char=zoo/'characters_face_zplus/WorldGenDeer'
    char.mkdir(parents=True,exist_ok=True)
    def save(path,rots,positions):
        e=np.degrees(matrix_to_euler_angles(torch.tensor(rots),'ZYX').numpy())
        save_dict(str(path),dict(rotations=e,positions=positions,offsets=offsets,
            parents=parents,names=names,order='zyx',frametime=1/30),fps=30)
    save(char/'rest.bvh',np.tile(np.eye(3),(1,len(names),1,1)),offsets[None])
    shutil.copy2(char/'rest.bvh',char/'WorldGenDeer_ffs.bvh')
    vertices=np.array([mesh.matrix_world@v.co for v in mesh.data.vertices])@B.T
    # Preserve source vertex/weight/UV indexing for the reference renderer.
    with (char/'base_mesh.obj').open('w') as f:
        f.write('mtllib base_mesh.mtl\nusemtl Deer\n')
        for v in vertices:f.write('v '+' '.join(map(str,v))+'\n')
        uv=mesh.data.uv_layers.active.data
        for l in mesh.data.loops:f.write('vt '+' '.join(map(str,uv[l.index].uv))+'\n')
        for p in mesh.data.polygons:
            f.write('f '+' '.join(f'{mesh.data.loops[l].vertex_index+1}/{l+1}' for l in p.loop_indices)+'\n')
    color=next(n.image for n in mesh.active_material.node_tree.nodes if n.type=='TEX_IMAGE' and
        n.image and ('albedo' in n.image.name.lower() or 'colour' in n.image.name.lower()))
    color.save_render(str(char/'deer-colour.png'))
    (char/'base_mesh.mtl').write_text('newmtl Deer\nKd 1 1 1\nmap_Kd deer-colour.png\n')
    weights=np.zeros((len(vertices),len(names)),np.float32)
    for v in mesh.data.vertices:
        for g in v.groups:
            name=mesh.vertex_groups[g.group].name
            if name in names:weights[v.index,names.index(name)]=g.weight
    assert np.max(np.abs(weights.sum(1)-1))<1e-5
    np.save(char/'skinning_weights.npy',weights)
    action=bpy.data.actions['Walk'];rig.animation_data_create().action=action
    rig.animation_data.action_slot=action.slots[0]
    count=36;rots=[];positions=[];max_residual=0
    for frame in np.arange(count)*24/30:
        bpy.context.scene.frame_set(int(frame),subframe=float(frame%1))
        mats=np.array([rig.pose.bones[n].matrix for n in names])
        delta=B@mats[:,:3,:3]@np.swapaxes(rest[:,:3,:3],-1,-2)@B.T
        local=delta.copy()
        pp=np.tile(offsets,(1,1));pp[0]=B@mats[0,:3,3]
        for j,p in enumerate(parents):
            if p>=0:
                local[j]=delta[p].T@delta[j]
                expected=delta[p]@offsets[j]
                measured=B@(mats[j,:3,3]-mats[p,:3,3])
                max_residual=max(max_residual,float(np.linalg.norm(expected-measured)))
        rots.append(local);positions.append(pp)
    motion=zoo/'bvh/WorldGenDeer#WorldGenDeer-Walk/y0.bvh';motion.parent.mkdir(parents=True,exist_ok=True)
    save(motion,np.array(rots),np.array(positions))
    bpy.context.scene.frame_set(0)
    scene=bpy.context.scene
    for obj in scene.objects:
        if obj.type=='MESH':obj.hide_render=obj!=mesh
    scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=8
    scene.render.threads_mode='FIXED';scene.render.threads=3
    scene.render.film_transparent=True;scene.render.resolution_x=640;scene.render.resolution_y=480
    scene.render.resolution_percentage=100
    # Camera lies along canonical -Z, the upstream y0 reference view.
    fwd=Vector(B[2]);scene.camera.location=-fwd*4+Vector((0,0,1.15))
    scene.camera.rotation_euler=(Vector((0,0,.9))-scene.camera.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(root/'input/mocap-reference.png');bpy.ops.render.render(write_still=True)
    # Upstream helper has a historical characters_fix_facezplus lookup.
    fixed=zoo/'characters_fix_facezplus/WorldGenDeer'
    for name in ('base_mesh.obj','base_mesh.mtl','skinning_weights.npy','deer-colour.png'):
        shutil.copy2(char/name,fixed/name)
    os.chdir(root/'mocap-work')
    from preprocess import extract_bvh_pose
    extract_bvh_pose.species_scale_cache.clear()
    extract_bvh_pose.process_bvh_root(str(zoo/'bvh'),str(zoo/'bvh_pose'),auto_scale='base_mesh',fps=30)
    from preprocess.build_species_info import build
    cond=np.load(root/'unimate-rig/cond.npy',allow_pickle=True).item()['WorldGenDeer']
    name_map={'WorldGenDeer':dict(zip(cond['joint_names'],cond['clean_joint_names']))}
    info=build(str(zoo),name_map=name_map,t5_model='E:/AI/WorldGen3D/models/t5-base',device='cpu')
    np.save(zoo/'species_info_dict.npy',dict(info))
    from preprocess.build_scale_cache import species_global_scale
    import pickle
    scale,n=species_global_scale([str(zoo/'bvh_pose'/motion.parent.name)])
    assert scale>.1 and n==1,(scale,n)
    with (zoo/'cache/__mesh2pose1002_species_scale_cache.pkl').open('wb') as f:
        pickle.dump({'WorldGenDeer':{'global_scale':scale,'bbox_center':np.zeros(3,np.float32)}},f)
    (root/'mocap-native-input-check.json').write_text(json.dumps({'joints':len(names),
        'unit':'metres','up':'Y','forward':'+Z','scale':scale,
        'native_non_root_translation_residual_m':max_residual,'gpu_started':False},indent=2))


if __name__=='__main__':main()
