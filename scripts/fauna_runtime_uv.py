"""Keep native shading UVs intact while giving every exported part usable bake UVs."""
import math
import bpy
import numpy as np


def projected_area(mesh, layer):
    mesh.calc_loop_triangles()
    coordinates=np.array([v.uv[:] for v in layer.data])
    faces=np.array([t.loops[:] for t in mesh.loop_triangles])
    if not len(faces):return 0.,0
    uv=coordinates[faces];a=uv[:,1]-uv[:,0];b=uv[:,2]-uv[:,0]
    areas=np.abs(a[:,0]*b[:,1]-a[:,1]*b[:,0])
    return float(areas.sum()),int((areas>1e-12).sum())


def prepare(obj,rect):
    mesh=obj.data
    mesh.calc_loop_triangles()
    original_render=next((u for u in mesh.uv_layers if u.active_render),mesh.uv_layers.active)
    original_render_name=original_render.name if original_render else None
    default=mesh.uv_layers.get('Fauna Paint UV') or mesh.uv_layers.active
    candidates=[u for u in mesh.uv_layers if u.name!='RuntimeUV']
    chosen=default;method='native shading layout'
    total=len(mesh.loop_triangles)
    if chosen is None or projected_area(mesh,chosen)[1]<max(1,total*.5):
        chosen=max(candidates,key=lambda u:projected_area(mesh,u)[1],default=None)
        if chosen and projected_area(mesh,chosen)[1]>=max(1,total*.5):method='nondegenerate alternate native layout'
        else:chosen=None
    chosen_name=chosen.name if chosen else None
    if chosen:
        values=np.array([v.uv[:] for v in chosen.data])
        # Alternate per-eye bake maps often occupy only one half of a source
        # image. Destination bounds use the whole assigned rectangle.
        if method!='native shading layout':
            low=values.min(0);span=np.maximum(values.max(0)-low,1e-8);values=(values-low)/span
        values=np.clip(values,0,1)
    target=mesh.uv_layers.new(name='RuntimeUV')
    if not chosen_name:
        # Constant source UVs are valid for flat materials, but cannot be used
        # as a destination atlas: every face would have zero raster coverage.
        for selected in bpy.context.selected_objects:selected.select_set(False)
        obj.hide_set(False);obj.select_set(True);bpy.context.view_layer.objects.active=obj
        mesh.uv_layers.active=target
        bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.02,correct_aspect=False)
        bpy.ops.object.mode_set(mode='OBJECT')
        target=mesh.uv_layers['RuntimeUV']
        values=np.array([v.uv[:] for v in target.data]);method='independent destination unwrap for constant source UVs'
    values=values*np.array(rect[2:])+np.array(rect[:2])
    for item,value in zip(target.data,values):item.uv=value
    # Material nodes with implicit UV input must still evaluate the original
    # native map, not the newly generated destination map.
    if original_render_name:
        original_render=mesh.uv_layers[original_render_name]
        mesh.uv_layers.active=original_render;original_render.active_render=True
    area,valid=projected_area(mesh,target)
    assert area>0 and valid>0,(obj.name,'no usable destination UV triangles')
    return {'object':obj.name,'method':method,'source_uv':chosen_name,'triangles':total,'nondegenerate_destination_triangles':valid,'destination_uv_area':area}
