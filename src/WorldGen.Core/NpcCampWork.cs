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
        public void Validate(){if(!double.IsFinite(DelayCalendarDays)||DelayCalendarDays<0||!double.IsFinite(HoldCalendarDay)||HoldCalendarDay< -1)throw new ArgumentException("Invalid local camp itinerary.");}
    }
    public static class NpcShelterRouting
    {
        // At most four corners and sixteen two-corner paths, no allocation,
        // physics queries or per-frame navigation mesh construction.
        public static bool OutsideWaypoint(double x,double z,double goalX,double goalZ,double halfWidth,double halfDepth,out double nextX,out double nextZ)
        {
            nextX=goalX;nextZ=goalZ;double w=halfWidth+.3,d=halfDepth+.3;
            bool Cross(double ax,double az,double bx,double bz)
            {
                double low=0,high=1;
                bool Axis(double a,double delta,double half)
                {if(Math.Abs(delta)<1e-9)return Math.Abs(a)<half;double near=(-half-a)/delta,far=(half-a)/delta;if(near>far)(near,far)=(far,near);low=Math.Max(low,near);high=Math.Min(high,far);return high>low+1e-7;}
                return Axis(ax,bx-ax,w)&&Axis(az,bz-az,d)&&high>1e-7&&low<1-1e-7;
            }
            if(!Cross(x,z,goalX,goalZ))return false;
            V Corner(int i)=>new V((i==0||i==3?-1:1)*(halfWidth+1),0,(i<2?-1:1)*(halfDepth+1));
            double Distance(double ax,double az,double bx,double bz)=>Math.Sqrt((ax-bx)*(ax-bx)+(az-bz)*(az-bz));
            if(Math.Abs(x)<w&&Math.Abs(z)<d)
            {V c=Corner(0);double nearest=Distance(x,z,c.X,c.Z);for(int i=1;i<4;i++){var candidate=Corner(i);double range=Distance(x,z,candidate.X,candidate.Z);if(range<nearest){c=candidate;nearest=range;}}nextX=c.X;nextZ=c.Z;return true;}
            double best=double.PositiveInfinity;V chosen=default;
            for(int i=0;i<4;i++)
            {
                var a=Corner(i);if(Cross(x,z,a.X,a.Z))continue;double first=Distance(x,z,a.X,a.Z);
                if(!Cross(a.X,a.Z,goalX,goalZ))
                {double cost=first+Distance(a.X,a.Z,goalX,goalZ);if(cost<best){best=cost;chosen=first<.28?new V(goalX,0,goalZ):a;}}
                for(int j=0;j<4;j++)
                {var b=Corner(j);if(i==j||Cross(a.X,a.Z,b.X,b.Z)||Cross(b.X,b.Z,goalX,goalZ))continue;double cost=first+Distance(a.X,a.Z,b.X,b.Z)+Distance(b.X,b.Z,goalX,goalZ);if(cost<best){best=cost;chosen=first<.28?b:a;}}
            }
            if(double.IsInfinity(best))return false;nextX=chosen.X;nextZ=chosen.Z;return true;
        }
    }
    // Move existing contributions; never generate replacement goods on a task change.
    public static class NpcCampWork
    {
        public static Possession Bundle(PlayerInventory stock,long group,long household,V position,double maximumKg=24)
        {
            bool Part(Possession p)=>p.CampMaterial==group&&p.OwnerHousehold==household&&(WoodCrafting.Wood(p)||CampRules.Hide(p)||p.Kind==PossessionKind.BarkSheet||p.Kind==PossessionKind.ReedBundle||FrameRules.Binding(p));
            bool looseWood=stock.Items.Any(p=>Part(p)&&WoodCrafting.Wood(p));
            var existing=stock.Items.FirstOrDefault(p=>p.Kind==PossessionKind.CampBundle&&p.CampMaterial==group&&p.OwnerHousehold==household&&p.BundledMaterials!=null&&(!looseWood||p.BundledMaterials.Any(FrameRules.Binding)));
            var tie=existing==null?stock.Items.FirstOrDefault(p=>p.CampMaterial==group&&p.OwnerHousehold==household&&FrameRules.Binding(p)):null;
            // Flexible covers can roll together in their own outer sheet.
            // This does not let an unlashed hide bind a load of wooden poles.
            if(existing==null&&tie==null)tie=stock.Items.FirstOrDefault(p=>Part(p)&&(CampRules.Hide(p)||p.Kind==PossessionKind.BarkSheet||p.Kind==PossessionKind.ReedBundle));
            if(existing==null&&tie==null)return null;
            bool wrapped=existing!=null?!existing.BundledMaterials.Any(FrameRules.Binding):tie!=null&&!FrameRules.Binding(tie);
            var goods=stock.Items.Where(p=>p!=tie&&Part(p)&&(!wrapped||CampRules.Hide(p)||p.Kind==PossessionKind.BarkSheet||p.Kind==PossessionKind.ReedBundle)).OrderByDescending(WoodCrafting.Wood).ToArray();double mass=existing?.MassKg??tie.MassKg;
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
        public static bool Portable(CampBlueprint plan,CampComponent part,bool scarceWood,bool takeCover=true)
            =>scarceWood&&(part.Kind==CampPartKind.Binding||part.Kind==CampPartKind.Post||part.Kind==CampPartKind.Beam||part.Kind==CampPartKind.Rib)||takeCover&&part.Kind==CampPartKind.Cover&&(plan.Cover==CampCover.Hide||plan.Cover==CampCover.Bark||plan.Cover==CampCover.Reed);
        public static CampComponent NextPacking(CampBlueprint plan,bool scarceWood,bool takeCover=true)
            =>CampBlueprints.Components(plan).Where(c=>Portable(plan,c,scarceWood,takeCover)&&CampBlueprints.State(plan,c)?.Materials.Count>0).OrderByDescending(c=>c.Stage).ThenBy(c=>c.Key,StringComparer.Ordinal).FirstOrDefault();
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
