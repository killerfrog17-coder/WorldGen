"""Describe actual preview geometry and sample its native motion; no game changes."""
import json,sys,hashlib
from pathlib import Path
import bpy
import numpy as np
REPO=Path(__file__).resolve().parent.parent;ROOT=REPO/'assets-source/humans/appearance-review'
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'Human physique comparison.blend'))
records=[]
for col in bpy.data.collections:
    if col.name not in [a+'_'+b for a in 'ABCDEF' for b in ['before','after']]:continue
    col.hide_viewport=False
    rig=next(o for o in col.objects if o.type=='ARMATURE')
    body=next(o for o in col.objects if o.type=='MESH' and o.get('physique_body'))
    data={}
    for role,clip in json.loads(rig['clips']).items():
        action=bpy.data.actions[clip['action']];rig.animation_data.action=action;rig.animation_data.action_slot=action.slots[0]
        samples=[]
        for f in sorted({1,max(1,clip['frames']//4),max(1,clip['frames']//2),max(1,clip['frames']*3//4),clip['frames']}):
            bpy.context.scene.frame_set(f);bpy.context.view_layer.update()
            obj=body.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=obj.to_mesh()
            coords=np.array([tuple(v.co) for v in mesh.vertices]);assert np.isfinite(coords).all()
            samples.append(dict(frame=f,vertices=len(coords)));obj.to_mesh_clear()
        data[role]=samples
    uv_check=None
    if col.name.endswith('_after'):
        flow=body.data.uv_layers.get('BodyHairFlow');assert flow and len(flow.data)==len(body.data.loops)
        flow_values=np.array([tuple(p.uv) for p in flow.data]);assert np.isfinite(flow_values).all()
        before=next(o for o in bpy.data.collections[col.name.replace('_after','_before')].objects if o.get('physique_body'))
        original_uv=next(u for u in body.data.uv_layers if u.name!='BodyHairFlow')
        source_uv=next(u for u in before.data.uv_layers if u.name!='BodyHairFlow')
        a=np.array([tuple(p.uv) for p in original_uv.data]);b=np.array([tuple(p.uv) for p in source_uv.data])
        assert np.array_equal(a,b),'Original skin UV changed: '+col.name
        assert original_uv.active_render,'Hair UV took over the skin UV'
        assert not any(o.get('strand_count') for o in col.objects),'Unexpected strand geometry'
        uv_check=dict(flow_uv='finite',original_skin_uv='bit-exact before/after',extra_hair_geometry=False)
    records.append(dict(person=col.name,clips=data,bones=len(rig.data.bones),body_vertices=len(body.data.vertices),hair_uv=uv_check))
original=REPO/'assets-source/humans/base/Human - Animation Comparison.blend'
sha=hashlib.sha256(original.read_bytes()).hexdigest();assert sha=='0946d797f3c620492444f2ec65d2a88280053fbda2c882dde3dfaf826942d229'
(ROOT/'audit.json').write_text(json.dumps(dict(original_appearance_source_sha256=sha,people=records,scope='Preview mechanical audit only; rendered images reviewed separately. No runtime game changes.'),indent=2))
print('PREVIEW_AUDIT_OK',len(records),flush=True)
