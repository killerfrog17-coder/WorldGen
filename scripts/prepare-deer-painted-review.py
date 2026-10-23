"""Review actual generated UV texture on the existing AI deer in Blender."""
import argparse
from pathlib import Path
import sys
import bpy
from mathutils import Vector


def main():
    ap=argparse.ArgumentParser()
    ap.add_argument('--studio',type=Path,required=True)
    ap.add_argument('--mesh',type=Path,required=True)
    ap.add_argument('--output',type=Path,required=True)
    ap.add_argument('--render',action='store_true')
    args=ap.parse_args(sys.argv[sys.argv.index('--')+1:])
    args.studio,args.mesh,args.output=[p.resolve() for p in (args.studio,args.mesh,args.output)]
    if args.output.exists():
        raise FileExistsError(args.output)
    bpy.ops.wm.open_mainfile(filepath=str(args.studio))
    previous=bpy.data.objects.get('Locally generated deer')
    if previous:
        bpy.data.objects.remove(previous,do_unlink=True)
    before=set(bpy.context.scene.objects)
    bpy.ops.import_scene.gltf(filepath=str(args.mesh))
    mesh=next(o for o in bpy.context.scene.objects if o not in before and o.type=='MESH')
    bpy.ops.object.select_all(action='DESELECT')
    mesh.select_set(True)
    bpy.context.view_layer.objects.active=mesh
    mesh.parent=None
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    low=Vector([min(v.co[i] for v in mesh.data.vertices) for i in range(3)])
    high=Vector([max(v.co[i] for v in mesh.data.vertices) for i in range(3)])
    scale=1.65/(high.z-low.z)
    for v in mesh.data.vertices:
        v.co=(v.co-Vector(((low.x+high.x)/2,(low.y+high.y)/2,low.z)))*scale
    mesh.name='Locally generated deer'
    for face in mesh.data.polygons:
        face.use_smooth=True
    scene=bpy.context.scene
    scene['review_status']='Local AI multiview-painted UV texture; animation work in progress'
    scene['texture_origin']='Hunyuan3D-2 local multiview diffusion, six independently generated views, visibility-weighted UV bake'
    for image in bpy.data.images:
        if image.source=='FILE' and not image.packed_file:
            image.pack()
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                area.spaces.active.shading.type='MATERIAL'
                area.spaces.active.show_region_ui=False
    bpy.ops.wm.save_as_mainfile(filepath=str(args.output))
    if args.render:
        scene.cycles.samples=16
        scene.render.resolution_percentage=80
        for name,position in [('painted-front',(2.7,-5,2.1)),('painted-back',(2.7,5,2.1))]:
            scene.camera.location=position
            scene.camera.rotation_euler=(Vector((0,0,.8))-scene.camera.location).to_track_quat('-Z','Y').to_euler()
            scene.render.filepath=str(args.output.parent/(name+'.png'))
            bpy.ops.render.render(write_still=True)


if __name__=='__main__':
    main()
