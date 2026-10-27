"""Read-only original bind skeleton sidecars; never touches natives or bundles."""
import argparse,hashlib,json,math,sys,time
from pathlib import Path
import bpy
from mathutils import Matrix,Vector

repo=Path(__file__).resolve().parent.parent
ap=argparse.ArgumentParser();ap.add_argument('--slugs',nargs='*')
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
rows=json.loads((repo/'artifacts/fauna-runtime-20261026/export-plan.json').read_text())['animals']
dest=repo/'unity/WorldGen.Unity/Assets/StreamingAssets/fauna-rigs';dest.mkdir(exist_ok=True)
signature=hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
for row in rows:
    if a.slugs and row['slug'] not in a.slugs:continue
    target=dest/(row['slug']+'.json')
    if target.exists():
        old=json.loads(target.read_text())
        if old.get('sourceSha256')==row['source_sha256'] and old.get('exporterSha256')==signature:continue
    source=Path(row['path']);before=hashlib.sha256(source.read_bytes()).hexdigest();assert before==row['source_sha256']
    bpy.ops.wm.open_mainfile(filepath=str(source));scene=bpy.context.scene
    entry=json.loads(scene['deer_four_rigs'])[0];rig=bpy.data.objects[entry['rig']];body=bpy.data.objects[entry['body']]
    rig.animation_data_clear();rig.data.pose_position='REST'
    for bone in rig.pose.bones:bone.matrix_basis=Matrix.Identity(4)
    if body.data.shape_keys:
        for key in list(body.data.shape_keys.key_blocks)[1:]:key.value=0
    scene.frame_set(0);bpy.context.view_layer.update()
    forward=Vector(row.get('forward') or [0,1,0]);yaw=math.atan2(forward.x,forward.y)
    points=[body.matrix_world@Vector(p) for p in body.bound_box]
    low=Vector([min(p[i] for p in points) for i in range(3)]);high=Vector([max(p[i] for p in points) for i in range(3)])
    span=max(high-low);axis=Matrix(((1,0,0,0),(0,0,1,0),(0,1,0,0),(0,0,0,1)))
    C=axis@Matrix.Rotation(yaw,4,'Z')@Matrix.Scale(1/span,4)@Matrix.Translation(Vector((-(low.x+high.x)*.5,-(low.y+high.y)*.5,-low.z)))
    bones=list(rig.data.bones);ids={b.name:i for i,b in enumerate(bones)};counts=[0]*len(bones);radii=[[] for _ in bones]
    heads=[C@rig.matrix_world@b.head_local for b in bones];tails=[C@rig.matrix_world@b.tail_local for b in bones]
    for name in entry['objects']:
        ob=bpy.data.objects.get(name)
        if not ob or ob.type!='MESH':continue
        groups={g.index:ids[g.name] for g in ob.vertex_groups if g.name in ids}
        for v in ob.data.vertices:
            skin=[(groups[g.group],g.weight) for g in v.groups if g.group in groups and g.weight>0]
            if not skin:continue
            bi=max(skin,key=lambda p:p[1])[0];counts[bi]+=1
            p=C@ob.matrix_world@v.co;d=tails[bi]-heads[bi];t=max(0,min(1,(p-heads[bi]).dot(d)/max(1e-12,d.length_squared)))
            radii[bi].append((p-heads[bi]-t*d).length)
    out=[]
    for i,b in enumerate(bones):
        B=C@rig.matrix_world@b.matrix_local
        # Axis conversion reverses handedness. An orthonormal, right-handed
        # frame at the same head lets Unity rigid transforms reconstruct S*B.
        x=B.to_3x3().col[0].normalized();y=B.to_3x3().col[1].normalized();z=x.cross(y).normalized();x=y.cross(z).normalized()
        bind=Matrix(((x.x,y.x,z.x,heads[i].x),(x.y,y.y,z.y,heads[i].y),(x.z,y.z,z.z,heads[i].z),(0,0,0,1)))
        length=(tails[i]-heads[i]).length
        values=sorted(radii[i]);radius=values[int(len(values)*.55)] if values else .01
        radius=max(.006,min(.12,length*.4,radius*.75))
        name=b.name.lower();excluded=('eye','ear','finger','toe','antler','horn','jaw','tongue','claw')
        if row['slug'] not in ('python','cobra','viper','snake','earthworm','caterpillar'):excluded+=('tail',)
        small=any(k in name for k in excluded)
        physical=counts[i]>=8 and length>.025 and not small
        out.append({'name':b.name,'parent':ids[b.parent.name] if b.parent else -1,'bind':[float(v) for r in bind for v in r],
                    'head':list(heads[i]),'tail':list(tails[i]),'radius':radius,'length':length,'vertices':counts[i],'physical':physical})
    if not any(b['physical'] for b in out):
        # Tiny segmented guild rigs may have only very short tail-labelled bones.
        candidates=[b for b in out if b['vertices']>0]
        if candidates:max(candidates,key=lambda b:b['vertices'])['physical']=True
    data={'version':1,'slug':row['slug'],'sourceSha256':before,'exporterSha256':signature,'bones':out}
    target.write_text(json.dumps(data,separators=(',',':'))+'\n')
    assert hashlib.sha256(source.read_bytes()).hexdigest()==before
    print('RIG',row['slug'],len(out),sum(b['physical'] for b in out),flush=True)
print('RAGDOLL RIGS READY',flush=True)
