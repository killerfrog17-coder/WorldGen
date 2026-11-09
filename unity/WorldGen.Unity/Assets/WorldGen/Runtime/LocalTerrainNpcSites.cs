using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    sealed class NpcSiteShape{public V Position;public double X,Z,Heading,Cos,Sin,Width,Depth;public bool Hearth;}
    sealed class NpcSiteSurvey{public ScatterInstance[] Solids;public NpcSiteShape[] Shapes;public double Radius,FootprintRadius,Milliseconds,MaxSolidRadius;public double[] SolidX,SolidZ,SolidRadius;public LocalTerrainFrame ProjectionFrame;public NpcCampSiteIndex Index;}
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
        LivingGroup group=null;
        foreach(var g in livingOrdered)
            if(!g.Grounded&&g.Residents!=null&&(g.SiteSurvey==null||g.SiteSurvey.ProjectionFrame!=frame)&&g.Situation.DistanceToPlayerM<1000&&g.Situation.Phase!=HumanCampPhase.Travelling){group=g;break;}
        if(group==null)foreach(var g in livingTraces.Values)
            if(!g.Grounded&&g.Residents!=null&&(g.SiteSurvey==null||g.SiteSurvey.ProjectionFrame!=frame)&&HumanDistance(g.Situation.Camp.Position,Position)<250&&(group==null||g.TraceAge<group.TraceAge))group=g;
        if(group!=null)StartLivingSiteSurvey(group);
    }
    // Keep the worker closure out of the per-frame no-work path.
    void StartLivingSiteSurvey(LivingGroup group)
    {
        livingSiteGroup=group.Situation.Band.Id;livingSiteId=group.Situation.Camp.Id;
        var situation=group.Situation;var people=group.Residents;var source=scatter;var token=cancellation.Token;var chart=frame;var previous=group.SiteSurvey;double radius=group.SiteSurveyRadius,planet=chart.RadiusM;int seed=sourceWorld.Recipe.Seed;
        livingSiteWork=Task.Run(()=>
        {
            var timer=System.Diagnostics.Stopwatch.StartNew();V center=situation.Camp.Position;
            var occupied=new HumanGroupSituation{Band=situation.Band,Camp=situation.Camp,Phase=HumanCampPhase.Occupied,LocalHour=situation.LocalHour};
            var east=V.Cross(new V(0,0,1),center).Unit;var north=V.Cross(center,east).Unit;
            double Dot(V a,V b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
            var shapes=HumanCampLayout.Create(occupied,people,center,planet,seed).Where(o=>o.Blueprint!=null||o.Kind==CampKind.Fire).Select(o=>
            {
                // Trample grass around seats without demanding a treeless court.
                // The fire itself needs clearance; a forest camp can keep trees.
                V delta=o.Position-center;return new NpcSiteShape{Position=o.Position,X=Dot(delta,east)*planet,Z=Dot(delta,north)*planet,Heading=o.Heading,Cos=Math.Cos(o.Heading*Math.PI/180),Sin=Math.Sin(o.Heading*Math.PI/180),Width=o.Blueprint?.WidthM??2.4,Depth=o.Blueprint?.DepthM??2.4,Hearth=o.Kind==CampKind.Fire};
            }).ToArray();
            double footprint=shapes.Select(s=>Math.Sqrt(s.X*s.X+s.Z*s.Z)+Math.Sqrt(s.Width*s.Width+s.Depth*s.Depth)*.5).DefaultIfEmpty(10).Max()+2;
            // A chart change only reprojects the immutable obstacles; it does not
            // regenerate a kilometre of scatter or project it on the main thread.
            var solids=previous?.Solids??source.SettlementSolids(center,radius,token);
            var result=new NpcSiteSurvey{Solids=solids,Shapes=shapes,Radius=previous?.Radius??radius,FootprintRadius=footprint,
                SolidX=new double[solids.Length],SolidZ=new double[solids.Length],SolidRadius=previous?.SolidRadius??solids.Select(s=>SolidTree(s.Kind)?Math.Max(.08,s.WidthM*(s.Kind==ScatterKind.GeneratedTree?new TreeMorphology(s.Variant).TrunkRadius:.035)):s.WidthM*.6).ToArray()};
            TerrainHandoff.Coordinates(chart,center,out double bx,out double bz);
            foreach(var shape in shapes){TerrainHandoff.Coordinates(chart,shape.Position,out double px,out double pz);shape.X=px-bx;shape.Z=pz-bz;}
            for(int i=0;i<solids.Length;i++){if((i&1023)==0)token.ThrowIfCancellationRequested();TerrainHandoff.Coordinates(chart,solids[i].Position,out result.SolidX[i],out result.SolidZ[i]);result.MaxSolidRadius=Math.Max(result.MaxSolidRadius,result.SolidRadius[i]);}
            result.Index=new NpcCampSiteIndex(result.SolidX,result.SolidZ);result.ProjectionFrame=chart;result.Milliseconds=timer.Elapsed.TotalMilliseconds;return result;
        },token);
    }
    bool LivingSiteClear(LivingGroup group,double x,double z)
    {
        var survey=group.SiteSurvey;if(survey==null||survey.ProjectionFrame!=frame)return false;
        var nearby=survey.Index.Around(x,z,survey.FootprintRadius+survey.MaxSolidRadius);
        while(nearby.MoveNext())
        {
            int i=nearby.Current;
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
