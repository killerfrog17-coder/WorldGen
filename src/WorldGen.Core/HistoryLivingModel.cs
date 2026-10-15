using System;
using System.Linq;
namespace WorldGen.Core
{
    // Optional extension keeps history17's saved hashes and replay unchanged.
    [Serializable] public sealed class HistoricalLivingCity
    {
        public int AbandonedYear=-1,DeclineYears,StressMilli,PeakYear,LastContactYear,DisruptedUntil;
        public long PeakPopulation;
        public double Productivity=1;
        public HistoricalLivingCity Copy()=>(HistoricalLivingCity)MemberwiseClone();
    }
    [Serializable] public sealed class HistoricalLivingRegion
    {
        public int Teacher=-1,Exposure,LastExchangeYear,CivicTeacher=-1,CivicExposure;
        public long CommittedAreaKm2,SettledPopulation;
        public HistoricalLivingRegion Copy()=>(HistoricalLivingRegion)MemberwiseClone();
    }
    [Serializable] public sealed class HistoricalLivingHistory
    {
        public string Version="living-history-1";
        public HistoricalCellTree TownBuckets=new HistoricalCellTree(180*90*16),BucketCounts=new HistoricalCellTree(180*90);
        public HistoricalCellTree CivicOrigins=new HistoricalCellTree(33554432);
        public HistoricalLivingCity[] Cities=Array.Empty<HistoricalLivingCity>();
        public HistoricalLivingRegion[] Regions=Array.Empty<HistoricalLivingRegion>();
        public HistoricalLivingHistory Copy()=>new HistoricalLivingHistory{Version=Version,TownBuckets=TownBuckets,BucketCounts=BucketCounts,CivicOrigins=CivicOrigins,Cities=Cities.Select(c=>c.Copy()).ToArray(),Regions=Regions.Select(r=>r.Copy()).ToArray()};
    }
    public sealed partial class Prehistory
    {
        public const string LivingHistoryVersion="history-18";
        static bool LivingHistory(PrehistoryState s)=>s?.History?.Aggregates?.Living!=null;
        public PrehistoryState UpgradeToLivingHistory(PrehistoryState source)
        {
            Validate(source);if(LivingHistory(source))return source;
            if(!AggregateHistory(source))source=UpgradeToAggregateHistory(source);
            var s=source.Copy();var a=s.History.Aggregates;
            a.Living=new HistoricalLivingHistory{Cities=s.Society.Settlements.Select(t=>new HistoricalLivingCity{PeakPopulation=t.Population,PeakYear=s.Years}).ToArray(),Regions=a.Regions.Select(_=>new HistoricalLivingRegion()).ToArray()};
            foreach(var polity in s.History.Polities)if(a.Living.CivicOrigins[polity.CultureId]<0)a.Living.CivicOrigins=a.Living.CivicOrigins.With(polity.CultureId,polity.Id);
            foreach(var region in a.Regions)if(region.Culture<0)
            {
                double best=-1;
                foreach(int cell in Patches[region.Id].Cells){double potential=landscapeStored[cell]+wildCropStorage[cell]*LandscapeCropFactor(cell)*.3+riverAccess[cell]*landscapeYield[cell]*.2;if(potential>best){best=potential;region.BestCell=cell;}}
            }
            foreach(var t in s.Society.Settlements)
            {
                if(t.Population>0){RegisterLivingTown(s,t.Id);a.Living.Regions[CellPatch[t.Cell]].CommittedAreaKm2+=Math.Max(1,a.Cities[t.Id].AreaKm2);a.Living.Regions[CellPatch[t.Cell]].SettledPopulation+=t.Population+a.Cities[t.Id].Rural+a.Cities[t.Id].Suburban;}
                else a.Living.Cities[t.Id].AbandonedYear=s.Years;
            }
            s.History.Version=LivingHistoryVersion;s.Seal();return s;
        }
        static int LivingTownBucket(V p)
        {int x=Math.Max(0,Math.Min(179,(int)((Math.Atan2(p.Y,p.X)/Math.PI+1)*90))),y=Math.Max(0,Math.Min(89,(int)((Math.Asin(p.Z)/Math.PI+.5)*90)));return y*180+x;}
        void RegisterLivingTown(PrehistoryState s,int id)
        {
            var l=s.History.Aggregates.Living;int bucket=LivingTownBucket(World.Surface.Cells[s.Society.Settlements[id].Cell].Position),count=Math.Max(0,l.BucketCounts[bucket]);
            // Sixteen centers per ~222 km square is already urban saturation.
            // Migration preserves denser historical saves; new foundations stop.
            if(count>=16)return;l.TownBuckets=l.TownBuckets.With(bucket*16+count,id);l.BucketCounts=l.BucketCounts.With(bucket,count+1);
        }
        void UnregisterLivingTown(PrehistoryState s,int id)
        {
            var l=s.History.Aggregates.Living;int bucket=LivingTownBucket(World.Surface.Cells[s.Society.Settlements[id].Cell].Position),count=Math.Max(0,l.BucketCounts[bucket]);
            for(int k=0;k<count;k++)if(l.TownBuckets[bucket*16+k]==id){l.TownBuckets=l.TownBuckets.With(bucket*16+k,l.TownBuckets[bucket*16+count-1]).With(bucket*16+count-1,-1);l.BucketCounts=l.BucketCounts.With(bucket,count-1);return;}
        }
        // Fixed spatial buckets, not a scan of any culture/state/region membership.
        int[] NearbyLivingTowns(PrehistoryState s,int cell,double rangeKm)
        {
            var l=s.History.Aggregates.Living;var p=World.Surface.Cells[cell].Position;int bucket=LivingTownBucket(p),x=bucket%180,y=bucket/180;
            int dy=Math.Min(3,1+(int)(rangeKm/220)),dx=Math.Min(12,1+(int)(rangeKm/(220*Math.Max(.12,Math.Cos(Math.Asin(p.Z))))));
            var found=new System.Collections.Generic.List<(double,int)>();
            for(int yy=Math.Max(0,y-dy);yy<=Math.Min(89,y+dy);yy++)for(int xx=x-dx;xx<=x+dx;xx++)
            {int b=yy*180+(xx+180)%180,count=Math.Min(16,Math.Max(0,l.BucketCounts[b]));for(int k=0;k<count;k++){int id=l.TownBuckets[b*16+k];if(id<0||l.Cities[id].AbandonedYear>=0)continue;double km=D.Angle(p,World.Surface.Cells[s.Society.Settlements[id].Cell].Position)*World.Recipe.RadiusKm;if(km<=rangeKm)found.Add((km,id));}}
            return found.OrderBy(v=>v.Item1).ThenBy(v=>v.Item2).Take(12).Select(v=>v.Item2).ToArray();
        }
    }
}
