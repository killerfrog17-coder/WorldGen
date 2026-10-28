using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    string CraftLabel(ItemCraftJob job)=>PortableCrafting.IsJob(job)?PortableCrafting.Recipe(job.RecipeId)?.Name??"Craft":WoodCrafting.Label(job.Action);
    Possession[] CraftAccessible()
    {
        RefreshInventoryNearby();return Inventory.Items.Where(AccessiblePossession).Concat(inventoryNearby.Where(n=>n.Natural).Select(n=>n.Item)).GroupBy(p=>p.Id).Select(g=>g.First()).ToArray();
    }
    bool PortablePlan(PortableRecipe recipe,string primaryId,out PortableCraftPlan plan,out string reason)
    {
        var available=CraftAccessible();plan=null;reason="Start with "+recipe.PrimaryName+".";
        foreach(var p in available.Where(p=>primaryId!=null?p.Id==primaryId:recipe.Accepts(p)))
        {if(PortableCrafting.Plan(Inventory,available,recipe,p.Id,out plan,out reason))return true;}
        return false;
    }
    void SetCraftWorkArea(Possession p,int index=0)
    {
        double angle=heading*Math.PI/180;p.Position=frame.Position(motion.X+Math.Sin(angle)*.8+index*.12,motion.Z+Math.Cos(angle)*.8);
        p.GroundOffsetM=0;p.GroundMoving=false;p.GroundRotation=new V(0,heading,PortableTechnology.Item(p.Kind)?.Long==true||WoodCrafting.Wood(p)?90:0);
    }
    bool StartPortableCraft(string recipeId,string primaryId=null)
    {
        var recipe=PortableCrafting.Recipe(recipeId);if(recipe==null)return false;
        PauseItemCraft(ActiveItemCraft);RefreshInventoryNearby(true);
        if(!PortablePlan(recipe,primaryId,out var plan,out var reason)){InventoryFeedback(reason);return false;}
        var involved=new HashSet<string>(plan.Ingredients.Concat(plan.Tools));
        var before=Inventory.Copy();var candidates=CraftAccessible().ToDictionary(p=>p.Id,p=>p);var naturals=inventoryNearby.Where(n=>n.Natural&&involved.Contains(n.Item.Id)).ToArray();
        foreach(var id in involved)if(Inventory.Find(id)==null)Inventory.Items.Add(candidates[id]);
        var equipment=PrepareWorkHands(new[]{plan.PrimaryId}.Concat(plan.Tools));
        int grounded=0;
        foreach(var p in Inventory.Items.Where(p=>involved.Contains(p.Id)&&(p.Place==PossessionPlace.RightHand||p.Place==PossessionPlace.LeftHand)))
        {p.Place=PossessionPlace.Ground;p.ContainerId="";SetCraftWorkArea(p,grounded++);}
        var primary=Inventory.Find(plan.PrimaryId);var tool=plan.Tools.Select(Inventory.Find).FirstOrDefault(p=>p.Hands==1);
        bool prepared=true;
        if(primary.Hands==2){if(primary.Place!=PossessionPlace.Ground){primary.Place=PossessionPlace.Ground;primary.ContainerId="";SetCraftWorkArea(primary);}if(tool!=null)prepared=Inventory.Move(tool.Id,PossessionPlace.RightHand,"",0,0,false,out reason);}
        else {prepared=Inventory.Move(primary.Id,tool==null?PossessionPlace.RightHand:PossessionPlace.LeftHand,"",0,0,false,out reason);if(prepared&&tool!=null)prepared=Inventory.Move(tool.Id,PossessionPlace.RightHand,"",0,0,false,out reason);}
        if(!prepared){PlayerRules.Inventory=before;InventoryFeedback(reason);return false;}
        var job=PortableCrafting.Start(Inventory,plan);job.Equipment=equipment;
        foreach(var n in naturals){inventoryCollected.Add(n.Item.NaturalId);Inventory.CollectedNatural.Add(n.Item.NaturalId);scatterDirty=true;}
        foreach(var id in involved){RemoveDroppedBody(id);Inventory.Find(id).GroundMoving=false;}
        inventorySelected=primary.Id;inventoryContext=null;inventoryNearbyAt=0;PlayPortableCraftSound(job);if(inventoryOpen)RenderInventoryPreview();return true;
    }
    void CancelItemCraft(ItemCraftJob job)
    {PauseItemCraft(job);Inventory.Crafts.Remove(job);inventoryContext=null;inventoryNotice="Work cancelled; materials retained.";inventoryNoticeUntil=Time.unscaledTime+3;}
    void PlayPortableCraftSound(ItemCraftJob job)
    {
        var recipe=PortableCrafting.Recipe(job.RecipeId);if(recipe==null||recipe.Tools.Length==0)return;
        if(recipe.Tools.Any(c=>(c&(CraftCapability.Strike|CraftCapability.Grind))!=0))PlayStoneCraftImpact();else PlayCraftSound(ItemCraftAction.PortableRecipe);
        craftSoundAt=Time.unscaledTimeAsDouble+1.5;
    }
    void UpdatePortableCrafting(ItemCraftJob job)
    {
        if(!PortableCrafting.ResumePlan(Inventory,CraftAccessible(),job,out _,out _)){PauseItemCraft(job);return;}
        var used=job.IngredientIds.ToArray();var output=StoneCrafting.Advance(Inventory,job,Math.Min(.25,Time.unscaledDeltaTime),PlayerRules.Settings);
        if(Time.unscaledTimeAsDouble>=craftSoundAt)PlayPortableCraftSound(job);if(output==null)return;
        RestoreWorkHands(job);
        foreach(var id in used)
        {
            RemoveDroppedBody(id);if(inventoryVisuals.TryGetValue(id,out var root)){DestroyInventoryVisual(root);inventoryVisuals.Remove(id);inventoryVisualPlaces.Remove(id);}
        }
        if(output.Place==PossessionPlace.Ground){SetCraftWorkArea(output);ReleasePossession(output,true);}
        inventoryNotice=output.Name+" finished.";inventoryNoticeUntil=Time.unscaledTime+3;inventoryNearbyAt=0;if(inventoryOpen)RenderInventoryPreview();
    }
}
