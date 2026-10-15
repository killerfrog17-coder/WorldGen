using WorldGen.Core;

// Review metric, not a claim that every curved arc is a defect. Uses final
// emerged terrain, not feature labels or pre-erosion volcanic potentials.
public static class IslandRingDiagnostics
{
    public sealed class PlateRow
    {
        public int Plate, OccupiedSectors, AvailableSectors;
        public double AreaKm2, PerimeterKm, CoveredFraction;
        public bool Suspicious;
    }
    public sealed class Report
    {
        public int Seed, Detail, DetachedIslands, SuspiciousPlates;
        public double IslandAreaKm2, CoveredPerimeterKm, EligiblePerimeterKm;
        public string Algorithm = "";
        public PlateRow[] Plates = Array.Empty<PlateRow>();
    }
    public static Report Assess(World w, CancellationToken token = default)
    {
        const int radius = 400000, sectors = 16;
        var s = w.Surface;
        var continentalMasses = new HashSet<int>();
        for (int i = 0; i < s.Cells.Length; i++)
            if (w.Continent[i] >= 0 && w.Landmass[i] >= 0) continentalMasses.Add(w.Landmass[i]);
        var islands = new List<int>(); var islandIds = new HashSet<int>(); double islandArea = 0;
        for (int i = 0; i < s.Cells.Length; i++)
        {
            if ((i & 4095) == 0) token.ThrowIfCancellationRequested();
            if (w.Continent[i] >= 0 || w.ElevationM[i] <= 0 || w.Hydrology.OceanConnected[i] || continentalMasses.Contains(w.Landmass[i])) continue;
            islands.Add(i); islandIds.Add(w.Landmass[i]); islandArea += s.Cells[i].AreaM2;
        }
        var distance = Graph.Distances(s, islands, out _, token);
        double meanPlateArea = s.Cells.Sum(c => (double)c.AreaM2) / w.Microplates.Length;
        var rows = new List<PlateRow>(); double coveredTotal = 0, perimeterTotal = 0;
        foreach (var plate in w.Microplates)
        {
            token.ThrowIfCancellationRequested();
            if (plate.ContinentId >= 0 || plate.AreaM2 > meanPlateArea) continue;
            var total = new double[sectors]; var covered = new double[sectors];
            V center = plate.Centroid, axis = V.Tangent(center, Math.Abs(center.Z) < .9 ? new V(0, 0, 1) : new V(1, 0, 0));
            V second = V.Cross(center, axis);
            foreach (int edgeId in plate.BoundaryEdges)
            {
                var edge = s.Edges[edgeId]; V midpoint = (s.Cells[edge.A].Position + s.Cells[edge.B].Position).Unit;
                V direction = V.Tangent(center, midpoint - center);
                // This diagnostic's angular binning is not authoritative state.
                double angle = Math.Atan2(V.Dot(direction, second), V.Dot(direction, axis)) + Math.PI;
                int sector = Math.Min(sectors - 1, (int)(angle / (2 * Math.PI) * sectors));
                total[sector] += edge.LengthM;
                if (Math.Min(distance[edge.A], distance[edge.B]) <= radius) covered[sector] += edge.LengthM;
            }
            double perimeter = total.Sum(), cover = covered.Sum(); if (perimeter == 0) continue;
            int available = total.Count(v => v > 0), occupied = Enumerable.Range(0, sectors).Count(k => total[k] > 0 && covered[k] / total[k] >= .35);
            var row = new PlateRow { Plate = plate.Id, AreaKm2 = plate.AreaM2 / 1e6, PerimeterKm = perimeter / 1000,
                AvailableSectors = available, OccupiedSectors = occupied, CoveredFraction = cover / perimeter,
                Suspicious = available >= 12 && occupied >= 12 && cover / perimeter >= .6 };
            rows.Add(row); perimeterTotal += perimeter; coveredTotal += cover;
        }
        return new Report { Seed = w.Recipe.Seed, Detail = w.Recipe.Detail, Algorithm = w.Recipe.AlgorithmVersion,
            DetachedIslands = islandIds.Count, IslandAreaKm2 = islandArea / 1e6, SuspiciousPlates = rows.Count(p => p.Suspicious),
            CoveredPerimeterKm = coveredTotal / 1000, EligiblePerimeterKm = perimeterTotal / 1000, Plates = rows.ToArray() };
    }
}
