using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class TerrainWorker
{
    public static async Task Run(int bootstrapPort,int parent,string token)
    {
        using var exit=new CancellationTokenSource();
        _=Task.Run(async()=>{while(!exit.IsCancellationRequested){await Task.Delay(1000);try{if(Process.GetProcessById(parent).HasExited)Environment.Exit(0);}catch{Environment.Exit(0);}}});
        using var bootstrap=new TcpClient{NoDelay=true};await bootstrap.ConnectAsync(IPAddress.Loopback,bootstrapPort);
        using var boot=bootstrap.GetStream();using var reader=new BinaryReader(boot);using var writer=new BinaryWriter(boot);
        writer.Write(token);writer.Flush();
        string expected=reader.ReadString(),sourcePath=reader.ReadString();double scale=reader.ReadDouble();World world;
        if(sourcePath=="@recipe")world=new WorldPipeline().Generate(WorldJson.ReadRecipe(reader.ReadString()));
        else if(sourcePath!="")world=WorldFile.ReadPlanetaryFile(sourcePath);
        else{int size=reader.ReadInt32();if(size<0||size>WorldFile.MaximumFileBytes)throw new InvalidDataException("Invalid terrain source size.");var bytes=reader.ReadBytes(size);if(bytes.Length!=size)throw new EndOfStreamException();world=WorldFile.ReadPlanetary(bytes);}
        if(world.Stages.Last().Hash!=expected)throw new InvalidDataException("Terrain worker received a different source world.");
        var terrain=LocalTerrainDerivationCache.Create(world,horizontalScale:scale);
        var samples=new TerrainSampleCache();
        var scatter=new LocalScatter(terrain,scale);
        string profilePath=Environment.GetEnvironmentVariable("WORLDGEN_TERRAIN_PROFILE");var profileGate=new object();
        var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
        writer.Write(TerrainMeshWire.Version);writer.Write(typeof(LocalTerrain).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion??"");writer.Write(LocalTerrain.Version);writer.Write(((IPEndPoint)listener.LocalEndpoint).Port);writer.Flush();
        // The player reserves capacity according to its loading-effort setting.
        // Four is a hard ceiling, not an unbounded request-driven thread count.
        using var slots=new SemaphoreSlim(4);
        using var mapGate=new SemaphoreSlim(1);
        using var maps=new ProgressiveTerrainMap(terrain,world,exit.Token);
        while(!exit.IsCancellationRequested)
        {
            var client=await listener.AcceptTcpClientAsync();client.NoDelay=true;
            _=Task.Run(async()=>
            {
                using var ownedClient=client;using var cancellation=new CancellationTokenSource();
                try
                {
                    // Root packets contain many scalar fields. Coalesce them
                    // into bulk writes rather than issuing a TCP send per value.
                    // Keep cancellation's duplex reader on the raw stream.
                    using var stream=client.GetStream();using var r=new BinaryReader(stream);
                    using var response=new BufferedStream(stream,65536);using var w=new BinaryWriter(response,System.Text.Encoding.UTF8,true);
                    if(r.ReadString()!=token)throw new InvalidDataException("Invalid terrain session token.");
                    byte command=r.ReadByte();if(command>5)throw new InvalidDataException("Unknown terrain worker command.");
                    double east=0,north=0,size=0;int segments=0;double[] edges=null;LocalTerrainFrame frame=null;ScatterKey key=default;
                    int revision=0,mapWidth=0;Layer mapLayer=default;TerrainMapViewport mapView=null;
                    ForestPageKey forest=default;
                    if(command==0||command==5)frame=TerrainMeshWire.ReadRequest(r,out east,out north,out size,out segments,out edges);
                    else if(command==1||command==4)key=TerrainMeshWire.ReadScatterKey(r);
                    else if(command==2)mapView=TerrainMapWire.ReadRequest(r,out revision,out mapLayer,out mapWidth);
                    else forest=TerrainMeshWire.ReadForestKey(r);
                    // One connection per request. Closing/cancelling it also
                    // cancels queued or active Core work, preserving near-first
                    // scheduling rather than leaving abandoned CPU jobs running.
                    _=Task.Run(async()=>{try{var b=new byte[1];int n=await stream.ReadAsync(b);if(n==0||b[0]!=0)cancellation.Cancel();}catch{try{cancellation.Cancel();}catch(ObjectDisposedException){}}});
                    if(command==2)
                    {
                        await mapGate.WaitAsync(cancellation.Token);int acquired=0;
                        try
                        {
                            // Map-open scheduling yields new terrain work. Reserve
                            // the same four lanes, not four additional CPU workers.
                            for(;acquired<4;acquired++)await slots.WaitAsync(cancellation.Token);
                            maps.Begin(revision,mapView,mapLayer,mapWidth);w.Write(true);w.Flush();
                            while(true)
                            {
                                cancellation.Token.ThrowIfCancellationRequested();
                                if(!maps.TryTake(out var update)){await Task.Delay(2,cancellation.Token);continue;}
                                if(update.Revision!=revision)continue;
                                update.TileHits=maps.TileHits;update.TilesBuilt=maps.TilesBuilt;
                                TerrainMapWire.WriteUpdate(w,update);
                                if(update.Complete||update.Error!="")break;
                            }
                        }
                        finally{maps.Cancel();if(acquired>0)slots.Release(acquired);mapGate.Release();}
                        return;
                    }
                    await slots.WaitAsync(cancellation.Token);
                    try
                    {
                        if(command==0||command==5)
                        {
                            var timing=string.IsNullOrEmpty(profilePath)?null:new TerrainBuildTiming();
                            var mesh=LocalTerrainMesh.Build(terrain,frame,east,north,size,segments,cancellation.Token,edges,samples,timing,coverageOnly:command==5);cancellation.Token.ThrowIfCancellationRequested();
                            if(timing!=null)
                            {
                                var entry=new{East=east,North=north,Size=size,Segments=segments,mesh.BuildMilliseconds,timing.GeometrySamples,timing.FullSurfaceQueries,timing.GeometryQueries,timing.SurfaceFallbacks,Stages=timing.Milliseconds};
                                lock(profileGate)File.AppendAllText(profilePath,System.Text.Json.JsonSerializer.Serialize(entry)+Environment.NewLine);
                            }
                            w.Write(true);TerrainMeshWire.WriteMesh(w,mesh);
                        }
                        else if(command==1||command==4){var patch=scatter.Build(key,cancellation.Token,includeTrees:command==1);cancellation.Token.ThrowIfCancellationRequested();w.Write(true);TerrainMeshWire.WriteScatter(w,patch);}
                        else{var page=scatter.BuildForest(forest,cancellation.Token);cancellation.Token.ThrowIfCancellationRequested();w.Write(true);TerrainMeshWire.WriteForest(w,page);}
                    }
                    finally{slots.Release();}
                }
                catch(OperationCanceledException){}
                catch(IOException){}
                catch(Exception error){try{using var w=new BinaryWriter(client.GetStream());w.Write(false);w.Write(error.Message);w.Flush();}catch{}Console.Error.WriteLine(error);}
            });
        }
    }
}
