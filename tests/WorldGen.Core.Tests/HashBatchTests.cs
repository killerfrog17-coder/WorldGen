using System.Security.Cryptography;
using System.Text;
using WorldGen.Core;

public static class HashBatchTests
{
    public static void Run(Action<bool,string> check)
    {
        foreach(int count in new[]{0,1,127,128,2047,2048,2049,8193})
        {
            int[] values=Enumerable.Range(0,count).Select(i=>i%4==0?int.MinValue:i%4==1?int.MaxValue:i%4==2?-i:i).ToArray();
            using var stream=new MemoryStream();
            using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
            {
                writer.Write((byte)1);writer.Write("System.Int32[]");
                foreach(int value in values){writer.Write((byte)1);writer.Write((byte)1);writer.Write("System.Int32");writer.Write(value);}
                writer.Write((byte)0);
            }
            string expected=Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
            check(StateHash.Compute(values)==expected,"Canonical integer hash bytes at length "+count);
        }
        var archive=new List<HistoricalEvent>{null!,new HistoricalEvent{Id=int.MaxValue,Cell=-1,Year=12345,Category=ChronicleCategory.Discoveries,Kind="échange",Text="Unicode: 水 / Ω; a complete record.",PolityId=17},new HistoricalEvent{Id=2,Text=null!}};
        using var events=new MemoryStream();
        using(var writer=new BinaryWriter(events,Encoding.UTF8,true))
        {
            writer.Write((byte)1);writer.Write("System.Collections.Generic.List<WorldGen.Core.HistoricalEvent>");
            foreach(var record in archive)
            {
                writer.Write((byte)1);writer.Write((byte)(record==null?0:1));if(record==null)continue;
                writer.Write(typeof(HistoricalEvent).FullName!);
                foreach(var field in typeof(HistoricalEvent).GetFields().OrderBy(f=>f.Name,StringComparer.Ordinal))
                {
                    writer.Write(field.Name);var value=field.GetValue(record);writer.Write((byte)(value==null?0:1));if(value==null)continue;
                    writer.Write(value.GetType().FullName!);if(value is string text)writer.Write(text);else writer.Write(Convert.ToInt32(value));
                }
            }
            writer.Write((byte)0);
        }
        check(StateHash.Compute(archive)==Convert.ToHexString(SHA256.HashData(events.ToArray())).ToLowerInvariant(),"Archive template matches independent reflection bytes, nulls and Unicode");
        var leaders=new HistoricalLeader[]{null!,new HistoricalLeader{Id=int.MinValue,Name="水 / Ω",Legacy=null!,Selection=null!},new HistoricalLeader{Id=int.MaxValue,EndedYear=-1,PredecessorId=0,Selection="Council appointment",Military=843,StartedYear=123456,Legacy="A recorded legacy"}};
        using var leaderBytes=new MemoryStream();
        using(var writer=new BinaryWriter(leaderBytes,Encoding.UTF8,true))
        {
            writer.Write((byte)1);writer.Write("WorldGen.Core.HistoricalLeader[]");
            foreach(var leader in leaders)
            {
                writer.Write((byte)1);writer.Write((byte)(leader==null?0:1));if(leader==null)continue;writer.Write(typeof(HistoricalLeader).FullName!);
                foreach(var field in typeof(HistoricalLeader).GetFields().OrderBy(f=>f.Name,StringComparer.Ordinal))
                {
                    if(field.Name==nameof(HistoricalLeader.PredecessorId)&&leader.PredecessorId==-1||field.Name==nameof(HistoricalLeader.Selection)&&string.IsNullOrEmpty(leader.Selection))continue;
                    writer.Write(field.Name);var value=field.GetValue(leader);writer.Write((byte)(value==null?0:1));if(value==null)continue;
                    writer.Write(value.GetType().FullName!);if(value is string text)writer.Write(text);else writer.Write((int)value);
                }
            }
            writer.Write((byte)0);
        }
        check(StateHash.Compute(leaders)==Convert.ToHexString(SHA256.HashData(leaderBytes.ToArray())).ToLowerInvariant(),"Leader blocks preserve independent canonical reflection bytes, optional fields, nulls, signed integers and Unicode");
        string originalLeaderHash=StateHash.Compute(leaders);leaders[2].Military++;check(StateHash.Compute(leaders)!=originalLeaderHash,"Changing an old leader remains detectable: no trusted historical-value cache");
        if(OperatingSystem.IsWindows())
        {
            var native=typeof(StateHash).Assembly.GetType("WorldGen.Core.CanonicalSha256+WindowsHash")!;
            using var sha=(SHA256)Activator.CreateInstance(native,true)!;
            foreach(int count in new[]{0,3,55,56,63,64,65,65537,1048576})
            {
                byte[] data=Enumerable.Range(0,count).Select(i=>(byte)(i*17)).ToArray();byte[] expected=SHA256.HashData(data);
                check(sha.ComputeHash(data).SequenceEqual(expected),"Windows provider standard digest and reuse at "+count);
                byte[] padded=new byte[count+14];data.CopyTo(padded,7);
                sha.Initialize();int split=count/2;sha.TransformBlock(padded,7,split,null,0);sha.TransformFinalBlock(padded,7+split,count-split);
                check(sha.Hash!.SequenceEqual(expected),"Windows provider segmented, offset digest at "+count);
            }
        }
    }
}
