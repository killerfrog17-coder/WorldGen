using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class WaterfallTests
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        const double radius=637100;const int seed=73421;int source=Enumerable.Range(0,1000).First(i=>WaterfallProfiles.CanHost(i,seed));
        var spans=new RiverscapeSpan[24];
        for(int i=0;i<spans.Length;i++)spans[i]=new RiverscapeSpan{A=new V(1,i*25/radius,0).Unit,B=new V(1,(i+1)*25/radius,0).Unit,
            SideA=new V(0,0,1),SideB=new V(0,0,1),LevelA=400-i*12,LevelB=400-(i+1)*12,WidthA=8,WidthB=8,Depth=3,DepthA=3,DepthB=3,Speed=3,Reach=source,From=i==0?source:-1,To=i==23?source+1:-1};
        var result=WaterfallProfiles.Apply(spans,radius,.85,source,seed);
        Check(result.Count(s=>s.Primitive==RiverFlowPrimitive.Waterfall)==1,"Supported bedrock reach receives one bounded knickpoint");
        Check(result.Length==spans.Length+2,"A waterfall adds only two cross-sections, not a new water simulation");
        Check(ReferenceEquals(WaterfallProfiles.Apply(spans,radius,.1,source,seed),spans),"Weak sediment channels keep their original profiles");
        Check(result[0].LevelA==spans[0].LevelA&&result[result.Length-1].LevelB==spans[spans.Length-1].LevelB,"Shared graph endpoint levels remain exact");
        for(int i=0;i<result.Length;i++)
        {
            var s=result[i];Check(s.LevelA>=s.LevelB&&double.IsFinite(s.Speed),"Water always descends with finite speed");
            if(i>0)
            {
                Check((s.A-result[i-1].B).Length<1e-12&&s.LevelA==result[i-1].LevelB,"Every replaced cross-section joins without a water seam");
                Check(Math.Abs(s.DepthA-result[i-1].DepthB)<1e-8,"Plunge-pool bed rejoins continuously");
            }
            for(int j=0;j<=10;j++)
            {
                double t=j/10d,y=s.A.Y*(1-t)+s.B.Y*t,old=400-y*radius/25*12;
                Check(s.LevelA*(1-t)+s.LevelB*t<=old+.01,"Replacement never raises water above the old bank-fit profile");
            }
        }
        Check(spans[8].LevelA==304&&spans[8].DepthA==3,"Replacement does not mutate the original cached profile");
        var fine=new RiverscapeSpan[128];
        for(int i=0;i<fine.Length;i++){fine[i]=spans[3];fine[i].A=new V(1,i*3/radius,0).Unit;fine[i].B=new V(1,(i+1)*3/radius,0).Unit;fine[i].LevelA=200-i*.6;fine[i].LevelB=200-(i+1)*.6;}
        Check(WaterfallProfiles.Apply(fine,radius,.7,source,seed).Any(s=>s.Primitive==RiverFlowPrimitive.Waterfall),"Metre-scale refined cascade stations can support a cumulative knickpoint");
        int hosts=Enumerable.Range(0,10000).Count(i=>WaterfallProfiles.CanHost(i,seed));Check(hosts>250&&hosts<650,"Only a small fraction of otherwise eligible reaches host falls");
        Console.WriteLine($"PASS {checks} waterfall constraints; {hosts}/10000 preliminary host keys");
    }
    public static void World(string path,string output)
    {
        Directory.CreateDirectory(output);var world=WorldFile.ReadFile(path);string history=world.Prehistory.Hash;
        var watch=System.Diagnostics.Stopwatch.StartNew();var terrain=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);var rows=new List<object>();int probed=0;
        foreach(var channel in terrain.Hydrology.Channels.Where(c=>WaterfallProfiles.CanHost(c.From,world.Recipe.Seed)&&c.WidthM>4&&c.WidthM<600&&(c.StartM-c.EndM)/Math.Max(1,c.LengthM)>.02).GroupBy(c=>c.From).Select(g=>g.First()).OrderBy(c=>Rng.Mix(unchecked((uint)c.From^0x45bca1u))).Take(128))
        {
            probed++;var spans=terrain.Amplification.RiverReach(channel.From);
            foreach(var fall in spans.Where(s=>s.Primitive==RiverFlowPrimitive.Waterfall))
            {
                V center=(fall.A+fall.B).Unit;var patch=terrain.Patch(center,30000);var sample=patch.Sample(center);
                if(!sample.Underwater||sample.WaterKind!=LocalWaterKind.River)throw new Exception("Actual waterfall has no paired wet bed");
                var frame=new LocalTerrainFrame(center,terrain.RadiusM,0,.1);
                var mesh=LocalTerrainMesh.Build(terrain,frame,-128,-128,256,16);
                if(mesh.WaterTriangles.Length==0||mesh.WaterPositions.Any(v=>!float.IsFinite(v)))throw new Exception("Actual waterfall has no finite rendered water mesh");
                var tagged=Enumerable.Range(0,mesh.WaterPositions.Length/3).Where(i=>mesh.WaterColors[i*4+3]>2.05f).ToArray();
                if(tagged.Length<4||tagged.Any(i=>Math.Abs(mesh.WaterColors[i*4+2])>1.0001f||mesh.WaterColors[i*4+3]>2.2001f))throw new Exception("Falling sheet lost its bounded lateral/descent attributes");
                if(tagged.Min(i=>mesh.WaterColors[i*4+2])>=0||tagged.Max(i=>mesh.WaterColors[i*4+2])<=0||tagged.Max(i=>mesh.WaterColors[i*4+3])-tagged.Min(i=>mesh.WaterColors[i*4+3])<.09f)throw new Exception("Clipped waterfall does not retain both sides and its full descent");
                rows.Add(new{fall.Reach,Position=new[]{center.X,center.Y,center.Z},Drop=fall.LevelA-fall.LevelB,Run=(fall.B-fall.A).Length*frame.RadiusM,Width=fall.WidthA*2,fall.LevelA,fall.LevelB,Triangles=mesh.WaterTriangles.Length/3});
                if(rows.Count>=4)break;
            }
            if(rows.Count>=4)break;
        }
        if(rows.Count==0)throw new Exception("No actual admitted bedrock waterfalls found after "+probed+" probes");
        if(world.Prehistory.Hash!=history)throw new Exception("Waterfall queries changed history");
        File.WriteAllText(Path.Combine(output,"falls.json"),System.Text.Json.JsonSerializer.Serialize(rows,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS {rows.Count} actual waterfalls / {probed} bounded probes with paired wet beds and rendered meshes; {watch.Elapsed.TotalSeconds:F2}s");
    }
}
