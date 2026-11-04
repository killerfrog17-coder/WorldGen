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
            Check(double.IsFinite(shaped)&&Math.Abs(shaped-h)<90,"Coasts have finite bounded local relief");
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
        Check(CoastalTerrain.SandColour(basalt).X<.3&&CoastalTerrain.SandColour(limestone).X>.8,"Basaltic and carbonate sands have distinct geology-derived colour");
        var mix=new GroundProfile{RockMixA=new V(0,.5,0),RockMixB=new V(.5,0,0)};
        Check((CoastalTerrain.SandColour(mix)-(CoastalTerrain.SandColour(basalt)+CoastalTerrain.SandColour(limestone))*.5).Length<1e-8,"Beach colour blends across lithology boundaries");
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
}
