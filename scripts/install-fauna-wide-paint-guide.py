"""Persist a viewed generated paint source and its explicit calibration."""
import argparse,hashlib,json,shutil
from pathlib import Path
repo=Path(__file__).resolve().parent.parent
ap=argparse.ArgumentParser();ap.add_argument('--slug',required=True);ap.add_argument('--view',choices=['front','right'],required=True);ap.add_argument('--guide-pixels',nargs='+',type=float,required=True);ap.add_argument('--only-wide',action='store_true');ap.add_argument('--camera-directory',default='head-paint-source');a=ap.parse_args();root=repo/'artifacts/fauna-anatomy-revision'/a.slug
meta_path=root/f'wide-{a.view}-paint-source.json';meta=json.loads(meta_path.read_text());source=Path(meta['generated_source']);assert meta['actually_visually_inspected'];dest=root/f'wide-{a.view}-paint-guide.png';shutil.copy2(source,dest)
target_image=root/a.camera_directory/f'{a.view}-clay.png';assert target_image.resolve().is_relative_to(root.resolve());meta.update(sha256=hashlib.sha256(dest.read_bytes()).hexdigest(),edit_target=str(target_image.relative_to(repo)),edit_target_sha256=hashlib.sha256(target_image.read_bytes()).hexdigest());meta_path.write_text(json.dumps(meta,indent=2)+'\n')
points=[a.guide_pixels[i:i+2] for i in range(0,len(a.guide_pixels),2)];expected=3 if a.view=='front' else 2;assert len(points)==expected
corners=[[0,0],[512,0],[0,512],[512,512]];guide_corners=[[0,0],[1254,0],[0,1254],[1254,1254]]
path=root/'face-paint-views.json';views=json.loads(path.read_text()) if path.exists() else []
if a.only_wide:views=[v for v in views if v.get('camera_directory')==a.camera_directory]
views=[v for v in views if not(v.get('camera_directory')==a.camera_directory and v['camera'] in ([a.view] if a.view=='front' else ['right','left']))]
config=dict(camera=a.view,image=dest.name,camera_directory=a.camera_directory,target_pixels=[[0,0]]*expected+corners,guide_pixels=points+guide_corners,two_sided=True,fade_border=.12,weight=1.,reject_neutral_exterior=True)
views.append(config)
if a.view=='right':
 other=json.loads(json.dumps(config));other.update(camera='left',flip_x=True);other['guide_pixels']=[[1253-x,y] for x,y in points]+guide_corners;views.append(other)
path.write_text(json.dumps(views,indent=2)+'\n')
if a.view=='front' and not (root/'front-paint-guide.png').exists():shutil.copy2(dest,root/'front-paint-guide.png')
print('INSTALLED',a.slug,a.view,meta['sha256'])
