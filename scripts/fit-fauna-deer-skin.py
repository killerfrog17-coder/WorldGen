"""CPU fit of the accepted skeleton with anatomical weights for the fresh deer."""
import argparse
import json
from pathlib import Path
import numpy as np

ap=argparse.ArgumentParser();ap.add_argument('--model-root',type=Path,required=True);ap.add_argument('--root',type=Path,required=True)
a=ap.parse_args();root=a.root.resolve();model=a.model_root.resolve()
source=json.loads((root/'source-rig.json').read_text());new=np.load(model/'anatomy.npz')
bones=source['bones'];names=[b['name'] for b in bones];targets={}
chain=[('Pelvis',(0,-.44,1.08)),('Spine1',(0,-.33,1.13)),('Spine2',(0,-.10,1.13)),('Chest',(0,.13,1.10))]
neck=[('Neck1',(0,.36,.88)),('Neck2',(0,.49,1.08)),('Neck3',(0,.60,1.25)),('Head',(0,.73,1.37))]
for group,last in [(chain,(0,.21,1.08)),(neck,(0,.81,1.39))]:
    for i,(name,head) in enumerate(group):targets[name]=dict(head=list(head),tail=list(group[i+1][1] if i+1<len(group) else last))
targets['MotionRoot']=dict(head=[0,0,0],tail=[.20,0,0])
for side,sign in [('L',1),('R',-1)]:
    front=[('Shoulder',(.070,.30,1.10)),('UpperFore',(.130,.31,.96)),('Elbow',(.130,.30,.73)),
           ('Wrist',(.120,.40,.43)),('ForeHoof',(.130,.44,.13))]
    hind=[('Thigh',(.120,-.43,1.05)),('Knee',(.120,-.30,.75)),('Hock',(.090,-.46,.45)),('HindHoof',(.110,-.36,.13))]
    for group,last in [(front,(.135,.47,.04)),(hind,(.115,-.31,.035))]:
        for i,(name,head) in enumerate(group):
            tail=group[i+1][1] if i+1<len(group) else last
            targets[name+'_'+side]=dict(head=[sign*head[0],*head[1:]],tail=[sign*tail[0],*tail[1:]])
    # The accepted skeleton's ear labels have the opposite spatial sign.
    esign=-sign
    targets['Ear_'+side]=dict(head=[esign*.065,.6721937,1.415],tail=[esign*.225,.7121937,1.65])
    targets['Muzzle_'+side]=dict(head=[-sign*.035,.955,1.295],tail=[-sign*.040,1.002,1.28])
targets['Jaw']=dict(head=[0,.89,1.315],tail=[0,.96,1.285])
targets['Tail1']=dict(head=[0,-.656,1.075],tail=[0,-.69,.97])
targets['Tail2']=dict(head=[0,-.69,.97],tail=[0,-.67,.875])
assert set(targets)==set(names)
p=new['vertices'];regions=new['region_weights']
def smooth(lo,hi,x):
    t=np.clip((x-lo)/(hi-lo),0,1);return t*t*(3-2*t)
weights=np.zeros((len(p),len(names)),np.float64)
def chain_field(chain,axes):
    centers=np.array([(np.array(targets[n]['head'])+targets[n]['tail'])*.5 for n in chain])
    delta=p[:,None,:]-centers[None,:,:]
    distance=(delta[:,:,axes]**2).sum(2)
    field=1/(distance+.055**2)**2;field/=field.sum(1,keepdims=True)
    result=np.zeros_like(weights);result[:,[names.index(n) for n in chain]]=field
    return result
