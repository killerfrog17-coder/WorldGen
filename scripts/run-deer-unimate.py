"""Isolated UniMate research trial; CPU preflight or explicitly gated inference.

Official checkpoints are non-commercial and have separate provenance caveats.
Outputs stay outside the accepted/game asset bank. No shared server changes.
"""
import argparse
import importlib
import json
import os
from pathlib import Path
import sys
import time


def main():
    ap=argparse.ArgumentParser()
    ap.add_argument('--root',type=Path,required=True)
    ap.add_argument('--repo',type=Path,required=True)
    ap.add_argument('--preflight',action='store_true')
    ap.add_argument('--takes',type=int,default=12)
    ap.add_argument('--guidance',type=float,default=4)
    ap.add_argument('--seed',type=int,default=732100)
    ap.add_argument('--name',default='pilot')
    args=ap.parse_args()
    root=args.root.resolve()
    repo=args.repo.resolve()
    if args.preflight:
        os.environ['CUDA_VISIBLE_DEVICES']=''
    else:
        import psutil
        playing=[p.info['name'] for p in psutil.process_iter(['name'])
                 if 'forest' in (p.info['name'] or '').lower()]
        if playing:
            raise RuntimeError('GPU run held: The Forest is still running. Wait for owner to close it.')
    os.environ['HF_HUB_OFFLINE']='1'
    os.environ['TRANSFORMERS_OFFLINE']='1'
    os.environ['TOKENIZERS_PARALLELISM']='false'
    os.environ['OMP_NUM_THREADS']='3'
    sys.path.insert(0,str(repo))
    import numpy as np
    import torch
    from transformers import T5EncoderModel,T5Tokenizer
    torch.set_num_threads(3)
    torch.set_grad_enabled(False)
    # Resolve only this encoder to the explicitly downloaded/pinned files.
    # Keep its public name in configuration (the factory's dimensional contract).
    encoder_path=root/'models/google--flan-t5-base'
    for cls in (T5EncoderModel,T5Tokenizer):
        original=cls.from_pretrained
        def load(name,*a,_original=original,**kw):
            path=str(encoder_path) if str(name)=='google/flan-t5-base' else name
            kw['local_files_only']=True
            return _original(path,*a,**kw)
        cls.from_pretrained=load
    sample=importlib.import_module('unimate.inference.sample')
    weights=root/'models/Linzhan--UniMate/unimate_uniml3d_f60_v3'
    checkpoint=weights/'checkpoints/checkpoint_step_150000.pt'
    config=sample.MainConfig.from_json(str(weights/'config.json'))
    config.sampling.device='cpu' if args.preflight else 'cuda'
    cases=[{'asset':str(root/'unimate-rig'),'id':'walk','prompt':'An object walks forward.'},
           {'asset':str(root/'unimate-rig'),'id':'run','prompt':'An object runs forward.'},
           {'asset':str(root/'unimate-rig'),'id':'feed','prompt':'An object lowers its head and eats.'},
           {'asset':str(root/'unimate-rig'),'id':'alert','prompt':'An object stands still and looks around.'}]
    casefile=root/'unimate-cases.json'
    casefile.write_text(json.dumps(cases,indent=2))
    output=root/'unimate-samples'/args.name
    inference_args=sample.InferenceArgs(exp_dir=str(weights),asset=(str(root/'unimate-rig'),),
        test_cases_json=str(casefile),model_path=str(checkpoint),output_dir=str(output),
        num_repetitions=args.takes,batch_size=1,only_save_motion=True,cfg_scale=args.guidance,seed=args.seed)
    if args.preflight:
        dataset,cases,assets=sample._prepare_cases(inference_args,config,args.guidance,
            str(weights/'dataset_stats.npy'),torch.device('cpu'))
        model=sample.create_model(config.dataset,config.model)
        sample._load_checkpoint(model,str(checkpoint),config)
        assert not torch.cuda.is_initialized()
        report={'case_count':len(cases),'assets':list(assets),'parameters':sum(p.numel() for p in model.parameters()),
            'cuda_initialized':False,'checkpoint_loaded':True,'license':'research evaluation only',
            'ready_for_gpu':True}
        (root/'unimate-preflight.json').write_text(json.dumps(report,indent=2))
        print(json.dumps(report,indent=2),flush=True)
        return
    if output.exists():
        raise FileExistsError(output)
    # Upstream reads the run config again. Override the config loader in this
    # process only; downloaded metadata and upstream source remain unchanged.
    original_config=sample.MainConfig.from_json
    def configured(path):
        c=original_config(path)
        c.sampling.device='cuda'
        return c
    sample.MainConfig.from_json=configured
    start=time.perf_counter()
    torch.cuda.reset_peak_memory_stats()
    sample.main(inference_args)
    report={'seconds':time.perf_counter()-start,'peak_cuda_mib':torch.cuda.max_memory_allocated()/2**20,
        'seed':args.seed,'takes_per_action':args.takes,'guidance':args.guidance,'research_only':True}
    (output/'worldgen-run.json').write_text(json.dumps(report,indent=2))
    print(json.dumps(report,indent=2),flush=True)


if __name__=='__main__':
    main()
