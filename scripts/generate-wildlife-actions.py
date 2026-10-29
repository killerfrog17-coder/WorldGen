"""Generate isolated animal actions with the existing local SA3 Small-SFX server."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import subprocess
import sys

import numpy as np
import soundfile as sf


# Broad voice families, not claims to reproduce every real species exactly.
# A cue is a context, and a form is a stable voice within that context.
CALLS = {
 'deer': ('adult deer', {'contact':'one soft nasal contact grunt', 'alert':'one sharp breathy alarm snort', 'pain':'one brief strained startled cry', 'rut':'one resonant rough rutting bellow'}),
 'moose': ('adult moose', {'contact':'one low nasal grunting contact call', 'alert':'one forceful short nasal alarm snort', 'pain':'one short rough distressed cry', 'rut':'one drawn out low nasal mating moan'}),
 'wolf': ('adult wolf', {'contact':'one brief soft whine', 'alert':'one sharp rough warning bark', 'threat':'one short low rumbling growl', 'pain':'one short startled yelp', 'howl':'one long wavering natural howl that rises and gently tails away'}),
 'fox': ('adult red fox', {'contact':'one short thin nasal yapping call', 'alert':'one sharp dry fox bark', 'pain':'one brief high startled yelp'}),
 'bear': ('adult brown bear', {'contact':'one low breathy grunt', 'threat':'one forceful breathy huff followed by a short deep growl', 'pain':'one brief rough strained bellow'}),
 'boar': ('adult wild boar', {'contact':'one short low throaty grunt', 'alert':'one loud sharp grunting alarm bark', 'pain':'one brief high rough squeal'}),
 'bovid': ('adult wild bovine', {'contact':'one low resonant contact moo', 'threat':'one short deep forceful bellow', 'pain':'one brief strained low bellow'}),
 'goat': ('adult wild goat', {'contact':'one short nasal bleat', 'alert':'one forceful nasal alarm bleat', 'pain':'one brief strained bleat'}),
 'sheep': ('adult wild sheep', {'contact':'one soft rounded baa bleat', 'alert':'one short loud alarm bleat', 'pain':'one brief rough strained bleat'}),
 'horse': ('adult wild horse', {'contact':'one soft fluttering nicker', 'alert':'one sharp alarm snort', 'pain':'one short strained whinny'}),
 'rabbit': ('rabbit', {'pain':'one brief high-pitched distressed squeal'}),
 'owl': ('woodland owl', {'ambient':'one low rounded two-part territorial hoot', 'alert':'one short sharp rough alarm call'}),
 'duck': ('wild duck', {'ambient':'one short natural quacking contact phrase', 'alert':'one brief fast sharp alarm quack'}),
 'goose': ('wild goose', {'ambient':'one resonant nasal contact honk', 'alert':'one loud harsh alarm honk'}),
 'pheasant': ('male pheasant', {'ambient':'one short rough two-part crowing call', 'alert':'one brief sharp cackling alarm call'}),
 'grouse': ('grouse', {'ambient':'one short low clucking contact call', 'alert':'one brief sharp clucking alarm phrase'}),
 'quail': ('wild quail', {'ambient':'one brief clear three-note whistled call', 'alert':'one short sharp chirruping alarm call'}),
 'gull': ('gull', {'ambient':'one short nasal mewing contact call', 'alert':'one brief rough barking alarm phrase'}),
 'raptor': ('hawk', {'ambient':'one short clear descending whistled call', 'alert':'one short sharp high alarm scream'}),
 'lion': ('adult lion', {'contact':'one low rounded grunting contact call', 'threat':'one short deep rumbling growl', 'pain':'one brief rough strained yowl'}),
 'big-cat': ('adult leopard', {'contact':'one short low rasping contact grunt', 'threat':'one short rough guttural growl', 'pain':'one brief sharp strained yowl'}),
 'howler': ('one howler monkey', {'ambient':'one resonant rough low howling phrase'}),
 'baboon': ('one adult baboon', {'contact':'one short low grunting contact call', 'alert':'one loud abrupt rough barking alarm call'}),
 'crane': ('one wild crane', {'ambient':'one clear resonant bugling contact call'}),
 'bee-wings': ('one bee flying close to the microphone', {'flight':'a gentle irregular insect wing buzz that subtly swells and recedes'}),
 'fly-wings': ('one housefly flying close to the microphone', {'flight':'a thin finely vibrating wing buzz that briefly approaches and recedes'}),
}


def main():
    ap=argparse.ArgumentParser()
    ap.add_argument('--root',type=Path,required=True)
    ap.add_argument('--url',default='http://127.0.0.1:8188')
    ap.add_argument('--expansion',action='store_true',help='Additional cricket/katydid takes and small-bird alarm calls')
    args=ap.parse_args(); root=args.root.resolve();root.mkdir(parents=True,exist_ok=True)
    rows=[]
    for take in range(1,5):
        for form in (1,2):
            for bank,(animal,cues) in CALLS.items():
                for cue,action in cues.items():
                    voice=' A slightly lower adult voice.' if form==1 and bank not in ('bee-wings','fly-wings') else ''
                    brief=f'{animal.capitalize()} gives {action}, with natural onset and decay into silence. Isolated close microphone, dry direct sound, clean silent background, one consistent animal voice.'+voice
                    rows.append(dict(id=f'{bank}-{cue}-f{form:02d}-t{take:02d}',group=bank,seconds=10 if cue=='howl' else 6,brief=brief,form=form,take=take,cue=cue))
    if args.expansion:
        import runpy
        prior=runpy.run_path(str(Path(__file__).with_name('generate-fauna-batch.py')))
        rows=[]
        for take in range(1,13):
            for group in ('cricket','katydid'):
                if group=='katydid' and take>8:continue
                for form,brief in enumerate(prior['FORMS'][group],1):
                    rows.append(dict(id=f'{group}-expansion-f{form:02d}-t{take:02d}',group=group,seconds=20,brief=brief+prior['CHARACTER'],form=form,take=take,cue='ambient'))
        for take in range(1,7):
            for form,brief in enumerate(('One small woodland songbird gives a short sharp series of high alarm chips, then falls quiet.','One small woodland songbird gives one thin high warning whistle followed by two crisp alarm chirps, then silence.'),1):
                rows.append(dict(id=f'bird-alert-f{form:02d}-t{take:02d}',group='bird',seconds=6,brief=brief+' Close dry isolated microphone, clean silent background, a single consistent bird voice.',form=form,take=take,cue='alert'))
    catalog=root/'catalog.csv'
    with catalog.open('w',newline='',encoding='utf-8') as stream:
        writer=csv.DictWriter(stream,fieldnames=list(rows[0]));writer.writeheader();writer.writerows(rows)
    index_path=root/'audition-index.json';index=json.loads(index_path.read_text()) if index_path.exists() else {}
    candidates=root/'candidates';jobs=root/'jobs';jobs.mkdir(exist_ok=True)
    command=[sys.executable,str(Path(__file__).with_name('generate-weather-sounds.py')),'--catalog',str(catalog),'--output',str(candidates),'--evidence',str(jobs),'--checkpoint','stable_audio_3_small_sfx.safetensors','--seed-base','29201031' if args.expansion else '29102831','--limit',str(len(rows)),'--allow-short-effects','--url',args.url]
    print('Planned isolated action takes:',len(rows),flush=True)
    child=subprocess.Popen(command,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True,encoding='utf-8',bufsize=1)
    by_id={r['id']:r for r in rows}
    def prepare(key):
        if key in index:return # Owner-deleted takes remain rejected on resume.
        row=by_id[key];info=json.loads((jobs/'results.json').read_text())[key]
        path=candidates/(key+'.wav');assert hashlib.sha256(path.read_bytes()).hexdigest()==info['file_sha256']
        audio,rate=sf.read(path,dtype='float32',always_2d=True);mono=audio.mean(axis=1);mono-=mono.mean()
        if row['group'] in ('cricket','katydid'):
            from scipy.signal import butter,sosfilt
            mono=sosfilt(butter(2,1200 if row['group']=='cricket' else 1500,btype='highpass',fs=rate,output='sos'),mono).astype('float32')
        rms=float(np.sqrt(np.mean(mono.astype('float64')**2)));peak=float(np.max(np.abs(mono)))
        mono*=min(10**(-25/20)/max(rms,1e-8),.65/max(peak,1e-8),6)
        fade=min(int(rate*.03),len(mono)//8);ramp=np.sin(np.linspace(0,np.pi/2,fade))**2
        mono[:fade]*=ramp;mono[-fade:]*=ramp[::-1];mono[0]=mono[-1]=0
        destination=root/'audition'/row['group']/(key+'.wav');destination.parent.mkdir(parents=True,exist_ok=True)
        sf.write(destination,mono,rate,subtype='PCM_16')
        index[key]=dict(group=row['group'],cue=row['cue'],form=row['form'],take=row['take'],audition=str(destination.relative_to(root)),sha256=hashlib.sha256(destination.read_bytes()).hexdigest(),prompt=info['prompt'],seed=info['actual_seed'],model=info['model'],seconds=len(mono)/rate)
        temp=index_path.with_suffix('.pending');temp.write_text(json.dumps(index,indent=2),encoding='utf-8');temp.replace(index_path)
    if (jobs/'results.json').exists():
        for key in json.loads((jobs/'results.json').read_text()):prepare(key)
    with (root/'generation.log').open('a',encoding='utf-8') as log:
        for line in child.stdout:
            log.write(line);log.flush()
            try:event=json.loads(line)
            except json.JSONDecodeError:continue
            if event.get('event')=='complete':prepare(event['id']);print(json.dumps({'prepared':len(index),'planned':len(rows),'last':event['id']}),flush=True)
    if child.wait():raise RuntimeError('Generation failed; inspect generation.log and jobs/current-job.json before resume')


if __name__=='__main__':main()
