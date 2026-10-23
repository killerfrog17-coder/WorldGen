"""CPU-only export of our deer for independent learned-motion evaluations."""
import argparse
import hashlib
import json
from pathlib import Path
import sys
import bpy
from mathutils import Vector


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--source',type=Path,required=True)
    parser.add_argument('--root',type=Path,required=True)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    source=args.source.resolve()
    root=args.root.resolve()
    target=root/'input'
    target.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(source))
    scene=bpy.context.scene
    rig=bpy.data.objects['AI Deer Rig']
    mesh=bpy.data.objects['Locally generated deer']
    rig.animation_data_clear()
    rig.parent=None
    rig.matrix_world.identity()
    rig.data.pose_position='POSE'
    for bone in rig.pose.bones:
        bone.matrix_basis.identity()
    bpy.context.view_layer.objects.active=rig
    rig.select_set(True)
    # The engine extraction node sits at ground height. These learned models
    # expect the anatomical pelvis as the root, not a weightless floor helper.
    bpy.ops.object.mode_set(mode='EDIT')
    helper=rig.data.edit_bones.get('MotionRoot')
    if helper:
        for child in list(helper.children):
            rest=child.matrix.copy()
            child.parent=None
            child.matrix=rest
        rig.data.edit_bones.remove(helper)
    bpy.ops.object.mode_set(mode='OBJECT')
    for obj in scene.objects:
        obj.select_set(False)
        if obj.type=='MESH':
            obj.hide_render=obj!=mesh
            obj.hide_set(obj!=mesh)
    rig.hide_set(False)
    rig.select_set(True)
    mesh.select_set(True)
    bpy.context.view_layer.objects.active=rig
    # Static input avoids feeding our existing imperfect motion to the model.
    glb=target/'WorldGenDeer.glb'
    bpy.ops.export_scene.gltf(filepath=str(glb),export_format='GLB',use_selection=True,
        export_animations=False,export_skins=True,export_def_bones=False,
        export_influence_nb=4,export_all_influences=False,export_yup=True,
        export_materials='EXPORT',export_cameras=False,export_lights=False)
    # An own, CC0-derived reference motion is also available to the video
    # pipeline's reference-feature preparation. No Truebones assets are used.
    fbx=target/'WorldGenDeer.fbx'
    bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'ARMATURE','MESH'},
        add_leaf_bones=False,bake_anim=False,mesh_smooth_type='FACE',path_mode='COPY',
        embed_textures=True)
    action=bpy.data.actions['Walk']
    rig.animation_data_create().action=action
    rig.animation_data.action_slot=action.slots[0]
    scene.frame_start=0
    scene.frame_end=28
    scene.render.fps=24
    bpy.ops.export_scene.fbx(filepath=str(target/'WorldGenDeer-Walk.fbx'),use_selection=True,
        object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=True,
        bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,
        bake_anim_simplify_factor=0,mesh_smooth_type='FACE',path_mode='COPY',embed_textures=True)
    rig.animation_data_clear()
    for b in rig.pose.bones:
        b.matrix_basis.identity()
    bpy.context.view_layer.update()
    scene.render.engine='CYCLES'
    scene.cycles.device='CPU'
    scene.cycles.samples=8
    scene.render.threads_mode='FIXED'
    scene.render.threads=3
    scene.render.resolution_x=640
    scene.render.resolution_y=480
    scene.render.resolution_percentage=100
    scene.render.film_transparent=True
    scene.camera.location=(2.2,-5.4,1.8)
    scene.camera.rotation_euler=(Vector((0,0,.8))-scene.camera.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(target/'deer-reference.png')
    bpy.ops.render.render(write_still=True)
    manifest={'source':str(source),'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),
        'input_glb_sha256':hashlib.sha256(glb.read_bytes()).hexdigest(),
        'reference':'Our mesh/rig/texture at neutral rest; existing CC0-derived Walk for reference only',
        'no_gpu_inference':True,'source_unchanged':True}
    (target/'provenance.json').write_text(json.dumps(manifest,indent=2))
    print('CPU deer inputs prepared.',flush=True)


if __name__=='__main__':
    main()
