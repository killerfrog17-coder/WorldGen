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
        {if(c.Population<=0)return 0;long at=state.PopulationAt[c.District];double elapsed=(state.Seconds-at)/(double)SimulationTime.Year;if(elapsed<=0)return c.Population;Span<double> ages=stackalloc double[5];RegionalProject(c,elapsed,at/(double)SimulationTime.Year,ages,out _,out _);return ages[0]+ages[1]+ages[2]+ages[3]+ages[4];}
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
            var ages=new double[5];Integrate(c,years,absoluteYear,ages,out double births,out double deaths);
            return new ForagerDemographicProjection(ages,births,deaths);
        }
        static void Integrate(ForagerCohort c,double years,double absoluteYear,Span<double> result,out double births,out double deaths)
            =>RegionalProject(c,years,absoluteYear,result,out births,out deaths);
        static void RegionalProject(ForagerCohort c,double years,double absoluteYear,Span<double> result,out double births,out double deaths)
        {
            if(years<0||!double.IsFinite(years))throw new ArgumentOutOfRangeException(nameof(years));
            double population=c.Population;
            if(population<=0){result.Clear();births=deaths=0;return;}
            double mortality=(c.Ages[0]*c.ChildMortality*1.55+c.Ages[1]*c.ChildMortality*.65+c.Ages[2]*c.AdultMortality+c.Ages[3]*c.AdultMortality*1.35+c.Ages[4]*c.ElderMortality)/population;
            double start=absoluteYear*SimulationTime.Year,end=(absoluteYear+years)*SimulationTime.Year;
            double shock=years>0?Math.Max(0,Math.Min(end,c.ShockEnd)-Math.Max(start,c.ShockStart))/(SimulationTime.Year*years)*c.ShockSeverity:0;
            double growth=D.Clamp(.04-mortality-shock,-.5,.025),capacity=Math.Max(.000001,c.Capacity);
            double next=growth>0?capacity/(1+(capacity/population-1)*Math.Exp(-growth*years)):population*Math.Exp(growth*years);
            // A lost habitat has no logistic refuge. Retain the food/cold
            // mortality computed by Rates rather than replacing it with a
            // gentler twelve-percent decline that lets an icebound remnant
            // persist for decades. This remains an analytic regional query.
            if(c.Capacity<1)next=population*Math.Exp(-Math.Max(.12,mortality+shock)*years);
            next=Math.Max(0,next);double mean=(population+next)*.5;
            double bornRate=(c.Ages[2]+c.Ages[3]*.18)/population*c.BirthRate;
            births=Math.Max(Math.Max(0,next-population),mean*bornRate*years);deaths=Math.Max(0,population+births-next);
            for(int k=0;k<5;k++)result[k]=next*c.Ages[k]/population;
        }
        public static void Materialize(ForagerCohort c,double years,double absoluteYear)
        {
            Integrate(c,years,absoluteYear,c.Ages,out double births,out double deaths);for(int i=0;i<5;i++)c.Ages[i]=D.Q(Math.Max(0,c.Ages[i]),1000000000);
            Sync(c);c.Births=D.Q(c.Births+births,1000000);c.Deaths=D.Q(c.Deaths+deaths,1000000);
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
