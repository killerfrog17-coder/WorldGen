"""Independent source-surface proof for local topology refinement."""
import numpy as np

def verify(old,new,retopology):
    count=len(old['points']);assert len(new['points'])>=count
    assert old['weights']==new['weights'][:count],'Original source skin changed'
    original_uv_names=[x[0] for x in old['uv']];new_original_uv=[row for row in new['uv'] if row[0] in original_uv_names]
    assert original_uv_names==[x[0] for x in new_original_uv],'Original UV layers changed'
    assert set(x[0] for x in new['uv'])-set(original_uv_names)<={'Fauna Paint UV'},'Unexpected new UV layer'
    source=new['points'][:count];area=np.zeros(len(old['faces']));parents=new['source_face_ids'];assert len(parents)==len(new['faces'])
    max_uv=max_skin=max_shape=0.;checked=set()
    old_weights=[dict(skin) for skin in old['weights']];new_weights=[dict(skin) for skin in new['weights']]
    for fi,face in enumerate(new['faces']):
        parent=parents[fi];assert 0<=parent<len(old['faces']);original=old['faces'][parent];assert len(original)==3
        A,B,C=source[original];U=B-A;V=C-A;matrix=np.array([[U@U,U@V],[U@V,V@V]]);points=new['points'][face]
        if np.linalg.norm(np.cross(U,V))<1e-12:
            assert face==original;continue
        bc=np.linalg.solve(matrix,np.stack([(points-A)@U,(points-A)@V]));bary=np.stack([1-bc.sum(0),bc[0],bc[1]],axis=1)
        assert bary.min()>-2e-5 and bary.max()<1+2e-5,'Refinement escaped source triangle'
        assert np.max(np.abs(bary@source[original]-points))<2e-6,'Refinement changed source surface'
        area[parent]+=np.linalg.norm(np.cross(points[1]-points[0],points[2]-points[0]))*.5
        for (_,old_uv),(_,new_uv) in zip(old['uv'],new_original_uv):
            error=float(np.max(np.abs(bary@old_uv[parent]-new_uv[fi])));max_uv=max(max_uv,error);assert error<3e-5,'Refined UV interpolation differs'
        for corner,vertex in enumerate(face):
            if vertex<count or vertex in checked:continue
            checked.add(vertex);expected={}
            for source_vertex,factor in zip(original,bary[corner]):
                for bone,weight in old_weights[source_vertex].items():expected[bone]=expected.get(bone,0)+float(factor)*weight
            actual=new_weights[vertex]
            error=max(abs(expected.get(bone,0)-actual.get(bone,0)) for bone in expected.keys()|actual.keys());max_skin=max(max_skin,error);assert error<2e-5,'Refined skin interpolation differs'
            for name,key in new['shapes'].items():
                expected_shape=bary[corner]@key[original];error=float(np.max(np.abs(expected_shape-key[vertex])));max_shape=max(max_shape,error);assert error<2e-6,'Refined shape interpolation differs'
    permitted=set(retopology['removed_source_parent_polygons'])
    for fi,face in enumerate(old['faces']):
        p=source[face];expected=np.linalg.norm(np.cross(p[1]-p[0],p[2]-p[0]))*.5
        assert area[fi]<=expected+2e-7,'Refinement duplicated surface'
        if fi not in permitted:assert abs(area[fi]-expected)<2e-7,'Surface lost outside documented openings/debris'
    return {'original_vertex_count':count,'added_vertices':len(new['points'])-count,'checked_used_new_vertices':len(checked),'max_uv_interpolation_error':max_uv,'max_skin_interpolation_error':max_skin,'max_shape_interpolation_error':max_shape,'source_surface_preserved_except_documented_openings_and_debris':True}
