"""Record reviewed, source-matched tree art and an uncontended camp comparison."""
import argparse
from datetime import datetime, timezone
import hashlib, json, re, statistics, subprocess
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--source',required=True)
p.add_argument('--slot',choices=['slot-a','slot-b'],default='slot-a')
p.add_argument('--prefix',default='trees-release')
p.add_argument('--performance-prefix',default='trees-release-perf')
p.add_argument('--editor',default='artifacts/tree-tight-quads-3')
p.add_argument('--visual-review',type=Path,required=True)
args=p.parse_args()

def read(path):
    b=path.read_bytes();return b.decode('utf-16' if b[:2] in [b'\xff\xfe',b'\xfe\xff'] else 'utf-8-sig')
def load(path):return json.loads(read(path))
def sha(path):
    with path.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def passed(folder):
    assert int(read(folder/'exit-code.txt'))==0 and not (folder/'failure.txt').exists(),folder

source=subprocess.check_output(['git','rev-parse',args.source],cwd=ROOT,text=True).strip()
subprocess.run(['git','diff','--exit-code',source,'--','src','unity/WorldGen.Unity/Assets/WorldGen/Runtime','unity/WorldGen.Unity/Assets/WorldGen/Resources','unity/WorldGen.Unity/Assets/WorldGen/Editor'],cwd=ROOT,check=True)
player=ROOT/'builds'/args.slot;stamp=load(player/'build-inputs.json')
assert stamp['Backend']=='Mono' and not stamp['Development']
package={'AssemblySha256':sha(player/'WorldGen_Data/Managed/Assembly-CSharp.dll'),
         'CoreSha256':sha(player/'WorldGen_Data/Managed/WorldGen.Core.dll'),
         'WorkerSha256':sha(player/'TerrainWorker/WorldGen.Cli.exe')}
assert package['WorkerSha256']==stamp['TerrainWorkerSha256']
packed={f.name:sha(f) for f in (player/'WorldGen_Data').glob('*') if f.is_file() and f.suffix in ['.assets','.resS','.resource']}
assert packed
review=load(args.visual_review);scenes=[]
for kind in ['Forest','TropicalRainforest','BorealForest','DryWoodland','WoodedSwamp','Savanna']:
    folder=ROOT/'artifacts/landscape-20261103'/(args.prefix+'-'+kind.lower());passed(folder)
    assert all(load(folder/'package.json')[k]==v for k,v in package.items()),'Stale scene package '+kind
    scene=load(folder/'scene.json');assert scene['Requested']==kind
    assert scene['TreeQuality']==3 and scene['TreeNear']==26 and scene['TreeFar']==70
    if kind in ['Forest','DryWoodland','WoodedSwamp']:assert scene['Actual']==kind
    else:assert scene['SampleBiome']==kind
    checks=read(folder/'checks.txt').splitlines();assert len(checks)>=10 and all(c.startswith('PASS ') for c in checks)
    assert sum('Visible distant-tree models complete' in c for c in checks)==2
    assert sum('midday summer illumination' in c for c in checks)==2
    images={n:sha(folder/(n+'.png')) for n in ['wide','ground']}
    assert review[kind]['accepted'] and review[kind]['images']==images,'Unreviewed images '+kind
    scenes.append({'kind':kind,'package':load(folder/'package.json'),'scene':scene,'checks':len(checks),'checks_sha256':sha(folder/'checks.txt'),'images':images,'review':review[kind]})

shaders=[]
for kind in ['tree','forest']:
    folder=ROOT/'artifacts/tree-20261103'/(args.prefix+'-'+kind+'-shader');passed(folder)
    receipt=load(folder/'package.json')
    assert all(receipt[n].lower()==package[k] for n,k in [('Assembly','AssemblySha256'),('Core','CoreSha256'),('Worker','WorkerSha256')])
    text=read(folder/'PASS.txt');assert 'tight quad max RGB delta' in text and 'early alpha max RGB delta 0' in text
    shaders.append({'kind':kind,'receipt':receipt,'pass_sha256':sha(folder/'PASS.txt'),'results':text})

editor=ROOT/args.editor;support=load(editor/'surface-support.json')
assert support['CpuPixelBytes']==19398656 and support['ShootGpuBytes']==22371328 and support['BarkGpuBytes']==3495520
assert all(r['ClippedPixels']==0 for r in support['Layers'])
models=read(editor/'results.txt').splitlines();assert len(models)==11
areas=[float(x) for x in re.findall(r'mean-area-ratio=([0-9.]+)',read(editor/'quad-area.txt'))];assert len(areas)==11
pixels=read(editor/'quad-pixels.txt').splitlines();assert len(pixels)==33
significant=[int(x) for x in re.findall(r'significant=([0-9]+)', '\n'.join(pixels))]
means=[float(x) for x in re.findall(r'mean=([0-9.]+)', '\n'.join(pixels))]
assert max(significant)<=64 and max(means)<=.02

