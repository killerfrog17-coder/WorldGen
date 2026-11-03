using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed partial class LocalTerrainExplorer
{
    void CaptureHumanAgeRest(HumanActor actor,HumanAgeModel model)
    {
        actor.AgeModel=model;actor.YoungRest=new Dictionary<Transform,Vector3>();
        var originals=actor.Definition.Prefab.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
        foreach(var bone in actor.Root.GetComponentsInChildren<SkinnedMeshRenderer>().SelectMany(r=>r.bones).Where(t=>t!=null).Distinct())
        {
            actor.YoungRest[bone]=bone.localPosition;
            if(bone.name=="pelvis"){actor.YoungPelvis=bone;actor.AdultPelvisRest=originals[bone.name].localPosition;}
        }
        actor.AgeMotionRatio=model.EyeHeight/actor.Definition.EyeHeight;
    }
    static void ApplyHumanAgePose(HumanActor actor)
    {
        if(actor.YoungRest==null)return;
        foreach(var pair in actor.YoungRest)
            pair.Key.localPosition=pair.Key==actor.YoungPelvis?pair.Value+(pair.Key.localPosition-actor.AdultPelvisRest)*actor.AgeMotionRatio:pair.Value;
    }
}
