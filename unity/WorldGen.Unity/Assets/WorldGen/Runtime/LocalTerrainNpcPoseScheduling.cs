using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    // Experimental opt-in: let Unity schedule the independent NPC graphs in
    // its normal animation phase, instead of synchronously completing each one
    // inside the AI loop. Clip clocks remain explicit; motors/senses unchanged.
    bool npcAutomaticPoses=Environment.GetCommandLineArgs().Contains("--worldgen-npc-automatic-poses"),deferNpcPoses;
    double livingLatePoseMs;
    bool AutomaticNpcPose(HumanActor actor)=>npcAutomaticPoses&&actor.Shared!=null&&actor.Resident?.AgeYears>=3;
    void SetNpcAutomaticPoses(bool enabled)
    {
        npcAutomaticPoses=enabled;
        foreach(var actor in livingPeople.Values)
        {actor.DeferredPose=false;actor.AutomaticAgeCached=false;actor.AnimationAt=0;actor.Graph.Stop();actor.Graph.SetTimeUpdateMode(AutomaticNpcPose(actor)?DirectorUpdateMode.GameTime:DirectorUpdateMode.Manual);if(actor.Shared!=null)actor.Output.SetSourcePlayable(actor.Shared.Output);actor.Graph.Play();}
    }
    void RestoreNpcMotionTransform(HumanActor actor)
    {
        actor.Root.transform.localPosition=new Vector3((float)(actor.Motor.X-originX),(float)(actor.Motor.Y-TerrainMotion.EyeHeightM)+actor.PoseGroundLift*actor.Root.transform.localScale.y,(float)(actor.Motor.Z-originZ));
        actor.Root.transform.localRotation=Quaternion.Euler(0,actor.Yaw,0);
    }
    void UpdateDeferredNpcPoses()
    {
        livingLatePoseMs=0;if(!npcAutomaticPoses)return;long began=System.Diagnostics.Stopwatch.GetTimestamp();
        foreach(var actor in livingPeople.Values)
        {
            if(actor.Health<=0||!AutomaticNpcPose(actor)||!livingGroups.TryGetValue(actor.GroupId,out var group))continue;
            RestoreNpcMotionTransform(actor); // Preserve the production post-animation motor/root ordering.
            if(actor.DeferredCrouch)CorrectHumanCrouch(actor);
            if(actor.YoungRest!=null)
            {
                if(actor.DeferredPose||!actor.AutomaticAgeCached){ApplyHumanAgePose(actor);actor.AutomaticPelvis=actor.YoungPelvis.localPosition;actor.AutomaticAgeCached=true;}
                else foreach(var pair in actor.YoungRest)pair.Key.localPosition=pair.Key==actor.YoungPelvis?actor.AutomaticPelvis:pair.Value;
            }
            actor.DeferredPose=false;
            UpdateNpcProps(actor,group,actor.NpcCrawling||actor.TargetSpeed>2.5f?null:NpcCarryRole(actor,group));
        }
        livingLatePoseMs=ElapsedHumanMs(began);
    }
    string VerifyAutomaticNpcPoses()
    {
        int checkedBones=0;double maximum=0;
        foreach(var actor in livingPeople.Values.Where(a=>a.Health>0&&AutomaticNpcPose(a)))
        {
            var expected=new Vector3((float)(actor.Motor.X-originX),(float)(actor.Motor.Y-TerrainMotion.EyeHeightM)+actor.PoseGroundLift*actor.Root.transform.localScale.y,(float)(actor.Motor.Z-originZ));
            if(Vector3.Distance(expected,actor.Root.transform.localPosition)>.001f||Quaternion.Angle(actor.Root.transform.localRotation,Quaternion.Euler(0,actor.Yaw,0))>.05f)throw new InvalidOperationException("Automatic NPC animation overwrote the physical motor transform.");
        }
        foreach(var actor in livingPeople.Values.Where(a=>a.Health>0&&a.Resident.AgeYears>=18&&a.Shared!=null&&!a.NpcCrouching).Take(6).Concat(livingPeople.Values.Where(a=>a.Health>0&&a.Resident.AgeYears>=3&&a.Resident.AgeYears<17&&a.Shared!=null&&!a.NpcCrouching).Take(4)))
        {
            var bones=actor.Root.GetComponentsInChildren<Transform>().Where(t=>t.name=="head"||t.name=="pelvis"||t.name=="hand_l"||t.name=="hand_r"||t.name=="calf_l"||t.name=="calf_r"||t.name=="spine_02").ToArray();
            var positions=bones.Select(b=>b.localPosition).ToArray();var rotations=bones.Select(b=>b.localRotation).ToArray();
            actor.Graph.Evaluate(0);RestoreNpcMotionTransform(actor);ApplyHumanAgePose(actor);
            for(int i=0;i<bones.Length;i++)
            {
                double error=Vector3.Distance(positions[i],bones[i].localPosition);maximum=Math.Max(maximum,error);
                if(error>.00001||1-Math.Abs(Quaternion.Dot(rotations[i],bones[i].localRotation))>.00001)throw new InvalidOperationException("Automatically scheduled NPC pose differs from immediate evaluation: "+actor.Resident.Name+" / "+bones[i].name+" positionError="+error+" before="+positions[i]+" after="+bones[i].localPosition+" rotationDot="+Quaternion.Dot(rotations[i],bones[i].localRotation)+" age="+actor.Resident.AgeYears+" speed="+actor.Speed+" graph="+actor.Graph.IsPlaying()+" mode="+actor.Graph.GetTimeUpdateMode()+" animator="+actor.Animator.enabled+" bound="+actor.Animator.hasBoundPlayables);
                checkedBones++;
            }
        }
        if(checkedBones<28)throw new InvalidOperationException("Too few actual automatic NPC poses checked.");
        return "PASS "+checkedBones+" actual head/hand/leg/spine bone poses match immediate native evaluation; maximum local position error="+maximum;
    }
}
