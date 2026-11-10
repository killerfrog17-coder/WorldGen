using System;
using System.Linq;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    void WeatherNpcCampObject(LivingGroup group,CampObject obj)
    {
        if(obj.Kind!=CampKind.Bedding&&obj.Blueprint==null)return;
        var district=sourceWorld.Prehistory.Foragers.Districts[group.Situation.Band.District];
        int revision=obj.Blueprint?.Revision??0;double old=obj.WeatheredThroughCalendarDay;
        foreach(var debris in CampWeathering.Advance(obj,LivingClock.CalendarDay,district.AnnualRainMm,district.MeanC,district.Canopy))
            if(Inventory.Find(debris.Id)==null)Inventory.Items.Add(debris);
        if(obj.Blueprint!=null?obj.Blueprint.Revision!=revision:obj.WeatheredThroughCalendarDay!=old&&old>=0)InvalidateCampVisual(obj);
    }
    void WeatherNpcCamp(LivingGroup group)
    {
        foreach(var obj in Camp.Objects.Where(c=>c.NpcSite==group.Situation.Camp.Id&&c.NpcGroup==group.Situation.Band.Id).ToArray())WeatherNpcCampObject(group,obj);
    }
    void AdvanceLivingTrace(LivingGroup group)
    {
        var memory=LivingState.Camps.FirstOrDefault(c=>c.Site==group.Situation.Camp.Id);
        if(memory==null||memory.AbandonedCalendarDay<0)return;
        double now=LivingClock.CalendarDay,last=memory.LastDecayCalendarDay<0?memory.AbandonedCalendarDay:memory.LastDecayCalendarDay;
        double elapsed=Math.Max(0,now-last);if(elapsed<1)return;
        WeatherNpcCamp(group);
        foreach(var bed in Camp.Objects.Where(c=>c.NpcSite==memory.Site&&c.NpcGroup==memory.Group&&c.Kind==CampKind.Bedding).ToArray())
        {
            if(bed.Materials.Sum(m=>m.MassKg)<.05)Camp.Objects.Remove(bed);
        }
        memory.LastDecayCalendarDay=now;StoreLivingCamp(group);
    }
}
