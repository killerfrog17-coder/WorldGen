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
        [System.Runtime.Serialization.OptionalField] public List<NpcMemory> Minds=new List<NpcMemory>();
        public HumanLocalState Copy()=>new HumanLocalState{People=People.Select(p=>p.Copy()).ToList(),Camps=Camps.Select(c=>c.Copy()).ToList(),Groups=Groups.Select(g=>g.Copy()).ToList(),Minds=(Minds??new List<NpcMemory>()).Select(m=>m.Copy()).ToList()};
        public void Validate()
        {
            Minds??=new List<NpcMemory>();
            foreach(var memory in Minds){memory?.ValidateDialogue();if(memory!=null){memory.Social??=new NpcSocialState();memory.Social.Validate();}}
            if(Minds.Any(m=>m==null||m.Person<=0||m.Recent==null||m.Recent.Count>12||m.Recent.Any(s=>s==null||s.Length>360)||(m.Thought??"").Length>160||(m.Trigger??"").Length>160)||Minds.Select(m=>m.Person).Distinct().Count()!=Minds.Count)throw new ArgumentException("Invalid NPC memories.");
            if(People==null||Camps==null||Groups==null||People.Any(p=>p==null)||Camps.Any(c=>c==null)||Groups.Any(g=>g==null))throw new ArgumentException("Invalid local human records.");
            if(People.Select(p=>p.Person).Distinct().Count()!=People.Count||Camps.Select(c=>c.Site).Distinct().Count()!=Camps.Count||Groups.Select(g=>g.Group).Distinct().Count()!=Groups.Count)throw new ArgumentException("Duplicate local human identity.");
            foreach(var p in People)if(p.Person<=0||!double.IsFinite(p.Health)||p.Health<0||p.Health>100||!double.IsFinite(p.SolarDays)||p.SolarDays<0||!double.IsFinite(p.Position.Length)||p.Position.Length>0&&Math.Abs(p.Position.Length-1)>1e-5)throw new ArgumentException("Invalid local person memory.");
            foreach(var p in People){p.Inventory?.Validate();p.Nutrition?.Validate();if(!double.IsFinite(p.NextMealSolarDay)||p.NextMealSolarDay<0)throw new ArgumentException("Invalid local meal time.");}
            foreach(var g in Groups)if(!double.IsFinite(g.Trust)||g.Trust< -1||g.Trust>1||!double.IsFinite(g.Fear)||g.Fear<0||g.Fear>1||!double.IsFinite(g.AlarmUntilSolarDay)||!double.IsFinite(g.FireWoodKg)||g.FireWoodKg<0||g.FireWoodKg>1000||!double.IsFinite(g.SuppliesUpdatedSolarDay)||g.SuppliesUpdatedSolarDay< -1||g.FuelContributions<0)throw new ArgumentException("Invalid group relationship/supply memory.");
            foreach(var c in Camps){if(c.Objects==null||!double.IsFinite(c.LastSolarDay)||!double.IsFinite(c.AbandonedCalendarDay)||c.AbandonedCalendarDay< -1||!double.IsFinite(c.LastDecayCalendarDay)||c.LastDecayCalendarDay< -1||!double.IsFinite(c.Position.Length)||Math.Abs(c.Position.Length-1)>1e-5)throw new ArgumentException("Invalid visited camp.");new CampState{Objects=c.Objects}.Validate();}
        }
    }
    [Serializable] public sealed class HumanPersonMemory
    {
        public long Person,Group;public double Health=100,SolarDays;public bool Dead;public V Position;public BleedingState Bleeding;
        [System.Runtime.Serialization.OptionalField] public PlayerInventory Inventory;
        [System.Runtime.Serialization.OptionalField] public PlayerSurvival Nutrition;
        [System.Runtime.Serialization.OptionalField] public double NextMealSolarDay;
        public HumanPersonMemory Copy(){var p=(HumanPersonMemory)MemberwiseClone();p.Bleeding=Bleeding?.Copy();p.Inventory=Inventory?.Copy();p.Nutrition=Nutrition?.Copy();return p;}
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
