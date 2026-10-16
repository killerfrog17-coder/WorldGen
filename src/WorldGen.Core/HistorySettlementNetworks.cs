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
        public static int LocalWalkSteps(double cellAreaKm2,int trial)
        {
            // Random-walk displacement scales approximately with sqrt(steps).
            // A fixed cell-hop count sent coarse-map farmers hundreds of km
            // beyond their actual local delivery range. Geometry only proposes
            // a site; the real route/crop/spacing tests still decide viability.
            double distance=120+60*Math.Max(0,Math.Min(2,trial));
            return Math.Max(1,Math.Min(8,(int)Math.Round(Math.Min(8,distance*distance/Math.Max(.001,cellAreaKm2)))));
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
            if(!CanFoundLivingTown(s,culture,target,parent))return false;
            long available=LivingFounderAvailability(s,parent),founders=Math.Min(250,available/12),farms=Math.Min(available/3,Math.Max(800,founders*8)),people=founders+farms;
            long investment=Math.Max(100,people/5);
            if(town.FoodReserve<people+town.Population/4||owner>=0&&(h.Polities[owner].Treasury<investment*2||a.Polities[owner].Receipts<=a.Polities[owner].ServiceCost))return false;
            int before=a.Cities.Length;FoundLivingTownFunded(s,culture,target,parent,owner);if(a.Cities.Length==before)return false;
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
            int from=s.Society.Settlements[parent].Cell,site=from,previous=-1;
            int steps=HistoricalSettlementNetworks.LocalWalkSteps(World.Surface.Cells[from].AreaM2/1000000.0,trial);
            for(int step=0;step<steps;step++)
            {
                var ns=World.Surface.Cells[site].Neighbors;int offset=(int)(HistoryRoll("living-local-town-walk-"+trial+"-"+step,parent,s.Years)*ns.Length),next=-1;
                for(int k=0;k<ns.Length;k++){int candidate=ns[(offset+k)%ns.Length];if(candidate!=previous&&CellPatch[candidate]>=0&&World.Landmass[candidate]==World.Landmass[from]&&World.Ecology.Habitat[candidate].ReliefM<2000){next=candidate;break;}}
                if(next<0)break;previous=site;site=next;
            }
            return site;
        }
        void TryLivingLocalTownOpportunity(PrehistoryState s,int parent,int elapsed)
        {
            double rate=LivingLocalTownRate(s,parent);if(rate<=0||elapsed<=0||HistoryRoll("living-local-town-opportunity",parent,s.Years)>=1-Math.Exp(-rate*elapsed))return;
            var town=s.Society.Settlements[parent];int culture=town.CultureId,from=town.Cell;
            // At most three local paths after a rare opportunity, never every
            // cell of the region, culture, state or the worldwide city list.
            for(int trial=0;trial<3;trial++)
            {
                int site=LivingLocalTownCandidate(s,parent,trial);
                double continuity=HistoricalLandscapeEffects.LivelihoodContinuity(World.Ecology.Habitat[from],World.Ecology.Habitat[from],World.Ecology.Habitat[site]);
                if(HistoryRoll("living-local-town-adaptation-"+trial,parent,s.Years)>Math.Max(.03,continuity)*EnvironmentalTransmission(s,from,site,culture))continue;
                if(TryFoundLivingLocalTown(s,parent,site))return;
            }
        }
    }
}
