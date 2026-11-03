using System;
using System.Linq;
using System.Runtime.CompilerServices;
namespace WorldGen.Core
{
    public enum AdoptionShape { Linear, EarlySpread, SocialSpread }
    // A finite, dated curve. Inspection never changes simulation state. Once
    // finished, evaluation returns its stored endpoint without exponentials.
    [Serializable] public sealed class AdoptionCurve
    {
        public double Start,End,From,To;
        public AdoptionShape Shape;
        public double At(double year)
        {
            if(year>=End)return To;if(year<=Start)return From;
            double t=(year-Start)/(End-Start);
            if(Shape==AdoptionShape.EarlySpread)t=(1-Math.Exp(-4*t))/(1-Math.Exp(-4));
            else if(Shape==AdoptionShape.SocialSpread)t=t*t*(3-2*t);
            return D.Clamp(From+(To-From)*t);
        }
        public static AdoptionCurve Fixed(double value)=>new AdoptionCurve{From=value,To=value};
    }
    [Serializable] public sealed class ForagerAdoptionPlan
    {
        public AdoptionCurve[] Knowledge,Access,Use;
    }
    public static class ForagerAdoption
    {
        sealed class Values { public double Year=double.NaN,KnowledgeYear=double.NaN,AccessYear=double.NaN,UseYear=double.NaN,KnowledgeEnd,AccessEnd,UseEnd,TerminalYear;public double[] Known,Access,Use,Makers; }
        static readonly ConditionalWeakTable<ForagerAdoptionPlan,Values> Cache=new ConditionalWeakTable<ForagerAdoptionPlan,Values>();
        static readonly uint[] InventionStreams=ForagerTechnology.All.Select(t=>Rng.StreamHash("regional-tribal-invention-"+t.Id)).ToArray(),SpreadStreams=ForagerTechnology.All.Select(t=>Rng.StreamHash("tribal-spread-duration-"+t.Id)).ToArray();
        static readonly int[] Order=TopologicalOrder();
        static int[] TopologicalOrder(){var done=new bool[ForagerTechnology.Count];var order=new System.Collections.Generic.List<int>();void Visit(int k){if(done[k])return;done[k]=true;var t=ForagerTechnology.All[k];foreach(int parent in t.Prerequisites)Visit(parent);foreach(var path in t.AlternativePaths)foreach(int parent in path)Visit(parent);order.Add(k);}for(int k=0;k<done.Length;k++)Visit(k);return order.ToArray();}
        public static ForagerAdoptionPlan Seed(ForagerCohort c)
        {
            var p=new ForagerAdoptionPlan{Knowledge=new AdoptionCurve[ForagerTechnology.Count],Access=new AdoptionCurve[ForagerTechnology.Count],Use=new AdoptionCurve[ForagerTechnology.Count]};
            for(int k=0;k<ForagerTechnology.Count;k++)
            {
                double known=D.Clamp(c.Knowledge[k]),access=Math.Min(known,ForagerTechnology.ProductCoverage(c.Skill[k]));
                p.Knowledge[k]=AdoptionCurve.Fixed(known);p.Access[k]=AdoptionCurve.Fixed(access);p.Use[k]=AdoptionCurve.Fixed(Math.Min(access,c.Use[k]));
            }
            return p;
        }
        public static ForagerCohort Project(ForagerState f,ForagerCohort c)
        {
            if(c==null||f.Adoption.Length<=c.District||f.Adoption[c.District]==null)return c;
            var plan=f.Adoption[c.District];var values=Cache.GetValue(plan,p=>{double k=p.Knowledge.Max(x=>x.End),a=p.Access.Max(x=>x.End),u=p.Use.Max(x=>x.End);return new Values{KnowledgeEnd=k,AccessEnd=a,UseEnd=u,TerminalYear=Math.Max(k,Math.Max(a,u))};});double year=Math.Min(values.TerminalYear,f.Seconds/(double)SimulationTime.Year);
            lock(values)
            {
                if(values.Year!=year)
                {
                    int n=ForagerTechnology.Count;double ky=Math.Min(year,values.KnowledgeEnd),ay=Math.Min(year,values.AccessEnd),uy=Math.Min(year,values.UseEnd);
                    bool changedKnowledge=ky!=values.KnowledgeYear,changedAccess=changedKnowledge||ay!=values.AccessYear;
                    if(changedKnowledge)
                    {
                        values.Known=new double[n];foreach(int k in Order)values.Known[k]=Math.Min(plan.Knowledge[k].At(ky),ForagerTechnology.Prerequisites(values.Known,k));values.KnowledgeYear=ky;
                    }
                    if(changedAccess)
                    {
                        values.Access=new double[n];values.Makers=new double[n];foreach(int k in Order){values.Access[k]=Math.Min(values.Known[k],Math.Min(plan.Access[k].At(ay),ForagerTechnology.Prerequisites(values.Access,k)));values.Makers[k]=values.Access[k]*(k==0||k==3||k==5?.45:.12);}values.AccessYear=ay;
                    }
                    if(changedAccess||uy!=values.UseYear){values.Use=new double[n];for(int k=0;k<n;k++)values.Use[k]=Math.Min(values.Access[k],plan.Use[k].At(uy));values.UseYear=uy;}
                    values.Year=year;
                }
                return c.TechnologyView(values.Known,values.Makers,values.Use,values.Access);
            }
        }
        public static double Access(ForagerCohort c,int k)=>c.TribalAccess==null?ForagerTechnology.ProductCoverage(c.Skill[k]):c.TribalAccess[k];
        static bool Equal(AdoptionCurve a,AdoptionCurve b)=>ReferenceEquals(a,b)||a.Start==b.Start&&a.End==b.End&&a.From==b.From&&a.To==b.To&&a.Shape==b.Shape;
        static bool Equal(ForagerAdoptionPlan a,ForagerAdoptionPlan b)
        {if(ReferenceEquals(a,b))return true;for(int k=0;k<ForagerTechnology.Count;k++)if(!Equal(a.Knowledge[k],b.Knowledge[k])||!Equal(a.Access[k],b.Access[k])||!Equal(a.Use[k],b.Use[k]))return false;return true;}
        static AdoptionCurve Spread(double year,double from,double to,double duration,AdoptionShape shape,double delay=0)
            =>Math.Abs(from-to)<1e-9?AdoptionCurve.Fixed(to):new AdoptionCurve{Start=year+delay,End=year+delay+Math.Max(1,duration),From=from,To=D.Clamp(to),Shape=shape};
        // Only inventions, new contact or a substantial change of practical
        // context create curves. There is no scheduled integration of percentages.
        public static ForagerAdoptionPlan Review(int seed,ForagerState f,ForagerCohort c,ForagerDistrict d,ForagerEnvironment e,ForagerCulture culture,int elapsed,double[] teachers,out ulong inventions)
        {
            double year=f.Seconds/(double)SimulationTime.Year;var current=Project(f,c);var old=f.Adoption[c.District]??Seed(c);
            double learningCapacity=ForagerSociality.Capacity(f,c,false),symbolicPotential=ForagerSociality.Capacity(f,c,true);
            ForagerAdoptionPlan next=null;ForagerAdoptionPlan Touch()=>next??(next=new ForagerAdoptionPlan{Knowledge=(AdoptionCurve[])old.Knowledge.Clone(),Access=(AdoptionCurve[])old.Access.Clone(),Use=(AdoptionCurve[])old.Use.Clone()});inventions=0;
            for(int k=0;k<ForagerTechnology.Count;k++)
            {
                var t=ForagerTechnology.All[k];double known=current.Knowledge[k],access=Access(current,k),use=current.Use[k];
                double materials=ForagerTechnology.Materials(d,e,k),demand=ForagerTechnology.Demand(d,e,current,k),foundation=ForagerTechnology.Prerequisites(current.Knowledge,k);
                bool invented=false;
                if(known==0&&c.Adults>=2&&foundation>0)
                {double complexity=D.Clamp((t.InnovationYears-60000)/640000d),innovationCapacity=(1-complexity)+complexity*(.25+.75*D.Clamp(symbolicPotential));double hazard=1-Math.Exp(-elapsed/t.InnovationYears*Math.Min(12,Math.Max(0,c.Adults)/2500)*foundation*(.2+.8*materials)*(.15+.85*demand)*innovationCapacity);invented=Rng.ValueFromStreamHash(seed,InventionStreams[k],unchecked(c.Id*104729+(int)year))<hazard;}
                bool contact=teachers!=null&&teachers[k]>.001&&foundation>.01;
                if((invented||contact||known>0)&&old.Knowledge[k].To<1&&year>=old.Knowledge[k].End)
                {
                    double communication=(.3+.7*f.Languages[c.Language].TechnicalRegister)*(.4+.6*D.Clamp(learningCapacity));
                    double distance=1+Math.Min(4,d.RadiusKm/100);double openness=.35+.65*culture.ExchangeOpenness;
                    double duration=t.TeachingYears*distance/(openness*communication)*(.7+.6*Rng.ValueFromStreamHash(seed,SpreadStreams[k],c.District));
                    // Start from the current share; one newly trained tribe is
                    // acquired over the opening of the curve, not a region jump.
                    Touch().Knowledge[k]=Spread(year,known,1,duration,k==0||k==2||k==3?AdoptionShape.EarlySpread:AdoptionShape.SocialSpread);
                    if(c.FirstKnownYear[k]<0)c.FirstKnownYear[k]=(int)year;if(invented)inventions|=1UL<<k;
                }
                var knowing=next?.Knowledge[k]??old.Knowledge[k];if(knowing.To<=0)continue;
                double practicalTarget=D.Clamp(materials*2.5),useTarget=D.Clamp(demand*(.65+.7*culture.TechniquePreference[k])*materials);
                // Replan only at the end, or when the environment moves the
                // target substantially. Always anchor at today's value.
                if(old.Access[k].To==0||year>=old.Access[k].End&&Math.Abs(old.Access[k].To-practicalTarget)>.12)
                {Touch().Access[k]=Spread(year,access,practicalTarget,Math.Max(t.PracticeYears*20,(knowing.End-year)*.9),AdoptionShape.SocialSpread,t.PracticeYears);}
                if(old.Use[k].To==0||year>=old.Use[k].End&&Math.Abs(old.Use[k].To-useTarget)>.15)
                {Touch().Use[k]=Spread(year,use,useTarget,Math.Max(120,t.TeachingYears*2),AdoptionShape.SocialSpread,t.PracticeYears*2);}
            }
            return next??old;
        }
        public static ForagerAdoptionPlan Merge(ForagerState f,ForagerCohort destination,ForagerCohort source,double oldPopulation,double migrants)
        {
            var a=Project(f,destination);var b=Project(f,source);double total=Math.Max(1,oldPopulation+migrants);var existing=f.Adoption[destination.District];var p=new ForagerAdoptionPlan{Knowledge=new AdoptionCurve[ForagerTechnology.Count],Access=new AdoptionCurve[ForagerTechnology.Count],Use=new AdoptionCurve[ForagerTechnology.Count]};double year=f.Seconds/(double)SimulationTime.Year;
            if(Equal(existing,f.Adoption[source.District]))return existing;
            for(int k=0;k<ForagerTechnology.Count;k++)
            {
                AdoptionCurve Blend(AdoptionCurve original,AdoptionCurve incoming,double av,double bv)=>Equal(original,incoming)||av==bv&&original.To==incoming.To&&year>=Math.Max(original.End,incoming.End)?original:Spread(year,(av*oldPopulation+bv*migrants)/total,(original.To*oldPopulation+incoming.To*migrants)/total,Math.Max(1,Math.Max(original.End,incoming.End)-year),original.Shape);
                var donor=f.Adoption[source.District];p.Knowledge[k]=Blend(existing.Knowledge[k],donor.Knowledge[k],a.Knowledge[k],b.Knowledge[k]);p.Access[k]=Blend(existing.Access[k],donor.Access[k],Access(a,k),Access(b,k));p.Use[k]=Blend(existing.Use[k],donor.Use[k],a.Use[k],b.Use[k]);
            }
            return p;
        }
    }
}
