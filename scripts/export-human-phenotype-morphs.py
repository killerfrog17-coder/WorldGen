"""Export licensed MPFB target morphs onto the existing six production sources.

No new rig, animation, skin texture, or named ethnic preset is made. Dependent
eyes/brows/hair receive the actual proxy-fit delta, preserving the edited base.
"""
import argparse, importlib.util, json, sys, time
from pathlib import Path
import bpy
import numpy as np

ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('human_setup',ROOT/'scripts/build-mpfb-human-demo.py')
setup=importlib.util.module_from_spec(spec);spec.loader.exec_module(setup)
from mpfb.services.targetservice import TargetService
from mpfb.services.clothesservice import ClothesService
from mpfb.services.humanservice import HumanService
from mpfb.entities.clothes.mhclo import Mhclo

SOURCE=ROOT/'assets-source/humans/appearance-review/Human physique comparison.blend'
OUT=ROOT/'assets-source/humans/phenotypes/exports'
TARGETS={
 'FaceWidth':(['head-scale-horiz-decr'],['head-scale-horiz-incr']),
 'JawWidth':(['chin-width-decr'],['chin-width-incr']),
 'Cheekbones':(['l-cheek-bones-decr','r-cheek-bones-decr'],['l-cheek-bones-incr','r-cheek-bones-incr']),
 'NoseWidth':(['nose-nostrils-width-decr'],['nose-nostrils-width-incr']),
 'NoseProjection':(['nose-scale-depth-decr'],['nose-scale-depth-incr']),
 'LipVolume':(['mouth-upperlip-volume-decr','mouth-lowerlip-volume-decr'],['mouth-upperlip-volume-incr','mouth-lowerlip-volume-incr']),
 'EyeOpening':(['l-eye-height2-decr','r-eye-height2-decr'],['l-eye-height2-incr','r-eye-height2-incr']),
 'EyeTilt':(['l-eye-eyefold-angle-down','r-eye-eyefold-angle-down'],['l-eye-eyefold-angle-up','r-eye-eyefold-angle-up']),
 'EyelidFold':(['l-eye-epicanthus-out','r-eye-epicanthus-out'],['l-eye-epicanthus-in','r-eye-epicanthus-in']),
 'BrowRidge':(['eyebrows-trans-backward'],['eyebrows-trans-forward']),
 'ChinProjection':(['chin-prominent-decr'],['chin-prominent-incr']),
 'Build':(['measure-waist-circ-decr'],['measure-waist-circ-incr']),
}

def add_grooming(body):
    roots=[ROOT/'assets-source/humans/research/builders/mpfb-assets',ROOT/'assets-source/humans/appearance-review/mpfb-assets']
    sources={p.stem:p for root in roots for p in root.rglob('*.mhclo')}
    specifications=[('Cropped',0,'short02','crop'),('Cropped',1,'afro01','crop'),('LooseShort',0,'cortu_short_messy_hair',''),('LooseShort',1,'afro01',''),('LooseLong',0,'long01',''),('LooseLong',1,'long01','wave'),('TiedBack',-1,'ponytail01',''),('Braided',-1,'braid01',''),('Braided',-1,'elvs_unkempt_french_braid','')]
    old_modifiers=set(body.modifiers);result=[]
    for index,(style,curl,name,adjust) in enumerate(specifications):
        path=sources[name]
        obj=HumanService.add_mhclo_asset(str(path),body,asset_type='Hair',subdiv_levels=0,material_type='NONE',import_subrig=False)
        obj.name='groom_'+style+'_'+str(index)
        obj['phenotype_proxy_path']=str(path)
        if adjust:
            xyz=coords(obj.data);top=coords(body.data)[:13380,2].max()
            if adjust=='crop':
                # Trim the volume outside the approximate scalp without moving
                # roots away from the head. This retains the asset's alpha cards.
                upper=xyz[:,2]>top-.16
                xyz[upper,2]=top-.16+(xyz[upper,2]-(top-.16))*.62
            else:
                # Wavy loose hair, with smooth roots; actual source cards retain
                # their UVs, alpha and skin weights. Not a skin-colour choice.
                length=np.maximum(0,top-.045-xyz[:,2]);strength=np.minimum(1,length/.12)
                xyz[:,0]+=.010*strength*np.sin(length*48+xyz[:,0]*25)
                xyz[:,1]+=.008*strength*np.sin(length*48+xyz[:,0]*25+1.5)
            obj.data.vertices.foreach_set('co',xyz.ravel());obj.data.update()
        texture=None
        for variant in 'ABCDEF':
            candidate=ROOT/f'assets-source/humans/appearance-runtime/exports/human-{variant.lower()}/{name}.png'
            if candidate.exists():texture=candidate;break
        if texture is None:
            candidates=list(path.parent.glob('*diffuse*.png'))
            if not candidates:raise RuntimeError('No licensed hair diffuse '+name)
            texture=candidates[0]
        obj['phenotype_texture_path']=str(texture)
        obj['phenotype_style']=style;obj['phenotype_curl']=curl
        result.append(obj)
    for modifier in list(body.modifiers):
        if modifier not in old_modifiers:body.modifiers.remove(modifier)
    return result

