"""Prepare a static native-material comparison; no rig/animation replacement."""
import argparse
import hashlib
import json
from pathlib import Path
import sys
import bpy
import numpy as np
from mathutils import Vector

ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True)
ap.add_argument('--previous',type=Path,required=True);a=ap.parse_args(sys.argv[sys.argv.index('--')+1:])
root=a.root.resolve();out=root/'Deer - TRELLIS2 Material Review.blend'
if out.exists():raise FileExistsError(out)
bpy.ops.wm.open_mainfile(filepath=str(a.previous.resolve()))
s=bpy.context.scene;rig=bpy.data.objects['AI Deer Rig'];old=bpy.data.objects['Locally generated deer']
rig.animation_data_clear();rig.parent=None;rig.matrix_world.identity();rig.data.pose_position='REST'
for pb in rig.pose.bones:pb.matrix_basis.identity()
for obj in s.objects:
    if obj.type=='MESH':obj.hide_render=True;obj.hide_set(True)
before=set(bpy.data.objects)
bpy.ops.import_scene.gltf(filepath=str(root/'Deer-TRELLIS2-native.glb'))
objects=set(bpy.data.objects)-before
meshes=[o for o in objects if o.type=='MESH'];assert meshes
# Preserve raw GLB. Normalize only the review object's world-space height/position.
points=np.concatenate([np.array([o.matrix_world@v.co for v in o.data.vertices]) for o in meshes])
lo,hi=points.min(0),points.max(0);scale=1.65/(hi[2]-lo[2]);center=(lo+hi)/2
for obj in objects:
    if obj.parent not in objects:
        obj.location.x-=center[0];obj.location.y-=center[1];obj.location.z-=lo[2]
        obj.location*=scale;obj.scale*=scale
new_ids=[o.name for o in meshes]
for obj in meshes:obj.hide_render=False;obj.hide_set(False)
# Center the old review at the same nominal floor without changing mesh data.
old_points=np.array([old.matrix_world@v.co for v in old.data.vertices]);old_center=(old_points.min(0)+old_points.max(0))/2
old.location.x-=old_center[0];old.location.y-=old_center[1];old.location.z-=old_points[:,2].min()
floor=bpy.data.objects.get('Studio floor (not part of animal)')
if floor:floor.hide_render=False;floor.hide_set(False);floor.location.z=-.004
info=dict(new=new_ids,previous=old.name,rig=rig.name,static_only=True,
    generated_triangles=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in meshes),
    normalized_height_m=1.65,raw_glb_unchanged=True)
s['trellis_review']=json.dumps(info)
s.render.engine='CYCLES';s.cycles.device='CPU';s.cycles.samples=16
s.render.threads_mode='FIXED';s.render.threads=3
s.render.resolution_x=850;s.render.resolution_y=650;s.render.resolution_percentage=100
target=Vector((0,0,.8));s.camera.data.type='ORTHO';s.camera.data.ortho_scale=2.65
checks=[]
for o in meshes:
    vv=np.array([v.co for v in o.data.vertices]);assert np.isfinite(vv).all()
    for mat in o.data.materials:
        assert mat and mat.use_nodes
        images=[n.image for n in mat.node_tree.nodes if n.type=='TEX_IMAGE' and n.image]
        assert images
        checks.append(dict(mesh=o.name,material=mat.name,images=[{'name':im.name,'size':list(im.size)} for im in images]))
for name,direction in [('side',(3,-.8,.25)),('front',(.5,-3,.25)),('back',(-3,.8,.25))]:
    s.camera.location=target+Vector(direction)
    s.camera.rotation_euler=(target-s.camera.location).to_track_quat('-Z','Y').to_euler()
    s.render.filepath=str(root/f'native-{name}.png');bpy.ops.render.render(write_still=True)
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            sp=area.spaces.active;sp.shading.type='MATERIAL';sp.shading.use_scene_world=False
            sp.overlay.show_overlays=False;sp.show_region_ui=True
            sp.region_3d.view_location=(0,0,.8);sp.region_3d.view_distance=3.4
            sp.region_3d.view_rotation=Vector((3,-.8,.25)).to_track_quat('Z','Y')
for im in bpy.data.images:
    if im.source=='FILE' and not im.packed_file:
        try:im.pack()
        except RuntimeError:pass
bpy.ops.wm.save_as_mainfile(filepath=str(out))
(root/'review-checks.json').write_text(json.dumps(dict(info=info,materials=checks,
    game_launched=False,rigging=False,animation=False,owner_art_accepted=False),indent=2))
print('TRELLIS2 material review ready.',flush=True)
