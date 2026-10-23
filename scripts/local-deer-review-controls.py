"""Explicit Blender sidebar for the locally generated deer study."""
import bpy
from mathutils import Vector


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
        rig.animation_data_create().action = action
        if action.slots:
            rig.animation_data.action_slot = action.slots[0]
        context.scene.frame_start = int(action.frame_range[0])
        context.scene.frame_end = int(action.frame_range[1])-1
        context.scene.frame_set(context.scene.frame_start)
        context.scene['review_clip'] = self.clip
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
        clips = [name for name in ('Idle','Walk','Run') if bpy.data.actions.get(name)]
        if clips:
            layout.separator()
            row = layout.row(align=True)
            for name in clips:
                row.operator('local_deer.clip', text=name,
                    depress=context.scene.get('review_clip') == name).clip = name
            layout.operator('local_deer.play', icon='PLAY')
            layout.prop(context.scene, 'frame_current', text='Frame')
            layout.label(text='Authored preview loops; no game controller')
        else:
            layout.label(text='Shape review; not animated yet')
        layout.separator()
        layout.label(text='Middle mouse: orbit; wheel: zoom')
        layout.label(text='Solid view keeps GPU use low')


for cls in (LOCALDEER_OT_view, LOCALDEER_OT_clip, LOCALDEER_OT_play, LOCALDEER_PT_review):
    bpy.utils.register_class(cls)
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type == 'VIEW_3D':
            area.spaces.active.show_region_ui = True
            for region in area.regions:
                if region.type == 'UI':
                    try:
                        region.active_panel_category = 'Generated Deer'
                    except (AttributeError, TypeError):
                        pass
print('Generated Deer review controls ready.')
