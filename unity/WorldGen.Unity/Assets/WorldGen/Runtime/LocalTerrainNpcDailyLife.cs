using System;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    sealed class NpcDailyState
    {
        public long Playmate,Missing;public double ChooseAt,ConcernAt,SearchUntil,SearchStarted,SearchPointAt,HideUntil;
        public V PlayGoal,HideGoal,SearchAnchor,SearchGoal;public string PlayRole="";
    }
    bool PrepareNpcDailyTask(HumanActor actor,LivingGroup group,HumanActivitySample routine,ref V target)
    {
        if(actor.Resident.AgeYears>=3&&actor.Resident.AgeYears<14&&routine.Activity==HumanActivity.Play)
        {PrepareNpcPlay(actor,group,routine,ref target);return true;}
        if(actor.Resident.AgeYears<16)return false;
        bool resume=actor.Daily==null;var daily=actor.Daily??(actor.Daily=new NpcDailyState());var social=NpcMemoryFor(actor.ResidentId).Social;
        if(resume)
        {
            // Only a saved, explicitly accepted search resumes. A reported
            // absence or general worry is not an instruction to go searching.
            var accepted=social.KnownPeople.FirstOrDefault(k=>!k.SeenDead&&k.SearchUntilSolarDay>PlayerRules.SolarDays);
            if(accepted!=null)BeginNpcSearch(actor,accepted.Person,accepted.LastPosition,accepted.SearchUntilSolarDay,false);
        }
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
            if(last?.SeenDead==true||actor.SimulationAge>daily.SearchUntil||last!=null&&last.SearchUntilSolarDay>0&&last.SearchUntilSolarDay<=PlayerRules.SolarDays||social.Incidents.Any(e=>e.Kind==NpcIncidentKind.Returned&&e.Subject==daily.Missing&&e.SolarDay>=(concern?.SolarDay??0))){if(last!=null){last.SearchUntilSolarDay=0;last.ConcernNextSolarDay=PlayerRules.SolarDays+.12;}daily.Missing=0;daily.ConcernAt=actor.SimulationAge+60;}
            else if(last!=null&&concern!=null&&last.LastSeenSolarDay>concern.SolarDay+1e-8)
            {
                social.Record(new NpcIncident{Kind=NpcIncidentKind.Returned,Subject=daily.Missing,SolarDay=PlayerRules.SolarDays,Position=last.LastPosition,Detail="You personally found the person you were worried about alive."});social.Contentment=Math.Min(1,social.Contentment+.25);daily.Missing=0;daily.ConcernAt=actor.SimulationAge+60;
            }
            else
            {
                if(actor.SimulationAge>=daily.SearchPointAt)
                {daily.SearchPointAt=actor.SimulationAge+8;TerrainHandoff.Coordinates(frame,daily.SearchAnchor,out double x,out double z);double phase=actor.SimulationAge*.15+HumanHash(actor.ResidentId)%628*.01;double radius=Math.Min(65,Math.Max(0,(actor.SimulationAge-daily.SearchStarted)*.25));daily.SearchGoal=frame.Position(x+Math.Cos(phase)*radius,z+Math.Sin(phase)*radius);}
                target=daily.SearchGoal.Length>.9?daily.SearchGoal:daily.SearchAnchor;actor.CurrentActivity=HumanActivity.Watch;actor.TaskWaiting=false;actor.TaskDetail="searching near the last seen place for "+(group.Residents.Members.FirstOrDefault(p=>p.Id==daily.Missing)?.Name??"a missing bandmate");return true;
            }
        }
        if(actor.SimulationAge<8||actor.SimulationAge<daily.ConcernAt||routine.Activity==HumanActivity.Sleep||routine.Activity==HumanActivity.Travel||HumanDistance(actor.Planet,group.Anchor)>35)return false;
        daily.ConcernAt=actor.SimulationAge+8+HumanHash(actor.ResidentId)%50*.1;
        foreach(var known in social.KnownPeople.OrderBy(p=>p.LastSeenSolarDay))
        {
            if(known.SeenDead)continue;var person=group.Residents.Members.FirstOrDefault(p=>p.Id==known.Person);if(person==null)continue;
            bool closeKin=person.Mother==actor.ResidentId||person.Father==actor.ResidentId||actor.Resident.Partner==person.Id||actor.Resident.Mother==person.Id||actor.Resident.Father==person.Id;
            var tie=social.Relationships.FirstOrDefault(t=>t.Other==person.Id);closeKin|=person.Household==actor.Resident.Household||tie!=null&&(tie.Affection>.5||tie.Trust>.65);
            if(person.AgeYears<14&&(person.Household==actor.Resident.Household||routine.Activity==HumanActivity.Childcare&&sourceWorld.Prehistory.Foragers.Cultures[group.Residents.Culture].ChildcareCooperation>.5))closeKin=true;
            if(!closeKin||known.ConcernNextSolarDay>PlayerRules.SolarDays||social.Incidents.Any(e=>e.Kind==NpcIncidentKind.Returned&&e.Subject==person.Id&&PlayerRules.SolarDays-e.SolarDay<.08))continue;
            if(string.IsNullOrEmpty(known.ExpectedReason))
            {
                // Bootstrap only from the saved last sighting and customary
                // check-in. Never inspect the absent actor's current position.
                double lastHour=((group.Situation.LocalHour-(PlayerRules.SolarDays-known.LastSeenSolarDay)*24)%24+24)%24;
                NpcAbsence.Expect(known,known.LastSeenSolarDay,lastHour,group.Situation.DaylightHours,person.AgeYears,HumanDistance(known.LastPosition,group.Anchor)<35,null);
            }
            double overdue=NpcAbsence.OverdueHours(known,PlayerRules.SolarDays,person.AgeYears,CurrentWeather.RainMmPerHour);
            var personality=NpcAwarenessFor(actor).Personality;bool light=group.Situation.LocalHour>12-group.Situation.DaylightHours/2+.2&&group.Situation.LocalHour<12+group.Situation.DaylightHours/2-.2;
            bool duty=routine.Activity==HumanActivity.Childcare||routine.Activity==HumanActivity.Watch||routine.Role=="NightWatch";
            bool distress=social.Incidents.Any(e=>e.Kind==NpcIncidentKind.DistressHeard&&e.Subject==person.Id&&PlayerRules.SolarDays-e.SolarDay<.08);
            double variation=Rng.Value(sourceWorld.Recipe.Seed,"absence-response",unchecked((int)actor.ResidentId^(int)person.Id^(int)(known.ExpectedReturnSolarDay*24)));
            var response=NpcAbsence.Response(overdue,person.AgeYears,.9,personality.Courage,personality.Kindness,personality.Patience,group.Situation.SecurityRisk,light,duty,distress,variation);
            if(response==NpcAbsenceResponse.None)continue;
            bool first=!social.Incidents.Any(e=>e.Kind==NpcIncidentKind.Missing&&e.Subject==person.Id&&e.SolarDay>=known.LastSeenSolarDay);
            if(first)
            {
                known.ConcernSinceSolarDay=PlayerRules.SolarDays;social.Fear=Math.Max(social.Fear,person.AgeYears<14?.2:.1);
                social.Record(new NpcIncident{Kind=NpcIncidentKind.Missing,Subject=person.Id,SolarDay=PlayerRules.SolarDays,Position=known.LastPosition,Detail=person.Name+" is overdue after "+known.ExpectedReason+". You do not know their present location or fate."});
                QueueNpcEvent(actor,"You are concerned that "+person.Name+" is overdue after "+known.ExpectedReason+". This is concern, not proof of harm; you have not automatically decided to search.",priority:2);
            }
            known.ConcernNextSolarDay=PlayerRules.SolarDays+.02;
            if(response==NpcAbsenceResponse.Search)
            {BeginNpcSearch(actor,person.Id,known.LastPosition,PlayerRules.SolarDays+.06,true);RecruitNpcSearch(actor,person,known.LastPosition);break;}
            actor.TaskDetail=response==NpcAbsenceResponse.WaitForDaylight?"concerned about "+person.Name+"; staying at camp until daylight":response==NpcAbsenceResponse.AskAround?"asking bandmates whether they have heard from "+person.Name:"concerned about "+person.Name+" being overdue; continuing normal duties";
            if(response==NpcAbsenceResponse.AskAround)AskNpcForNews(actor,person);
        }
        return false;
    }
    void BeginNpcSearch(HumanActor actor,long missing,V anchor,double until,bool notify,double sourceSightDay=-1)
    {
        var daily=actor.Daily??(actor.Daily=new NpcDailyState());var social=NpcMemoryFor(actor.ResidentId).Social;
        var known=social.KnownPeople.FirstOrDefault(k=>k.Person==missing);
        if(known==null){known=new NpcKnownPerson{Person=missing,LastPosition=anchor,LastSeenSolarDay=Math.Max(0,sourceSightDay>=0?sourceSightDay:PlayerRules.SolarDays-.1),ReportedPresence=true,ExpectedReason="a reported overdue return"};social.KnownPeople.Add(known);if(social.KnownPeople.Count>64)social.KnownPeople.RemoveAt(0);}
        known.SearchUntilSolarDay=until;daily.Missing=missing;daily.SearchAnchor=daily.SearchGoal=anchor;daily.SearchStarted=actor.SimulationAge;daily.SearchUntil=actor.SimulationAge+Math.Max(1,(until-PlayerRules.SolarDays)*PlayerRules.Settings.RealMinutesPerDay*60);daily.SearchPointAt=actor.SimulationAge+2;
        if(notify)QueueNpcEvent(actor,"You have decided to make a bounded search near the last known place for an overdue bandmate. Their actual whereabouts and fate are unknown.",priority:2);
    }
    void AskNpcForNews(HumanActor actor,ForagerPersonSample missing)
    {
        foreach(var peer in livingPeople.Values)
        {
            if(peer==actor||peer.GroupId!=actor.GroupId||peer.Health<=0||HumanDistance(peer.Planet,actor.Planet)>5||!NpcCanHear(actor,peer.Root.transform.position+Vector3.up,5))continue;
            var heard=NpcMemoryFor(peer.ResidentId).Social.KnownPeople.FirstOrDefault(k=>k.Person==missing.Id);
            var own=NpcMemoryFor(actor.ResidentId).Social.KnownPeople.First(k=>k.Person==missing.Id);
            if(heard==null||heard.LastSeenSolarDay<=own.LastSeenSolarDay||heard.SeenDead)continue;
            // A report can reassure without pretending the listener saw it.
            if(PlayerRules.SolarDays-heard.LastSeenSolarDay<.03)
            {
                NpcMemoryFor(actor.ResidentId).Social.Record(new NpcIncident{Kind=NpcIncidentKind.Returned,Actor=peer.ResidentId,Subject=missing.Id,SolarDay=PlayerRules.SolarDays,Position=heard.LastPosition,FirstHand=false,Reporter=peer.ResidentId,Confidence=.75,Detail=peer.Resident.Name+" reports recently seeing "+missing.Name+" alive. You did not personally see this."});
                own.ConcernSinceSolarDay=own.SearchUntilSolarDay=0;own.ConcernNextSolarDay=PlayerRules.SolarDays+.08;return;
            }
        }
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
            BeginNpcSearch(helper,missing.Id,lastKnown,PlayerRules.SolarDays+.06,false,NpcMemoryFor(requester.ResidentId).Social.KnownPeople.FirstOrDefault(k=>k.Person==missing.Id)?.LastSeenSolarDay??-1);
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