def coords(mesh):
    a=np.empty(len(mesh.vertices)*3,dtype=np.float32);mesh.vertices.foreach_get('co',a);return a.reshape((-1,3))

def age_source(letter,years):
    for obj in list(bpy.data.objects):bpy.data.objects.remove(obj,do_unlink=True)
    recipe=next(r for r in json.loads((ROOT/'scripts/human_demo_recipes.json').read_text()) if r['id']==letter)
    macro=TargetService.get_default_macro_info_dict()
    for key in ['gender','height','weight','muscle','proportions']:macro[key]=recipe[key]
    macro['age']={3:.045,8:.15,13:.24}[years];macro['weight']=.48;macro['muscle']=.4
    macro['race']=dict(zip(['african','asian','caucasian'],recipe['race']))
    human=HumanService._create_default_human_info_dict();hair='ponytail01' if recipe['gender']==0 else 'short02'
    human.update(name='MPFB_'+letter,phenotype=macro,rig='game_engine',eyes='high-poly/high-poly.mhclo',eyebrows='eyebrow001/eyebrow001.mhclo',eyelashes='eyelashes01/eyelashes01.mhclo',teeth='teeth_base/teeth_base.mhclo',hair=hair+'/'+hair+'.mhclo',clothes=[],skin_material_type='NONE',eyes_material_type='NONE',targets=[])
    settings=HumanService.get_default_deserialization_settings();settings['subdiv_levels']=0
    body=HumanService.deserialize_from_dict(human,settings);rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');rig.name='MPFB_'+letter
    objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
    for obj in objects:
        stem=obj.name.split('.',1)[-1];kind=getattr(obj,'MPFB_GEN_object_type','')
        label='Body' if obj==body else 'Eyes' if 'high-poly' in stem else 'Teeth' if 'teeth' in stem else 'Eyebrows' if 'eyebrow' in stem else 'Eyelashes' if 'eyelashes' in stem else 'Hair'
        obj['phenotype_proxy_stem']=stem;obj.name='Young_'+letter+'_'+label
        if obj.data.shape_keys:
            key=obj.shape_key_add(name='baked_age_basis',from_mix=True);points=np.empty(len(key.data)*3,dtype=np.float32);key.data.foreach_get('co',points);obj.shape_key_clear();obj.data.vertices.foreach_set('co',points)
    return rig,objects,body

