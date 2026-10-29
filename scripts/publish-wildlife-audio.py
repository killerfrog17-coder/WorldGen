"""Admit screened local AI recordings without recreating owner-deleted takes."""
import argparse
import csv
import hashlib
import io
import json
from pathlib import Path
import shutil


def atomic(path, content):
    temp=path.with_suffix('.pending');temp.write_text(content,encoding='utf-8');temp.replace(path)


def main():
    ap=argparse.ArgumentParser()
    ap.add_argument('--batch',type=Path,action='append',required=True)
    ap.add_argument('--screen',type=Path,action='append',required=True)
    ap.add_argument('--output',type=Path,default=Path('sounds/fauna'))
    args=ap.parse_args();args.output.mkdir(parents=True,exist_ok=True)
    admitted_path=args.output/'origins.json'
    admitted=json.loads(admitted_path.read_text(encoding='utf-8')) if admitted_path.exists() else {}
    screened={}
    for path in args.screen:screened.update(json.loads(path.read_text(encoding='utf-8'))['files'])
    added=0
    for batch in args.batch:
        for key,entry in json.loads((batch/'audition-index.json').read_text(encoding='utf-8')).items():
            if key in admitted:
                # Enrich provenance without restoring an owner-deleted file.
                admitted[key]['processing']=entry.get('processing',entry.get('preparation','mono downmix, bounded gain and endpoint fades'))
                for field in ('source_audio_sha256','trim_start_seconds','trim_end_seconds'):
                    if field in entry:admitted[key][field]=entry[field]
                continue
            source=batch/entry['audition'];screen=screened.get(key)
            if not source.exists() or not screen or not screen['accepted']:continue
            digest=hashlib.sha256(source.read_bytes()).hexdigest()
            if digest!=entry['sha256'] or digest!=screen['sha256']:raise ValueError('Audio/screen mismatch: '+key)
            relative=f'ai/{entry["group"]}/{key}.wav';destination=args.output/relative;destination.parent.mkdir(parents=True,exist_ok=True)
            if destination.exists():raise FileExistsError('Unmanaged active take: '+str(destination))
            temp=destination.with_suffix('.pending');shutil.copyfile(source,temp);temp.replace(destination)
            admitted[key]=dict(bank=entry['group'],form=entry['form'],take=entry['take'],file=relative,sha256=digest,
                seconds=entry.get('seconds',screen['seconds']),cue=entry.get('cue','ambient'),prompt=entry['prompt'],seed=entry['seed'],model=entry['model'],
                source=str(source.resolve()),screened=True,listening_approval='Owner audition pending; delete/reject any unwanted take.',
                license='Stability AI Community License and Gemma encoder terms',screen_reasons=screen['reasons'],processing=entry.get('processing',entry.get('preparation','mono downmix, bounded gain and endpoint fades')))
            for field in ('source_audio_sha256','trim_start_seconds','trim_end_seconds'):
                if field in entry:admitted[key][field]=entry[field]
            added+=1
    rows=sorted(admitted.values(),key=lambda r:(r['bank'],r['cue'],r['form'],r['take']))
    buffer=io.StringIO(newline='');writer=csv.writer(buffer,delimiter='\t',lineterminator='\n');writer.writerow(['bank','form','take','file','sha256','seconds','cue'])
    for row in rows:writer.writerow([row['bank'],row['form'],row['take'],row['file'],row['sha256'],f'{row["seconds"]:.6f}',row['cue']])
    atomic(admitted_path,json.dumps(admitted,indent=2));atomic(args.output/'library.tsv',buffer.getvalue())
    atomic(args.output/'ATTRIBUTION.txt','WORLDGEN WILDLIFE AUDIO\n\nLocally generated Stable Audio 3 Small-SFX, existing weights. No paid service and no copied field recordings.\nModel: https://huggingface.co/stabilityai/stable-audio-3-small-sfx\nModel licence: https://stability.ai/license\nPrompting: https://stability.ai/guides/stable-audio-3-prompt-guide\nEncoder: t5gemma_b_b_ul2; Gemma terms apply.\n\norigins.json records exact prompts, seeds, models, processing and delivered file hashes. Automated contamination screening is not human listening approval or a guarantee of species accuracy.\n\nDelete any active WAV you dislike; it is skipped on the next automatic refresh. The publisher will not recreate indexed deletions. Active library: sounds/fauna/ai. No in-game sound-review UI.\n')
    print(json.dumps({'added':added,'catalogued':len(rows),'present':sum((args.output/r['file']).exists() for r in rows),'bytes':sum((args.output/r['file']).stat().st_size for r in rows if (args.output/r['file']).exists())}))


if __name__=='__main__':main()
