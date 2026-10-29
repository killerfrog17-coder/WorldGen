"""Publish local SA3 nonverbal body foley; never synthesize replacement waveforms."""
import argparse
import hashlib
import json
from pathlib import Path
import numpy as np
import soundfile as sf

root=Path(__file__).resolve().parents[1]
ap=argparse.ArgumentParser()
ap.add_argument('--task',type=Path,default=root/'artifacts/survival-camp-20261029')
ap.add_argument('--audit',type=Path)
args=ap.parse_args()
records=json.loads((args.task/'audio-jobs/results.json').read_text())
audit=json.loads(args.audit.read_text())['files'] if args.audit else {}
destination=root/'sounds/player-survival';destination.mkdir(parents=True,exist_ok=True)
provenance=[];screen=[]
for key,record in sorted(records.items()):
    scores=audit.get(key+'.wav',{}).get('scores',{})
    rejected=[name for name,threshold in [('Music',.2),('Singing',.3),('Conversation',.5)] if scores.get(name,0)>threshold]
    if rejected:
        print('REJECT',key,rejected);continue
    source=Path(record['path']);data,rate=sf.read(source,dtype='float32',always_2d=True);audio=data.mean(axis=1)
    assert rate==44100 and np.isfinite(audio).all()
    # Preserve generated human foley, trimming only quiet padding and applying
    # short fades. Mono places the sound at the player's own perspective.
    threshold=max(.001,float(np.max(np.abs(audio)))*.025);active=np.flatnonzero(np.abs(audio)>threshold)
    assert len(active),key
    start=max(0,int(active[0])-.03*rate);end=min(len(audio),int(active[-1])+.12*rate)
    audio=audio[int(start):int(end)].copy();fade=min(441,len(audio)//4)
    audio[:fade]*=np.linspace(0,1,fade);audio[-fade:]*=np.linspace(1,0,fade)
    rms=float(np.sqrt(np.mean(audio.astype('float64')**2)));gain=min(.065/max(.0001,rms),.75/max(.0001,float(np.max(np.abs(audio)))))
    audio*=gain;path=destination/(key+'.wav');sf.write(path,audio,rate,subtype='PCM_16')
    digest=hashlib.sha256(path.read_bytes()).hexdigest();screen.append(path.name+'\t'+digest)
    provenance.append(dict(record,path=path.relative_to(root).as_posix(),sha256=digest,channels=1,sample_rate=rate,seconds=len(audio)/rate,trim_start_seconds=start/rate,trim_end_seconds=end/rate,automated_audit=scores,human_listening='pending owner review'))
(destination/'screened.tsv').write_text('\n'.join(screen)+'\n')
folder=root/'assets/player-audio';folder.mkdir(parents=True,exist_ok=True)
(folder/'provenance.json').write_text(json.dumps(dict(model='stabilityai/stable-audio-3-small-sfx',prompt_guide='https://stability.ai/guides/stable-audio-3-prompt-guide',generation='local, free; nonverbal SFX',clips=provenance),indent=2))
print('Published',len(provenance),'mono player sounds')
