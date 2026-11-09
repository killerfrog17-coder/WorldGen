using System;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    sealed class NpcDailyState
    {
        public long Playmate,Missing;public double ChooseAt,ConcernAt,SearchUntil,SearchPointAt,HideUntil;
        public V PlayGoal,HideGoal,SearchAnchor,SearchGoal;public string PlayRole="";
    }
    bool PrepareNpcDailyTask(HumanActor actor,LivingGroup group,HumanActivitySample routine,ref V target)
    {
        if(actor.Resident.AgeYears>=3&&actor.Resident.AgeYears<14&&routine.Activity==HumanActivity.Play)
        {PrepareNpcPlay(actor,group,routine,ref target);return true;}
        if(actor.Resident.AgeYears<16)return false;
        var daily=actor.Daily??(actor.Daily=new NpcDailyState());var social=NpcMemoryFor(actor.ResidentId).Social;
        if(social.Grief>.3&&(routine.Activity==HumanActivity.Rest||routine.Activity==HumanActivity.Socialise||routine.Activity==HumanActivity.Teach))
        {
            var loss=social.Incidents.LastOrDefault(e=>e.Kind==NpcIncidentKind.Death&&PlayerRules.SolarDays-e.SolarDay<2);
            if(loss!=null&&((int)Math.Floor(PlayerRules.SolarDays*48)+HumanHash(actor.ResidentId))%4==0)
            {
                target=LivingHouse(actor)?.Position??group.Anchor;actor.CurrentActivity=HumanActivity.Rest;actor.TaskWaiting=false;
                string name=group.Residents.Members.FirstOrDefault(p=>p.Id==loss.Subject)?.Name??"a lost companion";actor.TaskDetail="taking a quiet interval to mourn "+name;return true;
            }
        }
        if(daily.Missing>0)
        {
            var last=social.KnownPeople.FirstOrDefault(p=>p.Person==daily.Missing);
            var concern=social.Incidents.LastOrDefault(e=>e.Kind==NpcIncidentKind.Missing&&e.Subject==daily.Missing);
            if(last?.SeenDead==true||actor.SimulationAge>daily.SearchUntil){daily.Missing=0;daily.ConcernAt=actor.SimulationAge+60;}
            else if(last!=null&&concern!=null&&last.LastSeenSolarDay>concern.SolarDay+1e-8)
            {
                social.Record(new NpcIncident{Kind=NpcIncidentKind.Returned,Subject=daily.Missing,SolarDay=PlayerRules.SolarDays,Position=last.LastPosition,Detail="You personally found the person you were worried about alive."});social.Contentment=Math.Min(1,social.Contentment+.25);daily.Missing=0;daily.ConcernAt=actor.SimulationAge+60;
            }
            else
            {
                if(actor.SimulationAge>=daily.SearchPointAt)
                {daily.SearchPointAt=actor.SimulationAge+8;TerrainHandoff.Coordinates(frame,daily.SearchAnchor,out double x,out double z);double phase=actor.SimulationAge*.15+HumanHash(actor.ResidentId)%628*.01;double radius=Math.Min(35,Math.Max(0,(actor.SimulationAge-daily.SearchUntil+100)*.2));daily.SearchGoal=frame.Position(x+Math.Cos(phase)*radius,z+Math.Sin(phase)*radius);}
                target=daily.SearchGoal.Length>.9?daily.SearchGoal:daily.SearchAnchor;actor.CurrentActivity=HumanActivity.Watch;actor.TaskWaiting=false;actor.TaskDetail="searching near the last seen place for "+(group.Residents.Members.FirstOrDefault(p=>p.Id==daily.Missing)?.Name??"a missing bandmate");return true;
            }
        }
        if(actor.SimulationAge<daily.ConcernAt||routine.Activity==HumanActivity.Sleep||routine.Activity==HumanActivity.Travel||HumanDistance(actor.Planet,group.Anchor)>35)return false;
        daily.ConcernAt=actor.SimulationAge+8+HumanHash(actor.ResidentId)%50*.1;
        foreach(var known in social.KnownPeople.OrderBy(p=>p.LastSeenSolarDay))
        {
            if(known.SeenDead)continue;var person=group.Residents.Members.FirstOrDefault(p=>p.Id==known.Person);if(person==null)continue;
            bool closeKin=person.Mother==actor.ResidentId||person.Father==actor.ResidentId||actor.Resident.Partner==person.Id||actor.Resident.Mother==person.Id||actor.Resident.Father==person.Id;
            if(person.AgeYears<14&&(person.Household==actor.Resident.Household||routine.Activity==HumanActivity.Childcare&&sourceWorld.Prehistory.Foragers.Cultures[group.Residents.Culture].ChildcareCooperation>.5))closeKin=true;
            double absence=(PlayerRules.SolarDays-known.LastSeenSolarDay)*24;bool evening=group.Situation.LocalHour>19||group.Situation.LocalHour<5;
            if(!closeKin||absence<(person.AgeYears<7?.35:person.AgeYears<14?evening?1:2:evening?4:12))continue;
            if(social.Incidents.Any(e=>e.Kind==NpcIncidentKind.Missing&&e.Subject==person.Id&&PlayerRules.SolarDays-e.SolarDay<.18))continue;
            social.Fear=Math.Max(social.Fear,.22);social.Record(new NpcIncident{Kind=NpcIncidentKind.Missing,Subject=person.Id,SolarDay=PlayerRules.SolarDays,Position=known.LastPosition,Detail="You are worried because "+person.Name+" has not been seen back when expected. Their fate is unknown; the last known place is only a starting point."});
            daily.Missing=person.Id;daily.SearchAnchor=daily.SearchGoal=known.LastPosition;daily.SearchUntil=actor.SimulationAge+100;daily.SearchPointAt=actor.SimulationAge+5;
            RecruitNpcSearch(actor,person,known.LastPosition);
            QueueNpcEvent(actor,"You are worried that "+person.Name+" is overdue. You only know where you last saw them; you do not know they are hurt or dead. You are looking for them.",priority:2);break;
        }
        return false;
    }
    int RecruitNpcSearch(HumanActor requester,ForagerPersonSample missing,V lastKnown)
    {
        int recruited=0;
        foreach(var helper in livingPeople.Values)
        {
            if(helper==requester||helper.GroupId!=requester.GroupId||helper.Health<55||helper.Resident.AgeYears<18||helper.Threat!=null||helper.Daily?.Missing>0||helper.CurrentActivity==HumanActivity.Sleep||helper.CurrentActivity==HumanActivity.Travel||helper.CurrentActivity==HumanActivity.Childcare||helper.Routine?.Role=="NightWatch")continue;
            if(HumanDistance(helper.Planet,requester.Planet)>12||!NpcCanHear(helper,requester.Root.transform.position+transform.up,12))continue;
            var personality=NpcAwarenessFor(helper).Personality;double closeness=NpcCloseness(helper,requester);
            if(personality.Kindness+closeness<.65||personality.Courage<.2)continue;
            var daily=helper.Daily??(helper.Daily=new NpcDailyState());daily.Missing=missing.Id;daily.SearchAnchor=daily.SearchGoal=lastKnown;daily.SearchUntil=helper.SimulationAge+100;daily.SearchPointAt=helper.SimulationAge+2;
            var social=NpcMemoryFor(helper.ResidentId).Social;social.Record(new NpcIncident{Kind=NpcIncidentKind.Missing,Actor=requester.ResidentId,Subject=missing.Id,SolarDay=PlayerRules.SolarDays,Position=lastKnown,FirstHand=false,Confidence=.75,Detail=requester.Resident.Name+" asked for help finding "+missing.Name+" and described the last known place. Their present whereabouts and fate are unknown."});
            QueueNpcEvent(helper,requester.Resident.Name+" has just asked you to help look for "+missing.Name+". You heard a last known place, not a current location or proof of an injury.",priority:2);
            if(++recruited==2)break;
        }
        return recruited;
    }
    void PrepareNpcPlay(HumanActor actor,LivingGroup group,HumanActivitySample routine,ref V target)
    {
        var daily=actor.Daily??(actor.Daily=new NpcDailyState());
        if(actor.SimulationAge>=daily.ChooseAt||daily.PlayRole!=routine.Role)
        {
            daily.ChooseAt=actor.SimulationAge+1.5;daily.PlayRole=routine.Role;
            var peers=livingPeople.Values.Where(p=>p!=actor&&p.GroupId==actor.GroupId&&p.Health>0&&p.Resident.AgeYears>=3&&p.Resident.AgeYears<14&&p.CurrentActivity==HumanActivity.Play&&HumanDistance(p.Planet,actor.Planet)<28);
            var peer=peers.OrderBy(p=>p.ResidentId==routine.Partner?0:1).ThenBy(p=>HumanDistance(p.Planet,actor.Planet)).FirstOrDefault();daily.Playmate=peer?.ResidentId??0;
            daily.PlayGoal=target;
            if(peer!=null)
            {
                bool sees=NpcSees(actor,peer.Root.transform.position+transform.up*.6f,28);bool seeker=(Math.Min(actor.ResidentId,peer.ResidentId)==actor.ResidentId)^((int)(actor.SimulationAge/18)%2==1);
                if(routine.Role=="PlayChase"&&seeker&&sees)daily.PlayGoal=peer.Planet;
                else if(routine.Role=="PlayMakeBelieve")
                {TerrainHandoff.Coordinates(frame,peer.Planet,out double x,out double z);daily.PlayGoal=frame.Position(x+1,z+1);}
                else if(routine.Role=="PlayHideSeek")
                {
                    if(seeker&&sees)daily.PlayGoal=peer.Planet;
                    else if(!seeker){if(actor.SimulationAge>=daily.HideUntil){daily.HideUntil=actor.SimulationAge+18;daily.HideGoal=NpcPlayHidingPlace(actor,peer,target);}daily.PlayGoal=daily.HideGoal;}
                    else {var last=NpcMemoryFor(actor.ResidentId).Social.KnownPeople.FirstOrDefault(p=>p.Person==peer.ResidentId);if(last!=null)daily.PlayGoal=last.LastPosition;}
                }
            }
        }
        target=NpcPlayAroundHearth(actor,daily.PlayGoal);actor.TaskWaiting=false;
        actor.TaskDetail=routine.Role=="PlayHideSeek"?"playing hide and seek":routine.Role=="PlayChase"?"playing a chasing game":"playing at making a camp";
        if(daily.Playmate>0&&livingPeople.TryGetValue(daily.Playmate,out var partner))actor.TaskDetail+=" with "+partner.Resident.Name;
    }
    V NpcPlayHidingPlace(HumanActor actor,HumanActor seeker,V fallback)
    {
        var here=actor.Root.transform.localPosition;double best=15*15;V result=fallback;
        foreach(var chunk in scatterChunks.Values)
        {
            if(chunk.Roots==null||chunk.AllBounds.SqrDistance(here)>best)continue;
            foreach(var batch in chunk.Groups)
            {
                if(batch.Bounds.SqrDistance(here)>best)continue;
                foreach(int i in batch.Indices)
                {
                    var p=chunk.Source.Instances[i];if(p.Kind!=ScatterKind.GeneratedTree&&p.Kind!=ScatterKind.Boulder&&p.Kind!=ScatterKind.MidPlant)continue;
                    var root=chunk.Roots[i];double d=(root-here).sqrMagnitude;if(d>=best)continue;
                    var away=root-seeker.Root.transform.localPosition;away.y=0;if(away.sqrMagnitude<.1)continue;away.Normalize();
                    var point=root+away*(p.Kind==ScatterKind.Boulder?(float)Math.Min(2,p.WidthM*.5+.5):.8f);result=frame.Position(point.x+originX,point.z+originZ);best=d;
                }
            }
        }
        return result;
    }
    V NpcPlayAroundHearth(HumanActor actor,V target)
    {
        TerrainHandoff.Coordinates(frame,target,out double x,out double z);double dx=x-actor.Motor.X,dz=z-actor.Motor.Z,length=dx*dx+dz*dz;
        if(length<.01)return target;
        foreach(var fire in Camp.Objects)
        {
            if(fire.Kind!=CampKind.Fire||!fire.Burning||HumanDistance(actor.Planet,fire.Position)>15)continue;
            TerrainHandoff.Coordinates(frame,fire.Position,out double fx,out double fz);double t=Math.Max(0,Math.Min(1,((fx-actor.Motor.X)*dx+(fz-actor.Motor.Z)*dz)/length));
            if(Math.Pow(actor.Motor.X+dx*t-fx,2)+Math.Pow(actor.Motor.Z+dz*t-fz,2)>2.6*2.6)continue;
            double angle=Math.Atan2(actor.Motor.Z-fz,actor.Motor.X-fx)+.55*(HumanHash(actor.ResidentId)%2==0?1:-1);return frame.Position(fx+Math.Cos(angle)*3.2,fz+Math.Sin(angle)*3.2);
        }
        return target;
    }
}
