using System;
using System.Collections.Generic;
using System.Linq;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        readonly Dictionary<(int,int),int> aggregateRelations=new Dictionary<(int,int),int>();
        readonly HashSet<(int,int)> aggregateShipping=new HashSet<(int,int)>();
        string aggregateRelationCheckpoint;
        void PrepareAggregateRelations(PrehistoryState source)
        {if(aggregateRelationCheckpoint==source.Hash)return;aggregateRelations.Clear();aggregateShipping.Clear();foreach(var link in source.History.Modern.Shipping)aggregateShipping.Add((link.PortA,link.PortB));for(int i=0;i<source.History.Relations.Length;i++)aggregateRelations[(source.History.Relations[i].A,source.History.Relations[i].B)]=i;aggregateRelationCheckpoint=source.Hash;}
        HistoricalRelation AggregateRelation(PrehistoryState s,int first,int second)
        {
            int low=Math.Min(first,second),high=Math.Max(first,second);var key=(low,high);var h=s.History;
            if(aggregateRelations.TryGetValue(key,out int at)&&at<h.Relations.Length&&h.Relations[at].A==low&&h.Relations[at].B==high)return h.Relations[at];
            var r=new HistoricalRelation{A=low,B=high};aggregateRelations[key]=h.Relations.Length;h.Relations=h.Relations.Concat(new[]{r}).ToArray();return r;
        }
        void FoundAggregateState(PrehistoryState s,int city,int parent)
        {
            var h=s.History;var a=h.Aggregates;var town=s.Society.Settlements[city];int id=h.Polities.Length;
            var polity=new HistoricalPolity{Id=id,CultureId=town.CultureId,CapitalId=city,ParentId=parent,FoundedYear=s.Years,RulerSince=s.Years,Name=town.Name+" State",ReligionId=HistoricalCells.Religion(s,town.Cell),Treasury=town.Population/2,Government="Local council and agrarian court"};
            h.Polities=h.Polities.Concat(new[]{polity}).ToArray();a.Polities=a.Polities.Concat(new[]{new HistoricalPolityAggregate{Id=id,LastYear=s.Years,LastSuccession=s.Years,ReachKm=100}}).ToArray();
            if(ProvincialHistory(s))Array.Resize(ref a.Provinces.FarmPotential,h.Polities.Length);
            if(CommunityHistory(s))a.Communities.Governments=a.Communities.Governments.Concat(new[]{new HistoricalGovernmentPolicy{LastReformYear=s.Years}}).ToArray();
            AppendImperialGovernment(s);
            h.Dynamics.States=h.Dynamics.States.Concat(new[]{new HistoricalStateDynamics{Id=id,FoundationPotential=(int)(AggregateCropSupport(s,town.CultureId,town.Cell,true)*1000),LastSuccession=s.Years,Tradition="Local provisioning, public works and collective defense",Cycle=new HistoricalPoliticalCycle()}}).ToArray();
            AppointAggregateLeader(s,id);ClaimAggregatePolity(s,town.Cell,id);AssignAggregateTown(s,city,id);ScheduleAggregate(s,StateProcess,id);s.Society.Cultures[town.CultureId].Stage=SocietyStage.Civilization;
            if(a.Cultures[town.CultureId].CivilizationSince<0)a.Cultures[town.CultureId].CivilizationSince=s.Years;
            EstablishCivilizationalLineage(s,town.CultureId,city);
            if(LivingHistory(s)&&a.Living.CivicOrigins[town.CultureId]<0)a.Living.CivicOrigins=a.Living.CivicOrigins.With(town.CultureId,id);
            HistoryEvent(s,parent<0?"state":"secession",polity.Name+(parent<0?" establishes independent institutions at ":" emerges as an independent successor court at ")+town.Name+".",town.Cell,town.CultureId,id,other:parent,town:city,category:ChronicleCategory.Politics);
        }
        void AppointAggregateLeader(PrehistoryState s,int polity)
        {
            var h=s.History;var p=h.Polities[polity];var ds=h.Dynamics.States[polity];int previous=ds.LeaderId,id=h.Dynamics.Leaders.Length;
            if(previous>=0){var former=h.Dynamics.Leaders[previous];former.EndedYear=s.Years;former.Legacy=former.Victories+" victories, "+former.Defeats+" defeats, "+former.Reforms+" public-work reforms.";SealAggregateLeader(s,previous);}
            double Roll(string key)=>HistoryRoll("aggregate-leader-"+key,id,s.Years);
            var leader=new HistoricalLeader{Id=id,PolityId=polity,StartedYear=s.Years,ExpectedEndYear=s.Years+18+(int)(32*Roll("tenure")),PredecessorId=previous,Military=200+(int)(650*Roll("military")),Administration=200+(int)(650*Roll("administration")),Diplomacy=200+(int)(650*Roll("diplomacy")),Ambition=150+(int)(750*Roll("ambition")),Name=HistoricalName("leader-name",unchecked(id*7919+s.Years)),Selection=previous<0?"Acclamation by the founding community":"Succession through the established court and local elites"};
            if(Roll("exceptional")<.015){leader.Military=900;leader.Ambition=950;leader.Administration=Math.Max(650,leader.Administration);}
            bool elected=CommunityHistory(s)&&h.Aggregates.Communities.Governments[polity].RepresentationMilli>=650;
            if(elected){leader.ExpectedEndYear=s.Years+4+(int)(6*Roll("elected-term"));leader.Selection="Selection through representative institutions and local councils";}
            h.Dynamics.Leaders=h.Dynamics.Leaders.Concat(new[]{leader}).ToArray();ds.LeaderId=id;ds.LastSuccession=s.Years;p.Ruler=leader.Name;p.RulerSince=s.Years;s.History.Aggregates.Polities[polity].SuccessionDue=leader.ExpectedEndYear;SealAggregateLeader(s,id);
            if(previous>=0){if(!elected){p.Legitimacy=Math.Max(100,p.Legitimacy-75);p.Cohesion=Math.Max(100,p.Cohesion-(h.Dynamics.Leaders[previous].Renown>=250?150:45));}HistoryEvent(s,elected?"election":"succession",leader.Name+(elected?" takes office under the representative institutions of ":" succeeds to the government of ")+p.Name+".",s.Society.Settlements[p.CapitalId].Cell,p.CultureId,p.Id,category:ChronicleCategory.Politics);}
        }
        void AdvanceAggregateState(PrehistoryState s,int id)
        {
            var h=s.History;var a=h.Aggregates;var p=h.Polities[id];var account=a.Polities[id];int elapsed=s.Years-account.LastYear;if(elapsed<=0)return;account.LastYear=s.Years;
            if(account.Cities.Count==0||account.Urban<25){EndAggregateState(s,id,"Its institutions no longer control an inhabited administrative center.");return;}
            var hc=h.Cultures[p.CultureId];var ds=h.Dynamics.States[id];if(ds.LeaderId<0)AppointAggregateLeader(s,id);var leader=h.Dynamics.Leaders[ds.LeaderId];
            if(CommunityHistory(s))AdvanceCommunityPolicy(s,id,elapsed);
            if(s.Years>=leader.ExpectedEndYear){if(LivingHistory(s)&&TryLivingRegimeChange(s,id))return;AppointAggregateLeader(s,id);leader=h.Dynamics.Leaders[ds.LeaderId];}
            account.ReachKm=(int)(HistoricalLocalEffects.AdministrativeReachKm(account.Urban,leader.Administration,ds.PublicWorks,HistoricalTechnologies.Has(hc,HistoricalTechnology.Writing),HistoricalTechnologies.Has(hc,HistoricalTechnology.Roads))*HistoricalModernEffects.AdministrativeCapacity(hc,s.Years)*HistoricalModernEffects.LandTransport(hc,s.Years));
            if(ConnectedHistory(s))account.ReachKm=(int)(account.ReachKm*(1+.8*HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Writing,s.Years)+1.2*HistoricalModernEffects.Adoption(hc,HistoricalTechnology.Roads,s.Years)));
            if(PassageHistory(s))account.ReachKm=(int)(account.ReachKm*DiffusionPortAdministration(s,id));
            double geography=Math.Sqrt(Math.Max(1,p.AreaKm2)/Math.PI)/Math.Max(50,account.ReachKm);
            double served=D.Clamp(1/(1+geography*geography));long receipts=(long)(account.Urban*.065+(account.Rural+account.Suburban)*.025+account.Remote*.012*served);
            long service=(long)((account.Urban+account.Rural+account.Remote)*.003*(.5+geography*geography)+p.AreaKm2*.0001);
            if(TechnicalHistory(s))service+=a.Empires.Governments[id].AdministrativePostCost;
            account.Receipts=receipts;account.ServiceCost=service;p.Treasury=Math.Max(0,Math.Min(1000000000,p.Treasury+(receipts-service)*elapsed));
            long remoteCapacity=ProvincialHistory(s)?ProvincialRemoteCapacity(s,id):Math.Max(1,(long)(account.RemoteCapacity*HistoricalModernEffects.FoodMultiplier(hc,s.Years)));long remote=AggregateLogisticPopulation(account.Remote,remoteCapacity,CommunityHistory(s)?CommunityRuralGrowth(s,p.CultureId):.004,elapsed);long delta=remote-account.Remote;account.Remote=remote;p.Population=Math.Max(0,p.Population+delta);a.TotalPopulation+=delta;s.Society.Cultures[p.CultureId].Population=Math.Max(0,s.Society.Cultures[p.CultureId].Population+delta);
            var cycle=ds.Cycle??(ds.Cycle=new HistoricalPoliticalCycle());
            if(LivingHistory(s))
            {
                double deficit=D.Clamp((service-receipts)/(double)Math.Max(1,service));
                int targetCapture=(int)Math.Max(0,Math.Min(900,150+deficit*450+p.Exhaustion*.3+(500-leader.Administration)*.35));
                cycle.EliteCapture=Math.Max(0,Math.Min(1000,cycle.EliteCapture+(int)((targetCapture-cycle.EliteCapture)*Math.Min(.3,elapsed/80.0))));
            }
            else cycle.EliteCapture=Math.Max(0,Math.Min(1000,cycle.EliteCapture+(leader.Administration>750?-2:1)*elapsed));
            cycle.ConquestDebt=Math.Max(0,cycle.ConquestDebt-(HistoricalTechnologies.Has(hc,HistoricalTechnology.Writing)?2:1)*elapsed);
            ds.Strain=(int)Math.Min(1000,geography*150+cycle.ConquestDebt*.4+cycle.EliteCapture*.2);p.Exhaustion=Math.Max(0,p.Exhaustion-elapsed*2);p.Legitimacy=Math.Min(900,p.Legitimacy+elapsed*(p.Treasury>0?2:-3));p.Cohesion=Math.Max(50,Math.Min(950,p.Cohesion+(int)((650-ds.Strain*.6-p.Exhaustion*.2-p.Cohesion)*Math.Min(.5,elapsed/50.0))));
            p.Army=(int)Math.Min(int.MaxValue,Math.Max(0,(account.Urban+account.Rural*.1+account.Remote*.02)*.03*p.Cohesion/1000));
            if(p.Treasury>account.Urban&&leader.Administration>550){long spend=Math.Min(p.Treasury/20,Math.Max(1,account.Urban/40));p.Treasury-=spend;ds.PublicWorks=Math.Min(1000,ds.PublicWorks+(int)Math.Min(elapsed*3,spend/100+1));}else ds.PublicWorks=Math.Max(0,ds.PublicWorks-elapsed);
            ds.PeakPopulation=Math.Max(ds.PeakPopulation,p.Population);ds.PeakAreaKm2=Math.Max(ds.PeakAreaKm2,p.AreaKm2);p.LastEstimateYear=s.Years;
            AdvanceImperialGovernment(s,id,elapsed);
            AdvanceTerritorialAdministration(s,id,elapsed);
            AdvanceColonialExpedition(s,id,elapsed);
            AdvanceRecognizedTitles(s,id,elapsed);
            if(h.Administration.Accounts.Length<h.Polities.Length){int old=h.Administration.Accounts.Length;Array.Resize(ref h.Administration.Accounts,h.Polities.Length);for(int k=old;k<h.Polities.Length;k++)h.Administration.Accounts[k]=new HistoricalAdministrativeAccount{PolityId=k};}
            var ledger=h.Administration.Accounts[id];ledger.Year=s.Years;ledger.ReachM=account.ReachKm*1000;ledger.UrbanReceipts=(long)(account.Urban*.065);ledger.RuralReceipts=receipts-ledger.UrbanReceipts;ledger.ServiceCost=service;ledger.TaxableUrbanResidents=account.Urban;ledger.DisconnectedResidents=(long)(account.Remote*(1-served));
            int sampledCity=account.Cities.At((int)(HistoryRoll("aggregate-province",id,s.Years)*account.Cities.Count));var town=s.Society.Settlements[sampledCity];double distance=D.Angle(World.Surface.Cells[town.Cell].Position,World.Surface.Cells[s.Society.Settlements[p.CapitalId].Cell].Position)*World.Recipe.RadiusKm;
            var peers=a.Cities[sampledCity].Neighbors;if(peers.Length>0){int peer=peers[(int)(HistoryRoll("aggregate-state-peer",id,s.Years)*peers.Length)];int peerOwner=a.Resolve(s.Society.Settlements[peer].PolityId,h);if(peerOwner>=0&&peerOwner!=id)ConsiderAggregateDiplomacy(s,id,peerOwner,leader,elapsed);}
            if(LivingHistory(s))AdvanceLivingIncorporation(s,id,sampledCity,elapsed);
            if(!ProvincialHistory(s)&&sampledCity!=p.CapitalId&&town.PolityId==id&&town.Population>=1000)
            {
                double stress=D.Clamp((distance/Math.Max(50,account.ReachKm)-.5)/2)+cycle.ConquestDebt/2000.0+cycle.EliteCapture/2500.0+(town.CultureId==p.CultureId?0:.1);h.Towns[sampledCity].AutonomyPressure=(int)Math.Min(1000,stress*1000);
                if(ConnectedHistory(s)&&town.CultureId!=p.CultureId)
                {
                    var local=h.Cultures[town.CultureId];
                    double localInstitutions=D.Clamp(local.Institution/650.0)*D.Clamp(town.Population/5000.0);
                    stress+=.2+.3*localInstitutions+.15*(1-p.Legitimacy/1000.0);
                    h.Towns[sampledCity].AutonomyPressure=(int)Math.Min(1000,stress*1000);
                }
                double risk=LivingHistory(s)?Math.Max(0,stress-.55)*.006*(.25+.75*D.Clamp((650-p.Cohesion)/400.0)):stress*.008;
                if(HistoryRoll("aggregate-secession",id,s.Years)<Math.Min(.4,risk*elapsed)){FoundAggregateState(s,sampledCity,id);if(ConnectedHistory(s))ClaimDevelopmentProvince(s,sampledCity,id);return;}
            }
            if((p.Cohesion<250||cycle.EliteCapture>800)&&HistoryRoll("aggregate-court-crisis",id,s.Years)<.005*elapsed){EndAggregateState(s,id,"Fiscal strain and rival local elites dissolve the central court; local communities and their histories survive.");return;}
            int target=LocalFrontierTarget(s,account.Frontier,id,"aggregate-state-frontier",1);
            if(ConnectedHistory(s))AdvanceDevelopmentFrontier(s,id,elapsed);
            if(target>=0&&!ConnectedHistory(s))
            {
                int other=HistoricalCells.Polity(s,target);double km=D.Angle(World.Surface.Cells[target].Position,World.Surface.Cells[s.Society.Settlements[p.CapitalId].Cell].Position)*World.Recipe.RadiusKm;
                if(other<0&&HistoricalCells.Culture(s,target)>=0&&km<account.ReachKm*(1+.3*Math.Log(1+account.CityCount))&&p.Treasury>100&&p.Cohesion>500&&HistoryRoll("aggregate-incorporation",id,s.Years)<.15*elapsed/5){ClaimAggregatePolity(s,target,id);p.Treasury-=100;}
                else if(other>=0&&other!=id)
                {
                    ConsiderAggregateDiplomacy(s,id,other,leader,elapsed);
                }
            }
            SealAggregateLeader(s,ds.LeaderId);
        }
        void ConsiderAggregateDiplomacy(PrehistoryState s,int id,int other,HistoricalLeader leader,int elapsed)
        {
            if(ConnectedHistory(s)){ConsiderDevelopmentDiplomacy(s,id,other,leader,elapsed);return;}
            var p=s.History.Polities[id];var relation=AggregateRelation(s,id,other);relation.Trust=Math.Max(-100,Math.Min(100,relation.Trust+(int)(HistoryRoll("aggregate-trust-"+other,id,s.Years)*11)-5));
            if(relation.WarSince<0&&relation.TruceUntil<s.Years&&p.Army>20&&p.Cohesion>400&&HistoryRoll("aggregate-war-"+other,id,s.Years)<.018*elapsed*(.2+leader.Ambition/1000.0))BeginAggregateCampaign(s,id,other,relation);
            else if(relation.WarSince<0){relation.Trade=relation.Trust>0;relation.Alliance=relation.Trust>75;}
        }
        void AdvanceAggregateRemote(PrehistoryState s,int id)
        {
            var h=s.History;var a=h.Aggregates;var cohort=a.Polities[id];int elapsed=s.Years-cohort.LastYear;if(elapsed<=0)return;cohort.LastYear=s.Years;
            var people=h.Cultures[h.Polities[id].CultureId];long capacity=ProvincialHistory(s)?ProvincialRemoteCapacity(s,id):(long)(cohort.RemoteCapacity*HistoricalModernEffects.FoodMultiplier(people,s.Years));long next=AggregateLogisticPopulation(cohort.Remote,capacity,CommunityHistory(s)?CommunityRuralGrowth(s,people.Id):.004,elapsed),delta=next-cohort.Remote;cohort.Remote=next;a.TotalPopulation+=delta;s.Society.Cultures[people.Id].Population=Math.Max(0,s.Society.Cultures[people.Id].Population+delta);
        }
        void BeginAggregateCampaign(PrehistoryState s,int attacker,int defender,HistoricalRelation relation,int front=-1)
        {
            var h=s.History;int id=h.Dynamics.Campaigns.Length;var leader=h.Dynamics.Leaders[h.Dynamics.States[attacker].LeaderId];
            var campaign=new HistoricalCampaign{Id=id,A=relation.A,B=relation.B,AttackerId=attacker,LeaderId=leader.Id,StartedYear=s.Years,LastActionYear=s.Years,Aim=HistoricalWarAim.Conquest,Name=h.Polities[attacker].Name+"–"+h.Polities[defender].Name+" War",Objective="Secure the contested frontier and its administrative centers"};
            h.Dynamics.Campaigns=h.Dynamics.Campaigns.Concat(new[]{campaign}).ToArray();relation.WarSince=s.Years;relation.Trade=relation.Alliance=false;ScheduleAggregate(s,CampaignProcess,id);
            if(ProvincialHistory(s))h.Aggregates.Provinces.Theaters=h.Aggregates.Provinces.Theaters.Concat(new[]{new HistoricalCampaignTheater{Cell=front}}).ToArray();
            if(CommunityHistory(s))h.Aggregates.Communities.Campaigns=h.Aggregates.Communities.Campaigns.Concat(new[]{new HistoricalCampaignAdvance()}).ToArray();
            HistoryEvent(s,"war",campaign.Name+" begins.",s.Society.Settlements[h.Polities[attacker].CapitalId].Cell,h.Polities[attacker].CultureId,attacker,defender,category:ChronicleCategory.Politics);
        }
        void AdvanceAggregateCampaign(PrehistoryState s,int id)
        {
            if(ConnectedHistory(s)){AdvanceDevelopmentCampaign(s,id);return;}
            var h=s.History;var a=h.Aggregates;var c=h.Dynamics.Campaigns[id];int elapsed=s.Years-c.LastActionYear;if(elapsed<=0)return;c.LastActionYear=s.Years;int attacker=c.AttackerId,defender=attacker==c.A?c.B:c.A;var pa=h.Polities[attacker];var pb=h.Polities[defender];var relation=AggregateRelation(s,attacker,defender);
            if(pa.EndedYear>=0||pb.EndedYear>=0||s.Years-c.StartedYear>8+(int)(HistoryRoll("aggregate-war-duration",id,c.StartedYear)*18)||pa.Exhaustion>750||pb.Exhaustion>750)
            {c.EndedYear=s.Years;c.Outcome=pa.EndedYear>=0||pb.EndedYear>=0?"One court ceased to exist":"Negotiated peace after exhaustion and frontier conflict";relation.WarSince=-1;relation.TruceUntil=s.Years+8+(int)(HistoryRoll("aggregate-truce",id,s.Years)*20);HistoryEvent(s,"peace",c.Name+" ends. "+c.Outcome+".",s.Society.Settlements[pa.CapitalId].Cell,pa.CultureId,pa.Id,pb.Id,category:ChronicleCategory.Politics);return;}
            pa.Exhaustion=Math.Min(1000,pa.Exhaustion+12*elapsed);pb.Exhaustion=Math.Min(1000,pb.Exhaustion+10*elapsed);
            var la=h.Dynamics.Leaders[h.Dynamics.States[attacker].LeaderId];var lb=h.Dynamics.Leaders[h.Dynamics.States[defender].LeaderId];double powerA=Math.Max(1,pa.Army)*(.4+la.Military/1000.0)*HistoricalModernEffects.Military(h.Cultures[pa.CultureId],s.Years),powerB=Math.Max(1,pb.Army)*(.5+lb.Military/1000.0)*HistoricalModernEffects.Military(h.Cultures[pb.CultureId],s.Years);
            bool won=HistoryRoll("aggregate-battle",id,s.Years)<powerA/(powerA+powerB);int winner=won?attacker:defender,loser=won?defender:attacker;int target=-1;
            for(int trial=0;trial<4;trial++){int next=LocalFrontierTarget(s,a.Polities[winner].Frontier,winner,"aggregate-campaign-target-"+trial+"-"+id,1);if(next>=0&&HistoricalCells.Polity(s,next)==loser){target=next;break;}}
            if(target<0&&a.Polities[winner].Cities.Count>0)
            {
                int origin=a.Polities[winner].Cities.At((int)(HistoryRoll("aggregate-expedition-origin",id,s.Years)*a.Polities[winner].Cities.Count));
                foreach(int peer in a.Cities[origin].Neighbors)
                {
                    var destination=s.Society.Settlements[peer];if(a.Resolve(destination.PolityId,h)!=loser)continue;int from=s.Society.Settlements[origin].Cell;double km=D.Angle(World.Surface.Cells[from].Position,World.Surface.Cells[destination.Cell].Position)*World.Recipe.RadiusKm;
                    if(km<=a.Polities[winner].ReachKm*3||HistoricalTechnologies.Has(h.Cultures[h.Polities[winner].CultureId],HistoricalTechnology.Navigation)&&km*1000<=HistoricalModernVoyageEffects.RangeM(h.Cultures[h.Polities[winner].CultureId],s.Years)&&AggregateSeaLane(from,destination.Cell)){target=destination.Cell;break;}
                }
            }
            if(target>=0)
            {
                int town=a.TownAt[target];ClaimAggregatePolity(s,target,winner);c.Conquests++;var ds=h.Dynamics.States[winner];ds.Cycle=ds.Cycle??new HistoricalPoliticalCycle();ds.Cycle.ConquestDebt=Math.Min(1000,ds.Cycle.ConquestDebt+35);ds.Cycle.LastConquestYear=s.Years;
                if(won)la.Victories++;else lb.Victories++;if(won)lb.Defeats++;else la.Defeats++;(won?la:lb).Renown+=town>=0?25:3;
                if(town>=0){c.CapturedSettlements=c.CapturedSettlements.Concat(new[]{town}).Distinct().ToArray();HistoryEvent(s,"conquest",h.Polities[winner].Name+" takes "+s.Society.Settlements[town].Name+" during "+c.Name+".",target,h.Polities[winner].CultureId,winner,loser,town,category:ChronicleCategory.Politics);}
            }
            else c.Repulses++;
            long lossA=Math.Min(pa.Army/5,20+(long)(pa.Army*.02*HistoryRoll("aggregate-casualties-a",id,s.Years))),lossB=Math.Min(pb.Army/5,20+(long)(pb.Army*.02*HistoryRoll("aggregate-casualties-b",id,s.Years)));c.CasualtiesA+=attacker==c.A?lossA:lossB;c.CasualtiesB+=attacker==c.B?lossA:lossB;
            CasualtiesAggregate(s,attacker,lossA);CasualtiesAggregate(s,defender,lossB);SealAggregateLeader(s,la.Id);SealAggregateLeader(s,lb.Id);
        }
        void CasualtiesAggregate(PrehistoryState s,int state,long casualties)
        {var account=s.History.Aggregates.Polities[state];if(account.Cities.Count==0)return;int id=account.Cities.At((int)(HistoryRoll("aggregate-recruits",state,s.Years)*account.Cities.Count));var t=s.Society.Settlements[id];var c=s.History.Aggregates.Cities[id];long rural=Math.Min(c.Rural,casualties);casualties-=rural;ChangeAggregateCityPopulation(s,id,Math.Max(0,t.Population-casualties),c.Suburban,c.Rural-rural);}
    }
}
