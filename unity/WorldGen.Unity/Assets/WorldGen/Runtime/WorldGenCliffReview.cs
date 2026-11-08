using System;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    // Opt-in native review only. Queries the same nonconvex meshes and nearby
    // collider installation that ordinary player movement uses.
    bool ReviewCliffRoof(ScatterChunk chunk,int index,out Vector3 roof,out Vector3 eye)
    {
        roof=eye=default;var source=chunk.Source.Instances[index];
        var center=chunk.Matrices[index].MultiplyPoint3x4(new Vector3(0,.5f,0));
        RefreshPlayerSolids(center,center);
        if(!scatterSolids.TryGetValue(source.Id,out var solid))return false;
        var patch=terrain.Patch(source.Position,5000);int probes=0;
        foreach(var collider in solid.Root.GetComponentsInChildren<MeshCollider>())
        {
            var mesh=collider.sharedMesh;var vertices=mesh.vertices;var triangles=mesh.triangles;
            for(int i=0;i<triangles.Length;i+=3)
            {
                var a=collider.transform.TransformPoint(vertices[triangles[i]]);var b=collider.transform.TransformPoint(vertices[triangles[i+1]]);var c=collider.transform.TransformPoint(vertices[triangles[i+2]]);
                var face=Vector3.Cross(b-a,c-a).normalized;
                var outward=collider.transform.TransformDirection(Vector3.forward);
                if(Vector3.Dot(face,transform.up)>-.15f||Vector3.Dot(face,outward)<.1f)continue;
                if((vertices[triangles[i]].z+vertices[triangles[i+1]].z+vertices[triangles[i+2]].z)/3<.07f)continue;
                var candidate=(a+b+c)/3;var local=transform.InverseTransformPoint(candidate);
                var at=frame.Position(local.x+originX,local.z+originZ);double floor=frame.SurfaceY(at,patch.Elevation(at));
                if(local.y-floor<2.3)continue;
                var outside=candidate+face*Mathf.Clamp((float)source.WidthM*.15f,2,6);
                var outsideLocal=transform.InverseTransformPoint(outside);var outsideAt=frame.Position(outsideLocal.x+originX,outsideLocal.z+originZ);
                double outsideFloor=frame.SurfaceY(outsideAt,patch.Elevation(outsideAt));
                if(outsideLocal.y-outsideFloor<1.65)continue;
                var ray=new Ray(outside,(candidate-outside).normalized);float distance=(candidate-outside).magnitude;
                if(collider.Raycast(ray,out var hit,distance+.1f)&&(hit.point-candidate).sqrMagnitude<.01f)
                {roof=local;eye=outsideLocal;return true;}
                if(++probes>128)return false;
            }
        }
        return false;
    }
}
