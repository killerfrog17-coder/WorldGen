using System;
using System.Collections.Generic;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    sealed class ViewItemAction {public double At,Duration;public ItemPrimaryUse Kind;public bool Release;public string Clip;}
    readonly Dictionary<string,ViewItemAction> viewItemActions=new Dictionary<string,ViewItemAction>();
    string ViewStrikeClip(Possession item)
    {
        if(item==null)return "Punch";
        if(LongViewSpear(item))return SupportSpear(item)?"SpearThrust2":"SpearThrust1";
        var kind=ItemUseRules.Profile(item).Primary;
        if(kind==ItemPrimaryUse.Thrust)return "ToolCutB";
        if(kind==ItemPrimaryUse.Blunt)return "ToolBlunt";
        return item?.Kind==PossessionKind.StoneAxe||item?.Kind==PossessionKind.StoneAdze?"ToolChop":"ToolCutA";
    }
    double ViewStrikeContact(Possession item)
    {
        string role=ViewStrikeClip(item);
        if(viewArms!=null&&viewArms.Clips.TryGetValue(role,out var clip)&&clip.contact>=0)return clip.contact;
        return role=="Punch"?.38:role=="ToolBlunt"?.42:role=="ToolCutA"||role=="ToolChop"?.4:.36;
    }
    void AnimateViewItem(Possession item,bool release=false,double duration=-1)
    {
        viewGatherAt=viewConsumeAt=-100;
        if(item==null){viewPunchAt=Time.unscaledTimeAsDouble;viewPunchDuration=duration>0?duration:.55;return;}
        if(release)
        {
            viewReleaseAt=Time.unscaledTimeAsDouble;viewReleaseKind=ItemUseRules.Profile(item).Primary;
            viewReleaseLeft=viewReleaseKind!=ItemPrimaryUse.Bow&&item.Place==PossessionPlace.LeftHand;viewReleaseSpear=LongViewSpear(item)||viewReleaseKind==ItemPrimaryUse.SpearThrower;
        }
        viewItemActions[item.Id]=new ViewItemAction{At=Time.unscaledTimeAsDouble,Duration=duration>0?duration:Math.Min(.85,ItemUseRules.Profile(item).RecoverySeconds),Kind=ItemUseRules.Profile(item).Primary,Release=release,Clip=ViewStrikeClip(item)};
    }
}
