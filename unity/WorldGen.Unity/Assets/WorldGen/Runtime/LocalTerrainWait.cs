using System;
using System.Linq;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    readonly LocalWaitClock waitClock=new LocalWaitClock();int waitFrame=-1;double waitDelta,waitCpu,waitStarted;
    void PrepareWaitFrame()
    {
        if(waitFrame==Time.frameCount)return;
        waitFrame=Time.frameCount;waitClock.Feedback(waitCpu);waitCpu=0;
        if(campResting)CheckWaitInterrupt();
        bool blocked=paused||mapOpen||ecologyOpen||CommandChatStopped||!Ready||DeathVisible;
        waitDelta=waitClock.Advance(campResting,blocked,Math.Min(.25,Time.unscaledDeltaTime));
        if(campResting&&livingWork!=null&&!livingWork.IsCompleted)waitDelta=Math.Min(waitDelta,.05);
    }
    double LocalSceneDelta {get{PrepareWaitFrame();return campResting?waitDelta:Math.Min(1.6,Math.Min(.25,Time.unscaledDeltaTime)*(campSleeping?32:1));}}
    int LocalSceneSteps=>campResting||campSleeping?Math.Max(1,(int)Math.Ceiling(LocalSceneDelta/.05)):1;
    public double CampAdvanceSeconds(double realSeconds)
    {PrepareWaitFrame();return campResting?waitDelta:campSleeping?Math.Min(1.6,Math.Max(0,realSeconds)*32):Math.Max(0,realSeconds);}
    void WaitCpu(long began)
    {if(campResting)waitCpu+=(System.Diagnostics.Stopwatch.GetTimestamp()-began)*1000d/System.Diagnostics.Stopwatch.Frequency;}
    bool CheckWaitInterrupt()
    {
        if(!campResting)return false;string reason=null;
        if(!motion.Grounded||motion.Flying||motion.Swimming||Vitals.Health<campSleepHealth-.1||Vitals.CoreTemperatureC<35||Vitals.CoreTemperatureC>39)reason="Danger interrupts your wait.";
        else if(npcPending?.Speaker==0&&npcPending.Priority>=3||npcSpeech.Values.Any(s=>s.Target=="player"&&s.Pace.Shown>0))reason="Someone is speaking to you.";
        else if(LivingState.Groups.Any(g=>g.AlarmUntilSolarDay>PlayerRules.SolarDays&&HumanDistance(g.ThreatPosition,Position)<45))reason="A nearby alarm interrupts your wait.";
        else foreach(var animal in animals.Values)
        {
            if(animal.Dead||animal.Retired||animal.MassKg<4||HumanDistance(animal.Planet,Position)>35)continue;
            if(animal.BehaviourAction=="attack"||animal.BehaviourAction=="hunt"&&animal.HuntTargetId==0||animal.Behaviour?.Predator==true&&animal.MassKg>18){reason="A dangerous animal is nearby.";break;}
        }
        if(reason==null)return false;WakeCamp(reason);return true;
    }
    void DrawWaiting()
    {
        var box=new Rect(Screen.width*.5f-170,Screen.height-70,340,34);GUI.Box(box,"Waiting · "+LocalWaitClock.Elapsed(PlayerRules.SolarDays-waitStarted)+" passed");
    }
}
