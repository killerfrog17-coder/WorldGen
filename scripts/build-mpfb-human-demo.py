"""Create six real MPFB humans, preserving morphs, fitted assets and native rigs."""
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector

REPO = Path(__file__).resolve().parent.parent
ROOT = REPO / 'artifacts/human-builder-demo'
sys.path.insert(0, str(REPO / 'artifacts/tools/mpfb2/src'))
original_extension_path = bpy.utils.extension_path_user
bpy.utils.extension_path_user = lambda package, **kwargs: str(ROOT / 'mpfb-user') if package == 'mpfb' else original_extension_path(package, **kwargs)
import mpfb
bpy.context.preferences.addons.new().module = 'mpfb'
mpfb.register()
from mpfb.services.humanservice import HumanService
from mpfb.services.locationservice import LocationService
from mpfb.services.targetservice import TargetService
from mpfb.services.objectservice import ObjectService
LocationService._user_data = str(ROOT / 'mpfb-assets')


def tint_material(material, tint, hair=False):
    tree = material.node_tree
    for node in list(tree.nodes):
        if node.type != 'TEX_IMAGE' or not node.image or node.image.colorspace_settings.name == 'Non-Color':
            continue
        outgoing = list(node.outputs['Color'].links)
        if not outgoing:
            continue
        blend = tree.nodes.new('ShaderNodeMixRGB')
        blend.blend_type = 'MULTIPLY'
        blend.inputs[0].default_value = 1
        blend.inputs[2].default_value = (*tint, 1)
        if hair:
            luminance=tree.nodes.new('ShaderNodeRGBToBW')
            detail=tree.nodes.new('ShaderNodeMath');detail.operation='MULTIPLY_ADD'
            detail.inputs[1].default_value=2.0;detail.inputs[2].default_value=.30
            tree.links.new(node.outputs['Color'],luminance.inputs[0])
            tree.links.new(luminance.outputs[0],detail.inputs[0])
            tree.links.new(detail.outputs[0],blend.inputs[1])
        else:
            tree.links.new(node.outputs['Color'], blend.inputs[1])
        for link in outgoing:
            tree.links.new(blend.outputs[0], link.to_socket)


def main():
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    recipes = json.loads((REPO / 'scripts/human_demo_recipes.json').read_text())
    evidence = []
    for recipe in recipes:
        before = set(bpy.data.objects)
        if recipe['id']=='E':recipe['hair']='ponytail01'
        if recipe['id']=='B':recipe['hair']='short02'
        macro = TargetService.get_default_macro_info_dict()
        for key in ['gender','age','height','weight','muscle','proportions']:
            macro[key] = recipe[key]
        macro['race'] = dict(zip(['african','asian','caucasian'], recipe['race']))
        human = HumanService._create_default_human_info_dict()
        human.update(name='MPFB_'+recipe['id'], phenotype=macro, rig='game_engine',
                     eyes='high-poly/high-poly.mhclo', eyebrows='eyebrow001/eyebrow001.mhclo',
                     eyelashes='eyelashes01/eyelashes01.mhclo', teeth='teeth_base/teeth_base.mhclo',
                     hair=recipe['hair']+'/'+recipe['hair']+'.mhclo',
                     skin_mhmat=recipe['skin']+'/'+recipe['skin']+'.mhmat',
                     skin_material_type='MAKESKIN', eyes_material_type='PROCEDURAL_EYES',
                     eyes_material_settings={'IrisMajorColor':[*recipe['iris_rgb'],1], 'IrisMinorColor':[*recipe['iris_rgb'],1]},
                     clothes=['female_sportsuit01/female_sportsuit01.mhclo' if recipe['gender']==0 else 'male_casualsuit01/male_casualsuit01.mhclo'],
                     targets=[{'target':name,'value':weight} for name,weight in recipe['targets'].items() if weight])
        settings = HumanService.get_default_deserialization_settings()
        settings['subdiv_levels'] = 0
        body = HumanService.deserialize_from_dict(human, settings)
        created = set(bpy.data.objects)-before
        rig = next(obj for obj in created if obj.type=='ARMATURE')
        collection = bpy.data.collections.new('MPFB_'+recipe['id'])
        bpy.context.scene.collection.children.link(collection)
        for obj in created:
            for old in list(obj.users_collection):
                old.objects.unlink(obj)
            collection.objects.link(obj)
            if obj.type=='MESH':
                for face in obj.data.polygons:
                    face.use_smooth = True
                modifier = obj.modifiers.new('Surface subdivision', 'SUBSURF')
                modifier.levels = modifier.render_levels = 1
            if obj.type=='ARMATURE':
                obj.show_in_front = True
                obj.hide_render = True
                obj.hide_set(True)
        for bone in rig.pose.bones:
            if bone.name in ['upperarm_l','upperarm_r','upperarm01.L','upperarm01.R']:
                direction = bone.tail-bone.head
                tilt = math.atan2(abs(direction.x), -direction.z)
                delta = tilt-math.radians(22)
                sign = 1 if direction.x>0 else -1
                rotation = Matrix.Rotation(sign*delta, 4, 'Y')
                point = bone.head.copy()
                bone.matrix = Matrix.Translation(point) @ rotation @ Matrix.Translation(-point) @ bone.matrix
        for obj in created:
            typ = getattr(obj,'MPFB_GEN_object_type','')
            if typ=='Hair':
                for material in obj.data.materials:
                    tint_material(material, recipe['hair_rgb'], hair=True)
                    for node in material.node_tree.nodes:
                        if node.type=='BSDF_PRINCIPLED':
                            node.inputs['Roughness'].default_value=.78
                            node.inputs['Specular IOR Level'].default_value=.24
            if typ=='Basemesh' and 'skin_tint' in recipe:
                for material in obj.data.materials:
                    tint_material(material, recipe['skin_tint'])
        bpy.context.view_layer.update()
        body['demo_id'] = recipe['id']
        body['demo_builder'] = 'MPFB 2.0.18'
        body['demo_label'] = recipe['label']
        body['demo_description'] = recipe['description']
        body['demo_recipe'] = json.dumps(human)
        body['demo_game_ready'] = False
        eyes = next((o for o in created if getattr(o,'MPFB_GEN_object_type','')=='Eyes'),None)
        head = sum((eyes.matrix_world@Vector(v) for v in eyes.bound_box), Vector())/8 if eyes else Vector((0,-.02,1.62))
        collection['head_center'] = list(head)
        collection['recipe'] = json.dumps(recipe)
        collection['builder'] = 'MPFB'
        evidence.append({'id':recipe['id'],'objects':len(created),'body_vertices':len(body.data.vertices),'morphs':len(body.data.shape_keys.key_blocks),'rig_bones':len(rig.data.bones),'head_center':list(head)})
        print('MPFB_CREATED',recipe['id'],evidence[-1],flush=True)
    for image in bpy.data.images:
        if image.source=='FILE' and image.filepath:
            image.pack()
    bpy.context.scene['human_demo_recipes'] = json.dumps(recipes)
    bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'MPFB - Appearance Candidates.blend'),compress=True)
    (ROOT/'mpfb-build-evidence.json').write_text(json.dumps(evidence,indent=2))
    print('MPFB_DEMO_READY',flush=True)


if __name__=='__main__':
    main()
