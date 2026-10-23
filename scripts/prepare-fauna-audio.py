"""Offline fauna library: authored DSP, SA3 candidates and licensed field excerpts.
No model inference or downloads in the game. Media is ignored by Git; provenance
and exact playback hashes are published beside it. Does not launch/play audio.
"""
import argparse
import csv
import hashlib
import io
import json
import math
from pathlib import Path
import re
import urllib.request

import numpy as np
import soundfile as sf
from scipy.signal import butter, sosfilt, resample_poly

RATE = 44100
FAMILIES = ('cricket', 'cicada', 'katydid', 'frog', 'bird')
SOURCES = {
    'cricket': ('TheWoodlandNomad', 363111, 'Cricket Chirp', 'CC0-1.0', 'https://creativecommons.org/publicdomain/zero/1.0/'),
    'cicada': ('macdaddyno1', 321591, 'Japanese Cicada', 'CC0-1.0', 'https://creativecommons.org/publicdomain/zero/1.0/'),
    'katydid': ('timb12957', 158782, 'SC_Katydid.wav', 'CC-BY-4.0', 'https://creativecommons.org/licenses/by/4.0/'),
    'frog': ('SpliceSound', 188195, 'Green frog, croaking.wav', 'CC0-1.0', 'https://creativecommons.org/publicdomain/zero/1.0/'),
    'bird': ('organicmanpl', 518749, 'Single bird', 'CC0-1.0', 'https://creativecommons.org/publicdomain/zero/1.0/'),
}

def fetch(url):
    req = urllib.request.Request(url, headers={'User-Agent': 'WorldGenFaunaAudio/1.0 (licensed sound attribution and public preview download)'})
    with urllib.request.urlopen(req, timeout=40) as response:
        data = response.read(32 * 1024 * 1024 + 1)
    if len(data) > 32 * 1024 * 1024:
        raise ValueError('Source exceeds bounded download size')
    return data

