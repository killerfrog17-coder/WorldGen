"""Crop three short hand-scraping strokes from the local SA3 master."""
from pathlib import Path
import json,hashlib,uuid
import numpy as np
import soundfile as sf
from scipy.signal import find_peaks
root=Path(__file__).resolve().parents[1];task=root/'artifacts/woodworking'
record=json.loads((task/'audio-jobs/results.json').read_text())['wood-scraping']
source=task/'audio-candidates/wood-scraping.wav'
assert hashlib.sha256(source.read_bytes()).hexdigest()==record['file_sha256']
audio,rate=sf.read(source,always_2d=True);audio=audio.mean(axis=1)
envelope=np.sqrt(np.mean(audio[:len(audio)//441*441].reshape(-1,441)**2,axis=1))
peaks,_=find_peaks(envelope,height=envelope.max()*.25,distance=90)
assert len(peaks)>=3
chosen=sorted(sorted(peaks,key=lambda i:envelope[i],reverse=True)[:3])
out=root/'unity/WorldGen.Unity/Assets/WorldGen/Resources/Crafting/Wood';out.mkdir(parents=True,exist_ok=True)
template=(root/'unity/WorldGen.Unity/Assets/WorldGen/Resources/Crafting/Stone/knap-01.wav.meta').read_text()
published=[]
for i,peak in enumerate(chosen):
    a=max(0,int(peak*441-rate*.16));b=min(len(audio),int(peak*441+rate*.55));clip=audio[a:b].copy()
    clip*=min(2,.55/max(1e-8,np.abs(clip).max()));clip[:353]*=np.linspace(0,1,353);clip[-1544:]*=np.linspace(1,0,1544)
    p=out/f'wood-{i+1:02}.wav';sf.write(p,clip,rate,subtype='PCM_16')
    meta=p.with_suffix('.wav.meta')
    if not meta.exists():
        lines=template.splitlines();lines[1]='guid: '+uuid.uuid4().hex;meta.write_text('\n'.join(line.rstrip() for line in lines)+'\n')
    published.append({'path':p.relative_to(root).as_posix(),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'crop_seconds':[a/rate,b/rate]})
manifest={'model':record['model'],'prompt':record['prompt'],'seed':record['actual_seed'],'source_sha256':record['file_sha256'],'processing':'mono fold, three short stroke crops, endpoint fades, bounded gain','license':'https://stability.ai/license','listening_approval':'Owner audition pending','clips':published}
(root/'assets/crafting-audio/woodworking-provenance.json').write_text(json.dumps(manifest,indent=2))
print(json.dumps({'clips':3,'bytes':sum(p.stat().st_size for p in out.glob('*.wav'))}))
