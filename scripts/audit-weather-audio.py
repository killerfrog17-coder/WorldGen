"""Offline semantic QA, never part of runtime. Classifier scores are diagnostics,
not proof of source purity or a replacement for human listening.
"""
import argparse
import json
import math
import hashlib
from pathlib import Path
import wave
import urllib.request

import numpy as np
from scipy.signal import resample_poly
import torch
from transformers import ASTFeatureExtractor, ASTForAudioClassification


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('folder', type=Path)
    ap.add_argument('--output', type=Path, required=True)
    ap.add_argument('--cache', default='D:/AI/AudioAudit')
    ap.add_argument('--screen', action='store_true', help='Write hash-bound screened.tsv for acceptable isolated sources only')
    args = ap.parse_args()
    torch.set_num_threads(4)
    name = 'MIT/ast-finetuned-audioset-10-10-0.4593'
    features = ASTFeatureExtractor.from_pretrained(name, cache_dir=args.cache)
    model = ASTForAudioClassification.from_pretrained(name, cache_dir=args.cache).eval()
    labels = model.config.id2label
    ontology_path=Path(args.cache)/'audioset-ontology.json'
    if not ontology_path.exists():
        urllib.request.urlretrieve('https://raw.githubusercontent.com/audioset/ontology/master/ontology.json', ontology_path)
    ontology=json.loads(ontology_path.read_text(encoding='utf-8'))
    by_id={x['id']:x for x in ontology}
    def descendants(root_name):
        stack=[x['id'] for x in ontology if x['name']==root_name]; names=set()
        while stack:
            node=by_id[stack.pop()]
            if node['name'] in names:continue
            names.add(node['name']);stack.extend(node.get('child_ids',[]))
        return names
    animal_labels=descendants('Animal')
    vehicle_labels=descendants('Vehicle')
    report = {}
    screened=[]
    for path in sorted(args.folder.glob('*.wav')):
        with wave.open(str(path), 'rb') as stream:
            assert stream.getsampwidth() == 2
            rate, channels = stream.getframerate(), stream.getnchannels()
            audio = np.frombuffer(stream.readframes(stream.getnframes()), dtype='<i2').astype('float32').reshape(-1, channels).mean(axis=1)/32768
        audio = resample_poly(audio, 16000//math.gcd(rate,16000), rate//math.gcd(rate,16000))
        # Overlapping five-second windows expose brief calls hidden by a long wind bed.
        windows = []
        scores = []
        for start in range(0, max(1,len(audio)-16000), 40000):
            part = audio[start:start+80000]
            if len(part) < 16000:continue
            inputs = features(part, sampling_rate=16000, return_tensors='pt')
            with torch.inference_mode():
                values = model(**inputs).logits.sigmoid()[0].cpu().numpy()
            scores.append(values)
            top = np.argsort(values)[-7:][::-1]
            windows.append(dict(seconds=start/16000, top={labels[int(i)]:round(float(values[i]),4) for i in top}))
        maximum = np.max(scores, axis=0)
        top = np.argsort(maximum)[-12:][::-1]
        named={labels[i]:float(maximum[i]) for i in range(len(maximum))}
        def maximum_of(names):return max((named.get(n,0) for n in names), default=0)
        reasons=[]
        wildlife=maximum_of(animal_labels)
        if wildlife>.10:reasons.append('wildlife')
        if named.get('Music',0)>.15:reasons.append('music')
        if named.get('Speech',0)>.25:reasons.append('speech')
        if maximum_of(['Rustling leaves','Rustle'])>.12:reasons.append('rustling')
        if maximum_of(vehicle_labels|{'Siren','Engine','Mechanical fan'})>.18:reasons.append('mechanical source')
        if maximum_of(['Waves, surf','Ocean','Stream','Waterfall'])>.15:reasons.append('unrelated water')
        wind=path.name.startswith('wind') or 'wind' in path.name
        rain=path.name.startswith('rain')
        thunder=path.name.startswith('thunder')
        if wind:
            if named.get('Sonar',0)>.20:reasons.append('electronic tone')
            if maximum_of(['Wind','Wind noise (microphone)','Whoosh, swoosh, swish'])<.15:reasons.append('weak wind identity')
            if maximum_of(['Rain','Rain on surface','Raindrop'])>.12:reasons.append('baked rain')
            if named.get('Thunder',0)>.10:reasons.append('baked thunder')
        if rain:
            if maximum_of(['Rain','Rain on surface','Raindrop'])<.4:reasons.append('weak rain identity')
            if named.get('Thunder',0)>.10:reasons.append('baked thunder')
        if thunder:
            if named.get('Thunder',0)<.3:reasons.append('weak thunder identity')
            if maximum_of(['Rain','Rain on surface','Raindrop'])>.15:reasons.append('baked rain')
        record = dict(top={labels[int(i)]:round(float(maximum[i]),4) for i in top},
                      scores={labels[i]:round(float(maximum[i]),5) for i in range(len(maximum))}, windows=windows)
        record['screen_rejections']=reasons
        if not reasons and (wind or rain or thunder):screened.append(path.name+'\t'+hashlib.sha256(path.read_bytes()).hexdigest())
        report[path.name] = record
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(dict(model=name, files=report), indent=2))
        print(path.name+': '+json.dumps(record['top']), flush=True)
        if args.screen:print('SCREEN '+('REJECT '+', '.join(reasons) if reasons else 'ACCEPT'), flush=True)
    if args.screen:
        (args.folder/'screened.tsv').write_text('# AST content screen, not human listening approval\n'+'\n'.join(screened)+'\n')


if __name__ == '__main__':
    main()
