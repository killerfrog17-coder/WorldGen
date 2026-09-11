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
    [Serializable]
    public sealed class Recipe
    {
        public const string CurrentAlgorithm = "worldgen-1.0.0";
        public int SchemaVersion = 1;
        public string AlgorithmVersion = CurrentAlgorithm;
        [Setting("World", "Seed", 0, 2147483647, 1)] public int Seed = 73421;
        [Setting("World", "Canonical cells (minimum)", 642, 40962, 1)] public int MacroCellCount = 10242;
        [Setting("World", "Planet radius (km)", 1000, 20000, 100)] public int RadiusKm = 6371;
        [Setting("Plates", "Microplates", 4, 500, 1)] public int MicroplateCount = 120;
        [Setting("Plates", "Parent plates", 2, 40, 1)] public int ParentCount = 18;
        [Setting("Plates", "Parent size inequality")] public double ParentInequality = .35;
        [Setting("Plates", "Microplate irregularity")] public double MicroplateIrregularity = .35;
        [Setting("Plates", "Directional inertia")] public double ParentInertia = .65;
        [Setting("Plates", "Growth curvature")] public double ParentCurvature = .4;
        [Setting("Plates", "Contested fraction", 0, .5, .01)] public double PreservedFraction = .1;
        [Setting("Plates", "Boundary roughness")] public double BoundaryRoughness = .35;
        [Setting("Continents", "Continent identities", 1, 12, 1)] public int ContinentCount = 5;
        [Setting("Continents", "Target land fraction", .1, .8, .01)] public double LandFraction = .3;
        [Setting("Continents", "Continent size variation")] public double ContinentVariation = .4;
        [Setting("Continents", "Connectedness")] public double Connectedness = .45;
        [Setting("Continents", "Isthmus tendency")] public double Isthmus = .45;
        [Setting("Continents", "Near straits / shelves")] public double NearStrait = .5;
        [Setting("Tectonics", "Tectonic intensity", 0, 2)] public double TectonicIntensity = 1;
        [Setting("Tectonics", "Parent stress contribution")] public double ParentStress = .65;
        [Setting("Tectonics", "Special collision frequency")] public double CollisionFrequency = .6;
        [Setting("Tectonics", "Special collision strength", 0, 2)] public double CollisionStrength = 1;
        [Setting("Tectonics", "Ocean escape search (km)", 200, 5000, 100)] public int EscapeDistanceKm = 1800;
        [Setting("Tectonics", "Ancient orogen frequency")] public double GhostFrequency = .5;
        [Setting("Tectonics", "Ancient orogen strength", 0, 2)] public double GhostStrength = .6;
        [Setting("Tectonics", "Ancient orogen age")] public double GhostAge = .65;
        [Setting("Tectonics", "Ancient orogen width (km)", 100, 1500, 50)] public int GhostWidthKm = 500;
        [Setting("Tectonics", "Hotspot / volcanic activity")] public double Volcanism = .5;
        [Setting("Tectonics", "Foreland basin strength", 0, 2)] public double ForelandStrength = .7;
        [Setting("Tectonics", "Relief and detail")] public double Relief = .55;
        [Setting("Tectonics", "Ocean floor detail")] public double OceanDetail = .5;
        [Setting("Tectonics", "Preserve inland seas", 0, 1, 1)] public int PreserveInlandSeas = 1;
        [Setting("Tectonics", "Minimum inland sea (km²)", 1000, 1000000, 1000)] public int InlandSeaMinAreaKm2 = 25000;
        [Setting("Tectonics", "Minimum inland sea depth (m)", 10, 2000, 10)] public int InlandSeaMinDepthM = 100;
        [Setting("Erosion", "Hydraulic erosion")] public double Hydraulic = .35;
        [Setting("Erosion", "Thermal erosion")] public double Thermal = .2;
        [Setting("Erosion", "Glacial erosion")] public double Glacial = .35;
        [Setting("Erosion", "Ridge preservation")] public double RidgePreservation = .4;
        [Setting("Hydrology", "Endorheic tendency")] public double Endorheic = .2;
        [Setting("Hydrology", "Minimum lake (km²)", 100, 1000000, 100)] public int MinLakeAreaKm2 = 1000;
        [Setting("Hydrology", "River contributing area (km²)", 1000, 10000000, 1000)] public int RiverThresholdKm2 = 150000;

        public Recipe Clone() => (Recipe)MemberwiseClone();
        public static readonly FieldInfo[] Settings = typeof(Recipe).GetFields().Where(f => f.GetCustomAttribute<SettingAttribute>() != null).OrderBy(f => f.MetadataToken).ToArray();
        public void Validate()
        {
            if (SchemaVersion != 1 || AlgorithmVersion != CurrentAlgorithm) throw new ArgumentException("Unsupported recipe or algorithm version.");
            foreach (var f in Settings)
            {
                var s = f.GetCustomAttribute<SettingAttribute>(); var v = Convert.ToDouble(f.GetValue(this));
                if (double.IsNaN(v) || double.IsInfinity(v) || v < s.Min || v > s.Max) throw new ArgumentException(s.Label + " must be between " + s.Min + " and " + s.Max + ".");
                if (f.FieldType == typeof(double)) f.SetValue(this, D.Q(v, 1000000));
            }
            if (ParentCount > MicroplateCount) throw new ArgumentException("Parent count cannot exceed microplate count.");
            if (ContinentCount > MicroplateCount) throw new ArgumentException("Continent count cannot exceed microplate count.");
        }
        public static Recipe Preset(string name)
        {
            var r = new Recipe();
            switch (name)
            {
                case "Preview": r.MacroCellCount = 2562; r.MicroplateCount = 80; break;
                case "Connected": r.Connectedness = .95; r.Isthmus = .9; r.LandFraction = .4; break;
                case "Isolated": r.Connectedness = 0; r.Isthmus = 0; r.LandFraction = .24; break;
                case "Glacial": r.Glacial = .95; r.Endorheic = .45; break;
                case "500 microplates": r.MicroplateCount = 500; break;
            }
            return r;
        }
    }
}
