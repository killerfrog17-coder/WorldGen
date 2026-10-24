"""Visible live material progress and existing animation controls, no inference."""
import json
from pathlib import Path
import runpy
import bpy
info=json.loads(bpy.context.scene['masked_coat_review']);root=Path(info['root'])
was_eating=bpy.context.scene.get('eating_correction',False)
bpy.context.scene['eating_correction']=False
runpy.run_path(str(Path(__file__).with_name('deer-own-rig-viewer.py')))
bpy.context.scene['eating_correction']=was_eating
state={'stage':'Preparing anatomy','stamp':None,'ready':False,'previous':False}
class DMC_OT_coat(bpy.types.Operator):
    bl_idname='deer_coat.compare';bl_label='Compare coat'
    previous:bpy.props.BoolProperty()
    def execute(self,context):
        state['previous']=self.previous
        bpy.data.objects[info['mesh']].data.materials[0]=bpy.data.materials[info['previous'] if self.previous else info['trial']]
        return {'FINISHED'}
def status_header(self,context):
    row=self.layout.row(align=True)
    row.operator('deer_coat.compare',text='New coat').previous=False
    row.operator('deer_coat.compare',text='Previous coat').previous=True
    row.label(text=state['stage'])
class DMC_PT_panel(bpy.types.Panel):
    bl_label='Masked coat progress';bl_idname='DMC_PT_panel'
    bl_space_type='VIEW_3D';bl_region_type='UI';bl_category='Deer'
    def draw(self,context):
        col=self.layout.column();col.label(text=state['stage'])
        col.label(text='Existing body / rig / motion')
        col.operator('deer_coat.compare',text='New coat').previous=False
        col.operator('deer_coat.compare',text='Previous coat').previous=True
def refresh():
    try:
        progress=json.loads((root/'progress.json').read_text());state['stage']=progress['stage']
        marker=root/'material-ready.json'
        if marker.exists() and marker.stat().st_mtime_ns!=state['stamp']:
            for role,filename,space in [('colour','coat-colour.png','sRGB'),('roughness','coat-roughness.png','Non-Color'),('normal','coat-normal.png','Non-Color')]:
                path=root/filename;node=bpy.data.materials[info['trial']].node_tree.nodes['Coat '+role]
                if node.image and node.image.source=='FILE':node.image.filepath=str(path);node.image.reload()
                else:node.image=bpy.data.images.load(str(path),check_existing=False)
                node.image.colorspace_settings.name=space
                mat=bpy.data.materials[info['trial']];bsdf=mat.node_tree.nodes.get('Principled BSDF')
                mat.node_tree.nodes['Coat normal mapping'].inputs['Strength'].default_value=1.0
                destination=mat.node_tree.nodes['Coat normal mapping'].inputs['Color'] if role=='normal' else bsdf.inputs['Base Color' if role=='colour' else 'Roughness']
                if not destination.is_linked:mat.node_tree.links.new(node.outputs['Color'],destination)
            state['stamp']=marker.stat().st_mtime_ns;state['ready']=True
        for screen in bpy.data.screens:
            for ar in screen.areas:
                if ar.type=='VIEW_3D':ar.tag_redraw()
    except (OSError,ValueError):pass
    return 1.0
for cls in (DMC_OT_coat,DMC_PT_panel):bpy.utils.register_class(cls)
bpy.types.VIEW3D_HT_header.prepend(status_header);bpy.app.timers.register(refresh,first_interval=.25)
print('Masked-coat live viewer ready.',flush=True)
