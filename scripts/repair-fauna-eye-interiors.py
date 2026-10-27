"""Seal verified corner gaps on canonical selected natives, then refresh runtime pins."""
import hashlib,json,shutil,struct,sys
from pathlib import Path
import bpy,numpy as np
sys.path.insert(0,str(Path(__file__).resolve().parent))
from fauna_eye_socket_lining import seal
repo=Path(__file__).resolve().parent.parent;root=repo/'artifacts/fauna-eye-closure-repair';runtime=repo/'artifacts/fauna-runtime-20261026'
if (root/'native-lining-repairs.json').exists():
    previous=json.loads((root/'native-lining-repairs.json').read_text())['animals']
    for record in previous:
        path=repo/'artifacts/fauna-anatomy-revision'/record['slug']/'Animal - Painted Species Review.blend'
        assert hashlib.sha256(path.read_bytes()).hexdigest()==record['after_sha256'],'Previously repaired native changed; require a fresh scoped inspection'
    print('CANONICAL_SOCKET_INTERIORS_ALREADY_SEALED',len(previous),flush=True)
    raise SystemExit(0)
audit=json.loads((root/'runtime-ray-audit.json').read_text())['animals']
slugs=[a['slug'] for a in audit if a['stitched'] and any(f['radius']<=.8 for e in a['eyes'] for f in e['failures'])]
assert len(slugs)==10 and 'deer' not in slugs
inventory=json.loads((runtime/'native-inventory.json').read_text());plan=json.loads((runtime/'export-plan.json').read_text());baseline=json.loads((root/'baseline.json').read_text())
proofs=[];bpy.context.preferences.filepaths.save_version=0

def identity(entry):
    h=hashlib.sha256()
    for name in entry['objects']:
        ob=bpy.data.objects.get(name)
        if not ob or ob.type!='MESH':continue
        h.update(name.encode());h.update(np.array([v.co[:] for v in ob.data.vertices],'<f4').tobytes());h.update(json.dumps([list(p.vertices) for p in ob.data.polygons]).encode())
        h.update(json.dumps([[(ob.vertex_groups[g.group].name,g.weight) for g in v.groups] for v in ob.data.vertices]).encode())
        if ob.data.shape_keys:
            for key in ob.data.shape_keys.key_blocks:h.update(np.array([v.co[:] for v in key.data],'<f4').tobytes())
    rig=bpy.data.objects[entry['rig']]
    for bone in rig.data.bones:h.update(bone.name.encode());h.update(np.array(bone.matrix_local,'<f4').tobytes())
    for name in dict.fromkeys([*entry['actions'].values(),*entry.get('demos',{}).values()]):
        action=bpy.data.actions[name];h.update(name.encode())
        for layer in action.layers:
            for strip in layer.strips:
                for slot in action.slots:
                    bag=strip.channelbag(slot)
                    if bag:
                        for curve in bag.fcurves:h.update(curve.data_path.encode());h.update(np.array([p.co[:] for p in curve.keyframe_points],'<f4').tobytes())
    return h.hexdigest()

for slug in slugs:
    item=next(x for x in plan['animals'] if x['slug']==slug);path=Path(item['path']);folder=path.parent
    before_sha=hashlib.sha256(path.read_bytes()).hexdigest();backup=root/'native-before'/slug;backup.mkdir(parents=True,exist_ok=True);shutil.copy2(path,backup/path.name)
    raw=(runtime/'exports'/slug/'mesh.bin').read_bytes();old_indices=struct.unpack_from('<4i',raw,8)[1]
    old=next(x for x in baseline['animals'] if x['slug']==slug);old['original_index_count']=old_indices
    bpy.ops.wm.open_mainfile(filepath=str(path));scene=bpy.context.scene;entry=json.loads(scene['deer_four_rigs'])[0];original=json.loads(json.dumps(entry));before_identity=identity(original)
    land=json.loads((folder/'face-landmarks.json').read_text());land['anatomical_orbit']['sealed_socket_interior']=True
    added=seal(slug,land,bpy.data.objects[entry['rig']]);assert identity(original)==before_identity
    entry['objects']+=added;entry.setdefault('facial_objects',[]).extend(added);entry['eye_closure_repair']={'added_objects':added,'original_content_sha256':before_identity}
    scene['deer_four_rigs']=json.dumps([entry]);bpy.ops.wm.save_as_mainfile(filepath=str(path),compress=True)
    after_sha=hashlib.sha256(path.read_bytes()).hexdigest();item['source_sha256']=after_sha
    record=next(x for x in inventory['animals'] if x['slug']==slug);record['sha256']=after_sha;record['entry']=entry
    for name in added:
        ob=bpy.data.objects[name];points=[ob.matrix_world@__import__('mathutils').Vector(corner) for corner in ob.bound_box]
        record['meshes'].append({'name':name,'vertices':len(ob.data.vertices),'polygons':len(ob.data.polygons),'visible':True,'matrix':list(map(list,ob.matrix_world)),'bounds':[[min(p[i] for p in points) for i in range(3)],[max(p[i] for p in points) for i in range(3)]],'uvs':['UVMap'],'materials':[ob.data.materials[0].name],'armature':True,'shape_keys':[]})
        mat=ob.data.materials[0];bs=mat.node_tree.nodes.get('Principled BSDF');record['materials'][mat.name]={'nodes':[{'name':bs.name,'type':'BSDF_PRINCIPLED','inputs':{'Alpha':1.}}],'links':[],'diffuse':list(mat.diffuse_color)}
    (folder/'face-landmarks.json').write_text(json.dumps(land,indent=2))
    state_path=folder/'paint-state.json'
    if state_path.exists():
        state=json.loads(state_path.read_text());state.setdefault('facial_objects',[]).extend(added);state['facial_triangles']=sum(sum(len(f.vertices)-2 for f in bpy.data.objects[n].data.polygons) for n in entry['facial_objects']);state['eye_closure_repair']=entry['eye_closure_repair'];state_path.write_text(json.dumps(state,indent=2))
    proof={'slug':slug,'before_sha256':before_sha,'after_sha256':after_sha,'original_mesh_skin_shapes_rig_actions_exact':True,'original_content_sha256':before_identity,'added_objects':added,'added_triangles':192}
    (folder/'eye-closure-repair.json').write_text(json.dumps(proof,indent=2));proofs.append(proof)
    print('CANONICAL_EYE_INTERIOR_SEALED',slug,flush=True)
(runtime/'native-inventory.json').write_text(json.dumps(inventory,indent=2));(runtime/'export-plan.json').write_text(json.dumps(plan,indent=2));(root/'baseline.json').write_text(json.dumps(baseline,indent=2));(root/'native-lining-repairs.json').write_text(json.dumps({'animals':proofs},indent=2))
