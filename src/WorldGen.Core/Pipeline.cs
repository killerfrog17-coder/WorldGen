using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace WorldGen.Core
{
    public static partial class StateHash
    {
        [ThreadStatic] static bool literalAggregateRecords;
        // Diagnostic reference for the typed historical record writers.
        public static string ComputeHistoricalReference(object value,CancellationToken cancellation=default)
        {bool previous=literalAggregateRecords;literalAggregateRecords=true;try{return Compute(value,cancellation);}finally{literalAggregateRecords=previous;}}
        static readonly ConcurrentDictionary<Type, string> TypeNames = new ConcurrentDictionary<Type, string>();
        static readonly ConcurrentDictionary<Type, FieldInfo[]> Fields = new ConcurrentDictionary<Type, FieldInfo[]>();
        static StateHash()
        {
            CheckWideHistoricalRecords();
            CheckRecord(typeof(HistoricalCoastChart),"Region:System.Int32","SourceCulture:System.Int32","Year:System.Int32");
            CheckRecord(typeof(HistoricalCityAggregate),"AreaKm2:System.Int64","BaseCropFactor:System.Double","BaseFarming:System.Int32","BaseFoodMultiplier:System.Double","BaseUrbanCapacity:System.Int64","ExchangeMilli:System.Int32","FloodMilli:System.Int32","FoodYieldMilli:System.Int32","Id:System.Int32","LastHarvestYear:System.Int32","LastPoliticalYear:System.Int32","LastVoyageYear:System.Int32","LastYear:System.Int32","Neighbors:System.Int32[]","ParentCity:System.Int32","Rural:System.Int64","RuralCapacity:System.Int64","SoilMilli:System.Int32","StoredYieldMilli:System.Int32","Suburban:System.Int64","Supported:System.Boolean");
            CheckRecord(typeof(HistoricalRegionAggregate),"AreaKm2:System.Int64","AridityMilli:System.Int32","BestCell:System.Int32","Capacity:System.Int64","Culture:System.Int32","Id:System.Int32","LastCultureYear:System.Int32","LastYear:System.Int32","MeanTenthsC:System.Int32","NativeMask:System.Int32","OpportunityMilli:System.Int32","ReliefM:System.Int32","Routes:System.Int32[]","SoilMilli:System.Int32","StoredMilli:System.Int32","YieldMilli:System.Int32");
            CheckRecord(typeof(HistoricalCultureAggregate),"Cities:WorldGen.Core.HistoricalLocalBag","CityCount:System.Int32","CivilizationSince:System.Int32","FoodCapacity:System.Int64","Frontier:WorldGen.Core.HistoricalLocalBag","Id:System.Int32","LargestCity:System.Int64","LastYear:System.Int32","NextTechnology:System.Int32","Region:System.Int32","Rural:System.Int64","SupportedCities:System.Int32","SupportedUrban:System.Int64","Urban:System.Int64");
            CheckRecord(typeof(HistoricalPolityAggregate),"Cities:WorldGen.Core.HistoricalLocalBag","CityCount:System.Int32","ConquestDebtMilli:System.Int32","Frontier:WorldGen.Core.HistoricalLocalBag","Id:System.Int32","LastSuccession:System.Int32","LastYear:System.Int32","ReachKm:System.Int32","Receipts:System.Int64","Remote:System.Int64","RemoteAreaKm2:System.Int64","RemoteCapacity:System.Int64","Rural:System.Int64","ServiceCost:System.Int64","Suburban:System.Int64","SuccessionDue:System.Int32","UnrestMilli:System.Int32","Urban:System.Int64");
            CheckRecord(typeof(HistoricalLivingCity),"AbandonedYear:System.Int32","DeclineYears:System.Int32","DisruptedUntil:System.Int32","LastContactYear:System.Int32","PeakPopulation:System.Int64","PeakYear:System.Int32","Productivity:System.Double","StressMilli:System.Int32");
            CheckRecord(typeof(HistoricalLivingRegion),"CivicExposure:System.Int32","CivicTeacher:System.Int32","CommittedAreaKm2:System.Int64","Exposure:System.Int32","LastExchangeYear:System.Int32","SettledPopulation:System.Int64","Teacher:System.Int32");
            CheckRecord(typeof(HistoricalExchangeMemory),"Capabilities:System.Int64[]","LastDirectYear:System.Int32[]","LastYear:System.Int32[]","Peers:System.Int32[]");
            CheckRecord(typeof(HistoricalCampaignTheater),"Cell:System.Int32","SupplyA:System.Int32","SupplyB:System.Int32");
            CheckRecord(typeof(HistoricalEvent),"Category:WorldGen.Core.ChronicleCategory","Cell:System.Int32","CultureId:System.Int32","Id:System.Int32","Kind:System.String","OtherPolityId:System.Int32","PolityId:System.Int32","ReligionId:System.Int32","SettlementId:System.Int32","Text:System.String","Year:System.Int32");
            CheckRecord(typeof(HistoricalLeader),"Administration:System.Int32","Ambition:System.Int32","Defeats:System.Int32","Diplomacy:System.Int32","EndedYear:System.Int32","ExpectedEndYear:System.Int32","Id:System.Int32","Legacy:System.String","Military:System.Int32","Name:System.String","PolityId:System.Int32","PredecessorId:System.Int32","Reforms:System.Int32","Renown:System.Int32","Selection:System.String","StartedYear:System.Int32","Victories:System.Int32");
            CheckRecord(typeof(Cell), "AreaM2:System.Int64", "DistanceM:System.Int32[]", "Id:System.Int32", "Neighbors:System.Int32[]", "Position:WorldGen.Core.V");
            CheckRecord(typeof(Edge), "A:System.Int32", "B:System.Int32", "Id:System.Int32", "LengthM:System.Int32", "TriangleA:System.Int32", "TriangleB:System.Int32");
            CheckRecord(typeof(V), "X:System.Double", "Y:System.Double", "Z:System.Double");
            CheckRecord(typeof(HabitatCell), "AridityMilli:System.Int32", "Biome:WorldGen.Core.Biome", "ColdTenthsC:System.Int32", "FirePotentialPermille:System.Int32",
                "FloodDaysPerYear:System.Int32", "FloodFractionPermille:System.Int32", "FloodPulsesPerYear:System.Int32", "Flooding:WorldGen.Core.FloodRegime",
                "GrowingDays:System.Int32", "Habitat:WorldGen.Core.Habitat", "MeanTenthsC:System.Int32", "PermafrostPermille:System.Int32", "PetMm:System.Int32",
                "ProductivityGCM2Year:System.Int32", "ReliefM:System.Int32", "SalinityMilliPpt:System.Int32", "Soil:WorldGen.Core.SoilKind", "SoilFertilityPermille:System.Int32",
                "WarmSeasonDryPermille:System.Int32", "WarmTenthsC:System.Int32", "WaterDeficitMm:System.Int32", "WaterTableCm:System.Int32");
        }
        static void CheckRecord(Type type, params string[] expected)
        {
            var actual = type.GetFields(BindingFlags.Instance | BindingFlags.Public).OrderBy(f => f.Name, StringComparer.Ordinal).Select(f => f.Name + ":" + f.FieldType.FullName);
            if (!actual.SequenceEqual(expected)) throw new InvalidOperationException("Update the canonical hash writer for " + type.Name + " before changing its record layout.");
        }
        public static string Compute(object value, CancellationToken cancellation = default)
        {
            using (var sha = CanonicalSha256.Create())
            using (var crypto = new CryptoStream(Stream.Null, sha, CryptoStreamMode.Write))
            using (var buffer = new BufferedStream(new CancellationStream(crypto, cancellation), 65536))
            {
                using (var writer = new BinaryWriter(buffer, Encoding.UTF8, true)) Write(writer, value);
                buffer.Flush(); crypto.FlushFinalBlock();
                return string.Concat(sha.Hash.Select(b => b.ToString("x2")));
            }
        }
        sealed class CancellationStream : Stream
        {
            readonly Stream target; readonly CancellationToken token;
            public CancellationStream(Stream target, CancellationToken token) { this.target = target; this.token = token; }
            public override void Write(byte[] bytes, int offset, int count) { token.ThrowIfCancellationRequested(); target.Write(bytes, offset, count); }
            public override void Flush() => target.Flush();
            public override bool CanRead => false; public override bool CanSeek => false; public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override int Read(byte[] b, int o, int c) => throw new NotSupportedException();
            public override long Seek(long o, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
        }
        static void Write(BinaryWriter w, object o)
        {
            if (o == null) { w.Write((byte)0); return; }
            w.Write((byte)1);
            // Preserve the exact canonical byte format while avoiding type lookup
            // and reflection on the millions of scalar values hashed in WASM.
            if (o is string text) { w.Write("System.String"); w.Write(text); return; }
            if(o is HistoricalCellTree spatial){spatial.WriteCanonicalFingerprint(w);return;}
            if(o is HistoricalFrozenAdministration frozen){w.Write("WorldGen.Core.HistoricalFrozenAdministration");w.Write(frozen.Fingerprint());return;}
            if (o is int i) { w.Write("System.Int32"); w.Write(i); return; }
            if (o is long l) { w.Write("System.Int64"); w.Write(l); return; }
            if (o is bool b) { w.Write("System.Boolean"); w.Write(b); return; }
            if (o is double d) { w.Write("System.Double"); Double(w, d); return; }
            if (o is V vector) { VectorBody(w, vector); return; }
            if(o is HistoricalEvent historyEvent){HistoryEventBody(w,historyEvent);return;}
            var t = o.GetType(); w.Write(TypeName(t));
            if(!literalAggregateRecords&&(o is Array?TryWriteWideHistoricalArray(w,o,t):TryWriteWideHistoricalRecord(w,o,t)))return;
            if(!literalAggregateRecords&&o is HistoricalLocalBag bag){w.Write("Count");Integer(w,bag.Count);w.Write("Items");Write(w,bag.Items);w.Write("Positions");Write(w,bag.Positions);return;}
            if(!literalAggregateRecords&&t==typeof(HistoricalCityAggregate[])){HistoricalCityAggregateArrayBody(w,(HistoricalCityAggregate[])o);return;}
            if(!literalAggregateRecords&&t==typeof(HistoricalCoastChart[])){HistoricalCoastChartArrayBody(w,(HistoricalCoastChart[])o);return;}
            if(!literalAggregateRecords&&t==typeof(HistoricalRegionAggregate[])){HistoricalRegionAggregateArrayBody(w,(HistoricalRegionAggregate[])o);return;}
            if(!literalAggregateRecords&&t==typeof(HistoricalCultureAggregate[])){HistoricalCultureAggregateArrayBody(w,(HistoricalCultureAggregate[])o);return;}
            if(!literalAggregateRecords&&t==typeof(HistoricalPolityAggregate[])){HistoricalPolityAggregateArrayBody(w,(HistoricalPolityAggregate[])o);return;}
            if(!literalAggregateRecords&&t==typeof(HistoricalLivingCity[])){HistoricalLivingCityArrayBody(w,(HistoricalLivingCity[])o);return;}
            if(!literalAggregateRecords&&t==typeof(HistoricalLivingRegion[])){HistoricalLivingRegionArrayBody(w,(HistoricalLivingRegion[])o);return;}
            if(!literalAggregateRecords&&t==typeof(HistoricalExchangeMemory[])){HistoricalExchangeMemoryArrayBody(w,(HistoricalExchangeMemory[])o);return;}
            if(!literalAggregateRecords&&t==typeof(HistoricalCampaignTheater[])){HistoricalCampaignTheaterArrayBody(w,(HistoricalCampaignTheater[])o);return;}
            if(t==typeof(System.Collections.Generic.List<HistoricalEvent>)||t==typeof(HistoricalEventArchive)){HistoryArchive(w,(System.Collections.Generic.IEnumerable<HistoricalEvent>)o);return;}
            if(t==typeof(HistoricalLeader[])){LeaderArrayBody(w,(HistoricalLeader[])o);return;}
            // Mesh records dominate large snapshots. Preserve the canonical bytes
            // while avoiding reflection and boxing for every cell, edge and corner.
            if (t == typeof(Cell[])) { foreach (var item in (Cell[])o) { w.Write((byte)1); CellRecord(w, item); } w.Write((byte)0); return; }
            if (t == typeof(Edge[])) { foreach (var item in (Edge[])o) { w.Write((byte)1); EdgeRecord(w, item); } w.Write((byte)0); return; }
            if (t == typeof(HabitatCell[])) { foreach (var item in (HabitatCell[])o) { w.Write((byte)1); HabitatRecord(w, item); } w.Write((byte)0); return; }
            if (t == typeof(V[])) { foreach (var item in (V[])o) { w.Write((byte)1); w.Write((byte)1); VectorBody(w, item); } w.Write((byte)0); return; }
            // CLR enum arrays can satisfy an underlying integer-array cast; exact
            // type checks keep the enum's distinct type name in the hash.
            if (t == typeof(int[])) { IntegerArray(w, (int[])o); return; }
            if (t == typeof(long[])) { foreach (long item in (long[])o) { w.Write((byte)1); w.Write((byte)1); w.Write("System.Int64"); w.Write(item); } w.Write((byte)0); return; }
            if (t == typeof(bool[])) { foreach (bool item in (bool[])o) { w.Write((byte)1); w.Write((byte)1); w.Write("System.Boolean"); w.Write(item); } w.Write((byte)0); return; }
            if (t == typeof(double[])) { foreach (double item in (double[])o) { w.Write((byte)1); w.Write((byte)1); w.Write("System.Double"); Double(w, item); } w.Write((byte)0); return; }
            if (t.IsEnum) { w.Write(Convert.ToInt32(o)); return; }
            if (o is IEnumerable seq) { foreach (var item in seq) { w.Write((byte)1); Write(w, item); } w.Write((byte)0); return; }
            foreach (var f in Fields.GetOrAdd(t, type => type.GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Where(f => !(type == typeof(World) && (f.Name == nameof(World.Stages)||f.Name==nameof(World.Prehistory))) && f.GetCustomAttribute<DisplaySettingAttribute>() == null)
                .OrderBy(f => f.Name, StringComparer.Ordinal).ToArray()))
            {
                // Preserve existing saved-world integrity hashes when no experimental profile exists.
                if (o is Recipe recipe && f.Name == nameof(Recipe.ClimateWeights) && recipe.ClimateWeights == null) continue;
                if(o is Recipe shapeRecipe && f.Name==nameof(Recipe.LandShape) && shapeRecipe.LandShape==null)continue;
                if(o is Recipe ecologicalRecipe && f.Name==nameof(Recipe.EcologyProfile) && ecologicalRecipe.EcologyProfile==null)continue;
                if(o is Taxon traitTaxon && f.Name==nameof(Taxon.Traits) && traitTaxon.Traits==null)continue;
                if(o is SpeciesPresence variantPresence && f.Name==nameof(SpeciesPresence.Variant) && variantPresence.Variant==null)continue;
                if(o is Recipe rainRecipe && f.Name==nameof(Recipe.InteriorRainMm) && rainRecipe.InteriorRainMm==0)continue;
                if (o is Recipe oldRecipe && f.Name == nameof(Recipe.MountainStrengthPercent) && oldRecipe.AlgorithmVersion != Recipe.CurrentAlgorithm && oldRecipe.AlgorithmVersion != Recipe.SubcellAlgorithm && oldRecipe.AlgorithmVersion != Recipe.FeatureBlendAlgorithm && oldRecipe.AlgorithmVersion != Recipe.RangeControlsAlgorithm && oldRecipe.AlgorithmVersion != Recipe.EcologyAlgorithm && oldRecipe.AlgorithmVersion != Recipe.TerrainControlsAlgorithm) continue;
                if (o is World legacy && f.Name == nameof(World.Ecology) && legacy.Ecology == null) continue;
                if (o is World oldWorld && f.Name == nameof(World.Subcells) && oldWorld.Subcells == null) continue;
                if(o is HistoricalCulture lineageCulture && f.Name==nameof(HistoricalCulture.ParentCultureId) && lineageCulture.ParentCultureId==-1)continue;
                if(o is HistoricalCulture institutionalCulture && f.Name==nameof(HistoricalCulture.InstitutionalExamples) && institutionalCulture.InstitutionalExamples==0)continue;
                if(o is HistoricalCulture technicalCulture && f.Name==nameof(HistoricalCulture.Technical) && technicalCulture.Technical==null)continue;
                if(o is PrehistoryState calendarState && f.Name==nameof(PrehistoryState.Timeline) && calendarState.Timeline==null)continue;
                if(o is HistoricalAgriculture cropSites && f.Name==nameof(HistoricalAgriculture.PracticeCell) && cropSites.PracticeCell==null)continue;
                if(o is HistoricalAgriculture cultivarRecord && f.Name==nameof(HistoricalAgriculture.Cultivars) && cultivarRecord.Cultivars==null)continue;
                if(o is HistoricalAgriculture husbandryRecord && f.Name==nameof(HistoricalAgriculture.HusbandryYears) && husbandryRecord.HusbandryYears==null)continue;
                if(o is HistoricalAgriculture animalRecord && f.Name==nameof(HistoricalAgriculture.Animals) && animalRecord.Animals==null)continue;
                if(o is HistoricalCultivar thermalCultivar && f.Name==nameof(HistoricalCultivar.MinimumWarmTenthsC) && thermalCultivar.MinimumWarmTenthsC==0)continue;
                if(o is HistoryState modernHistory && f.Name==nameof(HistoryState.Modern) && modernHistory.Modern==null)continue;
                if(o is HistoryState aggregateHistory && f.Name==nameof(HistoryState.Aggregates) && aggregateHistory.Aggregates==null)continue;
                if(o is HistoryState languageHistory && f.Name==nameof(HistoryState.Languages) && languageHistory.Languages==null)continue;
                if(o is HistoryState originHistory && f.Name==nameof(HistoryState.OriginReach) && originHistory.OriginReach==null)continue;
                if(o is HistoricalAggregateState oldAggregate && f.Name==nameof(HistoricalAggregateState.Living) && oldAggregate.Living==null)continue;
                if(o is HistoricalAggregateState oldProvince && f.Name==nameof(HistoricalAggregateState.Provinces) && oldProvince.Provinces==null)continue;
                if(o is HistoricalAggregateState oldCommunity && f.Name==nameof(HistoricalAggregateState.Communities) && oldCommunity.Communities==null)continue;
                if(o is HistoricalAggregateState oldImperial && f.Name==nameof(HistoricalAggregateState.Empires) && oldImperial.Empires==null)continue;
                if(o is HistoricalImperialSystem oldFrontiers && f.Name==nameof(HistoricalImperialSystem.Frontiers) && oldFrontiers.Frontiers==null)continue;
                if(o is HistoricalImperialSystem oldConnections && f.Name==nameof(HistoricalImperialSystem.Connections) && oldConnections.Connections==null)continue;
                if(o is HistoricalColonialConnections oldCoasts && f.Name==nameof(HistoricalColonialConnections.Coasts) && oldCoasts.Coasts==null)continue;
                if(o is HistoricalColonialConnections oldAmbitions && f.Name==nameof(HistoricalColonialConnections.Ambitions) && oldAmbitions.Ambitions==null)continue;
                if(o is HistoricalModernSettings shippingSettings && f.Name==nameof(HistoricalModernSettings.Shipping) && shippingSettings.Shipping?.Length==0)continue;
                if(o is HistoricalAgriculture wildSites && f.Name==nameof(HistoricalAgriculture.WildOriginCell) && wildSites.WildOriginCell==null)continue;
                if(o is HistoricalReligion reachReligion && f.Name==nameof(HistoricalReligion.Reach) && reachReligion.Reach==ReligiousReach.Local)continue;
                if(o is HistoryState faithHistory && f.Name==nameof(HistoryState.Faith) && faithHistory.Version!="history-6" && faithHistory.Version!="history-7" && faithHistory.Version!="history-8" && faithHistory.Version!="history-9"&&faithHistory.Version!="history-10"&&faithHistory.Version!="history-11"&&faithHistory.Version!="history-12"&&faithHistory.Version!="history-13"&&(faithHistory.Version!="history-15"&&faithHistory.Version!="history-16"))continue;
                if(o is HistoryState regionalHistory && f.Name==nameof(HistoryState.RegionalLearning) && regionalHistory.RegionalLearning==null)continue;
                if(o is HistoryState administrativeHistory && f.Name==nameof(HistoryState.Administration) && administrativeHistory.Administration==null)continue;
                if(o is HistoryState voyageHistory&&f.Name==nameof(HistoryState.OceanSettlements)&&voyageHistory.OceanSettlements==null)continue;
                if(o is HistoricalDynamics dependencyHistory && f.Name==nameof(HistoricalDynamics.Dependencies) && (dependencyHistory.Dependencies==null||dependencyHistory.Dependencies.Length==0))continue;
                if(o is HistoricalLeader successionLeader && f.Name==nameof(HistoricalLeader.PredecessorId) && successionLeader.PredecessorId==-1)continue;
                if(o is HistoricalLeader selectedLeader && f.Name==nameof(HistoricalLeader.Selection) && string.IsNullOrEmpty(selectedLeader.Selection))continue;
                if(o is HistoricalStateDynamics successionState && (f.Name==nameof(HistoricalStateDynamics.HouseSerial)&&successionState.HouseSerial==0||f.Name==nameof(HistoricalStateDynamics.DisputedUntil)&&successionState.DisputedUntil==0))continue;
                if(o is HistoricalTown optionalTown && SkipOptionalTownField(optionalTown,f.Name))continue;
                if(o is HistoricalStateDynamics cycleState && f.Name==nameof(HistoricalStateDynamics.Cycle) && cycleState.Cycle==null)continue;
                if(o is HistoricalCampaign optionalCampaign&&f.Name==nameof(HistoricalCampaign.Context)&&string.IsNullOrEmpty(optionalCampaign.Context))continue;
                if(o is HistoricalPolity optionalPolity&&f.Name==nameof(HistoricalPolity.LastEstimateYear)&&optionalPolity.LastEstimateYear==0)continue;
                if(o is HistoricalDistrictOffice inheritedOffice&&(f.Name==nameof(HistoricalDistrictOffice.PredecessorOfficeId)&&inheritedOffice.PredecessorOfficeId==-1||f.Name==nameof(HistoricalDistrictOffice.PredecessorPolityId)&&inheritedOffice.PredecessorPolityId==-1))continue;
                if(o is HistoricalAdministrativeAccount urbanAccount&&(f.Name==nameof(HistoricalAdministrativeAccount.UrbanReceipts)&&urbanAccount.UrbanReceipts==0||f.Name==nameof(HistoricalAdministrativeAccount.TaxableUrbanResidents)&&urbanAccount.TaxableUrbanResidents==0||f.Name==nameof(HistoricalAdministrativeAccount.DisconnectedUrbanResidents)&&urbanAccount.DisconnectedUrbanResidents==0))continue;
                if(o is HistoricalOceanSettlement provisionedVoyage&&f.Name==nameof(HistoricalOceanSettlement.Provisions)&&provisionedVoyage.Provisions==0)continue;
                if(o is HistoricalOceanSettlement urbanVoyage&&f.Name==nameof(HistoricalOceanSettlement.UrbanMigrants)&&urbanVoyage.UrbanMigrants==0)continue;
                if(o is HistoricalOceanSettlement arrivalVoyage&&f.Name==nameof(HistoricalOceanSettlement.ArrivalFood)&&arrivalVoyage.ArrivalFood==0)continue;
                if(o is HistoricalOceanSettlement sailingVoyage&&f.Name==nameof(HistoricalOceanSettlement.SailingDistanceM)&&sailingVoyage.SailingDistanceM==0)continue;
                if(o is HistoricalOceanSettlement departureVoyage&&f.Name==nameof(HistoricalOceanSettlement.DepartureTownId)&&departureVoyage.DepartureTownId==-1)continue;
                if(o is HistoricalModernSettings civicSettings&&f.Name==nameof(HistoricalModernSettings.CivicRivalries)&&(civicSettings.CivicRivalries==null||civicSettings.CivicRivalries.Length==0))continue;
                if(o is HistoricalIncorporation districtProject&&f.Name==nameof(HistoricalIncorporation.Cells)&&districtProject.Cells==null)continue;
                if(o is HistoryState history && f.Name==nameof(HistoryState.Agriculture) && history.Version!="history-3" && history.Version!="history-4" && history.Version!="history-5" && history.Version!="history-6" && history.Version!="history-7" && history.Version!="history-8" && history.Version!="history-9"&&history.Version!="history-10"&&history.Version!="history-11"&&history.Version!="history-12"&&history.Version!="history-13"&&(history.Version!="history-15"&&history.Version!="history-16"))continue;
                if(o is HistoryState dynamicsHistory && f.Name==nameof(HistoryState.Dynamics) && dynamicsHistory.Version!="history-5" && dynamicsHistory.Version!="history-6" && dynamicsHistory.Version!="history-7" && dynamicsHistory.Version!="history-8" && dynamicsHistory.Version!="history-9"&&dynamicsHistory.Version!="history-10"&&dynamicsHistory.Version!="history-11"&&dynamicsHistory.Version!="history-12"&&dynamicsHistory.Version!="history-13"&&(dynamicsHistory.Version!="history-15"&&dynamicsHistory.Version!="history-16"))continue;
                w.Write(f.Name);
                if(o is HistoryState compactHistory&&(compactHistory.Version=="history-3"||compactHistory.Version=="history-4"||compactHistory.Version=="history-5"||compactHistory.Version=="history-6"||compactHistory.Version=="history-7"||compactHistory.Version=="history-8"||(compactHistory.Version=="history-9"||(compactHistory.Version=="history-10"||(compactHistory.Version=="history-11"||(compactHistory.Version=="history-12"||(compactHistory.Version=="history-13"||(compactHistory.Version=="history-14"||(compactHistory.Version=="history-15"||compactHistory.Version=="history-16"))))))))&&f.Name==nameof(HistoryState.Archive))CompactHistoryArchive(w,compactHistory.Archive);
                else Write(w, f.GetValue(o));
            }
        }
        static void Integer(BinaryWriter writer, int value) { writer.Write((byte)1); writer.Write("System.Int32"); writer.Write(value); }
        static bool SkipOptionalTownField(HistoricalTown town,string field)
        {
            switch(field)
            {
                case nameof(HistoricalTown.Demography):return town.Demography==null;
                case nameof(HistoricalTown.Cropping):return town.Cropping==null;
                case nameof(HistoricalTown.CivicPreparationYears):return town.CivicPreparationYears==0;
                case nameof(HistoricalTown.CivicExampleExposure):return town.CivicExampleExposure==0;
                case nameof(HistoricalTown.CivicTeacherPolityId):return town.CivicTeacherPolityId==-1;
                case nameof(HistoricalTown.AutonomyPressure):return town.AutonomyPressure==0;
                case nameof(HistoricalTown.LastAutonomyActionYear):return town.LastAutonomyActionYear==0;
                case nameof(HistoricalTown.UnrestPolityId):return town.UnrestPolityId==-1;
                case nameof(HistoricalTown.LastHarvestHazard):return town.LastHarvestHazard==HarvestHazard.None;
                case nameof(HistoricalTown.LastHarvestHazardYear):return town.LastHarvestHazardYear==0;
                case nameof(HistoricalTown.ShortageYears):return town.ShortageYears==0;
                case nameof(HistoricalTown.ImportedFood):return town.ImportedFood==0;
                case nameof(HistoricalTown.ExportedFood):return town.ExportedFood==0;
                case nameof(HistoricalTown.FoodTransportLoss):return town.FoodTransportLoss==0;
                case nameof(HistoricalTown.DiseaseDeaths):return town.DiseaseDeaths==0;
                case nameof(HistoricalTown.DiseaseKind):return town.DiseaseKind==HistoricalDiseaseKind.None;
                case nameof(HistoricalTown.DiseaseMortalityPermille):return town.DiseaseMortalityPermille==0;
                case nameof(HistoricalTown.DiseaseWaveId):return town.DiseaseWaveId==0;
                case nameof(HistoricalTown.KindImmunity):return town.KindImmunity==null;
                default:return false;
            }
        }
        static void IntegerArray(BinaryWriter writer, int[] values)
        {
            // Exactly the existing enumerable/scalar wire bytes, batched to avoid
            // millions of repeated string encodings and tiny stream writes per tick.
            const int stride=19, batch=2048;
            if(values.Length<128){foreach(int value in values){writer.Write((byte)1);Integer(writer,value);}writer.Write((byte)0);return;}
            byte[] block=new byte[stride*Math.Min(batch,values.Length)];
            byte[] prefix={1,1,12,83,121,115,116,101,109,46,73,110,116,51,50};
            for(int at=0;at<block.Length;at+=stride)Buffer.BlockCopy(prefix,0,block,at,prefix.Length);
            for(int first=0;first<values.Length;first+=batch)
            {
                int count=Math.Min(batch,values.Length-first);
                for(int i=0;i<count;i++){int value=values[first+i],at=i*stride+15;block[at]=(byte)value;block[at+1]=(byte)(value>>8);block[at+2]=(byte)(value>>16);block[at+3]=(byte)(value>>24);}
                writer.Write(block,0,count*stride);
            }
            writer.Write((byte)0);
        }
        static void HistoryEventBody(BinaryWriter w,HistoricalEvent e)
        {
            // Same canonical field order/bytes as reflection; archives dominate long-history checkpoints.
            w.Write("WorldGen.Core.HistoricalEvent");
            w.Write("Category");Write(w,e.Category);
            w.Write("Cell");Integer(w,e.Cell);w.Write("CultureId");Integer(w,e.CultureId);w.Write("Id");Integer(w,e.Id);
            w.Write("Kind");Write(w,e.Kind);w.Write("OtherPolityId");Integer(w,e.OtherPolityId);w.Write("PolityId");Integer(w,e.PolityId);
            w.Write("ReligionId");Integer(w,e.ReligionId);w.Write("SettlementId");Integer(w,e.SettlementId);w.Write("Text");Write(w,e.Text);w.Write("Year");Integer(w,e.Year);
        }
        sealed class HistoryTemplate
        {
            public readonly byte[] Bytes;public readonly int[] Offsets;
            public HistoryTemplate(int part)
            {
                using(var memory=new MemoryStream())using(var writer=new BinaryWriter(memory))
                {
                    var offsets=new System.Collections.Generic.List<int>();
                    if(part==0)writer.Write("WorldGen.Core.HistoricalEvent");
                    var names=part==0?new[]{"Category","Cell","CultureId","Id"}:part==1?new[]{"OtherPolityId","PolityId","ReligionId","SettlementId"}:new[]{"Year"};
                    foreach(string name in names){writer.Write(name);writer.Write((byte)1);writer.Write(name=="Category"?"WorldGen.Core.ChronicleCategory":"System.Int32");offsets.Add((int)memory.Position);writer.Write(0);}
                    if(part<2)writer.Write(part==0?"Kind":"Text");Bytes=memory.ToArray();Offsets=offsets.ToArray();
                }
            }
        }
        static readonly HistoryTemplate HistoryHead=new HistoryTemplate(0),HistoryMiddle=new HistoryTemplate(1),HistoryTail=new HistoryTemplate(2);
        static void PutInteger(byte[] bytes,int at,int value){bytes[at]=(byte)value;bytes[at+1]=(byte)(value>>8);bytes[at+2]=(byte)(value>>16);bytes[at+3]=(byte)(value>>24);}
        static void HistoryArchive(BinaryWriter writer,System.Collections.Generic.IEnumerable<HistoricalEvent> archive)
        {
            // Immutable templates; scratch belongs to this hash invocation. No cached
            // authoritative values or skipped integrity checks, including old saves.
            var head=(byte[])HistoryHead.Bytes.Clone();var middle=(byte[])HistoryMiddle.Bytes.Clone();var tail=(byte[])HistoryTail.Bytes.Clone();
            foreach(var e in archive)
            {
                writer.Write((byte)1);writer.Write((byte)(e==null?0:1));if(e==null)continue;
                PutInteger(head,HistoryHead.Offsets[0],(int)e.Category);PutInteger(head,HistoryHead.Offsets[1],e.Cell);PutInteger(head,HistoryHead.Offsets[2],e.CultureId);PutInteger(head,HistoryHead.Offsets[3],e.Id);
                PutInteger(middle,HistoryMiddle.Offsets[0],e.OtherPolityId);PutInteger(middle,HistoryMiddle.Offsets[1],e.PolityId);PutInteger(middle,HistoryMiddle.Offsets[2],e.ReligionId);PutInteger(middle,HistoryMiddle.Offsets[3],e.SettlementId);
                PutInteger(tail,HistoryTail.Offsets[0],e.Year);
                writer.Write(head);Write(writer,e.Kind);writer.Write(middle);Write(writer,e.Text);writer.Write(tail);
            }
            writer.Write((byte)0);
        }
        static void CompactHistoryArchive(BinaryWriter writer,System.Collections.Generic.IReadOnlyList<HistoricalEvent> archive)
        {
            // New history-3 contract only. Hash every value on every verification,
            // without repeating type and field names eleven times per event.
            // CheckRecord above deliberately fails if the event layout changes.
            writer.Write("history-3/archive-compact-v1");
            if(archive==null){writer.Write(-1);return;}writer.Write(archive.Count);
            foreach(var e in archive)
            {
                writer.Write(e!=null);if(e==null)continue;
                writer.Write((int)e.Category);writer.Write(e.Cell);writer.Write(e.CultureId);writer.Write(e.Id);
                writer.Write(e.Kind!=null);if(e.Kind!=null)writer.Write(e.Kind);
                writer.Write(e.OtherPolityId);writer.Write(e.PolityId);writer.Write(e.ReligionId);writer.Write(e.SettlementId);
                writer.Write(e.Text!=null);if(e.Text!=null)writer.Write(e.Text);writer.Write(e.Year);
            }
        }
        static void HabitatRecord(BinaryWriter w, HabitatCell h)
        {
            if(h==null){w.Write((byte)0);return;}
            w.Write((byte)1);w.Write("WorldGen.Core.HabitatCell");
            void I(string name,int value){w.Write(name);Integer(w,value);}
            void E(string name,string type,int value){w.Write(name);w.Write((byte)1);w.Write(type);w.Write(value);}
            I("AridityMilli",h.AridityMilli);E("Biome","WorldGen.Core.Biome",(int)h.Biome);I("ColdTenthsC",h.ColdTenthsC);I("FirePotentialPermille",h.FirePotentialPermille);
            I("FloodDaysPerYear",h.FloodDaysPerYear);I("FloodFractionPermille",h.FloodFractionPermille);I("FloodPulsesPerYear",h.FloodPulsesPerYear);E("Flooding","WorldGen.Core.FloodRegime",(int)h.Flooding);
            I("GrowingDays",h.GrowingDays);E("Habitat","WorldGen.Core.Habitat",(int)h.Habitat);I("MeanTenthsC",h.MeanTenthsC);I("PermafrostPermille",h.PermafrostPermille);I("PetMm",h.PetMm);
            I("ProductivityGCM2Year",h.ProductivityGCM2Year);I("ReliefM",h.ReliefM);I("SalinityMilliPpt",h.SalinityMilliPpt);E("Soil","WorldGen.Core.SoilKind",(int)h.Soil);I("SoilFertilityPermille",h.SoilFertilityPermille);
            I("WarmSeasonDryPermille",h.WarmSeasonDryPermille);I("WarmTenthsC",h.WarmTenthsC);I("WaterDeficitMm",h.WaterDeficitMm);I("WaterTableCm",h.WaterTableCm);
        }
        static void Real(BinaryWriter writer, double value) { writer.Write((byte)1); writer.Write("System.Double"); Double(writer, value); }
        static void VectorBody(BinaryWriter writer, V value)
        {
            writer.Write("WorldGen.Core.V"); writer.Write("X"); Real(writer, value.X); writer.Write("Y"); Real(writer, value.Y); writer.Write("Z"); Real(writer, value.Z);
        }
        static void CellRecord(BinaryWriter writer, Cell cell)
        {
            if (cell == null) { writer.Write((byte)0); return; }
            writer.Write((byte)1); writer.Write("WorldGen.Core.Cell");
            writer.Write("AreaM2"); writer.Write((byte)1); writer.Write("System.Int64"); writer.Write(cell.AreaM2);
            writer.Write("DistanceM"); Write(writer, cell.DistanceM); writer.Write("Id"); Integer(writer, cell.Id);
            writer.Write("Neighbors"); Write(writer, cell.Neighbors); writer.Write("Position"); writer.Write((byte)1); VectorBody(writer, cell.Position);
        }
        static void EdgeRecord(BinaryWriter writer, Edge edge)
        {
            if (edge == null) { writer.Write((byte)0); return; }
            writer.Write((byte)1); writer.Write("WorldGen.Core.Edge");
            writer.Write("A"); Integer(writer, edge.A); writer.Write("B"); Integer(writer, edge.B); writer.Write("Id"); Integer(writer, edge.Id);
            writer.Write("LengthM"); Integer(writer, edge.LengthM); writer.Write("TriangleA"); Integer(writer, edge.TriangleA); writer.Write("TriangleB"); Integer(writer, edge.TriangleB);
        }
        static void Double(BinaryWriter writer, double value) { double quantized = D.Q(value, 1e6); writer.Write(quantized == 0 ? 0.0 : quantized); }
        static string TypeName(Type t) => TypeNames.GetOrAdd(t, type => type==typeof(HistoricalEventArchive)?TypeName(typeof(System.Collections.Generic.List<HistoricalEvent>)):type.IsGenericType ? (type.GetGenericTypeDefinition().FullName ?? type.Name).Split('`')[0] + "<" + string.Join(",", type.GetGenericArguments().Select(TypeName)) + ">" : type.FullName ?? type.Name);
    }
    public sealed class WorldPipeline
    {
        public World Generate(Recipe recipe, Action<GenerationProgress> progress = null, CancellationToken cancellation = default, Action<string, World> preview = null)
        {
            var r = recipe.Clone(); r.Validate(); var w = new World { Recipe = r };
            string previous = StateHash.Compute(r);
            void Stage(string name, int percent, Action action, Func<object> output)
            {
                cancellation.ThrowIfCancellationRequested(); progress?.Invoke(new GenerationProgress(name, percent)); action();
                cancellation.ThrowIfCancellationRequested();
                string hash = StateHash.Compute(new object[] { previous, name, output() }, cancellation); previous = hash;
                w.Stages.Add(new StageRecord { Name = name, Hash = hash, Entities = w.Surface.Cells.Length });
                // Synchronous observer: copy completed presentation inputs before returning.
                // No preview data participates in the authoritative world or hashes.
                if (name == "coarse-plates" || name == "erosion" || name == "climate" || name == "hydrology" || name == "biogeography") preview?.Invoke(name, w);
            }
            Stage("coarse-plates", 0, () =>
            {
                var coarseRecipe = r.Clone(); coarseRecipe.Detail = SphericalMesh.CanonicalCellCount; coarseRecipe.Irregularity = .75;
                var coarse = new World { Recipe = coarseRecipe, Surface = SphericalMesh.BuildCoarse(r, cancellation) };
                PlateHierarchy.Microplates(coarse, cancellation); Continents.Build(coarse, cancellation);
                w.Metrics.AddRange(coarse.Metrics);
                w.Warnings.AddRange(coarse.Warnings);
                w.ContinentalCandidate = coarse.ContinentalCandidate; w.ContinentalScore = coarse.ContinentalScore; w.ContinentalCandidateScores = coarse.ContinentalCandidateScores;
                w.CoarseSurface = coarse.Surface; w.CoarsePlate = coarse.CellMicroplate; w.CoarseContinent = coarse.Continent;
                w.CoarseHash = StateHash.Compute(new object[] { coarse.Surface, coarse.CellMicroplate, coarse.Continent, coarse.Microplates }, cancellation);
                w.Microplates = coarse.Microplates;
            }, () => w.CoarseHash);
            Stage("geometry", 8, () => w.Surface = SphericalMesh.Build(r, cancellation), () => w.Surface);
            Stage("plate-projection", 18, () => PlateProjection.Apply(w, cancellation), () => new object[] { w.CellMicroplate, w.Continent, w.Microplates });
            Stage("plate-physics", 26, () => PlatePhysics.Apply(w, cancellation), () => new object[] { w.Microplates, w.MantlePermille });
            Stage("superplates", 32, () => PlateHierarchy.Parents(w, cancellation), () => new object[] { w.Parents, w.Microplates });
            Stage("boundaries", 38, () => Tectonics.Build(w, cancellation), () => new object[] { w.Boundaries, w.Stress, w.BoundaryDistanceM });
            Stage("terrain", 45, () => { if (r.TerrainSource == "Earth") EarthTerrain.Apply(w, cancellation); else Terrain.Build(w, cancellation); }, () => new object[] { w.TectonicM, w.Features, w.CoastDistanceM, w.FoldPermille, w.CratonPermille, w.BasinPermille, w.PlateauPermille });
            Stage("erosion", 65, () => { if (r.TerrainSource != "Earth") { Erosion.Apply(w, cancellation); MapShapes.ApplyTerrain(w,cancellation); } }, () => new object[] { w.ElevationM, w.PreErosionM, w.Landmass, w.Hydrology.OceanConnected, w.Glaciated });
            Stage("climate", 80, () => Climate.Build(w, cancellation), () => w.Climate);
            Stage("hydrology", 90, () => Hydrology.Build(w, cancellation), () => w.Hydrology);
            if((r.AlgorithmVersion == Recipe.CurrentAlgorithm || r.AlgorithmVersion == Recipe.SubcellAlgorithm))Stage("subcell-geography",91,()=>SubcellGeography.Build(w,cancellation),()=>w.Subcells);
            if (((r.AlgorithmVersion == Recipe.CurrentAlgorithm || r.AlgorithmVersion == Recipe.SubcellAlgorithm) || r.AlgorithmVersion == Recipe.FeatureBlendAlgorithm || r.AlgorithmVersion == Recipe.RangeControlsAlgorithm || r.AlgorithmVersion == Recipe.EcologyAlgorithm))
            {
                Stage("habitats", 92, () => Ecology.BuildHabitats(w, cancellation), () => new object[] { w.Ecology.Habitat, w.Ecology.WaterSalinityPpt });
                Stage("ecoregions", 94, () => Ecology.BuildRegions(w, cancellation), () => new object[] { w.Ecology.Province, w.Ecology.Region, w.Ecology.Regions });
                Stage("biogeography", 96, () => Biogeography.Build(w, cancellation), () => w.Ecology);
            }
            progress?.Invoke(new GenerationProgress("finalizing", 98));
            long total = w.Surface.Cells.Sum(c => c.AreaM2);
            w.Metric("coarse_cells", w.CoarseSurface.Cells.Length); w.Metric("terrain_cells", w.Surface.Cells.Length);
            w.Metric("land_fraction", w.Surface.Cells.Where(c => !w.Hydrology.OceanConnected[c.Id]).Sum(c => c.AreaM2) / (double)total);
            long continentalArea = w.Surface.Cells.Where(c => w.Continent[c.Id] >= 0).Sum(c => c.AreaM2);
            long continentalBelowZero = w.Surface.Cells.Where(c => w.Continent[c.Id] >= 0 && w.ElevationM[c.Id] < 0).Sum(c => c.AreaM2);
            long inlandBelowZero = w.Surface.Cells.Where(c => w.Continent[c.Id] >= 0 && w.ElevationM[c.Id] < 0 && !w.Hydrology.OceanConnected[c.Id]).Sum(c => c.AreaM2);
            w.Metric("continental_below_zero_fraction", continentalBelowZero / (double)continentalArea);
            w.Metric("inland_below_zero_fraction", inlandBelowZero / (double)continentalArea);
            w.Metric("landmass_count", w.Landmass.Where(i => i >= 0).Distinct().Count());
            w.Metric("min_elevation", w.ElevationM.Min(), "m"); w.Metric("max_elevation", w.ElevationM.Max(), "m");
            w.Stages.Add(new StageRecord { Name = "complete", Hash = StateHash.Compute(w, cancellation), Entities = w.Surface.Cells.Length });
            progress?.Invoke(new GenerationProgress("complete", 100)); return w;
        }
        public World Reapply(Recipe recipe, Action<GenerationProgress> progress = null, CancellationToken cancellation = default) => Generate(recipe, progress, cancellation);
        public World Reconstruct(World snapshot, Action<GenerationProgress> progress = null, CancellationToken cancellation = default) => Generate(snapshot.Recipe, progress, cancellation);
    }
}
