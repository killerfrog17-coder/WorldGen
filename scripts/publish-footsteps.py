"""Extract quiet isolated contacts from local AI Foley, never synthesize footsteps.

Source content screening is diagnostic, not human listening approval. Retain
prompt/seed/model/crop/gain provenance and hash-bind runtime admission.
"""
import hashlib
import json
from pathlib import Path
import numpy as np
import soundfile as sf

root=Path(__file__).resolve().parents[1]
output=root/'sounds/footsteps';output.mkdir(exist_ok=True)
choices={
    'soil':('short','footstep-soil-clean'), 'sand':('short','footstep-sand-clean'),
    'gravel':('source','footstep-gravel-01'), 'stone':('source','footstep-stone-01'),
    'mud':('source','footstep-mud-02'), 'organic':('source','footstep-organic-02'),
    'snow':('short','footstep-snow-clean')}
selected={};hashes=[]
for material,(batch,source_id) in choices.items():
    jobs=root/('artifacts/footstep-'+batch+'-jobs')
    source=root/('artifacts/footstep-'+batch+'-candidates')/(source_id+'.wav')
    record=json.loads((jobs/'results.json').read_text())[source_id]
    audit=json.loads((jobs/'content-audit.json').read_text())['files'][source.name]
    assert not audit['screen_rejections'], (source_id,audit['screen_rejections'])
    assert hashlib.sha256(source.read_bytes()).hexdigest()==record['file_sha256']
    samples,rate=sf.read(source,dtype='float32',always_2d=True);assert rate==44100
    mono=samples.mean(axis=1);hop=441
    blocks=mono[:len(mono)//hop*hop].reshape(-1,hop)
    energy=np.sqrt(np.mean(blocks.astype('float64')**2,axis=1))
    floor=float(np.percentile(energy,20));threshold=max(float(energy.max())*.16,floor*2.0,.003)
    peaks=np.nonzero((energy[1:-1]>energy[:-2])&(energy[1:-1]>=energy[2:])&(energy[1:-1]>threshold))[0]+1
    isolated=[]
    for peak in sorted(peaks,key=lambda i:-energy[i]):
        if peak<10 or peak>len(energy)-12 or any(abs(peak-old)<35 for old in isolated):continue
        isolated.append(int(peak))
    assert len(isolated)>=4, (material,'not enough distinct contact transients')
    # Use ordinary contacts, not only the loudest stomps. Keep four real variations.
    isolated=sorted(isolated,key=lambda i:energy[i])
    start=max(0,(len(isolated)-4)//2);contacts=sorted(isolated[start:start+4])
    for index,peak in enumerate(contacts,1):
        onset=peak
        while onset>max(0,peak-12) and energy[onset-1]>max(floor*1.5,energy[peak]*.12):onset-=1
        end=peak+8
        while end<min(len(energy)-1,peak+48) and energy[end]>max(floor*1.5,energy[peak]*.08):end+=1
        lo=max(0,onset*hop-882);hi=min(len(mono),max(lo+int(.18*rate),(end+3)*hop))
        clip=mono[lo:hi].copy();clip-=float(clip.mean())
        assert .12<=len(clip)/rate<=1.2
        rms=float(np.sqrt(np.mean(clip.astype('float64')**2)));peak_value=float(np.max(np.abs(clip)))
        gain=min(1.,.48/max(peak_value,1e-6),.08/max(rms,1e-6));clip*=gain
        clip[:220]*=np.linspace(0,1,220,dtype='float32');clip[-1764:]*=np.linspace(1,0,1764,dtype='float32')
        path=output/(material+'-'+str(index).zfill(2)+'.wav')
        if path.exists():
            preceding=root/'artifacts/footsteps/selected.json'
            assert preceding.exists() and hashlib.sha256(path.read_bytes()).hexdigest()==json.loads(preceding.read_text())[path.name]['sha256'], 'Refusing changed/unmanaged '+str(path)
        sf.write(path,clip,rate,subtype='PCM_16')
        digest=hashlib.sha256(path.read_bytes()).hexdigest();hashes.append(path.name+'\t'+digest)
        selected[path.name]=dict(source_id=source_id,source_sha256=record['file_sha256'],seed=record['actual_seed'],model=record['model'],prompt=record['prompt'],source_start_frame=lo,source_end_frame=hi,gain=gain,seconds=len(clip)/rate,sha256=digest)
target=root/'artifacts/footsteps';target.mkdir(exist_ok=True)
(target/'selected.json').write_text(json.dumps(selected,indent=2))
(output/'screened.tsv').write_text('# AI source-screened, trimmed contacts; not human listening certification\n'+'\n'.join(hashes)+'\n')
print('Published28 distinct AI footfalls across7 materials; all source prompts/crops/hashes retained.')
