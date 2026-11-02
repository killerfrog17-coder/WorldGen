using System;
using System.Linq;
namespace WorldGen.Core
{
    public enum PaleolithicTechnique { FlakeCutting,PreparedCores,Scraping,WoodenSpears,Hafting,FireTending,HideWorking,PlantBinding,HideWraps,FrictionFire,CompoundAdhesive,BoneAwls,HideSewing,FittedClothing,InsulatedClothing,BonePoints,PressureFlaking,Bows,BarbedPoints,FishingLines,Rafts,Needles,SpearThrowers,FoodPreservation }
    public enum PaleolithicSpeech { Vocabulary,Sequencing,DisplacedReference,ClauseCombination }
    public sealed class SapientSpeciesDefinition
    {
        public readonly string Id,Name;public readonly double GenerationYears;
        public SapientSpeciesDefinition(string id,string name,double generation){Id=id;Name=name;GenerationYears=generation;}
    }
    public static class SapientSpecies
    {
        public static readonly SapientSpeciesDefinition Human=new SapientSpeciesDefinition("homo-sapiens","Homo sapiens",28);
        public static SapientSpeciesDefinition Find(string id)=>id==Human.Id?Human:null;
    }
    [Serializable] public sealed class PaleolithicCommunity
    {
        public int Patch,Identity,Parent=-1,FoundedYear,NextSpeechChange;
        public int[] Contacts=Array.Empty<int>();
        public string Species="homo-sapiens";public LanguageProfile Language,PreviousLanguage;public long LanguageChangeSeconds;
        // Values at BoundarySeconds. Rates are held until the next deterministic
        // decision boundary; querying between boundaries never mutates state.
        public double Residents,Capacity,GrowthRate;
        public double[] Knowledge=new double[PaleolithicRules.TechniqueCount],Targets=new double[PaleolithicRules.TechniqueCount],Rates=new double[PaleolithicRules.TechniqueCount],Discovery=new double[PaleolithicRules.TechniqueCount];
        public double[] Speech=new double[4],SpeechRates=new double[4];
        public PaleolithicCommunity Copy(){var c=(PaleolithicCommunity)MemberwiseClone();c.Contacts=(int[])Contacts.Clone();c.Language=Language.Copy();c.PreviousLanguage=PreviousLanguage?.Copy();c.Knowledge=(double[])Knowledge.Clone();c.Targets=(double[])Targets.Clone();c.Rates=(double[])Rates.Clone();c.Discovery=(double[])Discovery.Clone();c.Speech=(double[])Speech.Clone();c.SpeechRates=(double[])SpeechRates.Clone();return c;}
    }
    [Serializable] public sealed class PaleolithicState
    {
        public string Version="paleolithic-1";public int StartYearsBeforePresent=200000,NextIdentity;
        public long Seconds,BoundarySeconds;public PaleolithicCommunity[] Communities=Array.Empty<PaleolithicCommunity>();
        public PaleolithicState Copy(){var p=(PaleolithicState)MemberwiseClone();p.Communities=Communities.Select(c=>c.Copy()).ToArray();return p;}
    }
    public sealed class PaleolithicBand
    {
        public long Id;public int Patch,Community,Cell;public string Name,Species,LanguageName;
        public LanguageProfile Language;public bool[] Techniques;public double[] Speech,Practice;
        public HumanAppearance Appearance;
        public bool Knows(PaleolithicTechnique t)=>Techniques[(int)t];
    }
    public readonly struct PaleolithicClimate
    {
        public readonly double Ice,TemperatureDeltaC,PrecipitationMultiplier,SeaFallM;
        public PaleolithicClimate(double ice,double temp,double rain,double sea){Ice=ice;TemperatureDeltaC=temp;PrecipitationMultiplier=rain;SeaFallM=sea;}
    }
}
