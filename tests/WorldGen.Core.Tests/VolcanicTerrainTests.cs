using WorldGen.Core;
using WorldGen.Serialization;
using WorldGen.Presentation;

static class VolcanicTerrainTests
{
    public static void Controls()
    {
        int count=0;void Check(bool ok,string why){count++;if(!ok)throw new Exception(why);}
        foreach(double crater in new[]{.09,.25})
        {
            var form=new VolcanicTerrain.Volcano{RadiusM=4000,HeightM=600,BaseM=300,CraterFraction=crater,CraterDepth=crater<.1?.34:.67};
            Check(form.Target(0,0)<form.Target(crater*.85,0),"Crater floor is below its surrounding rim");
            double maxSlope=0,prior=form.Target(0,0);
            for(int i=1;i<=2000;i++)
            {
                double height=form.Target(i/2000d,0);Check(double.IsFinite(height),"Finite volcanic surface");
                maxSlope=Math.Max(maxSlope,Math.Abs(height-prior)/(form.RadiusM/2000));prior=height;
            }
            Check(maxSlope<1.5,"Crater/flank profile has bounded slopes without needle spikes");
            Check(Math.Abs(form.Target(1,0)-form.BaseM)<1e-8,"Edifice returns to its foot datum");
        }
        var controls=new LandscapeControls{BareRock=1};
        var soil=LandscapeProvince.Ground(new GroundProfile{DepthM=1,Organic=.2,RockMixA=new V(1,0,0)},controls,new V(1,0,0),731);
        Check(soil.DepthM<.01&&soil.Organic==0&&soil.RockWeight(1)>.99,"Fresh volcanic parent is exposed basalt with negligible soil");
        Check(LandscapeProvince.RootedAdmission(controls,new V(1,0,0),731)==0,"Fresh volcanic rock excludes established rooted vegetation");
        Console.WriteLine($"PASS {count} volcanic shape/material controls");
    }
    public static void World(string path,string output)
    {
        Directory.CreateDirectory(output);var world=WorldFile.ReadFile(path);string hash=world.Prehistory.Hash;
        var watch=System.Diagnostics.Stopwatch.StartNew();var terrain=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);
        var forms=terrain.Amplification.Volcanoes;var rows=new List<object>();
        if(forms.Forms.Length==0||forms.IndexedFaces>=world.Surface.Triangles.Length/3)throw new Exception("Volcanic context must exist without indexing every ordinary terrain face");
        foreach(var form in forms.Forms.Take(12))
        {
            var patch=terrain.Patch(form.Center,Math.Max(20000,form.RadiusM*20));
            var center=patch.Sample(form.Center);var rim=(form.Center+form.Axis*(form.RadiusM*form.CraterFraction/(terrain.RadiusM*.1))).Unit;
            var edge=patch.Sample(rim);
            if(!double.IsFinite(center.ElevationM)||!double.IsFinite(edge.ElevationM))throw new Exception("Nonfinite actual volcano terrain");
            var frame=new WorldGen.Presentation.LocalTerrainFrame(rim,terrain.RadiusM,0,.1);
            var left=WorldGen.Presentation.LocalTerrainMesh.Build(terrain,frame,-64,-32,64,16);
            var right=WorldGen.Presentation.LocalTerrainMesh.Build(terrain,frame,0,-32,64,16);
            for(int z=0;z<=16;z++)if(left.Positions[(z*17+16)*3+1]!=right.Positions[z*17*3+1])throw new Exception("Volcanic terrain differs at shared tile edge");
            rows.Add(new{form.FeatureId,form.Form,form.AgeMyr,Center=new[]{form.Center.X,form.Center.Y,form.Center.Z},form.RadiusM,form.HeightM,CenterHeight=center.ElevationM,RimHeight=edge.ElevationM,Exposure=center.Province.BareRock});
        }
        if(world.Prehistory.Hash!=hash)throw new Exception("Local landforms changed regional history");
        File.WriteAllText(Path.Combine(output,"volcanoes.json"),System.Text.Json.JsonSerializer.Serialize(rows,
            new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS actual volcanic sites {rows.Count}; {forms.Forms.Length} forms, {forms.IndexedFaces}/{world.Surface.Triangles.Length/3} indexed faces; prepare+meshes {watch.Elapsed.TotalSeconds:F2}s");
    }
}
