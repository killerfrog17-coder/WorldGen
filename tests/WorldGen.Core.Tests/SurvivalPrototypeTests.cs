using System;
using System.Linq;
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
        var saved=WorldJson.Read<SurvivalState>(WorldJson.Write(s));saved.Validate();Check(saved.Corpses[0].SpawnId==42,"Carcass identity saved");
        var copy=s.Copy();copy.Corpses[0].MeatKg=0;copy.Inventory.Items[0].Condition=.5;Check(s.Corpses[0].MeatKg>0&&s.Inventory.Items[0].Condition==1,"Save copies independent");
        var play=new PlaySession();play.Accumulate(24*60*60);Check(Math.Abs(play.PendingHistorySeconds-SimulationTime.Year)<1e-5,"24 one-hour days advance exactly one historical year");
        s.WaterDeficitL=2;var water=s.Inventory.Items.First(i=>i.Item=="waterskin");Check(SurvivalPhysiology.Eat(s,water.Id,out _),"Drink waterskin");Check(s.Inventory.Has("empty-waterskin")&&s.WaterDeficitL==1,"Vessel retained and one litre consumed");
        s.Inventory.TryAdd("raw-meat",1,out _);var spoiled=s.Inventory.Items.First(i=>i.Item=="raw-meat");spoiled.AgeDays=5;Check(!SurvivalCrafting.Craft(s,craft,true,out _),"Cooking cannot sanitize spoiled meat");
        foreach(var r in SurvivalCrafting.Recipes){Check(r.Materials.All(m=>SurvivalItems.TryGet(m.item,out _)),"Recipe inputs exist "+r.Id);Check(SurvivalItems.TryGet(r.Output,out _),"Recipe output exists "+r.Id);}
        Console.WriteLine("PASS "+checks+" survival/inventory/calendar assertions");
    }
}
