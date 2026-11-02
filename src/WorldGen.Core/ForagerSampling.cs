using System;
using System.Collections.Generic;
using System.Linq;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        // These are demographic candidates, not authoritative spawned NPCs.
        // A host retains people/camp edits once instantiated. Identifiers use
        // ancestry and birth cohorts, never request order, date or frame RNG.
        public int ForagerBandCount(PrehistoryState s,ForagerCohort c)
        {
            double years=(s.Foragers.Seconds-s.Foragers.BoundarySeconds)/(double)SimulationTime.Year;
            double population=years>0?ForagerDemography.Project(c,years,s.Foragers.BoundarySeconds/(double)SimulationTime.Year).Population:c.Population;
            return population<12?0:Math.Max(1,(int)Math.Round(population/(24+12*Rng.Value(World.Recipe.Seed,"resident-band-size",c.Id))));
        }
        static long ForagerBandId(int cohort,int ordinal)=>((long)(cohort+1)<<32)|(uint)(ordinal+1);
        static long PersonId(long band,int household,int birthYear,int role)
        {
            uint a=Rng.Mix((uint)band^unchecked((uint)birthYear*104729u)^(uint)household*193u^(uint)role*7919u);
            uint b=Rng.Mix((uint)(band>>32)^a^0x6b7143u);return ((long)(b&0x7fffffffu)<<32)|a;
        }
        V ForagerOffset(V center,double km,double bearing)
        {
            V east=V.Cross(new V(0,0,1),center).Unit;if(east.Length<.1)east=new V(0,1,0);
            V north=V.Cross(center,east).Unit;double angle=km/World.Recipe.RadiusKm;
            return (center*Math.Cos(angle)+(east*Math.Cos(bearing)+north*Math.Sin(bearing))*Math.Sin(angle)).Unit;
        }
        (V position,int cell) ForagerBandHome(ForagerState f,ForagerCohort c,int ordinal)
        {
            var d=f.Districts[c.District];var cells=World.Surface.Cells;int key=unchecked(c.Id*104729+ordinal*7919);
            // A fixed spiral of potential homes expands from the entry place.
            // New ordinal bands reach farther; changing population/frontier
            // never reindexes an existing band's home onto another source cell.
            double spacing=6+20/(1+d.Productivity/150d),bearing=ordinal*2.399963229728653+Rng.Value(World.Recipe.Seed,"community-home-bearing",c.Id)*Math.PI*2;
            V proposed=ForagerOffset(cells[c.HomeCell].Position,spacing*Math.Sqrt(ordinal),bearing);
            int cell=d.Cells.OrderByDescending(i=>V.Dot(proposed,cells[i].Position)).First();
            double clearance=cells[cell].DistanceM.Length>0?cells[cell].DistanceM.Min()/1000.0*.35:5;
            double radius=Math.Min(clearance,Math.Max(1,Math.Sqrt(cells[cell].AreaM2/1e6/Math.PI)*.7));
            double distance=Math.Sqrt(Rng.Value(World.Recipe.Seed,"band-home-radius",key))*radius;
            double angle=Rng.Value(World.Recipe.Seed,"band-home-bearing",key)*Math.PI*2;
            return(ForagerOffset(cells[cell].Position,distance,angle),cell);
        }
        public ForagerBandSample ForagerBand(PrehistoryState s,int cohortId,int ordinal=0)
        {
            if(!Foraging(s)||ordinal<0)return null;var f=s.Foragers;var c=f.Cohorts.FirstOrDefault(x=>x.Id==cohortId);
            if(c==null)return null;int count=ForagerBandCount(s,c);if(ordinal>=count)return null;
            var d=f.Districts[c.District];var culture=f.Cultures[c.Culture];double years=f.Seconds/(double)SimulationTime.Year;
            int key=unchecked(c.Id*104729+ordinal*7919);long id=ForagerBandId(c.Id,ordinal);var home=ForagerBandHome(f,c,ordinal);
            double population=ForagerDemography.Project(c,(f.Seconds-f.BoundarySeconds)/(double)SimulationTime.Year,f.BoundarySeconds/(double)SimulationTime.Year).Population;
            int people=Math.Max(12,(int)Math.Round(population/count));
            double range=Math.Sqrt(Math.Max(20,d.AreaKm2*c.Occupation/count)/Math.PI);
            var band=new ForagerBandSample{Id=id,Cohort=c.Id,District=d.Id,Lineage=c.Lineage,Culture=c.Culture,Language=c.Language,Belief=c.Belief,Home=home.position,People=people,
                Name=culture.Name+" · "+LanguageGenerator.WordWithRootSeed(f.Languages[c.Language].Sounds,"home",key).Text,
                HomeRangeKm=range,MoveFrequencyPerYear=culture.Residence==ForagerResidence.AquaticResidence?2:culture.Residence==ForagerResidence.CollectorCamps?4:5+5*d.Seasonality[0],
                Aggregation=D.Clamp(.2+.35*(d.Coast+d.River)+.25*c.Connectivity),FoodSecurity=c.FoodSecurity,Hospitality=culture.GuestAccess,Residence=culture.Residence,Techniques=new bool[ForagerTechnology.Count]};
            if(c.MinorityLanguage>=0&&Rng.Value(World.Recipe.Seed,"band-minority-language",key)<c.MinorityLanguageShare)band.Language=c.MinorityLanguage;
            if(c.MinorityBelief>=0&&Rng.Value(World.Recipe.Seed,"band-minority-belief",key)<c.MinorityBeliefShare)band.Belief=c.MinorityBelief;
            // Technique-specific, spatially correlated prevalence. One rank for
            // all tools incorrectly makes every skilled band know the same set.
            for(int k=0;k<band.Techniques.Length;k++)band.Techniques[k]=Rng.Value(World.Recipe.Seed,"band-technique-"+k,key)<c.Skill[k];
            bool[] visited=new bool[band.Techniques.Length];bool Can(int k){if(visited[k])return band.Techniques[k];visited[k]=true;var t=ForagerTechnology.All[k];return band.Techniques[k]=band.Techniques[k]&&t.Prerequisites.All(Can)&&(t.AlternativePaths.Length==0||t.AlternativePaths.Any(path=>path.All(Can)));}
            for(int k=0;k<band.Techniques.Length;k++)Can(k);
            band.SeasonalCamps=ForagerSeasonalCamps(f,c,band,home.cell,key);
            double season=(years%1+1)%1*4;int current=(int)season,next=(current+1)%4;
            // Most time is spent at camp; movement takes actual travel days.
            // Crossing a season boundary interpolates the final days of travel.
            double travel=Math.Min(.12,DistanceKm(band.SeasonalCamps[current].Position,band.SeasonalCamps[next].Position)/18/91.25);
            double fraction=season-current,t=travel>0?D.Clamp((fraction-(1-travel))/travel):0;
            band.Position=(band.SeasonalCamps[current].Position*(1-t)+band.SeasonalCamps[next].Position*t).Unit;
            band.Members=ForagerHouseholds(f,c,band,years,key);
            band.ContactBands=f.Exchanges.Where(e=>e.A==c.Id||e.B==c.Id).Where(e=>e.Strength>.005).OrderByDescending(e=>e.Strength).Take(8).Select(e=>e.A==c.Id?e.B:e.A).Select(other=>f.Cohorts.First(x=>x.Id==other)).Where(other=>ForagerBandCount(s,other)>0).Select(other=>ForagerBandId(other.Id,(int)(Rng.Value(World.Recipe.Seed,"band-marriage-contact",key^other.Id)*ForagerBandCount(s,other)))).ToArray();
            return band;
        }
        ForagerCampSample[] ForagerSeasonalCamps(ForagerState f,ForagerCohort c,ForagerBandSample band,int homeCell,int key)
        {
            var d=f.Districts[c.District];var result=new ForagerCampSample[4];
            for(int season=0;season<4;season++)
            {
                int best=homeCell;double bestScore=double.NegativeInfinity;
                foreach(int cell in d.Cells)
                {
                    var h=World.Ecology.Habitat[cell];double distance=DistanceKm(band.Home,World.Surface.Cells[cell].Position);
                    if(distance>Math.Max(10,band.HomeRangeKm*1.4))continue;
                    double water=D.Clamp(h.AridityMilli/1000.0)+HistoricalRiverAccess(cell)*3;
                    double shelter=AnimalEcotypes.Canopy(h)/1000.0-h.ReliefM/3000.0;
                    double score=.6*water+(season==3?.5*shelter:.25*h.ProductivityGCM2Year/800.0)-distance/Math.Max(10,band.HomeRangeKm)+.25*Rng.Value(World.Recipe.Seed,"camp-selection-"+season,key^cell);
                    if(score>bestScore){bestScore=score;best=cell;}
                }
                var ground=World.Surface.Cells[best];double limit=ground.DistanceM.Length==0?2:ground.DistanceM.Min()/1000.0*.3;
                double radius=Math.Min(Math.Min(8,band.HomeRangeKm*.35),limit);
                V center=best==homeCell?band.Home:ground.Position;
                V position=ForagerOffset(center,radius*(.2+.8*Rng.Value(World.Recipe.Seed,"camp-radius-"+season,key)),Rng.Value(World.Recipe.Seed,"camp-bearing-"+season,key)*Math.PI*2);
                var environment=ForagerEcology.At(World.Recipe.Seed,d,f.Seconds/(double)SimulationTime.Year,World.ElevationM[best]);
                result[season]=new ForagerCampSample{Id=PersonId(band.Id,season,0,47),Position=position,Cell=best,Season=season,
                    Purpose=season==3?"Sheltered lean-season camp":season==1&&d.River+d.Coast>.6?"Seasonal fishing and gathering camp":season==2?"Harvest and social gathering camp":"Hunting and gathering camp",
                    DurationDays=365.0/Math.Max(2,band.MoveFrequencyPerYear),WaterAccess=environment.Water,Exposure=D.Clamp(d.Relief/1800.0+.4*(1-environment.Canopy)),FoodOpportunity=environment.Food.Sum()/5};
            }
            return result;
        }
        ForagerPersonSample[] ForagerHouseholds(ForagerState f,ForagerCohort c,ForagerBandSample band,double years,int key)
        {
            var candidates=new List<ForagerPersonSample>();int now=(int)Math.Floor(years);int familyCount=Math.Max(2,(int)Math.Ceiling(band.People/5.0));
            for(int family=0;family<familyCount;family++)
            {
                int phase=(int)(Rng.Value(World.Recipe.Seed,"household-generation-phase",key^family*7919)*28);
                for(int generation=(now-phase-90)/28-1;generation<=(now-phase)/28;generation++)
                {
                    int motherBirth=generation*28+phase;long household=PersonId(band.Id,family,motherBirth,19);
                    int fatherBirth=motherBirth-2-(int)(Rng.Value(World.Recipe.Seed,"partner-age",key^family^motherBirth)*5);
                    long mother=PersonId(band.Id,family,motherBirth,0),father=PersonId(band.Id,family,fatherBirth,1);
                    var a=Person(mother,motherBirth,true,household,-1,-1);var b=Person(father,fatherBirth,false,household,-1,-1);
                    if(a!=null){a.Partner=father;candidates.Add(a);}if(b!=null){b.Partner=mother;candidates.Add(b);}
                    int children=2+(int)(Rng.Value(World.Recipe.Seed,"household-births",key^family^motherBirth)*4);
                    for(int child=0;child<children;child++)
                    {
                        int birth=motherBirth+19+child*3;long id=PersonId(band.Id,family,birth,child+4);
                        var person=Person(id,birth,Rng.Value(World.Recipe.Seed,"child-sex",(int)id)>.5,household,mother,father);if(person!=null)candidates.Add(person);
                    }
                }
            }
            var selected=candidates.OrderBy(p=>Rng.Value(World.Recipe.Seed,"household-resident-rank",unchecked((int)p.Id^(int)(p.Id>>32)))).Take(band.People).OrderBy(p=>p.Household).ThenByDescending(p=>p.AgeYears).ToArray();
            // Parent and partner references may refer to deceased people or to
            // relatives living in neighbouring bands; they are not dangling
            // indexes into this display's member array.
            return selected;
            ForagerPersonSample Person(long id,int birth,bool female,long household,long mother,long father)
            {
                double birthday=Rng.Value(World.Recipe.Seed,"individual-birthday",unchecked((int)id));double age=years-birth-birthday;
                if(age<0||age>95)return null;double draw=Rng.Value(World.Recipe.Seed,"individual-lifetime",unchecked((int)id^(int)(id>>32)));
                double survival=age<5?Math.Exp(-.085*age):age<15?.654*Math.Exp(-.012*(age-5)):age<50?.58*Math.Exp(-.011*(age-15)):.395*Math.Exp(-.035*(Math.Exp((age-50)/8)-1));
                if(draw>survival)return null;
                var person=new ForagerPersonSample{Id=id,Household=household,Mother=mother,Father=father,Female=female,AgeYears=age,
                    Name=LanguageGenerator.WordWithRootSeed(f.Languages[band.Language].Sounds,"person",unchecked((int)id)).Text,
                    Role=age<5?"Young child":age<13?"Child and learner":age<19?"Apprentice forager":age>58?"Experienced elder":"Adult forager",Appearance=new double[8],Skills=new double[ForagerTechnology.Count]};
                for(int k=0;k<8;k++)person.Appearance[k]=D.Clamp(c.Appearance[k]+(Rng.Value(World.Recipe.Seed,"household-appearance-"+k,(int)household)-.5)*.12+(Rng.Value(World.Recipe.Seed,"individual-appearance-"+k,(int)id)-.5)*.08);
                for(int k=0;k<person.Skills.Length;k++)person.Skills[k]=band.Techniques[k]?D.Clamp(c.Skill[k]*D.Clamp((age-5)/18)*(.7+.6*Rng.Value(World.Recipe.Seed,"individual-skill-"+k,(int)id))):0;
                return person;
            }
        }
        public ForagerBandSample[] ForagerBandsNear(PrehistoryState s,V position,double radiusKm,int maximum=32)
        {
            if(!Foraging(s)||radiusKm<0||maximum<1)return Array.Empty<ForagerBandSample>();var result=new List<(int cohort,int ordinal,double distance)>();
            foreach(var c in s.Foragers.Cohorts.Where(x=>x.Population>=12))
            {
                var d=s.Foragers.Districts[c.District];if(DistanceKm(position,d.Center)>radiusKm+d.RadiusKm*2+30)continue;
                int count=ForagerBandCount(s,c);
                for(int ordinal=0;ordinal<count;ordinal++)
                {var home=ForagerBandHome(s.Foragers,c,ordinal);double distance=DistanceKm(position,home.position);if(distance<=radiusKm+Math.Max(10,d.RadiusKm/Math.Sqrt(Math.Max(1,count))))result.Add((c.Id,ordinal,distance));}
            }
            return result.OrderBy(x=>x.distance).Take(maximum*2).Select(x=>ForagerBand(s,x.cohort,x.ordinal)).Where(b=>b!=null&&DistanceKm(position,b.Position)<=radiusKm).Take(maximum).ToArray();
        }
    }
}
