"""Copy built-in imagegen conditioning references into the owned batch folder."""
import argparse,hashlib,json,re,shutil
from pathlib import Path
ap=argparse.ArgumentParser();ap.add_argument('--manifest',type=Path,required=True);ap.add_argument('--root',type=Path,required=True)
a=ap.parse_args();root=a.root.resolve();index=root/'reference-index.json';rows=json.loads(index.read_text()) if index.exists() else []
known={x['slug']:x for x in rows}
for item in json.loads(a.manifest.read_text()):
    assert re.fullmatch('[a-z0-9-]+',item['slug']);src=Path(item['source']).resolve()
    assert src.is_relative_to(Path.home()/'.codex/generated_images') and src.suffix=='.png'
    dest=root/(item['slug']+'.png');digest=hashlib.sha256(src.read_bytes()).hexdigest()
    if dest.exists():assert hashlib.sha256(dest.read_bytes()).hexdigest()==digest
    else:shutil.copy2(src,dest)
    known[item['slug']]={**item,'sha256':digest,'project_path':str(dest),'role':'shape conditioning only, not a painted game texture'}
    print('Reference ready',item['slug'])
index.write_text(json.dumps(list(known.values()),indent=2))
