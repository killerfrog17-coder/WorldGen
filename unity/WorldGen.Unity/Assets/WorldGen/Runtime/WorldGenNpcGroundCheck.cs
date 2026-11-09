using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    struct NpcCoverProbe{public Vector4 Position,Color,Shape,Normal,Grasses,Herbs;}
    static void CheckLivingCampGround(LocalTerrainExplorer owner,LivingGroup group,string output,List<string> checks)
    {
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("NPC CAMP GROUND "+text);checks.Add("PASS "+text);Debug.Log("NPC CAMP GROUND "+text);}
        TerrainHandoff.Coordinates(owner.frame,group.Anchor,out double x,out double z);
        Check(group.SiteSurvey!=null&&owner.LivingSiteClear(group,x,z),"Actual occupied shelter/hearth footprints do not intersect canonical tree trunks or boulders");
        var survey=group.SiteSurvey;
        bool Literal(double cx,double cz)
        {
            for(int i=0;i<survey.Solids.Length;i++)
            {
                if(!SolidTree(survey.Solids[i].Kind)&&owner.CollectedRock(survey.Solids[i]))continue;
                double sx=survey.SolidX[i],sz=survey.SolidZ[i],r=survey.SolidRadius[i];
                if((sx-cx)*(sx-cx)+(sz-cz)*(sz-cz)>Math.Pow(survey.FootprintRadius+r,2))continue;
                foreach(var shape in survey.Shapes)
                {
                    double dx=sx-(cx+shape.X),dz=sz-(cz+shape.Z),lx=dx*shape.Cos-dz*shape.Sin,lz=dx*shape.Sin+dz*shape.Cos;
                    if(Math.Abs(lx)<shape.Width*.5+r+.35&&Math.Abs(lz)<shape.Depth*.5+r+.35)return false;
                }
            }
            return true;
        }
        int clear=0,blocked=0;
        for(int i=0;i<513;i++)
        {
            double bearing=i*2.399963229728653,distance=i==0?0:Math.Sqrt(i/512d)*(survey.Radius-survey.FootprintRadius-10);
            double sampleX=x+Math.Sin(bearing)*distance,sampleZ=z+Math.Cos(bearing)*distance;bool literal=Literal(sampleX,sampleZ),indexed=owner.LivingSiteClear(group,sampleX,sampleZ);
            if(literal!=indexed)throw new InvalidOperationException("Camp spatial index disagrees with full literal obstacle scan at "+i);
            if(indexed)clear++;else blocked++;
        }
        Check(clear>0&&blocked>0,"513 real camp candidates have identical indexed and full-scan clearance, including clear and blocked sites");
        File.WriteAllText(Path.Combine(output,"camp-site-index-proof.txt"),"513 literal/index pairs exact; clear="+clear+"; blocked="+blocked+"; solids="+survey.Solids.Length);
        owner.campMaskCheckAt=0;owner.UpdateCampClearing();
        var house=owner.Camp.Objects.FirstOrDefault(c=>c.NpcGroup==group.Situation.Band.Id&&c.Blueprint!=null);
        if(house==null)return;
        TerrainHandoff.Coordinates(owner.frame,house.Position,out double hx,out double hz);
        int px=(int)Math.Floor(hx-owner.campMaskX),pz=(int)Math.Floor(hz-owner.campMaskZ);
        Check(owner.campClearingPixels[pz*CampMaskSide+px]==255,"Occupied shelter floor has a full-strength ground clearing mask");
        var probes=new[]{new NpcCoverProbe{Position=new Vector4((float)(hx-owner.originX),0,(float)(hz-owner.originZ),1),Color=Vector4.one},new NpcCoverProbe{Position=new Vector4((float)(owner.campMaskX+2-owner.originX),0,(float)(owner.campMaskZ+2-owner.originZ),1),Color=Vector4.one}};
        var shader=Instantiate(owner.groundCoverPlacement);int kernel=shader.FindKernel("Cull");
        using(var roots=new GraphicsBuffer(GraphicsBuffer.Target.Structured,2,96))
        using(var count=new GraphicsBuffer(GraphicsBuffer.Target.Raw,1,4))
        using(var read=new GraphicsBuffer(GraphicsBuffer.Target.Raw,1,4))
        {
            var visible=new GraphicsBuffer[6];
            try
            {
                roots.SetData(probes);count.SetData(new uint[]{2});shader.SetBuffer(kernel,"_PlacedRoots",roots);shader.SetBuffer(kernel,"_RootCount",count);
                string[] names={"_NearRoots","_MiddleRoots","_FarRoots","_DistantRoots","_HorizonRoots","_FootRoots"};
                for(int i=0;i<6;i++){visible[i]=new GraphicsBuffer(GraphicsBuffer.Target.Append,2,4);shader.SetBuffer(kernel,names[i],visible[i]);}
                shader.SetVector("_CullOrigin",Vector4.zero);shader.SetVector("_CullCamera",Vector4.zero);shader.SetVectorArray("_CullPlanes",Enumerable.Repeat(new Vector4(0,0,0,1000000),6).ToArray());
                shader.SetFloat("_GrassRange",1000000);shader.SetFloat("_GrassNear",1000000);shader.SetFloat("_GrassNearDensity",1);shader.SetInt("_SpritesOnly",0);shader.SetInt("_CanopyEnabled",0);shader.SetInt("_CoverageEnabled",0);shader.SetFloat("_CoverageSize",48);
                shader.SetTexture(kernel,"_TerrainCoverage",Texture2D.blackTexture);shader.SetTexture(kernel,"_CampClearing",owner.campClearingTexture);shader.SetVector("_CampClearingRect",new Vector4((float)(owner.campMaskX-owner.originX),(float)(owner.campMaskZ-owner.originZ),CampMaskSide,0));
                uint DrawCount(bool mask)
                {
                    foreach(var buffer in visible)buffer.SetCounterValue(0);shader.SetInt("_CampClearingEnabled",mask?1:0);shader.Dispatch(kernel,1,1,1);
                    GraphicsBuffer.CopyCount(visible[0],read,0);var result=new uint[1];read.GetData(result);return result[0];
                }
                Check(DrawCount(false)==2&&DrawCount(true)==1,"Packed compute shader removes grass inside the real camp floor and retains grass outside");
            }
            finally{foreach(var buffer in visible)buffer?.Dispose();Destroy(shader);}
        }
        File.WriteAllText(Path.Combine(output,"camp-site-survey.txt"),group.SiteSurvey.Solids.Length+" natural solids; "+group.SiteSurvey.Milliseconds.ToString("F2")+"ms background; "+group.PlacementAttempt+" bounded candidate tests");
    }
}
