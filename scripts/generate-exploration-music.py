"""Resumable SA3 Medium/CUDA production. Uses the existing local ComfyUI, no paid API.
Prompts/masters/results stay outside Git; never edits the historical soundtrack.
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
    ap = argparse.ArgumentParser()
    ap.add_argument('--comfy', type=Path, default=Path('E:/AI/ComfyUI'))
    ap.add_argument('--url', default='http://127.0.0.1:8188')
    ap.add_argument('--output', type=Path, default=Path(__file__).resolve().parents[1]/'music/exploration')
    ap.add_argument('--evidence', type=Path, default=Path(__file__).resolve().parents[1]/'artifacts/exploration-music')
    ap.add_argument('--limit', type=int, default=30)
    args = ap.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    args.evidence.mkdir(parents=True, exist_ok=True)

    def api(path, body=None):
        request = urllib.request.Request(args.url+path, data=None if body is None else json.dumps(body).encode(), headers={'Content-Type': 'application/json'})
        with urllib.request.urlopen(request, timeout=30) as response:
            return json.load(response)

    devices = api('/system_stats')['devices']
    if not any(d.get('type') == 'cuda' for d in devices):
        raise RuntimeError('CUDA server required; refusing CPU fallback')
    rows = list(csv.DictReader(Path(__file__).with_name('exploration-music.csv').open(encoding='utf-8')))
    tracks = []
    for i, row in enumerate(rows, 1):
        row.update(id=i, seconds=int(row['seconds']), seed=190019+7919*i)
        row['filename'] = f"{row['title']} [biome={row['biomes']};time={row['time']}].ogg"
        row['prompt'] = ('TrackType: Music, VocalType: Instrumental. '+row['brief']+f" {row['bpm']} BPM. "
                         'Purely instrumental acoustic full mix for quiet wilderness exploration; natural dynamics, '
                         'gentle melodic development, an audible opening and a short resolved ending. '
                         'Music only, without voices, animal calls, wind or environmental sound effects.')
        tracks.append(row)
    write_json(args.evidence/'catalog.json', dict(model='SA3 Medium', decoder='SAME-L tiled CUDA', steps=8, cfg=1,
               precision='FP16 diffusion / FP32 VAE, no INT8', devices=devices, tracks=tracks,
               sources=['https://stability.ai/guides/stable-audio-3-prompt-guide']))
    result_path = args.evidence/'results.json'
    results = json.loads(result_path.read_text()) if result_path.exists() else {}
    failures = []
    started = time.time()

    def status():
        write_json(args.evidence/'status.json', dict(completed=len(results), planned=len(tracks), failures=failures,
                   backend='ComfyUI-CUDA SA3 Medium', elapsed_seconds=time.time()-started))

    with FileLock(str(args.evidence/'render.lock'), timeout=0):
        status()
        count = 0
        for track in tracks:
            if (args.evidence/'PAUSE').exists() or count >= args.limit:
                break
            key = str(track['id'])
            destination = args.output/track['filename']
            if key in results and destination.exists():
                if hashlib.sha256(destination.read_bytes()).hexdigest() != results[key]['file_sha256']:
                    raise ValueError('Existing published track changed: '+str(destination))
                continue
            if destination.exists():
                raise FileExistsError('Refusing to overwrite unmanaged audio: '+str(destination))
            if shutil.disk_usage(args.comfy).free < 2*1024**3:
                raise OSError('Less than 2GB free for audio masters')
            for attempt in range(3):
                seed = track['seed']+104729*attempt
                try:
                    print(json.dumps(dict(event='start', title=track['title'], attempt=attempt+1)), flush=True)
                    graph = {
                        '1': {'class_type':'CheckpointLoaderSimple','inputs':{'ckpt_name':'stable_audio_3_medium.safetensors'}},
                        '2': {'class_type':'CLIPLoader','inputs':{'clip_name':'t5gemma_b_b_ul2.safetensors','type':'stable_audio','device':'default'}},
                        '3': {'class_type':'CLIPTextEncode','inputs':{'clip':['2',0],'text':track['prompt']}},
                        '4': {'class_type':'CLIPTextEncode','inputs':{'clip':['2',0],'text':''}},
                        '5': {'class_type':'EmptyLatentAudio','inputs':{'seconds':track['seconds'],'batch_size':1}},
                        '6': {'class_type':'ConditioningStableAudio','inputs':{'positive':['3',0],'negative':['4',0],'seconds_start':0,'seconds_total':track['seconds']}},
                        '7': {'class_type':'KSampler','inputs':{'model':['1',0],'positive':['6',0],'negative':['6',1],'latent_image':['5',0],
                              'seed':seed,'steps':8,'cfg':1.,'sampler_name':'lcm','scheduler':'simple','denoise':1.}},
                        '8': {'class_type':'VAEDecodeAudioTiled','inputs':{'samples':['7',0],'vae':['1',2],'tile_size':256,'overlap':64}},
                        '9': {'class_type':'SaveAudio','inputs':{'audio':['8',0],'filename_prefix':f"worldgen-exploration/{track['id']:02d} {track['title']}"}}}
                    submission = api('/prompt', dict(prompt=graph, client_id='worldgen-exploration'))
                    if submission.get('node_errors'):
                        raise RuntimeError(str(submission['node_errors']))
                    prompt_id = submission['prompt_id']
                    write_json(args.evidence/'current-job.json', dict(title=track['title'], prompt_id=prompt_id))
                    until = time.time()+900
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
                            raise TimeoutError('Generation still queued/running; resume later after inspecting current-job.json')
                        time.sleep(.5)
                    pcm, sr = sf.read(master, dtype='float32', always_2d=True)
                    if sr != 44100 or pcm.shape[1] != 2 or not np.isfinite(pcm).all():
                        raise ValueError('Invalid stereo master')
                    audio = pcm.T
                    n = audio.shape[1]//4410*4410
                    env = np.sqrt(np.mean(audio[:, :n].reshape(2, -1, 4410).astype('float64')**2, axis=(0, 2)))
                    body = float(np.percentile(env, 65))
                    audible = np.flatnonzero(env > max(.0015, body*.04))
                    if body < .003 or not len(audible):
                        raise ValueError('Near-silent audio')
                    start = max(0, int(audible[0]*4410)-2205)
                    end = min(audio.shape[1], int((audible[-1]+1)*4410)+4410)
                    audio = audio[:, start:end].copy()
                    seconds = audio.shape[1]/sr
                    if not 90 <= seconds <= 180:
                        raise ValueError('Duration outside 90-180s')
                    silent = env[audible[0]:audible[-1]+1] < max(.001, body*.015)
                    run = longest = 0
                    for quiet in silent:
                        run = run+1 if quiet else 0
                        longest = max(longest, run)
                    if longest > 100:
                        raise ValueError('More than 10 seconds internal near-silence')
                    rms = float(np.sqrt(np.mean(audio.astype('float64')**2)))
                    gain = min(10**(-23/20)/rms, 10**(-2/20)/max(float(np.max(np.abs(audio))), 1e-8))
                    audio *= gain
                    audio[:, :882] *= np.linspace(0, 1, 882, dtype='float32')
                    audio[:, -44100:] *= np.linspace(1, 0, 44100, dtype='float32')
                    fingerprint = hashlib.sha256(np.rint(audio*32767).astype('int16').tobytes()).hexdigest()
                    if any(r['pcm_sha256'] == fingerprint for r in results.values()):
                        raise ValueError('Duplicate audio')
                    temp = destination.with_suffix('.pending')
                    with sf.SoundFile(temp, 'w', samplerate=sr, channels=2, format='OGG', subtype='VORBIS', compression_level=.7) as stream:
                        for offset in range(0, audio.shape[1], 4096):
                            stream.write(audio[:, offset:offset+4096].T)
                    decoded, decoded_sr = sf.read(temp, dtype='float32', always_2d=True)
                    peak = float(np.max(np.abs(decoded)))
                    if decoded_sr != sr or not np.isfinite(decoded).all() or peak >= .999 or np.sqrt(np.mean(decoded**2)) < .002:
                        raise ValueError('Encoded readback failed')
                    record = dict(track, actual_seed=seed, master=str(master), prompt_id=prompt_id, seconds=seconds,
                                  pcm_sha256=fingerprint, file_sha256=hashlib.sha256(temp.read_bytes()).hexdigest(),
                                  peak_dbfs=20*math.log10(max(peak, 1e-9)), gain_db=20*math.log10(gain), path=str(destination))
                    write_json(master.with_suffix('.json'), record)
                    temp.replace(destination)
                    results[key] = record
                    write_json(result_path, results)
                    status()
                    count += 1
                    print(json.dumps(dict(event='complete', title=track['title'], completed=len(results), seconds=seconds)), flush=True)
                    break
                except TimeoutError:
                    raise  # Never submit duplicate work while an existing GPU job may still be running.
                except Exception as exc:
                    print(json.dumps(dict(event='failed', title=track['title'], error=str(exc))), flush=True)
                    if 'out of memory' in str(exc).lower():
                        raise
                    if attempt == 2:
                        failures.append(dict(title=track['title'], error=str(exc)))
                        status()
        status()
        if failures:
            raise RuntimeError(str(failures))


if __name__ == '__main__':
    main()
