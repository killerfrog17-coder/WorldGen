using System;
using System.Linq;
namespace WorldGen.Core
{
    [Serializable] public sealed class HistoricalTerritorialAdministration
    {
        // Nonnegative codes are real town IDs; -2-id denotes an administrative
        // post. -1 means no recorded service source. These are not new people.
        public HistoricalCellTree Sources,PostAt;
        public long[] DistrictAreas=Array.Empty<long>();
        public HistoricalAdministrativePost[] Posts=Array.Empty<HistoricalAdministrativePost>();
        public HistoricalTerritorialAdministration Copy()=>new HistoricalTerritorialAdministration{Sources=Sources,PostAt=PostAt,DistrictAreas=(long[])DistrictAreas.Clone(),Posts=Posts.Select(p=>p.Copy()).ToArray()};
    }
    [Serializable] public sealed class HistoricalAdministrativePost
    {
        public int Id,PolityId,Cell,ParentSource,Depth,FoundedYear,ClosedYear=-1,AutonomyMilli;
        public long AnnualCost,AreaKm2;
        public double MessageEffortKm;
        public string Name="";
        public bool Maritime;
        public int Founders,LegDistanceM;
        public HistoricalAdministrativePost Copy()=>(HistoricalAdministrativePost)MemberwiseClone();
    }
    public static class HistoricalProvinceIdentity
    {
        public const int PostIdentityOffset=16777216;
        public static bool Exists(PrehistoryState s,int identity)
        {var t=s?.History?.Aggregates?.Empires?.Territories;return t!=null&&identity>=0&&(identity<PostIdentityOffset?identity<t.DistrictAreas.Length:identity-PostIdentityOffset<t.Posts.Length);}
        public static int Of(PrehistoryState s,int cell)
        {
            var t=s.History?.Aggregates?.Empires?.Territories;if(t==null||cell<0||cell>=t.Sources.Length||HistoricalCells.Polity(s,cell)<0)return -1;
            int source=t.Sources[cell];return source>=0?source:source<=-2?PostIdentityOffset+(-source-2):-1;
        }
        public static int Source(int identity)=>identity<PostIdentityOffset?identity:-2-(identity-PostIdentityOffset);
        public static int Center(PrehistoryState s,int identity)
        {return !Exists(s,identity)?-1:identity<PostIdentityOffset?s.Society.Settlements[identity].Cell:s.History.Aggregates.Empires.Territories.Posts[identity-PostIdentityOffset].Cell;}
        public static string Name(PrehistoryState s,int identity)
        {return !Exists(s,identity)?"Unknown province":identity<PostIdentityOffset?s.Society.Settlements[identity].Name+" District":s.History.Aggregates.Empires.Territories.Posts[identity-PostIdentityOffset].Name;}
        public static long Area(PrehistoryState s,int identity)
        {return !Exists(s,identity)?0:identity<PostIdentityOffset?s.History.Aggregates.Empires.Territories.DistrictAreas[identity]:s.History.Aggregates.Empires.Territories.Posts[identity-PostIdentityOffset].AreaKm2;}
    }
    public sealed partial class Prehistory
    {
        double TerritorialPoliticalArea(int cell)=>World.Hydrology.OceanConnected[cell]?inhabitableArea[cell]:World.Surface.Cells[cell].AreaM2/1000000.0*(1-World.Hydrology.LakeCoverageFraction[cell]);
        bool PoliticalFrontierCell(PrehistoryState s,int cell,int layer)=>CellPatch[cell]>=0||layer==1&&TechnicalHistory(s)&&!World.Hydrology.OceanConnected[cell];
        void InitializeTerritorialAdministration(PrehistoryState s)
        {
            var a=s.History.Aggregates;if(a.Empires.Territories!=null)return;
            a.Empires.Territories=new HistoricalTerritorialAdministration{Sources=a.Provinces.Centers,PostAt=new HistoricalCellTree(CellPatch.Length),DistrictAreas=new long[a.Cities.Length]};
            foreach(var polity in s.History.Polities)if(polity.EndedYear<0)polity.AreaKm2=0;
            // Explicit setup/migration only; ongoing territorial changes apply
            // deltas, and no state event enumerates district membership.
            for(int cell=0;cell<CellPatch.Length;cell++)
            {long area=(long)Math.Round(TerritorialPoliticalArea(cell));int source=a.Empires.Territories.Sources[cell];if(source>=0)a.Empires.Territories.DistrictAreas[source]+=area;int owner=HistoricalCells.Polity(s,cell);if(owner>=0)s.History.Polities[owner].AreaKm2+=area;}
        }
        void AssignTerritorialSource(PrehistoryState s,int cell,int source)
        {
            if(!TechnicalHistory(s))return;var t=s.History.Aggregates.Empires.Territories;int old=t.Sources[cell];if(old==source)return;
            if(ServiceRecoveryHistory(s)&&old>=0)s.History.Aggregates.Empires.Connections.ServiceRecovery.CityEdges[old].Remove(cell);
            long area=(long)Math.Round(TerritorialPoliticalArea(cell));
            if(old>=0)t.DistrictAreas[old]=Math.Max(0,t.DistrictAreas[old]-area);else if(old<=-2)t.Posts[-old-2].AreaKm2=Math.Max(0,t.Posts[-old-2].AreaKm2-area);
            if(source>=0)t.DistrictAreas[source]+=area;else if(source<=-2)t.Posts[-source-2].AreaKm2+=area;
            t.Sources=t.Sources.With(cell,source);
            RefreshServiceRecoveryNeighborhood(s,cell);
            if(ProvincialFrontierHistory(s))
            {
                if(old<=-2)s.History.Aggregates.Empires.Frontiers.Posts[-old-2].Remove(cell);
                RefreshProvincialFrontier(s,cell);
                foreach(int neighbor in World.Surface.Cells[cell].Neighbors)RefreshProvincialFrontier(s,neighbor);
            }
        }
        int TerritorialNodeCell(PrehistoryState s,int source)=>source>=0?s.Society.Settlements[source].Cell:s.History.Aggregates.Empires.Territories.Posts[-source-2].Cell;
        bool TerritorialNodeLive(PrehistoryState s,int polity,int source)
        {
            var a=s.History.Aggregates;var t=a.Empires.Territories;
            if(RevisedRealmHistory(s)&&source<=-2)
            {
                var hc=s.History.Cultures[s.History.Polities[polity].CultureId];
                if(RevisedMaritimeChainKm(s,polity,source)>StateMaritimeNetworkKm(s,polity))return false;
            }
            for(int depth=0;depth<8;depth++)
            {
                if(source>=0)return source<a.Cities.Length&&a.Living.Cities[source].AbandonedYear<0&&a.Resolve(s.Society.Settlements[source].PolityId,s.History)==polity;
                if(source== -1||-source-2>=t.Posts.Length)return false;var post=t.Posts[-source-2];
                if(post.ClosedYear>=0||a.Resolve(post.PolityId,s.History)!=polity||HistoricalCells.Polity(s,post.Cell)!=polity)return false;
                if(post.Maritime)
                {var hc=s.History.Cultures[s.History.Polities[polity].CultureId];if(!HistoricalTechnologies.Has(hc,HistoricalTechnology.Navigation)||post.LegDistanceM>HistoricalDevelopmentEffects.CoastRangeKm(hc,s.Years,ColonialHistory(s))*1000)return false;}
                source=post.ParentSource;
            }
            return false;
        }
        void ValidateTerritorialAdministration(PrehistoryState s)
        {
            if(!TechnicalHistory(s))return;var a=s.History.Aggregates;var t=a.Empires?.Territories;
            if(t?.Sources?.Length!=CellPatch.Length||t.PostAt?.Length!=CellPatch.Length||t.DistrictAreas?.Length!=a.Cities.Length||t.Posts==null)throw new ArgumentException("Territorial service alignment.");
            var areas=new long[a.Cities.Length];var posts=new long[t.Posts.Length];
            for(int cell=0;cell<CellPatch.Length;cell++)
            {
                int source=t.Sources[cell],at=t.PostAt[cell];if(source>=a.Cities.Length||source<-t.Posts.Length-1||at>=t.Posts.Length||at< -1)throw new ArgumentException("Territorial service reference.");
                long area=(long)Math.Round(TerritorialPoliticalArea(cell));if(source>=0)areas[source]+=area;else if(source<=-2)posts[-source-2]+=area;
            }
            for(int i=0;i<areas.Length;i++)if(areas[i]!=t.DistrictAreas[i])throw new ArgumentException("District area delta account.");
            for(int i=0;i<t.Posts.Length;i++)
            {
                var p=t.Posts[i];if(p==null||p.Id!=i||p.PolityId<0||p.PolityId>=a.Polities.Length||p.Cell<0||p.Cell>=CellPatch.Length||p.ParentSource== -1||p.ParentSource>=a.Cities.Length||p.ParentSource<-t.Posts.Length-1||p.ParentSource== -2-i||p.Depth<1||p.Depth>7||p.FoundedYear>s.Years||p.ClosedYear>s.Years||p.AnnualCost<0||p.AreaKm2!=posts[i]||p.MessageEffortKm<0||double.IsNaN(p.MessageEffortKm)||p.Founders<0||p.Founders>250||p.LegDistanceM<0)throw new ArgumentException("Invalid administrative post "+i+".");
            }
            var costs=new long[a.Polities.Length];var counts=new int[a.Polities.Length];
            foreach(var post in t.Posts)if(post.ClosedYear<0){int owner=a.Resolve(post.PolityId,s.History);if(owner>=0){costs[owner]+=post.AnnualCost;counts[owner]++;}}
            for(int i=0;i<a.Polities.Length;i++)if(s.History.Polities[i].EndedYear<0&&(a.Empires.Governments[i].AdministrativePostCost!=costs[i]||a.Empires.Governments[i].AdministrativePosts.Count!=counts[i]))throw new ArgumentException("Provincial payroll delta account.");
        }
    }
}
