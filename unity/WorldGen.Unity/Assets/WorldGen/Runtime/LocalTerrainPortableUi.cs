using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    Vector2 portableContextScroll;float inventoryMiddleScroll;
    void DrawEquipmentStorage(Rect middle,float y,float inset,float cell,float scale)
    {
        var slots=new[]{PossessionPlace.Back,PossessionPlace.Shoulder,PossessionPlace.Waist,PossessionPlace.SlungBack};float gap=8*scale,width=(middle.width-2*inset-3*gap)/4;
        for(int i=0;i<slots.Length;i++)
        {
            var r=new Rect(middle.x+inset+i*(width+gap),y,width,65*scale);InventoryLabel(new Rect(r.x,r.y-25*scale,r.width,24*scale),slots[i]==PossessionPlace.SlungBack?"Slung":slots[i].ToString(),Mathf.RoundToInt(13*scale));
            InventoryFill(r,new Color(.08f,.11f,.12f));InventoryOutline(r,InventoryLine);inventoryZones.Add(new InventoryZone{Rect=r,Clip=r,Place=slots[i]});
            var item=Inventory.Items.FirstOrDefault(p=>p.Place==slots[i]);if(item!=null)DrawInventoryItem(r,new NearbyPossession{Item=item},scale);
        }
        var clip=new Rect(middle.x+inset,y+99*scale,middle.width-2*inset-14*scale,middle.yMax-y-112*scale);float height=0;
        var containers=Inventory.WornContainers.Concat(Inventory.Items.Where(p=>p.ContainerWidth>0&&(p.Place==PossessionPlace.LeftHand||p.Place==PossessionPlace.RightHand||p.Place==PossessionPlace.Container&&PortableTechnology.Belt(Inventory.Find(p.ContainerId))&&Inventory.Carried(p)))).Distinct().ToArray();
        foreach(var bag in containers)
        {
            float row=clip.y+height-inventoryMiddleScroll;
            if(row>=clip.y&&row+26*scale<=clip.yMax)InventoryLabel(new Rect(clip.x,row,clip.width,26*scale),bag.Name+"  "+bag.ContainerWidth+" x "+bag.ContainerHeight,Mathf.RoundToInt(14*scale),InventoryGold);
            DrawInventoryGrid(bag,new Rect(clip.x,row+30*scale,bag.ContainerWidth*cell,bag.ContainerHeight*cell),cell,clip);height+=bag.ContainerHeight*cell+48*scale;
        }
        if(containers.Length==0)InventoryLabel(clip,"No carrier equipped. Hold items, or make carrying equipment.",Mathf.RoundToInt(15*scale));
        inventoryMiddleScroll=Mathf.Clamp(inventoryMiddleScroll,0,Mathf.Max(0,height-clip.height));
        if(height>clip.height)inventoryMiddleScroll=GUI.VerticalScrollbar(new Rect(middle.xMax-inset-11*scale,clip.y,11*scale,clip.height),inventoryMiddleScroll,clip.height,0,height);
        if(inventoryContext==null&&Event.current.type==EventType.ScrollWheel&&clip.Contains(Event.current.mousePosition)){inventoryMiddleScroll=Mathf.Clamp(inventoryMiddleScroll+Event.current.delta.y*cell,0,Mathf.Max(0,height-clip.height));Event.current.Use();}
    }
    void DrawPortableContext(float scale)
    {
        if(inventoryContext==null)return;var p=inventoryContext.Item;var job=CraftFor(p.Id);var available=CraftAccessible();
        var recipes=job!=null?(PortableCrafting.IsJob(job)?new[]{PortableCrafting.Recipe(job.RecipeId)}:Array.Empty<PortableRecipe>()):PortableCrafting.Recipes.Where(r=>r.Accepts(p)).ToArray();
        var legacy=job!=null?(PortableCrafting.IsJob(job)?Array.Empty<ItemCraftAction>():new[]{job.Action}):StoneCrafting.Rock(p)?new[]{ItemCraftAction.Flake}:WoodCrafting.Wood(p)?
            (p.Kind==PossessionKind.Branch?new[]{ItemCraftAction.TrimBranch,ItemCraftAction.StripBark,ItemCraftAction.DiggingStick,ItemCraftAction.WoodenSpear}:p.Kind==PossessionKind.TrimmedPole?new[]{ItemCraftAction.StripBark,ItemCraftAction.DiggingStick,ItemCraftAction.WoodenSpear}:new[]{ItemCraftAction.StripBark}):Array.Empty<ItemCraftAction>();
        float width=390*scale;float height=Mathf.Min(Screen.height-24*scale,(220+(recipes.Length+legacy.Length)*72)*scale);
        var rect=new Rect(Mathf.Clamp(inventoryMenuAt.x,0,Screen.width-width),Mathf.Clamp(inventoryMenuAt.y,0,Screen.height-height),width,height);inventoryContextRect=rect;
        InventoryFill(rect,new Color(.075f,.1f,.11f));InventoryOutline(rect,InventoryGold);GUILayout.BeginArea(new Rect(rect.x+8*scale,rect.y+8*scale,rect.width-16*scale,rect.height-16*scale));
        portableContextScroll=GUILayout.BeginScrollView(portableContextScroll);
        var label=new GUIStyle(GUI.skin.label){fontSize=Mathf.RoundToInt(13*scale),wordWrap=true};
        string detail=StoneCrafting.Rock(p)?StoneCrafting.Material(p)+". "+(StoneCrafting.FractureQuality(p)>0?"Can produce cutting flakes.":"Unsuitable for flaking."):WoodCrafting.Wood(p)?WoodDescription(p):p.Name;
        if(!string.IsNullOrEmpty(p.AppearanceSpecies))detail+=" - material from "+PortableTechnology.SpeciesName(p.AppearanceSpecies)+".";
        GUILayout.Label(detail,label);
        if(ItemUseRules.Profile(p).Weapon)GUILayout.Label("Condition "+Math.Round(ItemUseRules.Condition(p)*100)+"%"+((PortableTechnology.Item(p.Kind)?.Capability&(CraftCapability.Cut|CraftCapability.Scrape))!=0?" · edge "+Math.Round(p.EdgeSharpness*100)+"%":""),label);
        void ActionButton(string title,bool ready,string reason,Action run)
        {
            bool enabled=GUI.enabled;GUI.enabled=ready;if(GUILayout.Button(title,InventoryControlStyle(GUI.skin.button,scale),GUILayout.Height(30*scale)))run();GUI.enabled=enabled;
            if(!ready)GUILayout.Label(reason,label);
        }
        foreach(var action in legacy)
        {
            bool ok=CraftInputs(job?.WorkpieceId??p.Id,out _,out _,out string reason,action);
            ActionButton(job!=null?(job.Running?"Pause: ":"Resume: ")+WoodCrafting.Label(action):WoodCrafting.Label(action),ok||job?.Running==true,reason,
                ()=>{if(job?.Running==true)PauseItemCraft(job);else StartInventoryKnapping(job?.WorkpieceId??p.Id,action);inventoryContext=null;});
        }
        foreach(var recipe in recipes)
        {
            if(recipe==null)continue;bool ok=PortableCrafting.Plan(Inventory,available,recipe,job?.WorkpieceId??p.Id,out _,out var reason);
            ActionButton(job!=null?(job.Running?"Pause: ":"Resume: ")+recipe.Name:recipe.Name,ok||job?.Running==true,reason,
                ()=>{if(job?.Running==true)PauseItemCraft(job);else StartPortableCraft(recipe.Id,job?.WorkpieceId??p.Id);inventoryContext=null;});
        }
        if(job!=null)ActionButton("Cancel work; keep materials",true,"",()=>{CancelItemCraft(job);});
        if(p.Kind==PossessionKind.DiggingStick)ActionButton("Dig fibrous roots here",true,"",()=>{BeginDigging(p);inventoryContext=null;});
        if(p.Kind==PossessionKind.HandLine||p.Kind==PossessionKind.FibreNet)ActionButton(p.Kind==PossessionKind.FibreNet?"Gather nearby fish":"Fish with hand-line",true,"",()=>{BeginFishing(p);inventoryContext=null;});
        if(worldToolWork!=null)ActionButton("Stop tool work",true,"",()=>{CancelWorldToolWork();inventoryContext=null;});
        GUILayout.Space(5*scale);
        ActionButton("Hold in right hand",true,"",()=>{RequestPossession(p.Id,PossessionPlace.RightHand);inventoryContext=null;});
        ActionButton("Hold in left hand",true,"",()=>{RequestPossession(p.Id,PossessionPlace.LeftHand);inventoryContext=null;});
        ActionButton("Stow in worn carrier",true,"",()=>{RequestPossession(p.Id,PossessionPlace.Container,autoStow:true);inventoryContext=null;});
        var slot=PortableTechnology.Wear(p);if(slot.HasValue)ActionButton("Wear on "+slot.Value.ToString().ToLowerInvariant(),true,"",()=>{RequestPossession(p.Id,slot.Value);inventoryContext=null;});
        ActionButton("Put on ground",true,"",()=>{RequestPossession(p.Id,PossessionPlace.Ground);inventoryContext=null;});
        GUILayout.EndScrollView();GUILayout.EndArea();
        if(Event.current.type==EventType.MouseDown&&!rect.Contains(Event.current.mousePosition)){inventoryContext=null;portableContextScroll=Vector2.zero;Event.current.Use();}
    }
}
