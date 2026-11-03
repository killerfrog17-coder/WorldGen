"""Verify requested surface replacements without changing the terrain budget."""
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT=Path(__file__).resolve().parents[1]
BASELINE='living-npcs-foundation-20261103'
report={'baseline':BASELINE,'expected_changed_slices':[1,2,3,6],'arrays':{}}
for name in ['albedo','normal','mask']:
    relative='unity/WorldGen.Unity/Assets/WorldGen/Resources/TerrainSurfaceArtV1/'+name+'.asset'
    before=subprocess.check_output(['git','show',BASELINE+':'+relative],cwd=ROOT).decode()
    after=(ROOT/relative).read_text()
    def payload(text):return bytes.fromhex(re.search(r'_typelessdata: ([0-9a-fA-F]+)',text).group(1))
    a,b=payload(before),payload(after)
    assert len(a)==len(b) and len(a)%14==0,'Texture storage budget changed'
    size=len(a)//14
    changed=[i for i in range(14) if a[i*size:(i+1)*size]!=b[i*size:(i+1)*size]]
    assert changed==[1,2,3,6],(name,changed)
    report['arrays'][name]={'payload_bytes':len(b),'changed_slices':changed,'original_other_slices_byte_exact':True,
        'payload_sha256':hashlib.sha256(b).hexdigest(),'file_sha256':hashlib.sha256((ROOT/relative).read_bytes()).hexdigest()}
out=ROOT/'assets/verification/landscape-surface-budget-20261103.json'
out.write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8');print(out)
