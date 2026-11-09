using System;
using System.Linq;

namespace WorldGen.Core
{
    [Serializable] public sealed class NpcCampTravel
    {
        public double DelayCalendarDays,HoldCalendarDay=-1;
        public long DepartingSite;
        public bool RebuildRequired;
        public NpcCampTravel Copy()=>(NpcCampTravel)MemberwiseClone();
        public void Validate(){if(!double.IsFinite(DelayCalendarDays)||DelayCalendarDays<0||!double.IsFinite(HoldCalendarDay)||HoldCalendarDay< -1||DepartingSite<0)throw new ArgumentException("Invalid local camp itinerary.");}
    }
    // Move existing contributions; never generate replacement goods on a task change.
    public static class NpcCampWork
    {
        public static bool Portable(CampBlueprint plan,CampComponent part,bool scarceWood)
            =>part.Kind==CampPartKind.Binding||part.Kind==CampPartKind.Cover&&(plan.Cover==CampCover.Hide||plan.Cover==CampCover.Bark||plan.Cover==CampCover.Reed)||scarceWood&&(part.Kind==CampPartKind.Post||part.Kind==CampPartKind.Beam||part.Kind==CampPartKind.Rib);
        public static CampComponent NextPacking(CampBlueprint plan,bool scarceWood)
            =>CampBlueprints.Components(plan).Where(c=>Portable(plan,c,scarceWood)&&CampBlueprints.State(plan,c)?.Materials.Count>0).OrderByDescending(c=>c.Stage).ThenBy(c=>c.Key,StringComparer.Ordinal).FirstOrDefault();
        public static Possession[] Remove(CampObject shelter,string key)
        {
            var state=shelter?.Blueprint?.Parts.FirstOrDefault(p=>p.Key==key);if(state==null)return Array.Empty<Possession>();
            var goods=state.Materials.ToArray();shelter.Blueprint.Parts.Remove(state);shelter.Blueprint.Revision++;
            foreach(var p in goods)
            {
                p.Place=PossessionPlace.Ground;p.ContainerId="";p.CampOwner=null;p.BoundContainerId=null;p.Position=shelter.Position;p.GroundMoving=p.InFlight=false;
                p.AttachedAnimal=0;p.AttachedHuman=0;p.GroundVelocity=p.GroundSpin=new V();p.CampMaterial=shelter.NpcGroup;
                // Folding changes packing footprint, not physical dimensions or provenance.
                if(CampRules.Hide(p)||p.Kind==PossessionKind.BarkSheet||p.Kind==PossessionKind.ReedBundle)
                {p.CellsWide=2;p.CellsHigh=Math.Max(1,(int)Math.Ceiling(p.MassKg/.6));p.Rotated=false;}
            }
            return goods;
        }
        public static HumanLifeClock ItineraryClock(HumanLifeClock clock,double delay)
            =>new HumanLifeClock(clock.CalendarSeconds-Math.Max(0,delay)*SimulationTime.Day,clock.SolarDays-Math.Max(0,delay)/clock.CalendarRate,clock.DayMinutes,clock.CalendarRate,clock.HorizontalScale,clock.PrimeLongitude);
    }
}
