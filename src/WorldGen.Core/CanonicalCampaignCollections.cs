using System;
using System.IO;
namespace WorldGen.Core
{
    public static partial class StateHash
    {
        static void CampaignCollectionBody(BinaryWriter writer,HistoricalCampaignCollection rows)
        {
            var scratch=literalAggregateRecords?null:new byte[][]{(byte[])WideHistoricalCampaignBlock0.Bytes.Clone(),(byte[])WideHistoricalCampaignBlock1.Bytes.Clone(),(byte[])WideHistoricalCampaignBlock2.Bytes.Clone(),(byte[])WideHistoricalCampaignBlock3.Bytes.Clone()};
            for(int i=0;i<rows.Length;i++)
            {
                var row=rows.Peek(i);writer.Write((byte)1);
                if(literalAggregateRecords){Write(writer,row);continue;}
                if(row==null){writer.Write((byte)0);continue;}
                if(ownedHistoryContext!=null&&row.EndedYear>=0)
                {
                    if(row.OwnedCanonicalRecord==null)row.OwnedCanonicalRecord=FreezeHistoricalRecord("WorldGen.Core.HistoricalCampaign",w=>WideHistoricalCampaignBody(w,row,scratch));
                    writer.Write(row.OwnedCanonicalRecord);continue;
                }
                writer.Write((byte)1);writer.Write("WorldGen.Core.HistoricalCampaign");WideHistoricalCampaignBody(writer,row,scratch);
            }
            writer.Write((byte)0);
        }
        static void CampaignTheaterCollectionBody(BinaryWriter writer,HistoricalCampaignTheaterCollection rows)
        {
            var scratch=literalAggregateRecords?null:(byte[])HistoricalCampaignTheaterBlock0.Bytes.Clone();
            for(int i=0;i<rows.Length;i++)
            {
                var row=rows.Peek(i);writer.Write((byte)1);
                if(literalAggregateRecords){Write(writer,row);continue;}
                writer.Write((byte)(row==null?0:1));if(row==null)continue;writer.Write("WorldGen.Core.HistoricalCampaignTheater");
                PutInteger(scratch,HistoricalCampaignTheaterBlock0.Offsets[0],row.Cell);PutInteger(scratch,HistoricalCampaignTheaterBlock0.Offsets[1],row.SupplyA);PutInteger(scratch,HistoricalCampaignTheaterBlock0.Offsets[2],row.SupplyB);writer.Write(scratch);
            }
            writer.Write((byte)0);
        }
        static void CampaignAdvanceCollectionBody(BinaryWriter writer,HistoricalCampaignAdvanceCollection rows)
        {
            var scratch=literalAggregateRecords?null:new byte[][]{(byte[])WideHistoricalCampaignAdvanceBlock0.Bytes.Clone()};
            for(int i=0;i<rows.Length;i++)
            {
                var row=rows.Peek(i);writer.Write((byte)1);
                if(literalAggregateRecords){Write(writer,row);continue;}
                writer.Write((byte)(row==null?0:1));if(row==null)continue;writer.Write("WorldGen.Core.HistoricalCampaignAdvance");WideHistoricalCampaignAdvanceBody(writer,row,scratch);
            }
            writer.Write((byte)0);
        }
        static void MilitaryOperationCollectionBody(BinaryWriter writer,HistoricalMilitaryOperationCollection rows)
        {
            var scratch=literalAggregateRecords?null:OptionalScratch(HistoricalMilitaryOperationOptionalBlocks);
            for(int i=0;i<rows.Length;i++)
            {
                var row=rows.Peek(i);writer.Write((byte)1);
                if(literalAggregateRecords){Write(writer,row);continue;}
                writer.Write((byte)(row==null?0:1));if(row==null)continue;writer.Write("WorldGen.Core.HistoricalMilitaryOperation");HistoricalMilitaryOperationOptionalBody(writer,row,scratch);
            }
            writer.Write((byte)0);
        }
    }
}
