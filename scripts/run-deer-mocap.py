"""Bounded local MoCapAnything V2 trial; no hosted services or donor mesh.

Official preprocessing/model functions, with DINO batches limited to two to fit
8 GB. Raw predictions are retained before any cleanup. Research outputs only.
"""
import argparse
import gc
import json
import os
from pathlib import Path
import subprocess
import sys
import time


def main():
    ap=argparse.ArgumentParser()
    ap.add_argument('--root',type=Path,required=True)
    ap.add_argument('--repo',type=Path,required=True)
    ap.add_argument('--reuse-features',action='store_true')
    a=ap.parse_args();root=a.root.resolve();repo=a.repo.resolve()
    import psutil
    if any('forest' in (p.info.get('name') or '').lower() for p in psutil.process_iter(['name'])):
        raise RuntimeError('GPU run held: Forest is running')
    os.environ.update(HF_HUB_OFFLINE='1',TRANSFORMERS_OFFLINE='1',OMP_NUM_THREADS='3')
    sys.path.insert(0,str(repo))
    import numpy as np
    import torch
    import yaml
    import imageio_ffmpeg
    from PIL import Image,ImageDraw
    from inference.video2pose2rot import DinoPipe,build_inference_batch
    from preprocess.briarmbg import BriaRMBG
    from preprocess.image_process import prepare_image
    from utils.config_utils import instantiate_from_config
    from utils.transforms3d import rotation_6d_to_matrix,matrix_to_quaternion
    from utils import bvh,animation
    started=time.perf_counter();torch.set_num_threads(3);torch.manual_seed(732100)
    out=root/'mocap-results';out.mkdir(exist_ok=True)
    frames=out/'source-frames';frames.mkdir(exist_ok=True)
    if len(list(frames.glob('*.png')))!=90:
        subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(),'-y','-loglevel','error','-threads','2',
            '-ss','8','-i',str(root/'input/mule-deer-CC0.webm'),'-t','3',
            '-vf','fps=30,scale=768:-1','-frames:v','90',str(frames/'%04d.png')],check=True)
    paths=sorted(frames.glob('*.png'));assert len(paths)==90,len(paths)
    ref=root/'input/mocap-reference.png';assert ref.exists()
    if a.reuse_features:
        features=np.load(out/'dino-features.npy')
    else:
        features=extract_features(root,out,ref,paths)
    zoo=root/'mocap-work/zoo';seq='WorldGenDeer#WorldGenDeer-Walk/y0'
    ref_embed=zoo/'npz_train_image_only'/f'{seq}.npz';ref_embed.parent.mkdir(parents=True,exist_ok=True)
    np.savez_compressed(ref_embed,image_embed=features[:1])
    cfg=yaml.safe_load((repo/'examples/custom_rig/inference.yaml').read_text())
    cfg['data'].update(base_dir=str(zoo),character_dir=str(zoo/'characters_face_zplus'),
        memory_pkl_path='',eval_seq_len=90)
    cfg['data']['retarget'].update(ref_seq=seq,ref_idx=0)
    batch=build_inference_batch(cfg,features[1:])
    model=instantiate_from_config(cfg['model']).float().cuda().eval()
    checkpoint=torch.load(root/'models/kehong--MoCapAnythingV2-weights/video2pos2rot_epoch60.pt',
        map_location='cpu',weights_only=False)
    sd=next((checkpoint[k] for k in ('model_state','model','model_state_dict','state_dict') if k in checkpoint),checkpoint)
    sd={(k[7:] if k.startswith('module.') else k):v for k,v in sd.items()}
    model.load_state_dict(sd,strict=True)
    del sd,checkpoint;gc.collect()
    with torch.inference_mode():
        prediction=model(batch,attention_kwargs=cfg['model']['attention_kwargs'],
            stage_cb=lambda stage:print('MoCap inference:',stage,flush=True))
    J=int(batch['J_valid'][0]);rot=prediction['pred_rot6d'][0,:,:J].cpu()
    pos=prediction['pred_position'][0,:,:J].cpu().numpy()
    np.save(out/'pred-rot6d.npy',rot.numpy());np.save(out/'pred-position.npy',pos)
    matrices=rotation_6d_to_matrix(rot)
    quats=matrix_to_quaternion(matrices).numpy()
    template,names,dt=bvh.load(str(zoo/'characters_face_zplus/WorldGenDeer/WorldGenDeer_ffs.bvh'))
    local=np.tile(template.offsets,(len(quats),1,1))
    anim=animation.Animation(animation.Quaternions(quats),local,template.orients,
        template.offsets,template.parents,template.end_site_offsets)
    fk=animation.positions_global(anim)
    rest=template.offsets.copy()
    for j,p in enumerate(template.parents):
        if p>=0:rest[j]+=rest[p]
    np.savez_compressed(out/'mocap-raw.npz',rotations=quats,positions=fk,local_positions=local,
        offsets=template.offsets,parents=template.parents,names=names,rest_positions=rest,
        predicted_positions=pos,fps=30)
    assert np.isfinite(quats).all() and np.isfinite(fk).all()
    (out/'run.json').write_text(json.dumps({'seconds':time.perf_counter()-started,
        'peak_cuda_mib':torch.cuda.max_memory_allocated()/2**20,'frames':len(quats),
        'source':'CC0 Extemporalist mule-deer foraging video, seconds 8-11',
        'source_fps':30,'reference':'same own deer, native CC0-derived Walk frame 0',
        'no_ground_truth':True,'rigid_bone_decode':True,'research_only':True,
        'cached_features':a.reuse_features},indent=2))
    print('MoCap GPU trial complete.',flush=True)


def extract_features(root,out,ref,paths):
    import numpy as np
    import torch
    from inference.video2pose2rot import DinoPipe
    from preprocess.briarmbg import BriaRMBG
    from preprocess.image_process import prepare_image
    rmbg=BriaRMBG()
    rmbg.load_state_dict(torch.load(root/'models/briaai--RMBG-1.4/model.pth',map_location='cpu',weights_only=True))
    rmbg=rmbg.cuda().eval()
    prepared=[]
    with torch.inference_mode():
        for i,p in enumerate([ref,*paths]):
            image=prepare_image(str(p),bg_color=np.array([1.,1.,1.]),rmbg_net=rmbg)
            prepared.append(image)
            if i in (0,1,31,61,90):image.save(out/f'mask-preview-{i:03}.png')
            if i%15==0:print('Segmented',i,'/',91,flush=True)
    del rmbg;gc.collect();torch.cuda.empty_cache()
    pipe=DinoPipe(torch.device('cuda'),model_id=str(root/'models/facebook--dinov2-large'))
    embeds=[]
    with torch.inference_mode():
        for i in range(0,len(prepared),2):
            x=pipe.feature_extractor_dinov2(prepared[i:i+2],return_tensors='pt').pixel_values.cuda().half()
            embeds.append(pipe.image_encoder_dinov2(x).last_hidden_state.cpu().float().numpy())
    features=np.concatenate(embeds);np.save(out/'dino-features.npy',features)
    del pipe;gc.collect();torch.cuda.empty_cache()
    return features


if __name__=='__main__':main()
