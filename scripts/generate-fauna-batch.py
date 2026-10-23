"""Offline SA3 audition batch. Never publishes into the game's active sound bank.
Compatible takes share a call-form prompt; seeds distinguish performances.
Completed mono clips appear incrementally. Deleting an audition is respected on
resume: its admission index remains and the file is never silently recreated.
"""
import argparse
import csv
from datetime import datetime, timezone
import hashlib
import io
import json
from pathlib import Path
import subprocess
import sys

from filelock import FileLock
import numpy as np
import soundfile as sf
from scipy.signal import butter, sosfilt


# Candidate counts are twice the proposed accepted bank (24/16/24/32/48).
# These are broad acoustic forms, NOT added Earth species or animal populations.
FORMS = {
    'cricket': [
        'A single cricket repeating a clear four-chirp phrase with brief quiet gaps.',
        'A single cricket producing a soft sustained tonal trill with gentle natural pauses.',
        'A single cricket repeating light paired chirps at a relaxed pace.',
        'A single cricket repeating a bright three-chirp phrase with long quiet gaps.',
        'A single cricket producing a fine high-pitched pulsing trill with short quiet pauses.',
        'A single cricket repeating low rounded chirps in short groups with natural quiet gaps.',
    ],
    'cicada': [
        'A single cicada producing a steady bright buzzing call with gradual swells.',
        'A single cicada repeating a dry pulsing buzz with slow rises and falls in intensity.',
        'A single cicada producing a long raspy vibrating trill that gently swells and subsides.',
        'A single cicada producing a clear high whirring buzz in gently rising phrases separated by quiet pauses.',
    ],
    'katydid': [
        'A single katydid repeating a dry raspy two-part chirr with quiet gaps.',
        'A single katydid repeating a short scratchy three-part chirr with natural quiet pauses.',
        'A single katydid repeating crisp rhythmic rasping phrases separated by silence.',
        'A single katydid repeating a brief high-pitched tick followed by a longer dry rasp, with quiet gaps.',
        'A single katydid producing a fine sibilant trill in short phrases separated by silence.',
        'A single katydid repeating widely spaced sharp scratchy single chirrs.',
    ],
    'frog': [
        'A single small frog repeating rounded pulsed croaks with quiet pauses.',
        'A single small frog repeating short hollow resonant trills with natural quiet pauses.',
        'A single small frog repeating a gentle two-croak phrase with irregular quiet gaps.',
        'A single frog repeating low soft throaty croaks in short groups with quiet pauses.',
        'A single small frog repeating short clear peeping calls separated by quiet gaps.',
        'A single frog repeating hollow bubbling croaks with natural quiet pauses.',
        'A single small frog repeating short dry rattling croaks in regular phrases separated by silence.',
        'A single frog repeating a resonant descending croak with a brief pulsed finish and quiet pauses.',
    ],
    'bird': [
        'A single small songbird repeating a short phrase of clear whistles and chirps with natural quiet pauses.',
        'A single small songbird repeating a delicate rising and falling whistle phrase with quiet gaps.',
        'A single small songbird repeating brief bright chirrup phrases separated by silence.',
        'A single small songbird repeating soft rounded two-note contact calls with irregular quiet gaps.',
        'A single small songbird repeating a short clear descending whistle phrase with quiet pauses.',
        'A single small songbird repeating a fine high trill followed by two short chirps, with quiet gaps.',
        'A single small songbird repeating a mellow three-note whistle phrase with long quiet pauses.',
        'A single small songbird repeating quick dry chattering contact calls in short groups separated by silence.',
    ],
}
CHARACTER = ' Close-miked isolated caller, dry direct sound, silent background, consistent voice and microphone position, subtle natural timing variation.'
RATE = 44100


def atomic_json(path, value):
    temp = path.with_suffix('.pending')
    temp.write_text(json.dumps(value, indent=2), encoding='utf-8')
    temp.replace(path)


