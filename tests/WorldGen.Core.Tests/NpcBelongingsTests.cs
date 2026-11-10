using System;
using System.Linq;
using WorldGen.Core;

static class NpcBelongingsTests
{
    public static void Run()
    {
        int n=0;void Check(bool ok,string why){if(!ok)throw new Exception(why);n++;}
        var person=new ForagerPersonSample{Id=191,Name="Tester",Household=2,AgeYears=30};var band=new ForagerBandSample{Id=7,Techniques=Enumerable.Repeat(true,43).ToArray()};
        var stock=NpcBelongings.Seed(person,band,new V(1,0,0));stock.Validate();int count=stock.Items.Count;var ids=stock.Items.Select(p=>p.Id).OrderBy(x=>x).ToArray();
        Check(count>3&&stock.Items.All(p=>p.OwnerPerson==191&&p.OwnerGroup==7),"Initial tools and containers have persistent personal/group ownership");
        Check(stock.Items.Any(p=>p.Kind==PossessionKind.StoneFlake&&WoodCrafting.Cutter(p)&&p.MassKg<.1),"A starting cutting flake has a working edge and sensible physical size");
        var initial=NpcBelongings.Seed(person,band,new V(1,0,0));NpcBelongings.PrepareInitial(initial,"SpearReady1",new V(0,1,0));initial.Validate();
        Check(initial.Items.Count==count&&initial.Items.Where(p=>p.Place==PossessionPlace.Ground).All(p=>p.Position.Y==1),"The initial load conserves items and leaves spare uncarried tools at the supplied home position");
        foreach(string role in new[]{"WorkKnapping","ToolReady","SpearReady1","Eat","FuelTend","Drink",null})
        {NpcBelongings.Equip(stock,NpcBelongings.ForTask(stock,role),new V(1,0,0));stock.Validate();}
        Check(stock.Items.Count==count&&ids.SequenceEqual(stock.Items.Select(p=>p.Id).OrderBy(x=>x)),"Task changes conserve identities and create no fake meals or fuel");
        Check(NpcBelongings.ForTask(stock,"Eat").Length==0&&NpcBelongings.ForTask(stock,"FuelTend").Length==0,"Without real food/fuel there is nothing to eat or put on a fire");
        var tool=stock.Items.First();Check(NpcOwnership.Allowed(tool,191,7,2)&&!NpcOwnership.Allowed(tool,192,7,2)&&!NpcOwnership.Allowed(tool,193,8,3),"Personal tools do not become free to take within the same band or household");
        var food=PortableTechnology.Create(PossessionKind.Berries,new V(1,0,0));NpcOwnership.Assign(food,person,7);
        Check(NpcOwnership.Allowed(food,192,7,3,.9)&&!NpcOwnership.Allowed(food,192,7,3,.2),"Food-sharing norms grant different food access without tool ownership");
        var bed=new CampObject{Kind=CampKind.Bedding,NpcGroup=7,NpcHousehold=2};
        Check(NpcOwnership.BedAllowed(bed,7,2)&&!NpcOwnership.BedAllowed(bed,7,3)&&!NpcOwnership.BedAllowed(bed,8,2),"Family sleeping places respect actual group and household membership");
        var state=new HumanLocalState();state.People.Add(new HumanPersonMemory{Person=191,Group=7,Position=new V(1,0,0),Inventory=stock});state.Validate();var copy=state.Copy();copy.People[0].Inventory.Items.Clear();
        Check(state.People[0].Inventory.Items.Count==count,"NPC belongings deep-copy independently");
        var source=new PlayerInventory();var destination=new PlayerInventory();var bag=PortableTechnology.Create(PossessionKind.WovenBasket,new V(1,0,0));bag.Place=PossessionPlace.Ground;source.Items.Add(bag);
        var natural=HumanEquipment.Item(PossessionKind.SmallRock,92,"r",new V(1,0,0));natural.NaturalId=71;natural.Place=PossessionPlace.Container;natural.ContainerId=bag.Id;source.Items.Add(natural);source.CollectedNatural.Add(71);
        bag.Place=PossessionPlace.RightHand;NpcBelongings.TransferTree(source,destination,bag);source.Validate();destination.Validate();
        Check(source.Items.Count==0&&destination.Items.Count==2&&destination.CollectedNatural.Contains(71),"Picking up a filled container transfers its contents and natural-collection identity atomically");
        bag.Place=PossessionPlace.Ground;NpcBelongings.TransferTree(destination,source,bag);source.Validate();destination.Validate();
        Check(source.Items.Count==2&&destination.Items.Count==0&&source.Items.Single(p=>p.NaturalId==71).ContainerId==bag.Id,"Putting the same container back conserves its contents and packing");
        var bare=new PlayerInventory();var knife=HumanEquipment.Item(PossessionKind.StoneFlake,191,"packing",new V(1,0,0));
        Check(NpcBelongings.CanTake(bare,knife)&&bare.Items.Count==0&&knife.Place==PossessionPlace.Ground,"Packing capacity preview leaves an external real knife and its inventory unchanged");
        for(int k=0;k<2;k++){var rock=HumanEquipment.Item(PossessionKind.SmallRock,191,"packing-"+k,new V(1,0,0));rock.Place=k==0?PossessionPlace.RightHand:PossessionPlace.LeftHand;bare.Items.Add(rock);}
        Check(!NpcBelongings.CanTake(bare,knife)&&bare.Items.Count==2,"Full bare hands cannot claim capacity for another tool");
        var carrier=PortableTechnology.Create(PossessionKind.HidePack,new V(1,0,0));carrier.Place=PortableTechnology.Wear(carrier).Value;bare.Items.Add(carrier);
        Check(NpcBelongings.CanTake(bare,knife)&&bare.Items.Count==3&&knife.Place==PossessionPlace.Ground,"A real worn carrier can receive the small tool without changing it during preview");
        Check(NpcBelongings.PackingPriority(knife)<NpcBelongings.PackingPriority(bare.Items[0]),"A cutting edge is retrieved before spare loose stones during departure");
        Check(!stock.Items.Any(PaleolithicClothing.Garment),"Knowledge alone does not give a warm-climate culture a uniform fur outfit");
        band.RoutineTechniques=Enumerable.Repeat(true,43).ToArray();
        var dressed=NpcBelongings.Seed(person,band,new V(1,0,0));dressed.Validate();
        Check(PaleolithicClothing.Insulation(dressed)>.85&&dressed.Items.Count(PaleolithicClothing.Garment)==4,"A band that actually uses insulated clothing starts with owned, wearable cold-weather gear");
        var child=new ForagerPersonSample{Id=192,Name="Child",Household=2,AgeYears=5};var childStock=NpcBelongings.Seed(child,band,new V(1,0,0));childStock.Validate();
        Check(childStock.Items.Count==4&&childStock.Items.All(PaleolithicClothing.Garment)&&childStock.CarriedMassKg<dressed.Items.Where(PaleolithicClothing.Garment).Sum(p=>p.MassKg),"Young children receive scaled clothing without adult weapons or task inventories");
        Console.WriteLine("NPC belongings checks PASS: "+n);
    }
}
