using System;
using System.IO;

namespace WorldGen.Core
{
    public static partial class StateHash
    {
        // Exact reflection-order byte templates only; all values remain live.
        static void CheckInternationalRecords()
        {
            CheckRecord(typeof(HistoricalDiplomaticAccount),"CoalitionEntries:System.Int32","ContinuitySuccessor:System.Int32","Id:System.Int32","LastPeaceDuesYear:System.Int32","LastTradeDuesYear:System.Int32","LastWarYear:System.Int32","PeaceArrearsYears:System.Int32","PeaceOrganization:System.Int32","Relations:WorldGen.Core.HistoricalLocalBag","TradeArrearsYears:System.Int32","TradeOrganization:System.Int32");
            CheckRecord(typeof(HistoricalInternationalOrganization),"Authority:System.Int32","Contributions:System.Int64","EndedYear:System.Int32","FailedMediations:System.Int32","FoundedYear:System.Int32","FounderPolity:System.Int32","Id:System.Int32","Kind:WorldGen.Core.HistoricalOrganizationKind","LastActionYear:System.Int32","LastMediationYear:System.Int32","MediationSpending:System.Int64","Mediations:System.Int32","Members:WorldGen.Core.HistoricalLocalBag","Name:System.String","Reserve:System.Int64","ServiceSpending:System.Int64");
            CheckRecord(typeof(HistoricalCoalitionEntry),"AllyId:System.Int32","CampaignId:System.Int32","OriginCampaignId:System.Int32","PolityId:System.Int32","Year:System.Int32");
            CheckRecord(typeof(HistoricalInternationalFinanceAccount),"PeaceAssessment:System.Int64","PeaceReserveDue:System.Int64","TradeAssessment:System.Int64","TradeReserveDue:System.Int64");
            CheckRecord(typeof(HistoricalInternationalOperatingBudget),"AnnualAssessments:System.Int64","AuthorityApplied:System.Int32","AuthorityWindowYears:System.Int32","LastOperatingCharge:System.Int64","UnfundedWork:System.Int64");
            CheckRecord(typeof(HistoricalInternationalFinances),"Accounts:WorldGen.Core.HistoricalInternationalFinanceAccount[]","Organizations:WorldGen.Core.HistoricalInternationalOperatingBudget[]");
            CheckRecord(typeof(HistoricalInternationalSystem),"Accounts:WorldGen.Core.HistoricalDiplomaticAccount[]","CoalitionEntries:WorldGen.Core.HistoricalCoalitionEntry[]","Finances:WorldGen.Core.HistoricalInternationalFinances","Organizations:WorldGen.Core.HistoricalInternationalOrganization[]","RelationCampaigns:WorldGen.Core.HistoricalRelationCampaigns","Version:System.String");
            CheckRecord(typeof(SimulationEvent),"Due:System.Int64","Entity:System.Int32","Process:System.Int32","Ticket:System.Int64");
            CheckRecord(typeof(SimulationTimeline),"Events:WorldGen.Core.SimulationEvent[]","NextTicket:System.Int64","ProcessedEvents:System.Int64","Seconds:System.Int64","Version:System.String");
        }
        static readonly ScalarRecordBlock[] DiplomaticBlocks={
            new ScalarRecordBlock(typeof(HistoricalDiplomaticAccount),"CoalitionEntries","ContinuitySuccessor","Id","LastPeaceDuesYear","LastTradeDuesYear","LastWarYear","PeaceArrearsYears","PeaceOrganization"),
            new ScalarRecordBlock(typeof(HistoricalDiplomaticAccount),"TradeArrearsYears","TradeOrganization")};
        static readonly ScalarRecordBlock[] OrganizationBlocks={
            new ScalarRecordBlock(typeof(HistoricalInternationalOrganization),"Authority","Contributions","EndedYear","FailedMediations","FoundedYear","FounderPolity","Id","Kind","LastActionYear","LastMediationYear","MediationSpending","Mediations"),
            new ScalarRecordBlock(typeof(HistoricalInternationalOrganization),"Reserve","ServiceSpending")};
        static readonly ScalarRecordBlock[] CoalitionBlocks={new ScalarRecordBlock(typeof(HistoricalCoalitionEntry),"AllyId","CampaignId","OriginCampaignId","PolityId","Year")};
        static readonly ScalarRecordBlock[] FinanceAccountBlocks={new ScalarRecordBlock(typeof(HistoricalInternationalFinanceAccount),"PeaceAssessment","PeaceReserveDue","TradeAssessment","TradeReserveDue")};
        static readonly ScalarRecordBlock[] OperatingBudgetBlocks={new ScalarRecordBlock(typeof(HistoricalInternationalOperatingBudget),"AnnualAssessments","AuthorityApplied","AuthorityWindowYears","LastOperatingCharge","UnfundedWork")};
        static readonly ScalarRecordBlock[] EventBlocks={new ScalarRecordBlock(typeof(SimulationEvent),"Due","Entity","Process","Ticket")};
        static readonly ScalarRecordBlock[] TimelineBlocks={new ScalarRecordBlock(typeof(SimulationTimeline),"NextTicket","ProcessedEvents","Seconds")};

