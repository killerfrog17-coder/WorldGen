"""Own low-poly microfauna and deliberately simple flap/hover preview loops.

No texture painting, generated texture or complex hand-authored animal gait.
These tiny visual proxies use a wing-envelope animation, not resolved200Hz beats.
"""
import argparse,json,math,sys
from pathlib import Path
import bpy,numpy as np
from mathutils import Vector,Quaternion

ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True);ap.add_argument('--kind',choices=['butterfly','firefly','bee','dragonfly','fly'],required=True);ap.add_argument('--animal',required=True)
ap.add_argument('--rebuild-unreviewed',action='store_true')
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=a.root.resolve();root.mkdir(parents=True,exist_ok=True);out=root/'Animal - Raw Audition.blend';rebuild=out.exists()
if rebuild:
    assert a.rebuild_unreviewed and not json.loads((root/'audition.json').read_text()).get('art_approved')
    assert root.parent==Path(__file__).resolve().parent.parent/'artifacts/fauna-overnight-20261024'
bpy.ops.wm.read_factory_settings(use_empty=True);scene=bpy.context.scene;entries=[]

def material(name,colour,alpha=1,emission=False):
    m=bpy.data.materials.new(name);m.use_nodes=True;n=m.node_tree.nodes;l=m.node_tree.links;b=n.get('Principled BSDF')
    hsv=n.new('ShaderNodeHueSaturation');hsv.name='Review coat variation';hsv.inputs['Color'].default_value=(*colour,1);l.new(hsv.outputs[0],b.inputs['Base Color'])
    b.inputs['Roughness'].default_value=.6;b.inputs['Alpha'].default_value=alpha
    if emission:
        b.inputs['Emission Color'].default_value=(*colour,1)
        # Match the60-frame preview period, with a smooth dark loop boundary.
        driver=b.inputs['Emission Strength'].driver_add('default_value').driver;driver.expression='6*max(0,sin(frame*0.10471975511965977))**4'
    return m

