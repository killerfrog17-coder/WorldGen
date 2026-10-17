using System;
using System.IO;

namespace WorldGen.Core
{
    public static partial class StateHash
    {
        static void CheckGeographicRecords()
        {
            CheckRecord(typeof(HistoricalGeographicMap),"FailedVoyages:System.Int32","Grade:WorldGen.Core.HistoricalCellTree","Host:WorldGen.Core.HistoricalCellTree","Landfalls:System.Int32","Missions:WorldGen.Core.HistoricalSeaMission[]","NextPlan:System.Int64","Opportunity:WorldGen.Core.HistoricalCellTree","Places:WorldGen.Core.HistoricalLocalBag","ReportedYear:WorldGen.Core.HistoricalCellTree","ReviewAfter:WorldGen.Core.HistoricalCellTree","ScheduledDue:System.Int64","Sharing:WorldGen.Core.HistoricalGeographicSharing","Shore:WorldGen.Core.HistoricalCellTree","Surveys:System.Int32","Voyages:System.Int32");
            CheckRecord(typeof(HistoricalGeographicSharing),"CityCursor:System.Int32","ContactCursor:System.Int32","PeerCursor:System.Int32","RecentRegion:System.Int32","ReportCursor:System.Int32");
        }
        static readonly ScalarRecordBlock[] GeographicMapBlocks={
            new ScalarRecordBlock(typeof(HistoricalGeographicMap),"FailedVoyages"),
            new ScalarRecordBlock(typeof(HistoricalGeographicMap),"Landfalls"),
            new ScalarRecordBlock(typeof(HistoricalGeographicMap),"NextPlan"),
            new ScalarRecordBlock(typeof(HistoricalGeographicMap),"ScheduledDue"),
            new ScalarRecordBlock(typeof(HistoricalGeographicMap),"Surveys","Voyages"),
            new ScalarRecordBlock(typeof(HistoricalGeographicSharing),"CityCursor","ContactCursor","PeerCursor","RecentRegion","ReportCursor")};
        static void GeographicSharingBody(BinaryWriter writer,HistoricalGeographicSharing record,byte[] scratch)
        {
            var offsets=GeographicMapBlocks[5].Offsets;
            PutInteger(scratch,offsets[0],record.CityCursor);PutInteger(scratch,offsets[1],record.ContactCursor);PutInteger(scratch,offsets[2],record.PeerCursor);PutInteger(scratch,offsets[3],record.RecentRegion);PutInteger(scratch,offsets[4],record.ReportCursor);writer.Write(scratch);
        }
        static void GeographicMapBody(BinaryWriter writer,HistoricalGeographicMap record,byte[][] scratch)
        {
            PutInteger(scratch[0],GeographicMapBlocks[0].Offsets[0],record.FailedVoyages);writer.Write(scratch[0]);
            writer.Write("Grade");Write(writer,record.Grade);writer.Write("Host");Write(writer,record.Host);
            PutInteger(scratch[1],GeographicMapBlocks[1].Offsets[0],record.Landfalls);writer.Write(scratch[1]);
            writer.Write("Missions");Write(writer,record.Missions);
            PutLong(scratch[2],GeographicMapBlocks[2].Offsets[0],record.NextPlan);writer.Write(scratch[2]);
            writer.Write("Opportunity");Write(writer,record.Opportunity);writer.Write("Places");Write(writer,record.Places);
            writer.Write("ReportedYear");Write(writer,record.ReportedYear);writer.Write("ReviewAfter");Write(writer,record.ReviewAfter);
            PutLong(scratch[3],GeographicMapBlocks[3].Offsets[0],record.ScheduledDue);writer.Write(scratch[3]);
            if(record.Sharing!=null)
            {writer.Write("Sharing");writer.Write((byte)1);writer.Write("WorldGen.Core.HistoricalGeographicSharing");GeographicSharingBody(writer,record.Sharing,scratch[5]);}
            writer.Write("Shore");Write(writer,record.Shore);
            PutInteger(scratch[4],GeographicMapBlocks[4].Offsets[0],record.Surveys);PutInteger(scratch[4],GeographicMapBlocks[4].Offsets[1],record.Voyages);writer.Write(scratch[4]);
        }
        static bool TryWriteGeographicRecord(BinaryWriter writer,object value,Type type)
        {
            if(type==typeof(HistoricalGeographicMap[]))
            {OptionalRecordArray(writer,(HistoricalGeographicMap[])value,"WorldGen.Core.HistoricalGeographicMap",GeographicMapBlocks,GeographicMapBody);return true;}
            if(type==typeof(HistoricalGeographicMap)){GeographicMapBody(writer,(HistoricalGeographicMap)value,OptionalScratch(GeographicMapBlocks));return true;}
            if(type==typeof(HistoricalGeographicSharing)){GeographicSharingBody(writer,(HistoricalGeographicSharing)value,(byte[])GeographicMapBlocks[5].Bytes.Clone());return true;}
            return false;
        }
    }
}
