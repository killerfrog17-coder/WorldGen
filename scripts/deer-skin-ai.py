"""Run local SkinTokens rig generation on our mesh, with native Torch SDPA.

The published default requires 14GB. This study uses one-beam sampling and
export-independent array output to try an 8GB card. No generated rig is claimed
until the prediction, skin transfer and Blender inspection actually succeed.
"""
import argparse
import importlib.machinery
import json
import os
from pathlib import Path
import sys
import time
import types


def main():
    ap=argparse.ArgumentParser()
    ap.add_argument('--repo',type=Path,required=True)
    ap.add_argument('--weights',type=Path,required=True)
    ap.add_argument('--mesh',type=Path,required=True)
    ap.add_argument('--output',type=Path,required=True)
    ap.add_argument('--preflight',action='store_true')
    ap.add_argument('--beams',type=int,default=1)
    ap.add_argument('--seed',type=int,default=731925)
    args=ap.parse_args()
    args.repo,args.weights,args.mesh,args.output=[p.resolve() for p in [args.repo,args.weights,args.mesh,args.output]]
    import torch
    import torch.nn.functional as F
    import numpy as np
    import trimesh
    # Equivalent non-causal attention through PyTorch's supported CUDA kernel;
    # no compiled FlashAttention wheel or system CUDA Toolkit needed.
    attention=types.ModuleType('flash_attn_interface')
    attention.__spec__=importlib.machinery.ModuleSpec('flash_attn_interface',loader=None)
    def native_attention(q,k,v,*a,**kw):
        out=F.scaled_dot_product_attention(q.transpose(1,2),k.transpose(1,2),v.transpose(1,2))
        return out.transpose(1,2),None
    attention.flash_attn_func=native_attention
    sys.modules['flash_attn_interface']=attention
    from transformers import AutoModelForCausalLM
    original=AutoModelForCausalLM.from_config
    def from_config(*a,**kw):
        kw['attn_implementation']='sdpa'
        return original(*a,**kw)
    AutoModelForCausalLM.from_config=from_config
    sys.path.insert(0,str(args.repo))
    from src.model.tokenrig import TokenRig
    print('SkinTokens imports ready',flush=True)
    if args.preflight:
        return
    if args.output.exists():
        raise FileExistsError(args.output)
    args.output.parent.mkdir(parents=True,exist_ok=True)
    os.chdir(args.weights)
    torch.set_num_threads(4)
    torch.manual_seed(args.seed)
    torch.set_grad_enabled(False)
    started=time.perf_counter()
    model=TokenRig.load_from_system_checkpoint('experiments/articulation_xl_quantization_256_token_4/grpo_1400.ckpt').eval().to('cuda')
    print('Model loaded. Generating skeleton and skin.',flush=True)
    mesh=trimesh.load(args.mesh,force='mesh')
    # Match upstream's Blender glTF importer: GLB Y-up -> Blender Z-up. Feeding
    # raw GLB coordinates directly produces a sideways conditioning mesh.
    converted=mesh.vertices[:,[0,2,1]].copy()
    converted[:,1]*=-1
    mesh=trimesh.Trimesh(converted,mesh.faces,process=False)
    centre=(mesh.vertices.min(0)+mesh.vertices.max(0))/2
    scale=2/(mesh.vertices.max(0)-mesh.vertices.min(0)).max()
    normalized=trimesh.Trimesh((mesh.vertices-centre)*scale,mesh.faces,process=False)
    # Match upstream 54,000-point mixed surface/vertex conditioning.
    generator=np.random.default_rng(args.seed)
    surface,face_ids=trimesh.sample.sample_surface(normalized,37616,seed=args.seed)
    chosen=generator.choice(len(normalized.vertices),16384,replace=len(normalized.vertices)<16384)
    points=np.concatenate([surface,normalized.vertices[chosen]],axis=0).astype(np.float32)
    normals=np.concatenate([normalized.face_normals[face_ids],normalized.vertex_normals[chosen]],axis=0).astype(np.float32)
    with torch.inference_mode():
        result=model.generate(torch.from_numpy(points).cuda(),torch.from_numpy(normals).cuda(),cls='articulation',
            max_length=2048,top_k=5,top_p=.95,temperature=1.0,repetition_penalty=2.0,
            num_return_sequences=1,num_beams=args.beams,do_sample=True)
    if result.detokenize_output is None or result.skin_pred is None:
        raise RuntimeError('No complete generated rig')
    joints=np.asarray(result.detokenize_output.joints)/scale+centre
    parents=np.asarray(result.detokenize_output.parents)
    skin=result.skin_pred.float().cpu().numpy()
    np.savez_compressed(args.output,joints=joints,parents=parents,sample_points=points/scale+centre,
                        sample_skin=skin,source_vertices=mesh.vertices,source_faces=mesh.faces)
    provenance={'method':'Local SkinTokens skeleton and skin generation','beams':args.beams,'seed':args.seed,'attention':'Native Torch SDPA',
        'joints':len(joints),'seconds':round(time.perf_counter()-started,2),
        'peak_gpu_mib':round(torch.cuda.max_memory_allocated()/2**20,1),'mesh':str(args.mesh),
        'coordinates':'Blender Z-up, matching upstream importer'}
    args.output.with_suffix('.json').write_text(json.dumps(provenance,indent=2))
    print(provenance,flush=True)


if __name__=='__main__':
    main()
