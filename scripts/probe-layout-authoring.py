"""Read-only source-scene dependency check, run in Blender without scratch inputs."""
import bpy,hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parent.parent
out=ROOT/'artifacts/layout-verification';out.mkdir(parents=True,exist_ok=True)
inventory=json.loads((ROOT/'assets-source/animals/export-metadata/native-inventory.json').read_text(encoding='utf-8'))['animals']
deer=next(r for r in inventory if r['slug']=='deer')
sources=[(ROOT/deer['source'].replace('\\','/'),deer['sha256']),(ROOT/'assets-source/humans/base/Human - Animation Comparison.blend','0946d797f3c620492444f2ec65d2a88280053fbda2c882dde3dfaf826942d229')]
report=[]
for source,expected in sources:
    assert hashlib.sha256(source.read_bytes()).hexdigest()==expected
    bpy.ops.wm.open_mainfile(filepath=str(source))
    missing=[i.filepath for i in bpy.data.images if i.source=='FILE' and i.packed_file is None and not Path(bpy.path.abspath(i.filepath)).is_file()]
    linked=[l.filepath for l in bpy.data.libraries if not Path(bpy.path.abspath(l.filepath)).is_file()]
    assert not missing and not linked,(source,missing,linked)
    assert any(o.type=='ARMATURE' for o in bpy.data.objects) and any(o.type=='MESH' for o in bpy.data.objects)
    report.append({'source':source.relative_to(ROOT).as_posix(),'original_sha256':expected,'missing_images':missing,'missing_libraries':linked,'objects':len(bpy.data.objects)})
(out/'authoring-probe.json').write_text(json.dumps({'passed':True,'scenes':report},indent=2),encoding='utf-8')
print('ARTIFACT-FREE SOURCE SCENES PASS',len(report))
