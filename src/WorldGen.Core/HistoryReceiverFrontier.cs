using System;
namespace WorldGen.Core
{
    [Serializable] public sealed class HistoricalReceiverFrontierRules
    {
        public int Version=1;
        public HistoricalReceiverFrontierRules Copy()=>(HistoricalReceiverFrontierRules)MemberwiseClone();
    }
    public static class HistoricalReceiverFrontier
    {
        public static int MissingLevels(int technology,ReadOnlySpan<int> levels)
        {
            int missing=0;
            foreach(var requirement in HistoricalTechnicalGraph.Requirements(technology))
                missing+=Math.Max(0,requirement.Level-(requirement.Technology<levels.Length?levels[requirement.Technology]:0));
            return missing;
        }
        public static int ReadinessPermille(int missing)
            =>missing<=0?1000:missing>=4?25:1000/(1+3*missing*missing);
        public static void NeededLevels(ReadOnlySpan<int> levels,ReadOnlySpan<int> offers,Span<int> needed)
        {
            needed.Clear();
            for(int k=0;k<Math.Min(levels.Length,offers.Length);k++)
            {
                if(offers[k]<=levels[k])continue;
                foreach(var requirement in HistoricalTechnicalGraph.Requirements(k))
                {
                    int prerequisite=requirement.Technology;
                    if(prerequisite>=levels.Length||prerequisite>=needed.Length||prerequisite>=offers.Length||
                        levels[prerequisite]>=requirement.Level||offers[prerequisite]<=levels[prerequisite])continue;
                    needed[prerequisite]=Math.Max(needed[prerequisite],requirement.Level);
                }
            }
        }
        public static long Priority(int technology,ReadOnlySpan<int> levels,int needed,long original)
        {
            if(original<=0)return 0;
            long weighted=original*ReadinessPermille(MissingLevels(technology,levels))/1000;
            if(needed>levels[technology])weighted*=2;
            return Math.Max(1,weighted);
        }
    }
    public sealed partial class Prehistory
    {
        public PrehistoryState UpgradeReceiverFrontierLearning(PrehistoryState source)
        {
            Validate(source);if(!LivingWorldHistory(source))throw new ArgumentException("Receiver-frontier learning requires history32.");
            if(source.History.ReceiverFrontier!=null)return source;
            var s=source.Copy();InitializeReceiverFrontierLearning(s);s.Seal();return s;
        }
        void InitializeReceiverFrontierLearning(PrehistoryState s)
        {if(s.History.ReceiverFrontier==null)s.History.ReceiverFrontier=new HistoricalReceiverFrontierRules();}
        void ValidateReceiverFrontierLearning(PrehistoryState s)
        {
            var rules=s.History.ReceiverFrontier;
            if(rules!=null&&(!LivingWorldHistory(s)||rules.Version!=1))throw new ArgumentException("Invalid receiver-frontier rules.");
        }
    }
}
