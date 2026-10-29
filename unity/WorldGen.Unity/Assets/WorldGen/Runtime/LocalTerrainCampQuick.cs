using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    sealed class CampChoice {public Possession Item;public string Label;public int Action;}
    readonly List<CampChoice> campChoices=new List<CampChoice>();
    string campChoiceId="",campChoiceTarget="";int campChoiceIndex;double campChoicesAt;
    CampObject QuickFire()
    {if(!Ready||CampBusy||inventoryOpen||paused||mapOpen||chatOpen||ecologyOpen||DeathVisible)return null;var c=AimedCamp(out _);return c?.Kind==CampKind.Fire?c:null;}
    void RefreshCampChoices(CampObject fire,bool force=false)
    {
        if(!force&&fire.Id==campChoiceTarget&&Time.unscaledTimeAsDouble<campChoicesAt)return;
        campChoicesAt=Time.unscaledTimeAsDouble+.2;bool changed=campChoiceTarget!=fire.Id;campChoiceTarget=fire.Id;campChoices.Clear();RefreshInventoryNearby();
        foreach(var food in fire.CookingItems??Enumerable.Empty<Possession>())campChoices.Add(new CampChoice{Item=food,Label="Take "+food.Name.ToLowerInvariant(),Action=1});
        if(!fire.Burning)campChoices.Add(new CampChoice{Label=fire.EmberSeconds>0?"Revive fire":"Light fire",Action=2});
        var held=Inventory.QuickHeld;
        foreach(var item in CraftAccessible().Where(p=>CampRules.Fuel(p)||CampRules.Cookable(p)&&!p.Burnt&&!p.Cooked).OrderBy(p=>p==held?0:1).GroupBy(p=>p.Kind).Select(g=>g.First()))
            campChoices.Add(new CampChoice{Item=item,Label=(CampRules.Cookable(item)?"Cook ":"Add ")+item.Name.ToLowerInvariant(),Action=0});
        if(fire.Burning||fire.EmberSeconds>0)campChoices.Add(new CampChoice{Label="Smother fire",Action=3});
        int retained=changed?-1:campChoices.FindIndex(c=>(c.Item?.Id??"action-"+c.Action)==campChoiceId);
        campChoiceIndex=retained>=0?retained:0;RememberCampChoice();
    }
    void RememberCampChoice(){if(campChoices.Count>0){var c=campChoices[campChoiceIndex];campChoiceId=c.Item?.Id??"action-"+c.Action;}}
    bool CycleCampChoice(float scroll)
    {
        var fire=QuickFire();if(fire==null||scroll==0)return false;RefreshCampChoices(fire);
        if(campChoices.Count>0){campChoiceIndex=(campChoiceIndex+(scroll>0?-1:1)+campChoices.Count)%campChoices.Count;RememberCampChoice();}return true;
    }
    bool QuickFireAction(CampObject fire)
    {
        RefreshCampChoices(fire,true);if(campChoices.Count==0)return true;var choice=campChoices[campChoiceIndex];
        if(choice.Action==1)
        {if(fire.CookingItems.Remove(choice.Item)){choice.Item.Position=Position;choice.Item.Place=PossessionPlace.Ground;ReceiveToolMaterial(choice.Item);InvalidateCampVisual(fire);}}
        else if(choice.Action==2)LightCamp(fire);
        else if(choice.Action==3){fire.Burning=false;fire.EmberSeconds=0;InvalidateCampVisual(fire);}
        else if(CampRules.Cookable(choice.Item))
        {
            if(fire.FirePowerKw<.2){InventoryFeedback("Light the fire before placing food.");return true;}
            var food=TakeCampMaterial(choice.Item);if(food!=null){food.ContainerId="";food.Place=PossessionPlace.Ground;food.GroundMoving=food.InFlight=false;fire.CookingItems.Add(food);InvalidateCampVisual(fire);}
        }
        else AddCampMaterial(fire,choice.Item);
        campChoicesAt=0;return true;
    }
    string QuickFirePrompt(CampObject fire)
    {
        RefreshCampChoices(fire);return campChoices.Count==0?"Campfire":campChoices[campChoiceIndex].Label+"  [E]";
    }
    void DrawQuickFire()
    {
        var fire=QuickFire();if(fire==null)return;RefreshCampChoices(fire);if(campChoices.Count==0)return;
        float scale=Mathf.Clamp(Screen.height/1080f,.65f,1.5f),cx=Screen.width*.5f,cy=Screen.height*.5f+88*scale;
        var old=GUI.color;var text=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontSize=Mathf.RoundToInt(13*scale)};
        int visible=Math.Min(5,campChoices.Count),start=Math.Max(0,Math.Min(campChoiceIndex-2,campChoices.Count-visible));
        for(int i=start;i<start+visible;i++)
        {
            var c=campChoices[i];float size=(i==campChoiceIndex?42:30)*scale,x=cx+(i-(start+(visible-1)*.5f))*60*scale;
            GUI.color=new Color(1,1,1,i==campChoiceIndex?1:.45f);
            var rect=new Rect(x-size*.5f,cy-size*.5f,size,size);
            if(c.Item!=null)DrawPickupCategory(rect,c.Item);
            else GUI.Label(rect,c.Action==2?"✦":"×",new GUIStyle(text){fontSize=Mathf.RoundToInt(30*scale)});
            if(i==campChoiceIndex)InventoryOutline(new Rect(rect.x-4*scale,rect.y-4*scale,rect.width+8*scale,rect.height+8*scale),new Color(.9f,.84f,.66f,.8f));
        }
        GUI.color=old;GUI.Label(new Rect(cx-220*scale,cy+30*scale,440*scale,25*scale),campChoices[campChoiceIndex].Label+"  ·  E  ·  Scroll",text);
        var food=campChoices[campChoiceIndex].Item;if(food!=null&&fire.CookingItems.Contains(food))
        {float progress=(float)Math.Min(1,food.CookingSeconds/CampRules.CookingDuration(food));var bar=new Rect(cx-70*scale,cy+56*scale,140*scale,3*scale);InventoryFill(bar,new Color(.15f,.13f,.1f,.7f));bar.width*=progress;InventoryFill(bar,food.Burnt?Color.gray:food.Cooked?new Color(.7f,.65f,.25f):new Color(.8f,.4f,.1f));}
    }
}
