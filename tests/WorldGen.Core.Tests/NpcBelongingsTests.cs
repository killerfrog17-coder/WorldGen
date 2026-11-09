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
        Console.WriteLine("NPC belongings checks PASS: "+n);
    }
}
