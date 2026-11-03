"""Reproducible CC0 tree art: photographed leaves assembled into leafy branches.

Downloads are permanent authoring inputs, not scratch. The output is editor-only;
TreeSurfaceArtBuilder publishes compressed GPU arrays and a smaller CPU bake bank.
Requires numpy, scipy, Pillow and requests. No generative service or paid assets.
"""
from pathlib import Path
import hashlib, io, json, math, random, zipfile
import numpy as np
import requests
from PIL import Image, ImageDraw, ImageFilter
from scipy import ndimage

ROOT=Path(__file__).resolve().parents[1]
SOURCE=ROOT/'assets-source/vegetation/tree-surfaces'
OUTPUT=ROOT/'unity/WorldGen.Unity/Assets/WorldGen/Editor/TreeSurfaceArtV1'
SIZE=512
LEAVES=['LeafSet005','LeafSet010','LeafSet016','LeafSet018','LeafSet019','LeafSet022','LeafSet024']
BARK=['chinese_hackberry_bark','bark_willow_02','pine_bark','palm_bark','metasequoia_bark']

def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def get(url):
    r=requests.get(url,timeout=90);r.raise_for_status();return r
def linear(a):return np.where(a<=.04045,a/12.92,((a+.055)/1.055)**2.4)
def srgb(a):return np.where(a<=.0031308,a*12.92,1.055*np.maximum(a,0)**(1/2.4)-.055)
def image(a):return Image.fromarray(np.uint8(np.clip(a,0,1)*255+.5))

def fetch():
    meta=get('https://ambientcg.com/api/v2/full_json?id='+','.join(LEAVES)+'&include=downloadData,imageData,tagData').json()
    for asset in meta['foundAssets']:
        id=asset['assetId'];folder=SOURCE/'ambientcg'/id;folder.mkdir(parents=True,exist_ok=True)
        if not list(folder.glob('*_Color.png')):
            d=next(x for x in asset['downloadFolders']['default']['downloadFiletypeCategories']['zip']['downloads'] if x['attribute']=='1K-PNG')
            data=get(d['downloadLink']).content
            for n in zipfile.ZipFile(io.BytesIO(data)).namelist():
                if n.endswith(('_Color.png','_NormalGL.png','_Opacity.png')):
                    (folder/Path(n).name).write_bytes(zipfile.ZipFile(io.BytesIO(data)).read(n))
        (folder/'source.json').write_text(json.dumps({'assetId':id,'page':'https://ambientcg.com/view?id='+id,'license':'CC0-1.0','license_url':'https://docs.ambientcg.com/license/','api':asset},indent=2)+'\n')
    for id in BARK:
        folder=SOURCE/'polyhaven'/id;folder.mkdir(parents=True,exist_ok=True)
        files=get('https://api.polyhaven.com/files/'+id).json();info=get('https://api.polyhaven.com/info/'+id).json();inputs={}
        for channel in ['Diffuse','nor_gl']:
            d=files[channel]['1k']['jpg'];p=folder/(channel+'.jpg')
            if not p.exists() or hashlib.md5(p.read_bytes()).hexdigest()!=d['md5']:p.write_bytes(get(d['url']).content)
            if hashlib.md5(p.read_bytes()).hexdigest()!=d['md5']:raise ValueError('Source checksum '+id)
            inputs[channel]={'url':d['url'],'md5':d['md5'],'sha256':sha(p)}
        (folder/'source.json').write_text(json.dumps({'assetId':id,'page':'https://polyhaven.com/a/'+id,'license':'CC0-1.0','license_url':'https://polyhaven.com/license','info':info,'inputs':inputs},indent=2)+'\n')
        print('Fetched',id,flush=True)

