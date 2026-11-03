using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        public const int RegionalProcessCount=7;
        // Historical approximations have broad windows; exact-date political
        // processes can later schedule their own explicit Due dates in the queue.
        // Technology events now sample invention/contact opportunities. Stored
        // adoption curves progress independently, including between reviews.
        static readonly int[] RegionalMinimum={200,600,200,300,200,700,300};
        static readonly int[] RegionalMaximum={400,1800,600,900,800,1700,900};
        static readonly uint[] RegionalStreams=Enumerable.Range(0,RegionalProcessCount).Select(p=>Rng.StreamHash("regional-event-"+p)).ToArray();
        void ScheduleRegional(ForagerState f,int region,int process,long now,long occurrence,bool first=false)
        {
            double draw=Rng.ValueFromStreamHash(World.Recipe.Seed,RegionalStreams[process],unchecked(region*104729+(int)occurrence*7919));
            long days=first?1+(long)(draw*RegionalMaximum[process]*365):RegionalMinimum[process]*365+(long)(draw*(RegionalMaximum[process]-RegionalMinimum[process])*365);
            f.PendingEvents=RegionalQueueNode.Push(f.PendingEvents,new SimulationEvent{Process=process,Entity=region,Ticket=occurrence,Due=now+days*SimulationTime.Day});
        }
        void InitializeRegionalCalendar(ForagerState f)
        {
            f.PopulationAt=new long[f.Districts.Length];f.ProcessAt=new long[f.Districts.Length*RegionalProcessCount];f.PendingEvents=null;
            foreach(var d in f.Districts){ScheduleRegional(f,d.Id,5,0,0,true);if(f.RegionResidents[d.Id]>=0)for(int p=0;p<RegionalProcessCount;p++)if(p!=5)ScheduleRegional(f,d.Id,p,0,0,true);}
            f.NextReviewSeconds=f.PendingEvents.Event.Due;
        }

        sealed class RegionalEventWork
        {
            readonly Prehistory engine;readonly PrehistoryState state;readonly ForagerState f;readonly CancellationToken token;
            readonly HashSet<int> people=new HashSet<int>(),resources=new HashSet<int>(),cultures=new HashSet<int>(),languages=new HashSet<int>(),beliefs=new HashSet<int>();
            readonly HashSet<int> biota=new HashSet<int>(),families=new HashSet<int>(),adoption=new HashSet<int>();readonly bool exclusive;bool remappedFamilies;
            readonly ForagerEnvironment[] climate;readonly int[] climateEpoch;long now;
            public RegionalEventWork(Prehistory engine,PrehistoryState state,CancellationToken token,bool exclusive=false)
            {this.engine=engine;this.state=state;this.f=state.Foragers;this.token=token;this.exclusive=exclusive;climate=new ForagerEnvironment[f.Districts.Length];climateEpoch=Enumerable.Repeat(int.MinValue,climate.Length).ToArray();}
            ForagerCohort Read(int region){int id=f.RegionResidents[region];return id<0?null:f.Cohorts[id];}
            ForagerCohort Write(int region)
            {var c=Read(region);if(c!=null&&people.Add(c.Id)&&!exclusive)f.Cohorts[c.Id]=c=c.Copy();return c;}
            void Materialize(int region)
            {
                var c=Write(region);if(c==null||f.PopulationAt[region]>=now)return;
                ForagerDemography.Materialize(c,(now-f.PopulationAt[region])/(double)SimulationTime.Year,f.PopulationAt[region]/(double)SimulationTime.Year);f.PopulationAt[region]=now;
            }
            ForagerEnvironment Environment(int region)
            {
                int epoch=(int)(now/SimulationTime.Year/25);var d=f.Districts[region];
                if(climateEpoch[region]!=epoch){climate[region]=ForagerEcology.At(engine.World.Recipe.Seed,d,epoch*25,engine.World.ElevationM[d.Cell]);climateEpoch[region]=epoch;}
                return ForagerBiota.Apply(climate[region],f.Biota[region]);
            }
            List<(ForagerCohort people,ForagerExchange relation)> Neighbours(ForagerCohort c)
            {
                var result=new List<(ForagerCohort,ForagerExchange)>();var a=Environment(c.District);var d=f.Districts[c.District];double total=0;ForagerCohort prepared=null;
                foreach(var group in RegionalPaths(f.Districts)[c.District])
                {
                    var other=Read(group[0].To);if(other==null||other.Population<1)continue;var b=Environment(other.District);
                    var pass=group.FirstOrDefault(p=>
                    {
                        // Ordinary land contacts never inspect equipment. Do
                        // not project 43 methods just to ask who lives next door.
                        bool gear=p.Sea&&!p.Shelf||p.MountainBarrier>.85&&Math.Min(a.SummerC,b.SummerC)<4;
                        return ForagerPassages.Open(engine.World.Recipe.Seed,p,d,a,b,gear?(prepared??(prepared=ForagerAdoption.Project(f,c))):c,true);
                    });if(pass==null)continue;
                    double openness=(f.Cultures[c.Culture].ExchangeOpenness+f.Cultures[other.Culture].ExchangeOpenness)*.5;
                    double strength=D.Clamp((.08+.55*openness)*Math.Exp(-ForagerPassages.TravelCostKm(pass,a,b)/500)*(pass.Sea?.2:1));if(strength<.002)continue;
                    total+=strength;result.Add((other,new ForagerExchange{A=Math.Min(c.Id,other.Id),B=Math.Max(c.Id,other.Id),Strength=strength,Hospitality=openness,SinceYear=(int)(now/SimulationTime.Year)}));
                }
                c.Connectivity=D.Clamp(total);c.Isolation=1-c.Connectivity;c.BilingualShare=D.Clamp(total*.4);c.MatingContacts=result.Sum(x=>x.Item1.Adults*x.Item2.Strength*.01);return result;
            }
            void Rates(int region,double elapsed)
            {
                var c=Write(region);if(c==null||c.Population<=0)return;var d=f.Districts[region];var e=Environment(region);
                if(resources.Add(region)&&!exclusive)f.Resources[region]=f.Resources[region].Copy();var r=f.Resources[region];
                var projected=ForagerAdoption.Project(f,c);var life=ForagerEcology.Livelihood(d,e,f.Lineages[c.Lineage],projected,f.Cultures[c.Culture],r,true);
                ForagerDemography.Rates(c,life,e,f.Cultures[c.Culture],life.Capacity);
                c.FrontierKm=Math.Min(d.RadiusKm*1.5,c.FrontierKm+ForagerPassages.FrontierKmPerYear(projected,f.Cultures[c.Culture],d,e,true)*elapsed);
                c.Occupation=D.Q(D.Clamp(c.Population/Math.Max(30,life.Capacity),.005,1),1000000);
                ForagerEcology.RecoverResources(r,elapsed,c.Population,life.Capacity,life.Diet);r.HumanDensity=c.Population/d.AreaKm2;r.LastReviewYear=(int)(now/SimulationTime.Year);
            }
            void Technology(ForagerCohort c,int year,int elapsed)
            {
                var adjacent=Neighbours(c);var teacher=new double[ForagerTechnology.Count];foreach(var contact in adjacent){var knowledge=ForagerAdoption.Project(f,contact.people).Knowledge;for(int k=0;k<teacher.Length;k++)teacher[k]+=knowledge[k]*contact.relation.Strength;}
                var previous=f.Adoption[c.District];f.Adoption[c.District]=ForagerAdoption.Review(engine.World.Recipe.Seed,f,c,f.Districts[c.District],Environment(c.District),f.Cultures[c.Culture],elapsed,teacher,out ulong invented);
                if(!ReferenceEquals(previous,f.Adoption[c.District]))adoption.Add(c.District);
                var view=ForagerAdoption.Project(f,c);c.Knowledge=(double[])view.Knowledge.Clone();c.Skill=(double[])view.Skill.Clone();c.Use=(double[])view.Use.Clone();
                for(int k=0;k<ForagerTechnology.Count;k++)
                {
                    if(c.Knowledge[k]>0&&c.FirstKnownYear[k]<0)c.FirstKnownYear[k]=year;
                    if(c.Skill[k]>=.5&&c.FirstSkilledYear[k]<0)c.FirstSkilledYear[k]=year;
                    if((invented&(1UL<<k))!=0)engine.AddRegionalEvent(f,c,year,ForagerEventKind.Innovation,ForagerTechnology.All[k].Name+" is being developed in this region.");
                }
            }
            void Language(ForagerCohort c,int year,int elapsed)
            {
                int id=c.Id;if(languages.Add(id)&&!exclusive)f.Languages[id]=f.Languages[id].Copy();var l=f.Languages[id];
                var peer=Neighbours(c).OrderByDescending(x=>x.relation.Strength).FirstOrDefault();
                ForagerLanguages.Evolve(engine.World.Recipe.Seed,l,year,elapsed,c.Isolation,peer.relation?.Strength??0,peer.people==null?null:f.Languages[peer.people.Language]);
                double potential=.25+.75*ForagerSociality.Capacity(f,c,true);
                l.TechnicalRegister=D.Q(PaleolithicRules.Approach(l.TechnicalRegister,potential*D.Clamp(.3+.02*ForagerAdoption.Project(f,c).Skill.Count(x=>x>.1)+.2*c.Connectivity),1/12000d,elapsed),1000000);
                l.NarrativePractice=D.Q(PaleolithicRules.Approach(l.NarrativePractice,potential*D.Clamp(.6+.3*c.Connectivity),1/15000d,elapsed),1000000);
                c.Speech=ForagerAffiliations.Regional(ForagerAffiliations.Approach(c.Speech,ForagerAffiliations.Single(id),1-Math.Exp(-elapsed/3000d),c.Population));ForagerAffiliations.Refresh(c);
                if(l.Family>=0&&ForagerLanguages.SharedRoots(l,f.LanguageFamilies[l.Family])<.2)
                {
                    var family=new ForagerLanguageFamily{Id=f.NextFamily++,Parent=l.Family,FounderLanguage=id,FoundedYear=year,OriginDistrict=c.District,Name=l.Name+" family",ReferenceRoots=(int[])l.Roots.Clone()};
                    f.LanguageFamilies=f.LanguageFamilies.Concat(new[]{family}).ToArray();families.Add(family.Id);l.Family=family.Id;
                    if(f.LanguageFamilies.Length>Math.Max(16,f.Districts.Length*2))
                    {for(int i=0;i<f.Languages.Length;i++)if(languages.Add(i)&&!exclusive)f.Languages[i]=f.Languages[i].Copy();if(!exclusive)f.LanguageFamilies=f.LanguageFamilies.Select(x=>x.Copy()).ToArray();CompactRegionalFamilies(f);remappedFamilies=true;}
                }
            }
            void Tradition(ForagerCohort c,int year,int elapsed)
            {
                int id=c.Id;if(beliefs.Add(id)&&!exclusive)f.Beliefs[id]=f.Beliefs[id].Copy();var peer=Neighbours(c).OrderByDescending(x=>x.relation.Strength).FirstOrDefault();
                ForagerBeliefs.Change(engine.World.Recipe.Seed,f.Beliefs[id],f.Districts[c.District],year,elapsed,c.Isolation,peer.relation?.Strength??0,peer.people==null?null:f.Beliefs[peer.people.Belief]);
                c.Traditions=ForagerAffiliations.Regional(ForagerAffiliations.Approach(c.Traditions,ForagerAffiliations.Single(id),1-Math.Exp(-elapsed/3000d),c.Population));ForagerAffiliations.Refresh(c);
            }
            void Migration(ForagerCohort c,int year,int elapsed)
            {
                if(c.Adults<18||c.Population<45)return;var projected=ForagerAdoption.Project(f,c);var d=f.Districts[c.District];var a=Environment(c.District);var culture=f.Cultures[c.Culture];ForagerPassage best=null;double score=0,number=0;
                foreach(var group in RegionalPaths(f.Districts)[c.District])
                {
                    var b=Environment(group[0].To);var pass=group.FirstOrDefault(p=>ForagerPassages.Open(engine.World.Recipe.Seed,p,d,a,b,projected,true));if(pass==null)continue;
                    var target=f.Districts[pass.To];var receiver=Read(pass.To);double present=receiver==null?0:ForagerDemography.Present(f,receiver);
                    var life=ForagerEcology.Livelihood(target,b,f.Lineages[c.Lineage],projected,culture,f.Resources[pass.To],true);if(life.Capacity<80||life.WinterSecurity<.08)continue;
                    double cost=ForagerPassages.TravelCostKm(pass,a,b);bool founding=present<1;if(founding&&c.FrontierKm<Math.Min(d.RadiusKm,cost*.35))continue;
                    double room=life.Capacity-present*.85;if(room<18)continue;double urgency=D.Clamp((.9-c.FoodSecurity)/.7),crowding=D.Clamp(c.Population/Math.Max(1,c.Capacity));
                    double chance=(1-Math.Exp(-elapsed/(founding?240:120d)))*(.15+.7*crowding+.9*urgency)*(.08+.6*culture.ExchangeOpenness);
                    if(pass.Sea&&!pass.Shelf)chance*=.03*Math.Exp(-(pass.WaterDistanceM>0?pass.WaterDistanceM:pass.DistanceM)/140000d);
                    if(Rng.Value(engine.World.Recipe.Seed,"regional-dispersal",unchecked(c.Id*104729+pass.To*193+year))>=chance)continue;
                    double n=Math.Min(c.Population*.12,room*.3);if(founding&&n<18||n<1)continue;
                    double value=Math.Log(1+room)/(1+cost/300);if(value>score){score=value;best=pass;number=n;}
                }
                if(best==null)return;var destination=Read(best.To);
                if(destination==null)
                {
                    destination=engine.NewForagerCohort(f,best.To,c.Lineage,year,c);ForagerDemography.SetPopulation(destination,0);destination.HomeCell=destination.FrontierCell=best.ToCell;
                    f.Adoption[best.To]=f.Adoption[c.District];adoption.Add(best.To);
                    f.Cohorts=f.Cohorts.Concat(new[]{destination}).ToArray();f.RegionResidents[best.To]=destination.Id;people.Add(destination.Id);f.PopulationAt[best.To]=now;
                    for(int p=0;p<RegionalProcessCount;p++)if(p!=5){f.ProcessAt[best.To*RegionalProcessCount+p]=now;engine.ScheduleRegional(f,best.To,p,now,0);}
                }
                else{Materialize(best.To);destination=Write(best.To);}
                if(destination.Teaching.Length==0){destination.Teaching=new double[ForagerTechnology.Count];destination.Innovation=new double[ForagerTechnology.Count];destination.TeachingFrom=new double[ForagerTechnology.Count*2];}
                double old=destination.Population,total=old+number;
                f.Adoption[best.To]=ForagerAdoption.Merge(f,destination,c,old,number);adoption.Add(best.To);
                for(int k=0;k<f.Lineages.Length;k++)f.SpeciesShares[best.To*f.Lineages.Length+k]=(old*f.SpeciesShare(destination,k)+number*f.SpeciesShare(c,k))/total;
                var tech=ForagerAdoption.Project(f,destination);destination.Knowledge=(double[])tech.Knowledge.Clone();destination.Skill=(double[])tech.Skill.Clone();destination.Use=(double[])tech.Use.Clone();
                for(int k=0;k<8;k++)destination.Appearance[k]=(destination.Appearance[k]*old+c.Appearance[k]*number)/total;
                for(int k=0;k<c.Admixture.Length;k++)destination.Admixture[k]=(destination.Admixture[k]*old+c.Admixture[k]*number)/total;
                destination.Speech=ForagerAffiliations.Regional(ForagerAffiliations.Blend(destination.Speech,old,c.Speech,number));destination.Traditions=ForagerAffiliations.Regional(ForagerAffiliations.Blend(destination.Traditions,old,c.Traditions,number));ForagerAffiliations.Refresh(destination);
                ForagerDemography.MovePopulation(c,destination,number);c.LastMigrationYear=destination.LastMigrationYear=year;Rates(best.To,0);
                int primary=0;for(int k=1;k<f.Lineages.Length;k++)if(f.SpeciesShare(destination,k)>f.SpeciesShare(destination,primary))primary=k;destination.Lineage=primary;
            }
            public void Run(long target,int maximum,bool timed,double milliseconds=8)
            {
                var watch=Stopwatch.StartNew();int processed=0;var ticks=engine.WorkObserver==null?null:new long[RegionalProcessCount];var counts=ticks==null?null:new int[RegionalProcessCount];
                while(f.PendingEvents.Event.Due<=target&&processed<maximum)
                {
                    token.ThrowIfCancellationRequested();var item=f.PendingEvents.Event;f.PendingEvents=RegionalQueueNode.Merge(f.PendingEvents.Left,f.PendingEvents.Right);now=f.Seconds=item.Due;
                    int region=item.Entity,slot=region*RegionalProcessCount+item.Process,year=(int)(now/SimulationTime.Year),elapsed=Math.Max(1,(int)Math.Round((now-f.ProcessAt[slot])/(double)SimulationTime.Year));
                    long workStarted=ticks==null?0:Stopwatch.GetTimestamp();var c=Read(region);
                    if(item.Process==5){biota.Add(region);ForagerBiota.AdvanceRegion(engine.World,f,Environment,region,year,elapsed,token);}
                    else if(c!=null)
                    {
                        Materialize(region);c=Write(region);
                        if(c.Population>=1)switch(item.Process)
                        {
                            case 0:Rates(region,elapsed);break;
                            case 1:Technology(c,year,elapsed);Rates(region,0);break;
                            case 2:Migration(c,year,elapsed);Rates(region,0);break;
                            case 3:Language(c,year,elapsed);break;
                            case 4:Tradition(c,year,elapsed);break;
                            case 6:if(cultures.Add(c.Culture)&&!exclusive)f.Cultures[c.Culture]=f.Cultures[c.Culture].Copy();var peer=Neighbours(c).OrderByDescending(x=>x.relation.Strength).FirstOrDefault();engine.ChangeForagerCulture(f,ForagerAdoption.Project(f,c),Environment(region),year,elapsed,peer.people==null?null:f.Cultures[peer.people.Culture],peer.relation?.Strength??0);break;
                        }
                    }
                    f.ProcessAt[slot]=now;engine.ScheduleRegional(f,region,item.Process,now,item.Ticket+1);f.ProcessedEvents++;f.BoundarySeconds=now;processed++;
                    if(ticks!=null){ticks[item.Process]+=Stopwatch.GetTimestamp()-workStarted;counts[item.Process]++;}
                    if(timed&&watch.Elapsed.TotalMilliseconds>=milliseconds)break;
                }
                f.NextReviewSeconds=f.PendingEvents.Event.Due;f.Seconds=Math.Min(target,f.NextReviewSeconds-1);token.ThrowIfCancellationRequested();
                if(ticks!=null)for(int p=0;p<RegionalProcessCount;p++)engine.WorkObserver(new SimulationWorkMetric{Stage="regional process "+p,Items=counts[p],Milliseconds=ticks[p]*1000d/Stopwatch.Frequency});
            }
            public void Capture(RegionalPublication p,int oldPeople,int oldCultures,int oldLanguages,int oldBeliefs,int oldIdentities,int oldFamilies)
            {
                RegionalChanges<T> Changed<T>(T[] values,IEnumerable<int> touched,int oldLength)where T:class
                {var ids=touched.Concat(Enumerable.Range(oldLength,Math.Max(0,values.Length-oldLength))).Where(i=>i<values.Length).Distinct().OrderBy(i=>i).ToArray();return new RegionalChanges<T>{Length=values.Length,Indices=ids,Values=ids.Select(i=>values[i]).ToArray()};}
                p.People=Changed(f.Cohorts,people,oldPeople);p.Adoption=Changed(f.Adoption,adoption,f.Adoption.Length);p.Resources=Changed(f.Resources,resources,f.Resources.Length);p.Cultures=Changed(f.Cultures,cultures,oldCultures);p.Languages=Changed(f.Languages,languages,oldLanguages);p.Beliefs=Changed(f.Beliefs,beliefs,oldBeliefs);p.Identities=Changed(f.Peoples,Array.Empty<int>(),oldIdentities);
                p.Families=Changed(f.LanguageFamilies,remappedFamilies?Enumerable.Range(0,f.LanguageFamilies.Length):families,oldFamilies);
                p.Biota=biota.OrderBy(i=>i).Select(i=>new RegionalBiotaUpdate{District=i,ReviewedYear=f.Biota[i].ReviewedYear,ForestCover=f.Biota[i].ForestCover,FoodAvailability=f.Biota[i].FoodAvailability}).ToArray();
            }
        }
        // Only for a worker that exclusively owns its input and has no readers.
        // Dirty indexes replace snapshot comparison; sent deltas are serialized
        // before the next advance. Normal UI/public snapshots remain immutable.
        public RegionalPublication AdvanceRegionalExclusive(PrehistoryState state,long seconds,CancellationToken token=default,double maximumMilliseconds=double.PositiveInfinity)
        {
            var f=state.Foragers;long from=f.Seconds;if(!state.Started||seconds<1||seconds>SimulationTime.Maximum-from)throw new ArgumentOutOfRangeException(nameof(seconds));
            long target=from+seconds;bool clockOnly=target<f.NextReviewSeconds;RegionalEventWork work=null;
            int people=f.Cohorts.Length,cultures=f.Cultures.Length,languages=f.Languages.Length,beliefs=f.Beliefs.Length,identities=f.Peoples.Length,families=f.LanguageFamilies.Length;
            if(clockOnly)f.Seconds=target;else{work=new RegionalEventWork(this,state,token,true);work.Run(target,int.MaxValue,!double.IsPositiveInfinity(maximumMilliseconds),maximumMilliseconds);state.DomainToken=new object();}
            state.Years=(int)(f.Seconds/SimulationTime.Year);state.Paleolithic.Seconds=f.Seconds;state.Paleolithic.BoundarySeconds=f.BoundarySeconds;state.InvalidateHash();
            if(clockOnly)return new RegionalPublication{ClockOnly=true,From=from,Seconds=f.Seconds};
            var p=RegionalPublication.Header(from,state);work.Capture(p,people,cultures,languages,beliefs,identities,families);return p;
        }
        void AddRegionalEvent(ForagerState f,ForagerCohort c,int year,ForagerEventKind kind,string text)
        {f.Events=f.Events.Concat(new[]{new ForagerEvent{Seconds=year*SimulationTime.Year,Kind=kind,Cohort=c.Id,District=c.District,Text=text}}).TakeLast(256).ToArray();}
    }
}
