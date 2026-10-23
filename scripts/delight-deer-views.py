"""Estimate albedo with local Marigold IID; preserve original high-frequency fur.

This reduces baked lighting; intrinsic decomposition is an estimate, not ground
truth. Saves raw estimates and lighting as diagnostics instead of discarding
source photos. All inputs are new SDXL views, never Hunyuan output.
"""
import argparse
import hashlib
import json
from pathlib import Path
import time
import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter


def linear_to_srgb(v):
    return np.where(v<=.0031308,v*12.92,1.055*np.maximum(v,0)**(1/2.4)-.055)


def main():
    ap=argparse.ArgumentParser()
    ap.add_argument('--model',type=Path,required=True)
    ap.add_argument('--root',type=Path,required=True)
    ap.add_argument('--views',default='all')
    ap.add_argument('--cpu-preview',action='store_true',help='Separate 512px/2-step CPU diagnostic; never used by final bake')
    args=ap.parse_args()
    import torch
    from safetensors.torch import load as load_bytes
    from transformers import CLIPTextModel,CLIPTextConfig
    from diffusers import MarigoldIntrinsicsPipeline,UNet2DConditionModel,AutoencoderKL
    torch.set_num_threads(2 if args.cpu_preview else 4)
    torch.set_grad_enabled(False)
    dtype=torch.float32 if args.cpu_preview else torch.float16
    unet=UNet2DConditionModel.from_pretrained(args.model/'unet',variant='fp16',
        torch_dtype=dtype,local_files_only=True,disable_mmap=True)
    vae=AutoencoderKL.from_pretrained(args.model/'vae',variant='fp16',
        torch_dtype=dtype,local_files_only=True,disable_mmap=True)
    # Avoid the installed Torch/Transformers Windows mapped-storage conversion
    # path: it hard-crashes when expanding this FP16 encoder to CPU FP32.
    with torch.device('meta'):
        encoder=CLIPTextModel(CLIPTextConfig.from_json_file(args.model/'text_encoder/config.json'))
    state=load_bytes((args.model/'text_encoder/model.fp16.safetensors').read_bytes())
    encoder.load_state_dict(state,strict=True,assign=True)
    del state
    encoder.text_model.embeddings.register_buffer('position_ids',
        torch.arange(encoder.config.max_position_embeddings).expand(1,-1),persistent=False)
    if any(buffer.is_meta for buffer in encoder.buffers()):
        raise ValueError('Unexpected unmaterialized encoder buffer')
    encoder=encoder.to(dtype=dtype)
    pipe=MarigoldIntrinsicsPipeline.from_pretrained(args.model,unet=unet,vae=vae,text_encoder=encoder,variant='fp16',
        torch_dtype=dtype,local_files_only=True)
    if not args.cpu_preview:
        pipe.enable_model_cpu_offload()
    pipe.vae.enable_tiling()
    pipe.vae.enable_slicing()
    indices=list(range(len(json.loads((args.root/'views.json').read_text())))) if args.views=='all' else [int(i) for i in args.views.split(',')]
    records=[]
    report_path=args.root/('delight-preview-provenance.json' if args.cpu_preview else 'delight-provenance.json')
    if report_path.exists():
        records=json.loads(report_path.read_text())
    for i in indices:
        tag='preview' if args.cpu_preview else 'view'
        path=args.root/f'albedo-{tag}-{i}.png'
        if path.exists():
            continue
        source=args.root/f'paint-{i}.png'
        start=time.perf_counter()
        if not args.cpu_preview:
            torch.cuda.reset_peak_memory_stats()
        photo=Image.open(source).convert('RGB')
        print('DELIGHT START',i,flush=True)
        result=pipe(image=photo,num_inference_steps=2 if args.cpu_preview else 4,ensemble_size=1,
            processing_resolution=512 if args.cpu_preview else 768,match_input_resolution=True,
            generator=torch.Generator('cpu' if args.cpu_preview else 'cuda').manual_seed(732000+i))
        if not np.isfinite(result.prediction).all():
            raise ValueError('Non-finite intrinsic prediction; no output published')
        albedo=linear_to_srgb(result.prediction[0]).clip(0,1)
        raw=Image.fromarray(np.uint8(albedo*255))
        raw.save(args.root/f'albedo-estimate-{tag}-{i}.png')
        shading=linear_to_srgb(result.prediction[1]).clip(0,1)
        Image.fromarray(np.uint8(shading*255)).save(args.root/f'lighting-estimate-{tag}-{i}.png')
        # Transfer broad albedo correction only, retaining source 1K fur detail.
        original=np.asarray(photo,np.float32)/255
        low_original=gaussian_filter(original,(4,4,0))
        low_estimate=gaussian_filter(albedo,(4,4,0))
        luma=np.array([.2126,.7152,.0722],np.float32)
        ratio=(low_estimate@luma+.02)/(low_original@luma+.02)
        brown=(original[:,:,0]>original[:,:,1]*1.06)&(original[:,:,1]>original[:,:,2]*1.08)&(original.mean(2)>.15)
        if brown.any():
            ratio/=max(float(np.median(ratio[brown])),.1)
        ratio=np.clip(ratio,.6,1.8)
        # Keep the generated coat's chromaticity/median exposure instead of
        # letting an intrinsic model trained on interiors recolour brown fur.
        corrected=np.clip(original*(.25+.75*ratio[:,:,None]),0,1)
        Image.fromarray(np.uint8(corrected*255)).save(path)
        records.append(dict(view=i,model='prs-eth/marigold-iid-lighting-v1-1',steps=2 if args.cpu_preview else 4,seed=732000+i,
            cpu_preview=args.cpu_preview,
            seconds=round(time.perf_counter()-start,2),peak_torch_mib=0 if args.cpu_preview else round(torch.cuda.max_memory_allocated()/2**20,1),
            source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),
            output_sha256=hashlib.sha256(path.read_bytes()).hexdigest(),
            transfer='Low-frequency luminance-only correction, median brown exposure/chroma retained; source fine fur retained'))
        report_path.write_text(json.dumps(records,indent=2))
        print('DELIGHT READY',i,records[-1]['seconds'],flush=True)


if __name__=='__main__':
    main()
