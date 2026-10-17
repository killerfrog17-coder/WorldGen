using System;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        static bool RevisedInlandHistory(PrehistoryState s)=>s?.History?.Aggregates?.Empires?.Connections?.Realms?.Version>=5;
        V RevisedInlandProposal(PrehistoryState s,int parent,int trial,double bearing)
        {
            var a=s.History.Aggregates;var town=s.Society.Settlements[parent];int from=town.Cell,owner=a.Resolve(town.PolityId,s.History);
            double radius=owner>=0?TerritorialServiceRadius(s,owner,from):-1,km=HistoricalInlandSettlement.LocalProposalKm(trial,LivingSettlementProbeReach(s,parent),radius);
            double direction=bearing+trial/3.0;
            if(trial==0)
            {
                // One proposal follows a real adjacent river corridor rather
                // than a uniformly drawn compass direction. Rotating local
                // tributaries includes upstream as well as seaward expansion.
                var neighbors=World.Surface.Cells[from].Neighbors;int offset=(parent+s.Years/80)%neighbors.Length;
                for(int k=0;k<neighbors.Length;k++)
                {
                    int next=neighbors[(offset+k)%neighbors.Length];
                    if(CellPatch[next]<0||World.Landmass[next]!=World.Landmass[from]||World.Ecology.Habitat[next].ReliefM>=2000||riverAccess[from]<=.02||riverAccess[next]<=.02||World.Hydrology.Watershed[from]<0||World.Hydrology.Watershed[from]!=World.Hydrology.Watershed[next])continue;
                    var p=World.Surface.Cells[from].Position;var east=V.Tangent(p,new V(0,0,1));if(east.Length<.5)east=V.Tangent(p,new V(1,0,0));var north=V.Cross(p,east).Unit;var to=World.Surface.Cells[next].Position;
                    direction=Math.Atan2(V.Dot(to,north),V.Dot(to,east))/(2*Math.PI);break;
                }
            }
            return HistoricalSettlementNetworks.LocalProbeKm(World.Surface.Cells[from].Position,World.Recipe.RadiusKm,direction,km);
        }
        bool TryRevisedLocalTownRelay(PrehistoryState s,int source,int target,int elapsed)
        {
            if(!RevisedInlandHistory(s)||source==target||elapsed<=0)return false;var h=s.History;var a=h.Aggregates;var from=s.Society.Settlements[source];var to=s.Society.Settlements[target];
            if(a.Living.Cities[source].AbandonedYear>=0||a.Living.Cities[target].AbandonedYear>=0||!a.Cities[source].Supported||!a.Cities[target].Supported||a.Living.Cities[source].DeclineYears>0||from.Population<1200||to.Population<300)return false;
            if(World.Landmass[from.Cell]<0||World.Landmass[from.Cell]!=World.Landmass[to.Cell])return false;
            double direct=D.Angle(World.Surface.Cells[from.Cell].Position,World.Surface.Cells[to.Cell].Position)*World.Recipe.RadiusKm;if(direct>280||!InstitutionalRouteAllowed(s,from.Cell,to.Cell,from.CultureId))return false;
            int owner=a.Resolve(from.PolityId,h),host=a.Resolve(to.PolityId,h);
            var lesson=h.Towns[source];int teacher=owner>=0?owner:lesson.CivicTeacherPolityId;double exposure=owner>=0?1000:lesson.CivicExampleExposure;
            if(from.CultureId==to.CultureId&&(teacher<0||exposure<=h.Towns[target].CivicExampleExposure))return false;
            if(owner>=0&&host>=0&&owner!=host)
            {if(!aggregateRelations.TryGetValue((Math.Min(owner,host),Math.Max(owner,host)),out int relation)||h.Relations.Peek(relation).WarSince>=0||!h.Relations.Peek(relation).Trade)return false;}
            double transport=HistoricalModernEffects.LandTransport(h.Cultures[from.CultureId],s.Years),effort=LivingTravelKm(s,from.Cell,to.Cell,from.CultureId);
            int staff=HistoricalRegionalTownExchange.Staff(from.Population);long food=HistoricalRegionalTownExchange.Food(staff,effort,transport),investment=HistoricalRegionalTownExchange.Investment(staff,effort,transport);
            if(from.FoodReserve<food+from.Population/2||owner>=0&&(h.Polities[owner].Treasury<investment*2||a.Polities[owner].Receipts<=a.Polities[owner].ServiceCost))return false;
            double transmission=EnvironmentalTransmission(s,from.Cell,to.Cell,from.CultureId);
            if(HistoryRoll("local-inland-relay-"+target,source,s.Years)>=transmission)return false;
            from.FoodReserve-=food;if(owner>=0)h.Polities[owner].Treasury-=investment;
            LinkLivingTowns(s,source,target);
            if(from.CultureId!=to.CultureId)OfferProvincialKnowledge(s,to.CultureId,from.CultureId,ProvincialKnowledge(h.Cultures[from.CultureId]),s.Years,true);
            // The actual teacher is here. A remembered distant capital gives
            // no refill; a local trained community can relay its own practice
            // even after the originating dynasty has ceased to exist.
            if(teacher>=0&&exposure>0)
            {
                var receiver=h.Towns[target];double expected=HistoricalInstitutionalTransmission.LocalLearning(exposure,transmission,Math.Min(80,elapsed));int gain=(int)expected;
                if(HistoryRoll("local-inland-civic-"+target,source,s.Years)<expected-gain)gain++;
                receiver.CivicTeacherPolityId=teacher;receiver.CivicExampleExposure=Math.Min(1000,receiver.CivicExampleExposure+gain);
            }
            return true;
        }
        void AdvanceRevisedLocalTownRelays(PrehistoryState s,int city)
        {
            if(!RevisedInlandHistory(s))return;var town=s.Society.Settlements[city];var neighbors=NearbyLivingTowns(s,town.Cell,280);if(neighbors.Length==0)return;
            int offset=(city+s.Years/40)%neighbors.Length;
            for(int k=0;k<Math.Min(2,neighbors.Length);k++){int peer=neighbors[(offset+k)%neighbors.Length];if(peer!=city)TryRevisedLocalTownRelay(s,city,peer,40);}
        }
    }
}
