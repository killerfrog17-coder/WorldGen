using WorldGen.Core;
using WorldGen.Presentation;

public static class LakePresentationTests
{
    public static void Run(Action<bool, string> check)
    {
        var w = Fixture();
        foreach (var layer in new[] { Layer.Terrain, Layer.Satellite, Layer.WaterBodies, Layer.LakesAndInlandSeas })
        {
            w.Hydrology.WaterBody[0] = -1;
            var dry = GlobeView.Build(w, layer);
            w.Hydrology.WaterBody[0] = 0;
            foreach (double fraction in new[] { .001, .5, .899999 })
            {
                w.Hydrology.LakeCoverageFraction[0] = fraction;
                var hidden = GlobeView.Build(w, layer);
                check(hidden.Colors.SequenceEqual(dry.Colors), layer + " sub-cell lake retains dry-cell color");
                check(hidden.Positions.SequenceEqual(dry.Positions), layer + " sub-cell lake retains terrain geometry");
            }
            w.Hydrology.LakeCoverageFraction[0] = .9;
            var visible = GlobeView.Build(w, layer);
            check(!visible.Colors.SequenceEqual(dry.Colors), layer + " 90% lake is painted");
            double x = visible.Positions[0], y = visible.Positions[1], z = visible.Positions[2];
            double expectedRadius = 1 + w.Hydrology.Waters[0].SurfaceElevationM * 14 / 6371000;
            check(Math.Abs(Math.Sqrt(x * x + y * y + z * z) - expectedRadius) < 1e-7, layer + " uses precise water surface");
            check(visible.Colors.Skip(9).SequenceEqual(dry.Colors.Skip(9)), layer + " ocean color unaffected by lake fraction");
        }
        w.Hydrology.LakeCoverageFraction[0] = .25;
        string description = GlobeView.Describe(w, 0);
        check(description.Contains("Estimated lake coverage:") && description.Contains("sub-cell footprint in smooth terrain") && description.Contains("km² of this cell"), "Inspection retains fractional lake coverage and area");
        var options = new ViewOptions { ShowRivers = true, GridSpacing = 0 };
        w.Hydrology.Waters[0].SubgridOutlet = true;
        w.Hydrology.Waters[0].ResidenceTimeYears = .25;
        w.Hydrology.Waters[0].CatchmentTransmissionLossM3PerS = 120;
        description = GlobeView.Describe(w, 0);
        check(description.Contains("Inferred subgrid outlet") && description.Contains("coarse-grid approximation") && description.Contains("catchment transmission loss"), "Inspection discloses hydrology approximations");
        var pieces = SurfaceMesh.Chunks(w, options).ToArray();
        check(pieces.Length == SurfaceMesh.ExpectedChunkCount(w, options) && pieces[^1].Triangles.Length == 6, "River ribbon remains visible over a sub-cell lake");
    }

    static World Fixture()
    {
        return new World
        {
            Surface = new Surface
            {
                Cells = new[] { new Cell { Id = 0, Position = new V(1, 0, 0), AreaM2 = 100000000 }, new Cell { Id = 1, Position = new V(0, 1, 0), AreaM2 = 100000000 } },
                Corners = new[] { new V(1, 1, .2).Unit, new V(1, 1, -.2).Unit },
                Triangles = new[] { 0, 0, 1, 0, 1, 1 }, Edges = new[] { new Edge { A = 0, B = 1, TriangleA = 0, TriangleB = 1 } }
            },
            ElevationM = new[] { 100, -100 }, CellMicroplate = new[] { 0, 0 }, Continent = new[] { 0, -1 }, Landmass = new[] { 0, -1 },
            Microplates = new[] { new Microplate { Id = 0, ParentAffinityId = 0 } },
            Climate = new ClimateResult
            {
                Classification = new[] { Koppen.Cfb, Koppen.Ocean },
                NorthernSpring = new ClimateSeason { TemperatureTenthsC = new[] { 100, 100 }, PrecipitationMm = new[] { 250, 250 } },
                NorthernAutumn = new ClimateSeason { TemperatureTenthsC = new[] { 140, 140 }, PrecipitationMm = new[] { 350, 350 } },
                NorthernSummer = new ClimateSeason { TemperatureTenthsC = new[] { 180, 180 }, PrecipitationMm = new[] { 500, 500 } },
                NorthernWinter = new ClimateSeason { TemperatureTenthsC = new[] { 80, 80 }, PrecipitationMm = new[] { 500, 500 } }
            },
            Hydrology = new HydrologyResult
            {
                OceanConnected = new[] { false, true }, WaterBody = new[] { 0, 1 }, LakeCoverageFraction = new[] { .25, 0 },
                Waters = new[] { new WaterBody { Id = 0, Kind = WaterKind.Lake, SurfaceM = 181, SurfaceElevationM = 180.75 }, new WaterBody { Id = 1, Kind = WaterKind.Ocean } },
                Rivers = new[] { new RiverReach { From = 0, To = 1, WidthM = 20 } }
            }
        };
    }
}
