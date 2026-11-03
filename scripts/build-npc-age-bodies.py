"""Create age-appropriate MPFB bodies, using installed CC0 sources and native rigs.

Exports geometry only. Unity reuses the existing skin/eye/hair materials and
motion banks; original adult source files are never modified.
"""
import argparse
import json
import sys
from pathlib import Path
import bpy
from mathutils import Vector

REPO = Path(__file__).resolve().parent.parent
AUTHORING = REPO / 'assets-source/humans/research/builders'
OUTPUT = REPO / 'assets-source/humans/npc-ages'
sys.path.insert(0, str(REPO / 'tools/mpfb2/src'))
original_extension_path = bpy.utils.extension_path_user
bpy.utils.extension_path_user = lambda package, **kwargs: str(AUTHORING / 'mpfb-user') if package == 'mpfb' else original_extension_path(package, **kwargs)
import mpfb
bpy.context.preferences.addons.new().module = 'mpfb'
mpfb.register()
from mpfb.services.humanservice import HumanService
from mpfb.services.locationservice import LocationService
from mpfb.services.targetservice import TargetService
LocationService._user_data = str(AUTHORING / 'mpfb-assets')

AGES = [(3, .045), (8, .15), (13, .24)]

def build(recipe, years, macro_age):
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)
    variant = recipe['id']
    macro = TargetService.get_default_macro_info_dict()
    for key in ['gender', 'height', 'weight', 'muscle', 'proportions']:
        macro[key] = recipe[key]
    macro['age'] = macro_age
    macro['weight'] = .48
    macro['muscle'] = .4
    macro['race'] = dict(zip(['african', 'asian', 'caucasian'], recipe['race']))
    human = HumanService._create_default_human_info_dict()
    hair = 'ponytail01' if recipe['gender'] == 0 else 'short02'
    human.update(name='MPFB_' + variant, phenotype=macro, rig='game_engine',
                 eyes='high-poly/high-poly.mhclo', eyebrows='eyebrow001/eyebrow001.mhclo',
                 eyelashes='eyelashes01/eyelashes01.mhclo', teeth='teeth_base/teeth_base.mhclo',
                 hair=hair + '/' + hair + '.mhclo', clothes=[],
                 skin_material_type='NONE', eyes_material_type='NONE',
                 targets=[])
    settings = HumanService.get_default_deserialization_settings()
    settings['subdiv_levels'] = 0
    body = HumanService.deserialize_from_dict(human, settings)
    rig = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')
    rig.name = 'MPFB_' + variant
    rig.location = Vector((0, 0, 0))
    rig.animation_data_clear()
    rig.data.pose_position = 'REST'
    surfaces = []
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    for obj in meshes:
        kind = getattr(obj, 'MPFB_GEN_object_type', '')
        stem = obj.name.split('.', 1)[-1]
        label = 'Body' if obj == body else 'Eyes' if 'high-poly' in stem else 'Teeth' if 'teeth' in stem else 'Eyebrows' if 'eyebrow' in stem else 'Eyelashes' if 'eyelashes' in stem else 'Hair'
        obj.name = 'Young_' + variant + '_' + label
        for modifier in list(obj.modifiers):
            if modifier.type == 'SUBSURF' or modifier.type == 'MASK' and modifier.name.startswith('Delete.'):
                obj.modifiers.remove(modifier)
        for polygon in obj.data.polygons:
            polygon.use_smooth = True
        surfaces.append(dict(object=obj.name, kind=label))
    bpy.context.view_layer.update()
    eyes = next(o for o in meshes if 'Eyes' in o.name)
    head = sum((eyes.matrix_world @ Vector(p) for p in eyes.bound_box), Vector()) / 8
    name = variant + '-' + str(years)
    folder = OUTPUT / name
    folder.mkdir(parents=True, exist_ok=True)
    for selected in bpy.context.selected_objects:
        selected.select_set(False)
    rig.hide_set(False)
    for obj in [rig, *meshes]:
        obj.hide_set(False)
        obj.select_set(True)
    bpy.context.view_layer.objects.active = rig
    rig.data.pose_position = 'POSE'
    for bone in rig.pose.bones:
        bone.matrix_basis.identity()
    bpy.ops.export_scene.fbx(filepath=str(folder / (name + '.fbx')), use_selection=True,
                            object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False,
                            axis_forward='-Z', axis_up='Y', bake_anim=False,
                            use_mesh_modifiers=True, use_custom_props=False,
                            path_mode='STRIP', mesh_smooth_type='FACE', use_triangles=True)
    record = dict(variant=variant, years=years, eye_height=head.z, surfaces=surfaces,
                  recipe=macro, license='MPFB/MakeHuman CC0 exported character output')
    (folder / 'body.json').write_text(json.dumps(record, indent=2))
    print('NPC_AGE_EXPORTED', name, head.z, flush=True)
    return record

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--variants', default='ABCDEF')
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
    recipes = json.loads((REPO / 'scripts/human_demo_recipes.json').read_text())
    OUTPUT.mkdir(parents=True, exist_ok=True)
    records = [build(recipe, years, macro) for recipe in recipes if recipe['id'] in args.variants for years, macro in AGES]
    (OUTPUT / 'manifest.json').write_text(json.dumps(records, indent=2))
