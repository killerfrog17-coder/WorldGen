using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

// Keep the allocation-heavy subdivision heap out of Unity's stop-the-world
// collector. This is the exact same Core/Presentation sampler, not a substitute
// terrain algorithm. Existing two-lane/near-first scheduling still owns requests.
public sealed class TerrainMeshProcess : IDisposable
{
    sealed class Connection {public int Port;public string Token;}
    readonly CancellationTokenSource lifetime=new CancellationTokenSource();
    readonly Task<Connection> ready;
    readonly object gate=new object();
    IntPtr hostHandle;TcpListener bootstrap;
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    struct StartupInfo
    {
        public int Size;public IntPtr Reserved,Desktop,Title;
        public int X,Y,XSize,YSize,XChars,YChars,Fill,Flags;
        public short Show,ReservedCount;public IntPtr ReservedBytes,Input,Output,Error;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct ProcessInfo {public IntPtr Process,Thread;public uint ProcessId,ThreadId;}
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    static extern bool CreateProcessW(string application,StringBuilder command,IntPtr processAttributes,IntPtr threadAttributes,bool inherit,uint flags,IntPtr environment,string directory,ref StartupInfo startup,out ProcessInfo process);
    [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll")]static extern bool GetExitCodeProcess(IntPtr handle,out uint code);
    [DllImport("kernel32.dll")]static extern bool TerminateProcess(IntPtr handle,uint code);
    public string Diagnostics {get;private set;}="Preparing terrain worker";
    public TerrainMeshProcess(string executable,World world,double scale,string sourcePath)
    {
        int parent=Process.GetCurrentProcess().Id;
        string expectedAssembly=typeof(LocalTerrain).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion??"";
        ready=Task.Run(()=>
        {
            var watch=Stopwatch.StartNew();string secret=Guid.NewGuid().ToString("N");
            try
            {
                bootstrap=new TcpListener(IPAddress.Loopback,0);bootstrap.Start();int port=((IPEndPoint)bootstrap.LocalEndpoint).Port;
                lock(gate)
                {
                    lifetime.Token.ThrowIfCancellationRequested();
                    // IL2CPP's managed Process.Start/CreateProcess wrapper is
                    // not reliable here; use the actual Windows API. This also
                    // gives us an owned handle for precise orphan-free cleanup.
                    var startup=new StartupInfo{Size=Marshal.SizeOf<StartupInfo>(),Flags=1,Show=0};
                    var command=new StringBuilder("\""+executable+"\" --terrain-worker "+port+" --parent "+parent+" --terrain-token "+secret);
                    if(!CreateProcessW(executable,command,IntPtr.Zero,IntPtr.Zero,false,0x08004000,IntPtr.Zero,Path.GetDirectoryName(executable),ref startup,out var info))
                        throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),"Could not launch terrain worker");
                    hostHandle=info.Process;CloseHandle(info.Thread);
                }
                var accept=bootstrap.AcceptTcpClientAsync();if(!accept.Wait(30000,lifetime.Token))throw new TimeoutException("Terrain worker did not connect.");
                using(var accepted=accept.GetAwaiter().GetResult())
                using(var stream=accepted.GetStream())
                using(var r=new BinaryReader(stream,System.Text.Encoding.UTF8,true))
                using(var w=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))
                {
                    accepted.NoDelay=true;accepted.ReceiveTimeout=120000;accepted.SendTimeout=120000;
                    if(r.ReadString()!=secret)throw new InvalidDataException("Terrain worker session mismatch.");
                    // Fresh worlds are reproducible from a tiny recipe. Avoid
                    // serializing the entire Unity world heap during play.
                    // Loaded worlds instead use their literal existing snapshot.
                    w.Write(world.Stages.Last().Hash);w.Write(sourcePath??"@recipe");w.Write(scale);
                    if(string.IsNullOrEmpty(sourcePath))w.Write(WorldJson.Write(world.Recipe));
                    w.Flush();int version=r.ReadInt32();string assembly=r.ReadString(),surface=r.ReadString();int endpoint=r.ReadInt32();
                    if(version!=TerrainMeshWire.Version||surface!=LocalTerrain.Version||expectedAssembly!=""&&assembly!=expectedAssembly)throw new InvalidDataException("Terrain worker and player assemblies differ; rebuild the packaged worker.");
                    Diagnostics="CoreCLR terrain worker ready in "+watch.Elapsed.TotalSeconds.ToString("F2")+"s; "+assembly;
                    return new Connection{Port=endpoint,Token=secret};
                }
            }
            catch{DisposeHost();throw;}
            finally{bootstrap?.Stop();bootstrap=null;}
        },lifetime.Token);
    }
    public LocalTerrainMesh Build(LocalTerrainFrame frame,double east,double north,double size,int segments,CancellationToken token,double[] edges)
    {
        try{
        using(var linked=CancellationTokenSource.CreateLinkedTokenSource(token,lifetime.Token))
        {
            while(!ready.Wait(50,linked.Token)){}var connection=ready.GetAwaiter().GetResult();
            using(var client=new TcpClient{NoDelay=true})
            using(linked.Token.Register(()=>client.Close()))
            {
                client.Connect(IPAddress.Loopback,connection.Port);
                using(var stream=client.GetStream())using(var r=new BinaryReader(stream,System.Text.Encoding.UTF8,true))using(var w=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))
                {
                    w.Write(connection.Token);w.Write((byte)0);TerrainMeshWire.WriteRequest(w,frame,east,north,size,segments,edges);
                    try
                    {
                        if(!r.ReadBoolean())throw new InvalidOperationException("Terrain worker: "+r.ReadString());
                        var mesh=TerrainMeshWire.ReadMesh(r);linked.Token.ThrowIfCancellationRequested();w.Write((byte)0);w.Flush();return mesh;
                    }
                    catch{linked.Token.ThrowIfCancellationRequested();throw;}
                }
            }
        }
        }catch{token.ThrowIfCancellationRequested();throw;}
    }
    public ScatterPatch Scatter(ScatterKey key,CancellationToken token)
    {
        try{
        using(var linked=CancellationTokenSource.CreateLinkedTokenSource(token,lifetime.Token))
        {
            while(!ready.Wait(50,linked.Token)){}var connection=ready.GetAwaiter().GetResult();
            using(var client=new TcpClient{NoDelay=true})using(linked.Token.Register(()=>client.Close()))
            {
                client.Connect(IPAddress.Loopback,connection.Port);
                using(var stream=client.GetStream())using(var r=new BinaryReader(stream,System.Text.Encoding.UTF8,true))using(var w=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))
                {
                    w.Write(connection.Token);w.Write((byte)1);TerrainMeshWire.WriteScatterKey(w,key);
                    if(!r.ReadBoolean())throw new InvalidOperationException("Scatter worker: "+r.ReadString());
                    var patch=TerrainMeshWire.ReadScatter(r,key);linked.Token.ThrowIfCancellationRequested();w.Write((byte)0);w.Flush();return patch;
                }
            }
        }
        }catch{token.ThrowIfCancellationRequested();throw;}
    }
    void DisposeHost(){lock(gate){if(hostHandle!=IntPtr.Zero){if(GetExitCodeProcess(hostHandle,out uint code)&&code==259)TerminateProcess(hostHandle,0);CloseHandle(hostHandle);hostHandle=IntPtr.Zero;}}}
    public void Dispose(){lifetime.Cancel();bootstrap?.Stop();DisposeHost();}
}
