"""Refine only orbital neighborhoods, interpolating existing mesh custom data."""
import bpy,bmesh,numpy as np
from fauna_surface_components import components

def refine(body,land,slug):
    mesh=body.data;before=np.array([v.co[:] for v in mesh.vertices]);old_count=len(before)
    source=mesh.attributes.new('Visual source face','INT','FACE');source.data.foreach_set('value',np.arange(len(mesh.polygons),dtype=np.int32))
    bm=bmesh.new();bm.from_mesh(mesh);passes=2 if slug in {'rabbit','lion','tiger','jaguar','leopard','puma','ocelot'} else 1
    body_faces=np.array([list(f.vertices) for f in mesh.polygons]);main=max(components(before,body_faces),key=len);marker=bm.verts.layers.int.new('Refine main surface');bm.verts.index_update()
    for vertex in bm.verts:vertex[marker]=int(vertex.index in main)
    selected_count=0
    for _ in range(passes):
        near=[]
        for edge in bm.edges:
            if not all(v[marker] for v in edge.verts):continue
            point=np.array((edge.verts[0].co+edge.verts[1].co)*.5)
            for side in ['right','left']:
                eye=land[side+'_eye'];frame=eye['fit_frame'];H,V,N=[np.array(frame[k]) for k in ['horizontal','vertical','normal']];rel=point-np.array(eye['point']);R=eye['radius'][0]
                if (rel@H/(R*3.1))**2+(rel@V/(R*2.8))**2<1 and abs(rel@N)<R*2.8:
                    near.append(edge);break
        if near:bmesh.ops.subdivide_edges(bm,edges=near,cuts=2,use_grid_fill=True)
        selected_count+=len(near)
    bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free();mesh.update()
    after=np.array([v.co[:] for v in mesh.vertices]);assert np.array_equal(before,after[:old_count]),'Refinement changed existing vertex coordinates/order'
    return {'method':'Local bmesh edge subdivision with interpolated original UV/deform/shape-key data; original vertex prefix unchanged. Source polygon ID retained on each child.','source_vertices':old_count,'added_vertices':len(after)-old_count,'selected_edges_across_passes':selected_count,'passes':passes,'appearance_approved':False}
