using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class TerrainWorkerCostTests
{
    // Explicit offline audit of packaged workers. It never starts Unity or
    // changes runtime preferences, and does not treat component cost as FPS.
    public static void Run(string path,string executable,string sitesPath,string output,bool verifyCurrent=false)
    {
        Directory.CreateDirectory(output);var world=WorldFile.ReadPlanetaryFile(path);
        using var sites=JsonDocument.Parse(File.ReadAllText(sitesPath).TrimStart('\ufeff'));
        var point=sites.RootElement.GetProperty("Sites")[0].GetProperty("Position");
        V at=new V(point.GetProperty("X").GetDouble(),point.GetProperty("Y").GetDouble(),point.GetProperty("Z").GetDouble());
        var frame=new LocalTerrainFrame(at,world.Recipe.RadiusKm*1000,0,.1);
        string secret=Guid.NewGuid().ToString("N");var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();var watch=Stopwatch.StartNew();
        using var child=Process.Start(new ProcessStartInfo{FileName=Path.GetFullPath(executable),Arguments=$"--terrain-worker {((IPEndPoint)listener.LocalEndpoint).Port} --parent {Environment.ProcessId} --terrain-token {secret}",UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden})!;
        try
        {
            int port;string version,assembly;
            var accept=listener.AcceptTcpClientAsync();if(!accept.Wait(30000))throw new Exception("Packaged worker did not connect");
            using(var client=accept.Result)using(var stream=client.GetStream())using(var r=new BinaryReader(stream))using(var w=new BinaryWriter(stream))
            {
                client.ReceiveTimeout=120000;if(r.ReadString()!=secret)throw new Exception("Worker token mismatch");
                w.Write(world.Stages.Last().Hash);w.Write(Path.GetFullPath(path));w.Write(.1);w.Flush();
                if(r.ReadInt32()!=TerrainMeshWire.Version)throw new Exception("Packaged worker wire mismatch");
                assembly=r.ReadString();version=r.ReadString();port=r.ReadInt32();
            }
            listener.Stop();double prepare=watch.Elapsed.TotalSeconds;var rows=new List<object>();
            File.WriteAllText(Path.Combine(output,"worker.json"),JsonSerializer.Serialize(new{Executable=Path.GetFullPath(executable),Assembly=assembly,Surface=version,PreparationSeconds=prepare},new JsonSerializerOptions{WriteIndented=true}));
            var requests=new[]{(0d,0d,102.4,64),(102.4,0d,102.4,64),(-102.4,0d,102.4,64),(0d,102.4,102.4,64),(0d,-102.4,102.4,64),(-1024d,-1024d,409.6,32),(1024d,-1024d,409.6,32),(1024d,1024d,409.6,32),(-1024d,1024d,409.6,32),(-2048d,0d,819.2,32),(2048d,0d,819.2,32),(0d,2048d,819.2,32)};
            foreach(var request in requests)
            {
                using var client=new TcpClient{NoDelay=true,ReceiveTimeout=120000};client.Connect(IPAddress.Loopback,port);
                using var stream=client.GetStream();using var incoming=new TerrainReadStream(stream);using var r=new BinaryReader(incoming);using var w=new BinaryWriter(stream);
                watch.Restart();double cpu=child.TotalProcessorTime.TotalMilliseconds;
                w.Write(secret);w.Write((byte)0);TerrainMeshWire.WriteRequest(w,frame,request.Item1,request.Item2,request.Item3,request.Item4,null);w.Flush();
                if(!r.ReadBoolean())throw new Exception("Worker request failed: "+r.ReadString());
                var mesh=TerrainMeshWire.ReadMesh(r);w.Write((byte)0);w.Flush();child.Refresh();
                var row=new{Kind="terrain",East=request.Item1,North=request.Item2,Size=request.Item3,Segments=request.Item4,ElapsedMs=watch.Elapsed.TotalMilliseconds,CpuMs=child.TotalProcessorTime.TotalMilliseconds-cpu,mesh.BuildMilliseconds,Vertices=mesh.Positions.Length/3,WaterVertices=mesh.WaterPositions.Length/3};rows.Add(row);
                File.WriteAllText(Path.Combine(output,"tiles.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
                Console.WriteLine($"{version} tile {request.Item1},{request.Item2}/{request.Item3} n{request.Item4}: {row.ElapsedMs:F1}ms wall / {row.BuildMilliseconds:F1}ms build / {row.CpuMs:F1}ms CPU");
            }
            var model=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);var scatter=new LocalScatter(model,.1);
            foreach(var delta in new[]{(0d,0d),(512d,0d),(0d,512d)})foreach(bool detail in new[]{false,true})
            {
                V position=frame.Position(delta.Item1,delta.Item2);var key=scatter.Address(position,detail);
                var forest=new ForestPageKey(key.Face,key.X*LocalScatter.TreeCandidateSide/LocalScatter.ForestPageSide,key.Y*LocalScatter.TreeCandidateSide/LocalScatter.ForestPageSide,0);
                foreach(byte command in new byte[]{1,3})
                {
                    if(detail&&command==3)continue;
                    using var client=new TcpClient{NoDelay=true,ReceiveTimeout=120000};client.Connect(IPAddress.Loopback,port);
                    using var stream=client.GetStream();using var incoming=new TerrainReadStream(stream);using var r=new BinaryReader(incoming);using var w=new BinaryWriter(stream);
                    watch.Restart();child.Refresh();double cpu=child.TotalProcessorTime.TotalMilliseconds;
                    w.Write(secret);w.Write(command);if(command==1)TerrainMeshWire.WriteScatterKey(w,key);else TerrainMeshWire.WriteForestKey(w,forest);w.Flush();
                    if(!r.ReadBoolean())throw new Exception("Packaged vegetation request failed: "+r.ReadString());
                    ScatterPatch actualScatter=command==1?TerrainMeshWire.ReadScatter(r,key):null;ForestPage actualForest=command==3?TerrainMeshWire.ReadForest(r,forest):null;
                    int count=actualScatter?.Instances.Length??actualForest!.Roots.Length;
                    w.Write((byte)0);w.Flush();child.Refresh();watch.Stop();double cpuMs=child.TotalProcessorTime.TotalMilliseconds-cpu;
                    using var payload=new MemoryStream();using(var encoded=new BinaryWriter(payload,System.Text.Encoding.UTF8,true)){if(command==1)TerrainMeshWire.WriteScatter(encoded,actualScatter!);else TerrainMeshWire.WriteForest(encoded,actualForest!);}
                    string hash=Convert.ToHexString(SHA256.HashData(payload.ToArray())).ToLowerInvariant();
                    if(verifyCurrent)
                    {
                        using var expectedPayload=new MemoryStream();using(var encoded=new BinaryWriter(expectedPayload,System.Text.Encoding.UTF8,true)){if(command==1)TerrainMeshWire.WriteScatter(encoded,scatter.Build(key));else TerrainMeshWire.WriteForest(encoded,scatter.BuildForest(forest));}
                        string expected=Convert.ToHexString(SHA256.HashData(expectedPayload.ToArray())).ToLowerInvariant();
                        if(expected!=hash)throw new Exception("Packaged/current complete vegetation payload differs at "+delta+", detail "+detail+", command "+command);
                    }
                    var row=new{Kind=command==1?(detail?"scatter-detail":"scatter"):"forest",East=delta.Item1,North=delta.Item2,ElapsedMs=watch.Elapsed.TotalMilliseconds,CpuMs=cpuMs,Items=count,PayloadSha256=hash,ExactCurrentPayload=(bool?)(verifyCurrent?true:null)};rows.Add(row);
                    File.WriteAllText(Path.Combine(output,"tiles.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
                    Console.WriteLine($"{version} {row.Kind} {delta}: {row.ElapsedMs:F1}ms wall / {row.CpuMs:F1}ms CPU, {count} items");
                }
            }
        }
        finally{listener.Stop();if(!child.HasExited){child.Kill(true);child.WaitForExit();}}
    }
}
