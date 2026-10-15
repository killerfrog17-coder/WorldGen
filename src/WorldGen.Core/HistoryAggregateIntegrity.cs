using System;
using System.Linq;
namespace WorldGen.Core
{
    public sealed partial class PrehistoryState
    {
        internal string AggregateHash(bool verify)
        {
            var h=History;var a=h.Aggregates;
            if(verify)
            {
                a.Cells.Fingerprint(true);a.TownAt.Fingerprint(true);a.Successors.Fingerprint(true);a.LeaderDigests.Fingerprint(true);
                a.Living?.TownBuckets.Fingerprint(true);a.Living?.BucketCounts.Fingerprint(true);
                a.Living?.CivicOrigins.Fingerprint(true);
                a.PreviousAdministration?.Fingerprint(true);
                string archive=StateHash.Compute(h.Archive.Take(a.ArchiveBaseCount).ToList());
                if(archive!=a.ArchiveBaseHash)throw new ArgumentException("Modified frozen history archive.");
                for(int i=a.ArchiveBaseCount;i<h.Archive.Count;i++)archive=StateHash.Compute(new object[]{archive,h.Archive[i]});
                if(archive!=a.ArchiveHash)throw new ArgumentException("Modified historical event log.");
                for(int i=0;i<h.Dynamics.Leaders.Length;i++)
                {string digest=StateHash.Compute(h.Dynamics.Leaders[i]);for(int k=0;k<8;k++)if(a.LeaderDigests[i*8+k]!=unchecked((int)Convert.ToUInt32(digest.Substring(k*8,8),16)))throw new ArgumentException("Modified historical leader record.");}
            }
            return StateHash.Compute(new object[]{Version,Species,WorldHash,Origin,Years,Started,Population,ArrivalYear,Ancestry,Society,h.Version,a,h.Cultures,h.Religions,h.Polities,h.Relations,h.Towns,h.Agriculture,h.Dynamics.States,h.Dynamics.Campaigns,h.Dynamics.Dependencies,h.Modern,h.OceanSettlements,h.Administration,Timeline});
        }
        internal void SealAggregateOwned(){Hash=AggregateHash(false);}
    }
    public sealed partial class Prehistory
    {
        void SealAggregateLeader(PrehistoryState s,int id)
        {var a=s.History.Aggregates;string digest=StateHash.Compute(s.History.Dynamics.Leaders[id]);for(int k=0;k<8;k++)a.LeaderDigests=a.LeaderDigests.With(id*8+k,unchecked((int)Convert.ToUInt32(digest.Substring(k*8,8),16)));}
    }
}
