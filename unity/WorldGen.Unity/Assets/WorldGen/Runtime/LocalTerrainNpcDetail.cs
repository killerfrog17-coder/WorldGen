using System;
using UnityEngine;
using UnityEngine.Rendering;

public sealed partial class LocalTerrainExplorer
{
    void UpdateLivingDetail(HumanActor actor)
    {
        if(Time.unscaledTime<actor.DetailAt)return;actor.DetailAt=Time.unscaledTime+.45f+(HumanHash(actor.ResidentId)%13)*.02f;
        if(actor.DetailRenderers==null)
        {
            actor.DetailRenderers=actor.Root.GetComponentsInChildren<SkinnedMeshRenderer>();actor.DetailKinds=new byte[actor.DetailRenderers.Length];
            for(int i=0;i<actor.DetailRenderers.Length;i++)
            {
                string name=actor.DetailRenderers[i].name.ToLowerInvariant();actor.DetailKinds[i]=(byte)(name.Contains("high-poly")?2:name.Contains("teeth")||name.Contains("eyebrow")||name.Contains("eyelash")?1:name.Contains("hair")||name.Contains("ponytail")||name.Contains("short0")?3:0);
            }
        }
        double distance=HumanDistance(actor.Planet,Position);
        bool hood=actor.Belongings?.Items.Exists(p=>p.Place==WorldGen.Core.PossessionPlace.Head&&p.Kind==WorldGen.Core.PossessionKind.HideHood)==true;
        for(int i=0;i<actor.DetailRenderers.Length;i++)
        {
            var renderer=actor.DetailRenderers[i];byte kind=actor.DetailKinds[i];bool visible=kind==3&&hood?false:kind==1?distance<18:kind==2?distance<40:true;
            if(renderer.enabled!=visible)renderer.enabled=visible;
            var shadows=kind==0||kind==3&&distance<40?ShadowCastingMode.On:ShadowCastingMode.Off;
            if(renderer.shadowCastingMode!=shadows)renderer.shadowCastingMode=shadows;
        }
    }
}
