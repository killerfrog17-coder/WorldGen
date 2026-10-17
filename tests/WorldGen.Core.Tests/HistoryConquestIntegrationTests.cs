using System.Reflection;
using System.Runtime.CompilerServices;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryConquestIntegrationTests
{
    public static void Run(Action<bool,string> check)
    {
        double Load(long people,double served,double distance=1,bool returned=false,double inclusion=0)=>HistoricalConquestIntegrationEffects.Capture(people,served,distance,returned,inclusion);
        check(Load(0,100)==0&&Load(-1,100)==0,"Empty terrain creates no invented integration residents.");
        check(Load(1000,999000)==.5,"A small acquisition is proportionate to the recipient's exclusive served population.");
        check(Load(1000,1000)==250&&Load(9000,1000)==450,"A small ancient court acquiring a large fraction still bears substantial load.");
        check(Load(1000,0)==500&&Load(1000,0,3)==1000,"An unserved remote empire cannot use nominal holdings to dilute a large distant acquisition.");
        check(Load(1000,1000,2)==500&&Load(1000,1000,1)==Load(1000,1000,0),"Physical effort above local service reach increases burden, while nearby service remains possible.");
        check(Load(1000,1000,double.NaN)==750&&Load(1000,double.NaN)==500,"Invalid capacity or route inputs cannot create a free acquisition.");
        check(Load(1000,1000,1,true)==187.5&&Load(1000,1000,1,false,1)==187.5,"Recent direct return and actual recipient inclusion are separate bounded discounts.");
        check(Load(1000,1000,1,true,1)==140.625,"Even directly returning fully included residents retain a nonzero integration load.");
        check(Load(long.MaxValue,long.MaxValue)==250&&Load(long.MaxValue,0)==500,"Large resident accounts avoid integer overflow.");
        double Capacity(double integration=1,double bureaucracy=1,int works=1000,int inclusion=1000,bool served=true,double distance=1,double crisis=0,int exhaustion=0)=>HistoricalConquestIntegrationEffects.Capacity(integration,bureaucracy,works,inclusion,served,distance,crisis,exhaustion);
        check(Capacity()==1&&Capacity(bureaucracy:0)==.5,"Recovery requires delivered institutions rather than a historical era label.");
        check(Capacity(integration:0)==0&&Capacity(works:0)==0&&Capacity(inclusion:0)==0&&Capacity(served:false)==0,"Missing actual integration, public works, recipient inclusion or service prevents paid repair.");
        check(Capacity(exhaustion:1000)==0&&Capacity(crisis:1)==.25,"Total exhaustion stops extra repair; a contested but funded government can still repair at reduced capacity.");
        check(Capacity(distance:2)==.5&&Capacity(distance:3)==.2&&Capacity(distance:double.PositiveInfinity)==0,"Physical delivery attenuates distant institutional capacity without a latitude or date ban.");
        check(Capacity(integration:2,bureaucracy:2,works:2000,inclusion:2000,exhaustion:-1)==1,"Capacity inputs remain bounded.");
        var old=HistoricalConquestIntegrationEffects.Recover(934,10,2,true,0,169570,16343,1000000);
        check(old.Debt==914&&old.Payment==0&&old.PaidReduction==0,"Without delivered paid capacity the preceding writing-era baseline is unchanged.");
        check(HistoricalConquestIntegrationEffects.Recover(934,10,1,true,0,0,0,0).Debt==924,"Nonliterate baseline recovery remains one unit per year.");
        var pause=HistoricalConquestIntegrationEffects.Recover(934,10,2,false,0,100,200,1000000);
        check(pause.Debt==934&&pause.Payment==0,"The old deficit/crisis baseline pause is retained by the caller.");
        var funded=HistoricalConquestIntegrationEffects.Recover(934,10,2,true,1,169570,16343,1000000);
        check(funded.PaidReduction==301&&funded.Debt==613&&funded.Payment==50869,"A funded serviced government amortizes actual integration load through explicitly paid work.");
        check(funded.Payment<=.05*(169570-16343)*10&&funded.Payment<=1000000,"Paid repair respects both the current surplus share and real treasury.");
        foreach(var inputs in new[]{(100L,100L,1000000L),(100L,200L,1000000L),(100L,0L,0L),(0L,0L,1000000L)})
        {
            var r=HistoricalConquestIntegrationEffects.Recover(934,10,2,true,1,inputs.Item1,inputs.Item2,inputs.Item3);
            check(r.Debt==914&&r.Payment==0,"No treasury or positive fiscal margin means no additional recovery.");
        }
        var constrained=HistoricalConquestIntegrationEffects.Recover(934,10,2,false,1,1000,0,7);
        check(constrained.PaidReduction==7&&constrained.Payment==7&&constrained.Debt==927,"Finite cash caps actual work rather than granting debt reduction on credit.");
        var contested=HistoricalConquestIntegrationEffects.Recover(934,10,2,false,Capacity(crisis:1),169570,16343,1000000);
        check(contested.PaidReduction>0&&contested.Debt<934&&contested.PaidReduction<funded.PaidReduction,"Paid institutional repair can continue in crisis without restoring the paused free baseline.");
        foreach(int debt in new[]{-1,0,1,934,1000,int.MaxValue})foreach(int elapsed in new[]{-1,0,1,10,int.MaxValue})
        {
            var r=HistoricalConquestIntegrationEffects.Recover(debt,elapsed,int.MaxValue,false,1,long.MaxValue,0,long.MaxValue);
            check(r.Debt>=0&&r.Debt<=1000&&r.Payment>=0&&r.Payment<=long.MaxValue&&r.PaidReduction>=0&&r.PaidReduction<=1000,"Hostile integer inputs cannot overflow money or create negative debt.");
        }
        var engine=(Prehistory)RuntimeHelpers.GetUninitializedObject(typeof(Prehistory));
        var guard=typeof(Prehistory).GetMethod("ConquestIntegrationHistory",BindingFlags.Instance|BindingFlags.NonPublic)!;
        foreach(int revision in new[]{0,1,4,6,11,23,24,25,26})
        {
            var s=new PrehistoryState{History=new HistoryState{Aggregates=new HistoricalAggregateState{Empires=new HistoricalImperialSystem{Connections=new HistoricalColonialConnections{Realms=new HistoricalRealmLifecycles{Version=revision}}}}}};
            check((bool)guard.Invoke(engine,new object[]{s})! == (revision>=25),"All preceding rule revisions retain their literal old capture and recovery branches.");
        }
    }
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));var source=world.Prehistory;string original=source.ComputeHash();var engine=new Prehistory(world);
        object Call(string method,params object[] args)=>typeof(Prehistory).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,args)!;
        var s=source.Copy();var a=s.History.Aggregates;var h=s.History;Call("PrepareAggregateEnvironment",s);Call("PrepareAggregateRelations",s);
        int winner=Enumerable.Range(0,h.Polities.Length).Where(i=>h.Polities[i].EndedYear<0&&a.Polities[i].Cities.Count>0).OrderByDescending(i=>h.Polities[i].Population).First();
        var towns=Enumerable.Range(0,a.Cities.Length).Where(i=>s.Society.Settlements[i].Population>0&&s.Society.Settlements[i].PolityId>=0&&a.Resolve(s.Society.Settlements[i].PolityId,h)>=0&&a.Resolve(s.Society.Settlements[i].PolityId,h)!=winner&&engine.CellPatch[s.Society.Settlements[i].Cell]>=0).Take(3).ToArray();
        check(towns.Length>0,"The controlled accounting fixture uses actual inhabited foreign towns from the saved world.");
        foreach(int town in towns)
        {
            var probe=s.Copy();int cell=probe.Society.Settlements[town].Cell;long before=probe.History.Polities[winner].Population,total=probe.History.Aggregates.TotalPopulation;
            long residents=(long)Call("ConquestTransferredResidents",probe,cell,winner);Call("ClaimAggregatePolity",probe,cell,winner);
            check(probe.History.Polities[winner].Population-before==residents,"The pre-capture resident quote exactly equals the actual town plus remote-cohort transfer, with no duplicate residents.");
            check(probe.History.Aggregates.TotalPopulation==total,"Political transfer leaves the authoritative global population unchanged.");
            check((long)Call("ConquestTransferredResidents",probe,cell,winner)==0,"A cell already controlled by the recipient cannot be charged again.");
        }
        if(towns.Length>0)
        {
            var probe=s.Copy();int town=towns[0],cell=probe.Society.Settlements[town].Cell,loser=probe.History.Aggregates.Resolve(probe.Society.Settlements[town].PolityId,probe.History);var prior=probe.History.Polities[loser];var local=probe.History.Aggregates.Communities.Cities[town];
            prior.ParentId=-1;local.RulerId=loser;local.InclusionMilli=0;
            double Load()=>(double)Call("ConquestCaptureLoad",probe,winner,loser,cell,probe.History.Polities[winner].CapitalId);
            double basis=Load();local.InclusionMilli=1000;
            check(basis>0&&Load()==basis,"Actual previous-owner inclusion gives the incoming ruler no counterfeit recipient-specific credit.");
            prior.CultureId=probe.History.Polities[winner].CultureId;
            check(Load()==basis,"Giving the actual losing court the same culture alone creates no historical-unity discount.");
            prior.ParentId=winner;prior.FoundedYear=probe.Years-51;
            check(Load()==basis,"An old direct descendant is not treated as a recent administrative return.");
            prior.FoundedYear=probe.Years-50;
            check(Math.Abs(Load()-basis*.75)<.000002,"The actual caller recognizes only a bounded recent direct-secession return.");
            prior.ParentId=-1;local.RulerId=winner;
            check(Math.Abs(Load()-basis*.75)<.000002,"Only an already recipient-aligned actual local ruler can supply inclusion credit.");
        }
        check(source.ComputeHash()==original,"Controlled transfer probes leave the loaded source entirely unchanged.");
        // These are controlled accounting probes, not a claim of a future-rule
        // valid save. Genuine revision25 cold continuation runs separately once
        // the central explicit upgrade has been enabled.
    }
}
