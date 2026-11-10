using WorldGen.Core;
using WorldGen.Serialization;

static class PaleolithicBindersTests
{
    public static void Run()
    {
        int n=0;void Check(bool ok,string text){n++;if(!ok)throw new Exception(text);}
        var at=new V(1,0,0);var settings=new PlaySettings();var inventory=new PlayerInventory();
        var ochre=PortableTechnology.Create(PossessionKind.RawOchre,at);ochre.Tint=new V(.72,.22,.08);var mill=PortableTechnology.Create(PossessionKind.Grindstone,at);var hammer=HumanEquipment.Item(PossessionKind.SmallRock,883,"r",at);inventory.Items.AddRange(new[]{ochre,mill,hammer});
        Check(PortableCrafting.Plan(inventory,inventory.Items,PortableCrafting.Recipe("grind-ochre"),ochre.Id,out var plan,out var reason),"Actual ochre, grinding surface and separate pounding stone can produce pigment: "+reason);
        var powder=StoneCrafting.Advance(inventory,PortableCrafting.Start(inventory,plan),10000,settings);Check(powder.Kind==PossessionKind.OchrePowder&&powder.Tint.X==.72&&powder.MassKg<=.08,"Grinding conserves material and source pigment colour");
        var gum=PortableTechnology.Create(PossessionKind.PlantGum,at);var water=PortableTechnology.Create(PossessionKind.Waterskin,at);PaleolithicWater.Fill(water,new WaterQuality(LocalWaterKind.River,.08,0,0),.2);inventory.Items.AddRange(new[]{gum,water});
        Check(PortableCrafting.Plan(inventory,inventory.Items,PortableCrafting.Recipe("gum-ochre-glue"),gum.Id,out plan,out reason),"Compound gum glue uses actual gum, pigment and water: "+reason);
        var mixture=StoneCrafting.Advance(inventory,PortableCrafting.Start(inventory,plan),10000,settings);Check(mixture.Kind==PossessionKind.AdhesiveMixture&&mixture.AdhesiveGumBased&&Math.Abs(water.WaterLitres-.18)<1e-9,"Gum mixing consumes 20 ml while retaining the actual vessel");
        var spear=PortableTechnology.Create(PossessionKind.StoneTippedSpear,at);inventory.Items.Add(spear);Check(!PaleolithicBinders.Bond(spear,mixture,true),"Unheated mixture cannot masquerade as prepared hafting glue");
        var fuel=WoodCrafting.Branch(new ScatterInstance{Id=539,Kind=ScatterKind.Branch,Position=at,WidthM=2,HeightM=.06});fuel.MassKg=5;
        inventory.Items.Remove(mill);var fire=new CampObject{Kind=CampKind.Fire,Position=at,Burning=true,Materials={fuel,mill},CookingItems={mixture}};inventory.Items.Remove(mixture);
        for(int i=0;i<120&&mixture.Kind!=PossessionKind.CompoundAdhesive;i++)CampRules.Advance(fire,30,0,.4,0,.2);
        Check(mixture.Kind==PossessionKind.CompoundAdhesive&&mixture.MassKg<.1,"A fueled hearth heats/dehydrates the same actual adhesive mixture");fire.CookingItems.Remove(mixture);inventory.Items.Add(mixture);
        double mass=mixture.MassKg+spear.MassKg,remaining=mixture.MassKg;Check(!PaleolithicBinders.Bond(spear,mixture,false),"Hard pitch is warmed before application");
        Check(PaleolithicBinders.Bond(spear,mixture,true)&&spear.HaftBonded&&Math.Abs(mixture.MassKg-(remaining-.005))<1e-9&&Math.Abs(mixture.MassKg+spear.MassKg-mass)<1e-9,"Bonding transfers exactly five grams from the real glue batch into the original weapon");
        Check(!PaleolithicBinders.Bond(spear,mixture,true),"Repeated sealing cannot stack unlimited glue or bonuses");
        ItemMaintenance.Complete(spear,false);Check(spear.HaftBonded,"Renewing the binding retains the existing adhesive joint");
        var knife=PortableTechnology.Create(PossessionKind.HaftedKnife,at);inventory.Items.Add(knife);PaleolithicBinders.Bond(knife,mixture,true);ItemMaintenance.Complete(knife,true);Check(!knife.HaftBonded,"Replacing an actual haft requires bonding its new joint again");
        var pigment=PortableTechnology.Create(PossessionKind.OchrePowder,at);inventory.Items.Add(pigment);remaining=pigment.MassKg;double volume=water.WaterLitres;
        Check(PaleolithicBinders.Paint(spear,pigment,water)&&spear.PigmentMarked&&pigment.MassKg==remaining-.002&&Math.Abs(water.WaterLitres-(volume-.005))<1e-9,"Gear decoration consumes two grams of actual ochre and five ml water");
        Check(!PaleolithicBinders.Paint(spear,pigment,water)&&!PaleolithicBinders.Paint(PortableTechnology.Create(PossessionKind.Nuts,at),pigment,water),"Paint application neither stacks invisibly nor turns food into decorated gear");
        var conifer=new TreeSpecies{Name="Test conifer",Form=TreeGrowthForm.NeedleCrown,MorphologyCode=(int)TreeGrowthForm.NeedleCrown<<28};var broad=new TreeSpecies{Name="Test gum tree",Form=TreeGrowthForm.BroadCrown,MorphologyCode=4<<8};
        Check(Foraging.Products(conifer).Any(p=>p.Kind==PossessionKind.PlantResin)&&Foraging.Products(broad).Any(p=>p.Kind==PossessionKind.PlantGum),"Actual inherited tree products expose resin and gum through normal gathering");
        var product=Foraging.Products(broad).First(p=>p.Kind==PossessionKind.PlantGum);var gathered=Foraging.Gather(product,at);Check(gathered.MassKg==product.MassKg&&!Foraging.Food(gathered)&&gathered.Name.Contains("Test gum tree"),"Gathering preserves tree identity and never treats adhesive gum as generic food");
        Check(PaleolithicBinders.OchreGround(new GroundProfile{DepthM=.3,Iron=.6,Clay=.3,Gravel=.1})&&!PaleolithicBinders.OchreGround(new GroundProfile{DepthM=.3,Iron=.1,Clay=.3}),"Mineral gathering depends on iron-rich fine soil");
        fire.Burning=false;fire.AshKg=.06;var coal=PaleolithicBinders.CollectCharcoal(fire,at);Check(coal!=null&&Math.Abs(fire.AshKg+coal.MassKg-.06)<1e-10,"Charcoal gathering subtracts the actual hearth residue mass");
        inventory.Validate();var restored=WorldJson.Read<PlayerInventory>(WorldJson.Write(inventory));restored.Validate();var camps=WorldJson.Read<CampState>(WorldJson.Write(new CampState{Objects={fire}}));camps.Validate();Check(restored.Find(spear.Id).HaftBonded&&restored.Find(spear.Id).PigmentMarked,"Glue and applied pigment persist through full inventory/camp capture");
        Console.WriteLine($"PASS {n} real-source pigments, gum/resin, heated adhesives, measured application and ownership checks");
    }
}
