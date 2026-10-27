"""Close the interior behind stitched lid rims without changing the visible eye."""
import bpy,numpy as np


def seal(slug,land,rig):
    if not land.get('anatomical_orbit',{}).get('sealed_socket_interior'):return []
    added=[]
    for side in ['right','left']:
        lid=bpy.data.objects[f'{slug} {side} socket and eyelid'];name=f'{slug} {side} sealed socket interior'
        if name in bpy.data.objects:bpy.data.objects.remove(bpy.data.objects[name],do_unlink=True)
        # Stitched orbits use 32 points around the inner opening. The new rim
        # duplicates those coordinates AND weights, so it cannot split in motion.
        count=32;rim=np.array([v.co[:] for v in lid.data.vertices[:count]])
        assert len(rim)==count
        frame=land[side+'_eye']['fit_frame'];normal=np.array(frame['normal']);normal/=np.linalg.norm(normal)
        radius=land[side+'_eye']['radius'][0]*land['anatomical_orbit'].get('globe_radius_factor',1)
        back=rim-normal*radius*1.15;center=back.mean(0)
        points=np.vstack([rim,back,center]);faces=[]
        for i in range(count):
            j=(i+1)%count;face=[i,j,j+count,i+count]
            A,B,D=points[face[0]],points[face[1]],points[face[3]]
            inward=rim.mean(0)-(rim[i]+rim[j])*.5
            if np.cross(B-A,D-A)@inward<0:face.reverse()
            faces.append(face)
            face=[count+i,count+j,count*2];A,B,D=points[face]
            if np.cross(B-A,D-A)@normal<0:face.reverse()
            faces.append(face)
        mesh=bpy.data.meshes.new(name);mesh.from_pydata(points.tolist(),[],faces);mesh.update()
        obj=bpy.data.objects.new(name,mesh);lid.users_collection[0].objects.link(obj)
        obj.parent=lid.parent;obj.matrix_parent_inverse=lid.matrix_parent_inverse.copy();obj.matrix_basis=lid.matrix_basis.copy()
        material=bpy.data.materials.get(slug+' internal socket tissue') or bpy.data.materials.new(slug+' internal socket tissue');material.use_nodes=True
        shader=material.node_tree.nodes.get('Principled BSDF');shader.inputs['Base Color'].default_value=(.018,.009,.007,1);shader.inputs['Roughness'].default_value=.65
        mesh.materials.append(material);uv=mesh.uv_layers.new(name='UVMap')
        for loop in mesh.loops:
            index=loop.vertex_index;uv.data[loop.index].uv=(.5,.5) if index==count*2 else ((index%count)/(count-1),.05 if index<count else .95)
        weights=[{lid.vertex_groups[g.group].name:g.weight for g in v.groups} for v in lid.data.vertices[:count]]
        average={key:sum(w.get(key,0) for w in weights)/count for key in set().union(*[set(w) for w in weights])}
        for i,skin in enumerate([*weights,*weights,average]):
            for bone,value in skin.items():
                group=obj.vertex_groups.get(bone) or obj.vertex_groups.new(name=bone);group.add([i],value,'REPLACE')
        modifier=obj.modifiers.new('Original selected rig','ARMATURE');modifier.object=rig
        source=next(m for m in lid.modifiers if m.type=='ARMATURE');modifier.use_deform_preserve_volume=source.use_deform_preserve_volume
        for face in mesh.polygons:face.use_smooth=True
        obj['eye_closure_repair']='Exact lid-rim seam and weighted blind socket lining; original body/globe/lid/rig/actions unchanged.'
        added.append(name)
    return added
