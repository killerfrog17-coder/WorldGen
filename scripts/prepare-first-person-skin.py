"""Appearance study only: map existing MPFB skin UVs onto a native CC0 FPS rig.
Never alter the approved human or animal sources. No runtime retarget/IK involved.
"""
from pathlib import Path
import bpy,bmesh,math,json,hashlib
from mathutils import Matrix,Vector
root=Path(__file__).resolve().parent.parent;task=root/'artifacts/first-person-rebuild-20261030';output=root/'assets/first-person';output.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(root/'assets/first-person/source/Arms.fbx'));scene=bpy.context.scene
fps=next(o for o in bpy.data.objects if o.type=='ARMATURE');arms=next(o for o in bpy.data.objects if o.type=='MESH');fps.animation_data_clear();fps.data.pose_position='REST'
original_actions=list(bpy.data.actions)
for a in original_actions:a.use_fake_user=True
before=set(bpy.data.objects)
source=root/'artifacts/human-runtime/exports/human-f/human-f.fbx';digest=hashlib.sha256(source.read_bytes()).hexdigest();bpy.ops.import_scene.fbx(filepath=str(source));imported=set(bpy.data.objects)-before
human=next(o for o in imported if o.type=='ARMATURE');human.animation_data_clear();human.data.pose_position='REST'
for p in human.pose.bones:p.matrix_basis=Matrix.Identity(4)
body=next(o for o in imported if o.type=='MESH' and 'body' in o.name);bpy.context.view_layer.update()
rest={b.name:human.matrix_world@b.matrix_local for b in human.data.bones};dest={b.name:fps.matrix_world@b.matrix_local for b in fps.data.bones}
mp={};palettes={}
def vec(m):return m.translation
for side,refside in [('l','L'),('r','R')]:
 for prefix,other in [('upperarm','UpperArm'),('lowerarm','LowerArm'),('clavicle','Shoulder')]:mp[prefix+'_'+side]=other+'.'+refside
 mp['hand_'+side]='Wrist.'+refside
 for finger,ref in [('index','Index'),('middle','Middle'),('ring','Ring'),('pinky','Pinky'),('thumb','Thumb')]:
  for i in range(1,4):mp[f'{finger}_{i:02d}_{side}']=f'{ref}{i if finger=="thumb" else i+1}.{refside}'
def palm_frame(points,wrist,middle,index,pinky):
 origin=points[wrist].translation;y=(points[middle].translation-origin).normalized();x=points[index].translation-points[pinky].translation;x=(x-y*x.dot(y)).normalized();z=x.cross(y).normalized();x=y.cross(z).normalized();m=Matrix((x,y,z)).transposed().to_4x4();m.translation=origin;return m,(points[middle].translation-origin).length
for name,other in mp.items():
 old=rest[name];new=dest[other];bone=human.data.bones[name];ref=fps.data.bones[other]
 oldvec=human.matrix_world.to_3x3()@(bone.tail_local-bone.head_local);newvec=fps.matrix_world.to_3x3()@(ref.tail_local-ref.head_local)
 align=oldvec.rotation_difference(newvec).to_matrix().to_4x4();rotation=align@old.copy();rotation.translation=new.translation
 radial=1.18 if name.startswith(('upperarm','lowerarm')) else 1.08
 scale=Matrix.Diagonal((radial,newvec.length/oldvec.length,radial,1));palettes[name]=rotation@scale@old.inverted()
for side,refside in [('l','L'),('r','R')]:
 old,ol=palm_frame(rest,'hand_'+side,'middle_01_'+side,'index_01_'+side,'pinky_01_'+side)
 new,nl=palm_frame(dest,'Wrist.'+refside,'Middle2.'+refside,'Index2.'+refside,'Pinky2.'+refside)
 palettes['hand_'+side]=new@Matrix.Diagonal((1.05,nl/ol,1.05,1))@old.inverted()
world=body.matrix_world.copy();inverse=world.inverted();used=[]
for v in body.data.vertices:
 influences=[(body.vertex_groups[g.group].name,g.weight) for g in v.groups if body.vertex_groups[g.group].name in palettes];total=sum(w for _,w in influences);used.append(total)
 if total>.1:
  point=world@v.co;v.co=inverse@sum((palettes[n]@point*w/total for n,w in influences),Vector())
