using System;
using System.Collections.Generic;
using UnityEngine;
using WorldGen.Core;

static partial class LocalScatterMeshes
{
    // Separate wood and foliage masks support cheap shading and rooted wind.
    static Mesh BuildTree(int code,int lod)
    {
        var b=new Builder();var m=new TreeMorphology(code);var foliage=new List<float>();
        Color leaf=HerbColour(m.Leaf),wood=HerbColour(m.Bark);int branchSides=lod==0?6:lod==1?4:3;
        var structural=new Rng(code,"tree-individual-structure-1");float leanX=(float)(structural.Unit()-.5)*.05f,leanZ=(float)(structural.Unit()-.5)*.05f;
        Vector3 Direction(float a)=>new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));
        Vector3 Trunk(float t)=>new Vector3(leanX*t*t,t,leanZ*t*t);
        void Mark(float material){while(foliage.Count<b.V.Count)foliage.Add(material);}
        void Branch(Vector3 a,Vector3 control,Vector3 end,float radius,int segments)
        {
            // Continuous sweep: shared cross-sections, not stacks of disconnected cylinders.
            int first=b.V.Count;
            for(int j=0;j<=segments;j++)
            {
                float t=j/(float)segments;Vector3 p=(1-t)*(1-t)*a+2*(1-t)*t*control+t*t*end;
                Vector3 axis=(2*(1-t)*(control-a)+2*t*(end-control)).normalized;
                Vector3 u=Vector3.Cross(axis,Mathf.Abs(axis.y)<.95f?Vector3.up:Vector3.right).normalized,v=Vector3.Cross(axis,u);
                float rr=radius*Mathf.Pow(Mathf.Max(.03f,1-t),.75f)+radius*.08f;
                for(int k=0;k<=branchSides;k++)
                {
                    float angle=k*Mathf.PI*2/branchSides;float bark=.94f+.06f*Mathf.Sin(angle*7+t*12);
                    b.Add(p+(u*Mathf.Cos(angle)+v*Mathf.Sin(angle))*rr,wood*new Color(bark,bark,bark,1),new Vector2(k/(float)branchSides,t));
                }
            }
            for(int j=0;j<segments;j++)for(int k=0;k<branchSides;k++)
            {int at=first+j*(branchSides+1)+k;b.Triangle(at,at+1,at+branchSides+1);b.Triangle(at+1,at+branchSides+2,at+branchSides+1);}
            Mark(0);
        }
        void Leaf(Vector3 root,Vector3 tip,float width,int parts,Color tint,bool packet=false,int packetVariant=0)
        {
            // Two endpoint-only rows collapse sin(pi*t) strips at coarse LOD.
            parts=Math.Max(2,parts);
            Vector3 axis=(tip-root).normalized,side=Vector3.Cross(axis,Vector3.up).normalized;if(side.sqrMagnitude<.01f)side=Vector3.right;
            int first=b.V.Count;
            for(int j=0;j<=parts;j++)
            {
                float t=j/(float)parts,w=(packet?.82f+.18f*Mathf.Sin(t*Mathf.PI):Mathf.Sin(t*Mathf.PI))*width;
                if(!packet&&m.LeafShape==TreeLeafShape.Lobed)w*=.78f+.22f*Mathf.Cos(t*Mathf.PI*(float)m.LeafLobes*2);
                Vector3 p=Vector3.Lerp(root,tip,t)+Vector3.up*(width*.35f*Mathf.Sin(t*Mathf.PI));
                b.Add(p-side*w,tint,new Vector2(0,t));b.Add(p+Vector3.up*w*.18f,tint,new Vector2(.5f,t));b.Add(p+side*w,tint,new Vector2(1,t));
            }
            for(int j=0;j<parts;j++)for(int k=0;k<2;k++){int at=first+j*3+k;b.Triangle(at,at+3,at+1);b.Triangle(at+1,at+3,at+4);}Mark(packet?2+packetVariant*.1f:1);
        }
        void Cluster(Vector3 at,Vector3 size,int seed)
        {
            // Small, rough 3D leaf masses only at distant LOD, not near green balls.
            b.Crown(at,size,lod==1?7:6,lod==1?4:3,leaf,seed,.13f);Mark(1);
        }
        void Spray(Vector3 at,Vector3 outwards,float radius,int seed)
        {
            var r=new Rng(seed,"tree-leaf-spray-1");int count=lod==0?6:lod==1?4:2;
            float compensation=Mathf.Sqrt(6f/count);
            for(int j=0;j<count;j++)
            {
                float azimuth=j*2.39996f+(float)r.Unit()*.65f;
                var dir=Direction(azimuth);float height=((float)r.Unit()-.3f)*radius;
                var root=at+dir*(radius*.35f*(float)r.Unit())+Vector3.up*height*.5f;
                var tip=root+(dir*radius*.85f+outwards*radius*.15f+Vector3.up*height)*compensation;
                Leaf(root,tip,radius*(float)m.LeafAspect*.95f*compensation,2,leaf*new Color(.92f+(float)r.Unit()*.12f,1,.94f,1),true,(seed+j)&3);
            }
        }
        void Ornament(Vector3 p,int seed)
        {
            if(lod!=0)return;var r=new Rng(seed,"tree-visible-reproduction-1");var reproduction=new TreeReproduction(code);
            if(r.Unit()>.15)return;
            if(reproduction.Fruit==TreeFruitForm.FleshyFruit||reproduction.Fruit==TreeFruitForm.Cone||reproduction.Fruit==TreeFruitForm.Nut)
            {b.Crown(p-Vector3.up*.006f,new Vector3(.006f,.008f,.006f),6,3,HerbColour(m.Fruit),seed,.03f);Mark(3);}
            else if(reproduction.HasFlowers)
                for(int j=0;j<5;j++)Leaf(p,p+Direction(j*1.2566f)*.009f,.003f,2,HerbColour(m.Blossom));
        }
        var form=m.Form;
        if(form==TreeGrowthForm.BroadCrown)
        {
            Branch(Vector3.zero,Trunk(.55f),Trunk(.94f),(float)m.TrunkRadius,lod==0?9:lod==1?5:3);
            int branches=m.Branches;
            for(int j=0;j<branches;j++)
            {
                var r=new Rng(code,"tree-primary-branch-1",j);float a=j*2.39996f+(float)r.Unit()*.6f;
                float t=(float)m.CrownBase+j*(.88f-(float)m.CrownBase)/branches;
                float outer=Mathf.Sin((t-(float)m.CrownBase)/Mathf.Max(.2f,1-(float)m.CrownBase)*Mathf.PI*.88f)*.26f+.10f;
                if((code&0x10000)!=0)outer*=1.12f;
                Vector3 root=Trunk(t),dir=Direction(a),tip=root+dir*outer+Vector3.up*(.08f+.09f*(float)m.BranchLift);
                var control=root+dir*outer*.4f+Vector3.up*.08f;
                Branch(root,control,tip,(float)m.TrunkRadius*.38f,lod==0?4:2);
                for(int k=0;k<3;k++)
                {
                    float q=.40f+k*.25f;var start=(1-q)*(1-q)*root+2*(1-q)*q*control+q*q*tip;var twig=Direction(a+(k-1)*.85f);
                    var end=start+twig*.10f+Vector3.up*.055f;
                    if(lod<2)Branch(start,Vector3.Lerp(start,end,.5f),end,.003f,lod==0?2:1);
                    Spray(end,twig,.055f+(float)r.Unit()*.018f,unchecked(code+j*31+k*101));Ornament(end,code+j*47+k);
                }
            }
        }
        else if(form==TreeGrowthForm.NeedleCrown)
        {
            Branch(Vector3.zero,Trunk(.5f),Trunk(1),(float)m.TrunkRadius,lod==0?8:4);
            int tiers=8;
            for(int tier=0;tier<tiers;tier++)
            {
                float t=.20f+tier*.72f/tiers,radius=.40f*(1-t)*(.8f+(float)m.CrownAspect*.3f);
                int limbs=6;
                for(int j=0;j<limbs;j++)
                {
                    float a=j*Mathf.PI*2/limbs+tier*.74f;var dir=Direction(a);Vector3 root=Trunk(t),tip=root+dir*radius+Vector3.up*.035f;
                    Branch(root,root+dir*radius*.4f-Vector3.up*.025f,tip,.006f,lod==0?3:2);
                    // Needle sprays are tapered folded geometry, not thousands of needles.
                    int sprays=lod==0?4:2;
                    for(int k=0;k<sprays;k++)
                    {float q=(k+.7f)/sprays;var at=Vector3.Lerp(root,tip,q);var side=Vector3.Cross(dir,Vector3.up);float spread=radius*(1-q)*.27f+.014f;
                        Leaf(at-side*spread,at+dir*.055f+Vector3.up*.025f,spread,lod==0?3:1,leaf);
                        Leaf(at+side*spread,at+dir*.055f+Vector3.up*.025f,spread,lod==0?3:1,leaf);}
                    Ornament(tip,code+tier*37+j);
                }
            }
        }
        else if(form==TreeGrowthForm.FrondCrown||form==TreeGrowthForm.TreeFern)
        {
            Branch(Vector3.zero,Trunk(.45f),Trunk(.78f),(float)m.TrunkRadius,lod==0?10:5);
            int fronds=lod==2?8:12;
            for(int j=0;j<fronds;j++)
            {
                var r=new Rng(code,"tree-frond-1",j);var dir=Direction(j*2.39996f);var across=Vector3.Cross(dir,Vector3.up);
                float reach=.35f+.10f*(float)r.Unit();Vector3 P(float t)=>Trunk(.78f)+dir*(t*reach)+Vector3.up*(.55f*t-.52f*t*t);
                Branch(P(0),Trunk(.78f)+dir*reach*.5f+Vector3.up*.275f,P(1),.0035f,lod==0?6:3);
                if(m.LeafShape==TreeLeafShape.FanFrond)
                {for(int k=-3;k<=3;k++)Leaf(P(.3f),P(.9f)+across*k*.025f,.025f,lod==0?4:2,leaf);}
                else
                {
                    int pairs=lod==0?10:lod==1?6:4;
                    for(int k=1;k<=pairs;k++)
                    {float t=k/(float)(pairs+1),length=.085f*Mathf.Sin(t*Mathf.PI);for(int sign=-1;sign<=1;sign+=2)
                        Leaf(P(t),P(t)+across*sign*length+dir*length*.25f,.012f*Mathf.Sqrt(10f/pairs),lod==0?3:1,leaf);}
                }
            }
        }
        else if(form==TreeGrowthForm.ArborescentSucculent)
        {
            Branch(Vector3.zero,Trunk(.5f),Trunk(.95f),.055f,lod==0?10:5);
            for(int j=0;j<5;j++)
            {float t=.28f+j*.10f;Vector3 dir=Direction(j*2.4f),root=Trunk(t),tip=root+dir*.25f+Vector3.up*(.15f+.10f*(j%2));
                Branch(root,root+dir*.28f,tip,.035f,lod==0?7:3);Cluster(tip,new Vector3(.055f,.10f,.055f),code+j);}
        }
        else
        {
            int stems=lod==2?3:5;
            for(int j=0;j<stems;j++)
            {Vector3 dir=Direction(j*2.4f),root=dir*.15f,tip=root+Vector3.up*(.75f+j*.04f)+dir*.05f;
                Branch(root,Vector3.Lerp(root,tip,.5f),tip,.012f,lod==0?8:4);
                int levels=lod==0?6:3;for(int k=0;k<levels;k++){float t=.35f+k*.5f/levels;var at=Vector3.Lerp(root,tip,t);Spray(at,Direction(k*2.4f+j),.06f,code+j*53+k);}}
        }
        var mesh=b.Finish();mesh.name="Generated tree "+m.Form+" "+code+" L"+lod;
        mesh.bounds=new Bounds(new Vector3(0,.53f,0),new Vector3(1.35f,1.15f,1.35f));
        var masks=new List<Vector2>(mesh.vertexCount);for(int j=0;j<b.V.Count;j++)masks.Add(new Vector2(Mathf.Clamp01(b.V[j].y),foliage[j]));mesh.SetUVs(1,masks);
        return mesh;
    }
}
