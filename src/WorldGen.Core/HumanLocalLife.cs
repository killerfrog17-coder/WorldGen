using System;
using System.Collections.Generic;
using System.Linq;
namespace WorldGen.Core
{
    public enum HumanCampPhase { SettingUp,Occupied,Packing,Travelling }
    public enum HumanActivity { Sleep,Rest,Socialise,TendFire,FetchWater,GatherPlants,GatherFuel,PrepareFood,Craft,RepairShelter,Teach,Childcare,Play,Travel,BuildCamp,PackCamp,Watch,Retreat,Defend,Carried }
    public readonly struct HumanLifeClock
    {
        public readonly double CalendarSeconds,SolarDays,DayMinutes,CalendarRate,HorizontalScale,PrimeLongitude;
        public double CalendarDay=>CalendarSeconds/SimulationTime.Day;
        public double Years=>CalendarSeconds/SimulationTime.Year;
        public double YearRealSeconds=>SimulationTime.Year/(double)SimulationTime.Day/CalendarRate*DayMinutes*60;
        public HumanLifeClock(double calendarSeconds,double solarDays,double dayMinutes=60,double calendarRate=1,double horizontalScale=1,double primeLongitude=0)
        {CalendarSeconds=calendarSeconds;SolarDays=solarDays;DayMinutes=Math.Max(1,dayMinutes);CalendarRate=Math.Max(1,calendarRate);HorizontalScale=Math.Max(.01,horizontalScale);PrimeLongitude=primeLongitude;}
    }
    public sealed class HumanCampSite
    {
        public long Id;public int Index,Cell;public V Position;public double ShelterWidth,ShelterDepth,WaterAccess,FoodOpportunity;
        public CampDesign Design;public CampCover Cover;public string Purpose="";public bool LongStay,ShelterAvailable;
    }
    public sealed class HumanGroupSituation
    {
        public ForagerBandSample Band;public HumanCampSite Camp,Destination;public V Position;
        public HumanCampPhase Phase;public double ArrivalDay,DepartureDay,JourneyEndDay,Progress,DistanceToPlayerM;
        public double DaylightHours,LocalHour,SecurityRisk;public int ForageParties;
        public bool PermanentBase,JourneyRest;
    }
    public sealed class HumanActivitySample
    {
        public long Person,Group,Partner=-1;public HumanActivity Activity;public V Position,Destination;
        public double SpeedMps,Effort,Progress;public int Party=-1;public string Role="";
    }
    public sealed class HumanCampTrace
    {
        public long Group;public HumanCampSite Site;public double AbandonedCalendarDay,AgeSolarDays;
    }
    // A bounded local projection over broad regional history, not a second
    // demographic ledger. No camp/person is appended to the global simulation.
    public sealed class HumanLocalLife
    {
        readonly World world;readonly Prehistory history;
        readonly Dictionary<long,Plan> plans=new Dictionary<long,Plan>();
        PrehistoryState snapshot;object domain;int planYear=int.MinValue;
        double planScale,planDay,planCalendar;
        ForagerBandSample[] spatialCandidates;V spatialCenter;double spatialRadius;
        sealed class Plan {public ForagerBandSample Band,Residents;public HumanCampSite[] Sites;public double[] Weights;public bool Permanent;public double Phase,SecurityRisk;}
        public int CachedGroups=>plans.Count;
        public HumanLocalLife(World world,Prehistory history){this.world=world;this.history=history;}
        static double Unit(long id,int seed,string stream,int slot=0)=>Rng.Value(seed,stream,unchecked((int)id^(int)(id>>32)^slot*7919));
        public static bool SpatialAdmission(int seed,long band,double horizontalScale)
        {
            // Compressing distances must not compress the same world population
            // into a much smaller played area. Thin WHOLE communities, retaining
            // their normal family/camp size and stable source identities.
            return horizontalScale>=1||Unit(band,seed,"human-played-area")<horizontalScale*horizontalScale;
        }
        void Rebase(PrehistoryState state,HumanLifeClock clock)
        {
            int year=(int)Math.Floor(clock.Years);
            if(!ReferenceEquals(domain,state.DomainToken)||planYear!=year||planScale!=clock.HorizontalScale||planDay!=clock.DayMinutes||planCalendar!=clock.CalendarRate)
            {plans.Clear();spatialCandidates=null;domain=state.DomainToken;planYear=year;planScale=clock.HorizontalScale;planDay=clock.DayMinutes;planCalendar=clock.CalendarRate;}
            snapshot=state;
        }
        public HumanGroupSituation[] Nearby(PrehistoryState state,HumanLifeClock clock,V observer,double radiusM=50000)
        {
            if(!Prehistory.Foraging(state)||radiusM<0)return Array.Empty<HumanGroupSituation>();Rebase(state,clock);
            // Cache a conservative wider footprint query. Exact current camp
            // positions are still projected below on every call; walking a few
            // metres must not rescan regional geometry or rebuild itineraries.
            if(spatialCandidates==null||spatialRadius!=radiusM||Angle(observer,spatialCenter)*world.Recipe.RadiusKm*1000*clock.HorizontalScale>5000)
            {
                double rawRadius=(radiusM+10000)/(1000*clock.HorizontalScale);
                spatialCandidates=history.ForagerBandFootprintsNear(state,observer,rawRadius,id=>SpatialAdmission(world.Recipe.Seed,id,clock.HorizontalScale));
                spatialCenter=observer;spatialRadius=radiusM;
            }
            var candidates=spatialCandidates;
            var found=new List<HumanGroupSituation>();var retained=new HashSet<long>();
            foreach(var band in candidates)
            {
                retained.Add(band.Id);if(!plans.TryGetValue(band.Id,out var plan))plans[band.Id]=plan=MakePlan(state,band,clock);
                var group=Situation(plan,clock);group.DistanceToPlayerM=Angle(observer,group.Position)*world.Recipe.RadiusKm*1000*clock.HorizontalScale;
                // Include the camp catchment: a foraging party can be near the
                // observer even while the hearth is several hundred metres away.
                if(group.DistanceToPlayerM<=radiusM+ForageRadius(group,clock))found.Add(group);
            }
            foreach(long id in plans.Keys.Where(id=>!retained.Contains(id)).ToArray())plans.Remove(id);
            return found.OrderBy(g=>g.DistanceToPlayerM).ThenBy(g=>g.Band.Id).ToArray();
        }
        public ForagerBandSample Residents(HumanGroupSituation group)
        {
            int ordinal=unchecked((int)(uint)group.Band.Id)-1;
            if(plans.TryGetValue(group.Band.Id,out var plan))return plan.Residents??(plan.Residents=history.ForagerBand(snapshot,group.Band.Cohort,ordinal));
            return history.ForagerBand(snapshot,group.Band.Cohort,ordinal);
        }
        public HumanGroupSituation Group(PrehistoryState state,HumanLifeClock clock,int cohort,int ordinal)
        {
            Rebase(state,clock);long id=((long)(cohort+1)<<32)|(uint)(ordinal+1);
            if(!plans.TryGetValue(id,out var plan)){var full=history.ForagerBand(state,cohort,ordinal);if(full==null)return null;plans[id]=plan=MakePlan(state,full,clock);}return Situation(plan,clock);
        }
        Plan MakePlan(PrehistoryState state,ForagerBandSample band,HumanLifeClock clock)
        {
            var f=state.Foragers;var d=f.Districts[band.District];var culture=f.Cultures[band.Culture];var belief=f.Beliefs[band.Belief];
            bool Has(int k)=>band.Techniques.Length>k&&band.Techniques[k];
            bool stableWater=d.River+d.Coast>.35&&d.Seasonality[3]+d.Seasonality[4]<1;
            bool permanent=culture.Residence==ForagerResidence.AquaticResidence&&stableWater&&(Has(23)||Has(32))&&band.FoodSecurity>.65;
            int count=permanent?1:Math.Max(2,band.SeasonalCamps.Length);var sites=new HumanCampSite[count];var weights=new double[count];
            double travelShare=culture.Residence==ForagerResidence.ResidentialMobility?.09:culture.Residence==ForagerResidence.SeasonalBaseCamps?.065:.04;
            double budgetKm=clock.YearRealSeconds*1.15*travelShare/(2*count*1000*clock.HorizontalScale);
            double radiusKm=Math.Min(Math.Max(.001,band.HomeRangeKm*1.25),Math.Max(.001,budgetKm));
            for(int i=0;i<count;i++)
            {
                var source=band.SeasonalCamps[i%band.SeasonalCamps.Length];double angle=Angle(band.Home,source.Position),distance=angle*world.Recipe.RadiusKm;
                V position=distance>radiusKm?Interpolate(band.Home,source.Position,radiusKm/distance):source.Position;
                int cell=source.Cell;double best=-2;foreach(int candidate in d.Cells){double dot=RawDot(position,world.Surface.Cells[candidate].Position);if(dot>best){best=dot;cell=candidate;}}
                var local=ForagerHabitat.For(world,state).Cell(cell);var h=local?.Habitat??world.Ecology.Habitat[cell];
                bool cold=h.ColdTenthsC<0,longStay=permanent||culture.Residence==ForagerResidence.CollectorCamps||culture.Residence==ForagerResidence.AquaticResidence;
                bool hides=Has(6)&&Has(35);
                CampCover cover=cold&&hides?CampCover.Hide:d.River>.3&&Has(36)?CampCover.Reed:d.Canopy>.45&&Has(3)&&d.AnnualRainMm>900?CampCover.Bark:d.Aridity<.4&&hides?CampCover.Hide:CampCover.Brush;
                bool framed=Has(7)&&(Has(34)||Has(31));bool stoneScreen=!framed&&d.Stone>.62&&Unit(band.Id,world.Recipe.Seed,"stone-windbreak",i)<.45;
                CampDesign design=framed?cold&&cover==CampCover.Hide?CampDesign.ConicalTent:longStay&&d.Canopy>.2?CampDesign.BrushHut:d.AnnualRainMm>1200?CampDesign.RidgeTent:CampDesign.LeanTo:stoneScreen?CampDesign.StoneWindbreak:CampDesign.Windbreak;
                // Religion/preferences affect shared meeting space and camp
                // organisation; there is no universal hereditary chief's house.
                double communal=D.Clamp(belief.CollectiveRites*culture.GuestAccess);
                sites[i]=new HumanCampSite{Id=SiteId(band.Id,i),Index=i,Cell=cell,Position=position,Design=design,Cover=cover,LongStay=longStay,ShelterAvailable=framed||stoneScreen,
                    ShelterWidth=2.1+.5*communal+.3*Unit(band.Id,world.Recipe.Seed,"camp-width",i),ShelterDepth=2.4+.5*Unit(band.Id,world.Recipe.Seed,"camp-depth",i),
                    WaterAccess=local?.WaterAccess??source.WaterAccess,FoodOpportunity=source.FoodOpportunity,Purpose=source.Purpose};
                weights[i]=.65+.7*Unit(band.Id,world.Recipe.Seed,"camp-stay",i);
                if(source.Season==3&&cold)weights[i]*=1.3;if(stableWater&&source.Season==1)weights[i]*=1.2;
            }
            double sum=weights.Sum();for(int i=0;i<count;i++)weights[i]/=sum;
            var footprint=new ForagerBandSample{Id=band.Id,Cohort=band.Cohort,District=band.District,Lineage=band.Lineage,Culture=band.Culture,Language=band.Language,Belief=band.Belief,
                Name=band.Name,Home=band.Home,People=band.People,HomeRangeKm=band.HomeRangeKm,MoveFrequencyPerYear=band.MoveFrequencyPerYear,Aggregation=band.Aggregation,FoodSecurity=band.FoodSecurity,Hospitality=band.Hospitality,
                Residence=band.Residence,Techniques=band.Techniques,RoutineTechniques=band.RoutineTechniques,WinterTechniques=band.WinterTechniques,SeasonalCamps=band.SeasonalCamps,ContactBands=band.ContactBands};
            double predator=0;var biota=band.District<f.Biota.Length?f.Biota[band.District]:null;
            if(biota!=null)for(int j=0;j<biota.Taxa.Length;j++){int t=biota.Taxa[j];if(t<0||t>=world.Ecology.Taxa.Length)continue;var taxon=world.Ecology.Taxa[t];if(taxon.MassKg>=18&&(taxon.Eats&Food.Vertebrate)!=0)predator+=biota.Abundance[j]*Math.Min(1,taxon.MassKg/80);}
            double scarcity=band.District<f.Resources.Length?f.Resources[band.District].HarvestPressure:0;
            return new Plan{Band=footprint,Residents=band.Members.Length>0?band:null,Sites=sites,Weights=weights,Permanent=permanent,Phase=59+28*(Unit(band.Id,world.Recipe.Seed,"camp-calendar")-.5),SecurityRisk=D.Clamp(.12+.6*(1-Math.Exp(-predator))+.15*scarcity+.15*(1-culture.GuestAccess))};
        }
        HumanGroupSituation Situation(Plan plan,HumanLifeClock clock)
        {
            double yearDays=SimulationTime.Year/(double)SimulationTime.Day,cycle=Math.Floor((clock.CalendarDay-plan.Phase)/yearDays),start=cycle*yearDays+plan.Phase,within=clock.CalendarDay-start;
            int index=0;double cursor=0;for(;index<plan.Sites.Length-1;index++){double duration=plan.Weights[index]*yearDays;if(within<cursor+duration)break;cursor+=duration;}
            var camp=plan.Sites[index];var next=plan.Sites[(index+1)%plan.Sites.Length];double visitStart=start+cursor,visitEnd=visitStart+plan.Weights[index]*yearDays;
            var journey=Journey(camp,next,visitEnd,clock,plan.Permanent);
            double depart=journey.depart,settleDays=Math.Min(.045*clock.CalendarRate,(visitEnd-visitStart)*.12),packDays=Math.Min(.035*clock.CalendarRate,(visitEnd-visitStart)*.1);
            HumanCampPhase phase=clock.CalendarDay>=depart&&!plan.Permanent?HumanCampPhase.Travelling:clock.CalendarDay>=depart-packDays&&!plan.Permanent?HumanCampPhase.Packing:clock.CalendarDay<visitStart+settleDays&&!plan.Permanent?HumanCampPhase.SettingUp:HumanCampPhase.Occupied;
            double progress=phase==HumanCampPhase.Travelling?journey.progress:phase==HumanCampPhase.SettingUp?D.Clamp((clock.CalendarDay-visitStart)/Math.Max(.000001,settleDays)):phase==HumanCampPhase.Packing?D.Clamp((clock.CalendarDay-(depart-packDays))/Math.Max(.000001,packDays)):1;
            var position=phase==HumanCampPhase.Travelling?Interpolate(camp.Position,next.Position,progress):camp.Position;
            double latitude=Math.Asin(D.Clamp(position.Z,-1,1)),declination=.409*Math.Sin(2*Math.PI*(clock.CalendarDay/yearDays-.218));
            double daylight=24/Math.PI*Math.Acos(D.Clamp(-Math.Tan(latitude)*Math.Tan(declination),-1,1));
            double hour=((clock.SolarDays*24+9+(Math.Atan2(position.Y,position.X)*180/Math.PI-clock.PrimeLongitude)/15)%24+24)%24;
            return new HumanGroupSituation{Band=plan.Band,Camp=camp,Destination=next,Position=position,Phase=phase,Progress=progress,ArrivalDay=visitStart,DepartureDay=depart,JourneyEndDay=visitEnd,
                DaylightHours=daylight,LocalHour=hour,SecurityRisk=plan.SecurityRisk,PermanentBase=plan.Permanent,JourneyRest=phase==HumanCampPhase.Travelling&&!journey.moving,ForageParties=Math.Max(1,(int)Math.Ceiling(plan.Band.People/14d))};
        }
        (double depart,double progress,bool moving) Journey(HumanCampSite from,HumanCampSite to,double arrival,HumanLifeClock clock,bool stationary=false)
        {
            double needed=stationary?0:Angle(from.Position,to.Position)*world.Recipe.RadiusKm*1000*clock.HorizontalScale/1.15/(clock.DayMinutes*60);
            if(needed<1e-9)return(arrival,1,false);
            double latitude=Math.Asin(D.Clamp(from.Position.Z,-1,1)),declination=.409*Math.Sin(2*Math.PI*(arrival/(SimulationTime.Year/(double)SimulationTime.Day)-.218));
            double daylight=24/Math.PI*Math.Acos(D.Clamp(-Math.Tan(latitude)*Math.Tan(declination),-1,1));
            double begin=D.Clamp(12-daylight/2+.7,5,9)/24,end=D.Clamp(12+daylight/2-.5,15,20)/24,span=end-begin;
            // Analytic work-window integration: travel pauses overnight without
            // adding a tick for every person/day or accelerating to catch up.
            double Work(double t){double day=Math.Floor(t);return day*span+D.Clamp(t-day-begin,0,span);}
            double Inverse(double w){double day=Math.Floor(w/span);return day+begin+w-day*span;}
            double localNow=clock.SolarDays+(9+(Math.Atan2(from.Position.Y,from.Position.X)*180/Math.PI-clock.PrimeLongitude)/15)/24;
            double localArrival=localNow+(arrival-clock.CalendarDay)/clock.CalendarRate;
            double startWork=Work(localArrival)-needed,localDeparture=Inverse(startWork);
            double hour=localNow-Math.Floor(localNow);
            return(clock.CalendarDay+(localDeparture-localNow)*clock.CalendarRate,D.Clamp((Work(localNow)-startWork)/needed),hour>=begin&&hour<=end);
        }
        double ForageRadius(HumanGroupSituation group,HumanLifeClock clock)
            =>Math.Min(1600,Math.Min(group.Band.HomeRangeKm*1000*clock.HorizontalScale,clock.DayMinutes*60*.14*1.2));
        public static (double wake,double sleep,double duration) SleepTiming(double daylight,double age,double chronotype,double social,double daily,double temperature=18)
        {
            double dawn=12-daylight*.5,winter=D.Clamp((12-daylight)/12);
            double wake=D.Clamp(dawn-.35+(chronotype-.5)*2.6+(daily-.5)*.8+(age>60?-.45:age>=13&&age<23?.35:0)-(daily<.06?.65:0),3.5,9.25);
            double duration=(age<6?10.2+chronotype:age<13?9+.6*chronotype:age>60?6.9+.7*chronotype:7+.8*chronotype)+winter*.7+(temperature< -5?.2:0);
            duration=Math.Max(6.5,duration+(daily-.5)*.55-(social-.5)*.3);
            return(wake,(wake+24-duration)%24,duration);
        }
        public HumanActivitySample Activity(HumanGroupSituation group,ForagerBandSample residents,ForagerPersonSample person,HumanLifeClock clock,double temperatureC=18,double rainMmHour=0)
        {
            var f=snapshot.Foragers;var culture=f.Cultures[group.Band.Culture];var belief=f.Beliefs[group.Band.Belief];int seed=world.Recipe.Seed;
            double age=person.AgeYears,personality=Unit(person.Id,seed,"human-work-personality"),social=Unit(person.Id,seed,"human-social-personality"),day=Math.Floor(clock.SolarDays);
            var result=new HumanActivitySample{Person=person.Id,Group=group.Band.Id,Position=group.Position,Destination=group.Position,Role=person.Role};
            var homeOffset=HumanCampLayout.HouseholdOffset(residents,person.Household,group.Camp.Id,seed);
            var family=residents.Members.Where(p=>p.Household==person.Household).OrderBy(p=>p.BirthYear).ToArray();
            var peers=family.Where(p=>(p.AgeYears>=13)==(age>=13)).ToArray();int place=Array.FindIndex(peers,p=>p.Id==person.Id);
            double bearing=Math.Atan2(homeOffset.X,homeOffset.Z)+Math.PI;
            double across=age>=13?(place-(peers.Length-1)*.5)*.65:(place%3-1)*.48,along=age>=13?.35:-.75-(place/3)*.45;
            homeOffset+=new V(across*Math.Cos(bearing)+along*Math.Sin(bearing),0,along*Math.Cos(bearing)-across*Math.Sin(bearing));
            double homeBearing=Math.Atan2(homeOffset.Z,homeOffset.X),homeRadius=homeOffset.Length;
            V home=Offset(group.Position,homeRadius/1000/clock.HorizontalScale,homeBearing);
            result.Position=result.Destination=home;
            if(age<3)
            {
                var guardian=residents.Members.FirstOrDefault(p=>p.Id==person.Mother&&p.AgeYears>=18)??residents.Members.FirstOrDefault(p=>p.Id==person.Father&&p.AgeYears>=18)??residents.Members.FirstOrDefault(p=>p.Household==person.Household&&p.AgeYears>=18);
                if(guardian!=null)
                {var care=Activity(group,residents,guardian,clock,temperatureC,rainMmHour);result.Activity=HumanActivity.Carried;result.Partner=guardian.Id;result.Position=care.Position;result.Destination=care.Destination;return result;}
            }
            if(group.Phase==HumanCampPhase.Travelling)
            {result.Activity=group.JourneyRest?HumanActivity.Rest:HumanActivity.Travel;result.Position=Offset(group.Position,(2+Unit(person.Id,seed,"travel-spread")*10)/1000/clock.HorizontalScale,Unit(person.Id,seed,"travel-bearing")*Math.PI*2);result.Destination=group.Destination.Position;result.SpeedMps=result.Activity==HumanActivity.Travel?1.15:0;return result;}
            if(group.Phase==HumanCampPhase.SettingUp||group.Phase==HumanCampPhase.Packing)
            {result.Activity=age>=12?group.Phase==HumanCampPhase.SettingUp?HumanActivity.BuildCamp:HumanActivity.PackCamp:HumanActivity.Play;result.Progress=group.Progress;result.Destination=home;return result;}
            double dawn=12-group.DaylightHours/2,dusk=12+group.DaylightHours/2;
            var sleep=SleepTiming(group.DaylightHours,age,personality,social,Unit(person.Id,seed,"sleep-onset",(int)day),temperatureC);double wake=sleep.wake;
            double hour=group.LocalHour;
            bool sleeping=(hour-sleep.sleep+24)%24<sleep.duration;
            string nightRole=HumanDailyLife.NightRole(group,culture,person,residents,clock.SolarDays,sleeping);
            if(nightRole.Length>0)
            {result.Activity=HumanActivity.Watch;result.Role=nightRole;if(nightRole=="NightWatch")result.Position=result.Destination=Offset(group.Position,.014/clock.HorizontalScale,homeBearing);return result;}
            if(sleeping){result.Activity=HumanActivity.Sleep;return result;}
            if(temperatureC>30&&hour>12&&hour<14.4&&personality<.8){result.Activity=age<6?HumanActivity.Sleep:HumanActivity.Rest;return result;}
            if(rainMmHour>3&&personality<.85){result.Activity=age<8?HumanActivity.Play:person.Skills[0]>.3?HumanActivity.Craft:HumanActivity.Socialise;return result;}
            if(hour<wake+1)
            {
                result.Activity=age<6?HumanActivity.Play:Unit(person.Id,seed,"morning-duty",(int)day)<.3?HumanActivity.FetchWater:HumanActivity.PrepareFood;
                if(result.Activity==HumanActivity.FetchWater)
                {
                    double reach=Math.Min(90,clock.DayMinutes*60/24*.25);
                    var water=Offset(group.Position,reach/1000/clock.HorizontalScale,Unit(group.Band.Id,seed,"water-direction")*Math.PI*2);
                    WorkTrip(result,home,water,hour,wake,wake+1,clock,HumanActivity.FetchWater,"Water");
                }
                return result;
            }
            if(hour>dusk-.7)
            {
                result.Activity=age<6?HumanActivity.Rest:belief.CollectiveRites>.6&&social<.3?HumanActivity.Teach:hour<dusk+1?HumanActivity.PrepareFood:HumanActivity.Socialise;
                result.Role=result.Activity==HumanActivity.PrepareFood?"HearthMeal":"HearthSocial";
                var homes=residents.Members.Select(p=>p.Household).Distinct().OrderBy(h=>h).ToArray();int hearth=Array.IndexOf(homes,person.Household)/3;
                var sharing=homes.Skip(hearth*3).Take(3).ToHashSet();var seats=residents.Members.Where(p=>p.AgeYears>=3&&sharing.Contains(p.Household)).OrderBy(p=>p.Id).ToArray();
                int seat=Array.FindIndex(seats,p=>p.Id==person.Id);double bearingAt=seat*Math.PI*2/Math.Max(1,seats.Length);
                var center=HumanCampLayout.HearthOffset(residents,hearth,group.Camp.Id,seed);double distance=2.1+Math.Max(0,seats.Length-10)*.055;
                var offset=center+new V(Math.Cos(bearingAt)*distance,0,Math.Sin(bearingAt)*distance);
                result.Position=result.Destination=Offset(group.Camp.Position,offset.Length/1000/clock.HorizontalScale,Math.Atan2(offset.Z,offset.X));return result;
            }
            var children=residents.Members.Where(p=>p.AgeYears<7).ToArray();var adults=residents.Members.Where(p=>p.AgeYears>=18&&p.AgeYears<65).OrderBy(p=>Unit(p.Id,seed,"care-duty",(int)day)).ToArray();
            int carers=children.Length==0?0:Math.Max(1,(int)Math.Ceiling(children.Length/(2.5+3*culture.ChildcareCooperation)));
            if(age<7)
            {
                result=HumanDailyLife.Play(group,residents,person,clock,seed);
                result.Position=result.Destination=Offset(group.Position,(age<6?5:8)/1000/clock.HorizontalScale,result.Progress*Math.PI*2);return result;
            }
            if(adults.Take(carers).Any(p=>p.Id==person.Id))
            {result.Activity=HumanActivity.Childcare;result.Partner=children.FirstOrDefault()?.Id??-1;result.Position=result.Destination=Offset(group.Position,.005/clock.HorizontalScale,homeBearing);return result;}
            // A daily shared learning interval has an actual teacher and
            // learners at the same work area; expertise is not hereditary rank.
            if(hour>wake+1.15&&hour<wake+2.15)
            {
                var teacher=residents.Members.Where(p=>p.AgeYears>=30&&p.AgeYears<76&&!adults.Take(carers).Any(a=>a.Id==p.Id))
                    .OrderByDescending(p=>p.Skills[0]+p.Skills[3]+p.Skills[7]+p.Skills[6]).ThenBy(p=>p.Id).FirstOrDefault();
                if(teacher!=null&&(person.Id==teacher.Id||age>=7&&age<17&&personality<culture.ChildcareCooperation*.6+.35))
                {
                    result.Activity=person.Id==teacher.Id?HumanActivity.Teach:HumanActivity.Rest;result.Role=person.Id==teacher.Id?"Teaching":"Learning";
                    result.Partner=person.Id==teacher.Id?residents.Members.FirstOrDefault(p=>p.AgeYears>=7&&p.AgeYears<17)?.Id??-1:teacher.Id;
                    double seat=person.Id==teacher.Id?0:1.1+Unit(person.Id,seed,"learning-seat")*.8;
                    result.Position=result.Destination=Offset(group.Position,seat/1000/clock.HorizontalScale,Unit(person.Id,seed,"learning-bearing")*Math.PI*2);return result;
                }
            }
            double workRank=Unit(person.Id,seed,"daily-work",(int)day);
            bool skill=person.Skills.Length>3&&person.Skills[0]+person.Skills[3]>.9;
            if(age>57||workRank<.16&&skill){result.Activity=age>57&&social<.5?HumanActivity.Teach:HumanActivity.Craft;return result;}
            if(workRank<.26)
            {
                double start=wake+1,finish=Math.Max(start+1,dusk-.8),period=(finish-start)/3;
                int trip=Math.Max(0,Math.Min(2,(int)((hour-start)/period)));
                double reach=Math.Min(130,period/24*clock.DayMinutes*60*.24);
                var wood=Offset(group.Position,reach/1000/clock.HorizontalScale,homeBearing+1+.7*trip);
                WorkTrip(result,home,wood,hour,start+trip*period,start+(trip+1)*period,clock,HumanActivity.GatherFuel,"Fuel");return result;
            }
            if(workRank<.34){result.Activity=HumanActivity.RepairShelter;return result;}
            if(age<13&&workRank>.45){result=HumanDailyLife.Play(group,residents,person,clock,seed);result.Position=result.Destination=Offset(group.Position,.009/clock.HorizontalScale,result.Progress*Math.PI*2);return result;}
            int party=(int)(Unit(person.Household,seed,"forage-party",(int)day)*group.ForageParties);result.Party=party;
            double begin=wake+1.3+.5*Unit(group.Band.Id,seed,"party-leave",party),end=Math.Max(begin+2,Math.Min(dusk-1,begin+6.5));
            double span=(end-begin)/24*clock.DayMinutes*60,range=Math.Min(ForageRadius(group,clock),span*1.15*.28);
            double direction=Unit(group.Band.Id,seed,"party-bearing",unchecked((int)day*31+party))*Math.PI*2;
            V target=Offset(group.Position,range/(1000*clock.HorizontalScale),direction);
            double journey=range/1.15/(clock.DayMinutes*60)*24,fraction;
            if(hour<begin||hour>end){result.Activity=HumanActivity.PrepareFood;return result;}
            if(hour<begin+journey){fraction=(hour-begin)/Math.Max(.001,journey);result.Activity=HumanActivity.Travel;result.SpeedMps=1.15;result.Position=Interpolate(home,target,fraction);result.Destination=target;}
            else if(hour>end-journey){fraction=(hour-(end-journey))/Math.Max(.001,journey);result.Activity=HumanActivity.Travel;result.SpeedMps=1.15;result.Position=Interpolate(target,home,fraction);result.Destination=home;}
            else{result.Activity=HumanActivity.GatherPlants;result.Position=Offset(target,.006/clock.HorizontalScale,homeBearing);result.Destination=result.Position;result.Progress=(hour-begin-journey)/Math.Max(.001,end-begin-2*journey);
                if(result.Progress>.38&&result.Progress<.46+.1*culture.GuestAccess){result.Activity=culture.GuestAccess>.45?HumanActivity.Socialise:HumanActivity.Rest;result.Role="ForagingBreak";}}
            return result;
        }
        void WorkTrip(HumanActivitySample result,V home,V work,double hour,double begin,double end,HumanLifeClock clock,HumanActivity activity,string label)
        {
            double distance=Angle(home,work)*world.Recipe.RadiusKm*1000*clock.HorizontalScale;
            double maxDistance=(end-begin)/24*clock.DayMinutes*60*.32*1.15;
            if(distance>maxDistance){work=Interpolate(home,work,maxDistance/distance);distance=maxDistance;}
            double travel=distance/1.15/(clock.DayMinutes*60)*24;
            if(hour<begin||hour>=end){result.Activity=HumanActivity.Rest;result.Role=label+"AtHome";return;}
            if(hour<begin+travel)
            {result.Activity=HumanActivity.Travel;result.Role=label+"Outbound";result.Progress=D.Clamp((hour-begin)/Math.Max(.0001,travel));result.Position=Interpolate(home,work,result.Progress);result.Destination=work;result.SpeedMps=1.15;}
            else if(hour>end-travel)
            {result.Activity=HumanActivity.Travel;result.Role=label+"Return";result.Progress=D.Clamp((hour-(end-travel))/Math.Max(.0001,travel));result.Position=Interpolate(work,home,result.Progress);result.Destination=home;result.SpeedMps=1.15;}
            else
            {result.Activity=activity;result.Role=label+"Working";result.Position=result.Destination=work;result.Progress=D.Clamp((hour-begin-travel)/Math.Max(.0001,end-begin-2*travel));}
        }
        public HumanCampTrace[] RecentCamps(HumanGroupSituation group,HumanLifeClock clock)
        {
            if(!plans.TryGetValue(group.Band.Id,out var plan)||plan.Permanent)return Array.Empty<HumanCampTrace>();
            var traces=new List<HumanCampTrace>();double yearDays=SimulationTime.Year/(double)SimulationTime.Day;
            double cycle=Math.Floor((clock.CalendarDay-plan.Phase)/yearDays)*yearDays+plan.Phase,cursor=0;
            for(int i=0;i<plan.Sites.Length;i++)
            {
                cursor+=plan.Weights[i]*yearDays;var site=plan.Sites[i];if(site.Id==group.Camp.Id&&group.Phase!=HumanCampPhase.Travelling)continue;
                var next=plan.Sites[(i+1)%plan.Sites.Length];double departure=Journey(site,next,cycle+cursor,clock).depart;
                if(departure>clock.CalendarDay)departure-=yearDays;
                double age=(clock.CalendarDay-departure)/clock.CalendarRate;
                if(departure>=0&&age<=45)traces.Add(new HumanCampTrace{Group=group.Band.Id,Site=site,AbandonedCalendarDay=departure,AgeSolarDays=age});
            }
            return traces.OrderBy(t=>t.AgeSolarDays).ToArray();
        }
        public static long SiteId(long band,int site)=>((long)Rng.Mix((uint)(band>>32)^(uint)site*7919u)<<32)|(uint)Rng.Mix((uint)band^(uint)site*193u);
        public static double Angle(V a,V b){double x=a.Y*b.Z-a.Z*b.Y,y=a.Z*b.X-a.X*b.Z,z=a.X*b.Y-a.Y*b.X;return Math.Atan2(Math.Sqrt(x*x+y*y+z*z),RawDot(a,b));}
        static double RawDot(V a,V b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
        public static V Interpolate(V a,V b,double t){t=D.Clamp(t);double angle=Angle(a,b);if(angle<1e-9)return a;return (a*(Math.Sin((1-t)*angle)/Math.Sin(angle))+b*(Math.Sin(t*angle)/Math.Sin(angle))).Unit;}
        V Offset(V center,double km,double bearing){var east=V.Cross(new V(0,0,1),center).Unit;if(east.Length<.1)east=new V(0,1,0);var north=V.Cross(center,east).Unit;double angle=km/world.Recipe.RadiusKm;return (center*Math.Cos(angle)+(east*Math.Cos(bearing)+north*Math.Sin(bearing))*Math.Sin(angle)).Unit;}
    }
}
