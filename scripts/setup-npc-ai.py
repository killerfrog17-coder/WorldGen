"""Install the pinned, free local NPC runtime into a shared per-user cache.

The two game build slots share these files. No model weights, executables,
credentials or generated saves belong in artifacts or the Git repository.
"""
from pathlib import Path
import hashlib,json,os,urllib.request,zipfile

ROOT=Path(__file__).resolve().parents[1]
manifest=json.loads((ROOT/'assets/npc-ai/runtime.json').read_text())
cache=Path(os.environ['LOCALAPPDATA'])/'WorldGen/NpcAI'
cache.mkdir(parents=True,exist_ok=True)
def download(record,path):
    if path.exists() and hashlib.sha256(path.read_bytes()).hexdigest()==record['sha256']:
        print('Verified existing '+path.name,flush=True);return
    partial=path.with_suffix(path.suffix+'.partial')
    request=urllib.request.Request(record['url'],headers={'User-Agent':'WorldGen-NPC-prototype'})
    sha=hashlib.sha256();count=0
    with urllib.request.urlopen(request,timeout=90) as response,partial.open('wb') as output:
        while True:
            block=response.read(1024*1024)
            if not block:break
            output.write(block);sha.update(block);count+=len(block)
            if count%(64*1024*1024)<len(block):print(f'{path.name}: {count//(1024*1024)} MiB',flush=True)
    if count!=record['bytes'] or sha.hexdigest()!=record['sha256']:raise RuntimeError('Download checksum/size mismatch: '+path.name)
    partial.replace(path);print('Downloaded and verified '+path.name,flush=True)

runtime=manifest['runtime'];archive=cache/(runtime['version']+'-cpu.zip')
target=cache/('llama-'+runtime['version'])
if not (target/'llama-server.exe').exists():
    download(runtime,archive);target.mkdir(exist_ok=True)
    with zipfile.ZipFile(archive) as package:
        for member in package.infolist():
            resolved=(target/member.filename).resolve()
            if not resolved.is_relative_to(target.resolve()):raise RuntimeError('Unsafe archive path')
        package.extractall(target)
    servers=list(target.rglob('llama-server.exe'))
    if len(servers)!=1:raise RuntimeError('Expected one local server executable')
    server=servers[0]
else:server=target/'llama-server.exe'
gpu=manifest['gpu_runtime'];gpu_archive=cache/(gpu['version']+'-vulkan.zip');gpu_target=cache/('llama-'+gpu['version']+'-vulkan')
if not (gpu_target/'llama-server.exe').exists():
    download(gpu,gpu_archive);gpu_target.mkdir(exist_ok=True)
    with zipfile.ZipFile(gpu_archive) as package:
        for member in package.infolist():
            if not (gpu_target/member.filename).resolve().is_relative_to(gpu_target.resolve()):raise RuntimeError('Unsafe archive path')
        package.extractall(gpu_target)
model=cache/manifest['model']['file'];download(manifest['model'],model)
license_path=target/'WORLDGEN-RUNTIME-LICENSE.txt'
if not license_path.exists():
    license_path.write_bytes(urllib.request.urlopen(runtime['license_url']).read())
config={'ServerPath':str(server),'ModelPath':str(model),'ModelName':manifest['model']['name'],'GpuServerPath':str(gpu_target/'llama-server.exe'),'Threads':2,'ContextTokens':2048,'MaxTokens':160,'BaseUrl':'','ApiKeyEnvironment':'','Enabled':True}
config_path=cache/'settings.json'
if config_path.exists():
    existing=json.loads(config_path.read_text());existing.setdefault('GpuServerPath',config['GpuServerPath']);config=existing
config_path.write_text(json.dumps(config,indent=2)+'\n')
(cache/'provenance.json').write_text(json.dumps(manifest,indent=2)+'\n')
print('NPC model ready: '+str(config_path),flush=True)