def synth(family, variant):
    rng = np.random.default_rng(84017 + FAMILIES.index(family)*911 + variant*71)
    length = 18 if family in FAMILIES[:3] else 5
    t = np.arange(RATE*length, dtype=np.float64)/RATE
    noise = rng.standard_normal(len(t))
    if family == 'cricket':
        carrier = 3900 + variant*430
        phrase = np.mod(t + variant*.17, 1.15 + variant*.08)
        pulse = np.mod(phrase, .13)
        env = np.sin(np.pi*np.clip(pulse/.065, 0, 1))**2 * (pulse < .065) * (phrase < .52)
        voice = (np.sin(2*np.pi*carrier*t + .1*np.sin(2*np.pi*31*t)) + .12*np.sin(4*np.pi*carrier*t)) * env
    elif family == 'cicada':
        band = sosfilt(butter(3, [3000+variant*200, 9000], btype='bandpass', fs=RATE, output='sos'), noise)
        micro = .52+.48*np.sin(2*np.pi*(82+variant*13)*t)**2
        swell = .48+.24*np.sin(2*np.pi*t/(4.8+variant*.7))+.12*np.sin(2*np.pi*t/1.7)
        voice = (band*.75 + np.sin(2*np.pi*(5300+variant*600)*t + 1.2*np.sin(2*np.pi*110*t))*.22)*micro*swell
    elif family == 'katydid':
        band = sosfilt(butter(3, [4200, 11500], btype='bandpass', fs=RATE, output='sos'), noise)
        phrase = np.mod(t+variant*.12, 1.35+variant*.09)
        pulse = np.mod(phrase, .16)
        env = np.sin(np.pi*np.clip(pulse/.095, 0, 1))**2 * (pulse < .095) * (phrase < .48)
        voice = (band + .12*np.sin(2*np.pi*(7400+variant*400)*t)) * env
    elif family == 'frog':
        phrase = np.mod(t, 1.3+variant*.16)
        on = .38+variant*.06
        env = np.sin(np.pi*np.clip(phrase/on, 0, 1))**2 * (phrase < on)
        freq = 260 + variant*130 + 55*np.exp(-phrase*12)
        phase = 2*np.pi*np.cumsum(freq)/RATE
        voice = (np.sin(phase)+.55*np.sin(phase*2)+.22*np.sin(phase*3))*env*(.55+.45*np.sin(2*np.pi*(22+variant*3)*t)**2)
    else:
        voice = np.zeros_like(t)
        for n, start in enumerate((.3, .65, 1.12, 2.5, 2.84, 3.4)):
            duration = .13+.03*((n+variant)%3)
            x = np.clip(t-start, 0, duration)
            f0 = 2100 + variant*470 + n%3*310
            slope = (1 if n%2 == 0 else -1)*(2600+variant*250)
            phase = 2*np.pi*(f0*x + .5*slope*x*x) + .12*np.sin(2*np.pi*35*x)
            env = np.sin(np.pi*x/duration)**2 * ((t >= start)&(t < start+duration))
            voice += (np.sin(phase)+.13*np.sin(phase*2))*env
    return voice.astype('float32')

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--ai-root', type=Path, default=Path('artifacts/fauna-ai-candidates'))
    ap.add_argument('--ai-evidence', type=Path, default=Path('artifacts/fauna-ai-jobs/results.json'))
    ap.add_argument('--only-ai', choices=FAMILIES, help='Publish one replacement without regenerating other banks or downloading recordings')
    ap.add_argument('--variant', type=int, choices=(1,2,3), default=1)
    ap.add_argument('--skip-ai', action='store_true')
    ap.add_argument('--replace', action='store_true', help='Replace this script\'s managed outputs after changing candidates')
    args = ap.parse_args()
    root = Path(__file__).resolve().parents[1]
    output = root/'sounds/fauna'
    evidence = root/'artifacts/fauna-audio'
    output.mkdir(parents=True, exist_ok=True)
    evidence.mkdir(parents=True, exist_ok=True)
    manifest = output/'library.tsv'
    old = {r['file']: r for r in csv.DictReader(manifest.open(encoding='utf-8'), delimiter='\t')} if manifest.exists() else {}
    records, details, credits = [], [], []
    ai_info_path = root/args.ai_evidence
    ai_info = json.loads(ai_info_path.read_text()) if ai_info_path.exists() else {}

    def publish(family, method, variant, audio, sr, provenance):
        if audio.ndim > 1:
            audio = np.mean(audio, axis=1)
        if sr != RATE:
            divisor = math.gcd(sr, RATE)
            audio = resample_poly(audio, RATE//divisor, sr//divisor)
        audio = np.asarray(audio, dtype='float32')
        if len(audio) < RATE*.3 or len(audio) > RATE*45 or not np.isfinite(audio).all():
            raise ValueError('Invalid duration/nonfinite PCM')
        # Remove field rumble, not a claim of semantic source isolation.
        cutoff = {'cricket': 1200, 'cicada': 900, 'katydid': 1500, 'frog': 90, 'bird': 600}[family]
        audio = sosfilt(butter(2, cutoff, btype='highpass', fs=RATE, output='sos'), audio).astype('float32')
        audio -= np.mean(audio)
        rms = float(np.sqrt(np.mean(audio.astype('float64')**2)))
        peak = float(np.max(np.abs(audio)))
        if rms < 1e-6:
            raise ValueError('Silent source')
        # Conservative common audition reference; runtime controls aggregate gain.
        gain = min(10**(-25/20)/rms, .55/max(peak, 1e-8), 12)
        audio *= gain
        fade = min(int(RATE*.12), len(audio)//8)
        ramp = np.sin(np.linspace(0, np.pi/2, fade))**2
        audio[:fade] *= ramp
        audio[-fade:] *= ramp[::-1]
        audio[0] = audio[-1] = 0
        filename = f'{family}-{method}-{variant+1:02d}.wav'
        destination = output/filename
        buffer = io.BytesIO()
        sf.write(buffer, audio, RATE, format='WAV', subtype='PCM_16')
        content = buffer.getvalue()
        sha = hashlib.sha256(content).hexdigest()
        if destination.exists() and hashlib.sha256(destination.read_bytes()).hexdigest() != sha:
            if filename not in old or not args.replace:
                raise FileExistsError(f'Refusing changed/unmanaged output {destination}; use --replace for managed replacements')
            if hashlib.sha256(destination.read_bytes()).hexdigest() != old[filename]['sha256']:
                raise ValueError('Existing managed audio was modified outside publisher')
        if not destination.exists() or hashlib.sha256(destination.read_bytes()).hexdigest() != sha:
            temp = destination.with_suffix('.pending')
            temp.write_bytes(content)
            temp.replace(destination)
        records.append(dict(family=family, method=method, file=filename, sha256=sha, seconds=f'{len(audio)/RATE:.6f}'))
        details.append(dict(records[-1], **provenance, gain=gain, rms=float(np.sqrt(np.mean(audio**2))), peak=float(np.max(np.abs(audio)))))

    if args.only_ai:
        if args.skip_ai:
            raise ValueError('--only-ai cannot be used with --skip-ai')
        family = args.only_ai
        name = f'{family}-ai-{args.variant:02d}'
        candidate = root/args.ai_root/(name+'.wav')
        info = ai_info.get(name)
        if info is None or hashlib.sha256(candidate.read_bytes()).hexdigest() != info['file_sha256']:
            raise ValueError('AI candidate/provenance mismatch: '+name)
        audio, sr = sf.read(candidate, dtype='float32', always_2d=True)
        publish(family, 'ai', args.variant-1, audio, sr, dict(origin='Local Stable Audio 3', prompt=info['prompt'], seed=info['actual_seed'], model=info['model'], source_sha256=info['file_sha256'], license='Stability AI Community License / Gemma terms'))
        changed = records[0]['file']
        records = [records[0] if r['file'] == changed else r for r in old.values()]
        if changed not in old:
            raise ValueError('One-clip publication requires an existing complete managed library')
        old_details = json.loads((evidence/'release.json').read_text())
        details = [details[0] if r['file'] == changed else r for r in old_details]
        with manifest.open('w', newline='', encoding='utf-8') as stream:
            writer = csv.DictWriter(stream, fieldnames=['family','method','file','sha256','seconds'], delimiter='\t')
            writer.writeheader(); writer.writerows(records)
        (evidence/'release.json').write_text(json.dumps(details, indent=2), encoding='utf-8')
        print(json.dumps(dict(replaced=changed, sha256=next(r['sha256'] for r in records if r['file']==changed))))
        return

    for family in FAMILIES:
        for variant in range(3):
            publish(family, 'code', variant, synth(family, variant), RATE,
                    dict(origin='Independently authored DSP', script='scripts/prepare-fauna-audio.py', seed=84017+FAMILIES.index(family)*911+variant*71))
            if not args.skip_ai:
                name = f'{family}-ai-{variant+1:02d}'
                candidate = root/args.ai_root/(name+'.wav')
                audio, sr = sf.read(candidate, dtype='float32', always_2d=True)
                info = ai_info.get(name)
                if info is None or hashlib.sha256(candidate.read_bytes()).hexdigest() != info['file_sha256']:
                    raise ValueError('AI candidate/provenance mismatch: '+name)
                publish(family, 'ai', variant, audio, sr, dict(origin='Local Stable Audio 3 Small-SFX', prompt=info['prompt'], seed=info['actual_seed'], model=info['model'], source_sha256=info['file_sha256'], license='Stability AI Community License / Gemma terms'))
        author, number, title, license_name, license_url = SOURCES[family]
        page = f'https://freesound.org/people/{author}/sounds/{number}/'
        html = fetch(page).decode('utf-8')
        if license_url.removeprefix('https://').rstrip('/') not in html:
            raise ValueError('Expected source license not present: '+page)
        previews = re.findall(r'https://cdn\.freesound\.org/previews/[^\s"<>]+-hq\.mp3', html)
        if not previews:
            raise ValueError('No public HQ preview: '+page)
        raw = evidence/f'{family}-freesound-{number}.mp3'
        if not raw.exists():
            raw.write_bytes(fetch(previews[0]))
        audio, sr = sf.read(raw, dtype='float32', always_2d=True)
        if len(audio)/sr < 1:
            raise ValueError('Short field source')
        # Three distinct excerpts, not three independently recorded animals.
        length = min(12, len(audio)/sr/3)
        for variant in range(3):
            start = int((len(audio)-length*sr)*variant/2)
            publish(family, 'recorded', variant, audio[start:start+int(length*sr)], sr,
                    dict(origin='Field recording (public HQ preview)', author=author, title=title, source=page, download=previews[0], license=license_name, license_url=license_url, excerpt_start_seconds=start/sr, source_sha256=hashlib.sha256(raw.read_bytes()).hexdigest()))
        credits.append(f'{family}: "{title}" by {author}\n{page}\n{license_name}: {license_url}\nPublic HQ preview; mono downmix, high-pass, excerpts, level normalization and fades. Three excerpts from ONE source recording; source backgrounds may remain.\n')
    # Runtime TSV is deliberately small, hash-bound and contains no arbitrary paths.
    with manifest.open('w', newline='', encoding='utf-8') as stream:
        writer = csv.DictWriter(stream, fieldnames=['family','method','file','sha256','seconds'], delimiter='\t')
        writer.writeheader()
        writer.writerows(records)
    (evidence/'release.json').write_text(json.dumps(details, indent=2), encoding='utf-8')
    (output/'ATTRIBUTION.txt').write_text('WORLDGEN FAUNA AUDIO - SOURCE CREDITS\n\n'+ '\n'.join(credits)+
        '\nAI: Local Stable Audio 3 Small-SFX. Stability AI Community License and Gemma terms apply; not unconditionally public-domain audio. Check commercial registration/revenue requirements before release.\nhttps://huggingface.co/stabilityai/stable-audio-3-small-sfx\nhttps://stability.ai/license\n\nCode: independently authored offline DSP in scripts/prepare-fauna-audio.py.\nMedia is not backed up in Git. release.json records processing, hashes, prompts and seeds. Waveform checks do not establish biological realism, source purity or seamless listening.\n', encoding='utf-8')
    print(json.dumps(dict(clips=len(records), families=len(FAMILIES), methods=3 if not args.skip_ai else 2, samples_checked='finite, bounded peak/duration, zero boundary samples; no listening/game launch')))

if __name__ == '__main__':
    main()
