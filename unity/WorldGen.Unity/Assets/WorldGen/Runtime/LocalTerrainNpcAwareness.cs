using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    sealed class NpcAnimalView {public Animal Animal;public string Name;public double Distance,At;public bool Dangerous;}
    sealed class NpcAwareness
    {
        public double NextSense;public NpcPersonality Personality;
        public readonly List<NpcAnimalView> Visible=new List<NpcAnimalView>();
        public string Environment="";public int Cell=-1;public double EnvironmentAt;
    }
    readonly Dictionary<long,NpcAwareness> npcAwareness=new Dictionary<long,NpcAwareness>();
    int npcSenseFrame=-1;long npcSenseTicks;int npcSenseChecks,npcSenseDeferred;
    NpcAwareness NpcAwarenessFor(HumanActor actor)
    {
        if(!npcAwareness.TryGetValue(actor.ResidentId,out var a))
        {var culture=sourceWorld.Prehistory.Foragers.Cultures[livingGroups[actor.GroupId].Residents.Culture];a=new NpcAwareness{Personality=NpcPersonality.For(actor.Resident,culture)};npcAwareness.Add(actor.ResidentId,a);}return a;
    }
    Vector3 NpcEye(HumanActor actor)=>actor.Root.transform.position+transform.up*((actor.AgeModel?.EyeHeight??actor.Definition.EyeHeight)*actor.Root.transform.localScale.y*.94f);
    bool NpcSees(HumanActor actor,Vector3 target,double maximum,bool grass=true)
    {
        var eye=NpcEye(actor);var delta=target-eye;double distance=delta.magnitude;if(distance>maximum)return false;if(distance<.1)return true;
        if(distance>3&&Vector3.Dot(actor.Root.transform.forward,delta.normalized)<-.1f)return false;
        if(TraceCombatObstacle(new Ray(eye,delta.normalized),(float)Math.Max(0,distance-.15),checkGround:false)!=null)return false;
        var from=transform.InverseTransformPoint(eye);var to=transform.InverseTransformPoint(target);double depth=0;
        for(int i=0;i<8;i++)
        {
            double t=(i+.5)/8,x=from.x+originX+(to.x-from.x)*t,z=from.z+originZ+(to.z-from.z)*t,y=from.y+(to.y-from.y)*t;
            if(HuntingColumn(x,z,out var cover,out var floor,out var slope))
            {if(floor>y+.08)return false;if(grass){depth+=WildlifePerception.GrassDepth(cover,y-floor,distance/8,slope);if(depth>3.2)return false;}}
            else if(VisibleGround(x,z,double.NegativeInfinity)>y+.08)return false;
        }
        return true;
    }
    double NpcSightRange(HumanActor actor)
    {
        double light=SkyFrame?.Daylight??1,range=8+70*Math.Sqrt(Math.Max(0,light));
        if(light<.1)foreach(var fire in Camp.Objects)if(fire.Kind==CampKind.Fire&&fire.Burning&&HumanDistance(actor.Planet,fire.Position)<9){range=Math.Max(range,15);break;}
        return Math.Min(range,CurrentWeather.VisibilityM*.7);
    }
    IEnumerable<Animal> NpcAnimalCandidates(HumanActor actor)
    {
        int x=(int)Math.Floor(actor.Motor.X/40),z=(int)Math.Floor(actor.Motor.Z/40);
        for(int dz=-2;dz<=2;dz++)for(int dx=-2;dx<=2;dx++)if(wildlifeNeighbours.TryGetValue((x+dx,z+dz),out var bucket))foreach(var animal in bucket)
            if(!animal.Retired&&animal.Root!=null&&animal.MassKg>=.25)yield return animal;
    }
    void UpdateNpcAwareness(HumanActor actor)
    {
        if(actor.Resident==null||actor.Health<=0||actor.CurrentActivity==HumanActivity.Sleep||HumanDistance(actor.Planet,Position)>250)return;
        var perception=NpcAwarenessFor(actor);if(actor.SimulationAge<perception.NextSense)return;
        if(npcSenseFrame!=Time.frameCount){npcSenseFrame=Time.frameCount;npcSenseTicks=0;}
        double budget=campSleeping||campResting?2:1;
        if(npcSenseTicks*1000d/System.Diagnostics.Stopwatch.Frequency>budget){npcSenseDeferred++;perception.NextSense=actor.SimulationAge+.05;return;}
        long began=System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            perception.NextSense=actor.SimulationAge+.7+HumanHash(actor.ResidentId)%17*.025;perception.Visible.Clear();
            var state=NpcMemoryFor(actor.ResidentId).Social;state.Advance(PlayerRules.SolarDays);
            double range=NpcSightRange(actor);
            var candidates=NpcAnimalCandidates(actor).Where(a=>HumanDistance(a.Planet,actor.Planet)<range).OrderBy(a=>HumanDistance(a.Planet,actor.Planet)).Take(12);
            foreach(var animal in candidates)
            {
                var target=animal.Renderer!=null?animal.Renderer.bounds.center:animal.Root.transform.position+Vector3.up*.4f;
                if(!NpcSees(actor,target,range))continue;npcSenseChecks++;
                string name=animal.Spawn.Profile.name;double distance=HumanDistance(animal.Planet,actor.Planet);
                bool danger=!animal.Dead&&animal.Behaviour?.Predator==true&&animal.MassKg>=18;
                perception.Visible.Add(new NpcAnimalView{Animal=animal,Name=name,Distance=distance,At=actor.SimulationAge,Dangerous=danger});
                if(!danger)continue;
                var old=state.Incidents.LastOrDefault(e=>e.Kind==NpcIncidentKind.AnimalSeen&&e.Animal==animal.Spawn.Id);
                bool first=old==null||PlayerRules.SolarDays-old.SolarDay>.1;
                double concern=(distance<12?.8:distance<30?.45:.18)*(1-perception.Personality.Courage*.45);
                state.Fear=Math.Max(state.Fear,concern);
                NoticeNpcAnimalThreat(actor,animal,distance);
                state.Record(new NpcIncident{Kind=NpcIncidentKind.AnimalSeen,Animal=animal.Spawn.Id,SolarDay=PlayerRules.SolarDays,Position=animal.Planet,Detail="You saw a "+name+" "+(distance<15?"close by":distance<35?"nearby":"farther away")+". It was "+(animal.BehaviourAction=="hunt"?"hunting":animal.BehaviourAction=="attack"?"attacking":"moving or resting")+"."});
                if(first&&distance<25)QueueNpcEvent(actor,"You have just noticed a "+name+" nearby. Your concern is about that animal. React proportionately; seeing it does not mean it has attacked.");
            }
            // Knowing that a relative exists does not reveal their whereabouts.
            foreach(var other in livingPeople.Values.Where(p=>p!=actor&&p.GroupId==actor.GroupId&&HumanDistance(actor.Planet,p.Planet)<Math.Min(22,range)).Take(12))
                if(NpcSees(actor,other.Root.transform.position+transform.up*.7f,range))
                {if(other.Health<=0)DiscoverNpcDeath(actor,other);else state.See(other.ResidentId,other.Planet,PlayerRules.SolarDays);}
        }
        finally{npcSenseTicks+=System.Diagnostics.Stopwatch.GetTimestamp()-began;}
    }
    string NpcWorldContext(HumanActor actor,LivingGroup group)
    {
        var perception=NpcAwarenessFor(actor);var f=sourceWorld.Prehistory.Foragers;var culture=f.Cultures[group.Residents.Culture];var belief=f.Beliefs[group.Residents.Belief];
        if(perception.Environment.Length==0||Time.unscaledTimeAsDouble>perception.EnvironmentAt)
        {
            var sample=walkingPatch.Sample(actor.Planet);perception.Cell=sample.Cell;var district=f.Districts[group.Residents.District];
            var biota=group.Residents.District<f.Biota.Length?f.Biota[group.Residents.District]:null;
            perception.Environment=NpcLocalKnowledge.Environment(sample.Biome,district,culture,biota,sourceWorld.Ecology?.Taxa)+" "+NpcLocalKnowledge.Beliefs(belief,sample.Biome,perception.Personality.Devotion);
            perception.EnvironmentAt=Time.unscaledTimeAsDouble+12;
        }
        var text=new StringBuilder(" Current time: "+NpcLocalKnowledge.Daypart(LocalSolarHour,group.Situation.DaylightHours)+". Weather you feel and see: "+NpcLocalKnowledge.Weather(CurrentWeather)+". "+perception.Environment+" "+perception.Personality.Describe());
        var social=NpcMemoryFor(actor.ResidentId).Social;social.Advance(PlayerRules.SolarDays);text.Append(" Current mood: ").Append(social.Mood).Append('.');
        var visible=perception.Visible.Where(s=>actor.SimulationAge-s.At<2&&!s.Animal.Retired).OrderBy(s=>s.Distance).Take(4).ToArray();
        if(visible.Length>0)text.Append(" Animals actually seen recently: ").Append(string.Join("; ",visible.Select(s=>s.Name+(s.Animal.Dead?" (dead)":s.Distance<15?" (close)":" (farther away)")))).Append('.');
        else text.Append(" No animal has been positively identified in your immediate view; do not invent a visible wolf or prey.");
        foreach(var e in social.Incidents.TakeLast(4))text.Append(" Remembered ").Append(e.FirstHand?"firsthand":"reported").Append(" event: ").Append(e.Detail).Append('.');
        return text.ToString();
    }
}
