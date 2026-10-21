using System;
using System.Collections.Generic;
using UnityEngine;
using WorldGen.Core;

// Anatomical primitives with seeded traits, not a library of named flowers.
static partial class LocalScatterMeshes
{
    static Color HerbColour(V v)=>new Color((float)v.X,(float)v.Y,(float)v.Z,1);
    static void HerbLeaf(Builder b,Vector3 start,Vector3 end,float width,int segments,Color colour,float cup=0)
    {
        var direction=(end-start).normalized;var side=Vector3.Cross(direction,Vector3.up).normalized;
        if(side.sqrMagnitude<.01f)side=Vector3.right;
        int first=b.V.Count;
        for(int i=0;i<=segments;i++)
        {
            float t=i/(float)segments,w=Mathf.Sin(t*Mathf.PI)*width;
            var center=Vector3.Lerp(start,end,t)+Vector3.up*(cup*t*t);
            var tint=colour*Color.Lerp(new Color(.68f,.75f,.62f,1),Color.white,t);
            b.Add(center-side*w,tint,new Vector2(0,t));b.Add(center+Vector3.up*(w*.18f),tint,new Vector2(.5f,t));b.Add(center+side*w,tint,new Vector2(1,t));
        }
        for(int i=0;i<segments;i++)for(int j=0;j<2;j++)
        {int at=first+i*3+j;b.Triangle(at,at+3,at+1);b.Triangle(at+1,at+3,at+4);}
    }
    static void HerbFlower(Builder b,Vector3 center,HerbMorphology m,int detail,float scale,int seed)
    {
        float radius=(float)m.HeadRadius*scale;
        for(int p=0;p<m.Petals;p++)
        {
            float angle=p*Mathf.PI*2/m.Petals+seed*.17f;
            var direction=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
            if(detail==0)HerbLeaf(b,center,center+direction*radius,radius*(float)m.PetalAspect*.38f,5,HerbColour(m.Blossom),radius*(float)m.Opening);
            else
            {
                var side=new Vector3(-direction.z,0,direction.x)*radius*(float)m.PetalAspect*.32f;
                var mid=center+direction*(radius*.55f)+Vector3.up*(radius*(float)m.Opening*.3f);
                int a=b.Add(center,HerbColour(m.Blossom),Vector2.zero),l=b.Add(mid-side,HerbColour(m.Blossom),new Vector2(0,.5f));
                int tip=b.Add(center+direction*radius+Vector3.up*(radius*(float)m.Opening),HerbColour(m.Blossom),Vector2.one),r=b.Add(mid+side,HerbColour(m.Blossom),new Vector2(1,.5f));
                b.Triangle(a,l,r);b.Triangle(l,tip,r);
            }
        }
        b.Crown(center+Vector3.up*radius*.12f,new Vector3(radius*.24f,radius*.15f,radius*.24f),detail==0?8:5,detail==0?4:2,
            Color.Lerp(HerbColour(m.Blossom),new Color(.67f,.47f,.13f),.65f),seed,.025f);
    }
    static Mesh BuildSpecialist(int code,int detail)
    {
        var b=new Builder();var m=HerbMorphology.Decode(code);var leaf=HerbColour(m.Leaf);var stem=HerbColour(m.Stem);
        int tubeSides=detail==0?6:4;
        if(m.Form==SpecialistForm.Fungus)
        {
            float stemHeight=.48f+(float)m.CapRoundness*.30f,capHeight=1-stemHeight;
            var cream=Color.Lerp(HerbColour(m.Blossom),new Color(.91f,.86f,.71f),.75f);
            b.Tube(Vector3.zero,Vector3.up*(stemHeight+.06f),.035f,.025f,detail==0?10:6,cream);
            int sides=detail==0?24:12,rings=detail==0?8:4,first=b.V.Count;
            for(int ring=0;ring<=rings;ring++)for(int j=0;j<=sides;j++)
            {
                float t=ring/(float)rings,angle=j*Mathf.PI*2/sides,r=.5f*Mathf.Sin(t*Mathf.PI*.5f);
                float ripple=1+.018f*Mathf.Sin(angle*5+code%31)*t*t;
                var position=new Vector3(Mathf.Cos(angle)*r*ripple,stemHeight+capHeight*Mathf.Cos(t*Mathf.PI*.5f),Mathf.Sin(angle)*r*ripple);
                var tint=HerbColour(m.Blossom)*Color.Lerp(Color.white,new Color(.78f,.74f,.68f),t*.45f);
                b.Add(position,tint,new Vector2(j/(float)sides,t));
            }
            for(int ring=0;ring<rings;ring++)for(int j=0;j<sides;j++)
            {int at=first+ring*(sides+1)+j;b.Triangle(at,at+1,at+sides+1);b.Triangle(at+1,at+sides+2,at+sides+1);}
            int underside=b.Add(Vector3.up*(stemHeight+.015f),cream,new Vector2(.5f,.5f));
            for(int j=0;j<sides;j++)
            {
                float a=j*Mathf.PI*2/sides,c=(j+1)*Mathf.PI*2/sides;
                int left=b.Add(new Vector3(Mathf.Cos(a)*.5f,stemHeight,Mathf.Sin(a)*.5f),cream,new Vector2(0,0));
                int right=b.Add(new Vector3(Mathf.Cos(c)*.5f,stemHeight,Mathf.Sin(c)*.5f),cream,new Vector2(1,0));b.Triangle(underside,left,right);
            }
        }
        else if(m.Form==SpecialistForm.Fern)
        {
            int pairs=detail==0?8:5;
            for(int f=0;f<m.Fronds;f++)
            {
                float a=f*2.399963f;var dir=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));var side=Vector3.Cross(dir,Vector3.up);
                Vector3 Point(float t)=>dir*(t*t*.52f)+Vector3.up*(.85f*t-.48f*t*t);
                Vector3 previous=Vector3.zero;
                for(int j=1;j<=pairs;j++)
                {
                    float t=j/(float)pairs;var at=Point(t);b.Tube(previous,at,.005f*(1-t)+.0015f,.003f*(1-t)+.001f,detail==0?4:3,stem);previous=at;
                    float length=Mathf.Sin(t*Mathf.PI)*.19f;
                    for(int sign=-1;sign<=1;sign+=2)HerbLeaf(b,at,at+side*(length*sign)+dir*(length*.25f)+Vector3.up*length*.15f,(float)m.LeafWidth*.28f*(1-t*.6f),detail==0?3:2,leaf);
                }
            }
        }
        else
        {
            int stems=m.Form==SpecialistForm.Rosette?1:m.Heads;
            for(int i=0;i<stems;i++)
            {
                float angle=i*2.399963f,shift=stems==1?0:.11f;
                var tip=new Vector3(Mathf.Sin(angle)*(shift+(float)m.Bend),.84f+.1f*Mathf.Cos(i*2.1f),Mathf.Cos(angle)*shift);
                if(m.Form==SpecialistForm.Rosette)tip.y=.28f;
                b.Tube(Vector3.zero,tip,m.Form==SpecialistForm.Rosette?.007f:.008f,.003f,tubeSides,stem);
                if(m.Form==SpecialistForm.Rosette)
                {
                    for(int j=0;j<m.LeafPairs*2;j++){float a=j*2.399963f;HerbLeaf(b,Vector3.up*.015f,new Vector3(Mathf.Sin(a)*.42f,.08f+((j&1)*.05f),Mathf.Cos(a)*.42f),(float)m.LeafWidth,detail==0?4:2,leaf);}
                }
                else for(int j=0;j<m.LeafPairs;j++)
                {
                    float t=.17f+j*.50f/m.LeafPairs,a=angle+j*2.399963f;var at=Vector3.Lerp(Vector3.zero,tip,t);
                    HerbLeaf(b,at,at+new Vector3(Mathf.Sin(a)*.25f,.13f,Mathf.Cos(a)*.25f),(float)m.LeafWidth*(1-t*.45f),detail==0?4:2,leaf);
                }
                if(m.Form==SpecialistForm.Spike)
                {
                    for(int j=0;j<6;j++)
                    {
                        float t=.62f+j*.065f,a=j*2.399963f;var at=Vector3.Lerp(Vector3.zero,tip,t);
                        var blossom=at+new Vector3(Mathf.Sin(a)*.03f,0,Mathf.Cos(a)*.03f);
                        if(detail==0)HerbFlower(b,blossom,m,detail,.30f,code+j);
                        else {float r=(float)m.HeadRadius*.30f;b.Crown(blossom,new Vector3(r,r*.7f,r),5,2,HerbColour(m.Blossom),code+j,.025f);}
                    }
                }
                else HerbFlower(b,tip,m,detail,m.Form==SpecialistForm.Rosette?1.3f:1,code+i);
            }
        }
        var mesh=b.Finish();mesh.name="Generated "+m.Form+" "+code+" L"+detail;
        // Anatomy UVs restart at every leaf/petal. A separate whole-shoot mask
        // prevents detached/frozen flower heads and keeps roots pinned.
        var bending=new System.Collections.Generic.List<Vector2>(mesh.vertexCount);
        float top=Mathf.Max(.001f,mesh.bounds.max.y);
        foreach(var vertex in mesh.vertices)bending.Add(new Vector2(Mathf.Clamp01(vertex.y/top),0));
        mesh.SetUVs(1,bending);
        // Smooth cap poles/seams while keeping the cream underside separate.
        if(m.Form==SpecialistForm.Fungus)
        {
            var vertices=mesh.vertices;var normals=mesh.normals;var groups=new Dictionary<Vector3Int,List<int>>();
            for(int i=0;i<vertices.Length;i++)
            {var p=vertices[i];if(p.y<.80f)continue;var key=new Vector3Int(Mathf.RoundToInt(p.x*100000),Mathf.RoundToInt(p.y*100000),Mathf.RoundToInt(p.z*100000));if(!groups.TryGetValue(key,out var group)){group=new List<int>();groups.Add(key,group);}group.Add(i);}
            foreach(var group in groups.Values){var normal=Vector3.zero;foreach(int i in group)normal+=normals[i];normal.Normalize();foreach(int i in group)normals[i]=normal;}mesh.normals=normals;
        }
        return mesh;
    }
}
