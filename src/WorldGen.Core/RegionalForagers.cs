using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        public const int RegionalReviewYears=100;

        // Regional state changes on broad historical boundaries. A fine clock
        // changes two wrappers and a small clock digest, never the frozen data.
        PrehistoryState AdvanceRegionalForagers(PrehistoryState source,long seconds,int maximumEvents,CancellationToken token,bool validate,int maximumWork)
        {
            if(validate)ValidateForagers(source);
            if(!source.Started)throw new InvalidOperationException("Start the world first.");
            if(seconds<1||seconds>SimulationTime.Maximum-source.Foragers.Seconds||maximumEvents<1||maximumWork<1)throw new ArgumentOutOfRangeException(nameof(seconds));
            token.ThrowIfCancellationRequested();long target=source.Foragers.Seconds+seconds;
            if(target<source.Foragers.NextReviewSeconds)
                return RegionalClock(source,target);
            PrehistoryState s;using(ProfileWork("regional event copy"))s=source.Copy();
            var f=s.Foragers;var intelligibility=new Dictionary<long,double>();int processed=0;
            using var journal=new ForagerJournal(f);
            while(f.NextReviewSeconds<=target&&processed<Math.Min(maximumEvents,maximumWork))
            {
                token.ThrowIfCancellationRequested();long next=f.NextReviewSeconds;int year=(int)(next/SimulationTime.Year);
                using(ProfileWork("regional population",f.Cohorts.Length))foreach(var c in f.Cohorts)
                    if(c.Population>0)ForagerDemography.Materialize(c,RegionalReviewYears,f.BoundarySeconds/(double)SimulationTime.Year);
                f.Seconds=f.BoundarySeconds=next;f.NextReviewSeconds=next+RegionalReviewYears*SimulationTime.Year;
                var environment=ForagerEnvironments(f,year);
                if(year%1000==0)
                {
                    using(ProfileWork("regional ecology",f.Districts.Length))ForagerBiota.Advance(World,f,environment,year,1000,token);
                    environment=ForagerEnvironments(f,year);
                }
                using(ProfileWork("regional contacts",f.Districts.Length))RegionalContacts(f,environment,year,RegionalReviewYears);
                using(ProfileWork("regional migration",f.Districts.Length))MigrateForagers(f,environment,year,RegionalReviewYears,token);
                RegionalContacts(f,environment,year,RegionalReviewYears);RefreshForagerTeachers(f);
                using(ProfileWork("regional technology",f.Cohorts.Length))LearnForagers(f,environment,year,RegionalReviewYears,token,intelligibility);
                if(year%500==0)
                {
                    using(ProfileWork("regional culture",f.Cohorts.Length))RegionalSocieties(f,environment,year,500,token);
                    intelligibility.Clear();
                }
                using(ProfileWork("regional resource rates",f.Cohorts.Length))SetForagerRates(f,environment,year,RegionalReviewYears,token);
                processed++;
            }
            long final=f.NextReviewSeconds<=target?Math.Min(target,f.NextReviewSeconds-1):target;
            f.Seconds=f.BoundarySeconds;
            journal.Flush();PublishForagers(s);token.ThrowIfCancellationRequested();using(ProfileWork("regional domain seal"))s.Seal();return final==f.Seconds?s:RegionalClock(s,final);
        }

        static PrehistoryState RegionalClock(PrehistoryState source,long target)
        {
            var clock=source.CopyClockOnly();clock.Foragers=source.Foragers.ClockCopy();clock.Paleolithic=source.Paleolithic.Copy();
            clock.Foragers.Seconds=target;clock.Paleolithic.Seconds=target;clock.Years=(int)(target/SimulationTime.Year);
            clock.Hash=clock.RegionalClockHash(clock.Foragers.DomainHash);return clock;
        }

        // A sparse ecoregion adjacency graph. No band placement, household
        // generation, all-pairs tribe search or persistent camp moves here.
        void RegionalContacts(ForagerState f,ForagerEnvironment[] environment,int year,int elapsed)
        {
            var byRegion=f.Cohorts.Where(c=>c.Population>=1).ToDictionary(c=>c.District);
            var contacts=new List<ForagerExchange>();
            foreach(var c in byRegion.Values)c.MatingContacts=c.Connectivity=0;
            foreach(var c in byRegion.Values)
            {
                var d=f.Districts[c.District];
                foreach(var group in RegionalPaths(f.Districts)[c.District])
                {
                    if(!byRegion.TryGetValue(group[0].To,out var other)||c.Id>=other.Id)continue;
                    var pass=group.FirstOrDefault(p=>ForagerPassages.Open(World.Recipe.Seed,p,d,environment[d.Id],environment[p.To],c,true));if(pass==null)continue;
                    double distance=ForagerPassages.TravelCostKm(pass,environment[d.Id],environment[pass.To]);
                    double access=(f.Cultures[c.Culture].ExchangeOpenness+f.Cultures[other.Culture].ExchangeOpenness)*.5;
                    double strength=D.Clamp((.08+.55*access)*Math.Exp(-distance/500)*(pass.Sea?.2:1));
                    if(strength<.002)continue;
                    contacts.Add(new ForagerExchange{A=c.Id,B=other.Id,SinceYear=year,Strength=strength,Hospitality=access,Trade=strength,Kinship=c.Identity==other.Identity?.6:.1,Bilingualism=strength*.5});
                    c.Connectivity+=strength;other.Connectivity+=strength;c.MatingContacts+=other.Adults*strength*.01;other.MatingContacts+=c.Adults*strength*.01;
                }
            }
            foreach(var c in byRegion.Values){c.Connectivity=D.Clamp(c.Connectivity);c.Isolation=1-c.Connectivity;c.BilingualShare=D.Clamp(c.Connectivity*.4);}
            f.Exchanges=contacts.ToArray();
        }

        void RegionalSocieties(ForagerState f,ForagerEnvironment[] environment,int year,int elapsed,CancellationToken token)
        {
            var neighbours=ForagerSociality.Neighbours(f);
            var oldCultures=f.Cultures.Select(c=>c.Copy()).ToArray();var oldLanguages=f.Languages.Select(l=>l.Copy()).ToArray();var oldBeliefs=f.Beliefs.Select(b=>b.Copy()).ToArray();
            // Each region owns a fixed language/tradition profile. Dialects,
            // households and their local preferences are procedural queries.
            var languageOwners=f.Cohorts.Where(c=>c.Population>=1).GroupBy(c=>c.Language).Select(g=>g.OrderByDescending(c=>c.Population).First()).ToArray();
            var beliefOwners=f.Cohorts.Where(c=>c.Population>=1).GroupBy(c=>c.Belief).Select(g=>g.OrderByDescending(c=>c.Population).First()).ToArray();
            foreach(var c in f.Cohorts.Where(c=>c.Population>=1))
            {
                token.ThrowIfCancellationRequested();var peer=neighbours[c.Id].OrderByDescending(x=>x.relation.Strength).FirstOrDefault();
                ChangeForagerCulture(f,c,environment[c.District],year,elapsed,peer.people==null?null:oldCultures[peer.people.Culture],peer.relation?.Strength??0);
                // Local convention is a distribution, not a list of actual
                // families converted, born, moved or tracked by the simulation.
                var localLanguage=f.Languages.First(l=>l.OriginDistrict==c.District);
                c.Speech=ForagerAffiliations.Approach(c.Speech,ForagerAffiliations.Single(localLanguage.Id),.15,c.Population);
                ForagerAffiliations.Refresh(c);
                int primary=0;for(int k=1;k<f.Lineages.Length;k++)if(f.SpeciesShare(c,k)>f.SpeciesShare(c,primary))primary=k;c.Lineage=primary;
            }
            foreach(var c in languageOwners)
            {
                var language=f.Languages[c.Language];var peer=neighbours[c.Id].Where(x=>x.people.Language!=c.Language).OrderByDescending(x=>x.relation.Strength).FirstOrDefault();
                double contact=peer.relation?.Strength??0;
                ForagerLanguages.Evolve(World.Recipe.Seed,language,year,elapsed,c.Isolation,contact,peer.people==null?null:oldLanguages[peer.people.Language]);
                double potential=.25+.75*ForagerSociality.Capacity(f,c,true);
                language.TechnicalRegister=D.Q(PaleolithicRules.Approach(language.TechnicalRegister,potential*D.Clamp(.3+.02*c.Skill.Count(x=>x>.1)+.2*c.Connectivity),1/12000d,elapsed),1000000);
                language.NarrativePractice=D.Q(PaleolithicRules.Approach(language.NarrativePractice,potential*D.Clamp(.6+.3*c.Connectivity),1/15000d,elapsed),1000000);
            }
            foreach(var c in beliefOwners)
            {
                var peer=neighbours[c.Id].Where(x=>x.people.Belief!=c.Belief).OrderByDescending(x=>x.relation.Strength).FirstOrDefault();
                ForagerBeliefs.Change(World.Recipe.Seed,f.Beliefs[c.Belief],f.Districts[c.District],year,elapsed,c.Isolation,peer.relation?.Strength??0,peer.people==null?null:oldBeliefs[peer.people.Belief]);
            }
            ForagerAncestry.Exchange(f,elapsed);
            ForagerLanguages.ReviewFamilies(f,year,null);CompactRegionalFamilies(f);
        }

        // Preserve living recognisable families plus a bounded ancestry
        // summary. Never accumulate full dead demographic/language actors.
        static void CompactRegionalFamilies(ForagerState f)
        {
            int limit=Math.Max(16,f.Districts.Length*2);if(f.LanguageFamilies.Length<=limit)return;
            var keep=new HashSet<int>(f.Languages.Select(l=>l.Family));
            foreach(var family in f.LanguageFamilies.OrderByDescending(x=>x.FoundedYear))
                if(keep.Count<limit&&family.Parent>=0)keep.Add(family.Parent);
            var selected=keep.OrderBy(x=>x).Select(id=>f.LanguageFamilies[id]).ToArray();var remap=selected.Select((x,i)=>(x.Id,i)).ToDictionary(x=>x.Id,x=>x.i);
            foreach(var language in f.Languages)language.Family=remap[language.Family];
            foreach(var family in selected){int parent=family.Parent;family.Id=remap[family.Id];family.Parent=parent>=0&&remap.TryGetValue(parent,out int id)?id:-1;}
            f.LanguageFamilies=selected;f.NextFamily=selected.Length;
        }
    }
}
