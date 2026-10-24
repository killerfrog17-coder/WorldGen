"""Assemble unrigged painted review with real glossy eyes, lids and whiskers."""
import argparse
import json
import math
import runpy
from pathlib import Path
import sys
import bpy
import numpy as np
from mathutils import Vector
ap=argparse.ArgumentParser();ap.add_argument('--root',type=Path,required=True)
ap.add_argument('--replace-unopened',action='store_true')
ap.add_argument('--output-name',default='Deer - Stage1 Model Review.blend')
ap.add_argument('--pilot',action='store_true',help='Unpublished first-coat face/head inspection only')
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=a.root.resolve()
if Path(a.output_name).name!=a.output_name:raise ValueError('Output must be a filename')
out=root/a.output_name
if out.exists() and not a.replace_unopened:raise FileExistsError(out)
bpy.ops.wm.open_mainfile(filepath=str(root/'Deer - Stage1 Features.blend'))
s=bpy.context.scene;info=json.loads(s['fauna_stage1']);body=bpy.data.objects[info['body']]
d=np.load(root/'anatomy.npz');data=bpy.data.meshes.new('UV mapped neutral deer body')
data.from_pydata(d['vertices'],[],d['faces']);data.update()
for p in data.polygons:p.use_smooth=True
uv=data.uv_layers.new(name='CoatUV')
for p in data.polygons:
    for li in p.loop_indices:uv.data[li].uv=d['vertex_uv'][data.loops[li].vertex_index]
data.normals_split_custom_set_from_vertices(d['normals']);body.data=data
coats=[]
profiles=[('Tawny woodland',.80,1.0),('Dappled spotted',.86,.90),('Grey upland',.78,.78),('Snow-pale',.76,.72),
    ('Dark closed-forest',.82,.86),('Light-striped thicket',.86,.85),('Dark-banded understory',.87,.84),('Sandy dry-woodland',1.00,1.05)]
if a.pilot:profiles=profiles[:1]
for i,(label,ear_scale,tail_scale) in enumerate(profiles):
    mat=bpy.data.materials.new('Coat '+str(i)+' - '+label);mat.use_nodes=True;mat.use_fake_user=True
    nodes=mat.node_tree.nodes;links=mat.node_tree.links;bsdf=nodes.get('Principled BSDF')
    for role,filename,space in [('colour','colour','sRGB'),('roughness','roughness','Non-Color'),('normal','normal','Non-Color')]:
        node=nodes.new('ShaderNodeTexImage');image=bpy.data.images.load(str(root/f'coat-{i}-{filename}.png'),check_existing=False)
        image.colorspace_settings.name=space;image.pack();node.image=image
        if role=='normal':
            normal=nodes.new('ShaderNodeNormalMap');normal.inputs['Strength'].default_value=.5
            links.new(node.outputs['Color'],normal.inputs['Color']);links.new(normal.outputs['Normal'],bsdf.inputs['Normal'])
        else:links.new(node.outputs['Color'],bsdf.inputs['Base Color' if role=='colour' else 'Roughness'])
    coats.append(dict(label=label,material=mat.name,ear_scale=ear_scale,tail_scale=tail_scale,antler_style=[2,1,3,0,1,0,1,2][i]))
body.data.materials.clear();body.data.materials.append(bpy.data.materials[coats[0]['material']])
# Local morphs retain fixed attachments. These are appearance traits, not rigs.
body.shape_key_add(name='Basis');base=d['vertices'];regions=d['region_weights']
frames=json.loads((root/'coat-settings.json').read_text())['ear_frames']
def smooth(a,b,x):
    t=np.clip((x-a)/(b-a),0,1);return t*t*(3-2*t)
for name,scale in [('Compact ears',.72),('Large ears',1.08)]:
    delta=np.zeros_like(base)
    for label,column in [('L',3),('R',4)]:
        pivot=np.array(frames[label]['root']);mask=smooth(.30,.80,regions[:,column])
        delta+=(base-pivot)*(scale-1)*mask[:,None]
    key=body.shape_key_add(name=name)
    for v,p in zip(key.data,base+delta):v.co=p
