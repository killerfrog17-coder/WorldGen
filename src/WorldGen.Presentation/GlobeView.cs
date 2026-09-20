using System;
using System.Linq;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    public enum Layer { Terrain, Satellite, Climate, Heightmap, Microplates, ParentPlates, Continents, Landmasses, Crust, OceanConnectivity, BoundaryType, Stress, CollisionContext, TectonicElevation, PreErosionElevation, FinalElevation, Bathymetry, LandBridges, ErosionDelta, WaterBodies, LakesAndInlandSeas, Watersheds, Rivers, ContributingArea, TemperatureSummer, TemperatureWinter, PrecipitationSummer, PrecipitationWinter, Continentality, WindSummer, WindWinter, OceanCurrentsSummer, OceanCurrentsWinter, Mantle }
    public enum Projection { Globe, Map }
    public sealed class ViewData { public float[] Positions = Array.Empty<float>(), Colors = Array.Empty<float>(), LineColors = Array.Empty<float>(), Lines = Array.Empty<float>(); public int[] Triangles = Array.Empty<int>(), CellIds = Array.Empty<int>(); public bool IsMap, Unlit; }
    public static class GlobeView
    {
        public static string LayerName(Layer layer) => layer == Layer.Microplates ? "Plates" : layer == Layer.ParentPlates ? "Superplates" : System.Text.RegularExpressions.Regex.Replace(layer.ToString(), "([a-z])([A-Z])", "$1 $2");
        static V Blend(V a, V b, double t) => a * (1 - D.Clamp(t)) + b * D.Clamp(t);
        static V Id(int i) { if (i < 0) return new V(.045, .09, .14); uint h = Rng.Mix((uint)i + 8127); return new V(.22 + (h & 255) / 360.0, .22 + ((h >> 8) & 255) / 360.0, .22 + ((h >> 16) & 255) / 360.0); }
        static V PlateColor(World w, int cell)
        {
            int plate = w.CellMicroplate[cell]; uint h = Rng.Mix((uint)plate + 8127);
            double a = (h & 255) / 255.0, b = ((h >> 8) & 255) / 255.0, c = ((h >> 16) & 255) / 255.0;
            return w.Continent[cell] >= 0
                ? new V(.16 + .16 * a, .34 + .28 * b, .13 + .16 * c)
                : new V(.035 + .07 * a, .20 + .20 * b, .44 + .28 * c);
        }
        public static V Elevation(int h)
        {
            if (h < 0) return Blend(new V(.025, .08, .16), new V(.12, .48, .56), (h + 6500) / 6500.0);
            if (h < 700) return Blend(new V(.3, .48, .36), new V(.52, .61, .4), h / 700.0);
            if (h < 2400) return Blend(new V(.52, .61, .4), new V(.59, .48, .34), (h - 700) / 1700.0);
            return Blend(new V(.59, .48, .34), new V(.94, .95, .91), (h - 2400) / 4500.0);
        }
        static V TerrainColor(World w, int cell, int h, int seaLevel)
        {
            if (seaLevel == 0 && h < 0 && !w.Hydrology.OceanConnected[cell]) return Elevation(Math.Max(0, h));
            return Elevation(h - seaLevel);
        }
        internal static ViewData CellValues(World w, Layer layer = Layer.Terrain, int seaLevel = 0, int detail = 0, double irregularity = .75, System.Threading.CancellationToken token = default)
        {
            int n = w.Surface.Cells.Length; var view = new ViewData { Positions = new float[n * 3], Colors = new float[n * 3], Triangles = w.Surface.Triangles, CellIds = Enumerable.Range(0, n).ToArray(), Unlit = layer == Layer.Heightmap || layer == Layer.Climate };
            var boundary = new Boundary[n]; foreach (var b in w.Boundaries) { var e = w.Surface.Edges[b.EdgeId]; foreach (int c in new[] { e.A, e.B }) if (boundary[c] == null || b.Stress > boundary[c].Stress) boundary[c] = b; }
            var land = layer == Layer.LandBridges ? Graph.Components(w.Surface, i => w.ElevationM[i] >= seaLevel) : w.Landmass;
            for (int i = 0; i < n; i++)
            {
                if ((i & 1023) == 0) token.ThrowIfCancellationRequested();
                int h = w.ElevationM[i], mp = w.CellMicroplate[i]; var p = w.Surface.Cells[i].Position; double radius = 1 + Math.Max(h - seaLevel, 0) / 6371000.0 * 14;
                view.Positions[i * 3] = (float)(p.X * radius); view.Positions[i * 3 + 1] = (float)(p.Z * radius); view.Positions[i * 3 + 2] = (float)(-p.Y * radius);
                V color = TerrainColor(w, i, h, seaLevel); var b = boundary[i];
                switch (layer)
                {
                    case Layer.Satellite: color = ClimateColors.Satellite(w, i); break;
                    case Layer.Climate: color = ClimateColors.Classification(w.Climate.Classification[i]); break;
                    case Layer.Heightmap: double shade = D.Clamp((h + 5000) / 11000.0); color = new V(shade, shade, shade); break;
                    case Layer.Microplates: color = PlateColor(w, i); break;
                    case Layer.ParentPlates: color = Id(w.Microplates[mp].ParentAffinityId); break;
                    case Layer.Continents: color = Id(w.Continent[i]); break;
                    case Layer.Landmasses: case Layer.LandBridges: color = Id(land[i]); break;
                    case Layer.Crust: color = w.Continent[i] >= 0 ? new V(.65, .48, .28) : new V(.06, .2, .35); break;
                    case Layer.BoundaryType: color = b == null ? new V(.07, .12, .17) : b.Ghost ? new V(.25, .25, .28) : b.Kind == BoundaryKind.Convergent ? new V(.95, .3, .2) : b.Kind == BoundaryKind.Divergent ? new V(.15, .75, .85) : new V(.85, .65, .2); break;
                    case Layer.Stress: color = Blend(new V(.06, .13, .22), new V(1, .3, .1), w.Stress[i] / 1800.0); break;
                    case Layer.CollisionContext: color = b == null || b.Context == CollisionContext.None ? new V(.08, .13, .19) : b.Context == CollisionContext.EscapeFace ? new V(.2, .8, .65) : b.Context == CollisionContext.DeadEnd ? new V(.9, .3, .35) : new V(.95, .72, .25); break;
                    case Layer.TectonicElevation: color = Elevation(w.TectonicM[i]); break;
                    case Layer.PreErosionElevation: color = Elevation(w.PreErosionM[i]); break;
                    case Layer.Bathymetry: color = h >= seaLevel ? new V(.32, .4, .35) : Elevation(h - seaLevel); break;
                    case Layer.ErosionDelta: int delta = h - w.PreErosionM[i]; color = Blend(new V(.16, .19, .21), delta >= 0 ? new V(.93, .65, .23) : new V(.2, .68, .93), Math.Abs(delta) / 500.0); break;
                    case Layer.TemperatureSummer: color = ClimateColors.Temperature(w.Climate.NorthernSummer.TemperatureTenthsC[i]); break;
                    case Layer.TemperatureWinter: color = ClimateColors.Temperature(w.Climate.NorthernWinter.TemperatureTenthsC[i]); break;
                    case Layer.PrecipitationSummer: color = ClimateColors.Rain(w.Climate.NorthernSummer.PrecipitationMm[i]); break;
                    case Layer.PrecipitationWinter: color = ClimateColors.Rain(w.Climate.NorthernWinter.PrecipitationMm[i]); break;
                    case Layer.Continentality: color = Blend(new V(.05, .3, .6), new V(.95, .5, .15), w.Climate.ContinentalityPermille[i] / 1000.0); break;
                    case Layer.WindSummer: case Layer.WindWinter:
                        var wind = layer == Layer.WindSummer ? w.Climate.NorthernSummer : w.Climate.NorthernWinter;
                        color = Blend(new V(.08, .2, .4), new V(.95, .7, .25), Math.Sqrt(Math.Pow(wind.WindEastMilli[i], 2) + Math.Pow(wind.WindNorthMilli[i], 2)) / 1600); break;
                    case Layer.OceanCurrentsSummer: case Layer.OceanCurrentsWinter:
                        var current = layer == Layer.OceanCurrentsSummer ? w.Climate.NorthernSummer : w.Climate.NorthernWinter;
                        color = !w.Hydrology.OceanConnected[i] ? new V(.19, .25, .2) : Blend(new V(.16, .3, .75), new V(.95, .3, .1), .5 + current.OceanWarmthMilli[i] / 1200.0); break;
                    case Layer.Mantle: color = Blend(new V(.2, .3, .8), new V(.95, .3, .15), .5 + w.MantlePermille[i] / 1200.0); break;
                }
                view.Colors[i * 3] = (float)color.X; view.Colors[i * 3 + 1] = (float)color.Y; view.Colors[i * 3 + 2] = (float)color.Z;
            }
            return view;
        }
        public static readonly Layer[] VisibleLayers = { Layer.Terrain, Layer.Satellite, Layer.Climate, Layer.Heightmap, Layer.Microplates, Layer.ParentPlates, Layer.Continents, Layer.Crust, Layer.BoundaryType, Layer.Stress, Layer.TectonicElevation, Layer.PreErosionElevation, Layer.ErosionDelta, Layer.TemperatureSummer, Layer.TemperatureWinter, Layer.PrecipitationSummer, Layer.PrecipitationWinter, Layer.Continentality, Layer.WindSummer, Layer.WindWinter, Layer.OceanCurrentsSummer, Layer.OceanCurrentsWinter, Layer.Mantle };
        public static ViewData Build(World w, Layer layer = Layer.Terrain, int seaLevel = 0, int detail = 0, double irregularity = .75)
            => SurfaceMesh.Combine(SurfaceMesh.Chunks(w, new ViewOptions { Layer = layer, SeaLevel = seaLevel, GridSpacing = 0 }));
        public static string Describe(World w, int cell)
        {
            if (cell < 0 || cell >= w.Surface.Cells.Length) return "Select a surface cell.";
            int plate = w.CellMicroplate[cell];
            return "Cell " + cell + " | " + w.ElevationM[cell] + " m | Plate " + plate + " | Superplate " + w.Microplates[plate].ParentAffinityId
                + "\nContinent " + w.Continent[cell] + " | Landmass " + w.Landmass[cell]
                + "\nClimate: " + w.Climate.Classification[cell] + " | Northern summer/winter: " + (w.Climate.NorthernSummer.TemperatureTenthsC[cell] / 10.0).ToString("0.0") + "/" + (w.Climate.NorthernWinter.TemperatureTenthsC[cell] / 10.0).ToString("0.0") + " C"
                + "\nAnnual precipitation: " + (w.Climate.NorthernSummer.PrecipitationMm[cell] + w.Climate.NorthernWinter.PrecipitationMm[cell]) + " mm";
        }
    }
}
