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
            if(validate)using(ProfileWork("forager input validation"))ValidateForagers(source);if(!source.Started)throw new InvalidOperationException("Start the world first.");
            if(seconds<1||seconds>SimulationTime.Maximum-source.Foragers.Seconds||maximumEvents<1||maximumWork<1)throw new ArgumentOutOfRangeException(nameof(seconds));
            PrehistoryState s;using(ProfileWork("forager checkpoint copy"))s=source.Copy();var f=s.Foragers;long target=f.Seconds+seconds;int processed=0;var intelligibility=new Dictionary<long,double>();
            while(f.NextReviewSeconds<=target&&processed<Math.Min(maximumEvents,maximumWork))
            {
                token.ThrowIfCancellationRequested();long next=f.NextReviewSeconds;int year=(int)(next/SimulationTime.Year);double previousYear=f.BoundarySeconds/(double)SimulationTime.Year;
                using(ProfileWork("forager demography",f.Cohorts.Length))foreach(var c in f.Cohorts)
                {
                    if((c.Id&127)==0)token.ThrowIfCancellationRequested();double previous=c.Population;if(previous<=0)continue;
                    ForagerDemography.Materialize(c,5,previousYear);
                    if(c.Population==0)AddForagerEvent(f,ForagerEventKind.Extinction,year,c,"The last resident bands have disappeared from this district.");
                    else if(c.Population<previous*.7)AddForagerEvent(f,ForagerEventKind.Decline,year,c,c.WinterSecurity<.3?"Severe winter exposure has broken up the local population.":c.FoodSecurity<.5?"A food crisis has sharply reduced the resident population.":"Illness and hardship have sharply reduced the local population.");
                }
                f.Seconds=f.BoundarySeconds=next;f.NextReviewSeconds=next+5*SimulationTime.Year;
                ForagerEnvironment[] environment;using(ProfileWork("forager environments",f.Districts.Length))environment=ForagerEnvironments(f,year);
                if(year%250==0){using(ProfileWork("forager biota",f.Biota.Length))ForagerBiota.Advance(World,f,environment,year,250,token);environment=ForagerEnvironments(f,year);}
                if(year%25==0)
                {
                    using(ProfileWork("forager contacts",f.Cohorts.Length))ForagerSociality.Review(World.Recipe.Seed,f,environment,year,25,intelligibility,c=>ForagerBandHome(f,c,0).position,World.Recipe.RadiusKm);
                    using(ProfileWork("forager migration",f.Cohorts.Length))MigrateForagers(f,environment,year,25,token);
                    using(ProfileWork("forager teachers",f.Cohorts.Length))RefreshForagerTeachers(f);
                    using(ProfileWork("forager shocks",f.Cohorts.Length))ReviewForagerShocks(f,environment,year);
                }
                using(ProfileWork("forager practices",f.Cohorts.Length))LearnForagers(f,environment,year,5,token,intelligibility);
                if(year%250==0){using(ProfileWork("forager societies",f.Cohorts.Length))ChangeForagerSocieties(f,environment,year,250,token);intelligibility.Clear();}
                using(ProfileWork("forager livelihood",f.Cohorts.Length))SetForagerRates(f,environment,year,5,token);processed++;
            }
            f.Seconds=f.NextReviewSeconds<=target?Math.Min(target,f.NextReviewSeconds-1):target;
            using(ProfileWork("forager publication",f.Cohorts.Length))PublishForagers(s);if(processed==0)s.DomainToken=source.DomainToken;token.ThrowIfCancellationRequested();using(ProfileWork("forager checkpoint hash"))s.Seal();return s;
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
                // Additional bands do not monopolise every shared resource in
                // proportion to headcount. Spatially separate home ranges and
                // reciprocal access give diminishing additional claims. All
                // allocations still sum to one resource budget.
                for(int i=0;i<people.Length;i++)for(int k=0;k<5;k++)demand[k]+=ForagerEcology.ResourceClaim(people[i].Population,lives[i].Diet[k]);
                var districtDiet=new double[5];
                for(int i=0;i<people.Length;i++)
                {
                    var c=people[i];double allocated=0;
                    for(int k=0;k<5;k++){double share=demand[k]>0?ForagerEcology.ResourceClaim(c.Population,lives[i].Diet[k])/demand[k]:0;allocated+=lives[i].Capacity*lives[i].Diet[k]*share;districtDiet[k]+=c.Population/total*lives[i].Diet[k];}
                    ForagerDemography.Rates(c,lives[i],e,f.Cultures[c.Culture],allocated);
                    double speed=ForagerPassages.FrontierKmPerYear(c,f.Cultures[c.Culture],d,e);c.FrontierKm=Math.Min(d.RadiusKm*1.5,c.FrontierKm+speed*elapsed);
                    c.Occupation=D.Q(D.Clamp(c.Population/Math.Max(30,lives[i].Capacity),.005,1),1000000);
                }
                double capacity=people.Sum(c=>c.Capacity);ForagerEcology.RecoverResources(resources,elapsed,total,capacity,districtDiet);resources.HumanDensity=D.Q(total/d.AreaKm2,1000000000);resources.LastReviewYear=year;
            }
            foreach(var resources in f.Resources)if(resources.LastReviewYear!=year)
            {ForagerEcology.RecoverResources(resources,elapsed,0,1,new double[5]);resources.HumanDensity=0;resources.LastReviewYear=year;}
        }
        void LearnForagers(ForagerState f,ForagerEnvironment[] environment,int year,int elapsed,CancellationToken token,IDictionary<long,double> intelligibility)
        {
            var previous=new double[ForagerTechnology.Count];
            var people=f.Cohorts.ToDictionary(c=>c.Id);var communication=new Dictionary<int,(double score,double weight)>();
            foreach(var edge in f.Exchanges)
            {
                if(!people.TryGetValue(edge.A,out var a)||!people.TryGetValue(edge.B,out var b)||a.Population<1||b.Population<1)continue;
                long pair=((long)Math.Min(a.Language,b.Language)<<32)|(uint)Math.Max(a.Language,b.Language);
                if(!intelligibility.TryGetValue(pair,out double affinity)){affinity=ForagerLanguages.Intelligibility(f.Languages[a.Language],f.Languages[b.Language]);intelligibility[pair]=affinity;}
                double mutual=Math.Max(affinity,D.Clamp(edge.Bilingualism/Math.Max(.000001,edge.Strength))),weight=edge.Strength*(1-.75*edge.Hostility);
                communication.TryGetValue(a.Id,out var aa);communication[a.Id]=(aa.score+mutual*weight,aa.weight+weight);
                communication.TryGetValue(b.Id,out var bb);communication[b.Id]=(bb.score+mutual*weight,bb.weight+weight);
            }
            var materials=f.Cohorts.Where(c=>c.Population>=1).Select(c=>c.District).Distinct().ToDictionary(id=>id,id=>Enumerable.Range(0,ForagerTechnology.Count).Select(k=>ForagerTechnology.Materials(f.Districts[id],environment[id],k)).ToArray());
            foreach(var c in f.Cohorts.Where(c=>c.Population>=1))
            {
                if((c.Id&63)==0)token.ThrowIfCancellationRequested();Array.Copy(c.Skill,previous,previous.Length);
                communication.TryGetValue(c.Id,out var access);double comprehension=access.weight>0?access.score/access.weight:1;
                ulong inventions=ForagerTechnology.Advance(World.Recipe.Seed,c,f.Districts[c.District],environment[c.District],f.Cultures[c.Culture],elapsed,year,c.TeachingFrom,c.TeachingAdults,c.ContactTeaching,f.Languages[c.Language].TechnicalRegister,ForagerSociality.Capacity(f,c,false),ForagerSociality.Capacity(f,c,true),materials[c.District],comprehension);
                for(int k=0;k<previous.Length;k++)
                {
                    if(c.Knowledge[k]>=.2&&c.FirstKnownYear[k]<0)c.FirstKnownYear[k]=year;
                    if(c.Skill[k]>=.5&&c.FirstSkilledYear[k]<0)c.FirstSkilledYear[k]=year;
                    if((inventions&(1UL<<k))!=0)f.Discoveries=f.Discoveries.Concat(new[]{new ForagerDiscovery{Id=f.Discoveries.Length,Technique=k,Cohort=c.Id,District=c.District,Year=year}}).ToArray();
                    if(previous[k]<.5&&c.Skill[k]>=.5)AddForagerEvent(f,ForagerEventKind.Innovation,year,c,ForagerTechnology.All[k].Name+" is becoming a trained local practice.");
                    if(previous[k]>=.25&&c.Skill[k]<.25){c.LastLossYear[k]=year;AddForagerEvent(f,ForagerEventKind.KnowledgeLoss,year,c,"Trained practice in "+ForagerTechnology.All[k].Name.ToLowerInvariant()+" has become scarce.");}
                }
            }
        }
        void RefreshForagerTeachers(ForagerState f)
        {
            // Contact opportunities are reviewed every25 years; individual
            // familiarity/practice continues on the five-year demographic
            // clock. Save the incoming context so reloads and caller budgets
            // cannot alter who teaches whom between network reviews.
            var neighbours=ForagerSociality.Neighbours(f);
            foreach(var c in f.Cohorts.Where(c=>c.Population>=1))
            {
                double weight=0,adults=0;
                c.TeachingFrom=ForagerTechnology.TeacherContext(neighbours[c.Id].Where(x=>x.people.Population>=1).Select(x=>(x.people.Skill,x.relation.Strength*(1-.75*x.relation.Hostility))));
                foreach(var contact in neighbours[c.Id])
                {
                    if(contact.people.Population<1)continue;double strength=contact.relation.Strength*(1-.75*contact.relation.Hostility);weight+=strength;adults+=contact.people.Adults*strength;
                }
                c.ContactTeaching=D.Q(weight,1000000);c.TeachingAdults=D.Q(adults,1000000);
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
            var oldSpeech=f.Cohorts.ToDictionary(c=>c.Id,c=>c.Speech.Select(x=>x.Copy()).ToArray());var oldTraditions=f.Cohorts.ToDictionary(c=>c.Id,c=>c.Traditions.Select(x=>x.Copy()).ToArray());
            var oldLanguages=f.Languages.Select(l=>l.Copy()).ToDictionary(l=>l.Id);var oldBeliefs=f.Beliefs.Select(b=>b.Copy()).ToDictionary(b=>b.Id);var oldCultures=f.Cultures.Select(x=>x.Copy()).ToArray();
            foreach(var c in f.Cohorts.Where(c=>c.Population>=1).ToArray())
            {
                if((c.Id&63)==0)token.ThrowIfCancellationRequested();var speech=f.Languages[c.Language];var district=f.Districts[c.District];
                double speechDistance=DistanceKm(district.Center,f.Districts[speech.OriginDistrict].Center);
                double speechHomelandTies=neighbours[c.Id].Where(p=>p.people.Language==c.Language&&DistanceKm(f.Districts[p.people.District].Center,f.Districts[speech.OriginDistrict].Center)<250).Sum(p=>p.relation.Strength);
                if(c.Population>=60&&speech.OriginDistrict!=c.District&&year-c.FoundedYear>1500&&year-c.LastLanguageReviewYear>2000&&speechDistance>250&&speechHomelandTies<.1)
                {
                    double regionalSpeakers=c.Population*ForagerAffiliations.Share(c.Speech,speech.Id)+neighbours[c.Id].Where(p=>p.relation.Strength>.025&&DistanceKm(district.Center,f.Districts[p.people.District].Center)<350).Sum(p=>p.people.Population*ForagerAffiliations.Share(p.people.Speech,speech.Id));
                    bool regionalCommunity=regionalSpeakers>=300||c.Isolation>.6&&year-c.LastLanguageReviewYear>4000;
                    if(regionalCommunity)
                    {
                    var localVariants=neighbours[c.Id].Where(p=>p.relation.Strength>.025&&DistanceKm(district.Center,f.Districts[p.people.District].Center)<350).SelectMany(p=>p.people.Speech.Select(x=>(id:x.Id,number:x.Share*p.people.Population))).Concat(c.Speech.Select(x=>(id:x.Id,number:x.Share*c.Population)));
                    bool shared=localVariants.Where(x=>x.id!=speech.Id&&f.Languages[x.id].Parent==speech.Id&&ForagerLanguages.SharedRoots(speech,f.Languages[x.id])>.42).GroupBy(x=>x.id).Any(g=>g.Sum(x=>x.number)>=5);
                    if(!shared)
                    {
                        var daughter=ForagerLanguages.Create(World.Recipe.Seed,f.NextLanguage++,c.District,year,speech);f.Languages=f.Languages.Concat(new[]{daughter}).ToArray();oldLanguages[daughter.Id]=daughter.Copy();
                        // A newly recorded regional dialect initially has the same
                        // words. It evolves locally afterwards; registering it is
                        // not an overnight replacement of everyone's vocabulary.
                        foreach(var local in f.Cohorts.Where(p=>p.Population>=1&&p.Language==speech.Id&&year-p.FoundedYear>750&&DistanceKm(district.Center,f.Districts[p.District].Center)<250&&DistanceKm(f.Districts[p.District].Center,f.Districts[speech.OriginDistrict].Center)>250))
                        {local.Speech=ForagerAffiliations.Replace(local.Speech,speech.Id,daughter.Id);ForagerAffiliations.Refresh(local);local.LastLanguageReviewYear=year;}
                        AddForagerEvent(f,ForagerEventKind.LanguageBranch,year,c,"A regional speech variety is becoming established across neighbouring camps.");
                    }
                    // Joining an already distinct local language uses gradual
                    // affiliation change below, rather than an instant relabel.
                    ForagerAffiliations.Refresh(c);c.LastLanguageReviewYear=year;
                    }
                }
                var peer=neighbours[c.Id].OrderByDescending(p=>p.relation.Strength).FirstOrDefault();
                using(ProfileWork("forager culture",1))ChangeForagerCulture(f,c,environment[c.District],year,elapsed,peer.people==null?null:oldCultures[peer.people.Culture],peer.people==null?0:peer.relation.Strength);
                using(ProfileWork("forager affiliations",1))ReviewForagerAffiliations(f,c,neighbours[c.Id],oldSpeech,oldTraditions,year,elapsed);
                if(c.Identity>=0&&f.Peoples[c.Identity].OriginDistrict!=c.District&&year-c.FoundedYear>3000)
                {
                    var prior=f.Peoples[c.Identity];double originDistance=DistanceKm(district.Center,f.Districts[prior.OriginDistrict].Center);
                    double homelandTies=neighbours[c.Id].Where(p=>p.people.Identity==c.Identity&&DistanceKm(f.Districts[p.people.District].Center,f.Districts[prior.OriginDistrict].Center)<600).Sum(p=>p.relation.Strength);
                    if(originDistance>600&&homelandTies<.05)
                    {
                        var relative=neighbours[c.Id].Where(p=>f.Peoples[p.people.Identity].Parent==prior.Id&&DistanceKm(district.Center,f.Districts[p.people.District].Center)<400).OrderByDescending(p=>p.relation.Strength).FirstOrDefault();
                        if(relative.people!=null)c.Identity=relative.people.Identity;
                        else{var identity=new ForagerPeopleIdentity{Id=f.NextPeople++,Parent=prior.Id,OriginDistrict=c.District,FoundedYear=year,Name=f.Cultures[c.Culture].Name,HeritageStory="Descendants of "+prior.Name+", with remembered homes in this district.",Affinities=new[]{prior.Id}};f.Peoples=f.Peoples.Concat(new[]{identity}).ToArray();c.Identity=identity.Id;}
                    }
                }
                foreach(var contact in neighbours[c.Id])
                {
                    foreach(var local in c.Speech.Where(x=>x.Share>.05))
                    {if(!languageContacts.TryGetValue(local.Id,out var list))languageContacts[local.Id]=list=new HashSet<int>();foreach(var other in contact.people.Speech.Where(x=>x.Share>.05))list.Add(other.Id);}
                }
                for(int k=0;k<8;k++)
                {
                    double uv=HistoryHumanVariation.UvExposure(district.Center.Z,World.ElevationM[district.Cell],(int)(district.Aridity*1000),(int)(environment[c.District].Canopy*1000));
                    c.Appearance[k]=HistoryHumanVariation.Evolve(World.Recipe.Seed,c.Id,year,(HumanAppearanceTrait)k,(int)Math.Round(c.Appearance[k]*10000),elapsed,(long)c.Population,uv,(int)(environment[c.District].MeanC*10),c.Use[14])/10000.0;
                }
            }
            using(ProfileWork("forager ancestry",f.Exchanges.Length))ForagerAncestry.Exchange(f,elapsed);
            foreach(var c in f.Cohorts.Where(c=>c.Population>=1))
            {
                int predominant=Array.IndexOf(c.Admixture,c.Admixture.Max());
                if(predominant!=c.Lineage&&c.Admixture[predominant]>.7)
                {c.Lineage=predominant;AddForagerEvent(f,ForagerEventKind.Admixture,year,c,"The resident population now has predominantly "+f.Lineages[predominant].Name+" ancestry; earlier ancestry remains represented.");}
            }
            // A shared tradition ages once, rather than once per community
            // which happens to practise it. Local branches spread through a
            // changing minority share before becoming the local majority.
            var beliefOwners=f.Cohorts.Where(c=>c.Population>=1).SelectMany(c=>c.Traditions.Select(x=>(id:x.Id,people:c,weight:c.Population*x.Share))).GroupBy(x=>x.id).Select(g=>g.OrderByDescending(x=>x.weight).First());
            using(ProfileWork("forager belief change",f.Beliefs.Length))foreach(var item in beliefOwners)
            {
                var c=item.people;var peer=neighbours[c.Id].Where(p=>p.people.Belief!=item.id).OrderByDescending(p=>p.relation.Strength).FirstOrDefault();
                ForagerBeliefs.Change(World.Recipe.Seed,f.Beliefs[item.id],f.Districts[c.District],year,elapsed,c.Isolation,peer.people==null?0:peer.relation.Strength,peer.people==null?null:oldBeliefs.TryGetValue(peer.people.Belief,out var prior)?prior:null);
            }
            // Exactly one update per language, even when multiple districts share
            // it. Mutable owner iteration never makes a widespread language age
            // faster merely because more people speak it.
            using(ProfileWork("forager speech change",f.Languages.Length))foreach(var speechOwner in f.Cohorts.Where(c=>c.Population>=1).SelectMany(c=>c.Speech.Select(x=>(id:x.Id,people:c,weight:c.Population*x.Share))).GroupBy(x=>x.id).Select(g=>g.OrderByDescending(x=>x.weight).First()))
            {
                var owner=speechOwner.people;int languageId=speechOwner.id;
                var peer=neighbours[owner.Id].Where(p=>p.people.Language!=languageId).OrderByDescending(p=>p.relation.Strength).FirstOrDefault();
                ForagerLanguages.Evolve(World.Recipe.Seed,f.Languages[languageId],year,elapsed,owner.Isolation,peer.people==null?0:peer.relation.Strength,peer.people==null?null:oldLanguages[peer.people.Language]);
                var language=f.Languages[languageId];var culture=f.Cultures[owner.Culture];
                double potential=.25+.75*ForagerSociality.Capacity(f,owner,true);
                double technical=D.Clamp(.2+.032*owner.Skill.Count(x=>x>.5)+.25*owner.Connectivity+.15*culture.ChildcareCooperation);
                double narrative=D.Clamp(.35+.3*owner.Connectivity+.2*culture.GuestAccess+.15*owner.Use[5]+.15*f.Beliefs[owner.Belief].CollectiveRites);
                double ritual=D.Clamp(.2+.55*f.Beliefs[owner.Belief].CollectiveRites+.2*owner.Connectivity);
                language.TechnicalRegister=D.Q(PaleolithicRules.Approach(language.TechnicalRegister,technical*potential,1/12000d,elapsed),1000000);
                language.NarrativePractice=D.Q(PaleolithicRules.Approach(language.NarrativePractice,narrative*potential,1/15000d,elapsed),1000000);
                language.RitualRegister=D.Q(PaleolithicRules.Approach(language.RitualRegister,ritual*potential,1/15000d,elapsed),1000000);
            }
            using(ProfileWork("forager family change",f.Languages.Length))ForagerLanguages.ReviewFamilies(f,year,languageContacts.ToDictionary(p=>p.Key,p=>p.Value.OrderBy(x=>x).ToArray()));
        }
        void ReviewForagerAffiliations(ForagerState f,ForagerCohort c,List<(ForagerCohort people,ForagerExchange relation)> contacts,
            Dictionary<int,ForagerAffiliation[]> oldSpeech,Dictionary<int,ForagerAffiliation[]> oldTraditions,int year,int elapsed)
        {
            var culture=f.Cultures[c.Culture];double exposure=contacts.Sum(p=>p.relation.Strength*(1-.8*p.relation.Hostility));
            var speechNorm=ForagerAffiliations.SpeechTarget(c.Speech,c.Population,contacts.Select(p=>(oldSpeech[p.people.Id],p.people.Population,p.relation.Strength*(1-.8*p.relation.Hostility))),culture.GuestAccess);
            double shift=1-Math.Exp(-elapsed*D.Clamp(exposure)*(.25+.75*culture.GuestAccess)/1400d);
            c.Speech=ForagerAffiliations.Approach(c.Speech,speechNorm,shift,c.Population);
            var belief=f.Beliefs[c.Belief];bool migrant=belief.OriginDistrict!=c.District;
            bool existingDaughter=c.Traditions.Any(x=>f.Beliefs[x.Id].Parent==belief.Id&&f.Beliefs[x.Id].OriginDistrict==c.District);
            if(!existingDaughter&&c.Population>=35&&year-belief.FoundedYear>1500&&year-c.FoundedYear>1000&&Rng.Value(World.Recipe.Seed,"local-belief-branch",c.Id^year)<1-Math.Exp(-elapsed*(.3+c.Isolation)/(migrant?3500d:12000d)))
            {
                var daughter=ForagerBeliefs.Create(World.Recipe.Seed,f.NextBelief++,c.District,year,f.Districts[c.District],f.Languages[c.Language],belief);
                f.Beliefs=f.Beliefs.Concat(new[]{daughter}).ToArray();c.Traditions=ForagerAffiliations.Transfer(c.Traditions,belief.Id,daughter.Id,.02);
                AddForagerEvent(f,ForagerEventKind.BeliefBranch,year,c,"Some households are developing a distinct local ritual tradition.");
            }
            var traditionNorm=ForagerAffiliations.SpeechTarget(c.Traditions,c.Population,contacts.Select(p=>(oldTraditions[p.people.Id],p.people.Population,p.relation.Strength*.5*(1-p.relation.Hostility))),culture.GuestAccess*.5);
            var emerging=c.Traditions.Where(x=>f.Beliefs[x.Id].Parent==belief.Id&&f.Beliefs[x.Id].OriginDistrict==c.District).OrderByDescending(x=>x.Share).FirstOrDefault();
            if(emerging!=null)traditionNorm=ForagerAffiliations.Blend(traditionNorm,.2,ForagerAffiliations.Single(emerging.Id),.8);
            c.Traditions=ForagerAffiliations.Approach(c.Traditions,traditionNorm,1-Math.Exp(-elapsed*(emerging!=null?1:.2+.4*D.Clamp(exposure))/1800d),c.Population);
            ForagerAffiliations.Refresh(c);
        }
        void ChangeForagerCulture(ForagerState f,ForagerCohort people,ForagerEnvironment e,int year,int elapsed,ForagerCulture donor,double contact)
        {
            var c=f.Cultures[people.Culture];var d=f.Districts[people.District];
            double Change(string field,double value,double target,double peer)
            {
                double drift=(Rng.Value(World.Recipe.Seed,"forager-social-drift-"+field,c.Id^year)-.5)*.025;
                return D.Q(D.Clamp(PaleolithicRules.Approach(value,target,1/12000d,elapsed)+(peer-value)*D.Clamp(contact*elapsed/14000d)+drift),1000000);
            }
            c.FoodSharing=Change("sharing",c.FoodSharing,.65+.25*D.Clamp(1-people.FoodSecurity),donor?.FoodSharing??c.FoodSharing);
            c.GuestAccess=Change("guests",c.GuestAccess,.4+.3*people.Connectivity-.25*people.ConflictPressure,donor?.GuestAccess??c.GuestAccess);
            c.ExchangeOpenness=Change("exchange",c.ExchangeOpenness,.35+.35*people.Connectivity+.15*D.Clamp(1-d.Stone),donor?.ExchangeOpenness??c.ExchangeOpenness);
            c.MarriageMobility=Change("marriage",c.MarriageMobility,.6+.25*people.Isolation,donor?.MarriageMobility??c.MarriageMobility);
            c.KinObligation=Change("kin",c.KinObligation,.6+.2*people.Isolation,donor?.KinObligation??c.KinObligation);
            c.ChildcareCooperation=Change("childcare",c.ChildcareCooperation,.7+.15*c.FoodSharing,donor?.ChildcareCooperation??c.ChildcareCooperation);
            c.ConflictAvoidance=Change("avoidance",c.ConflictAvoidance,.6+.2*people.ConflictPressure,donor?.ConflictAvoidance??c.ConflictAvoidance);
            c.PrestigeCompetition=Change("prestige",c.PrestigeCompetition,.25+.25*people.Connectivity,donor?.PrestigeCompetition??c.PrestigeCompetition);
            for(int k=0;k<5;k++)c.FoodPreference[k]=Change("diet-"+k,c.FoodPreference[k],D.Clamp(.25+people.Diet[k]*1.5),donor?.FoodPreference[k]??c.FoodPreference[k]);
            for(int k=0;k<ForagerTechnology.Count;k++)c.TechniquePreference[k]=Change("practice-"+k,c.TechniquePreference[k],.25+.5*ForagerTechnology.Demand(d,e,people,k),donor?.TechniquePreference[k]??c.TechniquePreference[k]);
            var desired=people.Diet[3]+people.Diet[4]>.48&&d.Seasonality[3]<.6?ForagerResidence.AquaticResidence:d.Seasonality[0]>.55||people.Use[32]>.35?ForagerResidence.CollectorCamps:people.Use[23]+people.Use[28]>.9?ForagerResidence.SeasonalBaseCamps:ForagerResidence.ResidentialMobility;
            if(c.Residence!=desired&&Rng.Value(World.Recipe.Seed,"forager-residence-change",c.Id^year)<1-Math.Exp(-elapsed/1800d))c.Residence=desired;
        }
    }
}
