using System;
using System.Collections.Generic;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        PrehistoryState relationIndexState;HistoricalRelation[] relationIndexRecords;string relationIndexHash;
        readonly Dictionary<(int,int),HistoricalRelation> relationIndex=new Dictionary<(int,int),HistoricalRelation>();
        HistoricalRelation FindHistoricalRelation(PrehistoryState s,int a,int b)
        {
            if(a<0||b<0||a==b)return null;var records=s.History.Relations;
            if(!ReferenceEquals(s,relationIndexState)||!ReferenceEquals(records,relationIndexRecords)||s.Hash!=relationIndexHash)
            {
                relationIndex.Clear();foreach(var record in records)
                {
                    var key=(record.A,record.B);if(!relationIndex.ContainsKey(key))relationIndex.Add(key,record);
                }
                relationIndexState=s;relationIndexRecords=records;relationIndexHash=s.Hash;
            }
            // Entries reference this working state's live relation objects.
            // War/truce/battle changes are not cached as stale boolean verdicts.
            relationIndex.TryGetValue((Math.Min(a,b),Math.Max(a,b)),out var relation);return relation;
        }
    }
}
