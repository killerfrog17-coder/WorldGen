"""Offline restoration checks: real archives, atomic replacement and boundaries."""
import contextlib
import hashlib
import importlib.util
import io
import json
import sys
import tempfile
import zipfile
from pathlib import Path

spec=importlib.util.spec_from_file_location('restore_game_assets',Path(__file__).with_name('restore-game-assets.py'))
restore=importlib.util.module_from_spec(spec);spec.loader.exec_module(restore)
checks=0
def check(ok,text):
    global checks
    checks+=1
    if not ok:
        raise AssertionError(text)

with tempfile.TemporaryDirectory(prefix='worldgen-asset-check-') as directory:
    root=Path(directory);destination=root/'checkout';lock=root/'lock.json'
    data={'sounds/test.wav':b'runtime bytes','artifacts/model/source.bin':b'authoring bytes'}
    buffer=io.BytesIO()
    with zipfile.ZipFile(buffer,'w') as archive:
        for payload in data.values():
            archive.writestr('objects/'+hashlib.sha256(payload).hexdigest(),payload)
    packed=buffer.getvalue()
    pack={'name':'test.zip','tag':'test','asset_id':1,'sha256':hashlib.sha256(packed).hexdigest(),'bytes':len(packed),'encoding':'zip'}
    manifest={'version':1,'repository':'killerfrog17-coder/WorldGen','packs':{'test':pack},'files':{p:{'sha256':hashlib.sha256(b).hexdigest(),'bytes':len(b),'pack':'test','member':'objects/'+hashlib.sha256(b).hexdigest()} for p,b in data.items()}}
    lock.write_text(json.dumps(manifest))
    class Fake:
        calls=0
        payload=packed
        def __init__(self,*args,**kwargs):
            pass
        def download(self,pack):
            Fake.calls+=1
            return io.BytesIO(Fake.payload)
    restore.GitHubAssets=Fake
    def run(*args):
        previous=sys.argv
        try:
            sys.argv=['restore','--lock',str(lock),'--destination',str(destination),*args]
            with contextlib.redirect_stdout(io.StringIO()):
                return restore.main()
        finally:
            sys.argv=previous
    check(run()==0 and all((destination/p).read_bytes()==b for p,b in data.items()),'Real archive restores exact runtime and authoring bytes')
    check(run()==0 and Fake.calls==1,'Unchanged assets require no download')
    (destination/'sounds/test.wav').write_bytes(b'local edit')
    try:
        run()
        raise AssertionError('Local edit was silently replaced')
    except RuntimeError:
        check((destination/'sounds/test.wav').read_bytes()==b'local edit' and Fake.calls==1,'Local edits block replacement before network I/O')
    check(run('--replace','--subset','runtime')==0 and (destination/'sounds/test.wav').read_bytes()==data['sounds/test.wav'],'Explicit runtime replacement restores only the requested subset')
    (destination/'sounds/test.wav').write_bytes(b'keep me')
    Fake.payload=b'corrupt download'
    try:
        run('--replace')
        raise AssertionError('Corrupt remote data accepted')
    except ValueError:
        check((destination/'sounds/test.wav').read_bytes()==b'keep me','Corrupt pack cannot overwrite the existing asset')
    Fake.payload=packed
    check(run('--verify-only')==1,'Verification identifies modified assets without repairing them')
    for path in ['', '.', '../escape', '/escape', 'C:/escape', 'a\\..\\escape']:
        try:
            restore.target_path(destination,path)
            raise AssertionError('Unsafe path accepted: '+path)
        except ValueError:
            check(True,'Unsafe path rejected')
    check(not list(destination.rglob('*.asset-pending')),'Failed validation leaves no partial asset replacement')
print('PASS',checks,'offline asset restoration checks')
