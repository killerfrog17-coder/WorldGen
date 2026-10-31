"""Visible controls for the isolated twelve-body appearance comparison."""
import json, math
from pathlib import Path
import bpy, blf
from mathutils import Vector
ROOT=Path(__file__).resolve().parent.parent/'assets-source/humans/appearance-review'
VIEWS=['Body','Torso','Legs','Face','Side','Three-quarter']
CLIPS=['Neutral','Idle','Walk','Run']
def refresh():
    scene=bpy.context.scene;person=scene.get('person','B');mode=scene.get('mode','Pair');view=scene.get('view','Body');clip=scene.get('clip','Neutral')
    head=Vector(bpy.data.collections[person+'_after']['head_center'])
    for col in bpy.data.collections:
        if col.name not in [a+'_'+b for a in 'ABCDEF' for b in ['before','after']]:continue
        letter,version=col.name.split('_');visible=letter==person and (mode=='Pair' or version==mode.lower())
        col.hide_render=not visible;col.hide_viewport=not visible
        separation=.30 if view=='Face' else .32 if view=='Legs' else .52
        offset=(-separation if version=='before' else separation) if mode=='Pair' else 0
        for obj in col.objects:
            if obj.parent is None:obj.location.x=0 if view=='Side' else offset;obj.location.y=offset if view=='Side' else 0
        rig=next(o for o in col.objects if o.type=='ARMATURE')
        rig.hide_set(not scene.get('skeleton',False))
        records=json.loads(rig['clips']);record=records[clip];action=bpy.data.actions[record['action']]
        rig.animation_data.action=action;rig.animation_data.action_slot=action.slots[0]
        if visible:scene.frame_end=record['frames'] if clip!='Neutral' else 94
    pair=mode=='Pair'
    focus=Vector((0,0,.88));location=Vector((0,-(5.1 if pair else 3.6),1.05));scene.camera.data.lens=58
    if view in ['Torso','Face']:
        focus=Vector((0,-.02,head.z-(.23 if view=='Torso' else .025)))
        location=focus+Vector((0,-((3 if pair else 1.7) if view=='Torso' else (2.0 if pair else .85)),.03))
        if view=='Face' and pair:scene.camera.data.lens=85
    if view=='Side':location=Vector((5.3 if pair else 3.8,0,1.1))
    if view=='Legs':
        focus=Vector((0,0,.48));location=Vector((0,-(2.2 if pair else 1.4),.48));scene.camera.data.lens=65
    if view=='Three-quarter':location=Vector((2.8,-5.2,1.35)) if pair else Vector((1.9,-3.1,1.15))
    scene.camera.location=location;scene.camera.rotation_euler=(focus-location).to_track_quat('-Z','Y').to_euler()
    scene.frame_set(min(scene.frame_current,scene.frame_end));bpy.context.view_layer.update()
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                area.spaces.active.region_3d.view_perspective='CAMERA';area.spaces.active.overlay.show_overlays=scene.get('skeleton',False)
            area.tag_redraw()
class PHYSIQUE_OT_control(bpy.types.Operator):
    bl_idname='physique.control';bl_label='Appearance comparison'
    kind:bpy.props.StringProperty();value:bpy.props.StringProperty()
    def execute(self,context):
        s=context.scene
        if self.kind=='play':bpy.ops.screen.animation_play();return {'FINISHED'}
        if self.kind=='skeleton':s['skeleton']=not s.get('skeleton',False)
        elif self.kind=='next':
            key=self.value;values={'person':list('ABCDEF'),'mode':['Pair','After','Before'],'view':VIEWS,'clip':CLIPS}[key]
            s[key]=values[(values.index(s[key])+1)%len(values)]
        else:s[self.kind]=self.value
        if self.kind=='clip' or self.kind=='next' and self.value=='clip':s.frame_set(1)
        refresh();return {'FINISHED'}
def button(layout,label,kind,value='',active=False):
    op=layout.operator('physique.control',text=label,depress=active);op.kind=kind;op.value=value
class PHYSIQUE_PT_review(bpy.types.Panel):
    bl_label='Human physique comparison';bl_idname='PHYSIQUE_PT_review';bl_space_type='VIEW_3D';bl_region_type='UI';bl_category='Item';bl_order=-100
    def draw(self,context):
        s=context.scene;l=self.layout
        l.label(text='LEFT: current | RIGHT: revised')
        row=l.row(align=True);row.scale_y=1.3
        for mode in ['Pair','After','Before']:button(row,mode,'mode',mode,s['mode']==mode)
        for entry in json.loads(s['physique_recipes']):
            row=l.row();row.scale_y=1.15;button(row,entry['id']+'  '+entry['label'],'person',entry['id'],s['person']==entry['id'])
        l.separator();l.label(text='Pose and motion')
        row=l.row(align=True)
        for clip in CLIPS:button(row,clip,'clip',clip,s['clip']==clip)
        button(l,'Pause / Play','play')
        l.separator()
        for a,b in [('Body','Torso'),('Face','Side')]:
            row=l.row(align=True)
            for v in [a,b]:button(row,v,'view',v,s['view']==v)
        button(l,'Three-quarter','view','Three-quarter',s['view']=='Three-quarter')
        button(l,'Legs close-up','view','Legs',s['view']=='Legs')
        button(l,'Show / Hide skeleton','skeleton',active=s.get('skeleton',False))
        l.separator();l.label(text='1 Person    2 Before / After / Pair');l.label(text='3 View       4 Pose / Motion')
        l.label(text='Preview only; game is unchanged.')
def overlay():
    region=bpy.context.region
    if not region:return
    s=bpy.context.scene;entry=next(r for r in json.loads(s['physique_recipes']) if r['id']==s['person'])
    blf.size(0,20);blf.color(0,.95,.96,.98,1);blf.position(0,20,region.height-32,0)
    blf.draw(0,entry['label']+'  |  '+s['clip'])
    blf.size(0,17);blf.position(0,20,region.height-57,0)
    blf.draw(0,'CURRENT (left)       REVISED (right)' if s['mode']=='Pair' else s['mode'].upper())
def initialize():
    for cls in [PHYSIQUE_OT_control,PHYSIQUE_PT_review]:bpy.utils.register_class(cls)
    config=bpy.context.window_manager.keyconfigs.addon
    if config:
        km=config.keymaps.new(name='3D View',space_type='VIEW_3D')
        for key,value in [('ONE','person'),('TWO','mode'),('THREE','view'),('FOUR','clip')]:
            k=km.keymap_items.new('physique.control',key,'PRESS');k.properties.kind='next';k.properties.value=value
    bpy.types.SpaceView3D.draw_handler_add(overlay,(),'WINDOW','POST_PIXEL')
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                space=area.spaces.active;space.show_region_ui=True;space.shading.type='MATERIAL';space.shading.use_scene_world=True;space.shading.use_scene_lights=True
    refresh();print('Human physique viewer ready.',flush=True)
if __name__=='__main__':bpy.app.timers.register(initialize,first_interval=.7)
