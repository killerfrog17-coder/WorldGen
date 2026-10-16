public static class HistoryLivingMechanismTests
{
    // Reproducible without ignored local saved-world fixtures. Integration and
    // longitudinal visual/runtime controls remain separate required checks.
    public static void Run(Action<bool,string> check)
    {
        WorldSummaryTests.Run(check);
        HistoryLivingWorldTests.Run(check);
        HistoryProtocentreTests.Run(check);
        HistoryChartMemoryTests.Run(check);
        HistoryInstitutionalTransmissionTests.Run(check);
        HistoryMaritimeIntegrationTests.Run(check);
        HistoryMaritimePracticeTests.Run(check);
        HistoryColonialPortRetryTests.Run(check);
        HistorySettlementNetworkTests.Run(check);
        HistoryInlandSettlementTests.Run(check);
        HistoryMilitarySpecializationTests.Run(check);
        HistoryNaturalResourceTests.Run(check);
        HistoryResourceEconomyTests.Run(check);
        HistoryHumanVariationTests.Run(check);
        HistoryPoliticalProgramsTests.Run(check);
        HistoryMarineForagerTests.Run(check);
        HistoryForagerMaritimeTests.Run(check);
        HistoryNuclearTests.Run(check);
        HistoryInternationalTests.Run(check);
        HistoryInternationalFinanceTests.Run(check);
        FaunaPackTests.Run(check);
    }
}
