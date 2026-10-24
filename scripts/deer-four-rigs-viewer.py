"""One-window comparison of four fresh meshes with independent generated rigs."""
import json,math,random,time
from pathlib import Path
import bpy
from mathutils import Vector

s=bpy.context.scene;models=json.loads(s['deer_four_rigs'])
state={'model':0,'clip':'Showcase','mode':0,'view':0,'skeleton':False,'coat':'Original fur','started':time.monotonic()}
def current():return models[state['model']]
def rig():return bpy.data.objects[current()['rig']]
def regions():
    for window in bpy.context.window_manager.windows:
        for area in window.screen.areas:
            if area.type=='VIEW_3D':yield window,area,area.spaces.active
def playback():
    for window,area,space in regions():
        with bpy.context.temp_override(window=window,area=area):
            if not window.screen.is_animation_playing:bpy.ops.screen.animation_play()
        return
def visibility():
    for index,entry in enumerate(models):
        body=bpy.data.objects[entry['body']];r=bpy.data.objects[entry['rig']]
        body.hide_set(index!=state['model']);body.hide_render=index!=state['model']
        r.hide_viewport=False;r.hide_set(index!=state['model'] or not state['skeleton']);r.show_in_front=True
        r.data.display_type='OCTAHEDRAL'
    for window,area,space in regions():
        space.overlay.show_overlays=state['skeleton'];space.overlay.show_bones=True
        space.overlay.show_floor=False;space.overlay.show_axis_x=False;space.overlay.show_axis_y=False
def choose(label,play=True,phase=0):
    r=rig();r.animation_data_clear();state['clip']=label
    for bone in r.pose.bones:bone.matrix_basis.identity()
    if label=='Bind':
        r.data.pose_position='REST';s.frame_start=0;s.frame_end=1;s.frame_set(0)
        if not bpy.app.background:
            for window,area,space in regions():
                with bpy.context.temp_override(window=window,area=area):
                    if window.screen.is_animation_playing:bpy.ops.screen.animation_cancel(restore_frame=False)
                break
    else:
        r.data.pose_position='POSE';entry=current();name=entry['demos'][label] if label in entry['demos'] else entry['actions'][label]
        action=bpy.data.actions[name];r.animation_data_create().action=action;r.animation_data.action_slot=action.slots[0]
        s.frame_start=int(action.frame_range[0]);s.frame_end=int(action.frame_range[1]);s.frame_set(s.frame_start+round(phase*(s.frame_end-s.frame_start)))
        if play and not bpy.app.background:playback()
    visibility()
def set_view():
    target=rig().matrix_world@rig().pose.bones['Head'].head if state['view']==1 else Vector((0,0,.8))
    direction=Vector((0,1,.05)) if state['view']==1 else Vector((1,0,.08))
    for window,area,space in regions():
        view=space.region_3d;view.view_perspective='PERSP';view.view_location=target
        view.view_distance=.9 if state['view']==1 else 3.1;view.view_rotation=direction.to_track_quat('Z','Y')
class D4_OT_model(bpy.types.Operator):
    bl_idname='deer_four.model';bl_label='1 - Next model'
    number:bpy.props.IntProperty(default=-1)
    def execute(self,context):
        phase=(s.frame_current-s.frame_start)/max(1,s.frame_end-s.frame_start)
        playing=context.screen.is_animation_playing if context.screen else False
        state['model']=(state['model']+1)%len(models) if self.number<0 else self.number
        choose(state['clip'],play=playing,phase=phase);set_view();return {'FINISHED'}
class D4_OT_species(bpy.types.Operator):
    bl_idname='deer_four.species';bl_label='2 - Randomize species colour'
    def execute(self,context):
        label,saturation,value=random.choice([('Woodland',1,1),('Pale',.45,1.4),('Dark',.8,.65),('Grey',.15,.95),('Warm',1.2,1.1)])
        for entry in models:
            hsv=bpy.data.materials[entry['material']].node_tree.nodes['Review coat variation']
            hsv.inputs['Saturation'].default_value=saturation;hsv.inputs['Value'].default_value=value
        state['coat']=label;return {'FINISHED'}
class D4_OT_view(bpy.types.Operator):
    bl_idname='deer_four.view';bl_label='3 - Side / Face / Orbit'
    def execute(self,context):state['view']=(state['view']+1)%3;state['started']=time.monotonic();set_view();return {'FINISHED'}
