"""Explicit Blender sidebar for the locally generated deer study."""
import bpy
from mathutils import Vector


def available_clips(scene):
    custom=scene.get('review_clips','')
    return custom.split('|') if custom else [n for n in ('Idle','Walk','Run') if bpy.data.actions.get(n)]


class LOCALDEER_OT_view(bpy.types.Operator):
    bl_idname = 'local_deer.view'
    bl_label = 'Inspect generated deer'
    side: bpy.props.StringProperty(default='three-quarter')

    def execute(self, context):
        directions = {'three-quarter': (3,-6,2), 'side': (0,-6,1.2),
                      'other-side': (0,6,1.2), 'front': (-6,0,1.3)}
        region = context.space_data.region_3d
        region.view_location = Vector((0,0,.85))
        region.view_distance = 3.7
        region.view_rotation = Vector(directions[self.side]).to_track_quat('Z','Y')
        region.view_perspective = 'ORTHO'
        return {'FINISHED'}


class LOCALDEER_OT_clip(bpy.types.Operator):
    bl_idname = 'local_deer.clip'
    bl_label = 'Preview authored movement'
    clip: bpy.props.StringProperty()

    def execute(self, context):
        rig = next((o for o in context.scene.objects if o.type == 'ARMATURE'), None)
        action = bpy.data.actions.get(self.clip)
        if rig is None or action is None:
            return {'CANCELLED'}
        data=rig.animation_data_create()
        data.action=None
        for bone in rig.pose.bones:
            bone.matrix_basis.identity()
        data.action = action
        if action.slots:
            rig.animation_data.action_slot = action.slots[0]
        context.scene.frame_start = int(action.frame_range[0])
        context.scene.frame_end = int(action.frame_range[1])-(1 if context.scene.get('review_loop_closed',True) else 0)
        context.scene.frame_set(context.scene.frame_start)
        context.scene['review_clip'] = self.clip
        if context.screen and context.window and not context.screen.is_animation_playing:
            bpy.ops.screen.animation_play()
        return {'FINISHED'}


class LOCALDEER_OT_play(bpy.types.Operator):
    bl_idname = 'local_deer.play'
    bl_label = 'Play / Pause'
    def execute(self, context):
        bpy.ops.screen.animation_play()
        return {'FINISHED'}


class LOCALDEER_PT_review(bpy.types.Panel):
    bl_label = 'Generated Deer'
    bl_idname = 'LOCALDEER_PT_review'
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = 'Generated Deer'

    def draw(self, context):
        layout = self.layout
        layout.label(text='LOCAL TRIPOSG SHAPE STUDY', icon='MESH_DATA')
        layout.label(text='New mesh from our generated reference')
        layout.label(text='No downloaded deer model')
        layout.separator()
        for title, side in [('Three-quarter','three-quarter'),('Side','side'),
                            ('Opposite side','other-side'),('Front','front')]:
            layout.operator('local_deer.view',text=title).side = side
        layout.prop(context.space_data.shading, 'type', text='Viewport')
        layout.prop(context.space_data.overlay, 'show_overlays', text='Show rig/grid')
        clips = available_clips(context.scene)
        if clips:
            layout.separator()
            row = layout.row(align=True)
            for name in clips:
                row.operator('local_deer.clip', text=name,
                    depress=context.scene.get('review_clip') == name).clip = name
            layout.operator('local_deer.play', icon='PLAY')
            layout.prop(context.scene, 'frame_current', text='Frame')
            layout.label(text='AI motion: research only' if context.scene.get('motion_origin','').startswith('AnyTop') else 'Authored preview loops; no game controller')
        else:
            layout.label(text='Shape review; not animated yet')
        layout.separator()
        layout.label(text='Middle mouse: orbit; wheel: zoom')
        layout.label(text='Solid view keeps GPU use low')


def draw_header(self,context):
    clips=available_clips(context.scene)
    if not clips:
        return
    layout=self.layout
    layout.separator_spacer()
    layout.label(text='Deer (research):' if context.scene.get('motion_origin','').startswith('AnyTop') else 'Deer:')
    for i,name in enumerate(clips):
        title=f'AI take {i+1}' if name.startswith('AI motion') else name
        layout.operator('local_deer.clip',text=title,
            depress=context.scene.get('review_clip')==name).clip=name
    layout.operator('local_deer.play',text='Pause / Play',icon='PAUSE' if context.screen.is_animation_playing else 'PLAY')


def autoplay():
    scene=bpy.context.scene
    if not scene.get('review_autoplay',False):
        return None
    for window in bpy.context.window_manager.windows:
        for area in window.screen.areas:
            if area.type=='VIEW_3D':
                region=next((r for r in area.regions if r.type=='WINDOW'),None)
                if region:
                    with bpy.context.temp_override(window=window,area=area,region=region):
                        bpy.ops.local_deer.clip(clip=scene.get('review_clip',available_clips(scene)[0]))
                    return None
    return 1.0


for cls in (LOCALDEER_OT_view, LOCALDEER_OT_clip, LOCALDEER_OT_play, LOCALDEER_PT_review):
    bpy.utils.register_class(cls)
bpy.types.VIEW3D_HT_header.prepend(draw_header)
bpy.app.timers.register(autoplay,first_interval=2.0)
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type == 'VIEW_3D':
            area.spaces.active.show_region_ui = not bool(bpy.context.scene.get('review_clips',''))
            for region in area.regions:
                if region.type == 'UI':
                    try:
                        region.active_panel_category = 'Generated Deer'
                    except (AttributeError, TypeError):
                        pass
print('Generated Deer review controls ready.')
