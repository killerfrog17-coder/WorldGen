using System.Reflection;
using System.Text.Json;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryCivilMaritimeTests
{
    public static void Run(Action<bool,string> check)
    {
        foreach(int revision in Enumerable.Range(0,27))foreach(double bureaucracy in new[]{0,.2,.5,1})foreach(int preceding in new[]{500,550})
            check(HistoricalCivilMaritimeEffects.CohesionFloor(revision,bureaucracy,preceding)==preceding,"Every preceding revision keeps its literal500/550 civil threshold.");
        foreach(int revision in Enumerable.Range(0,27))foreach(double fleet in new[]{0,.199,.2,1})foreach(double roll in new[]{0,.25,.999999})
            check(HistoricalCivilMaritimeEffects.IdleReviewYears(revision,fleet,roll)==5+15*roll,"Every preceding review keeps the exact named-roll arithmetic.");
        foreach(double bureaucracy in new[]{0,.2,.5,1})foreach(int preceding in new[]{500,550})
            check(HistoricalCivilMaritimeEffects.CohesionFloor(27,bureaucracy,preceding)==HistoricalRealmPoliticalEffects.FleetCohesionFloor(bureaucracy),"Civil planning and commitment reuse the existing deployed fleet-continuity floor.");
        check(HistoricalCivilMaritimeEffects.CohesionFloor(27,0,550)==500&&HistoricalCivilMaritimeEffects.CohesionFloor(27,1,550)==320,"Ancient funded continuity remains demanding while real impersonal administration can continue through lower national cohesion.");
        check(HistoricalCivilMaritimeEffects.CohesionFloor(27,double.NaN,550)==500&&HistoricalCivilMaritimeEffects.CohesionFloor(27,double.PositiveInfinity,550)==500,"Invalid institutional capacity cannot create an invalid permissive threshold.");
        foreach(double roll in new[]{0,.25,1})
        {
            check(HistoricalCivilMaritimeEffects.IdleReviewYears(27,.2,roll)==1+2*roll,"Actually funded returning fleets receive bounded one-to-three-year reviews.");
            check(HistoricalCivilMaritimeEffects.IdleReviewYears(27,.199,roll)==5+15*roll,"Weak returning fleets retain the preceding review cadence.");
        }
        foreach(double fleet in new[]{double.NaN,double.PositiveInfinity,-1})check(HistoricalCivilMaritimeEffects.IdleReviewYears(27,fleet,.5)==12.5,"Invalid fleet inputs do not qualify for accelerated reviews.");
        check(HistoricalCivilMaritimeEffects.IdleReviewYears(27,1,double.NaN)==3,"Invalid review rolls never produce an immediate retry.");
        check(!HistoricalMaritimeMissionEffects.ExpeditionReadiness(516,0,128,9354,884,117633990,135886,10000),"The actual civil-court correction does not relax hostile-state expedition readiness.");
        check(!HistoricalMaritimeMissionEffects.ExpeditionReadiness(550,0,0,10000,900,1000000,100000,1000)&&HistoricalMaritimeMissionEffects.ExpeditionReadiness(600,0,0,10000,900,1000000,100000,1000),"The preceding armed600 threshold is unchanged.");
    }
    public static void World(string path,string output,Action<bool,string> check)
    {
        if(File.Exists(output))throw new IOException("Use new civil maritime evidence.");
        var world=WorldFile.Read(File.ReadAllBytes(path));var original=world.Prehistory;string hash=original.ComputeHash();long originalReads=original.History.Aggregates.CellReads;var engine=new Prehistory(world);
        check(original.Years==155700&&original.History.Polities[21].Cohesion==516&&original.History.Polities[21].CultureId==1,"The fixture uses the actual dated court, not a forced rich/high-cohesion substitute.");
        object Call(string name,params object[] args)=>typeof(Prehistory).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(engine,args)!;
        var evidence=new List<object>();PrehistoryState positive=null;HistoricalSeaMission convoy=null;
        foreach(var variant in new[]{(Name:"old26",Revision:26,Cash:true,Food:true),(Name:"new27",Revision:27,Cash:true,Food:true),(Name:"new27-no-cash",Revision:27,Cash:false,Food:true),(Name:"new27-no-food",Revision:27,Cash:true,Food:false)})
        {
            var s=original.Copy();var a=s.History.Aggregates;var p=s.History.Polities[21];a.Empires.Connections.Realms.Version=variant.Revision;var map=a.Empires.Connections.Geography.Governments[21];map.NextPlan=SimulationTime.Seconds(s);
            if(!variant.Cash)p.Treasury=0;if(!variant.Food)for(int i=0;i<a.Polities[21].Cities.Count;i++)s.Society.Settlements[a.Polities[21].Cities.At(i)].FoodReserve=0;
            long money=p.Treasury,people=a.TotalPopulation,food=s.Society.Settlements.Sum(t=>t.FoodReserve);int before=map.Missions.Length;var reasons=new SortedDictionary<string,int>();engine.SeaVentureObserver=(id,r)=>{if(id==21)reasons[r]=reasons.TryGetValue(r,out int n)?n+1:1;};
            Call("PrepareAggregateEnvironment",s);Call("PrepareAggregateRelations",s);Call("PlanSeaVentures",s,21);engine.SeaVentureObserver=null;
            var added=map.Missions.Skip(before).ToArray();long debit=money-p.Treasury,foodDebit=food-s.Society.Settlements.Sum(t=>t.FoodReserve);
            check(a.TotalPopulation==people,"Planning never creates or removes settlers from the actual human ledger.");
            if(variant.Name=="old26")check(added.Length==2&&added.All(m=>m.Kind==HistoricalSeaMission.Survey)&&debit==1342&&foodDebit==12,"The actual old26 planner exactly reproduces frozen paid-survey decisions and cash/food debits.");
            else if(variant.Name=="new27")
            {
                check(added.Count(m=>m.Kind==HistoricalSeaMission.Convoy)==1&&debit==1593&&foodDebit==19,"Actual cohesion516 now funds the same paid convoy as the separately tested550 counterfactual, without free ships or provisions.");
                positive=s;convoy=added.Single(m=>m.Kind==HistoricalSeaMission.Convoy);check(convoy.Target==157370&&convoy.Due>convoy.Departed,"The positive is the witnessed empty Moroccan shore and retains a real future journey clock.");
            }
            else check(added.Length==0&&debit==0,"No real cash or no actual supplier food prevents all new funded missions.");
            evidence.Add(new{variant.Name,variant.Revision,Reasons=reasons,CashSpent=debit,FoodSpent=foodDebit,NewMissions=added.Length,Convoys=added.Count(m=>m.Kind==HistoricalSeaMission.Convoy)});
        }
        // A same-frame commitment probe is explicitly separate from the real
        // journey below. It checks both sides of the readiness/commit contract.
        foreach(int revision in new[]{26,27})
        {
            var s=original.Copy();var a=s.History.Aggregates;a.Empires.Connections.Realms.Version=revision;var route=new Prehistory.Route{A=engine.CellPatch[convoy.Source],B=engine.CellPatch[convoy.Target],CellA=convoy.Source,CellB=convoy.Target,Sea=true,DistanceM=convoy.DistanceM};
            Call("PrepareAggregateEnvironment",s);Call("PrepareAggregateRelations",s);int before=a.Empires.Territories.PostAt[convoy.Target];long treasury=s.History.Polities[21].Treasury,total=a.TotalPopulation;
            Call("CommitTerritorialSeaCharter",s,route,1,1,true,21);int after=a.Empires.Territories.PostAt[convoy.Target];
            check(revision==26?after==before:after>=0&&a.Empires.Territories.Posts[after].PolityId==21,"The actual empty-coast commitment agrees with its guarded civil readiness, while old26 remains blocked.");
            check(a.TotalPopulation==total&&(revision==26?s.History.Polities[21].Treasury==treasury:s.History.Polities[21].Treasury<treasury),"A real charter costs treasury and conserves actual people; the preceding rejected charter spends nothing.");
        }
        int refused=-1;
        foreach(int city in Enumerable.Range(0,original.Society.Settlements.Length).Where(i=>original.Society.Settlements[i].PolityId<0&&original.Society.Settlements[i].Population>=500).Take(64))
        {
            var s=original.Copy();var a=s.History.Aggregates;var p=s.History.Polities[21];a.Empires.Connections.Realms.Version=27;var town=s.Society.Settlements[city];
            Call("PrepareAggregateEnvironment",s);Call("PrepareAggregateRelations",s);
            if(!a.Cities[city].Supported||a.Living.Cities[city].AbandonedYear>=0||!(bool)Call("DiffusionShore",town.Cell))continue;
            var route=new Prehistory.Route{A=engine.CellPatch[convoy.Source],B=engine.CellPatch[town.Cell],CellA=convoy.Source,CellB=town.Cell,Sea=true,DistanceM=(int)Math.Round(D.Angle(world.Surface.Cells[convoy.Source].Position,world.Surface.Cells[town.Cell].Position)*world.Recipe.RadiusKm*1000)};
            var supplier=new object[]{s,21,route,a.TownAt[convoy.Source],-1,0L};if(!(bool)Call("MaritimeCharterLogistics",supplier))continue;
            int last=(int)Call("EnvironmentalContactYear",s,town.CultureId,p.CultureId);bool familiar=town.CultureId==p.CultureId||last>=0&&last>=s.Years-100;
            var leader=s.History.Dynamics.Leaders[s.History.Dynamics.States[21].LeaderId];double consent=HistoricalMaritimeIntegrationEffects.ConsentChance(1,leader.Diplomacy,p.Legitimacy,a.Communities.Governments[21].RepresentationMilli,familiar,1-town.Stability/1000.0);
            if((double)Call("HistoryRoll","maritime-local-council-"+city,21,s.Years)<consent)continue;
            long investment=HistoricalMaritimeIntegrationEffects.CouncilInvestment(town.Population,route.DistanceM);
            check(p.Exhaustion<500&&p.Treasury>=investment*2&&a.Polities[21].Receipts>a.Polities[21].ServiceCost+investment/100&&HistoricalImperialHierarchy.Patron(s,21)<0,"The refusal probe passes real money, fiscal margin, exhaustion and hierarchy gates before the actual failed consent.");
            long money=p.Treasury,population=town.Population,total=a.TotalPopulation;int army=p.Army,states=s.History.Polities.Length;
            bool accepted=(bool)Call("CommitMaritimeCouncil",s,21,city,route,1,true);
            check(!accepted&&town.PolityId<0&&town.Population==population&&p.Army==army&&p.Treasury==money&&a.TotalPopulation==total&&s.History.Polities.Length==states,"Actual physically supplied independent town with failed consent cannot be coerced at516 under the lower CIVIL entry floor.");
            refused=city;break;
        }
        check(refused>=0,"The refusal negative uses a real supported reachable independent town, not an already invalid logistics fixture.");
        var idleEvidence=new List<object>();
        foreach(var idle in new[]{(Revision:26,Cash:true),(Revision:27,Cash:true),(Revision:27,Cash:false)})
        {
            var s=original.Copy();var a=s.History.Aggregates;var p=s.History.Polities[21];a.Empires.Connections.Realms.Version=idle.Revision;var map=a.Empires.Connections.Geography.Governments[21];long now=SimulationTime.Seconds(s);map.NextPlan=now;
            // Controlled absence of currently reviewable opportunities, not a
            // fabricated lack of ships, provisions or source-port funding.
            for(int region=0;region<map.ReviewAfter.Length;region++)map.ReviewAfter=map.ReviewAfter.With(region,s.Years+100);
            if(!idle.Cash)p.Treasury=0;long cash=p.Treasury,food=s.Society.Settlements.Sum(t=>t.FoodReserve);
            Call("PrepareAggregateEnvironment",s);Call("PrepareAggregateRelations",s);double fleet=(double)Call("ReturningFleetCapacity",s,21);Call("PlanSeaVentures",s,21);
            double years=(map.NextPlan-now)/(double)SimulationTime.Year;
            check(map.Missions.Length==0&&p.Treasury==cash&&food==s.Society.Settlements.Sum(t=>t.FoodReserve),"An empty opportunity portfolio never creates a mission or spends actual stocks.");
            check(fleet>=.2&&(idle.Cash?(idle.Revision==26?years>=5&&years<=20:years>=1&&years<=3):map.NextPlan==now),"Actual funded fleet empty reviews use the guarded cadence; a no-cash early exit cannot gain a faster review.");
            idleEvidence.Add(new{idle.Revision,idle.Cash,ReturningFleet=fleet,ReviewYears=years});
        }
        // Schedule the genuinely paid new27 missions into the real calendar.
        // The central marker is deliberately not activated by this fixture.
        var queue=new SimulationEventQueue(positive.Timeline.Events);var queueField=typeof(Prehistory).GetField("aggregateQueue",BindingFlags.NonPublic|BindingFlags.Instance)!;queueField.SetValue(engine,queue);
        try{Call("ScheduleSeaMissions",positive,21);positive.Timeline.Events=queue.Ordered();}finally{queueField.SetValue(engine,null);}
        long seconds=convoy.Due-SimulationTime.Seconds(positive)+SimulationTime.Day;
        var arrived=(PrehistoryState)Call("AdvanceAggregateCalendar",positive,seconds,int.MaxValue,CancellationToken.None,false,int.MaxValue);
        check(SimulationTime.Seconds(arrived)>=convoy.Due,"The funded mission actually travels through the event calendar before the landfall assertion.");
        check(arrived.History.Aggregates.Empires.Territories.PostAt[convoy.Target]>=0&&HistoricalCells.Polity(arrived,convoy.Target)==21,"The actual paid early-modern voyage establishes a serviced charter at arrival, not only a planner promise.");
        original.History.Aggregates.CellReads=originalReads;check(original.ComputeHash()==hash,"All controlled future-rule probes preserve the actual26 source hash.");
        File.WriteAllText(output,JsonSerializer.Serialize(new{Source=path,Hash=hash,Year=original.Years,Warning="Detached future27 controlled fixtures; no future-rule valid-save or full cold-upgrade claim. Actual source unchanged. Same-frame charter checks are separate from the subsequently scheduled paid journey; idle portfolios deliberately defer every candidate.",Cases=evidence,IdleCases=idleEvidence,RefusingIndependentTown=refused,ArrivalYear=arrived.Years,ArrivalHash=arrived.Hash},new JsonSerializerOptions{WriteIndented=true}));
    }
}
