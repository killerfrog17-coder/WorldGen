"""Explicit controls for the isolated learned-motion comparison; no live IK."""
import json
import bpy
from mathutils import Vector

scene=bpy.context.scene
catalogue=json.loads(scene['motion_comparison_catalogue'])
families=list(dict.fromkeys(c['family'] for c in catalogue))


def select(context,family=None,variant=None,baseline=None):
    scene=context.scene;p=scene.motion_review
    family=family or p.family
    variant=variant or p.variant
    candidates=[c for c in catalogue if c['family']==family]
    if family=='Current CC0-derived':
        clip=next(c for c in candidates if c['action']==(baseline or p.baseline))
    else:clip=next(c for c in candidates if c['variant']==variant)
    rig=bpy.data.objects['AI Deer Rig']
    action=bpy.data.actions[clip['action']]
    rig.animation_data_create().action=action
    rig.animation_data.action_slot=action.slots[0]
    scene.render.fps=clip['fps'];scene.frame_start=0;scene.frame_end=clip['frames']-1
    scene.frame_set(0);scene['motion_review_active']=clip['action']
    scene['motion_review_status']=clip['status']
    for area in context.screen.areas if context.screen else []:
        if area.type=='VIEW_3D':area.tag_redraw()


def changed(self,context):select(context)


class MOTIONREVIEW_Properties(bpy.types.PropertyGroup):
    family:bpy.props.EnumProperty(items=[(f,f,'') for f in families],update=changed)
    variant:bpy.props.EnumProperty(items=[('raw','Raw AI',''),('clean','Contact cleanup','')],update=changed)
    baseline:bpy.props.EnumProperty(items=[(n,n,'') for n in ('Walk','Run','Graze','Alert')],update=changed)
    follow:bpy.props.BoolProperty(name='Keep deer in view',default=True)


class MOTIONREVIEW_OT_choose(bpy.types.Operator):
    bl_idname='motion_review.choose';bl_label='Compare motion'
    family:bpy.props.StringProperty(default='')
    variant:bpy.props.StringProperty(default='')
    baseline:bpy.props.StringProperty(default='')
    def execute(self,context):
        p=context.scene.motion_review
        if self.family:p.family=self.family
        if self.variant:p.variant=self.variant
        if self.baseline:p.baseline=self.baseline
        select(context)
        return {'FINISHED'}


class MOTIONREVIEW_OT_play(bpy.types.Operator):
    bl_idname='motion_review.play';bl_label='Pause / Play'
    def execute(self,context):
        bpy.ops.screen.animation_play();return {'FINISHED'}


class MOTIONREVIEW_OT_restart(bpy.types.Operator):
    bl_idname='motion_review.restart';bl_label='Replay from start'
    def execute(self,context):
        context.scene.frame_set(0)
        if not context.screen.is_animation_playing:bpy.ops.screen.animation_play()
        return {'FINISHED'}


class MOTIONREVIEW_OT_view(bpy.types.Operator):
    bl_idname='motion_review.view';bl_label='Inspect deer'
    side:bpy.props.BoolProperty(default=True)
    def execute(self,context):
        direction=Vector((.403,-.915,.2)) if self.side else Vector((-1,-1,.35))
        region=context.space_data.region_3d
        region.view_location=(0,0,.8);region.view_distance=4.2
        region.view_rotation=direction.to_track_quat('Z','Y')
        return {'FINISHED'}


class MOTIONREVIEW_PT_panel(bpy.types.Panel):
    bl_label='AI motion comparison';bl_idname='MOTIONREVIEW_PT_panel'
    bl_space_type='VIEW_3D';bl_region_type='UI';bl_category='Motion'
    def draw(self,context):
        layout=self.layout;p=context.scene.motion_review
        layout.prop(p,'family',text='')
        if p.family=='Current CC0-derived':layout.prop(p,'baseline',text='Clip')
        else:layout.prop(p,'variant',expand=True)
        row=layout.row(align=True)
        row.operator('motion_review.play',text='Pause / Play')
        row.operator('motion_review.restart',text='Replay')
        layout.label(text=context.scene.get('motion_review_status',''))
        layout.separator()
        layout.label(text='Current authored baseline:')
        row=layout.row(align=True)
        for name in ('Walk','Run','Graze','Alert'):
            op=row.operator('motion_review.choose',text=name)
            op.family='Current CC0-derived';op.baseline=name
        layout.separator()
        for family in families[1:]:
            layout.operator('motion_review.choose',text=family,depress=p.family==family).family=family
        layout.separator()
        row=layout.row(align=True)
        row.operator('motion_review.view',text='Side').side=True
        row.operator('motion_review.view',text='3/4').side=False
        layout.prop(p,'follow')
        layout.label(text='Same accepted mesh, skin and paint')
        layout.label(text='No live foot IK or procedural gaze')
        layout.label(text='Short takes replay with an end jump')
        layout.label(text='REVIEW is not a quality pass')
        layout.label(text='Private research; not game assets')


def header(self,context):
    p=context.scene.motion_review
    row=self.layout.row(align=True)
    row.prop(p,'family',text='')
    if p.family=='Current CC0-derived':row.prop(p,'baseline',text='')
    else:
        for variant,title in [('raw','Raw AI'),('clean','Cleaned')]:
            row.operator('motion_review.choose',text=title,depress=p.variant==variant).variant=variant
    row.operator('motion_review.play',text='Pause / Play')
    row.operator('motion_review.restart',text='Replay')


classes=(MOTIONREVIEW_Properties,MOTIONREVIEW_OT_choose,MOTIONREVIEW_OT_play,
    MOTIONREVIEW_OT_restart,MOTIONREVIEW_OT_view,MOTIONREVIEW_PT_panel)
for cls in classes:bpy.utils.register_class(cls)
bpy.types.Scene.motion_review=bpy.props.PointerProperty(type=MOTIONREVIEW_Properties)
bpy.types.VIEW3D_HT_header.prepend(header)
scene.sync_mode='FRAME_DROP'
scene.motion_review.family='UniMate walk'
scene.motion_review.variant='raw'

# Preserve rejection labels even if another family is selected while playing.
# They describe the take/cleanup, not the unchanged underlying mesh.


def follow_deer(scene,*args):
    if not scene.motion_review.follow:return
    head=bpy.data.objects['AI Deer Rig'].pose.bones['Pelvis'].head
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':area.spaces.active.region_3d.view_location=(head.x,head.y,.85)


bpy.app.handlers.frame_change_post.append(follow_deer)
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.show_region_ui=True
            for region in area.regions:
                if region.type=='UI':
                    try:region.active_panel_category='Motion'
                    except (AttributeError,TypeError):pass


def start():
    for window in bpy.context.window_manager.windows:
        for area in window.screen.areas:
            if area.type=='VIEW_3D':
                with bpy.context.temp_override(window=window,area=area):
                    if not window.screen.is_animation_playing:bpy.ops.screen.animation_play()
                return None
    return 1


if not bpy.app.background:bpy.app.timers.register(start,first_interval=2)
print('Deer motion comparison viewer ready.',flush=True)
