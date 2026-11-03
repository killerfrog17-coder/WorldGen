using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using WorldGen.Core;
using WorldGen.Serialization;

static class RegionalHistoryWorker
{
    public static async Task Run(int port,int parent,string token)
    {
        _=Task.Run(async()=>{while(true){await Task.Delay(1000);try{if(Process.GetProcessById(parent).HasExited)Environment.Exit(0);}catch{Environment.Exit(0);}}});
        using var client=new TcpClient{NoDelay=true};await client.ConnectAsync(IPAddress.Loopback,port);
        using var stream=client.GetStream();using var r=new BinaryReader(stream);using var w=new BinaryWriter(stream);
        w.Write(token);w.Write(RegionalWire.Version);w.Flush();
        try
        {
            string source=r.ReadString(),expected=r.ReadString();
            var world=source=="@recipe"?new WorldPipeline().Generate(WorldJson.ReadRecipe(r.ReadString())):WorldFile.ReadPlanetaryFile(source);
            if(world.Stages.Last().Hash!=expected)throw new InvalidDataException("History worker received a different planet.");
            var engine=new Prehistory(world);PrehistoryState state=ReadState();var session=new SimulationSession(engine,state);w.Write(true);RegionalWire.WritePacket(w,RegionalPublication.Between(state,state));w.Flush();
            while(true)
            {
                byte command=r.ReadByte();
                if(command==1){state=ReadState();session=new SimulationSession(engine,state);w.Write(true);w.Flush();continue;}
                if(command==2){state=session.AdvanceSeconds(r.ReadInt64(),int.MaxValue);w.Write(true);w.Flush();continue;}
                if(command!=0)throw new InvalidDataException("Unknown history command.");
                long seconds=r.ReadInt64();var before=state;var timer=Stopwatch.StartNew();state=session.AdvanceSeconds(seconds,int.MaxValue);
                var patch=RegionalPublication.Between(before,state);w.Write(timer.Elapsed.TotalMilliseconds);RegionalWire.WritePacket(w,patch);w.Flush();
            }
            PrehistoryState ReadState(){var value=RegionalWire.ReadPacket<PrehistoryState>(r);value.Hash=null;return value;}
        }
        catch(Exception ex){Console.Error.WriteLine(ex);throw;}
    }
}
