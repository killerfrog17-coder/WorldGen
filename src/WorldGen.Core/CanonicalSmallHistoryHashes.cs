using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
namespace WorldGen.Core
{
    public static partial class StateHash
    {
        // Only known, bounded small records use this allocation path. Long
        // public strings retain the streaming implementation. No model value,
        // digest, writable buffer or cryptographic state is cached between calls.
        static bool SmallHistoricalValue(object value)
        {
            if(value is HistoricalLeader leader)return (long)(leader.Name?.Length??0)+(leader.Legacy?.Length??0)+(leader.Selection?.Length??0)<=4096;
            if(value is HistoricalEvent e)return SmallHistoryEvent(e);
            return value is object[] pair&&pair.Length==2&&pair[0] is string previous&&previous.Length<=128&&pair[1] is HistoricalEvent item&&SmallHistoryEvent(item);
        }
        static bool SmallHistoryEvent(HistoricalEvent e)=>(long)(e.Text?.Length??0)+(e.Kind?.Length??0)<=4096;
        static string ComputeSmallHistorical(object value,CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            using(var memory=new MemoryStream(1024))
            using(var writer=new BinaryWriter(memory,Encoding.UTF8,true))
            using(var sha=CanonicalSha256.Create())
            {
                Write(writer,value);writer.Flush();cancellation.ThrowIfCancellationRequested();
                byte[] digest=sha.ComputeHash(memory.GetBuffer(),0,checked((int)memory.Length));
                return string.Concat(digest.Select(b=>b.ToString("x2")));
            }
        }
    }
}