class D4_OT_mode(bpy.types.Operator):
    bl_idname='deer_four.mode';bl_label='4 - Showcase / Locomotion'
    def execute(self,context):state['mode']=1-state['mode'];choose('Locomotion' if state['mode'] else 'Showcase');return {'FINISHED'}
class D4_OT_skeleton(bpy.types.Operator):
    bl_idname='deer_four.skeleton';bl_label='Show / Hide Skeleton'
    def execute(self,context):state['skeleton']=not state['skeleton'];visibility();return {'FINISHED'}
class D4_OT_play(bpy.types.Operator):
    bl_idname='deer_four.play';bl_label='Pause / Play'
    def execute(self,context):
        if state['clip']!='Bind':bpy.ops.screen.animation_play()
        return {'FINISHED'}
class D4_OT_clip(bpy.types.Operator):
    bl_idname='deer_four.clip';bl_label='Preview animation'
    clip:bpy.props.StringProperty()
    def execute(self,context):choose(self.clip);return {'FINISHED'}
class D4_OT_free(bpy.types.Operator):
    bl_idname='deer_four.free';bl_label='Stop automatic orbit'
    def invoke(self,context,event):state['view']=-1;return {'PASS_THROUGH'}
def controls(layout):
    row=layout.row(align=True);row.operator('deer_four.model',text=f"1: Model {state['model']+1}/4")
    row.operator('deer_four.species',text='2: Species');row.operator('deer_four.view',text='3: View');row.operator('deer_four.mode',text='4: Loop')
    row.operator('deer_four.skeleton',text='Hide Skeleton' if state['skeleton'] else 'Show Skeleton')
    row.operator('deer_four.play',text='Pause / Play')
def header(self,context):controls(self.layout)
class D4_PT_panel(bpy.types.Panel):
    bl_idname='D4_PT_panel';bl_label='Four deer - independent generated rigs'
    bl_space_type='VIEW_3D';bl_region_type='UI';bl_category='Animal Review'
    def draw(self,context):
        col=self.layout.column();entry=current();col.label(text=f"MODEL {state['model']+1} / 4 - {entry['joints']} generated joints")
        row=col.row(align=True)
        for index in range(4):row.operator('deer_four.model',text=str(index+1),depress=index==state['model']).number=index
        for op in ('deer_four.model','deer_four.species','deer_four.view','deer_four.mode'):col.operator(op)
        col.operator('deer_four.skeleton',text='Hide Skeleton' if state['skeleton'] else 'Show Skeleton');col.operator('deer_four.play')
        col.label(text='Playing: '+state['clip'].replace('_',' '));col.label(text='Coat: '+state['coat']+' (colour only)')
        col.separator();col.label(text='Individual animations')
        for label in ('Bind','Idle','Walk','Trot','Run','Eating','Sleep','lay_to_idle'):
            col.operator('deer_four.clip',text='Get Up' if label=='lay_to_idle' else label).clip=label
        col.label(text='Fresh skeleton + network skin per model')
        col.label(text='Raw automatic transfer; no manual repairs')
def tick():
    for window,area,space in regions():
        if state['view']==2:
            angle=(time.monotonic()-state['started'])*.22
            space.region_3d.view_rotation=Vector((math.cos(angle),math.sin(angle),.08)).to_track_quat('Z','Y')
        elif state['view']==1:space.region_3d.view_location=rig().matrix_world@rig().pose.bones['Head'].head
        area.tag_redraw()
    return .05
for cls in (D4_OT_model,D4_OT_species,D4_OT_view,D4_OT_mode,D4_OT_skeleton,D4_OT_play,D4_OT_clip,D4_OT_free,D4_PT_panel):bpy.utils.register_class(cls)
bpy.types.VIEW3D_HT_header.prepend(header)
config=bpy.context.window_manager.keyconfigs.addon
if config:
    km=config.keymaps.new(name='3D View',space_type='VIEW_3D')
    for key,op in [('ONE','deer_four.model'),('TWO','deer_four.species'),('THREE','deer_four.view'),('FOUR','deer_four.mode'),('MIDDLEMOUSE','deer_four.free')]:km.keymap_items.new(op,key,'PRESS')
choose('Showcase',play=False);set_view()
if not bpy.app.background:
    bpy.ops.deer_four.skeleton();assert not rig().hide_get();bpy.ops.deer_four.skeleton();assert rig().hide_get()
    bpy.app.timers.register(tick,first_interval=.1);bpy.app.timers.register(lambda:(playback(),None)[1],first_interval=1.5)
print('Four generated deer rigs viewer ready.',flush=True)
