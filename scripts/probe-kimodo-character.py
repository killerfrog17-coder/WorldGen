"""Read the existing human audition and record its bind rig for Kimodo fitting."""
import hashlib
import json
from pathlib import Path
import bpy

REPO = Path(__file__).resolve().parent.parent
ROOT = REPO / 'artifacts/kimodo-first-person-demo'
SOURCE = REPO / 'artifacts/human-animation-preview/Human - Animation Comparison.blend'
ROOT.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
report = {'source': str(SOURCE), 'sha256': hashlib.sha256(SOURCE.read_bytes()).hexdigest(), 'people': []}
for letter in 'ABCDEF':
    col = bpy.data.collections['MPFB_' + letter]
    rig = next(o for o in col.objects if o.type == 'ARMATURE')
    report['people'].append({
        'variant': letter, 'rig': rig.name, 'matrix': [list(row) for row in rig.matrix_world],
        'eye': list(col['head_center']),
        'objects': [{'name': o.name, 'type': o.type,
                     'vertices': len(o.data.vertices) if o.type == 'MESH' else 0,
                     'modifiers': [(m.name, m.type) for m in o.modifiers] if o.type == 'MESH' else [],
                     'materials': [m.name for m in o.data.materials] if o.type == 'MESH' else []}
                    for o in col.objects],
        'bones': {b.name: {'parent': b.parent.name if b.parent else None,
                           'head': list(b.head_local), 'tail': list(b.tail_local),
                           'matrix': [list(row) for row in b.matrix_local]}
                  for b in rig.data.bones},
    })
(ROOT / 'character-source.json').write_text(json.dumps(report, indent=2))
print('CURRENT HUMAN SOURCE READ ONLY', report['sha256'], flush=True)
print(json.dumps(report['people'][1], indent=2), flush=True)
