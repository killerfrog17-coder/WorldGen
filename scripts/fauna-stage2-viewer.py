"""Authored-motion preview with phase-blended demonstrations and species traits."""
import json
from pathlib import Path
import runpy
import bpy
from mathutils import Vector

scene=bpy.context.scene;info=json.loads(scene['fauna_stage2'])
scene['fauna_stage1']=scene['fauna_stage2']
base=runpy.run_path(str(Path(__file__).with_name('fauna-stage1-viewer.py')))
rig=bpy.data.objects[info['rig']];eye_bind=Vector(info['head_target']);head_inverse=rig.data.bones['Head'].matrix_local.inverted()
state={'mode':0,'clip':'Behaviour showcase'}


def choose(label,play=True):
    action=bpy.data.actions[info['actions'][label]]
    rig.animation_data_clear();rig.data.pose_position='POSE'
    rig.animation_data_create().action=action;rig.animation_data.action_slot=action.slots[0]
    scene.render.fps=30;scene.frame_start=int(action.frame_range[0]);scene.frame_end=int(action.frame_range[1]);scene.frame_set(scene.frame_start)
    state['clip']=label
    if play and not bpy.app.background:
        for window in bpy.context.window_manager.windows:
            for area in window.screen.areas:
                if area.type=='VIEW_3D':
                    with bpy.context.temp_override(window=window,area=area):
                        if not window.screen.is_animation_playing:bpy.ops.screen.animation_play()
                    return


class F2_OT_mode(bpy.types.Operator):
    bl_idname='fauna_motion.mode';bl_label='4 - Showcase / Locomotion'
    def execute(self,context):
        state['mode']=1-state['mode'];choose('Locomotion demo' if state['mode'] else 'Behaviour showcase');return {'FINISHED'}


class F2_OT_clip(bpy.types.Operator):
    bl_idname='fauna_motion.clip';bl_label='Show animation'
    clip:bpy.props.StringProperty()
    def execute(self,context):choose(self.clip);return {'FINISHED'}


class F2_OT_play(bpy.types.Operator):
    bl_idname='fauna_motion.play';bl_label='Pause / Play'
    def execute(self,context):
        if not bpy.app.background:bpy.ops.screen.animation_play()
        return {'FINISHED'}


class F2_PT_motion(bpy.types.Panel):
    bl_idname='F2_PT_motion';bl_label='Animation preview'
    bl_space_type='VIEW_3D';bl_region_type='UI';bl_category='Animal Review'
    def draw(self,context):
        col=self.layout.column();col.label(text='Playing: '+state['clip'].replace('_',' '))
        col.operator('fauna_motion.mode');col.operator('fauna_motion.play')
        for label in info['actions']:
            if label.endswith('demo') or label=='Behaviour showcase':continue
            col.operator('fauna_motion.clip',text=('Get Up' if label=='lay_to_idle' else label.replace('_',' '))).clip=label
        col.label(text='Authored motion transfer - 30 fps')


def header(self,context):
    row=self.layout.row(align=True);row.operator('fauna_motion.mode',text='4: Showcase / Locomotion');row.operator('fauna_motion.play')


def follow_head(s,*args):
    target=rig.matrix_world@rig.pose.bones['Head'].matrix@head_inverse@eye_bind
    base['info']['head_target']=list(target)
    if base['state']['view']==1:
        for window in bpy.context.window_manager.windows:
            for area in window.screen.areas:
                if area.type=='VIEW_3D':area.spaces.active.region_3d.view_location=target


for cls in (F2_OT_mode,F2_OT_clip,F2_OT_play,F2_PT_motion):bpy.utils.register_class(cls)
bpy.types.VIEW3D_HT_header.prepend(header)
keyconfig=bpy.context.window_manager.keyconfigs.addon
if keyconfig:keyconfig.keymaps.new(name='3D View',space_type='VIEW_3D').keymap_items.new('fauna_motion.mode','FOUR','PRESS')
bpy.app.handlers.frame_change_post.append(follow_head)
scene.sync_mode='FRAME_DROP';choose('Behaviour showcase',play=False)
if not bpy.app.background:
    def autoplay():choose('Behaviour showcase');return None
    bpy.app.timers.register(autoplay,first_interval=1.5)
print('Fauna stage2 viewer ready.',flush=True)
