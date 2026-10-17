using System;
using System.Linq;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        void ReconcileMilitaryParties(PrehistoryState s,int id)
        {
            var c=s.History.Dynamics.Campaigns[id];var m=s.History.Aggregates.Empires.Connections.Military;var o=m.Campaigns[id];
            if(o.PartyA==c.A&&o.PartyB==c.B)return;
            ReleaseMilitaryOperation(s,id);
            m.Campaigns[id]=new HistoricalMilitaryOperation{PartyA=c.A,PartyB=c.B,OriginalCapitalA=s.Society.Settlements[s.History.Polities[c.A].CapitalId].Cell,OriginalCapitalB=s.Society.Settlements[s.History.Polities[c.B].CapitalId].Cell};
        }
        double PrepareMilitaryField(PrehistoryState s,int id,int polity,int front,int elapsed,ref int supply,out int men)
        {
            men=0;var h=s.History;var a=h.Aggregates;var p=h.Polities[polity];
            if(p.EndedYear>=0||p.Army<20||ProvincialFieldPower(s,polity,front,ref supply,p.Army)<=0||supply<0){ReserveMilitaryContingent(s,id,polity,0);return 0;}
            var source=s.Society.Settlements[supply];
            double km=D.Angle(World.Surface.Cells[source.Cell].Position,World.Surface.Cells[front].Position)*World.Recipe.RadiusKm;
            double foodPerMan=.35*Math.Max(1,elapsed)*(1+km/2000),payPerMan=1.5*Math.Max(1,elapsed);
            long food=Math.Max(0,source.FoodReserve-source.Population/4);
            int possible=(int)Math.Min(p.Army/2,Math.Min(food/foodPerMan,p.Treasury/payPerMan));
            bool seaService=SeaServiceHistory(s)&&(World.Landmass[source.Cell]!=World.Landmass[front]||InstitutionalUnsupportedWaterKm(source.Cell,front,out _)>35);
            if(seaService&&possible>=20)
            {
                var transport=QuoteSeaService(s,polity,supply,source.Cell,front,(long)Math.Ceiling(possible*(foodPerMan+.25)),0,0,source.Population/4,true);
                possible=transport.Allowed?(int)Math.Min(possible,Math.Floor(transport.Cargo/(foodPerMan+.25))):0;
            }
            if(possible<20){ReserveMilitaryContingent(s,id,polity,0);return 0;}
            int committed=ReserveMilitaryContingent(s,id,polity,possible);men=Math.Min(committed,possible);
            double power=ProvincialFieldPower(s,polity,front,ref supply,men);if(power<=0){men=0;return 0;}
            long consumed=Math.Min(source.FoodReserve,(long)Math.Ceiling(men*foodPerMan)),paid=Math.Min(p.Treasury,(long)Math.Ceiling(men*payPerMan));
            if(seaService&&!CommitSeaService(s,QuoteSeaService(s,polity,supply,source.Cell,front,consumed,men,paid,source.Population/4)))
            {ReserveMilitaryContingent(s,id,polity,0);men=0;return 0;}
            source.FoodReserve-=consumed;p.Treasury-=paid;var ledger=a.Empires.Connections.Military.Governments[polity];ledger.FieldPayroll+=paid;ledger.FieldProvisions+=consumed;return power;
        }
        void EndRevisedMilitaryCampaign(PrehistoryState s,int id,string outcome,int truce=15,bool trade=false)
        {
            var h=s.History;var c=h.Dynamics.Campaigns[id];if(c.EndedYear>=0){ReleaseMilitaryOperation(s,id);return;}
            c.EndedYear=s.Years;c.Outcome=outcome;var relation=AggregateRelation(s,c.A,c.B);relation.WarSince=-1;relation.TruceUntil=s.Years+truce;relation.Trade=trade;
            ReleaseMilitaryOperation(s,id);
            HistoryEvent(s,"peace",c.Name+" ends. "+outcome+".",h.Aggregates.Provinces.Theaters[id].Cell,h.Polities[c.AttackerId].CultureId,c.A,c.B,category:ChronicleCategory.Politics);
        }
        bool TryRevisedMilitarySettlement(PrehistoryState s,int id,int winner,int loser,double power,double enemyPower)
        {
            var h=s.History;var c=h.Dynamics.Campaigns[id];var o=h.Aggregates.Empires.Connections.Military.Campaigns[id];bool first=winner==o.PartyA;
            int pressure=first?o.CorePressureA:o.CorePressureB,victories=first?o.VictoriesA:o.VictoriesB,capital=first?o.OriginalCapitalB:o.OriginalCapitalA;
            bool core=HistoricalCells.Polity(s,capital)==winner;
            if(!HistoricalMilitaryOperationEffects.Submission(core,pressure,victories,power,enemyPower))return false;
            if(c.Aim==HistoricalWarAim.Frontier)
            {EndRevisedMilitaryCampaign(s,id,"A supplied campaign secures a limited frontier settlement; the defeated country keeps its government",12);return true;}
            if(c.Aim==HistoricalWarAim.Tribute)
            {
                long tribute=Math.Min(h.Polities[loser].Treasury/3,Math.Max(100,h.Polities[winner].Army));h.Polities[loser].Treasury-=tribute;h.Polities[winner].Treasury+=tribute;c.TributePaid+=tribute;c.TributeRecipientId=winner;
                EndRevisedMilitaryCampaign(s,id,"The defeated court pays tribute after losses around its political core",20,true);return true;
            }
            if(c.Aim==HistoricalWarAim.TradeRoute)
            {EndRevisedMilitaryCampaign(s,id,"A negotiated settlement reopens the contested trade route after a supplied campaign",12,true);return true;}
            if(h.Polities[winner].Cohesion<450||h.Polities[winner].Treasury<100||h.Polities[loser].EndedYear>=0)return false;
            double chance=D.Clamp(.10+.25*D.Clamp(power/Math.Max(1,enemyPower)-1)+.25*(core?1:pressure/1000.0)+.20*D.Clamp(h.Polities[loser].Exhaustion/800.0));
            if(HistoryRoll("military-core-settlement",id,s.Years)>=chance)return false;
            if(!SetImperialPatron(s,loser,HistoricalImperialHierarchy.Sovereign(s,winner),true,"Supplied forces defeat the local field army and occupy or repeatedly threaten its political core; the surviving court accepts external allegiance."))return false;
            EndRevisedMilitaryCampaign(s,id,"A military settlement retains the defeated country as a constituent government",25,true);return true;
        }
        bool SpendMilitaryMarchAndSiege(PrehistoryState s,int id,int winner,int target,int source,double fieldRatio)
        {
            var h=s.History;var a=h.Aggregates;var o=a.Empires.Connections.Military.Campaigns[id];var hc=h.Cultures[h.Polities[winner].CultureId];int front=a.Provinces.Theaters[id].Cell;
            ref int days=ref(winner==o.PartyA?ref o.MarchDaysA:ref o.MarchDaysB);
            double km=D.Angle(World.Surface.Cells[front].Position,World.Surface.Cells[target].Position)*World.Recipe.RadiusKm;
            double riding=source>=0?MilitaryRidingPractice(s,h.Polities[winner].CultureId,s.Society.Settlements[source].Cell):0;
            int march=HistoricalMilitaryOperationEffects.MarchDays(km,riding,HistoricalMilitarySpecializationEffects.MountedTerrain(CropHabitat(s,target),LivingClimate(s,target),0),
                HistoricalModernEffects.Adoption(hc,HistoricalTechnology.CombustionEngines,s.Years),HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Railways,s.Years),h.Dynamics.Leaders[h.Dynamics.States[winner].LeaderId].Military);
            int town=a.TownAt[target],owner=HistoricalCells.Polity(s,target);
            if(town>=0&&owner>=0)
            {
                if(o.SiegeTarget!=target||o.SiegeBesieger!=winner){o.SiegeTarget=target;o.SiegeBesieger=winner;o.SiegeDays=0;}
                if(days<march)return false;
                var defender=h.Cultures[h.Polities[owner].CultureId];double defenses=D.Clamp((h.Dynamics.States[owner].PublicWorks/1000.0+HistoricalModernEffects.Adoption(defender,HistoricalTechnology.Writing,s.Years))*.5);
                double engineering=.2*HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Roads,s.Years)+.2*HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Steel,s.Years)+.4*HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Gunpowder,s.Years)+.2*HistoricalModernEffects.Adoption(hc,HistoricalTechnology.IndustrialMetallurgy,s.Years);
                int required=HistoricalMilitaryOperationEffects.SiegeDays(defenses,engineering,fieldRatio),work=Math.Min(days-march,Math.Max(0,required-o.SiegeDays));days-=march+work;o.SiegeDays+=work;
                if(o.SiegeDays<required)return false;o.SiegeTarget=-1;o.SiegeBesieger=-1;o.SiegeDays=0;return true;
            }
            if(days<march)return false;days-=march;return true;
        }
        void AdvanceRevisedMilitaryCampaign(PrehistoryState s,int id)
        {
            var h=s.History;var a=h.Aggregates;var c=h.Dynamics.Campaigns[id];if(c.EndedYear>=0){ReleaseMilitaryOperation(s,id);return;}
            ReconcileMilitaryParties(s,id);var o=a.Empires.Connections.Military.Campaigns[id];var theater=a.Provinces.Theaters[id];int elapsed=s.Years-c.LastActionYear;if(elapsed<=0)return;c.LastActionYear=s.Years;
            int attacker=c.AttackerId,defender=attacker==c.A?c.B:c.A;var pa=h.Polities[attacker];var pb=h.Polities[defender];
            if(theater.Cell<0||theater.Cell>=CellPatch.Length){EndRevisedMilitaryCampaign(s,id,"No physically identified theater supports this operation",8);return;}
            if(pa.EndedYear>=0||pb.EndedYear>=0){EndRevisedMilitaryCampaign(s,id,"A belligerent loses its remaining government");return;}
            if(HistoricalImperialHierarchy.Sovereign(s,attacker)==HistoricalImperialHierarchy.Sovereign(s,defender))
            {EndRevisedMilitaryCampaign(s,id,"The belligerents enter the same imperial settlement");return;}
            double powerA=PrepareMilitaryField(s,id,attacker,theater.Cell,elapsed,ref theater.SupplyA,out int menA),powerB=PrepareMilitaryField(s,id,defender,theater.Cell,elapsed,ref theater.SupplyB,out int menB);
            if(powerA<=0&&powerB<=0)
            {
                o.IdleYears+=elapsed;c.Repulses++;if(o.IdleYears>=4)EndRevisedMilitaryCampaign(s,id,"Neither side can sustain a supplied field force; the offensive is abandoned",8);return;
            }
            o.IdleYears=0;bool won=powerB<=0||powerA>0&&HistoryRoll("military-field-battle",id,s.Years)<powerA/(powerA+powerB);
            int winner=won?attacker:defender,loser=won?defender:attacker;double winningPower=won?powerA:powerB,losingPower=won?powerB:powerA;int winningMen=won?menA:menB;
            int supplyA=theater.SupplyA,supplyB=theater.SupplyB;
            if(menA>=20&&menB>=20)
            {
                double ratio=winningPower/Math.Max(.000001,losingPower);
                int lossA=HistoricalMilitaryOperationEffects.Losses(menA,won,ratio,elapsed),lossB=HistoricalMilitaryOperationEffects.Losses(menB,!won,ratio,elapsed);
                MilitaryOperationLosses(s,id,attacker,supplyA,lossA);MilitaryOperationLosses(s,id,defender,supplyB,lossB);
                var la=h.Dynamics.Leaders[h.Dynamics.States[attacker].LeaderId];var lb=h.Dynamics.Leaders[h.Dynamics.States[defender].LeaderId];
                if(won){la.Victories++;lb.Defeats++;la.Renown+=8;}else{lb.Victories++;la.Defeats++;lb.Renown+=8;}
                if(winner==o.PartyA)o.VictoriesA++;else o.VictoriesB++;
                SealAggregateLeader(s,la.Id);SealAggregateLeader(s,lb.Id);AggregateRelation(s,attacker,defender).LastBattle=s.Years;
                if(o.VictoriesA+o.VictoriesB<=2||lossA+lossB>=5000)
                    HistoryEvent(s,"field-battle",h.Polities[winner].Name+" wins a supplied field engagement in "+c.Name+". The belligerents lose "+lossA+" and "+lossB+" actual soldiers; surviving forces and reserves must support the next operation.",theater.Cell,h.Polities[winner].CultureId,winner,loser,category:ChronicleCategory.Politics);
            }
            pa.Exhaustion=Math.Min(1000,pa.Exhaustion+12*elapsed);pb.Exhaustion=Math.Min(1000,pb.Exhaustion+12*elapsed);
            if(TryRevisedMilitarySettlement(s,id,winner,loser,winningPower,losingPower))return;
            var hc=h.Cultures[h.Polities[winner].CultureId];var leader=h.Dynamics.Leaders[h.Dynamics.States[winner].LeaderId];
            long annual=(long)Math.Round(HistoricalCommunityCampaignEffects.AnnualAdvanceKm2(winningPower,HistoricalModernEffects.LandTransport(hc,s.Years),leader.Military,leader.Ambition)*D.Clamp(winningMen/300.0));
            var progress=a.Communities.Campaigns[id];ref long area=ref(won?ref progress.AttackerAreaKm2:ref progress.DefenderAreaKm2);area=Math.Min(annual*3,area+annual*elapsed);
            ref int days=ref(winner==o.PartyA?ref o.MarchDaysA:ref o.MarchDaysB);days=Math.Min(730,days+365*elapsed);
            for(int action=0;action<64;action++)
            {
                int target=ProvincialBattleTarget(s,theater.Cell,loser);if(target<0){c.Repulses++;break;}
                int source=won?theater.SupplyA:theater.SupplyB;
                // The annual battle does not confer unlimited range on all
                // subsequent towns reached during that same operation.
                int liveSource=source;if(ProvincialFieldPower(s,winner,target,ref liveSource,winningMen)<=0)break;
                long cost=Math.Max(1,(long)Math.Round(TerritorialPoliticalArea(target)*(1+World.Ecology.Habitat[target].ReliefM/1200.0)));
                if(area<cost||!SpendMilitaryMarchAndSiege(s,id,winner,target,source,winningPower/Math.Max(1,losingPower)))break;
                area-=cost;int town=a.TownAt[target];ClaimAggregatePolity(s,target,winner);theater.Cell=target;c.Conquests++;
                var cycle=h.Dynamics.States[winner].Cycle??(h.Dynamics.States[winner].Cycle=new HistoricalPoliticalCycle());
                double debt=town>=0?90:8*inhabitableArea[target]/25000;int whole=(int)debt;
                if(ReturningFleetHistory(s)){if(HistoryRoll("military-conquest-debt-"+target,id,s.Years)<debt-whole)whole++;}
                else whole=town>=0?90:Math.Max(1,(int)Math.Round(debt));
                cycle.ConquestDebt=Math.Min(1000,cycle.ConquestDebt+whole);cycle.LastConquestYear=s.Years;
                if(town>=0)
                {
                    CreditRealmTownCapture(s,winner,town);
                    var captured=s.Society.Settlements[town];captured.Stability=Math.Max(100,captured.Stability-150);captured.FoodReserve=captured.FoodReserve*2/3;
                    c.CapturedSettlements=c.CapturedSettlements.Concat(new[]{town}).Distinct().ToArray();if(won)theater.SupplyA=town;else theater.SupplyB=town;
                    if(winner==o.PartyA)o.CorePressureA=Math.Min(1000,o.CorePressureA+200);else o.CorePressureB=Math.Min(1000,o.CorePressureB+200);
                    HistoryEvent(s,"conquest",h.Polities[winner].Name+" takes "+captured.Name+" after a supplied march and siege during "+c.Name+".",target,h.Polities[winner].CultureId,winner,loser,town,category:ChronicleCategory.Politics);
                }
                if(h.Polities[loser].EndedYear>=0){EndRevisedMilitaryCampaign(s,id,"The defeated government loses its remaining administrative center");return;}
                if(TryRevisedMilitarySettlement(s,id,winner,loser,winningPower,losingPower))return;
            }
            if((pa.Exhaustion>700||pb.Exhaustion>700||s.Years-c.StartedYear>4&&c.Repulses>=4)&&HistoryRoll("military-exhausted-negotiation",id,s.Years)<.25)
                EndRevisedMilitaryCampaign(s,id,"Losses, fatigue and stalled objectives bring a negotiated ceasefire",10);
        }
    }
}
