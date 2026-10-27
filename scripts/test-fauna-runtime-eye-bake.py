"""Regression: constant source UVs must not erase lids; real alpha must survive."""
import json,sys
from pathlib import Path
import bpy,numpy as np
sys.path.insert(0,str(Path(__file__).resolve().parent))
from fauna_runtime_uv import prepare,projected_area
from fauna_runtime_surface import Surface

root=Path(__file__).resolve().parent.parent/'artifacts/fauna-eye-closure-repair/tests';root.mkdir(exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)

def quad(name,uvs,transparent=False):
    mesh=bpy.data.meshes.new(name);mesh.from_pydata([(0,0,0),(1,0,0),(1,1,0),(0,1,0)],[],[(0,1,2,3)]);mesh.update()
    obj=bpy.data.objects.new(name,mesh);bpy.context.scene.collection.objects.link(obj)
    uv=mesh.uv_layers.new(name='UVMap')
    for loop in mesh.loops:uv.data[loop.index].uv=uvs[loop.vertex_index]
    material=bpy.data.materials.new(name);material.use_nodes=True;shader=material.node_tree.nodes.get('Principled BSDF');shader.inputs['Base Color'].default_value=(.2,.1,.03,1)
    if transparent:
        pixels=np.ones((4,4,4),np.float32);pixels[:,:2,3]=0
        image=bpy.data.images.new('Intentional alpha cutout',4,4,alpha=True,float_buffer=True);image.pixels.foreach_set(pixels.ravel())
        texture=material.node_tree.nodes.new('ShaderNodeTexImage');texture.image=image;texture.extension='EXTEND';material.node_tree.links.new(texture.outputs['Alpha'],shader.inputs['Alpha'])
    mesh.materials.append(material);return obj

opaque=quad('Opaque lid with legitimate constant shading UV',[(0,0)]*4)
cutout=quad('Intentional alpha cutout',[(0,0),(1,0),(1,1),(0,1)],True)
before=np.array([v.co[:] for v in opaque.data.vertices]);record=prepare(opaque,(.02,.02,.44,.96));prepare(cutout,(.54,.02,.44,.96))
assert np.array_equal(before,np.array([v.co[:] for v in opaque.data.vertices]))
assert projected_area(opaque.data,opaque.data.uv_layers['UVMap'])[0]==0
assert projected_area(opaque.data,opaque.data.uv_layers['RuntimeUV'])[0]>.01
surface=Surface([opaque,cutout],128);surface.bake('mask',root/'mask.png',[[opaque.data.materials[0]],[cutout.data.materials[0]]])
image=bpy.data.images.load(str(root/'mask.png'),check_existing=False);pixels=np.array(image.pixels[:]).reshape(128,128,4)
assert pixels[64,28,1]>.99,'Solid lid became transparent'
assert pixels[64,78,1]<.05,'Intentional transparent source was made opaque'
assert pixels[64,115,1]>.99,'Intentional solid half was lost'
tiny=quad('Subpixel sample fixture',[(0,0)]*4);tiny.data.clear_geometry();tiny.data.from_pydata([(0,0,0),(1,0,0),(0,1,0)],[],[(0,1,2)]);tiny.data.update();uv=tiny.data.uv_layers.new(name='RuntimeUV')
for loop,point in zip(tiny.data.loops,[(.501,.501),(.5011,.501),(.501,.5011)]):uv.data[loop.index].uv=point
thin=Surface([tiny],32)
assert sum(len(g['pixels']) for g in thin.groups)>0,'Thin valid UV triangle got no shading sample'
(root/'regression.json').write_text(json.dumps({'opaque_constant_source_uv_preserved':True,'opaque_lid_visible':True,'true_source_alpha_preserved':True,'subpixel_triangle_retained':True,'mesh_vertices_unchanged':True},indent=2))
print('FAUNA_EYE_BAKE_REGRESSION_PASS',flush=True)