# Delete unrelated anatomy before spatial transfer, keeping original per-loop UV data.
mesh=bmesh.new();mesh.from_mesh(body.data);mesh.verts.ensure_lookup_table();bmesh.ops.delete(mesh,geom=[v for v in mesh.verts if used[v.index]<=.1],context='VERTS');mesh.to_mesh(body.data);mesh.free();body.data.update()
for o in bpy.context.selected_objects:o.select_set(False)
arms.select_set(True);bpy.context.view_layer.objects.active=arms
# Bake appearance per texel onto a new UV atlas, so source UV island boundaries
# cannot interpolate across unrelated parts of the original body map.
scene.render.engine='CYCLES';scene.cycles.samples=8
arms.data.uv_layers.new(name='ArmSkin');arms.data.uv_layers.active_index=len(arms.data.uv_layers)-1;arms.data.uv_layers.active.active_render=True
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.012);bpy.ops.object.mode_set(mode='OBJECT')
original=bpy.data.images.load(str(root/'artifacts/human-runtime/exports/human-f/body.png'))
donor_mat=bpy.data.materials.new('Read-only UV donor');donor_mat.use_nodes=True;nodes=donor_mat.node_tree.nodes;nodes.clear();out=nodes.new('ShaderNodeOutputMaterial');em=nodes.new('ShaderNodeEmission');texture=nodes.new('ShaderNodeTexImage');texture.image=original;donor_mat.node_tree.links.new(texture.outputs['Color'],em.inputs['Color']);donor_mat.node_tree.links.new(em.outputs[0],out.inputs['Surface']);body.data.materials.clear();body.data.materials.append(donor_mat)
for modifier in list(body.modifiers):body.modifiers.remove(modifier)
mat=bpy.data.materials.new('Baked original skin');mat.use_nodes=True;bs=mat.node_tree.nodes['Principled BSDF'];bs.inputs['Roughness'].default_value=.58
image=bpy.data.images.new('FPS arm skin F',width=2048,height=2048);image.generated_color=(.52,.35,.24,1);tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=image;mat.node_tree.nodes.active=tex;mat.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color']);arms.data.materials.clear();arms.data.materials.append(mat)
# Nearest-surface correspondence is robust to the donor's different hand shape.
# Rasterise target UVs then sample donor UVs per texel, never across UV seams.
import sys
sys.path.append(str(root/'artifacts/first-person-rebuild-20261030/python-packages'))
import numpy as np,time
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform
clock=time.perf_counter();body.data.calc_loop_triangles();arms.data.calc_loop_triangles()
donor_vertices=[world@v.co for v in body.data.vertices];donor_triangles=[tuple(t.vertices) for t in body.data.loop_triangles];donor_uv=body.data.uv_layers.active.data
bvh=BVHTree.FromPolygons(donor_vertices,donor_triangles,all_triangles=True)
source_pixels=np.array(original.pixels[:],dtype=np.float32).reshape(original.size[1],original.size[0],4)
N=2048;correspondence=np.zeros((N,N,2),np.float32);pixels=np.zeros((N,N,4),np.float32);filled=np.zeros((N,N),bool);uv_data=arms.data.uv_layers.active.data
for it,triangle in enumerate(arms.data.loop_triangles):
 uv=np.array([uv_data[i].uv[:] for i in triangle.loops]);xy=uv*(N-1);lo=np.maximum(0,np.floor(xy.min(axis=0)).astype(int));hi=np.minimum(N-1,np.ceil(xy.max(axis=0)).astype(int))
 xx,yy=np.meshgrid(np.arange(lo[0],hi[0]+1),np.arange(lo[1],hi[1]+1));a,b,c=xy;den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
 if abs(den)<1e-8:continue
 u=((b[1]-c[1])*(xx-c[0])+(c[0]-b[0])*(yy-c[1]))/den;v=((c[1]-a[1])*(xx-c[0])+(a[0]-c[0])*(yy-c[1]))/den;w=1-u-v;inside=(u>=-1e-5)&(v>=-1e-5)&(w>=-1e-5)
 vx=np.array([(arms.matrix_world@arms.data.vertices[i].co)[:] for i in triangle.vertices]);coords=u[inside,None]*vx[0]+v[inside,None]*vx[1]+w[inside,None]*vx[2];col=[]
 for point in coords:
  pos,normal,index,distance=bvh.find_nearest(Vector(point));ids=donor_triangles[index];t=body.data.loop_triangles[index];a1,b1,c1=[donor_vertices[i] for i in ids];uv1,uv2,uv3=[Vector((*donor_uv[i].uv,0)) for i in t.loops];mapped=barycentric_transform(pos,a1,b1,c1,uv1,uv2,uv3)
  sx=min(original.size[0]-1,max(0,int(mapped.x*(original.size[0]-1))));sy=min(original.size[1]-1,max(0,int(mapped.y*(original.size[1]-1))));col.append((mapped.x,mapped.y))
 correspondence[yy[inside],xx[inside]]=col;filled[yy[inside],xx[inside]]=True
 if it%1000==0:print('UV raster',it,len(arms.data.loop_triangles),'seconds',time.perf_counter()-clock,flush=True)
