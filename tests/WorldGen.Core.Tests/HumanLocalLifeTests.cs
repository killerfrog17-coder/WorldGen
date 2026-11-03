using System.Diagnostics;
using WorldGen.Core;
using WorldGen.Serialization;

static class HumanLocalLifeTests
{
    public static void Run(string path)
    {
        var world=WorldFile.ReadFile(path);var engine=new Prehistory(world);
        var state=world.Prehistory;
        if(!Prehistory.Foraging(state)||state.Foragers.Version!=ForagerState.RegionalVersion)
            state=engine.AdvanceYears(engine.StartForagers(engine.PrepareForagers()),40000);
        world.Prehistory=state;var life=new HumanLocalLife(world,engine);int checks=0;
        void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
        string hash=state.ComputeHash();var cohorts=state.Foragers.Cohorts.Where(c=>engine.ForagerBandCount(state,c)>0).OrderByDescending(c=>c.Population).Take(24).ToArray();
        Check(cohorts.Length>0,"Living fixture population required");
        var activities=new HashSet<HumanActivity>();var modes=new HashSet<ForagerResidence>();var shelters=new HashSet<CampDesign>();
        foreach(var c in cohorts)
        {
            var clock=new HumanLifeClock(SimulationTime.Seconds(state),0,60,365.2425/24,.1);
            var group=life.Group(state,clock,c.Id,0);var people=life.Residents(group);modes.Add(group.Band.Residence);shelters.Add(group.Camp.Design);
            var repeat=new HumanLocalLife(world,engine).Group(state,clock,c.Id,0);
            Check(group.Camp.Id==repeat.Camp.Id&&HumanLocalLife.Angle(group.Position,repeat.Position)<1e-12,"Camp layout independent of query order and object lifetime");
            Check(people.Members.Length>0&&people.Members.Select(p=>p.Id).Distinct().Count()==people.Members.Length,"Unique real households inhabit a camp");
            Check(people.Members.All(p=>!string.IsNullOrWhiteSpace(p.Name)),"Residents have names from their language");
            Check(group.Camp.Position.Length>.99999&&group.Camp.WaterAccess>=0,"Camp candidate has finite planetary geometry and habitat metadata");
            var objects=HumanCampLayout.Create(group,people,group.Camp.Position,world.Recipe.RadiusKm*1000*.1,world.Recipe.Seed);
            foreach(var obj in objects.Where(o=>o.Blueprint!=null))foreach(var part in obj.Blueprint.Parts)
            {var definition=CampBlueprints.Components(obj.Blueprint).Single(d=>d.Key==part.Key);foreach(var material in part.Materials)Check(CampBlueprints.Suitable(obj.Blueprint,definition,material),$"Generated {obj.Blueprint.Design}/{definition.Kind} accepts {material.Kind} {material.HeightM}m/{material.WidthM}m/{material.MassKg}kg");}
            new CampState{Objects=objects.ToList()}.Validate();
            Check(objects.All(o=>o.NpcGroup==group.Band.Id&&o.NpcSite==group.Camp.Id),"Real camp materials have their community/site provenance");
            var repeatedObjects=HumanCampLayout.Create(group,people,group.Camp.Position,world.Recipe.RadiusKm*1000*.1,world.Recipe.Seed);
            Check(objects.Select(o=>o.Id).SequenceEqual(repeatedObjects.Select(o=>o.Id))&&objects.SelectMany(o=>o.OwnedMaterials()).Select(p=>p.Id).SequenceEqual(repeatedObjects.SelectMany(o=>o.OwnedMaterials()).Select(p=>p.Id)),"Structures and their material identities regenerate deterministically");
            var byId=people.FamilyRecords.ToDictionary(p=>p.Id);
            foreach(var child in people.Members.Where(p=>p.AgeYears<18))Check(child.Mother<0||byId.ContainsKey(child.Mother),"Children retain linked family records");
            for(int hour=0;hour<24;hour+=2)
            {
                double longitude=Math.Atan2(group.Position.Y,group.Position.X)*180/Math.PI;
                var daily=new HumanLifeClock(clock.CalendarSeconds,(hour-9)/24d,60,clock.CalendarRate,.1,longitude);
                var dated=life.Group(state,daily,c.Id,0);
                double movedLongitude=Math.Atan2(dated.Position.Y,dated.Position.X)*180/Math.PI;
                double expectedHour=((hour+(movedLongitude-longitude)/15)%24+24)%24;
                Check(Math.Abs(dated.LocalHour-expectedHour)<1e-8,"Schedule follows the same local solar clock as the player");
                foreach(var person in people.Members)
                {var activity=life.Activity(dated,people,person,daily);activities.Add(activity.Activity);Check(activity.Person==person.Id&&activity.Group==group.Band.Id&&Math.Abs(activity.Position.Length-1)<1e-8,"Activities preserve identity and spherical position");}
            }
            // Move through the full seasonal itinerary using physical one-second
            // differences; compressed seasons must never make family groups sprint.
            foreach(double dayMinutes in new[]{1d,15,60,1440})
            {
                var local=new HumanLocalLife(world,engine);double year=SimulationTime.Year/(double)SimulationTime.Day;
                for(int sample=0;sample<120;sample++)
                {
                    double seconds=clock.CalendarSeconds+sample*year/120*SimulationTime.Day;
                    var a=new HumanLifeClock(seconds,sample*year/120/clock.CalendarRate,dayMinutes,clock.CalendarRate,.1);
                    double deltaCalendar=SimulationTime.Day*clock.CalendarRate/(dayMinutes*60);
                    var b=new HumanLifeClock(seconds+deltaCalendar,a.SolarDays+1/(dayMinutes*60),dayMinutes,clock.CalendarRate,.1);
                    var first=local.Group(state,a,c.Id,0);var next=local.Group(state,b,c.Id,0);
                    double speed=HumanLocalLife.Angle(first.Position,next.Position)*world.Recipe.RadiusKm*1000*.1;
                    Check(speed<1.17,"Seasonal travel stays at a family walking pace at every time scale: "+speed);
                }
            }
        }
        Check(activities.Contains(HumanActivity.Sleep)&&activities.Contains(HumanActivity.Play)&&activities.Contains(HumanActivity.GatherPlants),"Daily population contains sleep, children and subsistence work");
        var target=life.Group(state,new HumanLifeClock(SimulationTime.Seconds(state),0),cohorts[0].Id,0);
        var timer=Stopwatch.StartNew();var nearby=life.Nearby(state,new HumanLifeClock(SimulationTime.Seconds(state),0),target.Position);double firstMs=timer.Elapsed.TotalMilliseconds;
        timer.Restart();var again=life.Nearby(state,new HumanLifeClock(SimulationTime.Seconds(state),0),target.Position);double warmMs=timer.Elapsed.TotalMilliseconds;
        Check(nearby.Select(g=>g.Band.Id).SequenceEqual(again.Select(g=>g.Band.Id)),"50km group query has stable ordering and membership");
        Check(nearby.All(g=>g.Band.Members.Length==0),"Wide area query never builds household/person ledgers");
        Check(state.ComputeHash()==hash,"NPC projection does not mutate regional history");
        var session=new PlaySession();session.Humans.People.Add(new HumanPersonMemory{Person=1,Group=2,Health=43,Dead=false,Position=target.Position});
        session.Humans.Groups.Add(new HumanGroupMemory{Group=2,Trust=-.2,Fear=.6,ThreatPosition=target.Position});
        session.Humans.Validate();var copy=session.Copy();copy.Humans.People[0].Health=12;
        Check(session.Humans.People[0].Health==43,"Local consequences deep copy with the player session");
        Console.WriteLine($"PASS {checks} human-life checks; {modes.Count} residence modes / {shelters.Count} shelters; 50km {nearby.Length} groups, first {firstMs:F2}ms / warm {warmMs:F2}ms");
    }
}
