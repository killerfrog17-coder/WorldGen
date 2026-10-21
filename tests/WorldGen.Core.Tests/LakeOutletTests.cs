using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class LakeOutletTests
{
    public static void Inspect(string snapshot)
    {
        var world=WorldFile.Read(File.ReadAllBytes(snapshot));string hash=StateHash.Compute(world);var session=WorldLibrary.ReadPlaySession(snapshot);
        var watch=System.Diagnostics.Stopwatch.StartNew();var terrain=new LocalTerrain(world,horizontalScale:session.Settings.HorizontalScale);
        var origin=session.PlayerPosition;var patch=terrain.Patch(origin,24000);var here=patch.Sample(origin);
        var frame=new LocalTerrainFrame(origin,terrain.RadiusM,here.ElevationM,session.Settings.HorizontalScale);
        Console.WriteLine($"Saved seed {world.Recipe.Seed}, scale {session.Settings.HorizontalScale}, position {origin.X:R},{origin.Y:R},{origin.Z:R}; prep {watch.Elapsed.TotalSeconds:F3}s, {terrain.Amplification.ConditionedLakeSpills} conditioned spills");
        Console.WriteLine($"At player: ground {here.ElevationM:F3}, water {here.WaterSurfaceM:F3}, {here.WaterKind}/{here.WaterBodyKey}, cell {here.Cell}");
        var lakes=new HashSet<int>();
        for(int z=-4;z<=4;z++)
        {
            for(int x=-4;x<=4;x++)
            {
                var s=patch.Sample(frame.Position(x*500,z*500));
                Console.Write($"{s.WaterKind.ToString()[0]}{s.WaterBodyKey}:{s.ElevationM:F0}/{s.WaterSurfaceM:F0} ");
                if(s.WaterKind==LocalWaterKind.Lake&&s.WaterBodyKey>0)lakes.Add((int)s.WaterBodyKey-1);
            }
            Console.WriteLine();
        }
        foreach(int id in lakes)
        {
            var lake=world.Hydrology.Waters[id];
            Console.WriteLine($"Lake {id}: level {lake.SurfaceElevationM:R}, spill {lake.SpillM}, subgrid {lake.SubgridOutlet}, area {lake.AreaM2:F0}, endorheic {lake.Endorheic}, outlet {lake.OutletCell}, downstream {lake.DownstreamCell}");
            int wet=0;foreach(int cell in lake.Cells.OrderByDescending(i=>world.Hydrology.LakeCoverageFraction[i]).Take(8))
            {var p=world.Surface.Cells[cell].Position;var s=terrain.Patch(p,2000).Sample(p);if(s.Underwater&&s.WaterKind==LocalWaterKind.Lake)wet++;}
            Console.WriteLine($"  retained wet source controls: {wet}");if(wet==0)throw new Exception("Spill correction must not hide the lake");
            foreach(int cell in new[]{lake.OutletCell,lake.DownstreamCell}.Where(i=>i>=0))
            {
                var p=world.Surface.Cells[cell].Position;TerrainHandoff.Coordinates(frame,p,out double x,out double z);
                var s=terrain.Patch(p,2000).Sample(p);
                Console.WriteLine($"  control {cell} at {x:F1},{z:F1}: source {world.ElevationM[cell]:F2}, local {s.ElevationM:F2}/{s.WaterSurfaceM:F2}, {s.WaterKind}/{s.WaterBodyKey}");
            }
            if(lake.OutletCell>=0)
            {
                var p=world.Surface.Cells[lake.OutletCell].Position;var s=terrain.Patch(p,2000).Sample(p);
                if(Math.Abs(s.WaterSurfaceM-lake.SurfaceElevationM)>.001||!s.Underwater)throw new Exception("Lake outlet must be submerged at exact lake datum");
                var span=terrain.Patch(p,2000).RiverSpans().First(t=>t.From==lake.OutletCell);
                if(span.LevelA!=lake.SurfaceElevationM)throw new Exception("Outgoing river must share lake endpoint datum");
            }
        }
        foreach(var shape in new[]{(204.8,32),(819.2,32),(3276.8,16)})
        {
            watch.Restart();var mesh=LocalTerrainMesh.Build(terrain,frame,-shape.Item1/2,-shape.Item1/2,shape.Item1,shape.Item2);
            double min=double.PositiveInfinity,max=double.NegativeInfinity;
            for(int i=0;i<mesh.WaterPositions.Length/3;i++)if(mesh.WaterColors[i*4+3]<1.5){min=Math.Min(min,mesh.WaterPositions[i*3+1]);max=Math.Max(max,mesh.WaterPositions[i*3+1]);}
            Console.WriteLine($"Mesh {shape}: {mesh.WaterTriangles.Length/3} water faces, standing Y {min:F2}..{max:F2}, time {watch.Elapsed.TotalMilliseconds:F1}ms");
            if(world.Recipe.Seed==11610541&&here.Cell==94291&&max>1)
                throw new Exception("Owner regression: standing lake surface remains suspended above saved location");
        }
        if(StateHash.Compute(world)!=hash)throw new Exception("Spill correction changed saved world/history");
        Console.WriteLine("PASS saved outlet datum, submerged mouth, retained lake, no overhead sheet and unchanged source "+hash);
    }
}
