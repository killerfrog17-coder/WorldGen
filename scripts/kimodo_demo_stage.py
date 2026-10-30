"""Camera/body/clip coordination for the isolated Kimodo Blender audition."""
import json
import math
import bpy
from mathutils import Matrix, Vector

LABELS = {'empty_walk': 'Walk — empty hands', 'spear_walk': 'Walk — spear', 'spear_throw': 'Throw spear'}


def rigs():
    return [o for o in bpy.data.objects if o.type == 'ARMATURE' and 'kimodo_clips' in o]


def selected():
    return next(o for o in rigs() if o['variant'] == bpy.context.scene['kimodo_variant'])


def set_action(obj, name):
    obj.animation_data_create()
    action = bpy.data.actions[name]
    obj.animation_data.action = action
    obj.animation_data.action_slot = action.slots[0]


def select_clip(name):
    scene = bpy.context.scene
    scene['kimodo_clip'] = name
    for rig in rigs():
        for bone in rig.pose.bones:
            bone.matrix_basis = Matrix.Identity(4)
        record = next(c for c in json.loads(rig['kimodo_clips']) if c['label'] == name)
        set_action(rig, record['action'])
        set_action(bpy.data.objects['Demo spear ' + rig['variant']], record['spear_action'])
        scene.frame_end = record['frames']
    scene.frame_start = 1
    scene.frame_set(1)
    refresh()
    update_camera()


def update_camera(scene=None):
    scene = scene or bpy.context.scene
    if 'kimodo_variant' not in scene:
        return
    rig = selected()
    bpy.context.view_layer.update() if not getattr(update_camera, 'in_handler', False) else None
    hip = rig.matrix_world @ rig.pose.bones['pelvis'].head
    rest_hip = rig.matrix_world @ rig.data.bones['pelvis'].head_local
    root = hip - rest_hip
    camera = scene.camera
    view = scene.get('kimodo_view', 'first')
    if view == 'first':
        eye = Vector(rig['demo_eye'])
        camera.location = (root.x, root.y + eye.y - .025, eye.z + root.z * .35)
        pitch = math.radians(scene.get('kimodo_pitch', -15.0))
        direction = Vector((0, -math.cos(pitch), math.sin(pitch)))
        camera.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
        camera.data.sensor_fit = 'HORIZONTAL'
        camera.data.sensor_width = 36
        camera.data.lens = 36 / (2 * math.tan(math.radians(94) / 2))
    else:
        focus = Vector((root.x, root.y, 1.0 + root.z))
        offsets = {'outside': Vector((2.5, -4.1, 1.0)), 'side': Vector((4.5, .1, .75)),
                   'front': Vector((0, -4.5, .8))}
        camera.location = focus + offsets.get(view, offsets['outside'])
        camera.rotation_euler = (focus - camera.location).to_track_quat('-Z', 'Y').to_euler()
        camera.data.lens = 44


def refresh():
    scene = bpy.context.scene
    variant = scene.get('kimodo_variant', 'B')
    first = scene.get('kimodo_view') == 'first'
    skeleton = bool(scene.get('kimodo_skeleton', False))
    for rig in rigs():
        letter = rig['variant']
        full = bpy.data.collections['MPFB_' + letter]
        local = bpy.data.collections['First person ' + letter]
        full.hide_viewport = letter != variant or first
        full.hide_render = letter != variant or first
        local.hide_viewport = letter != variant or not first
        local.hide_render = letter != variant or not first
        rig.hide_set(letter != variant or not skeleton)
        rig.show_in_front = True
        spear = bpy.data.objects['Demo spear ' + letter]
        hidden = letter != variant or scene.get('kimodo_clip') == 'empty_walk'
        spear.hide_set(hidden)
        spear.hide_render = hidden
        for ob in spear.children:
            ob.hide_set(hidden)
            ob.hide_render = hidden
    update_camera()
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == 'VIEW_3D':
                space = area.spaces.active
                space.region_3d.view_perspective = 'CAMERA'
                space.overlay.show_overlays = skeleton
                space.overlay.show_extras = False
                space.overlay.show_floor = False
                area.tag_redraw()


def on_frame(scene, depsgraph=None):
    update_camera.in_handler = True
    try:
        update_camera(scene)
    finally:
        update_camera.in_handler = False


def register_handler():
    for old in list(bpy.app.handlers.frame_change_post):
        if getattr(old, '__module__', '') == __name__:
            bpy.app.handlers.frame_change_post.remove(old)
    bpy.app.handlers.frame_change_post.append(on_frame)