# Dilation through nearest filled texel prevents dark mip seams.
for dilation in range(24):
 old=filled.copy()
 for axis,shift in [(0,1),(0,-1),(1,1),(1,-1)]:
  edge=np.roll(old,shift,axis=axis);edge[0 if shift>0 else -1,:]=False if axis==0 else edge[0 if shift>0 else -1,:]
  if axis==1:edge[:,0 if shift>0 else -1]=False
  mask=(~filled)&edge;correspondence[mask]=np.roll(correspondence,shift,axis=axis)[mask];filled[mask]=True
np.savez_compressed(task/'arm-skin-correspondence.npz',uv=correspondence,filled=filled)
# One anatomical correspondence, six original pigments; bilinear source sampling.
for variant in 'abcdef':
 source_image=bpy.data.images.load(str(root/f'artifacts/human-runtime/exports/human-{variant}/body.png'),check_existing=False);data=np.array(source_image.pixels[:],np.float32).reshape(source_image.size[1],source_image.size[0],4)
 x=np.clip(correspondence[:,:,0]*(source_image.size[0]-1),0,source_image.size[0]-1);y=np.clip(correspondence[:,:,1]*(source_image.size[1]-1),0,source_image.size[1]-1);ix=x.astype(np.int32);iy=y.astype(np.int32);jx=np.minimum(ix+1,source_image.size[0]-1);jy=np.minimum(iy+1,source_image.size[1]-1);fx=(x-ix)[:,:,None];fy=(y-iy)[:,:,None];pixels=(data[iy,ix]*(1-fx)+data[iy,jx]*fx)*(1-fy)+(data[jy,ix]*(1-fx)+data[jy,jx]*fx)*fy;pixels[~filled]=(.5,.32,.22,1)
 skin=bpy.data.images.new('First person skin '+variant.upper(),width=N,height=N);skin.pixels.foreach_set(pixels.astype(np.float32).ravel());skin.update();skin.filepath_raw=str(output/(variant.upper()+'.png'));skin.file_format='PNG';skin.save()
 if variant=='f':tex.image=skin
 bpy.data.images.remove(source_image)
print('SIX SKIN VARIANTS BAKED',time.perf_counter()-clock,flush=True)
# Only the new atlas survives export. Explicit UV node prevents accidental use of
# the source palette layout after removing layers.
for layer in list(arms.data.uv_layers):
 if layer.name!='ArmSkin':arms.data.uv_layers.remove(layer)
arms.data.uv_layers['ArmSkin'].active_render=True
uv_node=mat.node_tree.nodes.new('ShaderNodeUVMap');uv_node.uv_map='ArmSkin';mat.node_tree.links.new(uv_node.outputs['UV'],tex.inputs['Vector'])
for polygon in arms.data.polygons:polygon.use_smooth=True
sub=arms.modifiers.new('Close-up silhouette','SUBSURF');sub.levels=1;sub.render_levels=1
for obj in imported:bpy.data.objects.remove(obj,do_unlink=True)
fps.data.pose_position='POSE';fps.animation_data_create();fps.animation_data.action=next(a for a in original_actions if a.name.endswith('Sword_Idle'))
scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True;scene.render.resolution_x=960;scene.render.resolution_y=720;scene.render.resolution_percentage=100;scene.view_settings.view_transform='AgX'
scene.world=bpy.data.worlds.new('Neutral review');scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.16,.18,.21,1);scene.world.node_tree.nodes['Background'].inputs[1].default_value=.5
for i,(loc,power,size) in enumerate([((1,-1,2),200,2),((-1,0,1),100,2),((0,2,1),80,2)]):
 data=bpy.data.lights.new('Review softbox '+str(i),'AREA');data.energy=power;data.shape='DISK';data.size=size;o=bpy.data.objects.new(data.name,data);scene.collection.objects.link(o);o.location=loc;o.rotation_euler=(-o.location).to_track_quat('-Z','Y').to_euler()
data=bpy.data.cameras.new('FPS camera');cam=bpy.data.objects.new('FPS camera',data);scene.collection.objects.link(cam);scene.camera=cam;cam.location=(0,0,0);data.sensor_fit='VERTICAL';data.sensor_height=32;data.lens=32/(2*math.tan(math.radians(60)/2));data.clip_start=.01;cam.rotation_euler=Vector((0,-1,0)).to_track_quat('-Z','Y').to_euler()

for m in list(bpy.data.materials):
 if m.users==0:bpy.data.materials.remove(m)
bpy.data.orphans_purge(do_recursive=True)
bpy.ops.file.pack_all();bpy.ops.wm.save_as_mainfile(filepath=str(output/'FirstPersonBase.blend'),compress=True)
assert hashlib.sha256(source.read_bytes()).hexdigest()==digest
print('FIRST-PERSON SKIN BASE READY',flush=True)
