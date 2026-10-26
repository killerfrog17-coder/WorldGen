"""Count the actual free GLB animation downloads, excluding duplicate exports."""
import hashlib
import json
from pathlib import Path
import struct

import numpy as np

REPO=Path(__file__).resolve().parent.parent
ROOT=REPO/'artifacts/human-animation-sources'


def inspect(path):
    raw=path.read_bytes()
    assert raw[:4]==b'glTF'
    length,kind=struct.unpack_from('<II',raw,12)
    assert kind==0x4E4F534A
    document=json.loads(raw[20:20+length]);binary=20+length+8

    def accessor(index):
        item=document['accessors'][index];view=document['bufferViews'][item['bufferView']]
        dtype=np.dtype({5126:'<f4',5125:'<u4',5123:'<u2',5121:'u1'}[item['componentType']])
        width={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}[item['type']]
        offset=binary+view.get('byteOffset',0)+item.get('byteOffset',0)
        return np.ndarray((item['count'],width),dtype=dtype,buffer=raw,offset=offset,
                          strides=(view.get('byteStride',dtype.itemsize*width),dtype.itemsize))

    clips=[]
    for animation in document.get('animations',[]):
        moving=False;duration=0
        for sampler in animation['samplers']:
            values=accessor(sampler['output']);times=accessor(sampler['input'])
            assert np.isfinite(values).all() and np.isfinite(times).all()
            moving |= bool(np.ptp(values,axis=0).max()>1e-5)
            duration=max(duration,float(times.max()))
        name=animation.get('name','unnamed')
        clips.append({'name':name,'duration_seconds':duration,'has_changing_track':moving,
                      'reference_pose':name in ['A_TPose','T-Pose']})
    joints=sorted({document['nodes'][index].get('name','') for skin in document.get('skins',[]) for index in skin['joints']})
    fingers=[name for name in joints if any(token in name.lower() for token in ['thumb','index','finger','middle','pinky','ring'])]
    return {'path':str(path.relative_to(REPO)),'sha256':hashlib.sha256(raw).hexdigest(),
            'joint_count':len(joints),'finger_related_joints_including_leaves':fingers,'clips':clips}


def main():
    files=[]
    for source in ['quaternius-ual1','quaternius-ual2','kaykit']:
        for path in sorted((ROOT/source).rglob('*.glb')):
            info=inspect(path)
            if not info['clips']:continue
            info['source']=source
            info['root_motion_duplicate']='_RM.' in path.name
            info['alternate_large_rig']=path.name.startswith('Rig_Large')
            info['special_set']='_Special.' in path.name
            files.append(info)
    summary={}
    for source in ['quaternius-ual1','quaternius-ual2','kaykit']:
        chosen=[item for item in files if item['source']==source and not item['root_motion_duplicate'] and not item['alternate_large_rig']]
        clips=[(item,clip) for item in chosen for clip in item['clips'] if not clip['reference_pose']]
        regular=[clip for item,clip in clips if not item['special_set']]
        summary[source]={'non_reference_entries':len(clips),'ordinary_human_entries_including_poses':len(regular),
                         'ordinary_human_changing_clips':sum(clip['has_changing_track'] for clip in regular),
                         'ordinary_human_static_poses':[clip['name'] for clip in regular if not clip['has_changing_track']],
                         'special_entries':sum(item['special_set'] for item,clip in clips)}
    report={'cost_usd':0,'method':'GLB animations/changing tracks; formats, RM versions and Large rig not double-counted',
            'quality_limit':'Inventory only. No retargeting, natural-motion, contacts, transitions or first-person acceptance.',
            'summary':summary,'ordinary_human_changing_clip_entries':sum(s['ordinary_human_changing_clips'] for s in summary.values()),
            'ordinary_human_pose_entries':sum(len(s['ordinary_human_static_poses']) for s in summary.values()),'files':files}
    (ROOT/'inventory.json').write_text(json.dumps(report,indent=2))
    lines=['source\tfile\tclip\tseconds\tmoving\tcounted_in_ordinary_set']
    for item in files:
        for clip in item['clips']:
            counted=not(item['root_motion_duplicate'] or item['alternate_large_rig'] or item['special_set'] or clip['reference_pose'])
            lines.append('\t'.join(map(str,[item['source'],Path(item['path']).name,clip['name'],clip['duration_seconds'],clip['has_changing_track'],counted])))
    (ROOT/'inventory.tsv').write_text('\n'.join(lines)+'\n')
    print(json.dumps({'summary':summary,'ordinary_human_changing_clip_entries':report['ordinary_human_changing_clip_entries'],
                      'ordinary_human_pose_entries':report['ordinary_human_pose_entries']},indent=2))


if __name__=='__main__':main()
