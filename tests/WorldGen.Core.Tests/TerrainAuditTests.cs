using WorldGen.Core;
using WorldGen.Serialization;

static class TerrainAuditTests
{
    public static void Run(string path)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));string before=StateHash.Compute(world);int checks=0;
        void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        foreach(double scale in new[]{.1,1d})
        {
            var terrain=new LocalTerrain(world,horizontalScale:scale);int wet=0;
            for(int i=0;i<80;i++)
            {
                var point=world.Surface.Cells[i*7919%world.Surface.Cells.Length].Position;var patch=terrain.Patch(point,2048/scale);
                for(int z=-2;z<=2;z++)for(int x=-2;x<=2;x++)
                {
                    var at=patch.Position(x*7,z*7);
                    // Query the fast path FIRST: a complete sample must not
                    // quietly warm required water/terrain state on its behalf.
                    double height=patch.Elevation(at);double potential=patch.ForestPotential(at);var sample=patch.Sample(at);
                    Check(height.Equals(sample.ElevationM),"Canonical height-only query changes final height");if(sample.Underwater)wet++;
                    Check(potential.Equals(sample.Forest.Coverage),"Forest-only admission filter changes raw profile coverage");
                }
            }
            Console.WriteLine($"scale{scale}:2000 exact final-height comparisons, {wet} wet probes");
        }
        var recipe=Recipe.Preset("Preview");recipe.Detail=5000;var surface=SphericalMesh.Build(recipe,default);int n=surface.Cells.Length;
        var legacy=new World{Recipe=recipe,Surface=surface,ElevationM=Enumerable.Repeat(250,n).ToArray(),Hydrology=new HydrologyResult{OceanConnected=new bool[n]}};
        var fallback=new LocalTerrain(legacy);Check(fallback.Amplification==null,"Legacy fixture must exercise non-amplified path");
        var p=fallback.Patch(new V(1,0,0));for(int i=-20;i<=20;i++){var at=p.Position(i,2*i);Check(p.Elevation(at)==p.Sample(at).ElevationM,"Literal legacy height fallback");}
        bool invalid=false;try{p.Elevation(new V(2,0,0));}catch(ArgumentOutOfRangeException){invalid=true;}Check(invalid,"Height-only validation retained");
        Check(StateHash.Compute(world)==before,"Performance query changed source world/history");
        Console.WriteLine($"PASS {checks} terrain performance equivalences; source {before}");
    }
}
