"""Tiny fauna colour atlases on the accepted simple geometry and wing envelopes.

One material per displayed body, 512-pixel butterfly atlas and 256-pixel other
atlases. No additional polygons, point lights, limbs, skeletons or motion tracks.
"""
import argparse,hashlib,json,math,runpy,sys
from pathlib import Path
import bpy,numpy as np
repo=Path(__file__).resolve().parent.parent
sys.path.insert(0,str(repo/'scripts'))
from fauna_hypothetical_coats import mottling
KINDS={'butterflies-and-moths-guild':'butterfly','fireflies-derived-lineage':'firefly','bees-guild':'bee','dragonflies-guild':'dragonfly','native-detritivorous-flies-guild':'fly'}
ap=argparse.ArgumentParser();ap.add_argument('--slug',choices=KINDS,required=True)
a=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);kind=KINDS[a.slug]
source=repo/'artifacts/fauna-overnight-20261024'/a.slug/'Animal - Raw Audition.blend'
root=repo/'artifacts/fauna-painted-20261024'/a.slug;root.mkdir(exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(source));s=bpy.context.scene;entries=json.loads(s['deer_four_rigs'])
source_sha=hashlib.sha256(source.read_bytes()).hexdigest()
def snapshot(entry):
    body=bpy.data.objects[entry['body']];rig=bpy.data.objects[entry['rig']];h=hashlib.sha256()
    h.update(np.array([v.co[:] for v in body.data.vertices],'<f4').tobytes());h.update(json.dumps([p.vertices[:] for p in body.data.polygons]).encode())
    h.update(json.dumps([[(body.vertex_groups[g.group].name,g.weight) for g in v.groups] for v in body.data.vertices]).encode())
    h.update(json.dumps([(b.name,b.parent.name if b.parent else None,b.head_local[:],b.tail_local[:],[list(r) for r in b.matrix_local]) for b in rig.data.bones]).encode())
    for name in [*entry['actions'].values(),*entry['demos'].values()]:
        action=bpy.data.actions[name]
        for layer in action.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    for curve in bag.fcurves:
                        h.update((name+curve.data_path+str(curve.array_index)).encode());h.update(np.array([(*k.co,*k.handle_left,*k.handle_right) for k in curve.keyframe_points],'<f4').tobytes())
    return h.hexdigest()
