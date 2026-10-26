"""Read-only ray/UV/material probe of an actual evaluated appearance view."""
import argparse,json,sys
from pathlib import Path
import bpy,numpy as np
from mathutils import Matrix,Vector
from mathutils.bvhtree import BVHTree
repo=Path(__file__).resolve().parent.parent;ap=argparse.ArgumentParser();ap.add_argument('--slug',required=True);a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=repo/'artifacts/fauna-anatomy-revision'/a.slug;bpy.ops.wm.open_mainfile(filepath=str(root/'Animal - Painted Species Review.blend'));s=bpy.context.scene;e=json.loads(s['deer_four_rigs'])[0];body=bpy.data.objects[e['body']];rig=bpy.data.objects[e['rig']];rig.animation_data_clear();rig.data.pose_position='REST'
coat=e['painting_coats'][0];choices=coat.get('appendage_variants');variant=choices[0] if choices else {}
if e.get('body_morph_traits') and body.data.shape_keys:
 for key,meta in e['body_morph_traits'].items():body.data.shape_keys.key_blocks[meta['shape_key']].value=variant.get('body_morphs',{}).get(key,0.)
bpy.context.view_layer.update();ev=body.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh();mesh.calc_loop_triangles();faces=[list(t.vertices) for t in mesh.loop_triangles];bvh=BVHTree.FromPolygons([ev.matrix_world@v.co for v in mesh.vertices],faces,all_triangles=True);g=next(x for x in json.loads((root/'appearance-options/receipt.json').read_text())['renders'] if x['coat']==0 and x['view']=='side');M=Matrix(g['camera_matrix']);rows=[]
images=[]
for node in body.data.materials[0].node_tree.nodes:
 if node.type=='TEX_IMAGE':images.append({'name':node.image.name,'path':node.image.filepath,'size':list(node.image.size)})
for pixel in [[220,181],[250,181],[150,210],[350,383],[180,388]]:
 x,y=pixel;origin=M@Vector(((x/512-.5)*g['scale'],(.5-y/512)*g['scale'],0));direction=-(M.to_3x3()@Vector((0,0,1)));hit,normal,tri,_=bvh.ray_cast(origin,direction,20)
 if hit is None:continue
 face=mesh.loop_triangles[tri];world=np.array([ev.matrix_world@mesh.vertices[i].co for i in face.vertices]);v0,v1,v2=world;A=np.c_[v1-v0,v2-v0];bc=np.linalg.lstsq(A,np.array(hit)-v0,rcond=None)[0];w=np.r_[1-bc.sum(),bc];uv=np.array([mesh.uv_layers['Fauna Paint UV'].data[i].uv[:] for i in face.loops]);q=w@uv;im=next(n.image for n in body.data.materials[0].node_tree.nodes if n.type=='TEX_IMAGE' and 'coat-0-colour' in n.image.filepath);ix,iy=np.clip(np.rint(q*np.array(im.size)).astype(int),0,np.array(im.size)-1);offset=(int(iy)*im.size[0]+int(ix))*4;colour=list(im.pixels[offset:offset+3]);rows.append({'pixel':pixel,'world_point':list(hit),'normal':list(normal),'triangle':int(tri),'uv':q.tolist(),'packed_image_rgb':colour,'image':im.name,'original_uv':(w@np.array([mesh.uv_layers['UVMap'].data[i].uv[:] for i in face.loops])).tolist()})

for row in rows:
 for tag,token in [('roughness','coat-roughness'),('normal','coat-normal')]:
  im=next(n.image for n in body.data.materials[0].node_tree.nodes if n.type=='TEX_IMAGE' and token in n.image.filepath);ix,iy=np.clip(np.rint(np.mod(row['original_uv'],1)*np.array(im.size)).astype(int),0,np.array(im.size)-1);offset=(int(iy)*im.size[0]+int(ix))*4;row[tag+'_pixel']=list(im.pixels[offset:offset+3])
report={'body_matrix':[list(v) for v in body.matrix_world],'images':images,'shape_keys':{k.name:k.value for k in body.data.shape_keys.key_blocks} if body.data.shape_keys else {},'probes':rows};(root/'evaluated-surface-probes.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report))
ev.to_mesh_clear()
camera=bpy.data.objects.new('Evaluated albedo probe',bpy.data.cameras.new('Evaluated albedo probe'));s.collection.objects.link(camera);camera.data.type='ORTHO';camera.data.ortho_scale=g['scale'];camera.matrix_world=M;s.camera=camera;s.render.engine='BLENDER_EEVEE_NEXT';s.eevee.taa_render_samples=16;s.render.resolution_x=s.render.resolution_y=512;s.render.resolution_percentage=100;s.render.image_settings.file_format='PNG'
for mat in bpy.data.materials:
 if not mat.use_nodes:continue
 nodes=mat.node_tree.nodes;links=mat.node_tree.links;bs=next((n for n in nodes if n.type=='BSDF_PRINCIPLED'),None);out=next((n for n in nodes if n.type=='OUTPUT_MATERIAL'),None)
 if bs and out:
  em=nodes.new('ShaderNodeEmission');em.inputs['Color'].default_value=bs.inputs['Base Color'].default_value
  if bs.inputs['Base Color'].is_linked:links.new(bs.inputs['Base Color'].links[0].from_socket,em.inputs['Color'])
  links.new(em.outputs[0],out.inputs['Surface'])
s.render.filepath=str(root/'body-albedo-probe.png');bpy.ops.render.render(write_still=True)
