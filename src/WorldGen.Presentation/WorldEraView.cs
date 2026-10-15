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
        public static int[] FirstYears(PrehistoryState state)
        {
            var first=new int[11];for(int k=0;k<first.Length;k++)first[k]=-1;first[2]=0;
            if(state?.History==null)return first;
            void Record(int era,int year){if(year>=0&&(first[era]<0||year<first[era]))first[era]=year;}
            foreach(var c in state.History.Cultures)
            {
                int Year(HistoricalTechnology t)=>c.Technical!=null?c.Technical.InstalledYear[(int)t]-1:HistoricalTechnologies.Has(c,t)?c.Technology[(int)t]-1:-1;
                if(c.Technical!=null)
                {
                    Record(3,Year(HistoricalTechnology.Cultivation));Record(4,Year(HistoricalTechnology.Bronze));Record(5,Year(HistoricalTechnology.Iron));
                    Record(6,Year(HistoricalTechnology.Scholasticism));Record(6,Together(Year(HistoricalTechnology.Steel),Year(HistoricalTechnology.Mills)));
                    Record(7,Second(Year(HistoricalTechnology.Printing),Year(HistoricalTechnology.Gunpowder),Year(HistoricalTechnology.OceanNavigation),Year(HistoricalTechnology.ScientificMethod)));
                    Record(8,Together(Year(HistoricalTechnology.SteamPower),Year(HistoricalTechnology.IndustrialMetallurgy)));Record(8,Year(HistoricalTechnology.Railways));
                    Record(9,Together(Year(HistoricalTechnology.Electricity),Year(HistoricalTechnology.CombustionEngines)));Record(9,Year(HistoricalTechnology.Radio));
                    Record(10,Together(Year(HistoricalTechnology.Computing),Year(HistoricalTechnology.Telecommunications)));continue;
                }
                Record(3,Year(HistoricalTechnology.Cultivation));Record(4,Year(HistoricalTechnology.Bronze));Record(5,Year(HistoricalTechnology.Iron));
                Record(6,Year(HistoricalTechnology.Scholasticism));
                int steel=Year(HistoricalTechnology.Steel),mills=Year(HistoricalTechnology.Mills);if(steel>=0&&mills>=0)Record(6,Math.Max(steel,mills));
                Record(7,Year(HistoricalTechnology.Printing));Record(7,Year(HistoricalTechnology.Gunpowder));Record(7,Year(HistoricalTechnology.OceanNavigation));
                Record(8,Year(HistoricalTechnology.SteamPower));Record(9,Year(HistoricalTechnology.Electricity));Record(9,Year(HistoricalTechnology.CombustionEngines));Record(10,Year(HistoricalTechnology.Computing));
            }
            return first;
        }
        static int Together(int a,int b)=>a<0||b<0?-1:Math.Max(a,b);
        static int Second(params int[] dates)
        {int first=int.MaxValue,second=int.MaxValue;foreach(int date in dates)if(date>=0){if(date<first){second=first;first=date;}else if(date<second)second=date;}return second==int.MaxValue?-1:second;}
        public static int CultureEra(HistoricalCulture culture,bool historicalPeak=false)
        {
            bool Has(HistoricalTechnology t)=>culture.Technical!=null&&historicalPeak?culture.Technical.InstalledYear[(int)t]>0:HistoricalTechnologies.Has(culture,t);
            return BundleEra(Has);
        }
        public static int KnowledgeEra(HistoricalCulture culture)=>BundleEra(t=>(int)t<culture.Technology.Length&&culture.Technology[(int)t]>0);
        static int BundleEra(Func<HistoricalTechnology,bool> Has)
        {
            int early=(Has(HistoricalTechnology.Printing)?1:0)+(Has(HistoricalTechnology.Gunpowder)?1:0)+(Has(HistoricalTechnology.OceanNavigation)?1:0)+(Has(HistoricalTechnology.ScientificMethod)?1:0);
            return Has(HistoricalTechnology.Computing)&&Has(HistoricalTechnology.Telecommunications)?10:
                Has(HistoricalTechnology.Electricity)&&Has(HistoricalTechnology.CombustionEngines)||Has(HistoricalTechnology.Radio)?9:
                Has(HistoricalTechnology.SteamPower)&&Has(HistoricalTechnology.IndustrialMetallurgy)||Has(HistoricalTechnology.Railways)?8:early>=2?7:
                Has(HistoricalTechnology.Scholasticism)||Has(HistoricalTechnology.Steel)&&Has(HistoricalTechnology.Mills)?6:Has(HistoricalTechnology.Iron)?5:Has(HistoricalTechnology.Bronze)?4:Has(HistoricalTechnology.Cultivation)?3:2;
        }
        // Highest capability actually reached anywhere, including a culture whose state fell.
        // These are soundtrack/display periods, not a claim that every society shares one era.
        public static int Of(PrehistoryState? state)
        {
            if(state==null)return 1;
            int era=2;
            if(state.History==null)return era;
            foreach(var culture in state.History.Cultures)
            {
                bool Has(HistoricalTechnology t)=>culture.Technical!=null?culture.Technical.InstalledYear[(int)t]>0:HistoricalTechnologies.Has(culture,t);
                if(culture.Technical!=null)
                {
                    int technical=CultureEra(culture,true);
                    era=Math.Max(era,technical);if(era==10)break;continue;
                }
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
