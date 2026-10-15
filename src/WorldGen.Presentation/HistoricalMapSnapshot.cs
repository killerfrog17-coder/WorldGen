using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using WorldGen.Core;
namespace WorldGen.Presentation
{
    // Renderer-only projection of a published checkpoint. Never used by Core
    // simulation actors. Multiple views/borders share one immutable export.
    public sealed class HistoricalMapSnapshot
    {
        sealed class Entry
        {
            public string Hash="";public Lazy<HistoricalMapSnapshot> Value=null!;
            public Entry(PrehistoryState source){Reset(source);}
            public void Reset(PrehistoryState source){Hash=source.Hash;Value=new Lazy<HistoricalMapSnapshot>(()=>new HistoricalMapSnapshot(source),LazyThreadSafetyMode.ExecutionAndPublication);}
        }
        static readonly ConditionalWeakTable<PrehistoryState,Entry> Entries=new ConditionalWeakTable<PrehistoryState,Entry>();
        readonly PrehistoryState source;readonly int[] cells,resolved,sovereigns,technology,boundaries,claims;readonly long[] remote;
        readonly System.Collections.Generic.Dictionary<int,int> technologySites=new System.Collections.Generic.Dictionary<int,int>();
        public int Count=>cells.Length/5;
        public static HistoricalMapSnapshot For(PrehistoryState state)
        {
            if(state==null)throw new ArgumentNullException(nameof(state));var entry=Entries.GetValue(state,s=>new Entry(s));Lazy<HistoricalMapSnapshot> value;
            lock(entry){if(entry.Hash!=state.Hash)entry.Reset(state);value=entry.Value;}return value.Value;
        }
        HistoricalMapSnapshot(PrehistoryState state)
        {
            source=state;int count=HistoricalCells.Count(state);cells=new int[count*5];
            if(state.History?.Aggregates!=null)state.History.Aggregates.Cells.CopyTo(cells);
            else for(int i=0;i<count;i++){cells[i*5]=HistoricalCells.Culture(state,i);cells[i*5+1]=HistoricalCells.Polity(state,i);cells[i*5+2]=HistoricalCells.Religion(state,i);cells[i*5+3]=HistoricalCells.Market(state,i);cells[i*5+4]=HistoricalCells.Stage(state,i);}
            resolved=new int[state.History?.Polities.Length??0];
            for(int i=0;i<resolved.Length;i++)resolved[i]=state.History!.Aggregates?.Resolve(i,state.History)??i;
            remote=new long[resolved.Length];if(state.History?.Aggregates!=null)for(int i=0;i<remote.Length;i++)remote[i]=state.History.Aggregates.Polities[state.History.Aggregates.PopulationCohort(i)].Remote;
            sovereigns=new int[resolved.Length];for(int i=0;i<sovereigns.Length;i++)sovereigns[i]=state.History!.Aggregates?.Empires==null?resolved[i]:HistoricalImperialHierarchy.Sovereign(state,i);
            technology=state.History?.Cultures.Select(c=>c.Technical!=null?WorldEraView.KnowledgeEra(c):HistoricalImperialHierarchy.InstalledPractices(c)).ToArray()??Array.Empty<int>();
            claims=new int[count];for(int i=0;i<count;i++)claims[i]=HistoricalRecognizedClaims.Holder(state,i);
            boundaries=new int[count];for(int i=0;i<count;i++){int p=PoliticalSovereign(i),c=Culture(i);boundaries[i]=p>=0?p:c>=0?-2-c:-1;int key=OverlayId(i,HistoryOverlay.Technology);if(key>=0&&!technologySites.ContainsKey(key))technologySites.Add(key,i);}
        }
        public int TechnologySite(int key)=>technologySites.TryGetValue(key,out int cell)?cell:-1;
        public int Culture(int cell)=>cell>=0&&cell<Count?cells[cell*5]:-1;
        public int Stage(int cell)=>cell>=0&&cell<Count?cells[cell*5+4]:-1;
        public int Religion(int cell)=>cell>=0&&cell<Count?cells[cell*5+2]:-1;
        public int Polity(int cell){int raw=cell>=0&&cell<Count?cells[cell*5+1]:-1;return raw>=0&&raw<resolved.Length?resolved[raw]:-1;}
        public long RemotePopulation(int cell){int raw=cell>=0&&cell<Count?cells[cell*5+1]:-1;return raw>=0&&raw<remote.Length?remote[raw]:0;}
        public int Sovereign(int cell){int raw=cell>=0&&cell<Count?cells[cell*5+1]:-1;return raw>=0&&raw<sovereigns.Length?sovereigns[raw]:-1;}
        // Recognized external title is deliberately separate from administration,
        // population and local culture. An actual state's territory always wins.
        public int PoliticalSovereign(int cell){int p=Sovereign(cell);return p>=0?p:cell>=0&&cell<Count&&claims[cell]>=0?sovereigns[claims[cell]]:-1;}
        public int Boundary(int cell)=>boundaries[cell];
        public int Boundary(int cell,HistoryOverlay overlay){if(overlay==HistoryOverlay.Provinces){int province=HistoricalProvinceIdentity.Of(source,cell);return province>=0?province:Boundary(cell);}if(overlay!=HistoryOverlay.Territories)return Boundary(cell);int p=Polity(cell),c=Culture(cell);return p>=0?p:c>=0?-2-c:-1;}
        public bool SameBoundaries(HistoricalMapSnapshot? other)
        {if(other==null||other.Count!=Count)return false;for(int i=0;i<boundaries.Length;i++)if(boundaries[i]!=other.boundaries[i])return false;return true;}
        public int OverlayId(int cell,HistoryOverlay overlay)
        {
            if(cell<0||cell>=Count||source.Society==null)return -1;int culture=Culture(cell),p=Polity(cell);var h=source.History;
            if(h==null)return culture>=0&&source.Society.Cultures[culture].Stage==SocietyStage.Civilization?culture:-1;
            if(overlay==HistoryOverlay.Provinces)return HistoricalProvinceIdentity.Of(source,cell);
            if(overlay==HistoryOverlay.Territories)return p;
            if(overlay==HistoryOverlay.Civilizations||overlay==HistoryOverlay.States)p=PoliticalSovereign(cell);
            if(overlay==HistoryOverlay.States)return p;if(overlay==HistoryOverlay.Religions)return Religion(cell);
            if(culture<0&&p>=0)culture=h.Polities[p].CultureId;if(culture<0)return -1;
            if(overlay==HistoryOverlay.Cultures)return culture;if(overlay==HistoryOverlay.Technology)return technology[culture];
            return p>=0?HistoricalRecognizedClaims.Civilization(source,h.Polities[p].CultureId):-1;
        }
        public int ColorId(int cell,HistoryOverlay overlay)
        {int owner=OverlayId(cell,overlay);return owner>=0&&overlay==HistoryOverlay.States&&source.History!=null?source.History.Polities[owner].CultureId:owner;}
    }
}
