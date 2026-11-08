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
    # Cliff faces own their stone colour; a neutral grey multiply made every
    # generated face inherit the terrain's brown dirt pigment.
    raw=ground.linear(np.asarray(ground.Image.open(master).convert('RGB'),dtype=float)/255)
    source_mean=raw.mean(axis=(0,1));c=np.clip(c*source_mean/c.mean(axis=(0,1)),.001,.98)
    h=ground.resize(c@np.array([.2126,.7152,.0722]),512);h=np.log(np.maximum(.005,h))
    broad=ground.gaussian_filter(h,2.5,mode='wrap')-ground.gaussian_filter(h,24,mode='wrap')
    fine=ground.gaussian_filter(h,.8,mode='wrap')-ground.gaussian_filter(h,2.5,mode='wrap')
    h=broad*(.018/max(.001,float(np.std(broad))))+fine*(.001/max(.001,float(np.std(fine))))
    dx=(np.roll(h,-1,1)-np.roll(h,1,1))*512/16
    dy=(np.roll(h,-1,0)-np.roll(h,1,0))*512/16
    # PNG rows run downward, while the wall chart's v increases upward.
    normal=ground.unit(ground.close_edges(ground.unit(np.stack([-dx,dy,np.ones_like(dx)],axis=2))))
    outputs=[]
    for channel,pixels in [('albedo',ground.srgb(c)),('normal',normal*.5+.5)]:
        path=OUT/f'layer-{index:02d}-{channel}.png';ground.save(path,pixels);outputs.append(ground.validate(path))
    assert np.max(np.abs(np.linalg.norm(normal,axis=2)-1))<1e-9
    row.update(master=master.relative_to(ROOT).as_posix(),master_sha256=ground.sha(master),linear_mean_colour=source_mean.tolist(),outputs=outputs)
data.update(processing='Generated stone colour retained, periodic-plus-smooth and exact opposing edges. Approximate relief:1.8cm broad plus1mm fine RMS at8m repeat, upward-v normal convention. Not measured PBR.',runtime_budget='Two BC7 layers: colour1024, normals512. Existing17-layer ground bank unchanged; no geometry change.')
manifest.write_text(json.dumps(data,indent=2)+'\n',encoding='utf-8')
print('PASS:2 cliff masters,4 seamless outputs, unit normals; ground bank untouched.')
