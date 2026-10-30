using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    sealed class ItemCharge
    {
        public Possession Weapon,Ammo;public int Button;public bool Left,Throw;
        public double Started,Duration;public CraftEquipmentState Equipment;
    }
    ItemCharge itemCharge;
    sealed class PendingRanged {public ItemCharge Charge;public double FireAt;public float Speed,Spread;}
    PendingRanged pendingRanged;
    readonly System.Random combatRandom=new System.Random();
    Possession FindRangedAmmo(Possession weapon)
    {
        var mode=ItemUseRules.Profile(weapon).Primary;
        return Inventory.Items.Where(p=>p.Id!=weapon.Id&&ItemUseRules.Condition(p)>0&&
            (p.Place==PossessionPlace.RightHand||p.Place==PossessionPlace.LeftHand||Inventory.InWornEquipment(p)))
            .Where(p=>mode==ItemPrimaryUse.Bow?(p.Kind==PossessionKind.Arrow||p.Kind==PossessionKind.UnfletchedArrow):mode==ItemPrimaryUse.Sling?p.Kind==PossessionKind.SmallRock&&p.MassKg>=.015&&p.MassKg<=.3:p.Kind==PossessionKind.SpearDart)
            .OrderBy(p=>p.Place==PossessionPlace.LeftHand||p.Place==PossessionPlace.RightHand?0:Inventory.Find(p.ContainerId)?.Kind==PossessionKind.Quiver?1:2).FirstOrDefault();
    }
    void BeginItemCharge(Possession weapon,bool left,int button,bool throwing)
    {
        if(weapon==null||pendingRanged!=null||!CombatReady(left)||ItemUseRules.Condition(weapon)<=0)return;
        var ammo=throwing?weapon:FindRangedAmmo(weapon);
        if(ammo==null){CombatNotice(ItemUseRules.Profile(weapon).Primary==ItemPrimaryUse.Bow?"Carry a finished or unfletched arrow in your hands or worn equipment.":ItemUseRules.Profile(weapon).Primary==ItemPrimaryUse.Sling?"A sling needs a carried small stone (15-300 g).":"Carry a spear-thrower dart.");return;}
        if(!SpendCombatStamina(1))return;
        var charge=new ItemCharge{Weapon=weapon,Ammo=ammo,Left=left,Button=button,Throw=throwing,Started=Time.unscaledTimeAsDouble,Duration=throwing?.8:ItemUseRules.Profile(weapon).Primary==ItemPrimaryUse.Bow?1.05:.8};
        if(!throwing)
        {
            charge.Equipment=PrepareWorkHands(new[]{weapon.Id,ammo.Id});
            if(!Inventory.Move(weapon.Id,left?PossessionPlace.LeftHand:PossessionPlace.RightHand,"",0,0,false,out _)||
               !Inventory.Move(ammo.Id,left?PossessionPlace.RightHand:PossessionPlace.LeftHand,"",0,0,false,out _))
            {var failed=new ItemCraftJob{Equipment=charge.Equipment};RestoreWorkHands(failed);return;}
            RemoveDroppedBody(ammo.Id);ammo.GroundMoving=false;
        }
        itemCharge=charge;combatGuarding=false;
    }
    void UpdateItemCharge(Mouse mouse)
    {
        var charge=itemCharge;if(charge==null)return;
        if(charge.Weapon.Place!=PossessionPlace.RightHand&&charge.Weapon.Place!=PossessionPlace.LeftHand){CancelItemCharge();return;}
        var button=charge.Button==0?mouse.leftButton:mouse.rightButton;var other=charge.Button==0?mouse.rightButton:mouse.leftButton;
        if(other.wasPressedThisFrame){CancelItemCharge();return;}
        if(SurvivalActive){Vitals.Stamina=Math.Max(0,Vitals.Stamina-Math.Min(.1,Time.unscaledDeltaTime)*2);if(Vitals.Stamina<=0){CancelItemCharge();return;}}
        if(button.wasReleasedThisFrame)ReleaseItemCharge();
    }
    void CancelItemCharge()
    {
        var charge=itemCharge;var released=pendingRanged?.Charge;itemCharge=null;pendingRanged=null;
        if(charge?.Equipment!=null)RestoreWorkHands(new ItemCraftJob{Equipment=charge.Equipment});
        if(released?.Equipment!=null)RestoreWorkHands(new ItemCraftJob{Equipment=released.Equipment});
    }
    void ReleaseItemCharge()
    {
        var charge=itemCharge;if(charge==null)return;double held=Time.unscaledTimeAsDouble-charge.Started;
        if(held<.12||Inventory.Find(charge.Ammo.Id)==null){CancelItemCharge();return;}
        double strength=Math.Max(0,Math.Min(1,held/charge.Duration));var profile=ItemUseRules.Profile(charge.Weapon);
        if(!SpendCombatStamina(Math.Max(1,profile.Stamina))){CancelItemCharge();return;}
        double fatigue=SurvivalActive?Math.Max(.15,Vitals.Stamina/100):1;
        double speed=charge.Throw?ItemUseRules.ThrowSpeed(charge.Ammo,strength,fatigue):profile.Primary==ItemPrimaryUse.Bow?ItemUseRules.BowSpeed(charge.Ammo,strength,ItemUseRules.Condition(charge.Weapon)):ItemUseRules.ThrowSpeed(charge.Ammo,strength,fatigue,profile.Primary==ItemPrimaryUse.Sling,profile.Primary==ItemPrimaryUse.SpearThrower);
        float spread=(float)((charge.Ammo.Kind==PossessionKind.UnfletchedArrow?1.6:.3)+(1-strength)*1.1);
        itemCharge=null;AnimateViewItem(charge.Weapon,true);
        // An already drawn bow releases immediately. A hand throw follows the
        // authored forward stroke and lets go when its fingers actually open.
        bool bow=!charge.Throw&&profile.Primary==ItemPrimaryUse.Bow;
        pendingRanged=new PendingRanged{Charge=charge,FireAt=Time.unscaledTimeAsDouble+(bow?0:.62*.24),Speed=(float)speed,Spread=spread};
        CombatCooldown(charge.Left,Math.Max(.35,profile.RecoverySeconds));
        if(bow)ResolveRangedRelease(Time.unscaledTimeAsDouble);
    }
    void ResolveRangedRelease(double now)
    {
        var pending=pendingRanged;if(pending==null||now<pending.FireAt)return;
        var charge=pending.Charge;
        if(Inventory.Find(charge.Ammo.Id)!=charge.Ammo||Inventory.AtHand(charge.Left?PossessionPlace.LeftHand:PossessionPlace.RightHand)!=charge.Weapon)
        {CancelItemCharge();return;}
        pendingRanged=null;
        var ray=PlayerAttackRay();var profile=ItemUseRules.Profile(charge.Weapon);
        Vector3 direction=Quaternion.Euler((float)(combatRandom.NextDouble()*2-1)*pending.Spread,(float)(combatRandom.NextDouble()*2-1)*pending.Spread,0)*ray.direction;
        if(charge.Equipment!=null)charge.Equipment.PreviousHands.RemoveAll(p=>p.Id==charge.Ammo.Id);
        LaunchItemProjectile(charge.Ammo,ray.origin+direction*.4f,direction,pending.Speed);
        PlayItemEffect(!charge.Throw&&profile.Primary==ItemPrimaryUse.Bow?"bow-release":"weapon-swish",ray.origin,.6f,true);
        if(!charge.Throw){charge.Weapon.UseDamage=Math.Min(1,charge.Weapon.UseDamage+.0008);RestoreWorkHands(new ItemCraftJob{Equipment=charge.Equipment});}
    }
}
