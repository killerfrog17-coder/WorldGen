using System.Diagnostics;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;
static class ForagerMapCacheTests
{
    public static void Run(string path)
    {
        var world=WorldFile.ReadFile(path);var original=world.Prehistory;var terrain=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);int checks=0;
        void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        var view=new TerrainMapViewport{Longitude=0,Latitude=0,LongitudeSpan=360};
        byte[] Finish(ProgressiveTerrainMap map,int revision)
        {
            var pixels=new byte[32*16*4];var watch=Stopwatch.StartNew();int count=0;
            while(watch.Elapsed.TotalSeconds<120)
            {
                if(!map.TryTake(out var update)){Thread.Sleep(1);continue;}
                if(update.Revision!=revision)continue;
                if(update.Error.Length>0)throw new Exception(update.Error);
                if(update.Complete){Check(count==32*16,"The final dated map covers every requested pixel");return pixels;}
                if(update.Preview||update.CanvasWidth!=32||update.Image==null)continue;
                var image=update.Image;
                for(int row=0;row<image.Height;row++)Array.Copy(image.Rgba,row*image.Width*4,pixels,((update.Y+row)*32+update.X)*4,image.Width*4);
                count+=image.Width*image.Height;
            }
            throw new TimeoutException("Dated progressive map did not finish");
        }
        byte[] Render(ProgressiveTerrainMap map,int revision){map.Begin(revision,view,Layer.Aridity,32);return Finish(map,revision);}
        using var cached=new ProgressiveTerrainMap(terrain,world,CancellationToken.None);
        var first=Render(cached,1);long initialBuilt=cached.TilesBuilt,hits=cached.TileHits;
        var second=Render(cached,2);Console.WriteLine($"Same date: differing bytes={first.Zip(second).Count(p=>p.First!=p.Second)}, built={initialBuilt}->{cached.TilesBuilt}, hits={hits}->{cached.TileHits}");
        Check(first.SequenceEqual(second),"Unchanged-date map pixels remain exact while adaptive tile sizes warm up");
        // Tile dimensions adapt to measured cost; warm the available partitions
        // before asserting cache reuse, rather than requiring a fixed partition.
        Render(cached,6);initialBuilt=cached.TilesBuilt;hits=cached.TileHits;
        var repeated=Render(cached,7);
        Check(first.SequenceEqual(repeated)&&cached.TileHits>hits,"An unchanged date reuses exact warmed cached tiles");
        var later=new Prehistory(world).AdvanceYears(original,25);world.Prehistory=later;
        var changed=Render(cached,3);
        Check(cached.TilesBuilt>initialBuilt,"A new dated climate epoch invalidates the earlier terrain-map tiles");
        using(var fresh=new ProgressiveTerrainMap(terrain,world,CancellationToken.None))Check(changed.SequenceEqual(Render(fresh,4)),"A dated cached stream matches a completely fresh current-date render");
        world.Prehistory=original;cached.Begin(5,view,Layer.Aridity,32);world.Prehistory=later;
        Check(first.SequenceEqual(Finish(cached,5)),"A request retains its captured date when gameplay publishes a newer state mid-render");
        Check(original.Hash==original.ComputeHash()&&later.Hash==later.ComputeHash(),"Terrain-map rendering cannot mutate either historical snapshot");
        Console.WriteLine($"PASS {checks} dated terrain-map cache and snapshot checks; built={cached.TilesBuilt}; hits={cached.TileHits}");
    }
}
