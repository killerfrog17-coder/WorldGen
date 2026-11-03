using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    sealed class LivingGroup
    {
        public HumanGroupSituation Situation;public ForagerBandSample Residents;public HumanActivitySample[] Activities;
        public V Anchor;public bool Grounded,CampLoaded;public int PlacementAttempt;public double LastSeen;
    }
    sealed class LivingPacket
    {
        public HumanGroupSituation[] Groups;public readonly Dictionary<long,ForagerBandSample> Residents=new Dictionary<long,ForagerBandSample>();
        public readonly Dictionary<long,HumanActivitySample[]> Activities=new Dictionary<long,HumanActivitySample[]>();public double Milliseconds;
    }
    HumanLocalLife livingPlanner;Task<LivingPacket> livingWork;double livingQueryAt,livingLastMs,livingTerrainAt;
    readonly Dictionary<long,LivingGroup> livingGroups=new Dictionary<long,LivingGroup>();
    readonly Dictionary<long,HumanActor> livingPeople=new Dictionary<long,HumanActor>();
    HumanLocalState LivingState=>PlayerRules.Humans??(PlayerRules.Humans=new HumanLocalState());
    HumanLifeClock LivingClock=>new HumanLifeClock(PlayCalendar.Seconds(PlayerRules,SimulationTime.Seconds(sourceWorld.Prehistory)),PlayerRules.SolarDays,
        PlayerRules.Settings.RealMinutesPerDay,PlayerRules.Settings.CalendarDaysPerDay,HorizontalScale,PlayerRules.PrimeMeridianLongitude);
    public string LivingHumanDebug=>livingGroups.Count+" communities / 50 km · "+livingPeople.Count+" residents · "+livingLastMs.ToString("F1")+" ms background query";
    static uint HumanHash(long id)=>Rng.Mix((uint)id^(uint)(id>>32));
    void UpdateLivingHumans(bool stopped,float dt)
    {
        if(sourceWorld?.Prehistory==null||!Prehistory.Foraging(sourceWorld.Prehistory)||humanLibrary==null)return;
        if(livingPlanner==null)livingPlanner=new HumanLocalLife(sourceWorld,new Prehistory(sourceWorld));
        if(livingWork!=null&&livingWork.IsCompleted)
        {
            var work=livingWork;livingWork=null;
            if(work.IsFaulted)Debug.LogException(work.Exception.GetBaseException());
            else if(!closed)ApplyLivingPacket(work.Result);
        }
        if(stopped)return;
        if(livingWork==null&&Time.unscaledTimeAsDouble>=livingQueryAt)
        {
            livingQueryAt=Time.unscaledTimeAsDouble+2;var state=sourceWorld.Prehistory;var clock=LivingClock;var observer=Position;
            double temperature=CurrentWeather.TemperatureC,rain=CurrentWeather.RainMmPerHour;
            livingWork=Task.Run(()=>
            {
                var timer=System.Diagnostics.Stopwatch.StartNew();var packet=new LivingPacket{Groups=livingPlanner.Nearby(state,clock,observer,50000)};
                foreach(var group in packet.Groups)
                {
                    if(closed)break;
                    // Include the whole foraging catchment, not only the camp's
                    // centre. A party can be encountered far from its shelter.
                    if(group.DistanceToPlayerM>2400)continue;
                    var residents=livingPlanner.Residents(group);packet.Residents[group.Band.Id]=residents;
                    packet.Activities[group.Band.Id]=residents.Members.Select(p=>livingPlanner.Activity(group,residents,p,clock,temperature,rain)).ToArray();
                }
                packet.Milliseconds=timer.Elapsed.TotalMilliseconds;return packet;
            });
        }
        long started=System.Diagnostics.Stopwatch.GetTimestamp();
        foreach(var group in livingGroups.Values.OrderBy(g=>g.Situation.DistanceToPlayerM))
        {
            if(group.Residents==null||group.Situation.DistanceToPlayerM>2400)continue;
            if(!group.Grounded&&group.Situation.Phase!=HumanCampPhase.Travelling)
            {
                if(TryLivingCampGround(group,out var anchor)){group.Anchor=anchor;group.Grounded=true;}
                if(ElapsedHumanMs(started)>1.5)break;
                if(!group.Grounded)continue;
            }
            if(group.Grounded&&!group.CampLoaded&&HumanDistance(group.Anchor,Position)<700)
            {LoadLivingCamp(group);if(ElapsedHumanMs(started)>1.5)break;}
            if(group.Activities==null)continue;
            foreach(var activity in group.Activities)
            {
                V point=LivingPosition(group,activity.Position);
                if(HumanDistance(point,Position)>650||livingPeople.ContainsKey(activity.Person))continue;
                var memory=LivingState.People.FirstOrDefault(p=>p.Person==activity.Person);if(memory?.Dead==true)continue;
                var person=group.Residents.Members.First(p=>p.Id==activity.Person);
                if(CreateLivingHuman(group,person,activity,point,memory))break;
            }
            if(ElapsedHumanMs(started)>1.5)break;
        }
        foreach(var actor in livingPeople.Values.ToArray())
        {
            if(HumanDistance(actor.Planet,Position)<1100&&livingGroups.ContainsKey(actor.GroupId))continue;
            CaptureLivingPerson(actor);DestroyHuman(actor);humanNpcs.Remove(actor);livingPeople.Remove(actor.ResidentId);
        }
        foreach(var group in livingGroups.Values.Where(g=>g.CampLoaded&&HumanDistance(g.Anchor,Position)>1400).ToArray())UnloadLivingCamp(group);
    }
    static double ElapsedHumanMs(long start)=>(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000d/System.Diagnostics.Stopwatch.Frequency;
    double HumanDistance(V a,V b)=>HumanLocalLife.Angle(a,b)*sourceWorld.Recipe.RadiusKm*1000*HorizontalScale;
    V LivingPosition(LivingGroup group,V point)
    {
        // Terrain validation translates the complete camp and its household
        // layout together; it never pulls the ground toward a candidate site.
        if(!group.Grounded||group.Situation.Phase==HumanCampPhase.Travelling)return point;
        return (point+group.Anchor-group.Situation.Camp.Position).Unit;
    }
    void ApplyLivingPacket(LivingPacket packet)
    {
        livingLastMs=packet.Milliseconds;var present=new HashSet<long>();
        foreach(var situation in packet.Groups)
        {
            long id=situation.Band.Id;present.Add(id);
            if(!livingGroups.TryGetValue(id,out var group))livingGroups[id]=group=new LivingGroup{Situation=situation};
            if(group.Situation.Camp.Id!=situation.Camp.Id||situation.Phase==HumanCampPhase.Travelling&&group.CampLoaded)
            {UnloadLivingCamp(group);group.Grounded=false;group.PlacementAttempt=0;}
            group.Situation=situation;group.LastSeen=Time.unscaledTimeAsDouble;
            if(packet.Residents.TryGetValue(id,out var residents)){group.Residents=residents;group.Activities=packet.Activities[id];}
            foreach(var actor in livingPeople.Values.Where(p=>p.GroupId==id))
                actor.Routine=group.Activities?.FirstOrDefault(a=>a.Person==actor.ResidentId)??actor.Routine;
        }
        foreach(var id in livingGroups.Keys.Where(id=>!present.Contains(id)).ToArray())
        {UnloadLivingCamp(livingGroups[id]);livingGroups.Remove(id);}
    }
    bool TryLivingCampGround(LivingGroup group,out V anchor)
    {
        var memory=LivingState.Camps.FirstOrDefault(c=>c.Site==group.Situation.Camp.Id);
        if(memory!=null){anchor=memory.Position;return true;}
        anchor=default;var at=group.Situation.Camp.Position;TerrainHandoff.Coordinates(frame,at,out double cx,out double cz);
        // Stable expanding search, resumed across frames. Dry, modest slopes
        // and room for the household ring are required before a camp appears.
        for(int step=0;step<2;step++)
        {
            int attempt=group.PlacementAttempt++;double bearing=attempt*2.399963229728653,range=attempt==0?0:Math.Sqrt(attempt)*7;
            double x=cx+Math.Sin(bearing)*range,z=cz+Math.Cos(bearing)*range;var position=frame.Position(x,z);var sample=walkingPatch.Sample(position);
            if(sample.Underwater||!double.IsNaN(sample.WaterSurfaceM)&&sample.WaterSurfaceM>sample.ElevationM-.15)continue;
            double center=Ground(x,z),spread=0;bool suitable=true;
            for(int k=0;k<8;k++)
            {
                double a=k*Math.PI/4,px=x+Math.Sin(a)*14,pz=z+Math.Cos(a)*14;var edge=walkingPatch.Sample(frame.Position(px,pz));
                double h=Ground(px,pz);spread=Math.Max(spread,Math.Abs(h-center));
                if(edge.Underwater||Math.Abs(h-center)>2.7){suitable=false;break;}
            }
            if(!suitable)continue;
            anchor=position;return true;
        }
        // No unsafe fallback after a fixed attempt count. The next frame
        // continues searching rather than placing houses under water/on cliffs.
        return false;
    }
    void LoadLivingCamp(LivingGroup group)
    {
        var memory=LivingState.Camps.FirstOrDefault(c=>c.Site==group.Situation.Camp.Id);
        var existing=Camp.Objects.Where(c=>c.NpcSite==group.Situation.Camp.Id&&c.NpcGroup==group.Situation.Band.Id).ToArray();
        var objects=existing.Length>0?existing:memory!=null&&memory.Disturbed?memory.Objects.Select(o=>o.Copy()).ToArray():HumanCampLayout.Create(group.Situation,group.Residents,group.Anchor,frame.RadiusM,sourceWorld.Recipe.Seed,Camp.SimulatedSeconds);
        foreach(var obj in objects)
        {
            if(Camp.Objects.Any(c=>c.Id==obj.Id))continue;
            if(obj.Blueprint!=null&&obj.Blueprint.FloorHeights==null)SurveyLivingShelter(obj);
            obj.LastSimulatedSeconds=Camp.SimulatedSeconds;Camp.Objects.Add(obj);
        }
        group.CampLoaded=true;
    }
    void SurveyLivingShelter(CampObject obj)
    {
        var plan=obj.Blueprint;TerrainHandoff.Coordinates(frame,obj.Position,out double x,out double z);double floor=Ground(x,z);plan.FloorHeights=new double[9];
        double yaw=obj.Heading*Math.PI/180;
        for(int i=0;i<3;i++)for(int j=0;j<3;j++)
        {double east=(i-1)*plan.WidthM*.5,north=(j-1)*plan.DepthM*.5;plan.FloorHeights[i*3+j]=Math.Max(-1.9,Math.Min(1.9,Ground(x+east*Math.Cos(yaw)+north*Math.Sin(yaw),z+north*Math.Cos(yaw)-east*Math.Sin(yaw))-floor));}
        foreach(var component in CampBlueprints.Components(plan))
        {
            var state=CampBlueprints.State(plan,component);if(state==null)continue;
            foreach(var material in state.Materials)
            {
                if(component.Kg>0){material.MassKg=component.Kg/state.Materials.Count;continue;}
                double length=component.LengthM+.01;material.MassKg*=length/material.HeightM;material.HeightM=length;
            }
        }
    }
    void UnloadLivingCamp(LivingGroup group)
    {
        if(!group.CampLoaded)return;
        StoreLivingCamp(group);
        foreach(var obj in Camp.Objects.Where(c=>c.NpcSite==group.Situation.Camp.Id&&c.NpcGroup==group.Situation.Band.Id).ToArray())
        {InvalidateCampVisual(obj);Camp.Objects.Remove(obj);}group.CampLoaded=false;
    }
    void StoreLivingCamp(LivingGroup group)
    {
        if(!group.CampLoaded)return;var state=LivingState;
        var memory=state.Camps.FirstOrDefault(c=>c.Site==group.Situation.Camp.Id);
        if(memory==null){memory=new HumanCampMemory{Site=group.Situation.Camp.Id,Group=group.Situation.Band.Id,Position=group.Anchor};state.Camps.Add(memory);}
        memory.LastSolarDay=PlayerRules.SolarDays;memory.Objects=Camp.Objects.Where(c=>c.NpcSite==memory.Site&&c.NpcGroup==memory.Group).Select(c=>c.Copy()).ToList();
        // Actual visited camp state is retained, including player edits and
        // removed material. It cannot regenerate free goods when revisited.
        memory.Disturbed=true;
    }
    bool CreateLivingHuman(LivingGroup group,ForagerPersonSample person,HumanActivitySample activity,V point,HumanPersonMemory memory)
    {
        TerrainHandoff.Coordinates(frame,point,out double x,out double z);var sample=walkingPatch.Sample(point);
        if(sample.Underwater)return false;
        uint hash=HumanHash(person.Id);int variant=person.Female?new[]{0,2,4}[hash%3]:new[]{1,3,5}[hash%3];
        int id=unchecked((int)(hash&0x3fffffffu))+100000;
        while(humanNpcs.Any(p=>p.Id==id))id++;
        var actor=CreateHuman(variant,id,true);actor.ResidentId=person.Id;actor.GroupId=group.Situation.Band.Id;actor.Resident=person;actor.Routine=activity;
        actor.Root.name=person.Name+" · "+group.Situation.Band.Name;
        actor.Health=memory?.Health??100;actor.Bleeding=memory?.Bleeding?.Copy();actor.Motor=new TerrainMotion();actor.Motor.Place(x,z,Ground(x,z));actor.Planet=point;
        actor.Yaw=actor.TargetYaw=(float)(hash%360);actor.Root.transform.localPosition=new Vector3((float)(x-originX),(float)(actor.Motor.Y-TerrainMotion.EyeHeightM),(float)(z-originZ));
        actor.Root.transform.localRotation=Quaternion.Euler(0,actor.Yaw,0);
        // Family identities/ages are already real. Age-specific fitted skins
        // are supplied by the appearance stage; never silently omit children.
        if(person.AgeYears<16)actor.Root.transform.localScale=Vector3.one*(float)Math.Max(.42,.48+person.AgeYears*.032);
        livingPeople.Add(person.Id,actor);humanNpcs.Add(actor);return true;
    }
    void UpdateLivingHuman(HumanActor actor,bool stopped,float dt)
    {
        if(actor.Health<=0){actor.Ragdoll?.SetPaused(stopped);return;}
        if(!livingGroups.TryGetValue(actor.GroupId,out var group)||actor.Routine==null)return;
        var routine=actor.Routine;V target=LivingPosition(group,routine.Position);
        TerrainHandoff.Coordinates(frame,target,out double tx,out double tz);
        if(dt>0)
        {
            double dx=tx-actor.Motor.X,dz=tz-actor.Motor.Z,distance=Math.Sqrt(dx*dx+dz*dz);
            var memory=LivingState.Groups.FirstOrDefault(g=>g.Group==actor.GroupId);
            bool alarm=memory!=null&&memory.AlarmUntilSolarDay>PlayerRules.SolarDays;
            if(alarm)
            {
                TerrainHandoff.Coordinates(frame,memory.ThreatPosition,out double hx,out double hz);dx=actor.Motor.X-hx;dz=actor.Motor.Z-hz;distance=Math.Sqrt(dx*dx+dz*dz);
                if(distance<90){actor.TargetSpeed=personRunSpeed(actor.Resident);actor.TargetYaw=(float)(Math.Atan2(dx,dz)*180/Math.PI);}else actor.TargetSpeed=0;
            }
            else
            {
                actor.TargetSpeed=distance>1.2?(float)Math.Min(actor.Resident.AgeYears<10?1:1.35,.55+distance*.35):0;
                if(actor.TargetSpeed>0)actor.TargetYaw=(float)(Math.Atan2(dx,dz)*180/Math.PI);
                else if(routine.Activity==HumanActivity.Socialise||routine.Activity==HumanActivity.Teach||routine.Activity==HumanActivity.Childcare)
                {TerrainHandoff.Coordinates(frame,group.Anchor,out double gx,out double gz);actor.TargetYaw=(float)(Math.Atan2(gx-actor.Motor.X,gz-actor.Motor.Z)*180/Math.PI);}
            }
            // Unfamiliar visitors attract a look, not supernatural detection or
            // an immediate full-speed flight response. Aggression raises alarm.
            double playerRange=Math.Sqrt(Math.Pow(actor.Motor.X-motion.X,2)+Math.Pow(actor.Motor.Z-motion.Z,2));
            if(!alarm&&playerRange<12&&playerRange>2&&actor.TargetSpeed<.1&&routine.Activity!=HumanActivity.Sleep)
                actor.TargetYaw=(float)(Math.Atan2(motion.X-actor.Motor.X,motion.Z-actor.Motor.Z)*180/Math.PI);
            actor.Yaw=Mathf.MoveTowardsAngle(actor.Yaw,actor.TargetYaw,dt*95);actor.Speed=Mathf.MoveTowards(actor.Speed,actor.TargetSpeed,dt*1.6f);
            if(Time.unscaledTime>=actor.ObstacleAt)
            {
                actor.ObstacleAt=Time.unscaledTime+.4;double a=actor.Yaw*Math.PI/180,px=actor.Motor.X+Math.Sin(a)*1.4,pz=actor.Motor.Z+Math.Cos(a)*1.4;
                var next=walkingPatch.Sample(frame.Position(px,pz));double floor=Ground(actor.Motor.X,actor.Motor.Z);
                if(next.Underwater||Math.Abs(Ground(px,pz)-floor)>.65){actor.Speed=0;actor.TargetYaw+=65;}
                var origin=transform.TransformPoint(new Vector3((float)(actor.Motor.X-originX),(float)floor+.8f,(float)(actor.Motor.Z-originZ)));
                if(Physics.SphereCast(origin,.23f,transform.TransformDirection(new Vector3((float)Math.Sin(a),0,(float)Math.Cos(a))),out var hit,1.3f,1<<ScatterSolidLayer,QueryTriggerInteraction.Ignore))
                {actor.Speed=Math.Min(actor.Speed,.25f);actor.TargetYaw+=55;}
            }
            double beforeX=actor.Motor.X,beforeZ=actor.Motor.Z;
            actor.Motor.Move(dt,actor.Speed/TerrainMotion.WalkSpeedMps,0,0,actor.Yaw,0,false,false,Ground,true);
            float actual=(float)(Math.Sqrt(Math.Pow(actor.Motor.X-beforeX,2)+Math.Pow(actor.Motor.Z-beforeZ,2))/dt);
            float interval=playerRange<40?0:playerRange<120?1f/30:playerRange<300?1f/15:1f/8;
            if(Time.unscaledTime>=actor.AnimationAt)
            {
                float elapsed=Math.Max(dt,interval);actor.AnimationAt=Time.unscaledTime+interval;AnimateHuman(actor,actual,!actor.Motor.Grounded,false,elapsed);
                string role=actual>.2f?null:LivingWorkRole(routine.Activity);
                actor.Shared?.Routine(role,Time.unscaledTime+HumanHash(actor.ResidentId)%100,elapsed);actor.Graph.Evaluate(0);
            }
        }
        actor.Planet=frame.Position(actor.Motor.X,actor.Motor.Z);
        actor.Root.transform.localPosition=new Vector3((float)(actor.Motor.X-originX),(float)(actor.Motor.Y-TerrainMotion.EyeHeightM),(float)(actor.Motor.Z-originZ));actor.Root.transform.localRotation=Quaternion.Euler(0,actor.Yaw,0);
    }
    static float personRunSpeed(ForagerPersonSample person)=>person.AgeYears<10?2.8f:person.AgeYears>65?3.2f:4.8f;
    static string LivingWorkRole(HumanActivity activity)=>activity==HumanActivity.GatherPlants||activity==HumanActivity.GatherFuel?"Gather":activity==HumanActivity.FetchWater?"Drink":activity==HumanActivity.PrepareFood?"Eat":activity==HumanActivity.Craft?"WorkKnapping":activity==HumanActivity.BuildCamp||activity==HumanActivity.RepairShelter?"WorkTwist":null;
    void CaptureLivingPerson(HumanActor actor)
    {
        if(actor.ResidentId==0||actor.Health>=100&&(actor.Bleeding==null||actor.Bleeding.Rate<=0))return;
        var memory=LivingState.People.FirstOrDefault(p=>p.Person==actor.ResidentId);
        if(memory==null){memory=new HumanPersonMemory{Person=actor.ResidentId,Group=actor.GroupId};LivingState.People.Add(memory);}
        memory.Health=actor.Health;memory.Dead=actor.Health<=0;memory.Position=actor.Planet;memory.SolarDays=PlayerRules.SolarDays;memory.Bleeding=actor.Bleeding?.Copy();
    }
    void CaptureLivingHumans()
    {foreach(var actor in livingPeople.Values)CaptureLivingPerson(actor);foreach(var group in livingGroups.Values)StoreLivingCamp(group);}
    void AlarmLivingGroup(HumanActor actor)
    {
        if(actor.ResidentId==0)return;var memory=LivingState.Groups.FirstOrDefault(g=>g.Group==actor.GroupId);
        if(memory==null){memory=new HumanGroupMemory{Group=actor.GroupId};LivingState.Groups.Add(memory);}
        memory.Trust=Math.Max(-1,memory.Trust-.45);memory.Fear=Math.Min(1,memory.Fear+.5);memory.ThreatPosition=Position;
        memory.AlarmUntilSolarDay=PlayerRules.SolarDays+120/(PlayerRules.Settings.RealMinutesPerDay*60);memory.LastSolarDay=PlayerRules.SolarDays;CaptureLivingPerson(actor);
    }
    void DisposeLivingHumans(){livingGroups.Clear();livingPeople.Clear();}
    PlayCommandResult ExecuteLivingCamps(string[] words)
    {
        var groups=livingGroups.Values.OrderBy(g=>g.Situation.DistanceToPlayerM).ToArray();
        if(words.Length==1)
        {
            var lines=new List<string>{LivingHumanDebug};
            for(int i=0;i<Math.Min(12,groups.Length);i++)
            {var g=groups[i].Situation;lines.Add((i+1)+". "+g.Band.Name+" · "+(g.DistanceToPlayerM/1000).ToString("F1")+" km · "+g.Band.People+" people · "+g.Phase);}
            lines.Add("/camps <number> for community details; /camps visit <number> to teleport for review.");return new PlayCommandResult(true,lines.ToArray());
        }
        bool visit=words.Length==3&&words[1].Equals("visit",StringComparison.OrdinalIgnoreCase);
        if(!int.TryParse(words[words.Length-1],out int index)||index<1||index>groups.Length)return new PlayCommandResult(false,"No such nearby community. /camps lists those within 50 km.");
        var group=groups[index-1];var band=group.Situation.Band;var f=sourceWorld.Prehistory.Foragers;
        if(visit)
        {
            V destination=group.Grounded?group.Anchor:group.Situation.Position;Teleport(destination);motion.Y=Ground(0,0)+TerrainMotion.EyeHeightM;
            motion.VerticalSpeed=0;UpdateCamera();livingQueryAt=0;return new PlayCommandResult(true,"Visiting "+band.Name+".");
        }
        return new PlayCommandResult(true,band.Name,"Culture: "+f.Cultures[band.Culture].Name+" · language: "+f.Languages[band.Language].Name,
            "People: "+(group.Residents?.Members.Length??band.People)+" · "+band.Residence+" · "+group.Situation.Phase,
            "Camp: "+CampBlueprints.Name(group.Situation.Camp.Design)+" · "+group.Situation.Camp.Cover+" · "+Math.Max(0,group.Situation.DepartureDay-LivingClock.CalendarDay).ToString("F1")+" calendar days until departure.");
    }
}
