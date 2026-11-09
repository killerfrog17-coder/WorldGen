using System;
using System.Linq;
using WorldGen.Core;

static class NpcMaterialUseTests
{
    public static void Run()
    {
        int n=0;void Check(bool ok,string why){if(!ok)throw new Exception(why);n++;}
        var at=new V(1,0,0);var stock=new PlayerInventory();var fire=new CampObject{Kind=CampKind.Fire,Position=at};
        var branch=HumanEquipment.Item(PossessionKind.Branch,3,"fuel",at);branch.HeightM=1.8;branch.MassKg=.8;branch.Place=PossessionPlace.RightHand;stock.Items.Add(branch);
        double mass=branch.MassKg;Check(NpcMaterialUse.Feed(stock,fire,branch)&&stock.Items.Count==0&&Math.Abs(fire.FuelKg-mass)<1e-9,"Feeding a long real branch transfers its finite mass into snapped fuel pieces");
        Check(!NpcMaterialUse.Feed(stock,fire,branch)&&Math.Abs(fire.FuelKg-mass)<1e-9,"A consumed inventory object cannot be fed a second time");
        var spear=HumanEquipment.Item(PossessionKind.WoodenSpear,3,"weapon",at);spear.Place=PossessionPlace.RightHand;stock.Items.Add(spear);Check(!NpcMaterialUse.Feed(stock,fire,spear),"Fire tending does not destroy a valuable spear as generic wood");
        var tinder=PortableTechnology.Create(PossessionKind.DryGrass,at);tinder.Place=PossessionPlace.LeftHand;stock.Items.Add(tinder);fire.EmberSeconds=900;
        Check(NpcMaterialUse.Feed(stock,fire,tinder)&&fire.Burning,"Real fine material can relight existing hot coals without a fabricated kit");
        var meal=PortableTechnology.Create(PossessionKind.Berries,at);meal.Place=PossessionPlace.LeftHand;meal.MassKg=.2;meal.FoodKcalPerKg=600;meal.FoodWaterPerKg=.8;stock.Items.Add(meal);var needs=new PlayerSurvival();needs.Restore();PlayerMetabolism.Initialize(needs);double energy=needs.DigestingKcal;
        Check(NpcMaterialUse.Eat(stock,needs,meal)>.19&&!stock.Items.Contains(meal)&&needs.DigestingKcal>energy+119,"Eating consumes actual food mass and adds its actual calories");
        Check(!NpcMaterialUse.HasIgnitionKit(stock),"Technology knowledge alone is not a physical fire drill");
        var drill=PortableTechnology.Create(PossessionKind.HandDrillSet,at);stock.Items.Add(drill);Check(!NpcMaterialUse.HasIgnitionKit(stock),"An ignition kit lying on the ground is not being carried");stock.Hold(drill.Id,out _);Check(NpcMaterialUse.HasIgnitionKit(stock),"Actually holding the ignition kit enables cold fire starting");
        Console.WriteLine("NPC material use checks PASS: "+n);
    }
}
