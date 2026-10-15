using System;
using WorldGen.Core;
namespace WorldGen.Presentation
{
    // Explicit, on-demand atlas/review calculation over aggregate accounts,
    // never terrain memberships or an ordinary simulation actor's turn.
    public static class HistoricalTechnologyCoverage
    {
        public static long[] Measure(PrehistoryState state)
        {
            var result=new long[HistoricalTechnologies.Names.Length];
            if(state==null||state.History==null||state.History.Aggregates==null||state.Society==null)return result;
            var history=state.History;var aggregate=history.Aggregates;
            void Add(int culture,long people)
            {
                if(culture<0||culture>=history.Cultures.Length||people<=0)return;
                var practices=history.Cultures[culture].Technology;
                for(int k=0;k<result.Length;k++)if(practices[k]>0)result[k]+=history.Cultures[culture].Technical==null?people:(long)Math.Round(people*history.Cultures[culture].Technical.Deployment[k]/1000.0);
            }
            for(int region=0;region<aggregate.Regions.Length;region++)Add(aggregate.Regions[region].Culture,state.Population[region]);
            for(int city=0;city<aggregate.Cities.Length;city++)
            {var town=state.Society.Settlements[city];var account=aggregate.Cities[city];Add(town.CultureId,town.Population+account.Suburban+account.Rural);}
            for(int polity=0;polity<aggregate.Polities.Length;polity++)Add(history.Polities[polity].CultureId,aggregate.Polities[polity].Remote);
            return result;
        }
    }
}
