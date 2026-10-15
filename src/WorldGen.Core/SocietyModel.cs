using System;
using System.Linq;
namespace WorldGen.Core
{
    public enum SocietyStage { Foragers, AdvancedCulture, EmbryonicCivilization, Civilization }
    public enum ChronicleCategory { Politics, Society, Discoveries }
    [Serializable] public sealed class CultureIdentity
    {
        public int Id, OriginCell, FoundedYear, StageYear, Farming, Organization;
        public SocietyStage Stage=SocietyStage.AdvancedCulture;
        public string Name="", Subsistence="", Resources="";
        public int Gathering, Hunting, Fishing, Herding;
        public long Population, AreaKm2;
        public int[] Contacts=Array.Empty<int>();
        public CultureIdentity Copy(){var c=(CultureIdentity)MemberwiseClone();c.Contacts=(int[])Contacts.Clone();return c;}
    }
    [Serializable] public sealed class Settlement
    {
        public int Id, CultureId, PolityId, Cell, FoundedYear;
        public string Name="", Government="Local council";
        public long Population, FoodCapacity, FoodReserve;
        public int Health=800, Stability=750, CultivatedPermille, CraftSharePermille=50;
        public Settlement Copy()=>(Settlement)MemberwiseClone();
    }
    [Serializable] public sealed class ChronicleEvent
    {
        public int Year, CultureId, Cell;public ChronicleCategory Category;public string Text="";
    }
    [Serializable] public sealed class SocietyState
    {
        public string Version="early-societies-1";
        public int[] Knowledge=Array.Empty<int>(), CellCulture=Array.Empty<int>();
        public CultureIdentity[] Cultures=Array.Empty<CultureIdentity>();
        public Settlement[] Settlements=Array.Empty<Settlement>();
        public ChronicleEvent[] Events=Array.Empty<ChronicleEvent>();
        public int SelectedStepYears=100;public bool ManualStep;
        public SocietyStage Phase=>Cultures.Length==0?SocietyStage.Foragers:Cultures.Max(c=>c.Stage);
        public SocietyState Copy()=>new SocietyState{Version=Version,Knowledge=(int[])Knowledge.Clone(),CellCulture=(int[])CellCulture.Clone(),Cultures=Cultures.Select(c=>c.Copy()).ToArray(),Settlements=Settlements.Select(t=>t.Copy()).ToArray(),Events=(ChronicleEvent[])Events.Clone(),SelectedStepYears=SelectedStepYears,ManualStep=ManualStep};
        internal SocietyState CopyDemographicKnowledge(){var copy=(SocietyState)MemberwiseClone();copy.Knowledge=(int[])Knowledge.Clone();return copy;}
        public static int DefaultStep(SocietyStage stage)=>stage==SocietyStage.Civilization?1:stage==SocietyStage.EmbryonicCivilization?10:stage==SocietyStage.AdvancedCulture?50:100;
    }
}
