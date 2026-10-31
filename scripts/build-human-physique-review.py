"""Isolated, reproducible MPFB physique and grooming audition. No game writes."""
import importlib.util, json, math, sys, hashlib
from pathlib import Path
import bpy
from mathutils import Matrix, Vector
import numpy as np
REPO=Path(__file__).resolve().parent.parent
ROOT=REPO/'assets-source/humans/appearance-review'
HAIR_TEXTURE=REPO/'assets/human-appearance-review/body-hair-mask.png'
sys.path.insert(0,str(REPO/'scripts'))
def module(name,file):
    spec=importlib.util.spec_from_file_location(name,REPO/'scripts'/file)
    mod=importlib.util.module_from_spec(spec);spec.loader.exec_module(mod);return mod
old=module('appearance_original','build-mpfb-human-demo.py')
old.LocationService._user_data=str(ROOT/'mpfb-assets')
retarget=module('physique_retarget','build-human-animation-preview.py')
bank=module('physique_bank','build-kimodo-human-bank.py')
retarget.source_map=lambda source:bank.SOMA_MAP
retarget.rotation_fix=bank.retarget_fix
from natural_human_bake import ground_action

def recipes():
    originals=json.loads((REPO/'scripts/human_demo_recipes.json').read_text())
    result=[]
    for letter,source,label,weight,muscle,hair,beard in [
        ('A','B','Man - lean / wiry',.45,1.,'cortu_short_messy_hair','rehmanpolanski_beard_viking'),
        ('B','D','Man - athletic',.53,1.,'long01','rehmanpolanski_beard_viking'),
        ('C','F','Man - compact / powerful',.59,1.,'cortu_short_messy_hair','rehmanpolanski_beard_viking'),
        ('D','C','Woman - lean / wiry',.47,.95,'elvs_unkempt_french_braid',None),
        ('E','A','Woman - athletic',.57,1.,'afro01',None),
        ('F','E','Woman - compact / powerful',.64,1.,'braid01',None)]:
        baseline=json.loads(json.dumps(next(r for r in originals if r['id']==source)))
        if source=='B':baseline['hair']='short02'
        if source=='E':baseline['hair']='ponytail01'
        revised=json.loads(json.dumps(baseline));revised.update(weight=weight,muscle=muscle,hair=hair)
        if not baseline['gender']:revised['firmness']=.8
        strength=(.48 if 'wiry' in label else .72) if baseline['gender'] else (.36 if 'wiry' in label else .53)
        revised['targets'].update({'torso-muscle-pectoral-incr':.12 if baseline['gender'] else .25,'torso-muscle-dorsi-incr':strength,'measure-shoulder-dist-incr':.20 if 'wiry' in label else .30,'measure-waist-circ-decr':.14})
        for side in 'lr':
            for part in ['upperarm-muscle','lowerarm-muscle','upperarm-shoulder-muscle','upperleg-muscle','lowerleg-muscle']:
                revised['targets'][side+'-'+part+'-incr']=strength
        revised['beard']='grinsegold_beard_sigmund_wip' if beard else None
        result.append(dict(id=letter,source=source,label=label,before=baseline,after=revised))
    return result


def bake_shapes(obj):
    if not obj.data.shape_keys:return
    key=obj.shape_key_add(name='Appearance baked for preview',from_mix=True)
    coords=np.empty(len(obj.data.vertices)*3,dtype=np.float32);key.data.foreach_get('co',coords)
    obj.shape_key_clear();obj.data.vertices.foreach_set('co',coords);obj.data.update()