def main():
    repo = Path(__file__).resolve().parents[1]
    ap = argparse.ArgumentParser()
    ap.add_argument('--batch-root', type=Path, required=True)
    ap.add_argument('--comfy', type=Path, default=Path('E:/AI/ComfyUI'))
    ap.add_argument('--url', default='http://127.0.0.1:8188')
    ap.add_argument('--seed-base', type=int, default=10227319)
    args = ap.parse_args()
    root = (repo/args.batch_root).resolve()
    if not root.is_relative_to((repo/'artifacts').resolve()):
        raise ValueError('Batch output must stay inside repository artifacts')
    root.mkdir(parents=True, exist_ok=True)
    with FileLock(str(root/'batch.lock'), timeout=0):
        run(repo, root, args)


def run(repo, root, args):
    tracks = []
    # Interleave groups so all five get audition material early, not after a
    # whole group has finished. Keep future regional call-form grouping explicit.
    for take in range(1, 13):
        for form in range(1, 9):
            for family, forms in FORMS.items():
                if form>len(forms) or take>(12 if family=='bird' else 8):
                    continue
                tracks.append(dict(id=f'{family}-ai-f{form:02d}-t{take:02d}', group=family,
                    seconds=20, brief=forms[form-1]+CHARACTER, form=form, take=take))
    if len(tracks) != 288:
        raise ValueError('Unexpected catalogue size')
    catalog = root/'catalog.csv'
    buffer = io.StringIO(newline='')
    writer = csv.DictWriter(buffer, fieldnames=['id','group','seconds','brief','form','take'])
    writer.writeheader();writer.writerows(tracks)
    content = buffer.getvalue().encode('utf-8')
    if catalog.exists() and catalog.read_bytes()!=content:
        raise ValueError('Existing batch catalogue changed; use a fresh batch root')
    if not catalog.exists():
        catalog.write_bytes(content)
    (root/'README.txt').write_text(
        'WORLDGEN FAUNA - AI AUDITION CANDIDATES\n'
        'Listen inside audition/<group>. Each fNN is a call form; tNN is a candidate take.\n'
        'Counts: cricket48 cicada32 katydid48 frog64 bird96, each20seconds.\n'
        'Files appear as jobs finish. Delete any audition WAV you dislike; resume will NOT recreate indexed deletions.\n'
        'Stereo candidates and raw Comfy masters are separate and retained for editing/provenance.\n'
        'Nothing is automatically installed into sounds/fauna or the running game.\n'
        'Source isolation/naturalness require listening: reject unwanted callers, background beds or artifacts.\n'
        'Progress: batch-status.json; per-clip origin: audition-index.json; prompts: catalog.csv.\n'
        'To pause after the current clip, create an empty file jobs/PAUSE.\n'
        'Local generation uses the RTX4060 and can compete with game GPU resources.\n'
        'Model: stabilityai/stable-audio-3-small-sfx, existing local installation.\n'
        'Model/encoder licensing: https://stability.ai/license and Gemma terms.\n', encoding='utf-8')
    candidates, jobs, audition = root/'candidates-stereo', root/'jobs', root/'audition'
    for folder in (candidates, jobs, audition):
        folder.mkdir(exist_ok=True)
    for family in FORMS:
        (audition/family).mkdir(exist_ok=True)
    by_id = {t['id']:t for t in tracks}
    index_path = root/'audition-index.json'
    index = json.loads(index_path.read_text()) if index_path.exists() else {}
    started = datetime.now(timezone.utc).isoformat()

    def status(state, error=None):
        atomic_json(root/'batch-status.json', dict(state=state, planned=288, prepared=len(index),
            present=sum((root/r['audition']).exists() for r in index.values()), started=started,
            updated=datetime.now(timezone.utc).isoformat(), error=error))

    def prepare(key):
        if key in index:  # Includes owner-deleted/rejected audition files.
            return
        track = by_id[key]
        results = json.loads((jobs/'results.json').read_text())
        info = results[key]
        path = candidates/(key+'.wav')
        if hashlib.sha256(path.read_bytes()).hexdigest()!=info['file_sha256']:
            raise ValueError('AI candidate/provenance hash mismatch: '+key)
        audio, sr = sf.read(path, dtype='float32', always_2d=True)
        if sr!=RATE or audio.shape[1]!=2 or not np.isfinite(audio).all():
            raise ValueError('Invalid stereo candidate')
        mono = np.mean(audio, axis=1)
        cutoff = dict(cricket=1200,cicada=900,katydid=1500,frog=90,bird=600)[track['group']]
        mono = sosfilt(butter(2, cutoff, btype='highpass', fs=RATE, output='sos'), mono).astype('float32')
        mono -= np.mean(mono)
        rms = float(np.sqrt(np.mean(mono.astype('float64')**2)))
        peak = float(np.max(np.abs(mono)))
        if len(mono)<RATE*18 or rms<1e-6:
            raise ValueError('Silent/short audition candidate')
        gain = min(10**(-25/20)/rms,.55/max(peak,1e-8),12)
        mono *= gain
        fade = min(int(RATE*.12),len(mono)//8)
        ramp = np.sin(np.linspace(0,np.pi/2,fade))**2
        mono[:fade]*=ramp;mono[-fade:]*=ramp[::-1];mono[0]=mono[-1]=0
        destination = audition/track['group']/(key+'.wav')
        if destination.exists():
            raise FileExistsError('Refusing unmanaged audition: '+str(destination))
        temp = destination.with_suffix('.pending')
        sf.write(temp, mono, RATE, subtype='PCM_16', format='WAV')
        sha = hashlib.sha256(temp.read_bytes()).hexdigest()
        temp.replace(destination)
        index[key] = dict(group=track['group'],form=track['form'],take=track['take'],
            audition=str(destination.relative_to(root)),sha256=sha,seconds=len(mono)/RATE,
            prompt=info['prompt'],seed=info['actual_seed'],model=info['model'],
            candidate_sha256=info['file_sha256'],master=info['master'],gain=gain,
            preparation='mono, group highpass, bounded gain, 120ms endpoint fades; not listening approval')
        atomic_json(index_path,index)
        status('generating')
        print(json.dumps(dict(event='audition-ready',id=key,prepared=len(index))),flush=True)

    status('starting')
    try:
        if (jobs/'results.json').exists():
            for key in json.loads((jobs/'results.json').read_text()):
                if key in by_id:
                    prepare(key)
        command = [sys.executable,str(repo/'scripts/generate-weather-sounds.py'),
            '--catalog',str(catalog),'--output',str(candidates),'--evidence',str(jobs),
            '--checkpoint','stable_audio_3_small_sfx.safetensors','--seed-base',str(args.seed_base),
            '--url',args.url,'--comfy',str(args.comfy),'--limit','288']
        with (root/'generator.log').open('a',encoding='utf-8',buffering=1) as log:
            child = subprocess.Popen(command,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,
                text=True,encoding='utf-8',errors='replace',bufsize=1)
            try:
                for line in child.stdout:
                    log.write(line)
                    try:
                        event=json.loads(line)
                    except json.JSONDecodeError:
                        continue
                    if event.get('event')=='complete':
                        prepare(event['id'])
                code=child.wait()
            except Exception:
                # Do not leave an uncontrolled producer after publisher failure.
                # Only our subprocess is stopped, never the shared Comfy server.
                child.terminate();child.wait()
                raise
        if code:
            raise RuntimeError('Generator exited '+str(code)+'; inspect generator.log/current-job.json before resuming')
        status('complete' if len(index)==288 else 'paused')
    except Exception as exc:
        status('failed',str(exc))
        raise


if __name__=='__main__':
    main()
