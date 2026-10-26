"""Raw selected head from body-axis cameras for explicit landmark inspection."""
import argparse,hashlib,json,sys
from pathlib import Path
import bpy,numpy as np
from mathutils import Vector
repo=Path(__file__).resolve().parent.parent
ap=argparse.ArgumentParser();ap.add_argument('--slug',required=True);ap.add_argument('--root',type=Path,required=True);ap.add_argument('--anatomical-axis',action='store_true');a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=a.root.resolve();meta=json.loads((root/'visual-source.json').read_text());source=repo/meta['source'];sha=hashlib.sha256(source.read_bytes()).hexdigest();bpy.ops.wm.open_mainfile(filepath=str(source));s=bpy.context.scene;e=json.loads(s['deer_four_rigs'])[0];body=bpy.data.objects[e['body']];rig=bpy.data.objects[e['rig']];rig.animation_data_clear();rig.data.pose_position='REST'
for ob in s.objects:
 if ob.type=='MESH':ob.hide_render=ob!=body
land=json.loads((root/'face-landmarks.json').read_text());mid=(np.array(land['right_eye']['point'])+land['left_eye']['point'])/2;guide=json.loads((root/'eye-audit/front.json').read_text());scale=guide['scale']*(3.0 if a.slug=='lynx' else 1.8);target=Vector(mid)+Vector((0,0,.012));dest=root/('face-frame-lab' if a.anatomical_axis else 'landmark-lab');dest.mkdir(exist_ok=True);camera=bpy.data.objects.new('Raw selected head laboratory',bpy.data.cameras.new('Raw selected head laboratory'));s.collection.objects.link(camera);camera.data.type='ORTHO';camera.data.ortho_scale=scale;s.camera=camera
s.render.engine='BLENDER_WORKBENCH';s.display.shading.light='STUDIO';s.display.shading.color_type='SINGLE';s.display.shading.single_color=(.46,.44,.40);s.display.shading.show_shadows=True;s.display.shading.show_cavity=True;s.display.shading.cavity_type='BOTH';s.render.resolution_x=s.render.resolution_y=768;s.render.resolution_percentage=100;s.render.image_settings.file_format='PNG'
directions=[('front',(0,1,.0125)),('right',(1,0,.0125)),('left',(-1,0,.0125))]
if a.anatomical_axis:directions=[('front',np.array(land['front_axis'])+[0,0,.0125]),('right',np.array(land['side_axis'])+[0,0,.0125]),('left',-np.array(land['side_axis'])+[0,0,.0125])]
for view,direction in directions:
 camera.location=target+Vector(direction).normalized()*3;camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler();s.render.filepath=str(dest/f'{view}-raw.png');bpy.ops.render.render(write_still=True);(dest/f'{view}.json').write_text(json.dumps({'camera_matrix':[list(row) for row in camera.matrix_world],'scale':scale,'size':[768,768],'source_sha256':sha,'scope':'Body-axis raw geometry diagnostic, not an assertion of exact anatomical front. Choose actual visible socket/nasal surface points.'},indent=2)+'\n')
assert hashlib.sha256(source.read_bytes()).hexdigest()==sha
