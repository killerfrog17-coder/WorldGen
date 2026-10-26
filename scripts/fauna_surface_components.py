"""Geometric surface connectivity without welding or changing a selected mesh.

Some imported low-poly bodies split vertices at every face/normal boundary.
Index-only connectivity mistakes their real head for detached debris. Treat
coincident positions as connected for anatomical sampling, keeping actual
vertices, UVs, weights and animation completely unchanged.
"""
import numpy as np

def components(points,faces):
    adjacency=[set() for _ in points]
    for face in faces:
        for vertex in face:adjacency[int(vertex)].update(int(i) for i in face if i!=vertex)
    coincident={}
    for i,point in enumerate(np.round(points,6)):coincident.setdefault(tuple(point),[]).append(i)
    for indices in coincident.values():
        if len(indices)>1:
            first=indices[0]
            for vertex in indices[1:]:adjacency[first].add(vertex);adjacency[vertex].add(first)
    remaining=set(range(len(points)));result=[]
    while remaining:
        seed=remaining.pop();part={seed};stack=[seed]
        while stack:
            for vertex in adjacency[stack.pop()]:
                if vertex in remaining:remaining.remove(vertex);part.add(vertex);stack.append(vertex)
        result.append(part)
    return result

def averaged_split_normals(mesh):
    """Return continuous projection/shading normals across imported UV splits."""
    points=np.array([v.co[:] for v in mesh.vertices]);normal=np.array([v.normal[:] for v in mesh.vertices])
    groups={}
    for i,point in enumerate(np.round(points,6)):groups.setdefault(tuple(point),[]).append(i)
    changed=0
    for ids in groups.values():
        if len(ids)<2:continue
        mean=normal[ids].sum(0);length=np.linalg.norm(mean)
        if length>1e-8:normal[ids]=mean/length;changed+=len(ids)
    return normal,changed

def smooth_split_normals(mesh):
    """Average exact-position shading normals, retaining split skin vertices."""
    normal,changed=averaged_split_normals(mesh)
    if changed:mesh.normals_split_custom_set_from_vertices(normal.tolist())
    return changed
