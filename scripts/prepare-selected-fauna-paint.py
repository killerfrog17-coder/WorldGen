"""Start CPU anatomical colouring on an owner-selected raw animal copy.

Preserves the selected bind geometry, weights, hierarchy and motion clips.
Creates UVs and a working colour preview; facial placement and atlas finishing
remain explicitly pending. No new neural inference or owner-window changes.
"""
import argparse, hashlib, json, sys
from pathlib import Path
import bpy
import numpy as np
from mathutils import Vector

repo = Path(__file__).resolve().parent.parent
sys.path.insert(0,str(repo/'scripts'))
from fauna_paint_recipes import SUPPORTED, family, profiles as coat_profiles, fur_source
ap = argparse.ArgumentParser(); ap.add_argument('--slug', required=True)
ap.add_argument('--replace-working', action='store_true')
a = ap.parse_args(sys.argv[sys.argv.index('--')+1:])
assert a.slug in SUPPORTED,'No inspected body-family paint recipe yet'
choices = json.loads((repo/'docs/fauna/OWNER_SELECTIONS.json').read_text())['animals']
choice = choices[a.slug]; assert choice['model_selected'] and len(choice['selected_models']) == 1
number = choice['selected_models'][0]
source_root = repo/'artifacts/fauna-overnight-20261024'/a.slug
root = repo/'artifacts/fauna-painted-20261024'/a.slug; root.mkdir(parents=True, exist_ok=True)
out = root/'Animal - Painted Species Review.blend'
if out.exists():
    prior=json.loads((root/'paint-state.json').read_text())
    assert a.replace_working and prior['status']=='anatomical_colouring_started' and not prior['appearance_approved']
source = source_root/'Animal - Raw Audition.blend'
source_hash = hashlib.sha256(source.read_bytes()).hexdigest()
for filename, key in [('model.glb','model_sha256'), ('generated-rig.npz','rig_sha256')]:
    assert hashlib.sha256((source_root/f'candidate-{number}'/filename).read_bytes()).hexdigest() == choice[key]
bpy.ops.wm.open_mainfile(filepath=str(source)); s = bpy.context.scene
entries = json.loads(s['deer_four_rigs']); entry = entries[number-1]
body = bpy.data.objects[entry['body']]; rig = bpy.data.objects[entry['rig']]
for row in entries:
    if row is entry: continue
    for name in (row['body'], row['rig']): bpy.data.objects.remove(bpy.data.objects[name], do_unlink=True)
allowed = set(entry['actions'].values()) | set(entry['demos'].values())
for action in list(bpy.data.actions):
    if action.name not in allowed: bpy.data.actions.remove(action)
rig.animation_data_clear(); rig.data.pose_position = 'REST'
body.hide_set(False); body.hide_render = False; rig.hide_set(True)
p = np.array([v.co[:] for v in body.data.vertices], dtype=np.float32)
faces = np.array([f.vertices[:] for f in body.data.polygons], dtype=np.int32)
geometry_hash = hashlib.sha256(p.tobytes()+faces.tobytes()).hexdigest()
names = entry['joint_names']; weights = np.zeros((len(p),len(names)),np.float32)
groups = {g.index:g.name for g in body.vertex_groups}
for v in body.data.vertices:
    for g in v.groups:
        if groups[g.group] in names: weights[v.index,names.index(groups[g.group])] = g.weight
normal = np.array([v.normal[:] for v in body.data.vertices],np.float32)
bpy.ops.object.select_all(action='DESELECT'); body.hide_set(False); body.select_set(True)
bpy.context.view_layer.objects.active = body
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=1.151917, island_margin=.012, area_weight=.1, correct_aspect=True, scale_to_bounds=True)
bpy.ops.object.mode_set(mode='OBJECT')
uv = np.array([x.uv[:] for x in body.data.uv_layers.active.data],np.float32).reshape(-1,3,2)
np.savez_compressed(root/'paint-anatomy.npz', vertices=p, normals=normal, faces=faces, uv=uv, weights=weights,
                    bone_names=np.array(names), joint_heads=np.array(entry['generated_joint_heads']), parents=np.array(entry['parents']))

