using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;

public static class HistoryHumanVariationTests
{
    public static void Run(Action<bool,string> check)
    {
        check(Enum.GetValues<HumanAppearanceTrait>().Length==8&&HistoryHumanVariation.Profile("Homo sapiens").TraitCount==8,"Human appearance reuses exactly eight existing regional ancestry values");
        try{HistoryHumanVariation.Profile("Elf");check(false,"Unimplemented sapient species");}catch(ArgumentException){check(true,"No invented phenotype/biology for an unimplemented fantasy species");}
        double equator=HistoryHumanVariation.UvExposure(0,100,1000,250),north=HistoryHumanVariation.UvExposure(.95,100,1000,250);
        check(equator>north*5&&HistoryHumanVariation.SkinTarget(equator)>HistoryHumanVariation.SkinTarget(north),"Annual UV, not a racial label or annual warmth, drives the skin-pigmentation environmental prior");
        check(HistoryHumanVariation.UvExposure(.5,3500,500,0)>HistoryHumanVariation.UvExposure(.5,0,500,0),"Highland exposure can be high even where air is colder");
        check(HistoryHumanVariation.UvExposure(.3,0,3000,950)<HistoryHumanVariation.UvExposure(.3,0,0,0),"Canopy and cloud proxies modestly reduce exposure without defining a biological race");
        int bright=5000,dim=5000,protectedValue=5000;
        for(int year=100;year<=30000;year+=100)
        {
            bright=HistoryHumanVariation.Evolve(42,3,year,HumanAppearanceTrait.SkinPigmentation,bright,100,10000,equator,200);
            dim=HistoryHumanVariation.Evolve(42,3,year,HumanAppearanceTrait.SkinPigmentation,dim,100,10000,north,200);
            protectedValue=HistoryHumanVariation.Evolve(42,3,year,HumanAppearanceTrait.SkinPigmentation,protectedValue,100,10000,equator,200,1);
        }
        check(bright>dim+3000,"Long residence produces a continuous pigmentation cline rather than instant latitude recoloring");
        check(bright>protectedValue+1000,"Cultural shelter/diet/exposure buffering can slow the simple selection prior");
        check(Math.Abs(HistoryHumanVariation.Evolve(42,3,100,HumanAppearanceTrait.SkinPigmentation,5000,100,1000000,equator,200)-5000)<100,"A century does not immediately replace a population's inherited appearance");
        foreach(var trait in new[]{HumanAppearanceTrait.HairPigmentation,HumanAppearanceTrait.IrisPigmentation,HumanAppearanceTrait.HairCurl,HumanAppearanceTrait.FacialVariation,HumanAppearanceTrait.NasalVariation,HumanAppearanceTrait.StaturePotential})
            check(HistoryHumanVariation.Evolve(42,3,100,trait,5000,100,1000,0,-200)==HistoryHumanVariation.Evolve(42,3,100,trait,5000,100,1000,1,350),"No unsupported direct climatic cause for "+HistoryHumanVariation.TraitName(trait));
        double small=0,large=0;
        for(int seed=0;seed<128;seed++)
        {
            small+=Math.Abs(HistoryHumanVariation.Evolve(seed,3,100,HumanAppearanceTrait.FacialVariation,5000,100,100,.5,200)-5000);
            large+=Math.Abs(HistoryHumanVariation.Evolve(seed,3,100,HumanAppearanceTrait.FacialVariation,5000,100,1000000,.5,200)-5000);
        }
        check(small>large*8,"Small isolated communities drift faster than large regional populations");
        for(int trait=0;trait<8;trait++)
        {
            int one=HistoryHumanVariation.Evolve(73421,32,12345,(HumanAppearanceTrait)trait,5000,250,3500,.7,230);
            check(one==HistoryHumanVariation.Evolve(73421,32,12345,(HumanAppearanceTrait)trait,5000,250,3500,.7,230),"Trait update is deterministic and query-order independent: "+trait);
            check(HistoryHumanVariation.Evolve(42,3,100,(HumanAppearanceTrait)trait,1234,0,100,.5,200)==1234&&HistoryHumanVariation.Evolve(42,3,100,(HumanAppearanceTrait)trait,1234,100,0,.5,200)==1234,"Empty/zero-duration regions do not evolve: "+trait);
        }
        check(HistoryHumanVariation.MixValue(1000,90,9000,10)==1800&&HistoryHumanVariation.MixValue(1000,0,9000,25)==9000,"Founder replacement and proportional ancestry admixture retain all contributing residents");
        check(HistoryHumanVariation.MixValue(1000,10000,9000,25)<1030,"A few arrivals cannot overwrite an established urban population's appearance");
        check(HistoryHumanVariation.MixValue(10000,long.MaxValue/2,0,long.MaxValue/2)==5000,"Weighted ancestry handles large aggregate populations without integer overflow");
        check(Math.Abs(HistoryHumanVariation.BodyTarget(-200)-HistoryHumanVariation.BodyTarget(350))<=2200,"Build adaptation is modest, not an unlimited climatic transformation");
        for(int seed=0;seed<500;seed++)for(int trait=0;trait<8;trait++)
        {
            int value=HistoryHumanVariation.Evolve(seed,3,10000,(HumanAppearanceTrait)trait,seed%2==0?0:10000,10000,100,seed%2,seed%2==0?-500:500);
            if(value<0||value>10000)throw new Exception("Unbounded human appearance.");
        }
        check(true,"All eight phenotype indices remain in existing0–10000 ancestry bounds");
        foreach(int pigment in new[]{0,5000,10000})foreach(var color in new[]{HistoryHumanVariation.SkinSwatch(pigment),HistoryHumanVariation.HairSwatch(pigment),HistoryHumanVariation.IrisSwatch(pigment)})
            check(color.X>=0&&color.X<=1&&color.Y>=0&&color.Y<=1&&color.Z>=0&&color.Z<=1,"Appearance swatches are bounded presentation values");
        Console.WriteLine($"Human appearance:30k-year UV contrast {bright}/{dim}; protected {protectedValue}; neutral drift |delta| sums {small}/{large}.");
    }
    public static void World(string path,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(path));
        if(world.Prehistory!=null)
        {
            var ownedEngine=new Prehistory(world);var owned=ownedEngine.UpgradeToLivingWorldHistory(world.Prehistory);
            var residents=typeof(Prehistory).GetMethod("ProvincialContactResidents",BindingFlags.NonPublic|BindingFlags.Instance)!;
            // Explicit fixture diagnostics may search regions; ordinary read and
            // adaptation events only query the cached local ownership cohort.
            int region=Enumerable.Range(0,owned.Population.Length).FirstOrDefault(r=>owned.Population[r]==0&&owned.History.Aggregates.Living.Regions[r].SettledPopulation==0&&(long)residents.Invoke(ownedEngine,new object[]{owned,r,false})!>0,-1);
            if(region>=0)check(ownedEngine.RegionalAppearance(owned,region).Available,"An actually inhabited owned-rural region remains visible after its residents move to the state remote ledger");
            else Console.WriteLine("Appearance fixture has no purely owned-rural region; no claim of that optional coverage.");
        }
        world.Prehistory=null;var engine=new Prehistory(world);var prepared=engine.Prepare();
        check(prepared.History.Version==Prehistory.LivingWorldHistoryVersion,"Fresh appearance fixture uses current32 rules");
        var init=typeof(Prehistory).GetMethod("InitializeFreshHumanVariation",BindingFlags.NonPublic|BindingFlags.Instance)!;
        init.Invoke(engine,new object[]{prepared});prepared.Seal();
        check(prepared.Ancestry.Skip(prepared.Origin*8).Take(8).Any(value=>value!=5000),"Fresh founder appearance is initialized without inventing occupied regions");
        check(Enumerable.Range(0,prepared.Population.Length).Where(i=>i!=prepared.Origin).All(i=>prepared.Ancestry.Skip(i*8).Take(8).All(value=>value==5000)),"Fresh initialization touches only eight founding-band values, not the whole world");
        var start=engine.Start(prepared);string original=start.ComputeHash();var advanced=start.Copy();advanced.Years=10000;int[] before=(int[])advanced.Ancestry.Clone();
        init.Invoke(engine,new object[]{advanced});check(before.SequenceEqual(advanced.Ancestry),"Initializer never resets an existing populated or progressed history");
        var legacy=start.Copy();legacy.History.Version=Prehistory.EnvironmentalHistoryVersion;
        check(!engine.RegionalAppearance(legacy,legacy.Origin).Available,"Old neutral ancestry is not presented as typed historical appearance under<=31");
        check(engine.RegionalAppearance(start,start.Origin).Available&&!engine.RegionalAppearance(null,-1).Available,"Read-only appearance samples require an occupied valid region");
        check(start.ComputeHash()==original,"Opening a regional appearance card never mutates saved simulation diagnostics or integrity");
        engine.RegionalAppearance(start,start.Origin);engine.RegionalAppearance(start,start.Origin);
        check(start.ComputeHash()==original,"Cold and warm cached appearance queries both preserve exact saved counters and integrity");
        var cold=start.Copy();var warm=start.Copy();var coldEngine=new Prehistory(world);var warmEngine=new Prehistory(world);
        warmEngine.RegionalAppearance(warm,warm.Origin);
        var contact=typeof(Prehistory).GetMethod("ProvincialContactResidents",BindingFlags.NonPublic|BindingFlags.Instance)!;
        contact.Invoke(coldEngine,new object[]{cold,cold.Origin,true});contact.Invoke(warmEngine,new object[]{warm,warm.Origin,true});
        check(cold.History.Aggregates.CellReads==warm.History.Aggregates.CellReads&&cold.ComputeHash()==warm.ComputeHash(),"Warming a read-only cohort cache cannot alter later authoritative read charges or state hashes");
        var one=new SimulationSession(engine,start);one.AdvanceSeconds(1000*SimulationTime.Year,int.MaxValue);
        var two=new SimulationSession(engine,start);two.AdvanceSeconds(400*SimulationTime.Year,int.MaxValue);two.AdvanceSeconds(600*SimulationTime.Year,int.MaxValue);
        check(one.Snapshot().Hash==two.Snapshot().Hash,"Actual region-event evolution preserves playback-partition determinism");
        var result=one.Snapshot();world.Prehistory=result;var loaded=WorldFile.Read(WorldFile.Write(world));
        check(loaded.Prehistory.Hash==result.Hash&&loaded.Prehistory.Ancestry.SequenceEqual(result.Ancestry),"Eight-trait appearance state persists through existing world-save integrity");
        check(start.ComputeHash()==original,"Appearance and continuation queries preserve their input checkpoint");
    }
}
