"""CPU evaluation of the retained fauna pigment graphs into their runtime UVs.

The selected natives use image/UV/attribute/mix/math/HSV nodes for pigment.
This avoids Cycles' intermittent OIIO bake crashes on the repaired UV layers.
No lighting, inference or model painting is performed.
"""
import numpy as np
import bpy

def linear(x):return np.where(x<=.04045,x/12.92,((x+.055)/1.055)**2.4)
def srgb(x):return np.where(x<=.0031308,x*12.92,1.055*np.maximum(0,x)**(1/2.4)-.055)
def colour(x,n):
    x=np.asarray(x)
    if x.ndim==0:return np.full((n,3),float(x))
    if x.ndim==1:return np.tile(x[:3],(n,1)) if len(x) in {3,4} else np.repeat(x[:,None],3,axis=1)
    return np.repeat(x,3,axis=1) if x.shape[1]==1 else x[:,:3]
def scalar(x,n):
    x=np.asarray(x)
    if x.ndim==0:return np.full(n,float(x))
    if x.ndim==1:return x if len(x)==n else np.full(n,float(x[0]))
    return x[:,0] if x.shape[1]==1 else x[:,:3]@np.array([.2126,.7152,.0722])

def hsv_adjust(rgb,hue,saturation,value):
    high=rgb.max(1);low=rgb.min(1);delta=high-low;s=delta/np.maximum(high,1e-12);h=np.zeros(len(rgb))
    valid=delta>1e-10
    for i in range(3):
        mask=valid&(rgb.argmax(1)==i)
        h[mask]=((rgb[mask,(i+1)%3]-rgb[mask,(i+2)%3])/delta[mask]+2*i)/6
    h=(h+hue-.5)%1;s=np.maximum(0,s*saturation);v=high*value
    sector=np.floor(h*6).astype(int);f=h*6-sector;p=v*(1-s);q=v*(1-s*f);t=v*(1-s*(1-f))
    choices=[np.c_[v,t,p],np.c_[q,v,p],np.c_[p,v,t],np.c_[p,q,v],np.c_[t,p,v],np.c_[v,p,q]]
    result=np.zeros_like(rgb)
    for i in range(6):result[sector%6==i]=choices[i][sector%6==i]
    return result

