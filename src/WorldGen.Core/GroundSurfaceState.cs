using System;

namespace WorldGen.Core
{
    /// <summary>Geographic query, independent of render chunks, LOD and floating origin.
    /// Heights/depths use physical world metres, not scaled Unity units; time uses
    /// the same absolute world-calendar days as WorldWeather, never wall time.</summary>
    public readonly struct GroundSurfaceQuery
    {
        public readonly V Position;
        public readonly double GroundElevationM, Days;
        public GroundSurfaceQuery(V position, double groundElevationM, double days)
        {
            if (!Finite(position.X) || !Finite(position.Y) || !Finite(position.Z) || !Finite(position.Length) || position.Length < .5)
                throw new ArgumentOutOfRangeException(nameof(position));
            if (!Finite(groundElevationM)) throw new ArgumentOutOfRangeException(nameof(groundElevationM));
            if (!Finite(days)) throw new ArgumentOutOfRangeException(nameof(days));
            Position = position.Unit; GroundElevationM = groundElevationM; Days = days;
        }
        internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    public enum SurfaceWaterKind { Unknown, None, Ocean, Lake, River, Puddle, Floodwater }

    /// <summary>Actual water at this query point, not a containing coarse cell or
    /// basin's potential level. Unknown is deliberately the default, not dry.</summary>
    public readonly struct SurfaceWaterState
    {
        public readonly SurfaceWaterKind Kind;
        public readonly double? LevelM;
        // Provider-scoped source identity; null means no stable identity available.
        public readonly long? BodyId;
        public bool IsKnown => Kind != SurfaceWaterKind.Unknown;
        public bool HasWater => Kind != SurfaceWaterKind.Unknown && Kind != SurfaceWaterKind.None;
        public static SurfaceWaterState Dry => new SurfaceWaterState(SurfaceWaterKind.None);

        public SurfaceWaterState(SurfaceWaterKind kind, double? levelM = null, long? bodyId = null)
        {
            if (!Enum.IsDefined(typeof(SurfaceWaterKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            bool wet = kind != SurfaceWaterKind.Unknown && kind != SurfaceWaterKind.None;
            if (wet != levelM.HasValue || (levelM.HasValue && !GroundSurfaceQuery.Finite(levelM.Value)) || (!wet && bodyId.HasValue))
                throw new ArgumentException("Water requires a finite datum; dry/unknown samples have no datum or body identity.");
            Kind = kind; LevelM = levelM; BodyId = bodyId;
        }

        /// <summary>Null for unknown; zero for dry. A provider must establish
        /// connected coverage BEFORE supplying a level, not flood every low point.</summary>
        public double? DepthM(double groundElevationM)
        {
            if (!GroundSurfaceQuery.Finite(groundElevationM)) throw new ArgumentOutOfRangeException(nameof(groundElevationM));
            return !IsKnown ? (double?)null : HasWater ? Math.Max(0, LevelM.Value - groundElevationM) : 0;
        }
    }

    /// <summary>Long-term habitat inputs, not today's weather or a flood simulation.
    /// Null means unavailable. A floodplain can be dry now and still have exposure.</summary>
    public readonly struct GroundSurfaceRegime
    {
        public readonly double? InundationFraction, WaterloggingFraction;
        public GroundSurfaceRegime(double? inundationFraction = null, double? waterloggingFraction = null)
        {
            GroundSurfaceState.Fraction(inundationFraction, nameof(inundationFraction));
            GroundSurfaceState.Fraction(waterloggingFraction, nameof(waterloggingFraction));
            InundationFraction = inundationFraction; WaterloggingFraction = waterloggingFraction;
        }
    }

    /// <summary>Shared inputs for future ground materials and vegetation. Null
    /// means unknown, zero means known absent. This is not serialized into World.
    /// Soil saturation, surface wetness, standing water and snow are independent:
    /// SoilMoisture is relative saturation (0 dry, 1 saturated), not water storage
    /// in millimetres. Rain rate does not prove puddles, and snow water equivalent
    /// is not depth.</summary>
    public readonly struct GroundSurfaceState
    {
        public readonly double? SoilMoisture, SurfaceWetness, SnowCover, SnowDepthM, SnowWaterEquivalentMm;
        public readonly SurfaceWaterState Water;
        public readonly GroundSurfaceRegime Regime;
        public GroundSurfaceState(double? soilMoisture = null, double? surfaceWetness = null,
            double? snowCover = null, double? snowDepthM = null, double? snowWaterEquivalentMm = null,
            SurfaceWaterState water = default, GroundSurfaceRegime regime = default)
        {
            Fraction(soilMoisture, nameof(soilMoisture)); Fraction(surfaceWetness, nameof(surfaceWetness));
            Fraction(snowCover, nameof(snowCover)); Nonnegative(snowDepthM, nameof(snowDepthM));
            Nonnegative(snowWaterEquivalentMm, nameof(snowWaterEquivalentMm));
            SoilMoisture = soilMoisture; SurfaceWetness = surfaceWetness; SnowCover = snowCover;
            SnowDepthM = snowDepthM; SnowWaterEquivalentMm = snowWaterEquivalentMm;
            Water = water; Regime = regime;
        }
        internal static void Fraction(double? value, string name)
        {
            Nonnegative(value, name);
            if (value > 1) throw new ArgumentOutOfRangeException(name);
        }
        static void Nonnegative(double? value, string name)
        {
            if (value.HasValue && (!GroundSurfaceQuery.Finite(value.Value) || value.Value < 0))
                throw new ArgumentOutOfRangeException(name);
        }
    }

    /// <summary>Future shared surface-effects boundary. Queries must be safe for
    /// concurrent terrain/vegetation workers, geographically stable across LODs,
    /// and reproducible from explicit world inputs/time (no engine RNG/wall clock).
    /// Providers must not mutate source terrain, hydrology or ecology while sampling.
    /// No implementation is registered: adding this contract changes no generation.</summary>
    public interface IGroundSurfaceStateProvider
    {
        GroundSurfaceState Sample(GroundSurfaceQuery query);
    }
}
