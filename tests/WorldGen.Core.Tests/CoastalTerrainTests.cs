using WorldGen.Core;
using WorldGen.Serialization;
using WorldGen.Presentation;

static class CoastalTerrainTests
{
    public static void Controls()
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        double minimum=double.MaxValue,maximum=double.MinValue,variation=0;
        foreach(double relief in new[]{0d,250})foreach(double sediment in new[]{0d,1})
        for(int i=0;i<1000;i++)
        {
            var p=new V(27000,i*7,42000);double h=-220+i*.44;
            double shaped=CoastalTerrain.Shape(p,731,h,.5,relief,sediment,.7);
            Check(double.IsFinite(shaped)&&Math.Abs(shaped-h)<150,"Coasts have finite bounded local relief");
            Check(CoastalTerrain.Shape(p,731,h,0,relief,sediment,.7)==h,"Unconnected inland terrain remains exact");
            if(Math.Abs(h)>=180)Check(shaped==h,"Deep sea and upland ground remain exact");
            double edge=CoastalTerrain.Shape(p,731,h,1e-8,relief,sediment,.7);
            Check(Math.Abs(edge-h)<1e-8,"Coastal influence tends continuously to zero at land-only boundaries");
            double next=CoastalTerrain.Shape(p+new V(0,.01,0),731,h+.001,.5,relief,sediment,.7);
            Check(Math.Abs(next-shaped)<.2,"Shore profile has no discontinuous shelves or tile jumps");
            minimum=Math.Min(minimum,shaped-h);maximum=Math.Max(maximum,shaped-h);variation+=Math.Abs(shaped-h);
        }
        Check(variation>1000&&maximum>5&&minimum< -5,"Actual shoreline deformation produces coves and headlands");
        var basalt=new GroundProfile{RockMixA=new V(0,1,0)};var limestone=new GroundProfile{RockMixB=new V(1,0,0)};
        Check(CoastalTerrain.ShoreMud(new GroundProfile{Clay=.5,Drainage=.1,Organic=.12})>.9,"Saturated clay/organic coasts retain muddy shores");
        Check(CoastalTerrain.ShoreMud(new GroundProfile{Clay=.03,Drainage=.9,Sand=.95})==0,"Well drained sandy coasts do not acquire a mud coating");
        var sandy=new GroundProfile{Sand=.7,Clay=.05,Gravel=.03,Drainage=.9,Organic=.04};
        Check(CoastalTerrain.BeachBand(sandy,1,.3)>.99&&CoastalTerrain.BeachBand(sandy,8,.3)==0,"Sea-spray beach band stops before inland vegetation");
        Check(CoastalTerrain.BeachBand(sandy,1,0)==0,"An inland lake does not acquire an ocean beach mask");
        Check(CoastalTerrain.ShoreGround(sandy,1,.3).Organic<.001&&CoastalTerrain.ShoreGround(sandy,1,.3).Sand>.93,"Ordinary sandy foreshore carries mineral sand rather than grassland soil");
        Check(CoastalTerrain.Deposition(new V(1000,2000,3000),73421,80,.5,.7)>.5,"Ordinary lowland amplitudes admit depositional beaches");
        Check(CoastalTerrain.SandColour(basalt).X<.3&&CoastalTerrain.SandColour(limestone).X>.8,"Basaltic and carbonate sands have distinct geology-derived colour");
        var mix=new GroundProfile{RockMixA=new V(0,.5,0),RockMixB=new V(.5,0,0)};
        Check((CoastalTerrain.SandColour(mix)-(CoastalTerrain.SandColour(basalt)+CoastalTerrain.SandColour(limestone))*.5).Length<1e-8,"Beach colour blends across lithology boundaries");
        double previous=double.NegativeInfinity;
        for(int j=0;j<=12000;j++)
        {
            double h=-40+j*.025,value=CoastalTerrain.PlatformProfile(h);
            Check(value>=previous,"Wave-cut profile remains monotone, with no closed tide basin or inverted shore");previous=value;
            if(h<=-20||h>=180)Check(Math.Abs(value-h)<1e-10,"Platform does not move deep seabed or upper coast");
        }
        Check(CoastalTerrain.PlatformProfile(0)==0,"Platform profile retains the ocean datum");
        Check(CoastalTerrain.PlatformProfile(80)<1.7&&CoastalTerrain.PlatformProfile(180)-CoastalTerrain.PlatformProfile(80)>170,"Broad shallow platform leads to a steep coastal scarp");
        Check(CoastalTerrain.BeachBand(sandy,1,.3,1)==0,"Explicit wave-cut rock is not painted as a sandy beach");
        var bare=LocalGeology.Surface(sandy,new V(1,0,0),731,1,1,.3,0,6371000,1);
        Check(bare.Rock>.99&&bare.Sand<.01,"Explicit bare shore rock overrides generic ocean sand suppression");
        var shore=new LandscapeControls{ShoreExposure=1};
        Check(LandscapeProvince.RootedAdmission(shore,new V(1000,2000,3000),731)==0,"Bare wave-cut platforms reject rooted vegetation");
        var planted=new GroundProfile{DepthM=1,Organic=.2};
        var eroded=LandscapeProvince.Ground(planted,shore,new V(1000,2000,3000),731);
        Check(eroded.DepthM<.01&&eroded.Organic<.001,"Exposed shore has consistent physical substrate");
        var faded=LandscapeControls.Blend(default,shore,.5);
        Check(faded.ShoreExposure==.5&&faded.ExposedRock==.5,"Shore exposure is blended, not selected by tile ownership");
        Console.WriteLine($"PASS {checks} coastal controls; height changes {minimum:F2}..{maximum:F2}m");
    }

    public static void World(string path,string output)
    {
        Directory.CreateDirectory(output);var world=WorldFile.ReadFile(path);string history=world.Prehistory.Hash;
        var watch=System.Diagnostics.Stopwatch.StartNew();var terrain=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);var rows=new List<object>();
        var coast=Enumerable.Range(0,world.Surface.Cells.Length).Where(i=>!world.Hydrology.OceanConnected[i]&&world.Surface.Cells[i].Neighbors.Any(n=>world.Hydrology.OceanConnected[n]));
        foreach(int cell in coast.OrderBy(i=>Rng.Mix(unchecked((uint)i^(uint)world.Recipe.Seed))).Take(24))
        {
            int ocean=world.Surface.Cells[cell].Neighbors.First(n=>world.Hydrology.OceanConnected[n]);
            V land=world.Surface.Cells[cell].Position,sea=world.Surface.Cells[ocean].Position;
            var patch=terrain.Patch((land+sea).Unit,(land-sea).Length*terrain.RadiusM+1000);
            V bank=land;int wet=0,dry=0;
            for(int j=0;j<=100;j++)
            {
                V p=(land*(1-j/100d)+sea*(j/100d)).Unit;var sample=patch.Sample(p);
                if(!double.IsFinite(sample.ElevationM))throw new Exception("Nonfinite actual coast");
                if(sample.Underwater){wet++;if(sample.WaterKind==LocalWaterKind.Ocean&&sample.WaterSurfaceM!=0)throw new Exception("Coast changed sea datum");}
                else{dry++;bank=p;}
            }
            if(wet==0||dry==0)continue;
            var frame=new LocalTerrainFrame(bank,terrain.RadiusM,0,.1);
            var left=LocalTerrainMesh.Build(terrain,frame,-64,-32,64,16);var right=LocalTerrainMesh.Build(terrain,frame,0,-32,64,16);
            for(int z=0;z<=16;z++)if(left.Positions[(z*17+16)*3+1]!=right.Positions[z*17*3+1])throw new Exception("Shared coastal mesh edge differs");
            rows.Add(new{Cell=cell,Position=new[]{bank.X,bank.Y,bank.Z},WetSamples=wet,DrySamples=dry});
        }
        if(rows.Count<8)throw new Exception("Too few actual connected shores were checked");
        if(world.Prehistory.Hash!=history)throw new Exception("Coasts changed regional history");
        File.WriteAllText(Path.Combine(output,"coasts.json"),System.Text.Json.JsonSerializer.Serialize(rows,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS {rows.Count} actual coast transects, sea datum and shared tile edges; {watch.Elapsed.TotalSeconds:F2}s");
    }
    public static void PlatformWorld(string path,string output)
    {
        Directory.CreateDirectory(output);var world=WorldFile.ReadFile(path);string history=world.Prehistory.Hash;
        var timer=System.Diagnostics.Stopwatch.StartNew();var terrain=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);
        var rows=new List<object>();int candidates=0,queries=0,exposed=0;double maximum=0;
        var coast=Enumerable.Range(0,world.Surface.Cells.Length).Where(i=>!world.Hydrology.OceanConnected[i]&&world.Surface.Cells[i].Neighbors.Any(n=>world.Hydrology.OceanConnected[n]));
        foreach(int cell in coast.OrderByDescending(i=>terrain.Profile(i).CoarseReliefM).Take(80))
        {
            candidates++;int ocean=world.Surface.Cells[cell].Neighbors.First(n=>world.Hydrology.OceanConnected[n]);
            V land=world.Surface.Cells[cell].Position,sea=world.Surface.Cells[ocean].Position;
            var patch=terrain.Patch((land+sea).Unit,(land-sea).Length*terrain.RadiusM+2000);
            V low=land,high=sea;
            if(patch.Sample(low).Underwater)continue;
            for(int j=0;j<20;j++){V mid=(low+high).Unit;if(patch.Sample(mid).ElevationM>0)low=mid;else high=mid;queries++;}
            V direction=V.Tangent(low,sea).Unit;bool accepted=false;
            foreach(double offset in new[]{4d,12,28,55,110,220})
            {
                V p=(low-direction*(offset/(terrain.RadiusM*.1))).Unit;var s=patch.Sample(p);queries++;
                maximum=Math.Max(maximum,s.Province.ShoreExposure);
                if(s.Underwater||s.WaterKind!=LocalWaterKind.Ocean||s.Province.ShoreExposure<.5||s.ElevationM>20)continue;
                exposed++;if(Math.Abs(s.WaterSurfaceM)>1e-9||Math.Abs(s.WaterMaskM+s.ElevationM)>.003)throw new Exception("Platform lost shared ocean datum/mask");
                if(Math.Abs(patch.Elevation(p)-s.ElevationM)>.002)throw new Exception("Height-only platform query differs");
                var rock=LocalGeology.Surface(s.Ground,p,terrain.SourceSeed,s.ElevationM,1,s.OceanInfluence,0,terrain.RadiusM*.1,s.Province.ExposedRock);
                if(rock.Rock<s.Province.ShoreExposure-.01)throw new Exception("Ocean sand heuristic erased exposed wave platform");
                if(s.Province.ShoreExposure>.95&&(s.Forest.Coverage>.03||LandscapeProvince.RootedAdmission(s.Province,p*(terrain.RadiusM*.1),terrain.SourceSeed)>.01))throw new Exception("Bare wave platform retains rooted trees");
                if(!accepted)
                {
                    var frame=new LocalTerrainFrame(p,terrain.RadiusM,0,.1);
                    var left=LocalTerrainMesh.Build(terrain,frame,-32,-16,32,8);var right=LocalTerrainMesh.Build(terrain,frame,0,-16,32,8);
                    for(int z=0;z<=8;z++)if(left.Positions[(z*9+8)*3+1]!=right.Positions[z*9*3+1])throw new Exception("Wave platform shared mesh edge differs");
                    rows.Add(new{Cell=cell,Position=new{s.ElevationM,p.X,p.Y,p.Z},Exposure=s.Province.ShoreExposure,Rock=rock.Rock,s.Forest.Coverage});accepted=true;
                }
            }
            if(rows.Count>=8)break;
        }
        File.WriteAllText(Path.Combine(output,"shore-platforms.json"),System.Text.Json.JsonSerializer.Serialize(new{Candidates=candidates,Queries=queries,MaximumExposure=maximum,Exposed=exposed,Sites=rows},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        if(rows.Count<2)throw new Exception("Too few actual exposed shore platforms: "+rows.Count+" / max "+maximum);
        if(world.Prehistory.Hash!=history)throw new Exception("Wave platforms changed regional history");
        Console.WriteLine($"PASS {rows.Count} actual wave-cut shores, {exposed} exposed samples/{queries} queries, exact edges/datum/history; max exposure {maximum:F3}; {timer.Elapsed.TotalSeconds:F2}s");
    }
}
