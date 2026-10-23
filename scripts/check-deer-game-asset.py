"""Bounded offline asset checks, not runtime FPS or art acceptance."""
import argparse
import hashlib
import json
from pathlib import Path
import sys
import bpy
import numpy as np
from mathutils.bvhtree import BVHTree


def rig_snapshot():
    scene=bpy.context.scene
    rig=bpy.data.objects['AI Deer Rig']
    rig.data.pose_position='POSE'
    rest=np.array([np.array(b.matrix_local) for b in rig.data.bones])
    poses=[]
    for name in ('Idle','Walk','Run'):
        action=bpy.data.actions[name]
        rig.animation_data.action=action
        if action.slots:
            rig.animation_data.action_slot=action.slots[0]
        for f in range(int(action.frame_range[0]),int(action.frame_range[1])+1):
            scene.frame_set(f)
            poses.append(np.array([np.array(b.matrix) for b in rig.pose.bones]))
    return rest,np.array(poses)


def main():
    ap=argparse.ArgumentParser()
    ap.add_argument('--source',type=Path,required=True)
    ap.add_argument('--review',type=Path,required=True)
    ap.add_argument('--root',type=Path,required=True)
    args=ap.parse_args(sys.argv[sys.argv.index('--')+1:])
    args.source,args.review,args.root=[p.resolve() for p in (args.source,args.review,args.root)]
    bpy.ops.wm.open_mainfile(filepath=str(args.source))
    original=rig_snapshot()
    bpy.ops.wm.open_mainfile(filepath=str(args.review))
    current=rig_snapshot()
    assert all(np.array_equal(a,b) for a,b in zip(original,current)), 'Rig or evaluated animation changed'
    scene=bpy.context.scene
    rig=bpy.data.objects['AI Deer Rig']
    assert len(rig.data.bones)==33
    assert len([o for o in scene.objects if o.type=='ARMATURE'])==1
    checks=[]
    names=scene['review_lods'].split('|')
    for name in names:
        mesh=bpy.data.objects[name]
        influences=[]
        for v in mesh.data.vertices:
            weights=[g.weight for g in v.groups if g.weight>1e-7]
            assert 1<=len(weights)<=4
            assert abs(sum(weights)-1)<1e-5
            influences.append(len(weights))
        assert len(mesh.data.materials)==1
        assert mesh.data.uv_layers.active
        mesh.hide_set(False)
        low=1e6
        for action_name,f in [('Idle',0),('Walk',4),('Walk',14),('Run',3),('Run',8)]:
            action=bpy.data.actions[action_name]
            rig.animation_data.action=action
            if action.slots:
                rig.animation_data.action_slot=action.slots[0]
            scene.frame_set(f)
            evaluated=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
            xyz=np.array([v.co[:] for v in evaluated.data.vertices])
            assert np.isfinite(xyz).all() and np.max(np.abs(xyz))<5
            low=min(low,float(xyz[:,2].min()))
        mesh.hide_set(True)
        checks.append(dict(name=name,vertices=len(mesh.data.vertices),triangles=len(mesh.data.polygons),
            max_weights=max(influences),sampled_min_z=low))
    mesh=bpy.data.objects[names[0]]
    tree=BVHTree.FromPolygons([v.co for v in mesh.data.vertices],[p.vertices[:] for p in mesh.data.polygons],all_triangles=True)
    high=np.load(args.root/'high-shape.npz')
    errors=[]
    for v in high['vertices'][::max(1,len(high['vertices'])//4000)]:
        point,normal,index,distance=tree.find_nearest(v)
        errors.append(distance)
    images=[]
    material=mesh.data.materials[0]
    animal_images={n.image for n in material.node_tree.nodes if n.type=='TEX_IMAGE' and n.image}
    for image in animal_images:
        # Packed file images are decoded lazily after opening a .blend.
        assert image.packed_file, 'External review texture missing packing'
        assert len(image.pixels)>0
        assert np.isfinite(image.pixels[0])
        images.append(dict(name=image.name,size=list(image.size),colour_space=image.colorspace_settings.name))
    for image in bpy.data.images:
        if image.packed_file:
            assert hashlib.sha256(image.packed_file.data).hexdigest()!='eb42fd356b0de4d1b326b49754f00e5fab1f0b8826ec1ba82b8c80783cbef3d9', 'Old Hunyuan image retained'
    assert sorted(tuple(i['size']) for i in images)==[(1024,1024),(2048,2048)],json.dumps(images)
    assert all('hunyuan' not in i['name'].lower() for i in images)
    report=dict(unchanged_rest_and_all_123_evaluated_bone_frames=True,lods=checks,textures=images,
        sampled_shape_distance_m=dict(max=max(errors),p95=float(np.percentile(errors,95)),samples=len(errors)),
        scope='Offline geometry/rig/texture checks and selected CPU renders, not runtime profiling or art acceptance')
    (args.root/'asset-checks.json').write_text(json.dumps(report,indent=2))
    print(json.dumps(report),flush=True)


if __name__=='__main__':
    main()
