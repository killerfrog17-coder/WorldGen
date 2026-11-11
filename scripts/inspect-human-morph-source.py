"""Read-only inventory of the existing, licensed character-authoring source."""
import bpy, json
from pathlib import Path
root=Path(__file__).resolve().parents[1]
bpy.ops.wm.open_mainfile(filepath=str(root/'assets-source/humans/appearance-review/Human physique comparison.blend'))
rows=[]
for col in bpy.data.collections:
    if not col.name.endswith('_after'): continue
    row={'collection':col.name,'objects':[]}
    for obj in col.objects:
        if obj.type=='MESH':
            row['objects'].append({'name':obj.name,'type':obj.get('MPFB_GEN_object_type',getattr(obj,'MPFB_GEN_object_type','')),'vertices':len(obj.data.vertices),'body':bool(obj.get('physique_body')),'keys':list(obj.data.shape_keys.key_blocks.keys())[:8] if obj.data.shape_keys else [],'modifiers':[(m.name,m.type) for m in obj.modifiers]})
        if obj.type=='ARMATURE':row['bones']={b.name:list(b.head_local) for b in obj.data.bones if b.name in ['head','neck_01','pelvis','upperarm_l','thigh_l']}
    rows.append(row)
out=root/'artifacts/npc-ancestry-20261111';out.mkdir(exist_ok=True,parents=True)
(out/'source-inventory.json').write_text(json.dumps(rows,indent=2))
print('HUMAN SOURCE INVENTORY',len(rows),flush=True)
