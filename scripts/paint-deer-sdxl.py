"""Local geometry-guided SDXL colour views; OpenRAIL++ models, no paid service.

Uses the existing isolated Python environment. Does not touch ComfyUI or old
Hunyuan images. Output is AI colour, not a generated complete PBR material.
"""
import argparse
import hashlib
import json
from pathlib import Path
import time


def main():
    ap=argparse.ArgumentParser()
    ap.add_argument('--models',type=Path,required=True)
    ap.add_argument('--root',type=Path,required=True)
    ap.add_argument('--appearance-root',type=Path,help='Optional locally downloaded RealVisXL UNet; same SDXL encoders/VAE')
    ap.add_argument('--fast-vae',type=Path,help='Optional local madebyollin FP16-safe decoder; GPU decode instead of CPU')
    ap.add_argument('--views',default='all')
    ap.add_argument('--steps',type=int,default=28)
    ap.add_argument('--strength',type=float,default=.98)
    ap.add_argument('--seed',type=int,default=731940)
    ap.add_argument('--fp8',action='store_true',help='Requires Diffusers >=0.35; FP8 weight storage, FP16 compute')
    args=ap.parse_args()
    import torch
    from PIL import Image
    from diffusers import AutoencoderKL,ControlNetModel,UNet2DConditionModel,StableDiffusionXLControlNetImg2ImgPipeline,DPMSolverMultistepScheduler
    torch.set_num_threads(4)
    torch.set_grad_enabled(False)
    specs=json.loads((args.root/'views.json').read_text())
    indices=list(range(len(specs))) if args.views=='all' else [int(i) for i in args.views.split(',')]
    indices=[i for i in indices if not (args.root/f'paint-{i}.png').exists()]
    if not indices:
        print('Requested views already exist; no reroll',flush=True)
        return
    control=ControlNetModel.from_pretrained(args.models/'depth-small',variant='fp16',
        torch_dtype=torch.float16,use_safetensors=True,local_files_only=True,disable_mmap=True)
    unet_root=args.appearance_root/'unet' if args.appearance_root else args.models/'sdxl-base/unet'
    model_name='SG161222/RealVisXL_V5.0' if args.appearance_root else 'stabilityai/stable-diffusion-xl-base-1.0'
    unet=UNet2DConditionModel.from_pretrained(unet_root,variant='fp16',
        torch_dtype=torch.float16,use_safetensors=True,local_files_only=True,disable_mmap=True)
    pipe=StableDiffusionXLControlNetImg2ImgPipeline.from_pretrained(args.models/'sdxl-base',
        unet=unet,controlnet=control,variant='fp16',torch_dtype=torch.float16,use_safetensors=True,
        local_files_only=True,add_watermarker=False)
    pipe.scheduler=DPMSolverMultistepScheduler.from_config(pipe.scheduler.config,
        algorithm_type='dpmsolver++',use_karras_sigmas=True)
    if args.fp8:
        pipe.unet.enable_layerwise_casting(storage_dtype=torch.float8_e4m3fn,
            compute_dtype=torch.float16,
            skip_modules_pattern=('norm','time_embedding','add_embedding','conv_in','conv_out'),
            skip_modules_classes=(torch.nn.LayerNorm,torch.nn.GroupNorm))
    pipe.enable_model_cpu_offload()
    # Native Torch SDPA is already memory-efficient. Manual attention slicing
    # replaces it with slower attention and can make 1K views take minutes.
    pipe.disable_attention_slicing()
    pipe.vae.enable_tiling()
    pipe.vae.enable_slicing()
    # Decode on CPU after checkpointing the latent. This avoids a full-size
    # FP32 VAE memory spike next to the UNet on an 8GB Windows GPU.
    decode_dtype=torch.float16 if args.fast_vae else torch.float32
    decode_args={} if args.fast_vae else dict(variant='fp16')
    cpu_vae=AutoencoderKL.from_pretrained(args.fast_vae or args.models/'sdxl-base/vae',
        torch_dtype=decode_dtype,local_files_only=True,use_safetensors=True,disable_mmap=True,**decode_args)
    cpu_vae.enable_tiling()
    common=('Wildlife photograph of an adult female red deer without antlers, '
        'real short tawny brown fur with fine individual hairs, cream belly and throat, '
        'dark natural eyes and nose. Photorealistic, sharp focus, soft even lighting, plain grey background. ')
    negative=('antlers, horns, stag, fawn, spots, extra limbs, extra heads, multiple animals, '
        'extra eyes, distorted eyes, mane, horse, collar, grass, scenery, text, watermark, '
        'shadow, harsh lighting, glare, oversaturated, blurry, painting, cartoon, '
        '3d render, plastic, sculpture, toy, smooth skin')
    records=[]
    record_path=args.root/'paint-provenance.json'
    if record_path.exists():
        records=json.loads(record_path.read_text())
    for i in indices:
        start=time.perf_counter()
        torch.cuda.reset_peak_memory_stats()
        prompt=common+specs[i]['description']+'.'
        if specs[i]['head']:
            prompt+=' Close up portrait, realistic small dark eyes, detailed short facial fur.'
        print('PAINT START',i,specs[i]['description'],flush=True)
        guide=Image.open(args.root/f'guide-{i}.png').convert('RGB')
        depth=Image.open(args.root/f'depth-{i}.png').convert('RGB')
        latent_path=args.root/f'latent-{i}.pt'
        request=dict(prompt=prompt,negative_prompt=negative,steps=args.steps,strength=args.strength,
            seed=args.seed+i,fp8=args.fp8,model=model_name,
            guide_sha256=hashlib.sha256((args.root/f'guide-{i}.png').read_bytes()).hexdigest(),
            depth_sha256=hashlib.sha256((args.root/f'depth-{i}.png').read_bytes()).hexdigest())
        request_path=args.root/f'latent-{i}.json'
        with torch.inference_mode():
            if latent_path.exists():
                if not request_path.exists() or json.loads(request_path.read_text())!=request:
                    raise ValueError('Saved latent request differs or lacks provenance; use a fresh output')
                latent=torch.load(latent_path,map_location='cpu',weights_only=True)
                print('Resuming saved latent',i,flush=True)
            else:
                latent=pipe(prompt=prompt,negative_prompt=negative,image=guide,control_image=depth,
                    num_inference_steps=args.steps,strength=args.strength,guidance_scale=7,
                    controlnet_conditioning_scale=.75,control_guidance_end=.9,output_type='latent',
                    generator=torch.Generator('cuda').manual_seed(args.seed+i)).images.to('cpu')
                torch.save(latent,latent_path)
                request_path.write_text(json.dumps(request,indent=2))
                print('LATENT SAVED',i,flush=True)
            if args.fast_vae:
                pipe.maybe_free_model_hooks()
                torch.cuda.empty_cache()
                cpu_vae.to('cuda')
            decoded=cpu_vae.decode(latent.to(device='cuda' if args.fast_vae else 'cpu',dtype=decode_dtype)/cpu_vae.config.scaling_factor,return_dict=False)[0].float().cpu()
            if args.fast_vae:
                cpu_vae.to('cpu')
                torch.cuda.empty_cache()
            pixels=(decoded[0].permute(1,2,0).numpy()/2+.5).clip(0,1)
            import numpy as np
            if not np.isfinite(pixels).all():
                raise ValueError('Non-finite image decode; saved latent retained')
            result=Image.fromarray(np.uint8(pixels*255))
        path=args.root/f'paint-{i}.png'
        result.save(path)
        records.append(dict(view=i,seed=args.seed+i,steps=args.steps,strength=args.strength,
            seconds=round(time.perf_counter()-start,2),peak_torch_mib=round(torch.cuda.max_memory_allocated()/2**20,1),
            model=model_name,
            precision='FP8 weight storage / FP16 compute' if args.fp8 else 'FP16',
            decoder_model='madebyollin/sdxl-vae-fp16-fix' if args.fast_vae else 'SDXL original FP32 CPU',
            control='diffusers/controlnet-depth-sdxl-1.0-small',
            prompt=prompt,negative_prompt=negative,sha256=hashlib.sha256(path.read_bytes()).hexdigest(),
            old_hunyuan_input=False))
        record_path.write_text(json.dumps(records,indent=2))
        print('PAINT READY',i,records[-1]['seconds'],records[-1]['peak_torch_mib'],flush=True)


if __name__=='__main__':
    main()
