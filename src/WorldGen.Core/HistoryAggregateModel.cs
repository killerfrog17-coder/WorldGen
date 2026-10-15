using System;
using System.Linq;
namespace WorldGen.Core
{
    // Runtime events address actors, never a materialized list of terrain holdings.
    // The persistent bags support random, local frontier work and O(log n) removal.
    [Serializable] public sealed class HistoricalLocalBag
    {
        public int Count;
        public HistoricalCellTree Items=new HistoricalCellTree(33554432),Positions=new HistoricalCellTree(33554432);
        public HistoricalLocalBag Copy()=>(HistoricalLocalBag)MemberwiseClone();
        public void Add(int cell){if(Positions[cell]>=0)return;Positions=Positions.With(cell,Count);Items=Items.With(Count++,cell);}
        public void Remove(int cell){int at=Positions[cell];if(at<0)return;int last=Items[--Count];Items=Items.With(at,last).With(Count,-1);Positions=Positions.With(last,at).With(cell,-1);}
        public int At(int index)=>index>=0&&index<Count?Items[index]:-1;
    }
    [Serializable] public sealed class HistoricalCityAggregate
    {
        [NonSerialized] internal byte[] OwnedCanonicalRecord;
        public int Id,LastYear,LastHarvestYear,LastPoliticalYear,LastVoyageYear,ParentCity=-1;
        public long Suburban,Rural,RuralCapacity,AreaKm2,BaseUrbanCapacity;
        public int BaseFarming;
        public bool Supported;
        public double BaseFoodMultiplier=1;
        public double BaseCropFactor=1;
        public int FoodYieldMilli,StoredYieldMilli,FloodMilli,ExchangeMilli,SoilMilli;
        public int[] Neighbors=Array.Empty<int>(); // bounded, physical city contacts
        public HistoricalCityAggregate Copy(){var c=(HistoricalCityAggregate)MemberwiseClone();c.Neighbors=(int[])Neighbors.Clone();return c;}
    }
    [Serializable] public sealed class HistoricalCultureAggregate
    {
        public int Id,LastYear,CityCount,SupportedCities,NextTechnology,Region=-1,CivilizationSince=-1;
        public long Urban,Rural,SupportedUrban,LargestCity,FoodCapacity;
        public HistoricalLocalBag Frontier=new HistoricalLocalBag(),Cities=new HistoricalLocalBag();
        public HistoricalCultureAggregate Copy(){var c=(HistoricalCultureAggregate)MemberwiseClone();c.Frontier=Frontier.Copy();c.Cities=Cities.Copy();return c;}
    }
    [Serializable] public sealed class HistoricalPolityAggregate
    {
        public int Id,LastYear,LastSuccession,CityCount,SuccessionDue;
        public long Urban,Suburban,Rural,Remote,RemoteCapacity,RemoteAreaKm2,Receipts,ServiceCost;
        public int ReachKm,UnrestMilli,ConquestDebtMilli;
        public HistoricalLocalBag Frontier=new HistoricalLocalBag(),Cities=new HistoricalLocalBag();
        public HistoricalPolityAggregate Copy(){var c=(HistoricalPolityAggregate)MemberwiseClone();c.Frontier=Frontier.Copy();c.Cities=Cities.Copy();return c;}
    }
    [Serializable] public sealed class HistoricalRegionAggregate
    {
        public int Id,LastYear,BestCell,NativeMask,Culture=-1,LastCultureYear;
        public long Capacity,AreaKm2;
        public int OpportunityMilli,YieldMilli,StoredMilli,SoilMilli,MeanTenthsC,AridityMilli,ReliefM;
        public int[] Routes=Array.Empty<int>();
        public HistoricalRegionAggregate Copy(){var c=(HistoricalRegionAggregate)MemberwiseClone();c.Routes=(int[])Routes.Clone();return c;}
    }
    [Serializable] public sealed class HistoricalAggregateState
    {
        public string Version="aggregate-history-1";
        [System.Runtime.Serialization.OptionalField] public HistoricalLivingHistory Living;
        [System.Runtime.Serialization.OptionalField] public HistoricalProvincialSupport Provinces;
        [System.Runtime.Serialization.OptionalField] public HistoricalCommunityDynamics Communities;
        public HistoricalCellTree Cells,TownAt,Successors,LeaderDigests;
        public HistoricalFrozenAdministration PreviousAdministration;
        public HistoricalCityAggregate[] Cities=Array.Empty<HistoricalCityAggregate>();
        public HistoricalCultureAggregate[] Cultures=Array.Empty<HistoricalCultureAggregate>();
        public HistoricalPolityAggregate[] Polities=Array.Empty<HistoricalPolityAggregate>();
        public HistoricalRegionAggregate[] Regions=Array.Empty<HistoricalRegionAggregate>();
        public int ArchiveBaseCount,LeaderBaseCount,FirstMaximumTechnologyYear=-1;
        public string ArchiveBaseHash="",LeaderBaseHash="",ArchiveHash="";
        public long TotalPopulation,TotalUrban,ProcessedLocalEvents,CellReads,CellWrites;
        public HistoricalAggregateState Copy()=>new HistoricalAggregateState{Version=Version,Living=Living?.Copy(),Provinces=Provinces?.Copy(),Communities=Communities?.Copy(),Cells=Cells,TownAt=TownAt,Successors=Successors,LeaderDigests=LeaderDigests,PreviousAdministration=PreviousAdministration,Cities=Cities.Select(c=>c.Copy()).ToArray(),Cultures=Cultures.Select(c=>c.Copy()).ToArray(),Polities=Polities.Select(c=>c.Copy()).ToArray(),Regions=Regions.Select(c=>c.Copy()).ToArray(),ArchiveBaseCount=ArchiveBaseCount,LeaderBaseCount=LeaderBaseCount,ArchiveBaseHash=ArchiveBaseHash,LeaderBaseHash=LeaderBaseHash,ArchiveHash=ArchiveHash,FirstMaximumTechnologyYear=FirstMaximumTechnologyYear,TotalPopulation=TotalPopulation,TotalUrban=TotalUrban,ProcessedLocalEvents=ProcessedLocalEvents,CellReads=CellReads,CellWrites=CellWrites};
        public int Resolve(int polity,HistoryState history)
        {
            // Successions point only to higher IDs: no cycles, no terrain rewrite.
            int guard=0;while(polity>=0&&history.Polities[polity].EndedYear>=0){int next=Successors[polity];if(next<=polity)return -1;polity=next;if(++guard>history.Polities.Length)throw new InvalidOperationException("Cyclic political succession.");}return polity;
        }
        public int PopulationCohort(int polity)
        {while(polity>=0){int next=Successors[polity];if(next<0)return polity;if(next<=polity)throw new InvalidOperationException("Cyclic population succession.");polity=next;}return -1;}
    }
}
