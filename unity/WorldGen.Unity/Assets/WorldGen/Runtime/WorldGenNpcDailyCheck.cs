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
    public IEnumerator CheckNpcDailyLife(string output,List<string> report)
    {
        void Check(bool ok,string text){if(!ok){File.WriteAllLines(Path.Combine(output,"failed-checks.txt"),report.Concat(new[]{"FAIL "+text}));Application.Quit(1);throw new InvalidOperationException(text);}report.Add("PASS "+text);Debug.Log("NPC DAILY PASS "+text);}
        npcModel??=new NpcModelClient();npcModel.Config.Enabled=false;PlayerRules.Mode=GameMode.Creative;ApplyPlayerMode();
        double until=Time.unscaledTimeAsDouble+100;
        while((!livingGroups.TryGetValue(PlayerRules.Identity.Group,out var ready)||!ready.CampLoaded||livingPeople.Values.Count(p=>p.GroupId==PlayerRules.Identity.Group&&p.Resident.AgeYears>=3&&p.Resident.AgeYears<14)<2)&&Time.unscaledTimeAsDouble<until)yield return null;
        Check(livingGroups.TryGetValue(PlayerRules.Identity.Group,out var group)&&group.CampLoaded,"The controlled resident's actual camp is loaded");
        var kids=livingPeople.Values.Where(p=>p.GroupId==PlayerRules.Identity.Group&&p.Resident.AgeYears>=3&&p.Resident.AgeYears<14).Take(2).ToArray();Check(kids.Length==2,"Two actual generated children are present, without inserting fake residents");paused=true;
        var fire=Camp.Objects.FirstOrDefault(c=>c.NpcGroup==group.Situation.Band.Id&&c.Kind==CampKind.Fire);Check(fire!=null,"The actual community has a shared hearth");
        if(!fire.Burning&&CampRules.IgniteReason(fire,false).Length==0)CampRules.Ignite(fire,false);
        TerrainHandoff.Coordinates(frame,fire.Position,out double cx,out double cz);campMaskCheckAt=0;UpdateCampClearing();Check(CampGroundClearing(cx,cz)>.9,"The actual hearth's trampled clearing is available to perception");
        if(HuntingColumn(cx,cz,out var clear,out _,out _))Check(clear.Grasses.Length<.01,"NPC/animal sight does not use invisible grass inside the cleared court");
        void Place(HumanActor p,double x,double z,float yaw){p.Motor.Place(x,z,CampFloor(x,z));p.Planet=frame.Position(x,z);p.Root.transform.localPosition=new Vector3((float)(x-originX),(float)(p.Motor.Y-TerrainMotion.EyeHeightM),(float)(z-originZ));p.Yaw=p.TargetYaw=yaw;p.Root.transform.localRotation=Quaternion.Euler(0,yaw,0);p.CurrentActivity=HumanActivity.Play;p.Threat=null;p.SimulationAge=40;p.Daily=null;}
        Place(kids[0],cx-2.8,cz+1,90);Place(kids[1],cx+2.8,cz+1,270);
        for(int i=0;i<2;i++)kids[i].Routine=new HumanActivitySample{Person=kids[i].ResidentId,Group=kids[i].GroupId,Activity=HumanActivity.Play,Role="PlayChase",Partner=kids[1-i].ResidentId,Position=kids[i].Planet,Destination=kids[1-i].Planet,SpeedMps=kids[i].Resident.AgeYears<6?1.8:2.6};
        V first=kids[0].Planet,second=kids[1].Planet;PrepareNpcDailyTask(kids[0],group,kids[0].Routine,ref first);PrepareNpcDailyTask(kids[1],group,kids[1].Routine,ref second);
        Check(kids[0].Daily.Playmate==kids[1].ResidentId&&kids[1].Daily.Playmate==kids[0].ResidentId,"Physical play uses the actual other child as a partner");
        Check(kids.All(p=>p.TaskDetail.Contains("chasing game")&&p.TaskDetail.Contains(kids.First(o=>o!=p).Resident.Name)),"Debug activities name the actual playmate and game");
        Check(fire.Burning,"The existing material-backed hearth is actually lit for the crossing check");var across=frame.Position(cx+3,cz+1);var around=NpcPlayAroundHearth(kids[0],across);Check(HumanDistance(around,fire.Position)>2.5&&HumanDistance(around,across)>.5,"A chasing child routes around a lit hearth rather than through its centre");
        var adults=livingPeople.Values.Where(p=>p.GroupId==group.Situation.Band.Id&&p.Resident.AgeYears>=18).ToArray();Check(adults.Length>1,"Actual adult bandmates are available");
        var parent=adults.FirstOrDefault(p=>group.Residents.Members.Any(k=>k.AgeYears<14&&(k.Mother==p.ResidentId||k.Father==p.ResidentId)));Check(parent!=null,"A real parent-child relationship exists");
        var child=group.Residents.Members.First(k=>k.AgeYears<14&&(k.Mother==parent.ResidentId||k.Father==parent.ResidentId));var social=NpcMemoryFor(parent.ResidentId).Social;
        social.See(child.Id,frame.Position(cx+20,cz+5),Math.Max(0,PlayerRules.SolarDays-1));
        double previousDay=PlayerRules.SolarDays;PlayerRules.SolarDays+=1;parent.Planet=group.Anchor;parent.SimulationAge=100;parent.Daily=null;
        var home=new HumanActivitySample{Activity=HumanActivity.Rest,Position=group.Anchor};V goal=group.Anchor;PrepareNpcDailyTask(parent,group,home,ref goal);
        Check(parent.Daily.Missing==child.Id&&social.Incidents.Any(e=>e.Kind==NpcIncidentKind.Missing&&e.Subject==child.Id),"A parent worries about a genuinely known overdue child using the last seen place");
        Check(!social.KnownPeople.First(p=>p.Person==child.Id).SeenDead&&!social.Incidents.Any(e=>e.Kind==NpcIncidentKind.Death&&e.Subject==child.Id),"Absence does not reveal death or the child's current location");
        social.See(child.Id,kids[0].Planet,PlayerRules.SolarDays+.0001);PrepareNpcDailyTask(parent,group,home,ref goal);
        Check(parent.Daily.Missing==0&&social.Incidents.Any(e=>e.Kind==NpcIncidentKind.Returned&&e.Subject==child.Id),"Actually seeing the child again resolves the search");PlayerRules.SolarDays=previousDay;
        var privateBed=Camp.Objects.FirstOrDefault(c=>c.Kind==CampKind.Bedding&&c.NpcGroup==group.Situation.Band.Id&&c.NpcHousehold!=PlayerRules.Identity.Person.Household);
        Check(privateBed!=null&&!PlayerMayUseCamp(privateBed),"An actual other family's sleeping place is marked private");
        LivingState.Copy().Validate();Check(true,"Sleep/play/search and social memories remain valid local state");
        // Aim the actual first-person camera at children in the camp, not an
        // unrelated horizon. This is an evidence scene, not an FPS benchmark.
        motion.Place(cx-7,cz+3,CampFloor(cx-7,cz+3));humanCamera.Restore(0,false);UpdateHumans();
        for(int i=0;i<8;i++){UpdateCamera();var d=transform.InverseTransformDirection(kids[0].Root.transform.position+transform.up*.6f-view.transform.position).normalized;heading=Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg;pitch=-Mathf.Asin(d.y)*Mathf.Rad2Deg;UpdateHumans();}
        npcThoughts=true;paused=false;UpdateCamera();yield return NpcLifeImage(output,"children-at-camp");paused=true;
        File.WriteAllText(Path.Combine(output,"daily-memory.json"),JsonUtility.ToJson(NpcMemoryFor(parent.ResidentId),true));paused=false;DisposeNpcMinds();
    }
}
