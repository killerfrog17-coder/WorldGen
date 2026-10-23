"""Simple native-clip viewer for the Mesh2Motion deer; no procedural gait."""
import json
import bpy
from mathutils import Vector

scene=bpy.context.scene
variants=json.loads(scene['mesh2motion_variants'])
CLIPS=['Idle','Walk','Trot','Run','Eating','Sleep','lay_to_idle','Death','Rear','Kick',
       'Head_But','Turn_Left','Turn_Right','Rest_Pose']


def select(context):
    scene=context.scene;p=scene.m2m_review;info=variants[p.mode]
    for mode,entry in variants.items():
        for name in entry['objects']:
            obj=bpy.data.objects[name];hidden=mode!=p.mode or name!=entry['mesh']
            obj.hide_set(hidden);obj.hide_render=hidden
    rig=bpy.data.objects[info['rig']];rig.animation_data_clear()
    rig.animation_data_create().action=bpy.data.actions[info['actions'][p.clip]]
    action=rig.animation_data.action;rig.animation_data.action_slot=action.slots[0]
    scene.render.fps=30
    scene.frame_start=int(action.frame_range[0]);scene.frame_end=int(action.frame_range[1])
    # The original cyclic clips have a duplicated closing pose. Avoid spending
    # two displayed frames at it; do not alter the source action keys.
    if p.clip in ('Idle','Walk','Trot','Run','Eating','Sleep'):
        scene.frame_set(scene.frame_start)
        first=[b.matrix.copy() for b in rig.pose.bones]
        scene.frame_set(scene.frame_end)
        error=max(abs(a[i][j]-b.matrix[i][j]) for a,b in zip(first,rig.pose.bones)
                  for i in range(4) for j in range(4))
        if error<1e-4:scene.frame_end-=1
    scene.frame_set(scene.frame_start)
    scene['mesh2motion_active']=p.mode+' / '+p.clip
    for area in context.screen.areas if context.screen else []:area.tag_redraw()


def changed(self,context):select(context)


class M2M_Properties(bpy.types.PropertyGroup):
    mode:bpy.props.EnumProperty(items=[('fitted','Fitted + weight cleanup',''),('template','Website template',''),
        ('limbs','Limb-adapted',''),('aligned','Full posture adapted','')],update=changed)
    clip:bpy.props.EnumProperty(items=[(n,n.replace('_',' '),'') for n in CLIPS],update=changed)


class M2M_OT_clip(bpy.types.Operator):
    bl_idname='m2m_review.clip';bl_label='Show animation'
    clip:bpy.props.StringProperty()
    def execute(self,context):
        context.scene.m2m_review.clip=self.clip
        return {'FINISHED'}


class M2M_OT_play(bpy.types.Operator):
    bl_idname='m2m_review.play';bl_label='Pause / Play'
    def execute(self,context):
        bpy.ops.screen.animation_play();return {'FINISHED'}


class M2M_OT_replay(bpy.types.Operator):
    bl_idname='m2m_review.replay';bl_label='Replay from start'
    def execute(self,context):
        context.scene.frame_set(context.scene.frame_start)
        if not context.screen.is_animation_playing:bpy.ops.screen.animation_play()
        return {'FINISHED'}


class M2M_OT_view(bpy.types.Operator):
    bl_idname='m2m_review.view';bl_label='Inspect deer'
    side:bpy.props.BoolProperty(default=True)
    def execute(self,context):
        direction=Vector((5,0,1)) if self.side else Vector((3.4,-4.6,1.1))
        region=context.space_data.region_3d;region.view_location=(-.2,0,.8)
        region.view_distance=3.6;region.view_rotation=direction.to_track_quat('Z','Y')
        return {'FINISHED'}


class M2M_PT_panel(bpy.types.Panel):
    bl_label='Mesh2Motion deer';bl_idname='M2M_PT_panel'
    bl_space_type='VIEW_3D';bl_region_type='UI';bl_category='Deer'
    def draw(self,context):
        layout=self.layout;p=context.scene.m2m_review
        layout.prop(p,'mode',text='Rig')
        row=layout.row(align=True);row.operator('m2m_review.play',text='Pause / Play')
        row.operator('m2m_review.replay',text='Replay')
        for names in [('Idle','Walk','Trot','Run'),('Eating','Sleep','lay_to_idle'),
                      ('Rear','Kick','Head_But'),('Turn_Left','Turn_Right'),('Death','Rest_Pose')]:
            row=layout.row(align=True)
            for n in names:row.operator('m2m_review.clip',text=n.replace('_',' '),depress=p.clip==n).clip=n
        row=layout.row(align=True);row.operator('m2m_review.view',text='Side').side=True
        row.operator('m2m_review.view',text='3/4').side=False
        layout.separator()
        layout.label(text='Our generated deer model and textures')
        layout.label(text='New Mesh2Motion rig and skin weights')
        layout.label(text='Original authored hoofed animations')
        layout.label(text='No live foot IK or generated motion')
        layout.label(text='Action buttons restart the chosen clip')


def header(self,context):
    p=context.scene.m2m_review;row=self.layout.row(align=True)
    for n in ('Idle','Walk','Trot','Run','Eating'):
        row.operator('m2m_review.clip',text='Graze' if n=='Eating' else n,depress=p.clip==n).clip=n
    row.operator('m2m_review.play',text='Pause / Play')
    row.operator('m2m_review.replay',text='Replay')
    row.prop(p,'clip',text='More')
    row.prop(p,'mode',text='Rig')


classes=(M2M_Properties,M2M_OT_clip,M2M_OT_play,M2M_OT_replay,M2M_OT_view,M2M_PT_panel)
for cls in classes:bpy.utils.register_class(cls)
bpy.types.Scene.m2m_review=bpy.props.PointerProperty(type=M2M_Properties)
bpy.types.VIEW3D_HT_header.prepend(header)
scene.m2m_review.mode=scene.get('mesh2motion_default','fitted')
scene.m2m_review.clip='Walk';scene.sync_mode='FRAME_DROP'


def autoplay():
    for window in bpy.context.window_manager.windows:
        for area in window.screen.areas:
            if area.type=='VIEW_3D':
                with bpy.context.temp_override(window=window,area=area):
                    if not window.screen.is_animation_playing:bpy.ops.screen.animation_play()
                return None
    return 1


if not bpy.app.background:bpy.app.timers.register(autoplay,first_interval=2)
print('Mesh2Motion deer viewer ready.',flush=True)