def smooth(low,high,x):
    t=np.clip((x-low)/(high-low),0,1); return t*t*(3-2*t)

# Working masks use continuous rest coordinates; final face masks are fitted
# separately. One gradient crosses torso and throat, avoiding a neck colour seam.
head = np.array(entry['generated_joint_heads'][names.index('Head')]); center_x = float(np.median(p[:,0]))
if family(a.slug)=='canid':
    # Raw rig role names can be imperfect; cosmetic anatomy follows geometry,
    # without changing that selected rig or its animation tracks.
    skull=p[(p[:,1]>.65)&(p[:,2]>1.16)&(p[:,2]<1.45)]
    assert len(skull)>12
    head=np.median(skull,axis=0);head[1]-=.035
torso = p[(p[:,1] < head[1]-.12) & (p[:,2]>.35) & (p[:,2]<head[2]+.1)]
belly_z = float(np.quantile(torso[:,2],.32))
belly = (1-smooth(belly_z-.12,belly_z+.22,p[:,2])) * (.45+.55*smooth(.25,-.55,normal[:,2]))
head_area = smooth(head[1]-.2,head[1]+.05,p[:,1])*smooth(head[2]-.35,head[2]-.1,p[:,2])
ear_base = float(head[2]+.10)
ears = smooth(ear_base,ear_base+.15,p[:,2])
inner = ears*smooth(.08,.65,normal[:,1])
tip = ears*smooth(float(p[:,2].max()-.16),float(p[:,2].max()-.04),p[:,2])
head_points = p[(head_area>.4) & (p[:,2]<ear_base+.05)]
nose = head_points[np.argmax(head_points[:,1])]
nose_mask = np.exp(-(((p[:,0]-nose[0])/.042)**2+((p[:,1]-nose[1])/.04)**2+((p[:,2]-nose[2])/.033)**2)*1.8)*head_area
tail = smooth(-.58,-.72,p[:,1]) * smooth(.20,.04,np.abs(p[:,0]-center_x))

