"""Fresh unrigged deer: canonical alignment, symmetry, reduction and guides."""
import argparse
import json
from pathlib import Path
import sys
import bpy
import bmesh
import numpy as np
from mathutils import Vector

ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=a.root.resolve()
out=root/'Deer - Stage1 Shape.blend'
if out.exists():raise FileExistsError(out)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(root/'deer-raw.glb'))
objs=[o for o in bpy.context.scene.objects if o.type=='MESH']
bpy.ops.object.select_all(action='DESELECT')
for o in objs:o.select_set(True)
bpy.context.view_layer.objects.active=objs[0];bpy.ops.object.join();obj=bpy.context.object
obj.name='Deer body';obj.parent=None;bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
p=np.array([v.co[:] for v in obj.data.vertices]);p[:,2]-=p[:,2].min();p*=1.65/p[:,2].max()
torso=p[(p[:,2]>.7)&(p[:,2]<1.2)];head=p[p[:,2]>1.35]
_,vectors=np.linalg.eigh(np.cov(torso[:,:2].T));f=vectors[:,-1]
if (head[:,:2].mean(0)-torso[:,:2].mean(0))@f<0:f=-f
r=np.array([f[1],-f[0]]);p[:,:2]=p[:,:2]@np.stack([r,f],axis=1)
torso=p[(p[:,2]>.7)&(p[:,2]<1.2)]
p[:,0]-=(np.quantile(torso[:,0],.02)+np.quantile(torso[:,0],.98))*.5
p[:,1]-=(np.quantile(torso[:,1],.02)+np.quantile(torso[:,1],.98))*.5
for v,co in zip(obj.data.vertices,p):v.co=co
# Remove only tiny disconnected generation debris; retain substantial parts.
bm=bmesh.new();bm.from_mesh(obj.data);todo=set(bm.verts);groups=[]
while todo:
    seed=todo.pop();component={seed};stack=[seed]
    while stack:
        for e in stack.pop().link_edges:
            for v in e.verts:
                if v in todo:todo.remove(v);component.add(v);stack.append(v)
    groups.append(component)
largest=max(map(len,groups));junk=[v for g in groups if len(g)<max(30,largest*.0003) for v in g]
if junk:bmesh.ops.delete(bm,geom=junk,context='VERTS')
bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=1e-6,
    plane_co=(0,0,0),plane_no=(1,0,0),clear_inner=True,clear_outer=False)
bm.to_mesh(obj.data);bm.free()
mirror=obj.modifiers.new('Neutral bilateral anatomy','MIRROR');mirror.use_clip=True;mirror.merge_threshold=.0002
bpy.ops.object.modifier_apply(modifier=mirror.name)
tri_high=obj.modifiers.new('Triangulated generated surface','TRIANGULATE')
bpy.ops.object.modifier_apply(modifier=tri_high.name)
high=np.array([v.co[:] for v in obj.data.vertices]);high_faces=np.array([f.vertices[:] for f in obj.data.polygons])
np.savez_compressed(root/'symmetric-high-shape.npz',vertices=high,faces=high_faces)
dec=obj.modifiers.new('Game body budget','DECIMATE');dec.ratio=4800/len(obj.data.polygons)
dec.use_symmetry=True;dec.symmetry_axis='X';dec.use_collapse_triangulate=True
bpy.ops.object.modifier_apply(modifier=dec.name)
tri=obj.modifiers.new('Triangles','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=tri.name)
for face in obj.data.polygons:face.use_smooth=True
p=np.array([v.co[:] for v in obj.data.vertices]);faces=np.array([f.vertices[:] for f in obj.data.polygons])
np.savez_compressed(root/'body-low.npz',vertices=p,faces=faces)
mat=bpy.data.materials.new('Neutral anatomical inspection');mat.use_nodes=True
mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.28,.20,.13,1)
mat.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.8
obj.data.materials.clear();obj.data.materials.append(mat)
s=bpy.context.scene;s.render.engine='CYCLES';s.cycles.device='CPU';s.cycles.samples=16
s.render.threads_mode='FIXED';s.render.threads=3;s.render.resolution_x=1000;s.render.resolution_y=850;s.render.resolution_percentage=100
world=bpy.data.worlds.new('Review world');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.18,.20,.23,1);world.node_tree.nodes['Background'].inputs[1].default_value=.65;s.world=world
for name,location,power,size in [('Key',(3,4,5),600,4),('Fill',(-3,1,3),400,3),('Rim',(0,-4,4),500,3)]:
    data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=size
    light=bpy.data.objects.new(name,data);s.collection.objects.link(light);light.location=location
    light.rotation_euler=(Vector((0,0,.9))-light.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.004));floor=bpy.context.object;floor.name='Review floor'
fm=bpy.data.materials.new('Floor');fm.diffuse_color=(.17,.19,.21,1);floor.data.materials.append(fm)
camera=bpy.data.objects.new('Inspection camera',bpy.data.cameras.new('Inspection camera'));s.collection.objects.link(camera);s.camera=camera;camera.data.type='ORTHO'
upper=p[(p[:,2]>1.22)&(p[:,1]>0)];hc=(upper.min(0)+upper.max(0))*.5
views={}
for name,direction,target,span in [('side',(1,0,0),(0,0,.82),2.8),('front',(0,1,0),(0,0,.82),2.1),
    ('rear',(0,-1,0),(0,0,.82),2.1),('head',(1,0,0),hc,.75),('face',(0,1,0),hc,.75)]:
    target=Vector(target);camera.location=target+Vector(direction)*4;camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=span
    s.render.filepath=str(root/(name+'-shape.png'));bpy.ops.render.render(write_still=True)
    views[name]=dict(camera_matrix=[list(row) for row in camera.matrix_world],scale=span,size=[1000,850])
(root/'shape-cameras.json').write_text(json.dumps(views,indent=2))
(root/'shape-preparation.json').write_text(json.dumps(dict(body_triangles=len(faces),
    canonical_axis='X lateral, +Y forward, Z up',bilateral_symmetry=True,unrigged=True,height=1.65),indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(out));print('Unrigged deer shape prepared.',flush=True)
