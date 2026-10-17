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
        public static string Description(int era)=>Name(era)+": "+(era switch
        {
            3=>"sustained proto-urban settlement institutions before the first state",4=>"the first recorded state institutions",5=>"iron production",
            6=>"steel, mills and organized scholarly institutions",
            7=>"printing, experimental science and a broader maritime, agricultural or gunpowder package",
            8=>"steam-powered industry and industrial metallurgy",
            9=>"electricity, combustion engines and supporting public-health/communication systems",
            10=>"electronics, computing and digital telecommunications",_=>"before an established agricultural package"
        })+". The world's technological frontier, not a calendar date or universal adoption.";
        public static int[] FirstYears(PrehistoryState state)
        {
            var first=new int[11];for(int k=0;k<first.Length;k++)first[k]=-1;first[2]=0;
            if(state?.History==null)return first;
            bool regional=state.History.RegionalTraditions!=null;
            void Record(int era,int year){if(year>=0&&(first[era]<0||year<first[era]))first[era]=year;}
            foreach(var c in state.History.Cultures)
            {
                int Year(HistoricalTechnology t)=>c.Technical!=null?c.Technical.InstalledYear[(int)t]-1:HistoricalTechnologies.Has(c,t)?c.Technology[(int)t]-1:-1;
                if(c.Technical!=null)
                {
                    if(!regional)Record(3,Year(HistoricalTechnology.Cultivation));Record(4,Year(HistoricalTechnology.Bronze));Record(5,Year(HistoricalTechnology.Iron));
                    RecordPackages(Year,Record);continue;
                }
                if(!regional)Record(3,Year(HistoricalTechnology.Cultivation));Record(4,Year(HistoricalTechnology.Bronze));Record(5,Year(HistoricalTechnology.Iron));
                RecordPackages(Year,Record);
            }
            if(regional)
            {
                first[3]=state.History.RegionalTraditions!.FirstProtoYear;
            }
            // Owner's observer convention: first states begin the Bronze Age,
            // even if their particular material tradition is not bronze-making.
            // This is not a technology grant or an unlock for distant colonies.
            first[4]=SimulationClockView.EpochYear(state);
            for(int era=5;era<=10;era++)if(first[era]>=0)first[era]=first[4]<0?-1:Math.Max(first[4],first[era]);
            return first;
        }
        static int Together(int a,int b)=>a<0||b<0?-1:Math.Max(a,b);
        static void RecordPackages(Func<HistoricalTechnology,int> year,Action<int,int> record)
        {
            record(6,Together(year(HistoricalTechnology.Scholasticism),Together(year(HistoricalTechnology.Steel),year(HistoricalTechnology.Mills))));
            record(7,Together(Together(year(HistoricalTechnology.Printing),year(HistoricalTechnology.ScientificMethod)),Second(year(HistoricalTechnology.Gunpowder),year(HistoricalTechnology.OceanNavigation),year(HistoricalTechnology.CropRotation))));
            record(8,Together(year(HistoricalTechnology.SteamPower),Together(year(HistoricalTechnology.IndustrialMetallurgy),year(HistoricalTechnology.Mills))));
            record(9,Together(Together(year(HistoricalTechnology.Electricity),year(HistoricalTechnology.CombustionEngines)),Second(year(HistoricalTechnology.GermTheory),year(HistoricalTechnology.Sanitation),year(HistoricalTechnology.Telegraph),year(HistoricalTechnology.Radio))));
            record(10,Together(Together(year(HistoricalTechnology.Computing),year(HistoricalTechnology.Telecommunications)),Together(year(HistoricalTechnology.Electronics),year(HistoricalTechnology.Electricity))));
        }
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
            int early=(Has(HistoricalTechnology.Gunpowder)?1:0)+(Has(HistoricalTechnology.OceanNavigation)?1:0)+(Has(HistoricalTechnology.CropRotation)?1:0);
            int modern=(Has(HistoricalTechnology.GermTheory)?1:0)+(Has(HistoricalTechnology.Sanitation)?1:0)+(Has(HistoricalTechnology.Telegraph)?1:0)+(Has(HistoricalTechnology.Radio)?1:0);
            return Has(HistoricalTechnology.Computing)&&Has(HistoricalTechnology.Telecommunications)&&Has(HistoricalTechnology.Electronics)&&Has(HistoricalTechnology.Electricity)?10:
                Has(HistoricalTechnology.Electricity)&&Has(HistoricalTechnology.CombustionEngines)&&modern>=2?9:
                Has(HistoricalTechnology.SteamPower)&&Has(HistoricalTechnology.IndustrialMetallurgy)&&Has(HistoricalTechnology.Mills)?8:
                Has(HistoricalTechnology.Printing)&&Has(HistoricalTechnology.ScientificMethod)&&early>=2?7:
                Has(HistoricalTechnology.Scholasticism)&&Has(HistoricalTechnology.Steel)&&Has(HistoricalTechnology.Mills)?6:Has(HistoricalTechnology.Iron)?5:Has(HistoricalTechnology.Bronze)?4:Has(HistoricalTechnology.Cultivation)?3:2;
        }
        // Highest capability actually reached anywhere, including a culture whose state fell.
        // These are soundtrack/display periods, not a claim that every society shares one era.
        public static int Of(PrehistoryState? state)
        {
            if(state==null)return 1;
            int era=2;
            if(state.History==null)return era;
            var regional=state.History.RegionalTraditions;
            bool states=SimulationClockView.EpochYear(state)>=0;
            if(!states&&regional!=null&&regional.FirstProtoYear<0)return 2;
            foreach(var culture in state.History.Cultures)
            {
                era=Math.Max(era,CultureEra(culture,true));
                if(era==10)break;
            }
            if(states)return Math.Max(4,era);
            return regional!=null?3:Math.Min(3,era);
        }
    }
}
