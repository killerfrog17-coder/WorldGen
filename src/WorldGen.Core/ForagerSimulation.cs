using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        internal PrehistoryState AdvanceForagers(PrehistoryState source,long seconds,int maximumEvents,CancellationToken token,bool validate,int maximumWork=int.MaxValue)
        {
            if(validate)ValidateForagers(source);if(!source.Started)throw new InvalidOperationException("Start the world first.");
            if(seconds<1||seconds>SimulationTime.Maximum-source.Foragers.Seconds||maximumEvents<1||maximumWork<1)throw new ArgumentOutOfRangeException(nameof(seconds));
            var s=source.Copy();var f=s.Foragers;long target=f.Seconds+seconds;int processed=0;
            while(f.NextReviewSeconds<=target&&processed<Math.Min(maximumEvents,maximumWork))
            {
                token.ThrowIfCancellationRequested();long next=f.NextReviewSeconds;int year=(int)(next/SimulationTime.Year);double previousYear=f.BoundarySeconds/(double)SimulationTime.Year;
                foreach(var c in f.Cohorts)
                {
                    if((c.Id&127)==0)token.ThrowIfCancellationRequested();double previous=c.Population;if(previous<=0)continue;
                    ForagerDemography.Materialize(c,5,previousYear);
                    if(c.Population==0)AddForagerEvent(f,ForagerEventKind.Extinction,year,c,"The last resident bands have disappeared from this district.");
                    else if(c.Population<previous*.7)AddForagerEvent(f,ForagerEventKind.Decline,year,c,c.WinterSecurity<.3?"Severe winter exposure has broken up the local population.":c.FoodSecurity<.5?"A food crisis has sharply reduced the resident population.":"Illness and hardship have sharply reduced the local population.");
                }
                f.Seconds=f.BoundarySeconds=next;f.NextReviewSeconds=next+5*SimulationTime.Year;
                var environment=ForagerEnvironments(f,year);
                if(year%25==0)
                {
                    ForagerSociality.Review(World.Recipe.Seed,f,environment,year,25);
                    MigrateForagers(f,environment,year,25,token);
                    ReviewForagerShocks(f,environment,year);
                }
                if(year%50==0)LearnForagers(f,environment,year,50,token);
                if(year%250==0)ChangeForagerSocieties(f,environment,year,250,token);
                SetForagerRates(f,environment,year,5,token);processed++;
            }
            f.Seconds=f.NextReviewSeconds<=target?Math.Min(target,f.NextReviewSeconds-1):target;
            PublishForagers(s);if(processed==0)s.DomainToken=source.DomainToken;token.ThrowIfCancellationRequested();s.Seal();return s;
        }
        static void AddForagerEvent(ForagerState f,ForagerEventKind kind,int year,ForagerCohort c,string text,int other=-1)
        {f.Events=f.Events.Concat(new[]{new ForagerEvent{Seconds=year*SimulationTime.Year,Kind=kind,Cohort=c?.Id??-1,District=c?.District??-1,Other=other,Text=text}}).TakeLast(2048).ToArray();}
        void SetForagerRates(ForagerState f,ForagerEnvironment[] environment,int year,int elapsed,CancellationToken token)
        {
            foreach(var group in f.Cohorts.Where(c=>c.Population>0).GroupBy(c=>c.District))
            {
                if((group.Key&127)==0)token.ThrowIfCancellationRequested();var d=f.Districts[group.Key];var e=environment[group.Key];var resources=f.Resources[group.Key];var people=group.ToArray();
                var lives=people.Select(c=>ForagerEcology.Livelihood(d,e,f.Lineages[c.Lineage],c,f.Cultures[c.Culture],resources)).ToArray();
                var demand=new double[5];double total=people.Sum(c=>c.Population);
                for(int i=0;i<people.Length;i++)for(int k=0;k<5;k++)demand[k]+=people[i].Population*lives[i].Diet[k];
                var districtDiet=new double[5];
                for(int i=0;i<people.Length;i++)
                {
                    var c=people[i];double allocated=0;
                    for(int k=0;k<5;k++){double share=demand[k]>0?c.Population*lives[i].Diet[k]/demand[k]:0;allocated+=lives[i].Capacity*lives[i].Diet[k]*share;districtDiet[k]+=c.Population/total*lives[i].Diet[k];}
                    ForagerDemography.Rates(c,lives[i],e,f.Cultures[c.Culture],allocated);
                    double speed=ForagerPassages.FrontierKmPerYear(c,f.Cultures[c.Culture],d,e);c.FrontierKm=Math.Min(d.RadiusKm*1.5,c.FrontierKm+speed*elapsed);
                    c.Occupation=D.Q(D.Clamp(c.Population/Math.Max(30,lives[i].Capacity),.005,1),1000000);
                }
                double capacity=people.Sum(c=>c.Capacity);ForagerEcology.RecoverResources(resources,elapsed,total,capacity,districtDiet);resources.HumanDensity=D.Q(total/d.AreaKm2,1000000000);resources.LastReviewYear=year;
            }
            foreach(var resources in f.Resources)if(resources.LastReviewYear!=year)
            {ForagerEcology.RecoverResources(resources,elapsed,0,1,new double[5]);resources.HumanDensity=0;resources.LastReviewYear=year;}
        }
        void LearnForagers(ForagerState f,ForagerEnvironment[] environment,int year,int elapsed,CancellationToken token)
        {
            var neighbours=ForagerSociality.Neighbours(f);
            var oldSkills=f.Cohorts.ToDictionary(c=>c.Id,c=>(double[])c.Skill.Clone());
            foreach(var c in f.Cohorts.Where(c=>c.Population>=1))
            {
                if((c.Id&63)==0)token.ThrowIfCancellationRequested();var teachers=new double[ForagerTechnology.Count];double weight=0,adults=0;
                if(neighbours.TryGetValue(c.Id,out var contacts))foreach(var contact in contacts)
                {
                    double strength=contact.relation.Strength*(1-.75*contact.relation.Hostility);weight+=strength;adults+=contact.people.Adults*strength;
                    for(int k=0;k<teachers.Length;k++)teachers[k]+=oldSkills[contact.people.Id][k]*strength;
                }
                for(int k=0;k<teachers.Length;k++)teachers[k]/=Math.Max(1,weight);
                var previous=(double[])c.Skill.Clone();ForagerTechnology.Advance(World.Recipe.Seed,c,f.Districts[c.District],environment[c.District],f.Cultures[c.Culture],elapsed,year,teachers,adults);
                for(int k=0;k<previous.Length;k++)
                {
                    if(previous[k]<.5&&c.Skill[k]>=.5)AddForagerEvent(f,ForagerEventKind.Innovation,year,c,ForagerTechnology.All[k].Name+" is becoming a trained local practice.");
                    if(previous[k]>=.25&&c.Skill[k]<.25)AddForagerEvent(f,ForagerEventKind.KnowledgeLoss,year,c,"Trained practice in "+ForagerTechnology.All[k].Name.ToLowerInvariant()+" has become scarce.");
                }
            }
        }
        void ReviewForagerShocks(ForagerState f,ForagerEnvironment[] environment,int year)
        {
            foreach(var c in f.Cohorts.Where(c=>c.Population>=1))
            {
                if(c.ShockEnd>f.Seconds)continue;var env=environment[c.District];var culture=f.Cultures[c.Culture];
                double infection=(.08+.8*env.Pathogens+.25*c.Connectivity)/1800;
                bool outbreak=Rng.Value(World.Recipe.Seed,"forager-local-outbreak",c.Id*7919+year)<1-Math.Exp(-25*infection);
                bool conflict=!outbreak&&Rng.Value(World.Recipe.Seed,"forager-local-conflict",c.Id*193+year)<25*c.ConflictPressure*(1-culture.ConflictAvoidance)/1500;
                if(!outbreak&&!conflict)continue;
                c.ShockStart=f.Seconds+(long)(5*SimulationTime.Year*Rng.Value(World.Recipe.Seed,"forager-shock-onset",c.Id^year));
                c.ShockEnd=c.ShockStart+(long)((1.5+3*Rng.Value(World.Recipe.Seed,"forager-shock-duration",c.Id^year))*SimulationTime.Year);
                c.ShockSeverity=D.Q((.008+.035*Rng.Value(World.Recipe.Seed,"forager-shock-severity",c.Id+year))*(outbreak?1:1-culture.ConflictAvoidance*.5),1000000);
                c.ShockKind=outbreak?ForagerEventKind.Outbreak:ForagerEventKind.Conflict;
                AddForagerEvent(f,c.ShockKind,year,c,outbreak?"A local episode of fever or intestinal illness is affecting the camps.":"Resource disputes have led to violence and the departure of families.");
            }
        }
        void ChangeForagerSocieties(ForagerState f,ForagerEnvironment[] environment,int year,int elapsed,CancellationToken token)
        {
            var neighbours=ForagerSociality.Neighbours(f);var languageContacts=new Dictionary<int,HashSet<int>>();
            var oldLanguages=f.Languages.Select(l=>l.Copy()).ToDictionary(l=>l.Id);var oldBeliefs=f.Beliefs.Select(b=>b.Copy()).ToDictionary(b=>b.Id);var oldAdmixture=f.Cohorts.ToDictionary(c=>c.Id,c=>(double[])c.Admixture.Clone());
            foreach(var c in f.Cohorts.Where(c=>c.Population>=1).ToArray())
            {
                if((c.Id&63)==0)token.ThrowIfCancellationRequested();var speech=f.Languages[c.Language];var district=f.Districts[c.District];
                if(speech.OriginDistrict!=c.District&&year-c.FoundedYear>1000&&c.Connectivity<.65)
                {
                    var daughter=ForagerLanguages.Create(World.Recipe.Seed,f.NextLanguage++,c.District,year,speech);f.Languages=f.Languages.Concat(new[]{daughter}).ToArray();oldLanguages[daughter.Id]=daughter.Copy();c.Language=daughter.Id;c.LastLanguageReviewYear=year;AddForagerEvent(f,ForagerEventKind.LanguageBranch,year,c,"A locally distinct speech community is becoming established.");
                }
                var belief=f.Beliefs[c.Belief];var peer=neighbours[c.Id].OrderByDescending(p=>p.relation.Strength).FirstOrDefault();
                ForagerBeliefs.Change(World.Recipe.Seed,belief,district,year,elapsed,c.Isolation,peer.people==null?0:peer.relation.Strength,peer.people==null?null:oldBeliefs[peer.people.Belief]);
                if(c.Identity>=0&&f.Peoples[c.Identity].OriginDistrict!=c.District&&year-c.FoundedYear>5000&&c.Isolation>.7)
                {
                    var prior=f.Peoples[c.Identity];var identity=new ForagerPeopleIdentity{Id=f.NextPeople++,Parent=prior.Id,OriginDistrict=c.District,FoundedYear=year,Name=f.Cultures[c.Culture].Name,HeritageStory="Descendants of "+prior.Name+", with remembered homes in this district.",Affinities=new[]{prior.Id}};f.Peoples=f.Peoples.Concat(new[]{identity}).ToArray();c.Identity=identity.Id;
                }
                foreach(var contact in neighbours[c.Id])
                {
                    if(!languageContacts.TryGetValue(c.Language,out var list))languageContacts[c.Language]=list=new HashSet<int>();list.Add(contact.people.Language);
                    double marriage=elapsed/30000.0*contact.relation.Strength*(f.Cultures[c.Culture].MarriageMobility+f.Cultures[contact.people.Culture].MarriageMobility)*.5;
                    for(int k=0;k<c.Admixture.Length;k++)c.Admixture[k]+=marriage*(oldAdmixture[contact.people.Id][k]-c.Admixture[k]);
                }
                double sum=c.Admixture.Sum();for(int k=0;k<c.Admixture.Length;k++)c.Admixture[k]=D.Q(D.Clamp(c.Admixture[k]/Math.Max(.000001,sum)),1000000000);
                for(int k=0;k<8;k++)
                {
                    double uv=HistoryHumanVariation.UvExposure(district.Center.Z,World.ElevationM[district.Cell],(int)(district.Aridity*1000),(int)(environment[c.District].Canopy*1000));
                    c.Appearance[k]=HistoryHumanVariation.Evolve(World.Recipe.Seed,c.Id,year,(HumanAppearanceTrait)k,(int)Math.Round(c.Appearance[k]*10000),elapsed,(long)c.Population,uv,(int)(environment[c.District].MeanC*10),c.Use[14])/10000.0;
                }
            }
            // Exactly one update per language, even when multiple districts share
            // it. Mutable owner iteration never makes a widespread language age
            // faster merely because more people speak it.
            foreach(var owner in f.Cohorts.Where(c=>c.Population>=1).GroupBy(c=>c.Language).Select(g=>g.OrderByDescending(c=>c.Population).First()))
            {
                var peer=neighbours[owner.Id].Where(p=>p.people.Language!=owner.Language).OrderByDescending(p=>p.relation.Strength).FirstOrDefault();
                ForagerLanguages.Evolve(World.Recipe.Seed,f.Languages[owner.Language],year,elapsed,owner.Isolation,peer.people==null?0:peer.relation.Strength,peer.people==null?null:oldLanguages[peer.people.Language]);
            }
            ForagerLanguages.ReviewFamilies(f,year,languageContacts.ToDictionary(p=>p.Key,p=>p.Value.OrderBy(x=>x).ToArray()));
        }
    }
}
