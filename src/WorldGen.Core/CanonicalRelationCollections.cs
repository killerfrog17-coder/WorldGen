using System;
using System.IO;
namespace WorldGen.Core
{
    public static partial class StateHash
    {
        static void RelationCollectionBody(BinaryWriter writer,HistoricalRelationCollection records)
        {
            var scratch=literalAggregateRecords?null:new byte[][]{(byte[])WideHistoricalRelationBlock0.Bytes.Clone()};
            for(int i=0;i<records.Length;i++)
            {
                var record=records.Peek(i);writer.Write((byte)1);
                if(literalAggregateRecords){Write(writer,record);continue;}
                // Only a private transaction may reuse a record's bytes. The
                // collection invalidates them on every mutable access, and
                // detaches/invalidate exposed records when forking. Public
                // integrity hashing always reads all actual fields, including
                // edits made through a reference retained before a prior hash.
                if(record!=null&&ownedHistoryContext!=null)
                {
                    if(record.OwnedCanonicalRecord==null)
                        record.OwnedCanonicalRecord=FreezeHistoricalRecord("WorldGen.Core.HistoricalRelation",w=>WideHistoricalRelationBody(w,record,scratch));
                    writer.Write(record.OwnedCanonicalRecord);continue;
                }
                writer.Write((byte)(record==null?0:1));if(record==null)continue;
                writer.Write("WorldGen.Core.HistoricalRelation");WideHistoricalRelationBody(writer,record,scratch);
            }
            writer.Write((byte)0);
        }
        static void RelationCampaignCollectionBody(BinaryWriter writer,HistoricalRelationCampaigns records)
        {
            if(literalAggregateRecords){foreach(int value in records){writer.Write((byte)1);Integer(writer,value);}writer.Write((byte)0);return;}
            const int stride=19,batch=2048;
            var block=integerArrayScratch;
            if(block==null)
            {
                block=new byte[stride*batch];byte[] prefix={1,1,12,83,121,115,116,101,109,46,73,110,116,51,50};
                for(int at=0;at<block.Length;at+=stride)Buffer.BlockCopy(prefix,0,block,at,prefix.Length);
                integerArrayScratch=block;
            }
            for(int first=0;first<records.Length;first+=batch)
            {
                int count=Math.Min(batch,records.Length-first);
                for(int i=0;i<count;i++){int value=records[first+i],at=i*stride+15;block[at]=(byte)value;block[at+1]=(byte)(value>>8);block[at+2]=(byte)(value>>16);block[at+3]=(byte)(value>>24);}
                writer.Write(block,0,count*stride);
            }
            writer.Write((byte)0);
        }
    }
}
