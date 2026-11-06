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
        public V Anchor;public bool Grounded,CampLoaded,TraceOnly;public int PlacementAttempt,ConstructionStep=-1;public double LastSeen,TraceAge;
        public bool BandHas(int k)=>(Residents?.Techniques??Situation.Band.Techniques)[k];
        public HumanActivitySample TenderDuty;public CampObject TendedFire;public double TendUntil,NextTend;
        public NpcResourceSites Resources;
        public NpcSiteSurvey SiteSurvey;public double SiteSurveyRadius=200;
    }
    sealed class LivingPacket
    {
        public HumanGroupSituation[] Groups;public double SolarDay;public PrehistoryState Source;
        public readonly Dictionary<long,ForagerBandSample> Residents=new Dictionary<long,ForagerBandSample>();
        public readonly Dictionary<long,HumanActivitySample[]> Activities=new Dictionary<long,HumanActivitySample[]>();public readonly List<HumanCampTrace> Traces=new List<HumanCampTrace>();public double Milliseconds;
    }
    HumanLocalLife livingPlanner;Task<LivingPacket> livingWork;double livingQueryAt,livingLastMs,livingTerrainAt;
    double livingCampLifeAt,livingTickMs;
    bool livingBenchmarkFreeze;
    readonly bool profileLiving=Environment.GetCommandLineArgs().Contains("--worldgen-npc-perf");
    readonly double[] livingCosts=new double[5];
    long LivingCost(int slot,long start)
    {if(!profileLiving)return 0;livingCosts[slot]+=ElapsedHumanMs(start);return System.Diagnostics.Stopwatch.GetTimestamp();}
    readonly Dictionary<long,LivingGroup> livingGroups=new Dictionary<long,LivingGroup>();
    readonly Dictionary<long,LivingGroup> livingTraces=new Dictionary<long,LivingGroup>();
    readonly Dictionary<long,HumanActor> livingPeople=new Dictionary<long,HumanActor>();
    LivingGroup[] livingOrdered=Array.Empty<LivingGroup>();readonly List<HumanActor> retiredLivingPeople=new List<HumanActor>();
    HumanLocalState LivingState=>PlayerRules.Humans??(PlayerRules.Humans=new HumanLocalState());
    HumanLifeClock LivingClock=>new HumanLifeClock(PlayCalendar.Seconds(PlayerRules,SimulationTime.Seconds(sourceWorld.Prehistory)),PlayerRules.SolarDays,
        PlayerRules.Settings.RealMinutesPerDay,PlayerRules.Settings.CalendarDaysPerDay,HorizontalScale,PlayerRules.PrimeMeridianLongitude);
    public string LivingHumanDebug=>livingGroups.Count+" communities / 50 km · "+livingPeople.Count+" residents · "+livingLastMs.ToString("F1")+" ms background query";
    static uint HumanHash(long id)=>Rng.Mix((uint)id^(uint)(id>>32));
    string HumanTargetLabel(HumanActor actor)
    {
        string condition=actor.Health<=0?"corpse":actor.Health.ToString("0.#")+"/100 HP";
        if(actor.Resident==null)return "Human · "+condition;
        var f=sourceWorld.Prehistory.Foragers;var group=livingGroups.TryGetValue(actor.GroupId,out var value)?value:null;
        return actor.Resident.Name+" · "+actor.Resident.AgeYears.ToString("0")+" years · "+condition+"\n"+
            (group==null?"":f.Cultures[group.Situation.Band.Culture].Name+" · "+f.Languages[group.Situation.Band.Language].Name+" · "+f.Lineages[group.Situation.Band.Lineage].Name+"\n")+actor.CurrentActivity;
    }
    void ScheduleLivingQuery()
    {
        livingQueryAt=Time.unscaledTimeAsDouble+2;var state=sourceWorld.Prehistory;var clock=LivingClock;var observer=Position;
        var physicalGroups=livingPeople.Values.Select(p=>p.GroupId).ToHashSet();
        double temperature=CurrentWeather.TemperatureC,rain=CurrentWeather.RainMmPerHour;
        livingWork=Task.Run(()=>
        {
            var timer=System.Diagnostics.Stopwatch.StartNew();var packet=new LivingPacket{Groups=livingPlanner.Nearby(state,clock,observer,50000),SolarDay=clock.SolarDays,Source=state};
            foreach(var group in packet.Groups)
            {
                if(closed)break;
                foreach(var trace in livingPlanner.RecentCamps(group,clock))
                    if(HumanLocalLife.Angle(trace.Site.Position,observer)*sourceWorld.Recipe.RadiusKm*1000*clock.HorizontalScale<900)
                    {packet.Traces.Add(trace);if(!packet.Residents.ContainsKey(group.Band.Id))packet.Residents[group.Band.Id]=livingPlanner.Residents(group);}
                // Include the whole foraging catchment, not only the camp's
                // centre. A party can be encountered far from its shelter.
                if(group.DistanceToPlayerM>2400&&!physicalGroups.Contains(group.Band.Id))continue;
                var residents=livingPlanner.Residents(group);packet.Residents[group.Band.Id]=residents;
                packet.Activities[group.Band.Id]=residents.Members.Select(p=>livingPlanner.Activity(group,residents,p,clock,temperature,rain)).ToArray();
            }
            packet.Milliseconds=timer.Elapsed.TotalMilliseconds;return packet;
        });
    }
    void UpdateLivingHumans(bool stopped,float dt)
    {
        if(sourceWorld?.Prehistory==null||!Prehistory.Foraging(sourceWorld.Prehistory)||humanLibrary==null)return;
        if(livingPlanner==null)livingPlanner=new HumanLocalLife(sourceWorld,new Prehistory(sourceWorld));
        if(livingWork!=null&&livingWork.IsCompleted)
        {
            var work=livingWork;livingWork=null;
            if(work.IsFaulted)Debug.LogException(work.Exception.GetBaseException());
            else if(!closed&&PrehistoryState.SameDomain(work.Result.Source,sourceWorld.Prehistory)&&Math.Abs(PlayerRules.SolarDays-work.Result.SolarDay)*PlayerRules.Settings.RealMinutesPerDay*60<5)ApplyLivingPacket(work.Result);
        }
        if(stopped)return;
        UpdateCampClearing();
        UpdateLivingSites();
        UpdateLivingResources();
        if(Time.unscaledTimeAsDouble>=livingCampLifeAt)
        {livingCampLifeAt=Time.unscaledTimeAsDouble+.5;foreach(var group in livingGroups.Values)AdvanceLivingCampLife(group);foreach(var trace in livingTraces.Values)if(trace.CampLoaded)AdvanceLivingTrace(trace);}
        if(livingWork==null&&Time.unscaledTimeAsDouble>=livingQueryAt)ScheduleLivingQuery();
        long started=System.Diagnostics.Stopwatch.GetTimestamp();
        foreach(var group in livingOrdered)
        {
            if(group.Residents==null||group.Situation.DistanceToPlayerM>2400)continue;
            if(!group.Grounded&&group.Situation.Phase!=HumanCampPhase.Travelling)
            {
                if(TryLivingCampGround(group,out var anchor)){group.Anchor=anchor;group.Grounded=true;}
                if(ElapsedHumanMs(started)>1.5)break;
            }
            if(group.Grounded&&!group.CampLoaded&&HumanDistance(group.Anchor,Position)<700)
            {LoadLivingCamp(group);if(ElapsedHumanMs(started)>1.5)break;}
            if(group.Activities==null)continue;
            foreach(var activity in group.Activities)
            {
                if(livingPeople.ContainsKey(activity.Person))continue;
                bool away=activity.Activity==HumanActivity.Travel||activity.Activity==HumanActivity.GatherPlants||activity.Activity==HumanActivity.GatherFuel||activity.Activity==HumanActivity.FetchWater||activity.Activity==HumanActivity.Carried;
                if(!group.Grounded&&group.Situation.Phase!=HumanCampPhase.Travelling&&!away)continue;
                V point=LivingPosition(group,activity.Position);
                if(HumanDistance(point,Position)>650)continue;
                var memory=LivingState.People.FirstOrDefault(p=>p.Person==activity.Person);if(memory?.Dead==true)continue;
                var person=group.Residents.Members.First(p=>p.Id==activity.Person);
                if(CreateLivingHuman(group,person,activity,point,memory))break;
            }
            if(ElapsedHumanMs(started)>1.5)break;
        }
        retiredLivingPeople.Clear();foreach(var actor in livingPeople.Values)
        {
            if(HumanDistance(actor.Planet,Position)<1100&&livingGroups.ContainsKey(actor.GroupId))continue;
            retiredLivingPeople.Add(actor);
        }
        foreach(var actor in retiredLivingPeople){CaptureLivingPerson(actor);DestroyHuman(actor);humanNpcs.Remove(actor);livingPeople.Remove(actor.ResidentId);}
        foreach(var group in livingOrdered)if(group.CampLoaded&&HumanDistance(group.Anchor,Position)>1400)UnloadLivingCamp(group);
        if(ElapsedHumanMs(started)<1.5)foreach(var trace in livingTraces.Values)
        {
            if(trace.CampLoaded&&HumanDistance(trace.Anchor,Position)>400)UnloadLivingCamp(trace);
            if(trace.CampLoaded||HumanDistance(trace.Situation.Camp.Position,Position)>250)continue;
            if(!trace.Grounded&&TryLivingCampGround(trace,out var anchor)){trace.Grounded=true;trace.Anchor=anchor;}
            if(trace.Grounded)LoadLivingTrace(trace);
            if(ElapsedHumanMs(started)>1.5)break;
        }
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
            if(!livingGroups.TryGetValue(id,out var group))livingGroups[id]=group=new LivingGroup{Situation=situation,Anchor=situation.Camp.Position};
            var previousPhase=group.Situation.Phase;
            if(group.Situation.Camp.Id!=situation.Camp.Id||situation.Phase==HumanCampPhase.Travelling&&group.CampLoaded)
            {UnloadLivingCamp(group,true);group.Grounded=false;group.PlacementAttempt=0;group.Resources=null;group.SiteSurvey=null;group.SiteSurveyRadius=200;group.Anchor=situation.Camp.Position;}
            group.Situation=situation;group.LastSeen=Time.unscaledTimeAsDouble;
            if(packet.Residents.TryGetValue(id,out var residents)){group.Residents=residents;if(packet.Activities.TryGetValue(id,out var activities))group.Activities=activities;}
            if(group.CampLoaded&&(situation.Phase==HumanCampPhase.SettingUp||situation.Phase==HumanCampPhase.Packing||previousPhase==HumanCampPhase.SettingUp&&situation.Phase==HumanCampPhase.Occupied))UpdateLivingCampConstruction(group);
            foreach(var actor in livingPeople.Values.Where(p=>p.GroupId==id))
            {
                actor.Routine=group.Activities?.FirstOrDefault(a=>a.Person==actor.ResidentId)??actor.Routine;
                actor.Resident=group.Residents?.Members.FirstOrDefault(p=>p.Id==actor.ResidentId)??actor.Resident;
            }
        }
        foreach(var id in livingGroups.Keys.Where(id=>!present.Contains(id)).ToArray())
        {UnloadLivingCamp(livingGroups[id]);livingGroups.Remove(id);}
        var traceIds=new HashSet<long>();
        foreach(var trace in packet.Traces)
        {
            traceIds.Add(trace.Site.Id);if(livingTraces.TryGetValue(trace.Site.Id,out var oldTrace)){oldTrace.TraceAge=trace.AgeSolarDays;continue;}
            var source=packet.Groups.First(g=>g.Band.Id==trace.Group);
            packet.Residents.TryGetValue(trace.Group,out var traceResidents);
            livingTraces[trace.Site.Id]=new LivingGroup{TraceOnly=true,TraceAge=trace.AgeSolarDays,Residents=traceResidents,Situation=new HumanGroupSituation{Band=source.Band,Camp=trace.Site,Position=trace.Site.Position,DepartureDay=trace.AbandonedCalendarDay,Phase=HumanCampPhase.Travelling}};
        }
        foreach(var id in livingTraces.Keys.Where(id=>!traceIds.Contains(id)).ToArray()){UnloadLivingCamp(livingTraces[id]);livingTraces.Remove(id);}
        livingOrdered=livingGroups.Values.OrderBy(g=>g.Situation.DistanceToPlayerM).ToArray();
    }
    bool TryLivingCampGround(LivingGroup group,out V anchor)
    {
        var memory=LivingState.Camps.FirstOrDefault(c=>c.Site==group.Situation.Camp.Id);
        if(memory!=null){anchor=memory.Position;return true;}
        anchor=default;var at=group.Situation.Camp.Position;TerrainHandoff.Coordinates(frame,at,out double cx,out double cz);
        if(group.SiteSurvey==null)return false;
        // Stable expanding search, resumed across frames. Dry, modest slopes
        // and room for the household ring are required before a camp appears.
        for(int step=0;step<2;step++)
        {
            int attempt=group.PlacementAttempt++;double bearing=attempt*2.399963229728653,range=attempt==0?0:Math.Sqrt(attempt)*7;
            if(range+group.SiteSurvey.FootprintRadius+10>group.SiteSurvey.Radius)
            {if(group.SiteSurveyRadius<1200){group.SiteSurveyRadius=Math.Min(1200,group.SiteSurveyRadius*2);group.SiteSurvey=null;}group.PlacementAttempt--;return false;}
            double x=cx+Math.Sin(bearing)*range,z=cz+Math.Cos(bearing)*range;var position=frame.Position(x,z);
            if(!LivingSiteClear(group,x,z))continue;
            double center=VisibleGround(x,z,double.NaN);if(double.IsNaN(center)){group.PlacementAttempt--;return false;}
            var sample=walkingPatch.Sample(position);
            if(sample.Underwater||!double.IsNaN(sample.WaterSurfaceM)&&sample.WaterSurfaceM>sample.ElevationM-.15)continue;
            double spread=0;bool suitable=true;
            double campRadius=group.SiteSurvey?.FootprintRadius??16;
            for(int k=0;k<8;k++)
            {
                double a=k*Math.PI/4,px=x+Math.Sin(a)*campRadius,pz=z+Math.Cos(a)*campRadius;var edge=walkingPatch.Sample(frame.Position(px,pz));
                double h=VisibleGround(px,pz,double.NaN);spread=Math.Max(spread,Math.Abs(h-center));
                if(double.IsNaN(h)||edge.Underwater||Math.Abs(h-center)>2.7){suitable=false;break;}
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
        if(memory!=null&&memory.AbandonedCalendarDay>=0&&group.Situation.ArrivalDay>memory.AbandonedCalendarDay)
        {
            foreach(var old in Camp.Objects.Where(c=>c.NpcSite==memory.Site&&c.NpcGroup==memory.Group).ToArray()){InvalidateCampVisual(old);Camp.Objects.Remove(old);}
            memory.Disturbed=false;memory.Objects.Clear();memory.AbandonedCalendarDay=-1;memory.LastDecayCalendarDay=-1;
        }
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
    void UpdateLivingCampConstruction(LivingGroup group)
    {
        int step=(int)(group.Situation.Progress*20)+(group.Situation.Phase==HumanCampPhase.Packing?100:0);if(step==group.ConstructionStep)return;group.ConstructionStep=step;
        // Only the components being erected/packed are changed. Hearth fuel,
        // cooking and any player-owned objects keep their current state.
        var projected=HumanCampLayout.Create(group.Situation,group.Residents,group.Anchor,frame.RadiusM,sourceWorld.Recipe.Seed,Camp.SimulatedSeconds);
        var wanted=projected.Where(c=>c.Kind==CampKind.Blueprint).ToDictionary(c=>c.Id);
        foreach(var obj in Camp.Objects.Where(c=>c.NpcGroup==group.Situation.Band.Id&&c.NpcSite==group.Situation.Camp.Id&&c.Kind==CampKind.Blueprint).ToArray())
        {
            if(!wanted.TryGetValue(obj.Id,out var next)){InvalidateCampVisual(obj);Camp.Objects.Remove(obj);continue;}
            SurveyLivingShelter(next);if(next.Blueprint.Parts.Count==obj.Blueprint.Parts.Count)continue;
            obj.Blueprint=next.Blueprint;obj.Blueprint.Revision++;InvalidateCampVisual(obj);wanted.Remove(obj.Id);
        }
        foreach(var obj in wanted.Values)if(!Camp.Objects.Any(c=>c.Id==obj.Id)){SurveyLivingShelter(obj);Camp.Objects.Add(obj);}
        if(group.Situation.Phase==HumanCampPhase.SettingUp||group.Situation.Phase==HumanCampPhase.Occupied)foreach(var obj in projected.Where(c=>c.Kind!=CampKind.Blueprint))
            if(!Camp.Objects.Any(c=>c.Id==obj.Id))Camp.Objects.Add(obj);
    }
    void SurveyLivingShelter(CampObject obj)
    {
        var plan=obj.Blueprint;double completion=plan.Parts.Sum(p=>p.WorkSeconds)/Math.Max(1,CampBlueprints.Components(plan).Sum(c=>c.Seconds));
        TerrainHandoff.Coordinates(frame,obj.Position,out double x,out double z);double floor=VisibleGround(x,z,0);var heights=new double[9];
        double yaw=obj.Heading*Math.PI/180;
        for(int i=0;i<3;i++)for(int j=0;j<3;j++)
        {double east=(i-1)*plan.WidthM*.5,north=(j-1)*plan.DepthM*.5;heights[i*3+j]=Math.Max(-1.9,Math.Min(1.9,VisibleGround(x+east*Math.Cos(yaw)+north*Math.Sin(yaw),z+north*Math.Cos(yaw)-east*Math.Sin(yaw),floor)-floor));}
        HumanCampLayout.FitShelter(obj,heights,completion);
    }
    void UnloadLivingCamp(LivingGroup group,bool abandoned=false)
    {
        if(!group.CampLoaded)return;
        StoreLivingCamp(group);
        if(abandoned)
        {
            var memory=LivingState.Camps.First(c=>c.Site==group.Situation.Camp.Id);memory.AbandonedCalendarDay=LivingClock.CalendarDay;
            memory.Objects.RemoveAll(c=>c.Kind!=CampKind.Fire&&c.Kind!=CampKind.Bedding);
            foreach(var fire in memory.Objects.Where(c=>c.Kind==CampKind.Fire)){fire.Burning=false;fire.EmberSeconds=0;fire.Materials.RemoveAll(p=>CampRules.Fuel(p));fire.AshKg=Math.Max(.08,fire.AshKg);}
        }
        foreach(var obj in Camp.Objects.Where(c=>c.NpcSite==group.Situation.Camp.Id&&c.NpcGroup==group.Situation.Band.Id).ToArray())
        {InvalidateCampVisual(obj);Camp.Objects.Remove(obj);}group.CampLoaded=false;
    }
    void LoadLivingTrace(LivingGroup group)
    {
        var memory=LivingState.Camps.FirstOrDefault(c=>c.Site==group.Situation.Camp.Id);
        if(memory!=null)
        {
            if(memory.AbandonedCalendarDay<0)memory.AbandonedCalendarDay=group.Situation.DepartureDay;
            foreach(var obj in memory.Objects.Where(c=>c.Kind==CampKind.Fire||c.Kind==CampKind.Bedding))
                if(!Camp.Objects.Any(c=>c.Id==obj.Id)){var copy=obj.Copy();copy.LastSimulatedSeconds=Camp.SimulatedSeconds;copy.Burning=false;copy.EmberSeconds=0;Camp.Objects.Add(copy);}
        }
        else if(group.Residents!=null)
        {
            var past=new HumanGroupSituation{Band=group.Situation.Band,Camp=group.Situation.Camp,Phase=HumanCampPhase.Occupied,LocalHour=12};
            foreach(var obj in HumanCampLayout.Create(past,group.Residents,group.Anchor,frame.RadiusM,sourceWorld.Recipe.Seed,Camp.SimulatedSeconds).Where(o=>o.Kind==CampKind.Fire||o.Kind==CampKind.Bedding))
            {
                if(obj.Kind==CampKind.Fire){obj.Burning=false;obj.EmberSeconds=0;obj.Materials.RemoveAll(CampRules.Fuel);obj.AshKg=.12*Math.Max(.15,1-group.TraceAge/60);}
                if(!Camp.Objects.Any(c=>c.Id==obj.Id))Camp.Objects.Add(obj);
            }
        }
        group.CampLoaded=true;
        StoreLivingCamp(group);var abandoned=LivingState.Camps.First(c=>c.Site==group.Situation.Camp.Id);abandoned.AbandonedCalendarDay=group.Situation.DepartureDay;
        AdvanceLivingTrace(group);
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
        if(!ResolveLivingDryPoint(point,group.Grounded?group.Anchor:group.Situation.Camp.Position,out point))return false;
        TerrainHandoff.Coordinates(frame,point,out double x,out double z);
        double residentFloor=VisibleGround(x,z,double.NaN);if(double.IsNaN(residentFloor))return false;
        uint hash=HumanHash(person.Id);double pigment=person.Appearance.Length>0?person.Appearance[0]:.5,curl=person.Appearance.Length>3?person.Appearance[3]:.5;
        int variant=person.Female?pigment>.62&&curl>.48?0:pigment<.38?2:4:person.AgeYears>55&&pigment<.55?5:pigment>.5?3:1;
        int id=unchecked((int)(hash&0x3fffffffu))+100000;
        while(humanNpcs.Any(p=>p.Id==id))id++;
        var ageModel=Resources.Load<WorldGenHumanAgeLibrary>("HumanAges/Library")?.Find(humanLibrary.People[variant].Variant,person.AgeYears);
        var actor=CreateHuman(variant,id,true,ageModel);actor.ResidentId=person.Id;actor.GroupId=group.Situation.Band.Id;actor.Resident=person;actor.Routine=activity;
        actor.Root.name=person.Name+" · "+group.Situation.Band.Name;
        actor.Health=memory?.Health??100;actor.Bleeding=memory?.Bleeding?.Copy();
        if(actor.Bleeding!=null)
        {
            double elapsed=Math.Min(1e9,Math.Max(0,(PlayerRules.SolarDays-actor.Bleeding.LastUpdatedDays)*PlayerRules.Settings.RealMinutesPerDay*60));
            BleedingRules.Advance(actor.Bleeding,elapsed,.15);actor.Health=Math.Min(actor.Health,BleedingRules.HealthLimit(actor.Bleeding,100));
            if(actor.Health<=0){memory.Dead=true;memory.Health=0;DestroyHuman(actor);return false;}
        }
        actor.Motor=new TerrainMotion();actor.Motor.Place(x,z,residentFloor);actor.Planet=point;
        actor.Yaw=actor.TargetYaw=(float)(hash%360);actor.Root.transform.localPosition=new Vector3((float)(x-originX),(float)(actor.Motor.Y-TerrainMotion.EyeHeightM),(float)(z-originZ));
        actor.Root.transform.localRotation=Quaternion.Euler(0,actor.Yaw,0);
        if(ageModel!=null)
        {
            double heightFactor=person.AgeYears<ageModel.Years?1-(ageModel.Years-person.AgeYears)*.035:1+(person.AgeYears-ageModel.Years)*.018;
            actor.Root.transform.localScale=Vector3.one*(float)(person.AgeYears<3?.58+.14*person.AgeYears:Math.Max(.78,Math.Min(1.09,heightFactor)));
        }
        livingPeople.Add(person.Id,actor);humanNpcs.Add(actor);return true;
    }
    void UpdateLivingHuman(HumanActor actor,bool stopped,float dt)
    {
        UpdateLivingDetail(actor);
        if(actor.Health<=0){actor.Ragdoll?.SetPaused(stopped);return;}
        if(!livingGroups.TryGetValue(actor.GroupId,out var group)||actor.Routine==null)return;
        if(actor.Routine.Activity==HumanActivity.Carried){UpdateCarriedHuman(actor,dt);return;}
        long cost=profileLiving?System.Diagnostics.Stopwatch.GetTimestamp():0;
        bool tending=group.TenderDuty?.Person==actor.ResidentId&&Time.unscaledTimeAsDouble<group.TendUntil;
        var routine=tending?group.TenderDuty:actor.Routine;actor.CurrentActivity=routine.Activity;
        V target=tending?routine.Position:LivingPosition(group,routine.Position);
        if(!tending)target=LivingResourceTarget(actor,group,target);
        target=LivingDryGoal(actor,target);
        string carryRole=NpcCarryRole(actor,group);
        TerrainHandoff.Coordinates(frame,target,out double tx,out double tz);
        var house=LivingHouse(actor);
        bool doorway=house!=null&&LivingDoorwayGoal(actor,house,ref tx,ref tz);
        cost=LivingCost(0,cost);
        if(dt>0)
        {
            double dx=tx-actor.Motor.X,dz=tz-actor.Motor.Z,distance=Math.Sqrt(dx*dx+dz*dz);
            var memory=LivingState.Groups.FirstOrDefault(g=>g.Group==actor.GroupId);
            bool alarm=memory!=null&&memory.AlarmUntilSolarDay>PlayerRules.SolarDays&&HumanDistance(actor.Planet,memory.ThreatPosition)<85;
            if(alarm)
            {
                actor.CurrentActivity=HumanActivity.Retreat;
                TerrainHandoff.Coordinates(frame,memory.ThreatPosition,out double hx,out double hz);dx=actor.Motor.X-hx;dz=actor.Motor.Z-hz;distance=Math.Sqrt(dx*dx+dz*dz);
                if(distance<90){actor.TargetSpeed=personRunSpeed(actor.Resident);actor.TargetYaw=(float)(Math.Atan2(dx,dz)*180/Math.PI);}else actor.TargetSpeed=0;
            }
            else
            {
                actor.TargetSpeed=distance>(doorway?.2:.35)?(float)Math.Min(actor.Resident.AgeYears<10?1:1.35,Math.Max(.12,distance*1.5)):0;
                if(actor.TargetSpeed>0)actor.TargetYaw=(float)(Math.Atan2(dx,dz)*180/Math.PI);
                else if(routine.Activity==HumanActivity.Sleep&&house!=null)actor.TargetYaw=(float)house.Heading;
                else if(routine.Activity==HumanActivity.Socialise||routine.Activity==HumanActivity.Teach||routine.Activity==HumanActivity.Childcare)
                {TerrainHandoff.Coordinates(frame,group.Grounded?group.Anchor:group.Situation.Camp.Position,out double gx,out double gz);actor.TargetYaw=(float)(Math.Atan2(gx-actor.Motor.X,gz-actor.Motor.Z)*180/Math.PI);}
                // Slow down while turning, particularly near a precise work or
                // entrance target. Otherwise a walking-speed minimum makes a
                // small target unreachable because the actor circles around it.
                if(actor.TargetSpeed>0)actor.TargetSpeed*=Mathf.Clamp01((Mathf.Cos(Mathf.DeltaAngle(actor.Yaw,actor.TargetYaw)*Mathf.Deg2Rad)+.05f)/.6f);
            }
            // Unfamiliar visitors attract a look, not supernatural detection or
            // an immediate full-speed flight response. Aggression raises alarm.
            double playerRange=Math.Sqrt(Math.Pow(actor.Motor.X-motion.X,2)+Math.Pow(actor.Motor.Z-motion.Z,2));
            if(!alarm&&playerRange<12&&playerRange>2&&actor.TargetSpeed<.1&&routine.Activity!=HumanActivity.Sleep&&LivingNoticesVisitor(actor,playerRange))
                actor.TargetYaw=(float)(Math.Atan2(motion.X-actor.Motor.X,motion.Z-actor.Motor.Z)*180/Math.PI);
            if(Time.unscaledTime<actor.AvoidUntil)actor.TargetYaw=actor.AvoidYaw;
            ExertionRules.Advance(actor.Exertion,ExertionProfile.Human(),dt,Math.Pow(Math.Min(1,actor.TargetSpeed/6),1.35),CurrentWeather.TemperatureC);
            actor.TargetSpeed=Math.Min(actor.TargetSpeed,6*(float)actor.Exertion.PowerScale);
            actor.Yaw=Mathf.MoveTowardsAngle(actor.Yaw,actor.TargetYaw,dt*95);actor.Speed=Mathf.MoveTowards(actor.Speed,actor.TargetSpeed,dt*1.6f);
            if(Time.unscaledTime>=actor.ObstacleAt)
            {
                bool moving=actor.Speed>.05f||actor.TargetSpeed>.05f;
                actor.ObstacleAt=Time.unscaledTime+(moving?.4:1.2);double a=actor.Yaw*Math.PI/180,px=actor.Motor.X+Math.Sin(a)*1.4,pz=actor.Motor.Z+Math.Cos(a)*1.4;
                float clearance=Math.Min(LivingHeadroom(actor),LivingHeadroom(actor,actor.Motor.X+Math.Sin(a)*.65,actor.Motor.Z+Math.Cos(a)*.65));
                float bodyHeight=(actor.AgeModel?.EyeHeight??actor.Definition.EyeHeight)*actor.Root.transform.localScale.y+.12f;
                actor.NpcCrawling=clearance<Math.Min(bodyHeight,1.22f);actor.NpcCrouching=clearance<bodyHeight;
                if(moving)
                {
                CacheLivingSolids(actor);SteerLivingScatter(actor,tx,tz);
                var next=walkingPatch.Sample(frame.Position(px,pz));double floor=VisibleGround(actor.Motor.X,actor.Motor.Z,actor.Motor.Y-TerrainMotion.EyeHeightM);
                double nextFloor=VisibleGround(px,pz,double.NaN);
                if(double.IsNaN(nextFloor)||next.Underwater||Math.Abs(nextFloor-floor)>.65){actor.Speed=0;actor.AvoidYaw=actor.Yaw+65;actor.AvoidUntil=Time.unscaledTime+1.2f;}
                var origin=transform.TransformPoint(new Vector3((float)(actor.Motor.X-originX),(float)floor+(actor.NpcCrawling?.3f:.8f),(float)(actor.Motor.Z-originZ)));
                if(Physics.SphereCast(origin,actor.NpcCrawling?.12f:.23f,transform.TransformDirection(new Vector3((float)Math.Sin(a),0,(float)Math.Cos(a))),out var hit,1.3f,1<<ScatterSolidLayer,QueryTriggerInteraction.Ignore))
                {actor.Speed=Math.Min(actor.Speed,.25f);actor.AvoidYaw=actor.Yaw+55;actor.AvoidUntil=Time.unscaledTime+1.2f;}
                }
            }
            if(actor.NpcCrawling){actor.Speed=Math.Min(actor.Speed,.6f);actor.TargetSpeed=Math.Min(actor.TargetSpeed,.6f);}
            cost=LivingCost(1,cost);
            double beforeX=actor.Motor.X,beforeY=actor.Motor.Y,beforeZ=actor.Motor.Z;
            bool walk=actor.Speed<1.7f,run=actor.Speed>4.2f;
            if(actor.Speed>.005f||!actor.Motor.Grounded)actor.Motor.Move(dt,actor.Speed/(walk?TerrainMotion.WalkSpeedMps:run?TerrainMotion.RunSpeedMps:TerrainMotion.JogSpeedMps),0,0,actor.Yaw,0,run,false,
                (x,z)=>VisibleGround(x,z,actor.Motor.Y-TerrainMotion.EyeHeightM),walk);
            ResolveLivingSolidContact(actor,beforeX,beforeY,beforeZ);
            ResolveLivingScatterContact(actor);
            float actual=(float)(Math.Sqrt(Math.Pow(actor.Motor.X-beforeX,2)+Math.Pow(actor.Motor.Z-beforeZ,2))/dt);
            cost=LivingCost(2,cost);
            float interval=playerRange<6?0:playerRange<50?1f/30:playerRange<160?1f/15:1f/8;
            if(Time.unscaledTimeAsDouble>=actor.AnimationAt)
            {
                bool crouch=actor.NpcCrouching;
                float elapsed=Math.Max(dt,interval);actor.AnimationAt=NextLivingAnimation(actor.ResidentId,interval);AnimateHuman(actor,actual/(actor.Root.transform.localScale.y*actor.AgeMotionRatio),!actor.Motor.Grounded,crouch,elapsed);
                bool hearth=routine.Role=="HearthMeal"||routine.Role=="HearthSocial";
                string role=actual>.2f?(actor.NpcCrawling?"Crawl":null):hearth?"SitGround":LivingWorkRole(routine.Activity);
                if(routine.Activity==HumanActivity.FetchWater&&group.Resources?.HasWater!=true)role="SitGround";
                if(crouch&&actual<=.2f&&role!="SleepSide")role="SitGround";
                actor.Shared?.Routine(role,Time.unscaledTime+HumanHash(actor.ResidentId)%100,elapsed);
                string upperRole=hearth&&actual<=.2f&&routine.Activity==HumanActivity.Socialise?"Social":carryRole=="WaterCarry"?"ToolReady":carryRole;
                actor.Shared?.Carry(actor.NpcCrawling||actor.TargetSpeed>2.5f||carryRole=="FuelTend"?null:upperRole,Time.unscaledTime+HumanHash(actor.ResidentId)%100,elapsed);
                actor.Graph.Evaluate(0);if(crouch&&role==null)CorrectHumanCrouch(actor);ApplyHumanAgePose(actor);
                string groundRole=role??(actual<.08f?"Idle":actual<2?"Walk":actual<4.5f?"Run":"Sprint");
                actor.PoseGroundLift=0;
                if(actor.AgeModel!=null)actor.PoseGroundLift=actor.AgeModel.Offset(groundRole);
                else if(actor.Shared!=null&&actor.Shared.Clips.TryGetValue(groundRole,out var groundClip))actor.PoseGroundLift=groundClip.GroundOffsetM;
            }
            cost=LivingCost(3,cost);
        }
        actor.Planet=frame.Position(actor.Motor.X,actor.Motor.Z);
        actor.Root.transform.localPosition=new Vector3((float)(actor.Motor.X-originX),(float)(actor.Motor.Y-TerrainMotion.EyeHeightM)+actor.PoseGroundLift*actor.Root.transform.localScale.y,(float)(actor.Motor.Z-originZ));actor.Root.transform.localRotation=Quaternion.Euler(0,actor.Yaw,0);
        UpdateNpcProps(actor,group,actor.NpcCrawling||actor.TargetSpeed>2.5f?null:carryRole);
        LivingCost(4,cost);
    }
    static float personRunSpeed(ForagerPersonSample person)=>person.AgeYears<10?2.8f:person.AgeYears>65?3.2f:4.8f;
    static double NextLivingAnimation(long person,double interval)
    {
        double now=Time.unscaledTimeAsDouble;if(interval<=0)return now;
        double phase=(HumanHash(person)%1024)/1024d*interval;
        return (Math.Floor((now-phase)/interval)+1)*interval+phase;
    }
    void UpdateCarriedHuman(HumanActor actor,float dt)
    {
        actor.CurrentActivity=HumanActivity.Carried;
        if(!livingPeople.TryGetValue(actor.Routine.Partner,out var guardian)||guardian.Health<=0)
        {
            guardian=livingPeople.Values.FirstOrDefault(p=>p.GroupId==actor.GroupId&&p.Resident.AgeYears>=18&&p.Health>0);
            if(guardian==null){actor.Root.SetActive(false);if(actor.InfantCarrier!=null)actor.InfantCarrier.SetActive(false);return;}
        }
        actor.Root.SetActive(true);bool asleep=guardian.Routine?.Activity==HumanActivity.Sleep&&guardian.Speed<.1f&&guardian.TargetSpeed<.1f;
        bool beside=asleep||guardian.Speed<.1f&&(guardian.Shared.ActionRole=="SitGround"||guardian.Shared.ActionRole=="WorkKneeling");
        if(guardian.Bones==null)guardian.Bones=guardian.Root.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
        string role=asleep?"SleepSide":"SitGround";
        double range=frame==null?0:HumanDistance(guardian.Planet,Position);float interval=range<8?1f/30:range<50?1f/15:1f/8;
        if(dt>0&&(frame==null||Time.unscaledTimeAsDouble>=actor.AnimationAt||actor.Shared.ActionRole!=role))
        {float step=Math.Max(dt,interval);actor.AnimationAt=NextLivingAnimation(actor.ResidentId,interval);AnimateHuman(actor,0,false,false,step);actor.Shared?.Routine(role,Time.unscaledTime,step);actor.Graph.Evaluate(0);ApplyHumanAgePose(actor);}
        var yaw=Quaternion.Euler(0,guardian.Yaw,0);float lift=actor.AgeModel?.Offset(role)??0;
        var offset=new Vector3(.5f,lift*actor.Root.transform.localScale.y,0);
        if(beside)
        {
            float ground=(float)(guardian.Motor.Y-TerrainMotion.EyeHeightM);
            actor.Root.transform.localPosition=new Vector3(guardian.Root.transform.localPosition.x,ground+lift*actor.Root.transform.localScale.y,guardian.Root.transform.localPosition.z)+yaw*new Vector3(.55f,0,0);actor.Root.transform.localRotation=yaw;
        }
        else
        {
            if(actor.Bones==null)actor.Bones=actor.Root.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
            var hip=actor.Root.transform.InverseTransformPoint(actor.Bones["pelvis"].position);
            var carry=NpcCarryOrientation(guardian);var seat=guardian.Bones["spine_02"].position+carry*new Vector3(0,.13f,-.15f);
            actor.Root.transform.rotation=carry;actor.Root.transform.position=seat-carry*Vector3.Scale(hip,actor.Root.transform.localScale);
        }
        actor.Planet=guardian.Planet;UpdateNpcInfantCarrier(actor,guardian,!beside);
        actor.Motor.Place(guardian.Motor.X,guardian.Motor.Z,guardian.Motor.Y-TerrainMotion.EyeHeightM);actor.Speed=0;
    }
    float LivingHeadroom(HumanActor actor)=>LivingHeadroom(actor,actor.Motor.X,actor.Motor.Z);
    float LivingHeadroom(HumanActor actor,double px,double pz)
    {
        float headroom=10;
        foreach(var obj in Camp.Objects)
        {
            if(obj.NpcGroup!=actor.GroupId||obj.Blueprint==null||HumanDistance(actor.Planet,obj.Position)>7)continue;
            var plan=obj.Blueprint;TerrainHandoff.Coordinates(frame,obj.Position,out double cx,out double cz);
            double dx=px-cx,dz=pz-cz,a=obj.Heading*Math.PI/180,x=dx*Math.Cos(a)-dz*Math.Sin(a),z=dx*Math.Sin(a)+dz*Math.Cos(a);
            if(Math.Abs(x)>plan.WidthM*.5||Math.Abs(z)>plan.DepthM*.5)continue;
            double height=CampBlueprints.Height(plan);
            if(plan.Design==CampDesign.RidgeTent)height*=1-Math.Abs(x)/(plan.WidthM*.5);
            else if(plan.Design==CampDesign.ConicalTent)height*=1-Math.Sqrt(x*x+z*z)/(plan.WidthM*.5);
            else if(plan.Design==CampDesign.BrushHut)height*=Math.Sqrt(Math.Max(0,1-(x*x+z*z)/(plan.WidthM*plan.WidthM*.25)));
            else if(plan.Design==CampDesign.LongHut)
            {double end=Math.Max(0,Math.Abs(z)-Math.Max(0,plan.DepthM-plan.WidthM)*.5);height*=Math.Sqrt(Math.Max(0,1-(x*x+end*end)/(plan.WidthM*plan.WidthM*.25)));}
            else if(plan.Design==CampDesign.LeanTo)height=.35+(height-.35)*(.5-z/plan.DepthM);
            else continue;
            // A roof can be high enough to stand under while its front flap is
            // lower. Anticipate that actual opening before the capsule hits it.
            if(z>plan.DepthM*.5-.8&&plan.Design!=CampDesign.LeanTo)
            {
                double opening=CampBlueprints.EntranceHeight(plan)-.05;
                if(Math.Abs(x)<.3&&(plan.Design==CampDesign.ConicalTent||plan.Design==CampDesign.BrushHut||plan.Design==CampDesign.LongHut))height=Math.Max(height,opening);
                height=Math.Min(height,opening);
            }
            headroom=Math.Min(headroom,(float)Math.Max(.35,height));
        }
        return headroom;
    }
    static string LivingWorkRole(HumanActivity activity)=>activity==HumanActivity.Sleep?"SleepSide":activity==HumanActivity.Rest||activity==HumanActivity.Childcare||activity==HumanActivity.Teach?"SitGround":activity==HumanActivity.Socialise?"Social":activity==HumanActivity.GatherPlants||activity==HumanActivity.GatherFuel||activity==HumanActivity.TendFire?"Gather":activity==HumanActivity.FetchWater?"Drink":activity==HumanActivity.PrepareFood?"Eat":activity==HumanActivity.Craft?"WorkKneeling":activity==HumanActivity.BuildCamp||activity==HumanActivity.RepairShelter?"WorkTwist":null;
    void CaptureLivingPerson(HumanActor actor)
    {
        if(actor.ResidentId==0||actor.Health>=100&&(actor.Bleeding==null||actor.Bleeding.Rate<=0))return;
        var memory=LivingState.People.FirstOrDefault(p=>p.Person==actor.ResidentId);
        if(memory==null){memory=new HumanPersonMemory{Person=actor.ResidentId,Group=actor.GroupId};LivingState.People.Add(memory);}
        memory.Health=actor.Health;memory.Dead=actor.Health<=0;memory.Position=actor.Planet;memory.SolarDays=PlayerRules.SolarDays;memory.Bleeding=actor.Bleeding?.Copy();
    }
    void CaptureLivingHumans()
    {foreach(var actor in livingPeople.Values)CaptureLivingPerson(actor);foreach(var group in livingGroups.Values)StoreLivingCamp(group);foreach(var trace in livingTraces.Values)StoreLivingCamp(trace);}
    void AlarmLivingGroup(HumanActor actor)
    {
        if(actor.ResidentId==0)return;var memory=LivingState.Groups.FirstOrDefault(g=>g.Group==actor.GroupId);
        if(memory==null){memory=new HumanGroupMemory{Group=actor.GroupId};LivingState.Groups.Add(memory);}
        memory.Trust=Math.Max(-1,memory.Trust-.45);memory.Fear=Math.Min(1,memory.Fear+.5);memory.ThreatPosition=Position;
        memory.AlarmUntilSolarDay=PlayerRules.SolarDays+120/(PlayerRules.Settings.RealMinutesPerDay*60);memory.LastSolarDay=PlayerRules.SolarDays;CaptureLivingPerson(actor);
    }
    void DisposeLivingHumans(){DisposeCampClearing();livingGroups.Clear();livingPeople.Clear();livingTraces.Clear();livingOrdered=Array.Empty<LivingGroup>();}
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
            V destination=group.Grounded?group.Anchor:group.Situation.Position;var east=V.Cross(new V(0,0,1),destination).Unit;
            destination=(destination+east*(25/frame.RadiusM)).Unit;Teleport(destination);motion.Y=Ground(0,0)+TerrainMotion.EyeHeightM;
            motion.VerticalSpeed=0;UpdateCamera();livingQueryAt=0;return new PlayCommandResult(true,"Visiting "+band.Name+".");
        }
        return new PlayCommandResult(true,band.Name,"Culture: "+f.Cultures[band.Culture].Name+" · language: "+f.Languages[band.Language].Name,
            "People: "+(group.Residents?.Members.Length??band.People)+" · "+band.Residence+" · "+group.Situation.Phase,
            "Camp: "+(group.Situation.Camp.ShelterAvailable?CampBlueprints.Name(group.Situation.Camp.Design)+" · "+group.Situation.Camp.Cover:"Open-air hearths and bedding")+" · "+Math.Max(0,group.Situation.DepartureDay-LivingClock.CalendarDay).ToString("F1")+" calendar days until departure.");
    }
}
