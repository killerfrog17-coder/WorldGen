using System;
using System.Linq;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    void CatchUpNpcHearth(CampObject obj)
    {
        if(obj.Kind!=CampKind.Fire)return;double elapsed=Math.Max(0,Camp.SimulatedSeconds-obj.LastSimulatedSeconds);
        if(elapsed>0)CampRules.Advance(obj,elapsed,CurrentWeather.RainMmPerHour,CurrentWeather.Humidity,Math.Sqrt(CurrentWeather.WindEastMps*CurrentWeather.WindEastMps+CurrentWeather.WindNorthMps*CurrentWeather.WindNorthMps),SkyFrame?.Daylight??0);
    }
    void AdvanceLivingCampLife(LivingGroup group)
    {
        if(group.CampLoaded)WeatherNpcCamp(group);
        bool active=group.Situation.Phase==HumanCampPhase.Occupied||group.Situation.Phase==HumanCampPhase.SettingUp;
        group.NeedsCampWork=active&&NeedsNpcCampWork(group);
        UpdateNpcCampWorkers(group);
        if(!active){group.TenderDuty=null;group.TendedFire=null;return;}
        if(!group.CampLoaded||group.TraceOnly||group.Residents==null||!group.BandHas(5))return;
        double now=UnityEngine.Time.unscaledTimeAsDouble;
        if(group.TenderDuty!=null)
        {
            if(now<group.TendUntil&&livingPeople.TryGetValue(group.TenderDuty.Person,out var tender)&&tender.Health>25&&tender.Threat==null&&Camp.Objects.Contains(group.TendedFire))return;
            group.TenderDuty=null;group.NextTend=now+2;
        }
        if(now<group.NextTend)return;group.NextTend=now+3;
        // A community that cannot make new fire must also preserve its live
        // hearth during warm daylight, rather than letting every coal die.
        bool wanted=group.Situation.LocalHour<8.5||group.Situation.LocalHour>16||CurrentWeather.TemperatureC<8||!group.BandHas(9);
        if(!wanted)return;
        long band=group.Situation.Band.Id;
        var fire=Camp.Objects.Where(c=>c.NpcGroup==band&&c.NpcSite==group.Situation.Camp.Id&&c.Kind==CampKind.Fire&&c.Wetness<.45&&(!c.Burning||c.FuelKg<1.4)&&(c.Burning||c.EmberSeconds>0||group.BandHas(9))).OrderBy(c=>c.FuelKg).FirstOrDefault();
        if(fire==null)return;
        var person=livingPeople.Values.Where(p=>p.GroupId==band&&p.Health>40&&p.Resident.AgeYears>=14&&p.Routine.Activity!=HumanActivity.Sleep&&p.Threat==null)
            .OrderByDescending(p=>!fire.Burning&&fire.EmberSeconds<=0&&(NpcMaterialUse.HasIgnitionKit(p.Belongings)||Inventory.Items.Any(i=>i.Kind==PossessionKind.HandDrillSet&&i.OwnerPerson==p.ResidentId&&NpcCanUseWorldItem(p,i)))?1000:0).ThenBy(p=>HumanDistance(p.Planet,fire.Position)).FirstOrDefault();
        if(person==null)return;
        group.TendedFire=fire;group.TendUntil=now+180;group.TenderDuty=new HumanActivitySample{Person=person.ResidentId,Group=band,Activity=HumanActivity.TendFire,Position=fire.Position,Destination=fire.Position};
    }
    void UpdateNpcCampWorkers(LivingGroup group)
    {
        if(!group.NeedsCampWork){group.CampWorkers.Clear();return;}
        double now=UnityEngine.Time.unscaledTimeAsDouble;if(now<group.CampWorkersAt)return;group.CampWorkersAt=now+3;
        // A limited setup crew suspends noncritical outings; most of the band
        // remains free to gather, care for children and keep watch. Actual
        // social/safety priorities can still interrupt an assigned worker.
        var available=livingPeople.Values.Where(p=>p.GroupId==group.Situation.Band.Id&&p.Health>40&&p.Resident.AgeYears>=14&&p.Threat==null&&p.Routine!=null&&p.Routine.Activity!=HumanActivity.Sleep&&p.Routine.Activity!=HumanActivity.Childcare&&p.Routine.Activity!=HumanActivity.Watch&&group.TenderDuty?.Person!=p.ResidentId&&p.MaterialTask?.Delivering!=true&&(HumanDistance(p.Planet,group.Anchor)<80||group.CampWorkers.Contains(p.ResidentId)&&p.CampTask?.Packing==false)).ToArray();
        // Fetching a pole is still construction work. Do not dismiss that
        // worker at the camp boundary and replace them every three seconds.
        var chosen=available.OrderByDescending(p=>group.CampWorkers.Contains(p.ResidentId)?1:0).ThenByDescending(p=>p.Routine.Activity==HumanActivity.BuildCamp||p.Routine.Activity==HumanActivity.RepairShelter||p.Routine.Activity==HumanActivity.Craft?1:0).ThenByDescending(p=>p.Resident.Skills?.Length>4?p.Resident.Skills[4]:0).ThenBy(p=>p.ResidentId).Take(Math.Min(4,Math.Max(group.CampWorkers.Count,Math.Max(1,available.Length/4)))).Select(p=>p.ResidentId).ToArray();
        group.CampWorkers.Clear();foreach(long person in chosen)group.CampWorkers.Add(person);
    }
    double NpcBeddingGoal(LivingGroup group,long household)=>.9+Math.Min(5,group.Residents.Members.Count(p=>p.Household==household))*.5;
    bool NeedsNpcCampWork(LivingGroup group)
    {
        if(!group.CampLoaded||group.TraceOnly||group.Residents==null)return false;
        foreach(var obj in Camp.Objects)
        {
            if(obj.NpcGroup!=group.Situation.Band.Id||obj.NpcSite!=group.Situation.Camp.Id)continue;
            if(obj.Blueprint!=null){foreach(var part in NpcCampParts(obj))if(!CampBlueprints.Done(obj.Blueprint,part))return true;}
            else if(obj.Kind==CampKind.Bedding&&obj.Materials.Sum(p=>p.MassKg)+.00001<NpcBeddingGoal(group,obj.NpcHousehold))return true;
        }
        return false;
    }
}
