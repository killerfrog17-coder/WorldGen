"""Download pinned inference-only motion assets; never runs CUDA or paid APIs."""
import argparse
import hashlib
import json
from pathlib import Path
import urllib.request


def request_json(url):
    req=urllib.request.Request(url,headers={'User-Agent':'WorldGen-local-motion-evaluation/1.0'})
    with urllib.request.urlopen(req,timeout=60) as response:
        return json.load(response)


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--root',type=Path,required=True)
    parser.add_argument('--download',action='store_true')
    args=parser.parse_args()
    args.root.mkdir(parents=True,exist_ok=True)
    catalogue={
        'Linzhan/UniMate':lambda p:p.startswith('unimate_uniml3d_f60_v3/') and
            (p.endswith(('config.json','dataset_stats.npy','checkpoint_step_150000.pt'))),
        'kehong/MoCapAnythingV2-weights':lambda p:p.endswith(('.json','.pt','.pth','.safetensors','.yaml','.yml')),
        'google/flan-t5-base':lambda p:p in ('config.json','model.safetensors','tokenizer_config.json',
            'special_tokens_map.json','spiece.model','tokenizer.json'),
        'facebook/dinov2-large':lambda p:p in ('config.json','model.safetensors','preprocessor_config.json'),
        'briaai/RMBG-1.4':lambda p:p in ('config.json','model.pth')}
    previous={e['repo']:e for e in json.loads((args.root/'model-inventory.json').read_text())} \
        if (args.root/'model-inventory.json').exists() else {}
    inventory=list(previous.values())
    def save_inventory():
        target=args.root/'model-inventory.json'
        temporary=target.with_suffix('.json.tmp')
        temporary.write_text(json.dumps(inventory,indent=2))
        temporary.replace(target)
    for repo,choose in catalogue.items():
        pinned=previous.get(repo,{}).get('revision')
        metadata=request_json('https://huggingface.co/api/models/'+repo+('/revision/'+pinned if pinned else ''))
        revision=metadata['sha']
        selected=[s['rfilename'] for s in metadata.get('siblings',[]) if choose(s['rfilename'])]
        entry={'repo':repo,'revision':revision,'files':selected,
               'license':metadata.get('cardData',{}).get('license'),
               'downloads':dict(previous.get(repo,{}).get('downloads',{}))}
        inventory[:]=[e for e in inventory if e['repo']!=repo]
        inventory.append(entry)
        print(repo,revision,selected,flush=True)
        save_inventory()
        if not args.download:
            continue
        for rel in selected:
            target=args.root/'models'/repo.replace('/','--')/rel
            target.parent.mkdir(parents=True,exist_ok=True)
            part=target.with_suffix(target.suffix+'.part')
            if not target.exists():
                url='https://huggingface.co/'+repo+'/resolve/'+revision+'/'+rel
                offset=part.stat().st_size if part.exists() else 0
                req=urllib.request.Request(url,headers={'User-Agent':'WorldGen-local-motion-evaluation/1.0',
                    **({'Range':f'bytes={offset}-'} if offset else {})})
                print('DOWNLOAD',repo,rel,'resume',offset,flush=True)
                with urllib.request.urlopen(req,timeout=120) as response:
                    append=offset>0 and response.status==206
                    with part.open('ab' if append else 'wb') as output:
                        while True:
                            chunk=response.read(4*1024*1024)
                            if not chunk:
                                break
                            output.write(chunk)
                part.rename(target)
            digest=hashlib.sha256()
            with target.open('rb') as source:
                for chunk in iter(lambda:source.read(4*1024*1024),b''):
                    digest.update(chunk)
            expected=previous.get(repo,{}).get('downloads',{}).get(rel,{}).get('sha256')
            if expected and expected!=digest.hexdigest():
                raise RuntimeError('Existing pinned model hash changed: '+str(target))
            entry['downloads'][rel]={'bytes':target.stat().st_size,'sha256':digest.hexdigest()}
            save_inventory()
    print('Inventory/download complete. No inference started.',flush=True)


if __name__=='__main__':
    main()
