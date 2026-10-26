"""Fit closed eye globes and lid bridges to actual openings in the chosen head.

Uses the approved deer construction: a buried ellipsoid, shaped aperture, fuller
upper hood and a bridge to the exact original skull boundary. Body vertices,
skin, shape keys and retained face UVs are preserved. Boundary copies receive
the same skin as their matching body vertices; this is geometric stitching,
not a claim that imported split-UV topology has become globally watertight.
"""
import json,math
import bpy,bmesh,numpy as np

def _mesh(name,points,faces,uvs):
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(points,[],faces);mesh.update();layer=mesh.uv_layers.new(name='UVMap')
    for loop in mesh.loops:layer.data[loop.index].uv=uvs[loop.vertex_index]
    for face in mesh.polygons:face.use_smooth=True
    return mesh

def _weights(ob,weights):
    for i,skin in enumerate(weights):
        for name,value in skin.items():
            group=ob.vertex_groups.get(name) or ob.vertex_groups.new(name=name);group.add([i],float(value),'REPLACE')

def prepare(body,patches,land,skin_sample,root,slug):
    mesh=body.data;p=np.array([v.co[:] for v in mesh.vertices]);faces=[list(f.vertices) for f in mesh.polygons];source_normals=np.array([v.normal[:] for v in mesh.vertices]);parent_attribute=mesh.attributes.get('Visual source face');source_parent_ids=[v.value for v in parent_attribute.data] if parent_attribute else None
    source_weights=[{body.vertex_groups[g.group].name:g.weight for g in v.groups} for v in mesh.vertices]
    keys=[tuple(x) for x in np.round(p,6)];edges={}
    for fi,face in enumerate(faces):
        for i,a in enumerate(face):
            b=face[(i+1)%len(face)];key=tuple(sorted((keys[a],keys[b])));edges.setdefault(key,[]).append((fi,a,b))
    from fauna_surface_components import components
    mid=(np.array(land['right_eye']['point'])+land['left_eye']['point'])/2;fleck_vertices=set()
    for part in components(p,np.array(faces)):
        points=p[list(part)]
        if len(part)<=20 and np.linalg.norm(points.mean(0)-mid)<.6 and np.ptp(points,axis=0).max()<.15:fleck_vertices.update(part)
    flecks={i for i,face in enumerate(faces) if all(v in fleck_vertices for v in face)}
    removed=set(flecks);proofs=[]
    from fauna_paint_recipes import family
    kind=family(slug);prey=slug in {'hare','rabbit'};bovid=kind in {'caprine','bovine','cervid','equine'} or slug=='buffalo'
    globe_vertical=.80 if prey else .70;globe_depth=.55;globe_offset=.40
    top,bottom,tilt=(.60,.53,.025) if prey else (.50,.40,.06) if slug=='red-fox' else (.55,.55,.02) if slug=='raccoon' else (.34,.34,.02) if kind=='ursid' else (.40,.35,.02) if bovid else (.30,.27,.02) if kind=='suine' else (.45,.36,.06)
    profile=land.get('anatomical_orbit',{});top=profile.get('aperture_top',top);bottom=profile.get('aperture_bottom',bottom);tilt=profile.get('aperture_tilt',tilt)
    for record in patches:
        side='right' if record['half']==0 else 'left';ob=record['object'];C=record['center'];H=record['horizontal'];V=record['vertical'];N=np.array(record.get('normal',np.cross(V,H)));N/=np.linalg.norm(N);R=record['radius'];rx=record['rx']
        centroids=np.array([p[f].mean(0) for f in faces]);rel=centroids-C
        q=((rel@H)/(rx*1.5))**2+((rel@V)/(R*.95))**2
        vertex_rel=p-C;vertex_q=((vertex_rel@H)/(rx*1.5))**2+((vertex_rel@V)/(R*.95))**2
        # Remove every triangle touching the aperture. A centroid test can leave
        # a long corner INSIDE the new lid, producing overlapping skin wedges.
        face_outward=source_normals[np.array(faces)].mean(1)@N
        touch=np.array([min(vertex_q[f])<1.05 for f in faces]);depth=rel@N;candidates=set(np.where(touch&(depth>-R*.8)&(depth<R*1.5)&(face_outward>.05))[0].tolist())-flecks;assert candidates,side+' has no orbital faces'
        adjacency={i:set() for i in candidates}
        for uses in edges.values():
            ids=[x[0] for x in uses if x[0] in candidates]
            for i in ids:adjacency[i].update(j for j in ids if j!=i)
        seed=min(candidates,key=lambda i:np.linalg.norm(centroids[i]-C));cut={seed};stack=[seed]
        while stack:
            for fi in adjacency[stack.pop()]:
                if fi not in cut:cut.add(fi);stack.append(fi)
        assert not (cut&(removed-flecks)),side+' orbital opening overlaps the other eye'
        boundary={};representatives={}
        for edge,uses in edges.items():
            inside=[u for u in uses if u[0] in cut];outside=[u for u in uses if u[0] not in cut]
            if len(inside)==1 and outside:
                a,b=edge;boundary.setdefault(a,set()).add(b);boundary.setdefault(b,set()).add(a)
                for _,vi,vj in outside:
                    representatives.setdefault(keys[vi],vi);representatives.setdefault(keys[vj],vj)
        assert len(boundary)>=5,(side,'No orbital boundary',len(cut),len(boundary))
        assert all(len(v)==2 for v in boundary.values()),(side,'Non-simple orbital boundary')
        angle=lambda key:math.atan2((np.array(key)-C)@V,(np.array(key)-C)@H)%(2*math.pi)
        loops=[];unvisited=set(boundary)
        while unvisited:
            start=min(unvisited,key=angle);loop=[start];previous=None;current=start
            for _ in range(len(boundary)+1):
                choices=[x for x in boundary[current] if x!=previous];nxt=choices[0]
                if nxt==start:break
                assert nxt not in loop,(side,'Self intersecting boundary graph')
                loop.append(nxt);previous,current=current,nxt
            unvisited.difference_update(loop);loops.append(loop)
        def planar(loop):return np.array([[(np.array(x)-C)@H,(np.array(x)-C)@V] for x in loop])
        def signed_area(loop):
            xy=planar(loop);return sum(np.cross(xy[i],xy[(i+1)%len(xy)]) for i in range(len(xy)))
        outer=max(loops,key=lambda loop:abs(signed_area(loop)));outer_xy=planar(outer)
        # Imported bodies may already have a smaller hole inside an orbital
        # region. The new skin bridge covers that enclosed inner boundary.
        # Disjoint openings still fail rather than silently spanning the face.
        for loop in loops:
            if loop is outer:continue
            for point in planar(loop):
                inside=False
                for i,A in enumerate(outer_xy):
                    B=outer_xy[(i+1)%len(outer_xy)]
                    if (A[1]>point[1])!=(B[1]>point[1]) and point[0]<(B[0]-A[0])*(point[1]-A[1])/(B[1]-A[1])+A[0]:inside=not inside
                assert inside,(side,'Disjoint orbital boundary loops require manual fitting')
        projected=np.array([[(np.array(x)-C)@H,(np.array(x)-C)@V] for x in outer]);area=sum(np.cross(projected[i],projected[(i+1)%len(outer)]) for i in range(len(outer)))
        if area<0:outer=[outer[0],*reversed(outer[1:])]
        boundary_ids=[representatives[key] for key in outer];outer_p=p[boundary_ids]
        lengths=np.linalg.norm(np.roll(outer_p,-1,axis=0)-outer_p,axis=1);arc=np.r_[0,np.cumsum(lengths)];arc=arc/arc[-1]*math.tau
        # Rebuild the eye as a closed, buried ellipsoid, not an oval surface cap.
        eye=bpy.data.objects[f'{slug} {side} eye'];old_materials=list(eye.data.materials);ev=[C+N*(R*globe_depth-R*globe_offset)];eu=[(.5,.5)];ef=[];segments=32;rows=12;Ri=R*(.82 if prey else .88)
        for row in range(1,rows):
            theta=math.pi*row/rows
            for col in range(segments):
                phi=math.tau*col/segments;u=math.sin(theta)*math.cos(phi)*R;v=math.sin(theta)*math.sin(phi)*R*globe_vertical;depth=math.cos(theta)*R*globe_depth-R*globe_offset
                ev.append(C+H*u+V*v+N*depth);eu.append((.5+.5*u/Ri,.5+.5*v/Ri))
        rear=len(ev);ev.append(C-N*(R*globe_depth+R*globe_offset));eu.append((.5,.5))
        for i in range(segments):ef.append((0,1+i,1+(i+1)%segments))
        for row in range(rows-2):
            for i in range(segments):
                a=1+row*segments+i;b=1+row*segments+(i+1)%segments;ef.extend([(a,b,b+segments),(a,b+segments,a+segments)])
        first=1+(rows-2)*segments
        for i in range(segments):ef.append((first+i,rear,first+(i+1)%segments))
        globe_center=C-N*R*globe_offset
        for i,face in enumerate(ef):
            A,B,D=[ev[j] for j in face]
            if np.cross(B-A,D-A)@((A+B+D)/3-globe_center)<0:ef[i]=tuple(reversed(face))
        eye.data=_mesh(slug+' fitted globe '+side,ev,ef,eu)
        for material in old_materials:eye.data.materials.append(material)
        _weights(eye,[skin_sample(C)[2]]*len(ev))
        # Three flesh rings, then an exact zipper to the existing skull boundary.
        verts=[];bare=[];triangles=[];count=32;medial=-1 if side=='right' else 1
        def depth_at(u,v):return R*globe_depth*math.sqrt(max(0.,1-(u/R)**2-(v/(R*globe_vertical))**2))-R*globe_offset
        for ring in range(3):
            for i in range(count):
                t=math.tau*i/count;c=math.cos(t);sn=math.sin(t);front=max(0,c*medial)**6
                if ring==0:
                    u=R*.90*c;v=R*(top if sn>=0 else bottom)*math.copysign(abs(sn)**(1 if sn>=0 else 1.1),sn)-medial*R*tilt*c;depth=depth_at(u,v)+R*.025
                elif ring==1:
                    u=R*1.08*c+medial*R*.12*front;v=R*(max(top+.15,.65) if sn>=0 else max(bottom+.12,.55))*math.copysign(abs(sn)**(1 if sn>=0 else 1.25),sn)-medial*R*tilt*c-R*.04*front
                    depth=depth_at(u,v)+R*((.075+.04*max(sn,0) if sn>=0 else .10) if slug in {'wolf','red-fox','hare','rabbit'} else (.16+.14*max(sn,0) if sn>=0 else .24))
                else:
                    fraction=i/count;k=min(len(outer_p)-1,int(np.searchsorted(arc/math.tau,fraction,side='right')-1));a0=arc[k]/math.tau;a1=arc[k+1]/math.tau;alpha=(fraction-a0)/max(a1-a0,1e-8);target=outer_p[k]*(1-alpha)+outer_p[(k+1)%len(outer_p)]*alpha
                    point=np.array(verts[count+i])*.35+target*.65+N*R*.015;verts.append(point);bare.append(0.);continue
                verts.append(C+H*u+V*v+N*depth);bare.append(1. if ring==0 else 0.)
        for ring in range(2):
            for i in range(count):
                j=(i+1)%count;a=ring*count+i;b=ring*count+j;triangles.extend([(a,a+count,b),(b,a+count,b+count)])
        boundary_start=len(verts);verts.extend(outer_p);bare.extend([0.]*len(outer_p));oi=ii=0
        while oi<len(outer_p) or ii<count:
            on=arc[oi+1] if oi<len(outer_p) else float('inf');it=math.tau*(ii+1)/count if ii<count else float('inf')
            if on<it:face=(boundary_start+oi%len(outer_p),boundary_start+(oi+1)%len(outer_p),2*count+ii%count);oi+=1
            else:face=(boundary_start+oi%len(outer_p),2*count+(ii+1)%count,2*count+ii%count);ii+=1
            if len(set(face))==3:triangles.append(face)
        for i,face in enumerate(triangles):
            A,B,D=[verts[j] for j in face]
            if np.cross(B-A,D-A)@N<0:triangles[i]=tuple(reversed(face))
        uv0=[(0.,0.)]*len(verts);materials=list(ob.data.materials);ob.data=_mesh(slug+' boundary-fitted orbital skin '+side,verts,triangles,uv0)
        for material in materials:ob.data.materials.append(material)
        marker=ob.data.attributes.new('Fitted lid weight','FLOAT','POINT');marker.data.foreach_set('value',np.ones(len(verts),np.float32));margin=ob.data.attributes.new('Fitted bare lid','FLOAT','POINT');margin.data.foreach_set('value',np.array(bare,np.float32))
        clone=ob.data.attributes.new('Fitted clone blend','FLOAT','POINT');clone.data.foreach_set('value',np.zeros(len(verts),np.float32))
        uv=ob.data.uv_layers.new(name='Orbital bake UV')
        for loop in ob.data.loops:
            rel=np.array(verts[loop.vertex_index])-C;uv.data[loop.index].uv=(record['half']*.5+(.5+float(rel@H)/(R*6))*.5,.5+float(rel@V)/(R*6))
        ob.data.uv_layers.active=ob.data.uv_layers['UVMap'];ob.data.uv_layers['UVMap'].active_render=True
        weights=[skin_sample(point)[2] for point in verts]
        for j,original_id in enumerate(boundary_ids):weights[boundary_start+j]=source_weights[original_id]
        _weights(ob,weights);removed.update(cut);proofs.append({'side':side,'boundary_body_vertices':boundary_ids,'boundary_skin_vertices':list(range(boundary_start,len(verts))),'skin_object':ob.name,'removed_faces':sorted(cut),'globe_radius':R,'enclosed_original_boundary_loops_covered':len(loops)-1})
        patch_normals=np.array([v.normal[:] for v in ob.data.vertices])
        for j,original_id in enumerate(boundary_ids):patch_normals[boundary_start+j]=source_normals[original_id]
        ob.data.normals_split_custom_set_from_vertices(patch_normals.tolist())
        # Lashes must grow from the FINAL lid, not the obsolete cap geometry.
        # The old independent placement could leave little blades on the brow.
        lash=bpy.data.objects.get(f'{slug} {side} subtle lashes and brow hairs')
        if lash:
            hair=[];hair_faces=[];length=R*(.23 if bovid else .16 if prey else .08 if kind=='ursid' else .12)
            for index in [3,6,9,12,15]:
                t=math.tau*index/count;start=np.array(verts[count+index]);direction=N*length*.65+V*length*.75+H*length*.18*math.cos(t);base=len(hair)
                for step in range(5):
                    q=step/4;point=start+direction*q+N*length*.12*math.sin(q*math.pi);width=R*.0025*(1-q)+R*.0001;hair.extend([point+H*width,point-H*width])
                for step in range(4):hair_faces.append((base+step*2,base+step*2+1,base+step*2+3,base+step*2+2))
            lash.data=_mesh(slug+' lid-attached lashes '+side,hair,hair_faces,[(0,0)]*len(hair));mat=bpy.data.materials.get(slug+' dark lash fibre') or bpy.data.materials.new(slug+' dark lash fibre');mat.use_nodes=True;bsdf=mat.node_tree.nodes.get('Principled BSDF');bsdf.inputs['Base Color'].default_value=(.008,.005,.003,1);bsdf.inputs['Roughness'].default_value=.75;lash.data.materials.append(mat);_weights(lash,[skin_sample(C)[2]]*len(hair))
    before=np.array([v.co[:] for v in body.data.vertices]);bm=bmesh.new();bm.from_mesh(body.data);bm.faces.ensure_lookup_table();bmesh.ops.delete(bm,geom=[bm.faces[i] for i in sorted(removed)],context='FACES_ONLY');bm.to_mesh(body.data);bm.free();body.data.update()
    assert np.array_equal(before,np.array([v.co[:] for v in body.data.vertices])),'Body vertex order changed'
    body.data.normals_split_custom_set_from_vertices(source_normals.tolist())
    # BMesh conversion can clear every render-active UV flag when the source
    # carries multiple UV layers. Tangent-space shading then has no UV basis.
    body.data.uv_layers.active=body.data.uv_layers['UVMap'];body.data.uv_layers['UVMap'].active_render=True
    report={'scope':'Original orbital faces replaced by boundary-fitted lid bridges and buried ellipsoidal eyes; original body vertex prefix, interpolated refinement data, rig and animation tracks retained. Tiny detached head debris faces removed. Boundary copies match body geometry/skin; no global watertight claim.','removed_polygon_indices':sorted(removed),'removed_fleck_polygon_indices':sorted(flecks),'removed_orbital_polygon_indices':sorted(removed-flecks),'eyes':proofs}
    if source_parent_ids is not None:report['removed_source_parent_polygons']=sorted({source_parent_ids[i] for i in removed})
    (root/'eye-retopology.json').write_text(json.dumps(report,indent=2)+'\n');return report
