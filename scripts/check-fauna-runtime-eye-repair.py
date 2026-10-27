"""Require complete exports, unchanged native geometry/skin/motion and opaque solid lids."""
import hashlib,json,struct
from pathlib import Path
import numpy as np
from PIL import Image

repo=Path(__file__).resolve().parent.parent;root=repo/'artifacts/fauna-eye-closure-repair';runtime=repo/'artifacts/fauna-runtime-20261026'
baseline={r['slug']:r for r in json.loads((root/'baseline.json').read_text())['animals']}
inventory={r['slug']:r for r in json.loads((runtime/'native-inventory.json').read_text())['animals']}
signature=hashlib.sha256(b''.join((repo/'scripts'/n).read_bytes() for n in ['export-fauna-runtime.py','fauna_runtime_surface.py','fauna_runtime_uv.py'])).hexdigest()
lining_file=root/'native-lining-repairs.json'
linings={r['slug']:r for r in json.loads(lining_file.read_text())['animals']} if lining_file.exists() else {}
rows=[]
for slug,old in baseline.items():
    folder=runtime/'exports'/slug;model=json.loads((folder/'model.json').read_text());receipt=json.loads((folder/'receipt.json').read_text())
    assert receipt['complete'] and receipt['exporter_sha256']==signature,slug
    lining=linings.get(slug);expected_sha=lining['after_sha256'] if lining else old['source_sha256']
    assert model['source_sha256']==expected_sha==hashlib.sha256(Path(old['source']).read_bytes()).hexdigest(),slug
    for name,sha in receipt['files'].items():assert hashlib.sha256((folder/name).read_bytes()).hexdigest()==sha,(slug,name)
    raw=(folder/'mesh.bin').read_bytes();n,k,b,p=struct.unpack_from('<4i',raw,8);width=9+2*model['influences']+3*int(model.get('hasMorph',False))
    vertices=np.frombuffer(raw,'<f4',n*width,24).reshape(n,width);faces=np.frombuffer(raw,'<i4',k,24+n*width*4)
    original_faces=faces[:old['original_index_count']] if lining else faces
    digest=hashlib.sha256(np.delete(vertices[original_faces],[6,7],axis=1).astype('<f4').tobytes()).hexdigest()
    assert digest==old['geometry_skin_triangle_sha256'],(slug,'geometry/skin/normals changed')
    assert hashlib.sha256((folder/'motion.bin').read_bytes()).hexdigest()==old['motion_sha256'],(slug,'motion changed')
    mask=np.asarray(Image.open(folder/'mask.png'));part_ids=vertices[:,8+2*model['influences']].astype(int)
    parts=[]
    for pi,name in enumerate(model['part_names']):
        uv=vertices[part_ids==pi,6:8]
        native=next(m for m in inventory[slug]['meshes'] if m['name']==name)
        opaque=True
        for material in native['materials']:
            graph=inventory[slug]['materials'][material]
            principal=next((x for x in graph['nodes'] if x['type']=='BSDF_PRINCIPLED'),None)
            if principal is None or principal['inputs'].get('Alpha',1)<.999 or any(link[2]==principal['name'] and link[3]=='Alpha' for link in graph['links']):opaque=False
        # Sample precisely as bilinear/clamped GPU UV sampling at mip 0.
        xy=uv*np.array([mask.shape[1],mask.shape[0]])-.5;lo=np.floor(xy).astype(int);fraction=xy-lo;hi=lo+1
        lo=np.clip(lo,0,[mask.shape[1]-1,mask.shape[0]-1]);hi=np.clip(hi,0,[mask.shape[1]-1,mask.shape[0]-1])
        def sample(x,y):return mask[mask.shape[0]-1-y,x,1]/255
        alpha=sample(lo[:,0],lo[:,1])*(1-fraction[:,0])*(1-fraction[:,1])+sample(hi[:,0],lo[:,1])*fraction[:,0]*(1-fraction[:,1])+sample(lo[:,0],hi[:,1])*(1-fraction[:,0])*fraction[:,1]+sample(hi[:,0],hi[:,1])*fraction[:,0]*fraction[:,1]
        discarded=int((alpha<.35).sum())
        assert not (opaque and discarded),(slug,name,'opaque vertices discarded',discarded)
        if 'socket and eyelid' in name:
            assert len(np.unique(uv,axis=0))>3,(slug,name,'collapsed eyelid UVs')
        parts.append({'name':name,'source_opaque':opaque,'alpha_min':float(alpha.min()),'discarded_vertices':discarded,'uv_points':len(np.unique(uv,axis=0))})
    if lining:assert len(faces)==old['original_index_count']+lining['added_triangles']*3
    rows.append({'slug':slug,'source_sha256':expected_sha,'native_unchanged':not bool(lining),'original_geometry_skin_normals_exact':True,'added_socket_lining_triangles':lining['added_triangles'] if lining else 0,'motion_exact':True,'parts':parts})
    print('RUNTIME_EYE_REPAIR_CHECK',slug,flush=True)
(root/'export-repair-validation.json').write_text(json.dumps({'count':len(rows),'exporter_sha256':signature,'animals':rows,'scope':'Exact native/geometry/skin/motion identity; valid runtime lid UVs and bilinear opaque-part alpha coverage. Render inspections and bundle-load checks recorded separately.'},indent=2))
assert len(rows)==177
print('ALL_RUNTIME_EYE_REPAIRS_PASS',len(rows))
