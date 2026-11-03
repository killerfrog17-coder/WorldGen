using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    sealed class NpcSiteShape{public V Position;public double X,Z,Heading,Cos,Sin,Width,Depth;public bool Hearth;}
    sealed class NpcSiteSurvey{public ScatterInstance[] Solids;public NpcSiteShape[] Shapes;public double Radius,FootprintRadius,Milliseconds;public double[] SolidX,SolidZ,SolidRadius;public LocalTerrainFrame ProjectionFrame;}
    Task<NpcSiteSurvey> livingSiteWork;long livingSiteGroup,livingSiteId;
    void UpdateLivingSites()
    {
        if(livingSiteWork?.IsCompleted==true)
        {
            var work=livingSiteWork;livingSiteWork=null;
            if(work.IsFaulted)Debug.LogWarning("Camp solid survey: "+work.Exception.GetBaseException().Message);
            else if(!work.IsCanceled)
            {
                if(livingGroups.TryGetValue(livingSiteGroup,out var found)&&found.Situation.Camp.Id==livingSiteId)found.SiteSurvey=work.Result;
                else if(livingTraces.TryGetValue(livingSiteId,out var trace))trace.SiteSurvey=work.Result;
            }
        }
        if(livingSiteWork!=null||scatter==null)return;
        bool Needed(LivingGroup g)=>!g.Grounded&&g.Residents!=null&&g.SiteSurvey==null;
        var group=livingOrdered.FirstOrDefault(g=>Needed(g)&&g.Situation.DistanceToPlayerM<1000&&g.Situation.Phase!=HumanCampPhase.Travelling)
            ??livingTraces.Values.Where(g=>Needed(g)&&HumanDistance(g.Situation.Camp.Position,Position)<250).OrderBy(g=>g.TraceAge).FirstOrDefault();
        if(group==null)return;
        livingSiteGroup=group.Situation.Band.Id;livingSiteId=group.Situation.Camp.Id;
        var situation=group.Situation;var people=group.Residents;var source=scatter;var token=cancellation.Token;double radius=group.SiteSurveyRadius,planet=frame.RadiusM;int seed=sourceWorld.Recipe.Seed;
        livingSiteWork=Task.Run(()=>
        {
            var timer=System.Diagnostics.Stopwatch.StartNew();V center=situation.Camp.Position;
            var occupied=new HumanGroupSituation{Band=situation.Band,Camp=situation.Camp,Phase=HumanCampPhase.Occupied,LocalHour=situation.LocalHour};
            var east=V.Cross(new V(0,0,1),center).Unit;var north=V.Cross(center,east).Unit;
            double Dot(V a,V b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
            var shapes=HumanCampLayout.Create(occupied,people,center,planet,seed).Where(o=>o.Blueprint!=null||o.Kind==CampKind.Fire).Select(o=>
            {
                V delta=o.Position-center;return new NpcSiteShape{Position=o.Position,X=Dot(delta,east)*planet,Z=Dot(delta,north)*planet,Heading=o.Heading,Cos=Math.Cos(o.Heading*Math.PI/180),Sin=Math.Sin(o.Heading*Math.PI/180),Width=o.Blueprint?.WidthM??Math.Max(2.4,o.ClearedWidthM),Depth=o.Blueprint?.DepthM??Math.Max(2.4,o.ClearedDepthM),Hearth=o.Kind==CampKind.Fire};
            }).ToArray();
            double footprint=shapes.Select(s=>Math.Sqrt(s.X*s.X+s.Z*s.Z)+Math.Sqrt(s.Width*s.Width+s.Depth*s.Depth)*.5).DefaultIfEmpty(10).Max()+2;
            var solids=source.SettlementSolids(center,radius,token);return new NpcSiteSurvey{Solids=solids,Shapes=shapes,Radius=radius,FootprintRadius=footprint,Milliseconds=timer.Elapsed.TotalMilliseconds,
                SolidX=new double[solids.Length],SolidZ=new double[solids.Length],SolidRadius=solids.Select(s=>SolidTree(s.Kind)?Math.Max(.08,s.WidthM*(s.Kind==ScatterKind.GeneratedTree?new TreeMorphology(s.Variant).TrunkRadius:.035)):s.WidthM*.6).ToArray()};
        },token);
    }
    bool LivingSiteClear(LivingGroup group,double x,double z)
    {
        var survey=group.SiteSurvey;if(survey==null)return false;
        if(survey.ProjectionFrame!=frame)
        {
            TerrainHandoff.Coordinates(frame,group.Situation.Camp.Position,out double bx,out double bz);
            foreach(var shape in survey.Shapes){TerrainHandoff.Coordinates(frame,shape.Position,out double px,out double pz);shape.X=px-bx;shape.Z=pz-bz;}
            for(int i=0;i<survey.Solids.Length;i++)TerrainHandoff.Coordinates(frame,survey.Solids[i].Position,out survey.SolidX[i],out survey.SolidZ[i]);
            survey.ProjectionFrame=frame;
        }
        for(int i=0;i<survey.Solids.Length;i++)
        {
            var solid=survey.Solids[i];
            if(!SolidTree(solid.Kind)&&CollectedRock(solid))continue;
            double sx=survey.SolidX[i],sz=survey.SolidZ[i],radius=survey.SolidRadius[i];
            if((sx-x)*(sx-x)+(sz-z)*(sz-z)>Math.Pow(survey.FootprintRadius+radius,2))continue;
            foreach(var shape in survey.Shapes)
            {
                double dx=sx-(x+shape.X),dz=sz-(z+shape.Z);
                double lx=dx*shape.Cos-dz*shape.Sin,lz=dx*shape.Sin+dz*shape.Cos;
                if(Math.Abs(lx)<shape.Width*.5+radius+.35&&Math.Abs(lz)<shape.Depth*.5+radius+.35)return false;
            }
        }
        return true;
    }
}
