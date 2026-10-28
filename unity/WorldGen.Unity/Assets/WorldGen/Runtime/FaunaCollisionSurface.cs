using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;

/// <summary>Read-only copy of exported skin channels. Broad phase comes first;
/// a posed surface is evaluated only for an actual nearby attack/projectile.</summary>
public sealed class FaunaCollisionSurface
{
    public readonly Vector3[] Vertices,Morph;public readonly Vector2[] Uv;public readonly int[] Triangles;
    public readonly Vector4[] Joints,Weights,ExtraJoints,ExtraWeights;public readonly int[] Parts;
    public FaunaCollisionSurface(string slug,string sourceSha,int expectedBones,string dataRoot=null)
    {
        using var file=File.OpenRead(Path.Combine(dataRoot??Path.Combine(Application.streamingAssetsPath,"fauna-collision"),slug+".bin.gz"));
        using var zip=new GZipStream(file,CompressionMode.Decompress);using var r=new BinaryReader(zip);
        if(new string(r.ReadChars(8))!="WGHITS01"||BitConverter.ToString(r.ReadBytes(32)).Replace("-","").ToLowerInvariant()!=sourceSha.ToLowerInvariant())throw new InvalidDataException("Fauna hit-skin provenance mismatch: "+slug);
        int count=r.ReadInt32(),indices=r.ReadInt32(),bones=r.ReadInt32(),parts=r.ReadInt32(),influences=r.ReadInt32(),hasMorph=r.ReadInt32();
        if(count<3||count>200000||indices<3||indices>2000000||bones!=expectedBones||parts<1||parts>64||(influences!=4&&influences!=8))throw new InvalidDataException("Fauna hit-skin counts");
        Vertices=new Vector3[count];Uv=new Vector2[count];Morph=new Vector3[count];Parts=new int[count];Joints=new Vector4[count];Weights=new Vector4[count];ExtraJoints=new Vector4[count];ExtraWeights=new Vector4[count];
        Vector4 Four()=>new Vector4(r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
        for(int i=0;i<count;i++)
        {
            Vertices[i]=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());Uv[i]=new Vector2(r.ReadSingle(),r.ReadSingle());
            Joints[i]=Four();if(influences==8)ExtraJoints[i]=Four();Weights[i]=Four();if(influences==8)ExtraWeights[i]=Four();Parts[i]=(int)r.ReadSingle();
            if(hasMorph!=0)Morph[i]=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
        }
        Triangles=new int[indices];for(int i=0;i<indices;i++){Triangles[i]=r.ReadInt32();if(Triangles[i]<0||Triangles[i]>=count)throw new InvalidDataException("Fauna hit triangle");}
        if(r.Read()!=-1)throw new InvalidDataException("Trailing fauna hit data");
    }
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
    public static Bounds WorldBounds(Bounds b,Matrix4x4 m)
    {
        var e=b.extents;var extent=new Vector3(Mathf.Abs(m.m00)*e.x+Mathf.Abs(m.m01)*e.y+Mathf.Abs(m.m02)*e.z,Mathf.Abs(m.m10)*e.x+Mathf.Abs(m.m11)*e.y+Mathf.Abs(m.m12)*e.z,Mathf.Abs(m.m20)*e.x+Mathf.Abs(m.m21)*e.y+Mathf.Abs(m.m22)*e.z);
        return new Bounds(m.MultiplyPoint3x4(b.center),extent*2);
    }
    public static bool RayTriangle(Ray ray,Vector3 a,Vector3 b,Vector3 c,float maximum,out float distance,out float u,out float v)
    {
        distance=u=v=0;Vector3 e1=b-a,e2=c-a,p=Vector3.Cross(ray.direction,e2);float determinant=Vector3.Dot(e1,p);if(Mathf.Abs(determinant)<1e-9f)return false;
        float inverse=1/determinant;Vector3 t=ray.origin-a;u=Vector3.Dot(t,p)*inverse;if(u<0||u>1)return false;Vector3 q=Vector3.Cross(t,e1);v=Vector3.Dot(ray.direction,q)*inverse;if(v<0||u+v>1)return false;
        distance=Vector3.Dot(e2,q)*inverse;return distance>=0&&distance<=maximum;
    }
}
