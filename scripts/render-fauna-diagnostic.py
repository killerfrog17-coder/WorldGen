"""Small CPU render for pipeline diagnostics or the optional final screenshot pass."""
import argparse,json,sys
from pathlib import Path
import bpy,numpy as np
from mathutils import Vector

ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True);ap.add_argument('--model',type=int,default=1);ap.add_argument('--clip',default='Bind');ap.add_argument('--phase',type=float,default=.5);ap.add_argument('--view',choices=['side','front','rear','oblique','top'],default='side');ap.add_argument('--raw-mesh',type=Path)
ap.add_argument('--blend-name',default='Animal - Raw Audition.blend');ap.add_argument('--head',action='store_true');ap.add_argument('--size',type=int,default=448)
ap.add_argument('--coat',type=int,default=0)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=a.root.resolve()
if a.raw_mesh:
    bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(a.raw_mesh.resolve()));s=bpy.context.scene
    body=max((o for o in s.objects if o.type=='MESH'),key=lambda o:len(o.data.vertices));a.clip='Raw'
    mat=bpy.data.materials.new('Diagnostic neutral');mat.diffuse_color=(.4,.38,.34,1);body.data.materials.clear();body.data.materials.append(mat)
else:
    assert Path(a.blend_name).name==a.blend_name
    bpy.ops.wm.open_mainfile(filepath=str(root/a.blend_name));s=bpy.context.scene;entries=json.loads(s['deer_four_rigs']);e=entries[a.model-1]
    for row in entries:
        obj=bpy.data.objects[row['body']];obj.hide_set(row!=e);obj.hide_render=row!=e
    r=bpy.data.objects[e['rig']];r.animation_data_clear()
    if a.clip=='Bind':r.data.pose_position='REST'
    else:
        r.data.pose_position='POSE';action=bpy.data.actions[e['actions'][a.clip]];r.animation_data_create().action=action;r.animation_data.action_slot=action.slots[0]
        first,last=action.frame_range;x=first+(last-first)*a.phase;s.frame_set(int(x),subframe=x-int(x))
    body=bpy.data.objects[e['body']]
    if e.get('painting_coats'):
        assert 0<=a.coat<len(e['painting_coats'])
        for name in [e['body'],*e.get('coat_objects',[])]:
            bpy.data.objects[name].data.materials[0]=bpy.data.materials[e['painting_coats'][a.coat]['material']]
bpy.context.view_layer.update();evaluated=body.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=evaluated.to_mesh();p=np.array([evaluated.matrix_world@v.co for v in mesh.vertices]);evaluated.to_mesh_clear()
center=Vector(((p.min(0)+p.max(0))*.5).tolist());span=float(np.ptp(p,axis=0).max());directions={'side':(1,0,.08),'front':(0,1,.08),'rear':(0,-1,.08),'oblique':(1,1,.25),'top':(0,0,1)}
if a.head and not a.raw_mesh:
    if e.get('paint_head_vertex') is not None:
        ev=body.evaluated_get(bpy.context.evaluated_depsgraph_get());head=np.array(ev.matrix_world@ev.data.vertices[e['paint_head_vertex']].co)
    else:head=np.array(r.matrix_world@r.pose.bones['Head'].head)
    center=Vector((head+np.array([0,.08,.035])).tolist());span=.72
camera=bpy.data.objects.new('Diagnostic camera',bpy.data.cameras.new('Diagnostic camera'));s.collection.objects.link(camera);camera.location=center+Vector(directions[a.view]).normalized()*span*2;camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=span*1.22;s.camera=camera
for pos,power,size in [((3,4,5),650,5),((-3,1,3),350,4),((0,-4,4),450,3)]:
    light=bpy.data.lights.new('Diagnostic softbox','AREA');light.energy=power;light.shape='DISK';light.size=size;obj=bpy.data.objects.new(light.name,light);s.collection.objects.link(obj);obj.location=center+Vector(pos);obj.rotation_euler=(center-obj.location).to_track_quat('-Z','Y').to_euler()
world=bpy.data.worlds.new('Diagnostic background');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.19,.21,.23,1);world.node_tree.nodes['Background'].inputs[1].default_value=.65;s.world=world
s.render.engine='CYCLES';s.cycles.device='CPU';s.cycles.samples=6;s.cycles.use_denoising=True;s.render.resolution_x=a.size;s.render.resolution_y=a.size;s.render.resolution_percentage=100
dest=root/'previews';dest.mkdir(exist_ok=True);suffix=('-head' if a.head else '')+(f'-coat{a.coat}' if a.coat else '');s.render.filepath=str(dest/f'{a.model}-{a.clip}-{a.view}{suffix}.png');s.render.image_settings.file_format='PNG';bpy.ops.render.render(write_still=True)
if a.head:(dest/f'{a.model}-{a.clip}-{a.view}{suffix}.json').write_text(json.dumps({'camera_matrix':[list(row) for row in camera.matrix_world],'scale':camera.data.ortho_scale,'size':[a.size,a.size],'blend':a.blend_name},indent=2))
print('CPU diagnostic saved',s.render.filepath,flush=True)
