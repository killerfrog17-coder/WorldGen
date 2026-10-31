"""Delete candidates are regenerable test outputs, never source or world saves."""
import os,json,subprocess,collections
from pathlib import Path
ROOT=Path(__file__).resolve().parent.parent
ART=ROOT/'artifacts';OUT=ART/'player-retention-20261031'
protected=set()
for name in ['game-assets.lock.json','legacy-runtime-assets.lock.json']:
    protected.update((ROOT/p).resolve() for p in json.loads((ROOT/'assets'/name).read_text())['files'])
protected.update((ROOT/p).resolve() for p in subprocess.check_output(['git','ls-files','-z'],cwd=ROOT).decode().split('\0') if p)
keep={'player-human-natural-review','player-world-feedback','world-feedback-20261031','player-retention-20261031','human-physique-review','climate-learning'}
extensions={'.png','.jpg','.jpeg','.webp','.bin','.raw','.obj','.ply','.exr','.mp4','.gif','.gz','.npy','.npz'}
rows=[];sizes=collections.defaultdict(int)
for entry in ART.iterdir():
    if not entry.is_dir() or entry.name in keep or entry.name.startswith('player-'):continue
    physical=entry.resolve()
    if not any(physical.is_relative_to(p) for p in [ART.resolve(),Path('D:/WorldGenBuildArtifacts'),Path('E:/WorldGen-builds')]):continue
    for base,dirs,files in os.walk(physical,followlinks=False):
        dirs[:]=[d for d in dirs if not (getattr((Path(base)/d).lstat(),'st_reparse_tag',0) in {0xA0000003,0xA000000C}) and d not in {'.git','worlds','saves','Saves','SaveGames'}]
        parts=Path(base).relative_to(physical).parts
        diagnostic=entry.name.startswith(('survival-runtime','survival-')) and ('native' in entry.name or 'runtime' in entry.name) or entry.name.startswith(('human-natural-native','fps-diagnostic')) or any(p.lower().startswith(('native-','native_','screenshots','captures','previews','renders','render-','test-output')) for p in parts)
        cache=entry.name in {'unity-source-check','unity-editor'} and any(p in {'Library','Temp','Logs','obj','bin'} for p in parts)
        for name in files:
            p=Path(base)/name
            if p.resolve() in protected:continue
            if p.suffix.lower() in {'.world','.blend','.fbx','.glb','.gltf','.f32','.safetensors','.pt','.pth','.ckpt','.onnx'}:continue
            if cache or diagnostic and p.suffix.lower() in extensions and '.world' not in name:
                size=p.stat().st_size;rows.append({'path':str(p),'bytes':size});sizes[entry.name]+=size
report={'roots':[str(ART.resolve()),'D:/WorldGenBuildArtifacts','E:/WorldGen-builds'],'protected':[str(p) for p in protected],'files':rows,'bytes':sum(r['bytes'] for r in rows),'groups':dict(sizes)}
(OUT/'test-output-plan.json').write_text(json.dumps(report,indent=2))
print(json.dumps({'files':len(rows),'GiB':report['bytes']/2**30,'groups':{k:round(v/2**30,3) for k,v in sorted(sizes.items(),key=lambda kv:-kv[1])[:20]}},indent=2))
