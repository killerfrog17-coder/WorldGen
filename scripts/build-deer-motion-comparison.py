"""Bake learned takes onto the unchanged accepted deer for Blender review.

CPU-only bpy script. Fits rest joint positions, then transfers global rotation
deltas into the actual bind axes. Stores deformation/retarget evidence; numeric
checks do not assert natural gait, loop quality or game readiness.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import sys
import numpy as np
from scipy.spatial.transform import Rotation


def similarity(x,y):
    xc=x-x.mean(0);yc=y-y.mean(0)
    u,s,vt=np.linalg.svd(xc.T@yc)
    d=np.ones(3);d[-1]=np.linalg.det(vt.T@u.T)
    r=vt.T@np.diag(d)@u.T
    scale=np.sum(s*d)/np.sum(xc*xc)
    t=y.mean(0)-scale*r@x.mean(0)
    residual=float(np.max(np.linalg.norm(x@r.T*scale+t-y,axis=1)))
    return r,scale,t,residual


def digest_mesh(obj):
    data=np.array([v.co for v in obj.data.vertices],dtype='<f4').tobytes()
    data+=np.array([l.vertex_index for l in obj.data.loops],dtype='<i4').tobytes()
    data+=np.array([l.uv for l in obj.data.uv_layers.active.data],dtype='<f4').tobytes()
    data+=str([(v.index,[(g.group,g.weight) for g in v.groups]) for v in obj.data.vertices]).encode()
    return hashlib.sha256(data).hexdigest()


def main():
    ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True)
    ap.add_argument('--source',type=Path,required=True);ap.add_argument('--render',action='store_true')
    a=ap.parse_args();root=a.root.resolve();source=a.source.resolve()
    os.environ['CUDA_VISIBLE_DEVICES']=''
    import bpy
    from mathutils import Matrix,Vector,Quaternion
    bpy.ops.wm.open_mainfile(filepath=str(source))
    scene=bpy.context.scene;rig=bpy.data.objects['AI Deer Rig'];mesh=bpy.data.objects['Locally generated deer']
    original_hash=digest_mesh(mesh)
    image_hashes={i.name:hashlib.sha256(np.asarray(i.pixels[:],dtype='<f4').tobytes()).hexdigest()
        for i in bpy.data.images if i.type=='IMAGE' and i.has_data}
    rig.parent=None;rig.matrix_world.identity();rig.animation_data_clear()
    for pb in rig.pose.bones:
        pb.matrix_basis.identity();pb.rotation_mode='QUATERNION'
        assert not pb.constraints,pb.name
    for obj in scene.objects:
        if obj.type=='MESH':
            obj.hide_set(obj!=mesh);obj.hide_render=obj!=mesh
    mesh.hide_set(False);mesh.hide_render=False;rig.hide_set(True)
    for act in bpy.data.actions:act.use_fake_user=True
    baseline={n:dict(action=n,fps=24,frames=int(bpy.data.actions[n].frame_range[1])+1,
        family='Current CC0-derived',variant='baseline',status='Existing native clip, no runtime IK')
        for n in ('Walk','Run','Graze','Alert')}
    reports=json.loads((root/'comparison-report.json').read_text());checks=[];catalogue=list(baseline.values())
    desired_samples={}
    for report in reports:
        for variant in ('raw','clean'):
            label=report['label']+' '+variant
            with np.load(root/'comparison-motions'/(label+'.npz')) as n:d={k:n[k].copy() for k in n.files}
            names=list(d['names']);rest=d['rest_positions'];positions=d['positions'];grot=d['global_rotations']
            target=np.array([rig.data.bones[name].head_local for name in names])
            r,scale,t,residual=similarity(rest,target)
            assert residual<2e-4,(label,residual)
            p=positions@r.T*scale+t
            # Ground translation remains as generated. No frame-by-frame floor lift.
            restm={b.name:np.array(b.matrix_local) for b in rig.data.bones}
            matrices=np.tile(np.eye(4),(len(p),len(names),1,1))
            for j,name in enumerate(names):
                matrices[:,j,:3,:3]=r@grot[:,j]@r.T@restm[name][:3,:3]
                matrices[:,j,:3,3]=p[:,j]
            action=bpy.data.actions.new(label);action.use_fake_user=True
            rig.animation_data_create().action=action
            previous={}
            for f in range(len(p)):
                lookup={name:matrices[f,j] for j,name in enumerate(names)}
                for name in names:
                    pb=rig.pose.bones[name];parent=pb.parent
                    if parent:
                        parent_pose=lookup.get(parent.name,restm[parent.name])
                        basis=np.linalg.inv(restm[name])@restm[parent.name]@np.linalg.inv(parent_pose)@lookup[name]
                    else:basis=np.linalg.inv(restm[name])@lookup[name]
                    loc,quat,sc=Matrix(basis.tolist()).decompose()
                    if name in previous and quat.dot(previous[name])<0:quat.negate()
                    previous[name]=quat.copy()
                    pb.location=loc;pb.rotation_quaternion=quat;pb.scale=(1,1,1)
                    pb.keyframe_insert('location',frame=f,group=name)
                    pb.keyframe_insert('rotation_quaternion',frame=f,group=name)
            for layer in action.layers:
                for strip in layer.strips:
                    for bag in strip.channelbags:
                        for fc in bag.fcurves:
                            for k in fc.keyframe_points:k.interpolation='LINEAR'
            rig.animation_data.action_slot=action.slots[0]
            max_fk=0.;minimum=1e9;extent=0.
            for f in range(len(p)):
                scene.frame_set(f)
                actual=np.array([rig.pose.bones[name].matrix.translation for name in names])
                max_fk=max(max_fk,float(np.linalg.norm(actual-p[f],axis=1).max()))
                evaluated=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
                vv=np.array([evaluated.matrix_world@v.co for v in evaluated.data.vertices])
                assert np.isfinite(vv).all()
                minimum=min(minimum,float(vv[:,2].min()));extent=max(extent,float(np.ptp(vv,axis=0).max()))
            assert max_fk<2e-4,(label,max_fk)
            checks.append(dict(label=label,rest_fit_max_m=residual,pose_head_error_max_m=max_fk,
                skinned_floor_min_m=minimum,skinned_extent_max_m=extent,frames=len(p)))
            metric=report[variant]
            status=metric['status']
            if variant=='clean' and not report['cleanup_admitted']:status='CLEANUP REJECTED'
            catalogue.append(dict(action=label,fps=30,frames=len(p),family=report['label'],variant=variant,
                status=status,score=metric['score'],source=report['source']))
            desired_samples[label]=matrices
            print('Baked',label,'retarget error',max_fk,flush=True)
    assert digest_mesh(mesh)==original_hash
    assert image_hashes=={i.name:hashlib.sha256(np.asarray(i.pixels[:],dtype='<f4').tobytes()).hexdigest()
        for i in bpy.data.images if i.type=='IMAGE' and i.has_data}
    scene['motion_comparison_catalogue']=json.dumps(catalogue)
    scene['motion_comparison_root']=str(root);scene['motion_comparison_research_only']=True
    scene.render.fps=30;scene.frame_start=0;scene.frame_end=59
    rig.animation_data.action=bpy.data.actions['UniMate walk raw']
    rig.animation_data.action_slot=rig.animation_data.action.slots[0]
    scene.frame_set(0)
    scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=12
    scene.render.threads_mode='FIXED';scene.render.threads=3
    scene.render.resolution_x=800;scene.render.resolution_y=600;scene.render.resolution_percentage=100
    scene.render.film_transparent=False
    scene.camera.location=(2.2,-5,1.7)
    scene.camera.rotation_euler=(Vector((0,0,.8))-scene.camera.location).to_track_quat('-Z','Y').to_euler()
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                space=area.spaces.active;space.show_region_ui=True;space.overlay.show_overlays=False
                space.region_3d.view_location=(0,0,.8);space.region_3d.view_distance=4.2
                space.region_3d.view_rotation=Vector((2.2,-5,1.3)).to_track_quat('Z','Y')
                space.shading.type='MATERIAL';space.shading.use_scene_world=False
    output=root/'Deer - AI Motion Comparison.blend'
    bpy.ops.wm.save_as_mainfile(filepath=str(output),check_existing=False)
    (root/'native-comparison-checks.json').write_text(json.dumps(dict(mesh_skin_uv_unchanged=True,
        image_pixels_unchanged=True,source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),
        mesh_digest=original_hash,clips=checks,game_integration=False),indent=2))
    if a.render:
        for name,frame in [('UniMate walk raw',15),('UniMate run raw',30),('UniMate feed raw',45),
                ('MoCap browse raw',45),('MoCap pose IK raw',45),('UniMate run clean',30)]:
            action=bpy.data.actions.get(name)
            if not action:continue
            rig.animation_data.action=action;rig.animation_data.action_slot=action.slots[0];scene.frame_set(frame)
            scene.render.filepath=str(root/(name+'.png'));bpy.ops.render.render(write_still=True)
    print('Blender comparison built.',flush=True)


if __name__=='__main__':main()
