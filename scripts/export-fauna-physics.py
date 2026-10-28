"""Read original native bone rest frames, without saving/changing any animal.

This is supplementary physics metadata; existing mesh, pose and material bundles
remain byte-for-byte unchanged. Blender --background --disable-autoexec --threads 2.
"""
from pathlib import Path
import bpy,hashlib,json,time
from mathutils import Matrix

root=Path(__file__).resolve().parents[1]
task=root/'artifacts/overnight-items-20261028';task.mkdir(parents=True,exist_ok=True)
plan=json.loads((root/'artifacts/fauna-runtime-20261026/export-plan.json').read_text())['animals']
rows=[];started=time.monotonic()
for item in plan:
    source=Path(item['path']);digest=hashlib.sha256(source.read_bytes()).hexdigest()
    assert digest==item['source_sha256'],('Source changed',item['slug'])
    exported=json.loads((root/'artifacts/fauna-runtime-20261026/exports'/item['slug']/'model.json').read_text())
    bpy.ops.wm.open_mainfile(filepath=str(source),load_ui=False)
    entry=json.loads(bpy.context.scene['deer_four_rigs'])[0];rig=bpy.data.objects[entry['rig']]
    bones=list(rig.data.bones);assert len(bones)==exported['bones']
    ids={b.name:i for i,b in enumerate(bones)};C=Matrix(exported['source_to_runtime']);W=C@rig.matrix_world
    rows.append({'slug':item['slug'],'source':digest,'bones':[
        {'name':b.name,'parent':ids[b.parent.name] if b.parent else -1,
         'head':list(W@b.head_local),'tail':list(W@b.tail_local),
         'rest':[float(x) for row in (W@b.matrix_local) for x in row]} for b in bones]})
    assert hashlib.sha256(source.read_bytes()).hexdigest()==digest
    print('PHYSICS METADATA',item['slug'],len(bones),flush=True)
out=root/'unity/WorldGen.Unity/Assets/WorldGen/Resources/FaunaPhysicsSkeletons.json'
out.write_text(json.dumps({'species':rows},separators=(',',':')),encoding='utf-8')
(task/'physics-metadata-proof.json').write_text(json.dumps({'species':len(rows),'bones':sum(len(r['bones']) for r in rows),'all_source_hashes_preserved':True,'seconds':time.monotonic()-started},indent=2))
print('PHYSICS METADATA COMPLETE',len(rows),flush=True)
