"""Verify the complete read-only export, retain credits, and stage streaming data.

Does not launch a player, generate a world, or change any native animal asset.
"""
import argparse,hashlib,json,shutil
from pathlib import Path

repo=Path(__file__).resolve().parent.parent
root=repo/'artifacts/fauna-runtime-20261026'
ap=argparse.ArgumentParser();ap.add_argument('--player-folder');a=ap.parse_args()
plan=json.loads((root/'export-plan.json').read_text())['animals']
catalogue=json.loads((repo/'unity/WorldGen.Unity/Assets/WorldGen/Resources/WorldGenFaunaCatalogue.json').read_text())['animals']
inventory={r['slug']:r for r in json.loads((root/'native-inventory.json').read_text())['animals']}
assert len(plan)==len(catalogue)==177 and {r['slug'] for r in plan}=={r['slug'] for r in catalogue}
signature=hashlib.sha256((repo/'scripts/export-fauna-runtime.py').read_bytes()+(repo/'scripts/fauna_runtime_surface.py').read_bytes()+(repo/'scripts/fauna_runtime_uv.py').read_bytes()).hexdigest()
output=repo/'unity/WorldGen.Unity/Assets/StreamingAssets/fauna';output.mkdir(parents=True,exist_ok=True)
rows=[];worst=0;clips=0;coats=0
for item in plan:
    slug=item['slug'];folder=root/'exports'/slug;receipt=json.loads((folder/'receipt.json').read_text());model=json.loads((folder/'model.json').read_text())
    assert receipt['complete'] and receipt['exporter_sha256']==signature,slug
    assert receipt['source_sha256']==item['source_sha256']==model['source_sha256']==hashlib.sha256(Path(item['path']).read_bytes()).hexdigest(),slug
    for name,digest in receipt['files'].items():assert hashlib.sha256((folder/name).read_bytes()).hexdigest()==digest,(slug,name)
    assert all(c['role'] in {'idle','walk','trot','run','fly','glide','swim'} for c in model['clips']),slug
    assert model['bones']<=128 and model['parts']<=64 and model['influences'] in {4,8},slug
    checks=model['nativeSkinChecks'];assert len(checks)==sum(min(3,c['frames']) for c in model['clips']),slug
    for clip in model['clips']:
        assert {c['frame'] for c in checks if c['clip']==clip['name']}=={0,clip['frames']//2,clip['frames']-1},(slug,clip['name'])
    error=max(c['normalized_error_max'] for c in checks);assert error<.0001,slug;worst=max(worst,error)
    bundle=output/(slug+'.fauna');assert bundle.is_file(),('Missing bundle',slug)
    clips+=len(model['clips']);coats+=len(model['coats'])
    rows.append({'slug':slug,'name':item['name'],'source_sha256':item['source_sha256'],
                 'bundle':bundle.name,'bundle_sha256':hashlib.sha256(bundle.read_bytes()).hexdigest(),'bundle_bytes':bundle.stat().st_size,
                 'motion_source_family':inventory[slug]['entry'].get('source_family','Owner-approved deer motion'),
                 'motion_license_note':model['sourceLicense'],'clips':model['clips'],'coat_count':len(model['coats']),
                 'native_skin_sample_max_error':error,'influences':model['influences'],'has_native_motion_morph':model.get('hasMorph',False)})
licenses=output/'licenses';licenses.mkdir(exist_ok=True)
source_root=repo/'artifacts/fauna-motion-sources'
# These small source-side records preserve the upstream URLs and license
# statements. No donor geometry, source video or neural checkpoint is shipped.
for file in source_root.rglob('*'):
    if not file.is_file() or file.stat().st_size>100000:continue
    name=file.name.lower()
    if 'license' not in name and name not in {'provenance.json','download-provenance.json','retained-source-provenance.json'}:continue
    relative=file.relative_to(source_root);dest=licenses/relative;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(file,dest)
(output/'ATTRIBUTION.txt').write_text('''WorldGen animal placeholder assets

The selected geometry and painted materials are owner-generated/authored game
assets. Their motion-source CC0 notes do not dedicate all WorldGen artwork or
game source to CC0. No third-party neural model checkpoints are distributed.

Existing fitted movement derives from the previously approved CC0 sources,
including Mesh2Motion animation assets (afk-rig0.1.2), OpenGameArt contributors
and Riley's Low Poly Ice Age pack, plus owner-authored simple microfauna and
other procedural movement. See assets.json for each selected native SHA and
motion family, and licenses/ for the retained upstream source/license records.

Mesh2Motion: https://github.com/Mesh2Motion/mesh2motion-app
CC0: https://creativecommons.org/publicdomain/zero/1.0/
Riley: https://rkuhlf-assets.itch.io/ice-age

Runtime conversion repacks existing pigment materials, mesh attributes and
native pose palettes. It introduces no newly retargeted animation or donor
animal mesh. The gull's original driven wing-fold morph is retained.
''')
manifest={'version':1,'animals':rows,'count':len(rows),'movement_clips':clips,'coat_layers':coats,
          'maximum_sampled_native_skin_error':worst,'runtime_game_launched':False,
          'validation_scope':'Source/output hashes; sampled native surface skinning; catalogue coverage; bundle presence. Not gameplay, full-frame contact or performance acceptance.'}
(output/'assets.json').write_text(json.dumps(manifest,indent=2)+'\n')
(root/'package-audit.json').write_text(json.dumps(manifest,indent=2)+'\n')
if a.player_folder:
    player=(repo/'artifacts'/a.player_folder).resolve();assert player.is_relative_to((repo/'artifacts').resolve())
    dest=player/'WorldGen_Data/StreamingAssets/fauna';dest.mkdir(parents=True,exist_ok=True)
    for file in output.rglob('*'):
        if not file.is_file() or file.suffix=='.meta' or file.suffix=='.manifest' or file.name=='fauna':continue
        target=dest/file.relative_to(output);target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(file,target)
    for row in rows:assert hashlib.sha256((dest/row['bundle']).read_bytes()).hexdigest()==row['bundle_sha256']
print('VERIFIED',len(rows),'animals,',clips,'movement clips,',coats,'coat layers; max sampled native error',worst)
print('BUNDLE SIZE',round(sum(r['bundle_bytes'] for r in rows)/1024**2,1),'MiB')
