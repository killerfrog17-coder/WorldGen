"""Offline SA3/CUDA SFX candidates; no procedural audio or music exporter.

Immutable source briefs are tracked; media, raw masters and job evidence are not.
Processing only trims/fades/normalizes recorded output. It synthesizes no sounds.
"""
import argparse
import csv
import hashlib
import json
import math
from pathlib import Path
import shutil
import time
import urllib.request

import numpy as np
import soundfile as sf
from filelock import FileLock


def write_json(path, value):
    temp = path.with_suffix(path.suffix + '.tmp')
    temp.write_text(json.dumps(value, indent=2), encoding='utf-8')
    temp.replace(path)


def main():
    root = Path(__file__).resolve().parents[1]
    ap = argparse.ArgumentParser()
    ap.add_argument('--comfy', type=Path, default=Path('E:/AI/ComfyUI'))
    ap.add_argument('--url', default='http://127.0.0.1:8188')
    ap.add_argument('--output', type=Path, default=root/'artifacts/weather-sound-candidates')
    ap.add_argument('--evidence', type=Path, default=root/'artifacts/weather-sound-candidate-jobs')
    ap.add_argument('--limit', type=int, default=100)
    ap.add_argument('--catalog', type=Path, default=Path(__file__).with_name('weather-sounds.csv'))
    ap.add_argument('--seed-base', type=int, default=192077)
    ap.add_argument('--sampler', default='lcm', choices=['lcm','euler'])
    ap.add_argument('--checkpoint', default='stable_audio_3_medium.safetensors')
    args = ap.parse_args()
    model_id='stabilityai/'+Path(args.checkpoint).stem.replace('_','-')
    args.output.mkdir(parents=True, exist_ok=True)
    args.evidence.mkdir(parents=True, exist_ok=True)

    def api(path, body=None):
        req = urllib.request.Request(args.url+path, data=None if body is None else json.dumps(body).encode(),
                                     headers={'Content-Type': 'application/json'})
        with urllib.request.urlopen(req, timeout=30) as response:
            return json.load(response)

    devices = api('/system_stats')['devices']
    if not any(d.get('type') == 'cuda' for d in devices):
        raise RuntimeError('CUDA required; refusing CPU fallback')
    tracks = list(csv.DictReader(args.catalog.open(encoding='utf-8')))
    for i, track in enumerate(tracks):
        track['seconds'] = int(track['seconds'])
        track['seed'] = args.seed_base + i*7919
        track['sampler'] = args.sampler
        track['checkpoint'] = args.checkpoint
        track['model'] = model_id
        # Post-trained CFG1 is not an exclusion mechanism. Describe the desired
        # source, not a field scene or a list of unwanted things. Audit the audio.
        track['prompt'] = 'TrackType: SFX. '+track['brief']
    write_json(args.evidence/'catalog.json', dict(model=model_id,
        checkpoint=args.checkpoint, encoder='t5gemma_b_b_ul2.safetensors',
        decoder=('SAME-S' if 'small' in args.checkpoint else 'SAME-L')+' tiled CUDA FP32', diffusion='FP16', steps=8, cfg=1,
        library_version=2, sources=['https://huggingface.co/'+model_id,
                 'https://kb.stability.ai/knowledge-base/stable-audio-3-prompt-guide',
                 'https://stability.ai/license'], devices=devices, tracks=tracks))
    results_path = args.evidence/'results.json'
    results = json.loads(results_path.read_text()) if results_path.exists() else {}
    failures = []
    started = time.time()

    def status():
        write_json(args.evidence/'status.json', dict(completed=len(results), planned=len(tracks),
            failures=failures, elapsed_seconds=time.time()-started, backend=args.checkpoint+' CUDA'))

    with FileLock(str(args.evidence/'render.lock'), timeout=0):
        status()
        count = 0
        for track in tracks:
            key = track['id']
            destination = args.output/(key+'.wav')
            if destination.exists():
                if key not in results or track['prompt'] != results[key]['prompt'] or args.sampler != results[key].get('sampler','lcm') or args.checkpoint != results[key].get('checkpoint','stable_audio_3_medium.safetensors') or track['seed'] != results[key]['seed'] or hashlib.sha256(destination.read_bytes()).hexdigest() != results[key]['file_sha256']:
                    raise ValueError('Refusing changed/unmanaged output: '+str(destination))
                continue
            if count >= args.limit or (args.evidence/'PAUSE').exists():
                break
            if shutil.disk_usage(args.comfy).free < 1024**3:
                raise OSError('Need 1GB free for audio masters')
            for attempt in range(3):
                seed = track['seed']+104729*attempt
                print(json.dumps(dict(event='start', id=key, attempt=attempt+1)), flush=True)
                graph = {
                    '1': {'class_type':'CheckpointLoaderSimple','inputs':{'ckpt_name':args.checkpoint}},
                    '2': {'class_type':'CLIPLoader','inputs':{'clip_name':'t5gemma_b_b_ul2.safetensors','type':'stable_audio','device':'default'}},
                    '3': {'class_type':'CLIPTextEncode','inputs':{'clip':['2',0],'text':track['prompt']}},
                    '4': {'class_type':'CLIPTextEncode','inputs':{'clip':['2',0],'text':''}},
                    '5': {'class_type':'EmptyLatentAudio','inputs':{'seconds':track['seconds'],'batch_size':1}},
                    '6': {'class_type':'ConditioningStableAudio','inputs':{'positive':['3',0],'negative':['4',0],'seconds_start':0,'seconds_total':track['seconds']}},
                    '7': {'class_type':'KSampler','inputs':{'model':['1',0],'positive':['6',0],'negative':['6',1],'latent_image':['5',0],
                        'seed':seed,'steps':8,'cfg':1.,'sampler_name':args.sampler,'scheduler':'simple','denoise':1.}},
                    '8': {'class_type':'VAEDecodeAudioTiled','inputs':{'samples':['7',0],'vae':['1',2],'tile_size':64,'overlap':16}},
                    '9': {'class_type':'SaveAudio','inputs':{'audio':['8',0],'filename_prefix':'worldgen-weather-isolated/'+key}}}
                submission = api('/prompt', dict(prompt=graph, client_id='worldgen-weather'))
                if submission.get('node_errors'):
                    raise RuntimeError(str(submission['node_errors']))
                prompt_id = submission['prompt_id']
                write_json(args.evidence/'current-job.json', dict(id=key, prompt_id=prompt_id))
                until = time.time()+1200
                while True:
                    history = api('/history/'+prompt_id).get(prompt_id)
                    if history:
                        if history.get('status', {}).get('status_str') == 'error':
                            raise RuntimeError(str(history['status']))
                        files = history.get('outputs', {}).get('9', {}).get('audio', [])
                        if files:
                            item = files[0]
                            master = (args.comfy/'output'/item.get('subfolder', '')/item['filename']).resolve()
                            if (args.comfy.resolve()/'output') not in master.parents:
                                raise ValueError('Unexpected output path')
                            break
                    if time.time() > until:
                        raise TimeoutError('Inspect current-job.json before resuming; do not duplicate a running job')
                    time.sleep(.5)
                try:
                    audio, sr = sf.read(master, dtype='float32', always_2d=True)
                    if sr != 44100 or audio.shape[1] != 2 or not np.isfinite(audio).all():
                        raise ValueError('Invalid SA3 stereo master')
                    thunder = track['group'] == 'thunder'
                    if thunder:
                        audio = np.mean(audio, axis=1, keepdims=True)
                        # Keep physical onset close to zero; the runtime adds propagation delay.
                        energy = np.sqrt(np.mean(audio[:len(audio)//441*441].reshape(-1, 441, 1).astype('float64')**2, axis=(1, 2)))
                        active = np.flatnonzero(energy > max(.002, float(energy.max())*.08))
                        if not len(active):
                            raise ValueError('Silent thunder')
                        audio = audio[max(0, int(active[0])*441-441):].copy()
                    rms = float(np.sqrt(np.mean(audio.astype('float64')**2)))
                    wind = track['group'].startswith('wind')
                    if rms < .001 or len(audio)/sr < (4 if thunder or wind else 18):
                        raise ValueError('Silent or short effect')
                    target = -19 if thunder else -32 if wind else -23
                    gain = min(10**(target/20)/rms, .70/max(float(np.max(np.abs(audio))), 1e-8))
                    if wind:gain = min(1.,gain)  # Never inflate a quiet wind/noise floor.
                    audio *= gain
                    fade_in = 220 if thunder else 4410
                    fade_out = 22050 if thunder else 4410
                    audio[:fade_in] *= np.linspace(0, 1, fade_in, dtype='float32')[:, None]
                    audio[-fade_out:] *= np.linspace(1, 0, fade_out, dtype='float32')[:, None]
                    fingerprint = hashlib.sha256(np.rint(audio*32767).astype('int16').tobytes()).hexdigest()
                    if any(r['pcm_sha256'] == fingerprint for r in results.values()):
                        raise ValueError('Duplicate output')
                    temp = destination.with_suffix('.pending')
                    with sf.SoundFile(temp, 'w', samplerate=sr, channels=audio.shape[1], format='WAV', subtype='PCM_16') as stream:
                        for offset in range(0, len(audio), 4096):
                            stream.write(audio[offset:offset+4096])
                    decoded, read_sr = sf.read(temp, dtype='float32', always_2d=True)
                    peak = float(np.max(np.abs(decoded)))
                    if read_sr != sr or not np.isfinite(decoded).all() or peak > .701 or np.sqrt(np.mean(decoded**2)) < .001:
                        raise ValueError('PCM readback failed')
                    record = dict(track, actual_seed=seed, master=str(master), prompt_id=prompt_id,
                        seconds=len(audio)/sr, channels=audio.shape[1], sample_rate=sr, gain_db=20*math.log10(gain),
                        pcm_sha256=fingerprint, file_sha256=hashlib.sha256(temp.read_bytes()).hexdigest(), path=str(destination))
                    write_json(master.with_suffix('.json'), record)
                    temp.replace(destination)
                    results[key] = record
                    write_json(results_path, results)
                    count += 1
                    status()
                    print(json.dumps(dict(event='complete', id=key, completed=len(results))), flush=True)
                    break
                except ValueError as exc:
                    print(json.dumps(dict(event='rejected', id=key, error=str(exc))), flush=True)
                    if attempt == 2:
                        failures.append(dict(id=key, error=str(exc)))
                        status()
                except Exception as exc:
                    failures.append(dict(id=key, error=str(exc)))
                    status()
                    raise
        status()
        if failures:
            raise RuntimeError(str(failures))


if __name__ == '__main__':
    main()