before=[snapshot(e) for e in entries]
LABELS={
 'butterfly':['Cream / dark veins pigment analogue','Orange / dark margins pigment analogue','Blue scale pigment analogue','Green banded pigment analogue','Bark-mottled moth pigment analogue','Hypothetical dry-leaf mottled lineage'],
 'bee':['Amber dark-banded bee','Buff dark-banded bee','Dark rusty bee pigment analogue','Metallic green pigment analogue','Hypothetical pale dryland bee'],
 'firefly':['Green-yellow light / dark body','Warm amber light / brown body','Yellow-green light / red pronotum','Hypothetical blue-green light lineage','Hypothetical pale woodland body / green light'],
 'dragonfly':['Blue abdomen pigment analogue','Red abdomen pigment analogue','Green abdomen pigment analogue','Brown abdomen pigment analogue','Hypothetical bronze marsh lineage'],
 'fly':['Dark fly','Green metallic bottle-fly pigment analogue','Blue metallic bottle-fly pigment analogue','Brown fly','Hypothetical muted bronze carrion-fly lineage'],
}[kind]
size=512 if kind=='butterfly' else 256
for entry in entries:
    body=bpy.data.objects[entry['body']];mesh=body.data;p=np.array([v.co[:] for v in mesh.vertices]);polys=list(mesh.polygons);original_mats=[poly.material_index for poly in polys]
    wing_ids=sorted({v for poly,m in zip(polys,original_mats) if m==1 for v in poly.vertices})
    wing=p[wing_ids];wx=max(float(np.abs(wing[:,0]).max()),.1);wy0,wy1=float(wing[:,1].min()),float(wing[:,1].max())
    yy,xx=np.mgrid[0:size,0:size];u=(xx+.5)/size;v=(yy+.5)/size
    # Texture half at u>=.5 is the wing panel, mirrored across the body.
    x=(u-.5)/.48*wx;y=wy0+(v-.02)/.96*(wy1-wy0);angle=np.arctan2(y-.12,x-.04)
    radial=np.sqrt((x-.04)**2+(y-.12)**2)
    veins=np.maximum((np.cos(angle*13+radial*3)>.975).astype(float),(np.cos(radial*27)>.994).astype(float))
    edges={}
    for poly,m in zip(polys,original_mats):
        if m!=1 or np.mean(p[list(poly.vertices),0])<0:continue
        ids=list(poly.vertices)
        for i,j in zip(ids,ids[1:]+ids[:1]):key=tuple(sorted((i,j)));edges[key]=edges.get(key,0)+1
    border=np.full_like(u,10.)
    for (i,j),count in edges.items():
        if count!=1:continue
        start=p[i,:2];end=p[j,:2];delta=end-start
        t=np.clip(((x-start[0])*delta[0]+(y-start[1])*delta[1])/max(float(delta@delta),1e-8),0,1)
        border=np.minimum(border,np.sqrt((x-start[0]-t*delta[0])**2+(y-start[1]-t*delta[1])**2))
    wing_margin=border<(.045 if kind=='butterfly' else .006)
    layer=mesh.uv_layers.active or mesh.uv_layers.new(name='UVMap')
    for poly,m in zip(polys,original_mats):
        for i in poly.loop_indices:
            point=p[mesh.loops[i].vertex_index]
            if m==1:uv=(.5+.48*abs(point[0])/wx,.02+.96*(point[1]-wy0)/max(wy1-wy0,.001))
            elif m==2:uv=(.29,.4)
            elif m==3:uv=(.39+.05*point[0],.1+.8*(point[1]-p[:,1].min())/max(np.ptp(p[:,1]),.001))
            else:uv=(.02+.21*(point[0]-p[:,0].min())/max(np.ptp(p[:,0]),.001),.02+.96*(point[1]-p[:,1].min())/max(np.ptp(p[:,1]),.001))
            layer.data[i].uv=uv
    mesh.materials.clear();coats=[]
    for index,label in enumerate(LABELS):
        base=[.08,.065,.04];wingcolour=[.65,.67,.57]
        if kind=='butterfly':wingcolour=[[.88,.78,.38],[.84,.43,.045],[.10,.40,.76],[.12,.58,.21],[.34,.25,.15],[.64,.55,.32]][index]
        elif kind=='bee':base=[[.80,.43,.06],[.64,.46,.20],[.25,.08,.035],[.12,.38,.24],[.68,.59,.37]][index]
        elif kind=='dragonfly':base=[[.07,.38,.66],[.61,.11,.055],[.17,.40,.17],[.34,.24,.10],[.38,.32,.13]][index]
        elif kind=='fly':base=[[.04,.045,.035],[.07,.28,.11],[.06,.14,.34],[.19,.12,.06],[.25,.24,.13]][index]
        rgb=np.tile(base,(size,size,1));alpha=np.ones((size,size))
        if kind=='bee':rgb*=np.where(np.sin(v*math.tau*5)>.2,.17,1.)[:,:,None]
        if kind=='dragonfly':rgb*=np.where(np.sin(v*math.tau*11)>.87,.27,1.)[:,:,None]
        if kind=='firefly':rgb*=.7 if index<4 else 2.2
        mask=u>=.5;rgb[mask]=wingcolour
        if kind=='butterfly':
            dark=np.array([.025,.024,.020]);marks=np.maximum(veins*.85,wing_margin.astype(float))
            if index==2:marks=wing_margin*.85+veins*.25
            if index==3:marks=np.maximum(wing_margin*.95,(np.cos(radial*12)>.35)*.8)
            if index>=4:marks=np.clip((mottling(x*11,y*13)-.25)*1.2+veins*.15+wing_margin*.18,0,.80)
            rgb[mask]=(rgb*(1-marks[:,:,None])+dark*marks[:,:,None])[mask]
            if index==1:
                dots=(np.cos(y*58)>.90)&(border>.015)&(border<.040)&mask;rgb[dots]=[.89,.86,.70]
        else:
            rgb[mask]*=(1-veins*.24-wing_margin*.25)[mask,None];alpha[mask]=.47 if kind!='bee' else .60
        rgb[(u>.25)&(u<.33)]=[.012,.014,.010]
        glow_col=[[.57,.95,.08],[1.,.69,.07],[.72,1.,.14],[.08,.88,.52],[.49,.95,.11]][index%5]
        glow=(u>.34)&(u<.47) if kind=='firefly' else np.zeros_like(u,dtype=bool)
        rgb[glow]=glow_col
        if kind=='firefly' and index==2:rgb[(u<.25)&(v>.63)&(v<.78)]=[.38,.065,.025]
        # Image pixels use bottom-up rows. No baked light or specular highlights.
        image=bpy.data.images.new(f'{a.slug} body{entry["number"]} palette{index}',width=size,height=size,alpha=True)
        rgb=np.clip(rgb,0,1);image.colorspace_settings.name='sRGB'
        image.pixels.foreach_set(np.dstack([rgb,alpha]).astype(np.float32).ravel());image.pack()
        material=bpy.data.materials.new(f'{a.slug} body{entry["number"]} coat{index}');material.use_nodes=True;material.use_fake_user=True
        shader=material.node_tree.nodes.get('Principled BSDF');shader.inputs['Roughness'].default_value=.60 if kind=='butterfly' else .42;shader.inputs['Metallic'].default_value=.32 if kind=='fly' and index in [1,2,4] else .08
        tex=material.node_tree.nodes.new('ShaderNodeTexImage');tex.image=image;material.node_tree.links.new(tex.outputs['Color'],shader.inputs['Base Color']);material.node_tree.links.new(tex.outputs['Alpha'],shader.inputs['Alpha'])
        if kind=='firefly':
            emission=bpy.data.images.get(a.slug+' half-body glow mask') or bpy.data.images.new(a.slug+' half-body glow mask',width=size,height=size,alpha=True)
            emission.pixels.foreach_set(np.dstack([glow,glow,glow,np.ones_like(u)]).astype(np.float32).ravel());emission.pack()
            enode=material.node_tree.nodes.new('ShaderNodeTexImage');enode.image=emission
            pulse=material.node_tree.nodes.new('ShaderNodeValue');pulse.name='Smooth light pulse';driver=pulse.outputs[0].driver_add('default_value').driver;driver.expression='6*max(0,sin((frame-1)*0.10471975511965977))**2'
            multiply=material.node_tree.nodes.new('ShaderNodeMath');multiply.operation='MULTIPLY';material.node_tree.links.new(enode.outputs['Color'],multiply.inputs[0]);material.node_tree.links.new(pulse.outputs[0],multiply.inputs[1]);material.node_tree.links.new(multiply.outputs[0],shader.inputs['Emission Strength']);material.node_tree.links.new(tex.outputs['Color'],shader.inputs['Emission Color'])
        coats.append({'label':label,'material':material.name,'pigment_study':True,'hypothetical':'Hypothetical' in label})
    mesh.materials.append(bpy.data.materials[coats[0]['material']])
    for poly in polys:poly.material_index=0
    entry.update(painting_coats=coats,painted=True,objects=[entry['body']],paint_body_distance=3.1)
    entry['texture_notes']='Tiny one-material UV atlas; wing patterns and translucency, no additional polygons. Pigment analogues retain the approved wing/body shape.'
    assert snapshot(entry)==before[entry['number']-1]
