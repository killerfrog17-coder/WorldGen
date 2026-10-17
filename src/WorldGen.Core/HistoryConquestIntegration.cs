using System;
namespace WorldGen.Core
{
    public readonly struct HistoricalConquestRecovery
    {
        public readonly int Debt,PaidReduction;public readonly long Payment;
        public HistoricalConquestRecovery(int debt,int paid,long payment){Debt=debt;PaidReduction=paid;Payment=payment;}
    }
    public static class HistoricalConquestIntegrationEffects
    {
        static double Unit(double value)=>double.IsNaN(value)||double.IsInfinity(value)?0:D.Clamp(value);
        // Authored burden units, not an empirical cost per person. A village
        // and a whole country are not equivalent acquisitions.
        public static double Capture(long residents,double administered,double distanceRatio,bool recentDirectReturn,double recipientInclusion)
        {
            if(residents<=0)return 0;
            if(double.IsNaN(administered)||double.IsInfinity(administered))administered=0;
            double fraction=residents/(Math.Max(0,administered)+residents);
            double distance=double.IsNaN(distanceRatio)||double.IsInfinity(distanceRatio)?3:1+Math.Min(2,Math.Max(0,distanceRatio-1));
            return D.Q(Math.Min(1000,500*fraction*distance*(recentDirectReturn?.75:1)*(1-.25*Unit(recipientInclusion))),1000000);
        }
        public static double Capacity(double integration,double bureaucracy,int works,int inclusion,bool served,double distanceRatio,double crisis,int exhaustion)
        {
            if(!served||double.IsNaN(distanceRatio)||double.IsInfinity(distanceRatio))return 0;
            double over=Math.Max(0,distanceRatio-1),delivery=1/(1+over*over);
            return Unit(integration)*(.5+.5*Unit(bureaucracy))*Unit(works/1000.0)*Unit(inclusion/1000.0)*delivery*(1-.75*Unit(crisis))*(1-Unit(exhaustion/1000.0));
        }
        public static HistoricalConquestRecovery Recover(int debt,int elapsed,int baseRate,bool baseAllowed,double capacity,long receipts,long service,long treasury)
        {
            debt=Math.Max(0,Math.Min(1000,debt));if(elapsed<=0)return new HistoricalConquestRecovery(debt,0,0);
            int remaining=(int)Math.Max(0,debt-(baseAllowed?(double)Math.Max(0,baseRate)*elapsed:0));
            int wanted=(int)Math.Floor(remaining*(1-Math.Exp(-.04*Unit(capacity)*elapsed)));
            if(wanted<=0||treasury<=0||receipts<=0||service<0||receipts<=service)return new HistoricalConquestRecovery(remaining,0,0);
            long price=Math.Max(1,Math.Max(0,receipts)/1000);
            decimal margin=Math.Max(0m,(decimal)receipts-service),budget=Math.Min(Math.Max(0,treasury),margin*.05m*elapsed);
            int paid=(int)Math.Min(wanted,Math.Min(1000m,decimal.Floor(budget/price)));
            // paid*price <= real budget <= treasury; decimal avoids an
            // overflowing intermediate long product even for hostile inputs.
            long payment=(long)((decimal)paid*price);
            return new HistoricalConquestRecovery(remaining-paid,paid,payment);
        }
    }
    public sealed partial class Prehistory
    {
        bool ConquestIntegrationHistory(PrehistoryState s)=>s?.History?.Aggregates?.Empires?.Connections?.Realms?.Version>=25;
        long ConquestTransferredResidents(PrehistoryState s,int cell,int winner)
        {
            var h=s.History;var a=h.Aggregates;if(HistoricalCells.Polity(s,cell)==winner||CellPatch[cell]<0)return 0;
            int raw=a.Cells[cell*5+1];if(LivingHistory(s))raw=a.PopulationCohort(raw);long people;
            double area=inhabitableArea[cell];int patch=CellPatch[cell];
            if(raw<0){var region=a.Regions[patch];double remaining=ProvincialHistory(s)?region.AreaKm2-a.Provinces.ClaimedAreaKm2[patch]:region.AreaKm2;people=Math.Min(s.Population[patch],(long)(s.Population[patch]*Math.Min(1,area/Math.Max(1,remaining))));}
            else{var before=a.Polities[raw];double fraction=Math.Min(1,area/Math.Max(1,before.RemoteAreaKm2));people=Math.Min(before.Remote,(long)(before.Remote*fraction));}
            if(raw==winner)people=0;
            int town=a.TownAt[cell];if(town>=0&&a.Resolve(s.Society.Settlements[town].PolityId,h)!=winner){var city=a.Cities[town];people+=s.Society.Settlements[town].Population+city.Suburban+city.Rural;}
            return Math.Max(0,people);
        }
        double ConquestCaptureLoad(PrehistoryState s,int winner,int loser,int cell,int supplyTown)
        {
            var h=s.History;var a=h.Aggregates;var p=h.Polities[winner];var account=a.Polities[winner];long acquired=ConquestTransferredResidents(s,cell,winner);
            double geography=Math.Sqrt(Math.Max(1,p.AreaKm2)/Math.PI)/Math.Max(50,account.ReachKm),served=1/(1+geography*geography);
            double administered=Math.Max(0,account.Urban)+Math.Max(0,(double)account.Suburban)+Math.Max(0,(double)account.Rural)+Math.Max(0,(double)account.Remote)*served;
            int capital=s.Society.Settlements[p.CapitalId].Cell;
            double effort=LivingTravelKm(s,capital,cell,p.CultureId);
            if(supplyTown>=0&&supplyTown<s.Society.Settlements.Length)
            {int supply=s.Society.Settlements[supplyTown].Cell;effort=Math.Max(effort,LivingTravelKm(s,capital,supply,p.CultureId)+LivingTravelKm(s,supply,cell,p.CultureId));}
            double reach=Math.Max(50,HistoricalProvincialEffects.LocalServiceKm(h.Cultures[p.CultureId],s.Years,h.Dynamics.States[winner].PublicWorks));
            bool recent=loser>=0&&h.Polities[loser].ParentId==winner&&s.Years>=h.Polities[loser].FoundedYear&&s.Years-h.Polities[loser].FoundedYear<=50;
            int town=a.TownAt[cell];double inclusion=0;
            if(town>=0){var local=a.Communities.Cities[town];if(local.RulerId>=0&&a.Resolve(local.RulerId,h)==winner)inclusion=local.InclusionMilli/1000.0;}
            return HistoricalConquestIntegrationEffects.Capture(acquired,administered,effort/reach,recent,inclusion);
        }
        long AdvanceConquestIntegration(PrehistoryState s,int id,int elapsed)
        {
            var h=s.History;var a=h.Aggregates;var p=h.Polities[id];var account=a.Polities[id];var ds=h.Dynamics.States[id];var cycle=ds.Cycle;double crisis=RealmCrisis(s,id),capacity=0;
            if(account.Cities.Count>0)
            {
                int city=account.Cities.At((int)(HistoryRoll("aggregate-province",id,s.Years)*account.Cities.Count));var town=s.Society.Settlements[city];var local=a.Communities.Cities[city];
                int source=a.Empires.Territories.Sources[town.Cell],capital=s.Society.Settlements[p.CapitalId].Cell;
                bool served=town.Population>0&&a.Living.Cities[city].AbandonedYear<0&&local.RulerId>=0&&a.Resolve(local.RulerId,h)==id&&TerritorialNodeLive(s,id,source)&&InstitutionalRouteAllowed(s,capital,town.Cell,p.CultureId);
                double distance=LivingTravelKm(s,capital,town.Cell,p.CultureId)/Math.Max(50,HistoricalProvincialEffects.LocalServiceKm(h.Cultures[p.CultureId],s.Years,ds.PublicWorks));
                capacity=HistoricalConquestIntegrationEffects.Capacity(RealmIntegration(s,id),RealmBureaucracy(s,id),ds.PublicWorks,local.InclusionMilli,served,distance,crisis,p.Exhaustion);
            }
            int rate=HistoricalTechnologies.Has(h.Cultures[p.CultureId],HistoricalTechnology.Writing)?2:1;
            var recovery=HistoricalConquestIntegrationEffects.Recover(cycle.ConquestDebt,elapsed,rate,crisis<=0&&account.Receipts>=account.ServiceCost,capacity,account.Receipts,account.ServiceCost,p.Treasury);
            cycle.ConquestDebt=recovery.Debt;p.Treasury-=recovery.Payment;
            return elapsed>0?recovery.Payment/elapsed:0;
        }
    }
}
