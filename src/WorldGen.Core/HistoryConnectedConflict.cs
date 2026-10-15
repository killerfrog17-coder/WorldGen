using System;
using System.Linq;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        void ConsiderDevelopmentDiplomacy(PrehistoryState s,int id,int other,HistoricalLeader leader,int elapsed)
        {
            var h=s.History;var p=h.Polities[id];var peer=h.Polities[other];if(peer.EndedYear>=0)return;
            var relation=AggregateRelation(s,id,other);if(relation.WarSince>=0)return;
            var hc=h.Cultures[p.CultureId];var pc=h.Cultures[peer.CultureId];
            double shared=p.CultureId==peer.CultureId?1:p.ReligionId==peer.ReligionId?.4:0;
            double exchange=relation.Trade?2:0;
            relation.Trust=Math.Max(-100,Math.Min(100,relation.Trust+(int)(HistoryRoll("development-trust-"+other,id,s.Years)*9)-4+(int)shared+(int)exchange));
            double power=Math.Max(1,p.Army)*HistoricalModernEffects.Military(hc,s.Years)/Math.Max(1,peer.Army*HistoricalModernEffects.Military(pc,s.Years));
            double opportunity=D.Clamp((power-.9)/2.0),rivalry=D.Clamp(-relation.Trust/100.0);
            double ambition=leader.Ambition/1000.0,provisioned=D.Clamp(p.Treasury/Math.Max(100.0,p.Army*4.0));
            double risk=.004*(.3+ambition)*(.1+1.8*opportunity+.5*rivalry)*(1-.65*shared)*provisioned;
            // Superior transport/weapons permit larger campaigns, not a forced
            // global conquest clock. Costly peer wars and negotiated trade remain.
            if(relation.TruceUntil<s.Years&&p.Army>30&&p.Cohesion>450&&HistoryRoll("development-war-"+other,id,s.Years)<1-Math.Exp(-risk*elapsed))
            {int front=ProvincialHistory(s)?ProvincialWarFront(s,id,other):-1;if(ProvincialHistory(s)&&front<0)return;p.Treasury=Math.Max(0,p.Treasury-Math.Max(50,p.Army/2));BeginAggregateCampaign(s,id,other,relation,front);}
            else{relation.Trade=relation.Trust>-25;relation.Alliance=relation.Trust>65&&power<2.5&&power>.4;}
        }
        void ClaimDevelopmentProvince(PrehistoryState s,int city,int parent)
        {
            var h=s.History;var a=h.Aggregates;var town=s.Society.Settlements[city];int owner=town.PolityId;
            if(owner<0||owner==parent)return;var hc=h.Cultures[town.CultureId];
            double reach=80+60*HistoricalModernEffects.LandTransport(hc,s.Years);
            var pending=new System.Collections.Generic.Queue<int>();var seen=new System.Collections.Generic.HashSet<int>();pending.Enqueue(town.Cell);seen.Add(town.Cell);
            int visited=0;
            while(pending.Count>0&&visited++<64)
            {
                int cell=pending.Dequeue();
                foreach(int next in World.Surface.Cells[cell].Neighbors)
                {
                    if(CellPatch[next]<0||seen.Count>=128||!seen.Add(next)||HistoricalCells.Polity(s,next)!=parent)continue;
                    if(LivingTravelKm(s,town.Cell,next,town.CultureId)>reach)continue;
                    ClaimAggregatePolity(s,next,owner);pending.Enqueue(next);
                }
            }
            var relation=AggregateRelation(s,owner,parent);relation.TruceUntil=s.Years+20;relation.Trust=-35;
            HistoryEvent(s,"provincial-independence",h.Polities[owner].Name+" retains neighboring communities around "+town.Name+" as local institutions separate from the former court.",town.Cell,town.CultureId,owner,parent,town:city,category:ChronicleCategory.Politics);
        }
        void AdvanceDevelopmentCampaign(PrehistoryState s,int id)
        {
            if(ProvincialHistory(s)){AdvanceProvincialCampaign(s,id);return;}
            var h=s.History;var a=h.Aggregates;var campaign=h.Dynamics.Campaigns[id];int elapsed=s.Years-campaign.LastActionYear;if(elapsed<=0)return;campaign.LastActionYear=s.Years;
            int attacker=campaign.AttackerId,defender=attacker==campaign.A?campaign.B:campaign.A;
            var pa=h.Polities[attacker];var pb=h.Polities[defender];var relation=AggregateRelation(s,attacker,defender);
            if(pa.EndedYear>=0||pb.EndedYear>=0||s.Years-campaign.StartedYear>6+(int)(HistoryRoll("development-war-duration",id,campaign.StartedYear)*16)||pa.Exhaustion>800||pb.Exhaustion>800)
            {
                campaign.EndedYear=s.Years;campaign.Outcome=pa.EndedYear>=0||pb.EndedYear>=0?"The defeated government lost its administrative center":"A negotiated settlement ends the campaign";
                relation.WarSince=-1;relation.TruceUntil=s.Years+10+(int)(HistoryRoll("development-truce",id,s.Years)*30);relation.Trust=Math.Max(-80,relation.Trust-25);
                HistoryEvent(s,"peace",campaign.Name+" ends. "+campaign.Outcome+".",s.Society.Settlements[pa.CapitalId].Cell,pa.CultureId,attacker,defender,category:ChronicleCategory.Politics);return;
            }
            var la=h.Dynamics.Leaders[h.Dynamics.States[attacker].LeaderId];var lb=h.Dynamics.Leaders[h.Dynamics.States[defender].LeaderId];
            double powerA=Math.Max(1,pa.Army)*(.4+la.Military/1000.0)*HistoricalModernEffects.Military(h.Cultures[pa.CultureId],s.Years);
            double powerB=Math.Max(1,pb.Army)*(.5+lb.Military/1000.0)*HistoricalModernEffects.Military(h.Cultures[pb.CultureId],s.Years);
            bool won=HistoryRoll("development-battle",id,s.Years)<powerA/(powerA+powerB);int winner=won?attacker:defender,loser=won?defender:attacker;
            var hc=h.Cultures[h.Polities[winner].CultureId];
            int actions=Math.Min(12,Math.Max(1,(int)Math.Ceiling(HistoricalModernEffects.LandTransport(hc,s.Years)*Math.Sqrt(Math.Max(1,h.Polities[winner].Army)/500.0))));
            for(int action=0;action<actions;action++)
            {
                int target=-1;
                for(int trial=0;trial<4;trial++){int next=LocalFrontierTarget(s,a.Polities[winner].Frontier,winner,"development-campaign-"+id+"-"+action+"-"+trial,1);if(next>=0&&HistoricalCells.Polity(s,next)==loser){target=next;break;}}
                if(target<0&&a.Polities[winner].Cities.Count>0)
                {
                    int town=a.Polities[winner].Cities.At((int)(HistoryRoll("development-army-origin-"+action,id,s.Years)*a.Polities[winner].Cities.Count));
                    foreach(int peer in a.Cities[town].Neighbors)
                    {
                        var destination=s.Society.Settlements[peer];if(a.Resolve(destination.PolityId,h)!=loser||a.Living.Cities[peer].AbandonedYear>=0)continue;
                        int from=s.Society.Settlements[town].Cell;double km=D.Angle(World.Surface.Cells[from].Position,World.Surface.Cells[destination.Cell].Position)*World.Recipe.RadiusKm;
                        bool land=World.Landmass[from]==World.Landmass[destination.Cell]&&km<a.Polities[winner].ReachKm*1.5;
                        bool sea=HistoricalTechnologies.Has(hc,HistoricalTechnology.Navigation)&&km<HistoricalDevelopmentEffects.CoastRangeKm(hc,s.Years)&&AggregateSeaLane(from,destination.Cell);
                        if(land||sea){target=destination.Cell;break;}
                    }
                }
                if(target<0){campaign.Repulses++;break;}
                int captured=a.TownAt[target];ClaimAggregatePolity(s,target,winner);campaign.Conquests++;
                var ds=h.Dynamics.States[winner];var cycle=ds.Cycle??(ds.Cycle=new HistoricalPoliticalCycle());cycle.ConquestDebt=Math.Min(1000,cycle.ConquestDebt+(captured>=0?90:8));cycle.LastConquestYear=s.Years;
                if(captured>=0)
                {
                    campaign.CapturedSettlements=campaign.CapturedSettlements.Concat(new[]{captured}).Distinct().ToArray();
                    HistoryEvent(s,"conquest",h.Polities[winner].Name+" takes "+s.Society.Settlements[captured].Name+" during "+campaign.Name+".",target,h.Polities[winner].CultureId,winner,loser,captured,category:ChronicleCategory.Politics);
                }
                if(h.Polities[loser].EndedYear>=0)break;
            }
            if(won){la.Victories++;lb.Defeats++;la.Renown+=5;}else{lb.Victories++;la.Defeats++;lb.Renown+=5;}
            pa.Exhaustion=Math.Min(1000,pa.Exhaustion+15*elapsed);pb.Exhaustion=Math.Min(1000,pb.Exhaustion+12*elapsed);
            long lossA=Math.Max(1,(long)(pa.Army*.015*elapsed)),lossB=Math.Max(1,(long)(pb.Army*.015*elapsed));
            campaign.CasualtiesA+=attacker==campaign.A?lossA:lossB;campaign.CasualtiesB+=attacker==campaign.B?lossA:lossB;
            CasualtiesAggregate(s,attacker,lossA);CasualtiesAggregate(s,defender,lossB);SealAggregateLeader(s,la.Id);SealAggregateLeader(s,lb.Id);
        }
    }
}
