"""Pin CC0 natural fracture/erosion scans for existing rock material slots.

The scans supply surface structure; generated geology supplies mean rock colour.
This is a game material library, not a claim of measured regional lithology.
"""
import hashlib
import json
from pathlib import Path
import urllib.request

root = Path(__file__).resolve().parents[1]
base = root / 'assets-source/terrain/surfaces/cc0-rock'
materials = []
def read(url):
    request=urllib.request.Request(url, headers={'User-Agent':'WorldGen offline free asset authoring github.com/killerfrog17-coder/WorldGen'})
    return urllib.request.urlopen(request,timeout=90).read()

for name, asset, layer in [('stratified','cliff_side',10),('carbonate-joints','marble_cliff_01',11),
                           ('weathered-fine','sandstone_cracks',12),('metamorphic','dark_rock',13)]:
    folder=base/asset;folder.mkdir(parents=True,exist_ok=True)
    catalog=json.loads(read('https://api.polyhaven.com/files/'+asset));info=json.loads(read('https://api.polyhaven.com/info/'+asset))
    record=dict(name=name,asset=asset,layer=layer,source='https://polyhaven.com/a/'+asset,
                license='CC0-1.0',license_url='https://polyhaven.com/license',dimensions_mm=info.get('dimensions'),files={})
    for channel,field in [('albedo','Diffuse'),('normal','nor_gl'),('mask','arm')]:
        source=catalog[field]['2k']['jpg'];path=folder/(channel+'.jpg')
        data=path.read_bytes() if path.exists() else read(source['url'])
        if hashlib.md5(data).hexdigest()!=source['md5']:raise ValueError('Source integrity mismatch '+str(path))
        path.write_bytes(data)
        record['files'][channel]=dict(path=path.relative_to(root).as_posix(),url=source['url'],sha256=hashlib.sha256(data).hexdigest(),bytes=len(data))
        print(asset,channel,len(data),flush=True)
    materials.append(record)
(base/'sources.json').write_text(json.dumps(materials,indent=2)+'\n')
(root/'assets/verification/rock-surfaces-20261104.json').write_text(json.dumps(dict(materials=materials,
    publication='Replace existing slots 10..13 in the seventeen-layer BC7 terrain arrays. Original granite, basalt, soils and litter slots retained.',
    scope='Natural scanned jointing/erosion patterns, recoloured by generated geology; not empirical field lithology.',
    budget='Same array dimensions, layers, draw passes, maximum sampled materials and GPU bytes.'),indent=2)+'\n')
