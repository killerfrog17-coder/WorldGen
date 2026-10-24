"""Keyboard/phone-friendly stage1 review: model, phenotype and camera cycling."""
import json
import math
import time
from pathlib import Path
import bpy
from mathutils import Vector,Quaternion

scene=bpy.context.scene;info=json.loads(scene['fauna_stage1'])
stage2=info.get('stage')==2
for coat in info['coats']:
    material=bpy.data.materials[coat['material']];nodes=material.node_tree.nodes;links=material.node_tree.links
    if 'Species tint' not in nodes:
        bsdf=nodes.get('Principled BSDF');source=bsdf.inputs['Base Color'].links[0].from_socket
        tint=nodes.new('ShaderNodeHueSaturation');tint.name='Species tint'
        links.new(source,tint.inputs['Color']);links.new(tint.outputs['Color'],bsdf.inputs['Base Color'])
state={'coat':0,'seed':732123,'view':0,'started':time.monotonic(),'bag':list(range(1,len(info['coats']))),'sex':'Female','antler':''}
class F1_OT_model(bpy.types.Operator):
    bl_idname='fauna_review.model';bl_label='1 - Model'
    def execute(self,context):self.report({'INFO'},'Motion set 1 / 1 - accepted authored bank' if stage2 else 'Model 1 / 1 - fresh deer');return {'FINISHED'}
class F1_OT_species(bpy.types.Operator):
    bl_idname='fauna_review.species';bl_label='2 - Species variation'
    def execute(self,context):
        state['seed']=(1664525*state['seed']+1013904223)&0xffffffff
        if not state['bag']:state['bag']=[i for i in range(len(info['coats'])) if i!=state['coat']]
        state['coat']=state['bag'].pop(state['seed']%len(state['bag']))
        material=bpy.data.materials[info['coats'][state['coat']]['material']]
        tint=material.node_tree.nodes['Species tint']
        tint.inputs['Hue'].default_value=.48+((state['seed']>>8)%1000)/25000
        tint.inputs['Saturation'].default_value=.90+((state['seed']>>16)%100)/500
        tint.inputs['Value'].default_value=.92+((state['seed']>>24)%100)/625
        bpy.data.objects[info['body']].data.materials[0]=material
        profile=info['coats'][state['coat']];body=bpy.data.objects[info['body']]
        if body.data.shape_keys and 'Compact ears' in body.data.shape_keys.key_blocks:
            ear=max(.72,min(1.08,profile.get('ear_scale',.85)+(((state['seed']>>12)%100)/100-.5)*.06))
            tail=max(.70,min(1.20,profile.get('tail_scale',1)+(((state['seed']>>20)%100)/100-.5)*.12))
            keys=body.data.shape_keys.key_blocks
            keys['Compact ears'].value=max(0,(1-ear)/.28);keys['Large ears'].value=max(0,(ear-1)/.08)
            keys['Short tail'].value=max(0,(1-tail)/.30);keys['Long tail'].value=max(0,(tail-1)/.20)
            state['ear']=ear;state['tail']=tail
        if info.get('antlers'):
            male=bool((state['seed']>>6)&1)
            active=profile.get('antler_style',0) if male else -1
            for index,antler in enumerate(info['antlers']):
                obj=bpy.data.objects[antler['object']];obj.hide_set(index!=active);obj.hide_render=index!=active
            state['sex']='Male' if male else 'Female'
            state['antler']=info['antlers'][active]['label'] if male else 'No antlers'
        context.scene['review_phenotype_seed']=str(state['seed']);return {'FINISHED'}
def set_view(context,index):
    for window in bpy.context.window_manager.windows:
        for area in window.screen.areas:
            if area.type!='VIEW_3D':continue
            r=area.spaces.active.region_3d;r.view_perspective='ORTHO'
            if index==1:
                r.view_location=info['head_target'];r.view_distance=.78;direction=Vector((0,1,.015))
            elif index==3:
                r.view_location=(0,0,.83);r.view_distance=3.0;direction=Vector((0,-1,.02))
            elif index==4:
                r.view_location=(0,0,.83);r.view_distance=3.0;direction=Vector((0,1,.02))
            else:
                r.view_location=(0,0,.83);r.view_distance=3.2;direction=Vector((1,0,.02))
            r.view_rotation=direction.to_track_quat('Z','Y');area.tag_redraw()
class F1_OT_view(bpy.types.Operator):
    bl_idname='fauna_review.view';bl_label='3 - Side / Face / Orbit'
    rear:bpy.props.BoolProperty(default=False)
    front:bpy.props.BoolProperty(default=False)
    def execute(self,context):
        if self.rear:state['view']=3
        elif self.front:state['view']=4
        else:state['view']=(state['view']+1)%3
        state['started']=time.monotonic();set_view(context,state['view']);return {'FINISHED'}

