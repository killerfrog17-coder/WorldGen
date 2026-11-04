using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class ErodedTerrainTests
{
    public static void World(string path,string output)
    {
        Directory.CreateDirectory(output);var world=WorldFile.ReadFile(path);string history=world.Prehistory.Hash;
        var watch=System.Diagnostics.Stopwatch.StartNew();var terrain=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);var rows=new List<object>();
        var cells=Enumerable.Range(0,world.Surface.Cells.Length).Where(i=>!world.Hydrology.OceanConnected[i]).ToArray();
        foreach(string kind in new[]{"Karst","Badlands"})
        {
            double Strength(LandscapeControls c)=>kind=="Karst"?c.Karst*(c.Moisture>.65?1:0):kind=="Badlands"?c.Badlands:c.Remnant;
            bool found=false;
            foreach(int cell in cells.Where(i=>kind!="Karst"||world.Ecology.Habitat[i].WarmTenthsC>180&&terrain.Profile(i).Landscape.Glacial<.25).OrderByDescending(i=>Strength(terrain.Profile(i).Landscape)).Take(96))
            {
                var p=terrain.Profile(cell);if(Strength(p.Landscape)<.25)continue;
                V center=world.Surface.Cells[cell].Position;var patch=terrain.Patch(center,30000);
                foreach(int x in new[]{-9000,-4500,0,4500,9000})
                {
                    V at=patch.Position(x,0),metres=at*(terrain.RadiusM*.1);
                    double expected=Math.Abs(ErodedTerrain.HeightOffset(metres,world.Recipe.Seed,p.Landscape));
                    if(expected<(kind=="Badlands"?8:35))continue;
                    var sample=patch.Sample(at);if(sample.Underwater||Strength(sample.Province)<.2)continue;
                    var frame=new LocalTerrainFrame(at,terrain.RadiusM,0,.1);
                    var left=LocalTerrainMesh.Build(terrain,frame,-128,-64,128,32);var right=LocalTerrainMesh.Build(terrain,frame,0,-64,128,32);
                    for(int z=0;z<=32;z++)if(left.Positions[(z*33+32)*3+1]!=right.Positions[z*33*3+1])throw new Exception("Eroded terrain shared edge differs");
                    if(left.Positions.Any(v=>!float.IsFinite(v))||right.Positions.Any(v=>!float.IsFinite(v)))throw new Exception("Nonfinite eroded terrain mesh");
                    rows.Add(new{Kind=kind,Cell=cell,Position=new[]{at.X,at.Y,at.Z},sample.ElevationM,Strength=Strength(sample.Province),ExpectedOffset=expected});found=true;break;
                }
                if(found)break;
            }
            if(!found)throw new Exception("Actual world has no accepted eroded context "+kind);
        }
        var remnants=terrain.Amplification.Remnants;if(remnants.Forms.Length==0)throw new Exception("No actual dry-bedrock outcrops");
        foreach(var form in remnants.Forms.Take(4))
        {
            var patch=terrain.Patch(form.Center,30000);var sample=patch.Sample(form.Center);
            if(sample.Province.RemnantExposure<.99||sample.Ground.DepthM>.05)throw new Exception("Outcrop does not expose actual bedrock");
            var frame=new LocalTerrainFrame(form.Center,terrain.RadiusM,0,.1);
            var left=LocalTerrainMesh.Build(terrain,frame,-64,-32,64,16);var right=LocalTerrainMesh.Build(terrain,frame,0,-32,64,16);
            for(int z=0;z<=16;z++)if(left.Positions[(z*17+16)*3+1]!=right.Positions[z*17*3+1])throw new Exception("Outcrop shared edge differs");
            rows.Add(new{Kind="Outcrop",form.Cell,Position=new[]{form.Center.X,form.Center.Y,form.Center.Z},form.HeightM,form.RadiusX,form.RadiusZ,sample.ElevationM});
        }
        if(world.Prehistory.Hash!=history)throw new Exception("Eroded terrain mutated regional history");
        File.WriteAllText(Path.Combine(output,"eroded-sites.json"),System.Text.Json.JsonSerializer.Serialize(rows,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS {rows.Count} actual eroded sites, finite meshes and shared tile edges; {remnants.Forms.Length} outcrops, {remnants.IndexedFaces} admitted faces; {watch.Elapsed.TotalSeconds:F2}s");
    }
    public static void Run()
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        var contexts=new[]{new LandscapeControls{Karst=1,Moisture=1},new LandscapeControls{Badlands=1,Arid=1},new LandscapeControls{Loess=1}};
        foreach(var controls in contexts)
        {
            double low=double.MaxValue,high=double.MinValue,slope=0;
            for(int x=-5000;x<5000;x+=37)for(int z=-5000;z<5000;z+=47)
            {
                V p=new V(x,637100,z);double h=ErodedTerrain.HeightOffset(p,73421,controls),next=ErodedTerrain.HeightOffset(p+new V(.1,0,0),73421,controls);
                Check(double.IsFinite(h)&&h> -90&&h<225,"Eroded terrain has bounded finite relief");
                Check(ErodedTerrain.HeightOffset(p,73421,default)==0,"Unrelated ordinary terrain stays exact");
                low=Math.Min(low,h);high=Math.Max(high,h);slope=Math.Max(slope,Math.Abs(next-h)/.1);
            }
            Check(high-low>(controls.Karst>0?100:controls.Remnant>0?50:controls.Badlands>0?40:15),"Supported geology produces landscape-scale relief, not metre noise");
            Console.WriteLine($"Eroded context karst={controls.Karst} badlands={controls.Badlands} loess={controls.Loess} remnant={controls.Remnant}; height {low:F2}..{high:F2}, slope {slope:F2}");
            Check(slope<(controls.Karst>0?10:controls.Loess>0?1.5:6),"Regional relief has bounded slopes appropriate to its landform");
        }
        var sandstone=new GroundProfile{RockMixA=new V(0,0,1),Sand=.6,DepthM=1,Drainage=.8};
        var outcrop=new BedrockRemnants.Outcrop{RadiusX=700,RadiusZ=300,HeightM=250};
        Check(outcrop.Profile(0)==1&&outcrop.Profile(.6)>.9,"A bedrock mass has a domed cap above a steep skirt, not another rolling hill");
        Check(outcrop.Profile(1)==0&&outcrop.Profile(.99999)<1e-9,"Outcrop returns continuously to unmodified ground");
        for(int i=0;i<=1000;i++)Check(double.IsFinite(outcrop.Profile(i/1000d))&&outcrop.Profile(i/1000d)>=0&&outcrop.Profile(i/1000d)<=1,"Finite bounded outcrop profile");
        Check(LandscapeProvince.Describe(sandstone,Biome.Desert,.1,0,120,0,0).Remnant>.5,"Resistant sedimentary desert bedrock can support isolated remnants");
        Check(LandscapeProvince.Describe(sandstone,Biome.Marsh,1,0,120,0,1).Remnant==0,"Wet marshes do not acquire desert monoliths");
        Console.WriteLine($"PASS {checks} eroded terrain checks");
    }
}
