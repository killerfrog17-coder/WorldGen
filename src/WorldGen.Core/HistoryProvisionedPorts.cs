using System;
using System.Linq;
namespace WorldGen.Core
{
    [Serializable] public sealed class HistoricalProvisionedPort
    {
        public int TownId,SupplierTownId,DepartureCell,HostPostId,FoundedYear,LastSupplyYear,LastSupplyElapsedYears,LastDeliveryYear=-1,LastExpansionYear,MaximumResidents=250;
        public long SentFood,ReceivedFood,LostFood,ShippingPaid,LastReceivedFood,ExpansionPaid,AdditionalMigrants,FoundingInvestment,InitialFood,InitialVoyageFood;
        public HistoricalProvisionedPort Copy()=>(HistoricalProvisionedPort)MemberwiseClone();
    }
    [Serializable] public sealed class HistoricalProvisionedPorts
    {
        public HistoricalCellTree ByTown=new HistoricalCellTree(33554432);
        public HistoricalProvisionedPort[] Ports=Array.Empty<HistoricalProvisionedPort>();
        public HistoricalProvisionedPorts Copy()=>new HistoricalProvisionedPorts{ByTown=ByTown,Ports=Ports.Select(x=>x.Copy()).ToArray()};
    }
    public static class HistoricalProvisionedPortEffects
    {
        public const int ResidentLimit=250;
        public static long UrbanFounders(long urban)=>Math.Min(ResidentLimit,Math.Max(0,urban-1500)/20);
        public static int RetentionPermille(double km,double shipping)=>Math.Max(350,Math.Min(970,(int)Math.Round(970-Math.Max(0,km)/(12*Math.Max(1,shipping)))));
        public static long ShippingCost(long food,double km)=>food<=0?0:Math.Max(1,(long)Math.Ceiling(food*(.02+Math.Max(0,km)/20000)));
        public static int OpportunityYears(int elapsed)=>Math.Max(0,Math.Min(3,elapsed));
        public static int HandlingCeiling(double navigation,double steam,double engines,int works)
            =>Math.Max(ResidentLimit,(int)Math.Floor(ResidentLimit*(1+4*D.Clamp(navigation)+12*D.Clamp(steam)+24*D.Clamp(engines))*(.5+.5*D.Clamp(works/1000.0))));
        public static int ExpansionResidents(int installed,int ceiling,long urban)
            =>(int)Math.Max(0,Math.Min(Math.Min(Math.Max(0,ceiling-installed),Math.Max(25,(installed+3)/4)),Math.Max(0,urban-1500)/40));
        public static double Shipping(double sailing,double navigation,double ocean,double steam,double engines,double storage,double pottery)
            =>1+.5*D.Clamp(sailing)+.75*D.Clamp(navigation)+1.5*D.Clamp(ocean)+2*D.Clamp(steam)+1.5*D.Clamp(engines)+.5*D.Clamp(storage)+.25*D.Clamp(pottery);
    }
    public sealed partial class Prehistory
    {
        sealed class ProvisionedPortPlan
        {
            public int Polity,Source,Target,Departure,Post;
            public long Founders,Provisions,Investment;
        }
        public PrehistoryState UpgradeProvisionedPorts(PrehistoryState source)
        {
            Validate(source);if(!LivingWorldHistory(source))throw new ArgumentException("Provisioned ports require history32.");
            if(source.History.Aggregates.Empires.Connections.ProvisionedPorts!=null)return source;
            var s=source.Copy();InitializeProvisionedPorts(s);s.Seal();return s;
        }
        void InitializeProvisionedPorts(PrehistoryState s)
        {var c=s.History.Aggregates.Empires.Connections;if(c.ProvisionedPorts==null)c.ProvisionedPorts=new HistoricalProvisionedPorts();}
        HistoricalProvisionedPort ProvisionedPort(PrehistoryState s,int town)
        {
            if(!LivingWorldHistory(s))return null;
            var ports=s.History.Aggregates.Empires.Connections.ProvisionedPorts;
            int index=ports==null?-1:ports.ByTown[town];return index<0?null:ports.Ports[index];
        }
        bool CanPrepareProvisionedPort(PrehistoryState s,int polity,Route route,int supplier,out ProvisionedPortPlan plan)
        {
            plan=null;var h=s.History;var a=h.Aggregates;
            if(!LivingWorldHistory(s)||a.Empires.Connections.ProvisionedPorts==null||polity<0||polity>=h.Polities.Length||supplier<0||supplier>=a.Cities.Length)return false;
            var p=h.Polities[polity];var source=s.Society.Settlements[supplier];int target=route.CellB,region=target>=0&&target<CellPatch.Length?CellPatch[target]:-1;
            if(region<0||!route.Sea||!DiffusionShore(target)||a.TownAt[target]>=0||World.Hydrology.LakeCoverageFraction[target]>=.9||p.EndedYear>=0||p.Cohesion<500)return false;
            var habitat=World.Ecology.Habitat[target];
            if(habitat.Biome==Biome.Ice||habitat.WarmTenthsC<80||habitat.GrowingDays<60||habitat.PermafrostPermille>700||habitat.ReliefM>2000)return false;
            double drinking=D.Clamp(habitat.AridityMilli/500.0+riverAccess[target]*12+(habitat.WaterTableCm<=300&&habitat.SalinityMilliPpt<3000?.15:0));
            if(drinking<.2)return false;
            // This is a paid urban station, not a switch to waive a farmer's
            // ecological/household checks. Viable carried fields use the old path.
            int culture=source.CultureId,practiced=h.Agriculture[culture].HusbandryYears?.Max()??0;
            double yield=LivingCropLandscape(s,culture,target,false);
            if(yield>=.10||yield>=.02&&practiced>=10000)return false;
            int post=a.Empires.Territories.PostAt[target];
            if(post<0||HistoricalCells.Polity(s,target)!=polity||!a.Empires.Territories.Posts[post].Maritime||a.Empires.Territories.Posts[post].ClosedYear>=0||!TerritorialNodeLive(s,polity,-2-post))return false;
            if(!LivingTerritorialInterest(s,polity,target)||!InstitutionalRouteAllowed(s,route.CellA,target,p.CultureId)||
                !MaritimeCharterLogistics(s,polity,route,supplier,out int actual,out _)||actual!=supplier)return false;
            var hc=h.Cultures[p.CultureId];
            if(HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Writing,s.Years)<.2||HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Navigation,s.Years)<.2||
                h.Dynamics.States[polity].PublicWorks<200||hc.Institution<350||a.Resolve(source.PolityId,h)!=polity||!a.Cities[supplier].Supported||a.Living.Cities[supplier].AbandonedYear>=0)return false;
            if(a.Living.BucketCounts[LivingTownBucket(World.Surface.Cells[target].Position)]>=16||NearbyLivingTowns(s,target,100).Length>0||
                a.Living.Regions[region].CommittedAreaKm2+10>a.Regions[region].AreaKm2*.65)return false;
            long people=HistoricalProvisionedPortEffects.UrbanFounders(source.Population);if(people<100)return false;
            double staging=D.Angle(World.Surface.Cells[source.Cell].Position,World.Surface.Cells[route.CellA].Position)*World.Recipe.RadiusKm;
            long provisions=HistoricalVoyageEffects.Provisioning(people,(int)Math.Min(int.MaxValue,route.DistanceM+staging*1000));
            long investment=HistoricalMaritimeIntegrationEffects.CouncilInvestment(people,route.DistanceM);
            long annual=HistoricalProvisionedPortEffects.ShippingCost(HistoricalProvisionedPortEffects.ResidentLimit*2,route.DistanceM/1000.0+staging);
            if(source.FoodReserve<people*3+provisions+source.Population/2||p.Treasury<investment*2+annual*3||a.Polities[polity].Receipts<=a.Polities[polity].ServiceCost+annual)return false;
            plan=new ProvisionedPortPlan{Polity=polity,Source=supplier,Target=target,Departure=route.CellA,Post=post,Founders=people,Provisions=provisions,Investment=investment};return true;
        }
        bool ValidProvisionedPortPlan(PrehistoryState s,int culture,int cell,int parent,ProvisionedPortPlan plan)
        {
            if(plan==null||plan.Source!=parent||plan.Target!=cell||s.Society.Settlements[parent].CultureId!=culture)return false;
            var route=new Route{CellA=plan.Departure,CellB=cell,A=CellPatch[plan.Departure],B=CellPatch[cell],Sea=true,
                DistanceM=(int)Math.Round(D.Angle(World.Surface.Cells[plan.Departure].Position,World.Surface.Cells[cell].Position)*World.Recipe.RadiusKm*1000)};
            return CanPrepareProvisionedPort(s,plan.Polity,route,parent,out var current)&&current.Founders==plan.Founders&&current.Provisions==plan.Provisions&&current.Investment==plan.Investment&&current.Post==plan.Post;
        }
        void TransferProvisionedPortFounders(PrehistoryState s,ProvisionedPortPlan plan)
        {
            var a=s.History.Aggregates;var source=s.Society.Settlements[plan.Source];var city=a.Cities[plan.Source];
            ChangeAggregateCityPopulation(s,plan.Source,source.Population-plan.Founders,city.Suburban,city.Rural);
            a.TotalPopulation+=plan.Founders;s.Society.Cultures[source.CultureId].Population+=plan.Founders;
            source.FoodReserve-=plan.Founders*3+plan.Provisions;s.History.Polities[plan.Polity].Treasury-=plan.Investment;
        }
        void RegisterProvisionedPort(PrehistoryState s,int town,ProvisionedPortPlan plan)
        {
            var ports=s.History.Aggregates.Empires.Connections.ProvisionedPorts;int id=ports.Ports.Length;
            ports.Ports=ports.Ports.Concat(new[]{new HistoricalProvisionedPort{TownId=town,SupplierTownId=plan.Source,DepartureCell=plan.Departure,HostPostId=plan.Post,FoundedYear=s.Years,LastSupplyYear=s.Years,LastExpansionYear=s.Years,FoundingInvestment=plan.Investment,InitialFood=plan.Founders*3,InitialVoyageFood=plan.Provisions}}).ToArray();
            ports.ByTown=ports.ByTown.With(town,id);
        }
        long SupplyProvisionedPort(PrehistoryState s,int town,int elapsed,HistoricalProvisionedPort port)
        {
            if(elapsed<=0||port.LastSupplyYear>=s.Years)return 0;
            port.LastSupplyYear=s.Years;port.LastSupplyElapsedYears=Math.Max(0,elapsed);port.LastReceivedFood=0;
            var h=s.History;var a=h.Aggregates;var target=s.Society.Settlements[town];var source=s.Society.Settlements[port.SupplierTownId];
            int owner=a.Resolve(target.PolityId,h),supplierOwner=a.Resolve(source.PolityId,h);int years=HistoricalProvisionedPortEffects.OpportunityYears(elapsed);
            if(owner<0||owner!=supplierOwner||years==0||a.Living.Cities[port.SupplierTownId].AbandonedYear>=0||!a.Cities[port.SupplierTownId].Supported)return 0;
            int post=a.Empires.Territories.PostAt[target.Cell];
            if(post!=port.HostPostId||a.Empires.Territories.Posts[post].ClosedYear>=0||!TerritorialNodeLive(s,owner,-2-post)||HistoricalCells.Polity(s,target.Cell)!=owner)return 0;
            var route=new Route{CellA=port.DepartureCell,CellB=target.Cell,A=CellPatch[port.DepartureCell],B=CellPatch[target.Cell],Sea=true,
                DistanceM=(int)Math.Round(D.Angle(World.Surface.Cells[port.DepartureCell].Position,World.Surface.Cells[target.Cell].Position)*World.Recipe.RadiusKm*1000)};
            if(!InstitutionalRouteAllowed(s,route.CellA,route.CellB,h.Polities[owner].CultureId)||!MaritimeCharterLogistics(s,owner,route,port.SupplierTownId,out int actual,out _)||actual!=port.SupplierTownId)return 0;
            var p=h.Polities[owner];if(h.Dynamics.States[owner].PublicWorks<200)return 0;
            double staging=D.Angle(World.Surface.Cells[source.Cell].Position,World.Surface.Cells[route.CellA].Position)*World.Recipe.RadiusKm;
            double distance=route.DistanceM/1000.0+staging;var hc=h.Cultures[p.CultureId];
            double shipping=HistoricalProvisionedPortEffects.Shipping(HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Sailing,s.Years),HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Navigation,s.Years),
                HistoricalModernEffects.Adoption(hc,HistoricalTechnology.OceanNavigation,s.Years),HistoricalModernEffects.Adoption(hc,HistoricalTechnology.SteamPower,s.Years),HistoricalModernEffects.Adoption(hc,HistoricalTechnology.CombustionEngines,s.Years),
                HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Storage,s.Years),HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Pottery,s.Years));
            // Railways improve only the actual inland staging leg, not ships.
            int retention=HistoricalProvisionedPortEffects.RetentionPermille(route.DistanceM/1000.0+staging*shipping/HistoricalModernEffects.LandTransport(hc,s.Years),shipping);
            long need=Math.Max(0,target.Population*years+Math.Min(port.MaximumResidents,target.Population)-target.FoodReserve);
            long surplus=Math.Max(0,source.FoodReserve-source.Population/2);
            var shipment=HistoricalFoodEffects.Deliver(surplus,need,retention,port.MaximumResidents*2L*years);
            long cost=HistoricalProvisionedPortEffects.ShippingCost(shipment.Sent,distance);
            if(shipment.Sent<=0||p.Treasury<cost)return 0;
            source.FoodReserve-=shipment.Sent;p.Treasury-=cost;
            h.Towns[port.SupplierTownId].ExportedFood+=shipment.Sent;h.Towns[port.SupplierTownId].FoodTransportLoss+=shipment.Lost;
            h.Towns[town].ImportedFood+=shipment.Received;
            port.SentFood+=shipment.Sent;port.ReceivedFood+=shipment.Received;port.LostFood+=shipment.Lost;port.ShippingPaid+=cost;port.LastReceivedFood=shipment.Received;port.LastDeliveryYear=s.Years;
            return shipment.Received;
        }
        void ExpandProvisionedPort(PrehistoryState s,int id,HistoricalProvisionedPort port,double supply)
        {
            if(s.Years-port.LastExpansionYear<40)return;port.LastExpansionYear=s.Years;
            var h=s.History;var a=h.Aggregates;var town=s.Society.Settlements[id];var city=a.Cities[id];var source=s.Society.Settlements[port.SupplierTownId];
            int owner=a.Resolve(town.PolityId,h);
            if(owner<0||a.Resolve(source.PolityId,h)!=owner||!city.Supported||supply<.95||town.Stability<550||a.Living.Cities[id].DeclineYears>0||port.LastDeliveryYear<s.Years-5)return;
            var p=h.Polities[owner];var hc=h.Cultures[p.CultureId];
            int ceiling=HistoricalProvisionedPortEffects.HandlingCeiling(HistoricalModernEffects.Adoption(hc,HistoricalTechnology.OceanNavigation,s.Years),HistoricalModernEffects.Adoption(hc,HistoricalTechnology.SteamPower,s.Years),HistoricalModernEffects.Adoption(hc,HistoricalTechnology.CombustionEngines,s.Years),h.Dynamics.States[owner].PublicWorks);
            int migrants=HistoricalProvisionedPortEffects.ExpansionResidents(port.MaximumResidents,ceiling,source.Population);if(migrants<20)return;
            var route=new Route{CellA=port.DepartureCell,CellB=town.Cell,A=CellPatch[port.DepartureCell],B=CellPatch[town.Cell],Sea=true,
                DistanceM=(int)Math.Round(D.Angle(World.Surface.Cells[port.DepartureCell].Position,World.Surface.Cells[town.Cell].Position)*World.Recipe.RadiusKm*1000)};
            int post=a.Empires.Territories.PostAt[town.Cell];
            if(post!=port.HostPostId||!TerritorialNodeLive(s,owner,-2-post)||HistoricalCells.Polity(s,town.Cell)!=owner||h.Dynamics.States[owner].PublicWorks<200||
                !InstitutionalRouteAllowed(s,route.CellA,route.CellB,p.CultureId)||!MaritimeCharterLogistics(s,owner,route,port.SupplierTownId,out int actual,out _)||actual!=port.SupplierTownId)return;
            double staging=D.Angle(World.Surface.Cells[source.Cell].Position,World.Surface.Cells[route.CellA].Position)*World.Recipe.RadiusKm;
            long voyage=HistoricalVoyageEffects.Provisioning(migrants,(int)Math.Min(int.MaxValue,route.DistanceM+staging*1000)),investment=HistoricalMaritimeIntegrationEffects.CouncilInvestment(migrants,route.DistanceM);
            long annual=HistoricalProvisionedPortEffects.ShippingCost((port.MaximumResidents+migrants)*2L,route.DistanceM/1000.0+staging);
            // Installed handling is not a world-wide demographic multiplier.
            // Its expansion needs today's real household surplus, warehouse
            // reserve and affordable recurring freight as well as construction.
            if(source.FoodReserve<source.Population/2+Math.Max(migrants*3L+voyage,(port.MaximumResidents+migrants)*3L)||p.Treasury<investment*2+annual*3||a.Polities[owner].Receipts<=a.Polities[owner].ServiceCost+annual)return;
            var sourceCity=a.Cities[port.SupplierTownId];
            ChangeAggregateCityPopulation(s,port.SupplierTownId,source.Population-migrants,sourceCity.Suburban,sourceCity.Rural);
            ChangeAggregateCityPopulation(s,id,town.Population+migrants,0,0);
            source.FoodReserve-=migrants*3L+voyage;town.FoodReserve+=migrants*3L;p.Treasury-=investment;
            long previous=town.FoodCapacity;port.MaximumResidents+=migrants;town.FoodCapacity=port.MaximumResidents;a.Cultures[town.CultureId].FoodCapacity+=town.FoodCapacity-previous;
            port.AdditionalMigrants+=migrants;port.ExpansionPaid+=investment;
            MixHumanVariation(s,CellPatch[source.Cell],CellPatch[town.Cell],migrants,s.Population[CellPatch[town.Cell]]);
            HistoryEvent(s,"harbor-expansion",town.Name+" expands its paid harbor and receives "+migrants+" additional residents from "+source.Name+". Food and voyage costs are charged to that actual supply community; no new fields are assumed.",town.Cell,town.CultureId,owner,town:id,category:ChronicleCategory.Society);
        }
        void ValidateProvisionedPorts(PrehistoryState s)
        {
            var a=s.History.Aggregates;var ports=a.Empires.Connections.ProvisionedPorts;if(ports==null)return;
            if(!LivingWorldHistory(s)||ports.ByTown?.Length!=33554432||ports.Ports==null||!ports.ByTown.AllPresentValues((town,index)=>town<a.Cities.Length&&index>=0&&index<ports.Ports.Length&&ports.Ports[index].TownId==town))throw new ArgumentException("Invalid provisioned port index.");
            for(int i=0;i<ports.Ports.Length;i++)
            {
                var p=ports.Ports[i];if(p==null||p.TownId<0||p.TownId>=a.Cities.Length||ports.ByTown[p.TownId]!=i||p.SupplierTownId<0||p.SupplierTownId>=p.TownId||p.DepartureCell<0||p.DepartureCell>=CellPatch.Length||CellPatch[p.DepartureCell]<0||p.HostPostId<0||p.HostPostId>=a.Empires.Territories.Posts.Length||a.Empires.Territories.Posts[p.HostPostId].Cell!=s.Society.Settlements[p.TownId].Cell||p.FoundedYear<0||p.FoundedYear>s.Years||p.LastSupplyYear<p.FoundedYear||p.LastSupplyYear>s.Years||p.LastSupplyElapsedYears<0||p.LastDeliveryYear< -1||p.LastDeliveryYear>s.Years||p.LastExpansionYear<p.FoundedYear||p.LastExpansionYear>s.Years||p.MaximumResidents<HistoricalProvisionedPortEffects.ResidentLimit||p.MaximumResidents>10250||p.SentFood<0||p.ReceivedFood<0||p.LostFood<0||p.SentFood!=p.ReceivedFood+p.LostFood||p.ShippingPaid<0||p.LastReceivedFood<0||p.LastReceivedFood>p.ReceivedFood||p.ExpansionPaid<0||p.AdditionalMigrants!=p.MaximumResidents-HistoricalProvisionedPortEffects.ResidentLimit)throw new ArgumentException("Invalid provisioned port ledger.");
                if(p.LastDeliveryYear>=0&&p.LastDeliveryYear<p.FoundedYear||(p.ReceivedFood==0)!=(p.LastDeliveryYear<0)||p.LastReceivedFood>0&&p.LastDeliveryYear!=p.LastSupplyYear||p.FoundingInvestment<=0||p.InitialFood<300||p.InitialFood>750||p.InitialVoyageFood<0||a.Cities[p.TownId].Rural!=0||a.Cities[p.TownId].Suburban!=0||a.Cities[p.TownId].RuralCapacity!=0||s.Society.Settlements[p.TownId].CultivatedPermille!=0)throw new ArgumentException("Provisioned harbor has fictitious farming households or invalid receipts.");
            }
        }
    }
}