class F1_OT_free_orbit(bpy.types.Operator):
    bl_idname='fauna_review.free_orbit';bl_label='Free orbit'
    def execute(self,context):
        state['view']=5
        for window in bpy.context.window_manager.windows:
            for area in window.screen.areas:
                if area.type=='VIEW_3D':
                    region=area.spaces.active.region_3d;region.lock_rotation=False;region.view_perspective='PERSP'
        return {'FINISHED'}
    def invoke(self,context,event):
        self.execute(context)
        if event.type=='MIDDLEMOUSE':
            self.region=context.space_data.region_3d
            self.previous=(event.mouse_x,event.mouse_y)
            context.window_manager.modal_handler_add(self)
            return {'RUNNING_MODAL'}
        return {'FINISHED'}
    def modal(self,context,event):
        if event.type in {'ESC','RIGHTMOUSE'} or (event.type=='MIDDLEMOUSE' and event.value=='RELEASE'):
            return {'FINISHED'}
        if event.type=='MOUSEMOVE':
            dx=event.mouse_x-self.previous[0];dy=event.mouse_y-self.previous[1]
            self.previous=(event.mouse_x,event.mouse_y)
            self.region.view_rotation=(Quaternion((0,0,1),-dx*.006)@
                self.region.view_rotation@Quaternion((1,0,0),dy*.006)).normalized()
            context.area.tag_redraw()
        return {'RUNNING_MODAL'}
def draw_controls(layout):
    row=layout.row(align=True)
    row.operator('fauna_review.model',text='1: Motion set 1/1' if stage2 else '1: Model 1/1')
    row.operator('fauna_review.species',text='2: Species variation')
    row.operator('fauna_review.view',text='3: Side / Face / Orbit')
    row.operator('fauna_review.view',text='Rear').rear=True
    row.operator('fauna_review.view',text='Front body').front=True
    row.operator('fauna_review.free_orbit',text='Free orbit')
def header(self,context):draw_controls(self.layout)
class F1_PT_panel(bpy.types.Panel):
    bl_idname='F1_PT_panel';bl_label='Animal animation review - Stage 2' if stage2 else 'Animal model review - Stage 1'
    bl_space_type='VIEW_3D';bl_region_type='UI';bl_category='Animal Review'
    def draw(self,context):
        col=self.layout.column();col.label(text='Deer animation - Motion set 1 / 1' if stage2 else 'Fresh deer - Model 1 / 1')
        col.label(text=info['coats'][state['coat']]['label'])
        col.label(text='Phenotype seed '+str(state['seed']))
        if info.get('antlers'):col.label(text=state['sex']+' - '+(state['antler'] or 'No antlers'))
        if 'ear' in state:col.label(text=f"Ear trait {state['ear']:.2f} / Tail {state['tail']:.2f}")
        col.label(text=('Up to ' if info.get('antlers') else '')+str(info['triangle_count'])+' triangles'+(' - fitted rig' if stage2 else ' - unrigged'))
        col.separator();col.operator('fauna_review.species');col.operator('fauna_review.view')
        col.label(text='Approved model - animation review' if stage2 else 'Awaiting model/coat approval')
def tick():
    for window in bpy.context.window_manager.windows:
        for area in window.screen.areas:
            if area.type=='VIEW_3D':
                if state['view']==2:
                    angle=(time.monotonic()-state['started'])*.22
                    area.spaces.active.region_3d.view_rotation=Vector((math.cos(angle),math.sin(angle),.08)).to_track_quat('Z','Y')
                area.tag_redraw()
    return .05 if state['view']==2 else .25
for cls in (F1_OT_model,F1_OT_species,F1_OT_view,F1_OT_free_orbit,F1_PT_panel):bpy.utils.register_class(cls)
bpy.types.VIEW3D_HT_header.prepend(header)
keyconfig=bpy.context.window_manager.keyconfigs.addon
if keyconfig:
    km=keyconfig.keymaps.new(name='3D View',space_type='VIEW_3D')
    for key,op in [('ONE','fauna_review.model'),('TWO','fauna_review.species'),('THREE','fauna_review.view')]:km.keymap_items.new(op,key,'PRESS')
    km.keymap_items.new('fauna_review.free_orbit','MIDDLEMOUSE','PRESS')
if not bpy.app.background:bpy.app.timers.register(tick,first_interval=.1)
if not bpy.app.background:
    import blf
    def draw_status():
        blf.size(0,17);blf.color(0,1,1,1,1);blf.enable(0,blf.SHADOW);blf.shadow(0,3,0,0,0,1)
        lines=[('STAGE 2 - DEER MOTION SET 1 / 1' if stage2 else 'STAGE 1 - DEER MODEL 1 / 1 - FACE REV '+str(info.get('face_revision',info.get('nose_revision',1)))),
            '2: '+info['coats'][state['coat']]['label']+' | seed '+str(state['seed']),
            '3: Side / Face / Orbit | '+('up to ' if info.get('antlers') else '')+str(info['triangle_count'])+' triangles']
        if info.get('antlers'):lines.insert(2,state['sex']+' | '+(state['antler'] or 'No antlers'))
        height=bpy.context.region.height
        for i,line in enumerate(lines):blf.position(0,18,height-50-i*24,0);blf.draw(0,line)
        blf.disable(0,blf.SHADOW)
    bpy.types.SpaceView3D.draw_handler_add(draw_status,(),'WINDOW','POST_PIXEL')
print('Fauna stage1 viewer ready.',flush=True)
