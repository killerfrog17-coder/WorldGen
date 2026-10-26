"""Shared staging and clip selection for the full-body animation audition."""
import json
import bpy
from mathutils import Vector


def rigs():
    return [o for o in bpy.data.objects if o.type=='ARMATURE' and 'preview_clips' in o]


def select_clip(label):
    scene=bpy.context.scene
    scene['preview_clip']=label
    for rig in rigs():
        clip=next(c for c in json.loads(rig['preview_clips']) if c['label']==label)
        rig.animation_data.action=bpy.data.actions[clip['action']]
        # Each generated action has its own slot; assign it explicitly after switching.
        rig.animation_data.action_slot=rig.animation_data.action.slots[0]
        for b in rig.pose.bones:b.location=(0,0,0);b.scale=(1,1,1)
        scene.frame_end=clip['frames']
    scene.frame_start=1;scene.frame_set(1)
    refresh()


def refresh():
    scene=bpy.context.scene;method=scene.get('demo_builder','MPFB');letter=scene.get('demo_variant','A')
    compare=scene.get('demo_compare',True);view=scene.get('demo_view','body');lineup=scene.get('demo_lineup',False)
    floor=bpy.data.objects.get('Neutral floor')
    if floor:
        floor.hide_render=scene.get('preview_clip')=='Swim';floor.hide_set(scene.get('preview_clip')=='Swim')
    for collection in bpy.data.collections:
        if not collection.name.startswith(('MPFB_','UMA_')):continue
        builder,code=collection.name.split('_')[:2]
        visible=(code==letter and (builder==method or compare)) or (lineup and builder==method)
        collection.hide_viewport=not visible;collection.hide_render=not visible
        offset=(ord(code)-ord('C')-.5)*1.2 if lineup else (-.6 if builder=='MPFB' else .6) if compare else 0
        for obj in collection.objects:
            if obj.parent is None:obj.location=(offset,0,0)
            if obj.type=='ARMATURE':obj.hide_set(not scene.get('preview_skeleton',False))
    if lineup:location=(0,-11.8,1.8);focus=(0,0,.95)
    elif view=='side':location=(4.4,-.8,1.5);focus=(0,0,.9)
    elif view=='front':location=(0,-5 if compare else -4,1.25);focus=(0,0,.9)
    else:location=(2.1,-5.2 if compare else -4.2,1.9);focus=(0,0,.9)
    if scene.get('preview_clip')=='Swim' and not lineup:
        focus=(0,0,.15)
    camera=scene.camera;camera.location=location
    camera.rotation_euler=(Vector(focus)-camera.location).to_track_quat('-Z','Y').to_euler()
    camera.data.lens=50
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                space=area.spaces.active;space.region_3d.view_perspective='CAMERA'
                space.region_3d.view_camera_zoom=0;space.show_region_ui=True
                space.overlay.show_overlays=scene.get('preview_skeleton',False)
                space.overlay.show_extras=False;space.overlay.show_floor=False
                space.shading.type='MATERIAL'
                space.shading.use_scene_world=True;space.shading.use_scene_lights=True
                area.tag_redraw()
