using System;
using System.Linq;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    public static class HomininView
    {
        public static string Date(PrehistoryState state)
        {int bp=state.DeepPast.YearsBeforePresent(state.Years);return bp>=0?bp.ToString("N0")+" years before reference":(-bp).ToString("N0")+" years after reference";}
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
                text+="\n\n"+l.Name+(l.SapiensLike?" (sapiens-like)":"")+" · "+Math.Round(p.People).ToString("N0")+" people\n"+t.Name+" · "+HomininHistory.StageName(HomininHistory.Stage(t));
                text+="\nCommunication capacity "+(p.Communication*100).ToString("F0")+" · teaching "+(t.Teaching*100).ToString("F0")+" · cultural retention "+(t.Retention*100).ToString("F0");
                text+="\nLearned practices: "+string.Join(", ",Enum.GetValues(typeof(LithicKnowledge)).Cast<LithicKnowledge>().Where(k=>k!=LithicKnowledge.None&&(t.Knowledge&k)!=0).Select(Prehistory.LithicSkillName));
                text+="\nLanguage lineage "+p.Language+" · ancestry "+string.Join(", ",p.Ancestry.Where(a=>a.Share>.005).Select(a=>h.Lineages[a.Lineage].Name+" "+(a.Share*100).ToString("F0")+"%"));
            }
            var env=h.Habitats.FirstOrDefault(e=>e.Region==region);if(env!=null)text+="\n\nGlacial climate: "+env.TemperatureDelta.ToString("F1")+" °C from baseline; rainfall ×"+env.RainFactor.ToString("F2")+"; woody cover "+(env.WoodyCover*100).ToString("F0")+"%; ice "+(env.Ice*100).ToString("F0")+"%.";
            return text;
        }
    }
}
