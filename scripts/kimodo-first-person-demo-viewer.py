"""Visible three-action controls for the Kimodo first-person/body review."""
import json
from pathlib import Path
import sys
import bpy
import blf

sys.path.insert(0, str(Path(__file__).resolve().parent))
import kimodo_demo_stage as stage


def button(layout, title, action, value='', depress=False):
    op = layout.operator('kimodo_demo.action', text=title, depress=depress)
    op.action = action
    op.value = value


class KIMODO_OT_action(bpy.types.Operator):
    bl_idname = 'kimodo_demo.action'
    bl_label = 'Kimodo review'
    action: bpy.props.StringProperty()
    value: bpy.props.StringProperty()

    def execute(self, context):
        scene = context.scene
        if self.action == 'clip':
            stage.select_clip(self.value)
        elif self.action == 'view':
            scene['kimodo_view'] = self.value
        elif self.action == 'look':
            scene['kimodo_view'] = 'first'
            scene['kimodo_pitch'] = float(self.value)
        elif self.action == 'variant':
            scene['kimodo_variant'] = self.value
        elif self.action == 'next_variant':
            variants = scene['kimodo_variants']
            scene['kimodo_variant'] = variants[(variants.index(scene['kimodo_variant']) + 1) % len(variants)]
        elif self.action == 'next_view':
            views = ['first', 'outside', 'side', 'front']
            scene['kimodo_view'] = views[(views.index(scene['kimodo_view']) + 1) % len(views)]
        elif self.action == 'skeleton':
            scene['kimodo_skeleton'] = not scene.get('kimodo_skeleton', False)
        elif self.action == 'restart':
            scene.frame_set(1)
        elif self.action == 'play':
            bpy.ops.screen.animation_play()
        stage.refresh()
        return {'FINISHED'}


class KIMODO_PT_review(bpy.types.Panel):
    bl_label = 'Kimodo — First Person / Body'
    bl_idname = 'KIMODO_PT_review'
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = 'Kimodo'
    bl_order = -100

    def draw(self, context):
        scene = context.scene
        layout = self.layout
        row = layout.row(align=True)
        row.scale_y = 1.5
        button(row, 'Pause' if context.screen.is_animation_playing else 'Play', 'play')
        button(row, 'Restart', 'restart')
        layout.separator()
        for key, label in stage.LABELS.items():
            row = layout.row()
            row.scale_y = 1.5
            button(row, label, 'clip', key, scene['kimodo_clip'] == key)
        layout.separator()
        row = layout.row(align=True)
        row.scale_y = 1.35
        button(row, 'First person', 'view', 'first', scene['kimodo_view'] == 'first')
        button(row, 'Outside', 'view', 'outside', scene['kimodo_view'] == 'outside')
        row = layout.row(align=True)
        button(row, 'Side', 'view', 'side', scene['kimodo_view'] == 'side')
        button(row, 'Front', 'view', 'front', scene['kimodo_view'] == 'front')
        layout.label(text='Look angle / visible body')
        row = layout.row(align=True)
        for title, value in [('Ahead', '0'), ('Hands', '-20'), ('Body', '-65')]:
            button(row, title, 'look', value)
        layout.separator()
        layout.label(text='Existing characters')
        row = layout.row(align=True)
        for letter in scene['kimodo_variants']:
            button(row, letter, 'variant', letter, scene['kimodo_variant'] == letter)
        button(layout, 'Hide skeleton' if scene.get('kimodo_skeleton') else 'Show skeleton', 'skeleton')
        layout.separator()
        layout.label(text='1 / 2 / 3: actions    4: view')
        layout.label(text='5: person    Space: play/pause')
        layout.label(text='Kimodo body motion; grips fitted')
        layout.label(text='Separate demo; game unchanged')


def overlay():
    scene = bpy.context.scene
    region = bpy.context.region
    if not region:
        return
    blf.size(0, 19)
    blf.color(0, 1, 1, 1, 1)
    blf.position(0, 24, region.height - 34, 0)
    blf.draw(0, stage.LABELS[scene['kimodo_clip']] + '  |  ' + scene['kimodo_view'] + '  |  ' + scene['kimodo_variant'])


def header(self, context):
    scene = context.scene
    if 'kimodo_variant' not in scene:
        return
    row = self.layout.row(align=True)
    for title, value in [('1 Walk', 'empty_walk'), ('2 Spear walk', 'spear_walk'), ('3 Throw', 'spear_throw')]:
        button(row, title, 'clip', value, scene['kimodo_clip'] == value)
    button(row, 'First person', 'look', '-15')
    button(row, 'Look down', 'look', '-65')
    button(row, 'Outside', 'view', 'outside')
    button(row, 'Person ' + scene['kimodo_variant'], 'next_variant')
    button(row, 'Skeleton', 'skeleton', depress=scene.get('kimodo_skeleton', False))
    button(row, 'Pause' if context.screen.is_animation_playing else 'Play', 'play')
    button(row, 'Restart', 'restart')
    self.layout.separator()


def initialize():
    for cls in [KIMODO_OT_action, KIMODO_PT_review]:
        bpy.utils.register_class(cls)
    config = bpy.context.window_manager.keyconfigs.addon
    if config:
        keymap = config.keymaps.new(name='3D View', space_type='VIEW_3D')
        for key, action, value in [('ONE', 'clip', 'empty_walk'), ('TWO', 'clip', 'spear_walk'),
                                   ('THREE', 'clip', 'spear_throw'), ('FOUR', 'next_view', ''),
                                   ('FIVE', 'next_variant', ''), ('SPACE', 'play', '')]:
            item = keymap.keymap_items.new('kimodo_demo.action', key, 'PRESS')
            item.properties.action = action
            item.properties.value = value
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == 'VIEW_3D':
                area.spaces.active.show_region_ui = True
                # Select our visible tab rather than leaving the old Item tab.
                for region in area.regions:
                    if region.type == 'UI':
                        try:
                            with bpy.context.temp_override(area=area, region=region):
                                bpy.ops.wm.context_set_string(data_path='space_data.context', value='Kimodo')
                        except Exception:
                            pass
    bpy.context.scene['kimodo_view'] = 'outside'
    stage.register_handler()
    stage.refresh()
    bpy.types.VIEW3D_HT_header.prepend(header)
    bpy.types.SpaceView3D.draw_handler_add(overlay, (), 'WINDOW', 'POST_PIXEL')
    bpy.context.scene.sync_mode = 'FRAME_DROP'
    bpy.ops.screen.animation_play()
    print('Kimodo first-person demo viewer ready.', flush=True)


bpy.app.timers.register(initialize, first_interval=.8)
