using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        // Query caches belong to an immutable published ledger. A new gameplay
        // snapshot gets its own index; no index is serialized or hashed. Avoid
        // scanning every retired population on each local NPC/atlas query.
        sealed class ForagerResidentIndex
        {
            public readonly ForagerCohort[][] Districts;
            public readonly Dictionary<int,ForagerExchange[]> Contacts;
            public ForagerResidentIndex(ForagerState f)
            {
                var lists=new List<ForagerCohort>[f.Districts.Length];
                foreach(var c in f.Cohorts)if(c.Population>=1)
                {var list=lists[c.District]??(lists[c.District]=new List<ForagerCohort>());list.Add(c);}
                Districts=lists.Select(list=>list?.ToArray()??Array.Empty<ForagerCohort>()).ToArray();
                var contacts=new Dictionary<int,List<ForagerExchange>>();
                void Add(int id,ForagerExchange exchange)
                {if(!contacts.TryGetValue(id,out var list))contacts[id]=list=new List<ForagerExchange>();list.Add(exchange);}
                foreach(var exchange in f.Exchanges){Add(exchange.A,exchange);Add(exchange.B,exchange);}
                Contacts=contacts.ToDictionary(p=>p.Key,p=>p.Value.ToArray());
            }
        }
        static readonly ConditionalWeakTable<ForagerState,ForagerResidentIndex> ForagerResidents=new ConditionalWeakTable<ForagerState,ForagerResidentIndex>();
        static ForagerCohort ForagerResident(ForagerState f,int id)
        {
            if(id>=0&&id<f.Cohorts.Length&&f.Cohorts[id].Id==id)return f.Cohorts[id];
            return f.Cohorts.FirstOrDefault(c=>c.Id==id);
        }
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
            int cell=d.Cells[0];double bestDot=V.Dot(proposed,cells[cell].Position);
            for(int index=1;index<d.Cells.Length;index++){int candidate=d.Cells[index];double dot=V.Dot(proposed,cells[candidate].Position);if(dot>bestDot){bestDot=dot;cell=candidate;}}
            double clearance=cells[cell].DistanceM.Length>0?cells[cell].DistanceM.Min()/1000.0*.35:5;
            double radius=Math.Min(clearance,Math.Max(1,Math.Sqrt(cells[cell].AreaM2/1e6/Math.PI)*.7));
            double distance=Math.Sqrt(Rng.Value(World.Recipe.Seed,"band-home-radius",key))*radius;
            double angle=Rng.Value(World.Recipe.Seed,"band-home-bearing",key)*Math.PI*2;
            return(ForagerOffset(cells[cell].Position,distance,angle),cell);
        }
        public ForagerBandSample ForagerBand(PrehistoryState s,int cohortId,int ordinal=0)
        {
            if(!Foraging(s)||ordinal<0)return null;var f=s.Foragers;var c=ForagerResident(f,cohortId);
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
            band.Language=ForagerAffiliations.Pick(c.Speech,Rng.Value(World.Recipe.Seed,"band-minority-language",key));
            band.Belief=ForagerAffiliations.Pick(c.Traditions,Rng.Value(World.Recipe.Seed,"band-minority-belief",key));
            // Technique-specific, spatially correlated prevalence. One rank for
            // all tools incorrectly makes every skilled band know the same set.
            for(int k=0;k<band.Techniques.Length;k++)band.Techniques[k]=Rng.Value(World.Recipe.Seed,"band-technique-"+k,key)<c.Skill[k];
            bool[] visited=new bool[band.Techniques.Length];bool Can(int k){if(visited[k])return band.Techniques[k];visited[k]=true;var t=ForagerTechnology.All[k];return band.Techniques[k]=band.Techniques[k]&&t.Prerequisites.All(Can)&&(t.AlternativePaths.Length==0||t.AlternativePaths.Any(path=>path.All(Can)));}
            for(int k=0;k<band.Techniques.Length;k++)Can(k);
            var environment=ForagerEcology.At(World.Recipe.Seed,d,years,World.ElevationM[home.cell]);
            band.RoutineTechniques=new bool[ForagerTechnology.Count];band.WinterTechniques=new bool[ForagerTechnology.Count];
            for(int k=0;k<band.Techniques.Length;k++)
            {
                // Conditional sampling keeps the population fraction at Use,
                // rather than erroneously multiplying Use by Skill a second time.
                double rank=Rng.Value(World.Recipe.Seed,"band-practice-"+k,key),skill=Math.Max(.000000001,c.Skill[k]);
                band.RoutineTechniques[k]=band.Techniques[k]&&rank<D.Clamp(c.Use[k]/skill);
                bool winter=k==5||k==8||k==13||k==14||k==31||k==34||k==35||k==36;
                band.WinterTechniques[k]=winter?band.Techniques[k]&&rank<D.Clamp(ForagerTechnology.WinterPractice(d,environment,c,k)/skill):band.RoutineTechniques[k];
            }
            band.SeasonalCamps=ForagerSeasonalCamps(s,c,band,home.cell,key);
            int camps=band.SeasonalCamps.Length;double season=((years-59/365d+Rng.Value(World.Recipe.Seed,"camp-calendar-phase",c.Id)*.08)%1+1)%1*camps;int current=(int)season,next=(current+1)%camps;
            // Most time is spent at camp; movement takes actual travel days.
            // Crossing a season boundary interpolates the final days of travel.
            double travel=Math.Min(.8,DistanceKm(band.SeasonalCamps[current].Position,band.SeasonalCamps[next].Position)/18/(365d/camps));
            double fraction=season-current,t=travel>0?D.Clamp((fraction-(1-travel))/travel):0;
            band.Position=(band.SeasonalCamps[current].Position*(1-t)+band.SeasonalCamps[next].Position*t).Unit;
            band.Members=ForagerHouseholds(f,c,band,years,key);
            var residents=ForagerResidents.GetValue(f,value=>new ForagerResidentIndex(value));
            var contacts=residents.Contacts.TryGetValue(c.Id,out var nearby)?nearby:Array.Empty<ForagerExchange>();
            band.ContactBands=contacts.Where(e=>e.Strength>.005).OrderByDescending(e=>e.Strength).Take(8).Select(e=>e.A==c.Id?e.B:e.A).Select(other=>ForagerResident(f,other)).Where(other=>ForagerBandCount(s,other)>0).Select(other=>ForagerBandId(other.Id,(int)(Rng.Value(World.Recipe.Seed,"band-marriage-contact",key^other.Id)*ForagerBandCount(s,other)))).ToArray();
            return band;
        }
        ForagerCampSample[] ForagerSeasonalCamps(PrehistoryState state,ForagerCohort c,ForagerBandSample band,int homeCell,int key)
        {
            var f=state.Foragers;var dated=ForagerHabitat.For(World,state);
            var d=f.Districts[c.District];int count=Math.Max(2,Math.Min(12,(int)Math.Round(band.MoveFrequencyPerYear)));var result=new ForagerCampSample[count];
            for(int stop=0;stop<count;stop++)
            {
                int season=((int)Math.Floor((stop+.5)*4d/count)+(d.Center.Z<0?2:0))%4;
                int best=homeCell;double bestScore=double.NegativeInfinity;
                foreach(int cell in d.Cells)
                {
                    var local=dated?.Cell(cell);var h=local?.Habitat??World.Ecology.Habitat[cell];double distance=DistanceKm(band.Home,World.Surface.Cells[cell].Position);
                    if(local!=null&&local.IceCover>.6)continue;
                    if(distance>Math.Max(10,band.HomeRangeKm*1.4))continue;
                    double water=local?.WaterAccess??D.Clamp(h.AridityMilli/1000.0)+HistoricalRiverAccess(cell)*3;
                    double shelter=(local?.ForestCover??AnimalEcotypes.Canopy(h)/1000.0)-h.ReliefM/3000.0;
                    double score=.6*water+(season==3?.5*shelter:.25*h.ProductivityGCM2Year/800.0)-distance/Math.Max(10,band.HomeRangeKm)+.25*Rng.Value(World.Recipe.Seed,"camp-selection-"+stop,key^cell);
                    if(score>bestScore){bestScore=score;best=cell;}
                }
                var selectedHabitat=dated?.Cell(best);bool suitable=selectedHabitat==null||selectedHabitat.IceCover<=.6;
                var ground=World.Surface.Cells[best];double limit=ground.DistanceM.Length==0?2:ground.DistanceM.Min()/1000.0*.3;
                double radius=Math.Min(Math.Min(8,band.HomeRangeKm*.35),limit);
                V center=best==homeCell?band.Home:ground.Position;
                V position=ForagerOffset(center,radius*(.2+.8*Rng.Value(World.Recipe.Seed,"camp-radius-"+stop,key)),Rng.Value(World.Recipe.Seed,"camp-bearing-"+stop,key)*Math.PI*2);
                var environment=ForagerEcology.At(World.Recipe.Seed,d,f.Seconds/(double)SimulationTime.Year,World.ElevationM[best]);
                result[stop]=new ForagerCampSample{Id=PersonId(band.Id,stop,0,47),Position=position,Cell=best,Season=season,
                    Purpose=season==3?"Sheltered lean-season camp":season==1&&d.River+d.Coast>.6?"Seasonal fishing and gathering camp":season==2?"Harvest and social gathering camp":"Hunting and gathering camp",
                    DurationDays=365.0/Math.Max(2,band.MoveFrequencyPerYear),ClimateSuitable=suitable,WaterAccess=suitable?(selectedHabitat?.WaterAccess??environment.Water):0,Exposure=suitable?D.Clamp(d.Relief/1800.0+.4*(1-(selectedHabitat?.ForestCover??environment.Canopy))):1,FoodOpportunity=suitable?ForagerBiota.Apply(environment,f.Biota[d.Id]).Food.Sum()/5:0};
            }
            return result;
        }
        ForagerPersonSample[] ForagerHouseholds(ForagerState f,ForagerCohort c,ForagerBandSample band,double years,int key)
        {
            var records=new Dictionary<long,ForagerPersonRecord>();int now=(int)Math.Floor(years);int familyCount=Math.Max(2,(int)Math.Ceiling(band.People/5.0));
            ForagerPersonRecord Record(long id,int birth,bool female,long household,bool adultFounder=false,long mother=-1,long father=-1)
            {
                if(records.TryGetValue(id,out var existing))return existing;
                double born=birth+Rng.Value(World.Recipe.Seed,"individual-birthday",unchecked((int)id));
                var record=new ForagerPersonRecord{Id=id,Household=household,Female=female,Mother=mother,Father=father,BirthYear=born,DeathYear=born+ForagerLifeHistory.Lifetime(World.Recipe.Seed,id,adultFounder)};
                records[id]=record;return record;
            }
            for(int family=0;family<familyCount;family++)
            {
                int phase=(int)(Rng.Value(World.Recipe.Seed,"household-generation-phase",key^family*7919)*28);
                for(int generation=(now-phase-100)/28-1;generation<=(now-phase)/28;generation++)
                {
                    int motherBirth=generation*28+phase;long household=PersonId(band.Id,family,motherBirth,19);
                    int fatherBirth=motherBirth-2-(int)(Rng.Value(World.Recipe.Seed,"partner-age",key^family^motherBirth)*5);
                    long mother=PersonId(band.Id,family,motherBirth,0),father=PersonId(band.Id,family,fatherBirth,1);
                    var a=Record(mother,motherBirth,true,household,true);var b=Record(father,fatherBirth,false,household,true);
                    double union=a.BirthYear+18;
                    if(b.DeathYear<=union)continue;
                    if(years>=union){a.Partner=father;b.Partner=mother;}
                    int children=2+(int)(Rng.Value(World.Recipe.Seed,"household-births",key^family^motherBirth)*4);
                    for(int child=0;child<children;child++)
                    {
                        int birth=motherBirth+19+child*3;long id=PersonId(band.Id,family,birth,child+4);
                        double born=birth+Rng.Value(World.Recipe.Seed,"individual-birthday",unchecked((int)id));
                        if(born>=a.DeathYear||born-.75>=b.DeathYear)continue;
                        Record(id,birth,Rng.Value(World.Recipe.Seed,"child-sex",(int)id)>.5,household,false,mother,father);
                    }
                }
            }
            var alive=records.Values.Where(p=>p.BirthYear<=years&&p.DeathYear>years).ToArray();
            var households=alive.GroupBy(p=>p.Household).Where(g=>g.Any(p=>years-p.BirthYear>=18)).OrderBy(g=>Rng.Value(World.Recipe.Seed,"household-resident-rank",unchecked((int)g.Key^(int)(g.Key>>32)))).Take(familyCount).ToArray();
            var selected=households.SelectMany(g=>g).ToList();
            // Children whose parents have died can live with another household;
            // never leave a generated camp consisting solely of orphan children.
            if(households.Length>0)
            {
                var orphaned=alive.GroupBy(p=>p.Household).Where(g=>!g.Any(p=>years-p.BirthYear>=18));
                foreach(var group in orphaned)
                {
                    long guardian=households[(int)(Rng.Value(World.Recipe.Seed,"foster-household",(int)group.Key)*households.Length)].Key;
                    foreach(var child in group){child.Household=guardian;selected.Add(child);}
                }
            }
            var related=selected.SelectMany(p=>new[]{p.Id,p.Mother,p.Father,p.Partner}).Where(id=>id>=0).ToHashSet();
            band.FamilyRecords=records.Values.Where(p=>related.Contains(p.Id)).OrderBy(p=>p.Id).ToArray();
            return selected.OrderBy(p=>p.Household).ThenBy(p=>p.BirthYear).Select(Person).ToArray();
            ForagerPersonSample Person(ForagerPersonRecord record)
            {
                long id=record.Id;double age=years-record.BirthYear;
                var person=new ForagerPersonSample{Id=id,Household=record.Household,Mother=record.Mother,Father=record.Father,Partner=age>=18?record.Partner:-1,Female=record.Female,AgeYears=age,BirthYear=record.BirthYear,DeathYear=record.DeathYear,
                    Name=LanguageGenerator.WordWithRootSeed(f.Languages[band.Language].Sounds,"person",unchecked((int)id)).Text,
                    Role=age<5?"Young child":age<13?"Child and learner":age<19?"Apprentice forager":age>58?"Experienced elder":"Adult forager",Appearance=new double[8],Skills=new double[ForagerTechnology.Count]};
                for(int k=0;k<8;k++)person.Appearance[k]=D.Clamp(c.Appearance[k]+(Rng.Value(World.Recipe.Seed,"household-appearance-"+k,(int)record.Household)-.5)*.12+(Rng.Value(World.Recipe.Seed,"individual-appearance-"+k,(int)id)-.5)*.08);
                for(int k=0;k<person.Skills.Length;k++)person.Skills[k]=band.Techniques[k]?D.Clamp(c.Skill[k]*D.Clamp((age-5)/18)*(.7+.6*Rng.Value(World.Recipe.Seed,"individual-skill-"+k,(int)id))):0;
                return person;
            }
        }

        public ForagerBandSample[] ForagerBandsNear(PrehistoryState s,V position,double radiusKm,int maximum=32)
        {
            if(!Foraging(s)||radiusKm<0||maximum<1)return Array.Empty<ForagerBandSample>();var result=new List<(int cohort,int ordinal,double distance)>();
            var index=ForagerResidents.GetValue(s.Foragers,f=>new ForagerResidentIndex(f));
            foreach(var d in s.Foragers.Districts)
            {
                if(DistanceKm(position,d.Center)>radiusKm+d.RadiusKm*2+30)continue;
                foreach(var c in index.Districts[d.Id])
                {
                    if(c.Population<12)continue;
                    int count=ForagerBandCount(s,c);
                    for(int ordinal=0;ordinal<count;ordinal++)
                    {var home=ForagerBandHome(s.Foragers,c,ordinal);double distance=DistanceKm(position,home.position);if(distance<=radiusKm+Math.Max(10,d.RadiusKm/Math.Sqrt(Math.Max(1,count))))result.Add((c.Id,ordinal,distance));}
                }
            }
            return result.OrderBy(x=>x.distance).ThenBy(x=>x.cohort).ThenBy(x=>x.ordinal).Take(maximum*2).Select(x=>ForagerBand(s,x.cohort,x.ordinal)).Where(b=>b!=null&&DistanceKm(position,b.Position)<=radiusKm).Take(maximum).ToArray();
        }
    }
}
