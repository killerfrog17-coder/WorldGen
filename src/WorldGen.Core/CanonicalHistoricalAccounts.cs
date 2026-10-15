using System;
using System.IO;
namespace WorldGen.Core
{
    public static partial class StateHash
    {
        // Checked typed layouts preserve the reflection wire format. No record
        // values or integrity results are memoized, including archived records.
        static void CheckWideHistoricalRecords()
        {
            CheckRecord(typeof(HistoricalPolity),"AreaKm2:System.Int64","Army:System.Int32","CapitalId:System.Int32","Cohesion:System.Int32","CultureId:System.Int32","EndedYear:System.Int32","Exhaustion:System.Int32","FoundedYear:System.Int32","Government:System.String","Id:System.Int32","LastEstimateYear:System.Int32","Legitimacy:System.Int32","Name:System.String","ParentId:System.Int32","Population:System.Int64","ReligionId:System.Int32","Ruler:System.String","RulerSince:System.Int32","Treasury:System.Int64");
            CheckRecord(typeof(HistoricalRelation),"A:System.Int32","Alliance:System.Boolean","B:System.Int32","LastBattle:System.Int32","Trade:System.Boolean","TruceUntil:System.Int32","Trust:System.Int32","WarSince:System.Int32");
            CheckRecord(typeof(HistoricalTown),"AutonomyPressure:System.Int32","CivicExampleExposure:System.Int32","CivicPreparationYears:System.Int32","CivicTeacherPolityId:System.Int32","Cropping:WorldGen.Core.HistoricalLocalHarvest","Demography:WorldGen.Core.HistoricalUrbanDemography","DiseaseDeaths:System.Int64","DiseaseKind:WorldGen.Core.HistoricalDiseaseKind","DiseaseMortalityPermille:System.Int32","DiseaseUntil:System.Int32","DiseaseWaveId:System.Int32","ExportedFood:System.Int64","FoodBase:System.Int64","FoodTransportLoss:System.Int64","Id:System.Int32","ImmunityUntil:System.Int32","ImportedFood:System.Int64","KindImmunity:System.Int32[]","LastAutonomyActionYear:System.Int32","LastHarvestHazard:WorldGen.Core.HarvestHazard","LastHarvestHazardYear:System.Int32","ShortageYears:System.Int32","UnrestPolityId:System.Int32","Walls:System.Int32");
            CheckRecord(typeof(HistoricalStateDynamics),"Cycle:WorldGen.Core.HistoricalPoliticalCycle","DisputedUntil:System.Int32","FoundationPotential:System.Int32","HouseSerial:System.Int32","Id:System.Int32","LastSuccession:System.Int32","LeaderId:System.Int32","PeakAreaKm2:System.Int64","PeakPopulation:System.Int64","PublicWorks:System.Int32","Strain:System.Int32","Tradition:System.String");
            CheckRecord(typeof(HistoricalCampaign),"A:System.Int32","Aim:WorldGen.Core.HistoricalWarAim","AttackerId:System.Int32","B:System.Int32","CapturedSettlements:System.Int32[]","CasualtiesA:System.Int64","CasualtiesB:System.Int64","Conquests:System.Int32","Context:System.String","EndedYear:System.Int32","Id:System.Int32","LastActionYear:System.Int32","LeaderId:System.Int32","Name:System.String","Objective:System.String","Outcome:System.String","Repulses:System.Int32","Spoils:System.Int64","StartedYear:System.Int32","TributePaid:System.Int64","TributeRecipientId:System.Int32");
            CheckRecord(typeof(HistoricalAgriculture),"AdoptedYear:System.Int32[]","Animals:WorldGen.Core.HistoricalAnimalHusbandry[]","Cultivars:WorldGen.Core.HistoricalCultivar[]","CultureId:System.Int32","HusbandryYears:System.Int32[]","NativeMask:System.Int32","Practice:System.Int32[]","PracticeCell:System.Int32[]","SourceCulture:System.Int32[]","WildOriginCell:System.Int32[]");
            CheckRecord(typeof(HistoricalCulture),"Id:System.Int32","Institution:System.Int32","InstitutionalExamples:System.Int32","LanguageId:System.Int32","ParentCultureId:System.Int32","ReligionId:System.Int32","Research:System.Int32[]","Sedentism:System.Int32","Surplus:System.Int32","Technical:WorldGen.Core.HistoricalTechnicalPractice","Technology:System.Int32[]");
            CheckRecord(typeof(HistoricalCultivar),"CropId:System.Int32","DaylengthLatitudeTenths:System.Int32","MaturityDays:System.Int32","MinimumWarmTenthsC:System.Int32","OriginCulture:System.Int32","ParentCulture:System.Int32","SelectedYears:System.Int32","WaterRequirementPermille:System.Int32","YieldPermille:System.Int32");
            CheckRecord(typeof(Settlement),"Cell:System.Int32","CraftSharePermille:System.Int32","CultivatedPermille:System.Int32","CultureId:System.Int32","FoodCapacity:System.Int64","FoodReserve:System.Int64","FoundedYear:System.Int32","Government:System.String","Health:System.Int32","Id:System.Int32","Name:System.String","PolityId:System.Int32","Population:System.Int64","Stability:System.Int32");
            CheckRecord(typeof(CultureIdentity),"AreaKm2:System.Int64","Contacts:System.Int32[]","Farming:System.Int32","Fishing:System.Int32","FoundedYear:System.Int32","Gathering:System.Int32","Herding:System.Int32","Hunting:System.Int32","Id:System.Int32","Name:System.String","Organization:System.Int32","OriginCell:System.Int32","Population:System.Int64","Resources:System.String","Stage:WorldGen.Core.SocietyStage","StageYear:System.Int32","Subsistence:System.String");
            CheckRecord(typeof(HistoricalAdministrativeAccount),"DisconnectedResidents:System.Int64","DisconnectedUrbanResidents:System.Int64","MaximumServiceDistanceM:System.Int32","PolityId:System.Int32","ReachM:System.Int32","RuralReceipts:System.Int64","ServiceCost:System.Int64","TaxableUrbanResidents:System.Int64","UrbanReceipts:System.Int64","Year:System.Int32");
            CheckRecord(typeof(HistoricalUrbanDemography),"ChronicLoss:System.Int64","NaturalChange:System.Int64","RuralInflow:System.Int64","RuralOutflow:System.Int64","TotalChronicLoss:System.Int64","TotalRuralInflow:System.Int64","TotalRuralOutflow:System.Int64","Year:System.Int32");
            CheckRecord(typeof(HistoricalLocalHarvest),"CropIds:System.Int32[]","CultureId:System.Int32","SharesPermille:System.Int32[]","Year:System.Int32");
            CheckRecord(typeof(HistoricalLocalCommunity),"InclusionMilli:System.Int32","RulerId:System.Int32","SinceYear:System.Int32");
            CheckRecord(typeof(HistoricalGovernmentPolicy),"CentralizationMilli:System.Int32","LastReformYear:System.Int32","RepresentationMilli:System.Int32");
            CheckRecord(typeof(HistoricalCampaignAdvance),"AttackerAreaKm2:System.Int64","DefenderAreaKm2:System.Int64");
        }
        static bool TryWriteWideHistoricalArray(BinaryWriter writer,object value,Type type)
        {
            if(type==typeof(HistoricalPolity[])){WideHistoricalPolityArray(writer,(HistoricalPolity[])value);return true;}
            if(type==typeof(HistoricalRelation[])){WideHistoricalRelationArray(writer,(HistoricalRelation[])value);return true;}
            if(type==typeof(HistoricalTown[])){WideHistoricalTownArray(writer,(HistoricalTown[])value);return true;}
            if(type==typeof(HistoricalStateDynamics[])){WideHistoricalStateDynamicsArray(writer,(HistoricalStateDynamics[])value);return true;}
            if(type==typeof(HistoricalCampaign[])){WideHistoricalCampaignArray(writer,(HistoricalCampaign[])value);return true;}
            if(type==typeof(HistoricalAgriculture[])){WideHistoricalAgricultureArray(writer,(HistoricalAgriculture[])value);return true;}
            if(type==typeof(HistoricalCulture[])){WideHistoricalCultureArray(writer,(HistoricalCulture[])value);return true;}
            if(type==typeof(HistoricalCultivar[])){WideHistoricalCultivarArray(writer,(HistoricalCultivar[])value);return true;}
            if(type==typeof(Settlement[])){WideSettlementArray(writer,(Settlement[])value);return true;}
            if(type==typeof(CultureIdentity[])){WideCultureIdentityArray(writer,(CultureIdentity[])value);return true;}
            if(type==typeof(HistoricalAdministrativeAccount[])){WideHistoricalAdministrativeAccountArray(writer,(HistoricalAdministrativeAccount[])value);return true;}
            if(type==typeof(HistoricalUrbanDemography[])){WideHistoricalUrbanDemographyArray(writer,(HistoricalUrbanDemography[])value);return true;}
            if(type==typeof(HistoricalLocalHarvest[])){WideHistoricalLocalHarvestArray(writer,(HistoricalLocalHarvest[])value);return true;}
            if(type==typeof(HistoricalLocalCommunity[])){WideHistoricalLocalCommunityArray(writer,(HistoricalLocalCommunity[])value);return true;}
            if(type==typeof(HistoricalGovernmentPolicy[])){WideHistoricalGovernmentPolicyArray(writer,(HistoricalGovernmentPolicy[])value);return true;}
            if(type==typeof(HistoricalCampaignAdvance[])){WideHistoricalCampaignAdvanceArray(writer,(HistoricalCampaignAdvance[])value);return true;}
            return false;
        }
        static bool TryWriteWideHistoricalRecord(BinaryWriter writer,object value,Type type)
        {
            if(type==typeof(HistoricalPolity)){WideHistoricalPolityBody(writer,(HistoricalPolity)value,null);return true;}
            if(type==typeof(HistoricalRelation)){WideHistoricalRelationBody(writer,(HistoricalRelation)value,null);return true;}
            if(type==typeof(HistoricalTown)){WideHistoricalTownBody(writer,(HistoricalTown)value,null);return true;}
            if(type==typeof(HistoricalStateDynamics)){WideHistoricalStateDynamicsBody(writer,(HistoricalStateDynamics)value,null);return true;}
            if(type==typeof(HistoricalCampaign)){WideHistoricalCampaignBody(writer,(HistoricalCampaign)value,null);return true;}
            if(type==typeof(HistoricalAgriculture)){WideHistoricalAgricultureBody(writer,(HistoricalAgriculture)value,null);return true;}
            if(type==typeof(HistoricalCulture)){WideHistoricalCultureBody(writer,(HistoricalCulture)value,null);return true;}
            if(type==typeof(HistoricalCultivar)){WideHistoricalCultivarBody(writer,(HistoricalCultivar)value,null);return true;}
            if(type==typeof(Settlement)){WideSettlementBody(writer,(Settlement)value,null);return true;}
            if(type==typeof(CultureIdentity)){WideCultureIdentityBody(writer,(CultureIdentity)value,null);return true;}
            if(type==typeof(HistoricalAdministrativeAccount)){WideHistoricalAdministrativeAccountBody(writer,(HistoricalAdministrativeAccount)value,null);return true;}
            if(type==typeof(HistoricalUrbanDemography)){WideHistoricalUrbanDemographyBody(writer,(HistoricalUrbanDemography)value,null);return true;}
            if(type==typeof(HistoricalLocalHarvest)){WideHistoricalLocalHarvestBody(writer,(HistoricalLocalHarvest)value,null);return true;}
            if(type==typeof(HistoricalLocalCommunity)){WideHistoricalLocalCommunityBody(writer,(HistoricalLocalCommunity)value,null);return true;}
            if(type==typeof(HistoricalGovernmentPolicy)){WideHistoricalGovernmentPolicyBody(writer,(HistoricalGovernmentPolicy)value,null);return true;}
            if(type==typeof(HistoricalCampaignAdvance)){WideHistoricalCampaignAdvanceBody(writer,(HistoricalCampaignAdvance)value,null);return true;}
            return false;
        }
        static readonly ScalarRecordBlock WideHistoricalPolityBlock0=new ScalarRecordBlock(typeof(HistoricalPolity),"AreaKm2","Army","CapitalId","Cohesion","CultureId","EndedYear","Exhaustion","FoundedYear");
        static readonly ScalarRecordBlock WideHistoricalPolityBlock1=new ScalarRecordBlock(typeof(HistoricalPolity),"Id");
        static readonly ScalarRecordBlock WideHistoricalPolityBlock2=new ScalarRecordBlock(typeof(HistoricalPolity),"Legitimacy");
        static readonly ScalarRecordBlock WideHistoricalPolityBlock3=new ScalarRecordBlock(typeof(HistoricalPolity),"ParentId","Population","ReligionId");
        static readonly ScalarRecordBlock WideHistoricalPolityBlock4=new ScalarRecordBlock(typeof(HistoricalPolity),"RulerSince","Treasury");
        static void WideHistoricalPolityArray(BinaryWriter writer,HistoricalPolity[] records)
        {
            var scratch=new byte[][]{(byte[])WideHistoricalPolityBlock0.Bytes.Clone(),(byte[])WideHistoricalPolityBlock1.Bytes.Clone(),(byte[])WideHistoricalPolityBlock2.Bytes.Clone(),(byte[])WideHistoricalPolityBlock3.Bytes.Clone(),(byte[])WideHistoricalPolityBlock4.Bytes.Clone()};
            foreach(var record in records)
            {
                writer.Write((byte)1);
                if(record==null){writer.Write((byte)0);continue;}
                if(ownedHistoryContext!=null&&(record.EndedYear>=0))
                {
                    if(record.OwnedCanonicalRecord==null)record.OwnedCanonicalRecord=FreezeHistoricalRecord("WorldGen.Core.HistoricalPolity",target=>WideHistoricalPolityBody(target,record,scratch));
                    writer.Write(record.OwnedCanonicalRecord);continue;
                }
                writer.Write((byte)1);writer.Write("WorldGen.Core.HistoricalPolity");WideHistoricalPolityBody(writer,record,scratch);
            }
            writer.Write((byte)0);
        }
        static void WideHistoricalPolityBody(BinaryWriter writer,HistoricalPolity record,byte[][] scratch)
        {
            if(scratch==null)scratch=new byte[][]{(byte[])WideHistoricalPolityBlock0.Bytes.Clone(),(byte[])WideHistoricalPolityBlock1.Bytes.Clone(),(byte[])WideHistoricalPolityBlock2.Bytes.Clone(),(byte[])WideHistoricalPolityBlock3.Bytes.Clone(),(byte[])WideHistoricalPolityBlock4.Bytes.Clone()};
            PutLong(scratch[0],WideHistoricalPolityBlock0.Offsets[0],record.AreaKm2);
            PutInteger(scratch[0],WideHistoricalPolityBlock0.Offsets[1],record.Army);
            PutInteger(scratch[0],WideHistoricalPolityBlock0.Offsets[2],record.CapitalId);
            PutInteger(scratch[0],WideHistoricalPolityBlock0.Offsets[3],record.Cohesion);
            PutInteger(scratch[0],WideHistoricalPolityBlock0.Offsets[4],record.CultureId);
            PutInteger(scratch[0],WideHistoricalPolityBlock0.Offsets[5],record.EndedYear);
            PutInteger(scratch[0],WideHistoricalPolityBlock0.Offsets[6],record.Exhaustion);
            PutInteger(scratch[0],WideHistoricalPolityBlock0.Offsets[7],record.FoundedYear);
            writer.Write(scratch[0]);
            writer.Write("Government");Write(writer,record.Government);
            PutInteger(scratch[1],WideHistoricalPolityBlock1.Offsets[0],record.Id);
            writer.Write(scratch[1]);
            if(record.LastEstimateYear!=0){writer.Write("LastEstimateYear");Write(writer,record.LastEstimateYear);}
            PutInteger(scratch[2],WideHistoricalPolityBlock2.Offsets[0],record.Legitimacy);
            writer.Write(scratch[2]);
            writer.Write("Name");Write(writer,record.Name);
            PutInteger(scratch[3],WideHistoricalPolityBlock3.Offsets[0],record.ParentId);
            PutLong(scratch[3],WideHistoricalPolityBlock3.Offsets[1],record.Population);
            PutInteger(scratch[3],WideHistoricalPolityBlock3.Offsets[2],record.ReligionId);
            writer.Write(scratch[3]);
            writer.Write("Ruler");Write(writer,record.Ruler);
            PutInteger(scratch[4],WideHistoricalPolityBlock4.Offsets[0],record.RulerSince);
            PutLong(scratch[4],WideHistoricalPolityBlock4.Offsets[1],record.Treasury);
            writer.Write(scratch[4]);
        }
        static readonly ScalarRecordBlock WideHistoricalRelationBlock0=new ScalarRecordBlock(typeof(HistoricalRelation),"A","Alliance","B","LastBattle","Trade","TruceUntil","Trust","WarSince");
        static void WideHistoricalRelationArray(BinaryWriter writer,HistoricalRelation[] records)
        {
            var scratch=new byte[][]{(byte[])WideHistoricalRelationBlock0.Bytes.Clone()};
            foreach(var record in records)
            {
                writer.Write((byte)1);writer.Write((byte)(record==null?0:1));if(record==null)continue;writer.Write("WorldGen.Core.HistoricalRelation");WideHistoricalRelationBody(writer,record,scratch);
            }
            writer.Write((byte)0);
        }
        static void WideHistoricalRelationBody(BinaryWriter writer,HistoricalRelation record,byte[][] scratch)
        {
            if(scratch==null)scratch=new byte[][]{(byte[])WideHistoricalRelationBlock0.Bytes.Clone()};
            PutInteger(scratch[0],WideHistoricalRelationBlock0.Offsets[0],record.A);
            scratch[0][WideHistoricalRelationBlock0.Offsets[1]]=(byte)(record.Alliance?1:0);
            PutInteger(scratch[0],WideHistoricalRelationBlock0.Offsets[2],record.B);
            PutInteger(scratch[0],WideHistoricalRelationBlock0.Offsets[3],record.LastBattle);
            scratch[0][WideHistoricalRelationBlock0.Offsets[4]]=(byte)(record.Trade?1:0);
            PutInteger(scratch[0],WideHistoricalRelationBlock0.Offsets[5],record.TruceUntil);
            PutInteger(scratch[0],WideHistoricalRelationBlock0.Offsets[6],record.Trust);
            PutInteger(scratch[0],WideHistoricalRelationBlock0.Offsets[7],record.WarSince);
            writer.Write(scratch[0]);
        }
        static readonly ScalarRecordBlock WideHistoricalTownBlock0=new ScalarRecordBlock(typeof(HistoricalTown),"DiseaseUntil");
        static readonly ScalarRecordBlock WideHistoricalTownBlock1=new ScalarRecordBlock(typeof(HistoricalTown),"FoodBase");
        static readonly ScalarRecordBlock WideHistoricalTownBlock2=new ScalarRecordBlock(typeof(HistoricalTown),"Id","ImmunityUntil");
        static readonly ScalarRecordBlock WideHistoricalTownBlock3=new ScalarRecordBlock(typeof(HistoricalTown),"Walls");
        static void WideHistoricalTownArray(BinaryWriter writer,HistoricalTown[] records)
        {
            var scratch=new byte[][]{(byte[])WideHistoricalTownBlock0.Bytes.Clone(),(byte[])WideHistoricalTownBlock1.Bytes.Clone(),(byte[])WideHistoricalTownBlock2.Bytes.Clone(),(byte[])WideHistoricalTownBlock3.Bytes.Clone()};
            foreach(var record in records)
            {
                writer.Write((byte)1);
                if(record==null){writer.Write((byte)0);continue;}
                if(ownedHistoryContext!=null&&(ownedHistoryContext.Aggregates.Living!=null&&ownedHistoryContext.Aggregates.Living.Cities[record.Id].AbandonedYear>=0))
                {
                    if(record.OwnedCanonicalRecord==null)record.OwnedCanonicalRecord=FreezeHistoricalRecord("WorldGen.Core.HistoricalTown",target=>WideHistoricalTownBody(target,record,scratch));
                    writer.Write(record.OwnedCanonicalRecord);continue;
                }
                writer.Write((byte)1);writer.Write("WorldGen.Core.HistoricalTown");WideHistoricalTownBody(writer,record,scratch);
            }
            writer.Write((byte)0);
        }
        static void WideHistoricalTownBody(BinaryWriter writer,HistoricalTown record,byte[][] scratch)
        {
            if(scratch==null)scratch=new byte[][]{(byte[])WideHistoricalTownBlock0.Bytes.Clone(),(byte[])WideHistoricalTownBlock1.Bytes.Clone(),(byte[])WideHistoricalTownBlock2.Bytes.Clone(),(byte[])WideHistoricalTownBlock3.Bytes.Clone()};
            if(!SkipOptionalTownField(record,"AutonomyPressure")){writer.Write("AutonomyPressure");Write(writer,record.AutonomyPressure);}
            if(!SkipOptionalTownField(record,"CivicExampleExposure")){writer.Write("CivicExampleExposure");Write(writer,record.CivicExampleExposure);}
            if(!SkipOptionalTownField(record,"CivicPreparationYears")){writer.Write("CivicPreparationYears");Write(writer,record.CivicPreparationYears);}
            if(!SkipOptionalTownField(record,"CivicTeacherPolityId")){writer.Write("CivicTeacherPolityId");Write(writer,record.CivicTeacherPolityId);}
            if(!SkipOptionalTownField(record,"Cropping")){writer.Write("Cropping");Write(writer,record.Cropping);}
            if(!SkipOptionalTownField(record,"Demography")){writer.Write("Demography");Write(writer,record.Demography);}
            if(!SkipOptionalTownField(record,"DiseaseDeaths")){writer.Write("DiseaseDeaths");Write(writer,record.DiseaseDeaths);}
            if(!SkipOptionalTownField(record,"DiseaseKind")){writer.Write("DiseaseKind");Write(writer,record.DiseaseKind);}
            if(!SkipOptionalTownField(record,"DiseaseMortalityPermille")){writer.Write("DiseaseMortalityPermille");Write(writer,record.DiseaseMortalityPermille);}
            PutInteger(scratch[0],WideHistoricalTownBlock0.Offsets[0],record.DiseaseUntil);
            writer.Write(scratch[0]);
            if(!SkipOptionalTownField(record,"DiseaseWaveId")){writer.Write("DiseaseWaveId");Write(writer,record.DiseaseWaveId);}
            if(!SkipOptionalTownField(record,"ExportedFood")){writer.Write("ExportedFood");Write(writer,record.ExportedFood);}
            PutLong(scratch[1],WideHistoricalTownBlock1.Offsets[0],record.FoodBase);
            writer.Write(scratch[1]);
            if(!SkipOptionalTownField(record,"FoodTransportLoss")){writer.Write("FoodTransportLoss");Write(writer,record.FoodTransportLoss);}
            PutInteger(scratch[2],WideHistoricalTownBlock2.Offsets[0],record.Id);
            PutInteger(scratch[2],WideHistoricalTownBlock2.Offsets[1],record.ImmunityUntil);
            writer.Write(scratch[2]);
            if(!SkipOptionalTownField(record,"ImportedFood")){writer.Write("ImportedFood");Write(writer,record.ImportedFood);}
            if(!SkipOptionalTownField(record,"KindImmunity")){writer.Write("KindImmunity");Write(writer,record.KindImmunity);}
            if(!SkipOptionalTownField(record,"LastAutonomyActionYear")){writer.Write("LastAutonomyActionYear");Write(writer,record.LastAutonomyActionYear);}
            if(!SkipOptionalTownField(record,"LastHarvestHazard")){writer.Write("LastHarvestHazard");Write(writer,record.LastHarvestHazard);}
            if(!SkipOptionalTownField(record,"LastHarvestHazardYear")){writer.Write("LastHarvestHazardYear");Write(writer,record.LastHarvestHazardYear);}
            if(!SkipOptionalTownField(record,"ShortageYears")){writer.Write("ShortageYears");Write(writer,record.ShortageYears);}
            if(!SkipOptionalTownField(record,"UnrestPolityId")){writer.Write("UnrestPolityId");Write(writer,record.UnrestPolityId);}
            PutInteger(scratch[3],WideHistoricalTownBlock3.Offsets[0],record.Walls);
            writer.Write(scratch[3]);
        }
        static readonly ScalarRecordBlock WideHistoricalStateDynamicsBlock0=new ScalarRecordBlock(typeof(HistoricalStateDynamics),"FoundationPotential");
        static readonly ScalarRecordBlock WideHistoricalStateDynamicsBlock1=new ScalarRecordBlock(typeof(HistoricalStateDynamics),"Id","LastSuccession","LeaderId","PeakAreaKm2","PeakPopulation","PublicWorks","Strain");
        static void WideHistoricalStateDynamicsArray(BinaryWriter writer,HistoricalStateDynamics[] records)
        {
            var scratch=new byte[][]{(byte[])WideHistoricalStateDynamicsBlock0.Bytes.Clone(),(byte[])WideHistoricalStateDynamicsBlock1.Bytes.Clone()};
            foreach(var record in records)
            {
                writer.Write((byte)1);
                if(record==null){writer.Write((byte)0);continue;}
                if(ownedHistoryContext!=null&&(ownedHistoryContext.Polities[record.Id].EndedYear>=0))
                {
                    if(record.OwnedCanonicalRecord==null)record.OwnedCanonicalRecord=FreezeHistoricalRecord("WorldGen.Core.HistoricalStateDynamics",target=>WideHistoricalStateDynamicsBody(target,record,scratch));
                    writer.Write(record.OwnedCanonicalRecord);continue;
                }
                writer.Write((byte)1);writer.Write("WorldGen.Core.HistoricalStateDynamics");WideHistoricalStateDynamicsBody(writer,record,scratch);
            }
            writer.Write((byte)0);
        }
        static void WideHistoricalStateDynamicsBody(BinaryWriter writer,HistoricalStateDynamics record,byte[][] scratch)
        {
            if(scratch==null)scratch=new byte[][]{(byte[])WideHistoricalStateDynamicsBlock0.Bytes.Clone(),(byte[])WideHistoricalStateDynamicsBlock1.Bytes.Clone()};
            if(record.Cycle!=null){writer.Write("Cycle");Write(writer,record.Cycle);}
            if(record.DisputedUntil!=0){writer.Write("DisputedUntil");Write(writer,record.DisputedUntil);}
            PutInteger(scratch[0],WideHistoricalStateDynamicsBlock0.Offsets[0],record.FoundationPotential);
            writer.Write(scratch[0]);
            if(record.HouseSerial!=0){writer.Write("HouseSerial");Write(writer,record.HouseSerial);}
            PutInteger(scratch[1],WideHistoricalStateDynamicsBlock1.Offsets[0],record.Id);
            PutInteger(scratch[1],WideHistoricalStateDynamicsBlock1.Offsets[1],record.LastSuccession);
            PutInteger(scratch[1],WideHistoricalStateDynamicsBlock1.Offsets[2],record.LeaderId);
            PutLong(scratch[1],WideHistoricalStateDynamicsBlock1.Offsets[3],record.PeakAreaKm2);
            PutLong(scratch[1],WideHistoricalStateDynamicsBlock1.Offsets[4],record.PeakPopulation);
            PutInteger(scratch[1],WideHistoricalStateDynamicsBlock1.Offsets[5],record.PublicWorks);
            PutInteger(scratch[1],WideHistoricalStateDynamicsBlock1.Offsets[6],record.Strain);
            writer.Write(scratch[1]);
            writer.Write("Tradition");Write(writer,record.Tradition);
        }
        static readonly ScalarRecordBlock WideHistoricalCampaignBlock0=new ScalarRecordBlock(typeof(HistoricalCampaign),"A","Aim","AttackerId","B");
        static readonly ScalarRecordBlock WideHistoricalCampaignBlock1=new ScalarRecordBlock(typeof(HistoricalCampaign),"CasualtiesA","CasualtiesB","Conquests");
        static readonly ScalarRecordBlock WideHistoricalCampaignBlock2=new ScalarRecordBlock(typeof(HistoricalCampaign),"EndedYear","Id","LastActionYear","LeaderId");
        static readonly ScalarRecordBlock WideHistoricalCampaignBlock3=new ScalarRecordBlock(typeof(HistoricalCampaign),"Repulses","Spoils","StartedYear","TributePaid","TributeRecipientId");
        static void WideHistoricalCampaignArray(BinaryWriter writer,HistoricalCampaign[] records)
        {
            var scratch=new byte[][]{(byte[])WideHistoricalCampaignBlock0.Bytes.Clone(),(byte[])WideHistoricalCampaignBlock1.Bytes.Clone(),(byte[])WideHistoricalCampaignBlock2.Bytes.Clone(),(byte[])WideHistoricalCampaignBlock3.Bytes.Clone()};
            foreach(var record in records)
            {
                writer.Write((byte)1);
                if(record==null){writer.Write((byte)0);continue;}
                if(ownedHistoryContext!=null&&(record.EndedYear>=0))
                {
                    if(record.OwnedCanonicalRecord==null)record.OwnedCanonicalRecord=FreezeHistoricalRecord("WorldGen.Core.HistoricalCampaign",target=>WideHistoricalCampaignBody(target,record,scratch));
                    writer.Write(record.OwnedCanonicalRecord);continue;
                }
                writer.Write((byte)1);writer.Write("WorldGen.Core.HistoricalCampaign");WideHistoricalCampaignBody(writer,record,scratch);
            }
            writer.Write((byte)0);
        }
        static void WideHistoricalCampaignBody(BinaryWriter writer,HistoricalCampaign record,byte[][] scratch)
        {
            if(scratch==null)scratch=new byte[][]{(byte[])WideHistoricalCampaignBlock0.Bytes.Clone(),(byte[])WideHistoricalCampaignBlock1.Bytes.Clone(),(byte[])WideHistoricalCampaignBlock2.Bytes.Clone(),(byte[])WideHistoricalCampaignBlock3.Bytes.Clone()};
            PutInteger(scratch[0],WideHistoricalCampaignBlock0.Offsets[0],record.A);
            PutInteger(scratch[0],WideHistoricalCampaignBlock0.Offsets[1],(int)record.Aim);
            PutInteger(scratch[0],WideHistoricalCampaignBlock0.Offsets[2],record.AttackerId);
            PutInteger(scratch[0],WideHistoricalCampaignBlock0.Offsets[3],record.B);
            writer.Write(scratch[0]);
            writer.Write("CapturedSettlements");Write(writer,record.CapturedSettlements);
            PutLong(scratch[1],WideHistoricalCampaignBlock1.Offsets[0],record.CasualtiesA);
            PutLong(scratch[1],WideHistoricalCampaignBlock1.Offsets[1],record.CasualtiesB);
            PutInteger(scratch[1],WideHistoricalCampaignBlock1.Offsets[2],record.Conquests);
            writer.Write(scratch[1]);
            if(!string.IsNullOrEmpty(record.Context)){writer.Write("Context");Write(writer,record.Context);}
            PutInteger(scratch[2],WideHistoricalCampaignBlock2.Offsets[0],record.EndedYear);
            PutInteger(scratch[2],WideHistoricalCampaignBlock2.Offsets[1],record.Id);
            PutInteger(scratch[2],WideHistoricalCampaignBlock2.Offsets[2],record.LastActionYear);
            PutInteger(scratch[2],WideHistoricalCampaignBlock2.Offsets[3],record.LeaderId);
            writer.Write(scratch[2]);
            writer.Write("Name");Write(writer,record.Name);
            writer.Write("Objective");Write(writer,record.Objective);
            writer.Write("Outcome");Write(writer,record.Outcome);
            PutInteger(scratch[3],WideHistoricalCampaignBlock3.Offsets[0],record.Repulses);
            PutLong(scratch[3],WideHistoricalCampaignBlock3.Offsets[1],record.Spoils);
            PutInteger(scratch[3],WideHistoricalCampaignBlock3.Offsets[2],record.StartedYear);
            PutLong(scratch[3],WideHistoricalCampaignBlock3.Offsets[3],record.TributePaid);
            PutInteger(scratch[3],WideHistoricalCampaignBlock3.Offsets[4],record.TributeRecipientId);
            writer.Write(scratch[3]);
        }
        static readonly ScalarRecordBlock WideHistoricalAgricultureBlock0=new ScalarRecordBlock(typeof(HistoricalAgriculture),"CultureId");
        static readonly ScalarRecordBlock WideHistoricalAgricultureBlock1=new ScalarRecordBlock(typeof(HistoricalAgriculture),"NativeMask");
        static void WideHistoricalAgricultureArray(BinaryWriter writer,HistoricalAgriculture[] records)
        {
            var scratch=new byte[][]{(byte[])WideHistoricalAgricultureBlock0.Bytes.Clone(),(byte[])WideHistoricalAgricultureBlock1.Bytes.Clone()};
            foreach(var record in records)
            {
                writer.Write((byte)1);writer.Write((byte)(record==null?0:1));if(record==null)continue;writer.Write("WorldGen.Core.HistoricalAgriculture");WideHistoricalAgricultureBody(writer,record,scratch);
            }
            writer.Write((byte)0);
        }
        static void WideHistoricalAgricultureBody(BinaryWriter writer,HistoricalAgriculture record,byte[][] scratch)
        {
            if(scratch==null)scratch=new byte[][]{(byte[])WideHistoricalAgricultureBlock0.Bytes.Clone(),(byte[])WideHistoricalAgricultureBlock1.Bytes.Clone()};
            writer.Write("AdoptedYear");Write(writer,record.AdoptedYear);
            if(record.Animals!=null){writer.Write("Animals");Write(writer,record.Animals);}
            if(record.Cultivars!=null){writer.Write("Cultivars");Write(writer,record.Cultivars);}
            PutInteger(scratch[0],WideHistoricalAgricultureBlock0.Offsets[0],record.CultureId);
            writer.Write(scratch[0]);
            if(record.HusbandryYears!=null){writer.Write("HusbandryYears");Write(writer,record.HusbandryYears);}
            PutInteger(scratch[1],WideHistoricalAgricultureBlock1.Offsets[0],record.NativeMask);
            writer.Write(scratch[1]);
            writer.Write("Practice");Write(writer,record.Practice);
            if(record.PracticeCell!=null){writer.Write("PracticeCell");Write(writer,record.PracticeCell);}
            writer.Write("SourceCulture");Write(writer,record.SourceCulture);
            if(record.WildOriginCell!=null){writer.Write("WildOriginCell");Write(writer,record.WildOriginCell);}
        }
        static readonly ScalarRecordBlock WideHistoricalCultureBlock0=new ScalarRecordBlock(typeof(HistoricalCulture),"Id","Institution");
        static readonly ScalarRecordBlock WideHistoricalCultureBlock1=new ScalarRecordBlock(typeof(HistoricalCulture),"LanguageId");
        static readonly ScalarRecordBlock WideHistoricalCultureBlock2=new ScalarRecordBlock(typeof(HistoricalCulture),"ReligionId");
        static readonly ScalarRecordBlock WideHistoricalCultureBlock3=new ScalarRecordBlock(typeof(HistoricalCulture),"Sedentism","Surplus");
        static void WideHistoricalCultureArray(BinaryWriter writer,HistoricalCulture[] records)
        {
            var scratch=new byte[][]{(byte[])WideHistoricalCultureBlock0.Bytes.Clone(),(byte[])WideHistoricalCultureBlock1.Bytes.Clone(),(byte[])WideHistoricalCultureBlock2.Bytes.Clone(),(byte[])WideHistoricalCultureBlock3.Bytes.Clone()};
            foreach(var record in records)
            {
                writer.Write((byte)1);writer.Write((byte)(record==null?0:1));if(record==null)continue;writer.Write("WorldGen.Core.HistoricalCulture");WideHistoricalCultureBody(writer,record,scratch);
            }
            writer.Write((byte)0);
        }
        static void WideHistoricalCultureBody(BinaryWriter writer,HistoricalCulture record,byte[][] scratch)
        {
            if(scratch==null)scratch=new byte[][]{(byte[])WideHistoricalCultureBlock0.Bytes.Clone(),(byte[])WideHistoricalCultureBlock1.Bytes.Clone(),(byte[])WideHistoricalCultureBlock2.Bytes.Clone(),(byte[])WideHistoricalCultureBlock3.Bytes.Clone()};
            PutInteger(scratch[0],WideHistoricalCultureBlock0.Offsets[0],record.Id);
            PutInteger(scratch[0],WideHistoricalCultureBlock0.Offsets[1],record.Institution);
            writer.Write(scratch[0]);
            if(record.InstitutionalExamples!=0){writer.Write("InstitutionalExamples");Write(writer,record.InstitutionalExamples);}
            PutInteger(scratch[1],WideHistoricalCultureBlock1.Offsets[0],record.LanguageId);
            writer.Write(scratch[1]);
            if(record.ParentCultureId!=-1){writer.Write("ParentCultureId");Write(writer,record.ParentCultureId);}
            PutInteger(scratch[2],WideHistoricalCultureBlock2.Offsets[0],record.ReligionId);
            writer.Write(scratch[2]);
            writer.Write("Research");Write(writer,record.Research);
            PutInteger(scratch[3],WideHistoricalCultureBlock3.Offsets[0],record.Sedentism);
            PutInteger(scratch[3],WideHistoricalCultureBlock3.Offsets[1],record.Surplus);
            writer.Write(scratch[3]);
            if(record.Technical!=null){writer.Write("Technical");Write(writer,record.Technical);}
            writer.Write("Technology");Write(writer,record.Technology);
        }
        static readonly ScalarRecordBlock WideHistoricalCultivarBlock0=new ScalarRecordBlock(typeof(HistoricalCultivar),"CropId","DaylengthLatitudeTenths","MaturityDays");
        static readonly ScalarRecordBlock WideHistoricalCultivarBlock1=new ScalarRecordBlock(typeof(HistoricalCultivar),"OriginCulture","ParentCulture","SelectedYears","WaterRequirementPermille","YieldPermille");
        static void WideHistoricalCultivarArray(BinaryWriter writer,HistoricalCultivar[] records)
        {
            var scratch=new byte[][]{(byte[])WideHistoricalCultivarBlock0.Bytes.Clone(),(byte[])WideHistoricalCultivarBlock1.Bytes.Clone()};
            foreach(var record in records)
            {
                writer.Write((byte)1);writer.Write((byte)(record==null?0:1));if(record==null)continue;writer.Write("WorldGen.Core.HistoricalCultivar");WideHistoricalCultivarBody(writer,record,scratch);
            }
            writer.Write((byte)0);
        }
        static void WideHistoricalCultivarBody(BinaryWriter writer,HistoricalCultivar record,byte[][] scratch)
        {
            if(scratch==null)scratch=new byte[][]{(byte[])WideHistoricalCultivarBlock0.Bytes.Clone(),(byte[])WideHistoricalCultivarBlock1.Bytes.Clone()};
            PutInteger(scratch[0],WideHistoricalCultivarBlock0.Offsets[0],record.CropId);
            PutInteger(scratch[0],WideHistoricalCultivarBlock0.Offsets[1],record.DaylengthLatitudeTenths);
            PutInteger(scratch[0],WideHistoricalCultivarBlock0.Offsets[2],record.MaturityDays);
            writer.Write(scratch[0]);
            if(record.MinimumWarmTenthsC!=0){writer.Write("MinimumWarmTenthsC");Write(writer,record.MinimumWarmTenthsC);}
            PutInteger(scratch[1],WideHistoricalCultivarBlock1.Offsets[0],record.OriginCulture);
            PutInteger(scratch[1],WideHistoricalCultivarBlock1.Offsets[1],record.ParentCulture);
            PutInteger(scratch[1],WideHistoricalCultivarBlock1.Offsets[2],record.SelectedYears);
            PutInteger(scratch[1],WideHistoricalCultivarBlock1.Offsets[3],record.WaterRequirementPermille);
            PutInteger(scratch[1],WideHistoricalCultivarBlock1.Offsets[4],record.YieldPermille);
            writer.Write(scratch[1]);
        }
        static readonly ScalarRecordBlock WideSettlementBlock0=new ScalarRecordBlock(typeof(Settlement),"Cell","CraftSharePermille","CultivatedPermille","CultureId","FoodCapacity","FoodReserve","FoundedYear");
        static readonly ScalarRecordBlock WideSettlementBlock1=new ScalarRecordBlock(typeof(Settlement),"Health","Id");
        static readonly ScalarRecordBlock WideSettlementBlock2=new ScalarRecordBlock(typeof(Settlement),"PolityId","Population","Stability");
        static void WideSettlementArray(BinaryWriter writer,Settlement[] records)
        {
            var scratch=new byte[][]{(byte[])WideSettlementBlock0.Bytes.Clone(),(byte[])WideSettlementBlock1.Bytes.Clone(),(byte[])WideSettlementBlock2.Bytes.Clone()};
            foreach(var record in records)
            {
                writer.Write((byte)1);
                if(record==null){writer.Write((byte)0);continue;}
                if(ownedHistoryContext!=null&&(ownedHistoryContext.Aggregates.Living!=null&&ownedHistoryContext.Aggregates.Living.Cities[record.Id].AbandonedYear>=0))
                {
                    if(record.OwnedCanonicalRecord==null)record.OwnedCanonicalRecord=FreezeHistoricalRecord("WorldGen.Core.Settlement",target=>WideSettlementBody(target,record,scratch));
                    writer.Write(record.OwnedCanonicalRecord);continue;
                }
                writer.Write((byte)1);writer.Write("WorldGen.Core.Settlement");WideSettlementBody(writer,record,scratch);
            }
            writer.Write((byte)0);
        }
        static void WideSettlementBody(BinaryWriter writer,Settlement record,byte[][] scratch)
        {
            if(scratch==null)scratch=new byte[][]{(byte[])WideSettlementBlock0.Bytes.Clone(),(byte[])WideSettlementBlock1.Bytes.Clone(),(byte[])WideSettlementBlock2.Bytes.Clone()};
            PutInteger(scratch[0],WideSettlementBlock0.Offsets[0],record.Cell);
            PutInteger(scratch[0],WideSettlementBlock0.Offsets[1],record.CraftSharePermille);
            PutInteger(scratch[0],WideSettlementBlock0.Offsets[2],record.CultivatedPermille);
            PutInteger(scratch[0],WideSettlementBlock0.Offsets[3],record.CultureId);
            PutLong(scratch[0],WideSettlementBlock0.Offsets[4],record.FoodCapacity);
            PutLong(scratch[0],WideSettlementBlock0.Offsets[5],record.FoodReserve);
            PutInteger(scratch[0],WideSettlementBlock0.Offsets[6],record.FoundedYear);
            writer.Write(scratch[0]);
            writer.Write("Government");Write(writer,record.Government);
            PutInteger(scratch[1],WideSettlementBlock1.Offsets[0],record.Health);
            PutInteger(scratch[1],WideSettlementBlock1.Offsets[1],record.Id);
            writer.Write(scratch[1]);
            writer.Write("Name");Write(writer,record.Name);
            PutInteger(scratch[2],WideSettlementBlock2.Offsets[0],record.PolityId);
            PutLong(scratch[2],WideSettlementBlock2.Offsets[1],record.Population);
            PutInteger(scratch[2],WideSettlementBlock2.Offsets[2],record.Stability);
            writer.Write(scratch[2]);
        }
        static readonly ScalarRecordBlock WideCultureIdentityBlock0=new ScalarRecordBlock(typeof(CultureIdentity),"AreaKm2");
        static readonly ScalarRecordBlock WideCultureIdentityBlock1=new ScalarRecordBlock(typeof(CultureIdentity),"Farming","Fishing","FoundedYear","Gathering","Herding","Hunting","Id");
        static readonly ScalarRecordBlock WideCultureIdentityBlock2=new ScalarRecordBlock(typeof(CultureIdentity),"Organization","OriginCell","Population");
        static readonly ScalarRecordBlock WideCultureIdentityBlock3=new ScalarRecordBlock(typeof(CultureIdentity),"Stage","StageYear");
        static void WideCultureIdentityArray(BinaryWriter writer,CultureIdentity[] records)
        {
            var scratch=new byte[][]{(byte[])WideCultureIdentityBlock0.Bytes.Clone(),(byte[])WideCultureIdentityBlock1.Bytes.Clone(),(byte[])WideCultureIdentityBlock2.Bytes.Clone(),(byte[])WideCultureIdentityBlock3.Bytes.Clone()};
            foreach(var record in records)
            {
                writer.Write((byte)1);writer.Write((byte)(record==null?0:1));if(record==null)continue;writer.Write("WorldGen.Core.CultureIdentity");WideCultureIdentityBody(writer,record,scratch);
            }
            writer.Write((byte)0);
        }
        static void WideCultureIdentityBody(BinaryWriter writer,CultureIdentity record,byte[][] scratch)
        {
            if(scratch==null)scratch=new byte[][]{(byte[])WideCultureIdentityBlock0.Bytes.Clone(),(byte[])WideCultureIdentityBlock1.Bytes.Clone(),(byte[])WideCultureIdentityBlock2.Bytes.Clone(),(byte[])WideCultureIdentityBlock3.Bytes.Clone()};
            PutLong(scratch[0],WideCultureIdentityBlock0.Offsets[0],record.AreaKm2);
            writer.Write(scratch[0]);
            writer.Write("Contacts");Write(writer,record.Contacts);
            PutInteger(scratch[1],WideCultureIdentityBlock1.Offsets[0],record.Farming);
            PutInteger(scratch[1],WideCultureIdentityBlock1.Offsets[1],record.Fishing);
            PutInteger(scratch[1],WideCultureIdentityBlock1.Offsets[2],record.FoundedYear);
            PutInteger(scratch[1],WideCultureIdentityBlock1.Offsets[3],record.Gathering);
            PutInteger(scratch[1],WideCultureIdentityBlock1.Offsets[4],record.Herding);
            PutInteger(scratch[1],WideCultureIdentityBlock1.Offsets[5],record.Hunting);
            PutInteger(scratch[1],WideCultureIdentityBlock1.Offsets[6],record.Id);
            writer.Write(scratch[1]);
            writer.Write("Name");Write(writer,record.Name);
            PutInteger(scratch[2],WideCultureIdentityBlock2.Offsets[0],record.Organization);
            PutInteger(scratch[2],WideCultureIdentityBlock2.Offsets[1],record.OriginCell);
            PutLong(scratch[2],WideCultureIdentityBlock2.Offsets[2],record.Population);
            writer.Write(scratch[2]);
            writer.Write("Resources");Write(writer,record.Resources);
            PutInteger(scratch[3],WideCultureIdentityBlock3.Offsets[0],(int)record.Stage);
            PutInteger(scratch[3],WideCultureIdentityBlock3.Offsets[1],record.StageYear);
            writer.Write(scratch[3]);
            writer.Write("Subsistence");Write(writer,record.Subsistence);
        }
        static readonly ScalarRecordBlock WideHistoricalAdministrativeAccountBlock0=new ScalarRecordBlock(typeof(HistoricalAdministrativeAccount),"DisconnectedResidents");
        static readonly ScalarRecordBlock WideHistoricalAdministrativeAccountBlock1=new ScalarRecordBlock(typeof(HistoricalAdministrativeAccount),"MaximumServiceDistanceM","PolityId","ReachM","RuralReceipts","ServiceCost");
        static readonly ScalarRecordBlock WideHistoricalAdministrativeAccountBlock2=new ScalarRecordBlock(typeof(HistoricalAdministrativeAccount),"Year");
        static void WideHistoricalAdministrativeAccountArray(BinaryWriter writer,HistoricalAdministrativeAccount[] records)
        {
            var scratch=new byte[][]{(byte[])WideHistoricalAdministrativeAccountBlock0.Bytes.Clone(),(byte[])WideHistoricalAdministrativeAccountBlock1.Bytes.Clone(),(byte[])WideHistoricalAdministrativeAccountBlock2.Bytes.Clone()};
            foreach(var record in records)
            {
                writer.Write((byte)1);writer.Write((byte)(record==null?0:1));if(record==null)continue;writer.Write("WorldGen.Core.HistoricalAdministrativeAccount");WideHistoricalAdministrativeAccountBody(writer,record,scratch);
            }
            writer.Write((byte)0);
        }
        static void WideHistoricalAdministrativeAccountBody(BinaryWriter writer,HistoricalAdministrativeAccount record,byte[][] scratch)
        {
            if(scratch==null)scratch=new byte[][]{(byte[])WideHistoricalAdministrativeAccountBlock0.Bytes.Clone(),(byte[])WideHistoricalAdministrativeAccountBlock1.Bytes.Clone(),(byte[])WideHistoricalAdministrativeAccountBlock2.Bytes.Clone()};
            PutLong(scratch[0],WideHistoricalAdministrativeAccountBlock0.Offsets[0],record.DisconnectedResidents);
            writer.Write(scratch[0]);
            if(record.DisconnectedUrbanResidents!=0){writer.Write("DisconnectedUrbanResidents");Write(writer,record.DisconnectedUrbanResidents);}
            PutInteger(scratch[1],WideHistoricalAdministrativeAccountBlock1.Offsets[0],record.MaximumServiceDistanceM);
            PutInteger(scratch[1],WideHistoricalAdministrativeAccountBlock1.Offsets[1],record.PolityId);
            PutInteger(scratch[1],WideHistoricalAdministrativeAccountBlock1.Offsets[2],record.ReachM);
            PutLong(scratch[1],WideHistoricalAdministrativeAccountBlock1.Offsets[3],record.RuralReceipts);
            PutLong(scratch[1],WideHistoricalAdministrativeAccountBlock1.Offsets[4],record.ServiceCost);
            writer.Write(scratch[1]);
            if(record.TaxableUrbanResidents!=0){writer.Write("TaxableUrbanResidents");Write(writer,record.TaxableUrbanResidents);}
            if(record.UrbanReceipts!=0){writer.Write("UrbanReceipts");Write(writer,record.UrbanReceipts);}
            PutInteger(scratch[2],WideHistoricalAdministrativeAccountBlock2.Offsets[0],record.Year);
            writer.Write(scratch[2]);
        }
        static readonly ScalarRecordBlock WideHistoricalUrbanDemographyBlock0=new ScalarRecordBlock(typeof(HistoricalUrbanDemography),"ChronicLoss","NaturalChange","RuralInflow","RuralOutflow","TotalChronicLoss","TotalRuralInflow","TotalRuralOutflow","Year");
        static void WideHistoricalUrbanDemographyArray(BinaryWriter writer,HistoricalUrbanDemography[] records)
        {
            var scratch=new byte[][]{(byte[])WideHistoricalUrbanDemographyBlock0.Bytes.Clone()};
            foreach(var record in records)
            {
                writer.Write((byte)1);writer.Write((byte)(record==null?0:1));if(record==null)continue;writer.Write("WorldGen.Core.HistoricalUrbanDemography");WideHistoricalUrbanDemographyBody(writer,record,scratch);
            }
            writer.Write((byte)0);
        }
        static void WideHistoricalUrbanDemographyBody(BinaryWriter writer,HistoricalUrbanDemography record,byte[][] scratch)
        {
            if(scratch==null)scratch=new byte[][]{(byte[])WideHistoricalUrbanDemographyBlock0.Bytes.Clone()};
            PutLong(scratch[0],WideHistoricalUrbanDemographyBlock0.Offsets[0],record.ChronicLoss);
            PutLong(scratch[0],WideHistoricalUrbanDemographyBlock0.Offsets[1],record.NaturalChange);
            PutLong(scratch[0],WideHistoricalUrbanDemographyBlock0.Offsets[2],record.RuralInflow);
            PutLong(scratch[0],WideHistoricalUrbanDemographyBlock0.Offsets[3],record.RuralOutflow);
            PutLong(scratch[0],WideHistoricalUrbanDemographyBlock0.Offsets[4],record.TotalChronicLoss);
            PutLong(scratch[0],WideHistoricalUrbanDemographyBlock0.Offsets[5],record.TotalRuralInflow);
            PutLong(scratch[0],WideHistoricalUrbanDemographyBlock0.Offsets[6],record.TotalRuralOutflow);
            PutInteger(scratch[0],WideHistoricalUrbanDemographyBlock0.Offsets[7],record.Year);
            writer.Write(scratch[0]);
        }
        static readonly ScalarRecordBlock WideHistoricalLocalHarvestBlock0=new ScalarRecordBlock(typeof(HistoricalLocalHarvest),"CultureId");
        static readonly ScalarRecordBlock WideHistoricalLocalHarvestBlock1=new ScalarRecordBlock(typeof(HistoricalLocalHarvest),"Year");
        static void WideHistoricalLocalHarvestArray(BinaryWriter writer,HistoricalLocalHarvest[] records)
        {
            var scratch=new byte[][]{(byte[])WideHistoricalLocalHarvestBlock0.Bytes.Clone(),(byte[])WideHistoricalLocalHarvestBlock1.Bytes.Clone()};
            foreach(var record in records)
            {
                writer.Write((byte)1);writer.Write((byte)(record==null?0:1));if(record==null)continue;writer.Write("WorldGen.Core.HistoricalLocalHarvest");WideHistoricalLocalHarvestBody(writer,record,scratch);
            }
            writer.Write((byte)0);
        }
        static void WideHistoricalLocalHarvestBody(BinaryWriter writer,HistoricalLocalHarvest record,byte[][] scratch)
        {
            if(scratch==null)scratch=new byte[][]{(byte[])WideHistoricalLocalHarvestBlock0.Bytes.Clone(),(byte[])WideHistoricalLocalHarvestBlock1.Bytes.Clone()};
            writer.Write("CropIds");Write(writer,record.CropIds);
            PutInteger(scratch[0],WideHistoricalLocalHarvestBlock0.Offsets[0],record.CultureId);
            writer.Write(scratch[0]);
            writer.Write("SharesPermille");Write(writer,record.SharesPermille);
            PutInteger(scratch[1],WideHistoricalLocalHarvestBlock1.Offsets[0],record.Year);
            writer.Write(scratch[1]);
        }
        static readonly ScalarRecordBlock WideHistoricalLocalCommunityBlock0=new ScalarRecordBlock(typeof(HistoricalLocalCommunity),"InclusionMilli","RulerId","SinceYear");
        static void WideHistoricalLocalCommunityArray(BinaryWriter writer,HistoricalLocalCommunity[] records)
        {
            var scratch=new byte[][]{(byte[])WideHistoricalLocalCommunityBlock0.Bytes.Clone()};
            foreach(var record in records)
            {
                writer.Write((byte)1);writer.Write((byte)(record==null?0:1));if(record==null)continue;writer.Write("WorldGen.Core.HistoricalLocalCommunity");WideHistoricalLocalCommunityBody(writer,record,scratch);
            }
            writer.Write((byte)0);
        }
        static void WideHistoricalLocalCommunityBody(BinaryWriter writer,HistoricalLocalCommunity record,byte[][] scratch)
        {
            if(scratch==null)scratch=new byte[][]{(byte[])WideHistoricalLocalCommunityBlock0.Bytes.Clone()};
            PutInteger(scratch[0],WideHistoricalLocalCommunityBlock0.Offsets[0],record.InclusionMilli);
            PutInteger(scratch[0],WideHistoricalLocalCommunityBlock0.Offsets[1],record.RulerId);
            PutInteger(scratch[0],WideHistoricalLocalCommunityBlock0.Offsets[2],record.SinceYear);
            writer.Write(scratch[0]);
        }
        static readonly ScalarRecordBlock WideHistoricalGovernmentPolicyBlock0=new ScalarRecordBlock(typeof(HistoricalGovernmentPolicy),"CentralizationMilli","LastReformYear","RepresentationMilli");
        static void WideHistoricalGovernmentPolicyArray(BinaryWriter writer,HistoricalGovernmentPolicy[] records)
        {
            var scratch=new byte[][]{(byte[])WideHistoricalGovernmentPolicyBlock0.Bytes.Clone()};
            foreach(var record in records)
            {
                writer.Write((byte)1);writer.Write((byte)(record==null?0:1));if(record==null)continue;writer.Write("WorldGen.Core.HistoricalGovernmentPolicy");WideHistoricalGovernmentPolicyBody(writer,record,scratch);
            }
            writer.Write((byte)0);
        }
        static void WideHistoricalGovernmentPolicyBody(BinaryWriter writer,HistoricalGovernmentPolicy record,byte[][] scratch)
        {
            if(scratch==null)scratch=new byte[][]{(byte[])WideHistoricalGovernmentPolicyBlock0.Bytes.Clone()};
            PutInteger(scratch[0],WideHistoricalGovernmentPolicyBlock0.Offsets[0],record.CentralizationMilli);
            PutInteger(scratch[0],WideHistoricalGovernmentPolicyBlock0.Offsets[1],record.LastReformYear);
            PutInteger(scratch[0],WideHistoricalGovernmentPolicyBlock0.Offsets[2],record.RepresentationMilli);
            writer.Write(scratch[0]);
        }
        static readonly ScalarRecordBlock WideHistoricalCampaignAdvanceBlock0=new ScalarRecordBlock(typeof(HistoricalCampaignAdvance),"AttackerAreaKm2","DefenderAreaKm2");
        static void WideHistoricalCampaignAdvanceArray(BinaryWriter writer,HistoricalCampaignAdvance[] records)
        {
            var scratch=new byte[][]{(byte[])WideHistoricalCampaignAdvanceBlock0.Bytes.Clone()};
            foreach(var record in records)
            {
                writer.Write((byte)1);writer.Write((byte)(record==null?0:1));if(record==null)continue;writer.Write("WorldGen.Core.HistoricalCampaignAdvance");WideHistoricalCampaignAdvanceBody(writer,record,scratch);
            }
            writer.Write((byte)0);
        }
        static void WideHistoricalCampaignAdvanceBody(BinaryWriter writer,HistoricalCampaignAdvance record,byte[][] scratch)
        {
            if(scratch==null)scratch=new byte[][]{(byte[])WideHistoricalCampaignAdvanceBlock0.Bytes.Clone()};
            PutLong(scratch[0],WideHistoricalCampaignAdvanceBlock0.Offsets[0],record.AttackerAreaKm2);
            PutLong(scratch[0],WideHistoricalCampaignAdvanceBlock0.Offsets[1],record.DefenderAreaKm2);
            writer.Write(scratch[0]);
        }
    }
}
