"""Visible appearance review controls for the two real character builders."""
import importlib.util
import json
from pathlib import Path

import bpy
import blf

REPO=Path(__file__).resolve().parent.parent
spec=importlib.util.spec_from_file_location('human_demo_scene',REPO/'scripts/assemble-human-demo.py')
stage=importlib.util.module_from_spec(spec);spec.loader.exec_module(stage)
RECIPES=json.loads(bpy.context.scene['demo_recipes'])
VIEWS=['face','threequarter','profile','body']


def refresh():
    scene=bpy.context.scene
    stage.select(scene.get('demo_builder','MPFB'),scene.get('demo_variant','A'),scene.get('demo_view','face'),scene.get('demo_compare',True),scene.get('demo_lineup',False))
    for screen in bpy.data.screens:
        for area in screen.areas:area.tag_redraw()


class HUMANDEMO_OT_action(bpy.types.Operator):
    bl_idname='human_demo.action';bl_label='Review characters'
    action:bpy.props.StringProperty()
    value:bpy.props.StringProperty()
    def execute(self,context):
        scene=context.scene
        if self.action=='builder':scene['demo_builder']=self.value;scene['demo_lineup']=False
        elif self.action=='variant':scene['demo_variant']=self.value;scene['demo_lineup']=False
        elif self.action=='view':scene['demo_view']=self.value;scene['demo_lineup']=False
        elif self.action=='compare':scene['demo_compare']=not scene.get('demo_compare',True);scene['demo_lineup']=False
        elif self.action=='lineup':scene['demo_lineup']=not scene.get('demo_lineup',False);scene['demo_compare']=False
        elif self.action=='method_next':scene['demo_builder']='UMA' if scene.get('demo_builder')=='MPFB' else 'MPFB';scene['demo_compare']=False
        elif self.action=='variant_next':scene['demo_variant']=chr(ord('A')+(ord(scene.get('demo_variant','A'))-ord('A')+1)%6);scene['demo_lineup']=False
        elif self.action=='view_next':scene['demo_view']=VIEWS[(VIEWS.index(scene.get('demo_view','face'))+1)%len(VIEWS)];scene['demo_lineup']=False
        refresh();return {'FINISHED'}


def button(layout,text,action,value='',depress=False):
    op=layout.operator('human_demo.action',text=text,depress=depress);op.action=action;op.value=value


class HUMANDEMO_PT_review(bpy.types.Panel):
    bl_label='Character Builder Comparison';bl_idname='HUMANDEMO_PT_review'
    bl_space_type='VIEW_3D';bl_region_type='UI';bl_category='Item';bl_order=-100
    def draw(self,context):
        scene=context.scene;layout=self.layout
        layout.label(text='Appearance audition • 12 adults')
        row=layout.row(align=True);row.scale_y=1.4
        for builder in ['MPFB','UMA']:button(row,builder,'builder',builder,scene.get('demo_builder')==builder)
        row=layout.row(align=True);row.scale_y=1.3
        button(row,'Compare both','compare',depress=scene.get('demo_compare',False))
        button(row,'Six-person lineup','lineup',depress=scene.get('demo_lineup',False))
        layout.separator();layout.label(text='People')
        for recipe in RECIPES:
            row=layout.row();row.scale_y=1.25
            button(row,recipe['id']+' · '+recipe['label'],'variant',recipe['id'],scene.get('demo_variant')==recipe['id'])
        layout.separator();layout.label(text='Views')
        for first,second in [(('Face','face'),('Three-quarter','threequarter')),(('Profile','profile'),('Full body','body'))]:
            row=layout.row(align=True);row.scale_y=1.2
            for text,value in [first,second]:button(row,text,'view',value,scene.get('demo_view')==value)
        layout.separator()
        recipe=next(r for r in RECIPES if r['id']==scene.get('demo_variant','A'))
        for part in recipe['description'].split(' / '):layout.label(text=part)
        layout.separator();layout.label(text='1 Method   2 Person   3 View')
        layout.label(text='4 Compare   5 Lineup')
        layout.label(text='Mouse wheel zooms; middle mouse orbits.')
        layout.label(text='Animation audition comes after selection.')


def overlay():
    scene=bpy.context.scene;region=bpy.context.region
    if not region:return
    blf.size(0,22);blf.color(0,.94,.95,.98,1)
    title=('MPFB — LEFT                  UMA — RIGHT' if scene.get('demo_compare') else scene.get('demo_builder','MPFB'))
    if scene.get('demo_lineup'):title+=' — SIX VARIANTS'
    blf.position(0,24,region.height-45,0);blf.draw(0,title)
    recipe=next(r for r in RECIPES if r['id']==scene.get('demo_variant','A'))
    blf.size(0,15);blf.position(0,24,region.height-70,0);blf.draw(0,recipe['id']+' · '+recipe['label'])


def initialize():
    for cls in [HUMANDEMO_OT_action,HUMANDEMO_PT_review]:bpy.utils.register_class(cls)
    keyconfig=bpy.context.window_manager.keyconfigs.addon
    if keyconfig:
        keymap=keyconfig.keymaps.new(name='3D View',space_type='VIEW_3D')
        for key,action in [('ONE','method_next'),('TWO','variant_next'),('THREE','view_next'),('FOUR','compare'),('FIVE','lineup')]:
            entry=keymap.keymap_items.new('human_demo.action',key,'PRESS');entry.properties.action=action
    bpy.types.SpaceView3D.draw_handler_add(overlay,(),'WINDOW','POST_PIXEL')
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                area.spaces.active.show_region_ui=True;area.spaces.active.overlay.show_overlays=False
                area.spaces.active.shading.type='MATERIAL'
                area.spaces.active.shading.use_scene_world=True;area.spaces.active.shading.use_scene_lights=True
    refresh();print('Human builder comparison viewer ready.',flush=True)


bpy.app.timers.register(initialize,first_interval=.7)
