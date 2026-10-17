using System;
using System.IO;

namespace WorldGen.Core
{
    public static partial class StateHash
    {
        static void CheckServiceRecords()
        {
            CheckRecord(typeof(HistoricalTerritorialAmbition),"AnchorCell:System.Int32","Landmass:System.Int32","Region:System.Int32","Scope:WorldGen.Core.HistoricalAmbitionScope","SinceYear:System.Int32");
            CheckRecord(typeof(HistoricalSeaServicePort),"Capacity:System.Int64","CargoDayCredit:System.Int64","Cell:System.Int32","CharterSpending:System.Int64","ConstructionSpending:System.Int64","CrewFood:System.Int64","ExpansionCredit:System.Int64","Key:System.Int32","LastSeconds:System.Int64","OwnerId:System.Int32","TownId:System.Int32","WorkSpent:System.Int64");
            CheckRecord(typeof(HistoricalColonialGovernment),"LastExpeditionYear:System.Int32","Sponsor:System.Int32","Titles:WorldGen.Core.HistoricalLocalBag");
        }
        static readonly ScalarRecordBlock[] AmbitionBlocks={new ScalarRecordBlock(typeof(HistoricalTerritorialAmbition),"AnchorCell","Landmass","Region","Scope","SinceYear")};
        static readonly ScalarRecordBlock[] SeaPortBlocks={new ScalarRecordBlock(typeof(HistoricalSeaServicePort),"Capacity","CargoDayCredit","Cell","CharterSpending","ConstructionSpending","CrewFood","ExpansionCredit","Key","LastSeconds","OwnerId","TownId","WorkSpent")};
        static readonly ScalarRecordBlock[] ColonialGovernmentBlocks={new ScalarRecordBlock(typeof(HistoricalColonialGovernment),"LastExpeditionYear","Sponsor")};
        static void AmbitionBody(BinaryWriter writer,HistoricalTerritorialAmbition r,byte[][] scratch)
        {
            var offsets=AmbitionBlocks[0].Offsets;var bytes=scratch[0];
            PutInteger(bytes,offsets[0],r.AnchorCell);PutInteger(bytes,offsets[1],r.Landmass);PutInteger(bytes,offsets[2],r.Region);PutInteger(bytes,offsets[3],(int)r.Scope);PutInteger(bytes,offsets[4],r.SinceYear);writer.Write(bytes);
        }
        static void SeaPortBody(BinaryWriter writer,HistoricalSeaServicePort r,byte[][] scratch)
        {
            var offsets=SeaPortBlocks[0].Offsets;var bytes=scratch[0];
            PutLong(bytes,offsets[0],r.Capacity);PutLong(bytes,offsets[1],r.CargoDayCredit);PutInteger(bytes,offsets[2],r.Cell);PutLong(bytes,offsets[3],r.CharterSpending);PutLong(bytes,offsets[4],r.ConstructionSpending);PutLong(bytes,offsets[5],r.CrewFood);PutLong(bytes,offsets[6],r.ExpansionCredit);PutInteger(bytes,offsets[7],r.Key);PutLong(bytes,offsets[8],r.LastSeconds);PutInteger(bytes,offsets[9],r.OwnerId);PutInteger(bytes,offsets[10],r.TownId);PutLong(bytes,offsets[11],r.WorkSpent);writer.Write(bytes);
        }
        static void ColonialGovernmentBody(BinaryWriter writer,HistoricalColonialGovernment r,byte[][] scratch)
        {
            var offsets=ColonialGovernmentBlocks[0].Offsets;var bytes=scratch[0];PutInteger(bytes,offsets[0],r.LastExpeditionYear);PutInteger(bytes,offsets[1],r.Sponsor);writer.Write(bytes);writer.Write("Titles");Write(writer,r.Titles);
        }
        static bool TryWriteServiceRecord(BinaryWriter writer,object value,Type type)
        {
            if(type==typeof(HistoricalTerritorialAmbition[])){OptionalRecordArray(writer,(HistoricalTerritorialAmbition[])value,"WorldGen.Core.HistoricalTerritorialAmbition",AmbitionBlocks,AmbitionBody);return true;}
            if(type==typeof(HistoricalSeaServicePort[])){OptionalRecordArray(writer,(HistoricalSeaServicePort[])value,"WorldGen.Core.HistoricalSeaServicePort",SeaPortBlocks,SeaPortBody);return true;}
            if(type==typeof(HistoricalColonialGovernment[])){OptionalRecordArray(writer,(HistoricalColonialGovernment[])value,"WorldGen.Core.HistoricalColonialGovernment",ColonialGovernmentBlocks,ColonialGovernmentBody);return true;}
            if(type==typeof(HistoricalTerritorialAmbition)){AmbitionBody(writer,(HistoricalTerritorialAmbition)value,OptionalScratch(AmbitionBlocks));return true;}
            if(type==typeof(HistoricalSeaServicePort)){SeaPortBody(writer,(HistoricalSeaServicePort)value,OptionalScratch(SeaPortBlocks));return true;}
            if(type==typeof(HistoricalColonialGovernment)){ColonialGovernmentBody(writer,(HistoricalColonialGovernment)value,OptionalScratch(ColonialGovernmentBlocks));return true;}
            return false;
        }
    }
}
