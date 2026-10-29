"""Offline source-content screen for generated wildlife takes; not listening approval."""
import argparse
import hashlib
import json
import math
from pathlib import Path
import time

import numpy as np
from scipy.signal import resample_poly
import soundfile as sf
import torch
from transformers import ASTFeatureExtractor, ASTForAudioClassification


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--batch', type=Path, action='append', required=True)
    ap.add_argument('--output', type=Path, required=True)
    ap.add_argument('--cache', default='D:/AI/AudioAudit')
    ap.add_argument('--device', default='cuda')
    args = ap.parse_args()
    args.output.parent.mkdir(parents=True, exist_ok=True)
    torch.set_num_threads(2)
    name = 'MIT/ast-finetuned-audioset-10-10-0.4593'
    features = ASTFeatureExtractor.from_pretrained(name, cache_dir=args.cache, local_files_only=True)
    model = ASTForAudioClassification.from_pretrained(name, cache_dir=args.cache, local_files_only=True).eval().to(args.device)
    labels = model.config.id2label
    result = json.loads(args.output.read_text(encoding='utf-8')) if args.output.exists() else {'model': name, 'files': {}}
    started = time.time()
    paths = []
    for batch in args.batch:
        index = json.loads((batch/'audition-index.json').read_text(encoding='utf-8'))
        for key, entry in index.items():
            path = batch/entry['audition']
            if path.is_file():
                paths.append((key, path, entry))
    for key, path, entry in paths:
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        if digest != entry['sha256']:
            raise ValueError('Changed audition requires explicit re-admission: '+str(path))
        if result['files'].get(key, {}).get('sha256') == digest:
            continue
        audio, rate = sf.read(path, dtype='float32', always_2d=True)
        mono = audio.mean(axis=1)
        reasons = []
        rms = float(np.sqrt(np.mean(mono.astype('float64')**2)))
        if not np.isfinite(audio).all() or rms < .0001 or float(np.max(np.abs(audio))) > .99:
            reasons.append('invalid/silent/clipped waveform')
        divisor = math.gcd(rate, 16000)
        audio16 = resample_poly(mono, 16000//divisor, rate//divisor).astype('float32')
        # Two overlapping ten-second views cover the entire take; a middle
        # window catches scene changes near the join. No runtime ML dependency.
        offsets = sorted(set([0, max(0, (len(audio16)-160000)//2), max(0, len(audio16)-160000)]))
        windows = [audio16[o:o+160000] for o in offsets]
        inputs = features(windows, sampling_rate=16000, return_tensors='pt')
        with torch.inference_mode():
            scores = model(**{k: v.to(args.device) for k, v in inputs.items()}).logits.sigmoid().cpu().numpy()
        maximum = scores.max(axis=0)
        named = {labels[i]: float(v) for i, v in enumerate(maximum)}
        def peak(*names):
            return max((named.get(n, 0) for n in names), default=0)
        family = entry['group']
        if peak('Music') > .22:
            reasons.append('music')
        if peak('Speech', 'Singing', 'Conversation') > .27:
            reasons.append('human voice')
        if peak('Engine', 'Vehicle', 'Siren', 'Electric shaver, electric razor', 'Alarm') > .30:
            reasons.append('mechanical source')
        if peak('Rain', 'Thunder', 'Stream', 'Waves, surf', 'Waterfall') > .28:
            reasons.append('baked weather/water')
        if peak('Wind noise (microphone)', 'Rustling leaves') > .36:
            reasons.append('baked wind/foliage')
        insect = family in ('cricket', 'cicada', 'katydid')
        if insect and peak('Bird', 'Bird vocalization, bird call, bird song', 'Chirp, tweet', 'Frog') > .32:
            reasons.append('other animal alongside insect')
        if family == 'frog' and peak('Bird', 'Bird vocalization, bird call, bird song', 'Chirp, tweet', 'Cricket') > .38:
            reasons.append('other animal alongside frog')
        if not insect and family != 'frog' and peak('Cricket', 'Frog') > .38:
            reasons.append('baked insect/frog')
        expected = peak('Cricket', 'Insect', 'Fly, housefly', 'Buzz') if insect else peak('Frog', 'Croak', 'Animal') if family == 'frog' else peak('Bird', 'Bird vocalization, bird call, bird song', 'Animal', 'Chirp, tweet', 'Coo', 'Crow')
        top = np.argsort(maximum)[-10:][::-1]
        result['files'][key] = dict(path=str(path.resolve()), sha256=digest, bank=family, form=entry['form'], take=entry['take'],
            seconds=len(mono)/rate, rms=rms, accepted=not reasons, reasons=reasons, identity_score=expected,cue=entry.get('cue','ambient'),
            weak_identity=expected < .08, top={labels[int(i)]:round(float(maximum[i]),4) for i in top},
            note='Automated contamination screen only; owner may reject any take by deleting its active file.')
        result['elapsed_seconds'] = time.time()-started
        result['present_candidates'] = len(paths)
        temp = args.output.with_suffix('.pending')
        temp.write_text(json.dumps(result, indent=2), encoding='utf-8');temp.replace(args.output)
        if len(result['files']) % 12 == 0:
            print(json.dumps({'screened':len(result['files']), 'accepted':sum(r['accepted'] for r in result['files'].values()), 'seconds':round(time.time()-started)}),flush=True)
    print(json.dumps({'screened':len(result['files']), 'accepted':sum(r['accepted'] for r in result['files'].values()), 'seconds':round(time.time()-started)}),flush=True)


if __name__ == '__main__':
    main()
