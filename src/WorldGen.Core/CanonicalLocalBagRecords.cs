using System;
using System.IO;
namespace WorldGen.Core
{
    public static partial class StateHash
    {
        static void CheckLocalBagRecords()
        {
            CheckRecord(typeof(HistoricalLocalBag),"Count:System.Int32","Items:WorldGen.Core.HistoricalCellTree","Positions:WorldGen.Core.HistoricalCellTree");
            CheckRecord(typeof(HistoricalPendingPortGovernment),"Cursor:System.Int32","Pending:WorldGen.Core.HistoricalLocalBag");
        }
        static byte[] CanonicalLabel(string label)
        {using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){writer.Write(label);return stream.ToArray();}}
        static byte[] LocalScalarHead(Type type,string field)
        {using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){writer.Write((byte)1);writer.Write(type.FullName);writer.Write(new ScalarRecordBlock(type,field).Bytes);return stream.ToArray();}}
        static readonly byte[] LocalBagHead=LocalScalarHead(typeof(HistoricalLocalBag),"Count"),PendingPortHead=LocalScalarHead(typeof(HistoricalPendingPortGovernment),"Cursor");
        static readonly byte[] LocalItemsLabel=CanonicalLabel("Items"),LocalPositionsLabel=CanonicalLabel("Positions"),LocalPendingLabel=CanonicalLabel("Pending"),PendingPortArrayHead=LocalArrayHead();
        static byte[] LocalArrayHead(){using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){writer.Write((byte)1);writer.Write(typeof(HistoricalPendingPortGovernment[]).FullName);return stream.ToArray();}}
        [ThreadStatic] static byte[] localBagHeadScratch,pendingPortHeadScratch;
        static void LocalBagRecord(BinaryWriter writer,HistoricalLocalBag record)
        {
            // Only immutable labels and reusable scalar scratch are retained.
            // Every count, tree and public edit is read live on every hash.
            var head=localBagHeadScratch??(localBagHeadScratch=(byte[])LocalBagHead.Clone());PutInteger(head,head.Length-4,record.Count);writer.Write(head);
            writer.Write(LocalItemsLabel);Write(writer,record.Items);writer.Write(LocalPositionsLabel);Write(writer,record.Positions);
        }
        static void PendingPortRecord(BinaryWriter writer,HistoricalPendingPortGovernment record)
        {
            var head=pendingPortHeadScratch??(pendingPortHeadScratch=(byte[])PendingPortHead.Clone());PutInteger(head,head.Length-4,record.Cursor);writer.Write(head);
            writer.Write(LocalPendingLabel);Write(writer,record.Pending);
        }
        static void PendingPortArray(BinaryWriter writer,HistoricalPendingPortGovernment[] records)
        {
            writer.Write(PendingPortArrayHead);
            foreach(var record in records){writer.Write((byte)1);if(record==null)writer.Write((byte)0);else PendingPortRecord(writer,record);}writer.Write((byte)0);
        }
    }
}
