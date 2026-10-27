using System;
using System.Linq;
using System.IO;
using WorldGen.Core;
using WorldGen.Serialization;

public static class SurvivalPrototypeTests
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
        var s=new SurvivalState();s.Validate();Check(s.Inventory.Held.Id=="stone-spear","Starter spear held");
        Check(s.Inventory.Mass>5&&s.Inventory.Mass<20,"Plausible starter load");
        var bare=new SurvivalInventory();Check(bare.TryAdd("stone",4,out _),"One handful of stones");Check(bare.TryAdd("stick",6,out _),"Second hand bundle");Check(!bare.TryAdd("log",1,out _),"No invisible naked backpack");
        string hash=WorldJson.Write(s.Inventory);Check(!s.Inventory.TryAdd("log",100,out _),"Oversized pickup rejected");Check(WorldJson.Write(s.Inventory)==hash,"Failed pickup atomic");
        s.Inventory.View=InventoryView.Grid;Check(s.Inventory.Pack(out _),"Same contents fit grid view");s.Inventory.View=InventoryView.List;Check(s.Inventory.Pack(out _),"Same contents fit list view");
        var arranged=new SurvivalInventory{View=InventoryView.Grid,NextId=7,Items=new[]{
            new SurvivalStack{Id=1,Item="pack",Container=-2},
            new SurvivalStack{Id=2,Item="stone-spear",Container=0,X=3,Y=0,Rotated=true},
            new SurvivalStack{Id=3,Item="dryhide",Container=0,X=4,Y=0,Rotated=true},
            new SurvivalStack{Id=4,Item="hide-wrap",Container=0,X=1,Y=0,Rotated=true},
            new SurvivalStack{Id=5,Item="hide-wrap",Container=0,X=1,Y=3},
            new SurvivalStack{Id=6,Item="stone-spear",Container=0,X=0,Y=0,Rotated=true}}};
        arranged.Validate();string arrangedBefore=WorldJson.Write(arranged);
        Check(!arranged.Compact(out _),"Greedy compaction may fail for an already valid manually arranged bag");
        Check(WorldJson.Write(arranged)==arrangedBefore,"Failed compaction preserves every previous slot rather than leaving overlapping items");
        arranged.Validate();Check(arranged.Pack(out _)&&WorldJson.Write(arranged)==arrangedBefore,"The preserved bag remains valid and retains its manual layout");
        var craft=SurvivalCrafting.Recipes.First(r=>r.Id=="cooked-meat");Check(s.Inventory.TryAdd("raw-meat",1,out _),"Meat added");hash=WorldJson.Write(s.Inventory);
        Check(!SurvivalCrafting.Craft(s,craft,false,out _),"Cooking requires fire");Check(hash==WorldJson.Write(s.Inventory),"Failed crafting preserves ingredients");
        Check(SurvivalCrafting.Craft(s,craft,true,out _),"Cooked at fire");Check(!s.Inventory.Has("raw-meat"),"No ingredient duplication");
        foreach(var recipe in SurvivalCrafting.Recipes.Where(r=>SurvivalItems.Get(r.Output).Kind==SurvivalItemKind.Food))
        {
            var creative=new SurvivalState{Mode=SurvivalMode.Creative,Inventory=new SurvivalInventory()};creative.Inventory.TryAdd("pack",1,out _);
            Check(SurvivalCrafting.Craft(creative,recipe,false,out _),"Creative food creation does not inspect absent ingredients: "+recipe.Id);
            Check(creative.Inventory.Items.Single(i=>i.Item==recipe.Output).Fraction==1,"Creative output is a full portion: "+recipe.Id);creative.Validate();
        }
        var storage=new SurvivalState{Inventory=new SurvivalInventory(),NextSite=2};
        var rack=new SurvivalSite{Id=1,Kind="cache",Position=new V(1,0,0)};storage.Sites=new[]{rack};
        storage.Inventory.TryAdd("pack",1,out _);storage.Inventory.TryAdd("knife",1,out _,2,1,.37);
        long wornKnife=storage.Inventory.Items.Single(i=>i.Item=="knife").Id;
        Check(SurvivalStorage.Store(storage,rack,wornKnife,1,out _)&&!storage.Inventory.Has("knife"),"Core storage transfer removes the actual carried tool");
        var cachedKnife=rack.Contents.Single();Check(cachedKnife.Condition==.37&&cachedKnife.AgeDays==2,"Stored tools retain age and condition");
        Check(SurvivalStorage.Take(storage,rack,cachedKnife,1,out _)&&storage.Inventory.Items.Single(i=>i.Item=="knife").Condition==.37,"Taking a tool does not repair it");
        storage.Inventory.TryAdd("raw-meat",2,out _,.4,.15,.7);long portionId=storage.Inventory.Items.Single(i=>i.Item=="raw-meat").Id;
        Check(SurvivalStorage.Store(storage,rack,portionId,2,out _),"Partial-sized food portions can be transferred together");
        var cachedFood=rack.Contents.Single();double storageMass=storage.Inventory.Mass+cachedFood.Count*cachedFood.Fraction*SurvivalItems.Get(cachedFood.Item).Kg;
        Check(SurvivalStorage.Take(storage,rack,cachedFood,1,out _)&&rack.Contents.Single().Count==1,"A partial stack transfer leaves the remaining camp food");
        var movedFood=storage.Inventory.Items.Single(i=>i.Item=="raw-meat");Check(movedFood.Fraction==.15&&movedFood.Condition==.7&&movedFood.AgeDays==.4,"Food transfers retain portion, condition and age");
        Check(Math.Abs(storageMass-storage.Inventory.Mass-rack.Contents.Sum(i=>i.Count*i.Fraction*SurvivalItems.Get(i.Item).Kg))<1e-10,"Camp transfers conserve total carried and cached mass");
        string storedBefore=WorldJson.Write(storage);Check(!SurvivalStorage.Take(storage,rack,cachedFood,1,out _)&&WorldJson.Write(storage)==storedBefore,"Stale camp-entry requests cannot transfer the same item twice");
        Check(!SurvivalStorage.Store(storage,rack.Copy(),wornKnife,1,out _)&&WorldJson.Write(storage)==storedBefore,"A detached camp cannot receive mutations from this saved game");
        var crowded=new SurvivalState{Inventory=new SurvivalInventory(),NextSite=2};crowded.Inventory.TryAdd("stone",4,out _);crowded.Inventory.TryAdd("stick",6,out _);
        var dropCache=new SurvivalSite{Id=1,Kind="drop",Position=new V(1,0,0),Contents=new[]{new SurvivalStack{Id=1,Item="knife",Condition=.25}}};crowded.Sites=new[]{dropCache};
        string crowdedBefore=WorldJson.Write(crowded);Check(!SurvivalStorage.Take(crowded,dropCache,dropCache.Contents[0],1,out _)&&WorldJson.Write(crowded)==crowdedBefore,"A failed pickup leaves both full hands and the dropped item unchanged");
        long branches=crowded.Inventory.Items.Single(i=>i.Item=="stick").Id;
        Check(SurvivalStorage.Drop(crowded,branches,1,new V(1,0,0),out var droppedBranch,out _)&&droppedBranch.Contents.Single().Count==1,"Dropping one item splits the actual carried stack");
        Check(SurvivalStorage.Take(crowded,droppedBranch,droppedBranch.Contents.Single(),1,out _)&&!crowded.Sites.Any(site=>site.Id==droppedBranch.Id)&&crowded.Inventory.Count("stick")==6,"Reclaiming the last dropped item retires only its empty site");
        var loadedStorage=WorldJson.Read<SurvivalState>(WorldJson.Write(storage));loadedStorage.Validate();Check(loadedStorage.Sites[0].Contents.Single().Fraction==.15,"Camp transfer state survives actual saved-data validation");
        var uniqueRack=new SurvivalSite{Id=1,Kind="cache",Position=new V(1,0,0),Contents=new[]{new SurvivalStack{Id=2,Item="stone"}}};storage.Sites=new[]{uniqueRack};
        Check(SurvivalStorage.Store(storage,uniqueRack,storage.Inventory.Items.Single(i=>i.Item=="knife").Id,1,out _)&&uniqueRack.Contents.Select(i=>i.Id).Distinct().Count()==2,"Storing after a removed entry does not reuse an occupied site-local ID");
        var loadedPack=new SurvivalState{Inventory=new SurvivalInventory()};foreach(var item in new[]{"pack","knife","stone","stick"})loadedPack.Inventory.TryAdd(item,1,out _);
        long packId=loadedPack.Inventory.Items.Single(i=>i.Item=="pack").Id;string packBefore=WorldJson.Write(loadedPack);
        Check(!SurvivalStorage.Drop(loadedPack,packId,1,new V(1,0,0),out _,out _)&&WorldJson.Write(loadedPack)==packBefore,"Dropping needed carrying equipment fails without losing it or its contents");
        Check(!SurvivalStorage.Drop(loadedPack,packId,1,new V(0,0,0),out _,out _)&&WorldJson.Write(loadedPack)==packBefore,"Invalid drop positions cannot mutate the saved inventory");
        var conditions=new SurvivalInventory();conditions.TryAdd("pack",1,out _);conditions.TryAdd("stone",1,out _,0,1,.2);conditions.TryAdd("stone",1,out _,0,1,1);
        Check(conditions.Items.Count(i=>i.Item=="stone")==2,"Picking up a worn stack does not merge it into pristine equipment");
        var dead=new SurvivalState{Dead=true,Health=0,Stamina=0,SprintExhausted=true,Days=12,Seed=7,ConsumptionEvents=19,NextSite=4};
        V fallen=new V(1,0,0),startPoint=new V(0,1,0);V Near(double angle)=>new V(Math.Cos(angle),Math.Sin(angle),0);
        var unfinishedBed=new SurvivalSite{Id=1,Kind="bed",Progress=.5,Position=Near(.001)};
        var completedBed=new SurvivalSite{Id=2,Kind="hide-bed",Position=Near(.002)};
        dead.Sites=new[]{unfinishedBed,completedBed,new SurvivalSite{Id=3,Kind="bed",Position=Near(.01)}};
        dead.Corpses=new[]{SurvivalPhysiology.Carcass("deer",1,fallen,70,0)};dead.Resources=new[]{new SurvivalResourceUse{Key="harvested",UsedDay=10,Count=1}};
        var oldInventory=dead.Inventory;oldInventory.Items.First(i=>i.Item=="knife").Condition=.42;
        Check(dead.Inventory.TryAdd("raw-meat",1,out _,.4,.2,.6),"Respawn fixture includes a fractional food portion");double deathMass=dead.Inventory.Mass;
        Check(SurvivalRespawn.TryApply(dead,fallen,startPoint,out var destination,out _)&&(destination-completedBed.Position).Length<1e-12,"Respawn ignores closer unfinished bedding and chooses a completed bed");
        var deathDrop=dead.Sites.Single(site=>site.Kind=="drop");Check(dead.Inventory.Items.Length==0&&Math.Abs(deathDrop.Contents.Sum(i=>i.Count*i.Fraction*SurvivalItems.Get(i.Item).Kg)-deathMass)<1e-10,"Respawn moves the complete equipment mass into the death pile without a free starter kit");
        Check(deathDrop.Contents.Single(i=>i.Item=="knife").Condition==.42&&deathDrop.Contents.Single(i=>i.Item=="raw-meat").Fraction==.2,"Death equipment retains wear and small food portions");
        oldInventory.Items.First(i=>i.Item=="knife").Condition=.01;Check(deathDrop.Contents.Single(i=>i.Item=="knife").Condition==.42,"The saved death pile does not alias the former inventory");
        Check(dead.Days==12&&dead.Seed==7&&dead.ConsumptionEvents==19&&dead.Corpses.Length==1&&dead.Resources.Single().Count==1&&!dead.Dead&&dead.Health==100&&dead.Stamina==100&&!dead.SprintExhausted,"Respawn restores the player without resetting world time, carcasses or gathering history");
        WorldJson.Read<SurvivalState>(WorldJson.Write(dead)).Validate();
        string livingBefore=WorldJson.Write(dead);Check(!SurvivalRespawn.TryApply(dead,fallen,startPoint,out _,out _)&&WorldJson.Write(dead)==livingBefore,"Living players cannot use respawn to duplicate equipment or heal");
        var emptyDeath=new SurvivalState{Dead=true,Health=0,Inventory=new SurvivalInventory(),Sites=new[]{unfinishedBed.Copy()},NextSite=2};
        Check(SurvivalRespawn.TryApply(emptyDeath,fallen,startPoint,out var fallback,out _)&&(fallback-startPoint).Length<1e-12&&emptyDeath.Sites.Length==1&&emptyDeath.NextSite==2,"With no completed bed or equipment, respawn uses the starting point and creates no empty death pile");
        emptyDeath.Dead=true;emptyDeath.Health=0;string invalidRespawn=WorldJson.Write(emptyDeath);
        Check(!SurvivalRespawn.TryApply(emptyDeath,new V(0,0,0),startPoint,out _,out _)&&WorldJson.Write(emptyDeath)==invalidRespawn,"Invalid respawn geometry leaves the dead player's state intact");
        var a=new SurvivalState();var b=a.Copy();var environment=new SurvivalEnvironment{TemperatureC=22,WindMps=1};
        SurvivalPhysiology.Advance(a,3600,environment);for(int i=0;i<60;i++)SurvivalPhysiology.Advance(b,60,environment);
        Check(Math.Abs(a.EnergyKcal-b.EnergyKcal)<1e-7&&Math.Abs(a.CoreTemperatureC-b.CoreTemperatureC)<1e-7,"Physiology independent of host request chunking");
        Check(a.EnergyKcal<6000&&a.WaterDeficitL>0,"Rest still consumes food and water");
        var tired=new SurvivalState{Stamina=6};SurvivalPhysiology.Advance(tired,15,new SurvivalEnvironment{TemperatureC=22,SpeedMps=6,Sprinting=true});
        Check(tired.SprintExhausted&&!SurvivalPhysiology.CanSprint(tired),"Empty sprint reserve enters recovery");
        SurvivalPhysiology.Advance(tired,60,new SurvivalEnvironment{TemperatureC=22,SpeedMps=3});
        Check(tired.Stamina>5&&!SurvivalPhysiology.CanSprint(tired),"A tiny recovery does not alternate sprint/jog every frame");
        tired=WorldJson.Read<SurvivalState>(WorldJson.Write(tired));Check(tired.SprintExhausted&&!SurvivalPhysiology.CanSprint(tired),"Sprint recovery persists across save/reload");
        SurvivalPhysiology.Advance(tired,120,new SurvivalEnvironment{TemperatureC=22,Resting=true});Check(SurvivalPhysiology.CanSprint(tired),"Recovering a useful reserve permits sprinting again");
        tired.Stamina=0;tired.SprintExhausted=true;tired.Mode=SurvivalMode.Creative;Check(SurvivalPhysiology.CanSprint(tired),"Creative movement ignores survival exhaustion");
        var cold=new SurvivalState();var sheltered=cold.Copy();SurvivalPhysiology.Advance(cold,4*3600,new SurvivalEnvironment{TemperatureC=-10,WindMps=8,RainMmPerHour=2});
        SurvivalPhysiology.Advance(sheltered,4*3600,new SurvivalEnvironment{TemperatureC=-10,WindMps=8,RainMmPerHour=2,Shelter=.9,Insulation=2,FireWarmth=12});
        Check(cold.CoreTemperatureC<sheltered.CoreTemperatureC&&cold.Health<sheltered.Health,"Shelter, insulation and fire protect from wet cold");
        var c=SurvivalPhysiology.Carcass("deer",42,new V(1,0,0),70,0);s.Corpses=new[]{c};double before=c.MeatKg;
        Check(SurvivalPhysiology.Harvest(s,c,"meat",out _),"Sharp tool harvests meat");Check(Math.Abs(before-c.MeatKg-.25)<1e-8,"Meat removed from finite carcass");
        var bones=new SurvivalState();var tenKg=SurvivalPhysiology.Carcass("small-deer",999,new V(1,0,0),10,0);bones.Corpses=new[]{tenKg};
        bool boneHarvest=true;for(int i=0;i<4;i++)boneHarvest&=SurvivalPhysiology.Harvest(bones,tenKg,"bone",out _);
        Check(boneHarvest&&tenKg.BoneKg==0,"Exhausted bone yield has no tiny negative floating-point balance");
        WorldJson.Read<SurvivalState>(WorldJson.Write(bones)).Validate();
        Check(!SurvivalPhysiology.Harvest(bones,tenKg,"bone",out _),"Exhausted carcass saves correctly and cannot supply another bone portion");
        c.SettledPose=new float[16];c.SettledPose[0]=1;c.PoseSource="test-source";
        var saved=WorldJson.Read<SurvivalState>(WorldJson.Write(s));saved.Validate();Check(saved.Corpses[0].SpawnId==42,"Carcass identity saved");
        Check(saved.Corpses[0].HasAnchor&&saved.Corpses[0].SettledPose[0]==1&&saved.Corpses[0].PoseSource=="test-source","Corpse anchor and skin pose survive serialization");
        var poseCopy=s.Copy();poseCopy.Corpses[0].SettledPose[0]=2;Check(c.SettledPose[0]==1,"Corpse pose snapshots detach their arrays");
        var copy=s.Copy();copy.Corpses[0].MeatKg=0;copy.Inventory.Items[0].Condition=.5;Check(s.Corpses[0].MeatKg>0&&s.Inventory.Items[0].Condition==1,"Save copies independent");
        var play=new PlaySession();play.Accumulate(24*60*60);Check(Math.Abs(play.PendingHistorySeconds-SimulationTime.Year)<1e-5,"24 one-hour days advance exactly one historical year");
        s.WaterDeficitL=2;var water=s.Inventory.Items.First(i=>i.Item=="waterskin");Check(SurvivalPhysiology.Eat(s,water.Id,out _),"Drink waterskin");Check(s.Inventory.Has("empty-waterskin")&&s.WaterDeficitL==1,"Vessel retained and one litre consumed");
        s.Inventory.TryAdd("raw-meat",1,out _);var spoiled=s.Inventory.Items.First(i=>i.Item=="raw-meat");spoiled.AgeDays=5;Check(!SurvivalCrafting.Craft(s,craft,true,out _),"Cooking cannot sanitize spoiled meat");
        var small=new SurvivalState();small.Inventory=new SurvivalInventory();small.Inventory.TryAdd("pack",1,out _);small.Inventory.TryAdd("knife",1,out _);
        var bird=SurvivalPhysiology.Carcass("small-bird",77,new V(1,0,0),.12,0);double mass=small.Inventory.Mass;
        Check(SurvivalPhysiology.Harvest(small,bird,"meat",out _),"Small birds provide usable fractional food portions");
        Check(Math.Abs(small.Inventory.Mass-mass-.12*.32)<1e-9&&bird.MeatKg==0,"Small-animal meat conserves mass");
        double calories=small.Inventory.Items.Where(i=>i.Item=="raw-meat").Sum(i=>i.Fraction*SurvivalItems.Get(i.Item).Kcal);
        Check(SurvivalCrafting.Craft(small,craft,true,out _),"Small portions can be roasted");
        Check(Math.Abs(small.Inventory.Items.Where(i=>i.Item=="cooked-meat").Sum(i=>i.Fraction*SurvivalItems.Get(i.Item).Kcal)-calories)<1e-8,"Cooking cannot multiply small portions into full steaks");
        var lean=SurvivalPhysiology.Carcass("mouse",78,new V(1,0,0),.02,0);Check(lean.SinewUnits*.04<=lean.MassKg*.008+1e-8&&lean.HideUnits==0,"Tiny animals cannot create heavyweight hides or sinew");
        var insects=new SurvivalState();var bee=SurvivalPhysiology.Carcass("bee",79,new V(1,0,0),.0001,0);var gnat=SurvivalPhysiology.Carcass("gnat",80,new V(1,0,0),.000001,0);insects.Corpses=new[]{bee,gnat};
        Check(bee.MassKg==.0001&&gnat.MassKg==.000001,"Small carcasses retain their actual mass instead of creating a ten-gram minimum");
        double beforeInsectMass=insects.Inventory.Mass;Check(SurvivalPhysiology.Harvest(insects,bee,"meat",out _),"Tiny edible portions can use the existing fractional food representation");
        Check(Math.Abs(insects.Inventory.Mass-beforeInsectMass-.0001*.32)<1e-9,"Tiny harvest conserves biomass rather than producing a large steak");
        WorldJson.Read<SurvivalState>(WorldJson.Write(insects)).Validate();Check(gnat.MeatKg<.000001,"Sub-ten-milligram carcasses remain valid saved survival data");
        Check(SurvivalPhysiology.AnimalVitality(.025)<=3&&SurvivalPhysiology.AnimalVitality(3)<SurvivalItems.Get("wood-spear").Damage,"A strike kills mouse-sized prey and a spear can kill rabbit-sized prey");
        Check(SurvivalPhysiology.AnimalVitality(100)>2*SurvivalItems.Get("wood-spear").Damage&&SurvivalPhysiology.AnimalVitality(100)<=2*SurvivalItems.Get("stone-spear").Damage&&SurvivalPhysiology.AnimalVitality(3900)>SurvivalPhysiology.AnimalVitality(100),"Deer retain the wooden/stone spear distinction and large animals have a greater damage threshold");
        var buildState=new SurvivalState();var blueprint=SurvivalCrafting.Shelters.First(b=>b.Id=="bed");var site=SurvivalCrafting.PlaceBlueprint(buildState,blueprint,new V(1,0,0),0);
        Check(buildState.Inventory.TryAdd("grass",2,out _)&&SurvivalCrafting.SupplyBlueprint(buildState,site,out _)&&site.Progress>0&&site.Progress<1,"Blueprint accepts a partial material delivery");
        Check(buildState.Inventory.TryAdd("grass",3,out _)&&SurvivalCrafting.SupplyBlueprint(buildState,site,out _)&&site.Progress==1&&!buildState.Inventory.Has("grass"),"A later delivery completes construction without duplicating materials");
        var incomplete=new SurvivalState();var layout=SurvivalCrafting.PlaceBlueprint(incomplete,SurvivalCrafting.Shelters.First(b=>b.Id=="lean-to"),new V(1,0,0),0);
        Check(incomplete.Inventory.TryAdd("stick",8,out _)&&SurvivalCrafting.SupplyBlueprint(incomplete,layout,out _),"Construction can aggregate several carried stacks in one delivery");
        WorldJson.Read<SurvivalState>(WorldJson.Write(incomplete)).Validate();Check(layout.Progress<1,"Incomplete multi-stack construction is a valid saved camp");
        var storedFood=small.Inventory.Items.First(i=>i.Item=="cooked-meat");var loadedSmall=WorldJson.Read<SurvivalState>(WorldJson.Write(small));loadedSmall.Validate();
        Check(loadedSmall.Inventory.Items.First(i=>i.Id==storedFood.Id).Fraction==storedFood.Fraction,"Fractional portions survive serialization");
        var fireState=new SurvivalState{Sites=new[]{new SurvivalSite{Id=1,Kind="hearth",Position=new V(1,0,0),FuelHours=2,Lit=false}},NextSite=2};
        SurvivalPhysiology.Advance(fireState,3600,new SurvivalEnvironment{TemperatureC=20});Check(fireState.Sites[0].FuelHours==2,"Fuel in an unlit hearth is not consumed");
        var old=PlaySession.Legacy();old.Validate();Check(old.Version=="play-session-1"&&old.Settings.CalendarDaysPerDay==1,"Legacy session clock is preserved");
        var current=new PlaySession{Survival=small};var read=WorldJson.Read<PlaySession>(WorldJson.Write(current));read.Validate();Check(read.Version=="play-session-2","Survival save version prevents old players silently discarding inventory");
        var persistent=new PlaySession{Survival=new SurvivalState()};persistent.Survival.Corpses=Enumerable.Range(0,8).Select(i=>SurvivalPhysiology.Carcass("deer",(ulong)(100+i),new V(1,0,0),70,0)).ToArray();
        foreach(var corpse in persistent.Survival.Corpses)corpse.SettledPose=Enumerable.Repeat(.12345678f,128*16).ToArray();
        string sessionJson=WorldJson.Write(persistent);Check(System.Text.Encoding.UTF8.GetByteCount(sessionJson)>65536,"Persistent physics save exercises the former camera-only size ceiling");
        string folder=Path.Combine(Path.GetTempPath(),"WorldGen-survival-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);string sessionFile=Path.Combine(folder,"play-session.json");
        try{File.WriteAllText(sessionFile,sessionJson);var actual=WorldLibrary.ReadPlaySession(Path.Combine(folder,"snapshot.world"));Check(actual.Survival.Corpses.Length==8&&actual.Survival.Corpses[7].SettledPose.Length==2048,"Actual library loader accepts a survival session with multiple saved ragdolls");}
        finally{File.Delete(sessionFile);Directory.Delete(folder);}
        foreach(var r in SurvivalCrafting.Recipes){Check(r.Materials.All(m=>SurvivalItems.TryGet(m.item,out _)),"Recipe inputs exist "+r.Id);Check(SurvivalItems.TryGet(r.Output,out _),"Recipe output exists "+r.Id);}
        Console.WriteLine("PASS "+checks+" survival/inventory/calendar assertions");
    }
}
