"""Record source-matched NPC evidence; reject stale packages and failed cases."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import subprocess

ROOT=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--source',required=True)
parser.add_argument('--slot',choices=['slot-a','slot-b'],default='slot-a')
parser.add_argument('--evidence',type=Path,default=ROOT/'artifacts/npc-20261103')
parser.add_argument('--prefix',default='accepted')
args=parser.parse_args();evidence=args.evidence.resolve();player=ROOT/'builds'/args.slot
def read(p):
    raw=p.read_bytes();return raw.decode('utf-16' if raw[:2] in (b'\xff\xfe',b'\xfe\xff') else 'utf-8-sig')
def load(p):return json.loads(read(p))
def sha(p):
    with p.open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest()
stamp=load(player/'build-inputs.json')
assert stamp['Backend']=='Mono' and not stamp['Development']
expected={'AssemblySha256':sha(player/'WorldGen_Data/Managed/Assembly-CSharp.dll'),
    'CoreSha256':sha(player/'WorldGen_Data/Managed/WorldGen.Core.dll'),
    'WorkerSha256':sha(player/'TerrainWorker/WorldGen.Cli.exe')}
assert expected['WorkerSha256']==stamp['TerrainWorkerSha256']
for assembly in stamp['Inputs']:
    assert sha(player/'WorldGen_Data/Managed'/(assembly['Assembly']+'.dll'))==assembly['Sha256']
cases=[]
for suffix in ['poses','wet','cold','aquatic','collector','start']:
    name=args.prefix+'-'+suffix
    folder=evidence/name
    assert int(read(folder/'exit-code.txt'))==0,name
    assert not (folder/'failure.txt').exists(),name
    receipt=load(folder/'package.json')
    assert all(receipt[k]==v for k,v in expected.items()),(name,'Stale package')
    checks=read(folder/'checks.txt').splitlines()
    assert checks and all(row.startswith('PASS ') for row in checks),name
    if suffix=='poses':assert len(checks)>=450
    else:
        for phrase in ['Natural residents stream','Scheduled home residents','surrounding scatter pages','Actual named binary save','Aggression preserves','does not duplicate']:
            assert any(phrase in row for row in checks),(name,phrase)
    cases.append({'name':name,'checks':len(checks),'scene':load(folder/'scene-info.json') if suffix!='poses' else None,
        'checks_sha256':sha(folder/'checks.txt'),'images':{p.name:sha(p) for p in folder.glob('*.png')}})
for name in [args.prefix+'-'+suffix for suffix in ['wet','cold','collector']]:
    text=read(evidence/name/'checks.txt');assert 'can enter' in text and 'can leave' in text,name
for name in [args.prefix+'-'+suffix for suffix in ['wet','collector']]:
    text=read(evidence/name/'checks.txt');assert 'scheduled packing' in text and 'finishes every roof' in text,name
assert 'remains occupied' in read(evidence/(args.prefix+'-aquatic')/'checks.txt')
assert next(c for c in cases if c['name']==args.prefix+'-start')['scene']['Year']==0
assert 'PASS 28242' in read(ROOT/'artifacts/npc-final-core-life.log')
assert 'PASS 13132' in read(ROOT/'artifacts/npc-final-core-blueprints.log')
proof=load(evidence/'source-input-proof.json');assert proof['original_locked_inputs_unchanged']==5048
performance=load(evidence/(args.prefix+'-wet')/'performance.json')
assert performance[0]['Residents']>=25
assert performance[0]['MeanNpcMs']<4 and performance[-1]['MeanNpcMs']<4
changed=subprocess.check_output(['git','diff','--name-only','pre-living-npcs-20261103',args.source,'--','assets-source','sounds','music'],cwd=ROOT,text=True).splitlines()
assert all(p.startswith(('assets-source/humans/npc-ages/','assets-source/humans/motion/npc/')) for p in changed),changed
manifest={'recorded_utc':datetime.now(timezone.utc).isoformat(),'source':args.source,'baseline':'pre-living-npcs-20261103',
    'slot':'builds/'+args.slot,'build':stamp,'package':expected,'cases':cases,'performance':performance,'asset_inputs':proof,
    'research':'assets/verification/npc-research-20261103.json',
    'architecture':{'broad_radius_km':50,'broad_state':'Regional history plus on-demand seeded community itineraries; no global tribe/person tick ledger',
        'local_detail':'Progressive nearby family actors, real material-backed camps, work/rest/childcare and physical walking',
        'persistence':'Visited camp materials, local wounds/deaths, community alarm/trust and decaying occupation traces'},
    'review_commands':['/camps','/camps <number>','/camps visit <number>'],
    'visual_review':{'method':'Actual game-camera renders with instanced scatter and GPU ground cover submitted; no blank standalone environment used for camp acceptance.',
        'reviewed':['Forest camp floors and wider hearth seats clear grass while retaining surrounding vegetation and mature trunks.',
            'Cold camp conical hide shelters follow the slope, retain roof components and admit seated residents.',
            'Body/age poses and linked infant carry/ground-rest scenes were inspected separately.'],
        'rejected_iteration':'The earlier courts-wet candidate imposed a 7m treeless hearth footprint, displaced the camp outside near vegetation rendering and was rejected. Final uses a small structural fire clearance with wider grass wear.'},
    'limits':['No LLM conversation, NPC hunting or complete NPC economic inventory/consumption loop yet.',
        'All human lineages reuse the existing human appearance families; species-specific bodies are deferred.',
        'Seasonal movement is a deterministic broad projection with local obstacle/doorway steering, not a global per-person path simulation.',
        'Reported timings are this machine/scene; rows explicitly identify whether terrain loading had settled.',
        'Full-scale first-time global river fitting is an existing startup cost (152 seconds measured); the shared exact cache avoids repeating it.',
        'NPC visuals remain subject to owner review. Landscape region and tree overhauls are separate later stages.']}
output=ROOT/'assets/verification/living-npcs-20261103.json';output.write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8');print(output)