def beard_material(obj,recipe):
    # The community asset references two absent AO/displacement maps. Build a
    # clean surface from its actual diffuse/alpha, rather than missing-map colors.
    image=bpy.data.images.load(str(ROOT/'mpfb-assets/clothes/grinsegold_beard_sigmund_wip/Material_Diffuse_Color1_d.png'),check_existing=True)
    mat=bpy.data.materials.new(obj.name+' natural beard');mat.use_nodes=True;mat.surface_render_method='DITHERED'
    nodes=mat.node_tree.nodes;links=mat.node_tree.links;bs=nodes.get('Principled BSDF')
    tex=nodes.new('ShaderNodeTexImage');tex.image=image
    tint=nodes.new('ShaderNodeMixRGB');tint.blend_type='MULTIPLY';tint.inputs[0].default_value=1
    tint.inputs[2].default_value=(*[min(.9,c*3+.055) for c in recipe['hair_rgb']],1)
    links.new(tex.outputs['Color'],tint.inputs[1]);links.new(tint.outputs[0],bs.inputs['Base Color'])
    bs.inputs['Roughness'].default_value=.67;bs.inputs['Specular IOR Level'].default_value=.3
    bump=nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.3;bump.inputs['Distance'].default_value=.0008;links.new(tex.outputs['Color'],bump.inputs['Height']);links.new(bump.outputs[0],bs.inputs['Normal'])
    obj.data.materials.clear();obj.data.materials.append(mat)
    bottom=min(v.co.z for v in obj.data.vertices)
    length=.025 if recipe['id']=='B' else .045 if recipe['id']=='D' else .035
    for vertex in obj.data.vertices:
        amount=max(0,min(1,(bottom+.07-vertex.co.z)/.07))**1.4
        vertex.co.z-=length*amount;vertex.co.y-=.008*amount
    obj.data.update()
    # Fade the cheek/jaw outline instead of giving the beard a razor-cut edge.
    import heapq
    edge_faces={}
    for polygon in obj.data.polygons:
        ids=list(polygon.vertices)
        for a,b in zip(ids,ids[1:]+ids[:1]):
            key=tuple(sorted((a,b)));edge_faces[key]=edge_faces.get(key,0)+1
    adjacency={i:[] for i in range(len(obj.data.vertices))};distance=[1e6]*len(obj.data.vertices);queue=[]
    for (a,b),count in edge_faces.items():
        length=(obj.data.vertices[a].co-obj.data.vertices[b].co).length;adjacency[a].append((b,length));adjacency[b].append((a,length))
        if count==1:
            for i in [a,b]:distance[i]=0;heapq.heappush(queue,(0,i))
    while queue:
        value,i=heapq.heappop(queue)
        if value>distance[i]:continue
        for j,edge in adjacency[i]:
            if value+edge<distance[j]:distance[j]=value+edge;heapq.heappush(queue,(distance[j],j))
    fade=obj.data.attributes.get('BeardEdgeFade') or obj.data.attributes.new('BeardEdgeFade','FLOAT','POINT')
    for i,value in enumerate(distance):fade.data[i].value=min(1,value/.009)
    attribute=nodes.new('ShaderNodeAttribute');attribute.attribute_name='BeardEdgeFade'
    alpha=nodes.new('ShaderNodeMath');alpha.operation='MULTIPLY';links.new(tex.outputs['Alpha'],alpha.inputs[0]);links.new(attribute.outputs['Fac'],alpha.inputs[1]);links.new(alpha.outputs[0],bs.inputs['Alpha'])

