using System;
using System.Collections.Generic;
using UnityEngine;
using WorldGen.Core;

// Seeded species anatomy + four structural variants, shared across instances.
static partial class LocalScatterMeshes
{
    static Mesh BuildMidPlant(int code,int lod)
    {
        var b=new Builder();var m=new MidMorphology(code);var reproduction=m.Reproduction;
        Color leaf=HerbColour(m.Leaf),wood=HerbColour(m.Wood);
        int segments=lod==0?4:lod==1?2:1,sides=lod==0?6:lod==1?4:3;
        Vector3 Direction(float angle)=>new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle));
        void Shoot(Vector3 a,Vector3 c,Vector3 end,float radius)
        {
            var last=a;for(int j=1;j<=segments;j++){float t=j/(float)segments;var p=(1-t)*(1-t)*a+2*(1-t)*t*c+t*t*end;b.Tube(last,p,radius*(1-t)+.0015f,radius*(1-t)*.72f+.001f,sides,wood);last=p;}
        }
        void Foliage(Vector3 root,Vector3 tip,float width,Color colour)
        {HerbLeaf(b,root,tip,width,segments,colour,Vector3.Distance(root,tip)*.12f);}
        void Ornament(Vector3 at,int seed)
        {
            if(lod>1||!reproduction.HasFlowers)return;
            var r=new Rng(seed,"mid-ornament-1");double draw=r.Unit();
            if(draw<reproduction.FruitFraction)
            {
                float radius=m.Form==MidGrowthForm.DwarfHeath?.015f:.011f;
                var fruit=HerbColour(m.Fruit);int count=lod==0?3:2;
                for(int j=0;j<count;j++)b.Crown(at+Direction(j*2.4f)*(radius*.8f)-Vector3.up*(j%2)*radius,
                    Vector3.one*radius,lod==0?7:5,lod==0?4:3,fruit,seed+j,.02f);
            }
            else if(draw<reproduction.FruitFraction+reproduction.FlowerFraction)
            {
                int petals=m.Petals;float radius=m.Form==MidGrowthForm.DwarfHeath?.024f:.028f;
                for(int j=0;j<petals;j++)Foliage(at,at+Direction(j*Mathf.PI*2/petals)*radius+Vector3.up*radius*.25f,radius*.38f,HerbColour(m.Blossom));
            }
        }
        if(m.Form==MidGrowthForm.Succulent)
        {
            var r=new Rng(code,"mid-succulent-1");int count=m.Branches;
            bool rosette=(code&64)!=0;
            for(int j=0;j<count;j++)
            {
                float angle=j*2.39996f+(float)r.Unit()*.3f;var dir=Direction(angle);
                if(rosette)
                {
                    float length=.32f+.12f*(float)r.Unit();Foliage(Vector3.up*.02f,dir*length+Vector3.up*(.12f+.4f*(float)r.Unit()),.055f,leaf);
                }
                else
                {
                    var root=dir*(j==0?0:.1f+.15f*(float)r.Unit());float height=j==0?.93f:.3f+.5f*(float)r.Unit();
                    // Fleshy columns/pads remain actual opaque geometry at every LOD.
                    if((code&128)==0)b.Crown(root+Vector3.up*height*.48f,new Vector3(.065f,height*.48f,.065f),lod==0?12:lod==1?8:5,lod==0?8:4,leaf,code+j,.025f);
                    else b.Crown(root+Vector3.up*height*.5f,new Vector3(.12f,height*.45f,.035f),lod==0?12:lod==1?8:5,lod==0?7:4,leaf,code+j,.015f);
                    if(j%3==0)Ornament(root+Vector3.up*height,code+j);
                }
            }
        }
        else if(m.Form==MidGrowthForm.LargeFern)
        {
            int pairs=lod==0?10:lod==1?6:4;
            for(int j=0;j<m.Branches;j++)
            {
                var r=new Rng(code,"mid-fern-1",j);Vector3 dir=Direction(j*2.39996f+(float)r.Unit()*.25f),side=Vector3.Cross(dir,Vector3.up);
                Vector3 P(float t)=>dir*(.43f*t*t)+Vector3.up*(1.65f*t-1.0f*t*t);
                Shoot(Vector3.zero,Vector3.up*.825f,P(1),.006f);
                for(int k=1;k<=pairs;k++)
                {
                    float t=k/(float)(pairs+1),length=.13f*Mathf.Sin(t*Mathf.PI),width=.017f*Mathf.Sqrt(10f/pairs);
                    for(int sign=-1;sign<=1;sign+=2)Foliage(P(t),P(t)+side*sign*length+dir*length*.28f,width,leaf);
                }
            }
        }
        else if(m.Form==MidGrowthForm.ReedStand)
        {
            int stems=lod==0?15:lod==1?10:7;
            for(int j=0;j<stems;j++)
            {
                var r=new Rng(code,"mid-reed-1",j);float a=j*2.39996f;
                Vector3 dir=Direction(a),root=dir*(.30f*Mathf.Sqrt((j+.3f)/15)),tip=root+dir*.075f+Vector3.up*(.65f+.3f*(float)r.Unit());
                Shoot(root,Vector3.Lerp(root,tip,.5f),tip,.0045f*Mathf.Sqrt(15f/stems));
                int leaves=lod==0?4:2;
                for(int k=0;k<leaves;k++){float t=.22f+k*.5f/leaves;var at=Vector3.Lerp(root,tip,t);Foliage(at,at+Direction(a+k*2.4f)*.20f+Vector3.up*.16f,.012f*Mathf.Sqrt(4f/leaves),leaf);}
                if(lod<2&&j%3==0)b.Crown(tip-Vector3.up*.045f,new Vector3(.015f,.06f,.015f),5,3,new Color(.46f,.35f,.18f),code+j,.05f);
            }
        }
        else
        {
            bool heath=m.Form==MidGrowthForm.DwarfHeath,cane=m.Form==MidGrowthForm.ArchingCane,scrub=m.Form==MidGrowthForm.OpenScrub;
            for(int j=0;j<m.Branches;j++)
            {
                var r=new Rng(code,"mid-branch-1",j);float a=j*2.39996f+(float)r.Unit()*.65f;var dir=Direction(a);
                float height=.48f+.38f*(float)r.Unit(),spread=heath?.33f:cane?.4f:.20f+.10f*(float)r.Unit();
                Vector3 end=dir*spread+Vector3.up*height,control=dir*(spread*.2f)+Vector3.up*(cane?1.15f:height*.65f);
                Shoot(Vector3.zero,control,end,heath?.008f:cane?.008f:.016f);
                int twigs=heath?3:3;
                for(int twig=0;twig<twigs;twig++)
                {
                    float t=.40f+twig*.20f;
                    Vector3 start=2*(1-t)*t*control+t*t*end,outward=Direction(a+twig*2.25f);
                    var tip=start+outward*(heath?.12f:.15f)+Vector3.up*.13f;
                    Shoot(start,Vector3.Lerp(start,tip,.5f),tip,.005f);
                    int full=scrub?4:m.Leaves,count=lod==0?full:lod==1?Math.Max(3,full/2):2;
                    float length=heath?.065f:scrub?.075f:.13f;
                    float width=length*(float)m.LeafAspect*.5f*Mathf.Sqrt(full/(float)count);
                    for(int k=0;k<count;k++)
                    {
                        float at=(k+.6f)/count;Vector3 point=Vector3.Lerp(start,tip,at),ld=Direction(a+twig*2.25f+k*2.39996f);
                        Foliage(point,point+ld*length+Vector3.up*(length*.35f),width,leaf);
                    }
                    Ornament(tip,unchecked(code+j*53+twig*7));
                    if(lod==0&&m.Thorniness>.2)
                        b.Tube(start,start+outward*.04f-Vector3.up*.012f,.0025f,0,3,wood);
                }
                Ornament(end,unchecked(code+j*41));
            }
        }
        var mesh=b.Finish();mesh.name="Generated mid "+m.Form+" "+code+" L"+lod;
        // A fixed species-space box avoids independent LOD normalization changing size.
        mesh.bounds=new Bounds(new Vector3(0,.5f,0),new Vector3(1.3f,1.2f,1.3f));
        var bend=new List<Vector2>(mesh.vertexCount);foreach(var p in mesh.vertices)bend.Add(new Vector2(Mathf.Clamp01(p.y),0));mesh.SetUVs(1,bend);
        return mesh;
    }
}
