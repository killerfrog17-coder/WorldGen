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
        public bool Dead=>Health<=0;
        public WildlifeIndividual Copy(){var p=(WildlifeIndividual)MemberwiseClone();p.BloodMask=BloodMask==null?null:(byte[])BloodMask.Clone();p.Parts=Parts==null?null:(int[])Parts.Clone();p.CorpsePose=CorpsePose==null?null:(double[])CorpsePose.Clone();return p;}
        public void Validate()
        {
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
        public WildlifeState Copy()=>new WildlifeState{Individuals=Individuals.Select(p=>p.Copy()).ToList()};
        public void Validate()
        {
            if(Individuals==null||Individuals.Any(p=>p==null)||Individuals.Select(p=>p.Id).Distinct().Count()!=Individuals.Count)throw new ArgumentException("Invalid wildlife records.");
            foreach(var p in Individuals)p.Validate();
        }
    }
}
