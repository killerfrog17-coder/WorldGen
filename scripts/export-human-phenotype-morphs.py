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

def coords(mesh):
    a=np.empty(len(mesh.vertices)*3,dtype=np.float32);mesh.vertices.foreach_get('co',a);return a.reshape((-1,3))

def main(variants):
    OUT.mkdir(parents=True,exist_ok=True);results=[]
    for letter in variants:
        started=time.monotonic();bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
        recipe=next(r for r in json.loads(bpy.context.scene['physique_recipes']) if r['source']==letter)
        collection=bpy.data.collections[recipe['id']+'_after'];collection.hide_viewport=False;collection.hide_render=False
        rig=next(o for o in collection.objects if o.type=='ARMATURE')
        objects=[o for o in collection.objects if o.type=='MESH'];body=next(o for o in objects if o.get('physique_body'))
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
            stem=obj.name.split('.',1)[-1];matches=[p for p in files if p.stem==stem]
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
                    key=TargetService.load_target(body,path,weight=.65,name='phenotype_'+name);added.append(key)
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
                print('PHENOTYPE_TARGET',letter,keys[-1],flush=True)
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
        file=OUT/('human-'+letter.lower()+'-morphs.fbx')
        bpy.ops.export_scene.fbx(filepath=str(file),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,axis_forward='-Z',axis_up='Y',bake_anim=False,use_mesh_modifiers=False,use_custom_props=False,path_mode='STRIP',mesh_smooth_type='FACE',use_triangles=True)
        results.append({'variant':letter,'keys':keys,'parts':[(o.name,len(o.data.vertices)) for o in output],'seconds':time.monotonic()-started})
        (OUT/'manifest.json').write_text(json.dumps(results,indent=2))
        print('PHENOTYPE_EXPORTED',letter,results[-1]['seconds'],flush=True)

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--variants',default='ABCDEF')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    main(args.variants)
