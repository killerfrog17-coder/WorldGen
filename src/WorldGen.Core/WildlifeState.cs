using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldGen.Core
{
    [Serializable] public sealed class WildlifeIndividual
    {
        public ulong Id;public string Species="";public V Position;public double Heading,Health,MaximumHealth,LastSeenDays;
        [System.Runtime.Serialization.OptionalField] public byte[] BloodMask;
        public int Coat;public int[] Parts;public bool Manual;public double[] CorpsePose;
        [System.Runtime.Serialization.OptionalField] public bool CorpseSettled;
        [System.Runtime.Serialization.OptionalField] public double CorpseHeightOffsetM,Morph;
        [System.Runtime.Serialization.OptionalField] public ulong GroupId;
        [System.Runtime.Serialization.OptionalField] public int GroupIndex,GroupSize;
        [System.Runtime.Serialization.OptionalField] public int Taxon=-1;
        [System.Runtime.Serialization.OptionalField] public ExertionState Exertion;
        [System.Runtime.Serialization.OptionalField] public BleedingState Bleeding;
        [System.Runtime.Serialization.OptionalField] public WildlifeAwarenessState Awareness;
        [System.Runtime.Serialization.OptionalField] public V ThreatPosition;
        [System.Runtime.Serialization.OptionalField] public ulong HuntTargetId;
        [System.Runtime.Serialization.OptionalField] public V HuntLastPosition;
        [System.Runtime.Serialization.OptionalField] public double HuntMemorySeconds;
        public bool Dead=>Health<=0;
        public WildlifeIndividual Copy(){var p=(WildlifeIndividual)MemberwiseClone();p.Exertion=Exertion?.Copy();p.Bleeding=Bleeding?.Copy();p.Awareness=Awareness?.Copy();p.BloodMask=BloodMask==null?null:(byte[])BloodMask.Clone();p.Parts=Parts==null?null:(int[])Parts.Clone();p.CorpsePose=CorpsePose==null?null:(double[])CorpsePose.Clone();return p;}
        public void Validate()
        {
            Exertion?.Validate();Awareness?.Validate();Bleeding?.Validate();
            if(!ItemUseRules.Finite(HuntMemorySeconds)||HuntMemorySeconds<0||HuntMemorySeconds>300||HuntMemorySeconds>0&&(!ItemUseRules.Finite(HuntLastPosition.Length)||Math.Abs(HuntLastPosition.Length-1)>1e-5))throw new ArgumentException("Invalid wildlife hunt memory.");
            if(BloodMask!=null&&BloodMask.Length!=256*256)throw new ArgumentException("Invalid saved wildlife blood mask.");
            if(!ItemUseRules.Finite(CorpseHeightOffsetM)||!ItemUseRules.Finite(Morph))throw new ArgumentException("Invalid corpse offset.");
            if(Id==0||string.IsNullOrEmpty(Species)||Species.Length>80||Species.Any(c=>!char.IsLetterOrDigit(c)&&c!='-'&&c!='_')||!ItemUseRules.Finite(Position.Length)||Math.Abs(Position.Length-1)>1e-5||!ItemUseRules.Finite(Health)||!ItemUseRules.Finite(MaximumHealth)||MaximumHealth<=0||Health<0||Health>MaximumHealth||!ItemUseRules.Finite(Heading)||!ItemUseRules.Finite(LastSeenDays)||LastSeenDays<0||Coat<0||Coat>4095||Parts!=null&&(Parts.Length!=4||Parts.Any(p=>p<0||p>65535))||CorpsePose!=null&&(CorpsePose.Length%16!=0||CorpsePose.Length>128*16||CorpsePose.Any(v=>!ItemUseRules.Finite(v))))throw new ArgumentException("Invalid saved wildlife individual.");
        }
    }
    /// <summary>Only notable/injured/dead individuals need records. A killed
    /// procedural spawn keeps its identity; streaming must not revive it.</summary>
    [Serializable] public sealed class WildlifeState
    {
        public List<WildlifeIndividual> Individuals=new List<WildlifeIndividual>();
        [System.Runtime.Serialization.OptionalField] public List<BloodSign> BloodSigns=new List<BloodSign>();
        [System.Runtime.Serialization.OptionalField] public int PopulationVersion;
        [System.Runtime.Serialization.OptionalField] public List<WildlifeGroup> Groups=new List<WildlifeGroup>();
        public WildlifeState Copy()=>new WildlifeState{PopulationVersion=PopulationVersion,Individuals=Individuals.Select(p=>p.Copy()).ToList(),BloodSigns=(BloodSigns??new List<BloodSign>()).Select(p=>p.Copy()).ToList(),Groups=(Groups??new List<WildlifeGroup>()).Select(g=>g.Copy()).ToList()};
        public void Validate()
        {
            if(Individuals==null||Individuals.Any(p=>p==null)||Individuals.Select(p=>p.Id).Distinct().Count()!=Individuals.Count)throw new ArgumentException("Invalid wildlife records.");
            foreach(var p in Individuals)p.Validate();
            BloodSigns=BloodSigns??new List<BloodSign>();foreach(var sign in BloodSigns){if(sign==null)throw new ArgumentException("Missing blood sign.");sign.Validate();}
            Groups=Groups??new List<WildlifeGroup>();if(Groups.Any(g=>g==null)||Groups.Select(g=>g.Id).Distinct().Count()!=Groups.Count)throw new ArgumentException("Invalid wildlife groups.");foreach(var g in Groups)g.Validate();
        }
    }
}
