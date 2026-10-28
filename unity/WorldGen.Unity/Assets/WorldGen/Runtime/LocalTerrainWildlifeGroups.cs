using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    readonly Dictionary<ulong,WildlifeGroup> wildlifeGroups=new Dictionary<ulong,WildlifeGroup>();
    readonly Dictionary<string,WildlifeBehaviourProfile> wildlifeBehaviours=new Dictionary<string,WildlifeBehaviourProfile>();
    readonly Dictionary<(int,int,int),HashSet<ulong>> wildlifeGroupIndex=new Dictionary<(int,int,int),HashSet<ulong>>();
    readonly HashSet<ulong> activeWildlifeGroups=new HashSet<ulong>();double wildlifeGroupIndexScale;
    readonly Queue<ulong> wildlifeGroupWork=new Queue<ulong>();
    IEnumerable<WildlifeGroup> ActiveWildlifeGroupRecords=>activeWildlifeGroups.Select(id=>wildlifeGroups[id]);
    (int,int,int) GroupIndexKey(V point)=>((int)Math.Floor(point.X/wildlifeGroupIndexScale),(int)Math.Floor(point.Y/wildlifeGroupIndexScale),(int)Math.Floor(point.Z/wildlifeGroupIndexScale));
    void IndexWildlifeGroup(WildlifeGroup group)
    {var key=GroupIndexKey(group.Position);if(!wildlifeGroupIndex.TryGetValue(key,out var bucket)){bucket=new HashSet<ulong>();wildlifeGroupIndex.Add(key,bucket);}bucket.Add(group.Id);}
    void EnsureWildlifeGroupIndex()
    {double scale=1500/frame.RadiusM;if(wildlifeGroupIndexScale==scale)return;wildlifeGroupIndexScale=scale;wildlifeGroupIndex.Clear();foreach(var group in wildlifeGroups.Values)IndexWildlifeGroup(group);}
    double nextWildlifeGroupTick;bool reviewWildlifeBehaviour;
    LocalTerrain.LocalTerrainPatch wildlifeDecisionPatch;Task<LocalTerrain.LocalTerrainPatch> wildlifeDecisionPatchWork;
    bool TryWildlifeGround(V point,out LocalTerrainSample sample)
    {
        bool Covers(LocalTerrain.LocalTerrainPatch patch)=>patch!=null&&(point-patch.Center).Length*terrain.RadiusM<patch.HalfExtentM*1.3;
        if(Covers(walkingPatch)){sample=walkingPatch.Sample(point);return true;}
        if(wildlifeDecisionPatchWork?.IsCompleted==true)
        {if(wildlifeDecisionPatchWork.IsCompletedSuccessfully)wildlifeDecisionPatch=wildlifeDecisionPatchWork.Result;else if(wildlifeDecisionPatchWork.IsFaulted)Debug.LogWarning("Regional wildlife ground: "+wildlifeDecisionPatchWork.Exception.GetBaseException().Message);wildlifeDecisionPatchWork=null;}
        if(Covers(wildlifeDecisionPatch)){sample=wildlifeDecisionPatch.Sample(point);return true;}
        if(wildlifeDecisionPatchWork==null)
        {var at=Position;var data=terrain;double extent=3600/HorizontalScale;var token=cancellation.Token;wildlifeDecisionPatchWork=Task.Run(()=>data.Patch(at,extent),token);}
        sample=default;return false;
    }
    bool WildlifeBehaviourEnabled=>!inventoryReviewMode||reviewWildlifeBehaviour;
    WildlifeBehaviourProfile AnimalBehaviour(FaunaPrototypeProfile profile,int taxon=-1)
    {
        string key=taxon<0?profile.slug:profile.slug+"/"+taxon;if(wildlifeBehaviours.TryGetValue(key,out var result))return result;
        Taxon data=taxon>=0&&taxon<sourceWorld.Ecology.Taxa.Length?sourceWorld.Ecology.Taxa[taxon]:null;
        if(data==null){var binding=FaunaPrototypePlanner.BindProfiles(sourceWorld.Ecology.Taxa,new[]{profile});if(binding.Count>0)data=sourceWorld.Ecology.Taxa[binding.First().Key];}
        result=WildlifeBehaviour.Describe(profile.slug+" "+(data?.Name??""),data?.MassKg??Math.Max(.0001,8*Math.Pow(profile.lengthM,3)),data?.Eats??Food.None,profile.medium=="air",profile.tiny);wildlifeBehaviours.Add(key,result);return result;
    }
    void RegisterWildlifeGroups(IEnumerable<FaunaPrototypeSpawn> spawns)
    {
        if(!WildlifeBehaviourEnabled)return;
        if(wildlifeGroups.Count==0&&Wildlife.Groups.Count>0){foreach(var group in Wildlife.Groups)wildlifeGroups[group.Id]=group;wildlifeGroupIndexScale=0;}
        EnsureWildlifeGroupIndex();
        foreach(var spawn in spawns)
        {
            var behaviour=AnimalBehaviour(spawn.Profile,spawn.Taxon);if(behaviour.Tier<WildlifeTrackingTier.Resident||spawn.GroupId==0)continue;
            if(!wildlifeGroups.TryGetValue(spawn.GroupId,out var group))
            {
                TerrainHandoff.Coordinates(frame,spawn.Position,out double gx,out double gz);double a=(spawn.Hash%360)*Math.PI/180;
                group=new WildlifeGroup{Id=spawn.GroupId,Species=spawn.Profile.slug,Taxon=spawn.Taxon,Sex=behaviour.Herd?(int)(spawn.GroupId%2):-1,Home=spawn.Position,Position=spawn.Position,Goal=frame.Position(gx+Math.Sin(a)*30,gz+Math.Cos(a)*30),Members=spawn.GroupSize,LastSolarDays=PlayerRules.SolarDays,NextDecisionDays=PlayerRules.SolarDays+(3+spawn.Hash%30)/(PlayerRules.Settings.RealMinutesPerDay*60),Intent=WildlifeIntent.Forage,Hunger=(spawn.Hash%23)/100.0,Thirst=(spawn.Hash%17)/100.0};Wildlife.Groups.Add(group);wildlifeGroups.Add(group.Id,group);IndexWildlifeGroup(group);
            }
            spawn.Position=GroupMemberPosition(spawn,group);
        }
    }
    void UpdateWildlifeGroups()
    {
        if(!WildlifeBehaviourEnabled||paused||mapOpen||ecologyOpen||chatOpen||inventoryOpen&&!CraftWorking)return;
        if(wildlifeGroupWork.Count==0)
        {
            if(Time.unscaledTimeAsDouble<nextWildlifeGroupTick)return;nextWildlifeGroupTick=Time.unscaledTimeAsDouble+1;
            EnsureWildlifeGroupIndex();activeWildlifeGroups.Clear();var center=GroupIndexKey(Position);
            for(int x=-2;x<=2;x++)for(int y=-2;y<=2;y++)for(int z=-2;z<=2;z++)if(wildlifeGroupIndex.TryGetValue((center.Item1+x,center.Item2+y,center.Item3+z),out var bucket))foreach(var id in bucket)activeWildlifeGroups.Add(id);
            foreach(var id in activeWildlifeGroups.OrderBy(id=>id))wildlifeGroupWork.Enqueue(id);
        }
        long started=System.Diagnostics.Stopwatch.GetTimestamp();
        while(wildlifeGroupWork.Count>0)
        {
            var id=wildlifeGroupWork.Dequeue();if(wildlifeGroups.TryGetValue(id,out var group))TickWildlifeGroup(group,PlayerRules.SolarDays);
            if((System.Diagnostics.Stopwatch.GetTimestamp()-started)*1000d/System.Diagnostics.Stopwatch.Frequency>=.75)break;
        }
    }
    void TickWildlifeGroup(WildlifeGroup group,double current)
    {
            double radius=frame.RadiusM;var profile=animalProfiles.FirstOrDefault(p=>p.slug==group.Species);if(profile==null)return;var behaviour=AnimalBehaviour(profile,group.Taxon);
            double distance=(group.Position-Position).Length*radius,elapsed=(current-group.LastSolarDays)*PlayerRules.Settings.RealMinutesPerDay*60;group.LastSolarDays=current;
            if(distance>behaviour.TrackingM*1.25)return;double dt=Math.Max(0,Math.Min(5,elapsed));if(dt<=0)return;
            TerrainMapViewport.Degrees(group.Position,out double latitude,out _);var habitat=sourceWorld.Ecology.Habitat[terrain.NearestCell(group.Position)];
            if(WildlifeBehaviour.Dormant(behaviour,skyYear,latitude,habitat.ColdTenthsC/10.0,CurrentWeather.TemperatureC)){group.Intent=WildlifeIntent.Shelter;return;}
            group.Hunger=Math.Min(1,group.Hunger+dt/1800);group.Thirst=Math.Min(1,group.Thirst+dt/(CurrentWeather.TemperatureC>30?900:1800));
            bool arrived=group.Intent!=WildlifeIntent.Rest&&group.Intent!=WildlifeIntent.Shelter&&(group.Goal-group.Position).Length*radius<3;if(arrived&&group.Intent==WildlifeIntent.Drink)group.Thirst=Math.Max(0,group.Thirst-.6);
            if(current>=group.NextDecisionDays||arrived)ChooseGroupGoal(group,profile,behaviour);
            if(group.Intent==WildlifeIntent.Rest||group.Intent==WildlifeIntent.Shelter)return;
            double speed=profile.walkMps*(group.Intent==WildlifeIntent.Flee?2.5:group.Intent==WildlifeIntent.Hunt?1.5:1);
            double gap=(group.Goal-group.Position).Length*radius;if(gap<1)return;V next=(group.Position+(group.Goal-group.Position)*Math.Min(1,speed*dt/gap)).Unit;
            if(!TryWildlifeGround(next,out var sample))return;bool wet=sample.Underwater||!double.IsNaN(sample.WaterSurfaceM)&&sample.WaterSurfaceM>sample.ElevationM+.05;
            if(profile.medium=="ground"&&wet||profile.medium=="water"&&!wet){group.NextDecisionDays=current;return;}
            var oldKey=GroupIndexKey(group.Position);group.Position=next;var newKey=GroupIndexKey(next);if(!oldKey.Equals(newKey)){wildlifeGroupIndex[oldKey].Remove(group.Id);if(wildlifeGroupIndex[oldKey].Count==0)wildlifeGroupIndex.Remove(oldKey);IndexWildlifeGroup(group);}
    }
    void ChooseGroupGoal(WildlifeGroup group,FaunaPrototypeProfile profile,WildlifeBehaviourProfile behaviour)
    {
        var random=new Rng(sourceWorld.Recipe.Seed,"wildlife-group-goal-1",unchecked((int)(group.Id^(group.Id>>32))));for(int i=0;i<group.Decision%97;i++)random.Unit();group.Decision++;
        double now=PlayerRules.SolarDays;group.NextDecisionDays=now+(20+random.Unit()*30)/(PlayerRules.Settings.RealMinutesPerDay*60);
        TerrainMapViewport.Degrees(group.Position,out double latitude,out _);double activity=WildlifeBehaviour.Activity(behaviour,LocalSolarHour,profile.nocturnal,CurrentWeather.RainMmPerHour,Math.Sqrt(CurrentWeather.WindEastMps*CurrentWeather.WindEastMps+CurrentWeather.WindNorthMps*CurrentWeather.WindNorthMps));
        if(random.Unit()>activity&&group.Hunger<.7&&group.Thirst<.7){group.Intent=WildlifeIntent.Rest;group.Goal=group.Position;return;}
        TerrainHandoff.Coordinates(frame,group.Position,out double x,out double z);double angle=random.Unit()*Math.PI*2,reach=behaviour.PackHunter?160:behaviour.Herd?75:35;
        group.Intent=WildlifeIntent.Forage;
        if(behaviour.Herd&&group.Sex==1&&WildlifeBehaviour.Rut(skyYear,latitude)&&group.Hunger<.7&&group.Thirst<.7)
        {var females=ActiveWildlifeGroupRecords.Where(g=>g.Species==group.Species&&g.Sex==0&&(g.Position-group.Position).Length*frame.RadiusM<700).OrderBy(g=>(g.Position-group.Position).Length).FirstOrDefault();if(females!=null){group.Intent=WildlifeIntent.Rut;group.Goal=females.Position;return;}}
        if(behaviour.Migratory&&Math.Abs(Math.Sin(skyYear*Math.PI*2))>.75)
        {group.Intent=WildlifeIntent.Migrate;angle=WildlifeBehaviour.MigrationHeading(skyYear,latitude);reach=350;}
        else if(behaviour.Predator&&group.Hunger>.35)
        {
            Taxon hunter=group.Taxon>=0&&group.Taxon<sourceWorld.Ecology.Taxa.Length?sourceWorld.Ecology.Taxa[group.Taxon]:null;
            var prey=ActiveWildlifeGroupRecords.Where(g=>g.Id!=group.Id&&(g.Position-group.Position).Length*frame.RadiusM<700&&wildlifeBehaviours.TryGetValue(g.Species+"/"+g.Taxon,out var b)&&!b.Predator&&b.Herd&&g.Taxon>=0&&g.Taxon<sourceWorld.Ecology.Taxa.Length&&WildlifeBehaviour.EdiblePrey(hunter?.MassKg??40,behaviour.PackHunter,hunter?.MinPreyKg??0,hunter?.MaxPreyKg??0,sourceWorld.Ecology.Taxa[g.Taxon].MassKg)).OrderBy(g=>(g.Position-group.Position).Length).FirstOrDefault();
            if(prey!=null){group.Intent=WildlifeIntent.Hunt;group.Goal=prey.Position;return;}
        }
        if(group.Thirst>.45&&profile.medium=="ground")
        {
            for(int i=0;i<16;i++){double a=i*Math.PI/8;V candidate=frame.Position(x+Math.Sin(a)*80,z+Math.Cos(a)*80);if(!TryWildlifeGround(candidate,out var sample)||sample.WaterKind==LocalWaterKind.None)continue;
                group.Intent=WildlifeIntent.Drink;group.Goal=frame.Position(x+Math.Sin(a)*65,z+Math.Cos(a)*65);if((group.Goal-group.Position).Length*frame.RadiusM<5)group.Thirst=Math.Max(0,group.Thirst-.6);return;}
        }
        if((group.Position-group.Home).Length*frame.RadiusM>(behaviour.PackHunter?850:300)&&group.Intent!=WildlifeIntent.Migrate)group.Goal=group.Home;
        else group.Goal=frame.Position(x+Math.Sin(angle)*reach,z+Math.Cos(angle)*reach);
        group.Heading=angle*Mathf.Rad2Deg;if(group.Intent==WildlifeIntent.Forage&&!behaviour.Predator)group.Hunger=Math.Max(0,group.Hunger-.08);
    }
    V GroupMemberPosition(FaunaPrototypeSpawn spawn,WildlifeGroup group)
    {
        TerrainHandoff.Coordinates(frame,group.Position,out double x,out double z);var behaviour=AnimalBehaviour(spawn.Profile,spawn.Taxon);
        double index=spawn.GroupIndex,side=spawn.GroupIndex%2==0?1:-1,a=group.Heading*Math.PI/180;
        if(behaviour.Migratory&&group.Intent==WildlifeIntent.Migrate){double rank=Math.Ceiling(index/2);return frame.Position(x+Math.Cos(a)*side*rank*2.5-Math.Sin(a)*rank*2,z-Math.Sin(a)*side*rank*2.5-Math.Cos(a)*rank*2);}
        double angle=index*2.399963+group.Id%41,spread=Math.Sqrt(index)*Math.Max(1.5,spawn.Profile.lengthM*1.4);return frame.Position(x+Math.Sin(angle)*spread,z+Math.Cos(angle)*spread);
    }
    void PlaceTrackedAnimal(Animal animal)
    {
        if(!WildlifeBehaviourEnabled||animal.Manual||!wildlifeGroups.TryGetValue(animal.Spawn.GroupId,out var group))return;
        var point=GroupMemberPosition(animal.Spawn,group);var sample=walkingPatch.Sample(point);if(animal.Spawn.Profile.medium=="ground"&&sample.Underwater)return;
        animal.Planet=point;TerrainHandoff.Coordinates(frame,point,out animal.X,out animal.Z);animal.GroundY=frame.SurfaceY(point,sample.ElevationM);animal.HomeX=animal.X;animal.HomeZ=animal.Z;
    }
}
