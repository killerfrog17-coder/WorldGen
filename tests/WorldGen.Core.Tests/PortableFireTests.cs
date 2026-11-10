using WorldGen.Core;
using WorldGen.Serialization;

static class PortableFireTests
{
    public static void Run()
    {
        int n=0;void Check(bool ok,string why){n++;if(!ok)throw new Exception(why);}
        var at=new V(1,0,0);var torch=PortableTechnology.Create(PossessionKind.Torch,at);var inventory=new PlayerInventory{Items={torch}};
        Check(torch.FireRemainingSeconds==PortableFire.TorchSeconds&&!PortableFire.Burning(torch),"A new wrapped torch contains finite real fuel and begins unlit");
        Check(!PortableFire.Light(torch,false,20)&&PortableFire.Light(torch,true,20),"An actual ignition source is required");
        double mass=torch.MassKg;var whole=torch.Copy();var slices=torch.Copy();PortableFire.Advance(whole,20.02,0,0,false);for(int i=1;i<=20;i++)PortableFire.Advance(slices,20+i*.001,0,0,false);
        Check(Math.Abs(whole.FireRemainingSeconds-slices.FireRemainingSeconds)<1e-7&&Math.Abs(whole.MassKg-slices.MassKg)<1e-10&&whole.MassKg<mass,"Fuel duration and burnt mass agree under analytic and sliced catch-up");
        var windy=torch.Copy();PortableFire.Advance(windy,20.02,0,10,false);Check(windy.FireRemainingSeconds<whole.FireRemainingSeconds,"Exposed wind accelerates torch fuel use");
        var wet=torch.Copy();PortableFire.Advance(wet,20.001,3,0,false);Check(!PortableFire.Burning(wet)&&wet.FireRemainingSeconds>0,"Rain extinguishes the open torch without creating new fuel");
        var bag=PortableTechnology.Create(PossessionKind.CarryFrame,at);bag.Place=PossessionPlace.Back;inventory.Items.Add(bag);
        Check(!inventory.CanMove(torch.Id,PossessionPlace.Container,bag.Id,0,0,false,out _),"A burning torch cannot silently be packed inside a carrier");torch.FireLit=false;Check(inventory.CanMove(torch.Id,PossessionPlace.Container,bag.Id,0,0,false,out _),"An extinguished torch can be packed in a sufficiently long carrying frame");
        PortableFire.Light(torch,true,20);PortableFire.Advance(torch,21,0,0,false);Check(!PortableFire.Burning(torch)&&torch.FireRemainingSeconds==0&&torch.MassKg>0&&torch.FireFuelKg==0,"A spent torch retains its real handle rather than disappearing");
        Check(!PortableFire.Light(torch,true,21),"An exhausted torch cannot be relit infinitely");
        inventory.Items.Add(PortableTechnology.Create(PossessionKind.BarkStrips,at));inventory.Items.Add(PortableTechnology.Create(PossessionKind.BarkStrips,at));inventory.Items.Add(PortableTechnology.Create(PossessionKind.PlantResin,at));
        double before=inventory.Items.Sum(p=>p.MassKg);Check(PortableCrafting.Plan(inventory,inventory.Items,PortableCrafting.Recipe("refuel-torch"),torch.Id,out var plan,out var reason),"Rewrapping a spent torch requires real fresh fuel: "+reason);
        StoneCrafting.Advance(inventory,PortableCrafting.Start(inventory,plan),10000,new PlaySettings());Check(torch.FireRemainingSeconds==PortableFire.TorchSeconds&&torch.FireFuelKg>0&&inventory.Items.Sum(p=>p.MassKg)<=before,"Refuelling consumes the actual wrapping and resin without creating mass");
        var fire=new CampObject{Kind=CampKind.Fire,Position=at,Burning=true,AshKg=.02,EmberSeconds=1800};var carrier=PortableTechnology.Create(PossessionKind.EmberCarrier,at);var tinder=PortableTechnology.Create(PossessionKind.DryGrass,at);
        Check(!PortableFire.Burning(carrier)&&!PortableFire.Light(carrier,true,21),"An empty carrier has no imaginary live embers");before=carrier.MassKg+tinder.MassKg+fire.AshKg;double ash=fire.AshKg;
        Check(PortableFire.LoadEmbers(carrier,fire,tinder,21)&&PortableFire.Burning(carrier)&&fire.AshKg==ash-.005&&carrier.FireRemainingSeconds==PortableFire.EmberSeconds,"Filling transfers real hearth coals and a measured tinder nest");
        Check(Math.Abs(carrier.MassKg+tinder.MassKg+fire.AshKg-before)<1e-10,"Coals, tinder and carrier retain the same total physical mass");
        var newborn=new CampObject{Kind=CampKind.Fire,Position=at,Burning=true};Check(!PortableFire.LoadEmbers(PortableTechnology.Create(PossessionKind.EmberCarrier,at),newborn,tinder,21),"A newly lit fire cannot supply charcoal it has not produced");
        var target=new CampObject{Kind=CampKind.Fire,Position=at,Materials={PortableTechnology.Create(PossessionKind.TinderBundle,at),PortableTechnology.Create(PossessionKind.KindlingBundle,at)}};double coal=carrier.FireFuelKg;
        Check(PortableFire.Ignite(target,carrier)&&target.Burning&&carrier.FireFuelKg<coal,"A live carrier ignites prepared tinder/kindling and transfers some of its actual coals");
        var bare=new CampObject{Kind=CampKind.Fire,Position=at};Check(!PortableFire.Ignite(bare,carrier),"Carried fire does not bypass the required fire lay");
        var drowned=carrier.Copy();PortableFire.Quench(drowned);Check(!PortableFire.Burning(drowned)&&drowned.FireFuelKg==0&&drowned.FireRemainingSeconds==0,"Immersion ruins the live ember nest instead of preserving underwater fire");
        var sheltered=carrier.Copy();PortableFire.Advance(sheltered,21.1,30,10,true);Check(PortableFire.Burning(sheltered),"An insulated carrier stored inside a real bag shelters its coals");
        PortableFire.Advance(carrier,22,0,0,false);Check(!PortableFire.Burning(carrier)&&carrier.FireRemainingSeconds==0,"Transported embers eventually die instead of remaining a permanent ignition cheat");
        inventory.Items.Add(carrier);inventory.Validate();var saved=WorldJson.Read<PlayerInventory>(WorldJson.Write(inventory));saved.Validate();Check(saved.Find(torch.Id).FireFuelKg==torch.FireFuelKg,"Fuel, clock and extinguished/loaded state survive full saves");
        Console.WriteLine($"PASS {n} portable flame, finite fuel/mass, weather, packing, ember transfer and ignition checks");
    }
}
