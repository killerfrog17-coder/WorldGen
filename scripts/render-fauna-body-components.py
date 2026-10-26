"""Read-only highlighted source component diagnostic, never a native save."""
import argparse,hashlib,json,sys
from pathlib import Path
import bpy,numpy as np
from mathutils import Matrix
repo=Path(__file__).resolve().parent.parent;sys.path.insert(0,str(repo/'scripts'));from fauna_surface_components import components
ap=argparse.ArgumentParser();ap.add_argument('--slug',required=True);ap.add_argument('--root',type=Path,required=True);ap.add_argument('--index',type=int,required=True);a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=a.root.resolve();meta=json.loads((root/'visual-source.json').read_text());source=repo/meta['source'];sha=hashlib.sha256(source.read_bytes()).hexdigest();bpy.ops.wm.open_mainfile(filepath=str(source));scene=bpy.context.scene;e=json.loads(scene['deer_four_rigs'])[0];body=bpy.data.objects[e['body']];rig=bpy.data.objects[e['rig']];rig.animation_data_clear();rig.data.pose_position='REST'
p=np.array([v.co[:] for v in body.data.vertices]);body.data.calc_loop_triangles();faces=np.array([f.vertices[:] for f in body.data.loop_triangles]);parts=sorted(components(p,faces),key=len,reverse=True);part=parts[a.index];selected=[f.tolist() for f in faces if all(int(i) in part for i in f)];other=[f.tolist() for f in faces if not all(int(i) in part for i in f)]
for ob in scene.objects:
 if ob.type=='MESH':ob.hide_render=True
for name,polys,color in [('Source body context',other,(.45,.45,.45,1)),('Highlighted disconnected component',selected,(.9,.06,.025,1))]:
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(p.tolist(),[],polys);mesh.update();ob=bpy.data.objects.new(name,mesh);scene.collection.objects.link(ob);ob.matrix_world=body.matrix_world.copy();ob.color=color
 for face in mesh.polygons:face.use_smooth=True
camera=bpy.data.objects.new('Component diagnostic camera',bpy.data.cameras.new('Component diagnostic camera'));scene.collection.objects.link(camera);camera.data.type='ORTHO';scene.camera=camera;scene.render.engine='BLENDER_WORKBENCH';scene.display.shading.color_type='OBJECT';scene.display.shading.light='STUDIO';scene.display.shading.show_cavity=True;scene.render.resolution_x=scene.render.resolution_y=512;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';dest=root/f'component-{a.index}-diagnostic';dest.mkdir(exist_ok=True)
for view in ['front','right','left']:
 g=json.loads((root/'eye-audit'/f'{view}.json').read_text());camera.matrix_world=Matrix(g['camera_matrix']);camera.data.ortho_scale=g['scale']*1.5;scene.render.filepath=str(dest/f'{view}.png');bpy.ops.render.render(write_still=True)
(dest/'component.json').write_text(json.dumps({'source_sha256':sha,'original_vertex_indices':sorted(part),'original_polygon_indices':[i for i,f in enumerate(body.data.polygons) if all(int(v) in part for v in f.vertices)],'vertices':len(part),'scope':'Read-only diagnostic; highlighted disconnected selected-body component, no deletion or appearance judgment.'},indent=2)+'\n');assert hashlib.sha256(source.read_bytes()).hexdigest()==sha
