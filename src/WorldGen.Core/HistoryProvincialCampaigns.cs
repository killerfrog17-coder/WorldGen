using System;
using System.Collections.Generic;
using System.Linq;
namespace WorldGen.Core
{
    [Serializable] public sealed class HistoricalCampaignTheater
    {
        public int Cell=-1,SupplyA=-1,SupplyB=-1;
        public HistoricalCampaignTheater Copy()=>(HistoricalCampaignTheater)MemberwiseClone();
    }
    public sealed partial class Prehistory
    {
        int ProvincialWarFront(PrehistoryState s,int attacker,int defender)
        {
            var a=s.History.Aggregates;
            for(int trial=0;trial<16;trial++)
            {int cell=LocalFrontierTarget(s,a.Polities[attacker].Frontier,attacker,"provincial-war-front-"+defender+"-"+trial,1);if(cell>=0&&HistoricalCells.Polity(s,cell)==defender)return cell;}
            var own=a.Polities[attacker];var hc=s.History.Cultures[s.History.Polities[attacker].CultureId];
            if(own.Cities.Count==0)return -1;
            int offset=(int)(HistoryRoll("provincial-war-port-"+defender,attacker,s.Years)*own.Cities.Count);
            for(int trial=0;trial<Math.Min(12,own.Cities.Count);trial++)
            {
                int from=s.Society.Settlements[own.Cities.At((offset+trial)%own.Cities.Count)].Cell;
                foreach(int peer in a.Cities[own.Cities.At((offset+trial)%own.Cities.Count)].Neighbors)
                {
                    var town=s.Society.Settlements[peer];if(a.Resolve(town.PolityId,s.History)!=defender||a.Living.Cities[peer].AbandonedYear>=0)continue;
                    double km=LivingTravelKm(s,from,town.Cell,s.History.Polities[attacker].CultureId);
                    bool land=World.Landmass[from]==World.Landmass[town.Cell]&&km<250*HistoricalModernEffects.LandTransport(hc,s.Years);
                    bool sea=HistoricalTechnologies.Has(hc,HistoricalTechnology.Navigation)&&km<HistoricalDevelopmentEffects.CoastRangeKm(hc,s.Years)&&AggregateSeaLane(from,town.Cell);
                    if(land||sea)return town.Cell;
                }
            }
            return -1;
        }
        double ProvincialFieldPower(PrehistoryState s,int polity,int front,ref int supply)
        {
            var h=s.History;var a=h.Aggregates;var p=h.Polities[polity];var account=a.Polities[polity];var hc=h.Cultures[p.CultureId];
            if(supply<0||supply>=a.Cities.Length||a.Living.Cities[supply].AbandonedYear>=0||a.Resolve(s.Society.Settlements[supply].PolityId,h)!=polity)supply=-1;
            int best=supply;double distance=best<0?double.MaxValue:LivingTravelKm(s,s.Society.Settlements[best].Cell,front,p.CultureId);
            int offset=account.Cities.Count==0?0:(int)(HistoryRoll("provincial-army-base-"+front,polity,s.Years)*account.Cities.Count);
            for(int trial=0;trial<Math.Min(12,account.Cities.Count);trial++)
            {int city=account.Cities.At((offset+trial)%account.Cities.Count);double d=LivingTravelKm(s,s.Society.Settlements[city].Cell,front,p.CultureId);if(d<distance||d==distance&&(best<0||city<best)){best=city;distance=d;}}
            supply=best;if(best<0)return .01;
            var town=s.Society.Settlements[best];var cityAccount=a.Cities[best];
            double range=HistoricalProvincialEffects.LocalServiceKm(hc,s.Years,h.Dynamics.States[polity].PublicWorks)*2;
            if(HistoricalTechnologies.Has(hc,HistoricalTechnology.Navigation)&&AggregateSeaLane(town.Cell,front))range=Math.Max(range,HistoricalDevelopmentEffects.CoastRangeKm(hc,s.Years));
            double provision=D.Clamp(.25+town.FoodReserve/Math.Max(1.0,town.Population));
            double forces=Math.Min(p.Army,p.Army*.15+(town.Population*.025+cityAccount.Rural*.015)*Math.Sqrt(HistoricalModernEffects.LandTransport(hc,s.Years)));
            var leader=h.Dynamics.Leaders[h.Dynamics.States[polity].LeaderId];
            return Math.Max(.01,forces*provision*Math.Exp(-distance/Math.Max(50,range))*(.4+leader.Military/1000.0)*HistoricalModernEffects.Military(hc,s.Years));
        }
        int ProvincialBattleTarget(PrehistoryState s,int front,int loser)
        {
            var a=s.History.Aggregates;
            if(HistoricalCells.Polity(s,front)==loser)return front;
            var pending=new Queue<int>();var seen=new HashSet<int>();pending.Enqueue(front);seen.Add(front);int best=-1;double score=-1;
            while(pending.Count>0&&seen.Count<40)
            {
                int cell=pending.Dequeue();
                foreach(int next in World.Surface.Cells[cell].Neighbors)
                {
                    a.CellReads++;if(CellPatch[next]<0||seen.Count>=40||!seen.Add(next))continue;int owner=HistoricalCells.Polity(s,next);
                    if(owner==loser)
                    {double value=(a.TownAt[next]>=0?10:0)+ProvincialPhysicalYield(s,s.History.Polities[loser].CultureId,next)+HistoryRoll("provincial-battle-target-"+next,front,s.Years)*.1;if(value>score){best=next;score=value;}}
                    else if(owner==HistoricalCells.Polity(s,front)&&D.Angle(World.Surface.Cells[front].Position,World.Surface.Cells[next].Position)*World.Recipe.RadiusKm<300)pending.Enqueue(next);
                }
            }
            return best;
        }
        void ProvincialBattleCasualties(PrehistoryState s,int city,long deaths)
        {
            if(city<0)return;var a=s.History.Aggregates;var t=s.Society.Settlements[city];var rural=a.Cities[city];
            long fromRural=Math.Min(rural.Rural,deaths);deaths-=fromRural;
            ChangeAggregateCityPopulation(s,city,Math.Max(0,t.Population-deaths),rural.Suburban,rural.Rural-fromRural);
        }
        void AdvanceProvincialCampaign(PrehistoryState s,int id)
        {
            var h=s.History;var a=h.Aggregates;var c=h.Dynamics.Campaigns[id];var theater=a.Provinces.Theaters[id];int elapsed=s.Years-c.LastActionYear;if(elapsed<=0)return;c.LastActionYear=s.Years;
            int attacker=c.AttackerId,defender=attacker==c.A?c.B:c.A;var pa=h.Polities[attacker];var pb=h.Polities[defender];var relation=AggregateRelation(s,attacker,defender);
            if(pa.EndedYear>=0||pb.EndedYear>=0||s.Years-c.StartedYear>6+(int)(HistoryRoll("development-war-duration",id,c.StartedYear)*16)||pa.Exhaustion>800||pb.Exhaustion>800)
            {
                c.EndedYear=s.Years;c.Outcome=pa.EndedYear>=0||pb.EndedYear>=0?"A change or loss of government ends this campaign":"Negotiations end a costly local campaign";
                relation.WarSince=-1;relation.TruceUntil=s.Years+10+(int)(HistoryRoll("development-truce",id,s.Years)*30);relation.Trust=Math.Max(-80,relation.Trust-25);
                HistoryEvent(s,"peace",c.Name+" ends. "+c.Outcome+".",theater.Cell,pa.CultureId,attacker,defender,category:ChronicleCategory.Politics);return;
            }
            ref int baseA=ref theater.SupplyA;ref int baseB=ref theater.SupplyB;
            double powerA=ProvincialFieldPower(s,attacker,theater.Cell,ref baseA),powerB=ProvincialFieldPower(s,defender,theater.Cell,ref baseB);
            int casualtySourceA=baseA,casualtySourceB=baseB;
            bool won=HistoryRoll("provincial-battle",id,s.Years)<powerA/(powerA+powerB);int winner=won?attacker:defender,loser=won?defender:attacker;
            var hc=h.Cultures[h.Polities[winner].CultureId];int actions=Math.Min(8,Math.Max(1,(int)Math.Ceiling(Math.Sqrt(HistoricalModernEffects.LandTransport(hc,s.Years)))));
            if(CommunityHistory(s))actions=CommunityCampaignActions(s,id,winner,won,won?powerA:powerB,elapsed);
            for(int action=0;action<actions;action++)
            {
                int target=ProvincialBattleTarget(s,theater.Cell,loser);if(target<0){c.Repulses++;break;}
                if(CommunityHistory(s)&&!SpendCommunityCampaignArea(s,id,won,target))break;
                int town=a.TownAt[target];ClaimAggregatePolity(s,target,winner);theater.Cell=target;c.Conquests++;
                var cycle=h.Dynamics.States[winner].Cycle??(h.Dynamics.States[winner].Cycle=new HistoricalPoliticalCycle());
                double debt=CommunityHistory(s)?8*inhabitableArea[target]/25000+(town>=0?90:0):town>=0?90:8;
                int whole=(int)debt;if(CommunityHistory(s)&&HistoryRoll("community-conquest-debt-"+target,id,s.Years)<debt-whole)whole++;
                cycle.ConquestDebt=Math.Min(1000,cycle.ConquestDebt+whole);cycle.LastConquestYear=s.Years;
                if(town>=0)
                {
                    var captured=s.Society.Settlements[town];captured.Stability=Math.Max(150,captured.Stability-150);captured.FoodReserve=captured.FoodReserve*2/3;
                    c.CapturedSettlements=c.CapturedSettlements.Concat(new[]{town}).Distinct().ToArray();
                    HistoryEvent(s,"conquest",h.Polities[winner].Name+" takes "+captured.Name+" during "+c.Name+".",target,h.Polities[winner].CultureId,winner,loser,town,category:ChronicleCategory.Politics);
                    if(won)theater.SupplyA=town;else theater.SupplyB=town;
                }
                if(h.Polities[loser].EndedYear>=0)break;
            }
            var la=h.Dynamics.Leaders[h.Dynamics.States[attacker].LeaderId];var lb=h.Dynamics.Leaders[h.Dynamics.States[defender].LeaderId];
            if(won){la.Victories++;lb.Defeats++;la.Renown+=5;}else{lb.Victories++;la.Defeats++;lb.Renown+=5;}
            pa.Exhaustion=Math.Min(1000,pa.Exhaustion+15*elapsed);pb.Exhaustion=Math.Min(1000,pb.Exhaustion+12*elapsed);
            long lossA=Math.Max(1,(long)(Math.Min(pa.Army,powerA)*.02*elapsed)),lossB=Math.Max(1,(long)(Math.Min(pb.Army,powerB)*.02*elapsed));
            c.CasualtiesA+=attacker==c.A?lossA:lossB;c.CasualtiesB+=attacker==c.B?lossA:lossB;
            ProvincialBattleCasualties(s,casualtySourceA,lossA);ProvincialBattleCasualties(s,casualtySourceB,lossB);SealAggregateLeader(s,la.Id);SealAggregateLeader(s,lb.Id);
        }
    }
}
