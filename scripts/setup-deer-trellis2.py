"""Download only pinned native TRELLIS.2 512 inference weights, no inference.

Stores the two large shape/texture flows on E:, other models in artifacts.
Uses the public timm publisher's DINOv3 release, retaining its model license.
No gated model fallback, paid service or shared Python changes.
"""
import concurrent.futures
import hashlib
import json
from pathlib import Path
import time
import urllib.request

ROOT=Path(__file__).resolve().parent.parent
OUT=ROOT/'artifacts/deer-trellis2-20261023'
WEIGHTS=ROOT/'artifacts/tools/trellis2-models'
FLOWS=Path('E:/AI/WorldGen3D/trellis2-flows')
REV='af44b45f2e35a493886929c6d786e563ec68364d'
DINO_REV='30c1109559f65dea34316b0d4842d35c5771fe11'
SPARSE_REV='25e0d31ffbebe4b5a97464dd851910efc3002d96'

def api(repo,rev):
    return json.load(urllib.request.urlopen('https://huggingface.co/api/models/'+repo+'/revision/'+rev+'?blobs=true'))

def fetch(repo,rev,name,dest,expected=None):
    dest=Path(dest);dest.parent.mkdir(parents=True,exist_ok=True)
    part=dest.with_suffix(dest.suffix+'.part')
    if dest.exists() and (expected is None or dest.stat().st_size==expected):return dest
    url=f'https://huggingface.co/{repo}/resolve/{rev}/{name}'
    for attempt in range(4):
        try:
            # Restart only this owned incomplete download; never alter old models.
            with urllib.request.urlopen(url,timeout=90) as response,part.open('wb') as f:
                while chunk:=response.read(4*1024*1024):f.write(chunk)
            if expected is not None and part.stat().st_size!=expected:raise ValueError('Incomplete '+name)
            part.replace(dest);print('Downloaded',name,dest.stat().st_size,flush=True);return dest
        except Exception:
            if attempt==3:raise
            time.sleep(2)

def main():
    OUT.mkdir(parents=True,exist_ok=True);WEIGHTS.mkdir(parents=True,exist_ok=True)
    fetch('microsoft/TRELLIS.2-4B',REV,'pipeline.json',OUT/'original-pipeline.json')
    original=json.loads((OUT/'original-pipeline.json').read_text());cfg=json.loads(json.dumps(original))
    info=api('microsoft/TRELLIS.2-4B',REV);sizes={x['rfilename']:x.get('size') for x in info['siblings']}
    sparse=api('microsoft/TRELLIS-image-large',SPARSE_REV);sparse_rev=SPARSE_REV;sparse_sizes={x['rfilename']:x.get('size') for x in sparse['siblings']}
    jobs=[];models={};records=[]
    for key,stem in original['args']['models'].items():
        if key.endswith('_1024'):continue
        if key=='sparse_structure_decoder':
            repo='microsoft/TRELLIS-image-large';rev=sparse_rev;stem='ckpts/ss_dec_conv3d_16l8_fp16';lengths=sparse_sizes
        else:repo='microsoft/TRELLIS.2-4B';rev=REV;lengths=sizes
        folder=FLOWS/key if key in ('shape_slat_flow_model_512','tex_slat_flow_model_512') else WEIGHTS/key
        target=folder/'model';models[key]=str(target)
        for ext in ('.json','.safetensors'):
            filename=stem+ext;dest=target.with_suffix(ext);jobs.append((repo,rev,filename,dest,lengths.get(filename)))
    cfg['args']['models']=models;cfg['args']['default_pipeline_type']='512';cfg['args']['low_vram']=True
    cfg['args']['image_cond_model']={'name':'OwnedTimmDinoV3','args':{'model_path':str(WEIGHTS/'dinov3/model.safetensors')}}
    dino=api('timm/vit_large_patch16_dinov3.lvd1689m',DINO_REV);dsize={x['rfilename']:x.get('size') for x in dino['siblings']}
    for filename in ('model.safetensors','config.json','LICENSE.md','README.md'):
        jobs.append(('timm/vit_large_patch16_dinov3.lvd1689m',DINO_REV,filename,WEIGHTS/'dinov3'/filename,dsize.get(filename)))
    # The weight repository does not carry a file named LICENSE. Preserve the
    # pinned source checkout's MIT text separately from the model card.
    (WEIGHTS/'LICENSE-TRELLIS2').write_bytes(Path('E:/AI/WorldGen3D/trellis2-source/LICENSE').read_bytes())
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        for path in pool.map(lambda job:fetch(*job),jobs):
            records.append({'path':str(path),'bytes':path.stat().st_size,
                'sha256':hashlib.file_digest(path.open('rb'),'sha256').hexdigest()})
    (WEIGHTS/'pipeline.json').write_text(json.dumps(cfg,indent=2))
    (OUT/'weight-provenance.json').write_text(json.dumps({'trellis_revision':REV,'sparse_revision':sparse_rev,
        'dino_publisher':'timm/vit_large_patch16_dinov3.lvd1689m','dino_revision':DINO_REV,
        'models':records,'gpu_inference':False,'paid_api':False},indent=2))
    print('Pinned 512 model set ready.',flush=True)

if __name__=='__main__':main()
