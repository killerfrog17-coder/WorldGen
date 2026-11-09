using System;
using System.Linq;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    void AdvanceLivingCampLife(LivingGroup group)
    {
        if(!group.CampLoaded||group.TraceOnly||group.Residents==null||group.Situation.Phase!=HumanCampPhase.Occupied||!group.BandHas(5))return;
        double now=UnityEngine.Time.unscaledTimeAsDouble;
        if(group.TenderDuty!=null)
        {
            if(now<group.TendUntil&&livingPeople.TryGetValue(group.TenderDuty.Person,out var tender)&&tender.Health>25&&tender.Threat==null&&Camp.Objects.Contains(group.TendedFire))return;
            group.TenderDuty=null;group.NextTend=now+2;
        }
        if(now<group.NextTend)return;group.NextTend=now+3;
        bool wanted=group.Situation.LocalHour<8.5||group.Situation.LocalHour>16||CurrentWeather.TemperatureC<8;
        if(!wanted)return;
        long band=group.Situation.Band.Id;
        var fire=Camp.Objects.Where(c=>c.NpcGroup==band&&c.NpcSite==group.Situation.Camp.Id&&c.Kind==CampKind.Fire&&c.Wetness<.45&&c.FuelKg<1.4&&(c.Burning||c.EmberSeconds>0||group.BandHas(9))).OrderBy(c=>c.FuelKg).FirstOrDefault();
        if(fire==null)return;
        var person=livingPeople.Values.Where(p=>p.GroupId==band&&p.Health>40&&p.Resident.AgeYears>=14&&p.Routine.Activity!=HumanActivity.Sleep&&p.Threat==null)
            .OrderByDescending(p=>NpcMaterialUse.HasIgnitionKit(p.Belongings)&&!fire.Burning&&fire.EmberSeconds<=0?1000:0).ThenBy(p=>HumanDistance(p.Planet,fire.Position)).FirstOrDefault();
        if(person==null)return;
        group.TendedFire=fire;group.TendUntil=now+180;group.TenderDuty=new HumanActivitySample{Person=person.ResidentId,Group=band,Activity=HumanActivity.TendFire,Position=fire.Position,Destination=fire.Position};
    }
}