class Surface:
    def __init__(self,objects,size):
        self.objects=objects;self.size=size;self.groups=[];self.images={};self.uv_cache={};self.attribute_cache={}
        for pi,ob in enumerate(objects):
            mesh=ob.data;mesh.calc_loop_triangles();target=mesh.uv_layers['RuntimeUV']
            mats={}
            for tri in mesh.loop_triangles:
                loops=np.array(tri.loops);xy=np.array([target.data[int(i)].uv[:] for i in loops])*size-.5
                lo=np.maximum(np.ceil(xy.min(0)).astype(int),0);hi=np.minimum(np.floor(xy.max(0)).astype(int),size-1)
                if np.any(lo>hi):lo=hi=np.clip(np.rint(xy.mean(0)),0,size-1).astype(int)
                A,B,C=xy;den=(B[1]-C[1])*(A[0]-C[0])+(C[0]-B[0])*(A[1]-C[1])
                if abs(den)<1e-12:continue
                yy,xx=np.mgrid[lo[1]:hi[1]+1,lo[0]:hi[0]+1]
                a=((B[1]-C[1])*(xx-C[0])+(C[0]-B[0])*(yy-C[1]))/den
                b=((C[1]-A[1])*(xx-C[0])+(A[0]-C[0])*(yy-C[1]))/den;c=1-a-b
                inside=(a>=-1e-7)&(b>=-1e-7)&(c>=-1e-7)
                if not inside.any():
                    # Retain a shading sample for thin lashes/whiskers whose
                    # nondegenerate bake triangle falls between pixel centres.
                    point=np.clip(np.rint(xy.mean(0)),0,size-1).astype(int)
                    pixels=np.array([point[1]*size+point[0]]);bary=np.array([[1/3,1/3,1/3]])
                else:pixels=yy[inside]*size+xx[inside];bary=np.c_[a[inside],b[inside],c[inside]]
                count=len(pixels);record=mats.setdefault(tri.material_index,{'pixels':[],'loops':[],'vertices':[],'bary':[],'triangles':[]})
                record['pixels'].append(pixels);record['loops'].append(np.tile(loops,(count,1)))
                record['vertices'].append(np.tile(np.array(tri.vertices),(count,1)));record['bary'].append(bary)
                record['triangles'].append(np.full(count,tri.index))
            for mi,record in mats.items():
                self.groups.append({'object':pi,'material':mi,**{k:np.concatenate(v) for k,v in record.items()}})
    def uv(self,group,name=None):
        mesh=self.objects[group['object']].data
        layer=mesh.uv_layers.get(name) if name else next((u for u in mesh.uv_layers if u.active_render),mesh.uv_layers.active)
        if layer is None:return np.full((len(group['pixels']),2),.5)
        key=(group['object'],group['material'],layer.name)
        if key not in self.uv_cache:
            values=np.empty(len(layer.data)*2,np.float32);layer.data.foreach_get('uv',values)
            self.uv_cache[key]=np.sum(values.reshape(-1,2)[group['loops']]*group['bary'][:,:,None],axis=1)
        return self.uv_cache[key]
    def attribute(self,group,name):
        mesh=self.objects[group['object']].data;attr=mesh.attributes.get(name) or mesh.color_attributes.get(name)
        count=len(group['pixels'])
        if attr is None:return np.zeros(count)
        key=(group['object'],group['material'],name)
        if key not in self.attribute_cache:
            field='color' if attr.data_type in {'FLOAT_COLOR','BYTE_COLOR'} else 'vector' if attr.data_type=='FLOAT_VECTOR' else 'value'
            width=4 if field=='color' else 3 if field=='vector' else 1
            values=np.empty(len(attr.data)*width,np.float32);attr.data.foreach_get(field,values);values=values.reshape(-1,width)
            indices=group['loops'] if attr.domain=='CORNER' else group['vertices']
            self.attribute_cache[key]=np.sum(values[indices]*group['bary'][:,:,None],axis=1)
        return self.attribute_cache[key]
    def image(self,node,uv,output):
        image=node.image
        if image is None:raise ValueError('Missing native image: '+node.name)
        if image.name not in self.images:
            pixels=np.empty(len(image.pixels),np.float32);image.pixels.foreach_get(pixels);pixels=pixels.reshape(image.size[1],image.size[0],4)
            if image.colorspace_settings.name=='sRGB' and not image.is_float:pixels[:,:,:3]=linear(np.clip(pixels[:,:,:3],0,1))
            self.images[image.name]=pixels
        pixels=self.images[image.name];height,width=pixels.shape[:2]
        coords=np.mod(uv[:,:2],1) if node.extension=='REPEAT' else np.clip(uv[:,:2],0,1)
        xy=coords*[width,height]-.5;lo=np.floor(xy).astype(int);t=xy-lo;hi=lo+1
        if node.extension=='REPEAT':lo%=np.array([width,height]);hi%=np.array([width,height])
        else:lo=np.clip(lo,[0,0],[width-1,height-1]);hi=np.clip(hi,[0,0],[width-1,height-1])
        result=pixels[lo[:,1],lo[:,0]]*(1-t[:,0,None])*(1-t[:,1,None])+pixels[lo[:,1],hi[:,0]]*t[:,0,None]*(1-t[:,1,None])+pixels[hi[:,1],lo[:,0]]*(1-t[:,0,None])*t[:,1,None]+pixels[hi[:,1],hi[:,0]]*t[:,0,None]*t[:,1,None]
        return result[:,3] if output=='Alpha' else result[:,:3]
    def evaluate(self,group,material,socket,cache):
        n=len(group['pixels'])
        if not socket.is_linked:return socket.default_value
        link=socket.links[0];node=link.from_node;output=link.from_socket.name;key=(node.name,output)
        if key in cache:return cache[key]
        def value(i):return self.evaluate(group,material,node.inputs[i],cache)
        if node.type=='TEX_IMAGE':
            uv=value('Vector') if node.inputs['Vector'].is_linked else self.uv(group)
            result=self.image(node,np.asarray(uv),output)
        elif node.type=='UVMAP':result=self.uv(group,node.uv_map)
        elif node.type in {'ATTRIBUTE','VERTEX_COLOR'}:
            result=self.attribute(group,node.attribute_name if node.type=='ATTRIBUTE' else node.layer_name)
            if output=='Fac':result=scalar(result,n)
            elif output=='Alpha':result=result[:,3] if np.asarray(result).ndim>1 and result.shape[1]>3 else np.ones(n)
        elif node.type=='MATH':
            a=scalar(value(0),n);b=scalar(value(1),n);op=node.operation
            if op=='MULTIPLY':result=a*b
            elif op=='MULTIPLY_ADD':result=a*b+scalar(value(2),n)
            elif op=='ADD':result=a+b
            elif op=='SUBTRACT':result=a-b
            elif op=='DIVIDE':result=a/np.where(np.abs(b)>1e-10,b,1)
            elif op=='MINIMUM':result=np.minimum(a,b)
            elif op=='MAXIMUM':result=np.maximum(a,b)
            elif op=='LESS_THAN':result=(a<b).astype(float)
            elif op=='GREATER_THAN':result=(a>b).astype(float)
            elif op=='POWER':result=np.maximum(0,a)**b
            elif op=='ABSOLUTE':result=np.abs(a)
            elif op=='SINE':result=np.sin(a)
            elif op=='COSINE':result=np.cos(a)
            elif op=='SQRT':result=np.sqrt(np.maximum(0,a))
            else:raise ValueError('Unsupported native material math: '+op)
            if node.use_clamp:result=np.clip(result,0,1)
        elif node.type=='MIX_RGB':
            f=np.clip(scalar(value(0),n),0,1)[:,None];a=colour(value(1),n);b=colour(value(2),n);mode=node.blend_type
            if mode=='MIX':mixed=b
            elif mode=='MULTIPLY':mixed=a*b
            elif mode=='ADD':mixed=a+b
            elif mode=='SUBTRACT':mixed=a-b
            elif mode=='SCREEN':mixed=1-(1-a)*(1-b)
            elif mode=='DARKEN':mixed=np.minimum(a,b)
            elif mode=='LIGHTEN':mixed=np.maximum(a,b)
            else:raise ValueError('Unsupported native pigment mix: '+mode)
            result=a*(1-f)+mixed*f
            if node.use_clamp:result=np.clip(result,0,1)
        elif node.type=='HUE_SAT':
            a=colour(value('Color'),n);f=np.clip(scalar(value('Fac'),n),0,1)[:,None]
            result=a*(1-f)+hsv_adjust(a,scalar(value('Hue'),n),scalar(value('Saturation'),n),scalar(value('Value'),n))*f
        elif node.type in {'RGB','VALUE'}:result=node.outputs[0].default_value
        else:raise ValueError('Unsupported native pigment node: '+node.type)
        cache[key]=result;return result
    def bake(self,kind,path,materials):
        size=self.size;image=np.zeros((size*size,4),np.float32);image[:,3]=1;known=np.zeros(size*size,bool)
        if kind=='mask':image[:,:3]=(.65,1,0)
        elif kind=='normal':image[:,:3]=(.5,.5,1)
        for group in self.groups:
            material=materials[group['object']][group['material']];n=len(group['pixels']);cache={}
            bsdf=next((node for node in material.node_tree.nodes if node.type=='BSDF_PRINCIPLED'),None) if material and material.use_nodes else None
            def get(name,default):return self.evaluate(group,material,bsdf.inputs[name],cache) if bsdf and name in bsdf.inputs else default
            if kind=='colour':values=colour(get('Base Color',material.diffuse_color if material else (.5,.5,.5,1)),n);values=srgb(np.clip(values,0,1))
            elif kind=='mask':values=np.c_[scalar(get('Roughness',.6),n),scalar(get('Alpha',1),n),np.clip(scalar(get('Emission Strength',0),n),0,1)]
            else:
                normal=next((node for node in material.node_tree.nodes if node.type=='NORMAL_MAP'),None) if material and material.use_nodes else None
                if normal:
                    vector=colour(self.evaluate(group,material,normal.inputs['Color'],cache),n)*2-1
                    strength=np.clip(scalar(self.evaluate(group,material,normal.inputs['Strength'],cache),n),0,1)
                    vector[:,:2]*=strength[:,None];vector[:,2]=1+(vector[:,2]-1)*strength
                    vector/=np.maximum(np.linalg.norm(vector,axis=1)[:,None],1e-8);values=vector*.5+.5
                else:values=np.tile([.5,.5,1.],(n,1))
            image[group['pixels'],:3]=values;known[group['pixels']]=True
        image=image.reshape(size,size,4);known=known.reshape(size,size)
        # Two pixels of padding keep mip filtering away from black atlas gaps.
        for _ in range(3):
            total=np.zeros_like(image);counts=np.zeros((size,size))
            for axis,shift in [(0,-1),(0,1),(1,-1),(1,1)]:
                valid=np.roll(known,shift,axis);total+=np.roll(image,shift,axis)*valid[:,:,None];counts+=valid
            fill=(~known)&(counts>0);image[fill]=total[fill]/counts[fill,None];known[fill]=True
        assert np.isfinite(image).all()
        target=bpy.data.images.new('CPU native surface '+kind,width=size,height=size,alpha=True,float_buffer=True)
        target.colorspace_settings.name='Non-Color';target.pixels.foreach_set(image.ravel());target.file_format='PNG';target.filepath_raw=str(path);target.save()
        return int(known.sum())
