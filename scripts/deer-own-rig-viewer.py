"""Direct clip viewer on the unchanged deer skeleton and skin."""
import json
import bpy
from mathutils import Vector

scene=bpy.context.scene;info=json.loads(scene['own_rig_review'])
F=Vector((-.91514678,-.40312079,0));R=Vector((.40312079,-.91514678,0))
CLIPS=['Bind pose','Idle','Walk','Trot','Run','Eating','Sleep','lay_to_idle','Death',
    'Rear','Kick','Head_But','Turn_Left','Turn_Right','Graze','Drink','Alert',
    'Lie Down','Rest','Get Up','Jump','Turn Left','Turn Right','Hit Left','Hit Right']


def select(context):
    s=context.scene;p=s.deer_own_review;rig=bpy.data.objects[info['rig']]
    rig.animation_data_clear()
    for bone in rig.pose.bones:bone.matrix_basis.identity()
    if p.clip=='Bind pose':
        rig.data.pose_position='REST';s.frame_start=0;s.frame_end=1;s.frame_set(0)
        if context.screen and context.screen.is_animation_playing:bpy.ops.screen.animation_cancel(restore_frame=False)
    else:
        rig.data.pose_position='POSE';bank=info[p.bank]
        clip=p.clip if p.clip in bank else 'Idle'
        action_name=info['uncorrected_eating'] if clip=='Eating' and p.raw_eating and 'uncorrected_eating' in info else bank[clip]
        action=bpy.data.actions[action_name];rig.animation_data_create().action=action
        rig.animation_data.action_slot=action.slots[0];s.render.fps=info[p.bank+'_fps']
        s.frame_start=int(action.frame_range[0]);s.frame_end=int(action.frame_range[1])
        if clip in ('Idle','Walk','Trot','Run','Eating','Sleep','Graze','Rest'):
            s.frame_set(s.frame_start);first=[b.matrix.copy() for b in rig.pose.bones]
            s.frame_set(s.frame_end)
            if max(abs(a[i][j]-b.matrix[i][j]) for a,b in zip(first,rig.pose.bones)
                   for i in range(4) for j in range(4))<1e-4:s.frame_end-=1
        s.frame_set(s.frame_start)
    for area in context.screen.areas if context.screen else []:area.tag_redraw()


def changed(self,context):select(context)


class DOR_Properties(bpy.types.PropertyGroup):
    bank:bpy.props.EnumProperty(items=[('transferred','Mesh2Motion transfer',''),
        ('original','Previous deer clips','')],update=changed)
    clip:bpy.props.EnumProperty(items=[(n,n,'') for n in CLIPS],update=changed)
    raw_eating:bpy.props.BoolProperty(name='Before fix',description='Compare the uncorrected feeding pose',update=changed)


class DOR_OT_clip(bpy.types.Operator):
    bl_idname='deer_own.clip';bl_label='Show clip'
    clip:bpy.props.StringProperty()
    def execute(self,context):
        if self.clip=='Eating' and self.clip not in info[context.scene.deer_own_review.bank]:
            context.scene.deer_own_review.bank='transferred'
        context.scene.deer_own_review.clip=self.clip
        if self.clip!='Bind pose' and context.screen and not context.screen.is_animation_playing:bpy.ops.screen.animation_play()
        return {'FINISHED'}


class DOR_OT_play(bpy.types.Operator):
    bl_idname='deer_own.play';bl_label='Pause / Play'
    def execute(self,context):
        if context.scene.deer_own_review.clip!='Bind pose':bpy.ops.screen.animation_play()
        return {'FINISHED'}


class DOR_OT_skeleton(bpy.types.Operator):
    bl_idname='deer_own.skeleton';bl_label='Show / hide skeleton'
    def execute(self,context):
        rig=bpy.data.objects[info['rig']]
        showing=rig.hide_get() or rig.hide_viewport
        rig.show_in_front=True;rig.hide_viewport=False;rig.hide_set(not showing)
        rig.data.display_type='OCTAHEDRAL'
        if showing:
            for area in context.screen.areas:
                for space in area.spaces:
                    if space.type=='VIEW_3D':space.overlay.show_overlays=True;space.overlay.show_bones=True
        return {'FINISHED'}