        static void DiplomaticBody(BinaryWriter writer,HistoricalDiplomaticAccount row,byte[][] scratch)
        {
            var at=DiplomaticBlocks[0].Offsets;var bytes=scratch[0];
            PutInteger(bytes,at[0],row.CoalitionEntries);PutInteger(bytes,at[1],row.ContinuitySuccessor);PutInteger(bytes,at[2],row.Id);
            PutInteger(bytes,at[3],row.LastPeaceDuesYear);PutInteger(bytes,at[4],row.LastTradeDuesYear);PutInteger(bytes,at[5],row.LastWarYear);
            PutInteger(bytes,at[6],row.PeaceArrearsYears);PutInteger(bytes,at[7],row.PeaceOrganization);writer.Write(bytes);
            writer.Write("Relations");Write(writer,row.Relations);
            at=DiplomaticBlocks[1].Offsets;bytes=scratch[1];PutInteger(bytes,at[0],row.TradeArrearsYears);PutInteger(bytes,at[1],row.TradeOrganization);writer.Write(bytes);
        }
        static void OrganizationBody(BinaryWriter writer,HistoricalInternationalOrganization row,byte[][] scratch)
        {
            var at=OrganizationBlocks[0].Offsets;var bytes=scratch[0];
            PutInteger(bytes,at[0],row.Authority);PutLong(bytes,at[1],row.Contributions);PutInteger(bytes,at[2],row.EndedYear);
            PutInteger(bytes,at[3],row.FailedMediations);PutInteger(bytes,at[4],row.FoundedYear);PutInteger(bytes,at[5],row.FounderPolity);
            PutInteger(bytes,at[6],row.Id);PutInteger(bytes,at[7],(int)row.Kind);PutInteger(bytes,at[8],row.LastActionYear);
            PutInteger(bytes,at[9],row.LastMediationYear);PutLong(bytes,at[10],row.MediationSpending);PutInteger(bytes,at[11],row.Mediations);writer.Write(bytes);
            writer.Write("Members");Write(writer,row.Members);writer.Write("Name");Write(writer,row.Name);
            at=OrganizationBlocks[1].Offsets;bytes=scratch[1];PutLong(bytes,at[0],row.Reserve);PutLong(bytes,at[1],row.ServiceSpending);writer.Write(bytes);
        }
        static void CoalitionBody(BinaryWriter writer,HistoricalCoalitionEntry row,byte[][] scratch)
        {
            var at=CoalitionBlocks[0].Offsets;var bytes=scratch[0];PutInteger(bytes,at[0],row.AllyId);PutInteger(bytes,at[1],row.CampaignId);
            PutInteger(bytes,at[2],row.OriginCampaignId);PutInteger(bytes,at[3],row.PolityId);PutInteger(bytes,at[4],row.Year);writer.Write(bytes);
        }
        static void FinanceAccountBody(BinaryWriter writer,HistoricalInternationalFinanceAccount row,byte[][] scratch)
        {
            var at=FinanceAccountBlocks[0].Offsets;var bytes=scratch[0];PutLong(bytes,at[0],row.PeaceAssessment);PutLong(bytes,at[1],row.PeaceReserveDue);
            PutLong(bytes,at[2],row.TradeAssessment);PutLong(bytes,at[3],row.TradeReserveDue);writer.Write(bytes);
        }
        static void OperatingBudgetBody(BinaryWriter writer,HistoricalInternationalOperatingBudget row,byte[][] scratch)
        {
            var at=OperatingBudgetBlocks[0].Offsets;var bytes=scratch[0];PutLong(bytes,at[0],row.AnnualAssessments);PutInteger(bytes,at[1],row.AuthorityApplied);
            PutInteger(bytes,at[2],row.AuthorityWindowYears);PutLong(bytes,at[3],row.LastOperatingCharge);PutLong(bytes,at[4],row.UnfundedWork);writer.Write(bytes);
        }
        static void EventBody(BinaryWriter writer,SimulationEvent row,byte[][] scratch)
        {
            var at=EventBlocks[0].Offsets;var bytes=scratch[0];PutLong(bytes,at[0],row.Due);PutInteger(bytes,at[1],row.Entity);
            PutInteger(bytes,at[2],row.Process);PutLong(bytes,at[3],row.Ticket);writer.Write(bytes);
        }
        static void TimelineBody(BinaryWriter writer,SimulationTimeline row)
        {
            writer.Write("Events");Write(writer,row.Events);
            var bytes=(byte[])TimelineBlocks[0].Bytes.Clone();var at=TimelineBlocks[0].Offsets;
            PutLong(bytes,at[0],row.NextTicket);PutLong(bytes,at[1],row.ProcessedEvents);PutLong(bytes,at[2],row.Seconds);writer.Write(bytes);
            writer.Write("Version");Write(writer,row.Version);
        }
        static bool TryWriteInternationalRecord(BinaryWriter writer,object value,Type type)
        {
            if(type==typeof(HistoricalDiplomaticAccount[])){OptionalRecordArray(writer,(HistoricalDiplomaticAccount[])value,"WorldGen.Core.HistoricalDiplomaticAccount",DiplomaticBlocks,DiplomaticBody);return true;}
            if(type==typeof(HistoricalInternationalOrganization[])){OptionalRecordArray(writer,(HistoricalInternationalOrganization[])value,"WorldGen.Core.HistoricalInternationalOrganization",OrganizationBlocks,OrganizationBody);return true;}
            if(type==typeof(HistoricalCoalitionEntry[])){OptionalRecordArray(writer,(HistoricalCoalitionEntry[])value,"WorldGen.Core.HistoricalCoalitionEntry",CoalitionBlocks,CoalitionBody);return true;}
            if(type==typeof(HistoricalInternationalFinanceAccount[])){OptionalRecordArray(writer,(HistoricalInternationalFinanceAccount[])value,"WorldGen.Core.HistoricalInternationalFinanceAccount",FinanceAccountBlocks,FinanceAccountBody);return true;}
            if(type==typeof(HistoricalInternationalOperatingBudget[])){OptionalRecordArray(writer,(HistoricalInternationalOperatingBudget[])value,"WorldGen.Core.HistoricalInternationalOperatingBudget",OperatingBudgetBlocks,OperatingBudgetBody);return true;}
            if(type==typeof(SimulationEvent[])){OptionalRecordArray(writer,(SimulationEvent[])value,"WorldGen.Core.SimulationEvent",EventBlocks,EventBody);return true;}
            if(type==typeof(HistoricalDiplomaticAccount)){DiplomaticBody(writer,(HistoricalDiplomaticAccount)value,OptionalScratch(DiplomaticBlocks));return true;}
            if(type==typeof(HistoricalInternationalOrganization)){OrganizationBody(writer,(HistoricalInternationalOrganization)value,OptionalScratch(OrganizationBlocks));return true;}
            if(type==typeof(HistoricalCoalitionEntry)){CoalitionBody(writer,(HistoricalCoalitionEntry)value,OptionalScratch(CoalitionBlocks));return true;}
            if(type==typeof(HistoricalInternationalFinanceAccount)){FinanceAccountBody(writer,(HistoricalInternationalFinanceAccount)value,OptionalScratch(FinanceAccountBlocks));return true;}
            if(type==typeof(HistoricalInternationalOperatingBudget)){OperatingBudgetBody(writer,(HistoricalInternationalOperatingBudget)value,OptionalScratch(OperatingBudgetBlocks));return true;}
            if(type==typeof(SimulationEvent)){EventBody(writer,(SimulationEvent)value,OptionalScratch(EventBlocks));return true;}
            if(type==typeof(SimulationTimeline)){TimelineBody(writer,(SimulationTimeline)value);return true;}
            if(type==typeof(HistoricalInternationalSystem))
            {
                var row=(HistoricalInternationalSystem)value;writer.Write("Accounts");Write(writer,row.Accounts);writer.Write("CoalitionEntries");Write(writer,row.CoalitionEntries);
                // Null optional field was omitted by the original reflection path.
                if(row.Finances!=null){writer.Write("Finances");Write(writer,row.Finances);}
                writer.Write("Organizations");Write(writer,row.Organizations);writer.Write("RelationCampaigns");Write(writer,row.RelationCampaigns);writer.Write("Version");Write(writer,row.Version);return true;
            }
            if(type==typeof(HistoricalInternationalFinances))
            {var row=(HistoricalInternationalFinances)value;writer.Write("Accounts");Write(writer,row.Accounts);writer.Write("Organizations");Write(writer,row.Organizations);return true;}
            return false;
        }
    }
}
