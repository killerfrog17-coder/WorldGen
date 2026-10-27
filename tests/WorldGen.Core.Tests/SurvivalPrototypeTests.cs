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
        var a=new SurvivalState();var b=a.Copy();var environment=new SurvivalEnvironment{TemperatureC=22,WindMps=1};
        SurvivalPhysiology.Advance(a,3600,environment);for(int i=0;i<60;i++)SurvivalPhysiology.Advance(b,60,environment);
        Check(Math.Abs(a.EnergyKcal-b.EnergyKcal)<1e-7&&Math.Abs(a.CoreTemperatureC-b.CoreTemperatureC)<1e-7,"Physiology independent of host request chunking");
        Check(a.EnergyKcal<6000&&a.WaterDeficitL>0,"Rest still consumes food and water");
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
