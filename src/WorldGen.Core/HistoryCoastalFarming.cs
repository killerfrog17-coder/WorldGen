using System;
namespace WorldGen.Core
{
    [Serializable] public sealed class HistoricalCoastalFarming
    {
        public int SinceYear;
        // A direct saved local-household policy, not a queried ancestry chain.
        public HistoricalCellTree Families=new HistoricalCellTree(33554432);
        public HistoricalCoastalFarming Copy()=>new HistoricalCoastalFarming{SinceYear=SinceYear,Families=Families};
    }
    public static class HistoricalCoastalFarmEffects
    {
        public static int Cohort(long urban)=>(int)Math.Min(1000,Math.Max(0,urban-1000)/4);
        public static int Urban(int cohort)=>Math.Max(0,cohort)/5;
        public static long AreaKm2(int cohort,double yield)=>Math.Max(50,Math.Min(2000,(long)Math.Ceiling(Math.Max(0,cohort)*.1/Math.Max(.02,yield))));
        public static double AnnualOpportunity(long urban,int cohort,double pressure,double transport,bool civic,int age)
        {
            if(urban<1000||cohort<200||age<(civic?30:80))return 0;
            return D.Q((civic?.003:.0015)*Math.Min(2,Math.Sqrt(urban/5000.0))*(.1+.9*D.Clamp(pressure))*Math.Sqrt(Math.Max(1,transport)),1000000000);
        }
    }
    public sealed partial class Prehistory
    {
        sealed class CoastalFarmPlan
        {
            public int Parent,Cell,Culture,Polity,People,Urban;
            public long Area,Provisions,Investment;
            public PreparedFieldQuote Fields;
        }
        bool CoastalFarmingEnabled(PrehistoryState s)=>LivingWorldHistory(s)&&s.History.Aggregates.Empires.Connections.CoastalFarming!=null;
        bool CoastalFarmDescendant(PrehistoryState s,int town)=>CoastalFarmingEnabled(s)&&s.History.Aggregates.Empires.Connections.CoastalFarming.Families[town]>=0;
        bool CoastalLocalHouseholds(PrehistoryState s,int town)
            =>CoastalFarmingEnabled(s)&&(ProvisionedPort(s,town)!=null||s.History.Aggregates.Empires.Connections.CoastalFarming.Families[town]>=0);
        public PrehistoryState UpgradeCoastalFarming(PrehistoryState source)
        {
            Validate(source);if(!LivingWorldHistory(source))throw new ArgumentException("Coastal farming upgrade requires history32.");
            if(CoastalFarmingEnabled(source))return source;
            var s=source.Copy();InitializeCoastalFarming(s);s.Seal();return s;
        }
        void InitializeCoastalFarming(PrehistoryState s)
        {var c=s.History.Aggregates.Empires.Connections;if(c.CoastalFarming==null)c.CoastalFarming=new HistoricalCoastalFarming{SinceYear=s.Years};}
        void RegisterCoastalFarmFamily(PrehistoryState s,int town,int parent)
        {
            if(parent>=0&&CoastalLocalHouseholds(s,parent))
            {var c=s.History.Aggregates.Empires.Connections.CoastalFarming;c.Families=c.Families.With(town,parent);}
        }
        double CoastalPortFarmRate(PrehistoryState s,int parent,double pressure,bool civic,int age,double transport)
        {
            var port=ProvisionedPort(s,parent);if(port==null||port.LastDeliveryYear<s.Years-5)return 0;
            var town=s.Society.Settlements[parent];return HistoricalCoastalFarmEffects.AnnualOpportunity(town.Population,HistoricalCoastalFarmEffects.Cohort(town.Population),pressure,transport,civic,age);
        }
        bool PrepareCoastalFarm(PrehistoryState s,int parent,int cell,out CoastalFarmPlan plan)
        {
            plan=null;if(!CoastalFarmingEnabled(s)||parent<0||parent>=s.History.Aggregates.Cities.Length)return false;
            var port=ProvisionedPort(s,parent);if(port==null||port.LastDeliveryYear<s.Years-5)return false;
            var h=s.History;var a=h.Aggregates;var source=s.Society.Settlements[parent];int culture=source.CultureId,owner=a.Resolve(source.PolityId,h),region=cell>=0&&cell<CellPatch.Length?CellPatch[cell]:-1;
            int people=HistoricalCoastalFarmEffects.Cohort(source.Population),urban=HistoricalCoastalFarmEffects.Urban(people);
            if(people<200||owner<0||h.Polities[owner].EndedYear>=0||a.Living.Cities[parent].AbandonedYear>=0||a.Living.Cities[parent].DeclineYears>0||!a.Cities[parent].Supported||h.Dynamics.States[owner].PublicWorks<200)return false;
            if(region<0||a.TownAt[cell]>=0||World.Hydrology.LakeCoverageFraction[cell]>=.9||World.Landmass[cell]!=World.Landmass[source.Cell])return false;
            double yield=CarriedCropLandscape(s,culture,cell,false,parent);int practiced=LocalSeedlotHistory(s)?CarriedCropPractice(s,culture,cell,parent):h.Agriculture[culture].HusbandryYears==null?0:MaxCoastalPractice(h.Agriculture[culture].HusbandryYears);
            PreparedFieldQuote fields=null;
            if(PreparedFieldHistory(s))
            {
                fields=QuotePreparedFields(s,culture,cell,parent,people-urban);
                LivingInlandSettlementCosts(s,parent,cell,people,out long fieldProvisions,out long fieldCarriage);
                long virginInvestment=fieldCarriage+Math.Max(500,(long)Math.Ceiling(HistoricalCoastalFarmEffects.AreaKm2(people,yield)*1.5+people/3.0));
                if(fields!=null&&PreparedQuoteAffordable(s,fields,people+fieldProvisions,virginInvestment))yield=fields.Landscape;else fields=null;
            }
            if(s.Society.Cultures[culture].Farming<180||yield<.10&&(yield<.02||practiced<10000))return false;
            var habitat=CropHabitat(s,cell);bool modern=HistoricalModernEffects.Adoption(h.Cultures[culture],HistoricalTechnology.CombustionEngines,s.Years)>.3;
            if((habitat.GrowingDays<90||habitat.WarmTenthsC<80||habitat.PermafrostPermille>700)&&!modern)return false;
            if(habitat.Biome==Biome.Ice||habitat.ReliefM>2000)return false;
            double drinking=D.Clamp(habitat.AridityMilli/500.0+riverAccess[cell]*12+(habitat.WaterTableCm<=300&&habitat.SalinityMilliPpt<3000?.15:0));if(drinking<.2)return false;
            double spacing=(100-45*HistoricalModernEffects.Adoption(h.Cultures[culture],HistoricalTechnology.Railways,s.Years))*Math.Sqrt(Math.Min(4,Math.Max(1,.25/Math.Max(.02,yield))));
            if(a.Living.BucketCounts[LivingTownBucket(World.Surface.Cells[cell].Position)]>=16||NearbyLivingTowns(s,cell,spacing).Length>0)return false;
            long area=HistoricalCoastalFarmEffects.AreaKm2(people,yield);
            if(a.Living.Regions[region].CommittedAreaKm2+area>a.Regions[region].AreaKm2*.65)return false;
            if(!InstitutionalRouteAllowed(s,source.Cell,cell,culture)||RespectRecognizedClaim(s,owner,cell)||LivingTravelKm(s,source.Cell,cell,culture)>TerritorialServiceRadius(s,owner,cell)*1.5)return false;
            int host=HistoricalCells.Polity(s,cell);
            if(host>=0&&host!=owner&&(!aggregateRelations.TryGetValue((Math.Min(owner,host),Math.Max(owner,host)),out int relation)||!h.Relations.Peek(relation).Trade||h.Relations.Peek(relation).WarSince>=0))return false;
            LivingInlandSettlementCosts(s,parent,cell,people,out long provisions,out long carriage);
            long investment=carriage+Math.Max(500,(long)Math.Ceiling(area*1.5+people/3.0));
            if(source.FoodReserve<people+provisions+(fields?.Food??0)+source.Population/4||h.Polities[owner].Treasury<(investment+(fields?.Investment??0))*2||a.Polities[owner].Receipts<=a.Polities[owner].ServiceCost)return false;
            plan=new CoastalFarmPlan{Parent=parent,Cell=cell,Culture=culture,Polity=owner,People=people,Urban=urban,Area=area,Provisions=provisions,Investment=investment,Fields=fields};return true;
        }
        static int MaxCoastalPractice(int[] years){int best=0;for(int i=0;i<years.Length;i++)best=Math.Max(best,years[i]);return best;}
        bool ValidCoastalFarmPlan(PrehistoryState s,int culture,int cell,int parent,CoastalFarmPlan plan)
            =>plan!=null&&plan.Culture==culture&&plan.Parent==parent&&plan.Cell==cell&&PrepareCoastalFarm(s,parent,cell,out var current)&&current.People==plan.People&&current.Area==plan.Area&&current.Provisions==plan.Provisions&&current.Investment==plan.Investment&&current.Polity==plan.Polity&&(current.Fields?.Food??0)==(plan.Fields?.Food??0)&&(current.Fields?.Investment??0)==(plan.Fields?.Investment??0);
        bool TryFoundCoastalFarm(PrehistoryState s,int parent,int cell)
        {
            if(!PrepareCoastalFarm(s,parent,cell,out var plan))return false;
            int before=s.History.Aggregates.Cities.Length,native=HistoricalCells.Culture(s,cell),source=s.Society.Settlements[parent].Cell;
            FoundLivingTownPrepared(s,plan.Culture,cell,parent,plan.Polity,null,plan);
            bool founded=s.History.Aggregates.Cities.Length>before;
            if(founded&&native>=0&&native!=plan.Culture)LivingCultureContactAt(s,plan.Culture,native,source,cell);
            return founded;
        }
        void TransferCoastalFarmFounders(PrehistoryState s,CoastalFarmPlan plan)
        {
            var a=s.History.Aggregates;var source=s.Society.Settlements[plan.Parent];var city=a.Cities[plan.Parent];
            ChangeAggregateCityPopulation(s,plan.Parent,source.Population-plan.People,city.Suburban,city.Rural);
            a.TotalPopulation+=plan.People;s.Society.Cultures[plan.Culture].Population+=plan.People;
            source.FoodReserve-=plan.People+plan.Provisions;s.History.Polities[plan.Polity].Treasury-=plan.Investment;
            // As in ordinary farm founding, only the urban share enters the
            // named warehouse. Rural first-growing-season rations are consumed
            // establishing the new fields, not another invisible food stock.
        }
        void ValidateCoastalFarming(PrehistoryState s)
        {
            var c=s.History.Aggregates.Empires.Connections.CoastalFarming;if(c==null)return;
            var a=s.History.Aggregates;
            if(!LivingWorldHistory(s)||c.SinceYear<0||c.SinceYear>s.Years||c.Families?.Length!=33554432||!c.Families.AllPresentValues((town,parent)=>town<a.Cities.Length&&parent>=0&&parent<town&&a.Cities[town].ParentCity==parent&&(c.Families[parent]>=0||ProvisionedPort(s,parent)!=null)))throw new ArgumentException("Invalid coastal local-household families.");
        }
    }
}
