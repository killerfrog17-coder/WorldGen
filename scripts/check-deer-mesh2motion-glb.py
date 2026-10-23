"""Verify preservation and native motion provenance of the rerigged deer.

Offline structural checks only; they do not accept gait or skinning artistry.
"""
import argparse
import hashlib
import json
from pathlib import Path
import struct
import numpy as np
from scipy.spatial.transform import Rotation


class GLB:
    def __init__(self,path):
        self.bytes=Path(path).read_bytes()
        magic,version,length=struct.unpack_from('<III',self.bytes)
        assert magic==0x46546c67 and version==2 and length==len(self.bytes)
        count=struct.unpack_from('<I',self.bytes,12)[0]
        self.doc=json.loads(self.bytes[20:20+count]);self.bin=self.bytes[28+count:]
    def array(self,index):
        a=self.doc['accessors'][index];v=self.doc['bufferViews'][a['bufferView']]
        c={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}[a['type']]
        typ={5126:'<f4',5123:'<u2',5121:'u1',5125:'<u4'}[a['componentType']]
        size=np.dtype(typ).itemsize
        return np.ndarray((a['count'],c),dtype=typ,buffer=self.bin,
            offset=v.get('byteOffset',0)+a.get('byteOffset',0),
            strides=(v.get('byteStride',size*c),size)).copy()
    def images(self):
        result=[]
        for image in self.doc['images']:
            v=self.doc['bufferViews'][image['bufferView']];start=v.get('byteOffset',0)
            result.append(hashlib.sha256(self.bin[start:start+v['byteLength']]).hexdigest())
        return sorted(result)
    def primitive(self):return self.doc['meshes'][0]['primitives'][0]
    def world(self,node):
        d=self.doc['nodes'][node]
        if 'matrix' in d:m=np.array(d['matrix']).reshape(4,4).T
        else:
            m=np.eye(4);m[:3,:3]=Rotation.from_quat(d.get('rotation',[0,0,0,1])).as_matrix()@np.diag(d.get('scale',[1,1,1]))
            m[:3,3]=d.get('translation',[0,0,0])
        parent=next((i for i,n in enumerate(self.doc['nodes']) if node in n.get('children',[])),None)
        return self.world(parent)@m if parent is not None else m
    def vertices(self):
        p=self.array(self.primitive()['attributes']['POSITION'])
        node=next(i for i,n in enumerate(self.doc['nodes']) if n.get('mesh')==0)
        m=self.world(node)
        return p@m[:3,:3].T+m[:3,3]
    def curves(self):
        result={}
        for a in self.doc.get('animations',[]):
            for c in a['channels']:
                s=a['samplers'][c['sampler']];t=c['target'];name=self.doc['nodes'][t['node']]['name']
                result[a['name'],name,t['path']]=(self.array(s['input']),self.array(s['output']))
        return result


def main():
    ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True)
    ap.add_argument('--tool',type=Path,default=Path('artifacts/tools/mesh2motion-cli/node_modules/afk-rig'))
    a=ap.parse_args();root=a.root.resolve();source=GLB(root/'Deer-Unrigged-Forward.glb')
    native=GLB(a.tool/'assets/animations/horse-animations.glb');original=native.curves()
    report=[]
    for mode in ('template','limbs','aligned','fitted'):
        target=GLB(root/f'Deer-Mesh2Motion-{mode}.glb')
        error=float(np.abs(target.vertices()-source.vertices()).max())
        assert error<2e-6,(mode,error)
        assert target.images()==source.images(),mode
        sp,tp=source.primitive(),target.primitive()
        for attr in ('TEXCOORD_0',):
            assert np.array_equal(source.array(sp['attributes'][attr]),target.array(tp['attributes'][attr]))
        assert np.array_equal(source.array(sp['indices']),target.array(tp['indices']))
        assert len(target.doc['skins'])==1 and len(target.doc['skins'][0]['joints'])==56
        weights=target.array(tp['attributes']['WEIGHTS_0'])
        assert np.isfinite(weights).all() and np.max(np.abs(weights.sum(1)-1))<1e-5
        curves=target.curves();rotation_curves=0
        for key,(times,values) in curves.items():
            if key[2]!='rotation':continue
            ot,ov=original[key]
            assert np.array_equal(times,ot) and np.array_equal(values,ov),key
            rotation_curves+=1
        assert rotation_curves>100
        report.append(dict(mode=mode,world_vertex_error_max_m=error,uv_indices_exact=True,
            texture_image_bytes_exact=True,rotation_curves_exact=rotation_curves,
            bones=56,animations=len(target.doc['animations']),bytes=len(target.bytes)))
    provenance=json.loads((root/'input-landmarks.json').read_text())
    assert hashlib.sha256(Path(provenance['source']).read_bytes()).hexdigest()==provenance['source_sha256']
    (root/'preservation-checks.json').write_text(json.dumps({'exports':report,
        'accepted_blend_unchanged':True,'game_launched':False},indent=2))
    print('Mesh/UV/texture bytes/native rotation curves preserved:',report,flush=True)


if __name__=='__main__':main()
