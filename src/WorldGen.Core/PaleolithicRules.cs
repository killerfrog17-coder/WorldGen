using System;
using System.Linq;
namespace WorldGen.Core
{
    public static class PaleolithicRules
    {
        public const int TechniqueCount=24,DecisionYears=50;
        public static readonly string[] Names={"Flake cutting","Prepared cores","Scraping","Wooden spears","Spearhead hafting","Fire tending","Hide working","Plant binding","Hide wraps","Friction fire","Compound adhesives","Bone awls","Hide sewing","Fitted clothing","Insulated clothing","Bone points","Pressure flaking","Bows","Barbed points","Fishing lines","Rafts","Eyed needles","Spear throwers","Food preservation"};
        static readonly int[][] prerequisites={new int[0],new[]{0},new[]{0},new[]{0},new[]{0,3,7},new int[0],new[]{2},new int[0],new[]{6},new[]{3,5},new[]{4,5},new[]{0,6},new[]{6,7,11},new[]{8,12},new[]{13},new[]{0,11},new[]{1,11},new[]{4,7,16},new[]{15},new[]{7,11},new[]{3,7},new[]{11,12},new[]{3,7,15},new[]{0,5}};
        public static int[] Prerequisites(PaleolithicTechnique t)=>(int[])prerequisites[(int)t].Clone();
        public static double PrerequisiteStrength(double[] knowledge,int t){double v=1;foreach(int p in prerequisites[t])v=Math.Min(v,knowledge[p]);return v;}
        public static double Approach(double value,double target,double rate,double years)=>target+(value-target)*Math.Exp(-Math.Max(0,rate)*Math.Max(0,years));
        public static double Adoption(PaleolithicState state,PaleolithicCommunity c,int technique)
        {double value=D.Clamp(Approach(c.Knowledge[technique],c.Targets[technique],c.Rates[technique],(state.Seconds-state.BoundarySeconds)/(double)SimulationTime.Year));foreach(int parent in prerequisites[technique])value=Math.Min(value,Adoption(state,c,parent));return value;}
        public static double SpeechAt(PaleolithicState s,PaleolithicCommunity c,int axis)=>D.Clamp(Approach(c.Speech[axis],1,c.SpeechRates[axis],(s.Seconds-s.BoundarySeconds)/(double)SimulationTime.Year));
        public static double Residents(PaleolithicState s,PaleolithicCommunity c)
        {
            if(c.Residents<=0)return 0;double years=(s.Seconds-s.BoundarySeconds)/(double)SimulationTime.Year;
            if(c.Capacity<=0)return c.Residents*Math.Exp(-.03*years);
            double e=Math.Exp(-c.GrowthRate*years);return Math.Max(0,c.Capacity/(1+(c.Capacity/Math.Max(.000001,c.Residents)-1)*e));
        }
        // Broad dated forcing, not a physical ice-sheet reconstruction. Smooth
        // interpolation captures MIS7/6/5 and the last glaciation; earlier/later
        // times use a continuous100ky approximation, never a calendar tech gate.
        public static PaleolithicClimate Climate(int seed,double elapsed,double sineLatitude,int elevationM=0,int province=0)
        {
            double bp=200000-elapsed;double[] age={0,12000,21000,70000,115000,125000,145000,190000,200000};double[] ice={.03,.25,1,.72,.65,.04,1,.12,.55};double amount;
            if(bp>=0&&bp<=200000){int i=0;while(i<age.Length-2&&bp>age[i+1])i++;double f=(bp-age[i])/(age[i+1]-age[i]);f=f*f*(3-2*f);amount=ice[i]+(ice[i+1]-ice[i])*f;}
            else if(bp<0)amount=.03+.47*(1-Math.Cos(2*Math.PI*bp/100000));
            else amount=.55+.215*(1-Math.Cos(2*Math.PI*(bp-200000)/100000));
            double lat=Math.Abs(sineLatitude),regional=Rng.Value(seed,"paleo-moisture",province)*2-1;
            return new PaleolithicClimate(amount,-amount*(2.5+8*lat*lat+Math.Max(0,elevationM-2000)*.0003),Math.Max(.45,1-amount*(.2+.15*lat)+regional*.1*amount),amount*125);
        }
        public static bool RouteOpen(Prehistory.Route r,PaleolithicClimate climate,double raft)
        {if(!r.Sea)return true;if(r.Shallow)return r.SeaDropM>0&&r.SeaDropM<=climate.SeaFallM;return raft>=.6&&r.DistanceM<=20000;}
        public static bool CanExpress(PaleolithicBand band,PaleolithicSpeech ability,double demand=.65)=>band!=null&&band.Speech[(int)ability]>=demand;
        public static WeatherFrame Weather(WeatherFrame w,PaleolithicClimate c)
        {
            double temp=w.TemperatureC+c.TemperatureDeltaC,water=w.PrecipitationMmPerHour*c.PrecipitationMultiplier,snow=D.Clamp((2-temp)/4);
            return new WeatherFrame(temp,w.Humidity,water*(1-snow),water*snow,w.CloudCover,w.CirrusCover,w.CloudBaseM,w.CloudThicknessM,w.CirrusBaseM,w.WindEastMps,w.WindNorthMps,w.GustMps,w.Thunderstorm,w.Fog,w.WindGustChance,w.WindGustDurationS,w.WindTurbulence);
        }
        public static bool CanName(PaleolithicBand band,string concept)
        {
            if(band==null)return false;switch(concept){case "writing":case "iron":case "bronze":case "king":case "farm":return false;case "bow":return band.Knows(PaleolithicTechnique.Bows);case "clothing":return band.Knows(PaleolithicTechnique.HideWraps);case "boat":return band.Knows(PaleolithicTechnique.Rafts);default:return true;}
        }
        public static string Utterance(PaleolithicBand band,string subject,string verb,string obj,string when="",string reason="")
        {
            if(band==null)throw new ArgumentNullException(nameof(band));
            string W(string c)=>LanguageGenerator.Word(band.Language,CanName(band,c)?c:"thing").Text;
            string text=band.Language.WordOrder=="SVO"?W(subject)+" "+W(verb)+" "+W(obj):band.Language.WordOrder=="VSO"?W(verb)+" "+W(subject)+" "+W(obj):W(subject)+" "+W(obj)+" "+W(verb);
            if(when.Length>0&&CanExpress(band,PaleolithicSpeech.DisplacedReference))text=W(when)+" "+text;
            if(!CanExpress(band,PaleolithicSpeech.Sequencing,.45))text=W(verb)+" "+W(obj);
            if(reason.Length>0&&CanExpress(band,PaleolithicSpeech.ClauseCombination))text+=" "+W("because")+" "+W(reason);return text;
        }
    }
}
