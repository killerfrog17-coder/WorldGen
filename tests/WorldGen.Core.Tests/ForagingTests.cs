using System;
using System.Collections.Generic;
using System.Linq;
using WorldGen.Core;
using WorldGen.Presentation;
using WorldGen.Serialization;

static class ForagingTests
{
    public static void Run()
    {
        int checks=0;void Check(bool value,string text){checks++;if(!value)throw new Exception(text);}
        V at=new V(1,0,0);
        var reed=new MidPlantSpecies{Name="Test reeds",MorphologyCode=(int)MidGrowthForm.ReedStand<<27,Form=MidGrowthForm.ReedStand};
        var bundle=Foraging.Products(reed,1).Single();Check(bundle.Kind==PossessionKind.ReedBundle&&bundle.Available(0,50,-10),"Reeds supply weaving material outside the fruit season");
        var bush=new MidPlantSpecies{Name="Test bush",MorphologyCode=1234,Form=MidGrowthForm.LeafyBush};
        Check(Foraging.Products(bush,1).Any(p=>p.Kind==PossessionKind.RawFibre),"Woody stems supply fibre without digging");
        var mushroom=new GroundSpecialist{Name="Test fungus",MorphologyCode=((int)SpecialistForm.Fungus<<24)|1234,Form=SpecialistForm.Fungus};
        var a=Foraging.Products(mushroom).Single();var b=Foraging.Products(mushroom).Single();Check(a.MassKg==b.MassKg&&a.Toxicity==b.Toxicity,"Species food traits are deterministic");
        Check(a.KcalPerKg>=220&&a.KcalPerKg<=340&&a.MassKg<.11,"Mushrooms are modest calorie fruiting bodies");
        var seasonal=new ForageProduct{Kind=PossessionKind.Berries,SeasonCentre=.6,SeasonWidth=.1};
        Check(seasonal.Available(.6,50,15)&&!seasonal.Available(.1,50,15),"Temperate fruit has a bounded harvest season");
        Check(seasonal.Available(.1,-50,15)&&!seasonal.Available(.6,-50,15),"Southern season is reversed");
        Check(!seasonal.Available(.6,50,-10),"Frozen fruit season rejects fresh picking");
        var fruit=PortableTechnology.Create(PossessionKind.Berries,at);fruit.FoodSpecies="Test berry";var body=new PlayerSurvival{Hunger=70,Thirst=60,Energy=50};
        double mass=fruit.MassKg;double bite=Foraging.Consume(body,fruit,true);
        Check(Math.Abs(bite-.005)<1e-9&&Math.Abs(fruit.MassKg-(mass-.005))<1e-9,"Sample consumes actual small food mass");
        Check(body.Hunger<70&&body.Thirst<60&&body.Energy>50,"Food mass restores calorie and water amounts");
        Check(body.Hunger>69,"A tiny berry sample is not a full meal");
        var poison=PortableTechnology.Create(PossessionKind.Mushroom,at);poison.FoodToxicity=1;poison.FoodDelayHours=8;body=new PlayerSurvival{Hunger=50,Thirst=20};
        Foraging.Consume(body,poison,true);Check(body.Health==100&&body.FoodExposures.Count==1,"Toxic sample can have delayed symptoms");
        Foraging.AdvancePoison(body,7*3600);Check(body.Health==100,"No premature mushroom safety signal or immediate damage");
        var copy=body.Copy();Foraging.AdvancePoison(copy,5*3600);Check(copy.Health<100&&body.Health==100,"Saved exposure copies are independent and later cause harm");
        body=new PlayerSurvival{Hunger=90};var meat=PortableTechnology.Create(PossessionKind.RawMeat,at);Foraging.Consume(body,meat,false);Check(body.Hunger<80&&body.Health==100&&body.FoodExposures==null,"Raw meat is nourishing without raw-meat penalties");
        var loot=Foraging.AnimalLoot(42,"deer","ground",70,at,2,123,true,true,true);
        Check(Math.Abs(loot.Where(Foraging.Food).Where(p=>p.Kind==PossessionKind.RawMeat).Sum(p=>p.MassKg)-22.4)<1e-7,"Meat yield scales with body mass");
        Check(loot.Any(p=>p.Kind==PossessionKind.RawHide&&p.AppearanceSpecies=="deer"&&p.AppearanceCoat==2&&p.SourceAnimalId==42),"Hide retains killed animal coat and identity");
        Check(loot.Any(p=>p.Kind==PossessionKind.Bone)&&loot.Any(p=>p.Kind==PossessionKind.Sinew)&&loot.Any(p=>p.Kind==PossessionKind.Antler),"Corpse provides craft materials");
        var inventory=new PlayerInventory{Items=loot};inventory.Validate();Check(inventory.CarriedMassKg==0,"Corpse contents do not weigh down the player");
        var hide=loot.First(p=>p.Kind==PossessionKind.RawHide);Check(inventory.Hold(hide.Id,out _)&&inventory.CarriedMassKg==hide.MassKg,"Taking loot moves the original item into a hand");
        var bag=PortableTechnology.Create(PossessionKind.HidePack,at);bag.Place=PossessionPlace.Back;inventory.Items.Add(bag);var flesh=loot.First(p=>p.Kind==PossessionKind.RawMeat);Check(inventory.Stow(flesh.Id,out _)&&flesh.Place==PossessionPlace.Container,"Loot can go directly to a worn carrier");
        inventory.ToolResources.Add(new NaturalToolRecord{Id="loot-42",Harvests=1});var session=new PlaySession{Inventory=inventory,Survival=copy};session.Validate();
        var restored=WorldJson.Read<PlaySession>(WorldJson.Write(session));restored.Validate();
        Check(restored.Inventory.Items.Any(p=>p.Place==PossessionPlace.Corpse&&p.CorpseOwner==42)&&restored.Inventory.ToolResources.Single().Harvests==1,"Unclaimed loot and generation ledger survive saves");
        Check(restored.Survival.FoodExposures!=null,"Food exposure state survives serialization");
        var whale=Foraging.AnimalLoot(3,"whale","ground",80000,at,0,0,false,true,false);Check(whale.Count<100&&whale.Sum(p=>p.MassKg)<80000,"Very large carcass yields bounded physical slabs without thousands of objects or excess mass");
        var fish=Foraging.AnimalLoot(7,"small-fish","water",.2,at,0,0,false,false,false);Check(fish.Any(p=>p.Kind==PossessionKind.RawFish)&&!fish.Any(p=>p.Kind==PossessionKind.RawHide),"Fish loot differs from a furred mammal");
        foreach(var kind in new[]{PossessionKind.Berries,PossessionKind.Fruit,PossessionKind.Nuts,PossessionKind.Mushroom,PossessionKind.EdibleRoot,PossessionKind.RawMeat,PossessionKind.RawFish,PossessionKind.AnimalFat})
        {var food=PortableTechnology.Create(kind,at);Foraging.ValidateFood(food);Check(Foraging.Food(food)&&!ItemUseRules.Profile(food).Weapon&&food.FoodKcalPerKg>0,kind+" is edible, valid and not a weapon");}
        var lethal=PortableTechnology.Create(PossessionKind.Mushroom,at);Foraging.Resize(lethal,.25);lethal.FoodToxicity=1;lethal.FoodDelayHours=8;body=new PlayerSurvival();Foraging.Consume(body,lethal,false);Foraging.AdvancePoison(body,12*3600);Check(body.Dead&&body.DeathCause==PlayerDeathCause.Poison,"Severe delayed poisoning reaches the existing death system");
        Console.WriteLine("Foraging checks PASS "+checks);
    }
}
