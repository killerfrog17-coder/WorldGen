using System;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        // Exact runtime-only memo. A town's seed inventory owns immutable
        // cultivar/practice snapshots. Neither weather nor living population
        // enters the field-landscape calculation; those effects remain live
        // in the caller. Never memoize a culture's mutable fallback profiles.
        public bool UseSeedLandscapeCache=true;
        public long SeedLandscapeCacheHits {get;private set;}
        public long SeedLandscapeCacheMisses {get;private set;}
        struct SeedLandscapeMemo
        {
            internal HistoricalTownSeeds Seeds;
            internal string HistoryVersion;
            internal int Revision,Flags,Ready;
            internal double Raw,Stored;
        }
        SeedLandscapeMemo[] seedLandscapeMemo;
        const int SeedLandscapeSlots=32768;
        static int SeedLandscapeSlot(HistoricalTownSeeds seeds,bool carried)
            =>unchecked(seeds.TownId*2+(carried?1:0))&(SeedLandscapeSlots-1);
        bool TrySeedLandscape(PrehistoryState s,HistoricalTownSeeds seeds,int cell,bool managed,bool pottery,bool carried,bool stored,out double value)
        {
            value=0;
            if(!UseAgronomicFitCache||!UseSeedLandscapeCache||!LivingWorldHistory(s)||!LocalSeedlotHistory(s)||seeds==null||seeds.FieldCell!=cell)return false;
            if(seedLandscapeMemo==null)seedLandscapeMemo=new SeedLandscapeMemo[SeedLandscapeSlots];
            ref var entry=ref seedLandscapeMemo[SeedLandscapeSlot(seeds,carried)];
            int flags=(managed?1:0)|(pottery?2:0);
            if(ReferenceEquals(entry.Seeds,seeds)&&entry.HistoryVersion==s.History.Version
                &&entry.Revision==s.History.Aggregates.Empires.Connections.Realms.Version&&entry.Flags==flags
                &&(entry.Ready&(stored?2:1))!=0)
            {
                SeedLandscapeCacheHits++;value=stored?entry.Stored:entry.Raw;return true;
            }
            SeedLandscapeCacheMisses++;return false;
        }
        void RememberSeedLandscape(PrehistoryState s,HistoricalTownSeeds seeds,int cell,bool managed,bool pottery,bool carried,bool stored,double value)
        {
            if(!UseAgronomicFitCache||!UseSeedLandscapeCache||!LivingWorldHistory(s)||!LocalSeedlotHistory(s)||seeds==null||seeds.FieldCell!=cell)return;
            ref var entry=ref seedLandscapeMemo[SeedLandscapeSlot(seeds,carried)];
            int flags=(managed?1:0)|(pottery?2:0);
            if(!ReferenceEquals(entry.Seeds,seeds)||entry.HistoryVersion!=s.History.Version
                ||entry.Revision!=s.History.Aggregates.Empires.Connections.Realms.Version||entry.Flags!=flags)
                entry=new SeedLandscapeMemo{Seeds=seeds,HistoryVersion=s.History.Version,Revision=s.History.Aggregates.Empires.Connections.Realms.Version,Flags=flags};
            if(stored)entry.Stored=value;else entry.Raw=value;
            entry.Ready|=stored?2:1;
        }
    }
}
