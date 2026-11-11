using System;
using UnityEngine;
using UnityEngine.Rendering;

public sealed partial class LocalTerrainExplorer
{
    WorldGenHumanDetailLibrary humanDetailLibrary;bool humanDetailLoaded;
    readonly System.Collections.Generic.Dictionary<Mesh,HumanDetailMesh> humanDetailMeshes=new System.Collections.Generic.Dictionary<Mesh,HumanDetailMesh>();
    void EnsureHumanDetail()
    {
        if(humanDetailLoaded)return;humanDetailLoaded=true;humanDetailLibrary=Resources.Load<WorldGenHumanDetailLibrary>("HumanDetail/Library");
        if(humanDetailLibrary==null)return;
        foreach(var entry in humanDetailLibrary.Meshes){humanDetailMeshes[entry.Original]=entry;humanDetailMeshes[entry.Middle]=entry;humanDetailMeshes[entry.Far]=entry;}
    }
    Mesh HumanNativeMesh(Mesh mesh){EnsureHumanDetail();return mesh!=null&&humanDetailMeshes.TryGetValue(mesh,out var entry)?entry.Original:mesh;}
    double NpcViewDistance(HumanActor actor)=>(actor.Root.transform.position-view.transform.position).magnitude;
    float LivingPoseInterval(HumanActor actor)
    {
        var delta=actor.Root.transform.position+transform.up*.8f-view.transform.position;float distance=delta.magnitude;
        // A generous view cone keeps bodies near the edge animated, and nearby
        // offscreen casters retain 15 Hz poses for moving shadows. Actual motors,
        // senses and action clocks still advance every normal gameplay step.
        bool visible=Vector3.Dot(delta,view.transform.forward)>-1&&Vector3.Dot(delta.normalized,view.transform.forward)>.1f;
        if(!visible)return distance<20?1f/15:1f/8;
        return distance<6?0:distance<35?1f/30:distance<90?1f/20:distance<180?1f/12:1f/8;
    }
    void UpdateLivingDetail(HumanActor actor)
    {
        if(Time.unscaledTime<actor.DetailAt)return;actor.DetailAt=Time.unscaledTime+.45f+(HumanHash(actor.ResidentId)%13)*.02f;
        if(actor.DetailRenderers==null)
        {
            actor.DetailRenderers=actor.Root.GetComponentsInChildren<SkinnedMeshRenderer>();actor.DetailKinds=new byte[actor.DetailRenderers.Length];
            for(int i=0;i<actor.DetailRenderers.Length;i++)
            {
                string name=actor.DetailRenderers[i].name.ToLowerInvariant();actor.DetailKinds[i]=(byte)(name.Contains("high-poly")||name.EndsWith("_eyes")?2:name.Contains("teeth")?4:name.Contains("eyebrow")||name.Contains("eyelash")?1:name.Contains("hair")||name.Contains("ponytail")||name.Contains("short0")?3:0);
            }
        }
        EnsureHumanDetail();double distance=NpcViewDistance(actor);
        bool hood=actor.Belongings?.Items.Exists(p=>p.Place==WorldGen.Core.PossessionPlace.Head&&p.Kind==WorldGen.Core.PossessionKind.HideHood)==true;
        for(int i=0;i<actor.DetailRenderers.Length;i++)
        {
            var renderer=actor.DetailRenderers[i];byte kind=actor.DetailKinds[i];bool visible=kind==3&&hood?false:kind==4?distance<6:kind==1?distance<12:kind==2?distance<40:true;
            if(renderer.enabled!=visible)renderer.enabled=visible;
            if(kind==0&&humanDetailMeshes.TryGetValue(renderer.sharedMesh,out var mesh))
            {
                // Full native body inside 10 m; hysteresis avoids threshold
                // thrashing. Reduced meshes keep the same bones, UVs and skin.
                Mesh next=renderer.sharedMesh;
                if(distance<9)next=mesh.Original;
                else if(distance>55)next=mesh.Far;
                else if(distance>11&&distance<50)next=mesh.Middle;
                if(renderer.sharedMesh!=next)renderer.sharedMesh=next;
            }
            var shadows=kind==0||kind==3&&distance<40?ShadowCastingMode.On:ShadowCastingMode.Off;
            if(renderer.shadowCastingMode!=shadows)renderer.shadowCastingMode=shadows;
        }
    }
}
