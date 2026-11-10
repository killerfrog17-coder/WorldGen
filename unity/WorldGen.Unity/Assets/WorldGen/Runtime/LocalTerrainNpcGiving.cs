using System;
using System.Linq;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    HumanActor NpcGiftRecipient()
    {
        if(npcTalkTarget?.Health>0&&HumanDistance(npcTalkTarget.Planet,Position)<3.2)return npcTalkTarget;
        return livingPeople.Values.Where(p=>p.Health>0&&p.Resident.AgeYears>=7&&p.CurrentActivity!=HumanActivity.Sleep&&HumanDistance(p.Planet,Position)<2.5&&NpcSees(p,transform.TransformPoint(PlayerFeet())+transform.up,4)).OrderBy(p=>HumanDistance(p.Planet,Position)).FirstOrDefault();
    }
    bool GiveNpcItem(HumanActor recipient,Possession item)
    {
        if(recipient==null||recipient.Health<=0||item==null||!Inventory.Carried(item)||HumanDistance(recipient.Planet,Position)>3.2||ActiveItemCraft!=null||worldToolWork!=null)return false;
        bool returning=item.OwnerPerson==recipient.ResidentId;
        if(!returning&&!PlayerMayTake(item)){InventoryFeedback("That is someone else's property.");return false;}
        var stock=recipient.Belongings;if(stock==null)return false;
        if(stock.Find(item.Id)!=null)return false;
        bool personal=item.OwnerPerson==PlayerPropertyKey;
        stock.Items.Add(item);bool room=NpcBelongings.Pack(stock,item)||stock.Hold(item.Id,out _);stock.Items.Remove(item);
        if(!room){InventoryFeedback(recipient.Resident.Name+" has no suitable free hand or carrier space.");return false;}
        var transfer=NpcBelongings.TransferTree(Inventory,stock,item);
        foreach(var p in transfer)
        {NpcOwnership.Assign(p,recipient.Resident,recipient.GroupId);RemoveDroppedBody(p.Id);if(inventoryVisuals.TryGetValue(p.Id,out var root)){DestroyInventoryVisual(root);inventoryVisuals.Remove(p.Id);}}
        double usefulness=Foraging.Food(item)||ItemUseRules.Profile(item).Weapon||item.ContainerWidth>0?.85:.35;
        var memory=NpcMemoryFor(recipient.ResidentId);NpcSocialRules.Gift(memory.Social,PlayerPropertyKey,recipient.ResidentId,PlayerRules.SolarDays,recipient.Planet,item.Name,returning,personal,usefulness);
        memory.Did(NpcDeedKind.Received,item.Id,item.Name,PlayerRules.SolarDays,PlayerPropertyKey);
        recipient.GearAt=recipient.SimulationAge+2;recipient.GearVisualHash=int.MinValue;CaptureLivingPerson(recipient);inventoryNearbyAt=0;
        QueueNpcEvent(recipient,"You just physically received "+item.Name+" from "+(PlayerRules.Identity?.Person?.Name??"the visitor")+(returning?", returning your property.":" as a gift.")+" React naturally in accordance with your actual feelings and customs. The item is now genuinely in your possession.",priority:3);
        InventoryFeedback((returning?"Returned ":"Gave ")+item.Name+" to "+recipient.Resident.Name+".");RenderInventoryPreview();return true;
    }
}
