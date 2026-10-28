"""Crop individual impacts from local Stable Audio 3 masters; no synthesis."""
from pathlib import Path
import hashlib,json,uuid
import numpy as np
import soundfile as sf
from scipy.signal import find_peaks
ROOT=Path(__file__).resolve().parents[1]
TASK=ROOT/'artifacts/stone-crafting'
OUT=ROOT/'unity/WorldGen.Unity/Assets/WorldGen/Resources/Crafting/Stone'
OUT.mkdir(parents=True,exist_ok=True)
records=json.loads((TASK/'audio-jobs/results.json').read_text())
published=[];plots=[]
for source_id in ['knapping-stone-01','knapping-stone-02']:
    path=TASK/'audio-candidates'/f'{source_id}.wav'
    record=records[source_id];assert hashlib.sha256(path.read_bytes()).hexdigest()==record['file_sha256']
    source,rate=sf.read(path,dtype='float32',always_2d=True);assert rate==44100
    mono=source.mean(axis=1);size=441
    envelope=np.sqrt(np.mean(mono[:len(mono)//size*size].reshape(-1,size)**2,axis=1))
    peaks,_=find_peaks(envelope,height=max(.004,envelope.max()*.28),prominence=envelope.max()*.15,distance=45)
    candidates=[]
    for peak in peaks:
        threshold=max(.0015,float(envelope[peak])*.08)
        start=int(peak)
        while start>0 and envelope[start-1]>threshold and peak-start<20:start-=1
        end=int(peak)+8
        while end<len(envelope)-1 and envelope[end]>threshold and end-peak<60:end+=1
        a=max(0,(start-1)*size);b=min(len(mono),(end+2)*size)
        clip=mono[a:b].copy()
        if not .08<=len(clip)/rate<=.85:continue
        candidates.append((float(np.max(np.abs(clip))),a,b,clip))
    assert len(candidates)>=3,(source_id,len(candidates))
    selected=sorted(candidates,reverse=True,key=lambda c:c[0])[:3]
    for _,a,b,clip in sorted(selected,key=lambda c:c[1]):
        clip*=min(.65/max(float(np.max(np.abs(clip))),1e-9),3)
        fade=min(220,len(clip)//6);tail=min(882,len(clip)//5)
        clip[:fade]*=np.linspace(0,1,fade);clip[-tail:]*=np.linspace(1,0,tail)
        destination=OUT/f'knap-{len(published)+1:02}.wav'
        sf.write(destination,clip,rate,subtype='PCM_16')
        decoded,read_rate=sf.read(destination);assert read_rate==rate and np.isfinite(decoded).all() and np.max(np.abs(decoded))<.651
        meta=destination.with_suffix('.wav.meta')
        if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\nAudioImporter:\n  externalObjects: {}\n  serializedVersion: 7\n  defaultSettings:\n    serializedVersion: 2\n    loadType: 0\n    sampleRateSetting: 0\n    sampleRateOverride: 44100\n    compressionFormat: 0\n    quality: 1\n    conversionMode: 0\n  platformSettingOverrides: {}\n  forceToMono: 0\n  normalize: 0\n  preloadAudioData: 1\n  loadInBackground: 0\n  ambisonic: 0\n  3D: 1\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n',encoding='utf-8')
        published.append({'path':str(destination.relative_to(ROOT)),'sha256':hashlib.sha256(destination.read_bytes()).hexdigest(),'source':source_id,'source_sha256':record['file_sha256'],'crop_seconds':[a/rate,b/rate],'seconds':len(clip)/rate,'peak':float(np.max(np.abs(decoded))),'prompt':record['prompt'],'seed':record['actual_seed'],'model':record['model']})
    plots.append((source_id,envelope,[(a/size,b/size) for _,a,b,_ in selected]))
manifest={'model':'stabilityai/stable-audio-3-small-sfx','source':'local generation, existing weights; no paid service','processing':'mono fold, isolated impact crops, bounded gain, short endpoint fades; no procedural audio','listening_approval':'Owner audition pending; numerical screening is not listening approval.','license':'https://stability.ai/license','prompt_guide':'https://stability.ai/guides/stable-audio-3-prompt-guide','clips':published}
dest=ROOT/'assets/crafting-audio';dest.mkdir(parents=True,exist_ok=True)
(dest/'stone-knapping-provenance.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
from PIL import Image,ImageDraw
image=Image.new('RGB',(1200,440),'#f4f6f4');draw=ImageDraw.Draw(image)
for index,(name,envelope,crops) in enumerate(plots):
    y=30+index*210;draw.text((25,y),name,fill='#203632');high=max(float(envelope.max()),1e-6)
    for a,b in crops:draw.rectangle((40+a/len(envelope)*1120,y+24,40+b/len(envelope)*1120,y+184),fill='#c7e4d0')
    draw.line([(40+i/len(envelope)*1120,y+180-float(e)/high*145) for i,e in enumerate(envelope)],fill='#285f45',width=2)
image.save(TASK/'audio-crops.png')
print(json.dumps({'published':len(published),'seconds':[round(p['seconds'],3) for p in published],'bytes':sum((ROOT/p['path']).stat().st_size for p in published)}))
