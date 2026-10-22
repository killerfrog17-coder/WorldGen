using System;
using System.Collections.Generic;
using System.Threading;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    /// <summary>One worker-owned exploration-map cache, bound to one terrain.
    /// Selected-layer rasters only; shared bounded terrain samples survive layer
    /// switches. No disk cache, player preferences or authoritative world edits.</summary>
    public sealed class TerrainMapContext
    {
        sealed class Entry {public TerrainMapImage Image=null!;}
        readonly LocalTerrain terrain;readonly List<Entry> images=new List<Entry>();
        internal readonly TerrainMapSampling.Sampler Sampler;
        V[] points=Array.Empty<V>();LocalTerrainSample[] samples=Array.Empty<LocalTerrainSample>();
        internal V[] PointScratch(int count){if(count>4096)return new V[count];if(points.Length<count)points=new V[count];return points;}
        internal LocalTerrainSample[] SampleScratch(int count){if(count>4096)return new LocalTerrainSample[count];if(samples.Length<count)samples=new LocalTerrainSample[count];return samples;}
        Layer layer=(Layer)(-1);int bytes;
        public long RasterHits {get;private set;}
        public long SampleHits=>Sampler.Hits;
        public long SampleMisses=>Sampler.Misses;
        public TerrainMapContext(LocalTerrain terrain){this.terrain=terrain;Sampler=new TerrainMapSampling.Sampler(terrain);}
        public bool Matches(LocalTerrain value)=>ReferenceEquals(value,terrain);
        public TerrainMapImage BuildTile(TerrainMapViewport view,int canvasWidth,int canvasHeight,int x,int y,int width,int height,CancellationToken token,Layer selected,float[]? palette,World? world)
        {
            // One-pixel halo makes slopes/shading identical across tile edges.
            var padded=TerrainMapSampling.Build(terrain,view,width+2,height+2,token,selected,palette,world,this,canvasWidth,canvasHeight,x-1,y-1);
            var image=new TerrainMapImage{Width=width,Height=height,View=view.Clone(),Rgba=new byte[width*height*4]};
            for(int row=0;row<height;row++)Buffer.BlockCopy(padded.Rgba,((row+1)*(width+2)+1)*4,image.Rgba,row*width*4,width*4);
            return image;
        }
        public TerrainMapImage Build(TerrainMapViewport view,int width,int height,CancellationToken token,Layer selected,float[]? palette,World? world)
        {
            token.ThrowIfCancellationRequested();if(layer!=selected){images.Clear();bytes=0;layer=selected;}
            for(int i=images.Count-1;i>=0;i--)
            {
                var image=images[i].Image;double tolerance=Math.Max(1e-12,view.LongitudeSpan*1e-8);
                if(image.Width!=width||image.Height!=height||Math.Abs(image.View.LongitudeSpan-view.LongitudeSpan)>tolerance||Math.Abs(TerrainMapViewport.Wrap(image.View.Longitude-view.Longitude))>tolerance||Math.Abs(image.View.Latitude-view.Latitude)>tolerance)continue;
                var entry=images[i];images.RemoveAt(i);images.Add(entry);RasterHits++;return image;
            }
            var result=TerrainMapSampling.Build(terrain,view,width,height,token,selected,palette,world,this);token.ThrowIfCancellationRequested();
            while(images.Count>0&&(images.Count>=8||bytes+result.Rgba.Length>6*1024*1024)){bytes-=images[0].Image.Rgba.Length;images.RemoveAt(0);}
            if(result.Rgba.Length<=6*1024*1024){images.Add(new Entry{Image=result});bytes+=result.Rgba.Length;}return result;
        }
    }
}