for number in range(1,5):
    v=[];f=[];groups=[];mats=[]
    bodymat=material(f'{a.kind} neutral chitin {number}',(.22,.18,.12));wingmat=material(f'{a.kind} unpainted wing {number}',(.58,.55,.47),.7 if a.kind!='butterfly' else 1)
    eyemat=material(f'{a.kind} eyes {number}',(.025,.025,.025));eyemat.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.16
    glowmat=material(f'{a.kind} luminous abdomen {number}',(.65,1,.12),emission=True)
    def face(ids,mat=0):f.append(ids);mats.append(mat)
    def point(p,bone):v.append(p);groups.append(bone);return len(v)-1
    def ellipsoid(center,scale,bone,mat=0,segments=8,rings=4):
        start=len(v)
        for j in range(rings+1):
            angle=math.pi*j/rings
            for k in range(segments):
                phi=math.tau*k/segments;point(tuple(center[d]+scale[d]*[math.sin(angle)*math.cos(phi),math.sin(angle)*math.sin(phi),math.cos(angle)][d] for d in range(3)),bone)
        for j in range(rings):
            for k in range(segments):
                aa=start+j*segments+k;bb=start+j*segments+(k+1)%segments;cc=start+(j+1)*segments+k;dd=start+(j+1)*segments+(k+1)%segments
                if j>0:face([aa,bb,cc],mat)
                if j<rings-1:face([bb,dd,cc],mat)
    def tube(p,q,r,bone,mat=0,sides=4):
        p=Vector(p);q=Vector(q);axis=(q-p).normalized();side=axis.cross(Vector((0,0,1)))
        if side.length<.1:side=axis.cross(Vector((1,0,0)))
        side.normalize();up=axis.cross(side);start=len(v)
        for end in (p,q):
            for k in range(sides):point(tuple(end+r*(math.cos(k*math.tau/sides)*side+math.sin(k*math.tau/sides)*up)),bone)
        for k in range(sides):face([start+k,start+(k+1)%sides,start+sides+k],mat);face([start+(k+1)%sides,start+sides+(k+1)%sides,start+sides+k],mat)
    def panel(outline,side,bone,mat=1):
        center=np.mean(np.array(outline),axis=0);c=point((side*center[0],center[1],.83),bone);ids=[point((side*x,y,.83),bone) for x,y in outline]
        for k in range(len(ids)):face([c,ids[k],ids[(k+1)%len(ids)]] if side>0 else [c,ids[(k+1)%len(ids)],ids[k]],mat)
    head_y=.52;body_length=.62
    if a.kind=='butterfly':
        head_y=.23
        # One small body with two short antennae. No oversized head/eyes/legs.
        thickness=.048 if number==4 else .036
        ellipsoid((0,0,.83),(thickness,.25,thickness),'Pelvis',segments=8,rings=4)
        for side in (-1,1):tube((side*.012,.21,.85),(side*.045,.33,.87),.0025,'Head',sides=3)
    elif a.kind=='bee':
        head_y=.28
        ellipsoid((0,0,.83),(.11+(number-1)*.005,.26,.10),'Pelvis',segments=6,rings=3)
        ellipsoid((0,.28,.84),(.08,.09,.075),'Head',segments=6,rings=3)
        for side in (-1,1):
            tube((side*.025,.34,.87),(side*.05,.42,.89),.0025,'Head',sides=3)
            for j in range(3):
                tube((side*.075,.14-j*.12,.79),(side*.15,.10-j*.12,.71),.0025,'Pelvis',sides=3)
    elif a.kind=='firefly':
        head_y=.34
        # The rear half of the actual body glows, not an extra tiny tail bulb.
        width=.078+(number-1)*.006
        ellipsoid((0,.12,.83),(.105,.18,.085),'Pelvis')
        ellipsoid((0,.34,.84),(.08,.10,.07),'Head',segments=6,rings=3)
        ellipsoid((0,-.33,.82),(width,.27,.07),'Pelvis',3,segments=8,rings=4)
        for side in (-1,1):
            tube((side*.025,.40,.87),(side*.055,.51,.89),.003,'Head',sides=3)
            for j in range(3):
                p=(side*.06,.22-j*.12,.79);q=(side*.16,.12-j*.12,.71);r=(side*.20,.16-j*.13,.68)
                tube(p,q,.003,'Pelvis',sides=3);tube(q,r,.0025,'Pelvis',sides=3)
    else:
        ellipsoid((0,.12,.83),(.095,.19,.09),'Pelvis');ellipsoid((0,.43,.84),(.10,.105,.10),'Head')
        ellipsoid((0,-body_length*.5,.82),(.065 if a.kind!='bee' else .13,body_length*.55,.065 if a.kind!='bee' else .11),'Pelvis')
    for side in (-1,1):
        if a.kind in ('butterfly','firefly','bee'):continue
        ellipsoid((side*.075,.47,.87),(.035,.045,.04),'Head',2,6,3)
        p=(side*.04,.50,.91);q=(side*.15,.80 if a.kind=='butterfly' else .64,.94);tube(p,q,.005,'Head')
        if a.kind=='butterfly':ellipsoid(q,(.014,.022,.014),'Head',0,6,3)
        for j in range(3):
            p=(side*.06,.22-j*.13,.79);q=(side*(.20+j*.015),.12-j*.12,.70);r=(side*(.30+j*.02),.16-j*.14,.65)
            tube(p,q,.005,'Pelvis');tube(q,r,.004,'Pelvis')
    wing_names=['Wing_L','Wing_R']+(['Hindwing_L','Hindwing_R'] if a.kind=='dragonfly' else [])
    for side,suffix in [(-1,'L'),(1,'R')]:
        if a.kind=='butterfly':
            if number==1:
                outline=[(.035,.12),(.32,.63),(.70,.76),(1.02,.48),(.97,.10),(.85,-.15),(.67,-.39),(.36,-.45),(.10,-.27),(.035,-.16)]
            elif number==2:
                outline=[(.035,.12),(.40,.65),(1.03,.82),(1.06,.47),(.91,.04),(.76,-.12),(.73,-.31),(.52,-.38),(.43,-.77),(.35,-.39),(.14,-.30),(.035,-.16)]
            elif number==3:
                outline=[(.035,.12),(.27,.43),(.63,.65),(.84,.52),(.94,.25),(.82,-.04),(.72,-.30),(.47,-.46),(.20,-.35),(.035,-.16)]
            else:
                outline=[(.035,.17),(.43,.48),(1.20,.52),(1.02,.05),(.76,-.18),(.48,-.32),(.10,-.23),(.035,-.12)]
            # One connected fore/hind silhouette per side, moving as one panel.
            panel(outline,side,'Wing_'+suffix)
        elif a.kind=='dragonfly':
            length=.88+(number-1)*.08
            panel([(.07,.23),(.25,.33),(length,.43),(length+.06,.31),(.40,.16)],side,'Wing_'+suffix)
            panel([(.07,.06),(.27,.10),(length*.95,-.08),(length,-.20),(.34,-.08)],side,'Hindwing_'+suffix)
        else:
            length=(.35 if a.kind=='bee' else .43)+(number-1)*(.015 if a.kind=='bee' else .035)
            panel([(.06,.19),(.22,.39),(length,.40),(length+.10,.25),(length*.8,.05),(.08,.02)],side,'Wing_'+suffix)
    if a.kind=='dragonfly':
        ellipsoid((0,-.60,.82),(.035,.45,.035),'Pelvis')
    mesh=bpy.data.meshes.new(f'{a.kind} own geometry {number}');mesh.from_pydata(v,[],f);mesh.validate();mesh.update()
    body=bpy.data.objects.new(f'{a.animal} {number}',mesh);scene.collection.objects.link(body)
    for m in (bodymat,wingmat,eyemat,glowmat):mesh.materials.append(m)
    for poly,mat in zip(mesh.polygons,mats):poly.material_index=mat;poly.use_smooth=mat!=1
    hinge=(.04,.12) if a.kind in ('butterfly','firefly') else (.07,.18)
    names=['Pelvis','Head',*wing_names];parents=[-1]+[0]*(len(names)-1);joints=[(0,0,.83),(0,head_y,.84),(-hinge[0],hinge[1],.83),(hinge[0],hinge[1],.83)]
    if len(wing_names)==4:joints += [(-.07,.04,.83),(.07,.04,.83)]
    arm=bpy.data.armatures.new(f'{a.kind} simple rig {number}');rig=bpy.data.objects.new(f'{a.kind} rig {number}',arm);scene.collection.objects.link(rig)
    bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
    for i,name in enumerate(names):
        b=arm.edit_bones.new(name);b.head=joints[i];b.tail=Vector(joints[i])+Vector((0,.15,0))
        if parents[i]>=0:b.parent=arm.edit_bones[names[parents[i]]]
    bpy.ops.object.mode_set(mode='OBJECT');rig.select_set(False);rig.show_in_front=True
    for name in names:
        ids=[i for i,g in enumerate(groups) if g==name]
        vg=body.vertex_groups.new(name=name)
        if ids:vg.add(ids,1,'REPLACE')
    mod=body.modifiers.new('Simple insect rig','ARMATURE');mod.object=rig;body.parent=rig
    actions={}
    for label in ['Idle','Flight','Perch','Showcase','Locomotion']:
        action=bpy.data.actions.new(f'{a.kind} {number} | {label}');action.use_fake_user=True;rig.animation_data_clear();rig.animation_data_create().action=action
        for bone in rig.pose.bones:bone.rotation_mode='QUATERNION';bone.matrix_basis.identity()
        for frame in range(61):
            t=frame/30;flight=label in ['Flight','Showcase','Locomotion']
            rootbone=rig.pose.bones['Pelvis'];rootbone.location=(.025*math.sin(t*math.tau),0,.025*math.sin(t*math.pi)) if flight else (0,0,0);rootbone.keyframe_insert('location',frame=frame)
            for wing in wing_names:
                side=-1 if wing.endswith('_L') else 1
                amplitude=math.radians(60 if a.kind=='butterfly' else 25);frequency=6 if a.kind=='butterfly' else 10
                angle=side*(amplitude*math.sin(t*math.tau*frequency+(.25 if 'Hind' in wing else 0))) if flight else side*math.radians(68 if a.kind=='butterfly' else 8)
                bone=rig.pose.bones[wing];bone.rotation_quaternion=Quaternion((0,1,0),angle);bone.keyframe_insert('rotation_quaternion',frame=frame)
        actions[label]=action.name
    rig.animation_data_clear();demos={k:actions.pop(k) for k in ['Showcase','Locomotion']}
    entry={'number':number,'animal':a.animal,'rig':rig.name,'body':body.name,'head':'Head','actions':actions,'demos':demos,'material':bodymat.name,'joints':len(names),'triangles':len(mesh.polygons),'parents':parents,'joint_names':names,'generated_joint_heads':joints,'source_family':'own simple microfauna','geometry_method':'Own procedural shape audition; no borrowed animal mesh','motion_method':'Own wing-envelope/hover preview; user authorized simple microfauna animation','painted':False}
    if a.kind in ('butterfly','firefly','bee'):
        entry['revision']={'butterfly':'Owner revision: small body/tiny antennae/joined wing pair','firefly':'Owner revision: simple body/luminous rear half','bee':'Owner revision: particle-like compact body/short appendages'}[a.kind]
        entry['visible_wing_panels']=2;entry['luminous_body_length_fraction']=.5 if a.kind=='firefly' else 0
    entries.append(entry);body.hide_set(number!=1);body.hide_render=number!=1;rig.hide_set(True)
scene['deer_four_rigs']=json.dumps(entries);scene['fauna_animal']=a.animal;scene.render.fps=30;scene.sync_mode='FRAME_DROP'
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':area.spaces.active.shading.type='MATERIAL';area.spaces.active.overlay.show_overlays=False;area.spaces.active.show_region_ui=True
bpy.data.orphans_purge(do_recursive=True);bpy.context.preferences.filepaths.save_version=0
save_path=root/'Animal - Raw Audition.rebuild.blend' if rebuild else out
bpy.ops.wm.save_as_mainfile(filepath=str(save_path))
if rebuild:
    bpy.ops.wm.open_mainfile(filepath=str(save_path));assert len(json.loads(bpy.context.scene['deer_four_rigs']))==4
    save_path.replace(out)
(root/'audition.json').write_text(json.dumps({'animal':a.animal,'candidates':entries,'status':'ready_for_owner_review','painted':False,'art_approved':False,'notes':'Simple own geometry/wing motion, no generated textures; not gameplay spawning or flight AI'},indent=2))
print('Microfauna audition ready',a.animal,flush=True)
