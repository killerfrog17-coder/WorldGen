using System;
using System.Collections.Generic;
using System.Linq;
namespace WorldGen.Core
{
    // Only locally consequential changes are saved. Unvisited camps/people are
    // deterministic projections and do not become permanent global agents.
    [Serializable] public sealed class HumanLocalState
    {
        public List<HumanPersonMemory> People=new List<HumanPersonMemory>();
        public List<HumanCampMemory> Camps=new List<HumanCampMemory>();
        public List<HumanGroupMemory> Groups=new List<HumanGroupMemory>();
        public HumanLocalState Copy()=>new HumanLocalState{People=People.Select(p=>p.Copy()).ToList(),Camps=Camps.Select(c=>c.Copy()).ToList(),Groups=Groups.Select(g=>g.Copy()).ToList()};
        public void Validate()
        {
            if(People==null||Camps==null||Groups==null||People.Any(p=>p==null)||Camps.Any(c=>c==null)||Groups.Any(g=>g==null))throw new ArgumentException("Invalid local human records.");
            if(People.Select(p=>p.Person).Distinct().Count()!=People.Count||Camps.Select(c=>c.Site).Distinct().Count()!=Camps.Count||Groups.Select(g=>g.Group).Distinct().Count()!=Groups.Count)throw new ArgumentException("Duplicate local human identity.");
            foreach(var p in People)if(p.Person<=0||!double.IsFinite(p.Health)||p.Health<0||p.Health>100||!double.IsFinite(p.SolarDays)||p.SolarDays<0||!double.IsFinite(p.Position.Length)||p.Position.Length>0&&Math.Abs(p.Position.Length-1)>1e-5)throw new ArgumentException("Invalid local person memory.");
            foreach(var g in Groups)if(!double.IsFinite(g.Trust)||g.Trust< -1||g.Trust>1||!double.IsFinite(g.Fear)||g.Fear<0||g.Fear>1||!double.IsFinite(g.AlarmUntilSolarDay)||!double.IsFinite(g.FireWoodKg)||g.FireWoodKg<0||g.FireWoodKg>1000||!double.IsFinite(g.SuppliesUpdatedSolarDay)||g.SuppliesUpdatedSolarDay< -1||g.FuelContributions<0)throw new ArgumentException("Invalid group relationship/supply memory.");
            foreach(var c in Camps){if(c.Objects==null||!double.IsFinite(c.LastSolarDay)||!double.IsFinite(c.AbandonedCalendarDay)||c.AbandonedCalendarDay< -1||!double.IsFinite(c.LastDecayCalendarDay)||c.LastDecayCalendarDay< -1||!double.IsFinite(c.Position.Length)||Math.Abs(c.Position.Length-1)>1e-5)throw new ArgumentException("Invalid visited camp.");new CampState{Objects=c.Objects}.Validate();}
        }
    }
    [Serializable] public sealed class HumanPersonMemory
    {
        public long Person,Group;public double Health=100,SolarDays;public bool Dead;public V Position;public BleedingState Bleeding;
        public HumanPersonMemory Copy(){var p=(HumanPersonMemory)MemberwiseClone();p.Bleeding=Bleeding?.Copy();return p;}
    }
    [Serializable] public sealed class HumanCampMemory
    {
        public long Site,Group;public V Position;public double LastSolarDay,AbandonedCalendarDay=-1;public bool Disturbed;
        [System.Runtime.Serialization.OptionalField] public double LastDecayCalendarDay=-1;
        public List<CampObject> Objects=new List<CampObject>();
        public HumanCampMemory Copy(){var c=(HumanCampMemory)MemberwiseClone();c.Objects=Objects.Select(o=>o.Copy()).ToList();return c;}
    }
    [Serializable] public sealed class HumanGroupMemory
    {
        public long Group;public double Trust,Fear,AlarmUntilSolarDay,LastSolarDay;public V ThreatPosition;
        public double FireWoodKg,SuppliesUpdatedSolarDay=-1;public int FuelContributions;
        public HumanGroupMemory Copy()=>(HumanGroupMemory)MemberwiseClone();
    }
}
