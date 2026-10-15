using System;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        bool AgronomicResearchSupported(PrehistoryState s,int culture)
        {
            if(s.Society.Cultures[culture].Farming>=400)return true;
            if(!AgronomicHistory(s))return false;
            var a=s.History.Aggregates;var own=a.Cultures[culture];
            if(own.Rural<10000||own.SupportedUrban<5000||own.Cities.Count==0)return false;
            // A predominantly fishing/foraging culture can still contain a
            // substantial community of practicing farmers. Inspect at most
            // three real centers, never a culture's terrain or city census.
            int offset=(int)(HistoryRoll("agronomic-research-sites",culture,s.Years)*own.Cities.Count);
            for(int k=0;k<Math.Min(3,own.Cities.Count);k++)
            {
                int city=own.Cities.At((offset+k)%own.Cities.Count);
                var account=a.Cities[city];var crops=s.History.Towns[city].Cropping;
                if(account.Supported&&account.Rural>=4000&&crops?.CropIds?.Length>0&&s.Society.Settlements[city].Population>=1000)return true;
            }
            return false;
        }
    }
}