def main(variants,ages):
    OUT.mkdir(parents=True,exist_ok=True);results=[]
    for letter,years in [(letter,age) for letter in variants for age in ages]:
        started=time.monotonic();bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
        if years:
            rig,objects,body=age_source(letter,years)
        else:
            recipe=next(r for r in json.loads(bpy.context.scene['physique_recipes']) if r['source']==letter)
            collection=bpy.data.collections[recipe['id']+'_after'];collection.hide_viewport=False;collection.hide_render=False
            rig=next(o for o in collection.objects if o.type=='ARMATURE')
            objects=[o for o in collection.objects if o.type=='MESH'];body=next(o for o in objects if o.get('physique_body'))
        identity=letter+('-'+str(years) if years else '')
        objects+=add_grooming(body)
        for obj in list(bpy.data.objects):
            if obj!=rig and obj not in objects:bpy.data.objects.remove(obj,do_unlink=True)
        rig.hide_set(False);rig.animation_data_clear();rig.data.pose_position='REST';rig.location=(0,0,0)
        for b in rig.pose.bones:b.matrix_basis.identity()
        for obj in objects:
            obj.hide_set(False);obj.hide_render=False
            for mod in obj.modifiers:
                if mod.type=='ARMATURE':mod.show_viewport=False;mod.show_render=False
        original={o:coords(o.data) for o in objects}
        # Resolve the original proxy descriptions from permanent author inputs.
        files=list((ROOT/'assets-source/humans/research/builders/mpfb-assets').rglob('*.mhclo'))+list((ROOT/'assets-source/humans/appearance-review/mpfb-assets').rglob('*.mhclo'))
        proxies={};fit_baseline={}
        for obj in objects:
            if obj==body:continue
            fragment=getattr(obj,'MPFB_GEN_asset_source','') or obj.get('MPFB_GEN_asset_source','')
            stem=obj.get('phenotype_proxy_stem',obj.name.split('.',1)[-1]);matches=[Path(obj['phenotype_proxy_path'])] if obj.get('phenotype_proxy_path') else [p for p in files if p.stem==stem]
            if not matches and fragment:matches=[p for p in files if p.name==Path(fragment).name]
            if not matches:raise RuntimeError('Missing permanent proxy for '+obj.name+' / '+fragment)
            proxy=Mhclo();proxy.load(str(matches[0]));proxy.clothes=obj;proxies[obj]=proxy
            ClothesService.fit_clothes_to_human(obj,body,proxy,set_parent=False)
            fit_baseline[obj]=coords(obj.data);obj.data.vertices.foreach_set('co',original[obj].ravel())
        frames={o:[] for o in objects};keys=[]
        for axis,pair in TARGETS.items():
            for sign,names in zip(['Low','High'],pair):
                added=[]
                for name in names:
                    path=TargetService.target_full_path(name)
                    if not path:raise RuntimeError('Missing licensed target '+name)
                    strength=.65*(max(.4,min(1,years/17)) if years else 1)
                    key=TargetService.load_target(body,path,weight=strength,name='phenotype_'+name);added.append(key)
                bpy.context.view_layer.update()
                for obj in objects:
                    if obj==body:
                        key=obj.shape_key_add(name='temporary_output',from_mix=True);points=np.empty(len(key.data)*3,dtype=np.float32);key.data.foreach_get('co',points);body.shape_key_remove(key)
                        frames[obj].append(points.reshape((-1,3)))
                    else:
                        ClothesService.fit_clothes_to_human(obj,body,proxies[obj],set_parent=False)
                        frames[obj].append(original[obj]+coords(obj.data)-fit_baseline[obj])
                        obj.data.vertices.foreach_set('co',original[obj].ravel())
                keys.append(axis+sign)
                for key in added:body.shape_key_remove(key)
        if body.data.shape_keys:body.shape_key_clear()
        output=[]
        for obj in objects:
            # Point IDs let the fixed helper mask be applied before creating
            # export shape keys. This preserves topology through every frame.
            attr=obj.data.attributes.new('source_index','INT','POINT');attr.data.foreach_set('value',np.arange(len(obj.data.vertices),dtype=np.int32))
            bpy.context.view_layer.update();evaluated=obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
            mesh=bpy.data.meshes.new_from_object(evaluated,preserve_all_data_layers=True,depsgraph=bpy.context.evaluated_depsgraph_get())
            ids=np.empty(len(mesh.vertices),dtype=np.int32);mesh.attributes['source_index'].data.foreach_get('value',ids)
            holder=bpy.data.objects.new('MPFB_'+letter+'.'+obj.name.split('.',1)[-1],mesh);bpy.context.scene.collection.objects.link(holder)
            holder.parent=rig;holder.matrix_world=obj.matrix_world.copy()
            for group in obj.vertex_groups:holder.vertex_groups.new(name=group.name)
            holder.shape_key_add(name='Basis')
            for name,points in zip(keys,frames[obj]):
                key=holder.shape_key_add(name=name);key.data.foreach_set('co',np.ascontiguousarray(points[ids]).ravel())
            modifier=holder.modifiers.new('Armature','ARMATURE');modifier.object=rig
            output.append(holder)
        for obj in bpy.context.selected_objects:obj.select_set(False)
        for obj in [rig,*output]:obj.select_set(True)
        bpy.context.view_layer.objects.active=rig
        file=OUT/('human-'+identity.lower()+'-morphs.fbx')
        bpy.ops.export_scene.fbx(filepath=str(file),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,axis_forward='-Z',axis_up='Y',bake_anim=False,use_mesh_modifiers=False,use_custom_props=False,path_mode='STRIP',mesh_smooth_type='FACE',use_triangles=True)
        hairs=[{'part':o.name.split('.',1)[-1],'style':o['phenotype_style'],'curl':o['phenotype_curl'],'texture':str(Path(o['phenotype_texture_path']).relative_to(ROOT)).replace('\\','/')} for o in objects if o.get('phenotype_style')]
        results.append({'variant':identity,'keys':keys,'parts':[(o.name,len(o.data.vertices)) for o in output],'hair':hairs,'seconds':time.monotonic()-started})
        (OUT/'manifest.json').write_text(json.dumps(results,indent=2))
        print('PHENOTYPE_EXPORTED',identity,results[-1]['seconds'],flush=True)

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--variants',default='ABCDEF')
    parser.add_argument('--ages',default='0,3,8,13')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    main(args.variants,[int(a) for a in args.ages.split(',')])
