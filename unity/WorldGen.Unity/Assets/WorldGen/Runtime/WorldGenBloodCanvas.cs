using System;
using UnityEngine;

/// <summary>Paints only triangles near the real impact point into an independent
/// UV mask. Original texture assets remain unchanged and the stain follows skinning.</summary>
public sealed class WorldGenBloodCanvas : IDisposable
{
    public const int Size=256;readonly byte[] pixels=new byte[Size*Size];
    public Texture2D Texture {get;private set;}
    public WorldGenBloodCanvas(byte[] saved=null)
    {
        if(saved?.Length==pixels.Length)Array.Copy(saved,pixels,pixels.Length);
        Texture=new Texture2D(Size,Size,TextureFormat.R8,false,true){name="Local impact stains",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};Upload();
    }
    public byte[] Capture()=>(byte[])pixels.Clone();
    public bool Stamp(Vector3[] vertices,Vector2[] uv,int[] triangles,Vector3 hit,Vector3 normal,float radius,Func<int,bool> visible=null)
    {
        bool changed=false;float squared=radius*radius;
        for(int t=0;t<triangles.Length;t+=3)
        {
            int ai=triangles[t],bi=triangles[t+1],ci=triangles[t+2];if(visible!=null&&!visible(ai))continue;
            Vector3 a=vertices[ai],b=vertices[bi],c=vertices[ci];var bounds=new Bounds(a,Vector3.zero);bounds.Encapsulate(b);bounds.Encapsulate(c);if(bounds.SqrDistance(hit)>squared)continue;
            Vector3 n=Vector3.Cross(b-a,c-a).normalized;if(Mathf.Abs(Vector3.Dot(n,normal))<.12f)continue;
            Vector2 x=uv[ai],y=uv[bi],z=uv[ci],e1=y-x,e2=z-x;float denominator=e1.x*e2.y-e1.y*e2.x;if(Mathf.Abs(denominator)<1e-10f)continue;
            int minX=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(x.x,Mathf.Min(y.x,z.x))*Size),0,Size-1),maxX=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(x.x,Mathf.Max(y.x,z.x))*Size),0,Size-1);
            int minY=Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(x.y,Mathf.Min(y.y,z.y))*Size),0,Size-1),maxY=Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(x.y,Mathf.Max(y.y,z.y))*Size),0,Size-1);
            for(int py=minY;py<=maxY;py++)for(int px=minX;px<=maxX;px++)
            {
                Vector2 p=new Vector2((px+.5f)/Size,(py+.5f)/Size)-x;float u=(p.x*e2.y-p.y*e2.x)/denominator,v=(e1.x*p.y-e1.y*p.x)/denominator;
                if(u<-.015f||v<-.015f||u+v>1.015f)continue;Vector3 at=a+(b-a)*u+(c-a)*v;float distance=(at-hit).sqrMagnitude;if(distance>squared)continue;
                float variation=.88f+.12f*Mathf.Sin(px*3.7f+py*1.9f);byte alpha=(byte)Mathf.RoundToInt(255*Mathf.SmoothStep(0,1,Mathf.Clamp01((1-distance/squared)*2.2f))*variation);
                int index=py*Size+px;if(alpha>pixels[index]){pixels[index]=alpha;changed=true;}
            }
        }
        if(changed)Upload();return changed;
    }
    void Upload(){Texture.SetPixelData(pixels,0);Texture.Apply(false,false);}
    public void Dispose(){if(Texture!=null)UnityEngine.Object.Destroy(Texture);Texture=null;}
}