profiles=coat_profiles(a.slug)
fur_path=repo/fur_source(a.slug)
if not fur_path.exists():fur_path=repo/'artifacts/fauna-assets/deer/fur-source.png'
fur = bpy.data.images.load(str(fur_path), check_existing=True); fur.pack()
coats=[]
for index,(label,base,pale,dark_tips) in enumerate(profiles):
    colour = np.tile(base,(len(p),1)); pale = np.array(pale)
    def blend(mask,target):
        return colour*(1-mask[:,None])+np.asarray(target)*mask[:,None]
    colour = blend(belly,pale)
    chin = head_area*smooth(head[2]+.03,head[2]-.14,p[:,2])*.55
    colour = blend(chin,pale)
    colour = blend(tail*.8,pale)
    colour = blend(inner*.8,[.45,.31,.27])
    if dark_tips: colour = blend(tip*.95,[.055,.050,.045])
    colour = blend(nose_mask,[.14,.105,.095])
    if 'mottled' in label:
        m=(.5+.5*np.sin(p[:,1]*21+np.sin(p[:,2]*15)*2))*smooth(.2,.8,normal[:,2])*.16
        colour = blend(m,[.14,.11,.08])
    attribute=body.data.color_attributes.new(name=f'Painted coat {index}',type='FLOAT_COLOR',domain='CORNER')
    linear=np.where(colour<=.04045,colour/12.92,((colour+.055)/1.055)**2.4)
    fur_weight=np.clip(1-nose_mask-inner*.5,0,1)
    corner=np.array([loop.vertex_index for loop in body.data.loops]);rgba=np.c_[linear[corner],fur_weight[corner]]
    attribute.data.foreach_set('color',rgba.astype(np.float32).ravel())
    mat=bpy.data.materials.new(f'{entry["animal"]} coat {index} - {label}');mat.use_nodes=True;mat.use_fake_user=True
    nodes=mat.node_tree.nodes;links=mat.node_tree.links;bsdf=nodes.get('Principled BSDF');bsdf.inputs['Roughness'].default_value=.82
    attr=nodes.new('ShaderNodeVertexColor');attr.layer_name=attribute.name
    tex=nodes.new('ShaderNodeTexImage');tex.image=fur;tex.projection='BOX';tex.projection_blend=.3
    coord=nodes.new('ShaderNodeTexCoord');scale=nodes.new('ShaderNodeVectorMath');scale.operation='SCALE';scale.inputs[3].default_value=3.5
    grey=nodes.new('ShaderNodeRGBToBW');detail=nodes.new('ShaderNodeMath');detail.operation='MULTIPLY_ADD';detail.inputs[1].default_value=1.1;detail.inputs[2].default_value=.70
    mul=nodes.new('ShaderNodeMixRGB');mul.blend_type='MULTIPLY';mul.inputs[0].default_value=1
    links.new(coord.outputs['Generated'],scale.inputs[0]);links.new(scale.outputs[0],tex.inputs['Vector']);links.new(tex.outputs['Color'],grey.inputs[0]);links.new(grey.outputs[0],detail.inputs[0]);links.new(detail.outputs[0],mul.inputs[2]);links.new(attr.outputs['Color'],mul.inputs[1]);links.new(mul.outputs[0],bsdf.inputs['Base Color'])
    bump=nodes.new('ShaderNodeBump');bump.inputs['Distance'].default_value=.003
    strength=nodes.new('ShaderNodeMath');strength.operation='MULTIPLY';strength.inputs[1].default_value=.22
    roughness=nodes.new('ShaderNodeMath');roughness.operation='MULTIPLY_ADD';roughness.inputs[1].default_value=.35;roughness.inputs[2].default_value=.5
    links.new(attr.outputs['Alpha'],strength.inputs[0]);links.new(strength.outputs[0],bump.inputs['Strength']);links.new(grey.outputs[0],bump.inputs['Height']);links.new(bump.outputs['Normal'],bsdf.inputs['Normal'])
    links.new(attr.outputs['Alpha'],roughness.inputs[0]);links.new(roughness.outputs[0],bsdf.inputs['Roughness'])
    coats.append({'label':label,'material':mat.name,'working_vertex_colour':attribute.name})
body.data.materials.clear();body.data.materials.append(bpy.data.materials[coats[0]['material']])
entry.update(selected_raw_candidate=number,painting_coats=coats,painted=True,appearance_approved=False)
entry['paint_head_vertex']=int(np.argmin(np.linalg.norm(p-head,axis=1)))
s['deer_four_rigs']=json.dumps([entry]);s['fauna_painting']=json.dumps({'source_candidate':number,'source_root':str(source_root),'stage':'Anatomical colouring working pass; facial fitting and atlas bake pending','coats':coats})
s['fauna_animal']=entry['animal'];s.frame_set(0)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(out))
assert hashlib.sha256(np.array([v.co[:] for v in body.data.vertices],np.float32).tobytes()+np.array([f.vertices[:] for f in body.data.polygons],np.int32).tobytes()).hexdigest()==geometry_hash
assert hashlib.sha256(source.read_bytes()).hexdigest()==source_hash
state={'slug':a.slug,'source_candidate':number,'source_blend_sha256':source_hash,'geometry_sha256':geometry_hash,'source_model_sha256':choice['model_sha256'],'source_rig_sha256':choice['rig_sha256'],'status':'anatomical_colouring_started','painting_started':True,'appearance_approved':False,'motion_approved':False,'coats':coats,'landmarks_pending':True,'nose_seed':nose.tolist(),'head_seed':head.tolist(),'center_x':center_x,'belly_z':belly_z,'ear_base':ear_base,'uv_added_without_geometry_change':True,'fur_source':str(fur_path.relative_to(repo)),'fur_sha256':hashlib.sha256(fur_path.read_bytes()).hexdigest()}
(root/'paint-state.json').write_text(json.dumps(state,indent=2)+'\n')
print('ANATOMICAL COLOURING STARTED',a.slug,number,len(coats),'coats; geometry/rig/motions retained',flush=True)
