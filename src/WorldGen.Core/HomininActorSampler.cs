using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldGen.Core
{
    /// <summary>Persist this result when creating an actor. It records a
    /// birthplace/population and actual inherited identity; it is not a new
    /// demographic person or a promise of a matching rendered archaic model.</summary>
    [Serializable] public sealed class HomininActorProfile
    {
        public string WorldHash="",HistoryHash="",LineageName="",TraditionName="",LanguageName="";
        public int Year,Cell,Region,Population,Lineage,Tradition,Language,IndividualSeed;
        public bool SapiensLike;
        public LithicKnowledge LearnedPractices;
        public HomininAncestry[] Ancestry=Array.Empty<HomininAncestry>();
        public HumanAppearance Appearance=new HumanAppearance();
    }

    /// <summary>Detached, read-only source for future NPC creation. Validate and
    /// copy once per demographic publication, not for every actor or frame.
    /// Sampling never changes the history census or uses a culture as a genetic
    /// category. A fresh sampler may select a different population; actors keep
    /// their persisted result rather than resampling as the world changes.</summary>
    public sealed class HomininActorSampler
    {
        readonly int seed,year;readonly int[] cellPatch;
        readonly string worldHash,historyHash;
        readonly Dictionary<int,HomininPopulation[]> regions;
        readonly HomininLineage[] lineages;readonly HomininTradition[] traditions;
        readonly string[] languages;
        public HomininActorSampler(Prehistory engine,PrehistoryState state)
        {
            if(engine==null||state?.DeepPast?.Active!=true)throw new ArgumentException("An active hominin history is required.");
            engine.Validate(state);var h=state.DeepPast;
            seed=engine.World.Recipe.Seed;year=state.Years;worldHash=state.WorldHash;historyHash=state.Hash;
            cellPatch=(int[])engine.CellPatch.Clone();lineages=h.Lineages.Select(l=>l.Copy()).ToArray();traditions=h.Traditions.Select(t=>t.Copy()).ToArray();
            languages=Enumerable.Range(0,h.NextLanguage).Select(i=>h.Version>=2?h.Languages[i].Name:"Communication identity "+i).ToArray();
            regions=h.Populations.Where(p=>p.People>0).GroupBy(p=>p.Region).ToDictionary(g=>g.Key,g=>g.OrderBy(p=>p.Id).Select(p=>p.Copy()).ToArray());
        }
        public bool TrySample(int cell,int individualSeed,bool sapiensOnly,out HomininActorProfile profile)
        {
            profile=null;
            if(cell<0||cell>=cellPatch.Length||!regions.TryGetValue(cellPatch[cell],out var residents))return false;
            double total=0;foreach(var p in residents)if(!sapiensOnly||lineages[p.Lineage].SapiensLike)total+=p.People;
            if(total<=0)return false;
            double choice=Rng.OpenValue(seed,"hominin-person-population-1",unchecked(individualSeed*397+cell*104729))*total;
            HomininPopulation selected=null;
            foreach(var p in residents)
            {
                if(sapiensOnly&&!lineages[p.Lineage].SapiensLike)continue;
                selected=p;choice-=p.People;if(choice<0)break;
            }
            if(selected==null)return false;
            var family=lineages[selected.Lineage];var values=selected.Appearance;
            var mean=new HumanAppearance{Available=true,Species=family.Name,Region=selected.Region,SampleCell=cell,
                Basis="Inherited local cohort traits; rendering and archaic anatomy require a separate actor implementation.",
                SkinPigmentation=values[0],HairPigmentation=values[1],IrisPigmentation=values[2],HairCurl=values[3],BodyCompactness=values[4],FacialVariation=values[5],NasalVariation=values[6],StaturePotential=values[7]};
            profile=new HomininActorProfile{WorldHash=worldHash,HistoryHash=historyHash,Year=year,Cell=cell,Region=selected.Region,Population=selected.Id,
                Lineage=selected.Lineage,Tradition=selected.Tradition,Language=selected.Language,IndividualSeed=individualSeed,SapiensLike=family.SapiensLike,
                LineageName=family.Name,TraditionName=traditions[selected.Tradition].Name,LanguageName=languages[selected.Language],
                LearnedPractices=traditions[selected.Tradition].Knowledge,Ancestry=selected.Ancestry.Select(a=>a.Copy()).ToArray(),
                Appearance=HistoryHumanVariation.Sample(mean,seed,individualSeed)};
            return true;
        }
    }
}
