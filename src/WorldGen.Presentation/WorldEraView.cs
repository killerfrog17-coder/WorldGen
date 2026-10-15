using System;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    // Observer-facing milestones only. No simulation clock, technology or save is changed.
    public static class WorldEraView
    {
        public static string Name(int era)=>era switch
        {
            1=>"Planet creation",2=>"Prehistory",3=>"Neolithic",4=>"Bronze Age",5=>"Iron Age",
            6=>"Medieval",7=>"Early Modern",8=>"Industrial",9=>"Modern",10=>"Postmodern",
            _=>throw new ArgumentOutOfRangeException(nameof(era))
        };
        // Highest capability actually reached anywhere, including a culture whose state fell.
        // These are soundtrack/display periods, not a claim that every society shares one era.
        public static int Of(PrehistoryState? state)
        {
            if(state==null)return 1;
            int era=2;
            if(state.History==null)return era;
            foreach(var culture in state.History.Cultures)
            {
                bool Has(HistoricalTechnology t)=>HistoricalTechnologies.Has(culture,t);
                int reached=Has(HistoricalTechnology.Computing)?10:
                    Has(HistoricalTechnology.Electricity)||Has(HistoricalTechnology.CombustionEngines)?9:
                    Has(HistoricalTechnology.SteamPower)?8:
                    Has(HistoricalTechnology.Printing)||Has(HistoricalTechnology.Gunpowder)||Has(HistoricalTechnology.OceanNavigation)?7:
                    Has(HistoricalTechnology.Scholasticism)||Has(HistoricalTechnology.Steel)&&Has(HistoricalTechnology.Mills)?6:
                    Has(HistoricalTechnology.Iron)?5:Has(HistoricalTechnology.Bronze)?4:Has(HistoricalTechnology.Cultivation)?3:2;
                era=Math.Max(era,reached);
                if(era==10)break;
            }
            return era;
        }
    }
}
