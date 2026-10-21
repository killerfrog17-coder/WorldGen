using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class TerrainWorkerTests
{
    public static void Run(string path,string executable,bool recipe=false)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var terrain=new LocalTerrain(world,horizontalScale:.1);
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
                using var stream=client.GetStream();using var r=new BinaryReader(stream);using var w=new BinaryWriter(stream);
                w.Write(secret);w.Write((byte)0);TerrainMeshWire.WriteRequest(w,frame,0,0,size,32,null);
                if(!r.ReadBoolean())throw new Exception("Worker rejected valid request.");
                var actual=TerrainMeshWire.ReadMesh(r);w.Write((byte)0);w.Flush();var expected=LocalTerrainMesh.Build(terrain,frame,0,0,size,32);
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
                using var client=new TcpClient{NoDelay=true};client.Connect(IPAddress.Loopback,port);using var stream=client.GetStream();using var r=new BinaryReader(stream);using var w=new BinaryWriter(stream);
                w.Write(secret);w.Write((byte)1);TerrainMeshWire.WriteScatterKey(w,key);if(!r.ReadBoolean())throw new Exception("Scatter worker rejected request.");var actual=TerrainMeshWire.ReadScatter(r,key);w.Write((byte)0);w.Flush();
                if(!actual.Instances.SequenceEqual(expected.Instances)||!actual.Center.Equals(expected.Center))throw new Exception("Worker changed scatter identities, positions, normals, sizes or colours.");
                count+=actual.Instances.Length;Console.WriteLine("Exact scatter "+key.Detail+": "+actual.Instances.Length+" instances");
            }
            // A disconnected request must not poison the next connection.
            using(var client=new TcpClient())
            {client.Connect(IPAddress.Loopback,port);using var w=new BinaryWriter(client.GetStream());w.Write(secret);w.Write((byte)0);var frame=new LocalTerrainFrame(TerrainMapViewport.FromDegrees(27.98,86.92),terrain.RadiusM,0,.1);TerrainMeshWire.WriteRequest(w,frame,10000,10000,3276.8,128,null);w.Write((byte)1);w.Flush();}
            Console.WriteLine("PASS "+count+" exact worker mesh floats, terrain/water indices, bounds and cancellation submission");
        }
        finally{listener.Stop();if(!child.HasExited)child.Kill();child.WaitForExit();}
    }
}
