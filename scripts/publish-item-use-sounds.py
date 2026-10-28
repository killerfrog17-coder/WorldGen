"""Publish short transients from locally generated SA3 masters; no synthesis."""
from pathlib import Path
import hashlib,json,uuid
import numpy as np
import soundfile as sf
from scipy.signal import find_peaks

root=Path(__file__).resolve().parents[1];task=root/'artifacts/overnight-items-20261028'
records=json.loads((task/'audio-jobs/results.json').read_text(encoding='utf-8'))
selection=task/'audio-selection.json'
if selection.exists():
    for key,choice in json.loads(selection.read_text(encoding='utf-8')).items():
        selected=json.loads((task/choice['jobs']/'results.json').read_text(encoding='utf-8'))[choice.get('id',key)]
        records[key]=dict(selected,_source=str(task/choice['candidates']/(choice.get('id',key)+'.wav')))
output=root/'unity/WorldGen.Unity/Assets/WorldGen/Resources/Items/Audio';output.mkdir(parents=True,exist_ok=True)
template=(root/'unity/WorldGen.Unity/Assets/WorldGen/Resources/Crafting/Stone/knap-01.wav.meta').read_text(encoding='utf-8')
durations={'body-hit':.55,'bow-release':.8,'weapon-swish':.6,'wood-chop':.7,'soil-dig':1.1,'fibre-handle':.9}
published=[]
for key,record in records.items():
    source=Path(record.get('_source',task/'audio-candidates'/(key+'.wav')));assert hashlib.sha256(source.read_bytes()).hexdigest()==record['file_sha256']
    samples,rate=sf.read(source,always_2d=True);mono=samples.mean(axis=1);hop=round(rate*.005)
    envelope=np.sqrt(np.mean(mono[:len(mono)//hop*hop].reshape(-1,hop)**2,axis=1))
    peaks,_=find_peaks(envelope,distance=round(.9/.005),prominence=max(.001,float(envelope.max())*.12))
    selected=sorted(sorted(peaks,key=lambda i:envelope[i],reverse=True)[:3]);assert len(selected)>=2,(key,'insufficient separated transients')
    for i,peak in enumerate(selected):
        onset=int(peak)
        while onset>0 and peak-onset<100 and envelope[onset]>envelope[peak]*.09:onset-=1
        start=max(0,onset*hop-round(rate*.012));end=min(len(mono),start+round(rate*durations[key]));clip=mono[start:end].copy()
        clip*=min(1.8,.62/max(1e-8,float(np.abs(clip).max())))
        fade=min(round(rate*.004),len(clip)//4);tail=min(round(rate*.045),len(clip)//4);clip[:fade]*=np.linspace(0,1,fade);clip[-tail:]*=np.linspace(1,0,tail)
        path=output/(key+f'-{i+1:02}.wav');sf.write(path,clip,rate,subtype='PCM_16')
        meta=path.with_suffix('.wav.meta')
        if not meta.exists():
            lines=template.splitlines();lines[1]='guid: '+uuid.uuid4().hex;meta.write_text('\n'.join(line.rstrip() for line in lines)+'\n',encoding='utf-8')
        published.append({'path':path.relative_to(root).as_posix(),'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'source_sha256':record['file_sha256'],'model':record['model'],'prompt':record['prompt'],'seed':record['actual_seed'],'crop_seconds':[start/rate,end/rate]})
provenance=root/'assets/item-audio';provenance.mkdir(parents=True,exist_ok=True)
(provenance/'provenance.json').write_text(json.dumps({'execution':'local CUDA ComfyUI, existing model weights; no service purchase','license':'https://stability.ai/license','processing':'mono fold, onset-aligned transient crops, bounded gain and endpoint fades only','listening':'Owner audition pending; automated classifier diagnostics are not listening approval','clips':published},indent=2),encoding='utf-8')
print(json.dumps({'clips':len(published),'bytes':sum((root/p['path']).stat().st_size for p in published)}))