def cutouts(id):
    folder=SOURCE/'ambientcg'/id
    colour=np.asarray(Image.open(next(folder.glob('*_Color.png'))).convert('RGB'))/255
    normal=np.asarray(Image.open(next(folder.glob('*_NormalGL.png'))).convert('RGB'))/255
    alpha=np.asarray(Image.open(next(folder.glob('*_Opacity.png'))).convert('L'))/255
    labels,count=ndimage.label(ndimage.binary_closing(alpha>.45,iterations=2))
    pieces=[]
    for i,s in enumerate(ndimage.find_objects(labels),1):
        if s is None:continue
        mask=labels[s]==i
        if mask.sum()<300:continue
        ys,xs=np.nonzero(mask);coords=np.stack([xs,ys],axis=1)
        cov=np.cov(coords.T);axis=np.linalg.eigh(cov)[1][:,-1]
        angle=math.degrees(math.atan2(axis[1],axis[0]))+90
        c=colour[s].copy();n=normal[s].copy();a=alpha[s]*mask
        # Neutralise only the mean, preserving scanned veins, blemishes and hue
        # variation. Species/instance colour remains the final broad tint.
        valid=a>.6;lc=linear(c);grey=lc@np.array([.2126,.7152,.0722])
        shade=grey*(.68/max(.025,grey[valid].mean()))
        # Independent channel equalisation amplifies almost absent blue into
        # magenta artefacts. Keep scan luminance and only restrained chroma.
        chroma=np.log(np.maximum(.008,lc))-np.log(np.maximum(.008,grey[:,:,None]))
        chroma-=chroma[valid].mean(axis=0)
        c=srgb(np.clip(shade[:,:,None]*np.exp(np.clip(chroma,-.22,.22)*.3),0,1))
        c=image(np.dstack([c,a])).rotate(angle,Image.Resampling.BICUBIC,expand=True)
        n=n*2-1;rad=math.radians(angle);x=n[:,:,0].copy();y=n[:,:,1].copy()
        n[:,:,0]=math.cos(rad)*x-math.sin(rad)*y;n[:,:,1]=math.sin(rad)*x+math.cos(rad)*y
        n=image(np.dstack([n*.5+.5,a])).rotate(angle,Image.Resampling.BICUBIC,expand=True)
        box=c.getbbox();c=c.crop(box);n=n.crop(box)
        aa=np.asarray(c)[:,:,3];h=c.height
        if (aa[:h//3]>100).sum()<(aa[-h//3:]>100).sum():
            c=c.transpose(Image.Transpose.ROTATE_180)
            nn=np.asarray(n).copy();nn[:,:,:2]=255-nn[:,:,:2];n=Image.fromarray(nn).transpose(Image.Transpose.ROTATE_180)
        pieces.append((c,n))
    if not pieces:raise ValueError('No photographed cutouts '+id)
    return sorted(pieces,key=lambda p:p[0].width*p[0].height,reverse=True)

def stamp(c,n,piece,base,tip,width,rng):
    dx,dy=tip[0]-base[0],tip[1]-base[1];length=math.hypot(dx,dy)
    cc,nn=piece
    height=max(2,round(length*SIZE));ww=max(2,round(height*width))
    cc=cc.resize((ww,height),Image.Resampling.LANCZOS);nn=nn.resize((ww,height),Image.Resampling.LANCZOS)
    angle=math.degrees(math.atan2(-dx,-dy));rad=math.radians(angle)
    normals=np.asarray(nn).astype(float)/255;v=normals[:,:,:3]*2-1;x=v[:,:,0].copy();y=v[:,:,1].copy()
    v[:,:,0]=math.cos(rad)*x-math.sin(rad)*y;v[:,:,1]=math.sin(rad)*x+math.cos(rad)*y
    nn=image(np.dstack([v*.5+.5,normals[:,:,3]]))
    cc=cc.rotate(angle,Image.Resampling.BICUBIC,expand=True);nn=nn.rotate(angle,Image.Resampling.BICUBIC,expand=True)
    cx=(base[0]+tip[0])*.5*SIZE;cy=(base[1]+tip[1])*.5*SIZE
    xy=(round(cx-cc.width/2),round(cy-cc.height/2));c.alpha_composite(cc,xy)
    # Store vector colours without alpha blending toward an unrelated old normal.
    n.paste(nn,xy,cc.getchannel('A'))

def shoot(family,variant,library):
    rng=random.Random(78119+family*101+variant*7907)
    c=Image.new('RGBA',(SIZE,SIZE));n=Image.new('RGBA',(SIZE,SIZE),(128,128,255,0))
    if family==4:
        # The scan already contains attached individual needles and twig detail.
        pieces=library['LeafSet019'];piece=pieces[variant%len(pieces)]
        stamp(c,n,piece,(.5,.96),(.5,.04),min(.85,piece[0].width/piece[0].height),rng)
        return c,n
    ids={0:['LeafSet005','LeafSet018','LeafSet024','LeafSet018'],1:['LeafSet022']*4,2:['LeafSet016','LeafSet010','LeafSet016','LeafSet010'],3:['LeafSet005','LeafSet024','LeafSet018','LeafSet024']}
    if family>=5:
        # Continuous blade detail: geometry supplies the pinnate/fan silhouette.
        p=library['LeafSet022'][variant%len(library['LeafSet022'])]
        w,h=p[0].size;box=(round(w*.3),round(h*.25),round(w*.7),round(h*.7))
        colour=p[0].crop(box).resize((SIZE,SIZE),Image.Resampling.LANCZOS);normal=p[1].crop(box).resize((SIZE,SIZE),Image.Resampling.LANCZOS)
        # Extend the leaf interior over the blade; no photographed rectangular
        # background or second cutout may punch holes in opaque frond geometry.
        ca=np.asarray(colour).copy();na=np.asarray(normal).copy();valid=ca[:,:,3]>100
        inds=ndimage.distance_transform_edt(~valid,return_distances=False,return_indices=True)
        ca=ca[tuple(inds)];na=na[tuple(inds)];ca[:,:,3]=255;na[:,:,3]=255
        return Image.fromarray(ca),Image.fromarray(na)
    pieces=library[ids[family][variant]]
    cd=ImageDraw.Draw(c);nd=ImageDraw.Draw(n)
    def line(a,b,width):
        xy=[tuple(round(v*SIZE) for v in a),tuple(round(v*SIZE) for v in b)]
        cd.line(xy,fill=(195,171,128,255),width=width);nd.line(xy,fill=(128,128,255,255),width=width)
    root=(.5,.97);tip=(.5+rng.uniform(-.06,.06),.05);line(root,tip,3)
    leaves=[]
    for row in range(8):
        y=.86-row*.10;fraction=row/8
        for sign in [-1,1]:
            joint=(.5+(tip[0]-.5)*(1-y),y+rng.uniform(-.015,.015))
            reach=(.35-.14*fraction)*rng.uniform(.9,1.05)
            end=(joint[0]+sign*reach,joint[1]-.13-rng.uniform(0,.04));line(joint,end,2)
            for k in range(6):
                t=(k+.65)/6;base=(joint[0]+(end[0]-joint[0])*t,joint[1]+(end[1]-joint[1])*t)
                length=rng.uniform(.087,.12)*(1-.18*fraction)
                angle=math.atan2(end[1]-joint[1],end[0]-joint[0])+(-1 if k%2 else 1)*rng.uniform(.45,1.15)
                target=(base[0]+math.cos(angle)*length,base[1]+math.sin(angle)*length)
                leaves.append((rng.choice(pieces),base,target,(.42 if family in [1,3] else .68)*rng.uniform(.7,1)))
    for p,base,target,width in leaves:stamp(c,n,p,base,target,width,rng)
    return c,n

def build():
    OUTPUT.mkdir(parents=True,exist_ok=True);library={id:cutouts(id) for id in LEAVES};outputs=[]
    sheet=Image.new('RGB',(4*SIZE,8*SIZE),(55,62,66))
    for family in range(8):
        for variant in range(4):
            c,n=shoot(family,variant,library);layer=family*4+variant
            # Bleed RGB into transparent texels before BC7 filtering/compression.
            ca=np.asarray(c).copy();na=np.asarray(n).copy();alpha=ca[:,:,3];valid=alpha>100
            inds=ndimage.distance_transform_edt(~valid,return_distances=False,return_indices=True)
            ca[:,:,:3]=ca[tuple(inds)][:,:,:3];na[:,:,:3]=na[tuple(inds)][:,:,:3];na[:,:,3]=128
            for channel,data in [('albedo',ca),('normal',na)]:
                p=OUTPUT/f'shoot-{layer:02d}-{channel}.png';Image.fromarray(data).save(p);outputs.append(p)
            preview=Image.fromarray(ca);sheet.paste(preview,(variant*SIZE,family*SIZE),preview)
    scratch=ROOT/'artifacts/tree-research';scratch.mkdir(parents=True,exist_ok=True);sheet.resize((1024,2048)).save(scratch/'shoots.png')
    for layer,id in enumerate(BARK):
        f=SOURCE/'polyhaven'/id
        c=np.asarray(Image.open(f/'Diffuse.jpg').convert('RGB').resize((512,512),Image.Resampling.LANCZOS))/255
        a=linear(c);mean=a.mean(axis=(0,1));c=srgb(np.clip(a*(.65/np.maximum(.01,mean)),.001,1))
        n=np.asarray(Image.open(f/'nor_gl.jpg').convert('RGB').resize((512,512),Image.Resampling.LANCZOS))/255*2-1
        n/=np.maximum(.001,np.linalg.norm(n,axis=2,keepdims=True))
        for channel,data in [('albedo',c),('normal',n*.5+.5)]:
            p=OUTPUT/f'bark-{layer:02d}-{channel}.png';image(data).save(p);outputs.append(p)
    inputs=[{'path':p.relative_to(ROOT).as_posix(),'sha256':sha(p),'bytes':p.stat().st_size} for p in sorted(SOURCE.rglob('*')) if p.is_file()]
    report={'version':'tree-surfaces-1','leaves':LEAVES,'bark_patterns':dict(zip(['Smooth','Fissured','Scaly','Fibrous','Peeling'],BARK)),
      'licenses':['https://docs.ambientcg.com/license/','https://polyhaven.com/license'],
      'research':[{'url':'https://unity.com/how-to/gpu-optimization','application':'Keep cutout geometry/overdraw bounded, reuse instanced art and measure actual forest views.'}],
      'processing':'Photographed isolated leaf alpha/normal cutouts, neutral mean tint, deterministic attached twig assemblies, RGB edge dilation. Continuous blade interiors retain geometric frond silhouettes. Registered scanned bark. TreeSurfaceArtBuilder preserves alpha coverage and pads surface colour/normal/depth at every mip.',
      'gpu':'32 shoot layers x512 BC7 colour/normal; 5 bark layers x512 BC7 colour/normal. CPU bake pixels downsampled to256. No extra draw calls or shadow lights.',
      'inputs':inputs,'outputs':[{'path':p.relative_to(ROOT).as_posix(),'sha256':sha(p),'bytes':p.stat().st_size} for p in outputs]}
    (ROOT/'assets/verification/tree-surface-sources-20261103.json').write_text(json.dumps(report,indent=2)+'\n')
    print('Prepared',len(outputs),'swatches from',len(inputs),'source inputs',flush=True)

if __name__=='__main__':
    import argparse
    p=argparse.ArgumentParser();p.add_argument('--offline',action='store_true');args=p.parse_args()
    if not args.offline:fetch()
    build()
