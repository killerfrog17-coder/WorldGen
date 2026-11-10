using WorldGen.Core;
using WorldGen.Serialization;

static class PaleolithicProcessingTests
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        var point=new V(1,0,0);var settings=new PlaySettings();var inventory=new PlayerInventory();
        var stone=new Possession{Id="granite",Kind=PossessionKind.SmallRock,Position=point,WidthM=.18,HeightM=.09,DepthM=.14,MassKg=1.8,CellsWide=2,CellsHigh=1,Variant=ToolStone.Encode(ToolStoneMaterial.Granite,0)};
        var hammer=HumanEquipment.Item(PossessionKind.SmallRock,17,"r",point);inventory.Items.AddRange(new[]{stone,hammer});
        Check(!PortableCrafting.Plan(inventory,inventory.Items,PortableCrafting.Recipe("handaxe"),stone.Id,out _,out _),"Whole-stone processing does not make granite knappable");
        Check(PortableCrafting.Plan(inventory,inventory.Items,PortableCrafting.Recipe("peck-grindstone"),stone.Id,out var plan,out var reason),"A nonflaking granite cobble can be pecked into a grinding surface: "+reason);
        var job=PortableCrafting.Start(inventory,plan);var mill=StoneCrafting.Advance(inventory,job,100000,settings);inventory.Validate();
        Check(mill.Kind==PossessionKind.Grindstone&&mill.Variant==stone.Variant&&mill.MassKg<=1.8&&mill.WidthM<=.18&&mill.HeightM<=.09,"Pecking retains actual stone material and cannot grow a bigger tool from a small stone");
        Check(ItemUseRules.Profile(mill).Primary==ItemPrimaryUse.Blunt,"A grinding stone is still a physical blunt object");
        var roots=PortableTechnology.Create(PossessionKind.EdibleRoot,point);Foraging.Resize(roots,3);roots.FoodSpecies="Test plant";roots.FoodKcalPerKg=913;roots.FoodWaterPerKg=.76;roots.FoodToxicity=.3;roots.FoodDelayHours=4;
        inventory.Items.Add(roots);inventory.Items.Remove(hammer);
        Check(!PortableCrafting.Plan(inventory,inventory.Items,PortableCrafting.Recipe("grind-roots"),roots.Id,out _,out _),"A single stone cannot simultaneously be the mill and the separate pounding tool");
        inventory.Items.Add(hammer);double calories=roots.MassKg*roots.FoodKcalPerKg,water=roots.MassKg*roots.FoodWaterPerKg;
        Check(PortableCrafting.Plan(inventory,inventory.Items,PortableCrafting.Recipe("grind-roots"),roots.Id,out plan,out reason),"Separate pounding tool and surface permit grinding: "+reason);
        job=PortableCrafting.Start(inventory,plan);var meal=StoneCrafting.Advance(inventory,job,100000,settings);inventory.Validate();
        Check(Math.Abs(meal.MassKg*meal.FoodKcalPerKg-calories*.95)<1e-8&&Math.Abs(meal.MassKg*meal.FoodWaterPerKg-water*.95)<1e-8,"A multi-kilogram gathered batch retains its actual nutrition and water instead of receiving template calories");
        Check(meal.FoodSpecies=="Test plant"&&meal.FoodToxicity==.3&&meal.Name.Contains("roots"),"Processing preserves plant identity and does not magically detoxify it");
        var vessel=PortableTechnology.Create(PossessionKind.Waterskin,point);inventory.Items.Add(vessel);
        Check(!PortableCrafting.Plan(inventory,inventory.Items,PortableCrafting.Recipe("plant-cake"),meal.Id,out _,out _),"Cake mixing requires actual water, not merely an empty container");
        PaleolithicWater.Fill(vessel,new WaterQuality(LocalWaterKind.River,.08,.2,0),.5);
        Check(PortableCrafting.Plan(inventory,inventory.Items,PortableCrafting.Recipe("plant-cake"),meal.Id,out plan,out reason)&&plan.Tools.Contains(vessel.Id)&&!plan.Ingredients.Contains(vessel.Id),"The vessel is reserved as a reusable tool: "+reason);
        job=PortableCrafting.Start(inventory,plan);StoneCrafting.Advance(inventory,job,.05,settings);double progress=job.WorkSeconds;
        PaleolithicWater.Drain(vessel,100);
        Check(StoneCrafting.Advance(inventory,job,100000,settings)==null&&!job.Running&&job.WorkSeconds==progress&&meal.Kind==PossessionKind.PlantMeal,"Removing reserved water pauses the project without consuming its food");
        PaleolithicWater.Fill(vessel,new WaterQuality(LocalWaterKind.River,.08,.2,0),.5);PortableCrafting.Start(inventory,plan);
        double massBefore=inventory.Items.Sum(p=>p.MassKg),cakeCalories=meal.MassKg*meal.FoodKcalPerKg*.98;
        var cake=StoneCrafting.Advance(inventory,job,100000,settings);inventory.Validate();
        Check(cake.Kind==PossessionKind.PlantCake&&inventory.Find(vessel.Id)==vessel&&Math.Abs(vessel.WaterLitres-.42)<1e-10&&inventory.Items.Sum(p=>p.MassKg)<=massBefore,"Completion consumes exactly 80 ml, retains the same vessel and conserves physical mass");
        Check(Math.Abs(cake.MassKg*cake.FoodKcalPerKg-cakeCalories)<1e-8&&cake.FoodWaterContamination>0&&cake.Name.StartsWith("Test plant"),"Measured water dilutes food by its actual volume without creating calories or erasing contamination");
        var save=WorldJson.Read<PlayerInventory>(WorldJson.Write(inventory));save.Validate();Check(save.Find(cake.Id).FoodWaterContamination==cake.FoodWaterContamination&&save.Find(cake.Id).FoodSourceKind==PossessionKind.EdibleRoot,"Processed food identity and source-water hazard survive save/load");
        var raw=new PlayerSurvival{WaterDeficitLitres=1,MetabolismVersion=1,WaterExposureThreshold=100};var control=raw.Copy();var safeCake=cake.Copy();safeCake.FoodWaterContamination=0;
        Foraging.Consume(raw,cake,false);Foraging.Consume(control,safeCake,false);
        Check(raw.WaterExposureHazard>0&&control.WaterExposureHazard==0&&Math.Abs(raw.WaterDeficitLitres-control.WaterDeficitLitres)<1e-10,"Untreated mixing water retains exposure without hydrating twice");
        inventory.Items.Remove(cake);var fuel=WoodCrafting.Branch(new ScatterInstance{Id=713,Kind=ScatterKind.Branch,Position=point,WidthM=2,HeightM=.06});fuel.MassKg=8;
        var fire=new CampObject{Id="test-fire",Kind=CampKind.Fire,Position=point,Burning=true,Materials={fuel},CookingItems={cake}};
        double toxin=cake.FoodToxicity;for(int i=0;i<100&&!cake.Cooked;i++)CampRules.Advance(fire,60,0,.4,0,.2);
        Check(cake.Cooked&&!cake.Burnt&&cake.FoodToxicity==toxin&&!cake.Name.Contains("Uncooked"),"A real fueled campfire cooks the cake while plant toxins are retained");
        var cooked=new PlayerSurvival{WaterExposureThreshold=100};Foraging.Consume(cooked,cake,false);Check(cooked.WaterExposureHazard==0,"Cooking removes the raw mixing-water exposure route");
        Console.WriteLine($"PASS {checks} grinding, measured-water reservation, food mass/nutrient/poison and camp-cooking checks");
    }
}
