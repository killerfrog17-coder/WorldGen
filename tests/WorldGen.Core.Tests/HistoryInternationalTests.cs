using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;
public static class HistoryInternationalTests
{
    static object Call(Prehistory e,string name,params object[] values)=>typeof(Prehistory).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(e,values);
    public static void Run(Action<bool,string> check)
    {
        check(!HistoricalInternationalEffects.TradeCapacity(.1,1,1000)&&HistoricalInternationalEffects.TradeCapacity(.5,.5,500),"A trade charter needs practiced accounting and exchange, not a global era label");
        check(!HistoricalInternationalEffects.PeaceCapacity(.1,1000)&&HistoricalInternationalEffects.PeaceCapacity(.5,500),"A broad peace forum requires deployed communication and institutions");
        double peace=HistoricalInternationalEffects.Acceptance(90,800,200,500,true),hostile=HistoricalInternationalEffects.Acceptance(-70,200,900,0,true);
        check(peace>hostile&&peace<1&&hostile>=0,"Negotiation can be rejected, with trust and leadership affecting acceptance");
        check(HistoricalInternationalEffects.AgreementAcceptance(.2,.9,true,false)==.9&&HistoricalInternationalEffects.AgreementAcceptance(.2,.9,false,true)==.2,"Accession evaluates the actual invited government's willingness, not the existing member's leadership");
        check(HistoricalInternationalEffects.AgreementAcceptance(.2,.9,false,false)==.2&&HistoricalInternationalEffects.AgreementAcceptance(.2,.9,true,true)==0,"Founding respects both governments' willingness and existing joint membership is not negotiated twice");
        check(HistoricalInternationalEffects.Dues(10000,10,1000,0)==50&&HistoricalInternationalEffects.Dues(10000,10,0,0)==0,"Treaty contributions are capped by actual treasury rather than minted dues");
        check(HistoricalInternationalEffects.Dues(10000,10,100000,1000000000000L)==0,"A full common reserve does not delete excess contributions");
        check(HistoricalInternationalEffects.Dues(long.MaxValue,int.MaxValue,long.MaxValue,0)==1000000000000L,"Extreme manually supplied economic values do not overflow treaty contributions");
        check(HistoricalInternationalEffects.TradeEfficiency(1000,10000,5)>1&&HistoricalInternationalEffects.TradeEfficiency(1000,0,5)==1,"A funded trade body reduces bounded friction, while an empty one grants no benefit");
        check(HistoricalInternationalEffects.TradeEfficiency(1000,10000,5)<=1.12&&HistoricalInternationalEffects.WarRestraint(1000,10000,5)>=.65,"Treaty effects cannot create free output or magically forbid war");
        check(HistoricalInternationalEffects.MediationChance(-100,0,0,0,0)>0&&HistoricalInternationalEffects.MediationChance(100,1000,1000,1000,1000)<1,"Even favorable mediation is uncertain and an unfavorable proposal is not categorically impossible");
        check(HistoricalInternationalEffects.CoalitionChance(5,90,0,true)>.5&&HistoricalInternationalEffects.CoalitionChance(5,90,0,true)>HistoricalInternationalEffects.CoalitionChance(5,90,0,false)*2,"Committed defenders can respond within years rather than waiting decades; joining an ally's offensive war is less automatic");
        check(HistoricalInternationalEffects.CoalitionChance(5,90,650,true)<.1&&HistoricalInternationalEffects.CoalitionChance(0,90,0,true)==0,"Exhaustion and elapsed opportunity limit alliance entry even before actual treasury and supply gates");
        var system=new HistoricalInternationalSystem{Accounts=new[]{new HistoricalDiplomaticAccount{Id=0}},Organizations=new[]{new HistoricalInternationalOrganization{Id=0}},RelationCampaigns=new[]{-1}};
        system.Accounts[0].Relations.Add(12);system.Organizations[0].Members.Add(0);var copy=system.Copy();copy.Accounts[0].Relations.Add(14);copy.Organizations[0].Members.Remove(0);copy.RelationCampaigns[0]=4;
        check(system.Accounts[0].Relations.Count==1&&system.Organizations[0].Members.Count==1&&system.RelationCampaigns[0]==-1,"International copy isolates mutable rosters, indexes and scalar records while sharing immutable trees");
    }
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var source=world.Prehistory;string original=source.ComputeHash();var engine=new Prehistory(world);var s=engine.UpgradeToLivingWorldHistory(source);var h=s.History;var system=h.International!;
        check(source.History.International==null&&source.ComputeHash()==original&&system.Accounts.Length==h.Polities.Length,"Explicit32 upgrade initializes aligned international actors without changing old-null save hashes");
        var active=h.Polities.Where(p=>p.EndedYear<0&&h.Aggregates.Polities[p.Id].Cities.Count>0).Take(2).ToArray();
        var queue=new SimulationEventQueue(s.Timeline.Events);var queueField=typeof(Prehistory).GetField("aggregateQueue",BindingFlags.NonPublic|BindingFlags.Instance)!;queueField.SetValue(engine,queue);
        if(active.Length<2)
        {
            int town=Array.FindIndex(s.Society.Settlements,t=>t.Population>0&&h.Aggregates.Resolve(t.PolityId,h)<0);
            if(town<0)throw new InvalidOperationException("International fixture needs two states or an existing unruled town.");
            Call(engine,"FoundAggregateState",s,town,-1);active=h.Polities.Where(p=>p.EndedYear<0&&h.Aggregates.Polities[p.Id].Cities.Count>0).Take(2).ToArray();
        }
        int a=active[0].Id,b=active[1].Id;var relation=(HistoricalRelation)Call(engine,"AggregateRelation",s,a,b)!;int ri=Array.IndexOf(h.Relations,relation);
        check(system.Accounts[a].Relations.Positions[ri]>=0&&system.Accounts[b].Relations.Positions[ri]>=0,"A real created contact is indexed for both actors without a world search");
        relation.Trade=true;relation.Trust=80;
        foreach(int state in new[]{a,b})
        {
            h.Polities[state].Treasury=1000000;h.Aggregates.Polities[state].Receipts=10000;var c=h.Cultures[h.Polities[state].CultureId];c.Institution=Math.Max(650,c.Institution);
            // A deliberate capable fixture, not a claim about the ordinary run.
            foreach(var technology in new[]{HistoricalTechnology.Writing,HistoricalTechnology.Coinage})
            {int k=(int)technology;c.Technology[k]=Math.Max(1,Math.Min(c.Technology[k]==0?s.Years-100:c.Technology[k],s.Years-100));c.Technical.Levels[k]=Math.Max(1,c.Technical.Levels[k]);c.Technical.LevelYear[k]=Math.Min(c.Technical.LevelYear[k],s.Years-100);c.Technical.Deployment[k]=Math.Max(500,c.Technical.Deployment[k]);c.Technical.InstalledYear[k]=Math.Max(1,c.Technical.LevelYear[k]+1);}
        }
        long treasury=h.Polities[a].Treasury+h.Polities[b].Treasury;
        Call(engine,"ConsiderInternationalOrganization",s,a,b,relation,100000,HistoricalOrganizationKind.TradeLeague);
        check(system.Organizations.Length>0&&system.Accounts[a].TradeOrganization==system.Accounts[b].TradeOrganization,"Actual known trade partners can establish a funded league through the ordinary eligibility and acceptance path");
        var body=system.Organizations[system.Accounts[a].TradeOrganization];
        check(h.Polities[a].Treasury+h.Polities[b].Treasury+body.Reserve==treasury&&body.Contributions==body.Reserve,"Founding transfers actual money into the common fund without creating wealth");
        system.Accounts[a].LastTradeDuesYear=s.Years-10;long total=h.Polities[a].Treasury+body.Reserve;Call(engine,"PayInternationalDues",s,a,body.Id);long paid=body.Contributions;
        check(h.Polities[a].Treasury+body.Reserve==total&&system.Accounts[a].LastTradeDuesYear==s.Years,"Dues are actual transfers with a saved per-member payment date");
        Call(engine,"PayInternationalDues",s,a,body.Id);check(body.Contributions==paid,"Repeated state work at the same date cannot collect a second set of dues");
        body.LastActionYear=s.Years-10;system.Accounts[b].LastTradeDuesYear=s.Years-10;Call(engine,"PayInternationalDues",s,b,body.Id);long service=body.ServiceSpending;
        system.Accounts[a].LastTradeDuesYear=s.Years-1;Call(engine,"PayInternationalDues",s,a,body.Id);
        check(body.ServiceSpending==service&&body.Reserve+body.ServiceSpending+body.MediationSpending==body.Contributions,"Multiple member updates cannot duplicate the organization's elapsed service bill");
        int peaceId=system.Organizations.Length;system.Organizations=system.Organizations.Concat(new[]{new HistoricalInternationalOrganization{Id=peaceId,FounderPolity=a,FoundedYear=s.Years,LastActionYear=s.Years,Kind=HistoricalOrganizationKind.PeaceCongress,Name="Diagnostic Congress",Reserve=500,Contributions=500}}).ToArray();
        Call(engine,"JoinInternationalOrganization",s,a,peaceId);Call(engine,"JoinInternationalOrganization",s,b,peaceId);
        int campaign=h.Dynamics.Campaigns.Length;Call(engine,"BeginAggregateCampaign",s,a,b,relation,s.Society.Settlements[h.Polities[b].CapitalId].Cell);
        check(h.Dynamics.Campaigns.Length==campaign+1&&system.RelationCampaigns[ri]==campaign,"Known war entry records the exact campaign for bounded mediation and allied calls");
        var congress=system.Organizations[peaceId];long reserve=congress.Reserve;Call(engine,"TryInternationalMediation",s,a,b,ri,5);int attempts=congress.Mediations+congress.FailedMediations;
        Call(engine,"TryInternationalMediation",s,b,a,ri,5);
        check(attempts==1&&congress.Mediations+congress.FailedMediations==1&&congress.Reserve==reserve-50,"A paid mediation attempt can occur after dues processing, but not once per member in the same year");
        var succession=s.Copy();var successor=succession.History.Polities[a].Copy();int next=succession.History.Polities.Length;successor.Id=next;succession.History.Polities=succession.History.Polities.Concat(new[]{successor}).ToArray();
        Call(engine,"AppendInternationalState",succession,a,true);
        check(succession.History.International!.Accounts[next].Relations.Count==system.Accounts[a].Relations.Count&&succession.History.International.Organizations[body.Id].Members.Positions[next]>=0&&succession.History.International.Accounts[next].LastTradeDuesYear==system.Accounts[a].LastTradeDuesYear,"A dynastic successor retains actual contacts, membership and payment dates without a census or duplicate dues");
        succession.History.Polities[a].EndedYear=s.Years;succession.History.Aggregates.Successors=succession.History.Aggregates.Successors.With(a,next);var successorEngine=new Prehistory(world);Call(successorEngine,"PrepareAggregateRelations",succession);
        check((int)Call(successorEngine,"InternationalPeer",succession,next,ri)==b,"Inherited historical relation endpoints resolve to the living dynasty rather than erasing the diplomatic network");
        int normalized=(int)Call(successorEngine,"CurrentInternationalRelation",succession,next,b,ri);
        check(succession.History.Relations[normalized].A==Math.Min(next,b)&&succession.History.Relations[normalized].Trust==relation.Trust&&succession.History.International.Accounts[next].Relations.Positions[ri]<0&&succession.History.International.Accounts[next].Relations.Positions[normalized]>=0,"A successor retains continuity but retires the old index from its active portfolio, preserving the original parties in the historical record");
        var stale=s.Copy();var staleEngine=new Prehistory(world);stale.History.International!.Accounts[a].TradeOrganization=stale.History.International.Accounts[a].PeaceOrganization=-1;Call(staleEngine,"PrepareAggregateRelations",stale);
        for(int k=0;k<80;k++)
        {int dead=stale.History.Polities.Length;stale.History.Polities=stale.History.Polities.Concat(new[]{new HistoricalPolity{Id=dead,EndedYear=stale.Years}}).ToArray();Call(staleEngine,"AppendInternationalState",stale,-1,false);Call(staleEngine,"AggregateRelation",stale,a,dead);}
        int portfolio=stale.History.International.Accounts[a].Relations.Count;Call(staleEngine,"AdvanceInternational",stale,a,5);int pruned=portfolio-stale.History.International.Accounts[a].Relations.Count;
        check(pruned>=1&&pruned<=8&&stale.History.International.Accounts[a].Relations.Positions[ri]>=0,"A stale-heavy diplomatic portfolio cleans a bounded pre-captured sample without erasing its living contact or invalidating bag indexes");
        s.Timeline.Events=queue.Ordered();queueField.SetValue(engine,null);s.Seal();engine.Validate(s);string before=s.ComputeHash();engine.InternationalProfile(s,a);engine.InternationalProfile(s,b);
        check(s.ComputeHash()==before,"Reading international narrative profiles never changes simulation state or diagnostics");
        world.Prehistory=s;var loaded=WorldFile.Read(WorldFile.Write(world));
        check(loaded.Prehistory.Hash==s.Hash&&loaded.Prehistory.History.International!.Organizations[body.Id].Contributions==body.Contributions,"International budgets, membership and campaign indexes roundtrip with canonical integrity");
        var one=engine.AdvanceYears(s,15);var two=engine.AdvanceYears(engine.AdvanceYears(s,6),9);check(one.Hash==two.Hash,"Organizations and diplomacy preserve arbitrary playback partition determinism");
        var continuity=s.Copy();var continuityEngine=new Prehistory(world);Call(continuityEngine,"PrepareAggregateRelations",continuity);
        int ruler=Array.FindIndex(continuity.History.Polities,p=>p.EndedYear<0&&continuity.History.Aggregates.Polities[p.Id].Cities.Count>0&&(double)Call(continuityEngine,"HistoryRoll","living-disputed-succession",p.Id,continuity.Years)<.65);
        if(ruler<0)throw new InvalidOperationException("Continuity fixture needs a live polity with a suitable deterministic contested-succession roll.");
        int opponent=continuity.History.Polities.First(p=>p.EndedYear<0&&p.Id!=ruler&&continuity.History.Aggregates.Polities[p.Id].Cities.Count>0).Id;
        var ch=continuity.History;var cq=new SimulationEventQueue(continuity.Timeline.Events);queueField.SetValue(continuityEngine,cq);var cr=(HistoricalRelation)Call(continuityEngine,"AggregateRelation",continuity,ruler,opponent)!;
        int warIndex=ch.International!.RelationCampaigns[Array.IndexOf(ch.Relations,cr)];
        if(warIndex<0||ch.Dynamics.Campaigns[warIndex].EndedYear>=0)
        {warIndex=ch.Dynamics.Campaigns.Length;Call(continuityEngine,"BeginAggregateCampaign",continuity,ruler,opponent,cr,continuity.Society.Settlements[ch.Polities[opponent].CapitalId].Cell);}
        var ongoing=ch.Dynamics.Campaigns[warIndex];int started=ongoing.StartedYear;var theater=ch.Aggregates.Provinces.Theaters[warIndex];
        ch.Polities[ruler].Cohesion=50;ch.Polities[ruler].Exhaustion=1000;ch.Aggregates.Communities.Governments[ruler].RepresentationMilli=50;
        var dynamics=ch.Dynamics.States[ruler];dynamics.Cycle.EliteCapture=dynamics.Cycle.ConquestDebt=1000;ch.Dynamics.Leaders[dynamics.LeaderId].Administration=0;
        int successorId=ch.Polities.Length;bool replaced=(bool)Call(continuityEngine,"TryLivingRegimeChange",continuity,ruler);
        check(replaced&&ch.Polities[ruler].EndedYear==continuity.Years&&ch.International.Accounts[ruler].ContinuitySuccessor==successorId,"A real contested regime replacement records explicit continuity while a campaign is active");
        bool continued=(bool)Call(continuityEngine,"ContinueInternationalCampaign",continuity,warIndex);
        check(continued&&ongoing.EndedYear<0&&(ongoing.A==successorId||ongoing.B==successorId)&&ongoing.StartedYear==started&&ReferenceEquals(theater,ch.Aggregates.Provinces.Theaters[warIndex]),"The existing war keeps its chronology and supplied theater rather than silently ending on a change of dynasty");
        check(ch.Relations.Any(r=>r.A==ongoing.A&&r.B==ongoing.B&&r.WarSince==started)&&ch.Dynamics.Leaders[ongoing.LeaderId].PolityId==ongoing.AttackerId,"Continuing campaigns have a real current relation and commander, with correctly sorted participant identities");
        ch.Polities[successorId].Exhaustion=0;continuity.Timeline.Events=cq.Ordered();queueField.SetValue(continuityEngine,null);continuity.Seal();continuityEngine.Validate(continuity);
        var resumed=continuityEngine.AdvanceYears(continuity,2);continuityEngine.Validate(resumed);
        check(resumed.History.International!.Accounts[ruler].ContinuitySuccessor==successorId,"A real continuing regime and war survive subsequent scheduled actor processing and integrity checks");
        var annex=s.Copy();annex.History.Polities[a].EndedYear=annex.Years;annex.History.Aggregates.Successors=annex.History.Aggregates.Successors.With(a,b);
        check((int)Call(engine,"InternationalContinuingParty",annex,a)==a,"Annexation or collapse cannot inherit wars merely because the ordinary ownership successor points to a conqueror");
        check(source.ComputeHash()==original,"International testing leaves the original saved history unchanged");
    }
}
