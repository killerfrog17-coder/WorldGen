"""Isolated native TRELLIS.2 deer shape+material generation, no game edits.

Original pretrained 512 shape/texture models. Public timm DINOv3 publisher;
native Torch SDPA adapter and MIT Torch UV bake, no restricted rasterizer.
Latents/voxel materials are saved before export so export retries need no reroll.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import sys
import time
import types


def main():
    ap=argparse.ArgumentParser();ap.add_argument('--source',type=Path,required=True)
    ap.add_argument('--root',type=Path,required=True);ap.add_argument('--repo',type=Path,required=True)
    ap.add_argument('--weights',type=Path,required=True);ap.add_argument('--seed',type=int,default=732023)
    ap.add_argument('--infer',action='store_true');ap.add_argument('--resume-decode',action='store_true')
    a=ap.parse_args();root=a.root.resolve();root.mkdir(parents=True,exist_ok=True)
    os.environ.update(ATTN_BACKEND='sdpa',SPARSE_ATTN_BACKEND='flash_attn',
        TRELLIS_UV_RASTERIZER='torch',TRELLIS_NO_REMBG='1',TRELLIS_MAX_RESOLUTION='512',
        TRELLIS_KEEP_CPU_WEIGHTS='0',
        HF_HUB_OFFLINE='1',TRANSFORMERS_OFFLINE='1',OPENCV_IO_ENABLE_OPENEXR='1',
        PYTORCH_CUDA_ALLOC_CONF='garbage_collection_threshold:0.65')
    sys.path.insert(0,str(a.repo.resolve()));sys.path.insert(0,str(a.repo.resolve()/'o-voxel'))
    import torch
    import torch.nn.functional as F
    import numpy as np
    import psutil
    from PIL import Image
    from safetensors.torch import load
    import safetensors.torch as safe_torch
    # safetensors 0.8's mapped-storage path crashes in torch_cpu on this Windows
    # runtime for the >2 GiB flow checkpoints. Deserialize owned bytes instead.
    def owned_load_file(filename,device='cpu',**kwargs):
        tensors=load(Path(filename).read_bytes())
        return {k:v.to(device) for k,v in tensors.items()}
    safe_torch.load_file=owned_load_file
    torch.set_num_threads(4);torch.set_grad_enabled(False)
    # Sparse attention's public API adapter. Computes each sequence with native
    # fused SDPA instead of requiring a separate flash-attn binary extension.
    flash=types.ModuleType('flash_attn')
    import importlib.machinery
    flash.__spec__=importlib.machinery.ModuleSpec('flash_attn',loader=None)
    def varlen(q,k,v,cq,ck,*args,**kwargs):
        out=[];qq=cq.cpu().tolist();kk=ck.cpu().tolist()
        for i in range(len(qq)-1):
            x=F.scaled_dot_product_attention(q[qq[i]:qq[i+1]].transpose(0,1).unsqueeze(0),
                k[kk[i]:kk[i+1]].transpose(0,1).unsqueeze(0),v[kk[i]:kk[i+1]].transpose(0,1).unsqueeze(0))
            out.append(x[0].transpose(0,1))
        return torch.cat(out)
    flash.flash_attn_varlen_func=varlen
    flash.flash_attn_varlen_qkvpacked_func=lambda qkv,cq,*args,**kw:varlen(*qkv.unbind(1),cq,cq)
    flash.flash_attn_varlen_kvpacked_func=lambda q,kv,cq,ck,*args,**kw:varlen(q,*kv.unbind(1),cq,ck)
    sys.modules['flash_attn']=flash
    import timm
    from trellis2.modules import image_feature_extractor
    class OwnedTimmDinoV3:
        def __init__(self,model_path,image_size=512):
            self.model=timm.create_model('vit_large_patch16_dinov3',pretrained=False,num_classes=0)
            state=load(Path(model_path).read_bytes());self.model.load_state_dict(state,strict=True);del state
            # TRELLIS uses layer-normalized pre-final-norm tokens, not the
            # classifier/global pooled embedding. Preserve all register tokens.
            self.model.norm=torch.nn.Identity();self.model.eval();self.image_size=image_size
            rope=getattr(self.model,'rope',None)
            if rope is not None and hasattr(rope,'periods'):
                rope.periods=rope.periods.to(torch.bfloat16).float()
            self.mean=torch.tensor([.485,.456,.406]).view(1,3,1,1)
            self.std=torch.tensor([.229,.224,.225]).view(1,3,1,1)
        def to(self,device):
            self.model.to(device);self.mean=self.mean.to(device);self.std=self.std.to(device)
        def cpu(self):self.to('cpu')
        def __call__(self,images):
            if isinstance(images,list):
                arr=np.stack([np.asarray(im.convert('RGB').resize((self.image_size,self.image_size),Image.Resampling.LANCZOS),np.float32)/255 for im in images])
                images=torch.from_numpy(arr).permute(0,3,1,2).to(self.mean.device)
            x=((images-self.mean)/self.std).float()
            tokens=self.model.forward_features(x)
            result=F.layer_norm(tokens,tokens.shape[-1:])
            assert torch.isfinite(result).all(),'Non-finite DINO conditioning'
            print('DINO conditioning:',tuple(result.shape),'range',result.min().item(),result.max().item(),flush=True)
            return result
    image_feature_extractor.OwnedTimmDinoV3=OwnedTimmDinoV3
    # Local-only checkpoint loader: do not try remote paths as a fallback.
    from trellis2 import models
    from trellis2.pipelines.base import Pipeline
    class DeferredFlow(torch.nn.Module):
        """Only one large diffusion model resident at a time, including on CPU."""
        def __init__(self,stem):
            super().__init__();self.stem=stem
            self.config=json.loads(Path(stem+'.json').read_text())['args']
            object.__setattr__(self,'inner',None)
        def materialize(self):
            if self.inner is None:
                print('Stage-load:',self.stem,flush=True)
                m=models.from_pretrained(self.stem)
                object.__setattr__(self,'inner',m.eval())
            return self.inner
        def __getattr__(self,name):
            try:return super().__getattr__(name)
            except AttributeError:
                if name in self.config:return self.config[name]
                return getattr(self.materialize(),name)
        def to(self,*args,**kw):self.materialize().to(*args,**kw);return self
        def cpu(self):
            import gc
            object.__setattr__(self,'inner',None);gc.collect();torch.cuda.empty_cache();return self
        def forward(self,*args,**kw):
            y=self.materialize()(*args,**kw);feats=y if isinstance(y,torch.Tensor) else y.feats
            assert torch.isfinite(feats).all(),'Non-finite flow output: '+self.stem
            return y
    def local_pipeline(path):
        cfg=json.loads((Path(path)/'pipeline.json').read_text())['args'];loaded={}
        for name,stem in cfg['models'].items():
            print('Loading native model:',name,flush=True)
            loaded[name]=DeferredFlow(stem) if 'flow_model' in name else models.from_pretrained(stem)
        p=Pipeline(loaded);p._pretrained_args=cfg;return p
    Pipeline.from_pretrained=staticmethod(local_pipeline)
    # Installed ABI-matched CUDA kernels; restricted NVIDIA preview renderer is
    # never imported. The fork's postprocess uses its MIT Torch UV rasterizer.
    import cumesh,flex_gemm
    from pipeline_worker import _apply_patches
    _apply_patches()
    # Use the fork's Python exporter with the ABI-matched wheel's native kernel.
    # Extend the package search path before executing its initializer.
    import importlib.util
    package_root=a.repo.resolve()/'o-voxel'/'o_voxel'
    native_roots=[str(Path(p)/'o_voxel') for p in sys.path
                  if (Path(p)/'o_voxel'/'_C.cp311-win_amd64.pyd').is_file()]
    spec=importlib.util.spec_from_file_location('o_voxel',package_root/'__init__.py',
        submodule_search_locations=[str(package_root),*native_roots])
    voxel_package=importlib.util.module_from_spec(spec);sys.modules['o_voxel']=voxel_package
    spec.loader.exec_module(voxel_package)
    from trellis2.pipelines import Trellis2ImageTo3DPipeline
    # RTX 4060 supports native BF16. Retain the pretrained flow precision;
    # FP16 conversion can overflow even though it uses the same memory.
    Trellis2ImageTo3DPipeline._convert_bf16_to_fp16_if_needed=lambda self:None
    from trellis2.modules.sparse import SparseTensor
    import o_voxel
    info=dict(torch=torch.__version__,cuda=torch.version.cuda,gpu=torch.cuda.get_device_name(0),
        source=str(a.source.resolve()),source_sha256=hashlib.sha256(a.source.read_bytes()).hexdigest(),
        seed=a.seed,pipeline='512',native_shape_and_texture=True,reference_projection=False,
        texture_size=2048,planned_triangles=14000,background_removal=False,
        image_encoder='public timm DINOv3 ViT-L/16, pre-final-norm tokens',
        decoder='native TRELLIS.2',attention='Torch SDPA',uv_rasterizer='MIT Torch',
        flow_precision='bfloat16',image_encoder_precision='float32',structure_decoder_precision='float32',
        cpu_flow_residency='one stage at a time',
        paid_service=False,game_launch=False)
    (root/'preflight.json').write_text(json.dumps(info,indent=2))
    if not a.infer:
        print('Native imports ready; no generation requested.',flush=True);return
    for p in psutil.process_iter(['name']):
        if (p.info['name'] or '').lower() in ('theforest.exe','sonsoftheforest.exe','worldgen.exe'):
            raise RuntimeError('Owner game is running; do not start inference')
    free,total=torch.cuda.mem_get_info()
    if free<5.5*2**30:raise RuntimeError('Not enough free GPU memory for the first pass')
    begin=time.perf_counter();torch.cuda.reset_peak_memory_stats()
    print('Generating on',info['gpu'],'free MiB',round(free/2**20),flush=True)
    pipe=Trellis2ImageTo3DPipeline.from_pretrained(str(a.weights.resolve()));pipe.cuda()
    pipe.models['sparse_structure_decoder'].convert_to_fp32()
    def structure_probe(module,inputs,output):
        assert torch.isfinite(output).all(),'Non-finite structure decode'
        print('Structure occupancy:',int((output>0).sum()),'range',output.min().item(),output.max().item(),flush=True)
    pipe.models['sparse_structure_decoder'].register_forward_hook(structure_probe)
    info['load_seconds']=time.perf_counter()-begin
    original=Image.open(a.source).convert('RGBA')
    if original.getchannel('A').getextrema()[0]==255:raise ValueError('Reference must have true alpha')
    # Native preprocessing crops/pads the reference; it never loads RMBG here.
    prepared=pipe.preprocess_image(original);prepared.save(root/'reference-prepared.png')
    sample_start=time.perf_counter()
    latent_path=root/'native-latents.pt'
    if a.resume_decode:
        data=torch.load(latent_path,map_location='cpu',weights_only=True)
        shape=SparseTensor(data['shape_feats'].cuda(),data['shape_coords'].cuda())
        tex=SparseTensor(data['tex_feats'].cuda(),data['tex_coords'].cuda());resolution=data['resolution']
    else:
        shape,tex,resolution=pipe.run(prepared,seed=a.seed,preprocess_image=False,
            pipeline_type='512',return_before_decode=True,
            sparse_structure_sampler_params={'steps':12},shape_slat_sampler_params={'steps':12},tex_slat_sampler_params={'steps':12})
        torch.save(dict(shape_feats=shape.feats.cpu(),shape_coords=shape.coords.cpu(),
            tex_feats=tex.feats.cpu(),tex_coords=tex.coords.cpu(),resolution=resolution),latent_path)
    info['sample_seconds']=time.perf_counter()-sample_start
    assert len(shape.coords)>0 and len(tex.coords)>0
    assert torch.isfinite(shape.feats).all() and torch.isfinite(tex.feats).all()
    (root/'stage-timings.json').write_text(json.dumps(info,indent=2))
    print('Native shape AND material latents saved.',flush=True)
    decode_start=time.perf_counter();mesh=pipe.decode_and_cleanup(shape,tex,resolution)[0]
    info['decode_seconds']=time.perf_counter()-decode_start
    torch.save(dict(vertices=mesh.vertices.cpu(),faces=mesh.faces.cpu(),attrs=mesh.attrs.cpu(),
        coords=mesh.coords.cpu(),voxel_size=mesh.voxel_size),root/'native-mesh-materials.pt')
    np.savez_compressed(root/'native-shape.npz',vertices=mesh.vertices.cpu().numpy(),faces=mesh.faces.cpu().numpy())
    info['native_vertices']=len(mesh.vertices);info['native_triangles']=len(mesh.faces)
    assert torch.isfinite(mesh.vertices).all() and torch.isfinite(mesh.attrs).all()
    assert len(mesh.faces)>0 and mesh.faces.min()>=0 and mesh.faces.max()<len(mesh.vertices)
    (root/'stage-timings.json').write_text(json.dumps(info,indent=2))
    print('Native mesh/material field saved:',info['native_triangles'],'triangles',flush=True)
    export_start=time.perf_counter()
    result=o_voxel.postprocess.to_glb(vertices=mesh.vertices,faces=mesh.faces,
        attr_volume=mesh.attrs,coords=mesh.coords,attr_layout=mesh.layout,
        voxel_size=mesh.voxel_size,aabb=[[-.5,-.5,-.5],[.5,.5,.5]],
        decimation_target=14000,texture_size=2048,remesh=False,verbose=True,use_tqdm=True)
    result.export(root/'Deer-TRELLIS2-native.glb')
    info.update(export_seconds=time.perf_counter()-export_start,total_seconds=time.perf_counter()-begin,
        peak_torch_mib=torch.cuda.max_memory_allocated()/2**20,
        output_sha256=hashlib.sha256((root/'Deer-TRELLIS2-native.glb').read_bytes()).hexdigest())
    (root/'generation.json').write_text(json.dumps(info,indent=2));print('Native deer generation/export complete.',flush=True)


if __name__=='__main__':main()
