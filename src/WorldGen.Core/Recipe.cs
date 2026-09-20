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
        public const string CurrentAlgorithm = "worldgen-3.6.0";
        public int SchemaVersion = 3;
        public string AlgorithmVersion = CurrentAlgorithm;
        [Setting("World", "Seed", 0, 16777215, 1)] public int Seed = 73421;
        [Setting("World", "Detail", 5000, 2560000, 1000)] public int Detail = 204000;
        [Setting("World", "Irregularity")] public double Irregularity = .75;
        [Setting("World", "Plates", 4, 500, 1)] public int MicroplateCount = 80;
        [Setting("World", "Continents", 1, 10, 1)] public int ContinentCount = 4;
        [Setting("World", "Continental complexes", 1, 10, 1)] public int ContinentalComplexCount = 4;
        [Setting("World", "Roughness", 0, .5, .01)] public double Relief = .4;
        [Setting("World", "Continent size variety")] public double ContinentVariation = .35;
        [Setting("World", "Land coverage", 0, 1, .01)] public double LandFraction = .3;
        [Setting("Terrain sculpting", "Terrain warp")] public double TerrainWarp = .75;
        [Setting("Terrain sculpting", "Smoothing")] public double Smoothing = .1;
        [Setting("Terrain sculpting", "Glacial erosion")] public double Glacial = .5;
        [Setting("Terrain sculpting", "Hydraulic erosion")] public double Hydraulic = .5;
        [Setting("Terrain sculpting", "Thermal erosion")] public double Thermal = .1;
        [Setting("Terrain sculpting", "Ridge sharpening")] public double RidgePreservation = .5;
        [Setting("Climate", "Temperature offset (C)", -15, 15, .5)] public double TemperatureOffset;
        [Setting("Climate", "Precipitation offset", -1, 1, .05)] public double PrecipitationOffset;
        public int RadiusKm => 6371;
        public int MacroCellCount => SphericalMesh.CanonicalCellCount;
        public Recipe Clone() => (Recipe)MemberwiseClone();
        public static readonly FieldInfo[] Settings = typeof(Recipe).GetFields().Where(f => f.GetCustomAttribute<SettingAttribute>() != null).OrderBy(f => f.MetadataToken).ToArray();
        public void Validate()
        {
            if (SchemaVersion != 3 || AlgorithmVersion != CurrentAlgorithm) throw new ArgumentException("This recipe uses an older or unsupported generator. Version 3 uses a fixed plate base and detail-dependent terrain; save a new recipe from the current settings.");
            foreach (var f in Settings)
            {
                var s = f.GetCustomAttribute<SettingAttribute>(); double v = Convert.ToDouble(f.GetValue(this));
                if (double.IsNaN(v) || double.IsInfinity(v) || v < s.Min || v > s.Max) throw new ArgumentException(s.Label + " must be between " + s.Min + " and " + s.Max + ".");
                if (f.FieldType == typeof(double)) f.SetValue(this, D.Q(v, 1000000));
            }
            if (ContinentCount > MicroplateCount) throw new ArgumentException("Continent count cannot exceed plate count.");
            if (ContinentalComplexCount > ContinentCount) ContinentalComplexCount = ContinentCount;
        }
        public static Recipe Preset(string name)
        {
            var r = new Recipe();
            switch (name)
            {
                case "Preview": r.Detail = 20000; break;
                case "Ocean world": r.LandFraction = .12; break;
                case "Continental": r.LandFraction = .65; break;
                case "Glacial": r.Glacial = .95; r.TemperatureOffset = -8; break;
                case "500 plates": r.MicroplateCount = 500; break;
            }
            return r;
        }
    }
}
