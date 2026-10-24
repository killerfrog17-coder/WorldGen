"""Visible A/B and clay/material buttons for a static generated-material review."""
import json
import bpy
from mathutils import Vector
s=bpy.context.scene;info=json.loads(s['trellis_review'])
class DT_OT_show(bpy.types.Operator):
    bl_idname='deer_trellis.show';bl_label='Show deer'
    previous:bpy.props.BoolProperty()
    def execute(self,context):
        for n in info['new']:
            o=bpy.data.objects[n];o.hide_set(self.previous);o.hide_render=self.previous
        o=bpy.data.objects[info['previous']];o.hide_set(not self.previous);o.hide_render=not self.previous
        context.scene['trellis_previous']=self.previous;return {'FINISHED'}
class DT_OT_material(bpy.types.Operator):
    bl_idname='deer_trellis.material';bl_label='View material'
    clay:bpy.props.BoolProperty()
    def execute(self,context):
        context.space_data.shading.type='SOLID' if self.clay else 'MATERIAL';return {'FINISHED'}
class DT_OT_view(bpy.types.Operator):
    bl_idname='deer_trellis.view';bl_label='Rotate view'
    direction:bpy.props.IntProperty()
    def execute(self,context):
        v=((3,-.8,.25),(.5,-3,.25),(-3,.8,.25))[self.direction]
        r=context.space_data.region_3d;r.view_location=(0,0,.8);r.view_distance=3.4
        r.view_rotation=Vector(v).to_track_quat('Z','Y');return {'FINISHED'}
def header(self,context):
    row=self.layout.row(align=True)
    row.operator('deer_trellis.show',text='TRELLIS.2 deer').previous=False
    row.operator('deer_trellis.show',text='Previous deer').previous=True
    row.operator('deer_trellis.material',text='Materials').clay=False
    row.operator('deer_trellis.material',text='Clay').clay=True
    for i,n in enumerate(('Side','Front','Back')):row.operator('deer_trellis.view',text=n).direction=i
class DT_PT_panel(bpy.types.Panel):
    bl_label='TRELLIS.2 material comparison';bl_idname='DT_PT_panel'
    bl_space_type='VIEW_3D';bl_region_type='UI';bl_category='Deer'
    def draw(self,context):
        self.layout.operator('deer_trellis.show',text='New TRELLIS.2 deer').previous=False
        self.layout.operator('deer_trellis.show',text='Previous deer').previous=True
        self.layout.label(text='Native shape + PBR material generation')
        self.layout.label(text='Built with DINOv3')
        self.layout.label(text='Static review; not rigged or animated')
for cls in (DT_OT_show,DT_OT_material,DT_OT_view,DT_PT_panel):bpy.utils.register_class(cls)
bpy.types.VIEW3D_HT_header.prepend(header)
print('TRELLIS2 viewer controls ready.',flush=True)
