using WorldGen.Core;
using WorldGen.Serialization;
using WorldGen.Presentation;
using System.Text.Json;

static class GraniteDomeTests
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        var upland=new LocalTerrainProfile{Biome=Biome.TemperateForest,ElevationM=1000,CoarseReliefM=800,
            Ground=new GroundProfile{RockMixA=new V(.9,0,0)},Landscape=new LandscapeControls{Moisture=.8f}};
        Check(GraniteDomes.Admission(upland)>.9,"Exposed granitic uplands can host domes in a moist climate");
        var basalt=upland;basalt.Ground.RockMixA=new V(0,1,0);
        Check(GraniteDomes.Admission(basalt)==0,"Basalt is not mislabeled as a granite dome province");
        var plain=upland;plain.CoarseReliefM=60;Check(GraniteDomes.Admission(plain)==0,"Flat depositional plains do not acquire random granite monuments");
        var marsh=upland;marsh.Landscape.Wet=.8f;Check(GraniteDomes.Admission(marsh)==0,"Flooded wetland provinces exclude the uplifted dome form");
        var alluvium=upland;alluvium.Sediment=.6;Check(GraniteDomes.Admission(alluvium)==0,"Deep sedimentary lowlands exclude bare bedrock domes");
        var ice=upland;ice.Biome=Biome.Ice;Check(GraniteDomes.Admission(ice)==0,"Icefields do not expose an extra dome above the ice");
        double previous=double.PositiveInfinity,maxSlope=0;
        for(int i=0;i<=1000;i++)
        {
            double r=i/1000d,p=GraniteDomes.Profile(r);
            Check(double.IsFinite(p)&&p>=0&&p<=1&&p<=previous,"Dome falls continuously from a rounded summit to its finite foot");
            if(i>0)maxSlope=Math.Max(maxSlope,(previous-p)*1000);previous=p;
            double x=-1+2*r,a=GraniteDomes.JointFace(x,true),b=GraniteDomes.JointFace(x,false);
            Check(a>=0&&a<=1&&b==1,"Joint face changes only the admitted split-dome variant");
        }
        Check(GraniteDomes.Profile(1)==0&&GraniteDomes.Profile(1.1)==0&&GraniteDomes.Profile(.999999)<1e-8,"Dome support retires smoothly without a tile-edge shelf");
        Check(maxSlope<1.51,"An ordinary rounded dome has no artificial steep annular skirt");
        Check(GraniteDomes.JointFace(-.5,true)==0&&GraniteDomes.JointFace(-.3,true)==1,"A jointed face has a bounded steep transition");
        var rock=new GroundProfile{DepthM=.005,RockMixA=new V(1,0,0),RockColor=new V(.6,.6,.6),Sand=.6,Clay=.2,Silt=.2,Drainage=.5};
        foreach(double iceCover in new[]{0d,.3,.8,1})
        {
            var s=LocalGeology.Surface(rock,new V(1,0,0),42,1000,1,0,iceCover,637100,1);
            Check(Math.Abs(s.Rock-(1-iceCover))<1e-10&&Math.Abs(s.Ice-iceCover)<1e-10,"Explicit exposed bedrock stays rock even on a flat summit, while preserving snow");
            Check(Math.Abs(Enumerable.Range(0,8).Sum(s.Weight)-1)<1e-10,"Exposed rock material weights remain normalized");
        }
        Console.WriteLine($"PASS {checks} granite-dome geology, relief, wetland, finite support and joint constraints");
    }
    public static void World(string input,string output)
    {
        var world=WorldFile.ReadFile(input);string hash=StateHash.Compute(world);
        var clock=System.Diagnostics.Stopwatch.StartNew();
        var model=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);
        double prepareMs=clock.Elapsed.TotalMilliseconds,radius=model.RadiusM*.1;
        var forms=model.Amplification.Remnants.Forms.Where(f=>f.Dome).ToArray();
        if(forms.Length==0)throw new Exception("Actual world has no admitted granite dome province");
        int samples=0,exposed=0;var examples=new List<object>();
        foreach(var f in forms.OrderByDescending(f=>f.HeightM).Take(24))
        {
            if(GraniteDomes.Admission(model.Profile(f.Cell))<=.01)throw new Exception("Dome outside eligible granitic upland geology");
            var patch=model.Patch(f.Center,f.RadiusM*30);
            for(int z=-5;z<=5;z++)for(int x=-5;x<=5;x++)
            {
                V at=(f.Center+f.Axis*(x*f.RadiusX/5/radius)+f.Side*(z*f.RadiusZ/5/radius)).Unit;
                var s=patch.Sample(at);samples++;
                if(!double.IsFinite(s.ElevationM))throw new Exception("Dome produces nonfinite terrain");
                if(s.Province.RemnantExposure>.9){exposed++;if(s.Forest.Coverage>.02)throw new Exception("Mature forest grows on an exposed granite summit");}
            }
            var summit=patch.Sample(f.Center);
            examples.Add(new{f.Cell,f.HeightM,f.RadiusX,f.RadiusZ,f.Jointed,Position=new{f.Center.X,f.Center.Y,f.Center.Z},summit.ElevationM,summit.Province.RemnantExposure});
        }
        if(exposed==0)throw new Exception("Dome survey never reaches exposed rock");
        var best=forms.OrderByDescending(f=>f.HeightM).First();var s0=model.Patch(best.Center,10000).Sample(best.Center);
        var frame=new LocalTerrainFrame(best.Center,model.RadiusM,s0.ElevationM,.1);int triangles=0;
        foreach(var shape in new[]{(size:204.8,segments:32),(size:3276.8,segments:32)})
        {
            var mesh=LocalTerrainMesh.Build(model,frame,-shape.size/2,-shape.size/2,shape.size,shape.segments);
            if(!mesh.Positions.All(float.IsFinite))throw new Exception("Dome render mesh has nonfinite vertices");
            triangles+=mesh.Triangles.Length/3;
        }
        var scatter=new LocalScatter(model);var summitPatch=model.Patch(best.Center,30000);int scatterItems=0;
        foreach(bool detail in new[]{false,true})foreach(var item in scatter.Build(scatter.Address(best.Center,detail)).Instances)
        {
            scatterItems++;if(item.Kind is ScatterKind.Stone or ScatterKind.Boulder or ScatterKind.Outcrop or ScatterKind.Branch)continue;
            if(summitPatch.Sample(item.Position).Province.RemnantExposure>=.98)throw new Exception("Rooted scatter grows directly on a bare granite summit");
        }
        if(scatterItems==0)throw new Exception("Bare rock scatter test needs actual loose stone candidates");
        if(StateHash.Compute(world)!=hash)throw new Exception("Dome derivation changed the canonical world");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output,"survey.json"),JsonSerializer.Serialize(new{count=forms.Length,jointed=forms.Count(f=>f.Jointed),prepareMs,samples,exposed,triangles,scatterItems,examples},new JsonSerializerOptions{IncludeFields=true,WriteIndented=true}));
        File.WriteAllText(Path.Combine(output,"sites.json"),WorldJson.Write(new VegetationBenchmarkSiteSet{HistoryHash=world.Prehistory.Hash,Sites=new[]{new VegetationBenchmarkSite{Name="granite-dome",Position=best.Center,Biome="TemperateForest"}}}));
        Console.WriteLine($"PASS {forms.Length} actual domes, {samples} terrain samples, {exposed} exposed rock samples, {triangles} mesh triangles; canonical world unchanged");
    }
}
