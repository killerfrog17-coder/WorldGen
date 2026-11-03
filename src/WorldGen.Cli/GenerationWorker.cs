using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;
static class GenerationWorker
{
    public static async Task Run(int port,int parent,string token)
    {
        _=Task.Run(async()=>{while(true){await Task.Delay(1000);try{if(Process.GetProcessById(parent).HasExited)Environment.Exit(0);}catch{Environment.Exit(0);}}});
        using var client=new TcpClient{NoDelay=true};await client.ConnectAsync(IPAddress.Loopback,port);using var stream=client.GetStream();
        using var reader=new BinaryReader(stream);using var buffer=new BufferedStream(stream,65536);using var writer=new BinaryWriter(buffer);
        writer.Write(token);writer.Write(RegionalWire.Version);writer.Flush();var recipe=WorldJson.ReadRecipe(reader.ReadString());
        var world=new WorldPipeline().Generate(recipe,p=>{writer.Write((byte)0);writer.Write(p.Stage);writer.Write(p.Percent);writer.Flush();},preview:(stage,w)=>
        {
            // Useful progressive previews, bounded to three snapshots rather
            // than repeatedly transferring the same fine mesh at every stage.
            if(stage!="coarse-plates"&&stage!="erosion"&&stage!="climate")return;
            writer.Write((byte)1);RegionalWire.Write(writer,GenerationPreview.Capture(stage,w));writer.Flush();
        },timing:sample=>{writer.Write((byte)2);RegionalWire.Write(writer,sample);writer.Flush();});
        writer.Write((byte)3);RegionalWire.Write(writer,world);writer.Flush();
    }
}
