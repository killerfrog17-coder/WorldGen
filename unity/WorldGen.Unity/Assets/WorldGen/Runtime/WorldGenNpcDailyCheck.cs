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
        while((!livingGroups.TryGetValue(PlayerRules.Identity.Group,out var ready)||!ready.CampLoaded||livingPeople.Values.Count(p=>p.GroupId==PlayerRules.Identity.Group&&p.Resident.AgeYears>=3&&p.Resident.AgeYears<14)<2||livingPeople.Values.Count(p=>p.GroupId==PlayerRules.Identity.Group&&p.Health>0)<15)&&Time.unscaledTimeAsDouble<until)yield return null;
        Check(livingGroups.TryGetValue(PlayerRules.Identity.Group,out var group)&&group.CampLoaded,"The controlled resident's actual camp is loaded");
        var kids=livingPeople.Values.Where(p=>p.GroupId==PlayerRules.Identity.Group&&p.Resident.AgeYears>=3&&p.Resident.AgeYears<14).Take(2).ToArray();Check(kids.Length==2,"Two actual generated children are present, without inserting fake residents");paused=true;
        livingQueryAt=Time.unscaledTimeAsDouble+45;while(livingWork!=null)yield return null;
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
        // Controlled real-resident sight probe: more than one sensory batch,
        // all on the hearth's known clear ground. No invented people or
        // remembered locations substitute for the actual eyes/occlusion path.
        var observer=adults[0];var crowd=livingPeople.Values.Where(p=>p.GroupId==observer.GroupId&&p!=observer&&p.Health>0).Take(14).ToArray();
        Check(crowd.Length>12,"More than twelve actual bandmates are available for the fair visual-check probe");
        var placements=crowd.Concat(new[]{observer}).Select(p=>(Actor:p,X:p.Motor.X,Z:p.Motor.Z,Yaw:p.Yaw,Planet:p.Planet,Activity:p.CurrentActivity,Age:p.SimulationAge,Daily:p.Daily)).ToArray();
        var observerMemory=NpcMemoryFor(observer.ResidentId);var previousSocial=observerMemory.Social.Copy();var eyes=NpcAwarenessFor(observer);int previousCursor=eyes.PeopleCursor;
        try
        {
            Place(observer,cx,cz,0);observer.CurrentActivity=HumanActivity.Watch;
            foreach(var p in crowd)Place(p,cx+.6,cz+.8,180);
            Physics.SyncTransforms();observerMemory.Social.KnownPeople.RemoveAll(k=>crowd.Any(p=>p.ResidentId==k.Person));
            for(int i=0;i<(livingPeople.Count+11)/12+1;i++){observer.SimulationAge+=2;eyes.NextSense=0;npcSenseFrame=-1;UpdateNpcAwareness(observer);}
            Check(crowd.All(p=>observerMemory.Social.KnownPeople.Any(k=>k.Person==p.ResidentId&&HumanDistance(k.LastPosition,p.Planet)<.1)),"Bounded visual checks eventually recognise every visible actual bandmate beyond the first twelve");
        }
        finally
        {
            foreach(var p in placements){Place(p.Actor,p.X,p.Z,p.Yaw);p.Actor.Planet=p.Planet;p.Actor.CurrentActivity=p.Activity;p.Actor.SimulationAge=p.Age;p.Actor.Daily=p.Daily;}
            observerMemory.Social=previousSocial;eyes.PeopleCursor=previousCursor;eyes.People.Clear();eyes.NextSense=0;Physics.SyncTransforms();
        }
        File.WriteAllText(Path.Combine(output,"family-roster.json"),JsonUtility.ToJson(group.Residents,true));
        var parent=adults.FirstOrDefault(p=>group.Residents.Members.Any(k=>k.AgeYears<14&&(k.Mother==p.ResidentId||k.Father==p.ResidentId||k.Household==p.Resident.Household)));Check(parent!=null,"A real parent or household caregiver is physically present with children");
        var child=group.Residents.Members.First(k=>k.AgeYears<14&&(k.Mother==parent.ResidentId||k.Father==parent.ResidentId||k.Household==parent.Resident.Household));var social=NpcMemoryFor(parent.ResidentId).Social;
        social.See(child.Id,frame.Position(cx+20,cz+5),Math.Max(0,PlayerRules.SolarDays-1));
        double previousDay=PlayerRules.SolarDays;PlayerRules.SolarDays+=1;parent.Planet=group.Anchor;parent.SimulationAge=100;parent.Daily=null;
        var home=new HumanActivitySample{Activity=HumanActivity.Rest,Position=group.Anchor};V goal=group.Anchor;PrepareNpcDailyTask(parent,group,home,ref goal);
        Check(social.Incidents.Any(e=>e.Kind==NpcIncidentKind.Missing&&e.Subject==child.Id),"A household caregiver worries about a genuinely known overdue child without automatically requiring a search");
        Check(!social.KnownPeople.First(p=>p.Person==child.Id).SeenDead&&!social.Incidents.Any(e=>e.Kind==NpcIncidentKind.Death&&e.Subject==child.Id),"Absence does not reveal death or the child's current location");
        social.See(child.Id,kids[0].Planet,PlayerRules.SolarDays+.0001);PrepareNpcDailyTask(parent,group,home,ref goal);
        Check(parent.Daily.Missing==0&&social.Incidents.Any(e=>e.Kind==NpcIncidentKind.Returned&&e.Subject==child.Id),"Actually seeing the child again resolves the search");PlayerRules.SolarDays=previousDay;
        var adultRelative=adults.First(p=>p!=parent).Resident;social.Tie(adultRelative.Id,PlayerRules.SolarDays).Affection=.65;
        Check(adultRelative!=null,"A real adult bandmate has an established friendship for scheduled-absence checks");
        social.See(adultRelative.Id,group.Anchor,PlayerRules.SolarDays);var absent=social.KnownPeople.First(k=>k.Person==adultRelative.Id);
        NpcAbsence.Expect(absent,PlayerRules.SolarDays,8,12,adultRelative.AgeYears,true,new HumanActivitySample{DepartureSolarDay=PlayerRules.SolarDays,ExpectedReturnSolarDay=PlayerRules.SolarDays+.4,OutingPurpose="foraging with a party"});
        PlayerRules.SolarDays+=.3;parent.Daily=new NpcDailyState();parent.SimulationAge=100;social.Incidents.RemoveAll(e=>e.Kind==NpcIncidentKind.Missing);absent.ConcernNextSolarDay=0;
        PrepareNpcDailyTask(parent,group,home,ref goal);
        Check(!social.Incidents.Any(e=>e.Kind==NpcIncidentKind.Missing&&e.Subject==adultRelative.Id)&&parent.Daily.Missing!=adultRelative.Id,"Seven hours away on a real expected party schedule does not create a missing alarm or search");
        social.Incidents.Add(new NpcIncident{Sequence=++social.EventSequence,Kind=NpcIncidentKind.Missing,Actor=adults.First(a=>a!=parent).ResidentId,Subject=adultRelative.Id,SolarDay=PlayerRules.SolarDays,Position=group.Anchor,FirstHand=false,Confidence=.75,Detail="A bandmate is concerned."});parent.Daily=null;
        PrepareNpcDailyTask(parent,group,home,ref goal);
        Check(parent.Daily.Missing!=adultRelative.Id,"A reported concern does not automatically restore an unaccepted search");PlayerRules.SolarDays=previousDay;
        var privateBed=Camp.Objects.FirstOrDefault(c=>c.Kind==CampKind.Bedding&&c.NpcGroup==group.Situation.Band.Id&&c.NpcHousehold!=PlayerRules.Identity.Person.Household);
        Check(privateBed!=null&&!PlayerMayUseCamp(privateBed),"An actual other family's sleeping place is marked private");
        var helper=adults.FirstOrDefault(p=>p!=parent&&NpcAwarenessFor(p).Personality.Courage>=.2&&NpcAwarenessFor(p).Personality.Kindness+NpcCloseness(p,parent)>=.65);Check(helper!=null,"An actual willing adult is available for the spoken search request");
        Place(parent,cx+3,cz+3,90);parent.CurrentActivity=HumanActivity.Rest;Place(helper,cx+4,cz+3,270);helper.CurrentActivity=HumanActivity.Rest;
        foreach(var adult in adults.Where(p=>p!=parent)){adult.Daily=null;NpcMemoryFor(adult.ResidentId).Social.Incidents.RemoveAll(e=>e.Kind==NpcIncidentKind.Missing&&e.Subject==child.Id);}
        social.Incidents.RemoveAll(e=>e.Subject==child.Id&&(e.Kind==NpcIncidentKind.Missing||e.Kind==NpcIncidentKind.Returned));
        if(livingPeople.TryGetValue(child.Id,out var closeChild))
        {
            Place(closeChild,cx+3,cz+4,0);Check(RecruitNpcSearch(parent,child,frame.Position(cx+20,cz+5))==0&&social.Incidents.Any(e=>e.Kind==NpcIncidentKind.Returned&&e.Subject==child.Id),"An awake missing person within earshot answers the call instead of being recruited to search for themselves");
            Place(closeChild,cx+40,cz+25,0);
        }
        int helpers=RecruitNpcSearch(parent,child,frame.Position(cx+20,cz+5));
        Check(helpers>0&&livingPeople.Values.Any(p=>p!=parent&&p.Daily?.Missing==child.Id&&NpcMemoryFor(p.ResidentId).Social.Incidents.Any(e=>e.Kind==NpcIncidentKind.Missing&&e.Subject==child.Id&&!e.FirstHand)),"An audible request recruits actual nearby adults with explicitly reported last-known information");
        Check(livingPeople.Values.Where(p=>p!=parent&&p.Daily?.Missing==child.Id).All(p=>!NpcMemoryFor(p.ResidentId).Social.KnownPeople.Any(k=>k.Person==child.Id&&k.SeenDead)),"Search recruits do not gain supernatural knowledge of an unseen death");
        var baby=group.Residents.Members.FirstOrDefault(p=>p.AgeYears<3);Check(baby!=null,"The actual household roster includes a young child for the caregiver check");
        double babyUntil=Time.unscaledTimeAsDouble+30;
        while(!livingPeople.ContainsKey(baby.Id)&&Time.unscaledTimeAsDouble<babyUntil){CreateLivingHuman(group,baby,new HumanActivitySample{Person=baby.Id,Group=group.Situation.Band.Id,Activity=HumanActivity.Carried,Partner=0,Position=group.Anchor,Destination=group.Anchor},group.Anchor,LivingState.People.FirstOrDefault(p=>p.Person==baby.Id));yield return null;}
        Check(livingPeople.TryGetValue(baby.Id,out var infant),"The normal resident/model factory creates that same real young child");
        double bx=cx+42,bz=cz+27;Place(infant,bx,bz,0);infant.Routine=new HumanActivitySample{Person=baby.Id,Group=infant.GroupId,Activity=HumanActivity.Carried,Partner=0,Position=infant.Planet};NpcPersonRecord(infant).Caregiver=long.MaxValue;
        // Ensure the controlled child is genuinely separated from all eligible adults.
        foreach(var adult in adults)if(HumanDistance(adult.Planet,infant.Planet)<3)Place(adult,cx+5,cz+3,0);
        var putDown=infant.Planet;UpdateCarriedHuman(infant,.1f);Check(HumanDistance(infant.Planet,putDown)<.05&&infant.CurrentActivity==HumanActivity.Rest,"Without a reachable caregiver the young child remains where put down instead of teleporting to a distant adult");
        Place(helper,bx+.9,bz,270);helper.CurrentActivity=HumanActivity.Rest;helper.Routine=new HumanActivitySample{Activity=HumanActivity.Rest,Position=helper.Planet};helper.Speed=helper.TargetSpeed=0;UpdateCarriedHuman(infant,.1f);
        Check(NpcPersonRecord(infant).Caregiver==helper.ResidentId&&HumanDistance(infant.Planet,helper.Planet)<.1,"An actual adult who approaches within reach can take over care");
        Check(NpcMemoryFor(helper.ResidentId).Social.Incidents.Any(e=>e.Kind==NpcIncidentKind.Help&&e.Subject==infant.ResidentId),"The real caregiver remembers taking responsibility for the child");
        LivingState.Copy().Validate();Check(true,"Sleep/play/search and social memories remain valid local state");
        // Aim the actual first-person camera at children in the camp, not an
        // unrelated horizon. This is an evidence scene, not an FPS benchmark.
        motion.Place(cx-7,cz+3,CampFloor(cx-7,cz+3));humanCamera.Restore(0,false);UpdateHumans();
        for(int i=0;i<8;i++){UpdateCamera();var d=transform.InverseTransformDirection(kids[0].Root.transform.position+transform.up*.6f-view.transform.position).normalized;heading=Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg;pitch=-Mathf.Asin(d.y)*Mathf.Rad2Deg;UpdateHumans();}
        var startA=kids[0].Planet;var startB=kids[1].Planet;double playUntil=Time.unscaledTimeAsDouble+10;bool captured=false;npcThoughts=true;paused=false;UpdateCamera();
        while(Time.unscaledTimeAsDouble<playUntil)
        {
            if(!captured&&kids.Any(p=>p.Speed>.7f)){captured=true;yield return NpcLifeImage(output,"children-at-camp");}
            yield return null;
        }
        paused=true;Check(Math.Max(HumanDistance(startA,kids[0].Planet),HumanDistance(startB,kids[1].Planet))>.8&&kids.Any(p=>p.CurrentActivity==HumanActivity.Play),"Children actually move through the camp during ten seconds of normal play updates");
        Check(captured,"The play evidence image is captured during actual movement");
        File.WriteAllText(Path.Combine(output,"daily-memory.json"),JsonUtility.ToJson(NpcMemoryFor(parent.ResidentId),true));paused=false;DisposeNpcMinds();
    }
}
