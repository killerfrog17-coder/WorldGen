using System;
using System.Linq;
using WorldGen.Core;
using WorldGen.Presentation;
using UnityEngine;

public sealed partial class LocalTerrainExplorer
{
    void AdvanceLivingCampLife(LivingGroup group)
    {
        if(!group.CampLoaded||group.TraceOnly||group.Residents==null||group.Situation.Phase!=HumanCampPhase.Occupied||!group.BandHas(5))return;
        long band=group.Situation.Band.Id;var memory=LivingState.Groups.FirstOrDefault(g=>g.Group==band);
        if(memory==null){memory=new HumanGroupMemory{Group=band};LivingState.Groups.Add(memory);}
        if(memory.SuppliesUpdatedSolarDay<0){memory.SuppliesUpdatedSolarDay=PlayerRules.SolarDays;memory.FireWoodKg=8;}
        double elapsed=Math.Max(0,PlayerRules.SolarDays-memory.SuppliesUpdatedSolarDay);memory.SuppliesUpdatedSolarDay=PlayerRules.SolarDays;
        double wood=sourceWorld.Prehistory.Foragers.Districts[group.Situation.Band.District].Wood;
        int gatherers=group.Activities?.Count(a=>a.Activity==HumanActivity.GatherFuel)??0;
        // Supply is a small local stock backed by scheduled gathering and the
        // regional wood resource. It is not an infinite per-frame fuel cheat.
        memory.FireWoodKg=Math.Min(45,memory.FireWoodKg+Math.Min(3,elapsed)*gatherers*8*wood);
        bool wanted=group.Situation.LocalHour<8.5||group.Situation.LocalHour>16||CurrentWeather.TemperatureC<8;
        if(!wanted||memory.FireWoodKg<.9)return;
        double now=Time.unscaledTimeAsDouble;
        if(group.TenderDuty!=null&&now<group.TendUntil)
        {
            if(!livingPeople.TryGetValue(group.TenderDuty.Person,out var tender)||tender.Health<=0){group.TenderDuty=null;return;}
            if(HumanDistance(tender.Planet,group.TendedFire.Position)>1.8)return;
            var fire=group.TendedFire;
            Possession Fuel(PossessionKind kind,double kg)
            {
                var item=PortableTechnology.Item(kind)!=null?PortableTechnology.Create(kind,fire.Position):new Possession{Kind=kind,Position=fire.Position,WoodSoundness=.9,WoodStraightness=.6,HeightM=.4,WidthM=.025,DepthM=.025};
                item.Id="npc-tended-"+band.ToString("x")+"-"+memory.FuelContributions++;item.MassKg=kg;return item;
            }
            if(!fire.Burning){CampRules.Add(fire,Fuel(PossessionKind.DryGrass,.03));CampRules.Add(fire,Fuel(PossessionKind.KindlingBundle,.12));}
            CampRules.Add(fire,Fuel(PossessionKind.Branch,.7));
            if(!fire.Burning&&group.BandHas(9)&&CampRules.IgniteReason(fire,true)=="")CampRules.Ignite(fire,true);
            memory.FireWoodKg=Math.Max(0,memory.FireWoodKg-.85);group.TenderDuty=null;group.NextTend=now+3;return;
        }
        if(now<group.NextTend)return;
        var needs=Camp.Objects.Where(c=>c.NpcGroup==band&&c.NpcSite==group.Situation.Camp.Id&&c.Kind==CampKind.Fire&&c.Wetness<.45&&c.FuelKg<.7&&(c.Burning||c.EmberSeconds>0||group.BandHas(9))).OrderBy(c=>c.FuelKg).FirstOrDefault();
        if(needs==null)return;
        var person=livingPeople.Values.Where(p=>p.GroupId==band&&p.Health>40&&p.Resident.AgeYears>=14&&p.Routine.Activity!=HumanActivity.Sleep).OrderBy(p=>HumanDistance(p.Planet,needs.Position)).FirstOrDefault();
        if(person==null)return;
        TerrainHandoff.Coordinates(frame,needs.Position,out double x,out double z);
        group.TendedFire=needs;group.TendUntil=now+20;group.TenderDuty=new HumanActivitySample{Person=person.ResidentId,Group=band,Activity=HumanActivity.TendFire,Position=frame.Position(x+1.1,z),Destination=needs.Position};
    }
}
