using System;
using System.Linq;
using WorldGen.Core;
using WorldGen.Serialization;

static class PortableCraftingTests
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
        var settings=new PlaySettings();V ground=new V(1,0,0);
        Possession Make(PossessionKind kind)
        {
            var p=PortableTechnology.Item(kind)!=null?PortableTechnology.Create(kind,ground):null;
            if(p==null)p=new Possession{Id=Guid.NewGuid().ToString("N"),Kind=kind,Position=ground,WidthM=.04,HeightM=1.8,DepthM=.04,MassKg=.8,CellsWide=1,CellsHigh=18,WoodSoundness=1,WoodStraightness=1,EdgeSharpness=.9,Variant=ToolStone.Encode(ToolStoneMaterial.Chert,0)};
            return p;
        }
        PlayerInventory Fixture(PortableRecipe recipe)
        {
            var state=new PlayerInventory();state.Items.Add(Make(recipe.Primary[0]));
            foreach(var need in recipe.Materials)for(int i=0;i<need.Count;i++)state.Items.Add(Make(need.Kinds[0]));
            foreach(var kind in new[]{PossessionKind.SmallRock,PossessionKind.HaftedKnife,PossessionKind.BoneAwl,PossessionKind.BoneNeedle})state.Items.Add(Make(kind));
            return state;
        }
        Check(PortableTechnology.Items.Select(p=>p.Kind).Distinct().Count()==PortableTechnology.Items.Length,"Unique item definitions");
        Check(PortableCrafting.Recipes.Select(r=>r.Id).Distinct().Count()==PortableCrafting.Recipes.Length,"Unique actions");
        Check(PortableTechnology.Named("")==null,"Empty command does not match aliases");
        foreach(var def in PortableTechnology.Items)
        {var p=Make(def.Kind);new PlayerInventory{Items={p}}.Validate();Check(PortableTechnology.Named(def.Name)==def,"Item command resolves "+def.Name);}
        foreach(var recipe in PortableCrafting.Recipes)
        {
            var state=Fixture(recipe);var primary=state.Items[0];string id=primary.Id;double mass=state.Items.Sum(p=>p.MassKg);
            Check(PortableCrafting.Plan(state,state.Items,recipe,id,out var plan,out string reason),recipe.Id+": "+reason);
            Check(!plan.Ingredients.Intersect(plan.Tools).Any(),"Reusable tools are not consumed: "+recipe.Id);
            var job=PortableCrafting.Start(state,plan);StoneCrafting.Advance(state,job,.05,settings);
            Check(job.Fraction>0&&job.Fraction<1,"Timed work: "+recipe.Id);
            var saved=WorldJson.Read<PlayerInventory>(WorldJson.Write(state));saved.Validate();var savedJob=saved.Crafts[0];
            Check(savedJob.WorkSeconds==job.WorkSeconds&&savedJob.RecipeId==recipe.Id,"Save partial multi-input project: "+recipe.Id);
            savedJob.Running=false;double before=savedJob.WorkSeconds;StoneCrafting.Advance(saved,savedJob,10,settings);Check(savedJob.WorkSeconds==before,"Pause: "+recipe.Id);savedJob.Running=true;
            var output=StoneCrafting.Advance(saved,savedJob,100000,settings);saved.Validate();
            Check(output!=null&&output.Kind==recipe.Output&&output.Id==id,"Transform original item: "+recipe.Id);
            Check(plan.Tools.All(t=>saved.Find(t)!=null)&&plan.Ingredients.Skip(1).All(t=>saved.Find(t)==null),"Exact consumption: "+recipe.Id);
            Check(saved.Items.Sum(p=>p.MassKg)<=mass&&saved.Crafts.Count==0,"No mass creation or leftover project: "+recipe.Id);
        }
        var recipePouch=PortableCrafting.Recipe("hide-pouch");var source=Fixture(recipePouch);var hide=source.Items[0];hide.AppearanceSpecies="elk";hide.AppearanceCoat=2;hide.SourceAnimalId=991;
        hide.MaterialOrigins=new(){new MaterialOrigin{Species="elk",Coat=2,AnimalId=991}};
        Check(PortableCrafting.Plan(source,source.Items,recipePouch,hide.Id,out var pouchPlan,out _),"Pouch plans");
        var heldJob=PortableCrafting.Start(source,pouchPlan);var missing=source.Find(pouchPlan.Ingredients[1]);source.Items.Remove(missing);
        Check(StoneCrafting.Advance(source,heldJob,10000,settings)==null&&!heldJob.Running&&hide.Kind==PossessionKind.WorkedHide,"Missing reserved material pauses without consumption");source.Items.Add(missing);heldJob.Running=true;
        var pouch=StoneCrafting.Advance(source,heldJob,10000,settings);Check(pouch.AppearanceSpecies=="elk"&&pouch.AppearanceCoat==2&&pouch.SourceAnimalId==991&&pouch.Name.StartsWith("Elk"),"Hide origin survives crafting");
        var clone=source.Copy();clone.Find(pouch.Id).MaterialOrigins[0].Species="deer";Check(pouch.MaterialOrigins[0].Species=="elk","Deep copied provenance");
        var slots=new PlayerInventory();var pack=Make(PossessionKind.HidePack);var satchel=Make(PossessionKind.HideSatchel);var belt=Make(PossessionKind.FibreBelt);var knife=Make(PossessionKind.HaftedKnife);
        slots.Items.AddRange(new[]{pack,satchel,belt,knife});
        Check(slots.Move(pack.Id,PossessionPlace.Back,"",0,0,false,out _)&&slots.Move(satchel.Id,PossessionPlace.Shoulder,"",0,0,false,out _)&&slots.Move(belt.Id,PossessionPlace.Waist,"",0,0,false,out _),"Independent carrier attachments");
        Check(slots.Move(knife.Id,PossessionPlace.Container,belt.Id,0,0,false,out _),"A full-size knife fits a belt attachment");slots.Validate();
        var spare=Make(PossessionKind.HidePack);slots.Items.Add(spare);Check(!slots.Move(spare.Id,PossessionPlace.Back,"",0,0,false,out _),"One back carrier at a time");
        Check(!slots.Move(belt.Id,PossessionPlace.Container,pack.Id,0,0,false,out _),"Loaded container nesting rejected");
        var sheath=Make(PossessionKind.KnifeSheath);slots.Items.Add(sheath);
        Check(slots.Move(knife.Id,PossessionPlace.Container,sheath.Id,0,0,false,out _)&&slots.Move(sheath.Id,PossessionPlace.Container,belt.Id,1,0,false,out _),"Filled sheath attaches to belt without allowing loaded bags to nest");
        Check(!PortableTechnology.Holds(belt,Make(PossessionKind.SmallRock)),"Loose rocks do not attach to belt loops");
        Check(slots.Move(knife.Id,PossessionPlace.RightHand,"",0,0,false,out _)&&knife.BoundContainerId==sheath.Id,"Drawing a tool remembers its sheath");
        Check(slots.Stow(knife.Id,out _)&&knife.ContainerId==sheath.Id,"Stowing returns a tool to its original worn sheath");
        var holding=new PlayerInventory();var heldPack=Make(PossessionKind.HidePack);var heldTool=Make(PossessionKind.HaftedKnife);holding.Items.AddRange(new[]{heldPack,heldTool});holding.Hold(heldPack.Id,out _);holding.Hold(heldTool.Id,out _);
        Check(!holding.Stow(heldTool.Id,out _),"A held bag does not become automatic stow space");
        var handState=CraftEquipmentState.Capture(holding,new[]{heldTool.Id});handState.Validate();var equipmentCopy=WorldJson.Read<CraftEquipmentState>(WorldJson.Write(handState));Check(equipmentCopy.PreviousHands.Count==2&&equipmentCopy.Borrowed.Count==1,"Borrowed tool and original hands save independently");
        var quiver=Make(PossessionKind.Quiver);var arrow=Make(PossessionKind.Arrow);slots.Items.AddRange(new[]{quiver,arrow});Check(slots.Move(arrow.Id,PossessionPlace.Container,quiver.Id,0,0,false,out _),"Arrows fit quiver");Check(!slots.CanMove(knife.Id,PossessionPlace.Container,quiver.Id,1,0,false,out _),"Quiver rejects unrelated tools");slots.Validate();
        var reachable=new System.Collections.Generic.HashSet<PossessionKind>(new[]{PossessionKind.SmallRock,PossessionKind.LargeRock,PossessionKind.StoneFlake,PossessionKind.Branch,PossessionKind.TrimmedPole,PossessionKind.RawFibre,PossessionKind.BarkSheet,PossessionKind.ReedBundle,PossessionKind.RawHide,PossessionKind.Sinew,PossessionKind.Feather,PossessionKind.Bone,PossessionKind.Antler});
        bool changed;do{changed=false;foreach(var r in PortableCrafting.Recipes)if(r.Primary.Any(reachable.Contains)&&r.Materials.All(n=>n.Kinds.Any(reachable.Contains))&&r.Tools.All(c=>reachable.Any(k=>(PortableTechnology.Capabilities(Make(k))&c)!=0)))changed|=reachable.Add(r.Output);}while(changed);
        foreach(var rawFood in PortableTechnology.Items.Where(d=>d.Form==PortableForm.Food))reachable.Add(rawFood.Kind); // Gathered/looted, not manufactured.
        Check(PortableTechnology.Items.All(d=>reachable.Contains(d.Kind)),"Entire portable tree is reachable from raw materials and early cutting flakes");
        Console.WriteLine("PASS "+checks+" portable technology, multi-input crafting, provenance and storage checks; "+PortableTechnology.Items.Length+" items / "+PortableCrafting.Recipes.Length+" actions");
    }
}
