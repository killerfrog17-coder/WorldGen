using System;
namespace WorldGen.Core
{
    public readonly struct ForagerDemographicProjection
    {
        public readonly double[] Ages;public readonly double Births,Deaths;
        public double Population=>Ages[0]+Ages[1]+Ages[2]+Ages[3]+Ages[4];
        public ForagerDemographicProjection(double[] ages,double births,double deaths){Ages=ages;Births=births;Deaths=deaths;}
    }
    public static class ForagerDemography
    {
        // Five aggregate age stocks, not a modern life table. These are model
        // priors calibrated against broad forager demography, with independent
        // environmental/food/trauma modifiers and explicit mass balance.
        static readonly double[] Ageing={1/5d,1/10d,1/25d,1/20d,0};
        public static double Present(ForagerState state,ForagerCohort c)
        {double elapsed=(state.Seconds-state.BoundarySeconds)/(double)SimulationTime.Year;return elapsed>0?Project(c,elapsed,state.BoundarySeconds/(double)SimulationTime.Year).Population:c.Population;}
        public static void SetPopulation(ForagerCohort c,double population)
        {c.Ages=new[]{population*.13,population*.2,population*.4,population*.2,population*.07};Sync(c);}
        static void Sync(ForagerCohort c){c.Children=c.Ages[0]+c.Ages[1];c.Adults=c.Ages[2]+c.Ages[3];c.Elders=c.Ages[4];}
        public static void Rates(ForagerCohort c,ForagerLivelihood life,ForagerEnvironment environment,ForagerCulture culture,double sharedCapacity)
        {
            c.Capacity=Math.Max(0,sharedCapacity);c.WinterSecurity=life.WinterSecurity;c.Health=life.Health;c.Diet=(double[])life.Diet.Clone();
            double load=c.Population/Math.Max(1,sharedCapacity),nutrition=D.Clamp(sharedCapacity/Math.Max(1,c.Population),0,1.05);
            c.FoodSecurity=nutrition;double stress=D.Clamp((1-nutrition)/.65),cold=1-life.WinterSecurity;
            double mates=D.Clamp((c.Adults+c.MatingContacts-3)/15),care=.88+.12*culture.ChildcareCooperation;
            c.BirthRate=.096*D.Clamp(nutrition*1.08,0,1)*D.Clamp(1-.22*load,.25,1)*care*mates;
            c.ChildMortality=.035+.025*environment.Pathogens+.12*stress*stress+.10*cold+.02*c.ConflictPressure;
            c.AdultMortality=.009+.007*environment.Pathogens+.055*stress*stress+.07*cold+.012*c.ConflictPressure;
            c.ElderMortality=.09+.03*environment.Pathogens+.16*stress+.12*cold;
            if(sharedCapacity<1){c.BirthRate=0;c.ChildMortality+=2.5;c.AdultMortality+=1.5;c.ElderMortality+=2.5;}
            c.BirthRate=D.Q(c.BirthRate,1000000000);c.ChildMortality=D.Q(c.ChildMortality,1000000000);c.AdultMortality=D.Q(c.AdultMortality,1000000000);c.ElderMortality=D.Q(c.ElderMortality,1000000000);
        }
        public static ForagerDemographicProjection Project(ForagerCohort c,double years,double absoluteYear)
        {
            if(years<0||!double.IsFinite(years))throw new ArgumentOutOfRangeException(nameof(years));
            var ages=(double[])c.Ages.Clone();double births=0,deaths=0;
            if(ages[0]+ages[1]+ages[2]+ages[3]+ages[4]<=0&&c.Population>0)ages=new[]{c.Children*.4,c.Children*.6,c.Adults*.67,c.Adults*.33,c.Elders};
            double remaining=years,at=absoluteYear;var next=new double[5];
            double[] mortality={c.ChildMortality*1.55,c.ChildMortality*.65,c.AdultMortality,c.AdultMortality*1.35,c.ElderMortality};
            while(remaining>1e-10)
            {
                double dt=Math.Min(1,remaining),shock=0;double midpoint=(at+dt*.5)*SimulationTime.Year;
                if(c.ShockEnd>c.ShockStart&&midpoint>=c.ShockStart&&midpoint<c.ShockEnd)shock=c.ShockSeverity;
                Array.Clear(next,0,next.Length);
                for(int k=0;k<5;k++)
                {
                    double death=mortality[k]+shock*(k==0||k==4?1.4:1),hazard=death+Ageing[k];
                    double lost=ages[k]*(1-Math.Exp(-hazard*dt)),died=hazard>0?lost*death/hazard:0;
                    next[k]+=ages[k]-lost;deaths+=died;if(k<4)next[k+1]+=lost-died;
                }
                double born=(ages[2]+ages[3]*.18)*c.BirthRate*dt;
                double newborns=born*Math.Exp(-(mortality[0]+shock)*dt*.5);next[0]+=newborns;births+=born;deaths+=born-newborns;
                var previous=ages;ages=next;next=previous;at+=dt;remaining-=dt;
            }
            return new ForagerDemographicProjection(ages,births,deaths);
        }
        public static void Materialize(ForagerCohort c,double years,double absoluteYear)
        {
            var p=Project(c,years,absoluteYear);for(int i=0;i<5;i++)c.Ages[i]=D.Q(Math.Max(0,p.Ages[i]),1000000000);
            Sync(c);c.Births=D.Q(c.Births+p.Births,1000000);c.Deaths=D.Q(c.Deaths+p.Deaths,1000000);
            if(c.Population<.5)
            {
                c.Deaths+=c.Population;Array.Clear(c.Ages,0,5);Sync(c);
                // Keep ancestry, migration/vital totals, affiliations and the
                // last toolkit/history. A dead community has no future incoming
                // teachers or invention hazards to cache in every later save.
                c.Teaching=c.Innovation=c.TeachingFrom=Array.Empty<double>();c.ContactTeaching=c.TeachingAdults=c.MatingContacts=0;
            }
        }
        public static void MovePopulation(ForagerCohort from,ForagerCohort to,double number)
        {
            double fraction=Math.Min(1,Math.Max(0,number)/Math.Max(.000001,from.Population));double moved=0;
            for(int k=0;k<5;k++){double n=from.Ages[k]*fraction;from.Ages[k]-=n;to.Ages[k]+=n;moved+=n;}
            from.Emigration+=moved;to.Immigration+=moved;Sync(from);Sync(to);
        }
    }
}
