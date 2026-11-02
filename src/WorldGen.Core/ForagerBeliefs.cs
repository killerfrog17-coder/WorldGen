using System;
using System.Linq;
namespace WorldGen.Core
{
    public static class ForagerBeliefs
    {
        static readonly string[] Landscapes={"river pools","old trees","rock shelters","mountain ridges","marsh edges","headlands","springs","ancestral camp places"};
        static readonly string[] Rites={"shared food and remembered stories","songs at the season's first gathering","gifts before a hunt","healing and dream telling","night dances at visiting camps","remembering the dead at a returning camp"};
        static readonly string[] Mortuary={"careful placement in a sheltered place","burial with a few personal objects","burial marked by stones","exposure away from the living camp","returning bones to a remembered place"};
        public static ForagerBelief Create(int seed,int id,int district,int year,ForagerDistrict environment,ForagerLanguage speech,ForagerBelief parent=null)
        {
            double Trait(string name,double value)=>D.Q(D.Clamp(value+(Rng.Value(seed,"belief-"+name,id)-.5)*(parent==null?.7:.25)),1000000);
            int landmark=environment.Coast>.5?5:environment.River>.4?0:environment.Relief>800?2:environment.Canopy>.5?1:6;
            var b=new ForagerBelief{Id=id,Parent=parent?.Id??-1,Family=parent?.Family??id,OriginDistrict=district,FoundedYear=year,Seed=seed^id*7919,
                Animacy=Trait("animacy",parent?.Animacy??.7),AncestorPresence=Trait("ancestors",parent?.AncestorPresence??.4),Afterlife=Trait("afterlife",parent?.Afterlife??.4),RitualSpecialists=Trait("specialists",parent?.RitualSpecialists??.35),HealingRites=Trait("healing",parent?.HealingRites??.6),CollectiveRites=Trait("collective",parent?.CollectiveRites??.6),ReciprocityWithGame=Trait("game",parent?.ReciprocityWithGame??.6),CreatorNarrative=Trait("creator",parent?.CreatorNarrative??.25),SacredLandscape=Landscapes[landmark],SacredCells=new[]{environment.Cell},SeasonalRite=Rites[(int)(Rng.Value(seed,"belief-rite",id)*Rites.Length)],MortuaryPractice=parent?.MortuaryPractice??Mortuary[(int)(Rng.Value(seed,"belief-mortuary",id)*Mortuary.Length)]};
            b.Name=LanguageGenerator.Capitalize(LanguageGenerator.WordWithRootSeed(speech.Sounds,"spirit",b.Seed).Text)+" tradition";
            b.SacredAnimal=environment.Food[2]>.4?"large game":environment.Food[3]+environment.Food[4]>.5?"fish":environment.Canopy>.5?"forest birds":"seasonal birds";
            // Food restrictions are social meanings, not a universal toxic-food
            // list or an instruction to forbid a population's only food source.
            int scarce=Array.IndexOf(environment.Food,environment.Food.Min());
            if(Rng.Value(seed,"belief-food-custom",id)<.35)b.FoodTaboos=new[]{scarce};
            return b;
        }
        public static void Change(int seed,ForagerBelief b,ForagerDistrict d,int year,int elapsed,double isolation,double contact,ForagerBelief donor=null)
        {
            double Drift(string key,double value,double other)
            {
                double local=(Rng.Value(seed,"belief-local-"+key,b.Id^year)-.5)*Math.Sqrt(elapsed/1000.0)*.12*(.5+isolation);
                return D.Q(D.Clamp(value+local+(other-value)*D.Clamp(contact*elapsed/10000)),1000000);
            }
            b.Animacy=Drift("animacy",b.Animacy,donor?.Animacy??b.Animacy);b.AncestorPresence=Drift("ancestor",b.AncestorPresence,donor?.AncestorPresence??b.AncestorPresence);b.Afterlife=Drift("afterlife",b.Afterlife,donor?.Afterlife??b.Afterlife);
            b.RitualSpecialists=Drift("specialists",b.RitualSpecialists,donor?.RitualSpecialists??b.RitualSpecialists);b.HealingRites=Drift("healing",b.HealingRites,donor?.HealingRites??b.HealingRites);b.CollectiveRites=Drift("collective",b.CollectiveRites,donor?.CollectiveRites??b.CollectiveRites);b.ReciprocityWithGame=Drift("game",b.ReciprocityWithGame,donor?.ReciprocityWithGame??b.ReciprocityWithGame);b.CreatorNarrative=Drift("creator",b.CreatorNarrative,donor?.CreatorNarrative??b.CreatorNarrative);
            if(Rng.Value(seed,"ritual-innovation",b.Id+year)<1-Math.Exp(-elapsed/2200.0))b.SeasonalRite=Rites[(int)(Rng.Value(seed,"ritual-selection",b.Id^year)*Rites.Length)];
            if(donor!=null&&Rng.Value(seed,"mortuary-contact",b.Id+year)<contact*elapsed/16000)b.MortuaryPractice=donor.MortuaryPractice;
        }
        public static double Similarity(ForagerBelief a,ForagerBelief b)
        {
            double traits=Math.Abs(a.Animacy-b.Animacy)+Math.Abs(a.AncestorPresence-b.AncestorPresence)+Math.Abs(a.Afterlife-b.Afterlife)+Math.Abs(a.RitualSpecialists-b.RitualSpecialists)+Math.Abs(a.CreatorNarrative-b.CreatorNarrative);
            return D.Clamp(.7*(1-traits/5)+.15*(a.SeasonalRite==b.SeasonalRite?1:0)+.15*(a.MortuaryPractice==b.MortuaryPractice?1:0));
        }
    }
}
