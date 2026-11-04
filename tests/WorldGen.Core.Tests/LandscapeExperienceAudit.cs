using System.Diagnostics;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class LandscapeExperienceAudit
{
    public static void Run(string worldPath,string manifestPath,string output)
    {
        Directory.CreateDirectory(output);
        var world=WorldFile.ReadFile(worldPath);var timer=Stopwatch.StartNew();
        var terrain=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);
        Console.WriteLine("Actual terrain prepared "+timer.Elapsed.TotalSeconds.ToString("F2")+"s");
        using var manifest=JsonDocument.Parse(File.ReadAllText(manifestPath));
        foreach(var entry in manifest.RootElement.GetProperty("cases").EnumerateArray())
        {
            var scene=entry.GetProperty("scene");string kind=scene.GetProperty("Requested").GetString();
            if(kind!="SandSea"&&kind!="SaltFlat")continue;
            var p=scene.GetProperty("Position");var at=new V(p.GetProperty("X").GetDouble(),p.GetProperty("Y").GetDouble(),p.GetProperty("Z").GetDouble());
            var patch=terrain.Patch(at,18000);var frame=new LocalTerrainFrame(at,terrain.RadiusM,0,.1);
            var heights=new List<double>();var offsets=new List<double>();var slopes=new List<double>();var surfaceWeights=new List<double>();
            var csv=new List<string>{"x,z,height,local_y,province_offset,salt_weight"};
            for(int z=-512;z<=512;z+=16)for(int x=-512;x<=512;x+=16)
            {
                var position=frame.Position(x,z);var sample=patch.Sample(position);
                double local=frame.SurfaceY(position,sample.ElevationM),offset=LandscapeProvince.HeightOffset(position*frame.RadiusM,world.Recipe.Seed,sample.Province);
                double east=patch.Sample(frame.Position(x+1,z)).ElevationM,north=patch.Sample(frame.Position(x,z+1)).ElevationM;
                double up=1/Math.Sqrt(1+Math.Pow(east-sample.ElevationM,2)+Math.Pow(north-sample.ElevationM,2));
                var ground=LocalGeology.Surface(sample.Ground,position,world.Recipe.Seed,sample.ElevationM,up,sample.OceanInfluence,0,frame.RadiusM);
                heights.Add(sample.ElevationM);offsets.Add(offset);slopes.Add(Math.Acos(up)*180/Math.PI);surfaceWeights.Add(ground.Salt);
                csv.Add(FormattableString.Invariant($"{x},{z},{sample.ElevationM},{local},{offset},{ground.Salt}"));
            }
            File.WriteAllLines(Path.Combine(output,kind+"-surface.csv"),csv);
            var meshes=new List<object>();
            foreach(int segments in new[]{16,32,64,128})
            {
                var mesh=LocalTerrainMesh.Build(terrain,frame,-256,-256,512,segments);
                meshes.Add(new{Segments=segments,Spacing=512d/segments,Minimum=mesh.MinHeightM,Maximum=mesh.MaxHeightM});
            }
            var estimator=new TerrainDetailEstimator(terrain,frame);var lodTimer=Stopwatch.StartNew();
            var plan=TerrainAdaptiveLod.Plan(0,0,1.8,.1,new TerrainDetailOptions{ViewDistanceM=2000},estimator.Error,864,68);
            double coldLodMs=lodTimer.Elapsed.TotalMilliseconds;lodTimer.Restart();
            TerrainAdaptiveLod.Plan(0,0,1.8,.1,new TerrainDetailOptions{ViewDistanceM=2000},estimator.Error,864,68);
            var lod=new{Tiles=plan.Count,Vertices=plan.Sum(p=>(long)(p.Value+1)*(p.Value+1)),Metadata=estimator.Count,ColdMs=coldLodMs,WarmMs=lodTimer.Elapsed.TotalMilliseconds};
            var row=new{Kind=kind,Point=new{at.X,at.Y,at.Z},Grid65x65HeightRange=heights.Max()-heights.Min(),ProvinceOffsetRange=offsets.Max()-offsets.Min(),
                MaximumSlopeDegrees=slopes.Max(),MeanSaltWeight=surfaceWeights.Average(),Meshes=meshes,Lod=lod};
            string json=JsonSerializer.Serialize(row,new JsonSerializerOptions{WriteIndented=true,IncludeFields=true});
            File.WriteAllText(Path.Combine(output,kind+"-audit.json"),json);Console.WriteLine(json);
        }
    }
}