def body_hair_flow_uv(body,rig):
    """A skin-attached flow map; limb hair follows each native rest limb axis.

    This adds only UV coordinates. Existing skin UVs, weights and geometry stay
    unchanged. Integer circumferential repeats close each cylindrical seam.
    """
    mesh=body.data;original=next((u for u in mesh.uv_layers if u.active_render),mesh.uv_layers.active)
    layer=mesh.uv_layers.get('BodyHairFlow') or mesh.uv_layers.new(name='BodyHairFlow',do_init=False)
    transform=rig.matrix_world.inverted()@body.matrix_world
    axes={}
    for side in ['l','r']:
        for region,start,end,repeats in [('arm','upperarm','hand',2),('leg','thigh','foot',4)]:
            a=rig.data.bones[start+'_'+side].head_local.copy();b=rig.data.bones[end+'_'+side].head_local.copy()
            direction=(b-a).normalized();front=Vector((0,-1,0));front=(front-direction*front.dot(direction)).normalized()
            across=direction.cross(front).normalized();axes[region+'_'+side]=(a,direction,front,across,repeats)
    mapped=[]
    for vertex in mesh.vertices:
        p=transform@vertex.co
        weights=[g for g in vertex.groups if body.vertex_groups[g.group].name in rig.data.bones]
        name=body.vertex_groups[max(weights,key=lambda g:g.weight).group].name if weights else ''
        side=name[-1:] if name.endswith(('_l','_r')) else ''
        region='arm' if name.startswith(('upperarm','lowerarm','hand','thumb','index','middle','ring','pinky')) else 'leg' if name.startswith(('thigh','calf','foot','ball')) else ''
        if region and side:
            key=region+'_'+side;a,direction,front,across,repeats=axes[key]
            delta=p-a;along=delta.dot(direction);radial=delta-direction*along
            angle=math.atan2(radial.dot(across),radial.dot(front))
            mapped.append((key,angle/math.tau*repeats,-along/.12,repeats))
        else:
            # Small continuous bend toward the sternum; no projection blending
            # that would merge independent hairs into branching marks.
            u=(p.x-.012*math.sin(p.z*7)*math.tanh(p.x*12))/.12
            mapped.append(('torso',u,p.z/.12,0))
    for polygon in mesh.polygons:
        values=[mapped[mesh.loops[i].vertex_index] for i in polygon.loop_indices]
        same=len({v[0] for v in values})==1;repeat=values[0][3]
        unwrap=same and repeat and max(v[1] for v in values)-min(v[1] for v in values)>repeat*.5
        for index,value in zip(polygon.loop_indices,values):
            key,u,v,_=value
            if unwrap and u<0:u+=repeat
            # Separate legs/arms and people do not share mirrored hair stamps.
            seed=(ord(body.name[0])-ord('A'))*.137
            u+=seed+(.173 if key.endswith('_l') else .619 if key.endswith('_r') else .41)
            v+=seed*.71+(.293 if key.endswith('_l') else .733 if key.endswith('_r') else .17)
            layer.data[index].uv=(u,v)
    if original:mesh.uv_layers.active=original;original.active_render=True
    body['body_hair_mapping']='Native limb-aligned cylindrical UV, 12cm axial repeat, unbranched fine mask; original skin UV preserved'

