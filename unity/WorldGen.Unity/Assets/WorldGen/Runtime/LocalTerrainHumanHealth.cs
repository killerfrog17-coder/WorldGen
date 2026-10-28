using System;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    void DamageHuman(HumanActor actor,double damage,CombatHit hit,Vector3 direction,double momentum=0)
    {
        if(actor==null||damage<=0||!ItemUseRules.Finite(damage))return;
        if(actor==humanPlayer)
        {
            double before=Vitals.Health;damage=GuardedPlayerDamage(damage,actor.Root.transform.position-direction*3);PlayerDamageRules.Apply(Vitals,Mode,damage,PlayerDeathCause.Combat);
            if(Vitals.Health<before){PlayItemEffect("body-hit",hit?.Point??actor.Root.transform.position,.6f,true);PaintHumanBlood(actor,hit,before-Vitals.Health);PauseItemCraft(ActiveItemCraft);CapturePlayerBlood();}
            return;
        }
        if(actor.Health<=0)return;actor.Health=Math.Max(0,actor.Health-damage);PaintHumanBlood(actor,hit,damage);
        PlayItemEffect("body-hit",hit?.Point??actor.Root.transform.position,.6f);
        if(actor.Health<=0)
        {
            actor.Graph.Stop();actor.Animator.enabled=false;actor.Ragdoll=gameObject.AddComponent<WorldGenHumanRagdoll>();
            var movement=new Vector3(Mathf.Sin(actor.Yaw*Mathf.Deg2Rad),0,Mathf.Cos(actor.Yaw*Mathf.Deg2Rad))*actor.Speed;
            actor.Ragdoll.Begin(actor.Root,(x,z)=>(float)Ground(x+originX,z+originZ),movement+direction*(float)Math.Min(4,momentum/70));
            foreach(var renderer in actor.Root.GetComponentsInChildren<SkinnedMeshRenderer>())renderer.updateWhenOffscreen=true;
        }
        else
        {actor.TargetYaw=(float)(Math.Atan2(actor.Motor.X-motion.X,actor.Motor.Z-motion.Z)*180/Math.PI);actor.TargetSpeed=6;actor.Decision=Time.unscaledTime+8;}
    }
}
