"""Blender-only preparation of CC0 cliff geometry; no source textures imported.

Run with --background --disable-autoexec --python this.py -- --inspect first.
"""
from pathlib import Path
import sys, json, math
import bpy, bmesh
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'assets-source/terrain/rocks/polyhaven'
REVIEW = ROOT / 'artifacts/terrain-surfaces-20261107/source-models'
REVIEW.mkdir(parents=True, exist_ok=True)
entries = json.loads((SOURCE/'sources.json').read_text())['assets']
inspection = []
for entry in entries:
    bpy.ops.wm.open_mainfile(filepath=str(ROOT/entry['file']), load_ui=False, use_scripts=False)
    objects = [o for o in bpy.data.objects if o.type == 'MESH']
    info = []
    for obj in objects:
        points = [obj.matrix_world @ v.co for v in obj.data.vertices]
        if not points: continue
        bm=bmesh.new();bm.from_mesh(obj.data)
        info.append({'name':obj.name, 'vertices':len(points), 'triangles':sum(len(p.vertices)-2 for p in obj.data.polygons),
            'bounds': [[min(p[i] for p in points), max(p[i] for p in points)] for i in range(3)],
            'boundary_edges':sum(e.is_boundary for e in bm.edges),
            'normal_mean':list(sum((f.normal*f.calc_area() for f in bm.faces),Vector())/max(.00001,sum(f.calc_area() for f in bm.faces)))})
        bm.free()
    inspection.append({'asset':entry['name'],'objects':info})
    print(json.dumps(inspection[-1]),flush=True)
    obj=max(objects,key=lambda o:len(o.data.polygons))
    for other in objects: other.hide_render=other!=obj
    obj.data.materials.clear()
    points=[obj.matrix_world @ Vector(c) for c in obj.bound_box]
    center=sum(points,Vector())/8;size=max(max(p[i] for p in points)-min(p[i] for p in points) for i in range(3))
    scene=bpy.context.scene
    scene.render.engine='BLENDER_WORKBENCH';scene.render.resolution_x=900;scene.render.resolution_y=720;scene.render.resolution_percentage=100
    scene.display.shading.light='STUDIO';scene.display.shading.color_type='SINGLE';scene.display.shading.single_color=(.5,.5,.5)
    scene.display.shading.show_shadows=True;scene.display.shading.show_cavity=True
    for side in [-1,1]:
        cam_data=bpy.data.cameras.new('ReviewCamera');cam=bpy.data.objects.new('ReviewCamera',cam_data);scene.collection.objects.link(cam)
        cam.location=center+Vector((.6,side*2.6,1))*size;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler()
        cam_data.type='ORTHO';cam_data.ortho_scale=size*1.4;scene.camera=cam
        scene.render.filepath=str(REVIEW/(entry['name']+f'-{side}.png'));bpy.ops.render.render(write_still=True)
(REVIEW/'inspection.json').write_text(json.dumps(inspection,indent=2))
