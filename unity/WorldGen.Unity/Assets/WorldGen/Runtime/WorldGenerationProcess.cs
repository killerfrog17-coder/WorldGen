using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using WorldGen.Core;
using WorldGen.Serialization;
public static class WorldGenerationProcess
{
    public static World Generate(string executable,Recipe recipe,Action<GenerationProgress> progress,Action<World> preview,Action<GenerationStageTiming> timing,CancellationToken token)
    {
        var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();string secret=Guid.NewGuid().ToString("N");int port=((IPEndPoint)listener.LocalEndpoint).Port;
        using var process=Process.Start(new ProcessStartInfo(executable,"--generation-worker "+port+" --parent "+Process.GetCurrentProcess().Id+" --generation-token "+secret){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Path.GetDirectoryName(executable)});
        TcpClient client=null;using var cancellation=token.Register(()=>{try{client?.Close();}catch{}try{if(!process.HasExited)process.Kill();}catch{}});
        try
        {
            var accept=listener.AcceptTcpClientAsync();while(!accept.Wait(50,token))if(process.HasExited)throw new IOException("Generation worker exited during startup.");
            client=accept.GetAwaiter().GetResult();client.NoDelay=true;client.ReceiveTimeout=180000;client.SendTimeout=180000;using var stream=client.GetStream();
            using var buffer=new BufferedStream(stream,65536);using var reader=new BinaryReader(buffer);using var writer=new BinaryWriter(stream);
            if(reader.ReadString()!=secret||reader.ReadInt32()!=RegionalWire.Version)throw new InvalidDataException("Generation worker protocol mismatch.");writer.Write(WorldJson.Write(recipe));writer.Flush();
            while(true)
            {
                token.ThrowIfCancellationRequested();int message=reader.ReadByte();
                if(message==0){progress?.Invoke(new GenerationProgress(reader.ReadString(),reader.ReadInt32()));continue;}
                if(message==1){var snapshot=RegionalWire.Read<World>(reader);preview?.Invoke(snapshot);continue;}
                if(message==2){var sample=RegionalWire.Read<GenerationStageTiming>(reader);timing?.Invoke(sample);continue;}
                if(message==3){var world=RegionalWire.Read<World>(reader);token.ThrowIfCancellationRequested();return world;}
                throw new InvalidDataException("Invalid generation worker message.");
            }
        }
        finally{client?.Dispose();listener.Stop();try{if(!process.HasExited)process.Kill();}catch{}}
    }
}
