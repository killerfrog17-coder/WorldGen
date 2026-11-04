using System;
using System.Collections.Generic;
using UnityEngine;
using WorldGen.Core;

static partial class LocalScatterMeshes
{
    static void TreeMark(Builder b,List<Vector2> mask,float type)
    {while(mask.Count<b.V.Count)mask.Add(new Vector2(Mathf.Clamp01(b.V[mask.Count].y),type));}
    static void TreeSweep(Builder b,List<Vector2> mask,Vector3 root,Vector3 control,Vector3 tip,float radius,float endRadius,int parts,int sides,Color colour)
    {
        int first=b.V.Count;Vector3 previousAxis=Vector3.zero,u=Vector3.zero;
        float repeats=Mathf.Max(1,Mathf.Round(2*Mathf.PI*radius/.04f));
        for(int j=0;j<=parts;j++)
        {
            float t=j/(float)parts;var p=(1-t)*(1-t)*root+2*t*(1-t)*control+t*t*tip;
            var axis=(2*(1-t)*(control-root)+2*t*(tip-control)).normalized;
            if(j==0)u=Vector3.Cross(axis,Mathf.Abs(axis.y)<.94f?Vector3.up:Vector3.right).normalized;
            else u=(Quaternion.FromToRotation(previousAxis,axis)*u).normalized;
            var v=Vector3.Cross(axis,u);previousAxis=axis;
            float rr=Mathf.Lerp(radius,endRadius,t);
            for(int k=0;k<=sides;k++){float angle=(k%sides)*2*Mathf.PI/sides;b.Add(p+(u*Mathf.Cos(angle)+v*Mathf.Sin(angle))*rr,colour,new Vector2(k/(float)sides*repeats,t*Vector3.Distance(root,tip)/.04f));}
        }
        for(int j=0;j<parts;j++)for(int k=0;k<sides;k++){int a=first+j*(sides+1)+k;b.Triangle(a,a+1,a+sides+1);b.Triangle(a+1,a+sides+2,a+sides+1);}TreeMark(b,mask,0);
    }
    static void TreeBlade(Builder b,List<Vector2> mask,Vector3 root,Vector3 control,Vector3 tip,float width,int parts,Color colour,bool simple=false)
    {
        parts=Math.Max(2,parts);int first=b.V.Count;
        var axis=(tip-root).normalized;var side=Vector3.Cross(axis,Mathf.Abs(axis.y)<.98f?Vector3.up:Vector3.right).normalized;
        if(simple)
        {
            // Retain root, tip, widest point and leaflet population.
            var center=root*.25f+control*.5f+tip*.25f;
            b.Add(root,colour,new Vector2(.5f,0));b.Add(center-side*width,colour,new Vector2(0,.5f));
            b.Add(tip,colour,new Vector2(.5f,1));b.Add(center+side*width,colour,new Vector2(1,.5f));
            b.Triangle(first,first+1,first+2);b.Triangle(first,first+2,first+3);TreeMark(b,mask,1);return;
        }
        // A folded kite retains the leaflet's curvature, silhouette and ridge
        // with four triangles; the old subdivided strip spent 16-20 on EACH of
        // hundreds of leaflets, largely invisible from an ordinary game view.
        var middle=root*.25f+control*.5f+tip*.25f;
        b.Add(root,colour,new Vector2(.5f,0));b.Add(middle-side*width,colour,new Vector2(0,.5f));
        b.Add(middle+Vector3.up*width*.23f,colour,new Vector2(.5f,.5f));
        b.Add(middle+side*width,colour,new Vector2(1,.5f));b.Add(tip,colour,new Vector2(.5f,1));
        b.Triangle(first,first+1,first+2);b.Triangle(first,first+2,first+3);
        b.Triangle(first+2,first+1,first+4);b.Triangle(first+3,first+2,first+4);TreeMark(b,mask,1);
    }
    static Mesh FinishTreeForm(Builder b,List<Vector2> mask,string name)
    {
        var mesh=b.Finish(false);mesh.SetUVs(1,mask);
        var vertices=mesh.vertices;var normals=mesh.normals;var shared=new Dictionary<Vector3,Vector3>();
        for(int i=0;i<vertices.Length;i++)if(mask[i].y<.5f)
        {shared.TryGetValue(vertices[i],out var sum);shared[vertices[i]]=sum+normals[i];}
        for(int i=0;i<vertices.Length;i++)if(mask[i].y<.5f)normals[i]=shared[vertices[i]].normalized;
        mesh.normals=normals;mesh.RecalculateTangents();mesh.RecalculateBounds();mesh.name=name;return mesh;
    }
    static Mesh BuildFrondTree(int code,int lod,TreeMorphology m)
    {
        var b=new Builder();var mask=new List<Vector2>();var random=new Rng(code,"tree-attached-fronds-2");
        var wood=HerbColour(m.Bark);var leaf=HerbColour(m.Leaf);bool fern=m.Form==TreeGrowthForm.TreeFern;
        float lean=((float)random.Unit()-.5f)*.12f;
        var crown=new Vector3(lean,.78f,lean*.3f);
        TreeSweep(b,mask,Vector3.zero,new Vector3(lean*.25f,.43f,0),crown,(float)m.TrunkRadius*(fern?1.6f:1),.011f,lod==0?12:6,lod==0?24:12,wood);
        int count=fern?13:15;
        for(int frond=0;frond<count;frond++)
        {
            float angle=frond*2.399963f+(float)random.Unit()*.25f;
            var outward=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));var across=Vector3.Cross(outward,Vector3.up);
            float reach=.34f+(float)random.Unit()*.12f,arch=.20f+(float)random.Unit()*.17f,drop=frond%4==0?.48f:.16f;
            Vector3 P(float t)=>crown+outward*(reach*t)+Vector3.up*(arch*Mathf.Sin(t*Mathf.PI)-drop*t*t);
            // The rachis and every leaflet share THIS curve; no quadratic stem
            // approximation that misses all the points used by its leaves.
            int segments=lod==0?10:6;
            float rachisEnd=m.LeafShape==TreeLeafShape.FanFrond&&!fern?.50f:1;
            for(int s=0;s<segments;s++){float a=s/(float)segments*rachisEnd,t=(s+1f)/segments*rachisEnd;TreeSweep(b,mask,P(a),P((a+t)*.5f),P(t),Mathf.Lerp(.0035f,.0005f,a),Mathf.Lerp(.0035f,.0005f,t),1,lod==0?5:3,leaf*.62f);}
            if(m.LeafShape==TreeLeafShape.FanFrond&&!fern)
            {
                var hub=P(.50f);
                for(int ray=-7;ray<=7;ray++)
                {var direction=Quaternion.AngleAxis(ray*6.5f,Vector3.up)*outward;var tip=hub+direction*.28f-Vector3.up*.065f;TreeBlade(b,mask,hub,Vector3.Lerp(hub,tip,.5f)+Vector3.up*.04f,tip,.025f,lod==0?5:3,leaf,lod>0);}
            }
            else
            {
                int pairs=fern?22:24;
                for(int k=1;k<=pairs;k++)
                {
                    float t=.08f+.88f*k/(pairs+1),length=(fern?.10f:.12f)*Mathf.Pow(Mathf.Sin(t*Mathf.PI),.7f);
                    for(int sign=-1;sign<=1;sign+=2)
                    {
                        Vector3 root=P(t),tip=root+across*(sign*length)+outward*length*.35f-Vector3.up*length*.32f;
                        TreeBlade(b,mask,root,Vector3.Lerp(root,tip,.45f)+Vector3.up*length*.22f,tip,fern?.010f:.014f,lod==0?4:2,leaf*new Color(.93f+(float)random.Unit()*.1f,1,.95f,1),lod>0);
                    }
                }
            }
        }
        return FinishTreeForm(b,mask,"Attached fronds 2 / "+code+" / "+lod);
    }
    static Mesh BuildCaneTree(int code,int lod,TreeMorphology m)
    {
        var b=new Builder();var mask=new List<Vector2>();var r=new Rng(code,"tree-leafy-cane-2");var leaf=HerbColour(m.Leaf);var wood=HerbColour(m.Bark);
        for(int stem=0;stem<5;stem++)
        {
            float angle=stem*2.399963f;var outwards=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));float height=.82f+(float)r.Unit()*.18f;
            var root=outwards*.18f;Vector3 P(float t)=>root+Vector3.up*(height*t)+outwards*(.13f*t*t);
            TreeSweep(b,mask,P(0),P(.5f)-outwards*.03f,P(1),.010f,.002f,lod==0?10:6,lod==0?7:4,wood);
            for(int node=0;node<9;node++)
            {
                float t=.28f+node*.075f;var at=P(t);var direction=Quaternion.AngleAxis(node*137.5f+stem*32,Vector3.up)*outwards;
                var end=at+direction*(.09f+.04f*(float)r.Unit())+Vector3.up*.035f;
                TreeSweep(b,mask,at,Vector3.Lerp(at,end,.5f),end,.002f,.0006f,2,3,wood);
                for(int j=0;j<3;j++)
                {
                    var dir=Quaternion.AngleAxis((j-1)*42,Vector3.up)*direction;var start=Vector3.Lerp(at,end,.55f+j*.17f);
                    float length=.13f+.055f*(float)r.Unit();var tip=start+dir*length-Vector3.up*length*.38f;
                    TreeBlade(b,mask,start,Vector3.Lerp(start,tip,.5f)+Vector3.up*length*.50f,tip,.014f,lod==0?4:2,leaf);
                }
            }
        }
        return FinishTreeForm(b,mask,"Leafy cane 2 / "+code+" / "+lod);
    }
    static Mesh BuildSucculentTree(int code,int lod,TreeMorphology m)
    {
        var b=new Builder();var mask=new List<Vector2>();var rng=new Rng(TreeFlora.RenderCode(code),"ribbed-succulent-1");
        var skin=HerbColour(m.Leaf)*new Color(1.14f,.94f,1.20f,1);var wood=HerbColour(m.Bark);
        int sides=lod==0?12:8;
        void Stem(Vector3 root,Vector3 control,Vector3 tip,float radius,int segments,bool main)
        {
            int first=b.V.Count;
            for(int row=0;row<=segments;row++)
            {
                float t=row/(float)segments;var p=(1-t)*(1-t)*root+2*t*(1-t)*control+t*t*tip;
                var axis=(2*(1-t)*(control-root)+2*t*(tip-control)).normalized;
                var u=Vector3.Cross(axis,Mathf.Abs(axis.y)<.95f?Vector3.up:Vector3.right).normalized;var v=Vector3.Cross(axis,u);
                float tipProgress=Mathf.Clamp01((t-.88f)/.12f);
                float taper=Mathf.Lerp(1,.70f,t)*Mathf.Sqrt(Mathf.Max(.008f,1-tipProgress*tipProgress));
                for(int k=0;k<=sides;k++)
                {
                    float a=k*2*Mathf.PI/sides,r=radius*taper*(1+.10f*Mathf.Cos(a*6));
                    Color tint=skin*(.90f+.10f*Mathf.Cos(a*6));if(main&&t<.2f)tint=Color.Lerp(wood,skin,t/.2f);
                    b.Add(p+(u*Mathf.Cos(a)+v*Mathf.Sin(a))*r,tint,new Vector2(k/(float)sides,t*4));
                }
            }
            for(int row=0;row<segments;row++)for(int k=0;k<sides;k++)
            {int a=first+row*(sides+1)+k;b.Triangle(a,a+1,a+sides+1);b.Triangle(a+1,a+sides+2,a+sides+1);}
            int cap=b.V.Count;b.Add(tip,skin,new Vector2(.5f,1));
            for(int k=0;k<sides;k++)b.Triangle(first+segments*(sides+1)+k,cap,first+segments*(sides+1)+k+1);
            TreeMark(b,mask,4);
        }
        var crown=new Vector3(.025f,.99f,-.018f);Stem(Vector3.zero,new Vector3(0,.5f,0),crown,.044f,lod==0?14:8,true);
        int arms=4+(int)(rng.Unit()*3);
        for(int i=0;i<arms;i++)
        {
            float angle=i*2.399963f+(float)rng.Unit()*.3f,y=.26f+i*.055f,reach=.15f+.12f*(float)rng.Unit();
            var outwards=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));var root=new Vector3(0,y,0);
            var tip=root+outwards*reach+Vector3.up*(.25f+.25f*(float)rng.Unit());
            // Broad elbows turn into upright, fleshy branches; no leaf balls.
            Stem(root,root+outwards*reach*1.28f-Vector3.up*.035f,tip,.024f+.009f*(float)rng.Unit(),lod==0?10:6,false);
        }
        return FinishTreeForm(b,mask,"Ribbed succulent 1 / "+code+" / "+lod);
    }
}
