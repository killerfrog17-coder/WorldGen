using System;
using System.Linq;
using WorldGen.Core;
namespace WorldGen.Presentation
{
    public static class PaleolithicView
    {
        public static string Age(PrehistoryState s)
        {
            if(!Prehistory.Paleolithic(s))return "";var c=s.Paleolithic.Communities;
            double complex=c.Where(x=>x.Residents>=25).Select(x=>PaleolithicRules.Adoption(s.Paleolithic,x,(int)PaleolithicTechnique.Bows)+PaleolithicRules.Adoption(s.Paleolithic,x,(int)PaleolithicTechnique.Needles)).DefaultIfEmpty(0).Max();
            return complex>.8?"Late Paleolithic":"Middle Paleolithic";
        }
        public static string Describe(Prehistory engine,PrehistoryState s,int cell)
        {
            if(engine==null||s==null||cell<0||cell>=engine.CellPatch.Length)return "Select a location";
            int patch=engine.CellPatch[cell];if(patch<0)return "No habitable land patch";var climate=engine.PaleolithicClimateAt(s,cell);
            string text=engine.PatchName(patch)+"\n"+Age(s)+" · "+SimulationClockView.Year(s,s.Years)+"\nClimate anomaly "+climate.TemperatureDeltaC.ToString("+0.0;-0.0;0.0")+" °C · moisture ×"+climate.PrecipitationMultiplier.ToString("0.00")+"\nSea-level fall "+climate.SeaFallM.ToString("0")+" m (migration model)";
            var c=s.Paleolithic.Communities.FirstOrDefault(x=>x.Patch==patch);if(c==null)return text+"\nNo resident sapient community.";
            var band=engine.PaleolithicBandAt(s,cell);text+="\n\nHomo sapiens · "+s.Population[patch].ToString("N0")+" residents in this patch";
            if(band==null)return text+"\nNo surviving local band.";
            text+="\nExample band: "+band.Name+"\nSpeech: "+band.LanguageName+" ("+band.Language.WordOrder+", oral)\n\nLOCAL KNOWLEDGE · share of band samples";
            for(int k=0;k<PaleolithicRules.TechniqueCount;k++){double a=PaleolithicRules.Adoption(s.Paleolithic,c,k);if(a>.005)text+="\n"+PaleolithicRules.Names[k]+": "+(100*a).ToString("0.0")+"%";}
            text+="\n\nSPEECH DEVELOPMENT · model capacities";for(int k=0;k<4;k++)text+="\n"+((PaleolithicSpeech)k)+": "+(100*band.Speech[k]).ToString("0.0")+"%";
            text+="\nExample: "+PaleolithicRules.Utterance(band,"we","hunt","deer","tomorrow","food");
            return text+"\n\nLocal band identities are stable. Knowledge is distinct from available materials. Later ages are disabled.";
        }
    }
}
