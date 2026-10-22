using System;
using System.Threading;
using UnityEngine;

// Multi-view, relightable impostors of the actual generated model. The bounded
// software baker runs off-thread: no cameras, GPU readbacks or render-target
// changes in the live view. No standing tree is rasterized on the CPU per frame.
static class LocalTreeImpostor
{
    public const int Grid=4,Views=Grid*Grid,Size=128;
    public sealed class Source
    {
        public Vector3[] Vertices,Normals;public Vector4[] Tangents;public Vector2[] Uv,Mask;
        public Color32[] Colours;public int[] Indices;public Vector3 Center;public float Radius;public int Shape;
        public Source(Mesh mesh,int shape)
        {Vertices=mesh.vertices;Normals=mesh.normals;Tangents=mesh.tangents;Uv=mesh.uv;Mask=mesh.uv2;Colours=mesh.colors32;Indices=mesh.triangles;Center=mesh.bounds.center;Radius=mesh.bounds.extents.magnitude*1.025f;Shape=shape;}
    }
    public sealed class Result {public Color32[][] Colours,Normals;public Vector3 Center;public float Radius;}
    public static Vector3 Direction(int index)
    {
        float x=(index%Grid)/(float)(Grid-1)*2-1,y=(index/Grid)/(float)(Grid-1)*2-1;
        return new Vector3((x+y)*.5f,1-Mathf.Max(Mathf.Abs(x),Mathf.Abs(y)),(x-y)*.5f).normalized;
    }
    static void Basis(Vector3 direction,out Vector3 right,out Vector3 up)
    {right=Vector3.Cross(Vector3.up,direction);if(right.sqrMagnitude<.0001f)right=Vector3.right;right.Normalize();up=Vector3.Cross(direction,right).normalized;}
    public static Result Bake(Source source,CancellationToken token)
    {
        const int size=Size*2;var result=new Result{Colours=new Color32[Views][],Normals=new Color32[Views][],Center=source.Center,Radius=source.Radius};
        var projected=new Vector3[source.Vertices.Length];var depth=new float[size*size];
        for(int view=0;view<Views;view++)
        {
            token.ThrowIfCancellationRequested();var direction=Direction(view);Basis(direction,out var right,out var up);
            for(int i=0;i<projected.Length;i++)
            {var p=source.Vertices[i]-source.Center;projected[i]=new Vector3((.5f+Vector3.Dot(p,right)/(2*source.Radius))*size,(.5f+Vector3.Dot(p,up)/(2*source.Radius))*size,Vector3.Dot(p,direction));}
            for(int i=0;i<depth.Length;i++)depth[i]=float.NegativeInfinity;
            var colour=new Color32[depth.Length];var normal=new Color32[depth.Length];
            for(int triangle=0;triangle<source.Indices.Length;triangle+=3)
            {
                if((triangle&255)==0)token.ThrowIfCancellationRequested();
                int ia=source.Indices[triangle],ib=source.Indices[triangle+1],ic=source.Indices[triangle+2];
                var a=projected[ia];var b=projected[ib];var c=projected[ic];
                float area=(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);if(Mathf.Abs(area)<.00001f)continue;
                int x0=Math.Max(0,(int)Math.Floor(Math.Min(a.x,Math.Min(b.x,c.x)))),x1=Math.Min(size-1,(int)Math.Ceiling(Math.Max(a.x,Math.Max(b.x,c.x))));
                int y0=Math.Max(0,(int)Math.Floor(Math.Min(a.y,Math.Min(b.y,c.y)))),y1=Math.Min(size-1,(int)Math.Ceiling(Math.Max(a.y,Math.Max(b.y,c.y))));
                float material=source.Mask[ia].y;bool foliage=material>1.5f&&material<2.5f;
                int layer=Math.Max(0,Math.Min(LocalTreeTextures.Layers-1,source.Shape*4+Mathf.RoundToInt((material-2)*10)));
                for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
                {
                    float px=x+.5f-a.x,py=y+.5f-a.y;
                    float v=(px*(c.y-a.y)-py*(c.x-a.x))/area,w=((b.x-a.x)*py-(b.y-a.y)*px)/area,u=1-v-w;
                    if(u<0||v<0||w<0)continue;
                    int at=y*size+x;float z=a.z*u+b.z*v+c.z*w;if(z<depth[at])continue;
                    var uv=source.Uv[ia]*u+source.Uv[ib]*v+source.Uv[ic]*w;
                    Color tint=(Color)source.Colours[ia]*u+(Color)source.Colours[ib]*v+(Color)source.Colours[ic]*w;
                    Vector3 n=(source.Normals[ia]*u+source.Normals[ib]*v+source.Normals[ic]*w).normalized;
                    if(foliage)
                    {
                        Color tex=Sample(LocalTreeTextures.Colour[layer],uv);if(tex.a<.4f)continue;tint*=tex;
                        var detail=(Vector3)(Vector4)Sample(LocalTreeTextures.Normals[layer],uv)*2-Vector3.one;
                        var tangent=(Vector3)(source.Tangents[ia]*u+source.Tangents[ib]*v+source.Tangents[ic]*w);tangent.Normalize();
                        n=(n+(tangent*detail.x+Vector3.Cross(n,tangent)*detail.y)*.45f).normalized;
                    }
                    if(Vector3.Dot(n,direction)<0)n=-n;
                    float height=source.Vertices[ia].y*u+source.Vertices[ib].y*v+source.Vertices[ic].y*w;
                    float ao=.72f+.28f*Mathf.Clamp01(height*1.7f);tint*=ao;tint.a=1;
                    colour[at]=tint;normal[at]=new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f,.5f+z/(2*source.Radius));depth[at]=z;
                }
            }
            // Supersampling avoids missing thin branches/leaves during a bake.
            var reduced=new Color32[Size*Size];var ns=new Color32[reduced.Length];
            for(int y=0;y<Size;y++)for(int x=0;x<Size;x++)
            {
                int weight=0,cr=0,cg=0,cb=0,nr=0,ng=0,nb=0,nd=0;
                for(int dy=0;dy<2;dy++)for(int dx=0;dx<2;dx++)
                {int at=(y*2+dy)*size+x*2+dx;var p=colour[at];var n=normal[at];int alpha=p.a;weight+=alpha;cr+=p.r*alpha;cg+=p.g*alpha;cb+=p.b*alpha;nr+=n.r*alpha;ng+=n.g*alpha;nb+=n.b*alpha;nd+=n.a*alpha;}
                int div=Math.Max(1,weight),at2=y*Size+x;
                reduced[at2]=new Color32((byte)(cr/div),(byte)(cg/div),(byte)(cb/div),(byte)(weight/4));
                ns[at2]=new Color32((byte)(nr/div),(byte)(ng/div),(byte)(nb/div),(byte)(nd/div));
            }
            result.Colours[view]=reduced;result.Normals[view]=ns;
        }
        return result;
    }
    static Color Sample(Color32[] pixels,Vector2 uv)
    {
        float x=Mathf.Clamp(uv.x*LocalTreeTextures.Size-.5f,0,LocalTreeTextures.Size-1),y=Mathf.Clamp(uv.y*LocalTreeTextures.Size-.5f,0,LocalTreeTextures.Size-1);
        int a=(int)x,b=(int)y,c=Math.Min(a+1,LocalTreeTextures.Size-1),d=Math.Min(b+1,LocalTreeTextures.Size-1);
        return Color.Lerp(Color.Lerp(pixels[b*LocalTreeTextures.Size+a],pixels[b*LocalTreeTextures.Size+c],x-a),Color.Lerp(pixels[d*LocalTreeTextures.Size+a],pixels[d*LocalTreeTextures.Size+c],x-a),y-b);
    }
}
