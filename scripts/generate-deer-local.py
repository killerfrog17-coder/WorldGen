"""Local TripoSG shape study. No API calls, downloaded animal mesh or game edits.

Run using the isolated WorldGen3D environment, not ComfyUI's live environment.
TripoSG's regular hierarchical decoder uses scikit-image, so the optional CUDA
diso/Flash-DMC extension is deliberately unavailable on this Windows route.
"""
import argparse
import hashlib
import json
import subprocess
import sys
import time
import types
from pathlib import Path


def sha256(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--source', type=Path, required=True)
    ap.add_argument('--repo', type=Path, required=True)
    ap.add_argument('--weights', type=Path, required=True)
    ap.add_argument('--output', type=Path, required=True)
    ap.add_argument('--seed', type=int, default=731923)
    ap.add_argument('--steps', type=int, default=50)
    ap.add_argument('--depth', type=int, default=8)
    ap.add_argument('--chunk', type=int, default=4096)
    ap.add_argument('--latents', type=Path)
    ap.add_argument('--infer', action='store_true', help='Explicitly allow local GPU inference')
    args = ap.parse_args()
    if args.output.exists():
        raise FileExistsError(args.output)
    if not args.source.is_file() or not (args.weights/'model_index.json').is_file():
        raise FileNotFoundError('Reference image or complete local weights missing')
    sys.path.insert(0, str(args.repo.resolve()))
    # This is NOT a replacement DMC implementation: fail if the flash route is
    # accidentally used. Hierarchical extraction never calls this extension.
    optional_diso = types.ModuleType('diso')
    def no_flash(*a, **kw):
        raise RuntimeError('Use the regular hierarchical scikit-image decoder')
    optional_diso.DiffDMC = no_flash
    sys.modules['diso'] = optional_diso
    import torch
    import numpy as np
    import trimesh
    from PIL import Image
    from triposg.pipelines.pipeline_triposg import TripoSGPipeline
    from triposg.inference_utils import hierarchical_extract_geometry
    from triposg.models.autoencoders.autoencoder_kl_triposg import TripoSGVAEModel

    print('Imports ready; local pipeline', flush=True)
    if not args.infer:
        print('Preflight only. No CUDA context or inference requested.', flush=True)
        return
    args.output.parent.mkdir(parents=True, exist_ok=True)
    start = time.perf_counter()
    torch.set_num_threads(4)
    torch.set_grad_enabled(False)
    print('GPU:', torch.cuda.get_device_name(0), flush=True)
    free, total = torch.cuda.mem_get_info()
    print('Free VRAM MiB:', round(free/2**20), flush=True)
    if free < 5.8*2**30:
        raise RuntimeError('Please free the GPU before inference (need >=5.8 GiB free)')
    image = Image.open(args.source).convert('RGBA')
    alpha = image.getchannel('A')
    bounds = alpha.point(lambda v: 255 if v > 8 else 0).getbbox()
    if bounds is None:
        raise ValueError('Reference alpha is empty')
    cropped = image.crop(bounds)
    side = int(max(cropped.size)*1.2)
    canvas = Image.new('RGBA', (side, side), (255,255,255,255))
    canvas.alpha_composite(cropped, ((side-cropped.width)//2, (side-cropped.height)//2))
    prepared = canvas.convert('RGB')
    prepared.save(args.output.parent/'reference-prepared.png')
    latent_path = args.output.with_suffix('.latents.pt')
    if args.latents:
        latents = torch.load(args.latents, map_location='cpu', weights_only=True)
        vae = TripoSGVAEModel.from_pretrained(args.weights/'vae', torch_dtype=torch.float16)
        vae.to('cuda')
        latents = latents.to('cuda', dtype=torch.float16)
        # Smaller chunks bound peak decoder memory on an 8GB card.
        def geometric(points):
            return vae.decode(latents, sampled_points=points, num_chunks=args.chunk).sample
        output = hierarchical_extract_geometry(geometric, 'cuda',
            bounds=(-1.005,-1.005,-1.005,1.005,1.005,1.005),
            dense_octree_depth=6, hierarchical_octree_depth=args.depth)[0]
    else:
        TripoSGPipeline.model_cpu_offload_seq = 'image_encoder_dinov2->transformer->vae'
        pipe = TripoSGPipeline.from_pretrained(str(args.weights), torch_dtype=torch.float16,
                                             local_files_only=True)
        pipe.enable_model_cpu_offload(gpu_id=0)
        original_decode = pipe.vae._decode
        def small_decode(*a, **kw):
            kw['num_chunks'] = args.chunk
            return original_decode(*a, **kw)
        pipe.vae._decode = small_decode
        def progress(pipeline, step, timestep, values):
            if step == args.steps-1:
                torch.save(values['latents'].detach().cpu(), latent_path)
                print('Latents saved; extracting surface', flush=True)
            return values
        with torch.inference_mode():
            mesh = pipe(image=prepared, num_inference_steps=args.steps,
                guidance_scale=7.0, generator=torch.Generator(device='cuda').manual_seed(args.seed),
                use_flash_decoder=False, dense_octree_depth=6,
                hierarchical_octree_depth=args.depth, callback_on_step_end=progress).samples[0]
        # The upstream pipeline computes meshes but returns its original
        # (vertices, faces) samples, not those temporary trimesh objects.
        output = (mesh.vertices, mesh.faces) if hasattr(mesh, 'vertices') else mesh
    if output[0] is None:
        raise RuntimeError('Surface extraction failed; preserved latents permit decoder-only retry')
    mesh = trimesh.Trimesh(np.asarray(output[0],dtype=np.float32), output[1], process=True)
    if not np.isfinite(mesh.vertices).all() or len(mesh.faces) == 0:
        raise ValueError('Invalid generated surface')
    mesh.export(args.output)
    provenance = {
        'method': 'Local TripoSG image-to-shape, regular hierarchical decoder',
        'reference': str(args.source.resolve()), 'reference_sha256': sha256(args.source),
        'reference_origin': 'New imagegen image, no downloaded deer asset',
        'repo_revision': subprocess.check_output(['git','-C',str(args.repo),'rev-parse','HEAD'],text=True).strip(),
        'weights': str(args.weights), 'seed': args.seed, 'steps': args.steps,
        'depth': args.depth, 'decoder_chunk': args.chunk,
        'vertices': len(mesh.vertices), 'triangles': len(mesh.faces),
        'bounds': mesh.bounds.tolist(), 'watertight': bool(mesh.is_watertight),
        'seconds': round(time.perf_counter()-start,2),
        'timing_scope': 'decoder-only recovery' if args.latents else 'model load, denoise and extraction',
        'recovered_latents': str(args.latents.resolve()) if args.latents else None,
        'gpu_peak_mib': round(torch.cuda.max_memory_allocated()/2**20,1),
        'mesh_sha256': sha256(args.output),
        'note': 'Shape only. No rig, animation or generated texture is claimed here.'
    }
    args.output.with_suffix('.json').write_text(json.dumps(provenance,indent=2))
    print(json.dumps(provenance,indent=2), flush=True)


if __name__ == '__main__':
    main()
