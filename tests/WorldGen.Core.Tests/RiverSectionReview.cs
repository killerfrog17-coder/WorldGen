using System.Text.Json;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

// Offline diagnostic of the actual sampled channel at prepared cross-sections.
// Used to distinguish a physical discontinuity from a render/LOD artefact.
static class RiverSectionReview
{
    public static void Run(string path,int source,string output,bool verify=false)
    {
        var world=WorldFile.ReadPlanetaryFile(path);
        var model=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);
        var spans=model.Amplification.RiverReach(source);
        if(spans.Length==0)throw new Exception("No active reach "+source);
        double radius=model.RadiusM*.1;
        var stations=new List<object>();var discontinuities=new List<object>();
        double maximumGroundJump=0,maximumWaterJump=0;
        for(int i=0;i<spans.Length;i++)
        {
            var s=spans[i];double length=(s.B-s.A).Length*radius;
            stations.Add(new{Index=i,Length=length,s.LevelA,s.LevelB,s.WidthA,s.WidthB,s.DepthA,s.DepthB,s.Alluvium,s.BankBendA,s.BankBendB,Primitive=s.Primitive.ToString()});
            if(i==0||s.Primitive==RiverFlowPrimitive.Waterfall||spans[i-1].Primitive==RiverFlowPrimitive.Waterfall)continue;
            V direction=(s.B-spans[i-1].A).Unit;
            var patch=model.Patch(s.A,3000);
            foreach(double q in new[]{-1.3,-1,-.8,-.4,0,.4,.8,1,1.3})
            {
                V at=(s.A+s.SideA*(q*s.WidthA/radius)).Unit;
                var a=patch.Sample((at-direction*(.05/radius)).Unit);
                var b=patch.Sample((at+direction*(.05/radius)).Unit);
                double groundJump=Math.Abs(a.ElevationM-b.ElevationM);
                bool comparable=a.Underwater&&b.Underwater&&a.WaterKind==LocalWaterKind.River&&b.WaterKind==LocalWaterKind.River;
                double waterJump=comparable?Math.Abs(a.WaterSurfaceM-b.WaterSurfaceM):0;
                maximumGroundJump=Math.Max(maximumGroundJump,groundJump);maximumWaterJump=Math.Max(maximumWaterJump,waterJump);
                if(groundJump>.1||waterJump>.01)discontinuities.Add(new{Index=i,Q=q,GroundJump=groundJump,WaterJump=waterJump,Before=new{a.ElevationM,a.WaterSurfaceM,Kind=a.WaterKind.ToString()},After=new{b.ElevationM,b.WaterSurfaceM,Kind=b.WaterKind.ToString()}});
            }
        }
        File.WriteAllText(output,JsonSerializer.Serialize(new{Source=source,MaximumGroundJump=maximumGroundJump,MaximumWaterJump=maximumWaterJump,Stations=stations,Discontinuities=discontinuities},new JsonSerializerOptions{WriteIndented=true}));
        if(verify&&(maximumGroundJump>.1||maximumWaterJump>.01))throw new Exception($"Lowland section continuity failed: ground {maximumGroundJump:F4}m, water {maximumWaterJump:F4}m across 0.1m");
        Console.WriteLine($"Reach {source}: {spans.Length} sections, max 10cm along-stream ground difference {maximumGroundJump:F4}m / water {maximumWaterJump:F4}m; {discontinuities.Count} diagnostic rows");
    }
}
