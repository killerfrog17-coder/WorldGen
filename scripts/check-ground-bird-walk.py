"""Measure actual whole-leg motion after a manual bird correspondence repair."""
import argparse,json,sys
from pathlib import Path
import bpy,numpy as np

p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True)
a=p.parse_args(sys.argv[sys.argv.index('--')+1:]);root=a.root.resolve()
bpy.ops.wm.open_mainfile(filepath=str(root/'Animal - Raw Audition.blend'))
scene=bpy.context.scene;config=json.loads((root/'manual-ground-walk.json').read_text());results=[]
for e in json.loads(scene['deer_four_rigs']):
    if str(e['number']) not in config['candidates']:continue
    rig=bpy.data.objects[e['rig']];body=bpy.data.objects[e['body']]
    act=bpy.data.actions[e['actions']['Walk']];rig.animation_data_create().action=act;rig.animation_data.action_slot=act.slots[0]
    bones=[];floors=[];skin=[]
    for frame in np.linspace(*act.frame_range,25):
        scene.frame_set(int(frame),subframe=float(frame-int(frame)));bpy.context.view_layer.update()
        matrices=np.array([np.array(rig.pose.bones[n].matrix) for n in e['joint_names']]);assert np.isfinite(matrices).all()
        bones.append(matrices[:,:3,3])
        obj=body.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=obj.to_mesh()
        vertices=np.array([v.co[:] for v in mesh.vertices]);assert np.isfinite(vertices).all();floors.append(float(vertices[:,2].min()));skin.append(vertices);obj.to_mesh_clear()
    bones=np.array(bones);legs=[]
    for leg in config['candidates'][str(e['number'])]['legs']:
        foot=leg['foot'];span=np.ptp(bones[:,foot],axis=0)
        assert span[1]>.02,'Foot still lacks a visible fore/aft stride'
        legs.append({'foot_joint':foot,'foot_xyz_range_m':span.tolist(),'driven_leg_joints':leg['nodes'][1:]})
    results.append({'candidate':e['number'],'legs':legs,'sampled_surface_floor_min_m':min(floors),
                    'surface_max_displacement_m':float(np.linalg.norm(np.array(skin)-skin[0],axis=2).max()),'finite':True})
report={'candidates':results,'sample_count_per_walk':25,'art_approved':False,'foot_contact_or_collision_free_claim':False,
        'method':'Explicit full leg chains, fixed-length planar IK of CC0 foot tracks, localized leg/rigid toe skin; unchanged learned rest heads and source bank.'}
(root/'manual-walk-checks.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
