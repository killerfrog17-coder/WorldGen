using System;
using System.Collections.Generic;
using System.Linq;
using WorldGen.Core;
namespace WorldGen.Presentation
{
    public static partial class HistoryAtlas
    {
        static void WorldExhibits(PrehistoryState s,Prehistory? engine,List<AtlasSection> sections)
        {
            long total=SocietyView.Population(s),named=s.Society.Settlements.Sum(t=>Math.Max(0,t.Population));
            sections.Insert(0,new AtlasSection{Title="Population accounts",Bars=true,Unit="people",Text="Named settlement residents versus all other modeled resident accounts. The second category includes rural populations, hinterlands and people outside centralized states; it is not a count of uninhabited land.",Links=new[]{new AtlasLink{Name="Named settlement residents",Value=named},new AtlasLink{Name="Other resident accounts",Value=Math.Max(0,total-named)}}});
            if(engine==null)return;
            int occupied=0;var livelihoods=new SortedDictionary<string,long>(StringComparer.Ordinal);
            for(int region=0;region<s.Population.Length;region++)
            {
                long residents=s.Population[region]+(s.History.Aggregates?.Living?.Regions[region].SettledPopulation??0);if(residents>0)occupied++;
                if(s.Population[region]<=0)continue;
                int culture=s.History.Aggregates?.Regions[region].Culture??-1;string name=culture>=0?s.Society.Cultures[culture].Subsistence:engine.Patches[region].Lifestyle+" foraging";
                if(!livelihoods.ContainsKey(name))livelihoods[name]=0;livelihoods[name]+=s.Population[region];
            }
            sections.Insert(1,new AtlasSection{Title="Human presence",Facts=new[]{F("Occupied habitat patches",N(occupied)),F("Habitat patches",N(s.Population.Length)),F("Recorded advanced cultures",N(s.Society.Cultures.Count(c=>c.Population>0)))},Text="Connected demographic habitat patches, not countries or equal-area cells. A count of occupied patches does not measure the percentage of land settled."});
            if(livelihoods.Count>0)sections.Insert(2,new AtlasSection{Title="Regional livelihoods",Bars=true,Unit="people",Text="The unadministered regional population accounts only. Named cities and state remote accounts are not assigned a forager lifestyle by this chart.",Links=livelihoods.OrderByDescending(x=>x.Value).Select(x=>new AtlasLink{Name=x.Key,Value=x.Value}).ToArray()});
        }
    }
}
