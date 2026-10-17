using System;
using System.IO;

namespace WorldGen.Core
{
    public static partial class StateHash
    {
        static void CheckNuclearRecords()=>CheckRecord(typeof(HistoricalNuclearState),"CivilianDeaths:System.Int64","DeliveryMilli:System.Int32","DeterrenceEpisodes:System.Int32","Dismantled:System.Int32","FirstWeaponYear:System.Int32","Id:System.Int32","Inherited:System.Int32","Investment:System.Int64","LastUseYear:System.Int32","LastYear:System.Int32","Produced:System.Int32","ProductionWork:System.Int64","ProgramYear:System.Int32","ReadinessMilli:System.Int32","Stockpile:System.Int32","Transferred:System.Int32","UpkeepPaid:System.Int64","Used:System.Int32");
        static readonly ScalarRecordBlock[] NuclearStateBlocks={new ScalarRecordBlock(typeof(HistoricalNuclearState),"CivilianDeaths","DeliveryMilli","DeterrenceEpisodes","Dismantled","FirstWeaponYear","Id","Inherited","Investment","LastUseYear","LastYear","Produced","ProductionWork","ProgramYear","ReadinessMilli","Stockpile","Transferred","UpkeepPaid","Used")};
        static void NuclearStateBody(BinaryWriter writer,HistoricalNuclearState r,byte[][] scratch)
        {
            var offsets=NuclearStateBlocks[0].Offsets;var bytes=scratch[0];
            PutLong(bytes,offsets[0],r.CivilianDeaths);PutInteger(bytes,offsets[1],r.DeliveryMilli);PutInteger(bytes,offsets[2],r.DeterrenceEpisodes);PutInteger(bytes,offsets[3],r.Dismantled);PutInteger(bytes,offsets[4],r.FirstWeaponYear);PutInteger(bytes,offsets[5],r.Id);PutInteger(bytes,offsets[6],r.Inherited);PutLong(bytes,offsets[7],r.Investment);PutInteger(bytes,offsets[8],r.LastUseYear);PutInteger(bytes,offsets[9],r.LastYear);PutInteger(bytes,offsets[10],r.Produced);PutLong(bytes,offsets[11],r.ProductionWork);PutInteger(bytes,offsets[12],r.ProgramYear);PutInteger(bytes,offsets[13],r.ReadinessMilli);PutInteger(bytes,offsets[14],r.Stockpile);PutInteger(bytes,offsets[15],r.Transferred);PutLong(bytes,offsets[16],r.UpkeepPaid);PutInteger(bytes,offsets[17],r.Used);writer.Write(bytes);
        }
        static bool TryWriteNuclearRecord(BinaryWriter writer,object value,Type type)
        {
            if(type==typeof(HistoricalNuclearState[])){OptionalRecordArray(writer,(HistoricalNuclearState[])value,"WorldGen.Core.HistoricalNuclearState",NuclearStateBlocks,NuclearStateBody);return true;}
            if(type==typeof(HistoricalNuclearState)){NuclearStateBody(writer,(HistoricalNuclearState)value,OptionalScratch(NuclearStateBlocks));return true;}
            return false;
        }
    }
}
