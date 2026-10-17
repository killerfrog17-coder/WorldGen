using System;
using System.Linq;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        long AggregateRuralCellPopulation(PrehistoryState s,int cell)
        {
            if(cell<0||cell>=CellPatch.Length||CellPatch[cell]<0)return 0;var a=s.History.Aggregates;int region=CellPatch[cell],market=HistoricalCells.Market(s,cell),state=HistoricalCells.Polity(s,cell);double area=World.Surface.Cells[cell].AreaM2/1e6;
            // Read-only downscaling for map inspection. This is never a census:
            // the authoritative totals live in city and remote accounts.
            if(market>=0&&market<a.Cities.Length&&a.Cities[market].AreaKm2>0)return (long)Math.Round((a.Cities[market].Rural+a.Cities[market].Suburban)*Math.Min(1,area/a.Cities[market].AreaKm2));
            int raw=a.Cells[cell*5+1];if(LivingHistory(s))raw=a.PopulationCohort(raw);if(raw>=0&&a.Polities[raw].RemoteAreaKm2>0)return (long)Math.Round(a.Polities[raw].Remote*Math.Min(1,area/a.Polities[raw].RemoteAreaKm2));
            return (long)Math.Round(s.Population[region]*Math.Min(1,area/Math.Max(1,a.Regions[region].AreaKm2)));
        }
        void SetAggregateLayer(PrehistoryState s,int cell,int layer,int owner)
        {
            var a=s.History.Aggregates;int old=a.Cells[cell*5+layer];if(old==owner)return;
            if(layer==0&&RegionalTraditions(s))old=HistoricalCells.Culture(s,cell);
            a.Cells=a.Cells.With(cell*5+layer,owner);a.CellWrites++;
            if(layer!=0&&layer!=1)return;
            long area=(long)Math.Round(layer==1&&TechnicalHistory(s)?TerritorialPoliticalArea(cell):inhabitableArea[cell]);
            if(layer==0){if(old>=0)s.Society.Cultures[old].AreaKm2-=area;if(owner>=0)s.Society.Cultures[owner].AreaKm2+=area;}
            else {old=old>=0?a.Resolve(old,s.History):-1;if(old>=0)s.History.Polities[old].AreaKm2=Math.Max(0,s.History.Polities[old].AreaKm2-area);if(owner>=0)s.History.Polities[owner].AreaKm2+=area;}
            RefreshAggregateFrontier(s,cell,layer,old);RefreshAggregateFrontier(s,cell,layer,owner);
            if(layer==1)ChangeColonialCoast(s,cell,old,owner);
            if(layer==1)RefreshServiceRecoveryNeighborhood(s,cell);
            foreach(int neighbor in World.Surface.Cells[cell].Neighbors){int id=layer==0?HistoricalCells.Culture(s,neighbor):HistoricalCells.Polity(s,neighbor);if(id>=0)RefreshAggregateFrontier(s,neighbor,layer,id);}
        }
        void RefreshAggregateFrontier(PrehistoryState s,int cell,int layer,int owner)
        {
            if(owner<0)return;var a=s.History.Aggregates;var bag=layer==0?a.Cultures[owner].Frontier:a.Polities[owner].Frontier;
            int current=layer==0?HistoricalCells.Culture(s,cell):HistoricalCells.Polity(s,cell);a.CellReads++;
            if(current!=owner){bag.Remove(cell);return;}
            foreach(int neighbor in World.Surface.Cells[cell].Neighbors){a.CellReads++;if(PoliticalFrontierCell(s,neighbor,layer)&&(layer==0?HistoricalCells.Culture(s,neighbor):HistoricalCells.Polity(s,neighbor))!=owner){bag.Add(cell);return;}}
            bag.Remove(cell);
        }
        int LocalFrontierTarget(PrehistoryState s,HistoricalLocalBag bag,int id,string stream,int layer)
        {
            if(bag.Count==0)return -1;int cell=bag.At(Math.Min(bag.Count-1,(int)(HistoryRoll(stream,id,s.Years)*bag.Count)));var neighbors=World.Surface.Cells[cell].Neighbors;
            int offset=(int)(HistoryRoll(stream+"-edge",id,s.Years)*neighbors.Length);
            for(int k=0;k<neighbors.Length;k++){int next=neighbors[(offset+k)%neighbors.Length];s.History.Aggregates.CellReads++;if(!PoliticalFrontierCell(s,next,layer)||World.Hydrology.LakeCoverageFraction[next]>=.9)continue;int owner=layer==0?HistoricalCells.Culture(s,next):HistoricalCells.Polity(s,next);if(owner!=id)return next;}
            RefreshAggregateFrontier(s,cell,layer,id);return -1;
        }
        void ClaimAggregatePolity(PrehistoryState s,int cell,int polity)
        {
            var h=s.History;var a=h.Aggregates;int old=HistoricalCells.Polity(s,cell),raw=a.Cells[cell*5+1];if(old==polity)return;
            if(LivingHistory(s))raw=a.PopulationCohort(raw);
            int patch=CellPatch[cell];
            if(patch<0)
            {
                if(!TechnicalHistory(s)||World.Hydrology.OceanConnected[cell])return;
                // Sovereignty over empty land is not a demographic account.
                // No invented villagers, farmland or ecological patch results.
                SetAggregateLayer(s,cell,1,polity);SetAggregateLayer(s,cell,4,polity>=0?3:0);
                a.Provinces.Centers=a.Provinces.Centers.With(cell,polity>=0?h.Polities[polity].CapitalId:-1);AssignTerritorialSource(s,cell,polity>=0?h.Polities[polity].CapitalId:-1);TransferTerritorialPost(s,cell,polity);return;
            }
            var region=a.Regions[patch];long people;
            double oldFraction=raw<0?0:Math.Min(1,inhabitableArea[cell]/Math.Max(1,a.Polities[raw].RemoteAreaKm2));
            if(ProvincialHistory(s)&&raw>=0)a.Provinces.FarmPotential[raw]=Math.Max(0,a.Provinces.FarmPotential[raw]-(long)(a.Provinces.FarmPotential[raw]*oldFraction));
            if(raw<0){double remaining=ProvincialHistory(s)?region.AreaKm2-a.Provinces.ClaimedAreaKm2[patch]:region.AreaKm2;people=Math.Min(s.Population[patch],(long)(s.Population[patch]*Math.Min(1,inhabitableArea[cell]/Math.Max(1,remaining))));s.Population[patch]-=people;}
            else {var before=a.Polities[raw];double fraction=Math.Min(1,inhabitableArea[cell]/Math.Max(1,before.RemoteAreaKm2));people=Math.Min(before.Remote,(long)(before.Remote*fraction));before.Remote-=people;before.RemoteCapacity=Math.Max(0,before.RemoteCapacity-(long)(before.RemoteCapacity*fraction));before.RemoteAreaKm2=Math.Max(0,before.RemoteAreaKm2-(long)Math.Round(inhabitableArea[cell]));if(h.Polities[raw].EndedYear<0)h.Polities[raw].Population=Math.Max(0,h.Polities[raw].Population-people);}
            if(polity>=0){a.Polities[polity].Remote+=people;a.Polities[polity].RemoteCapacity+=Math.Max(people,ruralWeight[cell]/1000);a.Polities[polity].RemoteAreaKm2+=(long)Math.Round(inhabitableArea[cell]);h.Polities[polity].Population+=people;}
            else s.Population[patch]+=people;
            if(ProvincialHistory(s))
            {
                if(polity>=0)a.Provinces.FarmPotential[polity]+=ProvincialFarmPotential(s,cell,h.Polities[polity].CultureId);
                a.Provinces.Centers=a.Provinces.Centers.With(cell,polity>=0?h.Polities[polity].CapitalId:-1);
                if(raw<0&&polity>=0)a.Provinces.ClaimedAreaKm2[patch]+=(long)Math.Round(inhabitableArea[cell]);
                if(raw>=0&&polity<0)a.Provinces.ClaimedAreaKm2[patch]=Math.Max(0,a.Provinces.ClaimedAreaKm2[patch]-(long)Math.Round(inhabitableArea[cell]));
                RefreshProvincialRegionalCapacity(s,patch);
            }
            SetAggregateLayer(s,cell,1,polity);SetAggregateLayer(s,cell,4,polity>=0?3:HistoricalCells.Culture(s,cell)>=0?1:0);
            if(TechnicalHistory(s))AssignTerritorialSource(s,cell,polity>=0?h.Polities[polity].CapitalId:-1);
            TransferTerritorialPost(s,cell,polity);
            int town=a.TownAt[cell];if(town>=0)AssignAggregateTown(s,town,polity);
        }
        void AssignAggregateTown(PrehistoryState s,int id,int polity)
        {
            var h=s.History;var a=h.Aggregates;var town=s.Society.Settlements[id];var city=a.Cities[id];int old=a.Resolve(town.PolityId,h);if(old==polity&&town.PolityId==polity)return;
            long residents=town.Population+city.Suburban+city.Rural;
            if(old>=0){var p=a.Polities[old];p.Cities.Remove(id);p.CityCount=p.Cities.Count;p.Urban-=town.Population;p.Rural-=city.Rural;p.Suburban-=city.Suburban;h.Polities[old].Population=Math.Max(0,h.Polities[old].Population-residents);}
            if(polity>=0){var p=a.Polities[polity];p.Cities.Add(id);p.CityCount=p.Cities.Count;p.Urban+=town.Population;p.Rural+=city.Rural;p.Suburban+=city.Suburban;h.Polities[polity].Population+=residents;town.Government=h.Polities[polity].Government;}
            town.PolityId=polity;
            QueueTerritorialServiceRecovery(s,id);
            if(CommunityHistory(s))TransferCommunityGovernment(s,id,polity);
            TransferRealmClaimantCity(s,id,old,polity);
            if(ProvincialHistory(s))a.Provinces.Centers=a.Provinces.Centers.With(town.Cell,polity>=0?id:-1);
            if(TechnicalHistory(s))AssignTerritorialSource(s,town.Cell,polity>=0?id:-1);
            TransferRealmCenterTitles(s,id,old,polity);
            if(old>=0&&h.Polities[old].CapitalId==id){var prior=a.Polities[old];if(prior.Cities.Count>0)h.Polities[old].CapitalId=prior.Cities.At(0);else EndAggregateState(s,old,"Its last administrative center was lost.");}
        }
        void EndAggregateState(PrehistoryState s,int id,string reason,int successor=-1)
        {
            ReleaseMilitaryGovernment(s,id);
            var h=s.History;var a=h.Aggregates;var p=h.Polities[id];if(p.EndedYear>=0)return;var account=a.Polities[id];
            if(LivingWorldHistory(s))CloseNuclearState(s,id,successor);
            CloseInternationalState(s,id);
            EndImperialGovernment(s,id);
            p.EndedYear=s.Years;p.Army=0;a.Successors=a.Successors.With(id,successor);
            QueueRetiredServiceGovernment(s,id);
            if(LivingWorldHistory(s)&&a.Empires.Connections.PortSettlements!=null)a.Empires.Connections.PortSettlements.Governments[id]=new HistoricalPendingPortGovernment();
            // Remote communities outlive the court. Their cohort retains its
            // geography and slower demographic event until locally reclaimed.
            // No teleportation to the capital and no visit to former holdings.
            if(successor>=0){var target=a.Polities[successor];target.Remote+=account.Remote;target.RemoteCapacity+=account.RemoteCapacity;h.Polities[successor].Population+=account.Remote;h.Polities[successor].AreaKm2+=p.AreaKm2;}
            else if(account.Remote>0){account.LastYear=s.Years;ScheduleAggregate(s,RemoteProcess,id);}
            if(successor>=0){account.Remote=0;account.RemoteCapacity=0;}
            if(ProvincialHistory(s)&&successor>=0){a.Provinces.FarmPotential[successor]+=a.Provinces.FarmPotential[id];a.Provinces.FarmPotential[id]=0;}
            int leader=h.Dynamics.States[id].LeaderId;if(leader>=0){h.Dynamics.Leaders[leader].EndedYear=s.Years;h.Dynamics.Leaders[leader].Legacy=reason;SealAggregateLeader(s,leader);}
            HistoryEvent(s,"collapse",p.Name+" loses its independent government. "+reason,s.Society.Settlements[p.CapitalId].Cell,p.CultureId,id,other:successor,category:ChronicleCategory.Politics);
        }
    }
}
