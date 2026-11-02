using System;
using System.Collections.Generic;
using System.Linq;
namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        void ValidateForagerLedger(PrehistoryState s)
        {
            var f=s.Foragers;int year=s.Years;
            void Require(bool ok,string detail){if(!ok)throw new ArgumentException("Invalid foraging ledger: "+detail);}
            bool Finite(double x)=>double.IsFinite(x)&&x>=0;
            bool Unit(double x)=>Finite(x)&&x<=1.000001;
            bool Values(double[] values,int count,double maximum=1)=>values!=null&&values.Length==count&&values.All(x=>Finite(x)&&x<=maximum+.000001);
            bool Years(int[] values)=>values!=null&&values.Length==ForagerTechnology.Count&&values.All(x=>x>=-1&&x<=year);
            Require(f.BoundarySeconds>=0&&f.BoundarySeconds%(5*SimulationTime.Year)==0,"fixed review boundary");
            Require(f.Biota?.Length==f.Districts.Length,"saved ecological districts");
            Require(f.NextCulture==f.Cultures.Length&&f.NextLanguage==f.Languages.Length&&f.NextBelief==f.Beliefs.Length&&f.NextFamily==f.LanguageFamilies.Length&&f.NextPeople==f.Peoples.Length,"dense identity registries");
            Require(f.NextCohort>=0&&f.Cohorts.All(c=>c.Id>=0&&c.Id<f.NextCohort),"cohort identity counter");
            var ids=f.Cohorts.Select(c=>c.Id).ToHashSet();
            foreach(var d in f.Districts)
            {
                Require(d.Id>=0&&d.Id<f.Districts.Length&&ReferenceEquals(d,f.Districts[d.Id])&&d.Patch>=0&&d.Patch<Patches.Length,"district identity");
                Require(double.IsFinite(d.AreaKm2)&&d.AreaKm2>0&&double.IsFinite(d.Center.Length)&&Math.Abs(d.Center.Length-1)<.000001,"land area and location");
                Require(d.Cells.Length>0&&d.Cells.Contains(d.Cell)&&d.Cells.Distinct().Count()==d.Cells.Length&&d.Cells.All(cell=>cell>=0&&cell<f.CellDistrict.Length&&f.CellDistrict[cell]==d.Id),"district membership");
                Require(d.Passages.All(p=>p.To>=0&&p.To<f.Districts.Length&&p.To!=d.Id&&p.DistanceM>0&&d.Cells.Contains(p.FromCell)&&f.Districts[p.To].Cells.Contains(p.ToCell)&&Finite(p.MountainBarrier)&&Finite(p.WaterCost)),"geographic passages");
                Require(Values(d.Food,5,10)&&Values(d.Seasonality,5),"base ecological opportunities");
                var resource=f.Resources[d.Id];Require(resource.District==d.Id&&Values(resource.Stock,5)&&Finite(resource.HarvestPressure)&&Finite(resource.HumanDensity)&&resource.LastReviewYear<=year,"resource ledger");
                var bio=f.Biota[d.Id];Require(bio.District==d.Id&&bio.ReviewedYear>=0&&bio.ReviewedYear<=year&&bio.Taxa.Length==bio.Abundance.Length&&Values(bio.Abundance,bio.Taxa.Length)&&Values(bio.FoodAvailability,5,1.6)&&Values(bio.BaselineFood,5,double.MaxValue)&&Unit(bio.ForestCover),"biota abundance/niche ledger");
                Require(bio.Taxa.All(t=>t>=0&&t<World.Ecology.Taxa.Length)&&bio.Taxa.SequenceEqual(bio.Taxa.OrderBy(x=>x).Distinct()),"sorted local taxa");
            }
            for(int cell=0;cell<f.CellDistrict.Length;cell++)Require(f.CellDistrict[cell]==-1||f.CellDistrict[cell]>=0&&f.CellDistrict[cell]<f.Districts.Length&&f.Districts[f.CellDistrict[cell]].Cells.Contains(cell),"cell-to-district lookup");
            foreach(var c in f.Cohorts)
            {
                Require(c.Parent<0||c.Parent<c.Id&&ids.Contains(c.Parent),"demographic ancestry");
                Require(c.FoundedYear<=year&&c.LastContactYear<=year&&c.LastMigrationYear<=year,"resident dates");
                Require(f.Districts[c.District].Cells.Contains(c.HomeCell)&&f.Districts[c.District].Cells.Contains(c.FrontierCell),"resident home and entry cells");
                Require(Values(c.Knowledge,ForagerTechnology.Count)&&Values(c.Skill,ForagerTechnology.Count)&&Values(c.Use,ForagerTechnology.Count)&&(c.Population==0&&c.Teaching.Length==0||Values(c.Teaching,ForagerTechnology.Count,10))&&(c.Population==0&&c.Innovation.Length==0||Values(c.Innovation,ForagerTechnology.Count)),"technique values");
                Require(Years(c.FirstKnownYear)&&Years(c.FirstSkilledYear)&&Years(c.LastLossYear),"persistent learning history");
                Require((c.Population==0&&c.TeachingFrom.Length==0||Values(c.TeachingFrom,ForagerTechnology.Count)||Values(c.TeachingFrom,ForagerTechnology.Count*2))&&Finite(c.ContactTeaching)&&Finite(c.TeachingAdults),"saved incoming teaching context");
                for(int k=0;k<ForagerTechnology.Count;k++)Require(c.Skill[k]<=c.Knowledge[k]+.000001&&c.Use[k]<=c.Skill[k]+.000001,"use <= skill <= familiarity");
                Require(Finite(c.Children)&&Finite(c.Adults)&&Finite(c.Elders)&&Math.Abs(c.Children-c.Ages[0]-c.Ages[1])<.000001&&Math.Abs(c.Adults-c.Ages[2]-c.Ages[3])<.000001&&Math.Abs(c.Elders-c.Ages[4])<.000001,"age-stock aggregates");
                Require(Finite(c.Births)&&Finite(c.Deaths)&&Finite(c.Immigration)&&Finite(c.Emigration)&&Finite(c.Capacity)&&Finite(c.MatingContacts)&&Finite(c.BirthRate)&&Finite(c.AdultMortality)&&Finite(c.ChildMortality)&&Finite(c.ElderMortality),"vital rates and totals");
                Require(Unit(c.Occupation)&&Unit(c.WinterSecurity)&&Unit(c.Health)&&Unit(c.Isolation)&&Unit(c.Connectivity)&&Unit(c.ConflictPressure)&&Finite(c.FoodSecurity)&&c.FoodSecurity<=1.500001,"livelihood constraints");
                Require(Values(c.Appearance,8)&&Values(c.Admixture,f.Lineages.Length)&&Math.Abs(c.Admixture.Sum()-1)<.000001,"appearance and mixed ancestry");
                bool Affiliations(ForagerAffiliation[] profile,int count)=>profile!=null&&profile.Length>0&&profile.All(x=>x.Id>=0&&x.Id<count&&Unit(x.Share)&&x.Share>0)&&profile.Select(x=>x.Id).SequenceEqual(profile.Select(x=>x.Id).OrderBy(x=>x).Distinct())&&Math.Abs(profile.Sum(x=>x.Share)-1)<.000001;
                Require(Affiliations(c.Speech,f.Languages.Length)&&Affiliations(c.Traditions,f.Beliefs.Length),"complete resident affiliations");
                Require(c.Language==c.Speech.OrderByDescending(x=>x.Share).ThenBy(x=>x.Id).First().Id&&c.Belief==c.Traditions.OrderByDescending(x=>x.Share).ThenBy(x=>x.Id).First().Id,"derived majority affiliations");
                Require(c.MinorityLanguage>=-1&&c.MinorityLanguage<f.Languages.Length&&c.MinorityBelief>=-1&&c.MinorityBelief<f.Beliefs.Length&&Unit(c.MinorityLanguageShare)&&Unit(c.MinorityBeliefShare)&&Unit(c.BilingualShare),"resident minorities");
                Require(c.ShockStart>=0&&c.ShockEnd>=c.ShockStart&&Finite(c.ShockSeverity),"shock timing");
            }
            Require(Finite(f.InitialPopulation)&&f.InitialPopulation>0&&Math.Abs(f.Cohorts.Sum(c=>c.Population+c.Deaths-c.Births)-f.InitialPopulation)<.05,"global birth/death conservation");
            Require(Math.Abs(f.Cohorts.Sum(c=>c.Immigration-c.Emigration))<.0001,"global migrant conservation");
            foreach(var l in f.Lineages)
            {
                Require(l.Id>=0&&l.Id<f.Lineages.Length&&ReferenceEquals(l,f.Lineages[l.Id])&&l.Parent<l.Id&&l.OriginDistrict>=0&&l.OriginDistrict<f.Districts.Length,"lineage identity/parent");
                Require(Unit(l.CulturalLearning)&&Unit(l.SymbolicPotential),"explicit lineage capacities");
                Require(Finite(l.AdultMassKg)&&l.AdultMassKg>=25&&l.AdultMassKg<120&&Finite(l.StatureM)&&l.StatureM>1&&l.StatureM<2.3&&Unit(l.Compactness)&&Unit(l.LimbRatio)&&Unit(l.FaceProjection)&&Unit(l.BrowRidge)&&Unit(l.JawBreadth)&&Unit(l.NoseForm),"human phenotype");
                Require(l.AncestralRoute.Length>0&&l.AncestralRoute[0]==l.AncestorOrigin&&l.AncestralRoute.Last()==l.OriginDistrict&&l.AncestralRoute.Zip(l.AncestralRoute.Skip(1),(a,b)=>f.Districts[a].Passages.Any(p=>p.To==b)).All(x=>x),"connected ancestral human dispersal");
                Require(l.Parent<0||l.SeparationYears<f.Lineages[l.Parent].SeparationYears,"descendant separation must follow its ancestral separation");
            }
            Require(f.Lineages.Count(l=>l.PlayerSpecies)==1&&f.Lineages.Select(l=>l.ScientificName).Distinct().Count()==f.Lineages.Length,"distinct lineages and designated player species");
            foreach(var c in f.Cultures)Require(c.Id>=0&&c.Id<f.Cultures.Length&&ReferenceEquals(c,f.Cultures[c.Id])&&c.Parent<c.Id&&c.OriginDistrict>=0&&c.OriginDistrict<f.Districts.Length&&c.FoundedYear<=year&&Values(c.FoodPreference,5)&&Values(c.TechniquePreference,ForagerTechnology.Count)&&new[]{c.FoodSharing,c.GuestAccess,c.KinObligation,c.ExchangeOpenness,c.PrestigeCompetition,c.ConflictAvoidance,c.MarriageMobility,c.ChildcareCooperation}.All(Unit),"cultural practice registry");
            foreach(var p in f.Peoples)Require(p.Id>=0&&p.Id<f.Peoples.Length&&ReferenceEquals(p,f.Peoples[p.Id])&&p.Parent<p.Id&&p.OriginDistrict>=0&&p.OriginDistrict<f.Districts.Length&&p.FoundedYear<=year&&p.Affinities.All(x=>x>=0&&x<f.Peoples.Length),"heritage registry");
            foreach(var l in f.Languages)Require(ReferenceEquals(l,f.Languages[l.Id])&&l.Parent<l.Id&&l.FoundedYear<=year&&l.LastChangeYear<=year&&l.RootYears.Length==ForagerLanguages.Concepts.Length&&l.RootYears.All(x=>x<=year)&&Values(l.Rates,l.Roots.Length,.01)&&Values(l.Grammar,10)&&Unit(l.NarrativePractice)&&Unit(l.TechnicalRegister)&&Unit(l.RitualRegister),"speech history and registers");
            foreach(var family in f.LanguageFamilies)Require(family.Id>=0&&family.Id<f.LanguageFamilies.Length&&ReferenceEquals(family,f.LanguageFamilies[family.Id])&&family.Parent<family.Id&&family.FounderLanguage>=0&&family.FounderLanguage<f.Languages.Length&&family.FoundedYear<=year&&family.ReferenceRoots.Length==ForagerLanguages.Concepts.Length,"recognisable family genealogy");
            foreach(var b in f.Beliefs)Require(b.Id>=0&&b.Id<f.Beliefs.Length&&ReferenceEquals(b,f.Beliefs[b.Id])&&b.Parent<b.Id&&b.Family>=0&&b.Family<f.Beliefs.Length&&b.FoundedYear<=year&&b.SacredCells.All(x=>x>=0&&x<World.Surface.Cells.Length)&&b.FoodTaboos.All(x=>x>=0&&x<5)&&new[]{b.Animacy,b.AncestorPresence,b.Afterlife,b.RitualSpecialists,b.HealingRites,b.CollectiveRites,b.ReciprocityWithGame,b.CreatorNarrative}.All(Unit),"local belief traditions");
            Require(f.Exchanges.Select(e=>(e.A,e.B)).Distinct().Count()==f.Exchanges.Length&&f.Exchanges.All(e=>e.A<e.B&&ids.Contains(e.A)&&ids.Contains(e.B)&&e.SinceYear<=year&&e.LastMeetingYear<=year&&new[]{e.Strength,e.Kinship,e.Hospitality,e.Trade,e.Bilingualism,e.Hostility}.All(Unit)),"exchange network");
            Require(f.Discoveries.Select(x=>x.Id).SequenceEqual(Enumerable.Range(0,f.Discoveries.Length))&&f.Discoveries.All(x=>x.Technique>=0&&x.Technique<ForagerTechnology.Count&&ids.Contains(x.Cohort)&&x.District>=0&&x.District<f.Districts.Length&&x.Year>=0&&x.Year<=year),"persistent local innovation provenance");
            Require(f.Events.All(e=>e.Seconds>=0&&e.Seconds<=f.Seconds&&(e.Cohort<0||ids.Contains(e.Cohort))&&e.District>=-1&&e.District<f.Districts.Length),"recent event chronology");
        }
    }
}

