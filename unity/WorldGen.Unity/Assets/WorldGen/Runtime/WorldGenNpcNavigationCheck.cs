using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    public IEnumerator CheckNpcLocalNavigation(string output,List<string> report)
    {
        void Check(bool value,string text){if(!value){File.WriteAllLines(Path.Combine(output,"failed-checks.txt"),report.Concat(new[]{"FAIL "+text}));Application.Quit(1);throw new InvalidOperationException(text);}report.Add("PASS "+text);Debug.Log("NPC NAVIGATION PASS "+text);}
        npcModel??=new NpcModelClient();npcModel.Config.Enabled=false;PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();
        double end=Time.unscaledTimeAsDouble+100;while((!livingGroups.TryGetValue(PlayerRules.Identity.Group,out var ready)||!ready.CampLoaded||!livingPeople.Values.Any(p=>p.GroupId==PlayerRules.Identity.Group&&p.Resident.AgeYears>=18))&&Time.unscaledTimeAsDouble<end)yield return null;
        Check(livingGroups.TryGetValue(PlayerRules.Identity.Group,out var group)&&group.CampLoaded,"A real camp and adult resident load for collision-aware local navigation");
        var actor=livingPeople.Values.First(p=>p.GroupId==group.Situation.Band.Id&&p.Resident.AgeYears>=18);paused=true;livingQueryAt=Time.unscaledTimeAsDouble+1000;while(livingWork!=null)yield return null;
        TerrainHandoff.Coordinates(frame,group.Anchor,out double x,out double z);z+=16;double floor=CampFloor(x,z);
        actor.Motor.Place(x,z,floor);actor.Planet=frame.Position(x,z);actor.Root.transform.localPosition=new Vector3((float)(x-originX),(float)floor,(float)(z-originZ));actor.Yaw=actor.TargetYaw=90;actor.Speed=0;actor.HasSafeGoal=false;actor.Threat=null;actor.Daily=null;
        motion.Place(x+10,z,CampFloor(x+10,z));motion.Flying=true;NpcAwarenessFor(actor).NextSense=Time.unscaledTimeAsDouble+1000;
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Navigation QA actual solid wall";wall.layer=ScatterSolidLayer;wall.transform.SetParent(transform,false);wall.transform.localPosition=new Vector3((float)(x+4-originX),(float)floor+1.5f,(float)(z-originZ));wall.transform.localScale=new Vector3(.45f,3,6);Physics.SyncTransforms();
        npcPlans[actor.ResidentId]=new MindPlan{Decision=new NpcDecision{action="approach",target="player",seconds=120},Event=new NpcEventQueue.Event{Person=actor.ResidentId},Until=Time.unscaledTimeAsDouble+120};
        double lateral=0,progressAt=0;end=Time.unscaledTimeAsDouble+65;
        while(actor.Motor.X<x+6&&Time.unscaledTimeAsDouble<end){actor.Threat=null;UpdateLivingHuman(actor,false,Math.Min(.1f,Time.unscaledDeltaTime));lateral=Math.Max(lateral,Math.Abs(actor.Motor.Z-z));if(Time.unscaledTimeAsDouble>=progressAt){progressAt=Time.unscaledTimeAsDouble+1;File.WriteAllText(Path.Combine(output,"navigation-state.txt"),"x="+(actor.Motor.X-x)+"; z="+(actor.Motor.Z-z)+"; speed="+actor.Speed+"; yaw="+actor.Yaw+"; avoid="+actor.AvoidYaw+"; lateral="+lateral);}yield return null;}
        Check(actor.Motor.X>x+6&&lateral>3,"The real capsule walks around a six-metre solid barrier instead of crossing it or spinning against it");
        Destroy(wall);npcPlans.Remove(actor.ResidentId);CaptureLivingPerson(actor);LivingState.Copy().Validate();Check(true,"The collision-aware resident retains a valid persistent identity and position");yield return null;DisposeNpcMinds();
    }
}
