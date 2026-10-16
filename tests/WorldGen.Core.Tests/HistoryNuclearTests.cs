using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;
using WorldGen.Presentation;

public static class HistoryNuclearTests
{
    static object Call(Prehistory engine,string name,params object[] values)=>typeof(Prehistory).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(engine,values)!;
    public static void Run(Action<bool,string> check)
    {
        int fission=(int)HistoricalTechnology.NuclearFission,weapons=(int)HistoricalTechnology.NuclearWeapons;
        check(fission==37&&weapons==38&&HistoricalTechnicalGraph.Count==39,"Nuclear capabilities append stable IDs without reinterpreting37 previous technologies");
        check(HistoricalTechnologies.Count("history-1")==15&&HistoricalTechnologies.Count("history-12")==15&&HistoricalTechnologies.Count("history-13")==37&&HistoricalTechnologies.Count("history-31")==37&&HistoricalTechnologies.Count("history-32")==39,"Capability counts retain the recorded historical version");
        check(new HistoricalTechnicalPractice().Levels.Length==37&&new HistoricalTechnicalPractice(39).Sources.All(i=>i==-1),"Legacy default technical records remain37; new sources start unknown rather than culture0");
        check(HistoricalTechnicalGraph.UsefulLevel((int)HistoricalTechnology.ScientificMethod,"history-31")==3&&HistoricalTechnicalGraph.UsefulLevel((int)HistoricalTechnology.ScientificMethod,"history-32")==5,"New prerequisites do not silently change old societies' research priorities");
        for(int k=0;k<HistoricalTechnicalGraph.Count;k++)
        {
            check(HistoricalTechnicalGraph.Requirements(k).All(r=>r.Technology>=0&&r.Technology<k&&r.Level>=1&&r.Level<=8),"Nuclear graph remains ordered and finite: "+k);
            check(HistoricalTechnicalGraph.Cost(k,2)>HistoricalTechnicalGraph.Cost(k,1)&&HistoricalTechnicalGraph.Cost(k,3)>HistoricalTechnicalGraph.Cost(k,2),"Further levels cost additional conserved work: "+k);
        }
        var levels=Enumerable.Repeat(1,39).ToArray();check(!HistoricalTechnicalGraph.Prerequisites(fission,levels),"Basic electricity and metallurgy do not suffice for independent nuclear research");
        levels[(int)HistoricalTechnology.ScientificMethod]=4;levels[(int)HistoricalTechnology.Electricity]=3;levels[(int)HistoricalTechnology.IndustrialMetallurgy]=3;
        check(HistoricalTechnicalGraph.Prerequisites(fission,levels)&&!HistoricalTechnicalGraph.Prerequisites(weapons,levels),"Civilian fission and nuclear armament are distinct capabilities");
        levels[fission]=2;levels[(int)HistoricalTechnology.ScientificMethod]=5;levels[(int)HistoricalTechnology.IndustrialMetallurgy]=4;
        check(HistoricalTechnicalGraph.Prerequisites(weapons,levels),"Supported advanced fission/science/industry can reach nuclear armament");
        check(!HistoricalTechnicalGraph.WithinCeiling(fission,HistoricalTechnologyCeiling.Industrial)&&HistoricalTechnicalGraph.WithinCeiling(fission,HistoricalTechnologyCeiling.Modern),"Industrial-or-lower technology limits exclude nuclear development");
        var demand=typeof(Prehistory).GetMethod("TechnicalDemand",BindingFlags.NonPublic|BindingFlags.Static)!;
        double peace=(double)demand.Invoke(null,new object[]{weapons,0d,0d,0d,1d,false,1d,true})!,war=(double)demand.Invoke(null,new object[]{weapons,1d,1d,0d,1d,false,1d,true})!;
        check(peace==0&&war>0,"Isolated peaceful societies receive no free independent nuclear-weapons research motivation");
        var old=new HistoricalCulture{Technology=new int[37],Technical=new HistoricalTechnicalPractice()};
        check(HistoricalNuclearEffects.CivilianEnergy(old,5000)==0&&HistoricalTechnicalGraph.Installed(old,fission)==0,"Old records can be inspected without reading beyond saved arrays");
        var current=new HistoricalCulture{Technology=new int[39],Technical=new HistoricalTechnicalPractice(39)};current.Technology[fission]=current.Technology[(int)HistoricalTechnology.Electricity]=1;
        check(HistoricalNuclearEffects.CivilianEnergy(current,5000)==0,"Nuclear design knowledge is not an installed power system");
        current.Technical.Deployment[fission]=600;current.Technical.Deployment[(int)HistoricalTechnology.Electricity]=800;
        check(HistoricalNuclearEffects.CivilianEnergy(current,5000)==.48,"Civilian nuclear capability depends on both actual nuclear and electric deployment");
        check(HistoricalNuclearArsenals.ProductionFunding(1000000,100000,10000000,20,1,500,0)==0,"Peaceful isolation supplies no nuclear arsenal production demand");
        check(HistoricalNuclearArsenals.ProductionFunding(1000000,100000,10000000,20,1,0,1)==0,"An absent connected uranium supply cannot be replaced with a free baseline nuclear material bonus");
        check(HistoricalNuclearArsenals.ProductionFunding(1000000,100000,0,20,1,500,1)==0&&HistoricalNuclearArsenals.ProductionFunding(1000000,100000,10000000,20,0,500,1)==0,"Weapon blueprints without real money or deployed industry cannot build weapons");
        long investment=HistoricalNuclearArsenals.ProductionFunding(1000000,100000,10000000,20,1,500,1);
        check(investment>0&&investment<=10000000/4&&investment<=100000*.12*20,"Nuclear production competes for a bounded existing fiscal surplus and specialist workforce");
        check(HistoricalNuclearArsenals.DesiredStockpile(1000000,1)<=8192&&HistoricalNuclearArsenals.DesiredStockpile(long.MaxValue,1)<=8192,"State arsenals have a finite representation without territory censuses");
        double undefended=HistoricalNuclearArsenals.AttackProbability(0,1000,1000,20,1000),oneSided=HistoricalNuclearArsenals.AttackProbability(20,1000,1000,0,0),mutual=HistoricalNuclearArsenals.AttackProbability(20,1000,1000,20,1000);
        check(undefended==1&&mutual>0&&mutual<oneSided&&oneSided<.1,"Credible mutual arsenals deter more strongly but do not categorically abolish limited war");
        check(HistoricalNuclearArsenals.AttackProbability(20,1000,0,20,1000)==1,"A stockpile without a delivery capability is not a global deterrence aura");
        check(HistoricalNuclearArsenals.EscalationChance(10,100,800,10,false)==0&&HistoricalNuclearArsenals.EscalationChance(10,900,200,1,false)==0,"Routine wars and nonexistential threats do not permit nuclear escalation");
        check(HistoricalNuclearArsenals.EscalationChance(10,900,200,5,true)<HistoricalNuclearArsenals.EscalationChance(10,900,200,5,false),"Expected nuclear retaliation strongly suppresses even desperate escalation");
    }
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var before=world.Prehistory;string original=before.ComputeHash();var engine=new Prehistory(world);
        var s=engine.UpgradeToLivingWorldHistory(before);engine.Validate(s);int fission=37,weapons=38;
        check(before.ComputeHash()==original&&before.History.Cultures.All(c=>c.Technology.Length==37),"Explicit nuclear extension preserves the original37-node checkpoint and hash");
        check(s.History.Cultures.All(c=>c.Technology.Length==39&&c.Technical.Levels.Length==39&&c.Technical.Sources.Skip(37).All(i=>i==-1)&&c.Technology.Skip(37).All(i=>i==0)),"Upgrade appends unknown research without awarding a discovery, power plant or weapon");
        var next=engine.AdvanceYears(s,10);var split=engine.AdvanceYears(engine.AdvanceYears(s,3),7);check(next.Hash==split.Hash,"39-node research and contact are partition independent");
        world.Prehistory=next;var loaded=WorldFile.Read(WorldFile.Write(world));check(loaded.Prehistory.Hash==next.Hash&&engine.AdvanceYears(loaded.Prehistory,3).Hash==engine.AdvanceYears(next,3).Hash,"Expanded technical arrays preserve saved continuation");
        var probe=s.Copy();int culture=Enumerable.Range(0,probe.History.Cultures.Length).OrderByDescending(c=>probe.History.Aggregates.Cultures[c].SupportedUrban).First();var hc=probe.History.Cultures[culture];var t=hc.Technical;var account=probe.History.Aggregates.Cultures[culture];
        account.SupportedCities=4;account.SupportedUrban=500000;account.CivilizationSince=probe.Years-1000;hc.Institution=hc.Surplus=1000;
        for(int k=0;k<37;k++){hc.Technology[k]=1;t.Levels[k]=8;t.LevelYear[k]=1;t.Deployment[k]=1000;}
        t.Progress[fission]=HistoricalTechnicalGraph.Cost(fission,1,probe.History.Version);int site=probe.Society.Cultures[culture].OriginCell;
        Call(engine,"AdvanceTechnicalResearch",probe,culture,40,-1,site);
        check(t.Levels[fission]>=1&&hc.Technology[fission]>0&&t.Deployment[fission]==0,"A funded supported scientific program can discover fission without retroactive instant deployment");
        check(t.LastResearchSpent<=t.LastResearchBudget&&t.LastLearningSpent<=t.LastLearningBudget&&t.Allocation.Sum()<=10000,"Nuclear projects still share the one finite specialist budget");
        check(t.Levels[weapons]==0,"Civilian discovery does not automatically grant nuclear weapons");
        t.Levels[fission]=0;hc.Technology[fission]=0;t.Deployment[fission]=0;t.Offers[weapons]=1;t.OfferYear[weapons]=probe.Years;t.Sources[weapons]=(culture+1)%probe.History.Cultures.Length;t.Learning[weapons]=HistoricalTechnicalGraph.Cost(weapons,1,probe.History.Version)/8;
        Call(engine,"AdvanceTechnicalResearch",probe,culture,40,-1,site);
        check(t.Levels[weapons]>0&&t.Deployment[weapons]==0,"An imported weapon design cannot conjure its absent nuclear/industrial deployment chain");
        check(TechnicalResearchView.Read(before,culture).All(p=>p.Id<37)&&HistoricalTechnologyCoverage.Measure(before).Length==39,"New catalogs safely render an unchanged37-node old save");
        ArsenalWorld(engine,s,check);
    }
    static void ArsenalWorld(Prehistory engine,PrehistoryState source,Action<bool,string> check)
    {
        var s=source.Copy();var h=s.History;var a=h.Aggregates;
        check(h.Nuclear!=null&&h.Nuclear.States.Length==h.Polities.Length&&h.Nuclear.States.All(r=>r.Stockpile==0&&r.Produced==0),"History upgrade awards no historical nation a free nuclear inventory");
        var active=h.Polities.Where(p=>p.EndedYear<0&&a.Polities[p.Id].Cities.Count>0).ToArray();
        var pair=(from first in active from second in active where first.Id<second.Id orderby D.Angle(engine.World.Surface.Cells[s.Society.Settlements[first.CapitalId].Cell].Position,engine.World.Surface.Cells[s.Society.Settlements[second.CapitalId].Cell].Position) select(first.Id,second.Id)).First();
        int owner=pair.Item1,rival=pair.Item2;var p=h.Polities[owner];var hc=h.Cultures[p.CultureId];var record=h.Nuclear.States[owner];var account=a.Polities[owner];
        for(int k=0;k<hc.Technology.Length;k++){hc.Technology[k]=1;hc.Technical.Levels[k]=8;hc.Technical.LevelYear[k]=0;hc.Technical.InstalledYear[k]=1;hc.Technical.Deployment[k]=1000;}
        hc.Institution=1000;h.Dynamics.States[owner].PublicWorks=1000;
        var relation=(HistoricalRelation)Call(engine,"AggregateRelation",s,owner,rival);relation.WarSince=s.Years-1;relation.Trust=-100;
        int capital=p.CapitalId;int[] peers=a.Cities[capital].Neighbors;a.Cities[capital].Neighbors=new[]{h.Polities[rival].CapitalId};
        long urban=account.Urban,receipts=account.Receipts,service=account.ServiceCost;
        // Controlled aggregate capacity, not a census or invented residents in
        // a saved world: restore the underlying population ledger before use.
        account.Urban=1000000;account.Receipts=500000;account.ServiceCost=1000;p.Treasury=100000000;
        foreach(int city in Enumerable.Range(0,a.Cities.Length).Where(city=>a.Resolve(s.Society.Settlements[city].PolityId,h)==owner))a.Cities[city].Neighbors=new[]{h.Polities[rival].CapitalId};
        record.LastYear=s.Years-200;long treasury=p.Treasury;Call(engine,"AdvanceNuclearState",s,owner,200);
        check(record.Stockpile>0&&record.Produced==record.Stockpile&&record.Investment>0&&p.Treasury==treasury-record.Investment-record.UpkeepPaid,"A hostile supplied industrial state commissions real weapons by spending actual treasury, not by possessing a capability string");
        int arsenal=record.Stockpile;long cost=record.Investment;Call(engine,"AdvanceNuclearState",s,owner,200);
        check(record.Stockpile==arsenal&&record.Investment==cost,"Reprocessing the same nuclear event date cannot duplicate paid production");
        account.Urban=urban;account.Receipts=receipts;account.ServiceCost=service;
        var unfunded=s.Copy();var abandoned=unfunded.History.Nuclear.States[owner];unfunded.History.Polities[owner].Treasury=0;abandoned.LastYear=s.Years-100;
        Call(engine,"AdvanceNuclearState",unfunded,owner,100);
        check(abandoned.Stockpile==0&&abandoned.Dismantled==arsenal&&abandoned.ReadinessMilli==0,"Unpaid stockpile stewardship eventually dismantles weapons instead of maintaining immortal free arsenals");
        var succession=s.Copy();int total=succession.History.Nuclear.States.Sum(r=>r.Stockpile);Call(engine,"CloseNuclearState",succession,owner,rival,false);
        check(succession.History.Nuclear.States.Sum(r=>r.Stockpile)==total&&succession.History.Nuclear.States[owner].Transferred==arsenal&&succession.History.Nuclear.States[rival].Inherited==arsenal&&succession.History.Nuclear.States[rival].ReadinessMilli==0,"Successor custody conserves weapons but does not clone the former government's trained readiness");
        check(!(bool)Call(engine,"NuclearCanReach",succession,rival,owner),"An inheriting nonnuclear government does not instantly gain operational strategic attack capability");
        var queueField=typeof(Prehistory).GetField("aggregateQueue",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var regime=s.Copy();var former=regime.History.Polities[owner];var dynamics=regime.History.Dynamics.States[owner];former.Cohesion=50;former.Exhaustion=1000;dynamics.Cycle.EliteCapture=dynamics.Cycle.ConquestDebt=1000;
        regime.History.Dynamics.Leaders[dynamics.LeaderId].Administration=0;regime.History.Aggregates.Communities.Governments[owner].RepresentationMilli=0;
        queueField.SetValue(engine,new SimulationEventQueue(regime.Timeline.Events));int formerCount=regime.History.Polities.Length;
        for(int opportunity=0;opportunity<20&&regime.History.Polities.Length==formerCount;opportunity++){Call(engine,"TryLivingRegimeChange",regime,owner);regime.Years++;}
        queueField.SetValue(engine,null);int replacement=regime.History.Polities.Length-1;
        check(regime.History.Polities.Length==formerCount+1&&regime.History.Nuclear.States.Length==regime.History.Polities.Length&&regime.History.Nuclear.States[owner].Stockpile==0&&regime.History.Nuclear.States[replacement].Stockpile==arsenal&&regime.History.Nuclear.States[replacement].ReadinessMilli==record.ReadinessMilli,"Actual regime-change creation appends an aligned account and transfers the continuing institution's arsenal and crews without duplication");
        relation.WarSince=-1;var queue=new SimulationEventQueue(s.Timeline.Events);queueField.SetValue(engine,queue);
        Call(engine,"BeginAggregateCampaign",s,owner,rival,relation,s.Society.Settlements[h.Polities[rival].CapitalId].Cell);int campaign=h.Dynamics.Campaigns.Length-1;
        long population=a.TotalPopulation,targetPopulation=s.Society.Settlements[h.Polities[rival].CapitalId].Population;
        check((bool)Call(engine,"ExecuteNuclearStrike",s,campaign,owner,rival),"An actually armed aircraft-capable belligerent can expend a weapon on a real opposing city");
        long lost=population-a.TotalPopulation;
        check(lost>0&&record.Stockpile==arsenal-1&&record.Used==1&&record.CivilianDeaths==lost&&s.Society.Settlements[h.Polities[rival].CapitalId].Population<targetPopulation,"Nuclear use debits exactly one weapon and actual city/suburban/rural casualty ledgers, without annexation or created population");
        check(relation.Trust==-100&&p.Legitimacy<650&&h.Archive.Any(e=>e.Kind=="nuclear-strike"),"Atomic use records an important historical event and causes political repercussions");
        s.Timeline.Events=queue.Ordered();queueField.SetValue(engine,null);s.Seal();engine.Validate(s);
        string profileHash=s.ComputeHash();var profile=engine.NuclearProfile(s,owner);
        check(profile.Available&&profile.Stockpile==record.Stockpile&&profile.InflictedCivilianDeaths==lost&&s.ComputeHash()==profileHash,"Nuclear profile distinguishes actual stockpile and inflicted losses without changing any authoritative history");
        var snapshot=engine.World.Prehistory;engine.World.Prehistory=s;var loaded=WorldFile.Read(WorldFile.Write(engine.World));engine.World.Prehistory=snapshot;
        check(loaded.Prehistory.Hash==s.Hash&&loaded.Prehistory.History.Nuclear.States[owner].Stockpile==record.Stockpile,"Nonempty arsenals and casualty/custody histories survive canonical save/load verification");
        var advanced=engine.AdvanceYears(s,10);var partitioned=engine.AdvanceYears(engine.AdvanceYears(s,3),7);
        check(advanced.Hash==partitioned.Hash,"Actually armed state maintenance, deployment, diplomacy and campaigns are invariant to publication partitioning");
    }
}
