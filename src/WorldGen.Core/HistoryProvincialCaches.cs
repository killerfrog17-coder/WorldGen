using System;
using System.Collections.Generic;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        // Diagnostic literal path. These indexes never choose dates, routes or
        // outcomes; their eviction/state is neither saved nor integrity hashed.
        public bool UseProvincialCaches=true;
        readonly Dictionary<string,uint> historicalStreamHashes=new Dictionary<string,uint>(StringComparer.Ordinal);
        readonly Queue<string> historicalStreamOrder=new Queue<string>();
        readonly Dictionary<int,(long Epoch,int Cohort,int Reads)> provincialCohortCache=new Dictionary<int,(long,int,int)>();
        HistoricalAggregateState provincialCacheOwner;HistoricalCellTree provincialCacheCells,provincialCacheSuccessors;
        long provincialCacheEpoch;
        PrehistoryState provincialKnowledgeContext;
        readonly Dictionary<int,long> provincialKnowledgeMasks=new Dictionary<int,long>();
        readonly struct ProvincialKnowledgeScope:IDisposable
        {
            readonly Prehistory owner;
            public ProvincialKnowledgeScope(Prehistory owner,PrehistoryState state){this.owner=owner;owner.provincialKnowledgeContext=state;owner.provincialKnowledgeMasks.Clear();}
            public void Dispose(){if(owner!=null)owner.provincialKnowledgeContext=null;}
        }
        long CurrentProvincialKnowledge(PrehistoryState s,int culture)
        {
            if(UseProvincialCaches&&ReferenceEquals(s,provincialKnowledgeContext))
            {if(!provincialKnowledgeMasks.TryGetValue(culture,out long mask)){mask=ProvincialKnowledge(s.History.Cultures[culture]);provincialKnowledgeMasks.Add(culture,mask);}return mask;}
            return ProvincialKnowledge(s.History.Cultures[culture]);
        }
        int CurrentProvincialKnowledgeCount(PrehistoryState s,int culture)=>ProvincialKnowledgeCount(CurrentProvincialKnowledge(s,culture));
        double CachedHistoricalRoll(string stream,int id,int year)
        {
            if(!UseProvincialCaches)return Rng.Value(World.Recipe.Seed,"history-1-"+stream,unchecked(id*104729+year*7919));
            stream=stream??"";
            if(!historicalStreamHashes.TryGetValue(stream,out uint hash))
            {
                hash=2166136261;foreach(char c in "history-1-"+stream)hash=unchecked((hash^c)*16777619);
                if(historicalStreamHashes.Count>=1024)historicalStreamHashes.Remove(historicalStreamOrder.Dequeue());
                historicalStreamHashes.Add(stream,hash);historicalStreamOrder.Enqueue(stream);
            }
            return Rng.ValueFromStreamHash(World.Recipe.Seed,hash,unchecked(id*104729+year*7919));
        }
        int CachedProvincialCohort(PrehistoryState s,int region)
        {
            var a=s.History.Aggregates;
            if(!ReferenceEquals(a,provincialCacheOwner)||!ReferenceEquals(a.Cells,provincialCacheCells)||!ReferenceEquals(a.Successors,provincialCacheSuccessors))
            {provincialCacheOwner=a;provincialCacheCells=a.Cells;provincialCacheSuccessors=a.Successors;provincialCacheEpoch++;}
            if(UseProvincialCaches&&provincialCohortCache.TryGetValue(region,out var entry)&&entry.Epoch==provincialCacheEpoch)
            {a.CellReads+=entry.Reads;return entry.Cohort;}
            var cells=Patches[region].Cells;int cohort=-1,reads=0;
            for(int k=0;k<5;k++)
            {int cell=k==0?a.Regions[region].BestCell:cells[(cells.Length-1)*(k-1)/3];a.CellReads++;reads++;int raw=a.Cells[cell*5+1];if(raw>=0){cohort=a.PopulationCohort(raw);break;}}
            if(UseProvincialCaches)provincialCohortCache[region]=(provincialCacheEpoch,cohort,reads);
            return cohort;
        }
    }
}
