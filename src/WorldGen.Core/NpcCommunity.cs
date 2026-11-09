using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldGen.Core
{
    public enum NpcWelcome { None,Guest,Member }
    public enum NpcCustom { None,ShareMore,HouseholdShares,WelcomeVisitors,CautiousVisitors,OrganizeWatch,RelaxWatch }
    [Serializable] public sealed class NpcCustomVote
    {
        public long Person,Proposer;public NpcCustom Custom;public double SolarDay;
        public NpcCustomVote Copy()=>(NpcCustomVote)MemberwiseClone();
    }
    [Serializable] public sealed class NpcCommunityState
    {
        public long Coordinator,LeaderCandidate;
        public double Sharing=-1,Visitors=-1,Watch=-1;
        public List<long> Guests=new List<long>(),LeaderSupport=new List<long>();
        public List<ForagerPersonSample> Joined=new List<ForagerPersonSample>();
        public List<ForagerPersonRecord> JoinedFamilies=new List<ForagerPersonRecord>();
        public List<NpcCustomVote> CustomVotes=new List<NpcCustomVote>();
        static ForagerPersonRecord Kin(ForagerPersonRecord p)=>new ForagerPersonRecord{Id=p.Id,Household=p.Household,Mother=p.Mother,Father=p.Father,Partner=p.Partner,Female=p.Female,BirthYear=p.BirthYear,DeathYear=p.DeathYear};
        public NpcCommunityState Copy()=>new NpcCommunityState{Coordinator=Coordinator,LeaderCandidate=LeaderCandidate,Sharing=Sharing,Visitors=Visitors,Watch=Watch,Guests=new List<long>(Guests),LeaderSupport=new List<long>(LeaderSupport),Joined=Joined.Select(PlayerIdentity.CopyPerson).ToList(),JoinedFamilies=JoinedFamilies.Select(Kin).ToList(),CustomVotes=CustomVotes.Select(v=>v.Copy()).ToList()};
        public void Validate()
        {
            bool Policy(double v)=>double.IsFinite(v)&&v>=-1&&v<=1;
            if(Coordinator<0||LeaderCandidate<0||!Policy(Sharing)||!Policy(Visitors)||!Policy(Watch)||Guests==null||LeaderSupport==null||Joined==null||JoinedFamilies==null||CustomVotes==null||Guests.Any(x=>x<=0)||LeaderSupport.Any(x=>x<=0)||Guests.Distinct().Count()!=Guests.Count||LeaderSupport.Distinct().Count()!=LeaderSupport.Count||Joined.Any(p=>p==null||p.Id<=0||p.AgeYears<18)||Joined.Select(p=>p.Id).Distinct().Count()!=Joined.Count||CustomVotes.Any(v=>v==null||v.Person<=0||v.Proposer<=0||!Enum.IsDefined(typeof(NpcCustom),v.Custom)||!double.IsFinite(v.SolarDay)))throw new ArgumentException("Invalid local community decisions.");
        }
    }
    public static class NpcCommunityRules
    {
        public static ForagerCulture Culture(ForagerCulture basis,NpcCommunityState local)
        {
            if(local==null||local.Sharing<0&&local.Visitors<0)return basis;
            var c=basis.Copy();if(local.Sharing>=0)c.FoodSharing=local.Sharing;if(local.Visitors>=0)c.GuestAccess=local.Visitors;return c;
        }
        public static NpcWelcome Welcome(ForagerCulture culture,ForagerBandSample band,long inviter,long coordinator,long visitor,Func<long,NpcSocialState> memory)
        {
            var person=band.Members.FirstOrDefault(p=>p.Id==inviter);if(person==null||person.AgeYears<18||visitor<=0||visitor==inviter)return NpcWelcome.None;
            var tie=memory(inviter)?.Relationships.FirstOrDefault(t=>t.Other==visitor);
            if(tie!=null&&(tie.Grievance>.35||tie.Fear>.4||tie.Trust<-.1))return NpcWelcome.None;
            if(inviter!=coordinator&&culture.GuestAccess<.7)return NpcWelcome.None;
            if((tie?.Trust??0)<.06&&culture.GuestAccess<.6)return NpcWelcome.None;
            int supporters=band.Members.Count(p=>p.AgeYears>=18&&memory(p.Id)?.Relationships.Any(t=>t.Other==visitor&&t.Trust>=.22&&t.Grievance<.25)==true);
            return inviter==coordinator&&supporters>=Math.Min(3,Math.Max(2,band.Members.Count(p=>p.AgeYears>=18)/5))?NpcWelcome.Member:NpcWelcome.Guest;
        }
        public static bool CanSupportLeader(ForagerPersonSample person,NpcPersonality personality,long candidate,NpcSocialState memory)
        {
            var tie=memory?.Relationships.FirstOrDefault(t=>t.Other==candidate);
            return person.AgeYears>=18&&person.Id!=candidate&&tie!=null&&tie.Trust>.22&&tie.Respect>=.12+personality.Ambition*.08&&tie.Grievance<.2&&tie.Fear<.3;
        }
        public static bool Elect(NpcCommunityState local,ForagerBandSample band,ForagerCulture culture,long candidate)
        {
            var eligible=band.Members.Where(p=>p.AgeYears>=18).Select(p=>p.Id).ToHashSet();
            if(!eligible.Contains(candidate)||local.LeaderCandidate!=candidate)return false;
            int support=local.LeaderSupport.Count(p=>p!=candidate&&eligible.Contains(p))+1; // the candidate's own participation
            int threshold=Math.Max(2,(int)Math.Ceiling(eligible.Count*(culture.PrestigeCompetition>.7&&culture.FoodSharing<.5?.68:.55)));
            if(support<threshold)return false;local.Coordinator=candidate;local.LeaderSupport.Clear();return true;
        }
        public static bool Supports(NpcCustom proposal,NpcPersonality p,double risk)
            =>proposal==NpcCustom.ShareMore?p.Kindness>.48:proposal==NpcCustom.HouseholdShares?p.Kindness<.52:proposal==NpcCustom.WelcomeVisitors?p.Sociability+.1>p.Vigilance:proposal==NpcCustom.CautiousVisitors?p.Vigilance+.1>p.Sociability:proposal==NpcCustom.OrganizeWatch?risk>.4||p.Vigilance>.65:proposal==NpcCustom.RelaxWatch?risk<.4&&p.Courage>p.Vigilance:false;
        public static bool VoteCustom(NpcCommunityState local,ForagerBandSample band,long voter,long proposer,NpcCustom proposal,double day)
        {
            if(local.Coordinator!=proposer||voter==proposer||!band.Members.Any(p=>p.Id==voter&&p.AgeYears>=18)||proposal==NpcCustom.None)return false;
            local.CustomVotes.RemoveAll(v=>v.Person==voter||day-v.SolarDay>7);local.CustomVotes.Add(new NpcCustomVote{Person=voter,Proposer=proposer,Custom=proposal,SolarDay=day});
            var eligible=band.Members.Where(p=>p.AgeYears>=18).Select(p=>p.Id).ToHashSet();int count=local.CustomVotes.Count(v=>v.Proposer==proposer&&v.Custom==proposal&&eligible.Contains(v.Person))+1;
            if(count<Math.Max(2,(int)Math.Ceiling(eligible.Count*.55)))return false;
            if(proposal==NpcCustom.ShareMore)local.Sharing=.85;else if(proposal==NpcCustom.HouseholdShares)local.Sharing=.4;
            else if(proposal==NpcCustom.WelcomeVisitors)local.Visitors=.8;else if(proposal==NpcCustom.CautiousVisitors)local.Visitors=.3;
            else local.Watch=proposal==NpcCustom.OrganizeWatch?1:0;
            local.CustomVotes.Clear();return true;
        }
        public static ForagerBandSample WithJoined(ForagerBandSample b,NpcCommunityState local)
        {
            if(local==null||local.Joined.Count==0)return b;
            return new ForagerBandSample{Id=b.Id,Cohort=b.Cohort,District=b.District,Lineage=b.Lineage,Culture=b.Culture,Language=b.Language,Belief=b.Belief,Name=b.Name,Home=b.Home,Position=b.Position,People=b.People,HomeRangeKm=b.HomeRangeKm,MoveFrequencyPerYear=b.MoveFrequencyPerYear,Aggregation=b.Aggregation,FoodSecurity=b.FoodSecurity,Hospitality=b.Hospitality,Residence=b.Residence,Techniques=b.Techniques,RoutineTechniques=b.RoutineTechniques,WinterTechniques=b.WinterTechniques,SeasonalCamps=b.SeasonalCamps,ContactBands=b.ContactBands,
                Members=local.Joined.Concat(b.Members).GroupBy(p=>p.Id).Select(g=>g.First()).ToArray(),FamilyRecords=b.FamilyRecords.Concat(local.JoinedFamilies).GroupBy(p=>p.Id).Select(g=>g.First()).ToArray()};
        }
    }
}
