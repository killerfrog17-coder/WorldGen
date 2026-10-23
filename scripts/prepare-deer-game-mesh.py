"""Reduce the accepted own deer shape, preserve its rig/actions, discard old paint.

Offline Blender preparation/assembly only. No game integration, rerigging,
motion regeneration, or borrowed model geometry.
"""
import argparse
import json
from pathlib import Path
import sys
import bpy
import numpy as np
from mathutils import Vector


def limit_weights(mesh):
    for v in mesh.data.vertices:
        influences=sorted([(g.group,g.weight) for g in v.groups if g.weight>1e-8],key=lambda p:p[1],reverse=True)
        keep=influences[:4]
        total=sum(w for _,w in keep)
        if total<=0:
            raise ValueError('Unweighted LOD vertex')
        for index,_ in influences[4:]:
            mesh.vertex_groups[index].remove([v.index])
        for index,w in keep:
            mesh.vertex_groups[index].add([v.index],w/total,'REPLACE')


def main():
    ap=argparse.ArgumentParser()
    ap.add_argument('stage',choices=['prepare','assemble'])
    ap.add_argument('--review',type=Path,required=True)
    ap.add_argument('--root',type=Path,required=True)
    ap.add_argument('--triangles',type=int,default=14000)
    ap.add_argument('--output-name',default='Deer - Game Asset Review.blend')
    args=ap.parse_args(sys.argv[sys.argv.index('--')+1:])
    args.review,args.root=args.review.resolve(),args.root.resolve()
    args.root.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(args.review))
    scene=bpy.context.scene
    rig=bpy.data.objects['AI Deer Rig']
    mesh=bpy.data.objects['Locally generated deer']
    if args.stage=='prepare':
        output=args.root/'Deer - Unpainted Game Mesh.blend'
        if output.exists():
            raise FileExistsError(output)
        rig.data.pose_position='REST'
        bpy.ops.object.select_all(action='DESELECT')
        mesh.select_set(True)
        bpy.context.view_layer.objects.active=mesh
        for modifier in list(mesh.modifiers):
            mesh.modifiers.remove(modifier)
        # Join identical seam vertices before reduction so the old UV layout
        # does not lock needless edges or cause discontinuous guide normals.
        import bmesh
        bm=bmesh.new()
        bm.from_mesh(mesh.data)
        bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-6)
        bm.to_mesh(mesh.data)
        bm.free()
        mesh.data.update()
        np.savez_compressed(args.root/'high-shape.npz',
            vertices=np.array([v.co[:] for v in mesh.data.vertices],np.float32),
            faces=np.array([p.vertices[:] for p in mesh.data.polygons],np.int32))
        source_triangles=sum(len(p.vertices)-2 for p in mesh.data.polygons)
        # Clear the rejected texture before any new view/control export.
        mesh.data.materials.clear()
        old_materials=[m for m in bpy.data.materials if m.users==0]
        for m in old_materials:
            bpy.data.materials.remove(m)
        for image in list(bpy.data.images):
            if image.users==0:
                bpy.data.images.remove(image)
        decimate=mesh.modifiers.new('Offline game mesh reduction','DECIMATE')
        decimate.ratio=min(1,args.triangles/source_triangles)
        decimate.use_collapse_triangulate=True
        bpy.ops.object.modifier_apply(modifier=decimate.name)
        triangulate=mesh.modifiers.new('Game triangles','TRIANGULATE')
        bpy.ops.object.modifier_apply(modifier=triangulate.name)
        vertices=np.array([v.co[:] for v in mesh.data.vertices],np.float32)
        faces=np.array([p.vertices[:] for p in mesh.data.polygons],np.int32)
        weights=np.zeros((len(vertices),len(mesh.vertex_groups)),np.float32)
        for v in mesh.data.vertices:
            for g in v.groups:
                weights[v.index,g.group]=g.weight
        # Decimation interpolates influences; enforce runtime top-four limit.
        best=np.argsort(weights,axis=1)[:,-4:]
        top=np.zeros_like(weights)
        np.put_along_axis(top,best,np.take_along_axis(weights,best,axis=1),axis=1)
        top/=np.maximum(top.sum(1,keepdims=True),1e-8)
        if np.any(top.sum(1)<.99):
            raise ValueError('Unweighted reduced vertex')
        np.savez_compressed(args.root/'game-mesh.npz',vertices=vertices,faces=faces,weights=top,
            groups=np.array([g.name for g in mesh.vertex_groups]))
        (args.root/'mesh-budget.json').write_text(json.dumps(dict(source_triangles=source_triangles,
            game_triangles=len(faces),vertices=len(vertices),max_weights=4,
            source_review=str(args.review),old_paint_reused=False),indent=2))
        modifier=mesh.modifiers.new('Approved rig','ARMATURE')
        modifier.object=rig
        modifier.use_deform_preserve_volume=True
        bpy.ops.wm.save_as_mainfile(filepath=str(output))
        print('GAME_MESH',len(faces),len(vertices),flush=True)
        return
    if Path(args.output_name).name!=args.output_name:
        raise ValueError('Output name must be a filename')
    output=args.root/args.output_name
    if output.exists():
        raise FileExistsError(output)
    data=np.load(args.root/'uv-mesh.npz')
    new_data=bpy.data.meshes.new('Game deer UV mesh')
    new_data.from_pydata(data['vertices'].tolist(),[],data['faces'].tolist())
    new_data.update()
    mesh.data=new_data
    mesh.vertex_groups.clear()
    for i,name in enumerate(data['groups']):
        group=mesh.vertex_groups.new(name=str(name))
        for vi in np.flatnonzero(data['weights'][:,i]>1e-7):
            group.add([int(vi)],float(data['weights'][vi,i]),'REPLACE')
    uv=new_data.uv_layers.new(name='UVMap')
    for loop in new_data.loops:
        uv.data[loop.index].uv=data['uv'][loop.vertex_index]
    for p in new_data.polygons:
        p.use_smooth=True
    material=bpy.data.materials.new('Deer / SDXL colour + baked normal')
    material.use_nodes=True
    nodes=material.node_tree.nodes
    shader=nodes.get('Principled BSDF')
    shader.inputs['Roughness'].default_value=.85
    shader.inputs['Metallic'].default_value=0
    texture=nodes.new('ShaderNodeTexImage')
    texture.image=bpy.data.images.load(str(args.root/'deer-albedo.png'),check_existing=False)
    texture.image.pack()
    material.node_tree.links.new(texture.outputs['Color'],shader.inputs['Base Color'])
    mesh.data.materials.clear()
    mesh.data.materials.append(material)
    # Capture shape detail in a modest tangent normal map, without keeping the
    # dense source geometry in the exported animal. No colour enters this bake.
    rig.data.pose_position='REST'
    high=np.load(args.root/'high-shape.npz')
    high_data=bpy.data.meshes.new('Offline normal bake shape')
    high_data.from_pydata(high['vertices'].tolist(),[],high['faces'].tolist())
    high_data.update()
    for p in high_data.polygons:
        p.use_smooth=True
    high_obj=bpy.data.objects.new('Offline normal bake source',high_data)
    scene.collection.objects.link(high_obj)
    normal_image=bpy.data.images.new('Deer tangent normal',width=1024,height=1024,alpha=False)
    normal_image.generated_color=(.5,.5,1,1)
    normal_image.colorspace_settings.name='Non-Color'
    normal_node=nodes.new('ShaderNodeTexImage')
    normal_node.image=normal_image
    nodes.active=normal_node
    bpy.ops.object.select_all(action='DESELECT')
    high_obj.select_set(True)
    mesh.select_set(True)
    bpy.context.view_layer.objects.active=mesh
    scene.render.engine='CYCLES'
    scene.cycles.device='CPU'
    scene.cycles.samples=8
    scene.render.threads_mode='FIXED'
    scene.render.threads=4
    scene.render.bake.use_selected_to_active=True
    scene.render.bake.cage_extrusion=.007
    scene.render.bake.max_ray_distance=.02
    scene.render.bake.margin=12
    bpy.ops.object.bake(type='NORMAL')
    normal_pixels=np.empty(1024*1024*4,np.float32)
    normal_image.pixels.foreach_get(normal_pixels)
    normal_pixels=normal_pixels.reshape(-1,4)
    # Suppress reversed ray hits in thin/close parts of the imperfect AI sculpt.
    # A near-identical high/low surface should not need a backwards normal.
    bad=(normal_pixels[:,2]<.5)|(~np.isfinite(normal_pixels).all(axis=1))
    normal_pixels[bad]=(.5,.5,1,1)
    normal_image.pixels.foreach_set(normal_pixels.ravel())
    normal_image.update()
    (args.root/'normal-bake.json').write_text(json.dumps(dict(size=1024,
        reverse_or_invalid_pixels_neutralized=int(bad.sum()),max_ray_distance_m=.02),indent=2))
    normal_image.filepath_raw=str(args.root/'deer-normal.png')
    normal_image.file_format='PNG'
    normal_image.save()
    normal_image.pack()
    normal_map=nodes.new('ShaderNodeNormalMap')
    material.node_tree.links.new(normal_node.outputs['Color'],normal_map.inputs['Color'])
    material.node_tree.links.new(normal_map.outputs['Normal'],shader.inputs['Normal'])
    bpy.data.objects.remove(high_obj,do_unlink=True)
    bpy.data.meshes.remove(high_data)
    scene.render.bake.use_selected_to_active=False
    modifier=next((m for m in mesh.modifiers if m.type=='ARMATURE'),None)
    if modifier is None:
        modifier=mesh.modifiers.new('Approved rig','ARMATURE')
    modifier.object=rig
    modifier.use_deform_preserve_volume=True
    rig.data.pose_position='POSE'
    # Offline LOD meshes share the same UV atlas/material/rig. One visible.
    lods=[mesh]
    budgets=[len(new_data.polygons)]
    for i,target in enumerate((6000,2200,700),start=1):
        lod=mesh.copy()
        lod.data=mesh.data.copy()
        lod.name=f'Deer LOD{i}'
        scene.collection.objects.link(lod)
        for m in list(lod.modifiers):
            lod.modifiers.remove(m)
        bpy.ops.object.select_all(action='DESELECT')
        lod.select_set(True)
        bpy.context.view_layer.objects.active=lod
        dec=lod.modifiers.new('Offline LOD reduction','DECIMATE')
        dec.ratio=target/len(new_data.polygons)
        dec.use_collapse_triangulate=True
        bpy.ops.object.modifier_apply(modifier=dec.name)
        limit_weights(lod)
        m=lod.modifiers.new('Approved rig','ARMATURE')
        m.object=rig
        m.use_deform_preserve_volume=True
        lod.hide_render=True
        lod.hide_set(True)
        lods.append(lod)
        budgets.append(len(lod.data.polygons))
    scene['review_lods']='|'.join(o.name for o in lods)
    scene['review_lod']=0
    scene['review_status']='New SDXL paint / budgeted mesh; same approved authored motion and rig'
    scene['review_approved_motion']=True
    scene['texture_origin']='Local SDXL + geometric depth guidance + UV projection; no Hunyuan texture input'
    scene['review_autoplay']=True
    scene['review_clip']='Walk'
    scene.frame_start=0
    scene.frame_end=int(bpy.data.actions['Walk'].frame_range[1])-1
    scene.frame_set(4)
    bpy.ops.object.select_all(action='DESELECT')
    mesh.select_set(True)
    bpy.context.view_layer.objects.active=mesh
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                area.spaces.active.shading.type='MATERIAL'
                area.spaces.active.overlay.show_overlays=False
                area.spaces.active.show_region_ui=False
                area.spaces.active.region_3d.view_location=Vector((0,0,.85))
                area.spaces.active.region_3d.view_distance=3.5
                area.spaces.active.region_3d.view_rotation=Vector((2,-5,1.6)).to_track_quat('Z','Y')
    (args.root/'lod-budget.json').write_text(json.dumps(dict(triangles=budgets,
        shared_material=True,shared_rig=True,texture_size=list(texture.image.size)),indent=2))
    for image in list(bpy.data.images):
        if image.users==0:
            bpy.data.images.remove(image)
    bpy.ops.wm.save_as_mainfile(filepath=str(output))
    scene.render.engine='CYCLES'
    scene.cycles.device='CPU'
    scene.cycles.samples=16
    scene.render.threads_mode='FIXED'
    scene.render.threads=4
    scene.render.resolution_percentage=85
    for name,position in [('front',(2.7,-5,2.1)),('back',(2.7,5,2.1))]:
        scene.camera.location=position
        scene.camera.rotation_euler=(Vector((0,0,.85))-scene.camera.location).to_track_quat('-Z','Y').to_euler()
        scene.render.filepath=str(args.root/f'review-{name}.png')
        bpy.ops.render.render(write_still=True)


if __name__=='__main__':
    main()
