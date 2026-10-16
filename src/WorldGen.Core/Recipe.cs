using System;
using System.Linq;
using System.Reflection;

namespace WorldGen.Core
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SettingAttribute : Attribute
    {
        public string Group, Label; public double Min, Max, Step;
        public SettingAttribute(string group, string label, double min = 0, double max = 1, double step = .05)
        { Group = group; Label = label; Min = min; Max = max; Step = step; }
    }
    [AttributeUsage(AttributeTargets.Field)] public sealed class DisplaySettingAttribute : Attribute { }
    [Serializable]
    public sealed class Recipe
    {
        public const string TerrainControlsAlgorithm = "worldgen-3.27.0";
        public const string EcologyAlgorithm = "worldgen-3.28.0";
        public const string RangeControlsAlgorithm = "worldgen-3.29.0";
        public const string FeatureBlendAlgorithm = "worldgen-3.30.0";
        public const string SubcellAlgorithm = "worldgen-3.31.0";
        public const string CurrentAlgorithm = "worldgen-3.32.0";
        public int SchemaVersion = 3;
        public string AlgorithmVersion = CurrentAlgorithm;
        public string TerrainSource = "Procedural";
        [System.Runtime.Serialization.OptionalField] public string EcologyProfile = "earth-native-2";
        [System.Runtime.Serialization.OptionalField] public string LandShape;
        [System.Runtime.Serialization.OptionalField]
        [Setting("Climate", "Inland moisture supply (mm/year)",0,2000,10)] public double InteriorRainMm;
        // Optional experimental profile; null preserves the 3.24 recipe/hash contract.
        [System.Runtime.Serialization.OptionalField] public double[] ClimateWeights = ClimateCalibration.DefaultWeights();
        [Setting("World", "Seed", 0, 16777215, 1)] public int Seed = 73421;
        [Setting("World", "Detail", 5000, 2560000, 1000)] public int Detail = 204000;
        [Setting("World", "Irregularity")] public double Irregularity = .75;
        [Setting("World", "Plates", 4, 500, 1)] public int MicroplateCount = 80;
        [Setting("World", "Continents", 1, 50, 1)] public int ContinentCount = 4;
        [Setting("World", "Interconnectedness", 1, 50, 1)] public int Interconnectedness = 1;
        [Setting("World", "Roughness", 0, .5, .01)] public double Relief = .4;
        [Setting("World", "Continent size variety")] public double ContinentVariation = .35;
        [Setting("World", "Land coverage", 0, 1, .01)] public double LandFraction = .3;
        [Setting("Terrain sculpting", "Terrain warp")] public double TerrainWarp = .75;
        [Setting("Terrain sculpting", "Smoothing")] public double Smoothing = .1;
        [Setting("Terrain sculpting", "Glacial erosion")] public double Glacial = .5;
        [Setting("Terrain sculpting", "Hydraulic erosion")] public double Hydraulic = .5;
        [Setting("Terrain sculpting", "Thermal erosion")] public double Thermal = .1;
        [Setting("Terrain sculpting", "Ridge sharpening")] public double RidgePreservation = .5;
        [System.Runtime.Serialization.OptionalField]
        [Setting("Terrain sculpting", "Mountain strength (%)", 0, 100000, .0001)] public double MountainStrengthPercent = 100;
        [System.Runtime.Serialization.OnDeserializing]
        void ReadLegacyDefaults(System.Runtime.Serialization.StreamingContext context) { MountainStrengthPercent = 100; EcologyProfile = null; }
        [Setting("Climate", "Temperature offset (C)", -15, 15, .5)] public double TemperatureOffset;
        [Setting("Climate", "Precipitation offset", -1, 1, .05)] public double PrecipitationOffset;
        [Setting("Terrain sculpting", "Lake amount (%)", 0, 100000, .0001)] public double LakeAmountPercent = 100;
        public int RadiusKm => 6371;
        public int MacroCellCount => SphericalMesh.CanonicalCellCount;
        public Recipe Clone() { var copy = (Recipe)MemberwiseClone(); copy.ClimateWeights = ClimateWeights == null ? null : (double[])ClimateWeights.Clone(); return copy; }
        public static readonly FieldInfo[] Settings = typeof(Recipe).GetFields().Where(f => f.GetCustomAttribute<SettingAttribute>() != null).OrderBy(f => f.MetadataToken).ToArray();
        public void Validate()
        {
            if(EcologyProfile!=null && EcologyProfile!="earth-native-2" && EcologyProfile!="earth-native-3")throw new ArgumentException("Unknown ecology content profile.");
            if (TerrainSource != "Procedural" && TerrainSource != "Earth") throw new ArgumentException("Unknown terrain source.");
            if(LandShape!=null && !MapShapes.Names.Contains(LandShape))throw new ArgumentException("Unknown land shape.");
            if(TerrainSource=="Earth" && LandShape!=null)throw new ArgumentException("Earth relief cannot also use a procedural shape.");
            if(AlgorithmVersion!=CurrentAlgorithm && (LandShape!=null||InteriorRainMm!=0||ContinentCount>20))throw new ArgumentException("Shape presets, inland moisture and over 20 continents require the current generator.");
            if (SchemaVersion != 3 || (AlgorithmVersion != CurrentAlgorithm && AlgorithmVersion != SubcellAlgorithm && AlgorithmVersion != FeatureBlendAlgorithm && AlgorithmVersion != RangeControlsAlgorithm && AlgorithmVersion != EcologyAlgorithm && AlgorithmVersion != TerrainControlsAlgorithm && AlgorithmVersion != ClimateCalibration.UntrainedAlgorithm && AlgorithmVersion != ClimateCalibration.Algorithm && AlgorithmVersion != ClimateCalibration.AggressiveAlgorithm)) throw new ArgumentException("This recipe uses an older or unsupported generator. Version 3 uses a fixed plate base and detail-dependent terrain; save a new recipe from the current settings.");
            ClimateCalibration.Validate(this);
            if (AlgorithmVersion != CurrentAlgorithm && AlgorithmVersion != SubcellAlgorithm && AlgorithmVersion != FeatureBlendAlgorithm && AlgorithmVersion != RangeControlsAlgorithm && AlgorithmVersion != EcologyAlgorithm && AlgorithmVersion != TerrainControlsAlgorithm && MountainStrengthPercent != 100) throw new ArgumentException("Mountain control requires the current generator version. Regenerate with current settings.");
            foreach (var f in Settings)
            {
                var s = f.GetCustomAttribute<SettingAttribute>(); double v = Convert.ToDouble(f.GetValue(this));
                if (double.IsNaN(v) || double.IsInfinity(v) || v < s.Min || v > s.Max) throw new ArgumentException(s.Label + " must be between " + s.Min + " and " + s.Max + ".");
                if (f.FieldType == typeof(double)) f.SetValue(this, D.Q(v, 1000000));
            }
            if (ContinentCount > MicroplateCount) throw new ArgumentException("Continent count cannot exceed plate count.");
        }
        public static Recipe Preset(string name)
        {
            var r = new Recipe();
            switch (name)
            {
                case "Earth": r.TerrainSource = "Earth"; r.Detail = 80000; break;
                case "Preview": r.Detail = 20000; break;
                case "Ocean world": r.LandFraction = .12; break;
                case "Continental": r.LandFraction = .65; break;
                case "Glacial": r.Glacial = .95; r.TemperatureOffset = -8; break;
                case "500 plates": r.MicroplateCount = 500; break;
            }
            WorldPresets.Apply(name,r);
            return r;
        }
    }
}