def surface_hair_and_dirt(body,rig,recipe,letter):
    """Cheap material coverage; no hair simulation or strand meshes."""
    hair=body.data.attributes.new('BodyHairCoverage','FLOAT','POINT')
    dirt=body.data.attributes.new('SkinDirtCoverage','FLOAT','POINT')
    world=[body.matrix_world@v.co for v in body.data.vertices[:13380]]
    low=min(v.z for v in world);height=max(v.z for v in world)-low
    male=bool(recipe['gender']);density={'A':.65,'B':.72,'C':.8,'D':.3,'E':.4,'F':.5}[letter]
    for i,p in enumerate(world):
        groups=[g for g in body.data.vertices[i].groups if body.vertex_groups[g.group].name in rig.data.bones]
        z=(p.z-low)/height;x=p.x/height;front=max(0,min(1,-p.y/.04))
        h=0.;d=0.
        for group in groups:
            bone=body.vertex_groups[group.group].name;bh=0.;bd=.1
            if 'lowerarm' in bone:bh=.8;bd=.42
            elif 'upperarm' in bone or 'clavicle' in bone:bh=.7 if male else .32;bd=.2
            elif 'calf' in bone:bh=.9;bd=.6
            elif 'thigh' in bone:bh=.75;bd=.25
            elif bone.startswith('spine') and male:
                chest=math.exp(-((z-.765)/.09)**2)*math.exp(-(x/.15)**4)
                belly=math.exp(-((z-.63)/.085)**2)*math.exp(-(x/.085)**2)
                bh=front*max(chest,belly*.95)+(1-front)*.6
            elif bone=='pelvis' and male:bh=.6*math.exp(-(x/.13)**4)
            elif bone.startswith('foot'):bh=.15;bd=.5
            elif bone.startswith('hand'):bh=.05;bd=.3
            elif bone.startswith('neck'):bh=.08 if male else 0
            elif bone=='head':bd=.055
            h+=bh*group.weight;d+=bd*group.weight
        hair.data[i].value=h*density;dirt.data[i].value=d*.4
    body_hair_flow_uv(body,rig)
    texture=bpy.data.images.load(str(HAIR_TEXTURE),check_existing=True)
    texture.colorspace_settings.name='Non-Color'
    for mat in body.data.materials:
        tree=mat.node_tree;nodes=tree.nodes;links=tree.links
        bs=next((n for n in nodes if n.type=='BSDF_PRINCIPLED'),None)
        if not bs:raise RuntimeError('Skin material has no surface shader')
        coord=nodes.new('ShaderNodeTexCoord');uv=nodes.new('ShaderNodeUVMap');uv.uv_map='BodyHairFlow'
        img=nodes.new('ShaderNodeTexImage');img.image=texture;img.extension='REPEAT';links.new(uv.outputs['UV'],img.inputs['Vector'])
        gray=nodes.new('ShaderNodeRGBToBW');links.new(img.outputs['Color'],gray.inputs[0])
        invert=nodes.new('ShaderNodeMath');invert.operation='SUBTRACT';invert.inputs[0].default_value=1;links.new(gray.outputs[0],invert.inputs[1])
        # More actual fine marks rather than the rejected fourfold ink gain.
        # Alpha-over unions preserve each shaft's width and taper.
        combined=invert.outputs[0]
        for offset in [(.37,.53),(.71,.19),(.11,.83),(.59,.31)]:
            shift=nodes.new('ShaderNodeVectorMath');shift.operation='ADD';shift.inputs[1].default_value=(*offset,0);links.new(uv.outputs['UV'],shift.inputs[0])
            detail=nodes.new('ShaderNodeTexImage');detail.image=texture;detail.extension='REPEAT';links.new(shift.outputs[0],detail.inputs['Vector'])
            amount=nodes.new('ShaderNodeRGBToBW');links.new(detail.outputs['Color'],amount.inputs[0])
            white=nodes.new('ShaderNodeMath');white.operation='SUBTRACT';white.inputs[0].default_value=1;links.new(combined,white.inputs[1])
            product=nodes.new('ShaderNodeMath');product.operation='MULTIPLY';links.new(white.outputs[0],product.inputs[0]);links.new(amount.outputs[0],product.inputs[1])
            union=nodes.new('ShaderNodeMath');union.operation='SUBTRACT';union.inputs[0].default_value=1;links.new(product.outputs[0],union.inputs[1]);combined=union.outputs[0]
        attribute=nodes.new('ShaderNodeAttribute');attribute.attribute_name='BodyHairCoverage'
        coverage=nodes.new('ShaderNodeMath');coverage.operation='MULTIPLY';coverage.inputs[1].default_value=1.5;coverage.use_clamp=True;links.new(attribute.outputs['Fac'],coverage.inputs[0])
        factor=nodes.new('ShaderNodeMath');factor.operation='MULTIPLY';links.new(coverage.outputs[0],factor.inputs[0]);links.new(combined,factor.inputs[1])
        source=bs.inputs['Base Color'].links[0].from_socket if bs.inputs['Base Color'].is_linked else None
        fallback=tuple(bs.inputs['Base Color'].default_value)
        noise=nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=16;noise.inputs['Detail'].default_value=4;noise.inputs['Roughness'].default_value=.7;links.new(coord.outputs['Object'],noise.inputs['Vector'])
        ramp=nodes.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].position=.43;ramp.color_ramp.elements[1].position=.75;links.new(noise.outputs['Fac'],ramp.inputs[0])
        da=nodes.new('ShaderNodeAttribute');da.attribute_name='SkinDirtCoverage'
        df=nodes.new('ShaderNodeMath');df.operation='MULTIPLY';links.new(da.outputs['Fac'],df.inputs[0]);links.new(ramp.outputs[0],df.inputs[1])
        dm=nodes.new('ShaderNodeMixRGB');dm.inputs[2].default_value=(.12,.073,.035,1);links.new(df.outputs[0],dm.inputs[0])
        if source:links.new(source,dm.inputs[1])
        else:dm.inputs[1].default_value=fallback
        links.new(dm.outputs[0],bs.inputs['Base Color'])
        bs.inputs['Roughness'].default_value=.66;bs.inputs['Specular IOR Level'].default_value=.22
        hair_bs=nodes.new('ShaderNodeBsdfPrincipled');hair_bs.label='Hair surface, separate from skin scattering'
        hair_bs.inputs['Base Color'].default_value=(*[max(.008,c*.5) for c in recipe['hair_rgb']],1)
        hair_bs.inputs['Roughness'].default_value=.55;hair_bs.inputs['Specular IOR Level'].default_value=.35
        anisotropy=hair_bs.inputs.get('Anisotropic')
        if anisotropy:anisotropy.default_value=.55
        hair_bs.inputs['Sheen Weight'].default_value=.08
        tangent=nodes.new('ShaderNodeTangent');tangent.direction_type='UV_MAP';tangent.uv_map='BodyHairFlow';links.new(tangent.outputs[0],hair_bs.inputs['Tangent'])
        bump=nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.22;bump.inputs['Distance'].default_value=.000045
        links.new(combined,bump.inputs['Height']);links.new(bump.outputs[0],hair_bs.inputs['Normal'])
        surface=nodes.new('ShaderNodeMixShader');links.new(factor.outputs[0],surface.inputs[0]);links.new(bs.outputs[0],surface.inputs[1]);links.new(hair_bs.outputs[0],surface.inputs[2])
        output=next(n for n in nodes if n.type=='OUTPUT_MATERIAL');links.new(surface.outputs[0],output.inputs['Surface'])


