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
            var engine=new Prehistory(world);PrehistoryState state=ReadState();engine.Validate(state);
            // Compile the calculation path during preparation, not the first
            // user-visible publication. Counter-based randomness and a deep
            // private copy leave the authoritative starting state untouched.
            var warm=state.Copy();warm.Started=true;if(warm.Foragers.Seconds+10000*SimulationTime.Year<SimulationTime.Maximum)engine.AdvanceRegionalExclusive(warm,10000*SimulationTime.Year);
            w.Write(true);RegionalWire.WritePacket(w,RegionalPublication.Between(state,state));w.Flush();
            while(true)
            {
                byte command=r.ReadByte();
                if(command==1){state=ReadState();engine.Validate(state);w.Write(true);w.Flush();continue;}
                if(command==2){engine.AdvanceRegionalExclusive(state,r.ReadInt64());w.Write(true);w.Flush();continue;}
                if(command!=0)throw new InvalidDataException("Unknown history command.");
                long target=state.Foragers.Seconds+r.ReadInt64();
                while(state.Foragers.Seconds<target)
                {
                    var timer=Stopwatch.StartNew();var patch=engine.AdvanceRegionalExclusive(state,Math.Min(target-state.Foragers.Seconds,4000*SimulationTime.Year),maximumMilliseconds:140);
                    bool more=state.Foragers.Seconds<target;w.Write(timer.Elapsed.TotalMilliseconds);w.Write(more);RegionalWire.WritePacket(w,patch);w.Flush();
                    // The receiver acknowledges the complete bytes before it
                    // decodes them. Compute can overlap decoding without ever
                    // mutating a publication that is still being serialized.
                    if(!more||r.ReadByte()!=0)break;
                }
            }
            PrehistoryState ReadState(){var value=RegionalWire.ReadPacket<PrehistoryState>(r);value.Hash=null;return value;}
        }
        catch(Exception ex){Console.Error.WriteLine(ex);throw;}
    }
}