s['deer_four_rigs']=json.dumps(entries);bpy.context.preferences.filepaths.save_version=0
dest=root/'Animal - Painted Species Review.blend';bpy.ops.wm.save_as_mainfile(filepath=str(dest))
bpy.ops.wm.open_mainfile(filepath=str(dest));entries=json.loads(bpy.context.scene['deer_four_rigs']);assert [snapshot(e) for e in entries]==before
viewer=runpy.run_path(str(repo/'scripts/deer-four-rigs-viewer.py'));seen=set()
for _ in range(len(LABELS)*3):bpy.ops.deer_four.species();seen.add(viewer['state']['coat_index'])
assert len(seen)==len(LABELS)
for entry in entries:
    ob=bpy.data.objects[entry['body']];assert np.isfinite(np.array([uv.uv[:] for uv in ob.data.uv_layers.active.data])).all()
    for coat in entry['painting_coats']:
        assert all(node.image.packed_file for node in bpy.data.materials[coat['material']].node_tree.nodes if node.type=='TEX_IMAGE')
    for clip in entry['actions']:
        rig=bpy.data.objects[entry['rig']];rig.animation_data_clear();action=bpy.data.actions[entry['actions'][clip]];rig.animation_data_create().action=action;rig.animation_data.action_slot=action.slots[0];rig.data.pose_position='POSE'
        for frame in [0,15,30,45,60]:bpy.context.scene.frame_set(frame);assert np.isfinite(np.array([v.co[:] for v in ob.evaluated_get(bpy.context.evaluated_depsgraph_get()).data.vertices])).all()
if kind=='firefly':
    pulse_material=bpy.data.materials[entries[0]['painting_coats'][0]['material']]
    bpy.data.objects[entries[0]['body']].data.materials[0]=pulse_material
    for frame,expected in [(1,0),(16,6),(31,0),(61,0)]:
        bpy.context.scene.frame_set(frame);bpy.context.view_layer.update()
        tree=pulse_material.node_tree.evaluated_get(bpy.context.evaluated_depsgraph_get())
        value=tree.nodes['Smooth light pulse'].outputs[0].default_value
        assert abs(value-expected)<.001,(frame,value,expected)
assert source_sha==hashlib.sha256(source.read_bytes()).hexdigest()
report={'slug':a.slug,'source_blend_sha256':source_sha,'checked_blend_sha256':hashlib.sha256(dest.read_bytes()).hexdigest(),'source_bind_geometry_skin_rig_actions_exact':True,'candidate_count':len(entries),'coats_per_candidate':len(LABELS),'all_coats_reachable':True,'packed_atlas_size':size,'no_added_triangles':True,'no_point_lights_added':True,'appearance_approved':False,'actually_visually_inspected':False,'scope':'Pigment/wing membrane studies on accepted simple forms; wing motion is original preview envelope, not resolved real wing-beat dynamics.'}
(root/'paint-procedural-checks.json').write_text(json.dumps(report,indent=2)+'\n')
(root/'paint-state.json').write_text(json.dumps({**report,'status':'procedural_coloured_working','painting_started':True,'coats':entries[0]['painting_coats']},indent=2)+'\n')
print('MICROFAUNA PAINT CHECKS PASS',a.slug,len(entries),'unchanged bodies',len(LABELS),'coats each')
