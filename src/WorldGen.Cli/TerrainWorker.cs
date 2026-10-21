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
        else if(sourcePath!="")world=WorldFile.Read(File.ReadAllBytes(sourcePath));
        else{int size=reader.ReadInt32();if(size<0||size>WorldFile.MaximumFileBytes)throw new InvalidDataException("Invalid terrain source size.");var bytes=reader.ReadBytes(size);if(bytes.Length!=size)throw new EndOfStreamException();world=WorldFile.Read(bytes);}
        if(world.Stages.Last().Hash!=expected)throw new InvalidDataException("Terrain worker received a different source world.");
        var terrain=new LocalTerrain(world,horizontalScale:scale);
        var samples=new TerrainSampleCache();
        var scatter=new LocalScatter(terrain,scale);
        var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
        writer.Write(TerrainMeshWire.Version);writer.Write(typeof(LocalTerrain).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion??"");writer.Write(LocalTerrain.Version);writer.Write(((IPEndPoint)listener.LocalEndpoint).Port);writer.Flush();
        using var slots=new SemaphoreSlim(2);
        while(!exit.IsCancellationRequested)
        {
            var client=await listener.AcceptTcpClientAsync();client.NoDelay=true;
            _=Task.Run(async()=>
            {
                using var ownedClient=client;using var cancellation=new CancellationTokenSource();
                try
                {
                    using var stream=client.GetStream();using var r=new BinaryReader(stream);using var w=new BinaryWriter(stream);
                    if(r.ReadString()!=token)throw new InvalidDataException("Invalid terrain session token.");
                    byte command=r.ReadByte();if(command>1)throw new InvalidDataException("Unknown terrain worker command.");
                    double east=0,north=0,size=0;int segments=0;double[] edges=null;LocalTerrainFrame frame=null;ScatterKey key=default;
                    if(command==0)frame=TerrainMeshWire.ReadRequest(r,out east,out north,out size,out segments,out edges);
                    else key=TerrainMeshWire.ReadScatterKey(r);
                    // One connection per request. Closing/cancelling it also
                    // cancels queued or active Core work, preserving near-first
                    // scheduling rather than leaving abandoned CPU jobs running.
                    _=Task.Run(async()=>{try{var b=new byte[1];int n=await stream.ReadAsync(b);if(n==0||b[0]!=0)cancellation.Cancel();}catch{try{cancellation.Cancel();}catch(ObjectDisposedException){}}});
                    await slots.WaitAsync(cancellation.Token);
                    try
                    {
                        if(command==0){var mesh=LocalTerrainMesh.Build(terrain,frame,east,north,size,segments,cancellation.Token,edges,samples);cancellation.Token.ThrowIfCancellationRequested();w.Write(true);TerrainMeshWire.WriteMesh(w,mesh);}
                        else{var patch=scatter.Build(key,cancellation.Token);cancellation.Token.ThrowIfCancellationRequested();w.Write(true);TerrainMeshWire.WriteScatter(w,patch);}
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
