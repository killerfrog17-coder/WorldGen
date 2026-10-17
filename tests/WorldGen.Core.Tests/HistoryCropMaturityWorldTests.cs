using System.Reflection;
using WorldGen.Core;
using WorldGen.Serialization;

// Offline actual-world query controls. No rules22 simulation is advanced.
public static class HistoryCropMaturityWorldTests
{
    static object Call(Prehistory engine,string method,params object[] args)=>typeof(Prehistory).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(engine,args)!;
    public static void Cold22(string file,Action<bool,string> check)
    {
        var world=WorldFile.Read(File.ReadAllBytes(file));var source=world.Prehistory;var engine=new Prehistory(world);string original=source.Hash;
        check(new HistoricalRealmLifecycles().Version==22,"Genuine22 cold acceptance requires a coherent activated22 assembly, not a manually forced future marker.");
        var next=engine.UpgradeRealmLifecycles(source);engine.Validate(next);
        check(next.History.Aggregates.Empires.Connections.Realms.Version==22&&StateHash.Compute(source.History.Agriculture)==StateHash.Compute(next.History.Agriculture),"Explicit22 changes the rule marker prospectively without fabricating selected traits or harvested stock.");
        check(next.History.Aggregates.TotalPopulation==source.History.Aggregates.TotalPopulation&&next.Society.Settlements.Select(t=>t.FoodReserve).SequenceEqual(source.Society.Settlements.Select(t=>t.FoodReserve))&&next.History.Polities.Select(p=>p.Treasury).SequenceEqual(source.History.Polities.Select(p=>p.Treasury)),"The actual crop-calendar upgrade conserves every existing person, town food stock and treasury.");
        world.Prehistory=next;var restored=WorldFile.Read(WorldFile.Write(world));var cold=new Prehistory(restored);cold.Validate(restored.Prehistory);
        check(restored.Prehistory.Hash==next.Hash,"A full-world cold load retains the activated22 rule marker and exact historical checkpoint.");
        var full=engine.AdvanceYears(next,12);var five=cold.AdvanceYears(restored.Prehistory,5);restored.Prehistory=five;
        var middle=WorldFile.Read(WorldFile.Write(restored));var other=new Prehistory(middle);var split=other.AdvanceYears(middle.Prehistory,7);engine.Validate(full);other.Validate(split);
        check(middle.Prehistory.Hash==five.Hash&&split.Hash==full.Hash,"Actual22 twelve years exactly equal five years, a genuine intermediate cold save/reload, and seven further years.");
        check(source.ComputeHash()==original,"Both new-rule continuations and cold saves leave the original21 checkpoint unchanged.");
        var fresh=engine.Start(engine.Prepare(source.Origin));check(fresh.History.Aggregates.Empires.Connections.Realms.Version==22,"Fresh current setup opts into22 without upgrading the preserved original world.");
        Console.WriteLine("Genuine22 cold12==5+save/load+7 "+full.Hash);
    }
    public static void World(string file,Action<bool,string> check,string old21HashOutput=null)
    {
        var world=WorldFile.Read(File.ReadAllBytes(file));var source=world.Prehistory;var engine=new Prehistory(world);engine.Validate(source);
        string hash=source.ComputeHash(),agriculture=StateHash.Compute(source.History.Agriculture);
        check(source.History.Aggregates.Empires.Connections.Realms.Version==21,"Actual input retains its saved revision21; no public upgrade opts it into22.");
        var old=source.Copy();old.History.Aggregates.Empires.Connections.Realms.Version=21;
        var next=old.Copy();next.History.Aggregates.Empires.Connections.Realms.Version=22;
        Call(engine,"PrepareAggregateEnvironment",old);Call(engine,"PrepareAggregateRelations",old);
        double Fit(PrehistoryState state,int cell,HistoricalCultivar seed,bool cache)
        {engine.UseAgronomicFitCache=cache;return (double)Call(engine,"CommunityTransferFit",state,cell,seed,false);}
        int french=176034;
        check(world.Surface.Cells.Length>french&&engine.CellPatch[french]>=0,"The actual accepted Earth terrain includes the recorded French field, not a synthesized habitat.");
        var profiles=(from crops in source.History.Agriculture from cropGroup in Enumerable.Range(0,6)
                      let seed=crops.Cultivars?[cropGroup] let cell=crops.PracticeCell[cropGroup]
                      where seed!=null&&seed.CropId>=0&&cell>=0&&cell<engine.CellPatch.Length&&engine.CellPatch[cell]>=0
                      select (Culture:crops.CultureId,Group:cropGroup,Cell:cell,Seed:seed)).Take(160).ToArray();
        check(profiles.Length>20,"The world fixture has many actually saved cultivated profiles and dated practice fields.");
        bool parity=true,wild=true,area=true,physical=true,orchards=true;int changed=0,orchardChecks=0;object example=null;
        var physicalFields=typeof(HabitatCell).GetFields().Where(f=>f.Name!=nameof(HabitatCell.WarmTenthsC)).ToArray();
        foreach(var profile in profiles)
        {
            var definition=HistoricalCropCatalog.Crops[profile.Seed.CropId];
            int group=profile.Seed.CropId==10?6:profile.Seed.CropId==12?7:definition.Group;
            foreach(int cell in new[]{profile.Cell,french}.Distinct())
            {
                double literalOld=Fit(old,cell,profile.Seed,false),cachedNew=Fit(next,cell,profile.Seed,true),literalNew=Fit(next,cell,profile.Seed,false),cachedOld=Fit(old,cell,profile.Seed,true);
                parity&=literalOld==cachedOld&&literalNew==cachedNew&&Fit(next,cell,profile.Seed,true)==literalNew&&Fit(old,cell,profile.Seed,true)==literalOld;
                int duration=definition.Group==4?0:Math.Min(365,profile.Seed.MaturityDays);
                var legacyHabitat=(HabitatCell)Call(engine,"CommunityAgronomicHabitat",old,cell,group);
                var matureHabitat=(HabitatCell)Call(engine,"CommunityCalendarHabitat",next,cell,group,duration);
                physical&=physicalFields.All(f=>Equals(f.GetValue(legacyHabitat),f.GetValue(matureHabitat)));
                wild&=(double)Call(engine,"CommunityWildCropFit",old,cell,definition.Group)==(double)Call(engine,"CommunityWildCropFit",next,cell,definition.Group);
                area&=(double)Call(engine,"CommunityAreaYield",old,cell,group,legacyHabitat,.5)==(double)Call(engine,"CommunityAreaYield",next,cell,group,legacyHabitat,.5);
                if(definition.Group==4){orchardChecks++;orchards&=literalOld==literalNew;}
                else if(literalOld!=literalNew){changed++;example??=new{profile.Culture,profile.Cell,TargetCell=cell,profile.Seed.CropId,profile.Seed.MaturityDays,Legacy=literalOld,MaturityCalendar=literalNew,LegacyWarmth=legacyHabitat.WarmTenthsC,MaturityWarmth=matureHabitat.WarmTenthsC};}
            }
        }
        check(parity,"Actual cultivar queries retain exact cached/literal equality across21→22→21 on the same warmed engine.");
        check(changed>0,"Actual saved non-orchard seeds, not just metadata counterfactuals, exercise the changed maturity calendar.");
        check(orchardChecks>0&&orchards,"Actually saved orchard cultivars retain their exact21 fit under22.");
        check(physical,"Only crop-window warmth changes: actual growing days, annual cold, soil, moisture, flooding and forest inputs remain identical; perennial cold constraints are not removed.");
        check(wild&&area,"Wild-crop opportunities and generic cultivated-area arithmetic remain literal21 when queried after22 cultivar caches.");
        check((int)Call(engine,"CommunityNativeCropMask",old,french)==(int)Call(engine,"CommunityNativeCropMask",next,french),"The new carried-seed calendar invents no local native crop provenance.");
        var potato=profiles.First(x=>x.Seed.CropId==10).Seed;
        int dry=-1,frozen=-1,shortSeason=-1;
        var rivers=(double[])typeof(Prehistory).GetField("riverSites",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(engine)!;
        for(int cell=0;cell<engine.CellPatch.Length&&(dry<0||frozen<0||shortSeason<0);cell++)
        {
            if(engine.CellPatch[cell]<0)continue;var h=(HabitatCell)Call(engine,"CropHabitat",old,cell);
            if(dry<0&&h.AridityMilli<90&&rivers[cell]<=.2&&h.Flooding!=FloodRegime.RiverSeasonal&&h.Flooding!=FloodRegime.RiverPersistent)dry=cell;
            if(frozen<0&&h.PermafrostPermille>=700)frozen=cell;
            if(shortSeason<0&&h.GrowingDays<potato.MaturityDays-50)shortSeason=cell;
        }
        check(dry>=0&&Fit(old,dry,potato,false)==0&&Fit(next,dry,potato,false)==0,"An actual unwatered hyper-arid cell remains zero-fit with the same saved potato seed.");
        check(frozen>=0&&Fit(old,frozen,potato,false)==0&&Fit(next,frozen,potato,false)==0,"Actual permanent frozen ground remains zero-fit; a warmer selected window cannot remove it.");
        check(shortSeason>=0&&Fit(old,shortSeason,potato,false)==0&&Fit(next,shortSeason,potato,false)==0,"An actual insufficient frost-free season remains unable to mature the saved seed.");
        var matched=potato.Copy();matched.DaylengthLatitudeTenths=(int)Math.Round(Math.Abs(Math.Asin(world.Surface.Cells[french].Position.Z))*180/Math.PI*10);
        var mismatched=matched.Copy();mismatched.DaylengthLatitudeTenths+=250;
        check(Fit(next,french,matched,false)>0&&Fit(next,french,mismatched,false)<Fit(next,french,matched,false),"The actual French field still penalizes a25-degree photoperiod mismatch; the maturity calendar does not waive it.");
        check(source.ComputeHash()==hash&&StateHash.Compute(source.History.Agriculture)==agriculture,"All private21/22 queries leave the original historical hash and every saved crop trait unchanged.");
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{Source=Path.GetFullPath(file),source.Years,Hash=hash,Profiles=profiles.Length,ChangedQueries=changed,OrchardQueries=orchardChecks,Example=example,DryCell=dry,FrozenCell=frozen,ShortSeasonCell=shortSeason}));
        if(old21HashOutput!=null)
        {
            if(File.Exists(old21HashOutput))throw new IOException("Do not overwrite a continuation control.");
            var twelve=engine.AdvanceYears(source,12);engine.Validate(twelve);File.WriteAllText(old21HashOutput,twelve.Hash);Console.WriteLine("Literal old21 +12 "+twelve.Hash);
            check(source.ComputeHash()==hash,"The literal21 chronological control also preserves its source checkpoint.");
        }
    }
}
