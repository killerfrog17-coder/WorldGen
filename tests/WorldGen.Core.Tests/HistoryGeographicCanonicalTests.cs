using WorldGen.Core;

public static class HistoryGeographicCanonicalTests
{
    public static void Run(Action<bool,string> check)
    {
        for(int sample=0;sample<40;sample++)
        {
            var map=new HistoricalGeographicMap(73){FailedVoyages=sample,Landfalls=sample*17,NextPlan=long.MaxValue-sample,ScheduledDue=long.MinValue+sample,Surveys=~sample,Voyages=sample*3};
            if(sample%2==0)map.Sharing=new HistoricalGeographicSharing{CityCursor=sample,ContactCursor=sample*3,PeerCursor=~sample,RecentRegion=sample-1,ReportCursor=sample*7};
            if(sample%3==0)map.Record(sample,2,sample,150,-1,sample+1);
            if(sample%4==0)map.Missions=new[]{new HistoricalSeaMission{Kind=1,Target=sample,Departed=long.MinValue+sample,Due=long.MaxValue-sample,Host=-1}};
            check(StateHash.Compute(map)==StateHash.ComputeHistoricalReference(map),"Typed geographical record is byte-identical to independent literal reflection.");
            var maps=new[]{map,null,map.Copy()};
            check(StateHash.Compute(maps)==StateHash.ComputeHistoricalReference(maps),"Array framing, null records, optional sharing and copied maps retain canonical bytes.");
            string previous=StateHash.Compute(map);map.Voyages++;
            check(StateHash.Compute(map)!=previous&&StateHash.Compute(map)==StateHash.ComputeHistoricalReference(map),"Every hash reads changed live geographical fields without trusting a value cache.");
        }
        var sharing=new HistoricalGeographicSharing();check(StateHash.Compute(sharing)==StateHash.ComputeHistoricalReference(sharing),"A standalone sharing record retains default negative-field bytes.");
    }
}
