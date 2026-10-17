using System;
using System.IO;
namespace WorldGen.Core
{
    public static partial class StateHash
    {
        // Immutable labels and per-call scalar scratch only. Every record value
        // is read live, including closed offices/operations and public edits.
        static void CheckOptionalHistoricalRecords()
        {
            CheckRecord(typeof(HistoricalAdministrativePost),"AnnualCost:System.Int64","AreaKm2:System.Int64","AutonomyMilli:System.Int32","Cell:System.Int32","ClosedYear:System.Int32","Depth:System.Int32","FoundedYear:System.Int32","Founders:System.Int32","Id:System.Int32","LegDistanceM:System.Int32","Maritime:System.Boolean","MessageEffortKm:System.Double","Name:System.String","ParentSource:System.Int32","PolityId:System.Int32");
            CheckRecord(typeof(HistoricalImperialGovernment),"AdministrativePostCost:System.Int64","AdministrativePosts:WorldGen.Core.HistoricalLocalBag","AnnualTribute:System.Int64","ArrearsYears:System.Int32","AutonomyMilli:System.Int32","ChildrenByHeight:System.Int32[]","Height:System.Int32","Imposed:System.Boolean","LastPatronSuccession:System.Int32","LastReportYear:System.Int32","Patron:System.Int32","ReportedAreaKm2:System.Int64","ReportedArmy:System.Int64","ReportedPopulation:System.Int64","SinceYear:System.Int32","SubjectAreaKm2:System.Int64","SubjectArmy:System.Int64","SubjectCount:System.Int32","SubjectPopulation:System.Int64","TotalTribute:System.Int64");
            CheckRecord(typeof(HistoricalMilitaryGovernment),"Deaths:System.Int64","FieldPayroll:System.Int64","FieldProvisions:System.Int64","MobilizationEpoch:System.Int32","RecruitmentSpending:System.Int64","Recruits:System.Int64","Reserved:System.Int32");
            CheckRecord(typeof(HistoricalMilitaryOperation),"Closed:System.Boolean","CommittedA:System.Int32","CommittedB:System.Int32","CorePressureA:System.Int32","CorePressureB:System.Int32","EpochA:System.Int32","EpochB:System.Int32","IdleYears:System.Int32","MarchDaysA:System.Int32","MarchDaysB:System.Int32","OriginalCapitalA:System.Int32","OriginalCapitalB:System.Int32","PartyA:System.Int32","PartyB:System.Int32","SiegeBesieger:System.Int32","SiegeDays:System.Int32","SiegeTarget:System.Int32","VictoriesA:System.Int32","VictoriesB:System.Int32");
            CheckRecord(typeof(HistoricalRecognizedTitle),"AreaKm2:System.Int64","Basis:System.String","EstablishedYear:System.Int32","Holder:System.Int32","Id:System.Int32","LastPrice:System.Int64","LastTransferYear:System.Int32","PreviousHolder:System.Int32","Recognizer:System.Int32","Region:System.Int32");
            CheckRecord(typeof(HistoricalRealmAccount),"CrisisSeverity:System.Int32","CrisisUntil:System.Int32","FoundedYear:System.Int32","RecognitionCursor:System.Int32","RecognitionUpkeep:System.Int64","RecognizedAreaKm2:System.Int64","RegimeChanges:System.Int32","RegimeSince:System.Int32","RegionalSeparations:System.Int32");
        }
        static byte[][] OptionalScratch(ScalarRecordBlock[] blocks)
        {var scratch=new byte[blocks.Length][];for(int k=0;k<blocks.Length;k++)scratch[k]=(byte[])blocks[k].Bytes.Clone();return scratch;}
        static void OptionalRecordArray<T>(BinaryWriter writer,T[] records,string name,ScalarRecordBlock[] blocks,Action<BinaryWriter,T,byte[][]> body) where T:class
        {
            var scratch=OptionalScratch(blocks);
            foreach(var record in records)
            {writer.Write((byte)1);writer.Write((byte)(record==null?0:1));if(record==null)continue;writer.Write(name);body(writer,record,scratch);}
            writer.Write((byte)0);
        }
        static bool TryWriteOptionalHistoricalArray(BinaryWriter writer,object value,Type type)
        {
            if(type==typeof(HistoricalAdministrativePost[])){OptionalRecordArray(writer,(HistoricalAdministrativePost[])value,"WorldGen.Core.HistoricalAdministrativePost",HistoricalAdministrativePostOptionalBlocks,HistoricalAdministrativePostOptionalBody);return true;}
            if(type==typeof(HistoricalImperialGovernment[])){OptionalRecordArray(writer,(HistoricalImperialGovernment[])value,"WorldGen.Core.HistoricalImperialGovernment",HistoricalImperialGovernmentOptionalBlocks,HistoricalImperialGovernmentOptionalBody);return true;}
            if(type==typeof(HistoricalMilitaryGovernment[])){OptionalRecordArray(writer,(HistoricalMilitaryGovernment[])value,"WorldGen.Core.HistoricalMilitaryGovernment",HistoricalMilitaryGovernmentOptionalBlocks,HistoricalMilitaryGovernmentOptionalBody);return true;}
            if(type==typeof(HistoricalMilitaryOperation[])){OptionalRecordArray(writer,(HistoricalMilitaryOperation[])value,"WorldGen.Core.HistoricalMilitaryOperation",HistoricalMilitaryOperationOptionalBlocks,HistoricalMilitaryOperationOptionalBody);return true;}
            if(type==typeof(HistoricalRecognizedTitle[])){OptionalRecordArray(writer,(HistoricalRecognizedTitle[])value,"WorldGen.Core.HistoricalRecognizedTitle",HistoricalRecognizedTitleOptionalBlocks,HistoricalRecognizedTitleOptionalBody);return true;}
            if(type==typeof(HistoricalRealmAccount[])){OptionalRecordArray(writer,(HistoricalRealmAccount[])value,"WorldGen.Core.HistoricalRealmAccount",HistoricalRealmAccountOptionalBlocks,HistoricalRealmAccountOptionalBody);return true;}
            return false;
        }
        static bool TryWriteOptionalHistoricalRecord(BinaryWriter writer,object value,Type type)
        {
            if(type==typeof(HistoricalAdministrativePost)){HistoricalAdministrativePostOptionalBody(writer,(HistoricalAdministrativePost)value,OptionalScratch(HistoricalAdministrativePostOptionalBlocks));return true;}
            if(type==typeof(HistoricalImperialGovernment)){HistoricalImperialGovernmentOptionalBody(writer,(HistoricalImperialGovernment)value,OptionalScratch(HistoricalImperialGovernmentOptionalBlocks));return true;}
            if(type==typeof(HistoricalMilitaryGovernment)){HistoricalMilitaryGovernmentOptionalBody(writer,(HistoricalMilitaryGovernment)value,OptionalScratch(HistoricalMilitaryGovernmentOptionalBlocks));return true;}
            if(type==typeof(HistoricalMilitaryOperation)){HistoricalMilitaryOperationOptionalBody(writer,(HistoricalMilitaryOperation)value,OptionalScratch(HistoricalMilitaryOperationOptionalBlocks));return true;}
            if(type==typeof(HistoricalRecognizedTitle)){HistoricalRecognizedTitleOptionalBody(writer,(HistoricalRecognizedTitle)value,OptionalScratch(HistoricalRecognizedTitleOptionalBlocks));return true;}
            if(type==typeof(HistoricalRealmAccount)){HistoricalRealmAccountOptionalBody(writer,(HistoricalRealmAccount)value,OptionalScratch(HistoricalRealmAccountOptionalBlocks));return true;}
            return false;
        }
        static readonly ScalarRecordBlock[] HistoricalAdministrativePostOptionalBlocks={new ScalarRecordBlock(typeof(HistoricalAdministrativePost),"AnnualCost","AreaKm2","AutonomyMilli","Cell","ClosedYear","Depth","FoundedYear","Founders","Id","LegDistanceM","Maritime","MessageEffortKm"),new ScalarRecordBlock(typeof(HistoricalAdministrativePost),"ParentSource","PolityId")};
        static void HistoricalAdministrativePostOptionalBody(BinaryWriter writer,HistoricalAdministrativePost record,byte[][] scratch)
        {
            PutLong(scratch[0],HistoricalAdministrativePostOptionalBlocks[0].Offsets[0],record.AnnualCost);
            PutLong(scratch[0],HistoricalAdministrativePostOptionalBlocks[0].Offsets[1],record.AreaKm2);
            PutInteger(scratch[0],HistoricalAdministrativePostOptionalBlocks[0].Offsets[2],record.AutonomyMilli);
            PutInteger(scratch[0],HistoricalAdministrativePostOptionalBlocks[0].Offsets[3],record.Cell);
            PutInteger(scratch[0],HistoricalAdministrativePostOptionalBlocks[0].Offsets[4],record.ClosedYear);
            PutInteger(scratch[0],HistoricalAdministrativePostOptionalBlocks[0].Offsets[5],record.Depth);
            PutInteger(scratch[0],HistoricalAdministrativePostOptionalBlocks[0].Offsets[6],record.FoundedYear);
            PutInteger(scratch[0],HistoricalAdministrativePostOptionalBlocks[0].Offsets[7],record.Founders);
            PutInteger(scratch[0],HistoricalAdministrativePostOptionalBlocks[0].Offsets[8],record.Id);
            PutInteger(scratch[0],HistoricalAdministrativePostOptionalBlocks[0].Offsets[9],record.LegDistanceM);
            scratch[0][HistoricalAdministrativePostOptionalBlocks[0].Offsets[10]]=(byte)(record.Maritime?1:0);
            PutDouble(scratch[0],HistoricalAdministrativePostOptionalBlocks[0].Offsets[11],record.MessageEffortKm);
            writer.Write(scratch[0]);
            writer.Write("Name");Write(writer,record.Name);
            PutInteger(scratch[1],HistoricalAdministrativePostOptionalBlocks[1].Offsets[0],record.ParentSource);
            PutInteger(scratch[1],HistoricalAdministrativePostOptionalBlocks[1].Offsets[1],record.PolityId);
            writer.Write(scratch[1]);
        }
        static readonly ScalarRecordBlock[] HistoricalImperialGovernmentOptionalBlocks={new ScalarRecordBlock(typeof(HistoricalImperialGovernment),"AdministrativePostCost"),new ScalarRecordBlock(typeof(HistoricalImperialGovernment),"AnnualTribute","ArrearsYears","AutonomyMilli"),new ScalarRecordBlock(typeof(HistoricalImperialGovernment),"Height","Imposed","LastPatronSuccession","LastReportYear","Patron","ReportedAreaKm2","ReportedArmy","ReportedPopulation","SinceYear","SubjectAreaKm2","SubjectArmy","SubjectCount","SubjectPopulation","TotalTribute")};
        static void HistoricalImperialGovernmentOptionalBody(BinaryWriter writer,HistoricalImperialGovernment record,byte[][] scratch)
        {
            PutLong(scratch[0],HistoricalImperialGovernmentOptionalBlocks[0].Offsets[0],record.AdministrativePostCost);
            writer.Write(scratch[0]);
            writer.Write("AdministrativePosts");Write(writer,record.AdministrativePosts);
            PutLong(scratch[1],HistoricalImperialGovernmentOptionalBlocks[1].Offsets[0],record.AnnualTribute);
            PutInteger(scratch[1],HistoricalImperialGovernmentOptionalBlocks[1].Offsets[1],record.ArrearsYears);
            PutInteger(scratch[1],HistoricalImperialGovernmentOptionalBlocks[1].Offsets[2],record.AutonomyMilli);
            writer.Write(scratch[1]);
            writer.Write("ChildrenByHeight");Write(writer,record.ChildrenByHeight);
            PutInteger(scratch[2],HistoricalImperialGovernmentOptionalBlocks[2].Offsets[0],record.Height);
            scratch[2][HistoricalImperialGovernmentOptionalBlocks[2].Offsets[1]]=(byte)(record.Imposed?1:0);
            PutInteger(scratch[2],HistoricalImperialGovernmentOptionalBlocks[2].Offsets[2],record.LastPatronSuccession);
            PutInteger(scratch[2],HistoricalImperialGovernmentOptionalBlocks[2].Offsets[3],record.LastReportYear);
            PutInteger(scratch[2],HistoricalImperialGovernmentOptionalBlocks[2].Offsets[4],record.Patron);
            PutLong(scratch[2],HistoricalImperialGovernmentOptionalBlocks[2].Offsets[5],record.ReportedAreaKm2);
            PutLong(scratch[2],HistoricalImperialGovernmentOptionalBlocks[2].Offsets[6],record.ReportedArmy);
            PutLong(scratch[2],HistoricalImperialGovernmentOptionalBlocks[2].Offsets[7],record.ReportedPopulation);
            PutInteger(scratch[2],HistoricalImperialGovernmentOptionalBlocks[2].Offsets[8],record.SinceYear);
            PutLong(scratch[2],HistoricalImperialGovernmentOptionalBlocks[2].Offsets[9],record.SubjectAreaKm2);
            PutLong(scratch[2],HistoricalImperialGovernmentOptionalBlocks[2].Offsets[10],record.SubjectArmy);
            PutInteger(scratch[2],HistoricalImperialGovernmentOptionalBlocks[2].Offsets[11],record.SubjectCount);
            PutLong(scratch[2],HistoricalImperialGovernmentOptionalBlocks[2].Offsets[12],record.SubjectPopulation);
            PutLong(scratch[2],HistoricalImperialGovernmentOptionalBlocks[2].Offsets[13],record.TotalTribute);
            writer.Write(scratch[2]);
        }
        static readonly ScalarRecordBlock[] HistoricalMilitaryGovernmentOptionalBlocks={new ScalarRecordBlock(typeof(HistoricalMilitaryGovernment),"Deaths","FieldPayroll","FieldProvisions","MobilizationEpoch","RecruitmentSpending","Recruits","Reserved")};
        static void HistoricalMilitaryGovernmentOptionalBody(BinaryWriter writer,HistoricalMilitaryGovernment record,byte[][] scratch)
        {
            PutLong(scratch[0],HistoricalMilitaryGovernmentOptionalBlocks[0].Offsets[0],record.Deaths);
            PutLong(scratch[0],HistoricalMilitaryGovernmentOptionalBlocks[0].Offsets[1],record.FieldPayroll);
            PutLong(scratch[0],HistoricalMilitaryGovernmentOptionalBlocks[0].Offsets[2],record.FieldProvisions);
            PutInteger(scratch[0],HistoricalMilitaryGovernmentOptionalBlocks[0].Offsets[3],record.MobilizationEpoch);
            PutLong(scratch[0],HistoricalMilitaryGovernmentOptionalBlocks[0].Offsets[4],record.RecruitmentSpending);
            PutLong(scratch[0],HistoricalMilitaryGovernmentOptionalBlocks[0].Offsets[5],record.Recruits);
            PutInteger(scratch[0],HistoricalMilitaryGovernmentOptionalBlocks[0].Offsets[6],record.Reserved);
            writer.Write(scratch[0]);
        }
        static readonly ScalarRecordBlock[] HistoricalMilitaryOperationOptionalBlocks={new ScalarRecordBlock(typeof(HistoricalMilitaryOperation),"Closed","CommittedA","CommittedB","CorePressureA","CorePressureB","EpochA","EpochB","IdleYears","MarchDaysA","MarchDaysB","OriginalCapitalA","OriginalCapitalB","PartyA","PartyB","SiegeBesieger","SiegeDays","SiegeTarget","VictoriesA","VictoriesB")};
        static void HistoricalMilitaryOperationOptionalBody(BinaryWriter writer,HistoricalMilitaryOperation record,byte[][] scratch)
        {
            scratch[0][HistoricalMilitaryOperationOptionalBlocks[0].Offsets[0]]=(byte)(record.Closed?1:0);
            PutInteger(scratch[0],HistoricalMilitaryOperationOptionalBlocks[0].Offsets[1],record.CommittedA);
            PutInteger(scratch[0],HistoricalMilitaryOperationOptionalBlocks[0].Offsets[2],record.CommittedB);
            PutInteger(scratch[0],HistoricalMilitaryOperationOptionalBlocks[0].Offsets[3],record.CorePressureA);
            PutInteger(scratch[0],HistoricalMilitaryOperationOptionalBlocks[0].Offsets[4],record.CorePressureB);
            PutInteger(scratch[0],HistoricalMilitaryOperationOptionalBlocks[0].Offsets[5],record.EpochA);
            PutInteger(scratch[0],HistoricalMilitaryOperationOptionalBlocks[0].Offsets[6],record.EpochB);
            PutInteger(scratch[0],HistoricalMilitaryOperationOptionalBlocks[0].Offsets[7],record.IdleYears);
            PutInteger(scratch[0],HistoricalMilitaryOperationOptionalBlocks[0].Offsets[8],record.MarchDaysA);
            PutInteger(scratch[0],HistoricalMilitaryOperationOptionalBlocks[0].Offsets[9],record.MarchDaysB);
            PutInteger(scratch[0],HistoricalMilitaryOperationOptionalBlocks[0].Offsets[10],record.OriginalCapitalA);
            PutInteger(scratch[0],HistoricalMilitaryOperationOptionalBlocks[0].Offsets[11],record.OriginalCapitalB);
            PutInteger(scratch[0],HistoricalMilitaryOperationOptionalBlocks[0].Offsets[12],record.PartyA);
            PutInteger(scratch[0],HistoricalMilitaryOperationOptionalBlocks[0].Offsets[13],record.PartyB);
            PutInteger(scratch[0],HistoricalMilitaryOperationOptionalBlocks[0].Offsets[14],record.SiegeBesieger);
            PutInteger(scratch[0],HistoricalMilitaryOperationOptionalBlocks[0].Offsets[15],record.SiegeDays);
            PutInteger(scratch[0],HistoricalMilitaryOperationOptionalBlocks[0].Offsets[16],record.SiegeTarget);
            PutInteger(scratch[0],HistoricalMilitaryOperationOptionalBlocks[0].Offsets[17],record.VictoriesA);
            PutInteger(scratch[0],HistoricalMilitaryOperationOptionalBlocks[0].Offsets[18],record.VictoriesB);
            writer.Write(scratch[0]);
        }
        static readonly ScalarRecordBlock[] HistoricalRecognizedTitleOptionalBlocks={new ScalarRecordBlock(typeof(HistoricalRecognizedTitle),"AreaKm2"),new ScalarRecordBlock(typeof(HistoricalRecognizedTitle),"EstablishedYear","Holder","Id","LastPrice","LastTransferYear","PreviousHolder","Recognizer","Region")};
        static void HistoricalRecognizedTitleOptionalBody(BinaryWriter writer,HistoricalRecognizedTitle record,byte[][] scratch)
        {
            PutLong(scratch[0],HistoricalRecognizedTitleOptionalBlocks[0].Offsets[0],record.AreaKm2);
            writer.Write(scratch[0]);
            writer.Write("Basis");Write(writer,record.Basis);
            PutInteger(scratch[1],HistoricalRecognizedTitleOptionalBlocks[1].Offsets[0],record.EstablishedYear);
            PutInteger(scratch[1],HistoricalRecognizedTitleOptionalBlocks[1].Offsets[1],record.Holder);
            PutInteger(scratch[1],HistoricalRecognizedTitleOptionalBlocks[1].Offsets[2],record.Id);
            PutLong(scratch[1],HistoricalRecognizedTitleOptionalBlocks[1].Offsets[3],record.LastPrice);
            PutInteger(scratch[1],HistoricalRecognizedTitleOptionalBlocks[1].Offsets[4],record.LastTransferYear);
            PutInteger(scratch[1],HistoricalRecognizedTitleOptionalBlocks[1].Offsets[5],record.PreviousHolder);
            PutInteger(scratch[1],HistoricalRecognizedTitleOptionalBlocks[1].Offsets[6],record.Recognizer);
            PutInteger(scratch[1],HistoricalRecognizedTitleOptionalBlocks[1].Offsets[7],record.Region);
            writer.Write(scratch[1]);
        }
        static readonly ScalarRecordBlock[] HistoricalRealmAccountOptionalBlocks={new ScalarRecordBlock(typeof(HistoricalRealmAccount),"CrisisSeverity","CrisisUntil","FoundedYear","RecognitionCursor","RecognitionUpkeep","RecognizedAreaKm2","RegimeChanges","RegimeSince","RegionalSeparations")};
        static void HistoricalRealmAccountOptionalBody(BinaryWriter writer,HistoricalRealmAccount record,byte[][] scratch)
        {
            PutInteger(scratch[0],HistoricalRealmAccountOptionalBlocks[0].Offsets[0],record.CrisisSeverity);
            PutInteger(scratch[0],HistoricalRealmAccountOptionalBlocks[0].Offsets[1],record.CrisisUntil);
            PutInteger(scratch[0],HistoricalRealmAccountOptionalBlocks[0].Offsets[2],record.FoundedYear);
            PutInteger(scratch[0],HistoricalRealmAccountOptionalBlocks[0].Offsets[3],record.RecognitionCursor);
            PutLong(scratch[0],HistoricalRealmAccountOptionalBlocks[0].Offsets[4],record.RecognitionUpkeep);
            PutLong(scratch[0],HistoricalRealmAccountOptionalBlocks[0].Offsets[5],record.RecognizedAreaKm2);
            PutInteger(scratch[0],HistoricalRealmAccountOptionalBlocks[0].Offsets[6],record.RegimeChanges);
            PutInteger(scratch[0],HistoricalRealmAccountOptionalBlocks[0].Offsets[7],record.RegimeSince);
            PutInteger(scratch[0],HistoricalRealmAccountOptionalBlocks[0].Offsets[8],record.RegionalSeparations);
            writer.Write(scratch[0]);
        }
    }
}
