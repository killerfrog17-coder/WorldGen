using System;
using System.Linq;

namespace WorldGen.Core
{
    // One controlled resident, not a new global population agent. Keep the
    // original identity and family links when local procedural people unload.
    [Serializable] public sealed class PlayerIdentity
    {
        public long Group;
        public int Cohort,District,Culture,Language,Belief,Lineage;
        public string BandName="",CultureName="",LanguageName="",BeliefName="",LineageName="";
        public ForagerPersonSample Person;
        public ForagerPersonRecord[] Family=Array.Empty<ForagerPersonRecord>();
        public bool[] Techniques=Array.Empty<bool>();
        public PlayerIdentity Copy()
        {
            var copy=(PlayerIdentity)MemberwiseClone();copy.Person=CopyPerson(Person);
            copy.Family=Family.Select(p=>new ForagerPersonRecord{Id=p.Id,Household=p.Household,Mother=p.Mother,Father=p.Father,Partner=p.Partner,Female=p.Female,BirthYear=p.BirthYear,DeathYear=p.DeathYear}).ToArray();
            copy.Techniques=(bool[])Techniques.Clone();return copy;
        }
        public static ForagerPersonSample CopyPerson(ForagerPersonSample p)=>p==null?null:new ForagerPersonSample{
            Id=p.Id,Household=p.Household,Mother=p.Mother,Father=p.Father,Partner=p.Partner,Name=p.Name,Role=p.Role,Female=p.Female,
            AgeYears=p.AgeYears,BirthYear=p.BirthYear,DeathYear=p.DeathYear,Appearance=(double[])p.Appearance.Clone(),Knowledge=(double[])p.Knowledge.Clone(),Skills=(double[])p.Skills.Clone(),
            Languages=p.Languages.Select(l=>new ForagerLanguageKnowledge{Language=l.Language,Fluency=l.Fluency,Native=l.Native}).ToArray()};
        public void Validate()
        {
            if(Group<=0||Person==null||Person.Id<=0||string.IsNullOrWhiteSpace(Person.Name)||Family==null||Techniques==null||
                Person.Appearance==null||Person.Knowledge==null||Person.Skills==null||Person.Languages==null||!double.IsFinite(Person.AgeYears)||Person.AgeYears<18)
                throw new ArgumentException("Invalid controlled resident identity.");
        }
    }
    public sealed class ResidentEntry
    {public PlayerIdentity Identity;public HumanGroupSituation Group;public HumanActivitySample Activity;public V Position;}
    public static class PlayerWorldEntry
    {
        public static int Variant(ForagerPersonSample person)
        {
            double pigment=person.Appearance.Length>0?person.Appearance[0]:.5,curl=person.Appearance.Length>3?person.Appearance[3]:.5;
            return person.Female?pigment>.62&&curl>.48?0:pigment<.38?2:4:person.AgeYears>55&&pigment<.55?5:pigment>.5?3:1;
        }
        public static ResidentEntry RandomResident(World world,PlaySession play,int seed)
        {
            var state=world.Prehistory;if(!Prehistory.Foraging(state))throw new InvalidOperationException("There are no simulated human communities at this time.");
            var history=new Prehistory(world);var f=state.Foragers;var rng=new Random(seed);
            var cohorts=f.Cohorts.Select(c=>(cohort:c,count:history.ForagerBandCount(state,c))).Where(p=>p.count>0).ToArray();
            double total=cohorts.Sum(p=>(double)p.count);
            var clock=new HumanLifeClock(Math.Max(0,SimulationTime.Seconds(state)+play.PendingHistorySeconds+play.CalendarOffsetSeconds),play.SolarDays,play.Settings.RealMinutesPerDay,play.Settings.CalendarDaysPerDay,play.Settings.HorizontalScale,play.PrimeMeridianLongitude);
            var planner=new HumanLocalLife(world,history);
            for(int attempt=0;attempt<8192&&total>0;attempt++)
            {
                double rank=rng.NextDouble()*total;var chosen=cohorts[cohorts.Length-1];
                foreach(var entry in cohorts){rank-=entry.count;if(rank<0){chosen=entry;break;}}
                int ordinal=rng.Next(chosen.count);long id=((long)(chosen.cohort.Id+1)<<32)|(uint)(ordinal+1);
                if(!HumanLocalLife.SpatialAdmission(world.Recipe.Seed,id,play.Settings.HorizontalScale))continue;
                var group=planner.Group(state,clock,chosen.cohort.Id,ordinal);if(group==null)continue;
                var residents=planner.Residents(group);
                var adults=residents.Members.Where(p=>p.AgeYears>=18&&!play.Humans.People.Any(m=>m.Person==p.Id&&(m.Dead||m.Health<=0))).ToArray();
                if(adults.Length==0)continue;
                var person=adults[rng.Next(adults.Length)];var activity=planner.Activity(group,residents,person,clock,18,0);
                var identity=new PlayerIdentity{Group=id,Cohort=residents.Cohort,District=residents.District,Culture=residents.Culture,Language=residents.Language,Belief=residents.Belief,Lineage=residents.Lineage,
                    BandName=residents.Name,CultureName=f.Cultures[residents.Culture].Name,LanguageName=f.Languages[residents.Language].Name,BeliefName=f.Beliefs[residents.Belief].Name,LineageName=f.Lineages[residents.Lineage].Name,
                    Person=person,Family=residents.FamilyRecords,Techniques=residents.Techniques}.Copy();
                return new ResidentEntry{Identity=identity,Group=group,Activity=activity,Position=activity.Position};
            }
            throw new InvalidOperationException("No living adult NPC community was found at this time and world scale. Advance the simulation, or enter on random land.");
        }
    }
}
