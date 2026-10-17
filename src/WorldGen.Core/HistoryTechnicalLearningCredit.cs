using System;

namespace WorldGen.Core
{
    public readonly struct HistoricalTechnicalCredit
    {
        public readonly bool Acquired;
        public readonly long ResearchUsed,LessonsUsed;
        public HistoricalTechnicalCredit(long research,long lessons){Acquired=true;ResearchUsed=research;LessonsUsed=lessons;}
    }
    public static class HistoricalTechnicalLearningCredit
    {
        // Existing work and actually delivered instruction, not extra effort.
        // Receipts may be old: retained texts do not require a living sender.
        public static HistoricalTechnicalCredit Mixed(int revision,long cost,long research,long lessons,long allocatedLessons,
            int currentLevel,int offeredLevel,int receiptYear,int now,bool validSource,bool prerequisites)
        {
            if(revision<23||cost<=0||research<=0||lessons<0||allocatedLessons<=0||currentLevel<0||currentLevel>=HistoricalTechnicalGraph.MaximumLevel||
                offeredLevel<=currentLevel||offeredLevel>HistoricalTechnicalGraph.MaximumLevel||receiptYear<0||receiptYear>now||!validSource||!prerequisites)return default;
            long used=Math.Min(cost,research),remaining=cost-used;
            // Avoid lessons*8 and remaining+7: valid public long banks/costs
            // must not overflow even at their maximum diagnostic boundary.
            long required=remaining/8+(remaining%8==0?0:1);
            if(lessons<required)return default;
            return new HistoricalTechnicalCredit(used,required);
        }
    }
}
