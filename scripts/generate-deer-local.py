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
    ap.add_argument('--shared-gpu', action='store_true',help='Owner-authorized game/inference coexistence trial')
    ap.add_argument('--resident-gpu',action='store_true',help='Fast loading: keep the pipeline on GPU without CPU offload hooks')
    args = ap.parse_args()
    if args.shared_gpu and args.resident_gpu:raise ValueError('Resident and sequential-offload modes are mutually exclusive')
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
    import safetensors.torch as safe_torch
    # Newer safetensors' mapped-storage slicing can access-violate on this
    # Windows installation. Use owned bytes as in the validated deer painters.
    def owned_load_file(filename,device='cpu',**kwargs):
        # Avoid both Rust/PyO3 deserialization and mapped torch storage slicing.
        # Read the documented tensor offsets into owned NumPy allocations.
        types={'F64':'<f8','F32':'<f4','F16':'<f2','BF16':'<u2','I64':'<i8','I32':'<i4',
               'I16':'<i2','I8':'i1','U8':'u1','BOOL':'?'}
        tensors={}
        with Path(filename).open('rb') as stream:
            header_size=int.from_bytes(stream.read(8),'little')
            if header_size>100*1024*1024:raise ValueError('Invalid tensor header')
            header=json.loads(stream.read(header_size));base=8+header_size
            for key,item in header.items():
                if key=='__metadata__':continue
                first,last=item['data_offsets'];dtype=np.dtype(types[item['dtype']])
                elements=int(np.prod(item['shape']))
                if last-first!=elements*dtype.itemsize:raise ValueError('Invalid tensor extent')
                stream.seek(base+first);array=np.fromfile(stream,dtype=dtype,count=elements)
                if array.size!=elements:raise ValueError('Truncated tensor data')
                # These inference components are explicitly requested in FP16.
                # Cast each tensor before retaining it: the 5.36 GiB FP32 flow
                # otherwise coexists with its half copy and exhausts commit.
                if item['dtype']=='F32':array=array.astype(np.float16)
                value=torch.from_numpy(array.reshape(item['shape']))
                if item['dtype']=='BF16':value=value.view(torch.bfloat16)
                tensors[key]=value.to(device)
        return tensors
    safe_torch.load_file=owned_load_file
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
    if args.shared_gpu:
        # WDDM's CUDA free figure can exceed the device-wide free VRAM while
        # another process is active. Use the conservative device-wide value.
        measured=subprocess.check_output(['nvidia-smi','--query-gpu=memory.free','--format=csv,noheader,nounits'],text=True)
        free=min(free,int(measured.strip().splitlines()[0])*2**20)
    print('Free VRAM MiB:', round(free/2**20), flush=True)
    minimum=(1.5 if args.shared_gpu else 5.8)*2**30
    if free < minimum:
        raise RuntimeError(f'Please free GPU memory before inference (need >={minimum/2**30:.1f} GiB free)')
    telemetry=[];stop_monitor=None
    if args.shared_gpu:
        import threading
        stop_monitor=threading.Event()
        # Bound our allocator; never change the game, driver or GPU clocks.
        torch.cuda.set_per_process_memory_fraction(min(.85,(free-512*2**20)/total))
        def monitor():
            while not stop_monitor.is_set():
                try:
                    row=subprocess.check_output(['nvidia-smi','--query-gpu=memory.used,memory.free,temperature.gpu,utilization.gpu',
                        '--format=csv,noheader,nounits'],text=True,creationflags=subprocess.CREATE_NO_WINDOW).strip()
                    telemetry.append(dict(elapsed=round(time.perf_counter()-start,2),values=row))
                    args.output.with_suffix('.gpu.json').write_text(json.dumps(telemetry,indent=2))
                except (OSError,subprocess.SubprocessError):pass
                stop_monitor.wait(2)
        threading.Thread(target=monitor,daemon=True).start()
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
        if args.resident_gpu:pipe.to('cuda')
        elif args.shared_gpu:pipe.enable_sequential_cpu_offload(gpu_id=0)
        else:pipe.enable_model_cpu_offload(gpu_id=0)
        original_decode = pipe.vae._decode
        def small_decode(*a, **kw):
            kw['num_chunks'] = args.chunk
            return original_decode(*a, **kw)
        pipe.vae._decode = small_decode
        def progress(pipeline, step, timestep, values):
            if telemetry and int(telemetry[-1]['values'].split(',')[2])>=85:
                raise RuntimeError('Shared-GPU trial paused at sampling boundary: GPU temperature >=85C')
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
    if stop_monitor:stop_monitor.set()
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
        'shared_gpu_trial': args.shared_gpu,
        'offload': 'none (resident GPU)' if args.resident_gpu and not args.latents else ('sequential CPU' if args.shared_gpu and not args.latents else 'model CPU'),
        'mesh_sha256': sha256(args.output),
        'note': 'Shape only. No rig, animation or generated texture is claimed here.'
    }
    args.output.with_suffix('.json').write_text(json.dumps(provenance,indent=2))
    print(json.dumps(provenance,indent=2), flush=True)


if __name__ == '__main__':
    main()
