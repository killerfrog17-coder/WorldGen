using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldGen.Core
{
    public static class PaleolithicProcessing
    {
        public static readonly PortableItem[] Items={
            new PortableItem{Kind=PossessionKind.Grindstone,Name="Pecked grinding stone",Form=PortableForm.GrindingStone,Width=.24,Height=.08,Depth=.20,Mass=3,Capability=CraftCapability.Grind|CraftCapability.GrindingSurface,Aliases="grindstone|grinding stone|quern"},
            new PortableItem{Kind=PossessionKind.PlantMeal,Name="Ground plant food",Form=PortableForm.Food,Width=.09,Height=.08,Depth=.09,Mass=.2,Aliases="meal|plant meal|ground food"},
            new PortableItem{Kind=PossessionKind.PlantCake,Name="Uncooked plant cake",Form=PortableForm.Food,Width=.13,Height=.035,Depth=.13,Mass=.3,Aliases="plant cake|cake|dough"}
        };
        public static readonly PortableRecipe[] Recipes={
            new PortableRecipe{Id="peck-grindstone",Name="Peck a grinding surface",Output=PossessionKind.Grindstone,Primary=new[]{PossessionKind.SmallRock,PossessionKind.LargeRock},Tools=new[]{CraftCapability.Strike},Materials=Array.Empty<CraftMaterialNeed>(),WorkSeconds=1800,Yield=.9,WholeStone=true,MinMassKg=1.5,MinWidthM=.15},
            new PortableRecipe{Id="grind-nuts",Name="Grind nuts into meal",Output=PossessionKind.PlantMeal,Primary=new[]{PossessionKind.Nuts},Tools=new[]{CraftCapability.GrindingSurface,CraftCapability.Strike},Materials=Array.Empty<CraftMaterialNeed>(),WorkSeconds=300,Yield=.97,PreserveNutrition=true,DistinctTools=true},
            new PortableRecipe{Id="grind-roots",Name="Pound roots into mash",Output=PossessionKind.PlantMeal,Primary=new[]{PossessionKind.EdibleRoot},Tools=new[]{CraftCapability.GrindingSurface,CraftCapability.Strike},Materials=Array.Empty<CraftMaterialNeed>(),WorkSeconds=600,Yield=.95,PreserveNutrition=true,DistinctTools=true},
            new PortableRecipe{Id="plant-cake",Name="Mix and shape a plant cake",Output=PossessionKind.PlantCake,Primary=new[]{PossessionKind.PlantMeal},Tools=Array.Empty<CraftCapability>(),Materials=Array.Empty<CraftMaterialNeed>(),WorkSeconds=180,Yield=.98,WaterLitres=.08,PreserveNutrition=true}
        };
        public readonly struct FoodBatch
        {
            public readonly double Calories,Water,Toxin,Delay,Contamination;
            public readonly string Species;public readonly PossessionKind Source;
            public FoodBatch(double calories,double water,double toxin,double delay,double contamination,string species,PossessionKind source)
            {Calories=calories;Water=water;Toxin=toxin;Delay=delay;Contamination=contamination;Species=species;Source=source;}
        }
        public static FoodBatch Nutrition(IEnumerable<Possession> ingredients,double yield,Possession vessel,double waterAdded)
        {
            var food=ingredients.Where(Foraging.Food).ToArray();double water=food.Sum(p=>p.MassKg*p.FoodWaterPerKg)*yield+waterAdded;
            double contamination=food.Sum(p=>p.Cooked?0:p.MassKg*p.FoodWaterPerKg*p.FoodWaterContamination)*yield+waterAdded*(vessel?.WaterContamination??0);
            var first=food.FirstOrDefault();
            return new FoodBatch(food.Sum(p=>p.MassKg*p.FoodKcalPerKg*(p.Burnt?.5:1))*yield,water,food.Sum(p=>p.MassKg*p.FoodToxicity)*yield,food.Select(p=>p.FoodDelayHours).DefaultIfEmpty(0).Max(),water>0?contamination/water:0,first?.FoodSpecies??"",first==null?default:first.FoodSourceKind==PossessionKind.Nuts||first.FoodSourceKind==PossessionKind.EdibleRoot?first.FoodSourceKind:first.Kind);
        }
        public static void ApplyNutrition(Possession output,FoodBatch batch)
        {
            output.FoodKcalPerKg=batch.Calories/output.MassKg;output.FoodWaterPerKg=D.Clamp(batch.Water/output.MassKg);output.FoodToxicity=D.Clamp(batch.Toxin/output.MassKg);
            output.FoodWaterContamination=batch.Contamination;output.FoodDelayHours=batch.Delay;output.FoodSpecies=batch.Species;output.FoodSourceKind=batch.Source;
            output.Cooked=output.Burnt=output.Dried=output.Smoked=false;output.CookingSeconds=output.DryingSeconds=0;
        }
    }
}
