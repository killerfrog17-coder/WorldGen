using System;
using System.Collections.Generic;
using UnityEngine;
using WorldGen.Core;

static partial class LocalScatterMeshes
{
    // Bounded space-colonisation architecture. Only shared art is generated;
    // this does not simulate growth or change authoritative species/placement.
    sealed class TreeNode
    {
        public Vector3 P,Direction;public int Parent,Children,Hits;public float Radius;
        public TreeNode(Vector3 p,int parent){P=p;Parent=parent;}
    }
    static Mesh BuildTree(int code,int lod)
    {
        if(!TerrainVideoOptions.TreeRebuild)return BuildLegacyTree(code,lod);
        var m=new TreeMorphology(code);
        if(m.Form!=TreeGrowthForm.BroadCrown&&m.Form!=TreeGrowthForm.NeedleCrown)
            return BuildLegacyTree(code,lod); // Exotic anatomy is a subsequent art checkpoint.
        var nodes=TreeSkeleton(code,m);var b=new Builder();var masks=new List<Vector2>();
        var leaf=HerbColour(m.Leaf);var wood=HerbColour(m.Bark);
        int sides=lod==0?7:4;
        void Mark(float type){while(masks.Count<b.V.Count)masks.Add(new Vector2(Mathf.Clamp01(b.V[masks.Count].y),type));}
        for(int i=1;i<nodes.Count;i++)
        {
            var n=nodes[i];var parent=nodes[n.Parent];
            if(lod>0&&n.Radius<.0014f&&n.Children==0)continue;
            var axis=(n.P-parent.P).normalized;var u=Vector3.Cross(axis,Mathf.Abs(axis.y)<.96f?Vector3.up:Vector3.right).normalized;var v=Vector3.Cross(axis,u);
            int start=b.V.Count;
            for(int ring=0;ring<2;ring++)for(int k=0;k<=sides;k++)
            {
                float a=k*2*Mathf.PI/sides;float radius=ring==0?parent.Radius:n.Radius;
                if(parent.Children>1)radius=Mathf.Lerp(n.Radius,parent.Radius,.58f);
                var p=(ring==0?parent.P:n.P)+(u*Mathf.Cos(a)+v*Mathf.Sin(a))*radius;
                b.Add(p,wood,new Vector2(k/(float)sides,(ring==0?parent.P.y:n.P.y)*5));
            }
            for(int k=0;k<sides;k++){int a=start+k;b.Triangle(a,a+1,a+sides+1);b.Triangle(a+1,a+sides+2,a+sides+1);}Mark(0);
        }
        // Canonical foliage positions/orientations are identical in both mesh
        // LODs. The middle mesh simplifies the surface, not the crown occupancy.
        for(int i=1;i<nodes.Count;i++)
        {
            var n=nodes[i];if(n.P.y<(float)m.CrownBase||n.Children>1)continue;
            if(n.Children==1&&nodes[i].Radius>.0045f)continue;
            var r=new Rng(code,"tree-rebuild-shoot-1",i);
            var axis=(n.P-nodes[n.Parent].P).normalized;
            int sprays=n.Children==0?3:1;
            for(int j=0;j<sprays;j++)
            {
                float angle=(float)r.Unit()*Mathf.PI*2;
                var normal=new Vector3(Mathf.Cos(angle),.30f+(float)r.Unit()*.7f,Mathf.Sin(angle)).normalized;
                var right=Vector3.Cross(normal,Vector3.up).normalized;
                var up=Vector3.Cross(right,normal).normalized;
                float span=(m.Form==TreeGrowthForm.NeedleCrown?.072f:.085f)*(.8f+(float)r.Unit()*.4f);
                var center=n.P+axis*span*.25f+right*((float)r.Unit()-.5f)*span;
                int variant=(i+j)%4;float material=2+variant*.1f;
                Color tint=leaf*new Color(.86f+(float)r.Unit()*.18f,.90f+(float)r.Unit()*.12f,.88f+(float)r.Unit()*.12f,1);
                if(lod==0)
                {
                    // A folded, fitted octagon avoids the empty corners of a
                    // large rectangular card. It is a shoot, not a whole crown.
                    int first=b.Add(center+normal*span*.13f,tint,new Vector2(.5f,.5f));
                    for(int k=0;k<8;k++)
                    {
                        float a=k*Mathf.PI*.25f;var q=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
                        b.Add(center+(right*q.x+up*q.y)*span,tint,q*.5f+Vector2.one*.5f);
                    }
                    for(int k=0;k<8;k++)b.Triangle(first,first+1+(k+1)%8,first+1+k);
                }
                else
                {
                    int first=b.V.Count;
                    b.Add(center-right*span-up*span,tint,Vector2.zero);b.Add(center+right*span-up*span,tint,Vector2.right);
                    b.Add(center-right*span+up*span,tint,Vector2.up);b.Add(center+right*span+up*span,tint,Vector2.one);
                    b.Triangle(first,first+2,first+1);b.Triangle(first+1,first+2,first+3);
                }
                Mark(material);
            }
        }
        var mesh=b.Finish();mesh.name="Tree architecture 2 / "+m.Form+" / "+code+" / L"+lod;mesh.SetUVs(1,masks);
        // Give foliage a three-dimensional lighting envelope, retaining detailed
        // twig normals in the shader. Do not alter opaque branch normals.
        var normals=mesh.normals;var crown=new Vector3(0,((float)m.CrownBase+1)*.5f,0);
        for(int i=0;i<normals.Length;i++)if(masks[i].y>1.5f)
            normals[i]=Vector3.Slerp(normals[i],(b.V[i]-crown+Vector3.up*.12f).normalized,.65f).normalized;
        mesh.normals=normals;mesh.RecalculateTangents();mesh.RecalculateBounds();return mesh;
    }
    static List<TreeNode> TreeSkeleton(int code,TreeMorphology m)
    {
        var r=new Rng(code,"tree-rebuild-skeleton-1");var nodes=new List<TreeNode>(240);
        float leanX=((float)r.Unit()-.5f)*.11f,leanZ=((float)r.Unit()-.5f)*.11f;
        Vector3 Trunk(float y)=>new Vector3(leanX*y*y,y,leanZ*y*y);
        int trunk=m.Form==TreeGrowthForm.NeedleCrown?24:Mathf.CeilToInt((float)m.CrownBase/.035f);
        for(int i=0;i<=trunk;i++)nodes.Add(new TreeNode(Trunk((m.Form==TreeGrowthForm.NeedleCrown?1:(float)m.CrownBase)*i/trunk),i-1));
        if(m.Form==TreeGrowthForm.NeedleCrown)
        {
            // Family-specific apical architecture; side limbs compete for a
            // finite crown envelope rather than repeated flat cone tiers.
            for(int tier=4;tier<trunk;tier+=2)
            {
                float y=nodes[tier].P.y,reach=(1-y)*.43f*(.8f+(float)m.CrownAspect*.25f);
                int limbs=4+(tier%3);
                for(int j=0;j<limbs;j++)
                {
                    float a=j*2*Mathf.PI/limbs+tier*2.39996f+(float)r.Unit()*.35f;
                    var d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));int parent=tier;
                    for(int k=1;k<=4;k++)
                    {
                        float t=k*.25f;var p=nodes[tier].P+d*reach*t+Vector3.up*(.05f*t-.10f*Mathf.Sin(t*Mathf.PI));
                        int branch=nodes.Count;nodes.Add(new TreeNode(p,parent));parent=branch;
                        if(k<4)for(int side=-1;side<=1;side+=2)
                        {var q=p+d*reach*.16f+Vector3.Cross(d,Vector3.up)*side*reach*(1-t)*.48f+Vector3.up*.018f;nodes.Add(new TreeNode(q,branch));}
                    }
                }
            }
        }
        else
        {
            var points=new List<Vector3>(280);float bottom=(float)m.CrownBase-.03f,aspect=.30f+.09f*(float)m.CrownAspect;
            for(int i=0;i<280;i++)
            {
                float y=bottom+(1-bottom)*(float)r.Unit(),q=(y-bottom)/(1-bottom);
                float angle=(float)r.Unit()*Mathf.PI*2;
                float envelope=Mathf.Pow(Mathf.Sin(q*Mathf.PI),.55f)*(1+.16f*Mathf.Sin(angle*3+code));
                float radius=aspect*Mathf.Sqrt((float)r.Unit())*envelope;
                points.Add(Trunk(y)+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius));
            }
            for(int iteration=0;iteration<48&&nodes.Count<220&&points.Count>0;iteration++)
            {
                int before=nodes.Count;
                foreach(var n in nodes){n.Direction=Vector3.zero;n.Hits=0;}
                for(int j=points.Count-1;j>=0;j--)
                {
                    int nearest=-1;float best=.25f*.25f;bool occupied=false;
                    for(int i=trunk/2;i<before;i++)
                    {
                        float distance=(points[j]-nodes[i].P).sqrMagnitude;
                        if(distance<.043f*.043f){occupied=true;break;}
                        if(nodes[i].Children<3&&distance<best){nearest=i;best=distance;}
                    }
                    if(occupied){points.RemoveAt(j);continue;}
                    if(nearest>=0){nodes[nearest].Direction+=(points[j]-nodes[nearest].P).normalized;nodes[nearest].Hits++;}
                }
                for(int i=trunk/2;i<before&&nodes.Count<220;i++)if(nodes[i].Hits>0)
                {
                    var n=nodes[i];var direction=(n.Direction/n.Hits+Vector3.up*(.10f+.12f*(float)m.BranchLift)).normalized;
                    var p=n.P+direction*.037f;bool duplicate=false;
                    for(int k=before;k<nodes.Count;k++)if((nodes[k].P-p).sqrMagnitude<.017f*.017f){duplicate=true;break;}
                    if(!duplicate){nodes.Add(new TreeNode(p,i));n.Children++;}
                }
                if(nodes.Count==before)break;
            }
        }
        // Pipe-model widths from supported terminal growth. Both mesh LODs use
        // the same skeleton and thicknesses, never independently random branches.
        var tips=new float[nodes.Count];for(int i=nodes.Count-1;i>=0;i--)
        {tips[i]=Mathf.Max(1,tips[i]);if(nodes[i].Parent>=0)tips[nodes[i].Parent]+=tips[i];}
        foreach(var n in nodes)n.Children=0;for(int i=1;i<nodes.Count;i++)nodes[nodes[i].Parent].Children++;
        for(int i=0;i<nodes.Count;i++)nodes[i].Radius=Mathf.Max(.0009f,(float)m.TrunkRadius*Mathf.Pow(tips[i]/tips[0],.52f));
        return nodes;
    }
}
