using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;
public static class MapMarksTests
{
    public static void Run(Action<bool,string> check)
    {
        V P(double x,double y)=>new V(Math.Cos(y)*Math.Cos(x),Math.Cos(y)*Math.Sin(x),Math.Sin(y));
        var world=new World{Recipe=new Recipe(),Surface=new Surface{Cells=new[]{new Cell{Position=P(3.11,.2)},new Cell{Position=P(-3.13,.21)},new Cell{Position=P(-3.09,.25)},new Cell{Position=P(-3.07,.27)}}},ElevationM=new[]{200,100,50,0},Hydrology=new HydrologyResult{Rivers=new[]{new RiverReach{From=0,To=1,Order=1,WidthM=20,DischargePotentialM3PerS=5},new RiverReach{From=1,To=2,Order=2,WidthM=60,DischargePotentialM3PerS=10},new RiverReach{From=2,To=3,Order=3,WidthM=150,DischargePotentialM3PerS=30}}}};
        string original=WorldJson.Write(world.Hydrology);var river=RiverMesh.Chunks(world).Single();
        check(river.Stroke.Length==river.CellIds.Length*4,"Every river vertex has continuous bank/cap/width/order metadata.");
        check(river.CellIds.Length==3*26&&river.Triangles.Length==3*60,"Indexed curved reaches and capsule caps have a bounded twenty-six vertices each.");
        check(river.Positions.All(float.IsFinite)&&river.Stroke.All(float.IsFinite),"All river geometry and metadata are finite.");
        check(river.Triangles.All(i=>i>=0&&i<river.CellIds.Length),"All river triangle indices are valid.");
        check(Enumerable.Range(0,river.CellIds.Length).Any(i=>river.Stroke[i*4+1]==1),"Rounded caps carry distance-field longitudinal coordinates.");
        var repeated=RiverMesh.Chunks(world).Single();check(river.Positions.SequenceEqual(repeated.Positions)&&river.Stroke.SequenceEqual(repeated.Stroke),"Curve and stroke construction is repeatable without random meanders.");
        var map=MapView.Project(river,0,-.003,true);
        check(map.Stroke.Length==map.CellIds.Length*4&&map.Stroke.All(float.IsFinite),"Seam clipping interpolates all four stroke attributes.");
        check(map.SpherePositions.Length==map.Positions.Length,"Dual map/globe river geometry stays aligned.");
        check(map.Positions.Where((_,i)=>i%3==0).All(x=>x>=-2.00001&&x<=2.00001),"Rivers crossing the dateline stay within the wrapped map.");
        check(WorldJson.Write(world.Hydrology)==original,"Visual smoothing leaves the complete hydrology data unchanged.");
        world.Hydrology.OceanConnected=new[]{false,false,false,true};var mouth=RiverMesh.Chunks(world).Single();int last=2*26+16;
        V Bank(int vertex)=>new V(mouth.Positions[vertex*3],-mouth.Positions[vertex*3+2],mouth.Positions[vertex*3+1]).Unit;
        check(Math.Abs(V.Dot((Bank(last)+Bank(last+1)).Unit,world.Surface.Cells[2].Position-world.Surface.Cells[3].Position))<.000001,"River mouths terminate at the shoreline bisector rather than floating into an ocean cell's center.");
        check(RiverMesh.VisibilityLimit(1)<RiverMesh.VisibilityLimit(3)&&RiverMesh.VisibilityLimit(3)<RiverMesh.VisibilityLimit(5),"Small channels require a closer zoom than main trunks.");
        var canceled=new CancellationToken(true);bool interrupted=false;try{RiverMesh.Chunks(world,canceled).ToArray();}catch(OperationCanceledException){interrupted=true;}check(interrupted,"River preparation remains cancellable.");
        var s=new PrehistoryState{Society=new SocietyState{Settlements=new[]{new Settlement{Id=0,PolityId=0,Population=1500},new Settlement{Id=1,PolityId=0,Population=100000},new Settlement{Id=2,PolityId=0,Population=200},new Settlement{Id=3,PolityId=0,Population=3000},new Settlement{Id=4,PolityId=0,Population=200000}}},History=new HistoryState{Polities=new[]{new HistoricalPolity{Id=0,CapitalId=0}},Aggregates=new HistoricalAggregateState{Polities=new[]{new HistoricalPolityAggregate()},Living=new HistoricalLivingHistory{Cities=Enumerable.Range(0,5).Select(i=>new HistoricalLivingCity{AbandonedYear=i==4?20:-1}).ToArray()}}}};
        string before=WorldJson.Write(s);var marks=SettlementMarkers.Build(s);
        check(marks[0].Length==0,"Off hides every settlement marker.");
        check(marks[1].Length==1&&marks[1][0].Id==0,"Capitals only retains a small but actual political capital.");
        check(marks[2].Length==4&&marks[2].All(t=>t.Id!=4),"All includes small live settlements, never abandoned cities.");
        check(marks[3].Length==3&&marks[3].All(t=>t.Id!=2),"Cities excludes small villages without excluding a small capital.");
        check(marks[4].Length==1&&marks[4][0].Id==1,"Large cities selects a relative global standout, not every state capital.");
        check(WorldJson.Write(s)==before,"Marker filters do not change settlement classifications or simulation state.");
        var mode=SettlementMarkerMode.Off;foreach(var expected in new[]{SettlementMarkerMode.CapitalsOnly,SettlementMarkerMode.All,SettlementMarkerMode.CitiesOnly,SettlementMarkerMode.LargeCitiesOnly,SettlementMarkerMode.Off}){mode=SettlementMarkers.Next(mode);check(mode==expected,"C cycles marker modes in the requested order.");}
    }
}
