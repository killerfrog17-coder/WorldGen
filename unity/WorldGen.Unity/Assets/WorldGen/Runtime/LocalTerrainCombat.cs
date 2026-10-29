using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    double combatRightReady,combatLeftReady,combatGlobalReady,combatNoticeAt;
    bool combatGuarding;
    bool CombatStopped=>!Ready||DeathVisible||SurvivalActive&&Vitals.Incapacitated||Spectating||paused||mapOpen||ecologyOpen||chatOpen||inventoryOpen||!Application.isFocused&&!inventoryReviewMode;
    void CombatNotice(string text){if(Time.unscaledTimeAsDouble<combatNoticeAt)return;combatNoticeAt=Time.unscaledTimeAsDouble+1.5;InventoryFeedback(text);}
    bool SpendCombatStamina(double amount)
    {
        if(!SurvivalActive)return true;if(Vitals.Stamina<amount){CombatNotice("Too exhausted for that action.");return false;}
        Vitals.Stamina=Math.Max(0,Vitals.Stamina-amount);return true;
    }
    bool CombatReady(bool left)=>Time.unscaledTimeAsDouble>=combatGlobalReady&&Time.unscaledTimeAsDouble>=(left?combatLeftReady:combatRightReady);
    void CombatCooldown(bool left,double seconds)
    {double now=Time.unscaledTimeAsDouble;combatGlobalReady=now+.16;if(left)combatLeftReady=now+seconds;else combatRightReady=now+seconds;}
    Ray PlayerAttackRay()
    {
        Vector3 eye=transform.TransformPoint(new Vector3((float)(motion.X-originX),(float)motion.Y+humanEyeOffset,(float)(motion.Z-originZ)));
        Vector3 aim=view.transform.forward;
        if(!humanCamera.FirstPerson)
        {
            // Aim from the camera, but make the actual attack originate at the
            // body. A camera behind a wall cannot turn into a remote weapon.
            var cameraRay=new Ray(view.transform.position,aim);var obstruction=TraceCombatObstacle(cameraRay,150);
            var target=TraceLiving(cameraRay,obstruction?.Distance??150);Vector3 point=target?.Point??obstruction?.Point??cameraRay.GetPoint(150);aim=(point-eye).normalized;
        }
        return new Ray(eye,aim);
    }
    void PrimaryItemAction(Possession item,bool left,int button)
    {
        if(!CombatReady(left))return;if(item!=null&&ItemUseRules.Condition(item)<=0){CombatNotice("This item needs repair before use.");return;}
        var profile=ItemUseRules.Profile(item);if(!profile.Weapon)return;
        if(profile.Primary==ItemPrimaryUse.Bow||profile.Primary==ItemPrimaryUse.Sling||profile.Primary==ItemPrimaryUse.SpearThrower){BeginItemCharge(item,left,button,false);return;}
        StrikeWithItem(item,left);
    }
    void StrikeWithItem(Possession item,bool left)
    {
        var profile=ItemUseRules.Profile(item);double weightPenalty=item==null?1:Math.Max(1,Math.Sqrt(item.MassKg/(item.Kind==PossessionKind.LargeRock?.7:1.5)));
        if(!profile.Weapon||!CombatReady(left)||!SpendCombatStamina(profile.Stamina*weightPenalty))return;
        CombatCooldown(left,profile.RecoverySeconds*weightPenalty);AnimateViewItem(item);var ray=PlayerAttackRay();float reach=(float)profile.ReachM;
        PlayItemEffect("weapon-swish",ray.origin,item==null?.12f:.3f,true);
        CombatHit chosen=null;CombatObstacle obstacle=null;float nearest=reach;
        // A small contact width is forgiving without treating an animal's whole
        // bounding box as flesh. Each candidate is still a real surface hit.
        foreach(var offset in new[]{Vector3.zero,view.transform.right*.045f,-view.transform.right*.045f,view.transform.up*.045f,-view.transform.up*.045f})
        {
            var stroke=new Ray(ray.origin+offset,ray.direction);var wall=TraceCombatObstacle(stroke,reach);var hit=TraceLiving(stroke,wall?.Distance??reach);
            if(hit!=null&&hit.Distance<=nearest){chosen=hit;nearest=hit.Distance;}
            if(wall!=null&&(obstacle==null||wall.Distance<obstacle.Distance))obstacle=wall;
        }
        double damage=ItemUseRules.MeleeDamage(item,SurvivalActive?.6+.4*Vitals.MovementScale:1);
        if(chosen!=null)
        {
            ApplyCombatHit(chosen,damage,ray.direction,Math.Sqrt(Math.Max(.01,item?.MassKg??.3)*profile.EnergyJ*2));
            ApplyItemImpactWear(item,profile.EnergyJ,false);SplitLooseSpear(item);
        }
        else if(obstacle!=null)
        {PlayItemEffect(obstacle.Tree?"wood-chop":"stone-contact",obstacle.Point,.4f);ApplyItemImpactWear(item,profile.EnergyJ,true);if(obstacle.Tree)ToolHitTree(item,obstacle,profile.EnergyJ);SplitLooseSpear(item);}
    }
    void ApplyCombatHit(CombatHit hit,double damage,Vector3 direction,double momentum)
    {if(hit.Animal!=null)DamageAnimal(hit.Animal,damage,hit.Point,direction,momentum,true);else if(hit.Human!=null)DamageHuman(hit.Human,damage,hit,direction,momentum);}
    void UpdateItemCombat()
    {
        combatGuarding=false;
        if(CombatStopped){CancelItemCharge();return;}
        var mouse=Mouse.current;if(mouse==null)return;
        if(worldToolWork!=null){if(mouse.leftButton.wasPressedThisFrame||mouse.rightButton.wasPressedThisFrame)CancelWorldToolWork();return;}
        if(itemCharge!=null){UpdateItemCharge(mouse);return;}
        if(mouse.leftButton.wasPressedThisFrame||mouse.rightButton.wasPressedThisFrame)PauseItemCraft(ActiveItemCraft);
        var right=Inventory.AtHand(PossessionPlace.RightHand);var left=Inventory.AtHand(PossessionPlace.LeftHand);bool dual=right!=null&&left!=null&&!ReferenceEquals(right,left);
        if(dual)
        {if(mouse.leftButton.wasPressedThisFrame)PrimaryItemAction(right,false,0);if(mouse.rightButton.wasPressedThisFrame)PrimaryItemAction(left,true,1);return;}
        var main=right??left;bool mainLeft=right==null&&left!=null;var profile=ItemUseRules.Profile(main);
        if(mouse.leftButton.wasPressedThisFrame)PrimaryItemAction(main,mainLeft,0);
        if(itemCharge!=null)return;
        if(profile.Secondary==ItemSecondaryUse.Block){combatGuarding=mouse.rightButton.isPressed&&(!SurvivalActive||Vitals.Stamina>0);return;}
        if(!mouse.rightButton.wasPressedThisFrame)return;
        if(profile.Secondary==ItemSecondaryUse.Throw)BeginItemCharge(main,mainLeft,1,true);
        else if(profile.Secondary==ItemSecondaryUse.Dig)BeginDigging(main);
        else if(profile.Secondary==ItemSecondaryUse.Fish||profile.Secondary==ItemSecondaryUse.Net)BeginFishing(main);
    }
    double GuardedPlayerDamage(double damage,Vector3 source)
    {
        if(!SurvivalActive||!combatGuarding)return damage;var guard=Inventory.QuickHeld;Vector3 forward=view.transform.forward;forward.y=0;Vector3 toward=source-humanPlayer.Root.transform.position;toward.y=0;
        double stamina=Vitals.Stamina,result=ItemUseRules.BlockDamage(damage,guard,Vector3.Dot(forward.normalized,toward.normalized),ref stamina);Vitals.Stamina=stamina;
        if(result<damage)ApplyItemImpactWear(guard,damage*.7,false);return result;
    }
    void ApplyItemImpactWear(Possession item,double energy,bool hard)
    {
        if(item==null||StoneCrafting.Rock(item))return;
        bool projectile=item.Kind==PossessionKind.Arrow||item.Kind==PossessionKind.UnfletchedArrow||item.Kind==PossessionKind.SpearDart;
        item.UseDamage=Math.Min(1,item.UseDamage+Math.Max(0,energy)*(projectile?(hard?.012:.0018):hard?.00006:.00013));
        if((PortableTechnology.Capabilities(item)&(CraftCapability.Cut|CraftCapability.Scrape))!=0)item.EdgeSharpness=Math.Max(0,item.EdgeSharpness-Math.Max(0,energy)*(hard?.00055:.00012));
    }
    void SplitLooseSpear(Possession item)
    {
        if(item?.Kind!=PossessionKind.LooseStoneSpear)return;
        var point=PortableTechnology.Create(PossessionKind.StonePoint,Position);point.Variant=item.Variant;point.MassKg=Math.Min(item.MassKg*.2,.12);item.MassKg-=point.MassKg;Inventory.Items.Add(point);ReleasePossession(point,true);
        item.Kind=PossessionKind.NotchedShaft;item.WidthM=item.DepthM=.03;item.CellsWide=1;
        if(!item.InFlight&&inventoryVisuals.TryGetValue(item.Id,out var root)){RemoveDroppedBody(item.Id);DestroyInventoryVisual(root);inventoryVisuals.Remove(item.Id);inventoryVisualPlaces.Remove(item.Id);}
    }
    partial void ToolHitTree(Possession tool,CombatObstacle obstacle,double energy);
    partial void BeginDigging(Possession item);
    partial void BeginFishing(Possession item);
}
