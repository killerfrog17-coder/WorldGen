using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    /// <summary>Cancellable preview + centre-out terrain tiles. CPU workers own
    /// separate sampling cursors; output is bounded and never calls Unity.</summary>
    public sealed class ProgressiveTerrainMap : IDisposable
    {
        public sealed class Update
        {
            public int Revision,X,Y,CanvasWidth,CanvasHeight;public bool Preview,Complete;public string Error="";
            public TerrainMapImage? Image;public Layer Layer;public long TileHits,TilesBuilt;
        }
        sealed class Worker
        {public readonly SemaphoreSlim Gate=new SemaphoreSlim(1);public readonly TerrainMapContext Context;public Worker(LocalTerrain t){Context=new TerrainMapContext(t);}}
        sealed class CachedTile {public (int,int,int,int,int,int) Key;public TerrainMapImage Image=null!;}
        sealed class CachedView {public int Id;public Layer Layer;public TerrainMapViewport View=null!;}
        public delegate void RemoteProducer(int revision,TerrainMapViewport view,Layer layer,int width,Action<Update> publish,CancellationToken token);
        readonly LocalTerrain terrain;readonly World? world;readonly Worker[] workers;readonly RemoteProducer? remote;
        readonly CancellationToken lifetime;CancellationTokenSource? request;
        readonly ConcurrentQueue<Update> output=new ConcurrentQueue<Update>();readonly SemaphoreSlim outputSlots=new SemaphoreSlim(16);
        readonly object cacheGate=new object();readonly List<CachedView> views=new List<CachedView>();int nextView,cacheBytes;
        readonly LinkedList<CachedTile> lru=new LinkedList<CachedTile>();
        readonly Dictionary<(int,int,int,int,int,int),LinkedListNode<CachedTile>> tiles=new Dictionary<(int,int,int,int,int,int),LinkedListNode<CachedTile>>();
        Layer paletteLayer=(Layer)(-1);float[]? palette;long tileHits,tilesBuilt;int previewWidth=16,tileSide=8;
        public int WorkerCount=>remote!=null?4:workers.Length;
        public long TileHits=>Interlocked.Read(ref tileHits);public long TilesBuilt=>Interlocked.Read(ref tilesBuilt);
        public ProgressiveTerrainMap(LocalTerrain terrain,World? world,CancellationToken lifetime,RemoteProducer? remote=null)
        {this.terrain=terrain;this.world=world;this.lifetime=lifetime;this.remote=remote;workers=new Worker[remote!=null?0:Math.Max(1,Math.Min(4,Environment.ProcessorCount-2))];for(int i=0;i<workers.Length;i++)workers[i]=new Worker(terrain);}
        public void Begin(int revision,TerrainMapViewport view,Layer layer,int targetWidth)
        {
            Cancel();while(TryTake(out _)){};
            var source=CancellationTokenSource.CreateLinkedTokenSource(lifetime);request=source;
            _=Task.Run(async()=>
            {
                try
                {
                    if(remote==null)await Produce(revision,view.Clone(),layer,targetWidth,source.Token).ConfigureAwait(false);
                    else remote(revision,view.Clone(),layer,targetWidth,u=>{Interlocked.Exchange(ref tileHits,u.TileHits);Interlocked.Exchange(ref tilesBuilt,u.TilesBuilt);Publish(u,source.Token).GetAwaiter().GetResult();},source.Token);
                }
                catch(OperationCanceledException){}
                catch(Exception e){if(!source.IsCancellationRequested)await Publish(new Update{Revision=revision,Error=e.Message,Layer=layer},source.Token).ConfigureAwait(false);}
                finally{source.Dispose();}
            });
        }
        public void Cancel(){try{request?.Cancel();}catch(ObjectDisposedException){}request=null;}
        public bool TryTake(out Update update){if(output.TryDequeue(out update!)){outputSlots.Release();return true;}return false;}
        async Task Publish(Update value,CancellationToken token)
        {
            await outputSlots.WaitAsync(token).ConfigureAwait(false);
            if(token.IsCancellationRequested){outputSlots.Release();token.ThrowIfCancellationRequested();}output.Enqueue(value);
        }
        int ViewKey(TerrainMapViewport view,Layer layer)
        {
            lock(cacheGate)
            {
                double tolerance=Math.Max(1e-14,view.LongitudeSpan*1e-8);
                foreach(var entry in views)if(entry.Layer==layer&&Math.Abs(entry.View.Latitude-view.Latitude)<tolerance&&Math.Abs(TerrainMapViewport.Wrap(entry.View.Longitude-view.Longitude))<tolerance&&Math.Abs(entry.View.LongitudeSpan-view.LongitudeSpan)<tolerance)return entry.Id;
                if(views.Count>=8)views.RemoveAt(0);int id=++nextView;views.Add(new CachedView{Id=id,Layer=layer,View=view.Clone()});return id;
            }
        }
        TerrainMapImage Tile(Worker worker,int viewId,TerrainMapViewport view,Layer layer,int width,int x,int y,int w,int h,float[]? colors,CancellationToken token)
        {
            var key=(viewId,width,x,y,w,h);
            lock(cacheGate)if(tiles.TryGetValue(key,out var found)){lru.Remove(found);lru.AddLast(found);Interlocked.Increment(ref tileHits);return found.Value.Image;}
            var result=worker.Context.BuildTile(view,width,width/2,x,y,w,h,token,layer,colors,world);token.ThrowIfCancellationRequested();
            Interlocked.Increment(ref tilesBuilt);
            lock(cacheGate)
            {
                if(!tiles.ContainsKey(key))
                {
                    while(lru.Count>0&&(cacheBytes+result.Rgba.Length>16*1024*1024||lru.Count>=4096)){var old=lru.First!;cacheBytes-=old.Value.Image.Rgba.Length;tiles.Remove(old.Value.Key);lru.RemoveFirst();}
                    tiles.Add(key,lru.AddLast(new CachedTile{Key=key,Image=result}));cacheBytes+=result.Rgba.Length;
                }
            }
            return result;
        }
        async Task Produce(int revision,TerrainMapViewport view,Layer layer,int targetWidth,CancellationToken token)
        {
            int first=Math.Max(4,Volatile.Read(ref previewWidth));var watch=Stopwatch.StartNew();
            var preview=TerrainMapPreview.Build(terrain,world,view,first,layer,token);
            // Target well below half a second; adapt future first previews to
            // observed cost, rather than requiring a fixed sharp first raster.
            double elapsed=Math.Max(.001,watch.Elapsed.TotalSeconds);
            Volatile.Write(ref previewWidth,Math.Max(4,Math.Min(32,(int)(first*Math.Sqrt(.15/elapsed)))));
            await Publish(new Update{Revision=revision,Image=preview,CanvasWidth=first,CanvasHeight=first/2,Preview=true,Layer=layer},token).ConfigureAwait(false);
            float[]? colors=null;
            if(!TerrainMapSampling.Continuous(layer)&&world!=null)
            {
                lock(cacheGate)if(paletteLayer==layer)colors=palette;
                if(colors==null){colors=GlobeView.CellValues(world,layer,token:token).Colors;token.ThrowIfCancellationRequested();lock(cacheGate){palette=colors;paletteLayer=layer;}}
            }
            int viewId=ViewKey(view,layer);
            for(int resolution=32;;resolution=Math.Min(targetWidth,resolution*2))
            {
                token.ThrowIfCancellationRequested();int width=Math.Min(targetWidth,resolution),height=width/2;
                int side=Math.Max(4,Math.Min(32,Volatile.Read(ref tileSide)));
                var work=new List<(int x,int y,int w,int h)>();
                for(int y=0;y<height;y+=side)for(int x=0;x<width;x+=side)work.Add((x,y,Math.Min(side,width-x),Math.Min(side,height-y)));
                work.Sort((a,b)=>{double Distance((int x,int y,int w,int h) p)=>Math.Pow(p.x+p.w*.5-width*.5,2)+Math.Pow(p.y+p.h*.5-height*.5,2);return Distance(a).CompareTo(Distance(b));});
                int next=-1;var tasks=new Task[workers.Length];
                for(int lane=0;lane<workers.Length;lane++)
                {
                    var worker=workers[lane];tasks[lane]=Task.Run(async()=>
                    {
                        await worker.Gate.WaitAsync(token).ConfigureAwait(false);
                        try
                        {
                            for(int at=Interlocked.Increment(ref next);at<work.Count;at=Interlocked.Increment(ref next))
                            {
                                token.ThrowIfCancellationRequested();var p=work[at];var clock=Stopwatch.StartNew();
                                var image=Tile(worker,viewId,view,layer,width,p.x,p.y,p.w,p.h,colors,token);
                                double seconds=Math.Max(.001,clock.Elapsed.TotalSeconds);
                                if(seconds>.15)Volatile.Write(ref tileSide,Math.Max(4,side/2));else if(seconds<.03)Volatile.Write(ref tileSide,Math.Min(32,side*2));
                                await Publish(new Update{Revision=revision,Image=image,X=p.x,Y=p.y,CanvasWidth=width,CanvasHeight=height,Layer=layer},token).ConfigureAwait(false);
                            }
                        }
                        finally{worker.Gate.Release();}
                    },token);
                }
                await Task.WhenAll(tasks).ConfigureAwait(false);
                if(width>=targetWidth)break;
            }
            await Publish(new Update{Revision=revision,Layer=layer,Complete=true},token).ConfigureAwait(false);
        }
        public void Dispose(){Cancel();while(TryTake(out _)){};}
    }
}