# Keep the back/ribcage on the spine. A spatially close foreign leg surface
# must never pull a patch of the back through the torso.
torso=chain_field(['Pelvis','Spine1','Spine2','Chest'],[1])
neck_field=chain_field(['Neck1','Neck2','Neck3','Head'],[1,2])
neck_gate=smooth(.28,.52,p[:,1])*smooth(.85,1.08,p[:,2])
weights=torso*(1-neck_gate[:,None])+neck_field*neck_gate[:,None]
for side,sign in [('L',1),('R',-1)]:
    for family,allowed in [('fore',['Shoulder','UpperFore','Elbow','Wrist','ForeHoof']),('hind',['Thigh','Knee','Hock','HindHoof'])]:
        keep=[names.index(n+'_'+side) for n in allowed]
        distances=[]
        for n in allowed:
            segment=targets[n+'_'+side];start=np.array(segment['head']);end=np.array(segment['tail']);axis=end-start
            t=np.clip((p-start)@axis/(axis@axis),0,1)
            distances.append(np.linalg.norm(p-start-t[:,None]*axis,axis=1))
        distances=np.array(distances).T
        pair=np.argsort(distances,axis=1)[:,:2];field=np.zeros_like(distances)
        chosen=np.take_along_axis(distances,pair,axis=1)
        pair_weights=np.exp(-((chosen-chosen.min(1,keepdims=True))/.035)**2)
        pair_weights/=pair_weights.sum(1,keepdims=True);np.put_along_axis(field,pair,pair_weights,axis=1)
        if family=='fore':gate=(1-smooth(.74,1.02,p[:,2]))*smooth(.10,.23,p[:,1])*(1-smooth(.48,.60,p[:,1]))
        else:gate=(1-smooth(.77,1.07,p[:,2]))*smooth(-.08,-.24,p[:,1])
        gate*=smooth(.015,.08,p[:,0]*sign)
        weights*=1-gate[:,None];weights[:,keep]+=field*gate[:,None]
# Diffuse joint weights over actual surface edges, welding UV splits only for
# this calculation. This softens muscle folds without editing bind geometry.
unique,reverse=np.unique(np.round(p,6),axis=0,return_inverse=True)
edges=np.concatenate([new['faces'][:,[0,1]],new['faces'][:,[1,2]],new['faces'][:,[2,0]]])
edges=np.unique(np.sort(reverse[edges],axis=1),axis=0);edges=edges[edges[:,0]!=edges[:,1]]
surface_weights=np.zeros((len(unique),len(names)));counts=np.bincount(reverse)
np.add.at(surface_weights,reverse,weights);surface_weights/=counts[:,None]
degree=np.bincount(edges.ravel(),minlength=len(unique))
for _ in range(10):
    total=np.zeros_like(surface_weights)
    np.add.at(total,edges[:,0],surface_weights[edges[:,1]]);np.add.at(total,edges[:,1],surface_weights[edges[:,0]])
    average=total/np.maximum(degree[:,None],1)
    surface_weights=surface_weights*.55+average*.45
weights=surface_weights[reverse]
head=smooth(.38,.66,regions[:,1]);head=np.maximum(head,(new['eye_regions'][:,1]>.01).astype(float))
head=np.maximum(head,smooth(.60,.70,p[:,1])*smooth(1.25,1.34,p[:,2]))
weights*=1-head[:,None];weights[:,names.index('Head')]+=head
for column,name in [(3,'Ear_R'),(4,'Ear_L')]:
    mask=regions[:,column]>.5;weights[mask]=0;weights[mask,names.index(name)]=1
tail=smooth(-.63,-.68,p[:,1])*smooth(.065,.035,np.abs(p[:,0]))*smooth(1.075,.99,p[:,2])*smooth(.80,.86,p[:,2])
tip=smooth(1.035,.92,p[:,2]);weights*=1-tail[:,None]
weights[:,names.index('Tail1')]+=tail*(1-tip);weights[:,names.index('Tail2')]+=tail*tip
for side,sign in [('L',1),('R',-1)]:
    for prefix,where in [('ForeHoof',p[:,1]>.08),('HindHoof',p[:,1]<-.05)]:
        hoof=(1-smooth(.105,.185,p[:,2]))*((p[:,0]*sign>.01)&where)
        weights*=1-hoof[:,None];weights[:,names.index(prefix+'_'+side)]+=hoof
weights[:,names.index('MotionRoot')]=0
top=np.argpartition(weights,-4,axis=1)[:,-4:];compact=np.take_along_axis(weights,top,axis=1)
assert (compact.sum(1)>1e-8).all();compact/=compact.sum(1,keepdims=True)
final=np.zeros_like(weights);np.put_along_axis(final,top,compact,axis=1)
assert np.isfinite(final).all() and np.max(np.abs(final.sum(1)-1))<1e-5
np.savez_compressed(root/'fitted-skin.npz',weights=final.astype(np.float32))
(root/'target-rig.json').write_text(json.dumps(targets,indent=2))
report=dict(method='Accepted rig landmark fit; anatomical spine/neck and limb capsule weights, geodesic smoothing, rigid head/hooves, top4 normalization',
    vertices=len(p),bones=len(names),max_weights=4,bind_geometry_changed=False,gpu_inference=False)
(root/'fit-report.json').write_text(json.dumps(report,indent=2));print(json.dumps(report),flush=True)
