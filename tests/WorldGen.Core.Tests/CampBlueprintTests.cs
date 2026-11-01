using WorldGen.Core;
using WorldGen.Serialization;
static class CampBlueprintTests
{
    public static void Run()
    {
        int n=0;void Check(bool ok,string why){if(!ok)throw new Exception(why);n++;}
        var at=new V(1,0,0);var inventory=new PlayerInventory();
        Possession Source(CampBlueprint p,CampComponent c)
        {
            var item=c.Kg==0?new Possession{Id=Guid.NewGuid().ToString("N"),Kind=PossessionKind.Branch,Position=at}:PortableTechnology.Create(c.Kind==CampPartKind.Binding?PossessionKind.Cordage:c.Kind==CampPartKind.Cover?(p.Cover==CampCover.Hide?PossessionKind.WorkedHide:p.Cover==CampCover.Bark?PossessionKind.BarkSheet:PossessionKind.DryGrass):PossessionKind.Branch,at);
            item.NaturalId=0;item.WoodStraightness=.9;item.WoodSoundness=.9;
            if(c.Kg>0)item.MassKg=c.Kg*1.5;else{item.HeightM=c.LengthM+.25;item.WidthM=c.WidthM*1.2;item.DepthM=item.WidthM;item.MassKg=item.HeightM*item.WidthM*item.WidthM*450;}
            inventory.Items.Add(item);return item;
        }
        foreach(var design in Enum.GetValues<CampDesign>())foreach(var cover in Enum.GetValues<CampCover>())
        {
            var plan=CampBlueprints.Create(design,2,2,cover);var defs=CampBlueprints.Components(plan);Check(defs.Count>0&&defs.Select(c=>c.Key).Distinct().Count()==defs.Count,"Stable component IDs");
            double beforeMass=0;
            foreach(var c in defs)
            {
                var item=Source(plan,c);double before=inventory.Items.Sum(i=>i.MassKg)+plan.Parts.SelectMany(s=>s.Materials).Sum(i=>i.MassKg);
                if(c.Kg==0)Check(!CampBlueprints.Supply(plan,c.Key,inventory,item.Id,false,out _,out _),"Long wood needs cutting capability");
                Check(CampBlueprints.Supply(plan,c.Key,inventory,item.Id,true,out var offcut,out _),"Appropriate contribution accepted");
                double after=inventory.Items.Sum(i=>i.MassKg)+plan.Parts.SelectMany(s=>s.Materials).Sum(i=>i.MassKg);Check(Math.Abs(before-after)<1e-8,"Supply and offcuts conserve mass");
                Check(CampBlueprints.Work(plan,c.Key,30)==30&&!CampBlueprints.Done(plan,c),"Supplying material does not skip labour");
                var round=WorldJson.Read<CampBlueprint>(WorldJson.Write(plan));Check(Math.Abs(CampBlueprints.State(round,c).WorkSeconds-30)<1e-8,"Partial construction survives save");
                CampBlueprints.Work(plan,c.Key,10000);Check(CampBlueprints.Done(plan,c),"Labour completes component");beforeMass+=1;
            }
            Check(CampBlueprints.Complete(plan),"Full plan completes");
            var camp=new CampState();var obj=new CampObject{Kind=CampKind.Blueprint,Blueprint=plan,Position=at};camp.Objects.Add(obj);camp.Validate();var copy=camp.Copy();copy.Objects[0].Blueprint.Parts[0].Materials[0].MassKg*=.5;Check(obj.Blueprint.Parts[0].Materials[0].MassKg!=copy.Objects[0].Blueprint.Parts[0].Materials[0].MassKg,"Contributions deep copied");
            if(design==CampDesign.LeanTo||design==CampDesign.BrushHut)Check(CampBlueprints.Protection(plan,0,0,0,1).Rain>.7,"Completed shelter blocks precipitation");
            if(design==CampDesign.Windbreak)Check(CampBlueprints.Protection(plan,0,1,0,1).Wind>0&&CampBlueprints.Protection(plan,0,-1,0,1).Wind==0,"Windbreak is directional");
            if(design==CampDesign.MaterialStore)
            {
                var wood=new Possession{Id=Guid.NewGuid().ToString("N"),Kind=PossessionKind.Branch,Position=at};wood.HeightM=1.5;inventory.Items.Add(wood);Check(CampBlueprints.Deposit(obj,inventory,wood.Id,out _),"Store accepts actual inventory wood");Check(!inventory.Carried(wood),"Stored materials do not add carried load");CampBlueprints.ValidateContents(camp,inventory);inventory.Move(wood.Id,PossessionPlace.Ground,"",0,0,false,out _);
            }
            inventory.Items.Clear();
        }
        var small=CampBlueprints.Create(CampDesign.LeanTo,1.5,1.5);var big=CampBlueprints.Create(CampDesign.LeanTo,4,3);Check(CampBlueprints.Components(big).Count>CampBlueprints.Components(small).Count,"Larger footprint creates additional components");Check(CampBlueprints.Components(big).Where(c=>c.Kind==CampPartKind.Beam).All(c=>c.LengthM<2),"Wide roofs split beams across extra supports");
        var bounds=CampBlueprints.Create(CampDesign.DryingRack,99,99);Check(bounds.WidthM==3&&bounds.DepthM==.7,"Rack only sizes its length");
        var p=CampBlueprints.Create(CampDesign.LeanTo,2,2);var coverPart=CampBlueprints.Components(p).First(c=>c.Kind==CampPartKind.Cover);var bad=Source(p,coverPart);Check(!CampBlueprints.Supply(p,coverPart.Key,inventory,bad.Id,true,out _,out _),"Cannot put roof on before frame");
        var food=PortableTechnology.Create(PossessionKind.RawMeat,at);double calories=food.MassKg*food.FoodKcalPerKg;CampBlueprints.Dry(food,1000000,0,.3,3,20,true);Check(food.Dried&&food.Smoked&&Math.Abs(calories-food.MassKg*food.FoodKcalPerKg)<1e-7,"Drying preserves total calories and source identity");double mass=food.MassKg;CampBlueprints.Dry(food,1000000,0,.3,3,20,true);Check(food.MassKg==mass,"Finished food cannot repeatedly shrink or gain nutrition");
        var wet=PortableTechnology.Create(PossessionKind.RawMeat,at);CampBlueprints.Dry(wet,100000,4,.9,3,20,false);Check(!wet.Dried&&wet.DryingSeconds==0,"Rain stops drying");
        var fire=new CampObject{Kind=CampKind.Fire,Position=at,Burning=true};var stone=PortableTechnology.Create(PossessionKind.Cordage,at);stone.Kind=PossessionKind.SmallRock;stone.MassKg=.6;CampRules.Add(fire,stone);var fuel=PortableTechnology.Create(PossessionKind.KindlingBundle,at);fuel.MassKg=2;CampRules.Add(fire,fuel);CampRules.Advance(fire,3600,0,.3,1,1);Check(fire.Materials.Any(i=>i.Id==stone.Id&&i.MassKg==.6),"Fire stones are never consumed as fuel");
        var roofPlan=CampBlueprints.Create(CampDesign.LeanTo,2,2);
        foreach(var c in CampBlueprints.Components(roofPlan)){var supply=Source(roofPlan,c);CampBlueprints.Supply(roofPlan,c.Key,inventory,supply.Id,true,out _,out _);CampBlueprints.Work(roofPlan,c.Key,10000);}
        double protection=CampBlueprints.Protection(roofPlan,0,0,0,1).Rain;var extra=PortableTechnology.Create(PossessionKind.DryGrass,at);extra.MassKg=10;inventory.Items.Add(extra);double massBefore=inventory.Items.Sum(i=>i.MassKg)+roofPlan.Parts.SelectMany(s=>s.Materials).Sum(i=>i.MassKg);
        Check(CampBlueprints.Thicken(roofPlan,inventory,extra.Id,out _),"Completed roofs accept extra covering");Check(CampBlueprints.Protection(roofPlan,0,0,0,1).Rain>protection,"Extra covering improves weather protection");Check(Math.Abs(massBefore-inventory.Items.Sum(i=>i.MassKg)-roofPlan.Parts.SelectMany(s=>s.Materials).Sum(i=>i.MassKg))<1e-8,"Extra layers conserve material mass");
        var sloped=CampBlueprints.Create(CampDesign.LeanTo,2,2);sloped.FloorHeights=new[]{-.4,-.4,-.4,0,0,0,.4,.4,.4};var supports=CampBlueprints.Components(sloped).Where(c=>c.Kind==CampPartKind.Post).ToArray();Check(supports.Max(c=>c.LengthM)-supports.Min(c=>c.LengthM)>.7,"Footing survey changes required support lengths");Check(supports.All(c=>Math.Abs(c.Points[0].Y-CampBlueprints.FloorAt(sloped,c.Points[0].X,c.Points[0].Z)+.1)<1e-8),"Every support meets its own terrain footing");
        var weapon=new Possession{Kind=PossessionKind.WoodenSpear,HeightM=4,WidthM=.04,WoodSoundness=1};Check(!CampBlueprints.Suitable(roofPlan,CampBlueprints.Components(roofPlan)[0],weapon),"Automatic construction does not consume finished spears");
        var invalid=new CampState{Objects=new(){new CampObject{Kind=CampKind.Blueprint,Position=at,Blueprint=new CampBlueprint{WidthM=double.NaN}}}};bool rejected=false;try{invalid.Validate();}catch(ArgumentException){rejected=true;}Check(rejected,"Malformed plans rejected on load");
        Console.WriteLine("PASS "+n+" camp blueprint dimensions, construction, ownership, drying, weather and save checks");
    }
}

