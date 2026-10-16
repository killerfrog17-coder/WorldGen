using System;
using System.Linq;
namespace WorldGen.Core
{
    [Serializable] public sealed class HistoricalProvincialSupport
    {
        public HistoricalCellTree Centers;
        public int UpgradedYear;
        public long[] FarmPotential=Array.Empty<long>();
        public long[] ClaimedAreaKm2=Array.Empty<long>();
        public long[] RegionalKnowledge=Array.Empty<long>();
        public HistoricalExchangeMemory[] Exchanges=Array.Empty<HistoricalExchangeMemory>();
        public HistoricalCampaignTheater[] Theaters=Array.Empty<HistoricalCampaignTheater>();
        public HistoricalProvincialSupport Copy()=>new HistoricalProvincialSupport{Centers=Centers,UpgradedYear=UpgradedYear,FarmPotential=(long[])FarmPotential.Clone(),ClaimedAreaKm2=(long[])ClaimedAreaKm2.Clone(),RegionalKnowledge=(long[])RegionalKnowledge.Clone(),Exchanges=Exchanges.Select(e=>e.Copy()).ToArray(),Theaters=Theaters.Select(t=>t.Copy()).ToArray()};
    }
    public sealed partial class Prehistory
    {
        void PrepareProvincialSupport(PrehistoryState s)
        {
            var a=s.History.Aggregates;if(a.Provinces!=null)return;
            var support=new HistoricalProvincialSupport{UpgradedYear=s.Years,FarmPotential=new long[a.Polities.Length],ClaimedAreaKm2=new long[a.Regions.Length],RegionalKnowledge=new long[a.Regions.Length],Exchanges=a.Cultures.Select(_=>new HistoricalExchangeMemory()).ToArray()};
            // Explicit initialization/upgrade only. No recurring mesh census.
            support.Centers=HistoricalCellTree.From(CellPatch.Length,cell=>{int p=HistoricalCells.Polity(s,cell);return p<0?-1:s.History.Polities[p].CapitalId;});
            for(int cell=0;cell<CellPatch.Length;cell++)
            {int raw=a.Cells[cell*5+1];if(raw<0||CellPatch[cell]<0)continue;int cohort=a.PopulationCohort(raw);support.FarmPotential[cohort]+=ProvincialFarmPotential(s,cell,s.History.Polities[cohort].CultureId);support.ClaimedAreaKm2[CellPatch[cell]]+=(long)Math.Round(inhabitableArea[cell]);}
            a.Provinces=support;
            support.Theaters=s.History.Dynamics.Campaigns.Select(c=>new HistoricalCampaignTheater{Cell=s.Society.Settlements[s.History.Polities[c.AttackerId==c.A?c.B:c.A].CapitalId].Cell}).ToArray();
            foreach(var c in s.Society.Cultures)foreach(int peer in c.Contacts)OfferProvincialKnowledge(s,c.Id,peer,ProvincialKnowledge(s.History.Cultures[peer]),s.Years,false);
            for(int r=0;r<a.Regions.Length;r++){int teacher=DevelopmentRegionalTeacher(s,r);if(teacher>=0)support.RegionalKnowledge[r]=ProvincialKnowledge(s.History.Cultures[teacher]);}
            for(int r=0;r<a.Regions.Length;r++)RefreshProvincialRegionalCapacity(s,r);
        }
        void RefreshProvincialRegionalCapacity(PrehistoryState s,int region)
        {
            var a=s.History.Aggregates;double total=Math.Max(1,a.Regions[region].AreaKm2);
            double available=(1-D.Clamp(a.Living.Regions[region].CommittedAreaKm2/total))*(1-D.Clamp(a.Provinces.ClaimedAreaKm2[region]/total));
            a.Regions[region].Capacity=(long)Math.Round(Patches[region].Capacity*available);
        }
        long ProvincialRegionalCapacity(PrehistoryState s,int region,int culture)
        {
            var a=s.History.Aggregates;var c=s.Society.Cultures[culture];double total=Math.Max(1,a.Regions[region].AreaKm2);
            double area=Math.Max(0,total-a.Provinces.ClaimedAreaKm2[region])*(1-D.Clamp(a.Living.Regions[region].CommittedAreaKm2/total));
            double yield=ProvincialPhysicalYield(s,culture,a.Regions[region].BestCell);
            return a.Regions[region].Capacity+(long)(area*65*yield*D.Clamp(c.Farming/600.0)*EffectiveFoodMultiplier(s,s.History.Cultures[culture]));
        }
        long ProvincialFarmPotential(PrehistoryState s,int cell,int culture)
        {
            int region=CellPatch[cell];double free=1-D.Clamp(s.History.Aggregates.Living.Regions[region].CommittedAreaKm2/(double)Math.Max(1,s.History.Aggregates.Regions[region].AreaKm2));
            return (long)Math.Round(inhabitableArea[cell]*free*65*ProvincialPhysicalYield(s,culture,cell));
        }
        double ProvincialPhysicalYield(PrehistoryState s,int culture,int cell)
        {
            var habitat=World.Ecology.Habitat[cell];
            double fraction=Math.Min(1,Math.Max(.08,D.Clamp((habitat.AridityMilli-100)/550.0)+riverAccess[cell]));
            // LivingCropLandscape normalizes the QUALITY of an irrigable strip
            // for local institutions. Production must restore its actual ACREAGE.
            return D.Q(LivingCropLandscape(s,culture,cell,false)*fraction,1000000);
        }
        long ProvincialRemoteCapacity(PrehistoryState s,int polity)
        {
            var a=s.History.Aggregates;int culture=s.History.Polities[polity].CultureId;
            double farming=D.Clamp(s.Society.Cultures[culture].Farming/600.0);
            var hc=s.History.Cultures[culture];
            return Math.Max(1,a.Polities[polity].RemoteCapacity+(long)(a.Provinces.FarmPotential[polity]*farming*EffectiveFoodMultiplier(s,hc)*(1-HistoricalProvincialEffects.UrbanShare(hc,s.Years))));
        }
        long ProvincialUrbanSupply(PrehistoryState s,int polity)
        {
            var a=s.History.Aggregates;var p=s.History.Polities[polity];var hc=s.History.Cultures[p.CultureId];
            double integration=D.Clamp((HistoricalModernEffects.LandTransport(hc,s.Years)-1)/3.5);
            double potential=a.Provinces.FarmPotential[polity]*D.Clamp(s.Society.Cultures[p.CultureId].Farming/600.0)*EffectiveFoodMultiplier(s,hc);
            double share=HistoricalProvincialEffects.UrbanShare(hc,s.Years);
            double marketed=Math.Min(potential*share,a.Polities[polity].Remote*share/Math.Max(.1,1-share));
            return (long)(marketed*integration/Math.Max(1,a.Polities[polity].CityCount)/1.15);
        }
        int ProvincialRegionalCohort(PrehistoryState s,int region)
            =>CachedProvincialCohort(s,region);
        int ProvincialRegionalCourt(PrehistoryState s,int region)
        {int cohort=ProvincialRegionalCohort(s,region);return cohort<0?-1:s.History.Aggregates.Resolve(cohort,s.History);}
        long ProvincialContactResidents(PrehistoryState s,int region,bool recordReads=true)
        {
            var a=s.History.Aggregates;long people=s.Population[region]+a.Living.Regions[region].SettledPopulation;
            int owner=CachedProvincialCohort(s,region,recordReads);
            // Contact staging estimate only, never a new population account.
            // Claiming countryside must not turn its residents into empty space
            // merely because they moved from a regional to a state rural ledger.
            if(owner>=0)people+=(long)(a.Polities[owner].Remote*Math.Min(1,a.Provinces.ClaimedAreaKm2[region]/(double)Math.Max(1,a.Polities[owner].RemoteAreaKm2)));
            return people;
        }
        int ProvincialServiceCenter(PrehistoryState s,int polity,int target)
        {
            var a=s.History.Aggregates;var p=s.History.Polities[polity];var hc=s.History.Cultures[p.CultureId];
            double radius=HistoricalProvincialEffects.LocalServiceKm(hc,s.Years,s.History.Dynamics.States[polity].PublicWorks);
            double density=AggregateRuralCellPopulation(s,target)/Math.Max(1,inhabitableArea[target]);
            radius*=1+2*HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Railways,s.Years)*(1-D.Clamp(density/15));
            int best=-1;double effort=radius;
            void Consider(int city)
            {
                if(city<0||city>=a.Cities.Length||a.Living.Cities[city].AbandonedYear>=0||a.Resolve(s.Society.Settlements[city].PolityId,s.History)!=polity)return;
                double d=LivingTravelKm(s,s.Society.Settlements[city].Cell,target,p.CultureId);
                if(d<effort||d==effort&&(best<0||city<best)){best=city;effort=d;}
            }
            Consider(p.CapitalId);
            foreach(int next in World.Surface.Cells[target].Neighbors)if(HistoricalCells.Polity(s,next)==polity)Consider(a.Provinces.Centers[next]);
            // Fixed fallback sample, never every city or holding of an empire.
            if(best<0)for(int trial=0;trial<12&&trial<a.Polities[polity].Cities.Count;trial++)
                Consider(a.Polities[polity].Cities.At((int)(HistoryRoll("provincial-service-"+target+"-"+trial,polity,s.Years)*a.Polities[polity].Cities.Count)));
            return best;
        }
        bool ProvincialIncorporationWorthwhile(PrehistoryState s,int polity,int target,int center)
        {
            var h=s.History;var p=h.Polities[polity];var hc=h.Cultures[p.CultureId];double area=Math.Max(1,inhabitableArea[target]);
            double potential=area*65*ProvincialPhysicalYield(s,p.CultureId,target)*D.Clamp(s.Society.Cultures[p.CultureId].Farming/600.0);
            double residents=Math.Max(AggregateRuralCellPopulation(s,target),potential*.12);
            double receipts=residents*.012;
            double terrain=1+World.Ecology.Habitat[target].ReliefM/700.0;
            double costs=area*.003*terrain/(1+.8*HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Roads,s.Years)+3*HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Railways,s.Years)+4*HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Telecommunications,s.Years));
            costs*=1+.5*LivingTravelKm(s,s.Society.Settlements[center].Cell,target,p.CultureId)/Math.Max(50,HistoricalProvincialEffects.LocalServiceKm(hc,s.Years,h.Dynamics.States[polity].PublicWorks));
            if(receipts>=costs)return true;
            bool route=riverAccess[target]>.02||World.Surface.Cells[target].Neighbors.Any(n=>World.Hydrology.OceanConnected[n]);
            bool reconnaissance=HistoricalTechnologies.Has(hc,HistoricalTechnology.Railways);
            // A funded harbor/frontier post can be strategic despite low local
            // receipts. Such claims are opportunities, not free desert acreage.
            return (reconnaissance||route&&HistoricalTechnologies.Has(hc,HistoricalTechnology.Navigation))&&p.Treasury>costs*100
                &&HistoryRoll("provincial-strategic-land-"+target,polity,s.Years)<.03;
        }
    }
}
