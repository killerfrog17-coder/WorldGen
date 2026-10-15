using System;
using System.IO;
using System.Threading;
namespace WorldGen.Core
{
    public static partial class StateHash
    {
        [ThreadStatic] static HistoryState ownedHistoryContext;
        internal static string ComputeOwnedHistorical(object value,HistoryState history,CancellationToken token=default)
        {
            var previous=ownedHistoryContext;ownedHistoryContext=history;
            try{return Compute(value,token);}finally{ownedHistoryContext=previous;}
        }
        // Only private, already validated aggregate transactions may reuse these
        // bytes. Public hashing/integrity verification always reads actual fields.
        // A finished court/campaign/abandoned center has no further actor events.
        static byte[] FreezeHistoricalRecord(string type,Action<BinaryWriter> body)
        {
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {writer.Write((byte)1);writer.Write(type);body(writer);return stream.ToArray();}
        }
        static byte[] FreezeHistoricalRecord(object record)
        {using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){Write(writer,record);return stream.ToArray();}}
    }
}
