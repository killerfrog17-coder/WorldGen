using System;
using System.Collections.Generic;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        PrehistoryState relationIndexState;HistoricalRelationCollection relationIndexRecords;string relationIndexHash;int relationIndexCount;
        readonly Dictionary<(int,int),int> relationIndex=new Dictionary<(int,int),int>();
        HistoricalRelation FindHistoricalRelation(PrehistoryState s,int a,int b)
        {
            if(a<0||b<0||a==b)return null;var records=s.History.Relations;
            if(!ReferenceEquals(s,relationIndexState)||!ReferenceEquals(records,relationIndexRecords)||records.Length!=relationIndexCount||s.Hash!=relationIndexHash)
            {
                relationIndex.Clear();for(int index=0;index<records.Length;index++)
                {
                    var record=records.Peek(index);var key=(record.A,record.B);if(!relationIndex.ContainsKey(key))relationIndex.Add(key,index);
                }
                relationIndexState=s;relationIndexRecords=records;relationIndexHash=s.Hash;relationIndexCount=records.Length;
            }
            // Entries reference this working state's live relation objects.
            // War/truce/battle changes are not cached as stale boolean verdicts.
            return relationIndex.TryGetValue((Math.Min(a,b),Math.Max(a,b)),out int relation)?records[relation]:null;
        }
    }
}
