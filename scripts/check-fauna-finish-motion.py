"""Sample added facial/trait skin across source clips; never rewrite a native file.

Finite geometry and attachment diagnostics are not contact/collision or gait
acceptance. Geometry signatures allow later albedo-only saves to be distinguished
from a genuine geometry/skin change that would require another motion check.
"""
import argparse,hashlib,json,sys,time
from pathlib import Path
import bpy,numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
repo=Path(__file__).resolve().parent.parent;root=repo/'artifacts/fauna-painted-20261024'
jobs=json.loads((root/'paint-queue.json').read_text())['jobs'];out=root/'inspection'
ap=argparse.ArgumentParser();ap.add_argument('--slugs',nargs='*')
args=ap.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
if args.slugs:jobs=[j for j in jobs if j['slug'] in args.slugs]
progress={'total':len(jobs),'completed':[],'errors':{},'warnings':{},'current':None}
def save():
    temp=out/'motion-check-progress.tmp';temp.write_text(json.dumps(progress,indent=2)+'\n');temp.replace(out/'motion-check-progress.json')
def signature(objects):
    h=hashlib.sha256()
    for ob in objects:
        h.update(ob.name.encode());h.update(np.array([v.co[:] for v in ob.data.vertices],'<f4').tobytes())
        h.update(json.dumps([[(ob.vertex_groups[g.group].name,g.weight) for g in v.groups] for v in ob.data.vertices]).encode())
    return h.hexdigest()
deferred=[]
for job in jobs:
    slug=job['slug'];folder=root/slug
    paint=json.loads((root/'finish-progress.json').read_text())
    if paint.get('current')==slug:deferred.append(job);continue
    progress['current']=slug;save()
    try:
        blend=folder/'Animal - Painted Species Review.blend';digest=hashlib.sha256(blend.read_bytes()).hexdigest()
        bpy.ops.wm.open_mainfile(filepath=str(blend));s=bpy.context.scene;entry=json.loads(s['deer_four_rigs'])[0]
        body=bpy.data.objects[entry['body']];rig=bpy.data.objects[entry['rig']];rig.hide_set(False);rig.animation_data_clear();rig.data.pose_position='REST'
        features=[bpy.data.objects[n] for n in [*entry.get('facial_objects',[]),*entry.get('appendage_objects',[])]]
        for ob in [body,*features]:ob.hide_set(False);ob.hide_render=False
        s.frame_set(0);bpy.context.view_layer.update();feature_sig=signature(features)
        edges={};initial={}
        for ob in features:
            points=np.array([v.co[:] for v in ob.data.vertices]);ids=np.array([e.vertices[:] for e in ob.data.edges])
            lengths=np.linalg.norm(points[ids[:,0]]-points[ids[:,1]],axis=1);mask=lengths>1e-5
            edges[ob.name]=(ids[mask],lengths[mask]);initial[ob.name]=points
        samples=[];worst_stretch=1.;worst_compression=1.;worst_eye=0.;warnings=[];worst_absolute=0.;worst_object={};visible_distortions=[]
        preferred=['Idle','Walk','Trot','Run','Eating','Sleep','Swim','Flight','Flap','Glide','Slither','Side winding','Sidewinding']
        clips=[c for c in preferred if c in entry['actions']]
        if not clips:clips=list(entry['actions'])[:2]
        for clip in clips:
            rig.data.pose_position='POSE';action=bpy.data.actions[entry['actions'][clip]];rig.animation_data_create().action=action;rig.animation_data.action_slot=action.slots[0]
            first,last=action.frame_range
            for phase in [0.,.2,.4,.6,.8,1.]:
                t=first+(last-first)*phase;s.frame_set(int(t),subframe=t-int(t));bpy.context.view_layer.update();deps=bpy.context.evaluated_depsgraph_get()
                ev=body.evaluated_get(deps);mesh=ev.to_mesh();body_points=[ev.matrix_world@v.co for v in mesh.vertices]
                assert np.isfinite(np.array(body_points)).all();faces=[p.vertices[:] for p in mesh.polygons]
                bvh=BVHTree.FromPolygons(body_points,faces,all_triangles=all(len(f)==3 for f in faces));ev.to_mesh_clear()
                for ob in features:
                    ev=ob.evaluated_get(deps);mesh=ev.to_mesh();p=np.array([v.co[:] for v in mesh.vertices]);assert np.isfinite(p).all(),ob.name
                    ids,lengths=edges[ob.name];ratios=np.linalg.norm(p[ids[:,0]]-p[ids[:,1]],axis=1)/lengths
                    if len(ratios):
                        worst_stretch=max(worst_stretch,float(ratios.max()));worst_compression=min(worst_compression,float(ratios.min()))
                        absolute=np.abs(ratios-1)*lengths;index=int(absolute.argmax())
                        if float(absolute[index])>worst_absolute:
                            worst_absolute=float(absolute[index]);worst_object={'object':ob.name,'clip':clip,'phase':phase,'edge_ratio':float(ratios[index]),'bind_edge_length':float(lengths[index])}
                        if np.any(((ratios>1.5)|(ratios<.5))&(absolute>.004)):visible_distortions.append({'object':ob.name,'clip':clip,'phase':phase,'absolute_change':float(absolute.max())})
                    if ob.name.endswith(' eye'):
                        center=ev.matrix_world@Vector(p.mean(0));distance=bvh.find_nearest(center)[3];worst_eye=max(worst_eye,float(distance))
                    ev.to_mesh_clear()
                samples.append({'clip':clip,'phase':phase})
        if visible_distortions:warnings.append('Added feature edge distortion exceeds conservative relative AND4mm absolute diagnostic bounds; inspect source motion/attachment.')
        if worst_eye>.09:warnings.append('Eye centre is more than9cm from the evaluated body surface at a sampled phase; inspect attachment.')
        report={'slug':slug,'read_blend_sha256':digest,'source_body_geometry_sha256':json.loads((folder/'paint-state.json').read_text())['geometry_sha256'],'added_geometry_skin_signature':feature_sig,'samples':samples,'all_sampled_surfaces_finite':True,'max_added_edge_ratio':worst_stretch,'min_added_edge_ratio':worst_compression,'max_eye_center_body_surface_distance':worst_eye,'warnings':warnings,'appearance_approved':False,'limits':'Added-feature attachment diagnostics only. Existing source body motion, contacts, anatomy and collisions remain independent of this result.'}
        report.update(max_added_edge_absolute_change=worst_absolute,worst_added_edge=worst_object,visible_distortion_samples=visible_distortions)
        (folder/'paint-motion-checks.json').write_text(json.dumps(report,indent=2)+'\n')
        if warnings:progress['warnings'][slug]=warnings
        progress['completed'].append(slug)
    except Exception as exc:progress['errors'][slug]=str(exc)
    save()
progress['current']=None;progress['deferred']=[j['slug'] for j in deferred];progress['stage']='finished';save()
