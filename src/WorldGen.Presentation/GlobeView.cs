using System;
using System.Linq;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    public enum Layer { Terrain, Satellite, Climate, Heightmap, Microplates, ParentPlates, Continents, Landmasses, Crust, OceanConnectivity, BoundaryType, Stress, CollisionContext, TectonicElevation, PreErosionElevation, FinalElevation, Bathymetry, LandBridges, ErosionDelta, WaterBodies, LakesAndInlandSeas, Watersheds, Rivers, ContributingArea, TemperatureSummer, TemperatureWinter, PrecipitationSummer, PrecipitationWinter, Continentality, WindSummer, WindWinter, OceanCurrentsSummer, OceanCurrentsWinter, Mantle, TemperatureSpring, TemperatureAutumn, PrecipitationSpring, PrecipitationAutumn, Ecoregions, Biomes, Floodplains, Soils, WaterTable, Permafrost, Aridity, Salinity, FirePotential, Relief, Simulation = 100 }
    public enum Projection { Globe, Map }
    public sealed class ViewData { public float[] Positions = Array.Empty<float>(), Colors = Array.Empty<float>(), LineColors = Array.Empty<float>(), Lines = Array.Empty<float>(); public float[] SpherePositions = Array.Empty<float>(), SphereLines = Array.Empty<float>(), NaturalHeight = Array.Empty<float>(), Stroke = Array.Empty<float>(); public int[] Triangles = Array.Empty<int>(), CellIds = Array.Empty<int>(), BlendCellIds = Array.Empty<int>(); public bool IsMap, Unlit; }
    public static class GlobeView
    {
        static readonly WaterBody MissingWater = new WaterBody();
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
            if (seaLevel == 0 && TryVisibleWater(w, cell, out var water) && water.Kind != WaterKind.Ocean) return new V(.055, .34, .68);
            if (seaLevel == 0 && h < 0 && !w.Hydrology.OceanConnected[cell]) return Elevation(Math.Max(0, h));
            return Elevation(h - seaLevel);
        }
        internal static bool TryWater(World w, int cell, out WaterBody water)
        {
            water = MissingWater; if (cell < 0 || cell >= w.Hydrology.WaterBody.Length) return false;
            int id = w.Hydrology.WaterBody[cell];
            if (id < 0 || id >= w.Hydrology.Waters.Length) return false;
            var found = w.Hydrology.Waters[id]; if (found == null) return false;
            water = found; return true;
        }
        // Sub-cell lakes remain authoritative hydrology, not whole-cell paint.
        // Keep one visibility rule for terrain, diagnostics and surface geometry.
        public const double WholeCellLakeCoverage = .9;
        internal static bool TryVisibleWater(World w, int cell, out WaterBody water)
            => TryWater(w, cell, out water) && (water.Kind == WaterKind.Ocean
                || (cell < w.Hydrology.LakeCoverageFraction.Length && w.Hydrology.LakeCoverageFraction[cell] >= WholeCellLakeCoverage));
        public static ViewData CellValues(World w, Layer layer = Layer.Terrain, int seaLevel = 0, int detail = 0, double irregularity = .75, System.Threading.CancellationToken token = default)
        {
            int n = w.Surface.Cells.Length; var view = new ViewData { Positions = new float[n * 3], Colors = new float[n * 3], Triangles = w.Surface.Triangles, CellIds = Enumerable.Range(0, n).ToArray(), Unlit = layer == Layer.Heightmap || layer == Layer.Climate || layer == Layer.Relief || EcologyView.IsLayer(layer) };
            var boundary = new Boundary[n]; foreach (var b in w.Boundaries) { var e = w.Surface.Edges[b.EdgeId]; foreach (int c in new[] { e.A, e.B }) if (boundary[c] == null || b.Stress > boundary[c].Stress) boundary[c] = b; }
            var land = layer == Layer.LandBridges ? Graph.Components(w.Surface, i => w.ElevationM[i] >= seaLevel) : w.Landmass;
            var displayedRiverOrder = layer == Layer.Rivers ? new int[n] : null;
            if (displayedRiverOrder != null) foreach (var river in w.Hydrology.Rivers)
                if (river.From >= 0 && river.From < n) displayedRiverOrder[river.From] = Math.Max(displayedRiverOrder[river.From], river.Order);
            for (int i = 0; i < n; i++)
            {
                if ((i & 1023) == 0) token.ThrowIfCancellationRequested();
                int h = w.ElevationM[i], mp = w.CellMicroplate[i]; var p = w.Surface.Cells[i].Position; double radius = 1 + Math.Max(h - seaLevel, 0) / 6371000.0 * 14;
                view.Positions[i * 3] = (float)(p.X * radius); view.Positions[i * 3 + 1] = (float)(p.Z * radius); view.Positions[i * 3 + 2] = (float)(-p.Y * radius);
                V color = TerrainColor(w, i, h, seaLevel); var b = boundary[i];
                switch (layer)
                {
                    case Layer.Relief:
                        double relief=TerrainRelief.For(w,i).DetailM;
                        color=TryVisibleWater(w,i,out _)||h<0?new V(.055,.25,.43):relief<120
                            ?Blend(new V(.24,.49,.35),new V(.76,.73,.40),relief/120)
                            :Blend(new V(.76,.73,.40),new V(.75,.34,.22),(Math.Log(1+relief/120)-Math.Log(2))/2);
                        break;
                    case Layer.Ecoregions: case Layer.Biomes: case Layer.Floodplains: case Layer.Soils: case Layer.WaterTable: case Layer.Permafrost: case Layer.Aridity: case Layer.Salinity: case Layer.FirePotential: color=EcologyView.Color(w,i,layer); break;
                    case Layer.Simulation: case Layer.Satellite: color = ClimateColors.Satellite(w, i); if (TryVisibleWater(w, i, out var satelliteWater) && satelliteWater.Kind != WaterKind.Ocean) color = new V(.055, .34, .68); break;
                    case Layer.Climate: color = TryVisibleWater(w, i, out var climateWater) && climateWater.Kind != WaterKind.Ocean
                        ? new V(.055, .34, .68) : ClimateColors.Classification(w.Climate.Classification[i]); break;
                    case Layer.Heightmap: double shade = D.Clamp((h + 5000) / 11000.0); color = new V(shade, shade, shade); break;
                    case Layer.Microplates: color = PlateColor(w, i); break;
                    case Layer.ParentPlates: color = Id(w.Microplates[mp].ParentAffinityId); break;
                    case Layer.Continents: color = Id(w.Continent[i]); break;
                    case Layer.Landmasses: case Layer.LandBridges: color = Id(land[i]); break;
                    case Layer.Crust: color = w.Continent[i] >= 0 ? new V(.65, .48, .28) : new V(.06, .2, .35); break;
                    case Layer.OceanConnectivity: color = w.Hydrology.OceanConnected.Length > i && w.Hydrology.OceanConnected[i] ? new V(.04, .28, .62) : new V(.34, .45, .29); break;
                    case Layer.BoundaryType: color = b == null ? new V(.07, .12, .17) : b.Ghost ? new V(.25, .25, .28) : b.Kind == BoundaryKind.Convergent ? new V(.95, .3, .2) : b.Kind == BoundaryKind.Divergent ? new V(.15, .75, .85) : new V(.85, .65, .2); break;
                    case Layer.Stress: color = Blend(new V(.06, .13, .22), new V(1, .3, .1), w.Stress[i] / 1800.0); break;
                    case Layer.CollisionContext: color = b == null || b.Context == CollisionContext.None ? new V(.08, .13, .19) : b.Context == CollisionContext.EscapeFace ? new V(.2, .8, .65) : b.Context == CollisionContext.DeadEnd ? new V(.9, .3, .35) : new V(.95, .72, .25); break;
                    case Layer.TectonicElevation: color = Elevation(w.TectonicM[i]); break;
                    case Layer.PreErosionElevation: color = Elevation(w.PreErosionM[i]); break;
                    case Layer.FinalElevation: color = Elevation(h); break;
                    case Layer.Bathymetry: color = h >= seaLevel ? new V(.32, .4, .35) : Elevation(h - seaLevel); break;
                    case Layer.ErosionDelta: int delta = h - w.PreErosionM[i]; color = Blend(new V(.16, .19, .21), delta >= 0 ? new V(.93, .65, .23) : new V(.2, .68, .93), Math.Abs(delta) / 500.0); break;
                    case Layer.WaterBodies:
                        color = TryVisibleWater(w, i, out var body) ? body.Kind == WaterKind.Ocean ? new V(.025, .12, .27) : Id(body.Id + 10000) : new V(.18, .21, .17); break;
                    case Layer.LakesAndInlandSeas:
                        color = !TryVisibleWater(w, i, out var lake) ? new V(.18, .21, .17) : lake.Kind == WaterKind.Ocean ? new V(.035, .11, .22)
                            : lake.Kind == WaterKind.TectonicInlandSea ? new V(.08, .46, .72) : lake.Kind == WaterKind.GlacialLake ? new V(.38, .78, .92)
                            : lake.Endorheic ? new V(.15, .56, .62) : new V(.08, .38, .78); break;
                    case Layer.Watersheds:
                        color = w.Hydrology.Watershed.Length <= i || w.Hydrology.Watershed[i] < 0 ? new V(.04, .12, .22) : Id(w.Hydrology.Watershed[i] + 20000); break;
                    case Layer.Rivers:
                        int order = displayedRiverOrder![i];
                        color = order > 0 ? Blend(new V(.12, .55, .9), new V(.02, .2, .62), order / 8.0) : w.Hydrology.OceanConnected.Length > i && w.Hydrology.OceanConnected[i] ? new V(.035, .12, .23) : new V(.18, .21, .17); break;
                    case Layer.ContributingArea:
                        double area = w.Hydrology.ContributingAreaM2.Length > i ? w.Hydrology.ContributingAreaM2[i] : 0;
                        color = Blend(new V(.16, .2, .17), new V(.05, .48, .92), (Math.Log10(Math.Max(1, area)) - 8) / 6); break;
                    case Layer.TemperatureSummer: color = ClimateColors.Temperature(w.Climate.NorthernSummer.TemperatureTenthsC[i]); break;
                    case Layer.TemperatureSpring: color = ClimateColors.Temperature(w.Climate.NorthernSpring.TemperatureTenthsC[i]); break;
                    case Layer.TemperatureAutumn: color = ClimateColors.Temperature(w.Climate.NorthernAutumn.TemperatureTenthsC[i]); break;
                    case Layer.TemperatureWinter: color = ClimateColors.Temperature(w.Climate.NorthernWinter.TemperatureTenthsC[i]); break;
                    case Layer.PrecipitationSummer: color = ClimateColors.Rain(w.Climate.NorthernSummer.PrecipitationMm[i]); break;
                    case Layer.PrecipitationSpring: color = ClimateColors.Rain(w.Climate.NorthernSpring.PrecipitationMm[i]); break;
                    case Layer.PrecipitationAutumn: color = ClimateColors.Rain(w.Climate.NorthernAutumn.PrecipitationMm[i]); break;
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
        public static readonly Layer[] VisibleLayers = { Layer.Terrain, Layer.Relief, Layer.Satellite, Layer.Simulation, Layer.Climate, Layer.Heightmap, Layer.Ecoregions, Layer.Biomes, Layer.Floodplains, Layer.Soils, Layer.WaterTable, Layer.Permafrost, Layer.Aridity, Layer.Salinity, Layer.FirePotential, Layer.Microplates, Layer.ParentPlates, Layer.Continents, Layer.Crust, Layer.OceanConnectivity, Layer.BoundaryType, Layer.Stress, Layer.TectonicElevation, Layer.PreErosionElevation, Layer.FinalElevation, Layer.ErosionDelta, Layer.WaterBodies, Layer.LakesAndInlandSeas, Layer.Watersheds, Layer.Rivers, Layer.ContributingArea, Layer.TemperatureSpring, Layer.TemperatureSummer, Layer.TemperatureAutumn, Layer.TemperatureWinter, Layer.PrecipitationSpring, Layer.PrecipitationSummer, Layer.PrecipitationAutumn, Layer.PrecipitationWinter, Layer.Continentality, Layer.WindSummer, Layer.WindWinter, Layer.OceanCurrentsSummer, Layer.OceanCurrentsWinter, Layer.Mantle };
        public static ViewData Build(World w, Layer layer = Layer.Terrain, int seaLevel = 0, int detail = 0, double irregularity = .75)
            => SurfaceMesh.Combine(SurfaceMesh.Chunks(w, new ViewOptions { Layer = layer, SeaLevel = seaLevel, GridSpacing = 0 }));
        public static string Describe(World w, int cell)
        {
            if (cell < 0 || cell >= w.Surface.Cells.Length) return "Select a surface cell.";
            int plate = w.CellMicroplate[cell];
            string geology = string.Join(", ", w.Features.Where(f => f.Cells.Contains(cell)).Take(5)
                .Select(f => f.Kind + (f.AgeMyr > 0 ? " (" + f.AgeMyr + " Myr proxy)" : "") + (f.BeltId >= 0 ? " [belt " + f.BeltId + "]" : "")));
            string water = TryWater(w, cell, out var body) ? "\nWater: " + body.Kind + " | surface " + body.SurfaceElevationM.ToString("0.00") + " m | " + (body.Endorheic ? "endorheic" : "outlet " + body.OutletCell) : "";
            if (body.Kind != WaterKind.Ocean && cell < w.Hydrology.LakeCoverageFraction.Length && w.Hydrology.LakeCoverageFraction[cell] > 0)
            {
                double coverage = w.Hydrology.LakeCoverageFraction[cell];
                water += "\nEstimated lake coverage: " + (coverage * 100).ToString("0.0") + "% | "
                    + (coverage * w.Surface.Cells[cell].AreaM2 / 1000000.0).ToString("N2") + " km² of this cell"
                    + (coverage < WholeCellLakeCoverage ? " | sub-cell footprint in smooth terrain" : " | shown as lake");
            }
            var reach = w.Hydrology.Rivers.FirstOrDefault(r => r.From == cell);
            if (body.SubgridOutlet)
                water += "\nInferred subgrid outlet | retention " + body.ResidenceTimeYears.ToString("0.##") + " years (coarse-grid approximation)";
            if (body.CatchmentTransmissionLossM3PerS > 0)
                water += "\nEstimated catchment transmission loss: " + body.CatchmentTransmissionLossM3PerS.ToString("N1") + " m3/s";
            string drainage = w.Hydrology.Watershed.Length <= cell ? "" : "\nDrainage: watershed " + w.Hydrology.Watershed[cell]
                + (w.Hydrology.ContributingAreaM2.Length <= cell ? "" : " | contributing " + (w.Hydrology.ContributingAreaM2[cell] / 1000000.0).ToString("N0") + " km²")
                + (reach == null ? "" : " | river order " + reach.Order + " | width " + reach.WidthM.ToString("N1") + " m"
                    + " | spring/summer/autumn/winter " + reach.NorthernSpringDischargeM3PerS.ToString("N1") + "/" + reach.NorthernSummerDischargeM3PerS.ToString("N1") + "/" + reach.NorthernAutumnDischargeM3PerS.ToString("N1") + "/" + reach.NorthernWinterDischargeM3PerS.ToString("N1") + " m³/s");
            return "Cell " + cell + " | " + w.ElevationM[cell] + " m | Plate " + plate + " | Superplate " + w.Microplates[plate].ParentAffinityId
                + "\nContinent " + w.Continent[cell] + " | Landmass " + w.Landmass[cell]
                + "\nClimate: " + w.Climate.Classification[cell] + " | Northern spring/summer/autumn/winter: " + string.Join(" / ", new[] { w.Climate.NorthernSpring, w.Climate.NorthernSummer, w.Climate.NorthernAutumn, w.Climate.NorthernWinter }.Select(s => (s.TemperatureTenthsC[cell] / 10.0).ToString("0.0"))) + " C"
                + "\nSeasonal precipitation (spring/summer/autumn/winter): " + string.Join(" / ", new[] { w.Climate.NorthernSpring, w.Climate.NorthernSummer, w.Climate.NorthernAutumn, w.Climate.NorthernWinter }.Select(s => s.PrecipitationMm[cell])) + " mm per quarter"
                + "\nAnnual precipitation: " + (w.Climate.NorthernSpring.PrecipitationMm[cell] + w.Climate.NorthernSummer.PrecipitationMm[cell] + w.Climate.NorthernAutumn.PrecipitationMm[cell] + w.Climate.NorthernWinter.PrecipitationMm[cell]) + " mm"
                + water + drainage + (geology.Length == 0 ? "" : "\nGeology: " + geology) + EcologyView.Describe(w,cell);
        }
    }
}
