using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using WorldGen.Core;
using WorldGen.Serialization;

// One owned CoreCLR history executor, using the already shipped worker binary.
// The UI receives committed regional deltas, never a second world per step.
public sealed class RegionalHistoryProcess:IDisposable
{
    readonly string executable,source;readonly World world;
    Process process;TcpClient client;TcpListener listener;BinaryReader reader;BinaryWriter writer;PrehistoryState last;
    public double ComputeMilliseconds,TransferMilliseconds,DecodeMilliseconds,ApplyMilliseconds;public long Bytes;public int Publications;
    public void ResetMeters(){ComputeMilliseconds=TransferMilliseconds=DecodeMilliseconds=ApplyMilliseconds=0;Bytes=0;Publications=0;}
    public RegionalHistoryProcess(string executable,World world,string source){this.executable=executable;this.world=world;this.source=source;}
    public void Prepare(PrehistoryState state,CancellationToken token)
    {
        if(client!=null)return;token.ThrowIfCancellationRequested();string secret=Guid.NewGuid().ToString("N");
        listener=new TcpListener(IPAddress.Loopback,0);listener.Start();int port=((IPEndPoint)listener.LocalEndpoint).Port;
        try
        {
            var info=new ProcessStartInfo(executable,"--regional-history-worker "+port+" --parent "+Process.GetCurrentProcess().Id+" --history-token "+secret){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Path.GetDirectoryName(executable)};
            info.EnvironmentVariables["DOTNET_TieredPGO"]="1";
            process=Process.Start(info);var accept=listener.AcceptTcpClientAsync();while(!accept.Wait(50,token))if(process.HasExited)throw new IOException("History worker exited during startup.");
            client=accept.GetAwaiter().GetResult();client.NoDelay=true;client.ReceiveTimeout=180000;client.SendTimeout=180000;
            reader=new BinaryReader(client.GetStream(),Encoding.UTF8,true);writer=new BinaryWriter(client.GetStream(),Encoding.UTF8,true);
            if(reader.ReadString()!=secret||reader.ReadInt32()!=RegionalWire.Version)throw new InvalidDataException("History worker protocol mismatch.");
            writer.Write(source??"@recipe");writer.Write(world.Stages.Last().Hash);if(source==null)writer.Write(WorldJson.Write(world.Recipe));
            RegionalWire.WritePacket(writer,state);writer.Flush();if(!reader.ReadBoolean())throw new IOException("History worker did not prepare.");RegionalWire.ReadPacket<RegionalPublication>(reader).Apply(state);last=state;token.ThrowIfCancellationRequested();
        }
        catch{Dispose();throw;}
        finally{listener?.Stop();listener=null;}
    }
    public PrehistoryState Advance(PrehistoryState state,long seconds,CancellationToken token,Action<PrehistoryState> publication=null,Func<bool> continuing=null)
    {
        Prepare(state,token);using(token.Register(()=>{try{client?.Close();}catch{}}))
        {
            if(!ReferenceEquals(last,state))
            {
                if(PrehistoryState.SameDomain(last,state)&&state.Foragers.Seconds>last.Foragers.Seconds){writer.Write((byte)2);writer.Write(state.Foragers.Seconds-last.Foragers.Seconds);}
                else{writer.Write((byte)1);RegionalWire.WritePacket(writer,state);}writer.Flush();if(!reader.ReadBoolean())throw new IOException("History rebase failed.");
            }
            writer.Write((byte)0);writer.Write(Math.Min(seconds,10000*SimulationTime.Year));writer.Flush();
            while(true)
            {
                ComputeMilliseconds+=reader.ReadDouble();bool more=reader.ReadBoolean(),again=false;
                var patch=RegionalWire.ReadPacket<RegionalPublication>(reader,out int bytes,out double transfer,out double decode,()=>{again=more&&(continuing?.Invoke()??false);if(more){writer.Write((byte)(again?0:1));writer.Flush();}});token.ThrowIfCancellationRequested();
                Bytes+=bytes;TransferMilliseconds+=transfer;DecodeMilliseconds+=decode;Publications++;var timer=Stopwatch.StartNew();last=patch.Apply(state);ApplyMilliseconds+=timer.Elapsed.TotalMilliseconds;publication?.Invoke(last);state=last;if(!again)return last;
            }
        }
    }
    public void Dispose()
    {
        try{client?.Close();}catch{}client=null;try{listener?.Stop();}catch{}listener=null;
        try{if(process!=null&&!process.HasExited)process.Kill();}catch{}process?.Dispose();process=null;
    }
}
