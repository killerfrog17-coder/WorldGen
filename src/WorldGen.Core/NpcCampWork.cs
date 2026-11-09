using System;
using System.Collections.Generic;
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
        public static Possession Bundle(PlayerInventory stock,long group,long household,V position,double maximumKg=24)
        {
            bool Part(Possession p)=>p.CampMaterial==group&&p.OwnerHousehold==household&&(WoodCrafting.Wood(p)||CampRules.Hide(p)||p.Kind==PossessionKind.BarkSheet||p.Kind==PossessionKind.ReedBundle);
            var existing=stock.Items.FirstOrDefault(p=>p.Kind==PossessionKind.CampBundle&&p.CampMaterial==group&&p.OwnerHousehold==household&&p.BundledMaterials!=null);
            var tie=existing==null?stock.Items.FirstOrDefault(p=>p.CampMaterial==group&&p.OwnerHousehold==household&&FrameRules.Binding(p)):null;
            if(existing==null&&tie==null)return null;
            var goods=stock.Items.Where(Part).OrderByDescending(WoodCrafting.Wood).ToArray();double mass=existing?.MassKg??tie.MassKg;
            var chosen=new List<Possession>();foreach(var p in goods)if(mass+p.MassKg<=maximumKg&&(existing?.BundledMaterials.Count??0)+chosen.Count<24){chosen.Add(p);mass+=p.MassKg;}
            if(chosen.Count==0||existing==null&&chosen.Count<2)return null;
            if(existing==null)
            {
                existing=PortableTechnology.Create(PossessionKind.CampBundle,position);existing.Id="bundle-"+tie.Id;existing.OwnerGroup=group;existing.OwnerHousehold=household;existing.OwnerName=tie.OwnerName;existing.CampMaterial=group;tie.Place=PossessionPlace.Ground;tie.ContainerId="";existing.BundledMaterials=new List<Possession>{tie};stock.Items.Remove(tie);stock.Items.Add(existing);
            }
            foreach(var p in chosen){stock.Items.Remove(p);p.Place=PossessionPlace.Ground;p.ContainerId="";existing.BundledMaterials.Add(p);}
            existing.MassKg=existing.BundledMaterials.Sum(p=>p.MassKg);existing.HeightM=Math.Max(.35,existing.BundledMaterials.Where(WoodCrafting.Wood).Select(p=>p.HeightM).DefaultIfEmpty(.6).Max());existing.WidthM=existing.DepthM=Math.Max(.15,Math.Sqrt(existing.BundledMaterials.Count)*.045);existing.CellsWide=2;existing.CellsHigh=(int)Math.Ceiling(existing.HeightM/.1);existing.Variant++;
            // Existing hand-held pieces were removed above, freeing their hands.
            if(existing.Place==PossessionPlace.Ground&&!stock.Move(existing.Id,PossessionPlace.Shoulder,"",0,0,false,out _))stock.Hold(existing.Id,out _);return existing;
        }
        public static Possession[] Unbundle(PlayerInventory stock,Possession bundle,V position)
        {
            if(bundle?.Kind!=PossessionKind.CampBundle||bundle.BundledMaterials==null||!stock.Items.Contains(bundle))return Array.Empty<Possession>();
            var goods=bundle.BundledMaterials.ToArray();if(goods.Any(p=>stock.Find(p.Id)!=null))throw new ArgumentException("Duplicate shelter bundle contribution.");
            stock.Items.Remove(bundle);foreach(var p in goods){p.Place=PossessionPlace.Ground;p.ContainerId="";p.Position=position;p.GroundMoving=p.InFlight=false;p.GroundOffsetM=.05;p.GroundRotation=new V(90,0,0);stock.Items.Add(p);}bundle.BundledMaterials=null;return goods;
        }
        public static void ValidateBundle(Possession p)
        {
            if(p.BundledMaterials==null)return;
            if(p.Kind!=PossessionKind.CampBundle||p.BundledMaterials.Count<3||p.BundledMaterials.Count>25||p.BundledMaterials.Any(i=>i==null||i.BundledMaterials!=null||i.MassKg<=0||!double.IsFinite(i.MassKg)||string.IsNullOrEmpty(i.Id))||p.BundledMaterials.Select(i=>i.Id).Distinct().Count()!=p.BundledMaterials.Count||Math.Abs(p.MassKg-p.BundledMaterials.Sum(i=>i.MassKg))>1e-7)throw new ArgumentException("Invalid bound shelter materials.");
        }
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
