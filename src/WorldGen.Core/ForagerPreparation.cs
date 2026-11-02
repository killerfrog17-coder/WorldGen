using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        public static bool Foraging(PrehistoryState state)=>state?.Foragers!=null;
        public PrehistoryState PrepareForagers(int origin=-1,CancellationToken token=default)
        {
            bool automatic=origin<0;if(automatic)origin=ChooseForagerOrigin();
            if(origin<0||origin>=Patches.Length||Patches[origin].Capacity<25)throw new ArgumentException("Choose a viable ancestral land region.");
            var s=new PrehistoryState{Version="prehistory-8",Origin=origin,WorldHash=World.Stages.Last().Hash,Population=new long[Patches.Length],ArrivalYear=Enumerable.Repeat(-1,Patches.Length).ToArray(),Ancestry=new int[Patches.Length*8],Paleolithic=new PaleolithicState{Version="paleolithic-3"},Foragers=new ForagerState{NextReviewSeconds=5*SimulationTime.Year},Society=new SocietyState{Knowledge=new int[Patches.Length],CellCulture=Enumerable.Repeat(-1,World.Surface.Cells.Length).ToArray()}};
            BuildForagerTopology(s.Foragers,token);var f=s.Foragers;
            int originDistrict=f.CellDistrict[Patches[origin].Cell];if(originDistrict<0)originDistrict=f.Districts.Where(d=>d.Patch==origin).Select(d=>d.Id).DefaultIfEmpty(-1).First();
            if(originDistrict<0)throw new ArgumentException("The selected region has no habitable land district.");
            var sapiens=SeedForagerLineages(f,originDistrict);
            f.Resources=f.Districts.Select(d=>new ForagerResources{District=d.Id}).ToArray();f.Biota=ForagerBiota.Seed(World,f,token);var people=new List<ForagerCohort>();
            var distances=f.Lineages.Select(l=>ForagerDistances(f,l.OriginDistrict,true,true).costs).ToArray();
            foreach(var district in f.Districts.OrderBy(d=>d.Id))
            {
                token.ThrowIfCancellationRequested();int lineage=-1;
                if(sapiens.Contains(district.Id))lineage=0;
                else
                {
                    double nearest=double.PositiveInfinity;
                    for(int k=1;k<f.Lineages.Length;k++)if(distances[k][district.Id]<nearest&&distances[k][district.Id]<4200000){nearest=distances[k][district.Id];lineage=k;}
                }
                if(lineage<0)continue;
                int priorCohort=f.NextCohort,priorCulture=f.Cultures.Length,priorLanguage=f.Languages.Length,priorPeople=f.Peoples.Length;
                var cohort=NewForagerCohort(f,district.Id,lineage,0);
                void DiscardCandidate()
                {
                    // Testing habitat viability must not fabricate historical
                    // cultures/languages/peoples for camps that never existed.
                    f.NextCohort=priorCohort;f.Cultures=f.Cultures.Take(priorCulture).ToArray();f.NextCulture=priorCulture;
                    f.Languages=f.Languages.Take(priorLanguage).ToArray();f.NextLanguage=priorLanguage;f.Peoples=f.Peoples.Take(priorPeople).ToArray();f.NextPeople=priorPeople;
                }
                var nearby=people.Where(c=>c.Lineage==lineage).Select(c=>(people:c,distance:DistanceKm(f.Districts[c.District].Center,district.Center))).Where(x=>x.distance<650).OrderBy(x=>x.distance).FirstOrDefault();
                if(nearby.people!=null&&Rng.Value(World.Recipe.Seed,"initial-speech-relatedness",district.Id)>.35)
                {
                    var speech=ForagerLanguages.Create(World.Recipe.Seed,cohort.Language,district.Id,0,f.Languages[nearby.people.Language]);
                    ForagerLanguages.Evolve(World.Recipe.Seed,speech,0,500+(int)(2500*Rng.Value(World.Recipe.Seed,"prior-speech-separation",district.Id)),.6,.15);
                    f.Languages[cohort.Language]=speech;
                }
                var environment=ForagerBiota.Apply(ForagerEcology.At(World.Recipe.Seed,district,0,World.ElevationM[district.Cell]),f.Biota[district.Id]);
                var life=ForagerEcology.Livelihood(district,environment,f.Lineages[lineage],cohort,f.Cultures[cohort.Culture],f.Resources[district.Id]);
                if(life.Capacity<35||life.WinterSecurity<.07){DiscardCandidate();continue;}
                double population=life.Capacity*(.14+.25*Rng.Value(World.Recipe.Seed,"ancestral-regional-density",district.Id));
                if(population<18){DiscardCandidate();continue;}
                ForagerDemography.SetPopulation(cohort,population);cohort.Occupation=D.Clamp(population/life.Capacity,.03,1);cohort.FrontierKm=Math.Max(15,district.RadiusKm*cohort.Occupation);
                cohort.FoundedYear=-3000-(int)(12000*Rng.Value(World.Recipe.Seed,"ancestral-residence",district.Id));cohort.LastMigrationYear=cohort.FoundedYear;
                var belief=ForagerBeliefs.Create(World.Recipe.Seed,f.NextBelief++,district.Id,0,district,f.Languages[cohort.Language],nearby.people==null?null:f.Beliefs[nearby.people.Belief]);
                f.Beliefs=f.Beliefs.Concat(new[]{belief}).ToArray();cohort.Traditions=ForagerAffiliations.Single(belief.Id);ForagerAffiliations.Refresh(cohort);
                ForagerDemography.Rates(cohort,life,environment,f.Cultures[cohort.Culture],life.Capacity);people.Add(cohort);
            }
            f.Cohorts=people.OrderBy(c=>c.Id).ToArray();
            f.InitialPopulation=f.Cohorts.Sum(c=>c.Population);
            if(!f.Cohorts.Any(c=>f.Lineages[c.Lineage].PlayerSpecies))throw new InvalidOperationException("The ancestral region cannot support established sapiens under the starting climate.");
            if(!f.Cohorts.Any(c=>f.Lineages[c.Lineage].PlayerSpecies&&f.Districts[c.District].Patch==s.Origin))
            {
                if(!automatic)throw new InvalidOperationException("Choose a warmer or better watered starting region.");
                s.Origin=f.Districts[f.Cohorts.Where(c=>f.Lineages[c.Lineage].PlayerSpecies).OrderByDescending(c=>c.Population).First().District].Patch;
            }
            ForagerLanguages.ReviewFamilies(f,0,null);
            var environments=ForagerEnvironments(f,0);ForagerSociality.Review(World.Recipe.Seed,f,environments,0,200,home:c=>ForagerBandHome(f,c,0).position,radiusKm:World.Recipe.RadiusKm);RefreshForagerTeachers(f);
            PublishForagers(s);s.Seal();return s;
        }
        double DistanceKm(V a,V b)=>World.Recipe.RadiusKm*Math.Acos(D.Clamp(V.Dot(a,b),-1,1));
        ForagerEnvironment[] ForagerEnvironments(ForagerState f,double year)=>f.Districts.Select(d=>ForagerBiota.Apply(ForagerEcology.At(World.Recipe.Seed,d,year,World.ElevationM[d.Cell]),f.Biota.Length>d.Id?f.Biota[d.Id]:null)).ToArray();
        public PrehistoryState StartForagers(PrehistoryState source)
        {ValidateForagers(source);if(source.Started)return source;var s=source.Copy();s.Started=true;s.Seal();return s;}
        void PublishForagers(PrehistoryState s)
        {
            var f=s.Foragers;double elapsed=(f.Seconds-f.BoundarySeconds)/(double)SimulationTime.Year;Array.Clear(s.Population,0,s.Population.Length);Array.Clear(s.Ancestry,0,s.Ancestry.Length);Array.Fill(s.Society.CellCulture,-1);
            var culturePopulation=new double[f.Cultures.Length];var cultureArea=new double[f.Cultures.Length];var patchTraits=new double[Patches.Length*8];
            var dominant=new Dictionary<int,(ForagerCohort people,double population)>();
            foreach(var c in f.Cohorts)
            {
                if(c.Population<=0)continue;
                double population=elapsed>0?ForagerDemography.Project(c,elapsed,f.BoundarySeconds/(double)SimulationTime.Year).Population:c.Population;if(population<.5)continue;
                var district=f.Districts[c.District];int patch=district.Patch;
                s.Population[patch]+=(long)Math.Round(population);if(s.ArrivalYear[patch]<0)s.ArrivalYear[patch]=Math.Max(0,c.FoundedYear);
                culturePopulation[c.Culture]+=population;cultureArea[c.Culture]+=district.AreaKm2*c.Occupation;
                for(int k=0;k<8;k++)patchTraits[patch*8+k]+=c.Appearance[k]*population;
                if(!dominant.TryGetValue(c.District,out var prior)||prior.population<population)dominant[c.District]=(c,population);
            }
            foreach(var record in dominant)
            {foreach(int cell in f.Districts[record.Key].Cells)s.Society.CellCulture[cell]=record.Value.people.Culture;}
            for(int p=0;p<Patches.Length;p++)if(s.Population[p]>0)for(int k=0;k<8;k++)s.Ancestry[p*8+k]=(int)Math.Round(D.Clamp(patchTraits[p*8+k]/s.Population[p])*10000);
            s.Society.Cultures=f.Cultures.Select(c=>new CultureIdentity{Id=c.Id,Name=c.Name,OriginCell=f.Districts[c.OriginDistrict].Cell,FoundedYear=c.FoundedYear,Stage=SocietyStage.Foragers,Population=(long)Math.Round(culturePopulation[c.Id]),AreaKm2=(long)Math.Round(cultureArea[c.Id]),Farming=0,Hunting=350,Gathering=450,Fishing=200,Subsistence=c.Residence==ForagerResidence.AquaticResidence?"Aquatic foraging and seasonal camps":c.Residence==ForagerResidence.CollectorCamps?"Seasonal base camps and collecting parties":"Mobile foraging and reciprocal access",Resources="Local wild foods, materials and social exchange"}).ToArray();
            int EventCulture(int id)=>id<0?-1:id<f.Cohorts.Length&&f.Cohorts[id].Id==id?f.Cohorts[id].Culture:f.Cohorts.FirstOrDefault(c=>c.Id==id)?.Culture??-1;
            s.Society.Events=f.Events.OrderBy(e=>e.Seconds).TakeLast(256).Select(e=>new ChronicleEvent{Year=(int)(e.Seconds/SimulationTime.Year),Cell=e.District>=0?f.Districts[e.District].Cell:-1,CultureId=EventCulture(e.Cohort),Category=e.Kind==ForagerEventKind.Innovation||e.Kind==ForagerEventKind.KnowledgeLoss?ChronicleCategory.Discoveries:ChronicleCategory.Society,Text=e.Text}).ToArray();
            s.Years=(int)(f.Seconds/SimulationTime.Year);s.Paleolithic.Seconds=f.Seconds;s.Paleolithic.BoundarySeconds=f.BoundarySeconds;
        }
        public void ValidateForagers(PrehistoryState s)
        {
            var f=s?.Foragers;
            if(s==null||s.Version!="prehistory-8"||f==null||(f.Version!="foragers-2"&&f.Version!="foragers-3"&&f.Version!="foragers-4")||s.Paleolithic?.Version!="paleolithic-3"||s.History!=null||s.Timeline!=null||s.HumanRanges!=null||s.WorldHash!=World.Stages.Last().Hash||s.Hash!=s.ComputeHash())throw new ArgumentException("Invalid foraging-world checkpoint.");
            if(f.CellDistrict?.Length!=World.Surface.Cells.Length||f.Districts==null||f.Resources?.Length!=f.Districts.Length||s.Population?.Length!=Patches.Length||s.Ancestry?.Length!=Patches.Length*8||s.Society?.CellCulture?.Length!=World.Surface.Cells.Length||s.Origin<0||s.Origin>=Patches.Length||f.Seconds<0||f.Seconds>SimulationTime.Maximum||f.BoundarySeconds>f.Seconds||f.NextReviewSeconds<=f.Seconds||f.NextReviewSeconds-f.BoundarySeconds!=5*SimulationTime.Year||s.Years!=f.Seconds/SimulationTime.Year)throw new ArgumentException("Invalid foraging geography/calendar.");
            bool Finite(double[] a,int count)=>a!=null&&a.Length==count&&a.All(x=>double.IsFinite(x)&&x>=0);
            foreach(var c in f.Cohorts)
            {
                if(c.District<0||c.District>=f.Districts.Length||c.Lineage<0||c.Lineage>=f.Lineages.Length||c.Culture<0||c.Culture>=f.Cultures.Length||c.Identity<0||c.Identity>=f.Peoples.Length||c.Language<0||c.Language>=f.Languages.Length||c.Belief<0||c.Belief>=f.Beliefs.Length||!Finite(c.Ages,5)||!Finite(c.Knowledge,ForagerTechnology.Count)||!Finite(c.Skill,ForagerTechnology.Count)||!Finite(c.Use,ForagerTechnology.Count)||!Finite(c.Appearance,8)||!Finite(c.Admixture,f.Lineages.Length)||Math.Abs(c.Ages.Sum()-c.Population)>.00001)throw new ArgumentException("Invalid foraging population/cohort.");
            }
            // Extinct resident records remain in the genealogy. Recolonisation
            // can found a new community. Shared heritage or species does not
            // make two distinct communities the same demographic ledger.
            if(f.Cohorts.Select(c=>c.Id).Distinct().Count()!=f.Cohorts.Length)throw new ArgumentException("Duplicate demographic cohort.");
            foreach(var l in f.Languages)if(l.Id<0||l.Id>=f.Languages.Length||!LanguageGenerator.Valid(l.Sounds)||l.Roots.Length!=ForagerLanguages.Concepts.Length||l.Rates.Length!=l.Roots.Length||l.Family<0||l.Family>=f.LanguageFamilies.Length)throw new ArgumentException("Invalid foraging speech ancestry.");
            if(s.Society.Settlements.Length!=0||s.Society.Cultures.Any(c=>c.Stage!=SocietyStage.Foragers||c.Farming!=0))throw new ArgumentException("Later-age institutions are not part of this foraging model.");
            ValidateForagerLedger(s);
        }
    }
}
