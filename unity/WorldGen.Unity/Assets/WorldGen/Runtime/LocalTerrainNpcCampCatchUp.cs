using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    sealed class NpcCampCatchUpJob
    {
        public long Site;public HumanCampMemory Original,From,To;public HumanPersonMemory[] People,OriginalPeople;
        public Possession[] OriginalLoose;public double[] OriginalLooseMass;public PlayerInventory Loose;public Task Work;
    }
    readonly Dictionary<long,NpcCampCatchUpJob> npcCampCatchUps=new Dictionary<long,NpcCampCatchUpJob>();
    bool PrepareUnobservedNpcCamp(LivingGroup group,ref CampObject[] objects)
    {
        long id=group.Situation.Band.Id,site=group.Situation.Camp.Id;
        if(npcCampCatchUps.TryGetValue(id,out var job))
        {
            if(!job.Work.IsCompleted)return false;
            npcCampCatchUps.Remove(id);
            if(job.Work.IsFaulted){UnityEngine.Debug.LogException(job.Work.Exception.GetBaseException());return true;}
            // Never overwrite intervening observed actors, another site or an
            // item the player picked up while this small background job ran.
            if(job.Site!=site||livingPeople.Values.Any(p=>p.GroupId==id)||!LivingState.Camps.Contains(job.Original)||job.OriginalLoose.Where((p,i)=>!Inventory.Items.Contains(p)||p.Place!=PossessionPlace.Ground||p.CampMaterial!=id||p.MassKg!=job.OriginalLooseMass[i]).Any())return true;
            if(job.From.Site!=site){job.Original.Objects=job.From.Objects;job.Original.AbandonedCalendarDay=group.Situation.ArrivalDay;}
            foreach(var person in job.People){var original=job.OriginalPeople.First(p=>p.Person==person.Person);original.Inventory=person.Inventory;original.SolarDays=PlayerRules.SolarDays;}
            foreach(var item in job.OriginalLoose)Inventory.Items.Remove(item);
            Inventory.Items.AddRange(job.Loose.Items);objects=job.To.Objects.ToArray();
            var saved=LivingState.Camps.FirstOrDefault(c=>c.Site==site);
            if(saved==null){saved=job.To;LivingState.Camps.Add(saved);}else saved.Objects=job.To.Objects;
            saved.LastSolarDay=PlayerRules.SolarDays;saved.Disturbed=true;inventoryNearbyAt=0;return true;
        }
        if(livingPeople.Values.Any(p=>p.GroupId==id))return true;
        var source=LivingState.Camps.Where(c=>c.Group==id).OrderByDescending(c=>c.LastSolarDay).FirstOrDefault();
        if(source==null||PlayerRules.SolarDays-source.LastSolarDay<.02)return true;
        if(source.Site!=site)foreach(var stored in source.Objects)WeatherNpcCampObject(group,stored);
        var originals=LivingState.People.Where(p=>p.Group==id&&p.Inventory!=null).ToArray();
        if(originals.Length==0)return true;
        var external=Inventory.Items.Where(p=>p.Place==PossessionPlace.Ground&&p.CampMaterial==id&&HumanDistance(p.Position,source.Position)<80).ToArray();
        job=new NpcCampCatchUpJob{Site=site,Original=source,From=source.Copy(),To=new HumanCampMemory{Site=site,Group=id,Position=group.Anchor,LastSolarDay=PlayerRules.SolarDays,Disturbed=true,Objects=objects.Select(c=>c.Copy()).ToList()},People=originals.Select(p=>p.Copy()).ToArray(),OriginalPeople=originals,OriginalLoose=external,OriginalLooseMass=external.Select(p=>p.MassKg).ToArray(),Loose=new PlayerInventory{Items=external.Select(p=>p.Copy()).ToList()}};
        if(source.Site==site)job.From=job.To;
        var roster=group.Residents.Members.ToArray();bool poles=NpcCarryCampPoles(group),cover=NpcTakeCampCover(group);
        double elapsed=Math.Min(3,PlayerRules.SolarDays-source.LastSolarDay),work=elapsed*8*3600*roster.Count(p=>p.AgeYears>=14);
        var pending=job;job.Work=Task.Run(()=>NpcCampCatchUp.Apply(pending.From,pending.To,pending.People,roster,pending.Loose,poles,work,cover));npcCampCatchUps[id]=job;return false;
    }
}
