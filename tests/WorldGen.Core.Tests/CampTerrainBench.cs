using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class CampTerrainBench
{
    public static void CheckBackdropPolicy()
    {
        int checks=0;
        foreach(double scale in new[]{.01,.1,1d,2d})foreach(double range in new[]{1000d,2000,10000,60000,250000})
        {
            int maximum=Math.Max(0,(int)Math.Floor(Math.Log(128/scale,2))),level=TerrainAdaptiveLod.BackdropLevel(scale,range);
            double size=TerrainLod.BaseSizeM*scale*(1<<level);
            if(level>maximum||level<0||size<range*2&&level<maximum)throw new Exception("Backdrop does not cover its view range within the established maximum.");checks++;
            var options=new TerrainDetailOptions{ViewDistanceM=range};var plan=TerrainAdaptiveLod.Plan(-37,23,1.7,scale,options,(_,__)=>0,800,68);
            foreach(var key in plan.Keys.Where(k=>k.Level<=level))
            {
                var parent=new TerrainTileKey(key.X>>(level-key.Level),key.Z>>(level-key.Level),level,scale);
                if(TerrainLod.DistanceSquared(parent,-37,23)>range*range)throw new Exception("Visible adaptive tile lost its backdrop ancestor.");checks++;
            }
        }
        if(TerrainAdaptiveLod.BackdropLevel(.1,1000)!=5||TerrainAdaptiveLod.BackdropLevel(.1,250000)!=10)throw new Exception("Local backdrop scales down without shortening the maximum horizon.");
        Console.WriteLine("PASS "+(checks+1)+" view-bounded backdrop coverage checks");
    }
    public static void Backdrops(string path,string output)
    {
        Directory.CreateDirectory(output);var world=WorldFile.ReadFile(path);var results=new List<object>();
        foreach(int level in new[]{10,5})
        {
            var terrain=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);var at=TerrainMapViewport.FromDegrees(-2,34);
            var frame=new LocalTerrainFrame(at,terrain.RadiusM,terrain.Patch(at).Sample(at).ElevationM,.1);double size=TerrainLod.BaseSizeM*.1*(1<<level),total=0;
            for(int z=-1;z<=0;z++)for(int x=-1;x<=0;x++)
            {
                var mesh=LocalTerrainMesh.Build(terrain,frame,x*size,z*size,size,8,edgeSteps:Enumerable.Repeat(size/8,4).ToArray());total+=mesh.BuildMilliseconds;
                Console.WriteLine($"Backdrop L{level} {x},{z}: {mesh.BuildMilliseconds:F1}ms, {mesh.Triangles.Length/3} ground, {mesh.WaterTriangles.Length/3} water triangles");
            }
            results.Add(new{level,size,totalSeconds=total/1000});File.WriteAllText(Path.Combine(output,"backdrops.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
        }
    }
    public static void Run(string path,string output)
    {
        Directory.CreateDirectory(output);var world=WorldFile.ReadFile(path);
        var watch=Stopwatch.StartNew();var terrain=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);double preparation=watch.Elapsed.TotalSeconds;
        var at=TerrainMapViewport.FromDegrees(-2,34);var local=terrain.Patch(at,20480);var sample=local.Sample(at);
        var frame=new LocalTerrainFrame(at,terrain.RadiusM,sample.ElevationM,.1);
        var options=new TerrainDetailOptions{ViewDistanceM=1000};var estimator=new TerrainDetailEstimator(terrain,frame);
        var plan=TerrainAdaptiveLod.Plan(0,0,1.7,.1,options,estimator.Error,800,68);
        var rows=new List<object>();var samples=new TerrainSampleCache();
        double profiles=0,meshing=0;int index=0;
        foreach(var pair in plan.OrderBy(p=>TerrainLod.DistanceSquared(p.Key,0,0)))
        {
            var key=pair.Key;double step=key.SizeM/pair.Value,east=key.X*key.SizeM,north=key.Z*key.SizeM;
            var patch=terrain.Patch(frame.Position(east+key.SizeM/2,north+key.SizeM/2),(key.SizeM*1.1+step*2)/.1);
            watch.Restart();var spans=patch.RiverSpans(step>32?step*.1:0);double profileMs=watch.Elapsed.TotalMilliseconds;
            var mesh=LocalTerrainMesh.Build(terrain,frame,east,north,key.SizeM,pair.Value,edgeSteps:TerrainLod.EdgeSteps(key,plan),sampleCache:samples);
            profiles+=profileMs;meshing+=mesh.BuildMilliseconds;
            rows.Add(new{index=++index,key.X,key.Z,key.Level,size=key.SizeM,n=pair.Value,spans=spans.Length,profileMs,buildMs=mesh.BuildMilliseconds,vertices=mesh.Positions.Length/3,groundTriangles=mesh.Triangles.Length/3,waterTriangles=mesh.WaterTriangles.Length/3});
            File.WriteAllText(Path.Combine(output,"tiles.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture,$"{index}/{plan.Count}: ({key.X},{key.Z}) L{key.Level}, {pair.Value} samples, profiles {profileMs:F1}ms/{spans.Length} spans, mesh {mesh.BuildMilliseconds:F1}ms, {mesh.Triangles.Length/3}+{mesh.WaterTriangles.Length/3} triangles"));
        }
        File.WriteAllText(Path.Combine(output,"summary.json"),JsonSerializer.Serialize(new{preparationSeconds=preparation,tiles=plan.Count,profileSeconds=profiles/1000,meshSeconds=meshing/1000},new JsonSerializerOptions{WriteIndented=true}));
    }
}
