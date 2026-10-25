"""Ray-pick facial anchors on the chosen body, ignoring detached debris.

Screen picks refer to the saved orthographic head guide. Left-side anchors
are cast onto the actual opposite surface, not copied through the head.
"""
import argparse,hashlib,json,sys
from pathlib import Path
import bpy,numpy as np
from mathutils import Matrix,Vector
from mathutils.bvhtree import BVHTree

repo=Path(__file__).resolve().parent.parent
sys.path.insert(0,str(repo/'scripts'))
from fauna_paint_recipes import SUPPORTED, family
ap=argparse.ArgumentParser();ap.add_argument('--slug',required=True)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);assert a.slug in SUPPORTED
root=repo/'artifacts/fauna-painted-20261024'/a.slug
state=json.loads((root/'paint-state.json').read_text())
bpy.ops.wm.open_mainfile(filepath=str(root/'Animal - Painted Species Review.blend'))
entry=json.loads(bpy.context.scene['deer_four_rigs'])[0];body=bpy.data.objects[entry['body']]
p=np.array([v.co[:] for v in body.data.vertices]);faces=np.array([f.vertices[:] for f in body.data.polygons])
adj=[set() for _ in p]
for face in faces:
    for v in face:adj[v].update(int(i) for i in face if i!=v)
remaining=set(range(len(p)));components=[]
while remaining:
    seed=remaining.pop();component={seed};stack=[seed]
    while stack:
        for v in adj[stack.pop()]:
            if v in remaining:remaining.remove(v);component.add(v);stack.append(v)
    components.append(component)
main=max(components,key=len);face_ids=[i for i,f in enumerate(faces) if all(int(v) in main for v in f)]
bvh=BVHTree.FromPolygons([Vector(x) for x in p],faces[face_ids].tolist(),all_triangles=True)
def cast(origin,direction):
    point,normal,index,distance=bvh.ray_cast(Vector(origin),Vector(direction).normalized())
    assert point is not None,'Facial guide ray missed main body'
    return {'point':list(point),'normal':list(normal),'triangle':face_ids[index],'distance':distance}
def cast_pixel(camera,pixel):
    matrix=Matrix(camera['camera_matrix']);width,height=camera['size'];scale=camera['scale']
    local=Vector((((pixel[0]+.5)/width-.5)*scale,(.5-(pixel[1]+.5)/height)*scale,0))
    inverse=body.matrix_world.inverted()
    return cast(inverse@(matrix@local),inverse.to_3x3()@(matrix.to_3x3()@Vector((0,0,-1))))
prior=root/'face-landmarks.json'
if prior.exists():
    saved=json.loads(prior.read_text());assert saved['body_geometry_sha256']==state['geometry_sha256']
    camera=saved['guide_camera']  # Later diagnostic renders may have another size.
else:camera=json.loads((root/'previews/1-Bind-side-head.json').read_text())
pixel={'hare':(507,535),'rabbit':(503,373),'red-fox':(447,462),'wolf':(490,330),'wild-boar':(500,450),'mouflon':(487,428)}[a.slug];width,height=camera['size'];scale=camera['scale']
head=np.array(state['head_seed']);nose_seed=np.array(state['nose_seed'])
front=nose_seed-head;front[2]=0;front/=np.linalg.norm(front)
side=np.array([front[1],-front[0],0])
if family(a.slug)=='lagomorph':
    right_eye=cast_pixel(camera,pixel);eye=np.array(right_eye['point'])
    mirrored=eye-2*np.dot(eye-head,side)*side
    left_eye=cast(mirrored-side*.8,side)
front_guide=None;front_pixels=None
independent=None;pick_path=root/'face-guide-picks.json'
if pick_path.exists():
    picks=json.loads(pick_path.read_text());assert picks['model_sha256']==state['source_model_sha256'],'Guide belongs to another chosen mesh'
    independent={}
    for side_name in ['right','left']:
        item=picks['eyes'][side_name];guide=json.loads((root/item['camera']).read_text())
        independent[side_name]={'camera':guide,'pixel':item['pixel']}
    right_eye,left_eye=[cast_pixel(independent[key]['camera'],independent[key]['pixel']) for key in ['right','left']]
    level=(right_eye['point'][2]+left_eye['point'][2])/2
    right_eye=cast([right_eye['point'][0]+.8,right_eye['point'][1],level],[-1,0,0])
    left_eye=cast([left_eye['point'][0]-.8,left_eye['point'][1],level],[1,0,0])
    if picks.get('nose_seed'):nose_seed=np.array(picks['nose_seed'])
    midpoint=(np.array(right_eye['point'])+np.array(left_eye['point']))/2
    front=nose_seed-midpoint;front[2]=0;front/=np.linalg.norm(front);side=np.array([front[1],-front[0],0])
elif family(a.slug)!='lagomorph':
    # Independently inspected sockets avoid copying a median-head plane onto
    # an asymmetric/oblique generated skull.
    front_guide=saved.get('front_guide_camera') if prior.exists() else None
    if front_guide is None:front_guide=json.loads((root/'previews/1-Bind-front-head.json').read_text())
    front_pixels={'red-fox':[(291,320),(397,320)],'wolf':[(287,246),(424,244)],'wild-boar':[(260,526),(606,565)],'mouflon':[(462,431),(679,404)]}[a.slug]
    right_eye,left_eye=[cast_pixel(front_guide,x) for x in front_pixels]
    level=(right_eye['point'][2]+left_eye['point'][2])/2
    right_eye,left_eye=[cast([anchor['point'][0],float(p[:,1].max()+1),level],[0,-1,0]) for anchor in [right_eye,left_eye]]
    # The two sockets can differ in depth on a generated skull. Their joining
    # line must not incorrectly turn the apparent head/corneal axis.
    midpoint=(np.array(right_eye['point'])+np.array(left_eye['point']))/2
    front=nose_seed-midpoint;front[2]=0;front/=np.linalg.norm(front)
    side=np.array([front[1],-front[0],0])
nose=cast([nose_seed[0],float(p[:,1].max()+1),nose_seed[2]],[0,-1,0])
assert abs(right_eye['point'][2]-left_eye['point'][2])<1e-5
landmarks={'slug':a.slug,'source_candidate':state['source_candidate'],'method':'Inspected guide-pixel ray to largest connected body; opposite eye ray onto actual surface; front nose ray excludes loose debris.','right_eye':right_eye,'left_eye':left_eye,'nose':nose,'head_seed':head.tolist(),'side_axis':side.tolist(),'front_axis':front.tolist(),'eye_guide_pixel':pixel,'guide_camera':camera,'ignored_landmark_components':len(components)-1,'main_component_vertices':len(main),'body_vertices_unchanged':True,'body_geometry_sha256':state['geometry_sha256']}
if front_guide:landmarks.update(method='Independent inspected front-camera socket rays on main body; no skull-plane mirroring.',eye_guide_pixel=None,front_guide_camera=front_guide,front_eye_pixels=front_pixels,eye_height_difference=abs(right_eye['point'][2]-left_eye['point'][2]))
if independent:landmarks.update(method='Independent opposite-side inspected guide rays, levelled and recast from their own side; no frontal hidden-socket assumption.',eye_guide_pixel=None,independent_eye_guides=independent,eye_height_difference=abs(right_eye['point'][2]-left_eye['point'][2]))
(root/'face-landmarks.json').write_text(json.dumps(landmarks,indent=2)+'\n')
print('FACIAL ANCHORS',a.slug,json.dumps({key:landmarks[key]['point'] for key in ['right_eye','left_eye','nose']}),flush=True)
