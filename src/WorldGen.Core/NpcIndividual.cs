using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldGen.Core
{
    // Derived only for instantiated people, not a global individual simulation.
    public sealed class NpcPersonality
    {
        public double Courage,Sociability,Kindness,Ambition,Patience,Curiosity,Vigilance,Devotion;
        public string Interest,Speech;
        public static NpcPersonality For(ForagerPersonSample person,ForagerCulture culture)
        {
            double Trait(string key,double center=.5)=>D.Clamp(center+(Rng.Value(519713,"person-"+key,unchecked((int)person.Id^(int)(person.Id>>32)))-.5)*.9);
            var p=new NpcPersonality{Courage=Trait("courage"),Sociability=Trait("sociability",.25+.45*culture.GuestAccess),Kindness=Trait("kindness",.25+.45*culture.FoodSharing),Ambition=Trait("ambition",.2+.55*culture.PrestigeCompetition),Patience=Trait("patience",.3+.4*culture.ConflictAvoidance),Curiosity=Trait("curiosity"),Vigilance=Trait("vigilance")};
            string[] interests={"making and repairing tools","animal habits and tracks","travel routes and places","plants and useful materials","food and cooking","stories and remembered people","helping and teaching children","weather and changing seasons"};
            int favorite=(int)(Rng.Value(519713,"person-interest",unchecked((int)person.Id))*interests.Length);
            if(person.Skills!=null&&person.Skills.Length>favorite&&person.Skills.Max()>.65)favorite=Array.IndexOf(person.Skills,person.Skills.Max())%interests.Length;
            p.Devotion=Trait("devotion");p.Interest=interests[favorite];p.Speech=p.Sociability<.3?"reserved; prefers short answers":p.Patience<.3?"blunt and easily irritated":p.Curiosity>.7?"curious; sometimes asks practical questions":p.Ambition>.7?"proud of competence and sensitive to status":"plain-spoken, with individual opinions";
            return p;
        }
        public string Describe()=>"Personality: "+(Courage>.7?"bold":Courage<.3?"cautious about danger":"weighs risks")+", "+Speech+". Often interested in "+Interest+". Kindness "+(Kindness>.65?"generous":Kindness<.3?"guarded":"selective")+"; competition "+(Ambition>.65?"important":"not a main concern")+".";
    }
    public enum NpcIncidentKind { AnimalSeen,AnimalHeard,Assault,Death,Theft,Trespass,Help,Missing,Returned,Argument,Reconciliation,Invitation,Leadership,DistressHeard }
    [Serializable] public sealed class NpcIncident
    {
        public long Sequence,Actor,Subject;public ulong Animal;public NpcIncidentKind Kind;
        public double SolarDay,Confidence=1;public V Position;public string Detail="";public bool FirstHand=true;
        public NpcIncident Copy()=>(NpcIncident)MemberwiseClone();
    }
    [Serializable] public sealed class NpcRelationship
    {
        public long Other;public double Trust,Affection,Respect,Grievance,Fear,LastSolarDay;
        public NpcRelationship Copy()=>(NpcRelationship)MemberwiseClone();
    }
    [Serializable] public sealed class NpcKnownPerson
    {
        public long Person;public double LastSeenSolarDay;public V LastPosition;public bool SeenDead;
        public NpcKnownPerson Copy()=>(NpcKnownPerson)MemberwiseClone();
    }
    [Serializable] public sealed class NpcSocialState
    {
        public double UpdatedSolarDay=-1,Fear,Anger,Grief,Contentment=.35;
        public long EventSequence;public List<NpcIncident> Incidents=new List<NpcIncident>();
        public List<NpcRelationship> Relationships=new List<NpcRelationship>();public List<NpcKnownPerson> KnownPeople=new List<NpcKnownPerson>();
        public NpcSocialState Copy()=>new NpcSocialState{UpdatedSolarDay=UpdatedSolarDay,Fear=Fear,Anger=Anger,Grief=Grief,Contentment=Contentment,EventSequence=EventSequence,Incidents=Incidents.Select(x=>x.Copy()).ToList(),Relationships=Relationships.Select(x=>x.Copy()).ToList(),KnownPeople=KnownPeople.Select(x=>x.Copy()).ToList()};
        public void Advance(double solarDay)
        {
            double elapsed=UpdatedSolarDay<0?0:Math.Max(0,solarDay-UpdatedSolarDay);UpdatedSolarDay=solarDay;
            Fear*=Math.Exp(-elapsed*24/1.2);Anger*=Math.Exp(-elapsed*24/9);Grief*=Math.Exp(-elapsed/5);Contentment=.35+(Contentment-.35)*Math.Exp(-elapsed*2);
        }
        public NpcRelationship Tie(long id,double day)
        {
            var tie=Relationships.FirstOrDefault(t=>t.Other==id);if(tie!=null)return tie;
            tie=new NpcRelationship{Other=id,LastSolarDay=day};Relationships.Add(tie);
            if(Relationships.Count>48)Relationships.Remove(Relationships.Where(t=>t!=tie).OrderBy(t=>Math.Abs(t.Trust)+Math.Abs(t.Affection)+t.Grievance+t.Fear).ThenBy(t=>t.LastSolarDay).First());return tie;
        }
        public void See(long person,V position,double day,bool dead=false)
        {
            var known=KnownPeople.FirstOrDefault(p=>p.Person==person);if(known==null){known=new NpcKnownPerson{Person=person};KnownPeople.Add(known);if(KnownPeople.Count>64)KnownPeople.Remove(KnownPeople.Where(p=>p!=known).OrderBy(p=>p.LastSeenSolarDay).First());}
            known.LastPosition=position;known.LastSeenSolarDay=day;known.SeenDead|=dead;
        }
        public void Record(NpcIncident item)
        {
            var old=Incidents.LastOrDefault(e=>e.Kind==item.Kind&&e.Actor==item.Actor&&e.Subject==item.Subject&&e.Animal==item.Animal&&item.SolarDay-e.SolarDay<.08);
            if(old!=null){old.SolarDay=item.SolarDay;old.Position=item.Position;old.Detail=NpcDecision.Clean(item.Detail,220);return;}
            item.Sequence=++EventSequence;item.Detail=NpcDecision.Clean(item.Detail,220);Incidents.Add(item);while(Incidents.Count>24)Incidents.RemoveAt(0);
        }
        public string Mood=>Grief>.5?"grieving":Fear>.55?"afraid":Anger>.55?"angry":Grief>.2?"sad":Fear>.2?"uneasy":Anger>.2?"irritated":Contentment>.65?"content":"settled";
        public void Validate()
        {
            bool Unit(double v)=>double.IsFinite(v)&&v>=0&&v<=1;
            if(!double.IsFinite(UpdatedSolarDay)||UpdatedSolarDay< -1||!Unit(Fear)||!Unit(Anger)||!Unit(Grief)||!Unit(Contentment)||EventSequence<0||Incidents==null||Relationships==null||KnownPeople==null||Incidents.Count>24||Relationships.Count>48||KnownPeople.Count>64)throw new ArgumentException("Invalid local social state.");
            if(Incidents.Any(e=>e==null||e.Sequence<1||e.Sequence>EventSequence||!double.IsFinite(e.SolarDay)||e.SolarDay<0||!Unit(e.Confidence)||e.Detail==null||e.Detail.Length>220)||Relationships.Any(r=>r==null||r.Other<=0||!double.IsFinite(r.Trust)||Math.Abs(r.Trust)>1||!double.IsFinite(r.Affection)||Math.Abs(r.Affection)>1||!Unit(r.Respect)||!Unit(r.Grievance)||!Unit(r.Fear))||KnownPeople.Any(p=>p==null||p.Person<=0||!double.IsFinite(p.LastSeenSolarDay)))throw new ArgumentException("Invalid local social record.");
        }
    }
}
