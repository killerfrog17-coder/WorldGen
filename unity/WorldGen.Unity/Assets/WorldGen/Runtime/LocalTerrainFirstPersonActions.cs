using System;
using System.Collections.Generic;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    sealed class ViewItemAction {public double At,Duration;public ItemPrimaryUse Kind;public bool Release;}
    readonly Dictionary<string,ViewItemAction> viewItemActions=new Dictionary<string,ViewItemAction>();
    void AnimateViewItem(Possession item,bool release=false,double duration=-1)
    {
        viewGatherAt=viewConsumeAt=-100;
        if(item==null){viewPunchAt=Time.unscaledTimeAsDouble;return;}
        if(release)
        {
            viewReleaseAt=Time.unscaledTimeAsDouble;viewReleaseKind=ItemUseRules.Profile(item).Primary;
            viewReleaseLeft=viewReleaseKind!=ItemPrimaryUse.Bow&&item.Place==PossessionPlace.LeftHand;viewReleaseSpear=LongViewSpear(item)||viewReleaseKind==ItemPrimaryUse.SpearThrower;
        }
        viewItemActions[item.Id]=new ViewItemAction{At=Time.unscaledTimeAsDouble,Duration=duration>0?duration:Math.Min(.85,ItemUseRules.Profile(item).RecoverySeconds),Kind=ItemUseRules.Profile(item).Primary,Release=release};
    }
}
