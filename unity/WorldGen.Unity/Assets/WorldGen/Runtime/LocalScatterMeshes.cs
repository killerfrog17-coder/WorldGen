using System;
using System.Collections.Generic;
using UnityEngine;
using WorldGen.Core;

/// <summary>Disposable art placeholders, never placement logic. Meshes have UVs/normals and metre-scale reference bounds.</summary>
static partial class LocalScatterMeshes
{
    sealed class Builder
    {
        public readonly List<Vector3> V=new List<Vector3>();readonly List<Vector2> uv=new List<Vector2>();readonly List<Color> c=new List<Color>();readonly List<int> t=new List<int>();
        public int Add(Vector3 p,Color color,Vector2 tex){V.Add(p);c.Add(color);uv.Add(tex);return V.Count-1;}
        public void Triangle(int a,int b,int d){t.Add(a);t.Add(b);t.Add(d);}
        public void Tube(Vector3 a,Vector3 b,float radius,float tip,int sides,Color color)
        {
            Vector3 axis=(b-a).normalized,u=Vector3.Cross(axis,Math.Abs(axis.y)<.9?Vector3.up:Vector3.right).normalized,v=Vector3.Cross(axis,u);int first=V.Count;
            for(int ring=0;ring<2;ring++)for(int i=0;i<=sides;i++){float angle=i*Mathf.PI*2/sides;Add((ring==0?a:b)+(u*Mathf.Cos(angle)+v*Mathf.Sin(angle))*(ring==0?radius:tip),color,new Vector2(i/(float)sides,ring));}
            for(int i=0;i<sides;i++){int q=first+i;Triangle(q,q+1,q+sides+1);Triangle(q+1,q+sides+2,q+sides+1);}
        }
        public void Crown(Vector3 center,Vector3 size,int sides,int rings,Color color,int seed,float rough=.12f)
        {
            int first=V.Count;
            for(int y=0;y<=rings;y++)for(int x=0;x<=sides;x++)
            {
                float lat=y*Mathf.PI/rings,angle=x*Mathf.PI*2/sides;uint h=Rng.Mix((uint)(seed+y*101+x%sides));float r=1+rough*((h&1023)/511.5f-1);
                var p=new Vector3(Mathf.Sin(lat)*Mathf.Cos(angle)*size.x,Mathf.Cos(lat)*size.y,Mathf.Sin(lat)*Mathf.Sin(angle)*size.z)*r;
                float shade=.86f+.2f*((h>>10)&255)/255;Add(center+p,color*new Color(shade,shade,shade,1),new Vector2(x/(float)sides,y/(float)rings));
            }
            for(int y=0;y<rings;y++)for(int x=0;x<sides;x++){int a=first+y*(sides+1)+x;Triangle(a,a+1,a+sides+1);Triangle(a+1,a+sides+2,a+sides+1);}
        }
        public void Conifer(int detail,int variant,Color leaf)
        {
            int sides=detail==0?11:detail==1?7:5,layers=detail==0?7:detail==1?4:2;
            Tube(Vector3.zero,Vector3.up*.98f,.025f,.003f,detail==0?7:4,new Color(.28f,.2f,.12f));
            for(int l=0;l<layers;l++)
            {
                float y=.18f+l*.7f/layers,r=.22f*(1-y)*(.94f+variant*.06f);int first=V.Count;
                int peak=Add(new Vector3(0,y+.25f,0),leaf,new Vector2(.5f,1));
                for(int i=0;i<=sides;i++){float a=i*Mathf.PI*2/sides+l*.72f;float jag=1+.16f*Mathf.Sin(i*2.9f+l+variant);Add(new Vector3(Mathf.Cos(a)*r*jag,y+Mathf.Sin(a*3+variant)*.018f,Mathf.Sin(a)*r*jag),leaf*new Color(.75f,.86f,.73f,1),new Vector2(i/(float)sides,0));}
                for(int i=0;i<sides;i++)Triangle(peak,first+i+2,first+i+1);
            }
        }
        public void Grass(int detail,int variant)
        {
            int blades=detail==0?7:3;
            for(int i=0;i<blades;i++)
            {
                float a=i*2.39996f+variant*.77f,h=.55f+.45f*((Rng.Mix((uint)(i+variant*77))&255)/255f),offset=.14f*Mathf.Sqrt(i/(float)blades);Vector3 root=new Vector3(Mathf.Sin(a)*offset,0,Mathf.Cos(a)*offset),side=new Vector3(Mathf.Cos(a),0,-Mathf.Sin(a));
                int first=V.Count,segments=detail==0?3:1;
                for(int j=0;j<=segments;j++){float f=j/(float)segments,w=.028f*(1-f)+.001f;Vector3 center=root+Vector3.up*(h*f)+new Vector3(Mathf.Sin(a),0,Mathf.Cos(a))*(f*f*.21f);Color color=Color.Lerp(new Color(.19f,.26f,.08f),new Color(.49f,.57f,.21f),f);Add(center-side*w,color,new Vector2(0,f));Add(center+side*w,color,new Vector2(1,f));}
                for(int j=0;j<segments;j++){int p=first+j*2;Triangle(p,p+2,p+1);Triangle(p+1,p+2,p+3);}
            }
        }
        public void Rock(int detail,int variant,bool outcrop)
        {
            int family=variant/3,shape=variant%3;
            bool rounded=family==0||family==3,layered=family==2||family==4||family==5;
            int sides=detail==0?24:detail==1?16:8,rings=detail==0?16:detail==1?10:6;
            int first=V.Count;
            for(int y=0;y<=rings;y++)for(int x=0;x<=sides;x++)
            {
                float lat=y*Mathf.PI/rings,angle=x*Mathf.PI*2/sides;
                float a=Mathf.Sin(lat)*Mathf.Cos(angle),b=Mathf.Cos(lat),c=Mathf.Sin(lat)*Mathf.Sin(angle);
                // An asymmetric weathered ellipsoid, not a superellipsoid/cube.
                // Continuous directional harmonics retain the same form at every LOD.
                float warp=1+.13f*Mathf.Sin(a*3.7f+b*2.1f+c*2.9f+shape*1.7f)+.08f*Mathf.Sin(a*7.1f-b*4.3f+c*3.1f+variant);
                float joint=layered?1-.025f*Mathf.Sin(b*13+shape):1;
                Vector3 p=new Vector3(a*.5f*warp*joint+.06f*b*b,(b*warp+1)*.5f,c*.43f*warp*joint+.04f*a*b);
                if(outcrop&&rounded)p.y=Mathf.Pow(Mathf.Max(0,p.y),.8f);
                if(family==5)p.x+=p.y*.10f;
                float band=layered?.91f+.045f*Mathf.Sin(b*19+a*2+shape):.94f+.04f*Mathf.Sin(angle*4+lat*5);
                Add(p,new Color(band,band,band,1),new Vector2(x/(float)sides,y/(float)rings));
            }
            for(int y=0;y<rings;y++)for(int x=0;x<sides;x++){int a=first+y*(sides+1)+x;Triangle(a,a+1,a+sides+1);Triangle(a+1,a+sides+2,a+sides+1);}
        }
        public Mesh Finish(){var mesh=new Mesh();mesh.SetVertices(V);mesh.SetColors(c);mesh.SetUVs(0,uv);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;}
    }
    public static Mesh Build(ScatterKind kind,int detail,int variant)
    {
        if(kind==ScatterKind.Herb||kind==ScatterKind.Fungus)return BuildSpecialist(variant,detail);
        if(kind==ScatterKind.MidPlant)return BuildMidPlant(variant,detail);
        if(kind==ScatterKind.GeneratedTree)return BuildTree(variant,detail);
        var b=new Builder();Color leaf=kind==ScatterKind.Conifer?new Color(.19f,.32f,.16f):new Color(.29f,.43f,.15f);leaf*=new Color(1+variant*.035f,1-variant*.025f,1,1);
        if(kind==ScatterKind.Grass)b.Grass(detail,variant);
        else if(kind==ScatterKind.Conifer)b.Conifer(detail,variant,leaf);
        else if(kind==ScatterKind.Boulder||kind==ScatterKind.Stone||kind==ScatterKind.Outcrop)b.Rock(detail,variant,kind==ScatterKind.Outcrop);
        else if(kind==ScatterKind.Shrub)
        {
            int n=detail==0?5:2;for(int i=0;i<n;i++){float a=i*2.39996f;b.Crown(new Vector3(Mathf.Sin(a)*.28f,.48f+Mathf.Sin(i+variant)*.12f,Mathf.Cos(a)*.28f),new Vector3(.4f,.43f,.4f),detail==0?8:5,detail==0?4:3,leaf,variant*101+i,.17f);}
        }
        else
        {
            bool umbrella=kind==ScatterKind.SavannaTree;int n=detail==0?8:detail==1?4:1;
            b.Tube(Vector3.zero,Vector3.up*.68f,.03f,.012f,detail==0?8:4,new Color(.32f,.22f,.13f));
            for(int i=0;i<n;i++)
            {
                float a=i*2.39996f+variant*.5f,r=n==1?0:.19f;Vector3 end=new Vector3(Mathf.Sin(a)*r,.68f+.12f*Mathf.Sin(i+variant),Mathf.Cos(a)*r);
                if(detail<2)b.Tube(Vector3.up*(.3f+i*.018f),end,.012f,.004f,4,new Color(.32f,.22f,.13f));
                b.Crown(end+Vector3.up*.12f,new Vector3(umbrella?.34f:.25f,umbrella?.13f:.26f,umbrella?.34f:.25f),detail==0?9:detail==1?6:4,detail==0?5:3,leaf,variant*101+i,.16f);
            }
        }
        var result=b.Finish();
        if(kind==ScatterKind.Boulder||kind==ScatterKind.Stone||kind==ScatterKind.Outcrop)
        {
            var vertices=result.vertices;var normals=result.normals;var groups=new Dictionary<Vector3Int,List<int>>();
            for(int i=0;i<vertices.Length;i++)
            {var p=vertices[i];var key=new Vector3Int(Mathf.RoundToInt(p.x*100000),Mathf.RoundToInt(p.y*100000),Mathf.RoundToInt(p.z*100000));if(!groups.TryGetValue(key,out var indices)){indices=new List<int>();groups.Add(key,indices);}indices.Add(i);}
            foreach(var indices in groups.Values){var n=Vector3.zero;foreach(int i in indices)n+=normals[i];n.Normalize();foreach(int i in indices)normals[i]=n;}
            result.normals=normals;result.RecalculateTangents();
        }
        result.name=kind+" placeholder L"+detail+" V"+variant;return result;
    }
}
