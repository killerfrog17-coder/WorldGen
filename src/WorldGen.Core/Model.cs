using System;
using System.Collections.Generic;
namespace WorldGen.Core
{
    [Serializable]
    public sealed class Cell
    {
        public int Id; public V Position; public long AreaM2; public int[] Neighbors = Array.Empty<int>(); public int[] DistanceM = Array.Empty<int>();
    }
    [Serializable]
    public sealed class Edge
    {
        public int Id, A, B, TriangleA, TriangleB, LengthM;
    }
    [Serializable]
    public sealed class Surface
    {
        public Cell[] Cells = Array.Empty<Cell>(); public int[] Triangles = Array.Empty<int>(); public V[] Corners = Array.Empty<V>(); public Edge[] Edges = Array.Empty<Edge>();
    }
    [Serializable]
    public sealed class Microplate
    {
        public int Id, SeedCell, ParentAffinityId = -1, ActiveActorId, ContinentId = -1, DensityKgM3;
        public bool Preserved; public int ContestDegree; public long AreaM2; public V Centroid, OriginalMotion, Motion;
        public int[] Cells = Array.Empty<int>(), Neighbors = Array.Empty<int>(), BoundaryEdges = Array.Empty<int>();
        public long[] SharedBoundaryM = Array.Empty<long>();
    }
    [Serializable]
    public sealed class ParentPlate
    {
        public int Id, SeedMicroplate; public long TargetAreaM2, AreaM2; public double ContinentalFraction;
        public V Motion, GrowthDirection, LastDirection; public int[] Microplates = Array.Empty<int>();
    }
    public enum BoundaryKind { Inactive, Convergent, Divergent, Transform }
    public enum CollisionContext { None, EscapeFace, Intermediate, DeadEnd }
    [Serializable]
    public sealed class Boundary
    {
        public int Id, EdgeId, MicroA, MicroB, SubductingMicroplate = -1;
        public int OverridingMicroplate = -1, BeltId = -1, AlongM, EndDistanceM;
        public V Normal;
        public bool Ghost; public BoundaryKind Kind, FormerKind; public CollisionContext Context;
        public int NormalMotion, TangentialMotion, Stress, AgeMyr, EscapePermille;
    }
    public enum FeatureKind { CollisionBelt, Plateau, EscapeBlocks, ForelandBasin, AncientOrogen, AncientRift, TransformCorridor, Rift, MidOceanRidge, Trench, VolcanicArc, IslandArc, Hotspot, IslandChain, LargeIgneousProvince, Shelf, Slope, LandBridge, InlandSea, GlacialBasin, Cordillera, BackArcBasin, AccretionaryWedge, ObliqueOrogen, RelictArc, RidgeHotspot, MarineBasin }
    [Serializable]
    public sealed class Feature
    {
        public int Id, BoundaryId = -1, OwnerMicroplate = -1, AgeMyr, AmplitudeM, WidthM;
        public int OwnerParent = -1, BeltId = -1;
        public string Name = ""; public FeatureKind Kind; public V Center, Axis; public int[] Cells = Array.Empty<int>();
    }
    public enum WaterKind { Ocean, Lake, TectonicInlandSea, GlacialLake, EndorheicLake }
    [Serializable]
    public sealed class WaterBody
    {
        public int Id, SurfaceM, SpillM, OutletCell = -1, DownstreamCell = -1, DepressionId = -1;
        public WaterKind Kind; public bool Endorheic; public long AreaM2; public double VolumeM3;
        public double MeanInflowM3PerS, EvaporationM3PerS;
        // Coarse-grid retention approximation, not a resolved river notch or aquifer model.
        public bool SubgridOutlet;
        public double ResidenceTimeYears, CatchmentTransmissionLossM3PerS;
        // Continuous water level; SurfaceM is its rounded compatibility/diagnostic value.
        public double SurfaceElevationM;
        public int[] Cells = Array.Empty<int>();
    }
    [Serializable]
    public sealed class Depression
    {
        public int Id, ParentId = -1, PitCell, SpillM, OutletCell = -1; public int[] Children = Array.Empty<int>();
        public bool UnresolvedSpillway;
        // Hydrological outlet conditioning is subgrid: the terrain heightmap is unchanged.
        public double EffectiveSpillElevationM, SpillwayIncisionM;
        public int[] SpillwayCells = Array.Empty<int>();
    }
    [Serializable]
    public sealed class RiverReach
    {
        public int Id, From, To, WatershedId, Order; public long ContributingAreaM2; public double DischargePotentialM3PerS;
        public double NorthernSpringDischargeM3PerS, NorthernSummerDischargeM3PerS, NorthernAutumnDischargeM3PerS, NorthernWinterDischargeM3PerS, WidthM;
    }
    [Serializable]
    public sealed class HydrologyResult
    {
        public int[] Receiver = Array.Empty<int>(), Watershed = Array.Empty<int>(), WaterBody = Array.Empty<int>(), FloodLevelM = Array.Empty<int>(), VisitOrder = Array.Empty<int>(), RiverOrder = Array.Empty<int>();
        public long[] ContributingAreaM2 = Array.Empty<long>(); public bool[] OceanConnected = Array.Empty<bool>();
        public WaterBody[] Waters = Array.Empty<WaterBody>(); public Depression[] Depressions = Array.Empty<Depression>(); public RiverReach[] Rivers = Array.Empty<RiverReach>();
        public double[] NorthernSummerDischargeM3PerS = Array.Empty<double>(), NorthernWinterDischargeM3PerS = Array.Empty<double>();
        public double[] NorthernSpringDischargeM3PerS = Array.Empty<double>(), NorthernAutumnDischargeM3PerS = Array.Empty<double>();
        public double[] LocalNorthernSummerRunoffM3PerS = Array.Empty<double>(), LocalNorthernWinterRunoffM3PerS = Array.Empty<double>();
        public double[] LocalNorthernSpringRunoffM3PerS = Array.Empty<double>(), LocalNorthernAutumnRunoffM3PerS = Array.Empty<double>();
        public double DryBasinTransmissionLossM3PerS;
        // Inland water only. Fractional lakes remain authoritative even when too small
        // to replace a whole terrain cell in the global display.
        public double[] LakeCoverageFraction = Array.Empty<double>(), LakeWaterVolumeM3 = Array.Empty<double>();
    }
    [Serializable] public sealed class StageRecord { public string Name = "", Hash = ""; public int Entities; }
    [Serializable] public sealed class Metric { public string Name = "", Unit = ""; public double Value; }
    [Serializable]
    public sealed class World
    {
        public int SchemaVersion = 3; public Recipe Recipe = new Recipe(); public Surface Surface = new Surface();
        public Surface CoarseSurface = new Surface(); public int[] CoarsePlate = Array.Empty<int>(), CoarseContinent = Array.Empty<int>(); public string CoarseHash = "";
        public int ContinentalCandidate; public long ContinentalScore; public long[] ContinentalCandidateScores = Array.Empty<long>();
        public Microplate[] Microplates = Array.Empty<Microplate>(); public ParentPlate[] Parents = Array.Empty<ParentPlate>(); public Boundary[] Boundaries = Array.Empty<Boundary>(); public Feature[] Features = Array.Empty<Feature>();
        public int[] CellMicroplate = Array.Empty<int>(), Continent = Array.Empty<int>(), Landmass = Array.Empty<int>(), TectonicM = Array.Empty<int>(), PreErosionM = Array.Empty<int>(), ElevationM = Array.Empty<int>(), Stress = Array.Empty<int>(), BoundaryDistanceM = Array.Empty<int>(), CoastDistanceM = Array.Empty<int>();
        public bool[] ProtectedInlandSea = Array.Empty<bool>(); public HydrologyResult Hydrology = new HydrologyResult();
        public bool[] Glaciated = Array.Empty<bool>();
        public int[] FoldPermille = Array.Empty<int>(), CratonPermille = Array.Empty<int>(), BasinPermille = Array.Empty<int>(), PlateauPermille = Array.Empty<int>();
        public int[] MantlePermille = Array.Empty<int>();
        public ClimateResult Climate = new ClimateResult();
        [System.Runtime.Serialization.OptionalField] public EcologyResult Ecology;
        [System.Runtime.Serialization.OptionalField] public SubcellResult Subcells;
        [System.Runtime.Serialization.OptionalField] public PrehistoryState Prehistory;
        public List<StageRecord> Stages = new List<StageRecord>(); public List<Metric> Metrics = new List<Metric>(); public List<string> Warnings = new List<string>();
        public void Metric(string name, double value, string unit = "") => Metrics.Add(new Metric { Name = name, Value = D.Q(value, 1000000), Unit = unit });
    }
    public sealed class GenerationProgress { public string Stage; public int Percent; public GenerationProgress(string s, int p) { Stage = s; Percent = p; } }
    // Orogen-style two-season climate is not a four-season hydrological runoff model.
    // Quarter-year depths; seasonIndex 0=spring, 1=summer, 2=autumn, 3=winter (northern calendar).
    public interface ISeasonalRunoffProvider { double[] RunoffDepthMetres(World world, int seasonIndex); }
}
