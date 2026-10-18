using System.Reflection;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

public static class HistorySovereigntyTests
{
    static object Call(Prehistory e,string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(e,args)!;
    public static void Run(Action<bool,string> check)
    {
        for(int rights=0;rights<=1000;rights+=10)
        {
            double peaceful=HistoricalSovereigntyEffects.Consent(500,rights,650,0,.5,0);
            check(peaceful>=0&&peaceful<=1,"Local consent is bounded.");
            check(peaceful>=HistoricalSovereigntyEffects.Consent(500,rights,650,700,.5,0),"Attributed capture harm discourages accepting that protector.");
            check(peaceful>=HistoricalSovereigntyEffects.Consent(500,rights,650,0,.5,.18),"Actual religious exclusion discourages submission.");
            check(peaceful>=HistoricalSovereigntyEffects.Consent(500,Math.Max(0,rights-10),650,0,.5,0),"Better offered local rights do not lower consent.");
        }
        check(!HistoricalSovereigntyEffects.CanSecede(false,0,1,1000,0),"A crisis alone does not create a local independence organization.");
        check(!HistoricalSovereigntyEffects.CanSecede(false,1000,0,100,900),"A locally supported faction cannot automatically displace a functioning court.");
        check(HistoricalSovereigntyEffects.CanSecede(false,800,.8,800,300),"An organized coalition can exploit a real central crisis.");
        check(HistoricalSovereigntyEffects.CanSecede(true,0,0,0,900),"A proven cut-off supported center need not wait for a fictional army-wide war.");
        for(int k=0;k<100;k++)check(HistoricalSovereigntyEffects.ReviewDelay(k/100.0)>=2&&HistoricalSovereigntyEffects.ReviewDelay(k/100.0)<=8,"Negotiation reviews use bounded random windows.");
        var r=new HistoricalSovereigntyHistory();var copy=r.Copy();copy.Cases=copy.Cases.With(3,20);
        check(r.Cases[3]<0&&copy.Cases[3]==20,"Published sovereignty trees detach immutably.");
        check(StateHash.Compute(r)!=StateHash.Compute(copy),"A political episode participates in integrity hashes.");
        check(StateHash.Compute(WorldJson.Read<HistoricalSovereigntyHistory>(WorldJson.Write(copy)))==StateHash.Compute(copy),"Sovereignty records retain the shared JSON representation.");
    }
    static void OrganizedSecession(Prehistory e,PrehistoryState source,Action<bool,string> check)
    {
        var s=source.Copy();var h=s.History;var a=h.Aggregates;int city=-1,owner=-1;
        foreach(var state in h.Polities.Where(p=>p.EndedYear<0&&a.Polities[p.Id].Cities.Count>=2))
        {
            for(int k=0;k<Math.Min(12,a.Polities[state.Id].Cities.Count);k++)
            {
                int id=a.Polities[state.Id].Cities.At(k);var t=s.Society.Settlements[id];
                if(id==state.CapitalId||t.Population<1200||a.Living.Cities[id].AbandonedYear>=0||(bool)Call(e,"SovereigntyDetached",s,state.Id,id))continue;
                city=id;owner=state.Id;break;
            }
            if(city>=0)break;
        }
        check(city>=0,"The actual terrain has a secondary populated center whose connection is not proven blocked.");
        var town=s.Society.Settlements[city];a.Cities[city].Supported=true;a.Cities[city].Neighbors=Array.Empty<int>();town.Stability=800;h.Cultures[town.CultureId].Institution=800;
        h.Polities[owner].Cohesion=800;a.Communities.Cities[city].InclusionMilli=0;h.Towns[city].AutonomyPressure=0;
        var realm=a.Empires.Connections.Realms;realm.Governments[owner].CrisisUntil=0;
        Call(e,"BeginSovereigntyCase",s,city,owner,-1,HistoricalSovereigntyRequest.Independence);s.Years=Prehistory.SovereigntyCaseAt(s,city).NextYear;
        int states=h.Polities.Length;Call(e,"AdvanceSovereigntyCase",s,city);
        check(h.Polities.Length==states&&HistoricalCells.Polity(s,town.Cell)==owner,"A connected town's unsupported independence request does not produce a state.");
        Call(e,"ResolveSovereigntyCase",s,city,Prehistory.SovereigntyCaseAt(s,city),HistoricalSovereigntyOutcome.Expired,"ends in the controlled test");s.Years+=21;
        realm.Governments[owner].CrisisUntil=s.Years+40;realm.Governments[owner].CrisisSeverity=1000;h.Towns[city].AutonomyPressure=900;h.Polities[owner].Cohesion=300;
        var preceding=HistoricalRealmClaimantHistory.At(s,city);if(preceding!=null){preceding.SupportMilli=0;preceding.ResolvedYear=s.Years;preceding.Outcome="Controlled earlier coalition dissolution";}
        h.Polities[owner].Treasury=1000000;h.Polities[owner].Army=3000;
        Call(e,"BeginSovereigntyCase",s,city,owner,-1,HistoricalSovereigntyRequest.Independence);
        check(HistoricalRealmClaimantHistory.Active(s,HistoricalRealmClaimantHistory.At(s,city)),"Severe local demands organize a real named claimant coalition rather than a free-standing flag.");
        s.Years=Prehistory.SovereigntyCaseAt(s,city).NextYear;
        long money=h.Polities[owner].Treasury,population=a.TotalPopulation;int army=h.Polities[owner].Army,campaigns=h.Dynamics.Campaigns.Length;
        var otherOwners=s.Society.Settlements.Select(t=>t.PolityId).ToArray();
        var queue=new SimulationEventQueue(s.Timeline.Events);var field=typeof(Prehistory).GetField("aggregateQueue",BindingFlags.Instance|BindingFlags.NonPublic)!;
        field.SetValue(e,queue);try{Call(e,"AdvanceSovereigntyCase",s,city);}finally{field.SetValue(e,null);}s.Timeline.Events=queue.Ordered();
        int successor=town.PolityId;
        check(h.Polities.Length==states+1&&successor==states&&Prehistory.SovereigntyTransferAt(s,city).Cause==HistoricalSovereigntyCause.Secession,"An organized local coalition in a real crisis can establish a recorded successor court.");
        check(a.TotalPopulation==population&&h.Polities[owner].Army+h.Polities[successor].Army==army,"The successor inherits actual people and levies rather than generating an independence army.");
        check(h.Polities[owner].Treasury+h.Polities[successor].Treasury==money-100&&h.Dynamics.Campaigns.Length==campaigns+1,"A funded surviving parent opens an actual paid restoration campaign after connected secession.");
        check(h.Relations.Any(r=>r.WarSince>=0&&(r.A==owner&&r.B==successor||r.A==successor&&r.B==owner)),"The new political conflict is visible as a real active war, not hidden territorial exchange.");
        check(s.Society.Settlements.Where(t=>t.Id!=city).All(t=>t.PolityId==otherOwners[t.Id]),"Local independence does not sweep neighboring towns or an entire province into the new flag.");
        var r=realm.Sovereignty;r.Cases=r.Cases.WithBlock(city*12,new[]{successor,owner,0,s.Years,s.Years,s.Years+2,700,650,-1,0,0,-1});
        field.SetValue(e,queue);try{Call(e,"ClaimAggregatePolity",s,town.Cell,owner);Call(e,"RecordSovereigntyConquest",s,city,owner,campaigns);}finally{field.SetValue(e,null);}s.Timeline.Events=queue.Ordered();
        var transfer=Prehistory.SovereigntyTransferAt(s,city);
        check(transfer.Cause==HistoricalSovereigntyCause.Conquest&&transfer.Campaign==campaigns&&transfer.From==successor&&transfer.To==owner&&Prehistory.SovereigntyCaseAt(s,city).Outcome==HistoricalSovereigntyOutcome.Superseded,"A capture transaction identifies the actual restoration campaign and supersedes the local petition.");
        Call(e,"ValidateSovereigntyHistory",s);
    }
    public static void World(string path,string output,Action<bool,string> check)
    {
        Directory.CreateDirectory(output);var world=WorldFile.Read(File.ReadAllBytes(path));var old=world.Prehistory;var e=new Prehistory(world);string before=old.ComputeHash();
        var s=e.UpgradeRealmLifecycles(old);e.Validate(s);
        check(s.History.Aggregates.Empires.Connections.Realms.Version>=34&&s.History.Aggregates.Empires.Connections.Realms.Sovereignty!=null,"Explicit upgrade initializes prospective sovereignty records.");
        check(old.ComputeHash()==before&&old.History.Aggregates.Empires.Connections.Realms.Sovereignty==null,"The unupgraded input remains byte-for-byte literal.");
        check(s.History.Aggregates.Cells.Fingerprint()==old.History.Aggregates.Cells.Fingerprint()&&s.History.Aggregates.TotalPopulation==old.History.Aggregates.TotalPopulation,"Upgrade grants no borders, people, towns or alleged historical transfers.");
        check(ReferenceEquals(s,e.UpgradeRealmLifecycles(s)),"A complete upgrade is idempotent.");
        var fresh=e.Start(e.Prepare());e.Validate(fresh);
        check(fresh.History.Aggregates.Empires.Connections.Realms.Version>=34&&fresh.History.Aggregates.Empires.Connections.Realms.Sovereignty!=null,"Fresh histories initialize the same prospective sovereignty rules without an alternate simulation route.");
        var proposal=s.Copy();var a=proposal.History.Aggregates;int target=-1,receiver=-1;
        foreach(var state in proposal.History.Polities.Where(p=>p.EndedYear<0).Take(16))
        {
            int capital=state.CapitalId;state.Legitimacy=state.Cohesion=800;state.Treasury=10000000;a.Cities[capital].Supported=true;a.Polities[state.Id].Receipts=Math.Max(a.Polities[state.Id].Receipts,a.Polities[state.Id].ServiceCost+1000);
            foreach(int neighbor in a.Cities[capital].Neighbors.Take(16))
            {
                var t=proposal.Society.Settlements[neighbor];if(t.Population<300||a.Living.Cities[neighbor].AbandonedYear>=0||neighbor==capital)continue;
                a.Cities[neighbor].Neighbors=new[]{capital};
                if((bool)Call(e,"SovereigntyReceiverReachable",proposal,neighbor,state.Id)){target=neighbor;receiver=state.Id;break;}
            }
            if(target>=0)break;
        }
        check(target>=0,"The actual saved terrain contains a locally reachable town/protector pair.");
        var town=proposal.Society.Settlements[target];Call(e,"ClaimAggregatePolity",proposal,town.Cell,-1);
        long population=a.TotalPopulation,people=town.Population,money=proposal.History.Polities[receiver].Treasury;int wars=proposal.History.Dynamics.Campaigns.Length;
        check((bool)Call(e,"BeginSovereigntyCase",proposal,target,-1,receiver,HistoricalSovereigntyRequest.Protection),"A real local request can be opened.");
        var first=Prehistory.SovereigntyCaseAt(proposal,target);
        check(HistoricalCells.Polity(proposal,town.Cell)<0&&town.Population==people&&proposal.History.Polities[receiver].Treasury==money,"A petition alone neither transfers ownership nor moves people nor debits agreement costs.");
        check(first.NextYear>=proposal.Years+2&&first.NextYear<=proposal.Years+8,"The saved review is randomized rather than an immediate transfer.");
        var insolvent=proposal.Copy();insolvent.History.Polities[receiver].Treasury=0;insolvent.Years=first.NextYear;Call(e,"AdvanceSovereigntyCase",insolvent,target);
        check(Prehistory.SovereigntyCaseAt(insolvent,target).Outcome==HistoricalSovereigntyOutcome.Refused&&HistoricalCells.Polity(insolvent,town.Cell)<0,"A physically reachable but insolvent protector cannot annex a town.");
        var noRoute=proposal.Copy();noRoute.History.Aggregates.Cities[target].Neighbors=Array.Empty<int>();noRoute.Years=first.NextYear;Call(e,"AdvanceSovereigntyCase",noRoute,target);
        check(Prehistory.SovereigntyCaseAt(noRoute,target).Outcome==HistoricalSovereigntyOutcome.Refused&&HistoricalCells.Polity(noRoute,town.Cell)<0,"Distant court knowledge without an actual local relationship cannot grant a protection agreement.");
        var empty=proposal.Copy();long emptyPopulation=empty.History.Aggregates.TotalPopulation;Call(e,"AbandonLivingTown",empty,target,"Controlled refuge/sovereignty counterfactual");
        check(Prehistory.SovereigntyCaseAt(empty,target).Outcome==HistoricalSovereigntyOutcome.Superseded&&empty.History.Aggregates.TotalPopulation==emptyPopulation,"Actual abandonment cancels the local petition and conserves its people as refugees.");
        bool accepted=false;int paid=0;
        for(int trial=0;trial<40&&!accepted;trial++)
        {
            if(trial>0){proposal.Years+=21;Call(e,"BeginSovereigntyCase",proposal,target,-1,receiver,HistoricalSovereigntyRequest.Protection);}
            proposal.Years=Prehistory.SovereigntyCaseAt(proposal,target).NextYear;long previous=proposal.History.Polities[receiver].Treasury;
            Call(e,"AdvanceSovereigntyCase",proposal,target);if(proposal.History.Polities[receiver].Treasury<previous)paid++;
            accepted=Prehistory.SovereigntyCaseAt(proposal,target).Outcome==HistoricalSovereigntyOutcome.Accepted;
        }
        check(accepted&&HistoricalCells.Polity(proposal,town.Cell)==receiver,"A financed, physically reachable and locally accepted agreement can change administration.");
        var transfer=Prehistory.SovereigntyTransferAt(proposal,target);
        check(transfer.Cause==HistoricalSovereigntyCause.NegotiatedProtection&&transfer.From==-1&&transfer.To==receiver&&transfer.DisputeSince>=s.Years,"The actual transfer retains its cause, parties and earlier negotiation date.");
        Call(e,"RecordSovereigntyConquest",proposal,target,receiver,-1);
        check(Prehistory.SovereigntyTransferAt(proposal,target).Cause==HistoricalSovereigntyCause.NegotiatedProtection,"An invented or absent campaign cannot relabel peaceful submission as conquest.");
        check(a.TotalPopulation==population&&town.Population==people&&proposal.History.Dynamics.Campaigns.Length==wars,"Peaceful submission creates neither people nor a fictional military campaign.");
        check(proposal.History.Polities[receiver].Treasury==money-paid*(100+people/50),"Every attempted local agreement spends its actual fixed/scale financing cost, not free treasury.");
        string readHash=proposal.ComputeHash();var section=HistoryAtlas.SovereigntySection(proposal,target);
        check(section!=null&&section.Facts.Any(f=>f.Label=="Last governing transfer")&&AtlasNavigation.Group(section.Title)=="Power & relations"&&proposal.ComputeHash()==readHash,"A read-only atlas account exposes negotiated transfers in the political section.");
        var detached=Prehistory.SovereigntyCaseAt(proposal,target);detached.Support=999;
        check(Prehistory.SovereigntyCaseAt(proposal,target).Support!=999,"Public sovereignty queries return detached data, not writable saved records.");
        OrganizedSecession(e,s,check);
        var full=e.AdvanceYears(s,80);var parts=e.AdvanceYears(e.AdvanceYears(s,31),49);e.Validate(full);
        check(full.Hash==parts.Hash,"Actual80-year local politics/campaign continuation is identical across31+49 publications.");
        world.Prehistory=full;var loaded=WorldFile.Read(WorldFile.Write(world));var cold=new Prehistory(loaded);cold.Validate(loaded.Prehistory);
        check(cold.AdvanceYears(loaded.Prehistory,12).Hash==e.AdvanceYears(full,12).Hash,"Actual binary cold continuation retains pending cases, decisions and later causal transfers.");
        var ledger=full.History.Aggregates.Empires.Connections.Realms.Sovereignty;
        check(StateHash.Compute(WorldJson.Read<HistoricalSovereigntyHistory>(WorldJson.Write(ledger)))==StateHash.Compute(ledger),"Actual sparse episode ledger roundtrips JSON.");
        check(old.ComputeHash()==before,"All review work preserves the old checkpoint.");
        File.WriteAllBytes(Path.Combine(output,"review.world"),WorldFile.Write(world));File.WriteAllText(Path.Combine(output,"expected12.hash"),e.AdvanceYears(full,12).Hash);File.WriteAllText(Path.Combine(output,"legacy12.hash"),e.AdvanceYears(old,12).Hash);
        var cases=Enumerable.Range(0,full.Society.Settlements.Length).Select(city=>new{City=city,Case=Prehistory.SovereigntyCaseAt(full,city),Transfer=Prehistory.SovereigntyTransferAt(full,city)}).Where(x=>x.Case!=null||x.Transfer!=null).ToArray();
        var focused=cases.Where(x=>full.History.Aggregates.Living.Cities[x.City].AbandonedYear<0).OrderByDescending(x=>x.Transfer?.Cause==HistoricalSovereigntyCause.Conquest).ThenBy(x=>x.City).FirstOrDefault();
        File.WriteAllText(Path.Combine(output,"result.json"),System.Text.Json.JsonSerializer.Serialize(new{Hash=full.Hash,Cases=cases.Count(x=>x.Case!=null),Pending=cases.Count(x=>x.Case?.Outcome==HistoricalSovereigntyOutcome.Pending),Transfers=cases.Count(x=>x.Transfer!=null),TransferCauses=cases.Where(x=>x.Transfer!=null).GroupBy(x=>x.Transfer.Cause).ToDictionary(x=>x.Key.ToString(),x=>x.Count()),Focus=focused==null?null:new{City=focused.City,Name=full.Society.Settlements[focused.City].Name,Cell=full.Society.Settlements[focused.City].Cell},Sample=cases.Take(12)},new System.Text.Json.JsonSerializerOptions{IncludeFields=true,WriteIndented=true}));
    }
}