class DOR_OT_view(bpy.types.Operator):
    bl_idname='deer_own.view';bl_label='View deer'
    face:bpy.props.BoolProperty(default=False)
    def execute(self,context):
        region=context.space_data.region_3d
        if self.face:
            rig=bpy.data.objects[info['rig']]
            region.view_location=rig.matrix_world@rig.pose.bones['Head'].head
            region.view_distance=.95;direction=F*2+R*1.2+Vector((0,0,.2))
        else:
            region.view_location=(0,0,.8);region.view_distance=3.5;direction=R*3.5+F*.4+Vector((0,0,.35))
        region.view_rotation=direction.to_track_quat('Z','Y');return {'FINISHED'}


def controls(layout,context):
    p=context.scene.deer_own_review;row=layout.row(align=True)
    for clip in ('Bind pose','Idle','Walk','Trot','Run','Eating'):
        row.operator('deer_own.clip',text=clip,depress=p.clip==clip).clip=clip
    row.operator('deer_own.play',text='Pause / Play')
    row.operator('deer_own.skeleton',text='Skeleton',depress=not bpy.data.objects[info['rig']].hide_get())
    if 'uncorrected_eating' in info and p.clip=='Eating':row.prop(p,'raw_eating',text='Before fix',toggle=True)


class DOR_PT_panel(bpy.types.Panel):
    bl_label='Original deer rig';bl_idname='DOR_PT_panel'
    bl_space_type='VIEW_3D';bl_region_type='UI';bl_category='Deer'
    def draw(self,context):
        layout=self.layout;p=context.scene.deer_own_review
        layout.prop(p,'bank',text='Motion');controls(layout,context)
        for clip in info[p.bank]:
            layout.operator('deer_own.clip',text=clip,depress=p.clip==clip).clip=clip
        row=layout.row(align=True)
        row.operator('deer_own.view',text='Face').face=True
        row.operator('deer_own.view',text='Whole deer').face=False
        layout.separator();layout.label(text='Fresh generated mesh / automatic weights' if scene.get('raw_audition') else 'Original model, skeleton and weights')
        layout.label(text='Existing rig and authored motion; no repair' if scene.get('raw_audition') else 'No replacement skin or live IK')
        layout.label(text='Transfer remains an unapproved trial')


def header(self,context):
    controls(self.layout,context)
    row=self.layout.row(align=True);row.prop(context.scene.deer_own_review,'bank',text='Motion')
    row.operator('deer_own.view',text='Face').face=True
    row.operator('deer_own.view',text='Whole deer').face=False


for cls in (DOR_Properties,DOR_OT_clip,DOR_OT_play,DOR_OT_skeleton,DOR_OT_view,DOR_PT_panel):bpy.utils.register_class(cls)
bpy.types.Scene.deer_own_review=bpy.props.PointerProperty(type=DOR_Properties)
bpy.types.VIEW3D_HT_header.prepend(header)
scene.deer_own_review.bank='transferred'
scene.deer_own_review.clip='Walk' if scene.get('raw_audition') else ('Eating' if scene.get('eating_correction',False) else 'Bind pose')
scene.sync_mode='FRAME_DROP';select(bpy.context)
if scene.get('eating_correction',False) and not bpy.app.background:
    def autoplay():
        for window in bpy.context.window_manager.windows:
            for area in window.screen.areas:
                if area.type=='VIEW_3D':
                    with bpy.context.temp_override(window=window,area=area):
                        if not window.screen.is_animation_playing:bpy.ops.screen.animation_play()
                    return None
        return 1
    bpy.app.timers.register(autoplay,first_interval=2)
print('Original-rig deer viewer ready.',flush=True)
