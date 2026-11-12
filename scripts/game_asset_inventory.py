"""Exact build/runtime and approved authoring inputs, excluding test players.

Large ignored inputs are versioned by game-assets.lock.json and GitHub release
packs. Ordinary tracked source/assets remain ordinary Git objects.
"""
import concurrent.futures
import hashlib
import json
import subprocess
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]


def sha(path):
    result=hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda:stream.read(4*1024*1024),b''):
            result.update(block)
    return result.hexdigest()


def inventory():
    tracked=set(subprocess.check_output(['git','ls-files','-z'],cwd=ROOT).decode('utf-8').split('\0'))
    files={}
    expected={}
    def add(path,group,required=False):
        path=Path(path)
        if not path.is_absolute():
            path=ROOT/path
        if not path.is_file():
            if required:
                raise FileNotFoundError(path)
            return
        relative=path.relative_to(ROOT).as_posix()
        if path.suffix.lower() in {'.exe','.dll','.pdb','.key','.pem','.pfx','.p12'} or path.name.startswith('.env'):
            raise ValueError('Not a game-asset input: '+relative)
        files.setdefault(relative,{'path':relative,'group':group,'bytes':path.stat().st_size,'tracked':relative in tracked})
    def tree(directory,group,suffixes):
        path=ROOT/directory
        if path.exists():
            for p in path.rglob('*'):
                if p.is_file() and p.suffix.lower() in suffixes:
                    add(p,group)
    native=ROOT/'assets-source/animals/export-metadata/native-inventory.json'
    add(native,'authoring-metadata',True)
    add('assets-source/animals/export-metadata/export-plan.json','authoring-metadata',True)
    for animal in json.loads(native.read_text())['animals']:
        path=ROOT/animal['source'];add(path,'animal-sources',True)
        expected[path.relative_to(ROOT).as_posix()]=animal['sha256']
    human='assets-source/humans/base/Human - Animation Comparison.blend'
    add(human,'human-sources',True)
    expected[human]='0946d797f3c620492444f2ec65d2a88280053fbda2c882dde3dfaf826942d229'
    baseline=ROOT/'assets-source/humans/motion/legacy/texture-baseline.json'
    add(baseline,'authoring-metadata',True)
    for texture in json.loads(baseline.read_text()):
        path=ROOT/'assets-source/humans/exports'/texture['path'];add(path,'human-sources',True)
        expected[path.relative_to(ROOT).as_posix()]=texture['sha256']
    tree('assets-source/animals/exports','animal-export-inputs',{'.bin','.json','.png'})
    tree('assets-source/humans/exports','human-sources',{'.fbx','.png','.json'})
    add('assets-source/humans/appearance-review/Human physique comparison.blend','human-appearance-source',True)
    tree('assets-source/humans/appearance-runtime','human-appearance-source',{'.fbx','.png','.json'})
    tree('assets-source/humans/npc-ages','human-npc-source',{'.fbx','.json'})
    tree('assets-source/humans/phenotypes','human-phenotype-source',{'.fbx','.json','.txt'})
    for sourceRoot,names in [('research/builders/mpfb-assets',['short02','ponytail01','afro01']),('appearance-review/mpfb-assets',['long01','braid01','cortu_short_messy_hair','elvs_unkempt_french_braid'])]:
        for name in names:
            tree('assets-source/humans/'+sourceRoot+'/hair/'+name,'human-phenotype-source',{'.obj','.mhclo','.mhmat','.png','.txt'})
    for directory in ['unity/WorldGen.Unity/Assets/WorldGen/GeneratedHumanPhenotypes','unity/WorldGen.Unity/Assets/WorldGen/Resources/HumanPhenotypes']:
        tree(directory,'human-phenotype-runtime',{'.asset','.prefab','.mat','.meta','.fbx','.png','.json'})
        add(directory+'.meta','human-phenotype-runtime')
    tree('assets-source/terrain/surfaces','terrain-authoring-source',{'.png','.jpg','.json','.txt'})
    tree('assets-source/terrain/rocks','terrain-rock-source',{'.blend','.json','.txt'})
    tree('unity/WorldGen.Unity/Assets/WorldGen/Resources/CliffRockV1','terrain-rock-runtime',{'.bytes','.json','.meta'})
    add('unity/WorldGen.Unity/Assets/WorldGen/Resources/CliffRockV1.meta','terrain-rock-runtime')
    tree('assets-source/vegetation/tree-surfaces','tree-authoring-source',{'.png','.jpg','.json','.txt'})
    tree('assets-source/vegetation/ground-clumps','ground-clump-authoring-source',{'.blend','.png','.jpg','.exr','.json','.txt'})
    for directory in ['unity/WorldGen.Unity/Assets/WorldGen/Editor/TerrainSurfaceArtV1','unity/WorldGen.Unity/Assets/WorldGen/Resources/TerrainSurfaceArtV1','unity/WorldGen.Unity/Assets/WorldGen/Editor/CliffSurfaceArtV1','unity/WorldGen.Unity/Assets/WorldGen/Resources/CliffSurfaceArtV1']:
        tree(directory,'terrain-runtime-art',{'.png','.asset','.bytes','.meta','.txt'})
        add(directory+'.meta','terrain-runtime-art')
    add('unity/WorldGen.Unity/Assets/WorldGen/Resources/GroundMossV1.png','terrain-runtime-art',True)
    add('unity/WorldGen.Unity/Assets/WorldGen/Resources/GroundMossV1.png.meta','terrain-runtime-art')
    add('unity/WorldGen.Unity/Assets/WorldGen/Resources/GroundLichenV1.png','terrain-runtime-art',True)
    add('unity/WorldGen.Unity/Assets/WorldGen/Resources/GroundLichenV1.png.meta','terrain-runtime-art')
    for directory in ['unity/WorldGen.Unity/Assets/WorldGen/Editor/TreeSurfaceArtV1','unity/WorldGen.Unity/Assets/WorldGen/Resources/TreeSurfaceArtV1']:
        tree(directory,'tree-runtime-art',{'.png','.asset','.bytes','.meta'})
        add(directory+'.meta','tree-runtime-art')
    tree('unity/WorldGen.Unity/Assets/WorldGen/Resources/TreePrototypesV1','tree-runtime-art',{'.asset','.meta'})
    add('unity/WorldGen.Unity/Assets/WorldGen/Resources/TreePrototypesV1.meta','tree-runtime-art')
    tree('assets-source/humans/motion/npc','human-npc-source',{'.f32','.json','.txt'})
    tree('assets/human-motion-npc','human-npc-source',{'.fbx','.json'})
    for hair in ['ponytail01','short02']:
        add('assets-source/humans/research/builders/mpfb-assets/hair/'+hair+'/'+hair+'_diffuse.png','human-npc-source',True)
    tree('assets-source/humans/motion/legacy','human-motion-inputs',{'.fbx'})
    tree('assets-source/humans/motion/source-library','human-motion-inputs',{'.bvh','.fbx','.glb','.bin','.json','.txt','.md'})
    tree('assets-source/humans/motion/animation-library','human-motion-inputs',{'.bvh','.fbx','.glb','.bin','.json','.txt','.md'})
    tree('unity/WorldGen.Unity/Assets/StreamingAssets/fauna','animal-runtime',{'','.fauna','.coats','.json','.manifest','.meta','.md','.txt'})
    for directory in ['unity/WorldGen.Unity/Assets/WorldGen/Resources/Humans','unity/WorldGen.Unity/Assets/WorldGen/GeneratedHumans','unity/WorldGen.Unity/Assets/WorldGen/Editor/GeneratedHumans','unity/WorldGen.Unity/Assets/WorldGen/GeneratedHumanAppearance','unity/WorldGen.Unity/Assets/WorldGen/Resources/HumanAppearance']:
        tree(directory,'human-runtime',{'.asset','.prefab','.mat','.meta','.fbx','.png','.json'})
        add(directory+'.meta','human-runtime')
    for directory in ['unity/WorldGen.Unity/Assets/WorldGen/GeneratedHumanAges','unity/WorldGen.Unity/Assets/WorldGen/GeneratedHumanNpcMotion','unity/WorldGen.Unity/Assets/WorldGen/Resources/HumanAges','unity/WorldGen.Unity/Assets/WorldGen/Resources/HumanNpcMotion']:
        tree(directory,'human-npc-runtime',{'.asset','.prefab','.mat','.meta','.fbx','.png','.json'})
        add(directory+'.meta','human-npc-runtime')
    add('unity/WorldGen.Unity/Assets/StreamingAssets/fauna.meta','animal-runtime')
    add('unity/WorldGen.Unity/Assets/StreamingAssets/world/Earth.world','default-world',True)
    add('unity/WorldGen.Unity/Assets/StreamingAssets/world/Earth.world.meta','default-world')
    add('unity/WorldGen.Unity/Assets/StreamingAssets/world.meta','default-world')
    tree('unity/WorldGen.Unity/Assets/StreamingAssets/atlas','static-world-data',{'.jpg','.png','.json','.meta'})
    add('unity/WorldGen.Unity/Assets/StreamingAssets/atlas.meta','static-world-data')
    tree('src/WorldGen.Web/wwwroot/atlas','static-world-data',{'.jpg','.png','.json'})
    tree('src/WorldGen.Web/wwwroot/world','static-world-data',{'.world','.json'})
    tree('src/WorldGen.Web/wwwroot/music','music-library',{'.ogg','.wav','.json','.txt'})
    tree('sounds','sound-library',{'.wav','.ogg','.mp3','.flac','.tsv','.json','.txt','.md'})
    tree('music','music-library',{'.wav','.ogg','.mp3','.flac','.txt','.json','.tsv'})
    # These roots are an explicit allow-list. World saves, model weights,
    # interpreters, Unity/Blender caches and old players are never traversed.
    records=sorted(files.values(),key=lambda r:(r['group'],r['path']))
    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
        digests=list(pool.map(lambda r:sha(ROOT/r['path']),records))
    for record,digest in zip(records,digests):
        record['sha256']=digest
        if record['path'] in expected and expected[record['path']]!=digest:
            raise ValueError('Approved authoring input changed: '+record['path'])
    return records


if __name__=='__main__':
    import argparse
    parser=argparse.ArgumentParser()
    parser.add_argument('output',type=Path)
    args=parser.parse_args()
    records=inventory()
    args.output.parent.mkdir(parents=True,exist_ok=True)
    args.output.write_text(json.dumps(records,indent=2))
    external=[r for r in records if not r['tracked']]
    print(json.dumps({'input_files':len(records),'external_files':len(external),'external_GiB':sum(r['bytes'] for r in external)/2**30,'groups':{g:round(sum(r['bytes'] for r in external if r['group']==g)/2**30,3) for g in sorted({r['group'] for r in external})}}))
