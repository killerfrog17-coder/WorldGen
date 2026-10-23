"""Prompt one free, local SA3 SFX candidate; optionally publish to the owned bank.
Requires the existing CUDA ComfyUI service and installed SA3 model/encoder.
Never starts/stops the owner's server, plays audio or launches the game.
"""
import argparse
import csv
from datetime import datetime, timezone
from pathlib import Path
import subprocess
import sys
import uuid


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--family', required=True, choices=('cricket','cicada','katydid','frog','bird'))
    ap.add_argument('--variant', type=int, choices=(1,2,3), default=1)
    ap.add_argument('--prompt', required=True, help='Describe only the desired isolated source, not unwanted sounds')
    ap.add_argument('--seed', type=int, default=731923)
    ap.add_argument('--url', default='http://127.0.0.1:8188')
    ap.add_argument('--comfy', type=Path, default=Path('E:/AI/ComfyUI'))
    ap.add_argument('--checkpoint', default='stable_audio_3_small_sfx.safetensors')
    ap.add_argument('--publish', action='store_true', help='Replace this managed playback variant and its manifest hash; takes effect on relaunch')
    args = ap.parse_args()
    root = Path(__file__).resolve().parents[1]
    run = root/'artifacts/fauna-ai-replacements'/(datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ')+'-'+uuid.uuid4().hex[:8])
    run.mkdir(parents=True)
    catalog = run/'prompt.csv'
    with catalog.open('w', newline='', encoding='utf-8') as stream:
        writer = csv.writer(stream)
        writer.writerow(('id','group','seconds','brief'))
        writer.writerow((f'{args.family}-ai-{args.variant:02d}',args.family,20,args.prompt))
    subprocess.run([sys.executable,str(root/'scripts/generate-weather-sounds.py'),
        '--catalog',str(catalog),'--output',str(run/'candidates'),'--evidence',str(run/'jobs'),
        '--checkpoint',args.checkpoint,'--seed-base',str(args.seed),'--url',args.url,'--comfy',str(args.comfy),'--limit','1'], check=True)
    print('Candidate and provenance:', run, flush=True)
    if args.publish:
        subprocess.run([sys.executable,str(root/'scripts/prepare-fauna-audio.py'),
            '--ai-root',str(run/'candidates'),'--ai-evidence',str(run/'jobs/results.json'),
            '--only-ai',args.family,'--variant',str(args.variant),'--replace'], check=True)
        print('Published to sounds/fauna. Relaunch through the repository shortcut to load it.')


if __name__ == '__main__':
    main()
