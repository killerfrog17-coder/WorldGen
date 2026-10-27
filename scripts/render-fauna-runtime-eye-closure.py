"""Actual exported mesh/atlas comparison with the game's alpha threshold."""
import argparse,json,math,struct,sys
from pathlib import Path
import bpy,numpy as np
from mathutils import Matrix,Vector

repo=Path(__file__).resolve().parent.parent;root=repo/'artifacts/fauna-eye-closure-repair'
ap=argparse.ArgumentParser();ap.add_argument('--slugs',nargs='+',default=['raccoon','wolf','red-fox','buffalo','rabbit','deer']);a=ap.parse_args(sys.argv[sys.argv.index('--')+1:])
plan={r['slug']:r for r in json.loads((repo/'artifacts/fauna-runtime-20261026/export-plan.json').read_text())['animals']}
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=4;scene.cycles.use_denoising=False
scene.render.threads_mode='FIXED';scene.render.threads=2;scene.render.resolution_x=512;scene.render.resolution_y=512
scene.render.image_settings.file_format='PNG';scene.view_settings.view_transform='Standard'
scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.55,.02,.3,1);scene.world.node_tree.nodes['Background'].inputs[1].default_value=.8
camera=bpy.data.cameras.new('Eye closure audit');cam=bpy.data.objects.new('Eye closure audit',camera);scene.collection.objects.link(cam);scene.camera=cam;camera.type='ORTHO'
light=bpy.data.lights.new('Soft key','AREA');light.energy=150;light.shape='DISK';light.size=3
lamp=bpy.data.objects.new('Soft key',light);scene.collection.objects.link(lamp)
B=Matrix.Rotation(math.pi/2,4,'X')
out=root/'renders';out.mkdir(exist_ok=True)
for slug in a.slugs:
    source=Path(plan[slug]['path']);landpath=source.parent/'face-landmarks.json'
    if not landpath.exists():continue
    land=json.loads(landpath.read_text())
    current=repo/'artifacts/fauna-runtime-20261026/exports'/slug
    metadata=json.loads((current/'model.json').read_text())
    if 'source_to_runtime' not in metadata:print('SKIP_NOT_READY',slug,flush=True);continue
    C=Matrix(metadata['source_to_runtime']);eye=land['right_eye'];center=B@C@Vector(eye['point'])
    frame=eye['fit_frame'];normal=(B@C).to_3x3()@Vector(frame['normal']);normal.normalize()
    horizontal=(B@C).to_3x3()@Vector(frame['horizontal']);horizontal.normalize()
    radius=(C.to_3x3()@Vector((eye['radius'][0],0,0))).length
    for version,folder in [('before',root/'before'/slug),('after',current)]:
        if not (folder/'mesh.bin').exists():continue
        model=json.loads((folder/'model.json').read_text());raw=(folder/'mesh.bin').read_bytes();n,k,bc,pc=struct.unpack_from('<4i',raw,8)
        width=9+2*model['influences']+3*int(model.get('hasMorph',False));values=np.frombuffer(raw,'<f4',n*width,24).reshape(n,width)
        faces=np.frombuffer(raw,'<i4',k,24+n*width*4).reshape(-1,3)
        parts=values[:,8+2*model['influences']].astype(int);mask=model['coats'][0]['options'][0]['parts']
        visible=np.array([(mask[part//16]>>(part%16))&1 for part in parts],bool);faces=faces[visible[faces].all(1)]
        points=[B@Vector(p) for p in values[:,:3]]
        mesh=bpy.data.meshes.new(slug+' runtime');mesh.from_pydata(points,[],faces.tolist());mesh.update()
        obj=bpy.data.objects.new(slug+' runtime',mesh);scene.collection.objects.link(obj)
        uv=mesh.uv_layers.new(name='RuntimeUV')
        for loop in mesh.loops:uv.data[loop.index].uv=values[loop.vertex_index,6:8]
        for face in mesh.polygons:face.use_smooth=True
        material=bpy.data.materials.new(slug+' runtime pigment');material.use_nodes=True;mesh.materials.append(material)
        nodes=material.node_tree.nodes;links=material.node_tree.links;bs=nodes.get('Principled BSDF');bs.inputs['Roughness'].default_value=.65
        image=nodes.new('ShaderNodeTexImage');image.image=bpy.data.images.load(str(folder/'coat-0.png'),check_existing=True);links.new(image.outputs['Color'],bs.inputs['Base Color'])
        texture=nodes.new('ShaderNodeTexImage');texture.image=bpy.data.images.load(str(folder/'mask.png'),check_existing=True);texture.image.colorspace_settings.name='Non-Color'
        separate=nodes.new('ShaderNodeSeparateColor');links.new(texture.outputs['Color'],separate.inputs[0]);clip=nodes.new('ShaderNodeMath');clip.operation='GREATER_THAN';clip.inputs[1].default_value=.35;links.new(separate.outputs['Green'],clip.inputs[0]);links.new(clip.outputs[0],bs.inputs['Alpha'])
        for view,direction in [('side',normal),('oblique',(normal*.8+horizontal*.6).normalized()),('upper',(normal*.85+Vector((0,0,.5))).normalized())]:
            cam.location=center+direction*max(.3,radius*8);cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();camera.ortho_scale=radius*5.8
            lamp.location=cam.location+Vector((-.4,-.5,.7));lamp.rotation_euler=(center-lamp.location).to_track_quat('-Z','Y').to_euler()
            scene.render.filepath=str(out/f'{slug}-{version}-{view}.png');bpy.ops.render.render(write_still=True)
            print('RUNTIME_EYE_RENDER',slug,version,view,flush=True)
        bpy.data.objects.remove(obj,do_unlink=True);bpy.data.meshes.remove(mesh)
print('RUNTIME_EYE_RENDER_DONE',flush=True)
