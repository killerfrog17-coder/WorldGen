"""Small static antler library: four paired shapes, shared packed grain maps."""
import math
from pathlib import Path
import bpy
import bmesh
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree


def build_library(body,root,eye_y):
    size=512;y,x=np.mgrid[:size,:size];u=x/size;v=y/size
    grain=(np.sin(u*math.pi*104+np.sin(v*19)*.8)+.45*np.sin(u*math.pi*238+v*7))*.5
    broad=np.sin(u*13+v*5)*.045+np.sin(u*31-v*9)*.025
    colour=np.array([.34,.235,.14])*(1+broad[:,:,None]+grain[:,:,None]*.11)
    tip=np.clip((v-.60)/.40,0,1)[:,:,None]*.65
    colour=colour*(1-tip)+np.array([.67,.57,.41])*tip
    rough=np.clip(.70+grain*.055,.58,.82)
    normal=np.dstack([grain*.035,np.sin(v*71+u*19)*.008,np.ones_like(u)])
    normal/=np.linalg.norm(normal,axis=2,keepdims=True)
    material=bpy.data.materials.new('Antler keratin - longitudinal grain');material.use_nodes=True
    nodes=material.node_tree.nodes;links=material.node_tree.links;bsdf=nodes.get('Principled BSDF')
    for name,pixels,space,target in [('colour',colour,'sRGB','Base Color'),
            ('roughness',np.repeat(rough[:,:,None],3,axis=2),'Non-Color','Roughness'),
            ('normal',normal*.5+.5,'Non-Color','Normal')]:
        image=bpy.data.images.new('Antler '+name,width=size,height=size,alpha=True)
        image.colorspace_settings.name=space
        image.pixels.foreach_set(np.dstack([pixels,np.ones_like(u)]).astype(np.float32).reshape(-1))
        image.filepath_raw=str(Path(root)/('antler-'+name+'.png'));image.file_format='PNG';image.save();image.pack()
        texture=nodes.new('ShaderNodeTexImage');texture.image=image
        if name=='normal':
            bump=nodes.new('ShaderNodeNormalMap');bump.inputs['Strength'].default_value=.35
            links.new(texture.outputs['Color'],bump.inputs['Color']);links.new(bump.outputs['Normal'],bsdf.inputs['Normal'])
        else:links.new(texture.outputs['Color'],bsdf.inputs[target])
    surface=BVHTree.FromPolygons([p.co for p in body.data.vertices],[p.vertices for p in body.data.polygons],all_triangles=True)
    library=[]
    for style,label in enumerate(('Young spikes','Narrow forks','Woodland branches','Wide crown')):
        vertices=[];faces=[];texcoords=[];roots=[]
        def stem(points,radii):
            points=[np.asarray(p,float) for p in points];start=len(vertices)
            rings=len(radii)
            for k,radius in enumerate(radii):
                tangent=points[min(k+1,len(points)-1)]-points[max(0,k-1)];tangent/=np.linalg.norm(tangent)
                across=np.cross(tangent,[0,1,0]);across/=np.linalg.norm(across);other=np.cross(tangent,across)
                for j in range(4):
                    angle=j*math.pi*.5;vertices.append(points[k]+radius*(across*math.cos(angle)+other*math.sin(angle)))
                    texcoords.append((j/4,k/rings))
            for k in range(rings-1):
                for j in range(4):
                    a=start+k*4+j;b=start+k*4+(j+1)%4;c=a+4;d=b+4
                    faces.extend([(a,b,c),(b,d,c)])
            faces.extend([(start,start+2,start+1),(start,start+3,start+2)])
            tip=len(vertices);vertices.append(points[-1]);texcoords.append((.5,1))
            for j in range(4):faces.append((start+(rings-1)*4+j,start+(rings-1)*4+(j+1)%4,tip))
        for sign in (-1,1):
            hit,_,_,_=surface.ray_cast(Vector((sign*.043,eye_y-.060,1.75)),Vector((0,0,-1)),.5)
            if hit is None:raise ValueError('Antler pedicle missed skull')
            base=np.array(hit)-np.array([0,0,.003]);roots.append(base.tolist())
            spread=[.76,.88,1.0,1.16][style];height=[.60,.83,1.0,1.05][style]
            def point(dx,dy,dz):return base+np.array([sign*dx*spread,dy,dz*height])
            if style==0:
                main=[point(0,0,0),point(.015,-.025,.065),point(.035,-.07,.13),point(.055,-.095,.19)]
                stem(main,[.009,.008,.004])
            else:
                main=[point(0,0,0),point(.018,-.015,.060),point(.062,-.065,.17),point(.115,-.13,.27),point(.14,-.19,.33)]
                stem(main,[.0105,.0115,.008,.0045])
                stem([main[1],point(.025,.070,.155)],[.0065])
                if style>=2:stem([main[2],point(.105,.005,.29)],[.0055])
                if style>=3:stem([main[3],point(.185,-.045,.36)],[.0045])
        data=bpy.data.meshes.new('Antler mesh - '+label);data.from_pydata(vertices,[],faces);data.update()
        uv=data.uv_layers.new(name='AntlerUV')
        for polygon in data.polygons:
            polygon.use_smooth=True
            us=[texcoords[data.loops[i].vertex_index][0] for i in polygon.loop_indices]
            for i in polygon.loop_indices:
                tu,tv=texcoords[data.loops[i].vertex_index]
                if max(us)-min(us)>.5 and tu<.5:tu+=1
                uv.data[i].uv=(tu,tv)
        bm=bmesh.new();bm.from_mesh(data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(data);bm.free()
        obj=bpy.data.objects.new('Antlers - '+label,data);bpy.context.scene.collection.objects.link(obj);data.materials.append(material)
        obj.hide_render=True;obj.hide_set(True)
        library.append(dict(label=label,object=obj.name,triangles=len(faces),roots=roots))
    return library
