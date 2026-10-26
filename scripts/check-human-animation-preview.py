"""Check saved actions, source preservation and sampled deformed surfaces."""
import hashlib
import json
import sys
from pathlib import Path

import bpy
import numpy as np

sys.path.insert(0,str(Path(__file__).resolve().parent))
from human_motion_common import ROOT, REPO
import human_animation_stage as stage


def digest(rig,collection):
    sha=hashlib.sha256()
    for bone in rig.data.bones:
        sha.update(bone.name.encode());sha.update(np.array(bone.matrix_local,dtype='<f4').tobytes())
    for obj in sorted([o for o in collection.objects if o.type=='MESH'],key=lambda o:o.name):
        sha.update(obj.name.encode());sha.update(np.array([tuple(v.co) for v in obj.data.vertices],dtype='<f4').tobytes())
        for v in obj.data.vertices:
            sha.update(str([(obj.vertex_groups[g.group].name,round(g.weight,8)) for g in v.groups]).encode())
        if obj.data.shape_keys:
            for key in obj.data.shape_keys.key_blocks:
                sha.update(key.name.encode());sha.update(np.array([tuple(v.co) for v in key.data],dtype='<f4').tobytes())
    return sha.hexdigest()


def main():
    baseline=REPO/'artifacts/human-builder-demo/Human - Character Builder Comparison.blend'
    baseline_sha=hashlib.sha256(baseline.read_bytes()).hexdigest()
    assert baseline_sha=='6a688775b591065c5d690cd6105e45d03a1662c669fad08cc7ea9dd8ed2e0cd6'
    bpy.ops.wm.open_mainfile(filepath=str(baseline))
    original={code:digest(next(o for o in bpy.data.collections['MPFB_'+code].objects if o.type=='ARMATURE'),bpy.data.collections['MPFB_'+code]) for code in 'ABCDEF'}
    native=ROOT/'Human - Animation Comparison.blend';bpy.ops.wm.open_mainfile(filepath=str(native))
    scene=bpy.context.scene;report={'native_sha256':hashlib.sha256(native.read_bytes()).hexdigest(),
                                 'baseline_sha256':baseline_sha,'characters':[],'limitations':['Pose sampling is not collision testing, contact IK or gameplay acceptance.']}
    assert len(stage.rigs())==12
    assert not [i.name for i in bpy.data.images if i.source=='FILE' and i.filepath and not i.packed_file]
    for method in ['MPFB','UMA']:
        for code in 'ABCDEF':
            collection=bpy.data.collections[method+'_'+code]
            rig=next(o for o in collection.objects if o.type=='ARMATURE')
            if method=='MPFB':assert digest(rig,collection)==original[code],code
            clips=json.loads(rig['preview_clips']);assert len(clips)==15
            for clip in clips:
                action=bpy.data.actions[clip['action']]
                assert action['license']=='CC0'
                assert hashlib.sha256((REPO/action['source_file']).read_bytes()).hexdigest()==action['source_sha256']
            report['characters'].append({'builder':method,'person':code,'bones':len(rig.data.bones),'clips':len(clips),
                                         'original_mpfb_mesh_weights_shapes_rest_exact':method=='MPFB'})
    checks=[]
    for label in json.loads(scene['animation_labels']):
        stage.select_clip(label)
        for phase in [0,.25,.5,.75,1]:
            scene.frame_set(1+round((scene.frame_end-1)*phase));graph=bpy.context.evaluated_depsgraph_get()
            for method in ['MPFB','UMA']:
                for code in 'ABCDEF':
                    collection=bpy.data.collections[method+'_'+code]
                    mins=[];maxs=[]
                    for obj in collection.objects:
                        if obj.type!='MESH':continue
                        evaluated=obj.evaluated_get(graph);mesh=evaluated.to_mesh()
                        points=np.empty(len(mesh.vertices)*3,dtype=np.float32);mesh.vertices.foreach_get('co',points)
                        points=points.reshape((-1,3));assert np.isfinite(points).all(),(label,phase,obj.name)
                        if len(points):mins.append(points.min(axis=0));maxs.append(points.max(axis=0))
                        evaluated.to_mesh_clear()
                    low=np.min(mins,axis=0);high=np.max(maxs,axis=0)
                    assert float(np.max(high-low))<4,(label,phase,method,code,low,high)
                    checks.append({'clip':label,'phase':phase,'builder':method,'person':code,'bounds_min':low.tolist(),'bounds_max':high.tolist()})
        print('SURFACE_CHECKED',label,flush=True)
    report['sampled_surfaces']=checks;report['sample_count']=len(checks)
    (ROOT/'native-check.json').write_text(json.dumps(report,indent=2))
    print('HUMAN_ANIMATION_NATIVE_PASS',len(report['characters']),len(checks),flush=True)


if __name__=='__main__':main()
