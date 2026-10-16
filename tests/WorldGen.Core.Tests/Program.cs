using WorldGen.Core;
using WorldGen.Serialization;
using WorldGen.Presentation;
if(args.Contains("--canonical-int-arrays-only")){int count=0;CanonicalIntegerArrayTests.Run((ok,message)=>{Interlocked.Increment(ref count);if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" exact canonical integer array assertions");return;}
if(args.Contains("--stopping-point-snapshot")){int at=Array.IndexOf(args,"--stopping-point-snapshot");HistoryStoppingPointReview.Snapshot(args[at+1],args[at+2]);return;}
if(args.Contains("--stopping-point-review")){int at=Array.IndexOf(args,"--stopping-point-review");HistoryStoppingPointReview.Run(args[at+1],args[at+2],int.Parse(args[at+3]));return;}
if(args.Contains("--provisioned-supplier-trace")){int at=Array.IndexOf(args,"--provisioned-supplier-trace");HistoryProvisionedSupplierTests.Trace(args[at+1],args[at+2],int.Parse(args[at+3]));return;}
if(args.Contains("--provisioned-supplier-audit")){int at=Array.IndexOf(args,"--provisioned-supplier-audit");HistoryProvisionedSupplierTests.Audit(args[at+1],args[at+2]);return;}
if(args.Contains("--coastal-farm-only")){int count=0;HistoryCoastalFarmTests.Run((ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" coastal farm assertions");return;}
if(args.Contains("--coastal-farm-world")){int count=0;HistoryCoastalFarmTests.World(args[Array.IndexOf(args,"--coastal-farm-world")+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" coastal farm integration assertions");return;}
if(args.Contains("--provisioned-supplier-review")){int at=Array.IndexOf(args,"--provisioned-supplier-review");HistoryProvisionedSupplierTests.Review(args[at+1],args[at+2],int.Parse(args[at+3]));return;}
if(args.Contains("--receiver-frontier-only")){int count=0;HistoryReceiverFrontierTests.Run((ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" receiver frontier assertions");return;}
if(args.Contains("--receiver-frontier-world")){int count=0;HistoryReceiverFrontierTests.World(args[Array.IndexOf(args,"--receiver-frontier-world")+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" receiver frontier integration assertions");return;}
if(args.Contains("--agronomic-fit-cache-world")){int count=0;HistoryAgronomicFitCacheTests.World(args[Array.IndexOf(args,"--agronomic-fit-cache-world")+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" exact agronomic fit cache assertions");return;}
if(args.Contains("--provisioned-supplier-only")){int count=0;HistoryProvisionedSupplierTests.Run((ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" provisioned supplier assertions");return;}
if(args.Contains("--provisioned-supplier-world")){int count=0;HistoryProvisionedSupplierTests.World(args[Array.IndexOf(args,"--provisioned-supplier-world")+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" provisioned supplier integration assertions");return;}
if(args.Contains("--provisioned-port-review")){int at=Array.IndexOf(args,"--provisioned-port-review");HistoryProvisionedPortTests.Review(args[at+1],args[at+2]);return;}
if(args.Contains("--provisioned-port-atlas-world")){int count=0;HistoryLivingAtlasTests.ProvisionedWorld(args[Array.IndexOf(args,"--provisioned-port-atlas-world")+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" actual harbor atlas assertions");return;}
if(args.Contains("--provisioned-port-only")){int count=0;HistoryProvisionedPortTests.Run((ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" provisioned port assertions");return;}
if(args.Contains("--provisioned-port-world")){int count=0;HistoryProvisionedPortTests.World(args[Array.IndexOf(args,"--provisioned-port-world")+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" provisioned port integration assertions");return;}
if(args.Contains("--regional-town-exchange-only")){int count=0;HistoryRegionalTownExchangeTests.Run((ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" regional town exchange assertions");return;}
if(args.Contains("--regional-town-exchange-world")){int count=0;HistoryRegionalTownExchangeTests.World(args[Array.IndexOf(args,"--regional-town-exchange-world")+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" regional town exchange integration assertions");return;}
if(args.Contains("--late-regional-audit")){int at=Array.IndexOf(args,"--late-regional-audit");HistoryLateRegionalAudit.Run(args[at+1],args[at+2]);return;}
if(args.Contains("--inland-settlement-only")){int count=0;HistoryInlandSettlementTests.Run((ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" inland settlement assertions");return;}
if(args.Contains("--inland-settlement-world")){int count=0;HistoryInlandSettlementTests.World(args[Array.IndexOf(args,"--inland-settlement-world")+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" inland settlement integration assertions");return;}
if(args.Contains("--environmental-geometry-world")){int count=0;HistoryEnvironmentalGeometryTests.World(args[Array.IndexOf(args,"--environmental-geometry-world")+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" environmental geometry assertions");return;}
if(args.Contains("--colonial-port-retry-only")){int count=0;HistoryColonialPortRetryTests.Run((ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" colonial port retry assertions");return;}
if(args.Contains("--colonial-port-retry-world")){int count=0;HistoryColonialPortRetryTests.World(args[Array.IndexOf(args,"--colonial-port-retry-world")+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" colonial port retry integration assertions");return;}
if(args.Contains("--forager-maritime-only")){int count=0;HistoryForagerMaritimeTests.Run((ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" forager maritime assertions");return;}
if(args.Contains("--forager-maritime-world")){int count=0;HistoryForagerMaritimeTests.World(args[Array.IndexOf(args,"--forager-maritime-world")+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" forager maritime integration assertions");return;}
if(args.Contains("--maritime-practice-only")){int count=0;HistoryMaritimePracticeTests.Run((ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" maritime practice assertions");return;}
if(args.Contains("--maritime-practice-world")){int count=0;HistoryMaritimePracticeTests.World(args[Array.IndexOf(args,"--maritime-practice-world")+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" actual current-state maritime practice assertions");return;}
if(args.Contains("--political-review")){int at=Array.IndexOf(args,"--political-review");HistoryPoliticalReview.Run(args[at+1],args[at+2]);return;}
if(args.Contains("--living-mechanisms-only")){int count=0;HistoryLivingMechanismTests.Run((ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" living-world pure mechanism assertions");return;}
if(args.Contains("--protocentre-only")){int count=0;HistoryProtocentreTests.Run((ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" protocentre assertions");return;}
if(args.Contains("--protocentre-world")){int count=0;HistoryProtocentreTests.World(args[Array.IndexOf(args,"--protocentre-world")+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" protocentre integration assertions");return;}
if(args.Contains("--chart-memory-only")){int count=0;HistoryChartMemoryTests.Run((ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" chart memory assertions");return;}
if(args.Contains("--chart-memory-world")){int count=0;HistoryChartMemoryTests.World(args[Array.IndexOf(args,"--chart-memory-world")+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" chart memory integration assertions");return;}
if(args.Contains("--origin-suitability-audit")){int at=Array.IndexOf(args,"--origin-suitability-audit");HistoryOriginSuitabilityAudit.Run(args[at+1],args[at+2]);return;}
if(args.Contains("--origin-opportunity-audit")){int at=Array.IndexOf(args,"--origin-opportunity-audit");HistoryOriginOpportunityAudit.Run(args[at+1],args[at+2],args[at+3]);return;}
if(args.Contains("--resource-economy-world")){int count=0;HistoryResourceEconomyTests.World(args[Array.IndexOf(args,"--resource-economy-world")+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" resource economy integration assertions");return;}
if(args.Contains("--marine-forager-only")){int count=0;HistoryMarineForagerTests.Run((ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" marine forager assertions");return;}
if(args.Contains("--marine-forager-world")){int count=0;HistoryMarineForagerTests.World(args[Array.IndexOf(args,"--marine-forager-world")+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" marine forager integration assertions");return;}
if(args.Contains("--settlement-network-only")){int count=0;HistorySettlementNetworkTests.Run((ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" settlement network assertions");return;}
if(args.Contains("--settlement-network-prestate-world")){int count=0;HistorySettlementNetworkTests.PrestateWorld(args[Array.IndexOf(args,"--settlement-network-prestate-world")+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" prestate settlement network integration assertions");return;}
if(args.Contains("--settlement-network-world")){int count=0;HistorySettlementNetworkTests.World(args[Array.IndexOf(args,"--settlement-network-world")+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" settlement network integration assertions");return;}
if(args.Contains("--international-finance-only")){int count=0;HistoryInternationalFinanceTests.Run((ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" international finance assertions");return;}
if(args.Contains("--international-finance-world")){int count=0;HistoryInternationalFinanceTests.World(args[Array.IndexOf(args,"--international-finance-world")+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" international finance integration assertions");return;}
if(args.Contains("--international-only")){int count=0;HistoryInternationalTests.Run((ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" international diplomacy assertions");return;}
if(args.Contains("--international-world")){int count=0;HistoryInternationalTests.World(args[Array.IndexOf(args,"--international-world")+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" international diplomacy integration assertions");return;}
if(args.Contains("--colonial-settlements-world")){int count=0;HistoryColonialSettlementTests.World(args[Array.IndexOf(args,"--colonial-settlements-world")+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" sponsored colonial settlement assertions");return;}
if(args.Contains("--institutional-provenance-world")){int count=0;HistoryInstitutionalProvenanceTests.World(args[Array.IndexOf(args,"--institutional-provenance-world")+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" local institutional provenance assertions");return;}
if(args.Contains("--living-atlas-world")){int count=0;HistoryLivingAtlasTests.World(args[Array.IndexOf(args,"--living-atlas-world")+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" living atlas assertions");return;}
if(args.Contains("--atlas-continuity-world")){int count=0;HistoryLivingAtlasTests.ContinuityWorld(args[Array.IndexOf(args,"--atlas-continuity-world")+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" actual regime-continuity atlas assertions");return;}
if(args.Contains("--politics-only")){int count=0;HistoryPoliticalProgramsTests.Run((ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" political program assertions");return;}
if(args.Contains("--politics-world")){int count=0;HistoryPoliticalProgramsTests.World(args[Array.IndexOf(args,"--politics-world")+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" political program integration assertions");return;}
if(args.Contains("--resource-economy-only")){int count=0;HistoryResourceEconomyTests.Run((ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" resource economy assertions");return;}
if(args.Contains("--nuclear-only")){int count=0;HistoryNuclearTests.Run((ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" nuclear technology assertions");return;}
if(args.Contains("--nuclear-world")){int count=0;HistoryNuclearTests.World(args[Array.IndexOf(args,"--nuclear-world")+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" nuclear integration assertions");return;}
if(args.Contains("--human-variation-only")){int count=0;HistoryHumanVariationTests.Run((ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" human variation assertions");return;}
if(args.Contains("--human-variation-world")){int count=0;HistoryHumanVariationTests.World(args[Array.IndexOf(args,"--human-variation-world")+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" human variation integration assertions");return;}
if(args.Contains("--settlement-network-audit")){int at=Array.IndexOf(args,"--settlement-network-audit");HistorySettlementNetworkAudit.Run(args[at+1],args[at+2]);return;}
if(args.Contains("--natural-resources-only")){int count=0;HistoryNaturalResourceTests.Run((ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" natural-resource assertions");return;}
if(args.Contains("--natural-resources-world")){int count=0;HistoryNaturalResourceTests.World(args[Array.IndexOf(args,"--natural-resources-world")+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" natural-resource world assertions");return;}
if(args.Contains("--fauna-only")){int count=0;FaunaPackTests.Run((ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" fauna lineage assertions");return;}
if(args.Contains("--fauna-review")){int at=Array.IndexOf(args,"--fauna-review");FaunaPackTests.Review(args[at+1],int.Parse(args[at+2]),int.Parse(args[at+3]),args[at+4]);return;}
if(args.Contains("--military-specialization-only")){int count=0;HistoryMilitarySpecializationTests.Run((ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" military-specialization assertions");return;}
if(args.Contains("--military-specialization-world")){int count=0;HistoryMilitarySpecializationTests.World(args[Array.IndexOf(args,"--military-specialization-world")+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" military-specialization world assertions");return;}
if(args.Contains("--world-realism-inspect")){HistoryWorldRealismReview.Inspect(args[Array.IndexOf(args,"--world-realism-inspect")+1]);return;}
if(args.Contains("--world-realism-replay")){int at=Array.IndexOf(args,"--world-realism-replay");HistoryWorldRealismReview.Replay(args[at+1],args[at+2],int.Parse(args[at+3]));return;}
if(args.Contains("--world-realism-review")){int at=Array.IndexOf(args,"--world-realism-review");HistoryWorldRealismReview.Run(args[at+1],int.Parse(args[at+2]),int.Parse(args[at+3]),args[at+4],int.Parse(args[at+5]),args.Length>at+6?args[at+6]:"earth-native-3");return;}
if(args.Contains("--living-world-only")){int count=0;HistoryLivingWorldTests.Run((ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" living-world mechanism assertions");return;}
if(args.Contains("--living-world-test")){int count=0;HistoryLivingWorldTests.World(args[Array.IndexOf(args,"--living-world-test")+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" living-world integration assertions");return;}
if(args.Contains("--institutional-transmission-only")){int count=0;HistoryInstitutionalTransmissionTests.Run((ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" institutional transmission assertions");return;}
if(args.Contains("--institutional-transmission-world")){int count=0;HistoryInstitutionalTransmissionTests.World(args[Array.IndexOf(args,"--institutional-transmission-world")+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" institutional route integration assertions");return;}
if(args.Contains("--maritime-integration-only")){int count=0;HistoryMaritimeIntegrationTests.Run((ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" maritime integration assertions");return;}
if(args.Contains("--maritime-integration-world")){int count=0;HistoryMaritimeIntegrationTests.World(args[Array.IndexOf(args,"--maritime-integration-world")+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" maritime integration fixture assertions");return;}
if(args.Contains("--environment-only")){int count=0;HistoryEnvironmentalTests.Run((ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" environmental mechanism assertions");return;}
if(args.Contains("--environment-world")){int count=0;HistoryEnvironmentalTests.World(args[Array.IndexOf(args,"--environment-world")+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" environmental history assertions");return;}
if(args.Contains("--environment-review")){int at=Array.IndexOf(args,"--environment-review");HistoryEnvironmentalTests.Review(args[at+1],int.Parse(args[at+2]),int.Parse(args[at+3]),args[at+4],int.Parse(args[at+5]));return;}
if(args.Contains("--environment-benchmark")){int at=Array.IndexOf(args,"--environment-benchmark");HistoryEnvironmentalTests.Benchmark(args[at+1],int.Parse(args[at+2]));return;}
if(args.Contains("--languages-only")){int count=0;LanguageTests.Run((ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" procedural language assertions");return;}
if(args.Contains("--language-world")){int count=0,at=Array.IndexOf(args,"--language-world");LanguageTests.World(args[at+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);},args.Length>at+2?args[at+2]:null);Console.WriteLine("PASS "+count+" language/history integration assertions");return;}
if(args.Contains("--ambitions-upgrade")){int at=Array.IndexOf(args,"--ambitions-upgrade");if(File.Exists(args[at+2]))throw new IOException("Refusing to overwrite a world");var w=WorldFile.Read(File.ReadAllBytes(args[at+1]));var p=new Prehistory(w);w.Prehistory=p.UpgradeToTerritorialHistory(w.Prehistory);p.Validate(w.Prehistory);Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[at+2]))!);File.WriteAllBytes(args[at+2],WorldFile.Write(w));Console.WriteLine("Upgraded territorial fixture "+w.Prehistory.Hash);return;}
if(args.Contains("--ambitions-only")){int at=Array.IndexOf(args,"--ambitions-only"),count=0;HistoryTerritorialAmbitionTests.Run(args[at+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" territorial ambition assertions");return;}
if(args.Contains("--coasts-only")){int at=Array.IndexOf(args,"--coasts-only"),count=0;HistoryCoastalTests.Run(args[at+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" coastal network assertions");return;}
if(args.Contains("--colonial-review")){HistoryColonialReview.Run(args);return;}
if(args.Contains("--colonial-only")){int at=Array.IndexOf(args,"--colonial-only"),count=0;HistoryColonialTests.Run(args[at+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" colonial continuity assertions");return;}
if(args.Contains("--archive-only")){int count=0;HistoryArchiveTests.Run((ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" persistent archive assertions");return;}
if(args.Contains("--territorial-audit")){HistoryTerritorialAudit.Run(args);return;}
if(args.Contains("--provincial-frontiers-only")){int at=Array.IndexOf(args,"--provincial-frontiers-only"),count=0;HistoryProvincialFrontierTests.Run(args[at+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" provincial frontier assertions");return;}
if(args.Contains("--history-resume-seconds-hash")){int at=Array.IndexOf(args,"--history-resume-seconds-hash");var w=WorldFile.Read(File.ReadAllBytes(args[at+1]));var session=new SimulationSession(new Prehistory(w),w.Prehistory);long target=long.Parse(args[at+2]);if(target>session.Seconds)session.AdvanceSeconds(target-session.Seconds,int.MaxValue);File.WriteAllText(args[at+3],session.Hash);Console.WriteLine("History resume "+session.Hash);return;}
if(args.Contains("--technical-review")){HistoryTechnicalReview.Run(args);return;}
if(args.Contains("--technical-only")){int count=0,at=Array.IndexOf(args,"--technical-only");HistoryTechnicalTests.Run(args[at+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" leveled-technology/imperial assertions");return;}
if(args.Contains("--verify-review-input")){
    int at=Array.IndexOf(args,"--verify-review-input");var source=WorldFile.Read(File.ReadAllBytes(args[at+1]));
    var rebuilt=new WorldPipeline().Generate(source.Recipe);
    if(!source.Stages.Select(s=>(s.Name,s.Hash)).SequenceEqual(rebuilt.Stages.Select(s=>(s.Name,s.Hash))))throw new Exception("Review input cannot be reproduced from its complete recipe");
    Console.WriteLine(WorldJson.Write(source.Recipe));return;
}
if(args.Contains("--world-load-review")){int at=Array.IndexOf(args,"--world-load-review");var timer=System.Diagnostics.Stopwatch.StartNew();var w=WorldFile.Read(File.ReadAllBytes(args[at+1]),status:stage=>Console.WriteLine(timer.ElapsedMilliseconds+" ms: "+stage));Console.WriteLine("Validated "+w.Surface.Cells.Length+" cells, historical hash "+w.Prehistory?.Hash+", elapsed "+timer.ElapsedMilliseconds+" ms");return;}
if(args.Contains("--canonical-wide-layout")){var types=new[]{typeof(HistoricalPolity),typeof(HistoricalRelation),typeof(HistoricalTown),typeof(HistoricalStateDynamics),typeof(HistoricalCampaign),typeof(HistoricalAgriculture),typeof(HistoricalCulture),typeof(HistoricalCultivar),typeof(Settlement),typeof(CultureIdentity),typeof(HistoricalAdministrativeAccount),typeof(HistoricalUrbanDemography),typeof(HistoricalLocalHarvest),typeof(HistoricalLocalCommunity),typeof(HistoricalGovernmentPolicy),typeof(HistoricalCampaignAdvance)};Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(types.Select(t=>new{Name=t.Name,Fields=t.GetFields().OrderBy(f=>f.Name,StringComparer.Ordinal).Select(f=>new{Name=f.Name,Type=f.FieldType.FullName,Scalar=f.FieldType.IsPrimitive||f.FieldType.IsEnum})})));return;}
if(args.Contains("--community-only")){int count=0,at=Array.IndexOf(args,"--community-only");HistoryCommunityTests.Run(args[at+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" local-community assertions");return;}
if(args.Contains("--history-map-only")){int count=0,at=Array.IndexOf(args,"--history-map-only");HistoryMapSnapshotTests.Run(args[at+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" historical display assertions");return;}
if(args.Contains("--historical-canonical-only")){int count=0;HistoricalCanonicalTests.Run((ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" historical canonical-byte assertions");return;}
if(args.Contains("--canonical-layout")){var types=new[]{typeof(HistoricalCityAggregate),typeof(HistoricalRegionAggregate),typeof(HistoricalCultureAggregate),typeof(HistoricalPolityAggregate),typeof(HistoricalLivingCity),typeof(HistoricalLivingRegion),typeof(HistoricalExchangeMemory),typeof(HistoricalCampaignTheater)};Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(types.Select(t=>new{Name=t.Name,Fields=t.GetFields().OrderBy(f=>f.Name,StringComparer.Ordinal).Select(f=>new{Name=f.Name,Type=f.FieldType.FullName,Scalar=f.FieldType.IsPrimitive||f.FieldType.IsEnum})})));return;}
if(args.Contains("--era-review")){HistoryEraReview.Run(args);return;}
if(args.Contains("--history-geography-review")){HistoryGeographyReview.Run(args);return;}
if(args.Contains("--news-audit")){PresentationNewsAudit.Run(args[Array.IndexOf(args,"--news-audit")+1]);return;}
if(args.Contains("--presentation-only")){int count=0;void CheckPresentation(bool ok,string message){count++;if(!ok)throw new Exception(message);}HistoryPresentationTests.Run(CheckPresentation);WorldSummaryTests.Run(CheckPresentation);Console.WriteLine("PASS "+count+" historical presentation and summary assertions");return;}
if(args.Contains("--atlas-only")){int count=0;HistoryAtlasTests.Run((ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" atlas presentation assertions");return;}
if(args.Contains("--crop-ecology-only")){int count=0;HistoryCropEcologyTests.Run((ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" crop ecology assertions");return;}
if(args.Contains("--provincial-only")){int count=0,at=Array.IndexOf(args,"--provincial-only");HistoryProvincialTests.Run((ok,message)=>{count++;if(!ok)throw new Exception(message);},args.Length>at+1?args[at+1]:null);Console.WriteLine("PASS "+count+" provincial-development assertions");return;}
if(args.Contains("--connected-only")){int at=Array.IndexOf(args,"--connected-only"),count=0;HistoryConnectedTests.Run(args[at+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" connected-development assertions");return;}
if(args.Contains("--interface-only")){int count=0;WorldInterfaceTests.Run((ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" world interface assertions");return;}
if(args.Contains("--living-only")){int at=Array.IndexOf(args,"--living-only"),count=0;HistoryLivingTests.Run(args[at+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" living-history assertions");return;}
if(args.Contains("--living-review")){HistoryLivingReview.Run(args);return;}
if(args.Contains("--aggregate-fixture")){int at=Array.IndexOf(args,"--aggregate-fixture");HistoryAggregateTests.Fixture(args[at+1],args[at+2]);return;}
if(args.Contains("--aggregate-only")){int at=Array.IndexOf(args,"--aggregate-only"),count=0;HistoryAggregateTests.Run(args[at+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" aggregate simulation assertions");return;}
if(args.Contains("--contact-only")){int count=0;HistoryDiplomacyTests.Run((ok,message)=>{count++;if(!ok)throw new Exception(message);});HistoryContactCacheTests.Run((ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" physical contact assertions");return;}
if(args.Contains("--routing-only")){int at=Array.IndexOf(args,"--routing-only"),count=0;HistoryRoutingPerformanceTests.Run(args[at+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" routing cache assertions");return;}
if(args.Contains("--performance-only")){int at=Array.IndexOf(args,"--performance-only"),count=0;HistoryPerformanceTests.Run(args[at+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" history performance assertions");return;}
if(args.Contains("--realism-project")){int at=Array.IndexOf(args,"--realism-project");HistoryRealismTests.Project(args[at+1],int.Parse(args[at+2]),args[at+3]);return;}
if(args.Contains("--realism-political")){int at=Array.IndexOf(args,"--realism-political"),count=0;HistoryRealismTests.Political(args[at+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" political realism assertions");return;}
if(args.Contains("--realism-only")){int at=Array.IndexOf(args,"--realism-only"),count=0;HistoryRealismTests.Run(args[at+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" ecology/history realism assertions");return;}
if(args.Contains("--realism-fixture")){int at=Array.IndexOf(args,"--realism-fixture");HistoryRealismTests.Fixture(args[at+1],int.Parse(args[at+2]),int.Parse(args[at+3]),args[at+4]);return;}
if(args.Contains("--emergence-only")){int at=Array.IndexOf(args,"--emergence-only"),count=0;HistoryEmergenceTests.Run(args[at+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" emergence assertions");return;}
if(args.Contains("--index-only")){int at=Array.IndexOf(args,"--index-only"),count=0;HistoryIndexTests.Run(args[at+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" history index assertions");return;}
if(args.Contains("--index-profile")){int at=Array.IndexOf(args,"--index-profile");HistoryIndexTests.Profile(args[at+1],int.Parse(args[at+2]),args[at+3],!args.Contains("--literal"));return;}
if(args.Contains("--history-save-years")){int at=Array.IndexOf(args,"--history-save-years");HistoryIndexTests.Prepare(args[at+1],args[at+3],int.Parse(args[at+2]));return;}
if(args.Contains("--history-fresh-fixture"))
{
    int at=Array.IndexOf(args,"--history-fresh-fixture");
    var world=WorldFile.Read(File.ReadAllBytes(args[at+1]));world.Prehistory=null;
    var engine=new Prehistory(world);world.Prehistory=engine.Start(engine.Prepare());
    File.WriteAllBytes(args[at+2],WorldFile.Write(world));Console.WriteLine("Fresh history fixture: "+world.Surface.Cells.Length+" cells; "+world.Prehistory.Hash);return;
}
if(args.Contains("--history-playback-hash"))
{
    int at=Array.IndexOf(args,"--history-playback-hash"),years=int.Parse(args[at+2]);
    if(years<1||years>10000)throw new ArgumentOutOfRangeException(nameof(years));
    var world=WorldFile.Read(File.ReadAllBytes(args[at+1]));var engine=new Prehistory(world);var source=world.Prehistory;string sourceHash=source.Hash;
    var session=new SimulationSession(engine,source);long begin=session.Seconds,target=checked(begin+years*SimulationTime.Year);int publications=0,lastReport=source.Years;
    while(session.Seconds<target)
    {
        long before=session.Seconds;var slice=SimulationClockView.Publication(before,target-before,1000*SimulationTime.Year);
        var next=session.AdvanceSeconds(slice.Seconds,slice.MaximumEvents,maximumGlobalWork:slice.MaximumGlobalWork);publications++;
        if(session.Seconds<=before||session.Seconds>before+slice.Seconds)throw new Exception("Playback publication lost or fabricated calendar progress.");
        if(next.Years-lastReport>=100){lastReport=next.Years;Console.WriteLine("Playback continuation year "+next.Years+", publications "+publications);}
    }
    var result=session.Snapshot();engine.Validate(result);
    if(source.ComputeHash()!=sourceHash||SimulationTime.Seconds(result)!=target)throw new Exception("Playback changed its source or lost exact requested time.");
    File.WriteAllText(args[at+3],result.Hash);Console.WriteLine("Playback resume "+result.Hash+"; "+publications+" bounded publications");return;
}
if(args.Contains("--history-ceiling-scenarios")){int at=Array.IndexOf(args,"--history-ceiling-scenarios");HistoryModernTests.CeilingScenarios(args[at+1],args[at+2]);return;}
if(args.Contains("--history-checkpoint-hash")){int at=Array.IndexOf(args,"--history-checkpoint-hash");var world=WorldFile.Read(File.ReadAllBytes(args[at+1]));new Prehistory(world).Validate(world.Prehistory);File.WriteAllText(args[at+2],world.Prehistory.Hash);Console.WriteLine("Validated checkpoint "+world.Prehistory.Hash);return;}
if(args.Contains("--parallel-history-only")){int at=Array.IndexOf(args,"--parallel-history-only"),count=0;HistoryParallelTests.Run(args[at+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" parallel history assertions");return;}
if(args.Contains("--history-hash-profile")){int at=Array.IndexOf(args,"--history-hash-profile");var world=WorldFile.Read(File.ReadAllBytes(args[at+1]));var rows=new List<string>{"stage,milliseconds"};foreach(var metric in SimulationHashProfile.Capture(world.Prehistory).OrderByDescending(m=>m.Milliseconds)){var row=FormattableString.Invariant($"{metric.Stage},{metric.Milliseconds:F3}");rows.Add(row);Console.WriteLine(row);}File.WriteAllLines(args[at+2],rows);return;}
if(args.Contains("--history-upgrade")){int at=Array.IndexOf(args,"--history-upgrade");var world=WorldFile.Read(File.ReadAllBytes(args[at+1]));var engine=new Prehistory(world);world.Prehistory=engine.UpgradeToModernHistory(world.Prehistory);engine.Validate(world.Prehistory);File.WriteAllBytes(args[at+2],WorldFile.Write(world));Console.WriteLine("Upgraded without rewriting the past: "+world.Prehistory.Hash);return;}
if(args.Contains("--modern-only")){int count=0;HistoryModernTests.Run((ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" modern history assertions");return;}

if(args.Contains("--history-work-profile")){int at=Array.IndexOf(args,"--history-work-profile");var w=WorldFile.Read(File.ReadAllBytes(args[at+1]));var engine=new Prehistory(w);var metrics=new List<SimulationWorkMetric>();engine.WorkObserver=metrics.Add;var next=engine.AdvanceYears(w.Prehistory,int.Parse(args[at+2]));engine.Validate(next);var rows=new List<string>{"stage,count,totalMilliseconds,maxMilliseconds,meanMilliseconds"};foreach(var g in metrics.GroupBy(m=>m.Stage).OrderByDescending(g=>g.Sum(m=>m.Milliseconds))){string row=FormattableString.Invariant($"{g.Key},{g.Count()},{g.Sum(m=>m.Milliseconds):F3},{g.Max(m=>m.Milliseconds):F3},{g.Average(m=>m.Milliseconds):F3}");rows.Add(row);Console.WriteLine(row);}File.WriteAllLines(args[at+3],rows);Console.WriteLine("HASH "+next.Hash);return;}

if(args.Contains("--institutional-only")){int count=0;HistoryInstitutionalTests.Run((ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" institutional succession assertions");return;}

if(args.Contains("--delegation-network-only")){int at=Array.IndexOf(args,"--delegation-network-only"),count=0;HistoryDelegationTests.Network(args[at+1],(ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" actual delegated network assertions");return;}
if(args.Contains("--delegation-only")){int count=0;HistoryDelegationTests.Run((ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" delegated administration and urban demography assertions");return;}
if(args.Contains("--local-history-only")){int count=0;HistoryLocalTests.Run((ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" local formation/administration assertions");return;}

if(args.Contains("--calendar-resume")){int at=Array.IndexOf(args,"--calendar-resume");var w=WorldFile.Read(File.ReadAllBytes(args[at+1]));var engine=new Prehistory(w);var s=engine.AdvanceSeconds(w.Prehistory,long.Parse(args[at+2]));engine.Validate(s);File.WriteAllText(args[at+3],s.Hash);if(args.Length>at+4){w.Prehistory=s;File.WriteAllBytes(args[at+4],WorldFile.Write(w));}Console.WriteLine("Calendar resume "+s.Hash);return;}

if(args.Contains("--calendar-only")){int count=0;SimulationCalendarTests.Run((ok,message)=>{count++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+count+" calendar assertions");return;}

if (args.Contains("--plate-shape-report"))
{
    int option = Array.IndexOf(args, "--plate-shape-report"), labelOption = Array.IndexOf(args, "--label");
    if (option + 1 >= args.Length) throw new ArgumentException("--plate-shape-report requires an output JSON path.");
    string label = labelOption >= 0 && labelOption + 1 < args.Length ? args[labelOption + 1] : Recipe.CurrentAlgorithm;
    var reports = new List<PlateShapeDiagnostics.Report>();
    foreach (int count in new[] { 80, 500 }) foreach (int seed in new[] { 73421, 42, 0, 17, 98765 })
    {
        var recipe = Recipe.Preset("Preview"); recipe.Seed = seed; recipe.MicroplateCount = count;
        var coarse = new World { Recipe = recipe, Surface = SphericalMesh.BuildCoarse(recipe, default) };
        PlateHierarchy.Microplates(coarse, default);
        var report = PlateShapeDiagnostics.Assess(coarse, label); reports.Add(report);
        Console.WriteLine($"{label} P{count} seed {seed}: q {report.MedianQ:F3}/{report.P90Q:F3}, axis {report.MedianAxisRatio:F3}/{report.P90AxisRatio:F3}, min {report.MinimumCells}, disconnected {report.DisconnectedPlates}");
    }
    string output = Path.GetFullPath(args[option + 1]); Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    File.WriteAllText(output, System.Text.Json.JsonSerializer.Serialize(reports, new System.Text.Json.JsonSerializerOptions { IncludeFields = true, WriteIndented = true }));
    return;
}

if (args.Contains("--island-ring-report"))
{
    int option = Array.IndexOf(args, "--island-ring-report"), detailOption = Array.IndexOf(args, "--detail");
    if (option + 1 >= args.Length) throw new ArgumentException("--island-ring-report requires an output JSON path.");
    int detail = detailOption >= 0 ? int.Parse(args[detailOption + 1]) : 20000;
    var reports = new List<IslandRingDiagnostics.Report>();
    foreach (int seed in new[] { 73421, 42, 0, 17, 98765 })
    {
        var recipe = Recipe.Preset("Preview"); recipe.Seed = seed; recipe.Detail = detail;
        var report = IslandRingDiagnostics.Assess(new WorldPipeline().Generate(recipe)); reports.Add(report);
        Console.WriteLine($"seed {seed}: suspicious plates {report.SuspiciousPlates}, detached islands {report.DetachedIslands}, island-near perimeter {report.CoveredPerimeterKm / report.EligiblePerimeterKm:P1}");
    }
    string output = Path.GetFullPath(args[option + 1]); Directory.CreateDirectory(Path.GetDirectoryName(output));
    File.WriteAllText(output, System.Text.Json.JsonSerializer.Serialize(reports, new System.Text.Json.JsonSerializerOptions { IncludeFields = true, WriteIndented = true }));
    return;
}

int assertions = 0;
if(args.Contains("--history-review-only")){int at=Array.IndexOf(args,"--history-review-only");var w=WorldFile.Read(File.ReadAllBytes(args[at+1]));string folder=Path.GetFullPath(args[at+2]);Directory.CreateDirectory(folder);HistoryAuditReview.Write(w,new Prehistory(w),w.Prehistory,folder);Console.WriteLine("Saved read-only review "+folder);return;}
if(args.Contains("--ocean-review")){OceanReview.Run(args);return;}
if(args.Contains("--history-resume-hash")){int at=Array.IndexOf(args,"--history-resume-hash");var w=WorldFile.Read(File.ReadAllBytes(args[at+1]));var engine=new Prehistory(w);var next=engine.AdvanceYears(w.Prehistory,int.Parse(args[at+2]));engine.Validate(next);File.WriteAllText(args[at+3],next.Hash);Console.WriteLine("History resume "+next.Hash);return;}
if(args.Contains("--history-town-trace")){int at=Array.IndexOf(args,"--history-town-trace");HistoryAuditReview.TraceTown(WorldFile.Read(File.ReadAllBytes(args[at+1])),int.Parse(args[at+2]),int.Parse(args[at+3]),args[at+4]);return;}
if(args.Contains("--hash-batches-only")){HashBatchTests.Run((ok,message)=>{assertions++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+assertions+" canonical hash batch assertions");return;}
if(args.Contains("--history-snapshot")){int at=Array.IndexOf(args,"--history-snapshot");var w=WorldFile.Read(File.ReadAllBytes(args[at+1]));string folder=Path.GetFullPath(args[at+2]);Directory.CreateDirectory(folder);HistoryAudit.Snapshot(w,new Prehistory(w),w.Prehistory,folder);Console.WriteLine("Saved historical snapshot "+folder);return;}
if(args.Contains("--history-profile")){int at=Array.IndexOf(args,"--history-profile");var w=WorldFile.Read(File.ReadAllBytes(args[at+1]));var e=new Prehistory(w);var s=w.Prehistory;var watch=new System.Diagnostics.Stopwatch();void Measure(string label,Action action){watch.Restart();for(int i=0;i<5;i++)action();Console.WriteLine($"{label}: {watch.Elapsed.TotalMilliseconds/5:F1}ms");}Measure("Hash",()=>s.ComputeHash());Measure("Validate",()=>e.Validate(s));Measure("Copy",()=>s.Copy());Measure("Annual step",()=>e.AdvanceYears(s,1));return;}
if(args.Contains("--history-benchmark")){int at=Array.IndexOf(args,"--history-benchmark");var watch=System.Diagnostics.Stopwatch.StartNew();var w=WorldFile.Read(File.ReadAllBytes(args[at+1]));Console.WriteLine($"Load {watch.Elapsed.TotalSeconds:F2}s");watch.Restart();var e=new Prehistory(w);Console.WriteLine($"Graph {watch.Elapsed.TotalSeconds:F2}s");var s=w.Prehistory;var samples=new List<double>();for(int i=0;i<20;i++){watch.Restart();s=e.AdvanceYears(s,1);samples.Add(watch.Elapsed.TotalMilliseconds);}watch.Restart();var codes=PrehistoryView.Codes(w,e,s);Console.WriteLine($"{w.Surface.Cells.Length} cells, {s.History.Archive.Count} events: annual ticks median {samples.OrderBy(x=>x).ElementAt(10):F1}ms, p95 {samples.OrderBy(x=>x).ElementAt(18):F1}ms; overlay {watch.Elapsed.TotalMilliseconds:F1}ms");return;}
if(args.Contains("--history-presentation-only")){HistoryPresentationTests.Run((ok,message)=>{assertions++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+assertions+" historical presentation assertions");return;}
if(args.Contains("--atlas-check")){HistoryAtlasTests.ReviewWorld(args[Array.IndexOf(args,"--atlas-check")+1]);return;}
if(args.Contains("--history-geography")){int at=Array.IndexOf(args,"--history-geography");var setup=new WorldSetup();var recipe=setup.Resolve(setup.Preset(args[at+1],new Recipe{Seed=int.Parse(args[at+2]),Detail=int.Parse(args[at+3])}));var world=new WorldPipeline().Generate(recipe);string file=Path.GetFullPath(args[at+4]);Directory.CreateDirectory(Path.GetDirectoryName(file)!);File.WriteAllBytes(file,WorldFile.Write(world));Console.WriteLine("Saved audit geography "+file+" "+world.Stages.Last().Hash);return;}
if(args.Contains("--history-cohort-review")){int at=Array.IndexOf(args,"--history-cohort-review");HistoryRegionalReview.Run(args[at+1],args[at+2]);return;}
if(args.Contains("--history-audit")){HistoryAudit.Run(args);return;}
if(args.Contains("--history-legend-only")){var w=WorldFile.Read(File.ReadAllBytes("artifacts/history-1/Earth-preview.world"));var s=w.Prehistory;foreach(var entry in SocietyView.Legend(s,HistoryOverlay.Religions)){if(s.History.CellReligion[entry.Cell]!=entry.ColorId)throw new Exception("Religion legend selected a converted holy site");assertions++;}Console.WriteLine("PASS "+assertions+" current-faith legend references");return;}
if(args.Contains("--history-era-only")){HistoryEraTests.Run((ok,message)=>{assertions++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+assertions+" era assertions");return;}
if(args.Contains("--history-only")){HistoryEraTests.Run((ok,message)=>{assertions++;if(!ok)throw new Exception(message);});HistoryTests.Run((ok,message)=>{assertions++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+assertions+" history assertions");return;}
if(args.Contains("--history-6-only")){HistoryEraTests.Run((ok,message)=>{assertions++;if(!ok)throw new Exception(message);});HistoryTests.Run((ok,message)=>{assertions++;if(!ok)throw new Exception(message);},"history-6");Console.WriteLine("PASS "+assertions+" history-6 assertions");return;}
if(args.Contains("--history-7-only")){HistoryTerritoryTests.Run((ok,message)=>{assertions++;if(!ok)throw new Exception(message);});HistoryEraTests.Run((ok,message)=>{assertions++;if(!ok)throw new Exception(message);});HistoryTests.Run((ok,message)=>{assertions++;if(!ok)throw new Exception(message);},"history-7");Console.WriteLine("PASS "+assertions+" history-7 assertions");return;}
if(args.Contains("--history-8-only")){HistoryFormationTests.Run((ok,message)=>{assertions++;if(!ok)throw new Exception(message);});HistoryTerritoryTests.Run((ok,message)=>{assertions++;if(!ok)throw new Exception(message);});HistoryEraTests.Run((ok,message)=>{assertions++;if(!ok)throw new Exception(message);});HistoryTests.Run((ok,message)=>{assertions++;if(!ok)throw new Exception(message);},"history-8");Console.WriteLine("PASS "+assertions+" history-8 assertions");return;}
if(args.Contains("--history-9-only")){HistoryRegionalTests.Run((ok,message)=>{assertions++;if(!ok)throw new Exception(message);});HistoryFormationTests.Run((ok,message)=>{assertions++;if(!ok)throw new Exception(message);});HistoryTerritoryTests.Run((ok,message)=>{assertions++;if(!ok)throw new Exception(message);});HistoryEraTests.Run((ok,message)=>{assertions++;if(!ok)throw new Exception(message);});HistoryTests.Run((ok,message)=>{assertions++;if(!ok)throw new Exception(message);},"history-9");Console.WriteLine("PASS "+assertions+" history-9 assertions");return;}
if(args.Contains("--regional-only")){HistoryRegionalTests.Run((ok,message)=>{assertions++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+assertions+" regional-learning assertions");return;}
if(args.Contains("--formation-only")){HistoryFormationTests.Run((ok,message)=>{assertions++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+assertions+" formation/diffusion assertions");return;}
if(args.Contains("--territory-only")){HistoryTerritoryTests.Run((ok,message)=>{assertions++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+assertions+" territorial assertions");return;}
if(args.Contains("--autonomy-scenario")){HistoryAutonomyTests.Run(args[Array.IndexOf(args,"--autonomy-scenario")+1],(ok,message)=>{assertions++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+assertions+" provincial autonomy assertions");return;}
if(args.Contains("--coastal-only")){CoastalDispersalTests.Run((ok,message)=>{assertions++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+assertions+" coastal assertions");return;}
if(args.Contains("--prehistory-replay")){var at=Array.IndexOf(args,"--prehistory-replay");var w=WorldFile.Read(File.ReadAllBytes(args[at+1]));var engine=new Prehistory(w);var s=engine.Prepare(w.Prehistory.Origin);s.Version=w.Prehistory.Version;s.Seal();s=engine.Start(s);s=engine.Advance(s,w.Prehistory.Years/Prehistory.YearsPerStep(s));if(s.Hash!=w.Prehistory.Hash)throw new Exception("Native/CoreCLR demographic replay mismatch");Console.WriteLine("PASS native/CoreCLR demographic replay "+s.Hash);return;}
if(args.Contains("--prehistory-only")){PrehistoryTests.Run((ok,message)=>{assertions++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+assertions+" prehistory assertions");return;}
if(args.Contains("--presets-only")){PresetTests.Run((ok,message)=>{assertions++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+assertions+" preset assertions");return;}
if(args.Contains("--shore-only")){ShoreSamplerTests.Run((ok,message)=>{assertions++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+assertions+" shore sampler assertions");return;}
if(args.Contains("--subcell-only")){SubcellTests.Run((ok,message)=>{assertions++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+assertions+" sub-cell assertions");return;}
if(args.Contains("--display-performance-only")){int at=Array.IndexOf(args,"--detail");DisplayPerformanceTests.Run((ok,message)=>{assertions++;if(!ok)throw new Exception(message);},at<0?20000:int.Parse(args[at+1]),args.Contains("--mesh-only"));Console.WriteLine("PASS "+assertions+" display assertions");return;}
if(args.Contains("--setup-only")){SetupTests.Run((ok,message)=>{assertions++;if(!ok)throw new Exception(message);});Console.WriteLine("PASS "+assertions+" setup assertions");return;}
// Misspelled or not-yet-built targeted flags must not silently launch the much
// longer complete corpus. The following legacy subsets are dispatched below.
if(args.Length>0&&!(args.Length==1&&new[]{"--ecology-review","--ecology-only","--terrain-controls-only","--world-library-only","--plates-only","--tectonics-only","--climate-only","--hydrology-only","--continents-only"}.Contains(args[0])))
    throw new ArgumentException("Unrecognized test command: "+string.Join(" ",args)+". Run without arguments for the complete suite.");
void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }
LanguageTests.Run(Check);
HistoryEnvironmentalTests.Run(Check);
WorldInterfaceTests.Run(Check);
void Connected(int[] members, Func<int, int[]> neighbors, string name)
{ var set = members.ToHashSet(); var visited = new HashSet<int> { members[0] }; var queue = new Queue<int>(); queue.Enqueue(members[0]); while (queue.Count > 0) foreach (int n in neighbors(queue.Dequeue())) if (set.Contains(n) && visited.Add(n)) queue.Enqueue(n); Check(visited.Count == members.Length, name + " disconnected"); }
void Validate(World w)
{
    var s = w.Surface; int n = s.Cells.Length; Check(n - s.Edges.Length + s.Triangles.Length / 3 == 2, "Euler characteristic");
    double expected = 4 * Math.PI * Math.Pow(w.Recipe.RadiusKm * 1000.0, 2); Check(Math.Abs(s.Cells.Sum(c => (double)c.AreaM2) / expected - 1) < .00001, "Spherical area");
    foreach (var c in s.Cells) { Check(Math.Abs(c.Position.Length - 1) < .000001, "Unit sphere"); Check(c.AreaM2 > 0, "Positive area"); foreach (int nb in c.Neighbors) Check(s.Cells[nb].Neighbors.Contains(c.Id), "Symmetric adjacency"); }
    Check(n == w.Recipe.Detail, "Detail is terrain resolution"); Check(w.CoarseSurface.Cells.Length == 20000, "Fixed 20,000-cell coarse plate base"); Check(w.Microplates.Length == w.Recipe.MicroplateCount, "Plate count");
    Check(w.ContinentalCandidateScores.Length == w.Recipe.Interconnectedness, "Coarse search diagnostics retained through the pipeline");
    Check(w.ContinentalScore == w.ContinentalCandidateScores.Max() && w.ContinentalCandidate == Array.IndexOf(w.ContinentalCandidateScores, w.ContinentalScore), "Pipeline retains the selected maximum and earliest tie");
    foreach (var p in w.Microplates)
    {
        Check(p.Cells.Length >= 1, "Useful plate"); Connected(p.Cells, i => s.Cells[i].Neighbors, "plate");
        Check(p.Cells.All(c => w.CellMicroplate[c] == p.Id), "Membership provenance");
        Check(p.ParentAffinityId >= 0 && p.ActiveActorId == p.Id, "Independent plate motion");
        Check(p.Cells.Sum(c => s.Cells[c].AreaM2) == p.AreaM2, "Plate area");
    }
    foreach (var p in w.Parents)
    {
        Connected(p.Microplates, i => w.Microplates[i].Neighbors, "superplate");
        Check(p.ContinentalFraction >= 0 && p.ContinentalFraction <= 1, "Superplate crust fraction");
    }
    if (w.Microplates.Length >= 8) Check(w.Boundaries.Any(b => b.Ghost), "Parent seams produce ghost boundaries");
    Check(w.Boundaries.Any(b => !b.Ghost && b.Kind != BoundaryKind.Inactive), "Parent boundaries remain active");
    Check(w.Climate.Classification.Length == n && w.Climate.Classification.All(c => Enum.IsDefined(c)), "Climate classes");
    foreach (var season in new[] { w.Climate.NorthernSpring, w.Climate.NorthernSummer, w.Climate.NorthernAutumn, w.Climate.NorthernWinter })
    {
        Check(season.TemperatureTenthsC.Length == n && season.TemperatureTenthsC.All(t => t >= -900 && t <= 650), "Climate temperature bounds");
        Check(season.PrecipitationMm.Length == n && season.PrecipitationMm.All(p => p >= 0), "Seasonal precipitation bounds");
        Check(season.WindEastMilli.Length == n && season.CurrentEastMilli.Length == n, "Circulation retained");
    }
    Check(w.Features.All(f => f.Cells.All(c => c >= 0 && c < n)), "Feature membership");
    for (int i = 0; i < n; i++) Check(w.FoldPermille[i] >= 0 && w.CratonPermille[i] >= 0 && w.BasinPermille[i] >= 0 && w.PlateauPermille[i] >= 0 && w.FoldPermille[i] + w.CratonPermille[i] + w.BasinPermille[i] + w.PlateauPermille[i] == 1000, "Continuous province partition");
    HydrologyTests.Validate(w, Check);
    Check(w.Hydrology.OceanConnected.Length == n, "Ocean connectivity mask");
    Check(Enumerable.Range(0, n).All(i => !w.Hydrology.OceanConnected[i] || w.ElevationM[i] < 0), "Ocean connectivity only marks below-sea-level cells");
    Check(!w.ProtectedInlandSea.Any(b => b), "No custom inland-sea preservation");
}
void ContinentalSearch()
{
    var recipe = Recipe.Preset("Preview");
    var surface = SphericalMesh.BuildCoarse(recipe, default);
    World Assign(Recipe r, CancellationToken cancellation = default)
    {
        var w = new World { Recipe = r, Surface = surface };
        PlateHierarchy.Microplates(w, default); Continents.Build(w, cancellation); return w;
    }
    {
        World previous = null;
        foreach (int attempts in new[] { 1, 2, 7, 50 })
        {
            var r = recipe.Clone(); r.Interconnectedness = attempts;
            var w = Assign(r);
            Check(w.ContinentalCandidateScores.Length == attempts, "Exactly N land/sea candidates are scored");
            Check(w.ContinentalScore == w.ContinentalCandidateScores.Max(), "Highest scoring candidate selected");
            Check(w.ContinentalCandidate == Array.IndexOf(w.ContinentalCandidateScores, w.ContinentalScore), "Equal scores prefer earliest candidate");
            foreach (var plate in w.Microplates)
                Check(plate.Cells.All(c => w.Continent[c] == plate.ContinentId), "Land/sea assignment uses whole plates");
            if (previous != null)
            {
                Check(previous.CellMicroplate.SequenceEqual(w.CellMicroplate), "Search does not regenerate plate geometry");
                Check(previous.ContinentalCandidateScores.SequenceEqual(w.ContinentalCandidateScores.Take(previous.ContinentalCandidateScores.Length)), "Candidate seeds form a stable prefix");
                Check(w.ContinentalScore >= previous.ContinentalScore, "More search iterations cannot lower the score");
            }
            previous = w;
        }
        var repeat = Assign(previous!.Recipe.Clone());
        Check(repeat.Continent.SequenceEqual(previous.Continent) && repeat.ContinentalCandidateScores.SequenceEqual(previous.ContinentalCandidateScores), "Search is reproducible");
        var detail = previous.Recipe.Clone(); detail.Detail = 5000; detail.Irregularity = .1;
        var changed = Assign(detail);
        Check(changed.Continent.SequenceEqual(previous.Continent) && changed.ContinentalCandidateScores.SequenceEqual(previous.ContinentalCandidateScores), "Search is independent of fine detail and irregularity");
    }
    var waterRecipe = recipe.Clone(); waterRecipe.LandFraction = 0; waterRecipe.Interconnectedness = 50;
    var water = Assign(waterRecipe);
    Check(water.ContinentalCandidateScores.Length == 50 && water.ContinentalCandidateScores.Distinct().Count() == 1 && water.ContinentalCandidate == 0, "All-water candidates exercise a real tie and still run all iterations");
    foreach (int invalidValue in new[] { 0, 51 })
    {
        var invalid = recipe.Clone(); invalid.Interconnectedness = invalidValue;
        try { invalid.Validate(); throw new Exception("Invalid search count accepted"); } catch (ArgumentException) { assertions++; }
    }
    foreach (string field in new[] { "Interconnectedness" })
    {
        var values = System.Text.Json.Nodes.JsonNode.Parse(WorldJson.Write(recipe))!.AsObject(); values.Remove(field);
        try { WorldJson.ReadRecipe(values.ToJsonString()); throw new Exception("Missing " + field + " accepted"); } catch (ArgumentException) { assertions++; }
    }
    using var cancel = new CancellationTokenSource(); cancel.Cancel();
    try { Assign(recipe, cancel.Token); throw new Exception("Continental search cancellation ignored"); } catch (OperationCanceledException) { assertions++; }
}
try
{
    if(args.Length==0)HistoryLivingMechanismTests.Run(Check);
    HistoryArchiveTests.Run(Check);
    HashBatchTests.Run(Check);
    if (args.Contains("--ecology-review")) { EcologyTests.WriteReview(); return; }
    if (args.Contains("--ecology-only")) { EcologyTests.Run(Check); Console.WriteLine("PASS " + assertions + " ecology assertions."); return; }
    if (args.Contains("--terrain-controls-only")) { TerrainControlTests.Run(Check); Console.WriteLine("PASS " + assertions + " terrain control assertions."); return; }
    if (args.Contains("--world-library-only")) { WorldLibraryTests.Run(Check); Console.WriteLine("PASS " + assertions + " world library assertions."); return; }
    PlateShapeTests.Run(Check);
    if (args.Contains("--plates-only")) { Console.WriteLine("PASS " + assertions + " plate growth assertions."); return; }
    TectonicTests.Run(Check);
    TerrainScaleTests.Run(Check);
    DrainageIncisionTests.Run(Check);
    if (args.Contains("--tectonics-only")) { Console.WriteLine("PASS " + assertions + " tectonic mechanism assertions."); return; }
    ClimateTests.Run(Check);
    if (args.Contains("--climate-only")) { Console.WriteLine("PASS " + assertions + " climate assertions."); return; }
    HydrologyTests.Run(Check);
    LakePresentationTests.Run(Check);
    EarthTests.Run(Check);
    WorldLibraryTests.Run(Check);
    if (args.Contains("--hydrology-only")) { Console.WriteLine("PASS " + assertions + " hydrology / Earth assertions."); return; }
    ContinentalSearch();
    if (args.Contains("--continents-only")) { Console.WriteLine("PASS " + assertions + " continental search assertions."); return; }
    TerrainControlTests.Run(Check);
    EcologyTests.Run(Check);
    SubcellTests.Run(Check);
    ShoreSamplerTests.Run(Check);
    Check(StateHash.Compute(-0.0) == StateHash.Compute(0.0), "Signed zero has a canonical hash across JSON runtimes");
    foreach(double x in new[]{1e-20,1e-8,.2,1.0,100.0,1e12})Check(Math.Abs(D.Sqrt(x)-D.Q(Math.Sqrt(x)))<=1e-9,"Deterministic square root accuracy");
    var recipe = Recipe.Preset("Preview"); var pipe = new WorldPipeline(); var world = pipe.Generate(recipe); Validate(world);
    string[] hashes = world.Stages.Select(s => s.Hash).ToArray();
    foreach (var repeat in new[] { pipe.Generate(recipe), pipe.Reapply(recipe), pipe.Generate(WorldJson.ReadRecipe(WorldJson.Write(recipe))), pipe.Reconstruct(WorldJson.ReadWorld(WorldJson.Write(world))) }) Check(hashes.SequenceEqual(repeat.Stages.Select(s => s.Hash)), "All-stage reconstruction equivalence");
    Check(StateHash.Compute(world) == StateHash.Compute(WorldJson.ReadWorld(WorldJson.Write(world))), "Result roundtrip");
    var invalid = recipe.Clone(); invalid.AlgorithmVersion = "future"; try { pipe.Generate(invalid); throw new Exception("Version accepted"); } catch (ArgumentException) { assertions++; }
    try { WorldJson.ReadRecipe(WorldJson.Write(recipe).Replace("\"Hydraulic\":0.5,", "")); throw new Exception("Incomplete recipe accepted"); } catch (ArgumentException) { assertions++; }
    var minimum = recipe.Clone(); minimum.MicroplateCount = 4; minimum.ContinentCount = 2; Validate(pipe.Generate(minimum));
    using (var cancel = new CancellationTokenSource()) { cancel.Cancel(); try { pipe.Generate(recipe, cancellation: cancel.Token); throw new Exception("Cancellation ignored"); } catch (OperationCanceledException) { assertions++; } }
    var from = new V(1, 0, 0); var to = new V(0, 1, 0); var direction = new V(0, 1, 0); var transported = V.Transport(direction, from, to); Check(Math.Abs(V.Dot(transported, to)) < .000001 && V.Dot(transported, new V(-1, 0, 0)) > .99999, "Great-circle transport");
    var detailRecipe = recipe.Clone(); detailRecipe.Detail = 5000; detailRecipe.Irregularity = .1;
    var lowDetail = pipe.Generate(detailRecipe); Validate(lowDetail);
    Check(world.CoarseHash == lowDetail.CoarseHash, "Detail and irregularity preserve the coarse partition");
    Check(world.Stages.Last().Hash != lowDetail.Stages.Last().Hash && lowDetail.ElevationM.Length == 5000, "Detail changes authoritative terrain");
    int changedHeights = 0;
    foreach (var cell in lowDetail.Surface.Cells)
        if (Math.Abs(lowDetail.ElevationM[cell.Id] - world.ElevationM[Erosion.Nearest(world.Surface, cell.Position)]) > 25) changedHeights++;
    Check(changedHeights > 500, "Fine terrain is recalculated, not merely rendered with extra polygons");
    string beforeView = StateHash.Compute(world);
    foreach (Layer layer in new[] { Layer.Terrain, Layer.Satellite, Layer.Climate, Layer.Heightmap })
    {
        var globe = GlobeView.Build(world, layer, detail: 20000);
        Check(globe.CellIds.All(i => i >= 0 && i < world.Recipe.Detail), "Cell surface picking provenance");
        if (layer != Layer.Heightmap) for (int i = 0; i < globe.Triangles.Length; i += 3)
        {
            int a=globe.Triangles[i]*3,b=globe.Triangles[i+1]*3,c=globe.Triangles[i+2]*3;
            for(int channel=0;channel<3;channel++)Check(globe.Colors[a+channel]==globe.Colors[b+channel] && globe.Colors[a+channel]==globe.Colors[c+channel],"Indexed categorical cell colors are crisp");
        }
        foreach (double longitude in new[] { 0.0, 179.0, -179.0 })
        {
            var map = MapView.Build(world, layer, centerLongitude: longitude, detail: 20000);
            Check(map.IsMap && map.Unlit && map.CellIds.Length * 3 == map.Positions.Length, "Map projection contract");
            Check(map.Positions.All(float.IsFinite), "Finite map coordinates");
            double mapArea = 0;
            for (int t = 0; t < map.Triangles.Length; t += 3)
            {
                int a = map.Triangles[t] * 3, b = map.Triangles[t + 1] * 3, c = map.Triangles[t + 2] * 3;
                double xa = map.Positions[a], xb = map.Positions[b], xc = map.Positions[c];
                Check(xa >= -2.00001 && xa <= 2.00001 && xb >= -2.00001 && xb <= 2.00001 && xc >= -2.00001 && xc <= 2.00001, "Map seam clipping");
                Check(Math.Max(xa, Math.Max(xb, xc)) - Math.Min(xa, Math.Min(xb, xc)) <= 2.001, "No triangles crossing map width");
                mapArea += Math.Abs((xb - xa) * (map.Positions[c + 1] - map.Positions[a + 1]) - (map.Positions[b + 1] - map.Positions[a + 1]) * (xc - xa)) / 2;
            }
            Check(Math.Abs(mapArea - 8) < .08, "Projected map covers the globe without gaps or overlap: " + mapArea + " " + layer + " " + longitude);
        }
    }
    var mapGrid = SurfaceMesh.Chunks(world, new ViewOptions { Projection = Projection.Map, GridSpacing = 30 }).Last();
    for (int i = 0; i < mapGrid.Lines.Length; i += 6)
        Check(Math.Abs(mapGrid.Lines[i] - mapGrid.Lines[i + 3]) < .0002 || Math.Abs(mapGrid.Lines[i + 1] - mapGrid.Lines[i + 4]) < .0002, "Map grid stays on meridians/parallels through pole endpoints");
    var overlayOptions = new ViewOptions { Wireframe = true, ShowPlates = true, GridSpacing = 30 };
    var overlay = SurfaceMesh.Combine(SurfaceMesh.Chunks(world, overlayOptions));
    Check(overlay.Lines.Length >= world.Surface.Edges.Length * 6, "Wireframe includes every dual cell edge");
    Check(overlay.LineColors.Length == overlay.Lines.Length, "Overlay colors accompany every line");
    var terrainPlates = SurfaceMesh.Combine(SurfaceMesh.Chunks(world, new ViewOptions { ShowPlates = true, GridSpacing = 0 }));
    var plateView = GlobeView.Build(world, Layer.Microplates);
    Check(terrainPlates.Colors.SequenceEqual(GlobeView.Build(world,Layer.Terrain).Colors), "Legacy plate overlay option does not replace terrain colors; plates have their own view");
    var climatePlates = SurfaceMesh.Combine(SurfaceMesh.Chunks(world, new ViewOptions { Layer = Layer.Climate, ShowPlates = true, GridSpacing = 0 }));
    Check(climatePlates.Colors.SequenceEqual(GlobeView.Build(world, Layer.Climate).Colors), "Plate outlines preserve climate colors");
    var riverOptions = new ViewOptions { ShowRivers = true, GridSpacing = 0 };
    var riverChunks = SurfaceMesh.Chunks(world, riverOptions).ToArray();
    int surfaceChunkCount = (world.Surface.Edges.Length + SurfaceMesh.EdgesPerChunk - 1) / SurfaceMesh.EdgesPerChunk;
    var ribbonChunks = riverChunks.Skip(surfaceChunkCount).ToArray();
    Check(riverChunks.Length == SurfaceMesh.ExpectedChunkCount(world, riverOptions), "River overlay reports its bounded presentation chunk count");
    Check(ribbonChunks.All(p => p.CellIds.Length <= SurfaceMesh.RiverReachesPerChunk * 6 && p.Triangles.Length == p.CellIds.Length), "River ribbons stay bounded and use triangle geometry");
    Check(ribbonChunks.Sum(p => p.CellIds.Length) == world.Hydrology.Rivers.Length * 6, "Every river reach produces one two-triangle ribbon");
    var ribbonPositions = ribbonChunks.SelectMany(p => p.Positions).ToArray();
    var scalable = world.Hydrology.Rivers.Select((r, i) => new { Reach = r, Index = i }).Where(x => x.Reach.WidthM > 100).OrderBy(x => x.Reach.WidthM).ToArray();
    double RenderedWidth(int reach)
    {
        int p = reach * 18; double x = ribbonPositions[p] - ribbonPositions[p + 3], y = ribbonPositions[p + 1] - ribbonPositions[p + 4], z = ribbonPositions[p + 2] - ribbonPositions[p + 5];
        return Math.Sqrt(x * x + y * y + z * z);
    }
    Check(scalable.Length < 2 || scalable[0].Reach.WidthM == scalable[^1].Reach.WidthM || RenderedWidth(scalable[^1].Index) > RenderedWidth(scalable[0].Index), "Major rivers render wider than smaller above-floor rivers");
    using (var cancelView = new CancellationTokenSource())
    {
        var enumerator = SurfaceMesh.Chunks(world, overlayOptions, cancelView.Token).GetEnumerator();
        Check(enumerator.MoveNext(), "First presentation chunk available"); cancelView.Cancel();
        try { enumerator.MoveNext(); throw new Exception("Presentation cancellation ignored"); } catch (OperationCanceledException) { assertions++; }
    }
    Check(beforeView == StateHash.Compute(world), "Views never mutate the world");
    Check(Climate.Classify(26, 25, 1800, 1700) == Koppen.Af, "Tropical rainforest");
    Check(Climate.Classify(35, 20, 10, 10) == Koppen.BWh, "Hot desert");
    Check(Climate.Classify(25, 8, 30, 500) == Koppen.Csa, "Mediterranean summer drought");
    Check(Climate.Classify(-4, -25, 50, 50) == Koppen.EF, "Ice cap");
    foreach (var entry in GoldenCorpus.Recipes())
    {
        var w = pipe.Generate(entry.Recipe); Validate(w);
        if (entry.Name == "earth")
        {
            // The owner rebased the slider20-fold. Preserve the old geographic
            // regression contract at equivalent forcing, not at a different input.
            // Default100% still receives full hydraulic validation above and exact
            // snapshot/golden checks below; its coarse Amazon limitation is recorded.
            var lowerRecipe = w.Recipe.Clone(); lowerRecipe.LakeAmountPercent = 5;
            var lower = pipe.Generate(lowerRecipe);
            EarthTests.LegacyForcingLakeSanity(lower, Check);
            HydrologyTests.Validate(lower, Check);
            Check(w.Hydrology.Waters.Where(b => b.Kind != WaterKind.Ocean).Sum(b => (double)b.AreaM2)
                >= lower.Hydrology.Waters.Where(b => b.Kind != WaterKind.Ocean).Sum(b => (double)b.AreaM2), "Rebased Earth default retains at least as much lake area as previous forcing");
            Check(w.ElevationM.SequenceEqual(lower.ElevationM) && StateHash.Compute(w.Climate) == StateHash.Compute(lower.Climate), "Lake amount cannot change Earth terrain or climate");
        }
        Check(StateHash.Compute(w) == StateHash.Compute(WorldFile.Read(WorldFile.Write(w))), entry.Name + " compact snapshot roundtrip");
        string golden = Path.Combine("tests", "golden", "v3", entry.Name + ".hashes.json"); if (!File.Exists(golden)) throw new Exception("Missing reviewed golden: " + golden); Check(WorldJson.Read<List<StageRecord>>(File.ReadAllText(golden)).Select(s => s.Hash).SequenceEqual(w.Stages.Select(s => s.Hash)), entry.Name + " golden stage hashes");
        Console.WriteLine("PASS " + entry.Name + " " + w.Stages.Last().Hash);
    }
    Console.WriteLine("PASS " + assertions + " assertions; topology, hierarchy, geology, erosion, presentation, serialization, cancellation and determinism.");
}
catch (Exception e) { Console.Error.WriteLine(e); Environment.ExitCode = 1; }
