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
            public readonly double[] CampBoundsKm;
            public readonly Dictionary<int,ForagerExchange[]> Contacts;
            public ForagerResidentIndex(World world,ForagerState f)
            {
                CampBoundsKm=ForagerGeometry.GetValue(f.Districts,_=>new ForagerGeometryIndex(world,f)).CampBoundsKm;
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
        sealed class ForagerGeometryIndex
        {
            public readonly double[] CampBoundsKm;
            public ForagerGeometryIndex(World world,ForagerState f)
            {
                CampBoundsKm=new double[f.Districts.Length];
                foreach(var d in f.Districts)foreach(int cell in d.Cells)
                {
                    var ground=world.Surface.Cells[cell];double clearance=ground.DistanceM.Length>0?ground.DistanceM.Min()/1000.0*.35:5;
                    double homeRadius=Math.Min(clearance,Math.Max(1,Math.Sqrt(ground.AreaM2/1e6/Math.PI)*.7));
                    CampBoundsKm[d.Id]=Math.Max(CampBoundsKm[d.Id],world.Recipe.RadiusKm*ForagerSpatialAngle(d.Center,ground.Position)+homeRadius+8.05);
                }
            }
        }
        static readonly ConditionalWeakTable<ForagerDistrict[],ForagerGeometryIndex> ForagerGeometry=new ConditionalWeakTable<ForagerDistrict[],ForagerGeometryIndex>();
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
            double population=ForagerDemography.Present(s.Foragers,c);
            var d=s.Foragers.Districts[c.District];var culture=s.Foragers.Cultures[c.Culture];
            // Residential communities include children, not only the adults
            // counted in many ethnographic band-size tables. Productive,
            // dependable resource bases support larger co-resident camps.
            double typical=26+18*D.Clamp(d.Productivity/450d)+10*culture.GuestAccess;
            if(culture.Residence==ForagerResidence.AquaticResidence)typical+=14*D.Clamp(d.Coast+d.River);
            typical*=.8+.4*Rng.Value(World.Recipe.Seed,"resident-band-size",c.Id);
            return population<12?0:Math.Max(1,(int)Math.Round(population/typical));
        }
        static long ForagerBandId(int cohort,int ordinal)=>((long)(cohort+1)<<32)|(uint)(ordinal+1);
        // Quantized V.Dot plus acos has a ~200m numerical floor at Earth
        // radius, even for the same point. Use raw atan2 for local admission;
        // historical migration/contact arithmetic deliberately stays unchanged.
        static double ForagerSpatialAngle(V a,V b)
        {
            double x=a.Y*b.Z-a.Z*b.Y,y=a.Z*b.X-a.X*b.Z,z=a.X*b.Y-a.Y*b.X;
            return Math.Atan2(Math.Sqrt(x*x+y*y+z*z),a.X*b.X+a.Y*b.Y+a.Z*b.Z);
        }
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
            if(c==null)return null;c=ForagerAdoption.Project(f,c);int count=ForagerBandCount(s,c);if(ordinal>=count)return null;
            var band=ForagerBandLayout(s,c,ordinal,count);var d=f.Districts[c.District];double years=f.Seconds/(double)SimulationTime.Year;
            int key=unchecked(c.Id*104729+ordinal*7919);
            double speciesDraw=Rng.Value(World.Recipe.Seed,"regional-band-species",key),speciesCumulative=0;
            for(int k=0;k<f.Lineages.Length;k++){speciesCumulative+=f.SpeciesShare(c,k);if(speciesDraw<speciesCumulative){band.Lineage=k;break;}}
            band.Language=ForagerAffiliations.Pick(c.Speech,Rng.Value(World.Recipe.Seed,"band-minority-language",key));
            band.Belief=ForagerAffiliations.Pick(c.Traditions,Rng.Value(World.Recipe.Seed,"band-minority-belief",key));
            // Individual makers, rather than an average weak proficiency
            // assigned to everybody. Band availability follows actual residents.
            double tribalRank=Rng.Value(World.Recipe.Seed,"band-knowledge-rank",key);
            for(int k=0;k<band.Techniques.Length;k++)band.Techniques[k]=tribalRank<c.Knowledge[k];
            bool[] visited=new bool[band.Techniques.Length];bool Can(int k){if(visited[k])return band.Techniques[k];visited[k]=true;var t=ForagerTechnology.All[k];return band.Techniques[k]=band.Techniques[k]&&t.Prerequisites.All(Can)&&(t.AlternativePaths.Length==0||t.AlternativePaths.Any(path=>path.All(Can)));}
            for(int k=0;k<band.Techniques.Length;k++)Can(k);
            band.Members=ForagerHouseholds(f,c,band,years,key);
            var adults=band.Members.Where(p=>p.AgeYears>=18&&p.AgeYears<60).ToArray();
            if(adults.Length>0)for(int k=0;k<band.Techniques.Length;k++)if(band.Techniques[k]&&!band.Members.Any(p=>p.Skills[k]>=.35)){var teacher=adults[(int)(Rng.Value(World.Recipe.Seed,"band-specialist-"+k,key)*adults.Length)];teacher.Skills[k]=.7;teacher.Knowledge[k]=.9;}
            int homeCell=ForagerBandHome(f,c,ordinal).cell;
            var environment=ForagerEcology.At(World.Recipe.Seed,d,years,World.ElevationM[homeCell]);
            band.RoutineTechniques=new bool[ForagerTechnology.Count];band.WinterTechniques=new bool[ForagerTechnology.Count];
            for(int k=0;k<band.Techniques.Length;k++)
            {
                // Knowing a maker does not imply routinely using every tool.
                // Group availability is distinct from individual skill prevalence.
                double rank=tribalRank;
                bool products=band.Techniques[k];
                band.RoutineTechniques[k]=products&&rank<c.Use[k];
                bool winter=k==5||k==8||k==13||k==14||k==31||k==34||k==35||k==36;
                band.WinterTechniques[k]=winter?products&&rank<ForagerTechnology.WinterPractice(d,environment,c,k,f.Version==ForagerState.RegionalVersion):band.RoutineTechniques[k];
            }
            var residents=ForagerResidents.GetValue(f,value=>new ForagerResidentIndex(World,value));
            var contacts=residents.Contacts.TryGetValue(c.Id,out var nearby)?nearby:Array.Empty<ForagerExchange>();
            band.ContactBands=contacts.Where(e=>e.Strength>.005).OrderByDescending(e=>e.Strength).Take(8).Select(e=>e.A==c.Id?e.B:e.A).Select(other=>ForagerResident(f,other)).Where(other=>ForagerBandCount(s,other)>0).Select(other=>ForagerBandId(other.Id,(int)(Rng.Value(World.Recipe.Seed,"band-marriage-contact",key^other.Id)*ForagerBandCount(s,other)))).ToArray();
            return band;
        }
        // Spatial queries need current itineraries, not thousands of fully
        // generated households. This layout is shared with full band sampling.
        ForagerBandSample ForagerBandLayout(PrehistoryState s,ForagerCohort c,int ordinal,int count)
        {
            var f=s.Foragers;var d=f.Districts[c.District];var culture=f.Cultures[c.Culture];double years=f.Seconds/(double)SimulationTime.Year;
            int key=unchecked(c.Id*104729+ordinal*7919);long id=ForagerBandId(c.Id,ordinal);var home=ForagerBandHome(f,c,ordinal);
            double population=ForagerDemography.Present(f,c);
            int people=Math.Max(12,(int)Math.Round(population/count));
            double range=Math.Sqrt(Math.Max(20,d.AreaKm2*c.Occupation/count)/Math.PI);
            double moveRank=Rng.Value(World.Recipe.Seed,"community-mobility",key);
            double moves=culture.Residence==ForagerResidence.AquaticResidence?2:culture.Residence==ForagerResidence.CollectorCamps?3+3*d.Seasonality[0]:culture.Residence==ForagerResidence.SeasonalBaseCamps?3+4*d.Seasonality[0]:
                (6+7*d.Seasonality[0]+6*(1-D.Clamp(c.FoodSecurity)))*(.55+1.15*moveRank);
            if(culture.Residence==ForagerResidence.ResidentialMobility&&d.MeanC>18&&d.Canopy>.55&&moveRank>.7)moves+=20*(moveRank-.7)/.3;
            moves=Math.Max(2,Math.Min(40,Math.Round(moves)));
            var band=new ForagerBandSample{Id=id,Cohort=c.Id,District=d.Id,Lineage=c.Lineage,Culture=c.Culture,Language=c.Language,Belief=c.Belief,Home=home.position,People=people,
                Name=culture.Name+" · "+LanguageGenerator.WordWithRootSeed(f.Languages[c.Language].Sounds,"home",key).Text,
                HomeRangeKm=range,MoveFrequencyPerYear=moves,
                Aggregation=D.Clamp(.2+.35*(d.Coast+d.River)+.25*c.Connectivity),FoodSecurity=c.FoodSecurity,Hospitality=culture.GuestAccess,Residence=culture.Residence,Techniques=new bool[ForagerTechnology.Count]};
            band.SeasonalCamps=ForagerSeasonalCamps(s,c,band,home.cell,key);
            int camps=band.SeasonalCamps.Length;double season=((years-59/365d+Rng.Value(World.Recipe.Seed,"camp-calendar-phase",c.Id)*.08)%1+1)%1*camps;int current=(int)season,next=(current+1)%camps;
            // Most time is spent at camp; movement takes actual travel days.
            // Crossing a season boundary interpolates the final days of travel.
            double travel=Math.Min(.8,DistanceKm(band.SeasonalCamps[current].Position,band.SeasonalCamps[next].Position)/18/(365d/camps));
            double fraction=season-current,t=travel>0?D.Clamp((fraction-(1-travel))/travel):0;
            band.Position=(band.SeasonalCamps[current].Position*(1-t)+band.SeasonalCamps[next].Position*t).Unit;
            return band;
        }
        ForagerCampSample[] ForagerSeasonalCamps(PrehistoryState state,ForagerCohort c,ForagerBandSample band,int homeCell,int key)
        {
            var f=state.Foragers;var dated=ForagerHabitat.For(World,state);
            var d=f.Districts[c.District];int count=Math.Max(2,Math.Min(40,(int)Math.Round(band.MoveFrequencyPerYear)));var result=new ForagerCampSample[count];
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
            var languageAccess=new Dictionary<int,double>();
            void LanguageAccess(int language,double value)
            {
                if(language==band.Language||value<=0)return;
                languageAccess.TryGetValue(language,out double before);languageAccess[language]=D.Clamp(before+value);
            }
            // Other resident households can supply a second home language;
            // external access follows actual saved contacts, not random colours
            // or every language anywhere in the district.
            foreach(var speech in c.Speech)LanguageAccess(speech.Id,.5*speech.Share);
            var residents=ForagerResidents.GetValue(f,value=>new ForagerResidentIndex(World,value));
            if(residents.Contacts.TryGetValue(c.Id,out var contacts))foreach(var contact in contacts)
            {
                var other=ForagerResident(f,contact.A==c.Id?contact.B:contact.A);if(other==null||other.Population<1)continue;
                foreach(var speech in other.Speech)LanguageAccess(speech.Id,contact.Bilingualism*(1-.75*contact.Hostility)*speech.Share);
            }
            var languageOpportunities=languageAccess.OrderBy(p=>p.Key).ToArray();
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
                    Name=LanguageGenerator.Capitalize(LanguageGenerator.WordWithRootSeed(f.Languages[band.Language].Sounds,"person",unchecked((int)id)).Text),
                    Role=age<5?"Young child":age<13?"Child and learner":age<19?"Apprentice forager":age>58?"Experienced elder":"Adult forager",Appearance=new double[8],Knowledge=new double[ForagerTechnology.Count],Skills=new double[ForagerTechnology.Count]};
                var languages=new List<ForagerLanguageKnowledge>{new ForagerLanguageKnowledge{Language=band.Language,Native=true,Fluency=D.Clamp((age-.7)/5)}};
                foreach(var opportunity in languageOpportunities)
                {
                    double maturity=D.Clamp((age-4)/12),rank=Rng.Value(World.Recipe.Seed,"household-contact-language-"+opportunity.Key,unchecked((int)record.Household^(int)(record.Household>>32)));
                    if(rank>=opportunity.Value*maturity)continue;
                    double fluency=maturity*(.65+.35*Rng.Value(World.Recipe.Seed,"individual-language-fluency-"+opportunity.Key,(int)id));
                    languages.Add(new ForagerLanguageKnowledge{Language=opportunity.Key,Fluency=fluency});
                }
                person.Languages=languages.ToArray();
                for(int k=0;k<8;k++)person.Appearance[k]=D.Clamp(c.Appearance[k]+(Rng.Value(World.Recipe.Seed,"household-appearance-"+k,(int)record.Household)-.5)*.12+(Rng.Value(World.Recipe.Seed,"individual-appearance-"+k,(int)id)-.5)*.08);
                double apprenticeship=D.Clamp((age-5)/18);
                void Skilled(int technique,double proficiency)
                {
                    if(!band.Techniques[technique]||person.Skills[technique]>=proficiency)return;
                    person.Skills[technique]=proficiency;person.Knowledge[technique]=Math.Max(person.Knowledge[technique],proficiency);
                    var method=ForagerTechnology.All[technique];foreach(int prerequisite in method.Prerequisites)Skilled(prerequisite,proficiency);
                    if(method.AlternativePaths.Length>0)
                    {
                        var path=method.AlternativePaths.OrderByDescending(option=>option.Min(k=>c.Skill[k])).First();
                        foreach(int prerequisite in path)Skilled(prerequisite,proficiency);
                    }
                }
                for(int k=0;k<person.Skills.Length;k++)
                {
                    if(band.Techniques[k]&&Rng.Value(World.Recipe.Seed,"individual-familiarity-"+k,(int)id)<.8)person.Knowledge[k]=Math.Max(person.Knowledge[k],D.Clamp((age-2)/12)*(.65+.35*Rng.Value(World.Recipe.Seed,"individual-familiarity-depth-"+k,(int)id)));
                    if(Rng.Value(World.Recipe.Seed,"individual-trained-"+k,(int)id)<c.Skill[k]/Math.Max(.000001,c.Knowledge[k]))Skilled(k,apprenticeship*(.65+.35*Rng.Value(World.Recipe.Seed,"individual-skill-"+k,(int)id)));
                }
                return person;
            }
        }

        public ForagerBandSample[] ForagerBandsNear(PrehistoryState s,V position,double radiusKm,int maximum=32)
            =>FindForagerBandsNear(s,position,radiusKm,maximum,true,null);
        // Nearby-world streaming needs only group footprints. Households and
        // individual skill/name/kin records are materialized at a nearer tier.
        public ForagerBandSample[] ForagerBandFootprintsNear(PrehistoryState s,V position,double radiusKm,Func<long,bool> admit=null)
            =>FindForagerBandsNear(s,position,radiusKm,int.MaxValue,false,admit);
        ForagerBandSample[] FindForagerBandsNear(PrehistoryState s,V position,double radiusKm,int maximum,bool members,Func<long,bool> admit)
        {
            if(!Foraging(s)||radiusKm<0||maximum<1)return Array.Empty<ForagerBandSample>();var result=new List<(int cohort,int ordinal,double distance,ForagerBandSample layout)>();
            var index=ForagerResidents.GetValue(s.Foragers,f=>new ForagerResidentIndex(World,f));
            foreach(var d in s.Foragers.Districts)
            {
                if(World.Recipe.RadiusKm*ForagerSpatialAngle(position,d.Center)>radiusKm+index.CampBoundsKm[d.Id])continue;
                foreach(var c in index.Districts[d.Id])
                {
                    if(c.Population<12)continue;
                    int count=ForagerBandCount(s,c);
                    for(int ordinal=0;ordinal<count;ordinal++)
                    {
                        if(admit!=null&&!admit(ForagerBandId(c.Id,ordinal)))continue;
                        var home=ForagerBandHome(s.Foragers,c,ordinal);
                        double range=Math.Sqrt(Math.Max(20,d.AreaKm2*c.Occupation/count)/Math.PI);
                        if(World.Recipe.RadiusKm*ForagerSpatialAngle(position,home.position)>radiusKm+Math.Max(10,range*1.4)+8.05)continue;
                        var layout=ForagerBandLayout(s,c,ordinal,count);double distance=World.Recipe.RadiusKm*ForagerSpatialAngle(position,layout.Position);
                        if(!members||distance<=radiusKm)result.Add((c.Id,ordinal,distance,layout));
                    }
                }
            }
            return result.OrderBy(x=>x.distance).ThenBy(x=>x.cohort).ThenBy(x=>x.ordinal).Take(maximum).Select(x=>
            {
                if(members)return ForagerBand(s,x.cohort,x.ordinal);
                var f=s.Foragers;var c=ForagerAdoption.Project(f,ForagerResident(f,x.cohort));var band=x.layout;int key=unchecked(c.Id*104729+x.ordinal*7919);
                double rank=Rng.Value(World.Recipe.Seed,"band-knowledge-rank",key),draw=Rng.Value(World.Recipe.Seed,"regional-band-species",key),cumulative=0;
                for(int k=0;k<f.Lineages.Length;k++){cumulative+=f.SpeciesShare(c,k);if(draw<cumulative){band.Lineage=k;break;}}
                band.Language=ForagerAffiliations.Pick(c.Speech,Rng.Value(World.Recipe.Seed,"band-minority-language",key));band.Belief=ForagerAffiliations.Pick(c.Traditions,Rng.Value(World.Recipe.Seed,"band-minority-belief",key));
                for(int k=0;k<band.Techniques.Length;k++)band.Techniques[k]=rank<c.Knowledge[k];
                bool[] visited=new bool[band.Techniques.Length];bool Can(int k){if(visited[k])return band.Techniques[k];visited[k]=true;var t=ForagerTechnology.All[k];return band.Techniques[k]=band.Techniques[k]&&t.Prerequisites.All(Can)&&(t.AlternativePaths.Length==0||t.AlternativePaths.Any(path=>path.All(Can)));}
                for(int k=0;k<band.Techniques.Length;k++)Can(k);
                return band;
            }).ToArray();
        }
    }
}
