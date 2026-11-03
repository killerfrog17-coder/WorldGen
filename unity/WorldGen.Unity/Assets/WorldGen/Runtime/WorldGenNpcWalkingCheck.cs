using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public sealed partial class LocalTerrainExplorer
{
    public sealed class NpcWalkRow{public string Stage,Pose;public double Time,X,Z,Y,Remaining,Speed;public bool Crouched,Crawling;}
    static IEnumerator CheckLivingWalking(LocalTerrainExplorer owner,LivingGroup group,string output,List<string> checks)
    {
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("NPC WALK "+text);checks.Add("PASS "+text);Debug.Log("NPC WALK "+text);}
        var actor=owner.livingPeople.Values.FirstOrDefault(p=>p.GroupId==group.Situation.Band.Id&&p.Resident.AgeYears>=18&&owner.LivingHouse(p)!=null);
        if(actor==null){Check(group.Situation.Camp.ShelterAvailable==false,"Open-air community has no invented doorway to test");yield break;}
        owner.livingQueryAt=double.MaxValue;
        while(owner.livingWork!=null)yield return null;
        var oldRoutine=actor.Routine;var house=owner.LivingHouse(actor);var oldPosition=actor.Planet;
        TerrainHandoff.Coordinates(owner.frame,house.Position,out double x,out double z);double a=house.Heading*Math.PI/180,side=house.Blueprint.Design==CampDesign.LeanTo?-1:1;
        double doorX=x+Math.Sin(a)*(house.Blueprint.DepthM*.5+3)*side,doorZ=z+Math.Cos(a)*(house.Blueprint.DepthM*.5+3)*side;
        double startX=doorX+Math.Cos(a)*2,startZ=doorZ-Math.Sin(a)*2;
        actor.Motor.Place(startX,startZ,owner.VisibleGround(startX,startZ,0));actor.Planet=owner.frame.Position(startX,startZ);actor.Speed=0;actor.AvoidUntil=0;actor.ObstacleAt=0;
        group.TenderDuty=null;group.NextTend=Time.unscaledTimeAsDouble+90;
        var track=new List<NpcWalkRow>();double begin=Time.realtimeSinceStartupAsDouble;
        foreach(string stage in new[]{"enter","leave"})
        {
            double tx=stage=="enter"?x:doorX,tz=stage=="enter"?z:doorZ;
            V goal=owner.frame.Position(tx,tz);var proposal=(goal-group.Anchor+group.Situation.Camp.Position).Unit;
            actor.HasSafeGoal=false;actor.Routine=new HumanActivitySample{Person=actor.ResidentId,Group=actor.GroupId,Activity=HumanActivity.Travel,Position=proposal,Destination=proposal};
            double deadline=Time.realtimeSinceStartupAsDouble+30,next=0;
            double Distance()=>Math.Sqrt(Math.Pow(actor.Motor.X-tx,2)+Math.Pow(actor.Motor.Z-tz,2));
            while(Distance()>.45&&Time.realtimeSinceStartupAsDouble<deadline)
            {
                if(Time.realtimeSinceStartupAsDouble>=next)
                {
                    next=Time.realtimeSinceStartupAsDouble+.1;
                    track.Add(new NpcWalkRow{Stage=stage,Time=Time.realtimeSinceStartupAsDouble-begin,X=actor.Motor.X,Z=actor.Motor.Z,Y=actor.Motor.Y,Remaining=Distance(),Speed=actor.Speed,Pose=actor.Shared.ActionRole,Crouched=actor.NpcCrouching,Crawling=actor.NpcCrawling});
                    File.WriteAllText(Path.Combine(output,"walking-track.json"),WorldJson.Write(track.ToArray()));
                }
                yield return null;
            }
            Check(Distance()<=.45,"A real resident can "+stage+" its actual material-backed shelter through the entrance without getting stuck");
        }
        Check(track.All(r=>double.IsFinite(r.Y)&&r.Speed<1.5),"Native walking uses finite terrain footing and normal human walking speed");
        actor.Routine=oldRoutine;TerrainHandoff.Coordinates(owner.frame,oldPosition,out double oldX,out double oldZ);actor.Motor.Place(oldX,oldZ,owner.VisibleGround(oldX,oldZ,0));actor.Planet=oldPosition;owner.livingQueryAt=0;
    }
}
