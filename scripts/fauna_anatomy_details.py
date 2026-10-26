"""Small species-specific face surfaces, fitted to existing selected head skin."""
import math
import bpy,numpy as np
from mathutils import Vector


def add(slug,land,body,root,repo,obj,sample,bvh,coat_material,coat_objects):
    F=np.array(land['front_axis']);S=np.array(land['side_axis']);Z=np.array([0.,0.,1.]);nose=np.array(land['nose']['point']);mid=(np.array(land['right_eye']['point'])+land['left_eye']['point'])/2
    result={}
    if slug in {'wild-boar','bear'}:
        # A boar has a broad terminal nasal disc with TWO nostrils. The chosen
        # body has one central cavity; cover it with a fitted, closed disc.
        N=F*.99+Z*.14;N/=np.linalg.norm(N);V=np.cross(S,N);V*=1 if V@Z>0 else -1
        fit=land.get('nasal_disc_fit',{});C=np.array(fit.get('center',nose))-F*.006-Z*.005;w,h=fit.get('width_radius',.074 if slug=='bear' else .080),fit.get('height_radius',.034 if slug=='bear' else .051);points=[C+N*.006];uv=[(.5,.5)];dark=[0.];faces=[];count=48;rows=7
        if slug=='bear':
            source=np.array([v.co[:] for v in body.data.vertices]);relative=source-C;field=((relative@S)/w)**2+((relative@V)/h)**2;depths=relative@N;near=depths[(field<1.4)&(abs(depths)<.10)];fore=float(np.quantile(near,.80)) if len(near) else 0.;points[0]=C+N*(fore+.002)
        for row in range(1,rows+1):
            rho=row/rows
            for j in range(count):
                a=math.tau*j/count;u=rho*math.cos(a);v=rho*math.sin(a);well=max(math.exp(-(((u-sign*.43)/.20)**2+((v-.05)/.31)**2)*2) for sign in [-1,1])
                depth=.006*(1-rho*rho)-.010*well-.006*rho**10
                taper=(.76+.24*(v+1)/2) if slug=='bear' else 1.
                point=C+S*w*u*taper+V*h*v+N*depth
                if slug=='bear':
                    target=C+S*w*u*taper+V*h*v;hit=bvh.ray_cast(Vector(target+N*.4),Vector(-N),.8)[0];edge=float((np.array(hit)-target)@N) if hit is not None else -.012
                    depth=(fore+.002-.012*well)*(1-rho**4)+(edge+.001)*rho**4
                    # Keep the pigmented nostril surface in front of retained
                    # source skin; otherwise the source fills the dark well.
                    depth=max(depth,edge+.001);point=target+N*depth
                points.append(point);uv.append((.5+.45*u,.5+.45*v));dark.append(well**2)
        for j in range(count):faces.append((0,1+j,1+(j+1)%count))
        for row in range(rows-1):
            for j in range(count):
                a=1+row*count+j;b=1+row*count+(j+1)%count;faces.extend([(a,a+count,b),(b,a+count,b+count)])
        rear=len(points);points.append(C-N*.028);uv.append((.5,.5));dark.append(0.)
        for j in range(count):faces.append((rear,1+(rows-1)*count+(j+1)%count,1+(rows-1)*count+j))
        interior=C-N*.018
        for index,face in enumerate(faces):
            A,B,D=[points[k] for k in face]
            if np.cross(B-A,D-A)@((A+B+D)/3-interior)<0:faces[index]=tuple(reversed(face))
        mat=bpy.data.materials.new(slug+' paired-nostril nasal disc');mat.use_nodes=True;n=mat.node_tree.nodes;l=mat.node_tree.links;bs=n.get('Principled BSDF');bs.inputs['Roughness'].default_value=.55 if slug=='bear' else .47;bs.inputs['Specular IOR Level'].default_value=.30
        tex=n.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(repo/'artifacts/fauna-assets/textures/skin-nasal-lid-v1.png'),check_existing=True);tex.image.pack()
        tint=n.new('ShaderNodeHueSaturation');tint.inputs['Saturation'].default_value=.30;tint.inputs['Value'].default_value=.14 if slug=='bear' else .26;l.new(tex.outputs['Color'],tint.inputs['Color'])
        attr=n.new('ShaderNodeAttribute');attr.attribute_name='Nasal nostril';mix=n.new('ShaderNodeMixRGB');mix.inputs[2].default_value=(.003,.0025,.002,1);l.new(attr.outputs['Fac'],mix.inputs[0]);l.new(tint.outputs['Color'],mix.inputs[1]);l.new(mix.outputs[0],bs.inputs['Base Color'])
        bump=n.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.12;bump.inputs['Distance'].default_value=.0005;l.new(tex.outputs['Color'],bump.inputs['Height']);l.new(bump.outputs['Normal'],bs.inputs['Normal'])
        ob=obj(slug+' broad nasal disc with paired nostrils',points,faces,[mat],uvs=uv,anchor=sample(nose)[2]);attribute=ob.data.attributes.new('Nasal nostril','FLOAT','POINT');attribute.data.foreach_set('value',np.array(dark,np.float32))
        result['nasal_disc']={'object':ob.name,'width':w*2,'height':h*2,'method':'Closed broad disc covering selected single cavity, two recessed pigmented nostrils; original head vertices unchanged.'}
    return result
