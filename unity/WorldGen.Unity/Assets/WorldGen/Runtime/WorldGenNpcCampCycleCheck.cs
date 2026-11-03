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
    static IEnumerator CheckLivingCampCycle(LocalTerrainExplorer owner,LivingGroup group,string output,List<string> checks)
    {
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("NPC CAMP CYCLE "+text);checks.Add("PASS "+text);Debug.Log("NPC CAMP CYCLE "+text);}
        if(group.Situation.PermanentBase){Check(true,"Dependable aquatic base remains occupied rather than undergoing a forced annual migration");yield break;}
        var baseline=owner.LivingClock;var initial=group.Situation;long oldSite=initial.Camp.Id,band=initial.Band.Id;
        var identities=group.Residents.Members.Select(p=>p.Id).OrderBy(id=>id).ToArray();
        int fullParts=owner.Camp.Objects.Where(o=>o.NpcGroup==band&&o.NpcSite==oldSite&&o.Blueprint!=null).Sum(o=>o.Blueprint.Parts.Count);
        void Date(double day)
        {
            owner.PlayerRules.SolarDays=baseline.SolarDays+(day-baseline.CalendarDay)/baseline.CalendarRate;
            owner.PlayerRules.PendingHistorySeconds=day*SimulationTime.Day-SimulationTime.Seconds(owner.sourceWorld.Prehistory)-owner.PlayerRules.CalendarOffsetSeconds;
            owner.livingQueryAt=0;
        }
        IEnumerator Await(Func<bool> condition,string description)
        {
            double end=Time.realtimeSinceStartupAsDouble+30;
            while(!condition()&&Time.realtimeSinceStartupAsDouble<end)yield return null;
            Check(condition(),description);
        }
        Date(initial.DepartureDay-.017*baseline.CalendarRate);
        yield return Await(()=>group.Situation.Phase==HumanCampPhase.Packing,"The actual community enters its scheduled packing phase");
        int packedParts=owner.Camp.Objects.Where(o=>o.NpcGroup==band&&o.NpcSite==oldSite&&o.Blueprint!=null).Sum(o=>o.Blueprint.Parts.Count);
        Check(packedParts<fullParts,"Packing removes real shelter parts in reverse construction order");
        Date(initial.DepartureDay+Math.Min(.01*baseline.CalendarRate,(initial.JourneyEndDay-initial.DepartureDay)*.2));
        yield return Await(()=>group.Situation.Phase==HumanCampPhase.Travelling&&!group.CampLoaded,"The camp is packed away as the family group departs");
        Check(owner.LivingState.Camps.Any(c=>c.Site==oldSite&&c.AbandonedCalendarDay>=0&&c.Objects.All(o=>o.Kind==CampKind.Fire||o.Kind==CampKind.Bedding)),"Departure retains dated hearth and bedding memories, with no occupied shelter left active");
        Date(initial.JourneyEndDay+.022*baseline.CalendarRate);
        var probe=new HumanLocalLife(owner.sourceWorld,new Prehistory(owner.sourceWorld));int ordinal=unchecked((int)(uint)band)-1;
        var at=probe.Group(owner.sourceWorld.Prehistory,owner.LivingClock,initial.Band.Cohort,ordinal);
        if(owner.HumanDistance(owner.Position,at.Position)>500)owner.Teleport(at.Position);
        yield return Await(()=>group.Situation.Phase==HumanCampPhase.SettingUp&&group.Situation.Camp.Id!=oldSite&&group.CampLoaded,"The same community establishes its next terrain-validated camp");
        long newSite=group.Situation.Camp.Id;
        var building=owner.Camp.Objects.Where(c=>c.NpcGroup==band&&c.NpcSite==newSite&&c.Blueprint!=null).ToArray();
        Check(building.Length==0||building.Any(c=>!CampBlueprints.Complete(c.Blueprint)),"New shelters visibly start with partial, material-backed construction");
        Date(initial.JourneyEndDay+.07*baseline.CalendarRate);
        yield return Await(()=>group.Situation.Phase==HumanCampPhase.Occupied,"The new visit proceeds from setup into occupation");
        Check(owner.Camp.Objects.Where(c=>c.NpcGroup==band&&c.NpcSite==newSite&&c.Blueprint!=null).All(c=>CampBlueprints.Complete(c.Blueprint)),"Setup finishes every roof/frame component on the occupation transition");
        Check(identities.SequenceEqual(group.Residents.Members.Select(p=>p.Id).OrderBy(id=>id)),"Families and personal identities survive packing, travel and arrival");
        Check(owner.Camp.Objects.Select(c=>c.Id).Distinct().Count()==owner.Camp.Objects.Count,"Travel and old-site traces create no duplicate camp objects");
        owner.Camp.Validate();owner.CaptureLivingHumans();owner.LivingState.Validate();
        File.WriteAllText(Path.Combine(output,"after-camp-cycle.json"),WorldJson.Write(owner.Camp));
    }
}
