"""PRIVATE RESEARCH ONLY: evaluate AnyTop learned deer motion.

AnyTop code is MIT; released checkpoints/data have separate non-commercial
research-only terms. These outputs are NOT admitted to the game or a shipping
asset bank. This trial establishes feasibility/quality, not reusable rights.
"""
import argparse
from argparse import Namespace
import json
from pathlib import Path
import re
import sys
import time


def main():
    ap=argparse.ArgumentParser()
    ap.add_argument('--repo',type=Path,required=True)
    ap.add_argument('--weights',type=Path,required=True)
    ap.add_argument('--t5',type=Path,required=True)
    ap.add_argument('--output',type=Path,required=True)
    ap.add_argument('--seed',type=int,default=731926)
    ap.add_argument('--takes',type=int,default=3)
    ap.add_argument('--frames',type=int,default=120)
    args=ap.parse_args()
    import numpy as np
    import torch
    from transformers import T5EncoderModel,AutoTokenizer
    sys.path.insert(0,str(args.repo.resolve()))
    from utils.model_util import create_model_and_diffusion_general_skeleton,load_model
    from data_loaders.tensors import truebones_batch_collate
    torch.set_num_threads(4)
    torch.set_grad_enabled(False)
    args.output.mkdir(parents=True,exist_ok=True)
    ckpt=args.weights/'checkpoints/quadropeds_model_dataset_truebones_bs_16_latentdim_128'
    config=Namespace(**json.loads((ckpt/'args.json').read_text()))
    dataset=np.load(args.weights/'dataset/truebones/zoo/truebones_processed/cond.npy',allow_pickle=True).item()['Deer']
    names=dataset['joints_names']
    def readable(s):
        for prefix in ['BN_Bip01','Bip01','BN','NPC','jt','Sabrecat','Elk']:
            if s.startswith(prefix):
                s=s[len(prefix):]
        parts=[]
        for part in re.split('(?=[A-Z]|_)',s):
            part=re.sub(r'[\d_]+','',part)
            if part in ['L','l']: part='Left'
            elif part in ['R','r']: part='Right'
            elif len(part)<=1: continue
            parts.append(part)
        return ' '.join(parts)
    print('Encoding anatomical joint names',flush=True)
    tokenizer=AutoTokenizer.from_pretrained(args.t5)
    encoder=T5EncoderModel.from_pretrained(args.t5).eval()
    inputs=tokenizer([readable(n) for n in names],return_tensors='pt',padding=True)
    mask=inputs['attention_mask']
    with torch.inference_mode():
        embeddings=(encoder(**inputs).last_hidden_state*mask.unsqueeze(-1)).sum(-2)/mask.sum(-1).clamp_min(1).unsqueeze(-1)
    del encoder
    frames=args.frames
    mean,std=dataset['mean'],dataset['std']
    rest=np.nan_to_num((dataset['tpos_first_frame']-mean)/(std+1e-6))
    temporal=torch.zeros(frames+1,frames+1)
    temporal[:,0]=1
    for i in range(frames+1):
        temporal[i,max(0,i-15):min(frames+1,i+17)]=1
    batch=[np.zeros((frames,len(names),13)),frames,dataset['parents'],rest,dataset['offsets'],temporal,
        dataset['joints_graph_dist'],dataset['joint_relations'],'Deer',embeddings.numpy(),0,mean,std,len(names)]
    _,kwargs=truebones_batch_collate([batch])
    kwargs['y']={k:v.cuda() if isinstance(v,torch.Tensor) else v for k,v in kwargs['y'].items()}
    model,diffusion=create_model_and_diffusion_general_skeleton(config)
    state=torch.load(ckpt/'model000189999.pt',map_location='cpu',weights_only=True)
    load_model(model,state)
    # Upstream overrides _apply/train without returning self, so do not chain.
    model.cuda()
    model.eval()
    timings=[]
    for take in range(args.takes):
        target=args.output/f'take-{take}.npz'
        if target.exists():
            raise FileExistsError(target)
        torch.manual_seed(args.seed+take)
        started=time.perf_counter()
        print('Generating learned motion take',take,flush=True)
        with torch.inference_mode():
            sample=diffusion.p_sample_loop(model,(1,len(names),13,frames),clip_denoised=False,
                model_kwargs=kwargs,skip_timesteps=0,init_image=None,progress=True,
                dump_steps=None,noise=None,const_noise=False)
        features=sample[0].cpu().permute(2,0,1).numpy()*std[None]+mean[None]
        # Recover the documented root-relative position representation. The 6D
        # rotation stores orthonormal matrix columns; world uses its inverse.
        x=features[:,0,3:6]
        y=features[:,0,6:9]
        x=x/np.maximum(np.linalg.norm(x,axis=-1,keepdims=True),1e-8)
        z=np.cross(x,y)
        z=z/np.maximum(np.linalg.norm(z,axis=-1,keepdims=True),1e-8)
        y=np.cross(z,x)
        rotations=np.stack([x,y,z],axis=-1)
        inverse=rotations.transpose(0,2,1)
        velocity=np.zeros((frames,3))
        velocity[1:,0]=features[:-1,0,9]
        velocity[1:,2]=features[:-1,0,11]
        root=np.cumsum(np.einsum('fij,fj->fi',inverse,velocity),axis=0)
        root[:,1]=features[:,0,1]
        points=np.einsum('fij,fkj->fki',inverse,features[:,1:,:3])
        points[:,:,0]+=root[:,None,0]
        points[:,:,2]+=root[:,None,2]
        positions=np.concatenate([root[:,None],points],axis=1)
        np.savez_compressed(target,positions=positions,features=features,parents=dataset['parents'],
            rest=dataset['tpos_first_frame'][:,:3],names=np.array(names),fps=20)
        timings.append(round(time.perf_counter()-started,2))
        print('Saved research-only motion',take,timings[-1],'seconds',flush=True)
    (args.output/'RESEARCH-ONLY.json').write_text(json.dumps({
        'method':'AnyTop learned motion diffusion, Deer conditioning',
        'terms':'Non-commercial research only; not approved for game redistribution',
        'weights_license':str(args.weights.resolve()/'LICENSE'),'seconds':timings,
        'action_control':'Unconditional given skeleton; takes are not labelled walk/run until inspected',
        'seed':args.seed,'frames':frames},indent=2))


if __name__=='__main__':
    main()
