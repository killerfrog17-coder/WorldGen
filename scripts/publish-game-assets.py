"""Publish exact asset dependencies as free, content-addressed release packs.

No LFS storage, binaries, personal saves, AI weights or build caches are uploaded.
Only one small temporary pack exists at a time. Every remote payload is downloaded
again and SHA256-verified before the lock file is committed or old builds removed.
"""
import argparse
import hashlib
import json
import subprocess
import urllib.error
import zipfile
from pathlib import Path,PurePosixPath

from game_asset_inventory import ROOT,sha
from github_asset_api import GitHubAssets


def write(path,data):
    temporary=path.with_suffix(path.suffix+'.pending')
    temporary.write_text(json.dumps(data,indent=2),encoding='utf-8')
    temporary.replace(path)


parser=argparse.ArgumentParser()
parser.add_argument('--inventory',type=Path,required=True)
parser.add_argument('--tag',required=True)
parser.add_argument('--work',type=Path,required=True)
parser.add_argument('--lock',type=Path,default=ROOT/'assets/game-assets.lock.json')
parser.add_argument('--base-lock',type=Path,action='append',default=[],help='Reuse content already preserved by another dependency manifest')
parser.add_argument('--publish',action='store_true',help='Publish the verified draft release')
args=parser.parse_args()
args.work.mkdir(parents=True,exist_ok=True)
records=json.loads(args.inventory.read_text())
records=[r for r in records if not r['tracked']]
def source_file(record):
    if 'source_path' not in record:
        return ROOT/record['path']
    if record['group']!='legacy-runtime-assets':
        raise ValueError('External source paths are restricted to the audited old game assets')
    source=Path(record['source_path']).resolve()
    data=next((p for p in source.parents if p.name=='WorldGen_Data'),None)
    if data is None or not (data.parent/'WorldGen.exe').is_file():
        raise ValueError('Historical asset is not inside a game package')
    allowed=[(ROOT/'artifacts').resolve(),Path('E:/WorldGen-builds').resolve(),Path('C:/WorldGenBuildArtifacts').resolve()]
    if data.parent in allowed or not any(data.parent.is_relative_to(p) for p in allowed):
        raise ValueError('Historical package is outside the explicitly owned build roots')
    relative=source.relative_to(data).as_posix()
    if not relative.startswith('StreamingAssets/') and not (source.parent==data and source.suffix.lower() in {'.assets','.ress','.resource','.unity3d'}):
        raise ValueError('Historical input is not runtime asset data')
    return source
for record in records:
    path=PurePosixPath(record['path'])
    if path.is_absolute() or '..' in path.parts or path.suffix.lower() in {'.exe','.dll','.pdb','.key','.pem','.pfx','.p12'} or path.name.startswith('.env'):
        raise ValueError('Unsafe dependency path')
prior=json.loads(args.lock.read_text()) if args.lock.exists() else {'files':{},'packs':{}}
if set(prior['files'])=={r['path'] for r in records} and all(prior['files'][r['path']]['sha256']==r['sha256'] for r in records):
    print('No asset changes; reusing the existing versioned packs.')
    raise SystemExit(0)
client=GitHubAssets()
head=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip()
try:
    client.request('/repos/'+client.repository+'/commits/'+head)
except urllib.error.HTTPError as error:
    if error.code==404:
        raise RuntimeError('Push the source checkpoint to GitHub before publishing its asset release.') from None
    raise
state_file=args.work/'publish-state.json'
state=json.loads(state_file.read_text()) if state_file.exists() else {}
if state.get('tag',args.tag)!=args.tag:
    raise ValueError('Work directory belongs to another asset version')
state['tag']=args.tag
if 'release' not in state:
    for release in client.request('/repos/'+client.repository+'/releases?per_page=100'):
        if release['tag_name']==args.tag:
            state['release']=release
            break
    if 'release' not in state:
        state['release']=client.request('/repos/'+client.repository+'/releases','POST',{'tag_name':args.tag,'target_commitish':head,'name':'Game asset dependencies — '+args.tag,'body':'Versioned authoring and runtime inputs for WorldGen. Exact file hashes and restoration are recorded in assets/game-assets.lock.json. No test players, build caches, AI model weights or personal saves. Content-addressed packs must be retained while referenced by Git history.','draft':True,'prerelease':False})
    write(state_file,state)
release=state['release']
existing={}
page=1
while True:
    batch=client.request('/repos/'+client.repository+'/releases/'+str(release['id'])+'/assets?per_page=100&page='+str(page))
    existing.update({r['name']:r for r in batch})
    if len(batch)<100:
        break
    page+=1
known={r['sha256']:r for r in prior['files'].values()}
available_packs=dict(prior['packs'])
for base in args.base_lock:
    saved=json.loads(base.read_text())
    known.update({r['sha256']:r for r in saved['files'].values()})
    available_packs.update(saved['packs'])
