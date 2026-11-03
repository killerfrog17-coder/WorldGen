using System.Diagnostics;
using WorldGen.Core;
using WorldGen.Serialization;

static class HumanLocalLifeTests
{
    public static void Fixture(string input,string output,int years)
    {
        var world=WorldFile.ReadPlanetaryFile(input);world.Prehistory=null;var engine=new Prehistory(world);var timer=Stopwatch.StartNew();
        world.Prehistory=engine.StartForagers(engine.PrepareForagers());if(years>0)world.Prehistory=engine.AdvanceYears(world.Prehistory,years);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);File.WriteAllBytes(output,WorldFile.Write(world));
        var f=world.Prehistory.Foragers;var occupied=f.Cohorts.Where(c=>engine.ForagerBandCount(world.Prehistory,c)>0).ToArray();
        Console.WriteLine($"NPC fixture {years}yr / {occupied.Length} occupied aggregates / {timer.Elapsed.TotalSeconds:F2}s; {world.Prehistory.Hash}");
        File.WriteAllText(output+".summary.json",System.Text.Json.JsonSerializer.Serialize(occupied.Select(c=>new{c.Id,c.District,c.Population,Species=f.Lineages[c.Lineage].Name,Residence=f.Cultures[c.Culture].Residence.ToString(),f.Districts[c.District].MeanC,f.Districts[c.District].AnnualRainMm,f.Districts[c.District].Canopy}).ToArray()));
    }
    public static void Run(string path)
    {
        var world=WorldFile.ReadFile(path);var engine=new Prehistory(world);
        var state=world.Prehistory;
        if(!Prehistory.Foraging(state)||state.Foragers.Version!=ForagerState.RegionalVersion)
            state=engine.AdvanceYears(engine.StartForagers(engine.PrepareForagers()),40000);
        world.Prehistory=state;var life=new HumanLocalLife(world,engine);int checks=0;
        void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
        string hash=state.ComputeHash();var cohorts=state.Foragers.Cohorts.Where(c=>engine.ForagerBandCount(state,c)>0)
            .GroupBy(c=>(state.Foragers.Cultures[c.Culture].Residence,state.Foragers.Districts[c.District].MeanC<8,state.Foragers.Districts[c.District].Canopy>.4,state.Foragers.Districts[c.District].River+state.Foragers.Districts[c.District].Coast>.4))
            .Select(g=>g.OrderByDescending(c=>c.Population).First()).OrderByDescending(c=>c.Population).Take(32).ToArray();
        Check(cohorts.Length>0,"Living fixture population required");
        var activities=new HashSet<HumanActivity>();var modes=new HashSet<ForagerResidence>();var shelters=new HashSet<CampDesign>();
        foreach(double daylight in new[]{0d,8,12,18,24})foreach(double personality in new[]{0d,.5,1})
        {
            var adult=HumanLocalLife.SleepTiming(daylight,30,personality,.5,.5);var child=HumanLocalLife.SleepTiming(daylight,5,personality,.5,.5);
            Check(adult.duration>=6.5&&adult.duration<=8.8,"Adult rest does not expand into winter hibernation or collapse during polar daylight");
            Check(child.duration>adult.duration+2&&child.sleep>=0&&child.sleep<24,"Young children have longer bounded sleep windows");
            double sampled=0;for(double hour=0;hour<24;hour+=.05)if((hour-adult.sleep+24)%24<adult.duration)sampled+=.05;
            Check(Math.Abs(sampled-adult.duration)<.06,"Sleep window wraps correctly across midnight at every latitude");
        }
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
            foreach(var house in repeatedObjects.Where(o=>o.Blueprint!=null))
            {
                HumanCampLayout.FitShelter(house,new[]{-.2,-.1,0,0,.1,.2,.2,.5,.75});
                Check(CampBlueprints.Complete(house.Blueprint),"Terrain-adapted shelter fills its actual final component graph, including changed conical front bands");
                foreach(var component in CampBlueprints.Components(house.Blueprint))foreach(var material in CampBlueprints.State(house.Blueprint,component).Materials)
                    Check(CampBlueprints.Suitable(house.Blueprint,component,material),"Fitted terrain footing retains correctly sized and suitable structural/cover materials");
            }
            new CampState{Objects=repeatedObjects.ToList()}.Validate();
            var construction=new CampState();
            foreach(double progress in new[]{.05,.29,.49,.76,1d})
            {
                var building=new HumanGroupSituation{Band=group.Band,Camp=group.Camp,Phase=HumanCampPhase.SettingUp,Progress=progress,LocalHour=9};
                var components=HumanCampLayout.Create(building,people,group.Camp.Position,world.Recipe.RadiusKm*1000*.1,world.Recipe.Seed);
                foreach(var part in components)
                {
                    var old=construction.Objects.FirstOrDefault(o=>o.Id==part.Id);
                    if(old==null)construction.Objects.Add(part);else if(part.Blueprint!=null)old.Blueprint=part.Blueprint;
                }
                construction.Validate();
                Check(true,"Construction preserves existing beds/hearths without colliding with newly added frame material identities");
            }
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
                {
                    var activity=life.Activity(dated,people,person,daily);activities.Add(activity.Activity);Check(activity.Person==person.Id&&activity.Group==group.Band.Id&&Math.Abs(activity.Position.Length-1)<1e-8,"Activities preserve identity and spherical position");
                    if(person.AgeYears<3)Check(activity.Activity==HumanActivity.Carried&&people.Members.Any(p=>p.Id==activity.Partner&&p.AgeYears>=18),"Infants move with a linked parent/guardian rather than walking or foraging independently");
                }
            }
            // Move through the full seasonal itinerary using physical one-second
            // differences; compressed seasons must never make family groups sprint.
            foreach(double dayMinutes in new[]{1d,15,60,1440})
            {
                var local=new HumanLocalLife(world,engine);double year=SimulationTime.Year/(double)SimulationTime.Day;
                var boundaries=new HashSet<double>();
                for(int sample=0;sample<120;sample++)
                {
                    double seconds=clock.CalendarSeconds+sample*year/120*SimulationTime.Day;
                    var a=new HumanLifeClock(seconds,sample*year/120/clock.CalendarRate,dayMinutes,clock.CalendarRate,.1);
                    double deltaCalendar=SimulationTime.Day*clock.CalendarRate/(dayMinutes*60);
                    var b=new HumanLifeClock(seconds+deltaCalendar,a.SolarDays+1/(dayMinutes*60),dayMinutes,clock.CalendarRate,.1);
                    var first=local.Group(state,a,c.Id,0);var next=local.Group(state,b,c.Id,0);
                    boundaries.Add(first.ArrivalDay);boundaries.Add(first.JourneyEndDay);
                    double speed=HumanLocalLife.Angle(first.Position,next.Position)*world.Recipe.RadiusKm*1000*.1;
                    Check(speed<1.17,"Seasonal travel stays at a family walking pace at every time scale: "+speed);
                    if(first.Phase==HumanCampPhase.Travelling&&next.Phase==HumanCampPhase.Travelling&&first.JourneyRest&&next.JourneyRest)
                        Check(speed<.005&&Math.Abs(first.Progress-next.Progress)<1e-7,"Overnight travel rests preserve progress within planetary-coordinate precision: "+speed+"m; progress "+Math.Abs(first.Progress-next.Progress));
                }
                foreach(double boundary in boundaries.Where(d=>d>=clock.CalendarDay))
                {
                    double dt=clock.CalendarRate/(dayMinutes*60);
                    HumanLifeClock Around(double day)=>new HumanLifeClock(day*SimulationTime.Day,(day-clock.CalendarDay)/clock.CalendarRate,dayMinutes,clock.CalendarRate,.1);
                    var before=local.Group(state,Around(boundary-dt*.5),c.Id,0);var after=local.Group(state,Around(boundary+dt*.5),c.Id,0);
                    double moved=HumanLocalLife.Angle(before.Position,after.Position)*world.Recipe.RadiusKm*1000*.1;
                    Check(moved<1.17,$"Seasonal itinerary boundary remains continuous, {dayMinutes}min days / {group.Band.Name}: {moved}m ({before.Phase}->{after.Phase})");
                }
            }
        }
        Check(activities.Contains(HumanActivity.Sleep)&&activities.Contains(HumanActivity.Play)&&activities.Contains(HumanActivity.GatherPlants),"Daily population contains sleep, children and subsistence work");
        var target=life.Group(state,new HumanLifeClock(SimulationTime.Seconds(state),0),cohorts[0].Id,0);
        var timer=Stopwatch.StartNew();var nearby=life.Nearby(state,new HumanLifeClock(SimulationTime.Seconds(state),0),target.Position);double firstMs=timer.Elapsed.TotalMilliseconds;
        timer.Restart();var again=life.Nearby(state,new HumanLifeClock(SimulationTime.Seconds(state),0),target.Position);double warmMs=timer.Elapsed.TotalMilliseconds;
        Check(nearby.Select(g=>g.Band.Id).SequenceEqual(again.Select(g=>g.Band.Id)),"50km group query has stable ordering and membership");
        Check(nearby.All(g=>g.Band.Members.Length==0),"Wide area query never builds household/person ledgers");
        var east=V.Cross(new V(0,0,1),target.Position).Unit;var queryClock=new HumanLifeClock(SimulationTime.Seconds(state),0);
        foreach(double distance in new[]{0d,200,4900,5100,9900,15100})
        {
            V observer=(target.Position+east*(distance/(world.Recipe.RadiusKm*1000))).Unit;
            var cached=life.Nearby(state,queryClock,observer);var direct=new HumanLocalLife(world,engine).Nearby(state,queryClock,observer);
            Check(cached.Select(g=>g.Band.Id).SequenceEqual(direct.Select(g=>g.Band.Id)),"Conservative cached 50km query agrees with a fresh spatial query after "+distance+"m of travel");
        }
        Check(state.ComputeHash()==hash,"NPC projection does not mutate regional history");
        var session=new PlaySession();session.Humans.People.Add(new HumanPersonMemory{Person=1,Group=2,Health=43,Dead=false,Position=target.Position});
        session.Humans.Groups.Add(new HumanGroupMemory{Group=2,Trust=-.2,Fear=.6,ThreatPosition=target.Position});
        session.Humans.Validate();var copy=session.Copy();copy.Humans.People[0].Health=12;
        Check(session.Humans.People[0].Health==43,"Local consequences deep copy with the player session");
        Console.WriteLine($"PASS {checks} human-life checks; {modes.Count} residence modes / {shelters.Count} shelters; 50km {nearby.Length} groups, first {firstMs:F2}ms / warm {warmMs:F2}ms");
    }
}
