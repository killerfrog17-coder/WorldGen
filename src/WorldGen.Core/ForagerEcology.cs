using System;
namespace WorldGen.Core
{
    public readonly struct ForagerEnvironment
    {
        public readonly double Years,WinterC,SummerC,MeanC,IceCover,Moisture,Productivity,Canopy,Water,Pathogens,Drought,Disturbance,Aridity,RainMm;
        public readonly double[] Food,LeanSeason;
        public ForagerEnvironment(double years,double winter,double summer,double mean,double ice,double moisture,double productivity,double canopy,double water,double pathogens,double drought,double disturbance,double[] food,double[] lean,double aridity=1,double rainMm=0)
        {Years=years;WinterC=winter;SummerC=summer;MeanC=mean;IceCover=ice;Moisture=moisture;Productivity=productivity;Canopy=canopy;Water=water;Pathogens=pathogens;Drought=drought;Disturbance=disturbance;Food=food;LeanSeason=lean;Aridity=aridity;RainMm=rainMm;}
    }
    public readonly struct ForagerLivelihood
    {
        public readonly double Capacity,WinterSecurity,FoodSecurity,Health,AnnualReturn;
        public readonly double[] Diet;
        public ForagerLivelihood(double capacity,double winter,double security,double health,double annual,double[] diet)
        {Capacity=capacity;WinterSecurity=winter;FoodSecurity=security;Health=health;AnnualReturn=annual;Diet=diet;}
    }
    public static class ForagerEcology
    {
        public static double ResourceClaim(double population,double dietShare,bool legacyConcave=false)
        {
            double claim=Math.Max(0,population)*Math.Max(0,dietShare);
            // Resource access cannot improve merely by splitting an identical
            // population into more records. Old checkpoints retain their rules.
            return legacyConcave?Math.Pow(claim,.75):claim;
        }
        public static double Smooth(int seed,string channel,int location,double years,double period)
        {
            int epoch=(int)Math.Floor(years/period);double t=years/period-epoch;t=t*t*(3-2*t);
            double a=Rng.Value(seed,channel,unchecked(location*7919+epoch)),b=Rng.Value(seed,channel,unchecked(location*7919+epoch+1));return a+(b-a)*t;
        }
        public static ForagerEnvironment At(int seed,ForagerDistrict d,double years,int elevationM=0)
        {
            var glacial=PaleolithicRules.Climate(seed,years,d.Center.Z,elevationM,d.Province);
            double province=Smooth(seed,"forager-regional-rain",d.Province,years,1450),local=Smooth(seed,"forager-multidecade-rain",d.Id,years,160);
            double severe=Smooth(seed,"forager-refugial-drought",d.Province,years,420);
            double drought=D.Clamp((.18-severe)/.18)*D.Clamp((1.2-d.Aridity)/1.2);
            double moisture=D.Clamp(glacial.PrecipitationMultiplier*(.72+.5*province)*(.82+.36*local)*(1-.75*drought),.12,1.6);
            double pulse=MonsoonRain(d,years,glacial.Ice)*(1-.6*drought),greening=D.Clamp((pulse-10)/50);
            double rain=d.PetMm>0?Math.Max(0,d.AnnualRainMm*moisture+pulse):0;
            double aridity=d.PetMm>0?rain/(d.PetMm*D.Clamp(1+.025*glacial.TemperatureDeltaC,.65,1.3)):d.Aridity*moisture;
            double winter=d.WinterC+glacial.TemperatureDeltaC,summer=d.SummerC+glacial.TemperatureDeltaC,mean=d.MeanC+glacial.TemperatureDeltaC;
            double latitude=Math.Abs(d.Center.Z),snow=D.Clamp((6-summer)/8)*D.Clamp((latitude-.43)/.3);
            double mountain=D.Clamp((elevationM-(3300-1700*glacial.Ice))/1500)*D.Clamp((10-summer)/15);
            double ice=D.Clamp(Math.Max(snow,mountain)*(.4+.6*glacial.Ice));
            double growth=D.Clamp((summer+2)/20)*D.Clamp((mean+16)/24);
            double npp=d.Productivity*growth*Math.Pow(moisture,.7)*(1-.9*ice);
            npp+=Math.Max(0,900*(1-Math.Exp(-rain/900))*growth*(1-.9*ice)-npp)*greening;
            double trees=D.Clamp((winter+30)/25)*D.Clamp((summer-5)/14)*D.Clamp((aridity-.25)/.9);
            double forestPotential=Math.Max(d.Canopy,.7*D.Clamp((npp-250)/700)*D.Clamp((aridity-.3)/.9));
            double canopy=forestPotential*trees,water=D.Clamp(d.Water*moisture+.2*d.River*(1-.5*drought));
            water+=Math.Max(0,D.Clamp((rain-180)/700)*.65-water)*greening;
            double pathogens=d.Pathogens*D.Clamp((mean-6)/18)*D.Clamp(moisture/.8);
            double disturbance=.7+.6*Smooth(seed,"forager-resource-mosaic",d.Id,years,650);
            var food=new double[5];var lean=new double[5];
            double change=d.Productivity>1?D.Clamp(npp/d.Productivity,0,1.7):0,open=D.Clamp(d.Canopy-canopy);
            for(int k=0;k<5;k++)
            {
                double climate=k<3?change:k==3?D.Clamp(.4+.6*moisture,0,1.3)*(1-.85*ice):1-.8*ice;
                double habitat=k==2?1+.7*open:k==0?.6+.4*D.Clamp((summer+5)/25):1;
                food[k]=Math.Max(0,d.Food[k]*climate*habitat*disturbance);
                lean[k]=D.Clamp(1-d.Seasonality[k]*(.45+.55*D.Clamp((15-winter)/35)),.04,1);
            }
            if(greening>0)
            {
                double green=D.Clamp(npp/850d),baseGreen=D.Clamp(d.Productivity/850d),days=D.Clamp((d.GrowingDays+glacial.TemperatureDeltaC*5)/240d);
                food[0]+=Math.Max(0,(.06+.9*green)*D.Clamp(aridity/.7)*(.4+.6*days)*disturbance-food[0])*greening;
                food[1]+=Math.Max(0,d.Food[1]*(.06+.5*green)/(.06+.5*baseGreen)*disturbance-food[1])*greening;
                food[2]+=Math.Max(0,d.Food[2]*(.08+.6*green)/(.08+.6*baseGreen)*(1+.7*open)*disturbance-food[2])*greening;
                lean[0]*=1-.3*D.Clamp(pulse/Math.Max(1,rain))*greening;
            }
            return new ForagerEnvironment(years,winter,summer,mean,ice,moisture,npp,canopy,water,pathogens,drought,disturbance,food,lean,aridity,rain);
        }
        public static double MonsoonRain(ForagerDistrict d,double years,double ice)
        {
            if(d.OceanMoistureAccess<=0)return 0;
            double latitude=Math.Abs(Math.Asin(d.Center.Z)*180/Math.PI),age=200000-years;
            double insolation=.5+.5*Math.Cos((age-8000)/23000*Math.PI*2+(d.Center.Z<0?Math.PI:0));
            double summer=D.Clamp((insolation-.35)/.65),belt=10+13*insolation;
            double monsoon=900*summer*Math.Exp(-Math.Pow((latitude-belt)/9,2))*(1-.65*ice);
            double winterStorms=220*insolation*Math.Exp(-Math.Pow((latitude-31)/6,2))*(1-.5*ice);
            return (monsoon+winterStorms)*d.OceanMoistureAccess;
        }
        public static double Sigmoid(double x)=>x>35?1:x< -35?0:1/(1+Math.Exp(-x));
        // Returns are person-support equivalents per km², not raw plant biomass.
        // Numerical coefficients are comparative game priors; the mechanisms
        // (winter bottleneck, pathogens, storage, toolkit fit) have separate tests.
        public static ForagerLivelihood Livelihood(ForagerDistrict d,ForagerEnvironment e,HomininLineage lineage,ForagerCohort c,ForagerCulture culture,ForagerResources resources,bool sharedProducts=false)
        {
            double Use(int k)=>k<c.Use.Length?D.Clamp(c.Use[k]):0;
            double flake=Use(0),haft=Use(4),fire=Use(5),hide=Use(8),fitted=Use(13),insulated=Use(14),bow=Use(17),barbs=Use(18),line=Use(19),atlatl=Use(22),preserve=Use(23);
            double nets=Use(24),snares=Use(25),grinding=Use(26),plants=Use(27),baskets=Use(28),containers=Use(29),boats=Use(30),shelter=Use(31),storage=Use(32);
            double handtools=Use(33),frames=Use(34),coverings=Math.Max(Use(35),Use(36)),hooks=Use(37),axes=Use(38),pots=Use(39),slings=Use(40);
            containers=D.Clamp(containers+.35*pots);plants=D.Clamp(plants+.08*handtools+.1*pots);
            double[] returns={.038*(.35+.2*flake+.35*plants+.25*grinding+.1*baskets+.06*axes),.014*(.2+.45*snares+.3*bow+.1*haft+.15*slings),.034*(.2+.3*haft+.25*atlatl+.12*bow+.1*Use(3)+.06*handtools),.075*(.08+.3*line+.5*nets+.2*barbs+.18*hooks),.13*(.18+.22*line+.3*nets+.45*boats+.1*hooks)};
            var diet=new double[5];double annual=0,lean=0;
            for(int k=0;k<5;k++)
            {
                double stock=resources?.Stock[k]??1,preference=culture?.FoodPreference.Length>k?culture.FoodPreference[k]:.5;
                diet[k]=e.Food[k]*returns[k]*stock*(.65+.7*preference);annual+=diet[k];
                double stored=D.Clamp(.28*preserve+.27*storage+.08*baskets);lean+=diet[k]*D.Clamp(e.LeanSeason[k]+stored,0,1);
            }
            // Annual routine use is not the fraction of protection people wear
            // in winter. A trained community can prepare its known winter gear
            // before entering a colder district. Material access still matters;
            // no knowledge/skill means no hypothetical future protection.
            double WinterPractice(int k)=>ForagerTechnology.WinterPractice(d,e,c,k,sharedProducts);
            double winterCover=Math.Max(WinterPractice(35),WinterPractice(36));
            double protection=7*WinterPractice(5)+12*WinterPractice(8)+13*WinterPractice(13)+16*WinterPractice(14)+9*WinterPractice(31)+3*WinterPractice(34)*(.2+.8*winterCover)+2*(lineage.Compactness-.5);
            double winter=Sigmoid((e.WinterC+protection+4)/3.5);
            double summer=1-D.Clamp((e.SummerC-34)/13)*(1-D.Clamp(e.Water+.3*containers+.15*e.Canopy));
            double water=D.Clamp((e.Water+.15*containers)/.33);
            double terrain=.55+.45*Math.Exp(-d.Relief/1400.0),disease=1-.55*e.Pathogens;
            double seasonal=D.Clamp(lean/Math.Max(.00001,annual),.03,1);
            double effective=annual*(.25+.75*seasonal)*winter*summer*water*terrain*disease*(1-e.IceCover);
            // Body size changes an individual's energy needs, strength and
            // foraging ability together. Applying only its metabolic penalty
            // here made the lightest lineage inevitably replace every other
            // human over deep time. Regional efficiency follows the actual
            // toolkit, diet, climate and resource access instead.
            double capacity=d.AreaKm2*effective;
            double population=c.Population,security=population>0?D.Clamp(capacity/Math.Max(1,population),0,1.5):1;
            for(int k=0;k<5;k++)diet[k]=annual>0?diet[k]/annual:0;
            double health=D.Clamp((.85-.3*e.Pathogens)*(.25+.75*D.Clamp(security))*(.5+.5*winter));
            return new ForagerLivelihood(capacity,winter,security,health,annual,diet);
        }
        public static void RecoverResources(ForagerResources r,double years,double population,double capacity,double[] diet)
        {
            double pressure=population/Math.Max(1,capacity);double[] recovery={.9,.5,.055,.28,.12};
            for(int k=0;k<5;k++)
            {
                double demand=Math.Max(0,pressure-.45)*diet[k];double target=D.Clamp(1-demand*(k==2?.8:.45),.12,1);
                r.Stock[k]=D.Q(PaleolithicRules.Approach(r.Stock[k],target,recovery[k],years),1000000000);
            }
            r.HarvestPressure=D.Q(pressure,1000000);
        }
    }
}
