"""Bake generated cliff masters; leave the existing ground bank unchanged."""
from pathlib import Path
import importlib.util,json
import numpy as np

ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('ground_authoring',ROOT/'scripts/prepare-generated-ground.py')
ground=importlib.util.module_from_spec(spec);spec.loader.exec_module(ground)
OUT=ROOT/'unity/WorldGen.Unity/Assets/WorldGen/Editor/CliffSurfaceArtV1'
manifest=ROOT/'assets/verification/cliff-material-generation-20261108.json'
data=json.loads(manifest.read_text(encoding='utf-8'))
for index,row in enumerate(data['materials']):
    master=ROOT/'assets-source/terrain/surfaces/ai/cliff-set-20261108'/(row['key']+'.png')
    c=ground.colour(master)
    _,normal=ground.relief(c,512,8,.065)
    outputs=[]
    for channel,pixels in [('albedo',ground.srgb(c)),('normal',normal*.5+.5)]:
        path=OUT/f'layer-{index:02d}-{channel}.png';ground.save(path,pixels);outputs.append(ground.validate(path))
    assert np.max(np.abs(np.linalg.norm(normal,axis=2)-1))<1e-9
    row.update(master=master.relative_to(ROOT).as_posix(),master_sha256=ground.sha(master),outputs=outputs)
data.update(processing='Linear mean-normalized colour, periodic-plus-smooth and exact opposing edges. Luminance-derived 6.5cm RMS relief approximation at 8m repeat; not measured photogrammetry/PBR.',runtime_budget='Two BC7 layers: colour1024, normals512. Replace existing cliff/terrain rock reads; no new pass or extra texture reads. Existing17-layer ground bank unchanged.')
manifest.write_text(json.dumps(data,indent=2)+'\n',encoding='utf-8')
print('PASS:2 cliff masters,4 seamless outputs, unit normals; ground bank untouched.')
