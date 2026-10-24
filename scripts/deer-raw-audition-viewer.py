"""Raw deer audition with the owner's numbered review controls."""
import json,math,random,runpy,time
from pathlib import Path
import bpy
from mathutils import Vector

base=runpy.run_path(str(Path(__file__).with_name('deer-own-rig-viewer.py')))
bpy.types.VIEW3D_HT_header.remove(base['header']);bpy.utils.unregister_class(base['DOR_PT_panel'])
s=bpy.context.scene;info=json.loads(s['own_rig_review']);demos=json.loads(s['raw_review_demos'])
rig=bpy.data.objects[info['rig']];body=bpy.data.objects[info['mesh']]
F=Vector((-.91514678,-.40312079,0));R=Vector((.40312079,-.91514678,0))
state={'view':0,'mode':0,'coat':'Original fur','started':time.monotonic()}
mat=body.data.materials[0];nodes=mat.node_tree.nodes;links=mat.node_tree.links
bsdf=nodes.get('Principled BSDF');source=bsdf.inputs['Base Color'].links[0].from_socket
hsv=nodes.new('ShaderNodeHueSaturation');links.new(source,hsv.inputs['Color']);links.new(hsv.outputs['Color'],bsdf.inputs['Base Color'])

def choose_demo():
    action=bpy.data.actions[demos['Locomotion' if state['mode'] else 'Showcase']]
    rig.animation_data_clear();rig.animation_data_create().action=action;rig.animation_data.action_slot=action.slots[0]
    rig.data.pose_position='POSE';s.frame_start=int(action.frame_range[0]);s.frame_end=int(action.frame_range[1]);s.frame_set(s.frame_start)
    for window in bpy.context.window_manager.windows:
        for area in window.screen.areas:
            if area.type=='VIEW_3D':
                with bpy.context.temp_override(window=window,area=area):
                    if not window.screen.is_animation_playing:bpy.ops.screen.animation_play()
                return

class RAW_OT_bank(bpy.types.Operator):
    bl_idname='raw_deer.bank';bl_label='1 - Animation set 1 / 1'
    def execute(self,context):self.report({'INFO'},'One existing authored motion bank');return {'FINISHED'}
class RAW_OT_species(bpy.types.Operator):
    bl_idname='raw_deer.species';bl_label='2 - Randomize species / coat'
    def execute(self,context):
        label,saturation,value=random.choice([('Woodland',1,1),('Pale',.45,1.4),('Dark',.8,.65),('Grey',.15,.95),('Warm',1.2,1.1)])
        hsv.inputs['Saturation'].default_value=saturation;hsv.inputs['Value'].default_value=value;state['coat']=label
        return {'FINISHED'}
def set_view():
    target=rig.matrix_world@rig.pose.bones['Head'].head if state['view']==1 else Vector((0,0,.8))
    direction=F+Vector((0,0,.05)) if state['view']==1 else R+Vector((0,0,.08))
    for window in bpy.context.window_manager.windows:
        for area in window.screen.areas:
            if area.type=='VIEW_3D':
                region=area.spaces.active.region_3d;region.view_perspective='PERSP';region.view_location=target
                region.view_distance=.9 if state['view']==1 else 3.2;region.view_rotation=direction.to_track_quat('Z','Y')
class RAW_OT_view(bpy.types.Operator):
    bl_idname='raw_deer.view';bl_label='3 - Side / Face / Orbit'
    def execute(self,context):state['view']=(state['view']+1)%3;state['started']=time.monotonic();set_view();return {'FINISHED'}
class RAW_OT_mode(bpy.types.Operator):
    bl_idname='raw_deer.mode';bl_label='4 - Showcase / Locomotion'
    def execute(self,context):state['mode']=1-state['mode'];choose_demo();return {'FINISHED'}
class RAW_OT_free(bpy.types.Operator):
    bl_idname='raw_deer.free';bl_label='Stop automatic orbit'
    def invoke(self,context,event):state['view']=-1;return {'PASS_THROUGH'}
def controls(layout):
    row=layout.row(align=True)
    for op,label in [('raw_deer.bank','1: Set 1/1'),('raw_deer.species','2: Species'),('raw_deer.view','3: View'),('raw_deer.mode','4: Loop')]:row.operator(op,text=label)
    row.operator('deer_own.skeleton',text='Show Skeleton' if rig.hide_get() else 'Hide Skeleton')
    row.operator('deer_own.play',text='Pause / Play')
def header(self,context):controls(self.layout)
class RAW_PT_review(bpy.types.Panel):
    bl_idname='RAW_PT_review';bl_label='Animal Review - Raw motion audition'
    bl_space_type='VIEW_3D';bl_region_type='UI';bl_category='Animal Review'
    def draw(self,context):
        col=self.layout.column();col.label(text='Fresh deer / 6000 triangles')
        for op in ('raw_deer.bank','raw_deer.species','raw_deer.view','raw_deer.mode'):col.operator(op)
        col.operator('deer_own.skeleton',text='Show Skeleton' if rig.hide_get() else 'Hide Skeleton')
        col.operator('deer_own.play');col.label(text='Coat: '+state['coat']+' (colour only)')
        col.separator();col.label(text='Individual animation')
        for label in ('Idle','Walk','Trot','Run','Eating','Sleep','lay_to_idle'):
            col.operator('deer_own.clip',text='Get Up' if label=='lay_to_idle' else label).clip=label
        col.label(text='Raw automatic fit; no manual repairs')
def tick():
    for window in bpy.context.window_manager.windows:
        for area in window.screen.areas:
            if area.type=='VIEW_3D':
                region=area.spaces.active.region_3d
                if state['view']==2:
                    angle=(time.monotonic()-state['started'])*.22;direction=R*math.cos(angle)+F*math.sin(angle)+Vector((0,0,.08))
                    region.view_rotation=direction.to_track_quat('Z','Y')
                elif state['view']==1:region.view_location=rig.matrix_world@rig.pose.bones['Head'].head
                area.tag_redraw()
    return .05
for cls in (RAW_OT_bank,RAW_OT_species,RAW_OT_view,RAW_OT_mode,RAW_OT_free,RAW_PT_review):bpy.utils.register_class(cls)
bpy.types.VIEW3D_HT_header.prepend(header)
config=bpy.context.window_manager.keyconfigs.addon
if config:
    km=config.keymaps.new(name='3D View',space_type='VIEW_3D')
    for key,op in [('ONE','raw_deer.bank'),('TWO','raw_deer.species'),('THREE','raw_deer.view'),('FOUR','raw_deer.mode'),('MIDDLEMOUSE','raw_deer.free')]:km.keymap_items.new(op,key,'PRESS')
for window in bpy.context.window_manager.windows:
    for area in window.screen.areas:
        if area.type=='VIEW_3D':area.spaces.active.show_region_ui=True
# Check the visibility control in the actual GUI context before announcing ready.
if not bpy.app.background:
    hidden=rig.hide_get();bpy.ops.deer_own.skeleton();assert rig.hide_get()!=hidden
    bpy.ops.deer_own.skeleton();assert rig.hide_get()==hidden
set_view()
if not bpy.app.background:
    bpy.app.timers.register(tick,first_interval=.1)
    bpy.app.timers.register(lambda: (choose_demo(),None)[1],first_interval=2.5)
print('Raw deer numbered viewer ready.',flush=True)
