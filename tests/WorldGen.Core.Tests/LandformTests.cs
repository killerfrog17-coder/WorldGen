using System.Diagnostics;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class LandformTests
{
    public static void Run(Action<bool,string> check)
    {
        check(OrogenMorphology.Retention(.5,.5)<1,"No legacy sixfold uplift");
        check(OrogenMorphology.Uplift(20000,6500,1)<6500&&Math.Abs(OrogenMorphology.Uplift(80000,6500,4)-OrogenMorphology.Uplift(20000,6500,1)*4)<1e-6,"Physical baseline saturates, but the owner's mountain percentage is still linear");
        check(OrogenMorphology.Retention(.95,.6)>OrogenMorphology.Retention(.15,.6)*3,"Strong variation between belts");
        check(OrogenMorphology.CollisionWidth(1)<300000,"Narrow collision front");
        check(OrogenMorphology.PlateauEnvelope(0,1000000)==0&&OrogenMorphology.PlateauEnvelope(300000,1000000)>.9&&OrogenMorphology.PlateauEnvelope(1000000,1000000)==0,"Broad asymmetric plateau behind the front");
        var recipe=Recipe.Preset("Preview");recipe.Detail=5000;
        var mesh=SphericalMesh.Build(recipe,default);int n=mesh.Cells.Length;
        var h=new HydrologyResult{OceanConnected=new bool[n],Receiver=Enumerable.Repeat(-1,n).ToArray(),
            WaterBody=Enumerable.Repeat(-1,n).ToArray(),LakeCoverageFraction=new double[n],LakeWaterVolumeM3=new double[n],
            NorthernSpringDischargeM3PerS=new double[n],NorthernSummerDischargeM3PerS=new double[n],
            NorthernAutumnDischargeM3PerS=new double[n],NorthernWinterDischargeM3PerS=new double[n]};
        var world=new World{Recipe=recipe,Surface=mesh,ElevationM=Enumerable.Repeat(250,n).ToArray(),Hydrology=h};
        int a=221,b=mesh.Cells[a].Neighbors[0],c=mesh.Cells[b].Neighbors.First(i=>i!=a&&!mesh.Cells[a].Neighbors.Contains(i));
        world.ElevationM[a]=300;world.ElevationM[b]=240;world.ElevationM[c]=180;
        h.Receiver[a]=b;h.Receiver[b]=c;h.VisitOrder=new[]{c,b,a};
        foreach(int i in new[]{a,b,c})h.NorthernSpringDischargeM3PerS[i]=h.NorthernSummerDischargeM3PerS[i]=h.NorthernAutumnDischargeM3PerS[i]=h.NorthernWinterDischargeM3PerS[i]=100;
        var terrain=new LocalTerrain(world,horizontalScale:.1);var hydrology=terrain.Hydrology;
        check(hydrology.Channels.Length==2,"Streams derive from saved runoff and receiver edges below the global river threshold");
        foreach(var channel in hydrology.Channels)
        {
            check(channel.StartM>=channel.EndM&&channel.WidthM>0,"Downhill finite-width channels");
            check(D.Angle(channel.Position(0),channel.A)<1e-7&&D.Angle(channel.Position(1),channel.B)<1e-7,"Exact connected endpoints");
            var patch=terrain.Patch(channel.Position(.5),240000);var another=terrain.Patch(channel.Position(.55),240000);
            for(int i=0;i<=40;i++)
            {
                V p=channel.Position(i/40.0);var sample=patch.Sample(p);var other=another.Sample(p);
                check(sample.Underwater&&Math.Abs(sample.WaterSurfaceM-channel.Level(i/40.0))<.1,"Continuous water over a carved river bed");
                check(sample.ElevationM==other.ElevationM&&sample.WaterSurfaceM==other.WaterSurfaceM&&sample.WaterMaskM==other.WaterMaskM,"Exactly equal independently indexed water/terrain patches");
            }
        }
        var river=hydrology.Channels[0];V riverPoint=river.Position(.5);var frame=new LocalTerrainFrame(riverPoint,terrain.RadiusM,river.Level(.5),.1);
        var tile=LocalTerrainMesh.Build(terrain,frame,-1024,-1024,2048,128);
        check(tile.WaterTriangles.Length>0&&tile.WaterTriangles.All(i=>i>=0&&i<tile.WaterPositions.Length/3),"Dedicated clipped river mesh indices");
        check(tile.WaterPositions.All(float.IsFinite),"Finite inland water geometry");
        check(tile.Positions.Length/3<tile.GroundVertices*3,"Channel-aware geometry remains bounded rather than subdividing the whole tile");
        h.NorthernSpringDischargeM3PerS[a]=h.NorthernSummerDischargeM3PerS[a]=h.NorthernAutumnDischargeM3PerS[a]=h.NorthernWinterDischargeM3PerS[a]=.08;
        var thinTerrain=new LocalTerrain(world,horizontalScale:.1);var thin=thinTerrain.Hydrology.Channels.First(c=>c.From==a);
        var thinFrame=new LocalTerrainFrame(thin.Position(.5),thinTerrain.RadiusM,thin.Level(.5),.1);
        var thinMesh=LocalTerrainMesh.Build(thinTerrain,thinFrame,-1024,-1024,2048,128);
        double waterArea=0;
        for(int k=0;k<thinMesh.WaterTriangles.Length;k+=3)
        {int i=thinMesh.WaterTriangles[k]*3,j=thinMesh.WaterTriangles[k+1]*3,l=thinMesh.WaterTriangles[k+2]*3;var p=thinMesh.WaterPositions;waterArea+=Math.Abs((p[j]-p[i])*(p[l+2]-p[i+2])-(p[j+2]-p[i+2])*(p[l]-p[i]))*.5;}
        check(thin.WidthM<3&&thinMesh.WaterTriangles.Length>0&&waterArea>1000&&waterArea<20000,"A two-metre stream remains visible without becoming a sixteen-metre grid channel");
        Console.WriteLine($"Thin stream {thin.WidthM:F2}m, clipped water {waterArea:F0}m2, added land vertices {thinMesh.Positions.Length/3-thinMesh.GroundVertices-512}");
        int lake=1200;double area=mesh.Cells[lake].AreaM2*.2;
        h.WaterBody[lake]=0;h.LakeCoverageFraction[lake]=.2;h.LakeWaterVolumeM3[lake]=area*8;
        h.Waters=new[]{new WaterBody{Id=0,Kind=WaterKind.Lake,Cells=new[]{lake},AreaM2=(long)area,VolumeM3=area*8,SurfaceElevationM=120}};
        var lakeTerrain=new LocalTerrain(world,horizontalScale:.1);var feature=lakeTerrain.Hydrology.Shores.Single();
        var lakePatch=lakeTerrain.Patch(feature.Center,Math.Min(240000,feature.RadiusM*1.4));
        check(lakePatch.Sample(feature.Center).Underwater&&lakePatch.Sample(feature.Center).WaterSurfaceM==120,"Fractional lake bed and common saved surface");
        foreach(double angle in new[]{0.0,.7,1.8,3.1,5.0})
        {
            V edge=feature.Boundary(angle,terrain.RadiusM);
            var inside=(feature.Center+(edge-feature.Center)*.98).Unit;var outside=(feature.Center+(edge-feature.Center)*1.02).Unit;
            check(lakePatch.Sample(inside).Underwater&&!lakePatch.Sample(outside).Underwater,"Continuous irregular shores independent of cell edges");
        }
        var lakeFrame=new LocalTerrainFrame(feature.Center,terrain.RadiusM,120,.1);
        var lakeMesh=LocalTerrainMesh.Build(lakeTerrain,lakeFrame,-1024,-1024,2048,64);
        check(lakeMesh.WaterTriangles.Length>0&&lakeMesh.WaterPositions.Where((_,i)=>i%3==1).All(y=>y<.1&&y>-20),"Lake water shares one curved physical surface");
    }

    public static void Review(string path,string folder,Action<bool,string> check)
    {
        Directory.CreateDirectory(folder);var lines=new List<string>();
        var world=WorldFile.Read(File.ReadAllBytes(path));string before=StateHash.Compute(world);
        var watch=Stopwatch.StartNew();var terrain=new LocalTerrain(world,horizontalScale:.1);
        lines.Add($"{Path.GetFileName(path)} {world.Surface.Cells.Length}: setup {watch.Elapsed.TotalMilliseconds:F0}ms, channels {terrain.Hydrology.Channels.Length}, shores {terrain.Hydrology.Shores.Length}");
        var land=Enumerable.Range(0,world.ElevationM.Length).Where(i=>!world.Hydrology.OceanConnected[i]).Select(i=>world.ElevationM[i]).OrderBy(v=>v).ToArray();
        lines.Add($"  Land heights: median {land[land.Length/2]}, p90 {land[(int)(land.Length*.9)]}, p99 {land[(int)(land.Length*.99)]}, max {land.Last()}; above4000m {100.0*land.Count(v=>v>4000)/land.Length:F3}%");
        check(before==StateHash.Compute(world),"Local landforms/water preserve the source world and history");
        check(terrain.Hydrology.Channels.All(c=>c.StartM>=c.EndM),"All actual-world channel profiles are non-increasing");
        var sites=new[]{(27.98,86.92),(32.0,88.0),(-32.65,-70.01),(38.5,-79.4),(30.1,-98.4),(38.07,-122.96)};
        foreach(var site in sites)
        {
            var patch=terrain.Patch(TerrainMapViewport.FromDegrees(site.Item1,site.Item2),40000);var values=new List<double>();var center=patch.Sample(0,0);
            for(int z=-10;z<=10;z++)for(int x=-10;x<=10;x++)values.Add(patch.Sample(x*1000,z*1000).ElevationM);
            lines.Add($"  {site}: elevation {center.ElevationM:F0}, range {values.Max()-values.Min():F0}, mountain amplitude {center.MountainHeightM:F0}, plateau {center.Plateau:F2}");
        }
        var durations=new List<double>();
        foreach(var channel in terrain.Hydrology.Channels.OrderByDescending(c=>c.WidthM).Where((_,i)=>i%100==0).Take(8))
        {
            V p=channel.Position(.5);var patch=terrain.Patch(p,20000);var other=terrain.Patch(channel.Position(.51),20000);var sample=patch.Sample(p);
            check(sample.Underwater,"Actual-world river centers have water over beds");
            check(sample.ElevationM==other.Sample(p).ElevationM,"Actual-world water patches agree exactly");
            var frame=new LocalTerrainFrame(p,terrain.RadiusM,sample.ElevationM,.1);
            var tile=LocalTerrainMesh.Build(terrain,frame,-1024,-1024,2048,128);durations.Add(tile.BuildMilliseconds);
            check(tile.WaterTriangles.Length>0&&tile.Positions.All(float.IsFinite),"Actual-world river meshes are complete and finite");
        }
        lines.Add($"128-segment river tiles: mean {durations.Average():F1}ms worst {durations.Max():F1}ms");
        File.WriteAllLines(Path.Combine(folder,Path.GetFileNameWithoutExtension(path)+"-numerical.txt"),lines);Console.WriteLine(string.Join("\n",lines));
    }
}
