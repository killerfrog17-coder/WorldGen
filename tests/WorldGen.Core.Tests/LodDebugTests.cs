using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class LodDebugTests
{
    public static void Run(string path)
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        var parent=new TerrainTileKey(-1,-2,10,.1);var child=new TerrainTileKey(-1024,-2048,0,.1);
        var rng=new Rng(41,"coverage-check");
        foreach(var resident in new[]{Array.Empty<TerrainTileKey>(),new[]{child},new[]{child,new TerrainTileKey(-2,-4,9,.1)},new[]{new TerrainTileKey(-2,-4,9,.1),new TerrainTileKey(-1,-4,9,.1),new TerrainTileKey(-2,-3,9,.1),new TerrainTileKey(-1,-3,9,.1)}})
        {
            var tree=TerrainCoverage.Build(parent,resident);
            for(int i=0;i<3000;i++)
            {
                double u=rng.Unit(),v=rng.Unit(),x=(parent.X+u)*parent.SizeM,z=(parent.Z+v)*parent.SizeM;
                bool covered=resident.Any(k=>x>=k.X*k.SizeM&&x<(k.X+1)*k.SizeM&&z>=k.Z*k.SizeM&&z<(k.Z+1)*k.SizeM);
                Check(TerrainCoverage.Visible(tree,u,v)==!covered,"Exactly one owner; unready child leaves fallback visible");
            }
        }
        var tiny=TerrainCoverage.Build(parent,new[]{child});Check(!TerrainCoverage.Visible(tiny,.5/1024,.5/1024)&&TerrainCoverage.Visible(tiny,2d/1024,.5/1024),"Sparse deep fine tile removes only its own area");
        Check(tiny.Length<200,"Sparse masks do not allocate full fine-resolution rasters");
        foreach(double lon in new[]{-179d,0d,140d})foreach(double lat in new[]{-80d,0d,80d})
        {
            var p=TerrainMapViewport.FromDegrees(lat,lon);var f=new LocalTerrainFrame(p,6371000,0,.1);
            foreach(int h in new[]{0,45,90,180,270}){double delta=Math.Abs(ExplorerNavigation.Bearing(f,p,h)-h);Check(Math.Min(delta,360-delta)<.00001,"Geographic compass bearing at chart origin");}
        }
        Check(ExplorerNavigation.Compass(359)=="N"&&ExplorerNavigation.Compass(90)=="E"&&ExplorerNavigation.Compass(-90)=="W","Compass wrapping");
        Check(ExplorerNavigation.Coordinates(new[]{"/tp","20","30","100"},70,out var target,out var height)&&height==100,"Coordinate command parses");
        TerrainMapViewport.Degrees(target,out double latitude,out double longitude);Check(Math.Abs(latitude-20)<.00001&&Math.Abs(longitude-100)<.00001,"Coordinates use official meridian");
        foreach(string bad in new[]{"NaN","Infinity","91","-91"})Check(!ExplorerNavigation.Coordinates(new[]{"/tp",bad,"0"},0,out _,out _),"Bad coordinates rejected without teleport");
        Check(!ExplorerNavigation.Coordinates(new[]{"/tp","0","0","-1"},0,out _,out _),"Bad altitude rejected");
        Check(PlayCommands.Help.SequenceEqual(PlayCommands.Help.OrderBy(x=>x,StringComparer.Ordinal)),"Help remains alphabetical");
        Check(PlayCommands.Complete("/tp r").SequenceEqual(new[]{"/tp random","/tp random land"}),"Teleport completion");
        double mean=0;for(int i=0;i<1000;i++){var p=ExplorerNavigation.RandomPoint(ref rng);Check(Math.Abs(p.Length-1)<1e-8,"Random destination is on sphere");mean+=p.Z;}
        Check(Math.Abs(mean/1000)<.08,"Random navigation is not biased to poles");
        Check(WaterwaysMapImage.MinimumHalfWidth(10000)>WaterwaysMapImage.MinimumHalfWidth(10),"Zoom admits smaller streams");
        if(path!=null)
        {
            var world=WorldFile.Read(File.ReadAllBytes(path));string hash=StateHash.Compute(world);var terrain=new LocalTerrain(world,horizontalScale:.1);
            var global=WaterwaysMapImage.Build(world,terrain,new TerrainMapViewport(),512,256);
            Check(global.Rgba.Where((_,i)=>i%4==3).Any(a=>a>0),"Actual world has visible waterway estimates");
            Check(global.Rgba.Where((_,i)=>i%4==3).Any(a=>a==235),"Whole-world overlay retains major river symbols at runtime resolution");
            Console.WriteLine("Whole-world overlay occupied pixels: "+global.Rgba.Where((_,i)=>i%4==3).Count(a=>a>0)+" / "+(global.Width*global.Height));
            Check(StateHash.Compute(world)==hash,"Debug overlay preserves source world/history");
            using var stop=new CancellationTokenSource();stop.Cancel();bool cancelled=false;
            try{WaterwaysMapImage.Build(world,terrain,new TerrainMapViewport(),token:stop.Token);}catch(OperationCanceledException){cancelled=true;}
            Check(cancelled,"Map work is cancellable");
        }
        Console.WriteLine("PASS "+checks+" coverage, navigation and map checks");
    }
}
