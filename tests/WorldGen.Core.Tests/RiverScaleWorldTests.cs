using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;
using System.Text.Json;

static class RiverScaleWorldTests
{
    public static void Run(string path,string output)
    {
        Directory.CreateDirectory(output);var world=WorldFile.ReadFile(path);var terrain=new LocalTerrain(world,horizontalScale:.1);Console.WriteLine("Junction datum fitting "+terrain.Amplification.RiverscapeFittingMilliseconds.ToString("F1")+"ms");var h=world.Hydrology;var rows=new List<object>();var sites=new List<object>();int probes=0,bad=0;
        double Q(int i)=>(h.NorthernSpringDischargeM3PerS[i]+h.NorthernSummerDischargeM3PerS[i]+h.NorthernAutumnDischargeM3PerS[i]+h.NorthernWinterDischargeM3PerS[i])*.25;
        var selected=new List<V>();
        foreach(int id in Enumerable.Range(0,world.Surface.Cells.Length).Where(i=>!h.OceanConnected[i]&&h.Receiver[i]>=0).OrderByDescending(Q).Take(250))
        {
            var profile=terrain.Amplification.RiverReach(id);var possible=profile.Where(s=>s.LevelA>5&&s.LevelB>1&&Math.Min(s.WidthA,s.WidthB)>250).OrderBy(s=>Math.Abs(s.LevelA-s.LevelB)).ToArray();
            foreach(var span in possible)
            {
                var at=(span.A+span.B).Unit;if(selected.Any(p=>(at-p).Length*terrain.RadiusM*.1<10000))continue;
                var patch=terrain.Patch(at,40000);var sample=patch.Sample(at);if(!sample.Underwater||sample.WaterKind!=LocalWaterKind.River||sample.WaterSurfaceM-sample.ElevationM<10)continue;
                TerrainMapViewport.Degrees(at,out double lat,out double lon);Console.WriteLine($"MAJOR {id}: {lat:R},{lon:R}, Q={Q(id):F0}, width={span.WidthA+span.WidthB:F0}m, depth={sample.WaterSurfaceM-sample.ElevationM:F2}m, speed={sample.WaterSpeedMPerS:F2}m/s");
                sites.Add(new{id,lat,lon,discharge=Q(id),width=span.WidthA+span.WidthB,depth=sample.WaterSurfaceM-sample.ElevationM,speed=sample.WaterSpeedMPerS});selected.Add(at);
                var frame=new LocalTerrainFrame(at,terrain.RadiusM,0,.1);
                foreach(var shape in new[]{(size:204.8,n:64),(size:819.2,n:32),(size:3276.8,n:16)})
                {
                    double east=-shape.size/2+shape.size/shape.n*.37,north=-shape.size/2+shape.size/shape.n*.27;var mesh=LocalTerrainMesh.Build(terrain,frame,east,north,shape.size,shape.n);int failures=0;double error=0;
                    for(int z=0;z<13;z++)for(int x=0;x<13;x++)
                    {
                        double ax=(x-6)*Math.Min(shape.size/15,span.WidthA/7),az=(z-6)*Math.Min(shape.size/15,span.WidthA/7);var point=frame.Position(ax,az);var actual=patch.Sample(point);if(!actual.Underwater||actual.WaterKind!=LocalWaterKind.River)continue;
                        probes++;double ground=RiverRepairTests.Height(mesh.Positions,mesh.Triangles,ax-east,az-north),water=RiverRepairTests.Height(mesh.WaterPositions,mesh.WaterTriangles,ax-east,az-north),expected=frame.SurfaceY(point,actual.WaterSurfaceM);
                        double delta=Math.Abs(water-expected);error=Math.Max(error,delta);if(!double.IsFinite(water)||water<ground-.005||delta>Math.Max(.03,Math.Pow(shape.size/shape.n,2)/frame.RadiusM*.5))failures++;
                    }
                    bad+=failures;rows.Add(new{id,shape.size,shape.n,failures,error,vertices=mesh.Positions.Length/3,waterTriangles=mesh.WaterTriangles.Length/3,ms=mesh.BuildMilliseconds});Console.WriteLine($"  {shape.size}m/{shape.n}: failures={failures}, level error={error:F5}m, triangles={mesh.WaterTriangles.Length/3}, build={mesh.BuildMilliseconds:F1}ms");
                    RiverscapeTests.Boundary(mesh,(ok,why)=>{if(!ok)throw new Exception("Major river "+id+": "+why);},frame,patch);
                }
                break;
            }
            if(selected.Count>=3)break;
        }
        var options=new JsonSerializerOptions{WriteIndented=true,NumberHandling=System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals};
        File.WriteAllText(Path.Combine(output,"sites.json"),JsonSerializer.Serialize(sites,options));File.WriteAllText(Path.Combine(output,"meshes.json"),JsonSerializer.Serialize(rows,options));
        if(selected.Count<3)throw new Exception("Fewer than three actual deep major-river sites found");if(bad>0)throw new Exception(bad+" major-river rendering probes failed");
        Console.WriteLine("PASS "+probes+" major-river probes, three deep river sites; active reaches "+terrain.Amplification.RiverscapeReachCount);
    }
}
