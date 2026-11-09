using System;
using System.Linq;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    bool campPromptRestricted;
    long PlayerPropertyKey=>PlayerRules.Identity?.Person?.Id??long.MaxValue;
    double PlayerFoodSharing
    {
        get{if(PlayerRules.Identity!=null&&livingGroups.TryGetValue(PlayerRules.Identity.Group,out var home))return EffectiveNpcCulture(home).FoodSharing;int culture=PlayerRules.Identity?.Culture??-1;var cultures=sourceWorld.Prehistory?.Foragers?.Cultures;return cultures!=null&&culture>=0&&culture<cultures.Length?cultures[culture].FoodSharing:0;}
    }
    bool PlayerMayTake(Possession item)
    {
        if(NpcOwnership.Allowed(item,PlayerPropertyKey,PlayerRules.Identity?.Group??0,PlayerRules.Identity?.Person?.Household??0,PlayerFoodSharing))return true;
        return item.OwnerGroup>0&&livingGroups.TryGetValue(item.OwnerGroup,out var host)&&LivingState.Groups.FirstOrDefault(g=>g.Group==item.OwnerGroup)?.Community?.Guests.Contains(PlayerPropertyKey)==true&&NpcOwnership.Allowed(item,PlayerPropertyKey,item.OwnerGroup,0,EffectiveNpcCulture(host).FoodSharing);
    }
    bool PlayerMayUseCamp(CampObject obj)=>obj==null||NpcOwnership.BedAllowed(obj,PlayerRules.Identity?.Group??0,PlayerRules.Identity?.Person?.Household??0);
    void ClaimPlayerItem(Possession item)
    {
        if(item.OwnerPerson!=0||item.OwnerGroup!=0)return;
        item.OwnerPerson=PlayerPropertyKey;item.OwnerName=Inventory.PlayerName;item.OwnerGroup=PlayerRules.Identity?.Group??0;item.OwnerHousehold=PlayerRules.Identity?.Person?.Household??0;
    }
    void WitnessPropertyUse(Possession item,CampObject obj=null,bool sleeping=false)
    {
        bool allowed=item!=null?PlayerMayTake(item):PlayerMayUseCamp(obj);if(allowed)return;
        long group=item?.OwnerGroup??obj?.NpcGroup??0,owner=item?.OwnerPerson??0;string name=item?.Name??(sleeping?"another household's sleeping place":"camp materials");
        var at=transform.TransformPoint(PlayerFeet())+transform.up;
        foreach(var witness in livingPeople.Values.Where(p=>p.Health>0&&p.Resident.AgeYears>=4&&p.GroupId==group&&p.CurrentActivity!=HumanActivity.Sleep&&HumanDistance(p.Planet,Position)<20))
        {
            if(!NpcSees(witness,at,20))continue;
            var social=NpcMemoryFor(witness.ResidentId).Social;social.Advance(PlayerRules.SolarDays);var tie=social.Tie(PlayerPropertyKey,PlayerRules.SolarDays);
            tie.Trust=Math.Max(-1,tie.Trust-.2);tie.Grievance=Math.Min(1,tie.Grievance+.25);tie.LastSolarDay=PlayerRules.SolarDays;social.Anger=Math.Max(social.Anger,witness.ResidentId==owner?.7:.4);
            string detail=(PlayerRules.Identity?.Person?.Name??"The visitor")+(sleeping?" used ":" took ")+name+" without permission"+(string.IsNullOrEmpty(item?.OwnerName)?"":" from "+item.OwnerName)+".";
            social.Record(new NpcIncident{Kind=sleeping?NpcIncidentKind.Trespass:NpcIncidentKind.Theft,Actor=PlayerPropertyKey,Subject=owner,SolarDay=PlayerRules.SolarDays,Position=Position,Detail=detail});
            QueueNpcEvent(witness,"You personally witnessed "+detail+" React according to your relationship, ownership and customs; you may ask for it back or object. This is not automatically a fight.",priority:3);
        }
    }
    bool PlayerMayUseForCraft(Possession item)=>Inventory.Carried(item)||PlayerMayTake(item);
}
