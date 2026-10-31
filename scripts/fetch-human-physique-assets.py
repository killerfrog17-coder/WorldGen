"""Small, isolated CC0 appearance library; reuse cached assets without copying."""
import hashlib, importlib.util, io, json, os, zipfile, subprocess
from pathlib import Path
import requests

REPO=Path(__file__).resolve().parent.parent
ROOT=REPO/'assets-source/humans/appearance-review'
DEST=ROOT/'mpfb-assets'
spec=importlib.util.spec_from_file_location('fetch_old',REPO/'scripts/fetch-human-demo-assets.py')
old=importlib.util.module_from_spec(spec);spec.loader.exec_module(old)
ROOT.mkdir(exist_ok=True)
for category in (REPO/'assets-source/humans/research/builders/mpfb-assets').iterdir():
    if not category.is_dir():continue
    for folder in category.iterdir():
        target=DEST/category.name/folder.name
        target.parent.mkdir(parents=True,exist_ok=True)
        if not target.exists():
            if folder.is_dir():subprocess.run(['cmd','/c','mklink','/J',str(target),str(folder)],check=True,capture_output=True)
            else:target.write_bytes(folder.read_bytes())
records=[]
for pack,prefixes in [('bodyparts05',['clothes/grinsegold_beard_sigmund_wip/','clothes/rehmanpolanski_beard_viking/','clothes/rehmanpolanski_moustache_viking/']),('hair01',['hair/cortu_short_messy_hair/','hair/elvs_unkempt_french_braid/']),('makehuman_system_assets',['hair/long01/','hair/braid01/'])]:
    url=f'https://files.makehumancommunity.org/asset_packs/{pack}/{pack}_cc0.zip'
    archive=zipfile.ZipFile(old.RemoteZip(url))
    names=archive.namelist()
    (ROOT/(pack+'-files.json')).write_text(json.dumps(names))
    selected=[n for n in names if any(n.startswith(p) for p in prefixes) and not n.endswith('/')]
    print(pack,'selected',len(selected),flush=True)
    for name in selected:
        target=(DEST/name).resolve()
        assert target.is_relative_to(DEST.resolve()),name
        target.parent.mkdir(parents=True,exist_ok=True)
        if not target.exists():target.write_bytes(archive.read(name))
        content=target.read_bytes()
        records.append(dict(path=name,bytes=len(content),sha256=hashlib.sha256(content).hexdigest(),source=url,license='CC0'))
        print(name,len(content),flush=True)
(ROOT/'asset-provenance.json').write_text(json.dumps(records,indent=2))
