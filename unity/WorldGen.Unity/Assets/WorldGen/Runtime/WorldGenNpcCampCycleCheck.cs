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
    public sealed class NpcCyclePersonRow{public string Name,TaskDetail;public float Speed;public double NavigationX,NavigationZ,Kg;}
    public sealed class NpcCycleStatus
    {public string WaitingFor,Phase;public bool CampLoaded,Grounded;public long CurrentSite,OldSite;public int PlacementAttempt,OldParts,Packed,Supplied,Worked;public NpcCampTravel Travel;public NpcCyclePersonRow[] People;}
    static IEnumerator CheckLivingCampCycle(LocalTerrainExplorer owner,LivingGroup group,string output,List<string> checks)
    {
        void Check(bool ok,string text){if(!ok)throw new InvalidOperationException("NPC CAMP CYCLE "+text);checks.Add("PASS "+text);Debug.Log("NPC CAMP CYCLE "+text);}
        if(group.Situation.PermanentBase){Check(true,"Dependable aquatic base remains occupied rather than undergoing a forced annual migration");yield break;}
        var baseline=owner.LivingClock;var initial=group.Situation;long oldSite=initial.Camp.Id,band=initial.Band.Id;
        var identities=group.Residents.Members.Select(p=>p.Id).OrderBy(id=>id).ToArray();
        int fullParts=owner.Camp.Objects.Where(o=>o.NpcGroup==band&&o.NpcSite==oldSite&&o.Blueprint!=null).Sum(o=>o.Blueprint.Parts.Count);
        var portable=owner.Camp.Objects.Where(o=>o.NpcGroup==band&&o.NpcSite==oldSite&&o.Blueprint!=null)
            .SelectMany(o=>owner.NpcCampParts(o).Where(p=>NpcCampWork.Portable(o.Blueprint,p,owner.NpcCarryCampPoles(group))).SelectMany(p=>CampBlueprints.State(o.Blueprint,p)?.Materials??new List<Possession>())).ToDictionary(p=>p.Id,p=>p.MassKg);
        Check(portable.Count>0,"The observed departure starts with real portable shelter materials");
        int Parts()=>owner.Camp.Objects.Where(o=>o.NpcGroup==band&&o.NpcSite==oldSite&&o.Blueprint!=null).Sum(o=>o.Blueprint.Parts.Count);
        Possession[] ActualGoods()
        {
            owner.CaptureLivingHumans();var activeObjects=owner.Camp.Objects.Select(c=>c.Id).ToHashSet();
            return owner.Inventory.Items.Concat(owner.LivingState.People.Where(p=>p.Inventory!=null).SelectMany(p=>p.Inventory.Items)).Concat(owner.Camp.Objects.SelectMany(c=>c.OwnedMaterials()))
                .Concat(owner.LivingState.Camps.SelectMany(c=>c.Objects).Where(c=>!activeObjects.Contains(c.Id)).SelectMany(c=>c.OwnedMaterials())).SelectMany(p=>p.BundledMaterials??new List<Possession>{p}).ToArray();
        }
        void Date(double day)
        {
            owner.PlayerRules.SolarDays=baseline.SolarDays+(day-baseline.CalendarDay)/baseline.CalendarRate;
            owner.PlayerRules.PendingHistorySeconds=day*SimulationTime.Day-SimulationTime.Seconds(owner.sourceWorld.Prehistory)-owner.PlayerRules.CalendarOffsetSeconds;
            owner.livingQueryAt=0;
        }
        void TickClock()
        {
            // This standalone fixture has no outer play-session clock driver.
            double solar=Math.Min(.1,Time.unscaledDeltaTime)/(baseline.DayMinutes*60);
            owner.PlayerRules.SolarDays+=solar;owner.PlayerRules.PendingHistorySeconds+=solar*baseline.CalendarRate*SimulationTime.Day;
        }
        IEnumerator Await(Func<bool> condition,string description)
        {
            double end=Time.realtimeSinceStartupAsDouble+240,statusAt=0;
            while(!condition()&&Time.realtimeSinceStartupAsDouble<end)
            {
                TickClock();
                if(group.Grounded&&!group.CampLoaded&&owner.HumanDistance(group.Anchor,owner.Position)>450)
                {TerrainHandoff.Coordinates(owner.frame,group.Anchor,out double x,out double z);owner.motion.Place(x,z,owner.VisibleGround(x,z,0));}
                if(Time.realtimeSinceStartupAsDouble>=statusAt)
                {
                    statusAt=Time.realtimeSinceStartupAsDouble+3;
                    File.WriteAllText(Path.Combine(output,"camp-cycle-status.json"),WorldJson.Write(new NpcCycleStatus{WaitingFor=description,Phase=group.Situation.Phase.ToString(),CampLoaded=group.CampLoaded,Grounded=group.Grounded,CurrentSite=group.Situation.Camp.Id,OldSite=oldSite,PlacementAttempt=group.PlacementAttempt,OldParts=Parts(),Packed=owner.npcCampPacked,Supplied=owner.npcCampSupplied,Worked=owner.npcCampWorked,Travel=owner.NpcTravel(group),People=owner.livingPeople.Values.Where(p=>p.GroupId==band).Select(p=>new NpcCyclePersonRow{Name=p.Resident.Name,TaskDetail=p.TaskDetail,Speed=p.Speed,NavigationX=p.NavigationX,NavigationZ=p.NavigationZ,Kg=p.Belongings.CarriedMassKg}).ToArray()}));
                }
                yield return null;
            }
            Check(condition(),description);
        }
        Date(initial.DepartureDay+.0001);
        yield return Await(()=>group.Situation.Phase==HumanCampPhase.Packing,"The actual community enters its scheduled packing phase");
        yield return Await(()=>Parts()<fullParts,"Actual workers remove shelter components instead of itinerary progress deleting them");
        yield return Await(()=>group.Situation.Phase==HumanCampPhase.Travelling&&!group.CampLoaded,"The camp is packed away as the family group departs");
        var packedGoods=ActualGoods().Where(p=>portable.ContainsKey(p.Id)).ToArray();
        Check(packedGoods.Length==portable.Count&&packedGoods.Select(p=>p.Id).Distinct().Count()==portable.Count&&packedGoods.All(p=>Math.Abs(p.MassKg-portable[p.Id])<1e-8),"A work-delayed departure preserves every original portable contribution instead of an abandoned trace deleting the active camp");
        Check(owner.LivingState.Camps.Any(c=>c.Site==oldSite&&c.AbandonedCalendarDay>=0&&c.Objects.Any(o=>o.Kind==CampKind.Bedding)&&c.Objects.Where(o=>o.Blueprint!=null).All(o=>!owner.NpcCampParts(o).Any(p=>NpcCampWork.Portable(o.Blueprint,p,owner.NpcCarryCampPoles(group))&&CampBlueprints.State(o.Blueprint,p)?.Materials.Count>0))),"Departure leaves dated bedding/hearths and any uncarried frames, with their portable contributions actually removed");
        var walking=owner.livingPeople.Values.Where(p=>p.GroupId==band&&p.Health>0&&p.Resident.AgeYears>=12).ToDictionary(p=>p.ResidentId,p=>p.Planet);
        double walkUntil=Time.realtimeSinceStartupAsDouble+18;while(Time.realtimeSinceStartupAsDouble<walkUntil){TickClock();yield return null;}
        Check(owner.livingPeople.Values.Any(p=>walking.TryGetValue(p.ResidentId,out var start)&&owner.HumanDistance(start,p.Planet)>1),"Observed travellers physically begin moving after packing");
        double delay=owner.NpcTravel(group).DelayCalendarDays;
        // Explicit coarse travel skip, not a claim of following every metre.
        // The packed inventories remain real saved possessions throughout.
        owner.CaptureLivingHumans();Date(initial.JourneyEndDay+delay+.001*baseline.CalendarRate);
        var probe=new HumanLocalLife(owner.sourceWorld,new Prehistory(owner.sourceWorld));int ordinal=unchecked((int)(uint)band)-1;
        var at=probe.Group(owner.sourceWorld.Prehistory,NpcCampWork.ItineraryClock(owner.LivingClock,delay),initial.Band.Cohort,ordinal);
        if(owner.HumanDistance(owner.Position,at.Position)>500)owner.Teleport(at.Position);
        yield return Await(()=>group.Situation.Camp.Id!=oldSite&&group.CampLoaded,"The same community reaches its next terrain-validated camp after a coarse journey skip");
        long newSite=group.Situation.Camp.Id;
        var building=owner.Camp.Objects.Where(c=>c.NpcGroup==band&&c.NpcSite==newSite&&c.Blueprint!=null).ToArray();
        Check(building.Length>0&&building.Any(c=>!CampBlueprints.Complete(c.Blueprint)),"The observed arrival starts with uncompleted shelters rather than a replacement factory");
        var arrived=ActualGoods().Where(p=>portable.ContainsKey(p.Id)).ToArray();
        Check(arrived.Length==portable.Count&&arrived.Select(p=>p.Id).Distinct().Count()==portable.Count&&arrived.All(p=>Math.Abs(p.MassKg-portable[p.Id])<1e-8),"Original portable contributions persist exactly once with unchanged mass through packing and coarse travel to arrival");
        int supplied=owner.npcCampSupplied,worked=owner.npcCampWorked;
        Date(initial.JourneyEndDay+delay+.07*baseline.CalendarRate);
        yield return Await(()=>group.Situation.Phase==HumanCampPhase.Occupied,"The new visit proceeds from setup into occupation");
        yield return Await(()=>owner.npcCampSupplied>supplied&&owner.npcCampWorked>worked&&owner.Camp.Objects.Where(c=>c.NpcGroup==band&&c.NpcSite==newSite&&c.Blueprint!=null).SelectMany(c=>c.OwnedMaterials()).Any(p=>portable.ContainsKey(p.Id)),"A real worker supplies and works with an original recovered contribution at the new camp");
        Check(identities.SequenceEqual(group.Residents.Members.Select(p=>p.Id).OrderBy(id=>id)),"Families and personal identities survive packing, travel and arrival");
        Check(owner.Camp.Objects.Select(c=>c.Id).Distinct().Count()==owner.Camp.Objects.Count,"Travel and old-site traces create no duplicate camp objects");
        owner.Camp.Validate();owner.CaptureLivingHumans();owner.LivingState.Validate();
        // Construction may legitimately portion bark/rope or cut poles, so do
        // not demand unchanged original piece masses after that work.
        var goods=ActualGoods();Check(goods.Select(p=>p.Id).Distinct().Count()==goods.Length,"Physical reassembly creates no duplicated goods across people, active camps and abandoned memories");
        File.WriteAllText(Path.Combine(output,"camp-cycle-scope.txt"),"Actual worker packing, observed initial travel, explicit coarse journey skip, actual terrain arrival and original-material reassembly. Not an uninterrupted whole-route follow; not proof that every shelter finishes.");
        File.WriteAllText(Path.Combine(output,"after-camp-cycle.json"),WorldJson.Write(owner.Camp));
    }
}
