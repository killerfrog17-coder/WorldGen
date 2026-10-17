using System;
using System.Linq;

namespace WorldGen.Core
{
    public enum ReligiousCosmology { AncestorsAndSpirits, DivineAssembly, OneGod, SacredUnity, NonTheistic }
    public enum ReligiousAfterlife { AncestralWorld, Rebirth, Judgment, Liberation, ThisWorld }
    public enum ReligiousRevelation { InheritedCustom, InspiredTeachers, SacredCanon, ContemplativeInsight }
    public enum ReligiousBranch { Root, Denomination, Synthesis, Renewal }
    public enum ReligiousIssue { None, SacredImages, AncestorRites, Sacrifice, Revelation, ClericalAuthority, CourtAuthority, MutualAid }
    public enum ReligiousInstitutionForm { HouseholdCircle, ShrineCouncil, TempleCollege, TeachingFellowship, MonasticOrder, CentralSee }

    // Stable structured inputs for simulation and later authored texts. These
    // are independent axes, not a hierarchy of religious advancement or virtue.
    [Serializable] public sealed class HistoricalReligiousDoctrine
    {
        public int Id,RootId,SecondaryParent=-1,Generation;
        public ReligiousBranch Branch;
        public ReligiousIssue Issue;
        public ReligiousCosmology Cosmology;
        public ReligiousAfterlife Afterlife;
        public ReligiousRevelation Revelation;
        public ReligiousInstitutionForm Organization;
        // 0..1000. Images/ancestors/sacrifice run from prohibited to obligatory.
        public int Images,Ancestors,Sacrifice,Exclusivity,Orthodoxy,Outreach,Universalism;
        public int ClericalAuthority,RulerSanctity,Asceticism,Charity,Purity,Nonviolence;
        public int Intermarriage,Hospitality,CalendarDays,FestivalDay,RestDays;
        public HistoricalReligiousDoctrine Copy()=>(HistoricalReligiousDoctrine)MemberwiseClone();
        public int[] Axes()=>new[]{Images,Ancestors,Sacrifice,Exclusivity,Orthodoxy,Outreach,Universalism,ClericalAuthority,RulerSanctity,Asceticism,Charity,Purity,Nonviolence,Intermarriage,Hospitality};
    }
    [Serializable] public sealed class HistoricalReligiousTraditions
    {
        public const int InstitutionFields=15;
        public int Version=1,SinceYear;
        public HistoricalReligiousDoctrine[] Doctrines=Array.Empty<HistoricalReligiousDoctrine>();
        // Two local bodies per town: public worship and a separately provisioned
        // lay association. Indexed by actor, never a recurring cell census.
        public HistoricalCellTree Institutions=new HistoricalCellTree(33554432);
        public HistoricalReligiousTraditions Copy()=>new HistoricalReligiousTraditions{Version=Version,SinceYear=SinceYear,Doctrines=Doctrines.Select(p=>p.Copy()).ToArray(),Institutions=Institutions};
    }
    public sealed class ReligiousComparison
    {
        public int BeliefAffinity,PracticeAffinity,AuthorityAffinity,Tension;
        public string Disagreement="No salient doctrinal conflict";
    }
    public sealed class ReligiousInstitutionView
    {
        public int Town,Slot,Faith,Seat,Support,Independence,SinceYear,ReviewedYear,Tension,Dissent,Patron,SeparatedYear;
        public ReligiousInstitutionForm Form;
    }
    public static class ReligiousDoctrines
    {
        static int Q(double v)=>(int)Math.Round(Math.Max(0,Math.Min(1000,v)));
        public static HistoricalReligiousDoctrine Root(int id,Func<int,double> roll)
        {
            int Value(int k)=>(int)(roll(k)*1001);
            var p=new HistoricalReligiousDoctrine{Id=id,RootId=id,Cosmology=(ReligiousCosmology)(int)(roll(0)*5),Afterlife=(ReligiousAfterlife)(int)(roll(1)*5),Revelation=(ReligiousRevelation)(int)(roll(2)*4),Organization=(ReligiousInstitutionForm)(int)(roll(3)*5),
                Images=Value(4),Ancestors=Value(5),Sacrifice=Value(6),Exclusivity=Value(7),Orthodoxy=Value(8),Outreach=Value(9),Universalism=Value(10),ClericalAuthority=Value(11),RulerSanctity=Value(12),Asceticism=Value(13),Charity=Value(14),Purity=Value(15),Nonviolence=Value(16),Intermarriage=Value(17),Hospitality=Value(18),CalendarDays=roll(19)<.5?354:365,FestivalDay=1+(int)(roll(20)*350),RestDays=4+(int)(roll(21)*7)};
            return p;
        }
        public static HistoricalReligiousDoctrine Descendant(HistoricalReligiousDoctrine parent,int id,ReligiousIssue issue,HistoricalReligiousDoctrine donor=null)
        {
            var p=parent.Copy();p.Id=id;p.Generation++;p.Issue=issue;p.SecondaryParent=donor?.Id??-1;p.Branch=donor!=null?ReligiousBranch.Synthesis:issue==ReligiousIssue.MutualAid?ReligiousBranch.Renewal:ReligiousBranch.Denomination;
            int Turn(int v)=>v<500?Math.Min(950,v+300):Math.Max(50,v-300);
            switch(issue)
            {
                case ReligiousIssue.SacredImages:p.Images=donor?.Images??Turn(p.Images);break;
                case ReligiousIssue.AncestorRites:p.Ancestors=donor?.Ancestors??Turn(p.Ancestors);break;
                case ReligiousIssue.Sacrifice:p.Sacrifice=donor?.Sacrifice??Turn(p.Sacrifice);break;
                case ReligiousIssue.Revelation:p.Revelation=donor?.Revelation??(ReligiousRevelation)(((int)p.Revelation+1)%4);break;
                case ReligiousIssue.ClericalAuthority:p.ClericalAuthority=Math.Max(50,p.ClericalAuthority-300);p.Organization=ReligiousInstitutionForm.TeachingFellowship;break;
                case ReligiousIssue.CourtAuthority:p.RulerSanctity=Math.Max(0,p.RulerSanctity-350);break;
                case ReligiousIssue.MutualAid:p.Charity=Math.Min(1000,p.Charity+250);p.Asceticism=Math.Min(1000,p.Asceticism+150);break;
            }
            return p;
        }
        static double Opposition(int a,int b)=>Math.Max(0,Math.Abs(a-b)-400)/600.0;
        public static ReligiousIssue MainIssue(HistoricalReligiousDoctrine a,HistoricalReligiousDoctrine b)
        {
            int[] gaps={Math.Abs(a.Images-b.Images),Math.Abs(a.Ancestors-b.Ancestors),Math.Abs(a.Sacrifice-b.Sacrifice),a.Revelation==b.Revelation?0:600,Math.Abs(a.ClericalAuthority-b.ClericalAuthority),Math.Abs(a.RulerSanctity-b.RulerSanctity)};
            int best=0;for(int k=1;k<gaps.Length;k++)if(gaps[k]>gaps[best])best=k;
            return gaps[best]>=250?(ReligiousIssue)(best+1):ReligiousIssue.None;
        }
        public static ReligiousComparison Compare(HistoricalReligiousDoctrine a,HistoricalReligiousDoctrine b)
        {
            if(a==null||b==null)return new ReligiousComparison();
            int theology=(a.Cosmology==b.Cosmology?0:350)+(a.Afterlife==b.Afterlife?0:250)+(a.Revelation==b.Revelation?0:200);
            double practice=(Math.Abs(a.Images-b.Images)+Math.Abs(a.Ancestors-b.Ancestors)+Math.Abs(a.Sacrifice-b.Sacrifice)+Math.Abs(a.Asceticism-b.Asceticism)+Math.Abs(a.Purity-b.Purity))/5.0;
            double authority=(Math.Abs(a.ClericalAuthority-b.ClericalAuthority)+Math.Abs(a.RulerSanctity-b.RulerSanctity))/2.0;
            double[] clashes={Opposition(a.Images,b.Images),Opposition(a.Ancestors,b.Ancestors),Opposition(a.Sacrifice,b.Sacrifice),a.Revelation==b.Revelation?0:.5,authority/1000};
            string[] reasons={"Veneration versus prohibition of sacred images","Obligatory versus forbidden ancestor rites","Required versus rejected sacrificial offerings","Competing sources of revelation","Disputed clerical or royal religious authority"};
            int strongest=0;for(int k=1;k<clashes.Length;k++)if(clashes[k]>clashes[strongest])strongest=k;
            double insistence=(a.Orthodoxy+b.Orthodoxy+a.Exclusivity+b.Exclusivity)/4000.0;
            // Kinship is not guaranteed harmony: closely related sects can
            // disagree sharply about a single non-negotiable practice.
            return new ReligiousComparison{BeliefAffinity=Q(1000-theology),PracticeAffinity=Q(1000-practice),AuthorityAffinity=Q(1000-authority),
                Tension=Q(1000*(.65*clashes[strongest]+.35*theology/800.0)*insistence),Disagreement=clashes[strongest]>.15?reasons[strongest]:"No salient doctrinal conflict"};
        }
        public static double LocalGrievance(ReligiousComparison difference,int policy,bool funded,int inclusion,int institution)
        {
            if(difference==null||!funded||policy!=2)return 0;
            return D.Q(.35*difference.Tension/1000.0*(.3+.7*D.Clamp(institution/1000.0))*(1-.65*D.Clamp(inclusion/1000.0)),1000000000);
        }
        public static double ConversionMultiplier(ReligiousComparison c,HistoricalReligiousDoctrine donor,int familiarity)
            =>D.Q(Math.Max(.15,Math.Min(1.5,.20+.35*c.BeliefAffinity/1000.0+.30*c.PracticeAffinity/1000.0+.25*donor.Outreach/1000.0+.15*donor.Universalism/1000.0+.10*donor.Intermarriage/1000.0+.25*D.Clamp(familiarity/1000.0)-.35*c.Tension/1000.0)),1000000000);
        public static int Affinity(ReligiousComparison c)=>(c.BeliefAffinity+c.PracticeAffinity+c.AuthorityAffinity)/3;
        public static string Name(Enum value)=>System.Text.RegularExpressions.Regex.Replace(value.ToString(),"([a-z])([A-Z])","$1 $2");
        public static string Position(int v,string low,string high)=>v<=300?low:v>=700?high:"Locally variable; neither strictly required nor forbidden";
    }
}
