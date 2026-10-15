using System;
using System.Collections.Generic;
using System.Linq;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        // Execution-only indexes over canonical arrays. A private session can
        // retain these across checkpoints. Public branches/hash changes rebuild
        // them, and cancellation discards them; none are saved or hashed.
        HistoricalWorkIndex historyWorkIndex;
        bool historyWorkActive;
        // Diagnostic reference path; this changes execution only, not rules.
        public bool UseHistoryIndexes { get; set; }=true;
        sealed class RuralSpan { public int Owner,Rank,Count;public long Prefix,Weight; }
        sealed class PatchLedger
        {
            public long Population=-1;
            public readonly List<RuralSpan> Spans=new List<RuralSpan>();
            public readonly SortedDictionary<int,long> Allocations=new SortedDictionary<int,long>();
        }
        sealed class HistoricalWorkIndex
        {
            public PrehistoryState State;public string Hash;
            public readonly SortedSet<int> CulturalCells=new SortedSet<int>(),CulturalFrontier=new SortedSet<int>(),PoliticalCells=new SortedSet<int>(),PoliticalFrontier=new SortedSet<int>(),StageOverrides=new SortedSet<int>(),DirtyPatches=new SortedSet<int>(),DirtyPoliticalPatches=new SortedSet<int>();
            public readonly List<SortedSet<int>> CultureCells=new List<SortedSet<int>>(),PolityCells=new List<SortedSet<int>>();
            public readonly PatchLedger[] Patches,PoliticalPatches;
            public long[] Rural=Array.Empty<long>(),Area=Array.Empty<long>(),PoliticalRural=Array.Empty<long>(),PoliticalArea=Array.Empty<long>();
            public readonly int[] CultureSite;public readonly long[] CultureSitePopulation;
            public CellLocator TownSites;public int TownSiteCount=-1;
            public bool TotalsVerified,StagesVerified;
            public long PoliticalRevision;
            public readonly SortedSet<int> AdministrativeChanges=new SortedSet<int>();
            public int[] MigrationMarket;
            public MigrationCatchment[] MigrationCells;
            public HistoricalWorkIndex(int patches){Patches=new PatchLedger[patches];PoliticalPatches=new PatchLedger[patches];CultureSite=Enumerable.Repeat(-2,patches).ToArray();CultureSitePopulation=new long[patches];}
            public void EnsureCulture(int id)
            {while(CultureCells.Count<=id)CultureCells.Add(new SortedSet<int>());if(Rural.Length<=id){Array.Resize(ref Rural,id+1);Array.Resize(ref Area,id+1);}}
            public void EnsurePolity(int id){while(PolityCells.Count<=id)PolityCells.Add(new SortedSet<int>());if(PoliticalRural.Length<=id){Array.Resize(ref PoliticalRural,id+1);Array.Resize(ref PoliticalArea,id+1);}}
        }
        HistoricalWorkIndex WorkIndex(PrehistoryState s)=>historyWorkActive&&ReferenceEquals(historyWorkIndex?.State,s)?historyWorkIndex:null;
        void BeginHistoricalWork(PrehistoryState source,PrehistoryState next)
        {
            if(!UseHistoryIndexes||source.Society==null||source.History==null){historyWorkIndex=null;return;}
            if(!ReferenceEquals(historyWorkIndex?.State,source)||historyWorkIndex.Hash!=source.Hash)
            {
                using(ProfileWork("membership index rebuild",source.Society.CellCulture.Length))
                {
                    var index=new HistoricalWorkIndex(Patches.Length){State=source,Hash=source.Hash};
                    foreach(var c in source.Society.Cultures)index.EnsureCulture(c.Id);
                    for(int cell=0;cell<source.Society.CellCulture.Length;cell++)
                    {
                        int owner=source.Society.CellCulture[cell];
                        if(owner>=0){index.EnsureCulture(owner);index.CulturalCells.Add(cell);index.CultureCells[owner].Add(cell);index.Area[owner]+=CellCulturalArea(source,cell);if(CellPatch[cell]>=0)index.DirtyPatches.Add(CellPatch[cell]);}
                    }
                    for(int cell=0;cell<source.History.CellPolity.Length;cell++)if(source.History.CellPolity[cell]>=0)
                    {int owner=source.History.CellPolity[cell];index.EnsurePolity(owner);index.PoliticalCells.Add(cell);index.PolityCells[owner].Add(cell);index.PoliticalArea[owner]+=CellCulturalArea(source,cell);if(CellPatch[cell]>=0)index.DirtyPoliticalPatches.Add(CellPatch[cell]);}
                    // A valid checkpoint may precede the next stage refresh:
                    // recently incorporated non-town cells can still show1.
                    for(int cell=0;cell<source.History.CellStage.Length;cell++)if(source.History.CellPolity[cell]>=0||source.History.CellStage[cell]!=(source.Society.CellCulture[cell]>=0?1:0))index.StageOverrides.Add(cell);
                    foreach(int cell in index.CulturalCells)if(World.Surface.Cells[cell].Neighbors.Any(next=>source.Society.CellCulture[next]!=source.Society.CellCulture[cell]))index.CulturalFrontier.Add(cell);
                    foreach(int cell in index.PoliticalCells)if(World.Surface.Cells[cell].Neighbors.Any(next=>source.History.CellPolity[next]!=source.History.CellPolity[cell]))index.PoliticalFrontier.Add(cell);
                    historyWorkIndex=index;
                }
            }
            // Engine copies preserve every market assignment. Carry its sparse
            // catchment index across that copy; a real market rebuild replaces
            // the array and is detected by MigrationCatchments below.
            if(ReferenceEquals(historyWorkIndex.MigrationMarket,source.History.CellMarket))historyWorkIndex.MigrationMarket=next.History.CellMarket;
            historyWorkIndex.State=next;historyWorkActive=true;
        }
        void EndHistoricalWork(PrehistoryState result,bool completed)
        {historyWorkActive=false;if(completed&&historyWorkIndex!=null){historyWorkIndex.State=result;historyWorkIndex.Hash=result.Hash;}else historyWorkIndex=null;}
        long CellCulturalArea(PrehistoryState s,int cell)=>(long)Math.Round(RevisedHistory(s)?HistoricalLandArea(cell):World.Surface.Cells[cell].AreaM2/1e6);
        IEnumerable<int> CulturalCells(PrehistoryState s)
            =>WorkIndex(s)?.CulturalCells??(IEnumerable<int>)Enumerable.Range(0,s.Society.CellCulture.Length).Where(i=>s.Society.CellCulture[i]>=0);
        IEnumerable<int> CultureCells(PrehistoryState s,int culture)
        {var index=WorkIndex(s);return index!=null&&culture>=0?(culture<index.CultureCells.Count?index.CultureCells[culture]:(IEnumerable<int>)Array.Empty<int>()):Enumerable.Range(0,s.Society.CellCulture.Length).Where(i=>s.Society.CellCulture[i]==culture);}
        IEnumerable<int> CulturalFrontierCells(PrehistoryState s)=>WorkIndex(s)?.CulturalFrontier??CulturalCells(s);
        IEnumerable<int> PoliticalCells(PrehistoryState s)
            =>WorkIndex(s)?.PoliticalCells??(IEnumerable<int>)Enumerable.Range(0,s.History.CellPolity.Length).Where(i=>s.History.CellPolity[i]>=0);
        IEnumerable<int> PoliticalFrontierCells(PrehistoryState s)=>WorkIndex(s)?.PoliticalFrontier??PoliticalCells(s);
        IEnumerable<int> PolityCells(PrehistoryState s,int polity)
        {var index=WorkIndex(s);return index!=null&&polity>=0?(polity<index.PolityCells.Count?index.PolityCells[polity]:(IEnumerable<int>)Array.Empty<int>()):Enumerable.Range(0,s.History.CellPolity.Length).Where(i=>s.History.CellPolity[i]==polity);}
        void SetCultureOwner(PrehistoryState s,int cell,int owner)
        {
            int previous=s.Society.CellCulture[cell];if(previous==owner)return;s.Society.CellCulture[cell]=owner;
            var index=WorkIndex(s);if(index==null)return;
            if(previous>=0){index.CultureCells[previous].Remove(cell);index.Area[previous]-=CellCulturalArea(s,cell);}
            if(owner>=0){index.EnsureCulture(owner);index.CulturalCells.Add(cell);index.CultureCells[owner].Add(cell);index.Area[owner]+=CellCulturalArea(s,cell);}else index.CulturalCells.Remove(cell);
            if(CellPatch[cell]>=0){index.DirtyPatches.Add(CellPatch[cell]);index.CultureSite[CellPatch[cell]]=-2;}
            index.StageOverrides.Add(cell);
            index.TotalsVerified=false;index.StagesVerified=false;
            void Frontier(int candidate)
            {int local=s.Society.CellCulture[candidate];if(local>=0&&World.Surface.Cells[candidate].Neighbors.Any(next=>s.Society.CellCulture[next]!=local))index.CulturalFrontier.Add(candidate);else index.CulturalFrontier.Remove(candidate);}
            Frontier(cell);foreach(int next in World.Surface.Cells[cell].Neighbors)Frontier(next);
        }
        void SetPoliticalOwner(PrehistoryState s,int cell,int owner)
        {
            int previous=s.History.CellPolity[cell];if(previous==owner)return;s.History.CellPolity[cell]=owner;
            var index=WorkIndex(s);if(index==null)return;
            index.PoliticalRevision++;index.AdministrativeChanges.Add(cell);
            if(previous>=0){index.PolityCells[previous].Remove(cell);index.PoliticalArea[previous]-=CellCulturalArea(s,cell);}
            if(owner>=0){index.EnsurePolity(owner);index.PoliticalCells.Add(cell);index.PolityCells[owner].Add(cell);index.PoliticalArea[owner]+=CellCulturalArea(s,cell);}else index.PoliticalCells.Remove(cell);
            index.StageOverrides.Add(cell);
            index.StagesVerified=false;
            if(CellPatch[cell]>=0)index.DirtyPoliticalPatches.Add(CellPatch[cell]);
            void Frontier(int candidate)
            {int local=s.History.CellPolity[candidate];if(local>=0&&World.Surface.Cells[candidate].Neighbors.Any(next=>s.History.CellPolity[next]!=local))index.PoliticalFrontier.Add(candidate);else index.PoliticalFrontier.Remove(candidate);}
            Frontier(cell);foreach(int next in World.Surface.Cells[cell].Neighbors)Frontier(next);
        }
        void RebuildPatchLedger(PrehistoryState s,PatchLedger[] ledgers,long[] totals,int[] owners,int patch)
        {
            var ledger=ledgers[patch]??(ledgers[patch]=new PatchLedger());
            foreach(var pair in ledger.Allocations)totals[pair.Key]-=pair.Value;
            ledger.Allocations.Clear();ledger.Spans.Clear();ledger.Population=-1;
            var cells=Patches[patch].Cells;RuralSpan run=null;
            for(int rank=0;rank<cells.Length;rank++)
            {
                int cell=cells[rank],owner=owners[cell];
                if(owner<0){run=null;continue;}
                if(run==null||run.Owner!=owner){run=new RuralSpan{Owner=owner,Rank=rank,Prefix=ruralPrefix[cell]};ledger.Spans.Add(run);}
                run.Count++;run.Weight+=ruralWeight[cell];
            }
        }
        void UpdateRuralLedgers(PrehistoryState s,PatchLedger[] ledgers,long[] totals,SortedSet<int> dirty,int[] owners)
        {
                foreach(int patch in dirty)RebuildPatchLedger(s,ledgers,totals,owners,patch);dirty.Clear();
                for(int patch=0;patch<ledgers.Length;patch++)
                {
                    var ledger=ledgers[patch];if(ledger==null||ledger.Population==s.Population[patch])continue;
                    foreach(var pair in ledger.Allocations)totals[pair.Key]-=pair.Value;ledger.Allocations.Clear();
                    long population=s.Population[patch],total=ruralPatchWeight[patch];int count=Patches[patch].Cells.Length;
                    foreach(var span in ledger.Spans)
                    {
                        long amount=EraHistory(s)&&total>0?AllocateRuralShare(population,span.Prefix,span.Weight,total):population/count*span.Count+Math.Max(0,Math.Min(span.Count,population%count-span.Rank));
                        ledger.Allocations[span.Owner]=(ledger.Allocations.TryGetValue(span.Owner,out long old)?old:0)+amount;
                    }
                    foreach(var pair in ledger.Allocations)totals[pair.Key]+=pair.Value;ledger.Population=population;
                }
        }
        bool RefreshIndexedSocietyTotals(PrehistoryState s)
        {
            var index=WorkIndex(s);if(index==null)return false;
            using(ProfileWork("regional population bookkeeping",Patches.Length))
            {
                UpdateRuralLedgers(s,index.Patches,index.Rural,index.DirtyPatches,s.Society.CellCulture);
                foreach(var c in s.Society.Cultures){index.EnsureCulture(c.Id);c.Population=index.Rural[c.Id];c.AreaKm2=index.Area[c.Id];}
                foreach(var town in s.Society.Settlements)s.Society.Cultures[town.CultureId].Population+=town.Population;
                index.TotalsVerified=true;
            }
            return true;
        }
        bool AddIndexedPoliticalTotals(PrehistoryState s)
        {
            var index=WorkIndex(s);if(index==null)return false;
            using(ProfileWork("regional state bookkeeping",Patches.Length))
            {
                foreach(var p in s.History.Polities)index.EnsurePolity(p.Id);
                UpdateRuralLedgers(s,index.PoliticalPatches,index.PoliticalRural,index.DirtyPoliticalPatches,s.History.CellPolity);
                foreach(var p in s.History.Polities){p.Population+=index.PoliticalRural[p.Id];p.AreaKm2+=index.PoliticalArea[p.Id];}
            }
            return true;
        }
        SortedDictionary<int,long> RuralPolityPatches(PrehistoryState s,int polity)
        {
            var result=new SortedDictionary<int,long>();var index=WorkIndex(s);
            if(index==null){foreach(int cell in PolityCells(s,polity))if(CellPatch[cell]>=0){int patch=CellPatch[cell];result[patch]=(result.TryGetValue(patch,out long prior)?prior:0)+RuralCellPopulation(s,cell);}return result;}
            UpdateRuralLedgers(s,index.PoliticalPatches,index.PoliticalRural,index.DirtyPoliticalPatches,s.History.CellPolity);
            for(int patch=0;patch<index.PoliticalPatches.Length;patch++)if(index.PoliticalPatches[patch]?.Allocations.TryGetValue(polity,out long people)==true)result[patch]=people;
            return result;
        }
        int[][] cultureSiteOrder;int cultureSiteMode=-1;
        readonly Dictionary<(int,int,bool,int),int[]> coastalCultureSites=new Dictionary<(int,int,bool,int),int[]>();
        int FindCoastalCultureSite(PrehistoryState s,int patch,int endpoint,bool founding)
        {
            var key=(patch,endpoint,founding,EraHistory(s)?2:RevisedHistory(s)?1:0);
            if(!UseHistoryIndexes||!coastalCultureSites.TryGetValue(key,out var candidates))
            {
                candidates=Patches[patch].Cells.Where(i=>(!founding||World.Hydrology.LakeCoverageFraction[i]<.5)&&((founding&&World.Hydrology.OceanConnected[i])||World.Surface.Cells[i].Neighbors.Any(j=>World.Hydrology.OceanConnected[j]))&&D.Angle(World.Surface.Cells[i].Position,World.Surface.Cells[endpoint].Position)*World.Recipe.RadiusKm<150).OrderByDescending(i=>founding?CultureOpportunity(s,i):HistoricalStorageOpportunity(i)).ThenBy(i=>i).ToArray();
                if(UseHistoryIndexes){if(coastalCultureSites.Count>=4096)coastalCultureSites.Clear();coastalCultureSites.Add(key,candidates);}
            }
            foreach(int cell in candidates)if(s.Society.CellCulture[cell]<0)return cell;return -1;
        }
        int FindCultureEmergenceSite(PrehistoryState s,int patch)
        {
            var index=WorkIndex(s);var society=s.Society;
            if(index==null)return Patches[patch].Cells.Where(i=>(society.CellCulture.Length==0||society.CellCulture[i]<0)&&World.Hydrology.LakeCoverageFraction[i]<.5&&(!EraHistory(s)||HistoricalSiteDensity(s,i)>=.022)).OrderByDescending(i=>CultureOpportunity(s,i)).ThenBy(i=>i).DefaultIfEmpty(-1).First();
            if(index.CultureSite[patch]!=-2&&index.CultureSitePopulation[patch]==s.Population[patch])return index.CultureSite[patch];
            int mode=EraHistory(s)?2:RevisedHistory(s)?1:0;
            if(cultureSiteOrder==null||cultureSiteMode!=mode)
            {
                using(ProfileWork("static emergence site priorities",World.Surface.Cells.Length))
                    cultureSiteOrder=Patches.Select(p=>p.Cells.Where(i=>World.Hydrology.LakeCoverageFraction[i]<.5).OrderByDescending(i=>CultureOpportunity(s,i)).ThenBy(i=>i).ToArray()).ToArray();
                cultureSiteMode=mode;
            }
            int found=-1;foreach(int cell in cultureSiteOrder[patch])if((society.CellCulture.Length==0||society.CellCulture[cell]<0)&&(!EraHistory(s)||HistoricalSiteDensity(s,cell)>=.022)){found=cell;break;}
            index.CultureSite[patch]=found;index.CultureSitePopulation[patch]=s.Population[patch];return found;
        }
        bool SettlementWithin100Km(PrehistoryState s,int cell)
        {
            var index=WorkIndex(s);var towns=s.Society.Settlements;
            if(index==null)return towns.Any(t=>D.Angle(World.Surface.Cells[t.Cell].Position,World.Surface.Cells[cell].Position)*World.Recipe.RadiusKm<100);
            if(index.TownSites==null||index.TownSiteCount!=towns.Length){index.TownSites=new CellLocator(towns.Select(t=>World.Surface.Cells[t.Cell]).ToArray());index.TownSiteCount=towns.Length;}
            double chord=2*D.Sin(101.0/(2*World.Recipe.RadiusKm));
            return index.TownSites.Within(World.Surface.Cells[cell].Position,chord*chord).Any(id=>D.Angle(World.Surface.Cells[towns[id].Cell].Position,World.Surface.Cells[cell].Position)*World.Recipe.RadiusKm<100);
        }
    }
}
