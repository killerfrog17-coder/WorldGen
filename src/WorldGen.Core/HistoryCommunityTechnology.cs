using System;
namespace WorldGen.Core
{
    public static class HistoricalCommunityTechnology
    {
        // Costs use the relevant workshop-network scale below, not history19's
        // fourfold bonus from merely knowing any four named peoples.
        public static int Cost(int technology)
            =>technology==(int)HistoricalTechnology.Bronze?250:technology==(int)HistoricalTechnology.Iron?700:HistoricalTechnologyDevelopment.Cost(technology);
    }
    public sealed partial class Prehistory
    {
        double CommunityTechnicalExchange(PrehistoryState s,int culture,int technology)
        {
            int participants=0;var h=s.History;var required=HistoricalTechnologies.Requirements(h.Version,technology);
            foreach(int peer in s.Society.Cultures[culture].Contacts)
            {
                if(!ProvincialRecentContact(s,culture,peer,true))continue;
                var practice=h.Aggregates.Cultures[peer];if(practice.SupportedCities<1||practice.SupportedUrban<1200)continue;
                bool relevant=true;foreach(int prerequisite in required)if(h.Cultures[peer].Technology[prerequisite]==0){relevant=false;break;}
                if(relevant&&++participants==4)break;
            }
            return 1+1.5*participants/4.0;
        }
    }
}
