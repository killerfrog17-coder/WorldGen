using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    sealed class NpcResourceSites{public V Water;public bool HasWater;public double QueryMs;}
    Task<NpcResourceSites> livingResourceWork;long livingResourceGroup,livingResourceSite;
    void UpdateLivingResources()
    {
        if(livingResourceWork?.IsCompleted==true)
        {
            var work=livingResourceWork;livingResourceWork=null;
            if(work.IsFaulted)Debug.LogWarning("Local camp resource survey: "+work.Exception.GetBaseException().Message);
            else if(!work.IsCanceled&&livingGroups.TryGetValue(livingResourceGroup,out var found)&&found.Situation.Camp.Id==livingResourceSite)found.Resources=work.Result;
        }
        if(livingResourceWork!=null)return;
        foreach(var group in livingGroups.Values)
            if(group.CampLoaded&&group.Resources==null&&group.Situation.Phase==HumanCampPhase.Occupied)
            {ScheduleLivingResourceQuery(group);return;}
    }
    void ScheduleLivingResourceQuery(LivingGroup group)
    {
        livingResourceGroup=group.Situation.Band.Id;livingResourceSite=group.Situation.Camp.Id;
        var center=group.Anchor;var data=terrain;double scale=HorizontalScale;var token=cancellation.Token;
        livingResourceWork=Task.Run(()=>
        {
            var timer=System.Diagnostics.Stopwatch.StartNew();var result=new NpcResourceSites();
            var patch=data.Patch(center,650/scale);double radius=data.RadiusM*scale,best=double.PositiveInfinity;
            void Water(V candidate)
            {
                double distance=HumanLocalLife.Angle(center,candidate)*radius;if(distance>450||distance>=best)return;
                var sample=patch.Sample(candidate);if(!WildlifeBehaviour.DrinkingWater(sample))return;
                var dry=center;var wet=candidate;
                for(int j=0;j<10;j++){var mid=HumanLocalLife.Interpolate(dry,wet,.5);if(patch.Sample(mid).Underwater)wet=mid;else dry=mid;}
                double bankDistance=HumanLocalLife.Angle(center,dry)*radius;
                if(bankDistance<best){best=bankDistance;result.Water=HumanLocalLife.Interpolate(dry,center,Math.Min(1,.6/Math.Max(.6,bankDistance)));result.HasWater=true;}
            }
            foreach(var piece in patch.ChannelPieces())
            {
                token.ThrowIfCancellationRequested();var d=piece.B-piece.A;var offset=center-piece.A;
                double square=d.X*d.X+d.Y*d.Y+d.Z*d.Z;if(square<1e-20)continue;
                double t=D.Clamp((offset.X*d.X+offset.Y*d.Y+offset.Z*d.Z)/square);Water((piece.A+d*t).Unit);
            }
            var east=V.Cross(new V(0,0,1),center).Unit;var north=V.Cross(center,east).Unit;
            foreach(double reach in new[]{25d,60,130,260,440})
            {
                if(best<reach*.7)break;
                for(int i=0;i<20;i++){token.ThrowIfCancellationRequested();double a=i*Math.PI/10;Water((center+(east*Math.Cos(a)+north*Math.Sin(a))*(reach/radius)).Unit);}
            }
            result.QueryMs=timer.Elapsed.TotalMilliseconds;return result;
        },token);
    }
    V LivingResourceTarget(HumanActor actor,LivingGroup group,V target)
    {
        string role=actor.Routine.Role;
        if(!role.StartsWith("Water")||group.Resources?.HasWater!=true)return target;
        var home=LivingHouse(actor)?.Position??group.Anchor;double progress=actor.Routine.Progress;
        if(role=="WaterOutbound")return HumanLocalLife.Interpolate(home,group.Resources.Water,progress);
        if(role=="WaterReturn")return HumanLocalLife.Interpolate(group.Resources.Water,home,progress);
        return group.Resources.Water;
    }
}
