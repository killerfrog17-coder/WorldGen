"""Publish local SA3 recordings as mono, crossfaded fire loops; no synthesis."""
import hashlib
import json
from pathlib import Path

import numpy as np
import soundfile as sf

root=Path(__file__).resolve().parents[1]
task=root/'artifacts/camp-water-20261029'
output=root/'sounds/campfire';output.mkdir(parents=True,exist_ok=True)
selected=[('fire-medium','wood-fire-01'),('fire-audio','small-fire-03'),('fire-medium','wood-fire-02')]
published=[];screen=[]
for index,(batch,key) in enumerate(selected,1):
    record=json.loads((task/(batch+'-jobs/results.json')).read_text())[key]
    source=task/(batch+'-candidates')/(key+'.wav')
    assert hashlib.sha256(source.read_bytes()).hexdigest()==record['file_sha256']
    audit=json.loads((task/(batch+'-audit.json')).read_text())['files'][key+'.wav']
    assert not audit['screen_rejections'],(key,audit['screen_rejections'])
    audio,rate=sf.read(source,dtype='float32',always_2d=True)
    mono=audio.mean(axis=1)[rate//2:-rate//2].copy()
    overlap=rate
    theta=np.arange(overlap,dtype='float32')/overlap*(np.pi*.5)
    loop=np.concatenate([mono[-overlap:]*np.cos(theta)+mono[:overlap]*np.sin(theta),mono[overlap:-overlap]])
    rms=float(np.sqrt(np.mean(loop.astype('float64')**2)))
    loop*=min(10**(-24/20)/max(rms,1e-9),.65/max(float(np.abs(loop).max()),1e-9))
    path=output/f'wood-fire-{index:02}.wav';sf.write(path,loop,rate,subtype='PCM_16')
    decoded,sr=sf.read(path,dtype='float32');assert sr==rate and np.isfinite(decoded).all() and len(decoded)/sr>20
    jump=float(abs(decoded[-1]-decoded[0]));assert jump<=max(.01,float(np.quantile(np.abs(np.diff(decoded)),.999)))
    digest=hashlib.sha256(path.read_bytes()).hexdigest();screen.append(path.name+'\t'+digest)
    published.append({'path':path.relative_to(root).as_posix(),'sha256':digest,'seconds':len(loop)/rate,'channels':1,'sample_rate':rate,'seam_jump':jump,'model':record['model'],'seed':record['actual_seed'],'prompt':record['prompt'],'source_sha256':record['file_sha256'],'automated_fire_score':audit['scores'].get('Fire',0),'automated_crackle_score':audit['scores'].get('Crackle',0)})
(output/'screened.tsv').write_text('# Hash-bound generated takes; owner listening pending\n'+'\n'.join(screen)+'\n')
provenance=root/'assets/camp-audio';provenance.mkdir(parents=True,exist_ok=True)
(provenance/'provenance.json').write_text(json.dumps({'execution':'Local Stable Audio 3, existing CUDA models; no paid calls','license':'https://stability.ai/license','processing':'Stereo-to-mono fold, endpoint trim, one-second equal-power loop overlap and bounded normalization. No generated procedural audio.','listening':'Not auditioned by the agent. Automated classification is diagnostic only; owner listening and rejection remain pending.','clips':published},indent=2))
print(json.dumps({'files':len(published),'bytes':sum((root/r['path']).stat().st_size for r in published),'clips':[{k:r[k] for k in ['path','seconds','automated_fire_score','automated_crackle_score']} for r in published]}))
