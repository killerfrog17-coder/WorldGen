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
    static string[] AttributeChanges(LocalTerrainMesh a,LocalTerrainMesh b)
    {
        var changes=new List<string>();
        foreach(var (name,x,y) in new[]{("positions",a.Positions,b.Positions),("normals",a.Normals,b.Normals),("colors",a.Colors,b.Colors),("soil",a.SurfaceWeights,b.SurfaceWeights),("groundB",a.GroundWeightsB,b.GroundWeightsB),("lithology",a.Lithology,b.Lithology),("waterPositions",a.WaterPositions,b.WaterPositions),("waterColors",a.WaterColors,b.WaterColors),("groundCover",a.GroundCover,b.GroundCover),("midCover",a.MidCover,b.MidCover),("forestCover",a.ForestCover,b.ForestCover)})
        {
            if(x.Length!=y.Length){changes.Add(name+": length "+x.Length+" -> "+y.Length);continue;}
            int count=0;double maximum=0;
            for(int i=0;i<x.Length;i++)if(BitConverter.SingleToInt32Bits(x[i])!=BitConverter.SingleToInt32Bits(y[i])){count++;maximum=Math.Max(maximum,Math.Abs(x[i]-y[i]));}
            if(count>0)changes.Add(name+": "+count+" components, max "+maximum.ToString("R"));
        }
        if(!a.Triangles.SequenceEqual(b.Triangles))changes.Add("land indices differ");
        if(!a.WaterTriangles.SequenceEqual(b.WaterTriangles))changes.Add("water indices differ");
        if(a.MinHeightM!=b.MinHeightM||a.MaxHeightM!=b.MaxHeightM||a.GroundVertices!=b.GroundVertices)changes.Add("bounds/regular grid metadata differ");
        return changes.ToArray();
    }
    static string MeshHash(LocalTerrainMesh mesh,bool geometryOnly=false,bool surfaceAttributesOnly=false)
    {
        double elapsed=mesh.BuildMilliseconds;
        float[] original=mesh.Colors;
        var surface=mesh.SurfaceWeights;var second=mesh.GroundWeightsB;
        var ground=mesh.GroundCover;var mid=mesh.MidCover;var forest=mesh.ForestCover;
        using var bytes=new MemoryStream();
        try
        {
            mesh.BuildMilliseconds=0;
            if(geometryOnly){mesh.Colors=(float[])original.Clone();for(int i=0;i<mesh.Colors.Length;i++)if(i%4!=3)mesh.Colors[i]=0;}
            if(surfaceAttributesOnly)mesh.Colors=mesh.SurfaceWeights=mesh.GroundWeightsB=mesh.GroundCover=mesh.MidCover=mesh.ForestCover=Array.Empty<float>();
            using(var writer=new BinaryWriter(bytes,System.Text.Encoding.UTF8,true))TerrainMeshWire.WriteMesh(writer,mesh);
        }
        finally{mesh.BuildMilliseconds=elapsed;mesh.Colors=original;mesh.SurfaceWeights=surface;mesh.GroundWeightsB=second;mesh.GroundCover=ground;mesh.MidCover=mid;mesh.ForestCover=forest;}
        return Convert.ToHexString(SHA256.HashData(bytes.ToArray())).ToLowerInvariant();
    }
    // Explicit offline audit of packaged workers. It never starts Unity or
    // changes runtime preferences, and does not treat component cost as FPS.
    public static void Run(string path,string executable,string sitesPath,string output,bool verifyCurrent=false,bool geometryOnly=false,bool farTiles=false,bool reportCurrent=false,bool surfaceAttributesOnly=false,bool ignoreForestProbeCount=false)
    {
        if(geometryOnly&&surfaceAttributesOnly)throw new ArgumentException("Choose one explicit comparison normalization");
        Directory.CreateDirectory(output);var world=WorldFile.ReadPlanetaryFile(path);
        using var sites=JsonDocument.Parse(File.ReadAllText(sitesPath).TrimStart('\ufeff'));
        var point=sites.RootElement.GetProperty("Sites")[0].GetProperty("Position");
        V at=new V(point.GetProperty("X").GetDouble(),point.GetProperty("Y").GetDouble(),point.GetProperty("Z").GetDouble());
        var frame=new LocalTerrainFrame(at,world.Recipe.RadiusKm*1000,sites.RootElement.TryGetProperty("AltitudeM",out var altitude)?altitude.GetDouble():0,.1);
        bool boundaryReview=sites.RootElement.TryGetProperty("CheckWaterBoundary",out var boundary)&&boundary.GetBoolean();
        bool terrainOnly=sites.RootElement.TryGetProperty("TerrainOnly",out var only)&&only.GetBoolean();
            bool forestOnly=sites.RootElement.TryGetProperty("ForestOnly",out var forestOnlyValue)&&forestOnlyValue.GetBoolean();
            int forestLevel=sites.RootElement.TryGetProperty("ForestLevel",out var levelValue)?levelValue.GetInt32():0;
            if(forestLevel<0||forestLevel>15)throw new ArgumentOutOfRangeException("ForestLevel");
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
            var model=LocalTerrainDerivationCache.Create(world,horizontalScale:.1);var scatter=new LocalScatter(model,.1);
            File.WriteAllText(Path.Combine(output,"worker.json"),JsonSerializer.Serialize(new{Executable=Path.GetFullPath(executable),Assembly=assembly,Surface=version,PreparationSeconds=prepare},new JsonSerializerOptions{WriteIndented=true}));
            var requests=new[]{(0d,0d,102.4,64),(102.4,0d,102.4,64),(-102.4,0d,102.4,64),(0d,102.4,102.4,64),(0d,-102.4,102.4,64),(-1024d,-1024d,409.6,32),(1024d,-1024d,409.6,32),(1024d,1024d,409.6,32),(-1024d,1024d,409.6,32),(-2048d,0d,819.2,32),(2048d,0d,819.2,32),(0d,2048d,819.2,32)};
            if(farTiles)requests=new[]{(-52428.8,-52428.8,52428.8,8),(0d,-52428.8,52428.8,8),(0d,0d,26214.4,8)};
            if(sites.RootElement.TryGetProperty("Requests",out var custom))requests=custom.EnumerateArray().Select(p=>(p.GetProperty("East").GetDouble(),p.GetProperty("North").GetDouble(),p.GetProperty("Size").GetDouble(),p.GetProperty("Segments").GetInt32())).ToArray();
            foreach(var request in requests)
            {
                using var client=new TcpClient{NoDelay=true,ReceiveTimeout=120000};client.Connect(IPAddress.Loopback,port);
                using var stream=client.GetStream();using var incoming=new TerrainReadStream(stream);using var r=new BinaryReader(incoming);using var w=new BinaryWriter(stream);
                watch.Restart();double cpu=child.TotalProcessorTime.TotalMilliseconds;
                w.Write(secret);w.Write((byte)0);TerrainMeshWire.WriteRequest(w,frame,request.Item1,request.Item2,request.Item3,request.Item4,null);w.Flush();
                if(!r.ReadBoolean())throw new Exception("Worker request failed: "+r.ReadString());
                var mesh=TerrainMeshWire.ReadMesh(r);w.Write((byte)0);w.Flush();child.Refresh();watch.Stop();double cpuMs=child.TotalProcessorTime.TotalMilliseconds-cpu;
                string hash=MeshHash(mesh,geometryOnly,surfaceAttributesOnly);int changedRgb=0;double maxRgbDelta=0;double? currentBuildMs=null;
                string bankBoundaryError=null;
                if(boundaryReview)try{WaterCouplingTests.ContainedBoundary(mesh);}catch(Exception error){bankBoundaryError=error.Message;}
                int? currentVertices=null,currentWaterVertices=null;bool? currentMatches=null;
                string[] currentDifferences=Array.Empty<string>();
                if(verifyCurrent||reportCurrent)
                {
                    double step=request.Item3/request.Item4;
                    var expected=LocalTerrainMesh.Build(model,frame,request.Item1,request.Item2,request.Item3,request.Item4,edgeSteps:new[]{step,step,step,step});
                    currentBuildMs=expected.BuildMilliseconds;
                    currentVertices=expected.Positions.Length/3;currentWaterVertices=expected.WaterPositions.Length/3;currentMatches=MeshHash(expected,geometryOnly,surfaceAttributesOnly)==hash;
                    if(currentMatches!=true)currentDifferences=AttributeChanges(mesh,expected);
                    if(geometryOnly)for(int i=0;i<mesh.Colors.Length;i++)if(i%4!=3&&mesh.Colors[i]!=expected.Colors[i]){changedRgb++;maxRgbDelta=Math.Max(maxRgbDelta,Math.Abs(mesh.Colors[i]-expected.Colors[i]));}
                }
                var row=new{Kind="terrain",East=request.Item1,North=request.Item2,Size=request.Item3,Segments=request.Item4,ElapsedMs=watch.Elapsed.TotalMilliseconds,CpuMs=cpuMs,mesh.BuildMilliseconds,ExpectedCurrentBuildMs=currentBuildMs,Vertices=mesh.Positions.Length/3,WaterVertices=mesh.WaterPositions.Length/3,CurrentVertices=currentVertices,CurrentWaterVertices=currentWaterVertices,CurrentPayloadMatches=currentMatches,CurrentDifferences=currentDifferences,BankBoundaryError=bankBoundaryError,PayloadSha256=hash,ExactCurrentPayload=verifyCurrent&&!geometryOnly&&!surfaceAttributesOnly?currentMatches:null,ExactPhysicalPayload=verifyCurrent&&geometryOnly?currentMatches:null,ExactGeometryWaterPayload=verifyCurrent&&surfaceAttributesOnly?currentMatches:null,SurfaceAttributesExcluded=surfaceAttributesOnly,TerrainRgbExcluded=geometryOnly||surfaceAttributesOnly,ChangedRgbComponents=changedRgb,MaxRgbDelta=maxRgbDelta};rows.Add(row);
                File.WriteAllText(Path.Combine(output,"tiles.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
                Console.WriteLine($"{version} tile {request.Item1},{request.Item2}/{request.Item3} n{request.Item4}: {row.ElapsedMs:F1}ms wall / {row.BuildMilliseconds:F1}ms build / {row.CpuMs:F1}ms CPU");
                if(verifyCurrent&&currentMatches!=true)throw new Exception("Packaged/current payload differs at "+request+": "+string.Join("; ",currentDifferences));
            }
            if(!farTiles&&!terrainOnly)foreach(var delta in new[]{(0d,0d),(512d,0d),(0d,512d)})foreach(bool detail in new[]{false,true})
            {
                V position=frame.Position(delta.Item1,delta.Item2);var key=scatter.Address(position,detail);
                var forest=new ForestPageKey(key.Face,(key.X*LocalScatter.TreeCandidateSide/LocalScatter.ForestPageSide)>>forestLevel,(key.Y*LocalScatter.TreeCandidateSide/LocalScatter.ForestPageSide)>>forestLevel,forestLevel);
                foreach(byte command in new byte[]{1,3})
                {
                    if(forestOnly&&command!=3)continue;
                    if(detail&&command==3)continue;
                    using var client=new TcpClient{NoDelay=true,ReceiveTimeout=120000};client.Connect(IPAddress.Loopback,port);
                    using var stream=client.GetStream();using var incoming=new TerrainReadStream(stream);using var r=new BinaryReader(incoming);using var w=new BinaryWriter(stream);
                    watch.Restart();child.Refresh();double cpu=child.TotalProcessorTime.TotalMilliseconds;
                    w.Write(secret);w.Write(command);if(command==1)TerrainMeshWire.WriteScatterKey(w,key);else TerrainMeshWire.WriteForestKey(w,forest);w.Flush();
                    if(!r.ReadBoolean())throw new Exception("Packaged vegetation request failed: "+r.ReadString());
                    ScatterPatch actualScatter=command==1?TerrainMeshWire.ReadScatter(r,key):null;ForestPage actualForest=command==3?TerrainMeshWire.ReadForest(r,forest):null;
                    int count=actualScatter?.Instances.Length??actualForest!.Roots.Length;
                    w.Write((byte)0);w.Flush();child.Refresh();watch.Stop();double cpuMs=child.TotalProcessorTime.TotalMilliseconds-cpu;
                    int? originalProbes=actualForest?.Probes,currentProbes=null;
                    if(command==3&&ignoreForestProbeCount)actualForest!.Probes=0;
                    using var payload=new MemoryStream();using(var encoded=new BinaryWriter(payload,System.Text.Encoding.UTF8,true)){if(command==1)TerrainMeshWire.WriteScatter(encoded,actualScatter!);else TerrainMeshWire.WriteForest(encoded,actualForest!);}
                    string hash=Convert.ToHexString(SHA256.HashData(payload.ToArray())).ToLowerInvariant();
                    if(verifyCurrent&&!geometryOnly&&!surfaceAttributesOnly)
                    {
                        using var expectedPayload=new MemoryStream();using(var encoded=new BinaryWriter(expectedPayload,System.Text.Encoding.UTF8,true)){if(command==1)TerrainMeshWire.WriteScatter(encoded,scatter.Build(key));else {var expectedForest=scatter.BuildForest(forest);currentProbes=expectedForest.Probes;if(ignoreForestProbeCount)expectedForest.Probes=0;TerrainMeshWire.WriteForest(encoded,expectedForest);}}
                        string expected=Convert.ToHexString(SHA256.HashData(expectedPayload.ToArray())).ToLowerInvariant();
                        if(expected!=hash)throw new Exception("Packaged/current complete vegetation payload differs at "+delta+", detail "+detail+", command "+command);
                    }
                    var row=new{Kind=command==1?(detail?"scatter-detail":"scatter"):"forest",East=delta.Item1,North=delta.Item2,ElapsedMs=watch.Elapsed.TotalMilliseconds,CpuMs=cpuMs,Items=count,PayloadSha256=hash,ExactCurrentPayload=(bool?)(verifyCurrent&&!geometryOnly&&!surfaceAttributesOnly&&!(command==3&&ignoreForestProbeCount)?true:null),ExactForestRoots=(bool?)(verifyCurrent&&!geometryOnly&&!surfaceAttributesOnly&&command==3&&ignoreForestProbeCount?true:null),ForestProbeCountExcluded=command==3&&ignoreForestProbeCount,OriginalProbes=originalProbes,CurrentProbes=currentProbes};rows.Add(row);
                    File.WriteAllText(Path.Combine(output,"tiles.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
                    Console.WriteLine($"{version} {row.Kind} {delta}: {row.ElapsedMs:F1}ms wall / {row.CpuMs:F1}ms CPU, {count} items");
                }
            }
            TerrainQueryTiming.Clear();TerrainQueryTiming.Enabled=true;watch.Restart();
            try
            {
                var mesh=LocalTerrainMesh.Build(model,frame,0,0,102.4,64);watch.Stop();
                var phases=Enumerable.Range(0,6).Select(TerrainQueryTiming.Milliseconds).ToArray();
                File.WriteAllText(Path.Combine(output,"local-query-phases.json"),JsonSerializer.Serialize(new{ElapsedMs=watch.Elapsed.TotalMilliseconds,PhasesMs=phases,Vertices=mesh.Positions.Length/3,Note="Inclusive HyperAmplification scopes; complete material/ecology blending and mesh construction also contribute outside these scopes."},new JsonSerializerOptions{WriteIndented=true}));
                Console.WriteLine($"Local tile {watch.Elapsed.TotalMilliseconds:F1}ms; locate/profile/shape/river/detail/pond inclusive ms: "+string.Join(", ",phases.Select(v=>v.ToString("F1"))));
            }
            finally{TerrainQueryTiming.Enabled=false;}
        }
        finally{listener.Stop();if(!child.HasExited){child.Kill(true);child.WaitForExit();}}
    }
}