def create(entry,version,prepared):
    recipe=entry[version];before=set(bpy.data.objects)
    macro=old.TargetService.get_default_macro_info_dict()
    for key in ['gender','age','height','weight','muscle','proportions','firmness']:
        if key in recipe:macro[key]=recipe[key]
    macro['race']=dict(zip(['african','asian','caucasian'],recipe['race']))
    human=old.HumanService._create_default_human_info_dict()
    human.update(name=entry['id']+'_'+version,phenotype=macro,rig='game_engine',eyes='high-poly/high-poly.mhclo',eyebrows='eyebrow001/eyebrow001.mhclo',eyelashes='eyelashes01/eyelashes01.mhclo',teeth='teeth_base/teeth_base.mhclo',hair=recipe['hair']+'/'+recipe['hair']+'.mhclo',skin_mhmat=recipe['skin']+'/'+recipe['skin']+'.mhmat',skin_material_type='MAKESKIN',eyes_material_type='PROCEDURAL_EYES',eyes_material_settings={'IrisMajorColor':[*recipe['iris_rgb'],1],'IrisMinorColor':[*recipe['iris_rgb'],1]},clothes=([recipe['beard']+'/'+recipe['beard']+'.mhclo'] if recipe.get('beard') else []),targets=[dict(target=k,value=v) for k,v in recipe['targets'].items() if v])
    for target in human['targets']:assert old.TargetService.target_full_path(target['target']),target
    settings=old.HumanService.get_default_deserialization_settings();settings['subdiv_levels']=0
    native_calculate=old.TargetService.calculate_target_stack_from_macro_info_dict
    def male_targets(macro_info=None,cutoff=.01):
        # MPFB's gender endpoint retains a 0.0098 female component, but its
        # breast-correction branch deliberately omits multiplication by that
        # component. Pure male copies must not get full female corrections.
        return [(name,value) for name,value in native_calculate(macro_info,cutoff) if not name.startswith('breast/')]
    if version=='after' and recipe['gender']==1:old.TargetService.calculate_target_stack_from_macro_info_dict=staticmethod(male_targets)
    try:body=old.HumanService.deserialize_from_dict(human,settings)
    finally:old.TargetService.calculate_target_stack_from_macro_info_dict=staticmethod(native_calculate)
    body['physique_body']=True
    created=set(bpy.data.objects)-before
    rig=next(o for o in created if o.type=='ARMATURE');rig['variant']=entry['id']+'_'+version
    col=bpy.data.collections.new(entry['id']+'_'+version);bpy.context.scene.collection.children.link(col)
    for obj in created:
        for c in list(obj.users_collection):c.objects.unlink(obj)
        col.objects.link(obj)
        if obj.type!='MESH':continue
        typ=getattr(obj,'MPFB_GEN_object_type','')
        if typ=='Hair' or typ=='Clothes':
            for mat in obj.data.materials:
                old.tint_material(mat,recipe['hair_rgb'],hair=True)
                mat.surface_render_method='DITHERED'
                for node in mat.node_tree.nodes:
                    if node.type=='BSDF_PRINCIPLED':
                        node.inputs['Roughness'].default_value=.86;node.inputs['Specular IOR Level'].default_value=.12
                        for link in list(node.inputs['Normal'].links):mat.node_tree.links.remove(link)
                        image=next((n for n in mat.node_tree.nodes if n.type=='TEX_IMAGE' and n.image and n.image.colorspace_settings.name!='Non-Color'),None)
                        if image:mat.node_tree.links.new(image.outputs['Alpha'],node.inputs['Alpha'])
        if typ=='Basemesh' and 'skin_tint' in recipe:
            for mat in obj.data.materials:old.tint_material(mat,recipe['skin_tint'])
        bake_shapes(obj)
        if 'grinsegold_beard_sigmund_wip' in obj.name:beard_material(obj,recipe)
        for p in obj.data.polygons:p.use_smooth=True
    bpy.context.view_layer.update()
    if version=='after':surface_hair_and_dirt(body,rig,recipe,entry['id'])
    rig.show_in_front=True;rig.hide_render=True;rig.hide_set(True)
    # A matched, relaxed neutral stance exposes geometry independently of generated motion.
    rig.animation_data_clear()
    for b in rig.pose.bones:b.matrix_basis=Matrix.Identity(4)
    for b in rig.pose.bones:
        if b.name not in ['upperarm_l','upperarm_r']:continue
        d=b.tail-b.head;delta=math.atan2(abs(d.x),-d.z)-math.radians(18)
        b.matrix=Matrix.Translation(b.head)@Matrix.Rotation((1 if d.x>0 else -1)*delta,4,'Y')@Matrix.Translation(-b.head)@b.matrix
    neutral=bpy.data.actions.new(rig['variant']+' Neutral');rig.animation_data_create();rig.animation_data.action=neutral
    for b in rig.pose.bones:
        b.rotation_mode='QUATERNION';b.keyframe_insert(data_path='rotation_quaternion',frame=1);b.keyframe_insert(data_path='location',frame=1)
    neutral.use_fake_user=True
    records={'Neutral':dict(action=neutral.name,frames=1)}
    for role,(motion,meta) in prepared.items():
        record=retarget.animate(rig,'MPFB',(role,motion,role),motion.samples)
        ground_action(rig,bpy.data.actions[record['action']],len(motion.samples));records[role]=record
    rig['clips']=json.dumps(records)
    eyes=next(o for o in created if getattr(o,'MPFB_GEN_object_type','')=='Eyes')
    rig.animation_data.action=neutral;rig.animation_data.action_slot=neutral.slots[0];bpy.context.scene.frame_set(1);bpy.context.view_layer.update()
    col['head_center']=list(sum((eyes.matrix_world@Vector(v) for v in eyes.bound_box),Vector())/8)
    col['recipe']=json.dumps(human)
    print('CREATED',entry['id'],version,len(body.data.vertices),flush=True)