performance={}
for name in ['baseline','candidate']:
    folder=ROOT/'artifacts/npc-20261103'/(args.performance_prefix+'-'+name);passed(folder)
    receipt=load(folder/'package.json')
    if name=='candidate':assert all(receipt[k]==v for k,v in package.items()),'Stale measured candidate'
    rows=load(folder/'performance.json');assert len(rows)==4 and rows[0]['Residents']>=25
    checks=read(folder/'checks.txt').splitlines();assert len(checks)>=18 and all(c.startswith('PASS ') for c in checks)
    for phrase in ['Actual named binary save','Unloading/revisiting','canonical tree trunks']:
        assert any(phrase in c for c in checks),(name,phrase)
    performance[name]={'package':receipt,'scene':load(folder/'scene-info.json'),'rows':rows,'checks':len(checks),'checks_sha256':sha(folder/'checks.txt'),'reviewed_camp_image_sha256':sha(folder/'residents-first.png')}
assert performance['baseline']['scene']==performance['candidate']['scene']
def frame(name):return statistics.mean(r['MeanFrameMs'] for r in performance[name]['rows'] if r['Mode'] in ['normal','normal-repeat'])
ratio=frame('candidate')/frame('baseline')
assert ratio<=1.10,('Measured camp frame regression requires investigation',ratio)
mode_ratios={r['Mode']:r['MeanFrameMs']/next(b['MeanFrameMs'] for b in performance['baseline']['rows'] if b['Mode']==r['Mode']) for r in performance['candidate']['rows']}
for mode in ['render-only','no-residents']:
    assert mode_ratios[mode]<=1.10,('Measured rendering regression requires investigation',mode,mode_ratios[mode])

lock=load(ROOT/'assets/game-assets.lock.json')
prior=json.loads(subprocess.check_output(['git','show','living-landscapes-20261103:assets/game-assets.lock.json'],cwd=ROOT))
assert all(lock['files'][n]['sha256']==r['sha256'] for n,r in prior['files'].items())
tree_inputs={n:r for n,r in lock['files'].items() if '/tree-surfaces/' in n or '/TreeSurfaceArtV1/' in n or n.endswith('/TreeSurfaceArtV1.meta')}
assert len(tree_inputs)>190
for n,r in tree_inputs.items():assert sha(ROOT/n)==r['sha256'] and r['pack'] in lock['packs'],n

manifest={'recorded_utc':datetime.now(timezone.utc).isoformat(),'source':source,'baseline':'living-landscapes-20261103','slot':'builds/'+args.slot,'build':stamp,'package':package,'packed_resources':packed,
 'art_sources':'assets/verification/tree-surface-sources-20261103.json','surface_budget':support,'model_reviews':models,'quad_area_ratios':areas,
 'quad_area_reduction_mean':1-statistics.mean(areas),'quad_comparison':{'poses':len(pixels),'max_significant_pixels':max(significant),'max_mean_rgb_error':max(means),'meaning':'All model vertices fit the projected boxes. Different quad interpolation can flip isolated alpha-edge pixels; mean RGB difference is measured on the 0..255 scale.'},
 'scenes':scenes,'shaders':shaders,'camp_comparison':performance,'camp_mean_frame_ratio':ratio,'camp_frame_ratios_by_mode':mode_ratios,
 'performance_conditions':'Players run sequentially with no competing game/build jobs. The camp harness deliberately retains identical heavy120/450m mesh ranges in both packages; landscape views explicitly use the real High26/70m preset. TerrainReady is retained per sample. Results are not a whole-world FPS guarantee.',
 'original_locked_inputs_unchanged':len(prior['files']),'tree_asset_inputs':len(tree_inputs),'locked_inputs':len(lock['files']),'immutable_packs':len(lock['packs']),
 'architecture':['Shared photographed branch and bark arrays, compressed BC7, coherent near/far surfaces.','Existing deterministic lineage/region placement retained; needle openness now affects crown structure.','Trapezoid shoot cards and projected tree bounding boxes reduce empty raster work.','Opaque colour, alpha and true surface attributes preserved while transparent mip texels receive valid normal/depth padding.','Active draw batches keep deferred tree bakes alive with a stationary camera; no new per-tree simulation ticks.'],
 'limits':['Generated tree architecture remains an approximation; no new live plant growth, seasonal leaf shedding or fruiting system.','Distant views remain128px16-direction proxies; no claim of mesh-level detail at all distances.','No replacement of the broader shrub/grass species catalogue or all of its artwork in this tree-focused pass.']}
out=ROOT/'assets/verification/tree-art-20261103.json';out.write_text(json.dumps(manifest,indent=2)+'\n')
print(json.dumps({'manifest':str(out),'scenes':len(scenes),'models':len(models),'camp_frame_ratio':ratio,'billboard_area_reduction':manifest['quad_area_reduction_mean']}))
