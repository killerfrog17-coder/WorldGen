using WorldGen.Core;
using WorldGen.Presentation;
public static class HistoryTerritoryTests
{
    public static void Run(Action<bool,string> check)
    {
        check(Prehistory.IsAggregateHistoryVersion(Prehistory.CurrentHistoryVersion),"New histories retain local emergence and diffusion on the aggregate actor calendar");
        double grievance=HistoricalAutonomyEffects.Grievance(650,250,600,500,true);
        check(grievance>HistoricalAutonomyEffects.Grievance(50,800,0,750,false),"Imperial stress and contested transitions can support provincial grievances");
        check(HistoricalAutonomyEffects.Pressure(300,grievance,false)==240,"Cultural difference without an organized local base cannot build a revolt");
        check(HistoricalAutonomyEffects.Pressure(300,0,true)==240,"Restored support allows an autonomy movement to subside");
        int pressure=0;for(int i=0;i<20;i++)pressure=HistoricalAutonomyEffects.Pressure(pressure,grievance,true);
        check(pressure==1000,"Sustained organized grievances accumulate, with a bounded pressure index");
        check(!HistoricalAutonomyEffects.ViableBase(1,6000,100000)&&HistoricalAutonomyEffects.ViableBase(2,18000,100000),"A small isolated movement differs from a viable organized provincial coalition");
        check(HistoricalAutonomyEffects.ViableBase(1,25000,150000)&&!HistoricalAutonomyEffects.ViableBase(3,20000,1000000),"A major city can support separation, but tiny coalitions cannot continually split a large realm");
        double small=HistoricalTerritorialEffects.ReachKm(5000,500,0,false,false);
        double large=HistoricalTerritorialEffects.ReachKm(150000,500,500,true,true);
        check(small<100&&large>small*3,"Young urban cores do not receive empire-scale administration simply from rural claims");
        check(HistoricalTerritorialEffects.ReachKm(5000,800,0,false,false)>small,"Administrative ability helps maintain territory");
        check(HistoricalTerritorialEffects.BattleChance(100,100)==.5,"Balanced forces permit victories by either belligerent");
        check(HistoricalTerritorialEffects.BattleChance(80,100)>0&&HistoricalTerritorialEffects.BattleChance(200,100)>HistoricalTerritorialEffects.BattleChance(80,100),"Field uncertainty does not erase military strength");
        check(HistoricalTerritorialEffects.BattleChance(0,100)==0&&HistoricalTerritorialEffects.BattleChance(100,0)==1,"Zero fighting capacity cannot win a battle");
        check(HistoricalTerritorialEffects.BattleChance(1,1000)<.000001,"Overwhelming numerical mismatch does not receive a one-percent miracle floor");
        var fertile=new HabitatCell{WarmTenthsC=240,ColdTenthsC=100,GrowingDays=240,AridityMilli=700,WarmSeasonDryPermille=500,SoilFertilityPermille=650};
        double good=HistoricalCrops.AbioticPotential(fertile);fertile.ReliefM=1100;
        check(HistoricalCrops.AbioticPotential(fertile)==good*.5,"Shared abiotic crop baseline exposes the same slope factor for relative adaptation");
        check(HistoricalTerritorialEffects.WithdrawalChance(200,0,650)>HistoricalTerritorialEffects.WithdrawalChance(800,10000,0),"Institutional and fiscal stress increase unsupported frontier withdrawal");
        var s=new PrehistoryState{Started=true,Society=new SocietyState{CellCulture=new[]{0,1,1},Cultures=new[]{new CultureIdentity{Id=0,Stage=SocietyStage.Civilization},new CultureIdentity{Id=1,Stage=SocietyStage.Civilization}}},History=new HistoryState{CellPolity=new[]{0,0,1},CellStage=new[]{3,3,3},Polities=new[]{new HistoricalPolity{Id=0,CultureId=0},new HistoricalPolity{Id=1,CultureId=0}}}};
        check(SocietyView.OverlayId(s,1,HistoryOverlay.Civilizations)==0,"Conquered political territory takes its ruler's culture");
        check(SocietyView.OverlayId(s,1,HistoryOverlay.Cultures)==1,"Conquest does not recolor the local culture layer");
        check(SocietyView.OverlayId(s,1,HistoryOverlay.States)==0&&SocietyView.OverlayId(s,2,HistoryOverlay.States)==1,"State identity remains distinct from its tint");
        check(SocietyView.OverlayColorId(s,1,HistoryOverlay.States)==SocietyView.OverlayColorId(s,2,HistoryOverlay.States),"Separate courts of one ruling culture share a political color");
        var w=new World{Surface=new Surface{Cells=new[]{new Cell(),new Cell(),new Cell()},Corners=new[]{new V(1,0,0),new V(.99,.1,0).Unit},Edges=new[]{new Edge{A=0,B=1,TriangleA=0,TriangleB=1},new Edge{A=1,B=2,TriangleA=0,TriangleB=1}}},Hydrology=new HydrologyResult{OceanConnected=new[]{false,false,false}}};
        var borders=new HistoryBorders();var edges=borders.Segments(w,s);
        check(edges.Length==6,"Borders follow state ownership even between identically colored states, not culture cells");
        check(ReferenceEquals(edges,borders.Segments(w,s)),"Border endpoints cache by immutable checkpoint");
        var projected=borders.Mesh(w,s);check(projected.Lines.Length==projected.SphereLines.Length&&projected.LineColors.Length==projected.Lines.Length,"Political borders have matching map/globe endpoint buffers");
        check(borders.Segments(w,new PrehistoryState{History=new HistoryState()}).Length==0,"An uninitialized history cannot index cell owners");
    }
}
