"""Build an isolated native Blender review of actual Mesh2Motion exports.

No motion synthesis, old skin transfer, live foot IK or game changes. Offline
renders/checks expose fit/deformation problems before owner review.
"""
import argparse
import hashlib
import json
from pathlib import Path
import sys
import bpy
import numpy as np
from mathutils import Vector


def bind(rig,action):
    rig.animation_data_clear()
    rig.animation_data_create().action=action
    rig.animation_data.action_slot=action.slots[0]


def main():
    ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True)
    ap.add_argument('--render',action='store_true');ap.add_argument('--default',default='fitted')
    ap.add_argument('--render-mode',default='all')
    a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=a.root.resolve()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene=bpy.context.scene;scene.render.fps=30;scene.render.fps_base=1
    variants={};checks=[]
    for mode in ('template','limbs','aligned','fitted'):
        before=set(bpy.data.objects);old_actions=set(bpy.data.actions)
        bpy.ops.import_scene.gltf(filepath=str(root/f'Deer-Mesh2Motion-{mode}.glb'))
        objects=set(bpy.data.objects)-before
        rigs=[o for o in objects if o.type=='ARMATURE']
        meshes=[o for o in objects if o.type=='MESH' and any(m.type=='ARMATURE' for m in o.modifiers)]
        assert len(rigs)==len(meshes)==1,(mode,len(rigs),len(meshes))
        rig=rigs[0];mesh=meshes[0];rig.name=f'Deer rig {mode}';mesh.name=f'Deer mesh {mode}'
        rig.animation_data_clear()
        for b in rig.pose.bones:b.matrix_basis.identity()
        actions={}
        for action in set(bpy.data.actions)-old_actions:
            # Import names may carry a .001 suffix because all variants coexist.
            name=action.name.rsplit('.',1)[0] if action.name.rsplit('.',1)[-1].isdigit() else action.name
            action.name=f'{mode} | {name}';action.use_fake_user=True;actions[name]=action.name
        collection=bpy.data.collections.new(f'Mesh2Motion {mode}');scene.collection.children.link(collection)
        for obj in objects:
            for old in list(obj.users_collection):old.objects.unlink(obj)
            collection.objects.link(obj)
        variants[mode]=dict(rig=rig.name,mesh=mesh.name,objects=[o.name for o in objects],actions=actions)
        assert len(actions)==14,(mode,actions)
        # Bind pose geometry; authored animation is inspected independently.
        rig.data.pose_position='REST';bpy.context.view_layer.update()
        rest=np.array([mesh.matrix_world@v.co for v in mesh.data.vertices])
        rig.data.pose_position='POSE'
        group_counts=np.array([len([g for g in v.groups if g.weight>1e-6]) for v in mesh.data.vertices])
        sums=np.array([sum(g.weight for g in v.groups) for v in mesh.data.vertices])
        assert group_counts.max()<=4 and np.max(np.abs(sums-1))<1e-4
        checks.append(dict(mode=mode,vertices=len(mesh.data.vertices),triangles=len(mesh.data.polygons),
            bones=len(rig.data.bones),animations=len(actions),max_influences=int(group_counts.max()),
            max_weight_sum_error=float(np.max(np.abs(sums-1))),rest_min=rest.min(0).tolist(),
            clips=[]))
        for name in ('Idle','Walk','Trot','Run','Eating','Sleep','lay_to_idle','Death'):
            action=bpy.data.actions[actions[name]];bind(rig,action)
            low=[];high=[];max_step=0.;previous=None
            for f in range(int(action.frame_range[0]),int(action.frame_range[1])+1):
                scene.frame_set(f)
                evaluated=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get())
                vv=np.array([evaluated.matrix_world@v.co for v in evaluated.data.vertices])
                assert np.isfinite(vv).all()
                low.append(float(vv[:,2].min()));high.append(float(vv[:,2].max()))
                if previous is not None:max_step=max(max_step,float(np.linalg.norm(vv-previous,axis=1).max()))
                previous=vv
            checks[-1]['clips'].append(dict(name=name,frames=list(action.frame_range),
                lowest=min(low),highest=max(high),floor_range=max(low)-min(low),max_vertex_step=max_step))
        for obj in objects:obj.hide_set(True);obj.hide_render=True
    # Compact studio: CPU renders, comfortable material-preview GUI.
    world=bpy.data.worlds.new('Deer review world');scene.world=world;world.use_nodes=True
    world.node_tree.nodes['Background'].inputs[0].default_value=(.35,.35,.35,1)
    world.node_tree.nodes['Background'].inputs[1].default_value=.6
    for location,power,size in [((3,-4,5),450,4),((-3,-1,3),250,3),((0,4,4),400,3)]:
        bpy.ops.object.light_add(type='AREA',location=location)
        light=bpy.context.object;light.data.energy=power;light.data.shape='DISK';light.data.size=size
        light.rotation_euler=(Vector((-.2,0,.8))-light.location).to_track_quat('-Z','Y').to_euler()
    bpy.ops.object.camera_add(location=(3.4,-4.6,1.9));scene.camera=bpy.context.object
    scene.camera.rotation_euler=(Vector((-.2,0,.8))-scene.camera.location).to_track_quat('-Z','Y').to_euler()
    scene.camera.data.type='ORTHO';scene.camera.data.ortho_scale=2.65
    bpy.ops.mesh.primitive_plane_add(size=20,location=(0,0,-.005));floor=bpy.context.object
    floor.name='Review ground';mat=bpy.data.materials.new('Review floor');mat.use_nodes=True
    mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(.11,.12,.13,1)
    mat.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.9;floor.data.materials.append(mat)
    scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=12
    scene.render.threads_mode='FIXED';scene.render.threads=3
    scene.render.resolution_x=800;scene.render.resolution_y=600;scene.render.resolution_percentage=100
    scene.view_settings.view_transform='AgX'
    scene['mesh2motion_variants']=json.dumps(variants);scene['mesh2motion_root']=str(root)
    scene['mesh2motion_default']=a.default
    def select(mode,name,frame):
        for key,info in variants.items():
            for n in info['objects']:
                obj=bpy.data.objects[n];hidden=key!=mode or n!=info['mesh']
                obj.hide_set(hidden);obj.hide_render=hidden
        info=variants[mode];rig=bpy.data.objects[info['rig']]
        rig.hide_set(True);rig.hide_render=True
        action=bpy.data.actions[info['actions'][name]];bind(rig,action)
        scene.frame_start=int(action.frame_range[0]);scene.frame_end=int(action.frame_range[1]);scene.frame_set(frame)
    if a.render:
        for mode in variants:
            if a.render_mode!='all' and mode!=a.render_mode:continue
            for name,time in [('Idle',0),('Walk',.25),('Run',.20),('Eating',1.3)]:
                select(mode,name,round(time*30));scene.render.filepath=str(root/f'{mode}-{name}.png')
                bpy.ops.render.render(write_still=True)
    select(a.default,'Walk',0)
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                space=area.spaces.active;space.overlay.show_overlays=False;space.show_region_ui=True
                space.shading.type='MATERIAL';space.shading.use_scene_world=False
                space.region_3d.view_location=(-.2,0,.8);space.region_3d.view_distance=3.6
                space.region_3d.view_rotation=Vector((3.4,-4.6,1.1)).to_track_quat('Z','Y')
    out=root/'Deer - Mesh2Motion Review.blend'
    bpy.ops.wm.save_as_mainfile(filepath=str(out),check_existing=False)
    (root/'native-checks.json').write_text(json.dumps(checks,indent=2))
    print('Mesh2Motion Blender review ready.',flush=True)


if __name__=='__main__':main()
