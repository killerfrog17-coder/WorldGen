using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Read-only copy of exported skin channels. Broad phase comes first;
/// a posed surface is evaluated only for an actual nearby attack/projectile.</summary>
public sealed class FaunaCollisionSurface
{
    public readonly Vector3[] Vertices,Morph;public readonly Vector2[] Uv;public readonly int[] Triangles;
    public readonly Vector4[] Joints,Weights,ExtraJoints,ExtraWeights;public readonly int[] Parts;
    public FaunaCollisionSurface(Mesh mesh)
    {
        if(!mesh.isReadable)throw new InvalidOperationException("Wildlife collision requires the original readable mesh.");
        Vertices=mesh.vertices;Uv=mesh.uv;Triangles=mesh.triangles;
        Vector4[] Read(int channel){var values=new List<Vector4>();mesh.GetUVs(channel,values);return values.Count==Vertices.Length?values.ToArray():new Vector4[Vertices.Length];}
        Joints=Read(2);Weights=Read(3);ExtraJoints=Read(5);ExtraWeights=Read(6);var parts=Read(4);Parts=new int[Vertices.Length];for(int i=0;i<Parts.Length;i++)Parts[i]=(int)parts[i].x;
        var morph=new List<Vector3>();mesh.GetUVs(7,morph);Morph=morph.Count==Vertices.Length?morph.ToArray():new Vector3[Vertices.Length];
    }
    public void Pose(Matrix4x4[] palette,int offset,Matrix4x4 root,float morph,Vector3[] output)
    {
        for(int i=0;i<Vertices.Length;i++)
        {
            Vector3 p=Vertices[i]+Morph[i]*morph,result=Vector3.zero;
            for(int j=0;j<4;j++){float weight=Weights[i][j];if(weight>0)result+=palette[offset+(int)Joints[i][j]].MultiplyPoint3x4(p)*weight;}
            for(int j=0;j<4;j++){float weight=ExtraWeights[i][j];if(weight>0)result+=palette[offset+(int)ExtraJoints[i][j]].MultiplyPoint3x4(p)*weight;}
            output[i]=root.MultiplyPoint3x4(result);
        }
    }
    public bool Visible(int vertex,Vector4 masks)
    {int part=Parts[vertex];return part>=0&&part<64&&(((uint)masks[part/16]>>(part%16))&1u)!=0;}
    public bool Raycast(Ray ray,Vector3[] posed,Vector4 masks,float maxDistance,out float distance,out int face,out Vector3 normal,out Vector2 uv)
    {
        distance=maxDistance;face=-1;normal=Vector3.up;uv=Vector2.zero;
        for(int i=0;i<Triangles.Length;i+=3)
        {
            int a=Triangles[i],b=Triangles[i+1],c=Triangles[i+2];if(!Visible(a,masks))continue;
            if(!RayTriangle(ray,posed[a],posed[b],posed[c],distance,out float t,out float u,out float v))continue;
            distance=t;face=i/3;normal=Vector3.Cross(posed[b]-posed[a],posed[c]-posed[a]).normalized;if(Vector3.Dot(normal,ray.direction)>0)normal=-normal;
            uv=Uv[a]*(1-u-v)+Uv[b]*u+Uv[c]*v;
        }
        return face>=0;
    }
    public static bool RayTriangle(Ray ray,Vector3 a,Vector3 b,Vector3 c,float maximum,out float distance,out float u,out float v)
    {
        distance=u=v=0;Vector3 e1=b-a,e2=c-a,p=Vector3.Cross(ray.direction,e2);float determinant=Vector3.Dot(e1,p);if(Mathf.Abs(determinant)<1e-9f)return false;
        float inverse=1/determinant;Vector3 t=ray.origin-a;u=Vector3.Dot(t,p)*inverse;if(u<0||u>1)return false;Vector3 q=Vector3.Cross(t,e1);v=Vector3.Dot(ray.direction,q)*inverse;if(v<0||u+v>1)return false;
        distance=Vector3.Dot(e2,q)*inverse;return distance>=0&&distance<=maximum;
    }
}
