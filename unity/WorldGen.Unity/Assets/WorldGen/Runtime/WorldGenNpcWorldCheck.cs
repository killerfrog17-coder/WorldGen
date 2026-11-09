using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    public IEnumerator CheckNpcWorld(string output,List<string> report)
    {
        void Check(bool ok,string why){if(!ok){File.WriteAllLines(Path.Combine(output,"failed-checks.txt"),report.Concat(new[]{"FAIL "+why}));Application.Quit(1);throw new InvalidOperationException(why);}report.Add("PASS "+why);Debug.Log("NPC WORLD PASS "+why);}
        PlayerRules.Mode=GameMode.Creative;Vitals.Restore();ApplyPlayerMode();paused=false;npcEvents.Clear();npcPlans.Clear();
        double until=Time.unscaledTimeAsDouble+90;
        while(livingPeople.Values.Count(p=>p.Health>0&&p.Resident.AgeYears>=18&&p.GroupId==PlayerRules.Identity.Group)<2&&Time.unscaledTimeAsDouble<until)yield return null;
        var actors=livingPeople.Values.Where(p=>p.Health>0&&p.Resident.AgeYears>=18&&p.GroupId==PlayerRules.Identity.Group).OrderBy(p=>HumanDistance(p.Planet,Position)).Take(2).ToArray();
        Check(actors.Length==2,"Actual adult bandmates are available for grounded-context and interruption checks");var first=actors[0];var second=actors[1];
        double savedHour=LocalSolarHour;var savedWeather=CurrentWeather;
        TerrainMapViewport.Degrees(Position,out _,out double longitude);PlayerRules.SetLocalSolarHour(12,longitude);SetDaylight(LocalSolarHour,skyYear);
        ExecuteAnimalSpawnCommand("/spawn wolf");until=Time.unscaledTimeAsDouble+90;
        while(!animals.Values.Any(a=>a.Manual&&a.Spawn.Profile.slug=="wolf")&&Time.unscaledTimeAsDouble<until)yield return null;
        var wolf=animals.Values.FirstOrDefault(a=>a.Manual&&a.Spawn.Profile.slug=="wolf");Check(wolf!=null,"A real wolf asset loads through the normal spawn queue");paused=true;
        double sx=first.Motor.X,sz=first.Motor.Z;
        wolf.X=sx;wolf.Z=sz+4;wolf.Planet=frame.Position(wolf.X,wolf.Z);wolf.GroundY=CampFloor(wolf.X,wolf.Z);wolf.Heading=wolf.TargetHeading=180;wolf.Speed=wolf.TargetSpeed=0;wolf.Fade=1;UpdateAnimal(wolf,0);
        first.Yaw=first.TargetYaw=0;first.Root.transform.localRotation=Quaternion.Euler(0,0,0);first.CurrentActivity=HumanActivity.Watch;
        nextWildlifeIndex=0;IndexWildlifeNeighbours();var sense=NpcAwarenessFor(first);sense.NextSense=0;npcSenseFrame=-1;UpdateNpcAwareness(first);
        Check(sense.Visible.Any(v=>v.Animal==wolf),"NPC eyes identify an actual nearby wolf on unobstructed resident ground");
        Check(NpcMemoryFor(first.ResidentId).Social.Incidents.Any(e=>e.Animal==wolf.Spawn.Id&&e.Detail.IndexOf("wolf",StringComparison.OrdinalIgnoreCase)>=0),"The witness stores the particular animal and location rather than generic magical fear");
        var obstacle=new GameObject("NPC awareness occlusion fixture");
        try
        {
            obstacle.layer=27;obstacle.transform.position=(NpcEye(first)+wolf.Renderer.bounds.center)*.5f;obstacle.transform.rotation=transform.rotation;obstacle.AddComponent<BoxCollider>().size=new Vector3(8,5,.4f);Physics.SyncTransforms();
            first.SimulationAge+=2;sense.NextSense=0;npcSenseFrame=-1;UpdateNpcAwareness(first);
            Check(!sense.Visible.Any(v=>v.Animal==wolf),"A real intervening solid hides the wolf from current perception");
            Check(NpcMemoryFor(first.ResidentId).Social.Incidents.Any(e=>e.Animal==wolf.Spawn.Id),"Losing sight retains an explicitly past sighting instead of revealing its new position");
        }
        finally{DestroyImmediate(obstacle);Physics.SyncTransforms();}
        var group=livingGroups[first.GroupId];var eventItem=new NpcEventQueue.Event{Person=first.ResidentId,Speaker=0,Trigger="You were directly asked about your surroundings."};
        CurrentWeather=new WeatherFrame(6,.8,5,0,.9,0,500,100,1000,4,4,7,0);sense.EnvironmentAt=0;
        string context=NpcContext(first,eventItem,NpcTargets(first,0).ToArray());File.WriteAllText(Path.Combine(output,"grounded-context.txt"),context);
        Check(context.Contains("heavy rain")&&context.Contains("around midday")&&context.Contains("Home environment:")&&context.Contains("Ordinary regional food sources:"),"Current time, actual weather and regional environment/diet reach the real NPC context");
        var belief=sourceWorld.Prehistory.Foragers.Beliefs[group.Residents.Belief];Check(context.Contains(belief.Name)&&context.Contains(belief.MortuaryPractice)&&context.Contains("Personality:")&&context.Contains("Current mood:"),"Individual traits and the actual local belief tradition enter the context");
        var copy=LivingState.Copy();copy.Validate();Check(copy.Minds.Any(m=>m.Person==first.ResidentId&&m.Social.Incidents.Any(e=>e.Animal==wolf.Spawn.Id)),"Witnessed local incidents survive saved-state copying and validation");
        CurrentWeather=savedWeather;PlayerRules.SetLocalSolarHour(savedHour,longitude);SetDaylight(LocalSolarHour,skyYear);RemoveAnimal(wolf.Spawn.Id);
        // Clear the controlled probe's pending reaction before the real-model test.
        npcEvents.Clear();npcPlans.Clear();paused=false;
        double x=second.Motor.X-2,z=second.Motor.Z;motion.Place(x,z,Ground(x,z));motion.Flying=false;UpdateHumans();UpdateCamera();
        // The sensory fixture may spend time streaming the wolf while these
        // residents continue their separate trips. Keep the cancellation probe
        // a controlled close conversation, not a test of where their trips end.
        if(HumanDistance(first.Planet,second.Planet)>10)
        {first.Motor.Place(second.Motor.X+3,second.Motor.Z,Ground(second.Motor.X+3,second.Motor.Z));first.Planet=frame.Position(first.Motor.X,first.Motor.Z);first.Root.transform.localPosition=new Vector3((float)(first.Motor.X-originX),(float)(first.Motor.Y-WorldGen.Presentation.TerrainMotion.EyeHeightM),(float)(first.Motor.Z-originZ));}
        npcModel??=new NpcModelClient();npcModel.Config.BackgroundReactions=false;
        int requests=npcModel.Requests,decisions=npcDecisions;npcModel.Config.BackgroundReactions=true;Check(QueueNpcEvent(first,"A familiar bandmate is passing nearby; greet them briefly."),"A background event is admitted before the player speaks");npcModel.Config.BackgroundReactions=false;UpdateNpcMinds(false);
        until=Time.unscaledTimeAsDouble+190;while(npcModel.Requests==requests&&npcThinking!=null&&!npcThinking.IsCompleted&&Time.unscaledTimeAsDouble<until)yield return null;
        Check(npcPending?.Person==first.ResidentId&&npcThinking!=null&&!npcThinking.IsCompleted,"A real background model request is in flight: "+npcModel.Status+" / "+npcLastError);var interrupted=npcPending;int spoken=npcUtterances;
        npcTalkTarget=second;Check(SpeakToNpc("What time of day is it, and what is the weather like here?"),"The player can directly address a different resident while background inference runs");
        Check(npcSuppressed==interrupted&&npcEvents.HasDirect(second.ResidentId),"Player speech cancels the lower-priority request and goes to the front");
        until=Time.unscaledTimeAsDouble+190;while(npcUtterances==spoken&&Time.unscaledTimeAsDouble<until&&npcLastError.Length==0)yield return null;
        Check(npcUtterances>spoken,"The foreground resident actually delivers streamed local-model speech: "+npcLastError);
        Check(NpcMemoryFor(second.ResidentId).Conversation.Any(t=>t.FromNpc)&&npcDecisions==decisions+1,"The preempted background decision is not executed later; any already-spoken prefix remains legitimate memory");
        File.WriteAllText(Path.Combine(output,"foreground-request.txt"),string.Join("\n",npcModel.LastMessages.Select(m=>m.role+": "+m.content)));
        File.WriteAllText(Path.Combine(output,"foreground-reply.txt"),NpcMemoryFor(second.ResidentId).Conversation.Last(t=>t.FromNpc).Text);
        npcThoughts=true;npcTalkTarget=second;OpenChat();yield return NpcLifeImage(output,"grounded-npc-dialogue");CloseChat();
        File.WriteAllText(Path.Combine(output,"sense-cost.txt"),"Positive perception checks="+npcSenseChecks+"; deferred budget checks="+npcSenseDeferred);
        DisposeNpcMinds();
    }
}
