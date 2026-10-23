"""Download only public SDXL/control weights needed for offline deer painting.

No API key, hosted inference, shared ComfyUI edits or payment. Pin and record
repository revisions, filenames and hashes; do not download alternate precision
copies or full automatic addon bundles.
"""
import argparse
import hashlib
import json
from pathlib import Path
import time
import urllib.request


def main():
    ap=argparse.ArgumentParser()
    ap.add_argument('--root',type=Path,required=True)
    ap.add_argument('--kind',choices=['base','delight','realvis','vae'],default='base')
    args=ap.parse_args()
    from huggingface_hub import snapshot_download
    args.root.mkdir(parents=True,exist_ok=True)
    repos=[('stabilityai/stable-diffusion-xl-base-1.0','sdxl-base',[
        'model_index.json','LICENSE.md','README.md','scheduler/*.json',
        'tokenizer/*','tokenizer_2/*','text_encoder/config.json','text_encoder/model.fp16.safetensors',
        'text_encoder_2/config.json','text_encoder_2/model.fp16.safetensors',
        'unet/config.json','unet/diffusion_pytorch_model.fp16.safetensors',
        'vae/config.json','vae/diffusion_pytorch_model.fp16.safetensors']),
        ('diffusers/controlnet-depth-sdxl-1.0-small','depth-small',[
            'README.md','config.json','diffusion_pytorch_model.fp16.safetensors'])]
    if args.kind=='delight':
        repos=[('prs-eth/marigold-iid-lighting-v1-1','marigold-lighting',[
            'model_index.json','README.md','scheduler/*.json','tokenizer/*',
            'text_encoder/config.json','text_encoder/model.fp16.safetensors',
            'unet/config.json','unet/diffusion_pytorch_model.fp16.safetensors',
            'vae/config.json','vae/diffusion_pytorch_model.fp16.safetensors'])]
    elif args.kind=='realvis':
        repos=[('SG161222/RealVisXL_V5.0','realvisxl',[
            'README.md','unet/config.json','unet/diffusion_pytorch_model.fp16.safetensors'])]
    elif args.kind=='vae':
        repos=[('madebyollin/sdxl-vae-fp16-fix','vae-fp16-fix',[
            'README.md','config.json','diffusion_pytorch_model.safetensors'])]
    records=[]
    for repo,folder,patterns in repos:
        meta=json.load(urllib.request.urlopen('https://huggingface.co/api/models/'+repo))
        revision=meta['sha']
        print('Download',repo,revision,flush=True)
        start=time.perf_counter()
        path=Path(snapshot_download(repo_id=repo,revision=revision,local_dir=args.root/folder,
            allow_patterns=patterns,max_workers=3))
        files=[]
        for p in sorted(path.rglob('*')):
            if p.is_file() and '.cache' not in p.parts:
                with p.open('rb') as stream:
                    digest=hashlib.file_digest(stream,'sha256').hexdigest()
                files.append(dict(path=str(p.relative_to(path)),bytes=p.stat().st_size,sha256=digest))
        records.append(dict(repo=repo,revision=revision,path=str(path),files=files,
            seconds=round(time.perf_counter()-start,2)))
        (args.root/'download-provenance.json').write_text(json.dumps(records,indent=2))
        print('Ready',repo,flush=True)


if __name__=='__main__':
    main()
