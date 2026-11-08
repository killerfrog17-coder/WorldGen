using System;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    bool ReviewCliffWall(out Vector3 target,out Vector3 outward)
    {
        target=outward=default;
        var candidates=scatterChunks.Values.Where(c=>c.Roots!=null)
            .SelectMany(c=>System.Linq.Enumerable.Range(0,c.Source.Instances.Length).Where(i=>c.Source.Instances[i].Kind==ScatterKind.Outcrop&&CliffRocks.Is(c.Source.Instances[i].Variant)).Select(i=>c.Source.Instances[i]))
            .OrderBy(p=>(p.Position-frame.Origin).Length).Take(24);
        foreach(var source in candidates)foreach(int sign in new[]{-1,1})
        {
            V across=V.Cross(source.Position,source.Normal).Unit;
            V at=(source.Position+across*(sign*Math.Max(24,source.WidthM*.9)/(terrain.RadiusM*HorizontalScale))).Unit;
            var patch=terrain.Patch(at,12000);var sample=patch.Sample(at);if(sample.Underwater)continue;
            V e=V.Cross(Math.Abs(at.Z)<.99?new V(0,0,1):new V(0,1,0),at).Unit,n=V.Cross(at,e).Unit;double step=4/(terrain.RadiusM*HorizontalScale);
            double sx=(patch.Elevation((at+e*step).Unit)-patch.Elevation((at-e*step).Unit))/8;
            double sz=(patch.Elevation((at+n*step).Unit)-patch.Elevation((at-n*step).Unit))/8;
            V normal=(at-e*sx-n*sz).Unit;if(V.Dot(normal,at)>.65)continue;
            TerrainHandoff.Coordinates(frame,at,out double x,out double z);
            var point=new Vector3((float)(x-originX),(float)frame.SurfaceY(at,sample.ElevationM)+.5f,(float)(z-originZ));
            var face=new Vector3((float)V.Dot(normal,frame.East),(float)V.Dot(normal,frame.Origin),(float)V.Dot(normal,frame.North)).normalized;
            var eye=point+face*70+Vector3.up*15;bool clear=true;
            for(int i=0;i<16;i++)
            {
                var q=Vector3.Lerp(eye,point,i/16f);var p=frame.Position(q.x+originX,q.z+originZ);
                if(q.y-frame.SurfaceY(p,patch.Elevation(p))<.8){clear=false;break;}
            }
            if(!clear)continue;target=point;outward=face;return true;
        }
        return false;
    }
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
