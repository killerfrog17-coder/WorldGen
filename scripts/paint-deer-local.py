"""Local AI multiview texture synthesis on the existing deer geometry.

Uses the upstream Hunyuan3D-2GP painting network and Tencent paint weights.
Geometry control rendering/UV baking are separate CPU stages. No single-photo
projection fallback, game launch, paid endpoint or shared-server modification.
"""
import argparse
import json
import sys
import time
from pathlib import Path


def main():
    ap=argparse.ArgumentParser()
    ap.add_argument('--repo',type=Path,required=True)
    ap.add_argument('--weights',type=Path,required=True)
    ap.add_argument('--reference',type=Path,required=True)
    ap.add_argument('--output',type=Path,required=True)
    ap.add_argument('--steps',type=int,default=30)
    ap.add_argument('--seed',type=int,default=731924)
    args=ap.parse_args()
    if (args.output/'paint-0.png').exists():
        raise FileExistsError('Use a fresh output for a new synthesis')
    import torch
    from PIL import Image
    from diffusers import AutoencoderKL,UNet2DConditionModel,EulerAncestralDiscreteScheduler
    from transformers import CLIPTextModel,CLIPTokenizer,CLIPImageProcessor
    from safetensors.torch import load_file
    sys.path.insert(0,str(args.repo.resolve()/'hy3dgen/texgen'))
    from hunyuanpaint.pipeline import HunyuanPaintPipeline
    from hunyuanpaint.unet.modules import UNet2p5DConditionModel
    torch.set_num_threads(4)
    torch.set_grad_enabled(False)
    torch.manual_seed(args.seed)
    started=time.perf_counter()
    print('Loading local painting model',flush=True)
    with torch.device('meta'):
        base=UNet2DConditionModel.from_config(json.loads((args.weights/'unet/config.json').read_text()))
        unet=UNet2p5DConditionModel(base)
    state=load_file(str(args.weights/'unet/diffusion_pytorch_model.safetensors'))
    unet.load_state_dict(state,strict=True,assign=True)
    del state
    unet=unet.to(dtype=torch.float16)
    pipeline=HunyuanPaintPipeline(
        vae=AutoencoderKL.from_pretrained(args.weights/'vae',torch_dtype=torch.float16),
        text_encoder=CLIPTextModel.from_pretrained(args.weights/'text_encoder',torch_dtype=torch.float16),
        tokenizer=CLIPTokenizer.from_pretrained(args.weights/'tokenizer'),
        unet=unet,
        scheduler=EulerAncestralDiscreteScheduler.from_pretrained(args.weights/'scheduler',timestep_spacing='trailing'),
        feature_extractor=CLIPImageProcessor.from_pretrained(args.weights/'feature_extractor'))
    pipeline.enable_model_cpu_offload(gpu_id=0)
    pipeline.vae.enable_slicing()
    pipeline.set_progress_bar_config(disable=False)
    image=Image.open(args.reference).convert('RGBA')
    bounds=image.getchannel('A').point(lambda x:255 if x>8 else 0).getbbox()
    image=image.crop(bounds)
    side=int(max(image.size)*1.2)
    padded=Image.new('RGBA',(side,side),(255,255,255,0))
    padded.alpha_composite(image,((side-image.width)//2,(side-image.height)//2))
    normal=[Image.open(args.output/f'normal-{i}.png').convert('RGB') for i in range(6)]
    position=[Image.open(args.output/f'position-{i}.png').convert('RGB') for i in range(6)]
    def progress(pipe,step,timestep,values):
        if step%5==0 or step==args.steps-1:
            print(f'Painting step {step+1}/{args.steps}; peak {torch.cuda.max_memory_allocated()/2**20:.0f} MiB',flush=True)
        return values
    print('Generating six mutually conditioned views',flush=True)
    with torch.inference_mode():
        result=pipeline(padded.resize((512,512)),width=512,height=512,
            num_inference_steps=args.steps,
            generator=torch.Generator(device='cuda').manual_seed(args.seed),
            num_in_batch=6,camera_info_gen=[[12,15,18,21,40,38]],camera_info_ref=[[0]],
            normal_imgs=[normal],position_imgs=[position],callback_on_step_end=progress).images
    for i,image in enumerate(result):
        image.save(args.output/f'paint-{i}.png')
    provenance={'method':'Local Hunyuan3D-2 multiview paint, CPU geometry controls and UV bake',
        'seed':args.seed,'steps':args.steps,'views':len(result),'seconds':round(time.perf_counter()-started,2),
        'peak_gpu_mib':round(torch.cuda.max_memory_allocated()/2**20,1),
        'weights':str(args.weights.resolve()),'reference':str(args.reference.resolve()),
        'delighting':'Not run; source uses soft neutral studio lighting',
        'single_photo_projection':False}
    (args.output/'paint-provenance.json').write_text(json.dumps(provenance,indent=2))
    print(json.dumps(provenance),flush=True)


if __name__=='__main__':
    main()
