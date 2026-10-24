"""Small skeletal projection diagnostic for classifying a licensed motion take."""
import argparse,json
from pathlib import Path
import numpy as np
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True);ap.add_argument('--clip',required=True)
a=ap.parse_args();s=json.loads((a.root/'source-rig.json').read_text());arrays=np.load(a.root/'source-motions.npz');clip=next(c for c in s['clips'] if c['label']==a.clip)
m=arrays[clip['key']];names=[b['name'] for b in s['bones']];ids={n:i for i,n in enumerate(names)}
fig,axes=plt.subplots(2,5,figsize=(14,6));samples=np.linspace(0,len(m)-1,5).astype(int)
for col,index in enumerate(samples):
    p=m[index,:,:3,3]
    for row,dims in enumerate([(1,2),(0,2)]):
        ax=axes[row,col]
        for b in s['bones']:
            if b['parent'] in ids:
                pair=p[[ids[b['name']],ids[b['parent']]]][:,dims]
                ax.plot(pair[:,0],pair[:,1],'-o',markersize=2,color='tab:red' if 'Left' in b['name'] else 'tab:blue' if 'Right' in b['name'] else 'black')
        ax.set_aspect('equal');ax.set_title(f'{index/30:.2f}s '+('Side' if row==0 else 'Front'));ax.grid(alpha=.2)
fig.suptitle(a.clip);fig.tight_layout();target=a.root/'source-motion-diagnostic.png';fig.savefig(target,dpi=110);plt.close(fig);print(target)
