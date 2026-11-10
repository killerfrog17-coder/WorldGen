using WorldGen.Core;
using WorldGen.Serialization;
using WorldGen.Presentation;
static class ForagerTemporalTests
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        var recipe=Recipe.Preset("Continents");recipe.Detail=5000;recipe.Seed=73421;
        var w=new WorldPipeline().Generate(recipe);var engine=new Prehistory(w);var start=engine.StartForagers(engine.PrepareForagers());
        var full=engine.AdvanceSeconds(start,251*SimulationTime.Year+12345);
        var split=start;long[] portions={60,3*SimulationTime.Year-60,17*SimulationTime.Year,12345,231*SimulationTime.Year};
        foreach(long portion in portions)split=engine.AdvanceSeconds(split,portion);
        Check(split.Hash==full.Hash,"Minute/year caller boundaries cannot alter climate, demography, teaching, migration or affiliation histories");
        var budget=start;long target=SimulationTime.Seconds(full);while(SimulationTime.Seconds(budget)<target)budget=engine.AdvanceSeconds(budget,target-SimulationTime.Seconds(budget),1);
        Check(budget.Hash==full.Hash,"A one-event work budget produces exactly the same full ledger");
        var session=new SimulationSession(engine,start);while(session.Seconds<target)session.AdvanceSeconds(target-session.Seconds,17,maximumGlobalWork:7);
        Check(session.Hash==full.Hash,"Private playback and global work budgets preserve the public simulation result");
        var published=session.Snapshot();published.Foragers.Cohorts[0].Speech[0].Share=0;published.Foragers.Cohorts[0].TeachingFrom[0]=0;published.Foragers.Biota[0].ForestCover=0;
        Check(session.Snapshot().ComputeHash()==session.Hash,"A UI snapshot cannot mutate private affiliations, teachers or ecological stocks");
        var middle=engine.AdvanceSeconds(start,23*SimulationTime.Year+999);w.Prehistory=middle;
        var restored=WorldFile.Read(WorldFile.Write(w));var restoredEngine=new Prehistory(restored);
        var resumed=restoredEngine.AdvanceSeconds(restored.Prehistory,target-SimulationTime.Seconds(restored.Prehistory));
        Check(resumed.Hash==full.Hash,"Compact save/reload between reviews retains cached teachers and projects from the same fixed boundary");
        if(full.Foragers.Version=="foragers-5")
        {
            var consumer=full.Copy();var maker=consumer.Foragers.Cohorts.First(c=>c.Population>=1);maker.Skill[13]=.1;maker.Knowledge[13]=.8;maker.Use[13]=.6;consumer.Hash=consumer.ComputeHash();engine.ValidateForagers(consumer);
            w.Prehistory=consumer;var products=WorldFile.Read(WorldFile.Write(w)).Prehistory;restoredEngine.ValidateForagers(products);
            Check(products.Hash==consumer.Hash&&products.Foragers.Cohorts.First(c=>c.Id==maker.Id).Use[13]==.6,"Shared goods serving more consumers than makers survive real compact save/reload without a legacy prevalence clamp");
        }
        long before=session.Seconds;string beforeHash=session.Hash;
        try{session.AdvanceSeconds(SimulationTime.Year,1,new CancellationToken(true));throw new Exception("Expected cancellation");}catch(OperationCanceledException){}
        Check(session.Seconds==before&&session.Hash==beforeHash,"Cancellation cannot commit a partial private checkpoint");
        using(var cancel=new CancellationTokenSource())
        {
            engine.WorkObserver=m=>{if(m.Stage=="regional process 2"&&m.Items>0)cancel.Cancel();};
            // Migration is staggered over 200–600 years; thirty years can
            // legitimately contain no migration work in the regional model.
            try{session.AdvanceSeconds(601*SimulationTime.Year,int.MaxValue,cancel.Token);throw new Exception("Expected mid-transaction cancellation");}catch(OperationCanceledException){}
            engine.WorkObserver=null;
            Check(session.Seconds==before&&session.Hash==beforeHash&&session.Snapshot().Hash==beforeHash,"Cancellation after migration rolls back the entire private demographic transaction");
        }
        var partial=engine.AdvanceSeconds(start,SimulationTime.Year);var c=partial.Foragers.Cohorts[0];
        Check(Math.Abs(ForagerDemography.Present(partial.Foragers,c)-ForagerDemography.Project(start.Foragers.Cohorts[0],1,0).Population)<1e-10,"Fractional-date observer population uses actual projected demographics");
        Check(start.Hash==start.ComputeHash(),"All alternate histories leave their original checkpoint intact");
        var play=new PlaySession{PendingHistorySeconds=SimulationTime.Day*.75};
        Check(!SimulationClockView.PlayPublicationDue(start,(long)play.PendingHistorySeconds,1000)&&SimulationClockView.PlayPublicationDue(start,SimulationTime.Day,1),"Ordinary gameplay batches complete day publications instead of copying all ancestry every two real seconds");
        double displayed=PlayCalendar.Seconds(play,SimulationTime.Seconds(start));play.Consume(SimulationTime.Day*.5);
        Check(PlayCalendar.Seconds(play,SimulationTime.Seconds(start)+SimulationTime.Day/2)==displayed,"Consuming a batched publication cannot jump or lose the displayed gameplay date");
        Check(SimulationClockView.PlayPublicationDue(new PrehistoryState(),60,0),"The publication optimization does not alter old-history scheduling");
        engine.ValidateForagers(full);engine.ValidateForagers(resumed);
        Console.WriteLine($"PASS {checks} temporal, budget, isolation, cancellation and save/reload checks; hash={full.Hash}");
    }
}
