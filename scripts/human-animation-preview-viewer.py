"""Visible controls for the isolated MPFB/UMA animation comparison."""
import json
import sys
from pathlib import Path

import bpy
import blf

sys.path.insert(0,str(Path(__file__).resolve().parent))
import human_animation_stage as stage
RECIPES=json.loads(bpy.context.scene['demo_recipes'])
LABELS=json.loads(bpy.context.scene['animation_labels'])


def button(layout,text,action,value='',depress=False,icon='NONE'):
    op=layout.operator('human_motion.action',text=text,depress=depress,icon=icon)
    op.action=action;op.value=value


class HUMANMOTION_OT_action(bpy.types.Operator):
    bl_idname='human_motion.action';bl_label='Animation review'
    action:bpy.props.StringProperty()
    value:bpy.props.StringProperty()
    def execute(self,context):
        scene=context.scene
        if self.action=='clip':stage.select_clip(self.value)
        elif self.action=='next_clip':stage.select_clip(LABELS[(LABELS.index(scene['preview_clip'])+1)%len(LABELS)])
        elif self.action=='builder':
            scene['demo_builder']=self.value;scene['demo_compare']=False;scene['demo_lineup']=False
        elif self.action=='compare':scene['demo_compare']=not scene.get('demo_compare',True);scene['demo_lineup']=False
        elif self.action=='lineup':scene['demo_lineup']=not scene.get('demo_lineup',False);scene['demo_compare']=False
        elif self.action=='variant':scene['demo_variant']=self.value;scene['demo_lineup']=False
        elif self.action=='view':scene['demo_view']=self.value
        elif self.action=='skeleton':scene['preview_skeleton']=not scene.get('preview_skeleton',False)
        elif self.action=='play':bpy.ops.screen.animation_play()
        elif self.action=='restart':scene.frame_set(scene.frame_start)
        elif self.action=='method_next':
            scene['demo_builder']='UMA' if scene.get('demo_builder')=='MPFB' else 'MPFB';scene['demo_compare']=False;scene['demo_lineup']=False
        elif self.action=='person_next':scene['demo_variant']=chr(65+(ord(scene.get('demo_variant','A'))-64)%6);scene['demo_lineup']=False
        elif self.action=='view_next':
            views=['body','front','side'];scene['demo_view']=views[(views.index(scene.get('demo_view','body'))+1)%3]
        stage.refresh()
        return {'FINISHED'}


class HUMANMOTION_PT_review(bpy.types.Panel):
    bl_label='Human Animation Comparison';bl_idname='HUMANMOTION_PT_review'
    bl_space_type='VIEW_3D';bl_region_type='UI';bl_category='Item';bl_order=-100
    def draw(self,context):
        scene=context.scene;layout=self.layout
        row=layout.row(align=True);row.scale_y=1.4
        for method in ['MPFB','UMA']:button(row,method,'builder',method,not scene.get('demo_compare') and scene.get('demo_builder')==method)
        button(row,'Compare','compare',depress=scene.get('demo_compare',False))
        row=layout.row(align=True);row.scale_y=1.5
        playing=context.screen.is_animation_playing
        button(row,'Pause' if playing else 'Play','play',icon='PAUSE' if playing else 'PLAY')
        button(row,'Restart','restart',icon='REW')
        layout.separator()
        layout.label(text='Animation')
        for index in range(0,len(LABELS),2):
            row=layout.row(align=True);row.scale_y=1.2
            for label in LABELS[index:index+2]:button(row,label,'clip',label,scene.get('preview_clip')==label)
        layout.separator();layout.label(text='People (same six appearance samples)')
        for index in range(0,6,3):
            row=layout.row(align=True);row.scale_y=1.3
            for recipe in RECIPES[index:index+3]:button(row,recipe['id'],'variant',recipe['id'],scene.get('demo_variant')==recipe['id'])
        recipe=next(r for r in RECIPES if r['id']==scene.get('demo_variant','A'))
        layout.label(text=recipe['label'])
        button(layout,'Six-person lineup','lineup',depress=scene.get('demo_lineup',False))
        layout.separator();row=layout.row(align=True);row.scale_y=1.2
        for text,value in [('Three-quarter','body'),('Front','front'),('Side','side')]:button(row,text,'view',value,scene.get('demo_view')==value)
        row=layout.row();row.scale_y=1.4
        button(row,'Hide skeleton' if scene.get('preview_skeleton') else 'Show skeleton','skeleton',depress=scene.get('preview_skeleton',False),icon='ARMATURE_DATA')
        layout.separator();layout.label(text='1 Method  2 Person  3 View  4 Compare')
        layout.label(text='5 Lineup  6 Next animation  Space Play')
        layout.label(text='Free CC0 motion / full-body audition')


def overlay():
    scene=bpy.context.scene;region=bpy.context.region
    if not region:return
    title='MPFB (left)  /  UMA (right)' if scene.get('demo_compare') else scene.get('demo_builder','MPFB')
    blf.size(0,22);blf.color(0,.95,.96,.99,1);blf.position(0,24,region.height-40,0);blf.draw(0,title)
    blf.size(0,16);blf.position(0,24,region.height-65,0)
    blf.draw(0,scene.get('demo_variant','A')+'  |  '+scene.get('preview_clip','Walk'))


def initialize():
    for cls in [HUMANMOTION_OT_action,HUMANMOTION_PT_review]:bpy.utils.register_class(cls)
    keyconfig=bpy.context.window_manager.keyconfigs.addon
    if keyconfig:
        keymap=keyconfig.keymaps.new(name='3D View',space_type='VIEW_3D')
        for key,action in [('ONE','method_next'),('TWO','person_next'),('THREE','view_next'),('FOUR','compare'),('FIVE','lineup'),('SIX','next_clip'),('SPACE','play')]:
            entry=keymap.keymap_items.new('human_motion.action',key,'PRESS');entry.properties.action=action
    bpy.types.SpaceView3D.draw_handler_add(overlay,(),'WINDOW','POST_PIXEL')
    bpy.context.scene.sync_mode='FRAME_DROP'
    stage.refresh()
    bpy.ops.screen.animation_play()
    print('Human animation comparison viewer ready.',flush=True)


bpy.app.timers.register(initialize,first_interval=.8)
