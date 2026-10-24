"""Small file/control checks for the raw gallery; no art or motion acceptance."""
import argparse,json,runpy,sys,hashlib
from pathlib import Path
import bpy
import numpy as np

ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=a.root.resolve()
bpy.ops.wm.open_mainfile(filepath=str(root/'Deer - Four Generated Rigs.blend'))
models=json.loads(bpy.context.scene['deer_four_rigs']);assert len(models)==4
approved=models[0].get('approved_art');art_preserved=None
if approved:
    provenance=json.loads((root/'approved-input.json').read_text());source=Path(provenance['blend'])
    assert hashlib.sha256(source.read_bytes()).hexdigest()==provenance['blend_sha256']
    original=provenance['metadata'];names=[original['body'],*original['eyes'],original['whiskers'],*[x['object'] for x in original['antlers']]]
    with bpy.data.libraries.load(str(source),link=False) as (available,loaded):loaded.objects=names
    originals=dict(zip(names,loaded.objects))
    def coords(mesh):return np.array([v.co[:] for v in mesh.vertices])
    def same_art(obj,old):
        assert np.array_equal(coords(obj.data),coords(old.data))
        assert [list(p.vertices) for p in obj.data.polygons]==[list(p.vertices) for p in old.data.polygons]
        if old.data.uv_layers.active:
            assert np.array_equal(np.array([v.uv[:] for v in obj.data.uv_layers.active.data]),np.array([v.uv[:] for v in old.data.uv_layers.active.data]))
        else:assert obj.data.uv_layers.active is None
        assert np.array_equal(np.array(obj.matrix_world),np.array(old.matrix_world))
        if old.data.shape_keys:
            for key in old.data.shape_keys.key_blocks:
                assert np.array_equal(np.array([v.co[:] for v in obj.data.shape_keys.key_blocks[key.name].data]),np.array([v.co[:] for v in key.data]))
    for entry in models:
        info=entry['approved_art'];newnames=[info['body'],*info['eyes'],info['whiskers'],*[x['object'] for x in info['antlers']]]
        for newname,oldname in zip(newnames,names):same_art(bpy.data.objects[newname],originals[oldname])
        assert entry['mesh_file']==str(root/'approved-body.glb')
    for coat in original['coats']:assert coat['material'] in bpy.data.materials
    # Imported source images receive suffixes; compare packed source bytes to
    # gallery images through matching material image nodes (no render needed).
    for original_name,old in originals.items():
        gallery=bpy.data.objects[models[0]['objects'][names.index(original_name)]]
        for current_mat,old_mat in zip(gallery.data.materials,old.data.materials):
            if not old_mat.node_tree:
                assert current_mat.diffuse_color[:]==old_mat.diffuse_color[:]
                continue
            for node in old_mat.node_tree.nodes:
                if node.type=='TEX_IMAGE' and node.image:
                    image=current_mat.node_tree.nodes[node.name].image
                    assert image.size[:]==node.image.size[:]
                    if node.image.packed_file:assert image.packed_file.data==node.image.packed_file.data
    art_preserved={'source_hash_unchanged':True,'base_geometry_uv_shape_keys_transforms':True,'packed_active_maps_unchanged':True,'all_coats_retained':True}
    for old in originals.values():bpy.data.objects.remove(old,do_unlink=True)
hashes=[];rows=[]
for entry in models:
    r=bpy.data.objects[entry['rig']];b=bpy.data.objects[entry['body']]
    actual=np.array([r.data.bones[n].head_local[:] for n in entry['joint_names']])
    error=float(np.abs(actual-np.array(entry['generated_joint_heads'])).max());assert error<1e-5
    parents=[r.data.bones[n].parent.name if r.data.bones[n].parent else None for n in entry['joint_names']]
    expected=[entry['joint_names'][p] if p>=0 else None for p in entry['parents']];assert parents==expected
    for vertex in b.data.vertices:
        weights=[g.weight for g in vertex.groups if g.weight>1e-7]
        assert len(weights)<=4 and abs(sum(weights)-1)<1e-5
    hashes.append(hashlib.sha256(Path(entry['rig_file']).read_bytes()).hexdigest())
    rows.append({'model':entry['number'],'joints':entry['joints'],'generated_head_max_error_m':error,'triangles':len(b.data.polygons)})
assert len(set(hashes))==4
viewer=runpy.run_path(str(Path(__file__).with_name('deer-four-rigs-viewer.py')))
for index in range(4):
    assert bpy.ops.deer_four.model(number=index)=={'FINISHED'}
    assert sum(not bpy.data.objects[m['body']].hide_get() for m in models)==1
    assert bpy.ops.deer_four.skeleton()=={'FINISHED'} and not bpy.data.objects[models[index]['rig']].hide_get()
    assert bpy.ops.deer_four.skeleton()=={'FINISHED'} and bpy.data.objects[models[index]['rig']].hide_get()
    for label in ('Bind','Idle','Walk','Trot','Run','Eating','Sleep','lay_to_idle'):assert bpy.ops.deer_four.clip(clip=label)=={'FINISHED'}
for _ in range(3):assert bpy.ops.deer_four.view()=={'FINISHED'}
for _ in range(2):assert bpy.ops.deer_four.mode()=={'FINISHED'}
for _ in range(16 if approved else 1):
    assert bpy.ops.deer_four.species()=={'FINISHED'}
    if approved:
        active=[m for m in models if not bpy.data.objects[m['body']].hide_get()];assert len(active)==1
        for entry in models:
            if entry!=active[0]:assert all(bpy.data.objects[n].hide_get() for n in entry['objects'])
(root/'structural-checks.json').write_text(json.dumps({'models':rows,'distinct_rig_hashes':hashes,'approved_art_preserved':art_preserved,'controls_pass':True,'art_checked':False,'natural_motion_approved':False},indent=2))
print('Four independent skeletons and review controls verified; no art acceptance.',flush=True)
