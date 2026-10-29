"""Trim empty lead/tails in discrete calls; preserve masters and hash-bound screening."""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
import soundfile as sf


def main():
    ap=argparse.ArgumentParser();ap.add_argument('--source',type=Path,required=True);ap.add_argument('--screen',type=Path,required=True);ap.add_argument('--output',type=Path,required=True)
    args=ap.parse_args();args.output.mkdir(parents=True,exist_ok=True)
    original=json.loads((args.source/'audition-index.json').read_text());screen=json.loads(args.screen.read_text());index={};derived={'model':screen['model'],'processing':'Inherited exact parent content screen; silence-tail trim and10ms endpoint fades add no sound sources.','files':{}}
    for key,entry in original.items():
        path=args.source/entry['audition'];digest=hashlib.sha256(path.read_bytes()).hexdigest();assert digest==entry['sha256']==screen['files'][key]['sha256']
        audio,rate=sf.read(path,dtype='float32');start=0;end=len(audio)
        if entry.get('cue')!='flight':
            hop=max(1,rate//100);n=len(audio)//hop
            rms=np.sqrt(np.mean(audio[:n*hop].reshape(n,hop).astype('float64')**2,axis=1))
            floor=min(float(np.percentile(rms,20))*2.5,float(rms.max())*.15)
            active=np.flatnonzero(rms>max(.001,float(rms.max())*.035,floor))
            if len(active):start=max(0,int(active[0])*hop-int(rate*.04));end=min(len(audio),(int(active[-1])+1)*hop+int(rate*.16))
        audio=audio[start:end].copy();fade=min(int(rate*.01),len(audio)//8);ramp=np.sin(np.linspace(0,np.pi/2,fade))**2
        audio[:fade]*=ramp;audio[-fade:]*=ramp[::-1];audio[0]=audio[-1]=0
        target=args.output/'audition'/entry['group']/(key+'.wav');target.parent.mkdir(parents=True,exist_ok=True);sf.write(target,audio,rate,subtype='PCM_16')
        out=dict(entry);out.update(audition=str(target.relative_to(args.output)),sha256=hashlib.sha256(target.read_bytes()).hexdigest(),seconds=len(audio)/rate,source_audio_sha256=digest,trim_start_seconds=start/rate,trim_end_seconds=(end+0)/rate,processing='Leading/trailing silence trim,10ms edge fades; source performance unchanged')
        index[key]=out
        screened=dict(screen['files'][key]);screened.update(path=str(target.resolve()),sha256=out['sha256'],seconds=out['seconds'],parent_screen_sha256=digest,screen_inherited_from=str(args.screen.resolve()))
        derived['files'][key]=screened
    (args.output/'audition-index.json').write_text(json.dumps(index,indent=2),encoding='utf-8')
    (args.output/'screen.json').write_text(json.dumps(derived,indent=2),encoding='utf-8')
    print(json.dumps({'prepared':len(index),'accepted':sum(r['accepted'] for r in derived['files'].values()),'seconds_before':sum(e['seconds'] for e in original.values()),'seconds_after':sum(e['seconds'] for e in index.values())}))


if __name__=='__main__':main()
