using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class TerrainWorkerTests
{
    public static void BootstrapReuse(string path,string executable,string output)
    {
        Directory.CreateDirectory(output);var world=WorldFile.ReadPlanetaryFile(path);string history=world.Prehistory?.Hash;
        string temporary=Path.Combine(Path.GetTempPath(),"WorldGen-bootstrap-"+Guid.NewGuid().ToString("N")+".world");
        var write=Stopwatch.StartNew();
        try
        {
            using(var file=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536))WorldFile.WritePlanetary(world,file);
            write.Stop();long bytes=new FileInfo(temporary).Length;
            int cell=Array.FindIndex(world.ElevationM,h=>h>100&&h<900);if(cell<0)throw new Exception("Bootstrap fixture requires ordinary dry land");
            var frame=new LocalTerrainFrame(world.Surface.Cells[cell].Position,world.Recipe.RadiusKm*1000,0,.1);
            (double Seconds,double CpuSeconds,string Payload) Boot(bool recipe)
            {
                string secret=Guid.NewGuid().ToString("N");var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();var timer=Stopwatch.StartNew();
                using var child=Process.Start(new ProcessStartInfo{FileName=Path.GetFullPath(executable),Arguments=$"--terrain-worker {((IPEndPoint)listener.LocalEndpoint).Port} --parent {Environment.ProcessId} --terrain-token {secret}",UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden})!;
                try
                {
                    int port;var accept=listener.AcceptTcpClientAsync();if(!accept.Wait(30000))throw new Exception("Bootstrap worker failed to connect");
                    using(var client=accept.Result)using(var stream=client.GetStream())using(var reader=new BinaryReader(stream))using(var writer=new BinaryWriter(stream))
                    {
                        client.ReceiveTimeout=180000;if(reader.ReadString()!=secret)throw new Exception("Bootstrap token mismatch");
                        writer.Write(world.Stages.Last().Hash);writer.Write(recipe?"@recipe":temporary);writer.Write(.1);if(recipe)writer.Write(WorldJson.Write(world.Recipe));writer.Flush();
                        if(reader.ReadInt32()!=TerrainMeshWire.Version)throw new Exception("Bootstrap wire mismatch");reader.ReadString();reader.ReadString();port=reader.ReadInt32();
                    }
                    timer.Stop();child.Refresh();double cpu=child.TotalProcessorTime.TotalSeconds;
                    using var query=new TcpClient{NoDelay=true,ReceiveTimeout=60000};query.Connect(IPAddress.Loopback,port);
                    using var incoming=query.GetStream();using var data=new BinaryReader(incoming);using var send=new BinaryWriter(incoming);
                    send.Write(secret);send.Write((byte)0);TerrainMeshWire.WriteRequest(send,frame,0,0,102.4,16,null);
                    if(!data.ReadBoolean())throw new Exception("Bootstrap first mesh failed: "+data.ReadString());
                    var mesh=TerrainMeshWire.ReadMesh(data);send.Write((byte)0);send.Flush();mesh.BuildMilliseconds=0;
                    using var payload=new MemoryStream();using(var encoded=new BinaryWriter(payload,System.Text.Encoding.UTF8,true))TerrainMeshWire.WriteMesh(encoded,mesh);
                    return(timer.Elapsed.TotalSeconds,cpu,Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(payload.ToArray())));
                }
                finally{listener.Stop();if(!child.HasExited){child.Kill(true);child.WaitForExit();}}
            }
            var recipeResult=Boot(true);var fileResult=Boot(false);
            if(recipeResult.Payload!=fileResult.Payload||world.Prehistory?.Hash!=history)throw new Exception("Planetary reuse changed first rendered mesh or parent history");
            File.WriteAllText(Path.Combine(output,"bootstrap.json"),System.Text.Json.JsonSerializer.Serialize(new{Cells=world.Surface.Cells.Length,WorldHash=world.Stages.Last().Hash,
                StreamWriteSeconds=write.Elapsed.TotalSeconds,SnapshotBytes=bytes,RecipeBootstrapSeconds=recipeResult.Seconds,RecipeCpuSeconds=recipeResult.CpuSeconds,
                FileBootstrapSeconds=fileResult.Seconds,FileCpuSeconds=fileResult.CpuSeconds,FileTotalSeconds=write.Elapsed.TotalSeconds+fileResult.Seconds,ExactFirstMeshSha256=fileResult.Payload,
                Note="Sequential CoreCLR component timings; shared CPU/Chrome, not a Unity/Mono fresh-entry or FPS claim."},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        }
        finally{File.Delete(temporary);}
        if(File.Exists(temporary))throw new Exception("Owned bootstrap snapshot was not cleaned");
        using(var cancelled=new CancellationTokenSource())
        {
            cancelled.Cancel();bool observed=false;
            try{using var file=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write);WorldFile.WritePlanetary(world,file,cancelled.Token);}
            catch(OperationCanceledException){observed=true;}
            finally{File.Delete(temporary);}
            if(!observed||File.Exists(temporary))throw new Exception("Cancelled streaming snapshot did not stop/clean up");
        }
        Console.WriteLine("PASS exact recipe/file first-mesh payload, unchanged parent history, owned snapshot and cancelled-write cleanup; "+Path.Combine(output,"bootstrap.json"));
    }
    public static void Run(string path,string executable,bool recipe=false)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var terrain=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);
        string secret=Guid.NewGuid().ToString("N");var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
        using var child=Process.Start(new ProcessStartInfo{FileName=Path.GetFullPath(executable),Arguments=$"--terrain-worker {((IPEndPoint)listener.LocalEndpoint).Port} --parent {Environment.ProcessId} --terrain-token {secret}",UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden})!;
        try
        {
            int port;
            var accept=listener.AcceptTcpClientAsync();if(!accept.Wait(30000))throw new Exception("Worker failed to connect.");
            using(var client=accept.Result)using(var stream=client.GetStream())using(var r=new BinaryReader(stream))using(var w=new BinaryWriter(stream))
            {
                if(r.ReadString()!=secret)throw new Exception("Session token mismatch.");
                w.Write(world.Stages.Last().Hash);w.Write(recipe?"@recipe":Path.GetFullPath(path));w.Write(.1);if(recipe)w.Write(WorldJson.Write(world.Recipe));w.Flush();
                if(r.ReadInt32()!=TerrainMeshWire.Version)throw new Exception("Wire version mismatch.");
                Console.WriteLine("Worker assembly "+r.ReadString()+"; "+r.ReadString());port=r.ReadInt32();
            }
            listener.Stop();int count=0;
            int dryCell=Enumerable.Range(0,world.ElevationM.Length).First(i=>world.ElevationM[i]>30&&world.ElevationM[i]<1200&&world.Ecology.Habitat[i].Biome==Biome.Grassland);
            V dryPoint=world.Surface.Cells[dryCell].Position;
            foreach(var at in new[]{TerrainMapViewport.FromDegrees(27.98,86.92),TerrainMapViewport.FromDegrees(30.2,-99),terrain.Amplification.RiverPoint(terrain.Hydrology.Channels.First(c=>c.WidthM>10&&c.StartM>10).From,.5),dryPoint})
            {
                double size=(at-dryPoint).Length<1e-12?102.4:204.8;
                var frame=new LocalTerrainFrame(at,terrain.RadiusM,terrain.Patch(at).Sample(0,0).ElevationM,.1);
                using var client=new TcpClient{NoDelay=true};client.Connect(IPAddress.Loopback,port);
                using var stream=client.GetStream();using var incoming=new TerrainReadStream(stream);using var r=new BinaryReader(incoming);using var w=new BinaryWriter(stream);
                double step=size/32;var intervals=new[]{0d,step*2,0,0,1,0,size*.5,step*.5,1,size*.5,size,step*2};
                w.Write(secret);w.Write((byte)0);TerrainMeshWire.WriteRequest(w,frame,0,0,size,32,intervals);
                if(!r.ReadBoolean())throw new Exception("Worker rejected valid request.");
                var actual=TerrainMeshWire.ReadMesh(r);w.Write((byte)0);w.Flush();var expected=LocalTerrainMesh.Build(terrain,frame,0,0,size,32,edgeSteps:intervals);
                if(size<150&&expected.GroundCover.Length==0)throw new Exception("Fine worker flora sidecar missing.");
                var pairs=new[]{(actual.Positions,expected.Positions),(actual.Normals,expected.Normals),(actual.Colors,expected.Colors),(actual.SurfaceWeights,expected.SurfaceWeights),(actual.GroundWeightsB,expected.GroundWeightsB),(actual.Lithology,expected.Lithology),(actual.WaterPositions,expected.WaterPositions),(actual.WaterColors,expected.WaterColors),(actual.GroundCover,expected.GroundCover)};
                foreach(var pair in pairs){if(!pair.Item1.SequenceEqual(pair.Item2))throw new Exception("Worker changed mesh float data at "+at.X+","+at.Y+","+at.Z);count+=pair.Item1.Length;}
                if(!actual.Triangles.SequenceEqual(expected.Triangles)||!actual.WaterTriangles.SequenceEqual(expected.WaterTriangles))throw new Exception("Worker changed terrain/water topology.");
                if(actual.MinHeightM!=expected.MinHeightM||actual.MaxHeightM!=expected.MaxHeightM)throw new Exception("Worker changed mesh bounds.");
            }
            var scatter=new LocalScatter(terrain,.1);
            foreach(bool detail in new[]{false,true})
            {
                var key=scatter.Address(TerrainMapViewport.FromDegrees(35.6,-83.5),detail);var expected=scatter.Build(key);
                using var client=new TcpClient{NoDelay=true};client.Connect(IPAddress.Loopback,port);using var stream=client.GetStream();using var incoming=new TerrainReadStream(stream);using var r=new BinaryReader(incoming);using var w=new BinaryWriter(stream);
                w.Write(secret);w.Write((byte)1);TerrainMeshWire.WriteScatterKey(w,key);if(!r.ReadBoolean())throw new Exception("Scatter worker rejected request.");var actual=TerrainMeshWire.ReadScatter(r,key);w.Write((byte)0);w.Flush();
                if(!actual.Instances.SequenceEqual(expected.Instances)||!actual.Center.Equals(expected.Center))throw new Exception("Worker changed scatter identities, positions, normals, sizes or colours.");
                count+=actual.Instances.Length;Console.WriteLine("Exact scatter "+key.Detail+": "+actual.Instances.Length+" instances");
            }
            int forestCell=Enumerable.Range(0,world.ElevationM.Length).First(i=>world.ElevationM[i]>30&&(world.Ecology.Habitat[i].Biome==Biome.BorealForest||world.Ecology.Habitat[i].Biome==Biome.TemperateForest||world.Ecology.Habitat[i].Biome==Biome.TropicalRainforest));
            var forestScatter=scatter.Address(world.Surface.Cells[forestCell].Position,false);
            var forestKey=new ForestPageKey(forestScatter.Face,forestScatter.X*LocalScatter.TreeCandidateSide/LocalScatter.ForestPageSide,forestScatter.Y*LocalScatter.TreeCandidateSide/LocalScatter.ForestPageSide,0);
            foreach(var key in new[]{forestKey,forestKey.Parent})
            {
                var expected=scatter.BuildForest(key);using var client=new TcpClient{NoDelay=true};client.Connect(IPAddress.Loopback,port);
                using var stream=client.GetStream();using var incoming=new TerrainReadStream(stream);using var r=new BinaryReader(incoming,System.Text.Encoding.UTF8,true);using var w=new BinaryWriter(stream,System.Text.Encoding.UTF8,true);
                w.Write(secret);w.Write((byte)3);TerrainMeshWire.WriteForestKey(w,key);if(!r.ReadBoolean())throw new Exception("Forest worker rejected request.");
                var actual=TerrainMeshWire.ReadForest(r,key);w.Write((byte)0);w.Flush();
                if(!actual.Roots.SequenceEqual(expected.Roots)||actual.Probes!=expected.Probes)throw new Exception("Buffered worker changed forest root metadata/payload.");
                count+=actual.Roots.Length;Console.WriteLine("Exact buffered forest "+key.Level+": "+actual.Roots.Length+" roots");
            }
            // A computation failure must reply on the original live stream,
            // not disappear as an unexplained EOF. The valid request below also
            // proves the worker remains usable after this failed request.
            using(var client=new TcpClient{NoDelay=true})
            {
                client.ReceiveTimeout=15000;client.Connect(IPAddress.Loopback,port);
                using var stream=client.GetStream();using var incoming=new TerrainReadStream(stream);using var r=new BinaryReader(incoming,System.Text.Encoding.UTF8,true);using var w=new BinaryWriter(stream,System.Text.Encoding.UTF8,true);
                var frame=new LocalTerrainFrame(dryPoint,terrain.RadiusM,0,.1);
                w.Write(secret);w.Write((byte)0);TerrainMeshWire.WriteRequest(w,frame,123,-456,204.8,3,null);
                if(r.ReadBoolean())throw new Exception("Malformed terrain request unexpectedly succeeded.");
                string failure=r.ReadString();
                if(!failure.Contains("terrain east=123 north=-456 size=204.8 segments=3",StringComparison.Ordinal)||!failure.Contains("ArgumentOutOfRangeException",StringComparison.Ordinal))
                    throw new Exception("Worker computation error lost its exception/request parameters: "+failure);
            }
            // A disconnected request must not poison the next connection.
            using(var client=new TcpClient())
            {client.Connect(IPAddress.Loopback,port);using var w=new BinaryWriter(client.GetStream());w.Write(secret);w.Write((byte)0);var frame=new LocalTerrainFrame(TerrainMapViewport.FromDegrees(27.98,86.92),terrain.RadiusM,0,.1);TerrainMeshWire.WriteRequest(w,frame,10000,10000,3276.8,128,null);w.Write((byte)1);w.Flush();}
            using(var client=new TcpClient{NoDelay=true})
            {
                client.Connect(IPAddress.Loopback,port);using var stream=client.GetStream();using var incoming=new TerrainReadStream(stream);using var r=new BinaryReader(incoming,System.Text.Encoding.UTF8,true);using var w=new BinaryWriter(stream,System.Text.Encoding.UTF8,true);
                var key=scatter.Address(dryPoint,false);w.Write(secret);w.Write((byte)1);TerrainMeshWire.WriteScatterKey(w,key);
                if(!r.ReadBoolean()||!TerrainMeshWire.ReadScatter(r,key).Instances.SequenceEqual(scatter.Build(key).Instances))throw new Exception("Worker failed a valid buffered request after cancellation.");w.Write((byte)0);w.Flush();
            }
            Console.WriteLine("PASS "+count+" exact worker mesh floats, terrain/water indices, bounds and cancellation submission");
        }
        finally{listener.Stop();if(!child.HasExited)child.Kill();child.WaitForExit();}
    }
}
