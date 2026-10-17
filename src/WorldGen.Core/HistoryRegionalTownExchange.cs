using System;
namespace WorldGen.Core
{
    public static class HistoricalRegionalTownExchange
    {
        public static int Staff(long urban)=>urban<1000?0:(int)Math.Min(60,Math.Max(12,urban/150));
        public static long Food(int staff,double effort,double transport)
            =>staff<=0?0:Math.Max(12,HistoricalInlandSettlement.Provisions(staff,Math.Max(0,effort)*2,transport));
        public static long Investment(int staff,double effort,double transport)
            =>staff<=0?0:Math.Max(24,HistoricalInlandSettlement.Investment(staff,Math.Max(0,effort)*2,transport));
        public static int Exposure(double transmission)=>Math.Max(1,(int)Math.Round(80*D.Clamp(transmission)));
    }
    public sealed partial class Prehistory
    {
        bool TryLivingTownRegionalDelivery(PrehistoryState s,int city,int region)
        {
            if(!LivingWorldHistory(s)||city<0||region<0||region>=Patches.Length)return false;
            var h=s.History;var a=h.Aggregates;var town=s.Society.Settlements[city];var account=a.Cities[city];var life=a.Living.Cities[city];
            int staff=HistoricalRegionalTownExchange.Staff(town.Population),target=a.Regions[region].BestCell,culture=town.CultureId;
            if(staff==0||!account.Supported||life.AbandonedYear>=0||life.DeclineYears>0||ProvincialContactResidents(s,region)<100)return false;
            int receiver=HistoricalCells.Culture(s,target);if(receiver<0)receiver=a.Regions[region].Culture;
            if(receiver==culture)return false;
            int owner=a.Resolve(town.PolityId,h),host=HistoricalCells.Polity(s,target);
            if(host>=0&&owner!=host)
            {
                if(owner<0||!aggregateRelations.TryGetValue((Math.Min(owner,host),Math.Max(owner,host)),out int relation)||!h.Relations[relation].Trade||h.Relations[relation].WarSince>=0)return false;
            }
            if(!InstitutionalRouteAllowed(s,town.Cell,target,culture))return false;
            // This is an overland/hinterland service. Actual overseas delivery
            // remains on the separately provisioned maritime path.
            if(World.Landmass[town.Cell]<0||World.Landmass[town.Cell]!=World.Landmass[target])return false;
            double transport=HistoricalModernEffects.LandTransport(h.Cultures[culture],s.Years),effort=LivingTravelKm(s,town.Cell,target,culture);
            if(effort>600*transport)return false;
            long food=HistoricalRegionalTownExchange.Food(staff,effort,transport),investment=HistoricalRegionalTownExchange.Investment(staff,effort,transport);
            if(town.FoodReserve<food+town.Population/2)return false;
            bool sponsored=owner>=0;
            if(sponsored&&(h.Polities[owner].EndedYear>=0||h.Polities[owner].Treasury<investment*2||a.Polities[owner].Receipts<=a.Polities[owner].ServiceCost))return false;
            double transmission=EnvironmentalTransmission(s,town.Cell,target,culture);
            if(HistoryRoll("living-town-regional-delivery-"+region,city,s.Years)>=transmission)return false;
            long package=ProvincialKnowledge(h.Cultures[culture]);if(package==0)return false;
            town.FoodReserve-=food;if(sponsored)h.Polities[owner].Treasury-=investment;
            var local=a.Living.Regions[region];
            local.Teacher=culture;local.Exposure=Math.Min(1000,local.Exposure+HistoricalRegionalTownExchange.Exposure(transmission));local.LastExchangeYear=s.Years;
            a.Provinces.RegionalKnowledge[region]|=package;
            // A dated physical receipt is enough: existing learning and actual
            // compatible cultivar uptake still do their own work. Do not call
            // an origin-to-origin fallback or award levels/crops here.
            if(receiver>=0)OfferProvincialKnowledge(s,receiver,culture,package,s.Years,true);
            if(sponsored)
            {
                double example=1000*InstitutionalLocalExample(s,owner,town.Cell,target);
                AccumulateInstitutionalDelivery(s,region,owner,example,transmission,40,"living-town-rural-institutions-"+city);
            }
            return true;
        }
        void AdvanceLivingTownRegionalExchange(PrehistoryState s,int city)
        {
            if(!LivingWorldHistory(s))return;int region=CellPatch[s.Society.Settlements[city].Cell];if(region<0)return;
            AdvanceRevisedLocalTownRelays(s,city);
            TryLivingTownRegionalDelivery(s,city,region);
            var routes=s.History.Aggregates.Regions[region].Routes;
            int offset=routes.Length==0?0:(int)(HistoryRoll("living-town-rural-route",city,s.Years)*routes.Length),previous=-1;
            for(int trial=0;trial<Math.Min(2,routes.Length);trial++)
            {
                var route=Routes[routes[(offset+trial)%routes.Length]];if(route.Sea)continue;
                int target=route.A==region?route.B:route.A;if(target==previous)continue;previous=target;
                TryLivingTownRegionalDelivery(s,city,target);
            }
        }
    }
}
