"""Export the accepted deer in Mesh2Motion's Y-up/+Z-forward convention.

Native Blender, CPU only. Shape, UVs and image pixels are retained; the previous
armature is removed from this export only. Its landmarks assist template fitting.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import sys
import bpy
import numpy as np
from mathutils import Matrix,Vector


def main():
    ap=argparse.ArgumentParser();ap.add_argument('--source',type=Path,required=True)
    ap.add_argument('--root',type=Path,required=True)
    a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);source=a.source.resolve();root=a.root.resolve()
    root.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(source))
    mesh=bpy.data.objects['Locally generated deer'];rig=bpy.data.objects['AI Deer Rig']
    rig.animation_data_clear();rig.parent=None;rig.matrix_world.identity()
    for bone in rig.pose.bones:bone.matrix_basis.identity()
    bpy.context.view_layer.update()
    forward=Vector((-.91514678,-.40312079,0)).normalized()
    yaw=-math.pi/2-math.atan2(forward.y,forward.x)
    rotation=Matrix.Rotation(yaw,4,'Z')
    def point(v):
        p=rotation@v
        return [float(p.x),float(p.z),float(-p.y)]
    joints={b.name:{'head':point(rig.matrix_world@b.head_local),
        'tail':point(rig.matrix_world@b.tail_local),'parent':b.parent.name if b.parent else None}
        for b in rig.data.bones}
    vertices=np.array([point(mesh.matrix_world@v.co) for v in mesh.data.vertices])
    world=mesh.matrix_world.copy();mesh.parent=None;mesh.matrix_world=rotation@world
    for mod in list(mesh.modifiers):
        if mod.type=='ARMATURE':mesh.modifiers.remove(mod)
    for obj in bpy.context.scene.objects:obj.select_set(False)
    mesh.hide_set(False);mesh.select_set(True);bpy.context.view_layer.objects.active=mesh
    out=root/'Deer-Unrigged-Forward.glb'
    bpy.ops.export_scene.gltf(filepath=str(out),export_format='GLB',use_selection=True,
        export_animations=False,export_skins=False,export_yup=True,export_materials='EXPORT')
    (root/'input-landmarks.json').write_text(json.dumps({'joints':joints,
        'bounds':{'min':vertices.min(0).tolist(),'max':vertices.max(0).tolist()},
        'source':str(source),'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),
        'glb_sha256':hashlib.sha256(out.read_bytes()).hexdigest(),
        'forward':'+Z','up':'+Y','unit':'metres','rotation_degrees':math.degrees(yaw),
        'purpose':'Existing anatomical markers only; previous rig/weights/animations not exported'},indent=2))
    print('Mesh2Motion unrigged input ready:',out,flush=True)


if __name__=='__main__':main()
