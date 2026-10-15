using System;
using System.IO;
namespace WorldGen.Core
{
    public static partial class StateHash
    {
        // Immutable canonical labels, per-call scratch and live field reads.
        // This preserves the reflection byte format; no model values are cached.
        sealed class ScalarRecordBlock
        {
            public readonly byte[] Bytes;public readonly int[] Offsets;
            public ScalarRecordBlock(Type type,params string[] fields)
            {
                using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
                {
                    Offsets=new int[fields.Length];
                    for(int i=0;i<fields.Length;i++)
                    {
                        var field=type.GetField(fields[i]);var valueType=field.FieldType;
                        writer.Write(fields[i]);writer.Write((byte)1);writer.Write(valueType.FullName);Offsets[i]=(int)stream.Position;
                        if(valueType==typeof(int)||valueType.IsEnum)writer.Write(0);
                        else if(valueType==typeof(long))writer.Write(0L);
                        else if(valueType==typeof(double))writer.Write(0.0);
                        else if(valueType==typeof(bool))writer.Write(false);
                        else throw new InvalidOperationException("Unsupported scalar record field "+field.Name);
                    }
                    Bytes=stream.ToArray();
                }
            }
        }
        static void PutLong(byte[] bytes,int at,long value){for(int k=0;k<8;k++)bytes[at+k]=(byte)(value>>(k*8));}
        static void PutDouble(byte[] bytes,int at,double value){double q=D.Q(value,1e6);PutLong(bytes,at,BitConverter.DoubleToInt64Bits(q==0?0.0:q));}
        static readonly ScalarRecordBlock HistoricalCityAggregateBlock0=new ScalarRecordBlock(typeof(HistoricalCityAggregate),"AreaKm2","BaseCropFactor","BaseFarming","BaseFoodMultiplier","BaseUrbanCapacity","ExchangeMilli","FloodMilli","FoodYieldMilli","Id","LastHarvestYear","LastPoliticalYear","LastVoyageYear","LastYear");
        static readonly ScalarRecordBlock HistoricalCityAggregateBlock1=new ScalarRecordBlock(typeof(HistoricalCityAggregate),"ParentCity","Rural","RuralCapacity","SoilMilli","StoredYieldMilli","Suburban","Supported");
        static void HistoricalCityAggregateArrayBody(BinaryWriter writer,HistoricalCityAggregate[] records)
        {
            var block0=(byte[])HistoricalCityAggregateBlock0.Bytes.Clone();
            var block1=(byte[])HistoricalCityAggregateBlock1.Bytes.Clone();
            foreach(var record in records)
            {
                if(record!=null&&ownedHistoryContext?.Aggregates.Living!=null&&ownedHistoryContext.Aggregates.Living.Cities[record.Id].AbandonedYear>=0)
                {
                    if(record.OwnedCanonicalRecord==null)record.OwnedCanonicalRecord=FreezeHistoricalRecord(record);
                    writer.Write((byte)1);writer.Write(record.OwnedCanonicalRecord);continue;
                }
                writer.Write((byte)1);writer.Write((byte)(record==null?0:1));if(record==null)continue;writer.Write("WorldGen.Core.HistoricalCityAggregate");
                PutLong(block0,HistoricalCityAggregateBlock0.Offsets[0],record.AreaKm2);
                PutDouble(block0,HistoricalCityAggregateBlock0.Offsets[1],record.BaseCropFactor);
                PutInteger(block0,HistoricalCityAggregateBlock0.Offsets[2],record.BaseFarming);
                PutDouble(block0,HistoricalCityAggregateBlock0.Offsets[3],record.BaseFoodMultiplier);
                PutLong(block0,HistoricalCityAggregateBlock0.Offsets[4],record.BaseUrbanCapacity);
                PutInteger(block0,HistoricalCityAggregateBlock0.Offsets[5],record.ExchangeMilli);
                PutInteger(block0,HistoricalCityAggregateBlock0.Offsets[6],record.FloodMilli);
                PutInteger(block0,HistoricalCityAggregateBlock0.Offsets[7],record.FoodYieldMilli);
                PutInteger(block0,HistoricalCityAggregateBlock0.Offsets[8],record.Id);
                PutInteger(block0,HistoricalCityAggregateBlock0.Offsets[9],record.LastHarvestYear);
                PutInteger(block0,HistoricalCityAggregateBlock0.Offsets[10],record.LastPoliticalYear);
                PutInteger(block0,HistoricalCityAggregateBlock0.Offsets[11],record.LastVoyageYear);
                PutInteger(block0,HistoricalCityAggregateBlock0.Offsets[12],record.LastYear);
                writer.Write(block0);
                writer.Write("Neighbors");Write(writer,record.Neighbors);
                PutInteger(block1,HistoricalCityAggregateBlock1.Offsets[0],record.ParentCity);
                PutLong(block1,HistoricalCityAggregateBlock1.Offsets[1],record.Rural);
                PutLong(block1,HistoricalCityAggregateBlock1.Offsets[2],record.RuralCapacity);
                PutInteger(block1,HistoricalCityAggregateBlock1.Offsets[3],record.SoilMilli);
                PutInteger(block1,HistoricalCityAggregateBlock1.Offsets[4],record.StoredYieldMilli);
                PutLong(block1,HistoricalCityAggregateBlock1.Offsets[5],record.Suburban);
                block1[HistoricalCityAggregateBlock1.Offsets[6]]=(byte)(record.Supported?1:0);
                writer.Write(block1);
            }
            writer.Write((byte)0);
        }
        static readonly ScalarRecordBlock HistoricalRegionAggregateBlock0=new ScalarRecordBlock(typeof(HistoricalRegionAggregate),"AreaKm2","AridityMilli","BestCell","Capacity","Culture","Id","LastCultureYear","LastYear","MeanTenthsC","NativeMask","OpportunityMilli","ReliefM");
        static readonly ScalarRecordBlock HistoricalRegionAggregateBlock1=new ScalarRecordBlock(typeof(HistoricalRegionAggregate),"SoilMilli","StoredMilli","YieldMilli");
        static void HistoricalRegionAggregateArrayBody(BinaryWriter writer,HistoricalRegionAggregate[] records)
        {
            var block0=(byte[])HistoricalRegionAggregateBlock0.Bytes.Clone();
            var block1=(byte[])HistoricalRegionAggregateBlock1.Bytes.Clone();
            foreach(var record in records)
            {
                writer.Write((byte)1);writer.Write((byte)(record==null?0:1));if(record==null)continue;writer.Write("WorldGen.Core.HistoricalRegionAggregate");
                PutLong(block0,HistoricalRegionAggregateBlock0.Offsets[0],record.AreaKm2);
                PutInteger(block0,HistoricalRegionAggregateBlock0.Offsets[1],record.AridityMilli);
                PutInteger(block0,HistoricalRegionAggregateBlock0.Offsets[2],record.BestCell);
                PutLong(block0,HistoricalRegionAggregateBlock0.Offsets[3],record.Capacity);
                PutInteger(block0,HistoricalRegionAggregateBlock0.Offsets[4],record.Culture);
                PutInteger(block0,HistoricalRegionAggregateBlock0.Offsets[5],record.Id);
                PutInteger(block0,HistoricalRegionAggregateBlock0.Offsets[6],record.LastCultureYear);
                PutInteger(block0,HistoricalRegionAggregateBlock0.Offsets[7],record.LastYear);
                PutInteger(block0,HistoricalRegionAggregateBlock0.Offsets[8],record.MeanTenthsC);
                PutInteger(block0,HistoricalRegionAggregateBlock0.Offsets[9],record.NativeMask);
                PutInteger(block0,HistoricalRegionAggregateBlock0.Offsets[10],record.OpportunityMilli);
                PutInteger(block0,HistoricalRegionAggregateBlock0.Offsets[11],record.ReliefM);
                writer.Write(block0);
                writer.Write("Routes");Write(writer,record.Routes);
                PutInteger(block1,HistoricalRegionAggregateBlock1.Offsets[0],record.SoilMilli);
                PutInteger(block1,HistoricalRegionAggregateBlock1.Offsets[1],record.StoredMilli);
                PutInteger(block1,HistoricalRegionAggregateBlock1.Offsets[2],record.YieldMilli);
                writer.Write(block1);
            }
            writer.Write((byte)0);
        }
        static readonly ScalarRecordBlock HistoricalCultureAggregateBlock0=new ScalarRecordBlock(typeof(HistoricalCultureAggregate),"CityCount","CivilizationSince","FoodCapacity");
        static readonly ScalarRecordBlock HistoricalCultureAggregateBlock1=new ScalarRecordBlock(typeof(HistoricalCultureAggregate),"Id","LargestCity","LastYear","NextTechnology","Region","Rural","SupportedCities","SupportedUrban","Urban");
        static void HistoricalCultureAggregateArrayBody(BinaryWriter writer,HistoricalCultureAggregate[] records)
        {
            var block0=(byte[])HistoricalCultureAggregateBlock0.Bytes.Clone();
            var block1=(byte[])HistoricalCultureAggregateBlock1.Bytes.Clone();
            foreach(var record in records)
            {
                writer.Write((byte)1);writer.Write((byte)(record==null?0:1));if(record==null)continue;writer.Write("WorldGen.Core.HistoricalCultureAggregate");
                writer.Write("Cities");Write(writer,record.Cities);
                PutInteger(block0,HistoricalCultureAggregateBlock0.Offsets[0],record.CityCount);
                PutInteger(block0,HistoricalCultureAggregateBlock0.Offsets[1],record.CivilizationSince);
                PutLong(block0,HistoricalCultureAggregateBlock0.Offsets[2],record.FoodCapacity);
                writer.Write(block0);
                writer.Write("Frontier");Write(writer,record.Frontier);
                PutInteger(block1,HistoricalCultureAggregateBlock1.Offsets[0],record.Id);
                PutLong(block1,HistoricalCultureAggregateBlock1.Offsets[1],record.LargestCity);
                PutInteger(block1,HistoricalCultureAggregateBlock1.Offsets[2],record.LastYear);
                PutInteger(block1,HistoricalCultureAggregateBlock1.Offsets[3],record.NextTechnology);
                PutInteger(block1,HistoricalCultureAggregateBlock1.Offsets[4],record.Region);
                PutLong(block1,HistoricalCultureAggregateBlock1.Offsets[5],record.Rural);
                PutInteger(block1,HistoricalCultureAggregateBlock1.Offsets[6],record.SupportedCities);
                PutLong(block1,HistoricalCultureAggregateBlock1.Offsets[7],record.SupportedUrban);
                PutLong(block1,HistoricalCultureAggregateBlock1.Offsets[8],record.Urban);
                writer.Write(block1);
            }
            writer.Write((byte)0);
        }
        static readonly ScalarRecordBlock HistoricalPolityAggregateBlock0=new ScalarRecordBlock(typeof(HistoricalPolityAggregate),"CityCount","ConquestDebtMilli");
        static readonly ScalarRecordBlock HistoricalPolityAggregateBlock1=new ScalarRecordBlock(typeof(HistoricalPolityAggregate),"Id","LastSuccession","LastYear","ReachKm","Receipts","Remote","RemoteAreaKm2","RemoteCapacity","Rural","ServiceCost","Suburban","SuccessionDue","UnrestMilli","Urban");
        static void HistoricalPolityAggregateArrayBody(BinaryWriter writer,HistoricalPolityAggregate[] records)
        {
            var block0=(byte[])HistoricalPolityAggregateBlock0.Bytes.Clone();
            var block1=(byte[])HistoricalPolityAggregateBlock1.Bytes.Clone();
            foreach(var record in records)
            {
                writer.Write((byte)1);writer.Write((byte)(record==null?0:1));if(record==null)continue;writer.Write("WorldGen.Core.HistoricalPolityAggregate");
                writer.Write("Cities");Write(writer,record.Cities);
                PutInteger(block0,HistoricalPolityAggregateBlock0.Offsets[0],record.CityCount);
                PutInteger(block0,HistoricalPolityAggregateBlock0.Offsets[1],record.ConquestDebtMilli);
                writer.Write(block0);
                writer.Write("Frontier");Write(writer,record.Frontier);
                PutInteger(block1,HistoricalPolityAggregateBlock1.Offsets[0],record.Id);
                PutInteger(block1,HistoricalPolityAggregateBlock1.Offsets[1],record.LastSuccession);
                PutInteger(block1,HistoricalPolityAggregateBlock1.Offsets[2],record.LastYear);
                PutInteger(block1,HistoricalPolityAggregateBlock1.Offsets[3],record.ReachKm);
                PutLong(block1,HistoricalPolityAggregateBlock1.Offsets[4],record.Receipts);
                PutLong(block1,HistoricalPolityAggregateBlock1.Offsets[5],record.Remote);
                PutLong(block1,HistoricalPolityAggregateBlock1.Offsets[6],record.RemoteAreaKm2);
                PutLong(block1,HistoricalPolityAggregateBlock1.Offsets[7],record.RemoteCapacity);
                PutLong(block1,HistoricalPolityAggregateBlock1.Offsets[8],record.Rural);
                PutLong(block1,HistoricalPolityAggregateBlock1.Offsets[9],record.ServiceCost);
                PutLong(block1,HistoricalPolityAggregateBlock1.Offsets[10],record.Suburban);
                PutInteger(block1,HistoricalPolityAggregateBlock1.Offsets[11],record.SuccessionDue);
                PutInteger(block1,HistoricalPolityAggregateBlock1.Offsets[12],record.UnrestMilli);
                PutLong(block1,HistoricalPolityAggregateBlock1.Offsets[13],record.Urban);
                writer.Write(block1);
            }
            writer.Write((byte)0);
        }
        static readonly ScalarRecordBlock HistoricalLivingCityBlock0=new ScalarRecordBlock(typeof(HistoricalLivingCity),"AbandonedYear","DeclineYears","DisruptedUntil","LastContactYear","PeakPopulation","PeakYear","Productivity","StressMilli");
        static void HistoricalLivingCityArrayBody(BinaryWriter writer,HistoricalLivingCity[] records)
        {
            var block0=(byte[])HistoricalLivingCityBlock0.Bytes.Clone();
            foreach(var record in records)
            {
                writer.Write((byte)1);writer.Write((byte)(record==null?0:1));if(record==null)continue;writer.Write("WorldGen.Core.HistoricalLivingCity");
                PutInteger(block0,HistoricalLivingCityBlock0.Offsets[0],record.AbandonedYear);
                PutInteger(block0,HistoricalLivingCityBlock0.Offsets[1],record.DeclineYears);
                PutInteger(block0,HistoricalLivingCityBlock0.Offsets[2],record.DisruptedUntil);
                PutInteger(block0,HistoricalLivingCityBlock0.Offsets[3],record.LastContactYear);
                PutLong(block0,HistoricalLivingCityBlock0.Offsets[4],record.PeakPopulation);
                PutInteger(block0,HistoricalLivingCityBlock0.Offsets[5],record.PeakYear);
                PutDouble(block0,HistoricalLivingCityBlock0.Offsets[6],record.Productivity);
                PutInteger(block0,HistoricalLivingCityBlock0.Offsets[7],record.StressMilli);
                writer.Write(block0);
            }
            writer.Write((byte)0);
        }
        static readonly ScalarRecordBlock HistoricalLivingRegionBlock0=new ScalarRecordBlock(typeof(HistoricalLivingRegion),"CivicExposure","CivicTeacher","CommittedAreaKm2","Exposure","LastExchangeYear","SettledPopulation","Teacher");
        static void HistoricalLivingRegionArrayBody(BinaryWriter writer,HistoricalLivingRegion[] records)
        {
            var block0=(byte[])HistoricalLivingRegionBlock0.Bytes.Clone();
            foreach(var record in records)
            {
                writer.Write((byte)1);writer.Write((byte)(record==null?0:1));if(record==null)continue;writer.Write("WorldGen.Core.HistoricalLivingRegion");
                PutInteger(block0,HistoricalLivingRegionBlock0.Offsets[0],record.CivicExposure);
                PutInteger(block0,HistoricalLivingRegionBlock0.Offsets[1],record.CivicTeacher);
                PutLong(block0,HistoricalLivingRegionBlock0.Offsets[2],record.CommittedAreaKm2);
                PutInteger(block0,HistoricalLivingRegionBlock0.Offsets[3],record.Exposure);
                PutInteger(block0,HistoricalLivingRegionBlock0.Offsets[4],record.LastExchangeYear);
                PutLong(block0,HistoricalLivingRegionBlock0.Offsets[5],record.SettledPopulation);
                PutInteger(block0,HistoricalLivingRegionBlock0.Offsets[6],record.Teacher);
                writer.Write(block0);
            }
            writer.Write((byte)0);
        }
        static void HistoricalExchangeMemoryArrayBody(BinaryWriter writer,HistoricalExchangeMemory[] records)
        {
            foreach(var record in records)
            {
                writer.Write((byte)1);writer.Write((byte)(record==null?0:1));if(record==null)continue;writer.Write("WorldGen.Core.HistoricalExchangeMemory");
                writer.Write("Capabilities");Write(writer,record.Capabilities);
                writer.Write("LastDirectYear");Write(writer,record.LastDirectYear);
                writer.Write("LastYear");Write(writer,record.LastYear);
                writer.Write("Peers");Write(writer,record.Peers);
            }
            writer.Write((byte)0);
        }
        static readonly ScalarRecordBlock HistoricalCampaignTheaterBlock0=new ScalarRecordBlock(typeof(HistoricalCampaignTheater),"Cell","SupplyA","SupplyB");
        static void HistoricalCampaignTheaterArrayBody(BinaryWriter writer,HistoricalCampaignTheater[] records)
        {
            var block0=(byte[])HistoricalCampaignTheaterBlock0.Bytes.Clone();
            foreach(var record in records)
            {
                writer.Write((byte)1);writer.Write((byte)(record==null?0:1));if(record==null)continue;writer.Write("WorldGen.Core.HistoricalCampaignTheater");
                PutInteger(block0,HistoricalCampaignTheaterBlock0.Offsets[0],record.Cell);
                PutInteger(block0,HistoricalCampaignTheaterBlock0.Offsets[1],record.SupplyA);
                PutInteger(block0,HistoricalCampaignTheaterBlock0.Offsets[2],record.SupplyB);
                writer.Write(block0);
            }
            writer.Write((byte)0);
        }
    }
}
