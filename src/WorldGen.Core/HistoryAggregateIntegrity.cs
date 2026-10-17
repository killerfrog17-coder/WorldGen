using System;
using System.Linq;
using System.Threading;
namespace WorldGen.Core
{
    public sealed partial class PrehistoryState
    {
        internal string AggregateHash(bool verify,CancellationToken token=default)
        {
            var h=History;var a=h.Aggregates;
            if(verify)
            {
                // Public models may be edited and explicitly resealed. Never
                // carry trusted closed-record bytes through that boundary.
                foreach(var p in h.Polities)p.OwnedCanonicalRecord=null;
                foreach(var d in h.Dynamics.States)d.OwnedCanonicalRecord=null;
                // Campaign pages detach and invalidate on mutable access/Fork.
                // Public writers never trust their cached bytes; clearing raw
                // shared frozen rows here would race a private snapshot hash.
                foreach(var t in h.Towns)t.OwnedCanonicalRecord=null;
                foreach(var c in a.Cities)c.OwnedCanonicalRecord=null;
                foreach(var t in Society.Settlements)t.OwnedCanonicalRecord=null;
                a.Cells.Fingerprint(true);a.TownAt.Fingerprint(true);a.Successors.Fingerprint(true);a.LeaderDigests.Fingerprint(true);
                a.Living?.TownBuckets.Fingerprint(true);a.Living?.BucketCounts.Fingerprint(true);
                a.Living?.CivicOrigins.Fingerprint(true);
                if(h.RegionalTraditions!=null)foreach(var tree in new[]{h.RegionalTraditions.Predecessors,h.RegionalTraditions.RegionalCounts,h.RegionalTraditions.Retired,h.RegionalTraditions.Preparation,h.RegionalTraditions.LastReview,h.RegionalTraditions.Decline,h.RegionalTraditions.ProtoSince,h.RegionalTraditions.Qualified})tree.Fingerprint(true);
                a.Provinces?.Centers.Fingerprint(true);
                a.Empires?.Connections?.ProvisionedPorts?.ByTown.Fingerprint(true);
                a.Empires?.Connections?.CoastalFarming?.Families.Fingerprint(true);
                a.Empires?.Connections?.Religion?.Communities.Fingerprint(true);
                a.Empires?.Connections?.Religion?.Policies.Fingerprint(true);
                a.Empires?.Connections?.Religion?.LayAssociations?.Records.Fingerprint(true);
                a.Empires?.Connections?.Religion?.Traditions?.Institutions.Fingerprint(true);
                if(h.Languages?.Spoken!=null){h.Languages.Spoken.Places.Fingerprint(true);h.Languages.Spoken.Regions.Fingerprint(true);h.Languages.Spoken.CultureVarieties.Fingerprint(true);}
                if(h.Identities!=null)foreach(var tree in new[]{h.Identities.Places,h.Identities.Regions,h.Identities.CultureDefaults,h.Identities.Memories,h.Identities.RulingIdentities})tree.Fingerprint(true);
                a.Empires?.Connections?.Realms?.Claimants?.At.Fingerprint(true);
                a.Empires?.Connections?.SeaService?.BySource.Fingerprint(true);
                a.Empires?.Connections?.LocalSeeds?.Towns.Fingerprint(true);
                a.Empires?.Connections?.PreparedPlots?.Cells.Fingerprint(true);
                a.Empires?.Connections?.PreparedPlots?.RegionalCapacity.Fingerprint(true);
                a.Empires?.Connections?.PreparedPlots?.RegionalEpoch.Fingerprint(true);
                if(a.Empires?.Connections?.ServiceRecovery!=null)
                {
                    var service=a.Empires.Connections.ServiceRecovery;
                    foreach(var tree in new[]{service.PendingSources.Items,service.PendingSources.Positions,service.Cursor,service.Reviewed,service.CreditKm2,service.LastReviewYear})tree.Fingerprint(true);
                    foreach(var edge in service.CityEdges){edge.Items.Fingerprint(true);edge.Positions.Fingerprint(true);}
                    service.RetiredGovernments?.Items.Fingerprint(true);service.RetiredGovernments?.Positions.Fingerprint(true);service.GovernmentCursor?.Fingerprint(true);
                }
                if(a.Empires?.Connections!=null)foreach(var culture in a.Empires.Connections.Cultures)culture.SeenCharts?.Fingerprint(true);
                if(a.Empires?.Connections?.PortSettlements!=null)
                {
                    var retry=a.Empires.Connections.PortSettlements;retry.LastTestYear.Fingerprint(true);
                    foreach(var government in retry.Governments){government.Pending.Items.Fingerprint(true);government.Pending.Positions.Fingerprint(true);}
                }
                if(a.Empires?.Connections?.Geography!=null)foreach(var map in a.Empires.Connections.Geography.Governments)
                {foreach(var tree in new[]{map.Grade,map.ReportedYear,map.Opportunity,map.Host,map.Shore,map.ReviewAfter,map.Places.Items,map.Places.Positions})tree.Fingerprint(true);}
                a.PreviousAdministration?.Fingerprint(true);
                string archive=StateHash.Compute(h.Archive.Take(a.ArchiveBaseCount).ToList());
                if(archive!=a.ArchiveBaseHash)throw new ArgumentException("Modified frozen history archive.");
                for(int i=a.ArchiveBaseCount;i<h.Archive.Count;i++)archive=StateHash.Compute(new object[]{archive,h.Archive[i]});
                if(archive!=a.ArchiveHash)throw new ArgumentException("Modified historical event log.");
                for(int i=0;i<h.Dynamics.Leaders.Length;i++)
                {string digest=StateHash.Compute(h.Dynamics.Leaders.Peek(i));for(int k=0;k<8;k++)if(a.LeaderDigests[i*8+k]!=unchecked((int)Convert.ToUInt32(digest.Substring(k*8,8),16)))throw new ArgumentException("Modified historical leader record.");}
            }
            var values=new object[]{Version,Species,WorldHash,Origin,Years,Started,Population,ArrivalYear,Ancestry,Society,h.Version,a,h.Cultures,h.Religions,h.Polities,h.Relations,h.Towns,h.Agriculture,h.Dynamics.States,h.Dynamics.Campaigns,h.Dynamics.Dependencies,h.Modern,h.OceanSettlements,h.Administration,Timeline};
            if(h.Languages!=null)values=values.Concat(new object[]{h.Languages}).ToArray();
            if(h.OriginReach!=null)values=values.Concat(new object[]{h.OriginReach}).ToArray();
            if(h.Nuclear!=null)values=values.Concat(new object[]{h.Nuclear}).ToArray();
            if(h.International!=null)values=values.Concat(new object[]{h.International}).ToArray();
            if(h.ReceiverFrontier!=null)values=values.Concat(new object[]{h.ReceiverFrontier}).ToArray();
            if(h.RegionalTraditions!=null)values=values.Concat(new object[]{h.RegionalTraditions}).ToArray();
            if(h.Identities!=null)values=values.Concat(new object[]{h.Identities}).ToArray();
            return verify?StateHash.Compute(values,token):StateHash.ComputeOwnedHistorical(values,h,token);
        }
        internal void SealAggregateOwned(CancellationToken token=default){Hash=AggregateHash(false,token);}
    }
    public sealed partial class Prehistory
    {
        void SealAggregateLeader(PrehistoryState s,int id)
        {var a=s.History.Aggregates;string digest=StateHash.Compute(s.History.Dynamics.Leaders[id]);for(int k=0;k<8;k++)a.LeaderDigests=a.LeaderDigests.With(id*8+k,unchecked((int)Convert.ToUInt32(digest.Substring(k*8,8),16)));}
    }
}
