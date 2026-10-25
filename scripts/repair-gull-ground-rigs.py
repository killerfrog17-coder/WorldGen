"""Two actual anatomical ground legs per new folded-wing gull model.

The neural graphs duplicated feet or rooted the torso at a toe. Preserve all
four own model surfaces, replace those graphs with compact manual ground rigs.
"""
import datetime,hashlib,json,shutil
from pathlib import Path
import numpy as np
repo=Path(__file__).resolve().parent.parent;root=repo/'artifacts/fauna-overnight-20261024/gull'
state=json.loads((root.parent/'queue-state.json').read_text());assert state.get('current')!='gull'
meta=json.loads((root/'audition.json').read_text());assert all(e['canonical_axis_correction'] is None for e in meta['candidates'])
config={'reason':'Own manually placed two-leg gull ground rigs; avoid duplicated neural feet and toe-only motion. Folded-wing ground review, no flight claim.','skin_ceiling_m':.58,'candidates':{}}
for number in range(1,5):
    folder=root/f'candidate-{number}';current=folder/'generated-rig.npz';backup=folder/'generated-rig.before-manual-ground.npz';assert not backup.exists()
    raw=np.load(current);v=raw['source_vertices'].copy();faces=raw['source_faces'].copy();mid=float(np.median(v[(v[:,2]>.5)&(v[:,2]<1.1),0]))
    joints=[[mid,.25,.67],[mid,.88,1.24],[mid,-.62,.65]];parents=[-1,0,0];legs=[]
    for side in (-1,1):
        feet=v[((v[:,0]-mid)*side>.025)&(v[:,2]<.12)&(v[:,1]>-.1)];assert len(feet)>12
        x=float(np.median(feet[:,0]));y=float(np.quantile(feet[:,1],.20));z=float(feet[:,2].min())+.035;start=len(joints)
        points=[[x*.9+mid*.1,y-.02,.47],[x*.95+mid*.05,y-.08,.30],[x,y,.12],[x,y+.035,z],[x,y+.20,z]]
        joints.extend(points);parents.extend([0,start,start+1,start+2,start+3]);legs.append({'nodes':[0,*range(start,start+5)],'roles':[0,1,2,3,4,5],'foot':start+3})
    joints=np.asarray(joints);parents=np.asarray(parents);skin=np.zeros((len(v),len(joints)));skin[:,0]=1
    for leg in legs:
        chain=leg['nodes'][1:];starts=joints[chain[:-1]];delta=np.diff(joints[chain],axis=0);t=np.clip(np.sum((v[:,None]-starts)*delta,axis=2)/np.sum(delta**2,axis=1),0,1);dd=np.linalg.norm(v[:,None]-(starts+t[:,:,None]*delta),axis=2);near=dd.argmin(1);distance=dd[np.arange(len(v)),near]
        for i in np.flatnonzero((distance<.15)&(v[:,2]<.58)):
            blend=float(np.clip((.15-distance[i])/.07,0,1)*np.clip((.58-v[i,2])/.14,0,1));k=int(near[i]);u=t[i,k];row=np.zeros(len(joints));row[chain[k]]=1-u;row[chain[k+1]]=u;skin[i]=skin[i]*(1-blend)+row*blend
    keep=np.argsort(skin,axis=1)[:,-4:];sparse=np.zeros_like(skin);np.put_along_axis(sparse,keep,np.take_along_axis(skin,keep,axis=1),axis=1);sparse/=sparse.sum(1)[:,None]
    shutil.copy2(current,backup);shutil.copy2(current.with_suffix('.json'),backup.with_suffix('.json'))
    np.savez_compressed(current,source_vertices=v,source_faces=faces,joints=joints,parents=parents,sample_points=v,sample_skin=sparse)
    current.with_suffix('.json').write_text(json.dumps({'method':'Owner-requested manual anatomical ground-bird rig','manual_rig':True,'joints':len(joints),'mesh_geometry_unchanged':True,'reason':config['reason'],'source_mesh_sha256':hashlib.sha256((folder/'model.glb').read_bytes()).hexdigest(),'rejected_neural_sha256':hashlib.sha256(backup.read_bytes()).hexdigest()},indent=2)+'\n')
    config['candidates'][str(number)]={'legs':legs,'mesh_sha256':hashlib.sha256((folder/'model.glb').read_bytes()).hexdigest(),'rig_sha256':hashlib.sha256(current.read_bytes()).hexdigest()}
(root/'manual-ground-walk.json').write_text(json.dumps(config,indent=2)+'\n');p=repo/'docs/fauna/GROUND_BIRD_WALK_REPAIRS.json';d=json.loads(p.read_text());d['gull']=config;p.write_text(json.dumps(d,indent=2)+'\n')
(root/'review-rebuild.json').write_text(json.dumps({'reason':config['reason'],'time':datetime.datetime.now().astimezone().isoformat(),'art_approved':False},indent=2)+'\n');print('Four manual ground-gull rigs prepared')
