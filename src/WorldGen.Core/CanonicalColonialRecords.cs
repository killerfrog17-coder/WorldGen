using System.IO;
namespace WorldGen.Core
{
    public static partial class StateHash
    {
        static readonly ScalarRecordBlock CoastalChartBlock=new ScalarRecordBlock(typeof(HistoricalCoastChart),"Region","SourceCulture","Year");
        static void HistoricalCoastChartArrayBody(BinaryWriter writer,HistoricalCoastChart[] records)
        {
            var block=(byte[])CoastalChartBlock.Bytes.Clone();
            foreach(var record in records)
            {
                writer.Write((byte)1);writer.Write((byte)(record==null?0:1));if(record==null)continue;
                writer.Write("WorldGen.Core.HistoricalCoastChart");PutInteger(block,CoastalChartBlock.Offsets[0],record.Region);PutInteger(block,CoastalChartBlock.Offsets[1],record.SourceCulture);PutInteger(block,CoastalChartBlock.Offsets[2],record.Year);writer.Write(block);
            }
            writer.Write((byte)0);
        }
    }
}
