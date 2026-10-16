using System;
using System.Linq;
namespace WorldGen.Core
{
    [Serializable] public sealed class HistoricalProvincialFrontiers
    {
        public HistoricalLocalBag[] Posts=Array.Empty<HistoricalLocalBag>();
        public HistoricalProvincialFrontiers Copy()=>new HistoricalProvincialFrontiers{Posts=Posts.Select(p=>p.Copy()).ToArray()};
    }
    public sealed partial class Prehistory
    {
        public const string ProvincialFrontierHistoryVersion="history-25";
        static bool ProvincialFrontierHistory(PrehistoryState s)=>LivingWorldHistory(s)||s?.History?.Version==ProvincialFrontierHistoryVersion||ColonialHistory(s);
        public PrehistoryState UpgradeToProvincialFrontierHistory(PrehistoryState source)
        {
            Validate(source);if(ProvincialFrontierHistory(source))return source;
            source=UpgradeToTechnicalHistory(source);var s=source.Copy();s.History.Version=ProvincialFrontierHistoryVersion;
            var empire=s.History.Aggregates.Empires;
            empire.Frontiers=new HistoricalProvincialFrontiers{Posts=empire.Territories.Posts.Select(p=>new HistoricalLocalBag()).ToArray()};
            // Explicit setup/upgrade only. Ongoing claims maintain local deltas.
            for(int cell=0;cell<CellPatch.Length;cell++)RefreshProvincialFrontier(s,cell);
            s.Seal();return s;
        }
        void AppendProvincialFrontier(PrehistoryState s)
        {if(ProvincialFrontierHistory(s)){var f=s.History.Aggregates.Empires.Frontiers;f.Posts=f.Posts.Concat(new[]{new HistoricalLocalBag()}).ToArray();}}
        void RefreshProvincialFrontier(PrehistoryState s,int cell)
        {
            var e=s.History.Aggregates.Empires;int source=e.Territories.Sources[cell];if(source> -2)return;
            int id=-source-2;var bag=e.Frontiers.Posts[id];
            // Queue service-district edges, whose identity survives political
            // successor links. Ownership is tested only when taking an action.
            // A regime change therefore needs no district-membership census.
            foreach(int neighbor in World.Surface.Cells[cell].Neighbors)
                if(PoliticalFrontierCell(s,neighbor,1)&&World.Hydrology.LakeCoverageFraction[neighbor]<.9&&e.Territories.Sources[neighbor]!=source){bag.Add(cell);return;}
            bag.Remove(cell);
        }
        int ProvincialFrontierTarget(PrehistoryState s,int polity,int trial)
        {
            var e=s.History.Aggregates.Empires;var posts=e.Governments[polity].AdministrativePosts;if(posts.Count==0)return -1;
            int id=posts.At((int)(HistoryRoll("provincial-frontier-office-"+trial,polity,s.Years)*posts.Count));
            int ancestor=id;bool maritime=false;
            for(int depth=0;depth<8;depth++){var post=e.Territories.Posts[ancestor];if(post.Maritime){maritime=true;break;}if(post.ParentSource> -2)break;ancestor=-post.ParentSource-2;}
            if(!maritime)return -1;
            var bag=e.Frontiers.Posts[id];if(bag.Count==0||!TerritorialNodeLive(s,polity,-2-id))return -1;
            int cell=bag.At((int)(HistoryRoll("provincial-frontier-cell-"+trial,polity,s.Years)*bag.Count));
            if(e.Territories.Sources[cell]!=-2-id){bag.Remove(cell);return -1;}
            if(HistoricalCells.Polity(s,cell)!=polity)return -1;
            var neighbors=World.Surface.Cells[cell].Neighbors;int offset=(int)(HistoryRoll("provincial-frontier-edge-"+trial,polity,s.Years)*neighbors.Length);
            for(int k=0;k<neighbors.Length;k++){int next=neighbors[(offset+k)%neighbors.Length];s.History.Aggregates.CellReads++;if(PoliticalFrontierCell(s,next,1)&&World.Hydrology.LakeCoverageFraction[next]<.9&&HistoricalCells.Polity(s,next)<0)return next;}
            // Keep occupied foreign edges: a neighboring court can dissolve
            // through a successor link without visiting its former holdings.
            // Its newly unclaimed boundary must not disappear from this queue.
            RefreshProvincialFrontier(s,cell);return -1;
        }
        void ValidateProvincialFrontiers(PrehistoryState s)
        {
            var e=s.History.Aggregates.Empires;var f=e?.Frontiers;
            if(!ProvincialFrontierHistory(s)){if(f!=null)throw new ArgumentException("Legacy provincial-frontier extension.");return;}
            if(f?.Posts?.Length!=e.Territories.Posts.Length)throw new ArgumentException("Provincial-frontier alignment.");
            for(int id=0;id<f.Posts.Length;id++)
            {var bag=f.Posts[id];if(bag==null||bag.Count<0||bag.Count>CellPatch.Length)throw new ArgumentException("Invalid provincial frontier.");for(int k=0;k<bag.Count;k++){int cell=bag.At(k);if(cell<0||cell>=CellPatch.Length||bag.Positions[cell]!=k||e.Territories.Sources[cell]!=-2-id)throw new ArgumentException("Invalid provincial-frontier reference.");}}
            // Integrity/loading only: a missing local front must not silently
            // strand a province. Stale entries are allowed and removed lazily.
            for(int cell=0;cell<CellPatch.Length;cell++)
            {
                int source=e.Territories.Sources[cell];if(source> -2)continue;int id=-source-2;
                foreach(int neighbor in World.Surface.Cells[cell].Neighbors)
                    if(PoliticalFrontierCell(s,neighbor,1)&&World.Hydrology.LakeCoverageFraction[neighbor]<.9&&e.Territories.Sources[neighbor]!=source)
                    {if(f.Posts[id].Positions[cell]<0)throw new ArgumentException("Missing provincial frontier.");break;}
            }
        }
    }
}