tail_root=np.array([0,-.656,1.075])
# Measured tail on this candidate: rear centre, z .87-1.07 m. Keep the rump
# and the fixed attachment untouched rather than scaling a broad hip region.
tail_mask=smooth(-.63,-.68,base[:,1])*smooth(.065,.035,np.abs(base[:,0]))*smooth(1.075,.99,base[:,2])*smooth(.80,.86,base[:,2])
for name,scale in [('Short tail',.70),('Long tail',1.20)]:
    key=body.shape_key_add(name=name);target=base+(base-tail_root)*(scale-1)*tail_mask[:,None]
    for v,p in zip(key.data,target):v.co=p
body.data.shape_keys.key_blocks['Compact ears'].value=(1-profiles[0][1])/.28
# A small dedicated iris texture is mapped onto curved eye geometry, not onto
# the coat. The lids occlude its upper/lower edges; shader highlights are live.
size=512;yy,xx=np.mgrid[:size,:size];u=(xx+.5)/size*2-1;v=(yy+.5)/size*2-1
r=np.sqrt((u*.020/.0105)**2+(v*.014/.0105)**2);angle=np.arctan2(v,u)
streak=.5+.5*np.sin(angle*67+np.sin(angle*13)*1.6+r*13)
iris=np.array([.15,.095,.043])*(.7+.35*streak[:,:,None])
blend=np.clip((1-r)*6,0,1)[:,:,None];rgb=np.array([.028,.024,.019])*(1-blend)+iris*blend
pupil=np.clip((1-((u*.020/.0085)**2+(v*.014/.0028)**2))*8,0,1)[:,:,None]
rgb=rgb*(1-pupil)+np.array([.004,.005,.004])*pupil
image=bpy.data.images.new('Deer iris and horizontal pupil',width=size,height=size,alpha=True)
rgba=np.dstack([rgb,np.ones((size,size))]);image.pixels.foreach_set(rgba.astype(np.float32).reshape(-1));image.pack()
eye_mat=bpy.data.materials.new('Glossy curved deer eyes');eye_mat.use_nodes=True
bsdf=eye_mat.node_tree.nodes.get('Principled BSDF');bsdf.inputs['Roughness'].default_value=.15
bsdf.inputs['IOR'].default_value=1.38;bsdf.inputs['Coat Weight'].default_value=0
bsdf.inputs['Specular IOR Level'].default_value=.35
texture=eye_mat.node_tree.nodes.new('ShaderNodeTexImage');texture.image=image;eye_mat.node_tree.links.new(texture.outputs['Color'],bsdf.inputs['Base Color'])
for name in info['eyes']:bpy.data.objects[name].data.materials.append(eye_mat)
land=json.loads((root/'landmarks.json').read_text());nose=np.array(land['nose']['point']);wv=[];wf=[]
for side in (-1,1):
    for i in range(4):
        start=nose+np.array([side*.026,-.014,-.008-i*.004])
        end=start+np.array([side*(.050+i*.007),-.015-i*.008,-.003-i*.003])
        mid=(start+end)*.5+np.array([0,.008,.002]);base=len(wv)
        for point,width in [(start,.00045),(mid,.00030),(end,.00005)]:wv.extend([point+[0,0,-width],point+[0,0,width]])
        wf.extend([(base,base+1,base+2),(base+1,base+3,base+2),(base+2,base+3,base+4),(base+3,base+5,base+4)])
def tapered_hair(start,end,bend,width,horizontal):
    start=np.array(start);end=np.array(end);mid=(start+end)*.5+np.array(bend);offset=np.array(horizontal)*width
    base=len(wv);wv.extend([start-offset,start+offset,mid-offset*.55,mid+offset*.55,end])
    wf.extend([(base,base+1,base+2),(base+1,base+3,base+2),(base+2,base+3,base+4)])