pending={}
files={}
packs={}
for record in records:
    old=known.get(record['sha256'])
    if old is not None and old['bytes']==record['bytes']:
        files[record['path']]=dict(old)
        packs[old['pack']]=available_packs[old['pack']]
    else:
        pending.setdefault(record['sha256'],record)
groups=[]
small=[]
size=0
for record in sorted(pending.values(),key=lambda r:(r['group'],r['sha256'])):
    if record['bytes']>64*1024*1024:
        if small:
            groups.append(small);small=[];size=0
        groups.append([record])
    else:
        if small and size+record['bytes']>64*1024*1024:
            groups.append(small);small=[];size=0
        small.append(record);size+=record['bytes']
if small:
    groups.append(small)
state.setdefault('verified',{})
objects={}
for index,group in enumerate(groups):
    raw=len(group)==1 and group[0]['bytes']>64*1024*1024
    identity=hashlib.sha256(('raw|' if raw else 'zip|').encode()+''.join(r['sha256'] for r in group).encode()).hexdigest()
    name='assets-'+identity+('.blob' if raw else '.zip')
    temporary=args.work/'current-pack.zip'
    path=source_file(group[0]) if raw else temporary
    print(json.dumps({'event':'pack','number':index+1,'total':len(groups),'files':len(group),'inputBytes':sum(r['bytes'] for r in group)}),flush=True)
    for record in group:
        if sha(source_file(record))!=record['sha256']:
            raise ValueError('Input changed during publication: '+record['path'])
    if not raw:
        if temporary.exists():
            temporary.unlink() # Only this tool's previous, unmanaged upload staging file.
        with zipfile.ZipFile(temporary,'w',zipfile.ZIP_DEFLATED,compresslevel=6,allowZip64=True) as archive:
            for record in group:
                info=zipfile.ZipInfo('objects/'+record['sha256'],date_time=(1980,1,1,0,0,0));info.compress_type=zipfile.ZIP_DEFLATED
                with source_file(record).open('rb') as source,archive.open(info,'w',force_zip64=True) as target:
                    for block in iter(lambda:source.read(1024*1024),b''):
                        target.write(block)
        with zipfile.ZipFile(temporary) as archive:
            for record in group:
                h=hashlib.sha256()
                with archive.open('objects/'+record['sha256']) as source:
                    for block in iter(lambda:source.read(1024*1024),b''):
                        h.update(block)
                if h.hexdigest()!=record['sha256']:
                    raise ValueError('Local archive differs from source')
    digest=sha(path)
    remote=existing.get(name)
    if remote is None:
        remote=client.upload(release,path,name);existing[name]=remote
    if remote['size']!=path.stat().st_size or remote.get('digest') not in (None,'sha256:'+digest):
        raise ValueError('An existing immutable asset pack differs: '+name)
    if state['verified'].get(name)!=digest:
        h=hashlib.sha256();received=0
        with client.request('/repos/'+client.repository+'/releases/assets/'+str(remote['id']),binary=True) as response:
            for block in iter(lambda:response.read(1024*1024),b''):
                h.update(block);received+=len(block)
        if h.hexdigest()!=digest or received!=path.stat().st_size:
            raise ValueError('Remote download verification failed: '+name)
        state['verified'][name]=digest;write(state_file,state)
    key=args.tag+'/'+name
    packs[key]={'tag':args.tag,'name':name,'asset_id':remote['id'],'sha256':digest,'bytes':remote['size'],'encoding':'raw' if raw else 'zip'}
    for record in group:
        objects[record['sha256']]={'sha256':record['sha256'],'bytes':record['bytes'],'pack':key,'member':None if raw else 'objects/'+record['sha256']}
    if not raw:
        temporary.unlink()
    print(json.dumps({'event':'remote_verified','number':index+1,'total':len(groups),'bytes':remote['size']}),flush=True)
for record in records:
    if record['path'] not in files:
        files[record['path']]=objects[record['sha256']]
    if sha(source_file(record))!=record['sha256']:
        raise ValueError('Source changed before the dependency checkpoint: '+record['path'])
manifest={'version':1,'repository':client.repository,'source_checkpoint':head,'transport':'GitHub Releases; no Git LFS or metered asset storage','files':dict(sorted(files.items())),'packs':dict(sorted(packs.items()))}
args.lock.parent.mkdir(parents=True,exist_ok=True)
write(args.lock,manifest)
if args.publish and release['draft']:
    release=client.request('/repos/'+client.repository+'/releases/'+str(release['id']),'PATCH',{'draft':False,'make_latest':'false'})
    state['release']=release;write(state_file,state)
write(args.work/'publication-proof.json',{'release':release['html_url'],'source_checkpoint':head,'files':len(files),'packs':len(packs),'all_payloads_uploaded_and_download_verified':True,'source_bytes':sum(r['bytes'] for r in records),'remote_bytes':sum(r['bytes'] for r in packs.values()),'manifest_sha256':sha(args.lock)})
print(json.dumps({'event':'complete','files':len(files),'packs':len(packs),'release':release['html_url']}),flush=True)
