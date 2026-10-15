using System;
using System.Linq;
namespace WorldGen.Core
{
    public enum HistoricalAmbitionScope { Ecoregion, Subcontinental, Continental }
    [Serializable] public sealed class HistoricalTerritorialAmbition
    {
        public HistoricalAmbitionScope Scope;
        public int AnchorCell,Region,Landmass,SinceYear;
        public HistoricalTerritorialAmbition Copy()=>(HistoricalTerritorialAmbition)MemberwiseClone();
        public string Description=>Scope==HistoricalAmbitionScope.Continental?"Hegemony over the homeland continent":Scope==HistoricalAmbitionScope.Subcontinental?"Regional hegemony across the surrounding subcontinental sphere":"Unification of the homeland ecoregional district";
    }
    [Serializable] public sealed class HistoricalTerritorialAmbitions
    {
        public HistoricalTerritorialAmbition[] Governments=Array.Empty<HistoricalTerritorialAmbition>();
        public HistoricalTerritorialAmbitions Copy()=>new HistoricalTerritorialAmbitions{Governments=Governments.Select(x=>x?.Copy()).ToArray()};
    }
    public sealed partial class Prehistory
    {
        public const string TerritorialHistoryVersion="history-28";
        static bool RegionalAmbitionHistory(PrehistoryState s)=>s?.History?.Version==TerritorialHistoryVersion;
        public PrehistoryState UpgradeToTerritorialHistory(PrehistoryState source)
        {
            Validate(source);if(RegionalAmbitionHistory(source))return source;source=UpgradeToCoastalHistory(source);
            var s=source.Copy();s.History.Version=TerritorialHistoryVersion;
            s.History.Aggregates.Empires.Connections.Ambitions=new HistoricalTerritorialAmbitions{Governments=new HistoricalTerritorialAmbition[s.History.Polities.Length]};
            // Explicit migration visits actor records only, never their holdings.
            for(int i=0;i<s.History.Polities.Length;i++)RefreshTerritorialAmbition(s,i);
            s.Seal();return s;
        }
        void AppendTerritorialAmbition(PrehistoryState s,int predecessor)
        {
            var aims=s.History.Aggregates.Empires.Connections.Ambitions;if(aims==null)return;
            aims.Governments=aims.Governments.Concat(new[]{predecessor<0?null:aims.Governments[predecessor]?.Copy()}).ToArray();
        }
        void RefreshTerritorialAmbition(PrehistoryState s,int polity)
        {
            var aims=s.History.Aggregates.Empires?.Connections?.Ambitions;if(aims==null)return;
            var h=s.History;var p=h.Polities[polity];var old=aims.Governments[polity];int cell=old?.AnchorCell??s.Society.Settlements[p.CapitalId].Cell;
            int leader=h.Dynamics.States[polity].LeaderId,ambition=leader>=0?h.Dynamics.Leaders[leader].Ambition:0;
            // An institutional preference persists across rulers. A ruler's
            // ambition changes its reach, not its ability to fund or win wars.
            double disposition=Rng.Value(World.Recipe.Seed,"territorial-hegemony-tradition",polity);
            var scope=ambition>=750&&disposition<.55?HistoricalAmbitionScope.Continental:
                ambition>=550&&disposition<.85?HistoricalAmbitionScope.Subcontinental:HistoricalAmbitionScope.Ecoregion;
            if(old!=null&&old.Scope==scope)return;
            aims.Governments[polity]=new HistoricalTerritorialAmbition{Scope=scope,AnchorCell=cell,Region=CellPatch[cell],Landmass=World.Landmass[cell],SinceYear=s.Years};
        }
        bool TerritorialAmbitionContains(PrehistoryState s,int polity,int cell)
        {
            var aims=s.History.Aggregates.Empires?.Connections?.Ambitions;if(aims==null||cell<0)return false;
            var aim=aims.Governments[polity];if(aim==null||CellPatch[cell]<0)return false;
            if(aim.Scope==HistoricalAmbitionScope.Ecoregion)return CellPatch[cell]==aim.Region;
            if(aim.Landmass<0||World.Landmass[cell]!=aim.Landmass)return false;
            // A transparent provisional geographic sphere, not a claim to have
            // identified named or physically exact subcontinents.
            return aim.Scope==HistoricalAmbitionScope.Continental||D.Angle(World.Surface.Cells[aim.AnchorCell].Position,World.Surface.Cells[cell].Position)*World.Recipe.RadiusKm<=1800;
        }
        int PreferTerritorialFrontier(PrehistoryState s,int polity,int target,int trial)
        {
            if(!RegionalAmbitionHistory(s)||TerritorialAmbitionContains(s,polity,target))return target;
            int alternative=LocalFrontierTarget(s,s.History.Aggregates.Polities[polity].Frontier,polity,"territorial-aim-frontier-"+trial,1);
            return TerritorialAmbitionContains(s,polity,alternative)?alternative:target;
        }
        double TerritorialWarMotivation(PrehistoryState s,int polity,int cell)
        {
            if(!RegionalAmbitionHistory(s))return 1;var p=s.History.Polities[polity];
            if(p.Exhaustion>=650||p.Treasury<Math.Max(100,p.Army)||p.Cohesion<450)return 0;
            return D.Q((TerritorialAmbitionContains(s,polity,cell)?1.75:1)*D.Clamp(1-p.Exhaustion/750.0),1000000000);
        }
        void TryRecognizeRegionalTitle(PrehistoryState s,int polity,int partner)
        {
            var h=s.History;var a=h.Aggregates;var c=a.Empires.Connections;var p=h.Polities[polity];var posts=a.Empires.Governments[polity].AdministrativePosts;
            int cell=s.Society.Settlements[p.CapitalId].Cell;bool established=s.Years-p.FoundedYear>=25;
            if(posts.Count>0&&HistoryRoll("regional-title-office-or-capital",polity,s.Years)<.75)
            {
                int id=posts.At((int)(HistoryRoll("regional-title-office",polity,s.Years)*posts.Count));var post=a.Empires.Territories.Posts[id];
                cell=post.Cell;established=post.ClosedYear<0&&s.Years-post.FoundedYear>=10&&TerritorialNodeLive(s,polity,-2-id);
            }
            int region=CellPatch[cell];if(!established||region<0||c.TitleAt[region]>=0||p.Treasury<1000||HistoricalCells.Polity(s,cell)!=polity)return;
            int peerCell=s.Society.Settlements[h.Polities[partner].CapitalId].Cell;
            bool informed=c.Cultures[h.Polities[partner].CultureId].Charts.Any(x=>x.Region==region)||
                World.Landmass[peerCell]>=0&&World.Landmass[peerCell]==World.Landmass[cell]&&LivingTravelKm(s,peerCell,cell,h.Polities[partner].CultureId)<=Math.Max(300,a.Polities[partner].ReachKm*3.0);
            if(!informed)return;
            int titleId=c.Titles.Length;
            c.Titles=c.Titles.Concat(new[]{new HistoricalRecognizedTitle{Id=titleId,Region=region,Holder=polity,Recognizer=partner,EstablishedYear=s.Years,LastTransferYear=s.Years,AreaKm2=c.RegionAreas[region],Basis="An established administrative foothold and an informed bilateral agreement covering the whole ecoregional district"}}).ToArray();
            c.TitleAt=c.TitleAt.With(region,titleId);c.Governments[polity].Titles.Add(titleId);p.Treasury-=1000;h.Polities[partner].Treasury=Math.Min(1000000000,h.Polities[partner].Treasury+1000);
            HistoryEvent(s,"territorial-recognition",h.Polities[partner].Name+" recognizes the regional title of "+p.Name+" across "+ColonialRegionName(region)+". The agreement covers the district, not just its administrative foothold; local peoples and actual control remain separate.",cell,p.CultureId,polity,partner,category:ChronicleCategory.Politics);
        }
        void ValidateTerritorialAmbitions(PrehistoryState s)
        {
            var aims=s.History.Aggregates.Empires.Connections.Ambitions;
            if(!RegionalAmbitionHistory(s)){if(aims!=null)throw new ArgumentException("Legacy territorial ambitions.");return;}
            if(aims?.Governments?.Length!=s.History.Polities.Length)throw new ArgumentException("Territorial ambition alignment.");
            foreach(var aim in aims.Governments)
                if(aim==null||aim.Scope<HistoricalAmbitionScope.Ecoregion||aim.Scope>HistoricalAmbitionScope.Continental||aim.AnchorCell<0||aim.AnchorCell>=CellPatch.Length||aim.Region!=CellPatch[aim.AnchorCell]||aim.Landmass!=World.Landmass[aim.AnchorCell]||aim.SinceYear<0||aim.SinceYear>s.Years)throw new ArgumentException("Invalid territorial ambition.");
        }
    }
}