def main():
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    data=recipes()
    if '--only' in sys.argv:data=[r for r in data if r['id'] in sys.argv[sys.argv.index('--only')+1]]
    (ROOT/'recipes.json').write_text(json.dumps(data,indent=2))
    selected=json.loads((REPO/'assets/human-motion-kimodo/bank.json').read_text())['people'][0]['clips']
    prepared={}
    for role in ['Idle','Walk','Run']:
        meta=next(r for r in selected if r['role']==role);source=bank.SomaMotion(REPO/meta['source'])
        samples,_=bank.prepare(source,meta['raw_name'],role,True,{role:{'begin':meta['begin'],'end':meta['end']}})
        prepared[role]=(bank.FittedMotion(source,samples),meta)
    for entry in data:
        for version in ['before','after']:create(entry,version,prepared)
    stage=module('physique_stage','assemble-human-demo.py');stage.setup()
    scene=bpy.context.scene;scene.render.fps=30
    scene['physique_recipes']=json.dumps(data);scene['person']='B';scene['mode']='After';scene['view']='Legs';scene['clip']='Neutral'
    for img in bpy.data.images:
        if img.source=='FILE' and img.filepath:img.pack()
    viewer=module('physique_view','human-physique-viewer.py');viewer.refresh()
    bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Human physique comparison.blend'),compress=True)
    print('PHYSIQUE_REVIEW_READY',flush=True)
if __name__=='__main__':main()
