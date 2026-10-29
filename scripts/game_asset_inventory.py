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
    native=ROOT/'artifacts/fauna-runtime-20261026/native-inventory.json'
    add(native,'authoring-metadata',True)
    for animal in json.loads(native.read_text())['animals']:
        path=ROOT/animal['source'];add(path,'animal-sources',True)
        expected[path.relative_to(ROOT).as_posix()]=animal['sha256']
    human='artifacts/human-animation-preview/Human - Animation Comparison.blend'
    add(human,'human-sources',True)
    expected[human]='0946d797f3c620492444f2ec65d2a88280053fbda2c882dde3dfaf826942d229'
    baseline=ROOT/'artifacts/human-running-runtime/texture-baseline.json'
    add(baseline,'authoring-metadata',True)
    for texture in json.loads(baseline.read_text()):
        path=ROOT/'artifacts/human-runtime/exports'/texture['path'];add(path,'human-sources',True)
        expected[path.relative_to(ROOT).as_posix()]=texture['sha256']
    tree('artifacts/fauna-runtime-20261026/exports','animal-export-inputs',{'.bin','.json','.png'})
    tree('artifacts/human-runtime/exports','human-sources',{'.fbx','.png','.json'})
    tree('artifacts/human-running-runtime','human-motion-inputs',{'.fbx'})
    tree('unity/WorldGen.Unity/Assets/StreamingAssets/fauna','animal-runtime',{'','.fauna','.json','.manifest','.meta','.md','.txt'})
    for directory in ['unity/WorldGen.Unity/Assets/WorldGen/Resources/Humans','unity/WorldGen.Unity/Assets/WorldGen/GeneratedHumans','unity/WorldGen.Unity/Assets/WorldGen/Editor/GeneratedHumans']:
        tree(directory,'human-runtime',{'.asset','.prefab','.mat','.meta','.fbx','.png','.json'})
        add(directory+'.meta','human-runtime')
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
