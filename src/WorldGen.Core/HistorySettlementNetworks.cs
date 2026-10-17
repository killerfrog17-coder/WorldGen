using System;
namespace WorldGen.Core
{
    public static class HistoricalSettlementNetworks
    {
        // An actor-local annual hazard, independent of the number of cities in
        // a distant cultural homeland. Pressure is relative to local capacity.
        public static double AnnualOpportunity(long urban,long transferable,double pressure,double landTransport,bool civic,int age)
        {
            if(urban<1000||transferable<1600||age<(civic?30:80))return 0;
            double scale=Math.Min(2,Math.Sqrt(urban/5000.0)),mobility=Math.Sqrt(Math.Max(1,landTransport));
            return D.Q((civic?.003:.0015)*scale*(.1+.9*D.Clamp(pressure))*mobility,1000000000);
        }
        public static double AgrarianOpportunity(long urban,long transferable,double pressure,double landTransport,bool civic,int age)
            =>D.Q(AnnualOpportunity(urban,transferable,pressure,landTransport,civic,age)*Math.Min(10,Math.Sqrt(Math.Max(0,transferable)/1600.0)),1000000000);
        public static double FarmingContinuity(double foragerContinuity,double actualCarriedCropFit,int farming)
            =>farming<250?foragerContinuity:Math.Max(foragerContinuity,.85*Math.Sqrt(D.Clamp(actualCarriedCropFit)));
        public static V LocalProbe(V from,double radiusKm,double bearingTurns,int trial)
        {
            // Proposal geometry only. Separated bearings avoid spending all
            // three trials wandering around the same crowded neighborhood.
            var east=V.Tangent(from,new V(0,0,1));if(east.Length<.5)east=V.Tangent(from,new V(1,0,0));var north=V.Cross(from,east).Unit;
            double angle=(bearingTurns+Math.Max(0,Math.Min(2,trial))/3.0)*2*D.Pi;
            double distance=(120+60*Math.Max(0,Math.Min(2,trial)))/Math.Max(1,radiusKm);
            var tangent=east*D.Sin(angle+D.Pi/2)+north*D.Sin(angle);
            return (from*D.Sin(distance+D.Pi/2)+tangent*D.Sin(distance)).Unit;
        }
        public static V TransportProbe(V from,double radiusKm,double bearingTurns,int trial,double reachFactor)
            =>LocalProbe(from,radiusKm/HistoricalInlandSettlement.ProbeScale(reachFactor,trial),bearingTurns,trial);
        public static V LocalProbeKm(V from,double radiusKm,double bearingTurns,double km)
        {
            var east=V.Tangent(from,new V(0,0,1));if(east.Length<.5)east=V.Tangent(from,new V(1,0,0));var north=V.Cross(from,east).Unit;
            double angle=bearingTurns*2*D.Pi,distance=Math.Max(0,km)/Math.Max(1,radiusKm);var tangent=east*D.Sin(angle+D.Pi/2)+north*D.Sin(angle);
            return (from*D.Sin(distance+D.Pi/2)+tangent*D.Sin(distance)).Unit;
        }
    }
    public sealed partial class Prehistory
    {
        double LivingLocalTownRate(PrehistoryState s,int parent)
        {
            var a=s.History.Aggregates;var town=s.Society.Settlements[parent];var city=a.Cities[parent];var life=a.Living.Cities[parent];
            if(life.AbandonedYear>=0||!city.Supported||life.DeclineYears>0||town.FoodReserve<town.Population*.5)return 0;
            int owner=a.Resolve(town.PolityId,s.History);bool civic=owner>=0||s.History.Towns[parent].CivicExampleExposure>=400;
            if(owner>=0&&(s.History.Polities[owner].Treasury<1000||a.Polities[owner].Receipts<=a.Polities[owner].ServiceCost))return 0;
            double pressure=Math.Max(town.Population/(double)Math.Max(1,town.FoodCapacity),city.Rural/(double)Math.Max(1,city.RuralCapacity));
            if(CoastalFarmingEnabled(s)&&ProvisionedPort(s,parent)!=null)return CoastalPortFarmRate(s,parent,pressure,civic,s.Years-town.FoundedYear,HistoricalModernEffects.LandTransport(s.History.Cultures[town.CultureId],s.Years));
            if(BorderContactHistory(s)&&s.Society.Cultures[town.CultureId].Farming>=250)
                return HistoricalSettlementNetworks.AgrarianOpportunity(town.Population,LivingFounderAvailability(s,parent),pressure,
                    HistoricalModernEffects.LandTransport(s.History.Cultures[town.CultureId],s.Years),civic,s.Years-town.FoundedYear);
            return HistoricalSettlementNetworks.AnnualOpportunity(town.Population,LivingFounderAvailability(s,parent),pressure,
                HistoricalModernEffects.LandTransport(s.History.Cultures[town.CultureId],s.Years),civic,s.Years-town.FoundedYear);
        }
        bool TryFoundLivingLocalTown(PrehistoryState s,int parent,int target)
        {
            var h=s.History;var a=h.Aggregates;var town=s.Society.Settlements[parent];int source=town.Cell,culture=town.CultureId,owner=a.Resolve(town.PolityId,h);
            if(CellPatch[target]<0||World.Landmass[target]!=World.Landmass[source]||!InstitutionalRouteAllowed(s,source,target,culture))return false;
            int host=HistoricalCells.Polity(s,target),nativeCulture=HistoricalCells.Culture(s,target);
            if(host>=0&&host!=owner)
            {
                if(owner<0||!aggregateRelations.TryGetValue((Math.Min(owner,host),Math.Max(owner,host)),out int relation)||!h.Relations[relation].Trade||h.Relations[relation].WarSince>=0)return false;
            }
            if(owner>=0&&(RespectRecognizedClaim(s,owner,target)||LivingTravelKm(s,source,target,culture)>TerritorialServiceRadius(s,owner,target)*1.5))return false;
            // These are actual migrating households, not a spontaneous native
            // foundation. They carry their own crop repertoire and identity;
            // the surrounding population remains native and can exchange later.
            if(CoastalFarmingEnabled(s)&&ProvisionedPort(s,parent)!=null)return TryFoundCoastalFarm(s,parent,target);
            if(!CanFoundLivingTown(s,culture,target,parent))return false;
            long available=LivingFounderAvailability(s,parent),founders=Math.Min(250,available/12),farms=Math.Min(available/3,Math.Max(800,founders*8)),people=founders+farms;
            LivingInlandSettlementCosts(s,parent,target,people,out long provisions,out long investment);
            if(town.FoodReserve<people+provisions+town.Population/4||owner>=0&&(h.Polities[owner].Treasury<investment*2||a.Polities[owner].Receipts<=a.Polities[owner].ServiceCost))return false;
            int before=a.Cities.Length;FoundLivingTownFunded(s,culture,target,parent,owner);if(a.Cities.Length==before)return false;
            // Founder transfer already pays startup food and debits existing
            // households. Charge only additional carriage rations here.
            town.FoodReserve-=provisions;
            if(owner>=0)
            {
                h.Polities[owner].Treasury-=investment;int city=a.TownAt[target];
                if(a.Resolve(s.Society.Settlements[city].PolityId,h)<0)ClaimAggregatePolity(s,target,owner);
            }
            if(nativeCulture>=0&&nativeCulture!=culture)LivingCultureContactAt(s,culture,nativeCulture,source,target);
            return true;
        }
        int LivingLocalTownCandidate(PrehistoryState s,int parent,int trial)
        {
            int from=s.Society.Settlements[parent].Cell;
            double bearing=HistoryRoll("living-local-town-bearing",parent,s.Years);
            var point=LivingWorldHistory(s)?HistoricalSettlementNetworks.TransportProbe(World.Surface.Cells[from].Position,World.Recipe.RadiusKm,bearing,trial,LivingSettlementProbeReach(s,parent)):
                HistoricalSettlementNetworks.LocalProbe(World.Surface.Cells[from].Position,World.Recipe.RadiusKm,bearing,trial);
            if(RevisedInlandHistory(s))point=RevisedInlandProposal(s,parent,trial,bearing);
            int site=aggregateTerrainRaster[InstitutionalRasterNode(point)];
            // The existing immutable routing raster supplies a constant-size
            // seed. At most four nearest-neighbor refinements recover detail
            // without a locator rebuild, fine-cell search or growing path.
            for(int step=0;step<4;step++)
            {
                int best=site;double score=V.Dot(point,World.Surface.Cells[site].Position);
                foreach(int candidate in World.Surface.Cells[site].Neighbors)
                {double current=V.Dot(point,World.Surface.Cells[candidate].Position);if(current>score||current==score&&candidate<best){score=current;best=candidate;}}
                if(best==site)break;site=best;
            }
            // Do not snap an invalid ocean, mountain or different-continent
            // proposal into a conveniently free land cell across that barrier.
            return CellPatch[site]>=0&&World.Landmass[site]==World.Landmass[from]&&World.Ecology.Habitat[site].ReliefM<2000?site:from;
        }
        void TryLivingLocalTownOpportunity(PrehistoryState s,int parent,int elapsed)
        {
            double rate=LivingLocalTownRate(s,parent);if(rate<=0||elapsed<=0||HistoryRoll("living-local-town-opportunity",parent,s.Years)>=1-Math.Exp(-rate*elapsed))return;
            var town=s.Society.Settlements[parent];int culture=town.CultureId,from=town.Cell;
            // At most three local probes after a rare opportunity, never every
            // cell of the region, culture, state or the worldwide city list.
            for(int trial=0;trial<3;trial++)
            {
                int site=LivingLocalTownCandidate(s,parent,trial);
                double continuity=HistoricalLandscapeEffects.LivelihoodContinuity(World.Ecology.Habitat[from],World.Ecology.Habitat[from],World.Ecology.Habitat[site]);
                // Migrating farmers carry an actual viable cultivar; they are
                // not forced to relearn the target forest's foraging diet too.
                // Terrain travel, crop/soil fit, food, households and spacing
                // remain independently checked before committing a foundation.
                if(BorderContactHistory(s))continuity=HistoricalSettlementNetworks.FarmingContinuity(continuity,CarriedCropSupport(s,culture,site,parent),s.Society.Cultures[culture].Farming);
                if(HistoryRoll("living-local-town-adaptation-"+trial,parent,s.Years)>Math.Max(.03,continuity)*EnvironmentalTransmission(s,from,site,culture))continue;
                if(TryFoundLivingLocalTown(s,parent,site))return;
            }
        }
    }
}
