using System;
using System.Collections.Generic;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    sealed class ViewItemAction {public double At,Duration;public ItemPrimaryUse Kind;public bool Release;}
    readonly Dictionary<string,ViewItemAction> viewItemActions=new Dictionary<string,ViewItemAction>();
    void AnimateViewItem(Possession item,bool release=false)
    {
        if(item==null){viewPunchAt=Time.unscaledTimeAsDouble;return;}if(release){viewReleaseAt=Time.unscaledTimeAsDouble;viewReleaseKind=ItemUseRules.Profile(item).Primary;viewReleaseLeft=viewReleaseKind!=ItemPrimaryUse.Bow&&item.Place==PossessionPlace.LeftHand;}
        viewItemActions[item.Id]=new ViewItemAction{At=Time.unscaledTimeAsDouble,Duration=Math.Min(.85,ItemUseRules.Profile(item).RecoverySeconds),Kind=ItemUseRules.Profile(item).Primary,Release=release};
    }
    void PoseFirstPersonAction(GameObject root,Possession item)
    {
        if(!humanHideBody||deathPresented||item.Place!=PossessionPlace.RightHand&&item.Place!=PossessionPlace.LeftHand)return;
        float side=item.Place==PossessionPlace.LeftHand?-1:1;Vector3 offset=Vector3.zero,angles=Vector3.zero;double now=Time.unscaledTimeAsDouble;
        if(itemCharge!=null&&(itemCharge.Weapon==item||itemCharge.Ammo==item))
        {
            float t=Mathf.SmoothStep(0,1,(float)((now-itemCharge.Started)/itemCharge.Duration));
            if(itemCharge.Throw){offset=new Vector3(-side*.03f,.10f,-.10f)*t;angles=new Vector3(-32,side*-12,side*-12)*t;}
            else if(ItemUseRules.Profile(itemCharge.Weapon).Primary==ItemPrimaryUse.Sling)
            {float a=(float)(now-itemCharge.Started)*11;offset=new Vector3(Mathf.Sin(a)*.1f,Mathf.Cos(a)*.12f,.03f)*t;angles.z=Mathf.Sin(a)*35*t;}
            else if(item.Kind==PossessionKind.Bow){offset=new Vector3(.27f,.03f,.02f)*t;angles=new Vector3(-3,side*5,-side*3)*t;}
            else{offset=new Vector3(0,.08f,-.24f)*t;angles.x=-18*t;}
        }
        else if(viewItemActions.TryGetValue(item.Id,out var action))
        {
            float t=(float)((now-action.At)/Math.Max(.2,action.Duration));
            if(t>=1)viewItemActions.Remove(item.Id);
            else
            {
                // Short anticipation, fast contact, slower eased recovery.
                float strike=t<.12f?-Mathf.SmoothStep(0,1,t/.12f)*.25f:t<.36f?Mathf.Lerp(-.25f,1,Mathf.SmoothStep(0,1,(t-.12f)/.24f)):1-Mathf.SmoothStep(0,1,(t-.36f)/.64f);
                if(action.Kind==ItemPrimaryUse.Thrust){offset=new Vector3(-side*.07f,-.015f,SupportSpear(item)?.20f:.23f)*strike;angles=new Vector3(7,side*5,0)*strike;}
                else{offset=new Vector3(-side*.24f,-.08f,.22f)*strike;angles=new Vector3(38,side*-28,side*32)*strike;}
                if(action.Release){offset=new Vector3(0,.03f,.12f)*Mathf.Sin(t*Mathf.PI);angles.x=14*Mathf.Sin(t*Mathf.PI);}
            }
        }
        else if(combatGuarding&&ItemUseRules.Profile(item).Secondary==ItemSecondaryUse.Block)
        {offset=new Vector3(-side*.18f,.14f,-.04f);angles=new Vector3(-20,0,side*55);}
        else if(worldToolWork?.Tool==item||ActiveItemCraft?.Running==true)
        {
            float wave=(float)Math.Sin(now*(worldToolWork?.Tool==item?5:8));offset=new Vector3(-side*.04f,.025f*wave,.035f*wave);angles=new Vector3(12*wave,0,side*5*wave);
        }
        float grip=(float)item.HeightM*(item.Kind==PossessionKind.Bow?.5f:WoodCrafting.Wood(item)||PortableTechnology.Item(item.Kind)?.Long==true?.35f:.18f);
        var pivot=root.transform.position+root.transform.up*grip;
        root.transform.rotation=view.transform.rotation*Quaternion.Euler(angles)*Quaternion.Inverse(view.transform.rotation)*root.transform.rotation;
        root.transform.position=pivot-root.transform.up*grip+view.transform.TransformVector(offset);
    }
}
