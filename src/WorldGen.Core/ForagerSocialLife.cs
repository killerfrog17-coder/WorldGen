using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldGen.Core
{
    public sealed class ForagerSocialLife
    {
        public string DecisionMaking,Sharing,Visitors,SpeechStyle;
        public long Coordinator,Guide,Teacher,Mediator;public bool Headperson;
        public double Sociability;
        static double Skill(ForagerPersonSample p,int i)=>p.Skills.Length>i?p.Skills[i]:0;
        public static ForagerSocialLife Create(ForagerCulture culture,ForagerBandSample band)
        {
            var c=culture;double settled=c.Residence==ForagerResidence.AquaticResidence?1:c.Residence==ForagerResidence.CollectorCamps?.7:c.Residence==ForagerResidence.SeasonalBaseCamps?.3:0;
            var s=new ForagerSocialLife{Headperson=settled*band.Aggregation>.58&&c.PrestigeCompetition>.68&&c.FoodSharing<.55,
                Sociability=.15+.5*c.GuestAccess+.25*c.ChildcareCooperation,
                Sharing=c.FoodSharing>.65?"Food is normally shared broadly; boasting and hoarding invite criticism.":c.KinObligation>.65?"Households help close kin first, with wider sharing negotiated.":"Contributions and reciprocal help matter; households retain some discretion.",
                Visitors=c.GuestAccess>.65?"Visitors with social connections are welcomed; introductions matter.":c.GuestAccess<.35?"Strangers are approached cautiously and introduced before joining camp.":"Visitors are assessed through introductions, kin and previous dealings.",
                SpeechStyle=c.ConflictAvoidance>.65?"Prefer tact, humour and mediation over direct orders.":c.PrestigeCompetition>.65?"People may speak directly and argue for their expertise; status does not make everyone obedient.":"Speak directly but respect other adults' independence."};
            s.DecisionMaking=s.Headperson?"An influential camp headperson coordinates decisions, supported by experienced adults; this is not a king or a state.":c.KinObligation>.7&&settled>.4?"Household elders confer; wider decisions require discussion between households.":"Adults discuss camp decisions; skilled people lead particular tasks, without a permanent ruler.";
            var adults=(band.Members??Array.Empty<ForagerPersonSample>()).Where(p=>p.AgeYears>=18).ToArray();
            long Pick(Func<ForagerPersonSample,double> score)=>adults.OrderByDescending(score).ThenBy(p=>p.Id).FirstOrDefault()?.Id??0;
            double Mature(ForagerPersonSample p)=>Math.Min(1,p.AgeYears/55);
            s.Guide=Pick(p=>Skill(p,1)+Skill(p,2)+.35*Mature(p));
            s.Teacher=Pick(p=>p.Skills.DefaultIfEmpty().Average()+.45*Mature(p));
            s.Mediator=Pick(p=>.7*Mature(p)+Skill(p,6)+Rng.Value(c.Seed,"social-mediator",unchecked((int)p.Id))*.25);
            s.Coordinator=Pick(p=>.5*Mature(p)+p.Skills.DefaultIfEmpty().Average()+Rng.Value(c.Seed,"social-influence",unchecked((int)p.Id))*.3);
            return s;
        }
        public string Roles(ForagerBandSample band)
        {
            string Name(long id)=>band.Members.FirstOrDefault(p=>p.Id==id)?.Name??"not established";
            return (Headperson?"Camp headperson: ":"Respected camp coordinator (not a chief): ")+Name(Coordinator)+"; travel guide: "+Name(Guide)+"; skilled teacher: "+Name(Teacher)+"; mediator: "+Name(Mediator)+". Roles can overlap; leadership is not restricted to men.";
        }
        static bool Siblings(long a,long b,Dictionary<long,ForagerPersonRecord> r)=>a>0&&b>0&&a!=b&&r.TryGetValue(a,out var x)&&r.TryGetValue(b,out var y)&&(x.Mother>0&&x.Mother==y.Mother||x.Father>0&&x.Father==y.Father);
        public static string Relationship(ForagerPersonSample self,ForagerPersonSample other,ForagerBandSample band)
        {
            if(self==null||other==null)return "unfamiliar person";if(self.Id==other.Id)return "yourself";
            var r=new Dictionary<long,ForagerPersonRecord>();foreach(var p in band.FamilyRecords??Array.Empty<ForagerPersonRecord>())r[p.Id]=p;
            foreach(var p in band.Members??Array.Empty<ForagerPersonSample>())if(!r.ContainsKey(p.Id))r[p.Id]=new ForagerPersonRecord{Id=p.Id,Mother=p.Mother,Father=p.Father,Female=p.Female};
            if(self.Partner==other.Id)return "your partner";if(self.Mother==other.Id)return "your mother";if(self.Father==other.Id)return "your father";
            if(other.Mother==self.Id||other.Father==self.Id)return other.Female?"your daughter":"your son";
            if(Siblings(self.Id,other.Id,r))return other.Female?"your sister":"your brother";
            long[] parents={self.Mother,self.Father},theirs={other.Mother,other.Father};
            if(parents.Any(id=>r.TryGetValue(id,out var p)&&(p.Mother==other.Id||p.Father==other.Id)))return other.Female?"your grandmother":"your grandfather";
            if(theirs.Any(id=>r.TryGetValue(id,out var p)&&(p.Mother==self.Id||p.Father==self.Id)))return other.Female?"your granddaughter":"your grandson";
            if(parents.Any(id=>Siblings(id,other.Id,r)))return other.Female?"your aunt":"your uncle";
            if(theirs.Any(id=>Siblings(id,self.Id,r)))return other.Female?"your niece":"your nephew";
            if(parents.Any(a=>theirs.Any(b=>Siblings(a,b,r))))return "your cousin";
            if(self.Household>0&&self.Household==other.Household)return "a member of your household";
            return "a fellow band member";
        }
        public static double Affinity(ForagerCulture c,ForagerPersonSample a,ForagerPersonSample b,ForagerBandSample band)
        {
            long low=Math.Min(a.Id,b.Id),high=Math.Max(a.Id,b.Id);double n=Rng.Value(c.Seed,"social-bond",unchecked((int)low^(int)(high>>32)^(int)high));
            string kin=Relationship(a,b,band);double family=kin=="a fellow band member"?0:kin=="a member of your household"?.1:.18*c.KinObligation;
            return Math.Max(-1,Math.Min(1,(n-.4)*1.4+family+(1-Math.Min(1,Math.Abs(a.AgeYears-b.AgeYears)/35))*.12));
        }
    }
}
