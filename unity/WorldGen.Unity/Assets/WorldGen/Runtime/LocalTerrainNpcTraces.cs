using System;
using System.Linq;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    void AdvanceLivingTrace(LivingGroup group)
    {
        var memory=LivingState.Camps.FirstOrDefault(c=>c.Site==group.Situation.Camp.Id);
        if(memory==null||memory.AbandonedCalendarDay<0)return;
        double now=LivingClock.CalendarDay,last=memory.LastDecayCalendarDay<0?memory.AbandonedCalendarDay:memory.LastDecayCalendarDay;
        double elapsed=Math.Max(0,now-last);if(elapsed<1)return;
        double rain=sourceWorld.Prehistory.Foragers.Districts[group.Situation.Band.District].AnnualRainMm;
        foreach(var bed in Camp.Objects.Where(c=>c.NpcSite==memory.Site&&c.NpcGroup==memory.Group&&c.Kind==CampKind.Bedding).ToArray())
        {
            HumanCampLayout.AgeBedding(bed,elapsed,rain);InvalidateCampVisual(bed);
            if(bed.Materials.Sum(m=>m.MassKg)<.05)Camp.Objects.Remove(bed);
        }
        memory.LastDecayCalendarDay=now;StoreLivingCamp(group);
    }
}
