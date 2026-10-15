using System;
using System.Runtime.Serialization;
namespace WorldGen.Core
{
    // Historical offices and fine-cell project footprints are archival, not
    // live simulation membership. Keep them intact without recopying them.
    [DataContract] public sealed class HistoricalFrozenAdministration
    {
        [DataMember] HistoricalAdministration records;
        [IgnoreDataMember] string digest;
        public HistoricalFrozenAdministration(HistoricalAdministration source){records=source?.Copy()??new HistoricalAdministration();}
        public HistoricalAdministration Read()=>records.Copy();
        public string Fingerprint(bool verify=false){if(verify||digest==null)digest=StateHash.Compute(records);return digest;}
    }
}
