namespace WorldGen.Core
{
    public sealed partial class Prehistory
    {
        public const string ResearchPacingHistoryVersion="history-29";
        public PrehistoryState UpgradeToResearchPacingHistory(PrehistoryState source)
        {
            Validate(source);if(source.History?.Version==ResearchPacingHistoryVersion)return source;
            source=UpgradeToTerritorialHistory(source);var s=source.Copy();s.History.Version=ResearchPacingHistoryVersion;s.Seal();return s;
        }
    }
}