for label in ('right','left'):
    anchor=land['eye_'+label];normal=np.array(anchor['normal']);horizontal=np.cross([0,0,1],normal);horizontal/=np.linalg.norm(horizontal)
    vertical=np.cross(normal,horizontal)
    for i,start in enumerate(anchor['upper_lid_roots']):
        length=.006+.003*math.sin((i+1)*1.7)**2
        end=np.array(start)+vertical*length+normal*(.0135+.002*math.sin(i*1.3)**2)+horizontal*((i-3.5)*.0005)
        tapered_hair(start,end,normal*.004,.00026,horizontal)
    for i,start in enumerate(anchor['brow_roots']):
        end=np.array(start)+1.55*(normal*(.009+i*.0015)+vertical*(.006-i*.001)+horizontal*((i-1)*.004))
        tapered_hair(start,end,vertical*.0015,.00022,horizontal)
wd=bpy.data.meshes.new('Low polygon muzzle whiskers');wd.from_pydata(wv,[],wf);wd.update()
whiskers=bpy.data.objects.new('Muzzle whiskers, lashes and brow hairs',wd);s.collection.objects.link(whiskers)
wm=bpy.data.materials.new('Whisker keratin');wm.diffuse_color=(.025,.021,.016,1);whiskers.data.materials.append(wm)
antler_builder=runpy.run_path(str(Path(__file__).with_name('build-fauna-antlers.py')))
antlers=antler_builder['build_library'](body,root,info['head_target'][1])
info.update(coats=coats,whiskers=whiskers.name,review_stage='Stage 1 - model/coat approval; no rig or animation yet',
    antlers=antlers,eye_hairs=dict(upper_lashes=16,brow_whiskers=6),
    nose_revision=json.loads((root/'coat-settings.json').read_text()).get('rhinarium_revision',1),review_file=out.name,
    face_revision=json.loads((root/'coat-settings.json').read_text()).get('face_revision',1),ear_range=[.72,1.08],tail_range=[.70,1.20],
    triangle_count=sum(len(o.data.loop_triangles) if len(o.data.loop_triangles) else sum(len(p.vertices)-2 for p in o.data.polygons)
        for o in [body,*[bpy.data.objects[n] for n in info['eyes']],whiskers]))
info['antlerless_triangles']=info['triangle_count']
info['triangle_count']+=max(x['triangles'] for x in antlers)
assert info['triangle_count']<=6000,info['triangle_count']
s['fauna_stage1']=json.dumps(info);s.render.film_transparent=False
body.select_set(True);bpy.context.view_layer.objects.active=body
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            sp=area.spaces.active;sp.shading.type='MATERIAL';sp.shading.use_scene_world=False;sp.overlay.show_overlays=False
            sp.show_region_ui=True;sp.region_3d.view_location=(0,0,.83);sp.region_3d.view_distance=3.2
            sp.region_3d.view_rotation=Vector((1,.05,.06)).to_track_quat('Z','Y');sp.region_3d.view_perspective='ORTHO'
            sp.clip_end=100
bpy.ops.wm.save_as_mainfile(filepath=str(out))
views={}
render_views=[('side',(1,0,0),(0,0,.82),2.8),('front',(0,1,0),(0,0,.82),2.1),
    ('rear',(0,-1,0),(0,0,.82),2.1),('face',(0,1,0),info['head_target'],.62),
    ('head',(1,.2,.02),info['head_target'],.65)]
render_views.extend([('eye',(1,.28,.06),land['eye_right']['point'],.13),
    ('oblique',(.75,1,.05),info['head_target'],.65)])
if a.pilot:render_views=render_views[-2:]
for name,direction,target,span in render_views:
    target=Vector(target);s.camera.location=target+Vector(direction).normalized()*4
    s.camera.rotation_euler=(target-s.camera.location).to_track_quat('-Z','Y').to_euler();s.camera.data.ortho_scale=span
    s.render.filepath=str(root/(name+'-painted.png'));bpy.ops.render.render(write_still=True)
(root/'stage1-assembly.json').write_text(json.dumps(info,indent=2))
print('Painted unrigged stage1 review assembled.',flush=True)
