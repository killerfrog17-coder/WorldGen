"""CPU-only anatomical inspection views of preserved and trial materials."""
import argparse
import json
from pathlib import Path
import sys
import bpy
import numpy as np
from mathutils import Vector
ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True)
ap.add_argument('--trial',action='store_true');a=ap.parse_args(sys.argv[sys.argv.index('--')+1:])
root=a.root.resolve();bpy.ops.wm.open_mainfile(filepath=str(root/'Deer - Masked Coat Working Review.blend'))
s=bpy.context.scene;info=json.loads(s['masked_coat_review']);obj=bpy.data.objects[info['mesh']]
mat=bpy.data.materials[info['trial'] if a.trial else info['previous']];obj.data.materials[0]=mat
if a.trial:
    mat.node_tree.nodes['Coat normal mapping'].inputs['Strength'].default_value=1.0
    for role,name,space in [('colour','coat-colour.png','sRGB'),('roughness','coat-roughness.png','Non-Color'),('normal','coat-normal.png','Non-Color')]:
        node=mat.node_tree.nodes['Coat '+role];node.image=bpy.data.images.load(str(root/name));node.image.colorspace_settings.name=space
        bsdf=mat.node_tree.nodes.get('Principled BSDF')
        dst=mat.node_tree.nodes['Coat normal mapping'].inputs['Color'] if role=='normal' else bsdf.inputs['Base Color' if role=='colour' else 'Roughness']
        mat.node_tree.links.new(node.outputs['Color'],dst)
basis=np.load(root/'anatomy.npz')['basis'];R,F,Z=[Vector(row) for row in basis]
center=Vector(basis.T@np.array([-.268,.715,1.435]))
views={}
for name,direction,target,scale in [('head-right',R,center,.85),('head-left',-R,center,.85),
    ('head-front',F,center,.85),('body',R*3.5+F*.25+Z*.2,Vector((0,0,.83)),2.7)]:
    s.camera.location=target+direction.normalized()*3
    s.camera.rotation_euler=(target-s.camera.location).to_track_quat('-Z','Y').to_euler();s.camera.data.ortho_scale=scale
    s.render.filepath=str(root/(name+('-trial.png' if a.trial else '-previous.png')));bpy.ops.render.render(write_still=True)
    views[name]=dict(camera_matrix=[list(row) for row in s.camera.matrix_world],scale=scale,size=[s.render.resolution_x,s.render.resolution_y])
(root/'anatomical-cameras.json').write_text(json.dumps(views,indent=2))
print('Anatomical inspection views complete.',flush=True)
