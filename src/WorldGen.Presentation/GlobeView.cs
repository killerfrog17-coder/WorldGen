using System;
using System.Linq;
using WorldGen.Core;

namespace WorldGen.Presentation
{
    public enum Layer { Terrain, Microplates, ParentPlates, Contested, Continents, Landmasses, Crust, OceanConnectivity, BoundaryType, Stress, CollisionContext, GhostBoundaries, TectonicElevation, PreErosionElevation, FinalElevation, Bathymetry, LandBridges, ErosionDelta, WaterBodies, LakesAndInlandSeas, Watersheds, Rivers, ContributingArea }
    public sealed class ViewData { public float[] Positions = Array.Empty<float>(), Colors = Array.Empty<float>(), Lines = Array.Empty<float>(); public int[] Triangles = Array.Empty<int>(); }
    public static class GlobeView
    {
        static V Blend(V a, V b, double t) => a * (1 - D.Clamp(t)) + b * D.Clamp(t);
        static V Id(int i) { if (i < 0) return new V(.045, .09, .14); uint h = Rng.Mix((uint)i + 8127); return new V(.22 + (h & 255) / 360.0, .22 + ((h >> 8) & 255) / 360.0, .22 + ((h >> 16) & 255) / 360.0); }
        public static V Elevation(int h)
        {
            if (h < 0) return Blend(new V(.025, .08, .16), new V(.12, .48, .56), (h + 6500) / 6500.0);
            if (h < 700) return Blend(new V(.3, .48, .36), new V(.52, .61, .4), h / 700.0);
            if (h < 2400) return Blend(new V(.52, .61, .4), new V(.59, .48, .34), (h - 700) / 1700.0);
            return Blend(new V(.59, .48, .34), new V(.94, .95, .91), (h - 2400) / 4500.0);
        }
        public static ViewData Build(World w, Layer layer = Layer.Terrain, int seaLevel = 0)
        {
            int n = w.Surface.Cells.Length; var view = new ViewData { Positions = new float[n * 3], Colors = new float[n * 3], Triangles = w.Surface.Triangles };
            var boundary = new Boundary[n]; foreach (var b in w.Boundaries) { var e = w.Surface.Edges[b.EdgeId]; foreach (int c in new[] { e.A, e.B }) if (boundary[c] == null || b.Stress > boundary[c].Stress) boundary[c] = b; }
            var land = layer == Layer.LandBridges ? Graph.Components(w.Surface, i => w.ElevationM[i] >= seaLevel) : w.Landmass;
            for (int i = 0; i < n; i++)
            {
                int h = w.ElevationM[i], mp = w.CellMicroplate[i]; var p = w.Surface.Cells[i].Position; double radius = 1 + Math.Max(h - seaLevel, 0) / 6371000.0 * 14;
                view.Positions[i * 3] = (float)(p.X * radius); view.Positions[i * 3 + 1] = (float)(p.Z * radius); view.Positions[i * 3 + 2] = (float)(-p.Y * radius);
                V color = Elevation(h - seaLevel); var b = boundary[i]; int waterId = w.Hydrology.WaterBody[i];
                switch (layer)
                {
                    case Layer.Microplates: color = Id(mp); break;
                    case Layer.ParentPlates: color = Id(w.Microplates[mp].ParentAffinityId); break;
                    case Layer.Contested: color = w.Microplates[mp].Preserved ? new V(1, .55, .18) : Id(w.Microplates[mp].ParentAffinityId) * .4; break;
                    case Layer.Continents: color = Id(w.Continent[i]); break;
                    case Layer.Landmasses: case Layer.LandBridges: color = Id(land[i]); break;
                    case Layer.Crust: color = w.Continent[i] >= 0 ? new V(.65, .48, .28) : new V(.06, .2, .35); break;
                    case Layer.OceanConnectivity: color = w.Hydrology.OceanConnected[i] ? new V(.08, .45, .65) : h < 0 ? new V(.65, .35, .85) : new V(.3, .35, .27); break;
                    case Layer.BoundaryType: color = b == null ? new V(.07, .12, .17) : b.Ghost ? new V(.25, .25, .28) : b.Kind == BoundaryKind.Convergent ? new V(.95, .3, .2) : b.Kind == BoundaryKind.Divergent ? new V(.15, .75, .85) : new V(.85, .65, .2); break;
                    case Layer.Stress: color = Blend(new V(.06, .13, .22), new V(1, .3, .1), w.Stress[i] / 1800.0); break;
                    case Layer.CollisionContext: color = b == null || b.Context == CollisionContext.None ? new V(.08, .13, .19) : b.Context == CollisionContext.EscapeFace ? new V(.2, .8, .65) : b.Context == CollisionContext.DeadEnd ? new V(.9, .3, .35) : new V(.95, .72, .25); break;
                    case Layer.GhostBoundaries: color = b != null && b.Ghost ? new V(.75, .5, .9) : new V(.08, .13, .19); break;
                    case Layer.TectonicElevation: color = Elevation(w.TectonicM[i]); break;
                    case Layer.PreErosionElevation: color = Elevation(w.PreErosionM[i]); break;
                    case Layer.Bathymetry: color = h >= seaLevel ? new V(.32, .4, .35) : Elevation(h - seaLevel); break;
                    case Layer.ErosionDelta: int delta = h - w.PreErosionM[i]; color = Blend(new V(.16, .19, .21), delta >= 0 ? new V(.93, .65, .23) : new V(.2, .68, .93), Math.Abs(delta) / 500.0); break;
                    case Layer.WaterBodies: color = Id(waterId); break;
                    case Layer.LakesAndInlandSeas: color = waterId < 0 ? new V(.15, .2, .19) : w.Hydrology.Waters[waterId].Kind == WaterKind.Ocean ? new V(.025, .09, .16) : w.Hydrology.Waters[waterId].Kind == WaterKind.TectonicInlandSea ? new V(.62, .38, .87) : new V(.15, .72, .9); break;
                    case Layer.Watersheds: color = h < 0 ? Elevation(h) : Id(w.Hydrology.Watershed[i]); break;
                    case Layer.ContributingArea: color = Blend(new V(.07, .12, .16), new V(.2, .85, .95), Math.Log10(1 + w.Hydrology.ContributingAreaM2[i] / 1e6) / 7); break;
                    case Layer.Rivers: color = Elevation(h) * .55; break;
                }
                if (layer == Layer.Terrain && waterId >= 0 && w.Hydrology.Waters[waterId].Kind != WaterKind.Ocean) color = new V(.1, .43, .57);
                view.Colors[i * 3] = (float)color.X; view.Colors[i * 3 + 1] = (float)color.Y; view.Colors[i * 3 + 2] = (float)color.Z;
            }
            if (layer == Layer.Rivers)
            {
                view.Lines = new float[w.Hydrology.Rivers.Length * 6]; int k = 0;
                foreach (var reach in w.Hydrology.Rivers) foreach (int i in new[] { reach.From, reach.To }) for (int axis = 0; axis < 3; axis++) view.Lines[k++] = view.Positions[i * 3 + axis] * 1.002f;
            }
            return view;
        }
        public static string Describe(World w, int cell)
        {
            if (cell < 0 || cell >= w.Surface.Cells.Length) return "Select a surface cell."; int mp = w.CellMicroplate[cell]; var m = w.Microplates[mp]; int water = w.Hydrology.WaterBody[cell];
            return "Cell " + cell + " · " + w.ElevationM[cell] + " m\nMicroplate " + mp + " · parent " + m.ParentAffinityId + (m.Preserved ? " · independent" : " · fused") + "\nContinent " + w.Continent[cell] + " · landmass " + w.Landmass[cell] + "\nWatershed " + w.Hydrology.Watershed[cell] + " · receiver " + w.Hydrology.Receiver[cell] + "\nContributing area " + (w.Hydrology.ContributingAreaM2[cell] / 1000000) + " km²\n" + (water < 0 ? "Dry surface" : w.Hydrology.Waters[water].Kind + " · surface " + w.Hydrology.Waters[water].SurfaceM + " m") + "\n" + string.Join(", ", w.Features.Where(f => Array.IndexOf(f.Cells, cell) >= 0).Take(5).Select(f => f.Name));
        }
    }
}
