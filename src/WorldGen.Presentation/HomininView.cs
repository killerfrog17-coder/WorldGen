using System;
using System.Linq;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    [Serializable] public sealed class HomininStageOverview
    {
        public string Label="Early Homo",Description="No resident populations are established yet.";
        public LithicStage Frontier,Predominant;public long Population;
        public long[] PeopleByStage=new long[(int)LithicStage.BronzeAge+1];
    }
    public static class HomininView
    {
        // Build once per demographic publication, not every rendered frame.
        // A frontier is a local capability, never a global technology grant.
        public static HomininStageOverview? Stages(PrehistoryState? state)
        {
            if(state?.DeepPast?.Active!=true)return null;
            var h=state.DeepPast;var result=new HomininStageOverview();
            var stages=h.Traditions.Select(t=>HomininHistory.Stage(t,h.RulesVersion<3)).ToArray();
            foreach(var p in h.Populations)if(p.People>0)result.PeopleByStage[(int)stages[p.Tradition]]+=(long)Math.Round(p.People);
            result.Population=result.PeopleByStage.Sum();if(result.Population==0)return result;
            int present=0;long most=0;
            for(int stage=0;stage<result.PeopleByStage.Length;stage++)
            {
                long people=result.PeopleByStage[stage];if(people==0)continue;
                present++;result.Frontier=(LithicStage)stage;
                if(people>most){most=people;result.Predominant=(LithicStage)stage;}
            }
            result.Label=HomininHistory.StageName(result.Frontier)+(present>1?" frontier":"");
            result.Description="Most people: "+HomininHistory.StageName(result.Predominant)+". Local traditions coexist; the frontier is not universal adoption. "+string.Join("; ",Enumerable.Range(0,result.PeopleByStage.Length).Where(i=>result.PeopleByStage[i]>0).Select(i=>HomininHistory.StageName((LithicStage)i)+": "+result.PeopleByStage[i].ToString("N0",System.Globalization.CultureInfo.InvariantCulture)+" people"));
            return result;
        }
        public static string Date(PrehistoryState state)=>Date(state,state.Years);
        public static string Date(PrehistoryState state,int elapsedYears)
            =>DateFromOrigin(state.DeepPast.OriginYearsBeforePresent,elapsedYears);
        public static string DateFromOrigin(int originYearsBeforePresent,int elapsedYears)
        {long bp=(long)originYearsBeforePresent-elapsedYears;return bp>=0?bp.ToString("N0")+" years before reference":(-bp).ToString("N0")+" years after reference";}
        public static string Status(PrehistoryState state)
        {
            var h=state.DeepPast;int living=h.Populations.Select(p=>p.Lineage).Distinct().Count();
            return Date(state)+" · "+state.Population.Sum().ToString("N0")+" people · "+living+" living lineages · "+h.Populations.Select(p=>p.Tradition).Distinct().Count()+" local traditions";
        }
        public static string Describe(Prehistory engine,PrehistoryState state,int cell)
        {
            if(cell<0||cell>=engine.CellPatch.Length)return "Select a hominin population.";int region=engine.CellPatch[cell];if(region<0)return "Open water";
            var h=state.DeepPast;var groups=h.Populations.Where(p=>p.Region==region).OrderByDescending(p=>p.People).ToArray();
            if(groups.Length==0)return engine.PatchName(region)+" · no resident hominin population";
            string text=engine.PatchName(region);
            foreach(var p in groups)
            {
                var t=h.Traditions[p.Tradition];var l=h.Lineages[p.Lineage];
                text+="\n\n"+l.Name+(l.SapiensLike?" (sapiens-like)":"")+" · "+Math.Round(p.People).ToString("N0")+" people\n"+t.Name+" · "+HomininHistory.StageName(HomininHistory.Stage(t,h.RulesVersion<3));
                text+="\nCommunication capacity "+(p.Communication*100).ToString("F0")+" · teaching "+(t.Teaching*100).ToString("F0")+" · cultural retention "+(t.Retention*100).ToString("F0");
                text+="\nLearned practices: "+string.Join(", ",Enum.GetValues(typeof(LithicKnowledge)).Cast<LithicKnowledge>().Where(k=>k!=LithicKnowledge.None&&(t.Knowledge&k)!=0).Select(Prehistory.LithicSkillName));
                var language=h.Version>=2?h.Languages[p.Language]:null;
                text+="\n"+(language?.Name??("Language identity "+p.Language))+(language?.Parent>=0?" (from "+h.Languages[language.Parent].Name+")":"")+" · ancestry "+string.Join(", ",p.Ancestry.Where(a=>a.Share>.005).Select(a=>h.Lineages[a.Lineage].Name+" "+(a.Share*100).ToString("F0")+"%"));
            }
            var env=h.Habitats.FirstOrDefault(e=>e.Region==region);if(env!=null)text+="\n\nGlacial climate: "+env.TemperatureDelta.ToString("F1")+" °C from baseline; rainfall ×"+env.RainFactor.ToString("F2")+"; woody cover "+(env.WoodyCover*100).ToString("F0")+"%; ice "+(env.Ice*100).ToString("F0")+"%.";
            return text;
        }
    }
}
