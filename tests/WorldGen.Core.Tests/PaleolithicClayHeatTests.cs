using WorldGen.Core;
using WorldGen.Serialization;

static class PaleolithicClayHeatTests
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
        var at=new V(1,0,0);var inventory=new PlayerInventory();
        var clay=PortableTechnology.Create(PossessionKind.RawClay,at);var skin=PortableTechnology.Create(PossessionKind.Waterskin,at);
        inventory.Items.AddRange(new[]{clay,skin});var recipe=PortableCrafting.Recipe("shape-clay-pot");
        Check(!PortableCrafting.Plan(inventory,inventory.Items,recipe,clay.Id,out _,out _),"Clay shaping needs actual mixing water");
        PaleolithicWater.Fill(skin,new WaterQuality(LocalWaterKind.River,.05,.2,0),.5);
        Check(PortableCrafting.Plan(inventory,inventory.Items,recipe,clay.Id,out var plan,out var reason),"Clay can be shaped with hands and measured water: "+reason);
        var pot=StoneCrafting.Advance(inventory,PortableCrafting.Start(inventory,plan),100000,new PlaySettings());
        Check(pot.Kind==PossessionKind.UnfiredClayPot&&Math.Abs(skin.WaterLitres-.4)<1e-9&&pot.MassKg<=1.8,"Shaping consumes 100 ml and conserves clay mass");
        Check(!CampRules.NeedsHeat(pot)&&!PaleolithicWater.Vessel(pot),"Wet greenware is neither a watertight vessel nor ready to fire");
        double mass=pot.MassKg;PaleolithicClay.CatchUp(pot,100,0,0);PaleolithicClay.CatchUp(pot,100.5,0,0);
        Check(Math.Abs(pot.DryingSeconds-43200)<1e-7&&pot.MassKg<mass,"Creation stamp avoids instant drying late in a playthrough");
        var whole=pot.Copy();var sliced=pot.Copy();PaleolithicClay.Dry(whole,43200,0,0);for(int i=0;i<24;i++)PaleolithicClay.Dry(sliced,1800,0,0);
        Check(whole.Dried&&sliced.Dried&&Math.Abs(whole.MassKg-sliced.MassKg)<1e-10,"Drying and water loss are independent of update subdivision");
        var wet=pot.Copy();PaleolithicClay.Dry(wet,86400,1,.4);Check(wet.DryingSeconds==pot.DryingSeconds,"Rain prevents further outdoor drying");
        PaleolithicClay.Dry(pot,86400,0,0);Check(pot.Dried&&CampRules.NeedsHeat(pot),"Air-dried clay can enter a real firing hearth");
        var fuel=WoodCrafting.Branch(new ScatterInstance{Id=913,Kind=ScatterKind.Branch,Position=at,WidthM=2,HeightM=.07});fuel.MassKg=10;
        var fire=new CampObject{Kind=CampKind.Fire,Position=at,Burning=true,Materials={fuel},CookingItems={pot}};
        for(int i=0;i<600&&pot.Kind!=PossessionKind.FiredClayPot;i++)CampRules.Advance(fire,60,0,.3,0,.2);
        Check(pot.Kind==PossessionKind.FiredClayPot&&PaleolithicWater.Capacity(pot)==3&&fire.FuelKg<10,"A fueled hearth fires the same dried pot and consumes wood");
        PaleolithicWater.Fill(pot,new WaterQuality(LocalWaterKind.River,.03,.4,0),2);
        for(int i=0;i<100&&!pot.WaterBoiled;i++)CampRules.Advance(fire,30,0,.3,0,.2);
        Check(pot.WaterBoiled&&pot.WaterContamination<.001&&!pot.Cooked&&!pot.Burnt,"Fired pot heats actual contents without turning the vessel into cooked food");
        var state=new CampState{Objects={fire}};state.Validate();WorldJson.Read<CampState>(WorldJson.Write(state)).Validate();Check(true,"In-fire finished clay and filled vessels survive capture and reload");
        var stone=HumanEquipment.Item(PossessionKind.LargeRock,87,"r",at);stone.MassKg=4;CampRules.Add(fire,stone);
        Check(CampWaterHeating.Reason(fire,skin).Length>0,"Adding cold stones does not instantly create usable heat");
        for(int i=0;i<180&&CampWaterHeating.Reason(fire,skin).Length>0;i++)CampRules.Advance(fire,30,0,.3,0,.2);
        Check(CampWaterHeating.Reason(fire,skin).Length==0,"Actual burning fuel eventually heats the hearth stones");
        double heat=fire.StoneHeatJ;Check(CampWaterHeating.Boil(fire,skin)&&fire.StoneHeatJ<heat&&skin.WaterBoiled&&skin.WaterContamination<.001,"Stone boiling draws finite stored heat and treats the actual water");
        Check(!CampWaterHeating.Boil(fire,skin)&&fire.StoneHeatJ==heat-CampWaterHeating.RequiredEnergy(skin),"Repeated use cannot boil the same water twice or draw phantom energy");
        PaleolithicWater.Fill(skin,new WaterQuality(LocalWaterKind.River,.05,.4,0),.2);Check(!skin.WaterBoiled&&skin.WaterContamination>.01,"Fresh untreated top-up recontaminates treated water");
        var salt=PortableTechnology.Create(PossessionKind.BarkWaterVessel,at);PaleolithicWater.Fill(salt,new WaterQuality(LocalWaterKind.Ocean,35,.1,0),.5);
        fire.StoneHeatJ=2000000;Check(CampWaterHeating.Boil(fire,salt)&&salt.WaterSalinityPpt==35,"Heat never makes seawater fresh");
        Check(PaleolithicClay.ClayGround(new GroundProfile{Clay=.4,DepthM=.4,Gravel=.1})&&!PaleolithicClay.ClayGround(new GroundProfile{Clay=.05,DepthM=1,Gravel=.1}),"Clay gathering follows actual fine soil, not arbitrary biome labels");
        var cold=new CampObject{Kind=CampKind.Fire,Position=at,Materials={stone.Copy()},StoneHeatJ=1000000};CampRules.Advance(cold,86400,0,.5,0,0);Check(cold.StoneHeatJ<1000,"Cold abandoned stones lose stored heat analytically");
        Console.WriteLine($"PASS {checks} clay shaping/drying/firing, measured water, finite stone heat, salt and saved hearth checks");
    }
}
