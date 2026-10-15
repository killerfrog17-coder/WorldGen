using System;
using System.Linq;
using System.Collections.Generic;
namespace WorldGen.Core
{
    [Serializable] public sealed class HistoricalCivicRivalry
    {
        public int Id,TownA,TownB,FirstYear,LastConflictYear,IntensityPermille,FirstCampaignId,LastCampaignId;
        public HistoricalCivicRivalry Copy()=>(HistoricalCivicRivalry)MemberwiseClone();
    }
    public static class HistoricalCivicMemoryEffects
    {
        // A bounded game prior, not an estimated universal cultural half-life.
        // Decay is evaluated from a dated conflict, never rounded every tick.
        public static int Intensity(HistoricalCivicRivalry memory,int year)
            =>(int)Math.Round(D.Q(memory.IntensityPermille*Math.Exp(-Math.Max(0,year-memory.LastConflictYear)/600.0),1000000));
        public static int PoliticalSalience(HistoricalCivicRivalry memory,int year,bool sharedInstitutions)
            =>(int)Math.Round(Intensity(memory,year)*(sharedInstitutions?.7:1));
        public static bool CanCooperate(int rivalry,double commonGrievance)=>rivalry<500||commonGrievance>=.65;
    }
    public sealed partial class Prehistory
    {
        HistoricalCivicRivalry[] civicMemoryRecords;
        readonly Dictionary<(int,int),HistoricalCivicRivalry> civicMemoryPairs=new Dictionary<(int,int),HistoricalCivicRivalry>();
        HistoricalCivicRivalry CivicMemory(PrehistoryState s,int a,int b)
        {
            var records=s.History.Modern?.CivicRivalries;if(records==null||records.Length==0||a==b)return null;
            if(!ReferenceEquals(records,civicMemoryRecords))
            {
                civicMemoryPairs.Clear();foreach(var r in records)civicMemoryPairs.Add((r.TownA,r.TownB),r);civicMemoryRecords=records;
            }
            civicMemoryPairs.TryGetValue((Math.Min(a,b),Math.Max(a,b)),out var memory);return memory;
        }
        int CivicRivalry(PrehistoryState s,int a,int b)
        {
            if(!ModernHistory(s))return 0;var memory=CivicMemory(s,a,b);if(memory==null)return 0;
            var x=s.Society.Settlements[a];var y=s.Society.Settlements[b];bool shared=x.PolityId>=0&&x.PolityId==y.PolityId;
            return HistoricalCivicMemoryEffects.PoliticalSalience(memory,s.Years,shared);
        }
        void RecordCivicRivalry(PrehistoryState s,Settlement x,Settlement y,int campaign,int severity)
        {
            if(!ModernHistory(s)||x.Id==y.Id||x.Population<500||y.Population<500||D.Angle(World.Surface.Cells[x.Cell].Position,World.Surface.Cells[y.Cell].Position)*World.Recipe.RadiusKm>400)return;
            var modern=s.History.Modern;var memory=CivicMemory(s,x.Id,y.Id);
            if(memory==null)
            {
                var records=modern.CivicRivalries??Array.Empty<HistoricalCivicRivalry>();
                memory=new HistoricalCivicRivalry{Id=records.Length,TownA=Math.Min(x.Id,y.Id),TownB=Math.Max(x.Id,y.Id),FirstYear=s.Years,FirstCampaignId=campaign,LastCampaignId=campaign,LastConflictYear=s.Years,IntensityPermille=Math.Min(1000,severity)};
                modern.CivicRivalries=records.Concat(new[]{memory}).ToArray();return;
            }
            // Repeated actions in one campaign do not manufacture a century's
            // worth of grievances in a few annual integration steps.
            if(memory.LastCampaignId==campaign&&s.Years-memory.LastConflictYear<25)return;
            memory.IntensityPermille=Math.Min(1000,HistoricalCivicMemoryEffects.Intensity(memory,s.Years)+severity);memory.LastConflictYear=s.Years;memory.LastCampaignId=campaign;
        }
    }
}
