using WorldGen.Core;

// Observational plate-shape report. These measurements deliberately have no
// pass/fail thresholds: reviewed baselines should establish useful bounds first.
public static class PlateShapeDiagnostics
{
    public sealed class PlateRow
    {
        public int Plate, Cells;
        public double AreaKm2, PerimeterKm, IsoperimetricQ, CovarianceAxisRatio;
        public bool Connected;
    }

    public sealed class Report
    {
        public string Label = "", Algorithm = "";
        public int Seed, PlateCount, CoarseCells, MinimumCells, DisconnectedPlates;
        public double MedianQ, P90Q, MedianAxisRatio, P90AxisRatio;
        public PlateRow[] Plates = Array.Empty<PlateRow>();
    }

    public static Report Assess(World world, string label = "", CancellationToken token = default)
    {
        var surface = world.Surface;
        var rows = new List<PlateRow>(world.Microplates.Length);
        foreach (var plate in world.Microplates.OrderBy(p => p.Id))
        {
            token.ThrowIfCancellationRequested();
            double perimeter = plate.BoundaryEdges.Sum(edge => (double)surface.Edges[edge].LengthM);
            double q = perimeter * perimeter / (4 * Math.PI * Math.Max(1, plate.AreaM2));
            rows.Add(new PlateRow
            {
                Plate = plate.Id,
                Cells = plate.Cells.Length,
                AreaKm2 = plate.AreaM2 / 1e6,
                PerimeterKm = perimeter / 1000,
                IsoperimetricQ = q,
                CovarianceAxisRatio = AxisRatio(surface, plate, world.Recipe.RadiusKm * 1000.0),
                Connected = IsConnected(surface, plate.Cells)
            });
        }

        var qValues = rows.Select(p => p.IsoperimetricQ).OrderBy(v => v).ToArray();
        var axisValues = rows.Select(p => p.CovarianceAxisRatio).OrderBy(v => v).ToArray();
        return new Report
        {
            Label = label,
            Algorithm = world.Recipe.AlgorithmVersion,
            Seed = world.Recipe.Seed,
            PlateCount = world.Microplates.Length,
            CoarseCells = surface.Cells.Length,
            MinimumCells = rows.Min(p => p.Cells),
            DisconnectedPlates = rows.Count(p => !p.Connected),
            MedianQ = Percentile(qValues, .5),
            P90Q = Percentile(qValues, .9),
            MedianAxisRatio = Percentile(axisValues, .5),
            P90AxisRatio = Percentile(axisValues, .9),
            Plates = rows.ToArray()
        };
    }

    private static bool IsConnected(Surface surface, int[] members)
    {
        if (members.Length == 0) return false;
        var membership = members.ToHashSet();
        var visited = new HashSet<int> { members[0] };
        var queue = new Queue<int>(); queue.Enqueue(members[0]);
        while (queue.Count > 0)
            foreach (int neighbor in surface.Cells[queue.Dequeue()].Neighbors)
                if (membership.Contains(neighbor) && visited.Add(neighbor)) queue.Enqueue(neighbor);
        return visited.Count == members.Length;
    }

    private static double AxisRatio(Surface surface, Microplate plate, double radiusM)
    {
        V center = plate.Centroid;
        V reference = Math.Abs(center.Z) < .9 ? new V(0, 0, 1) : new V(1, 0, 0);
        V xAxis = V.Tangent(center, reference), yAxis = V.Cross(center, xAxis).Unit;
        double weight = 0, meanX = 0, meanY = 0;
        foreach (int cell in plate.Cells)
        {
            var sample = Coordinates(surface.Cells[cell], center, xAxis, yAxis, radiusM);
            double area = surface.Cells[cell].AreaM2;
            weight += area; meanX += sample.X * area; meanY += sample.Y * area;
        }
        meanX /= weight; meanY /= weight;
        double xx = 0, xy = 0, yy = 0;
        foreach (int cell in plate.Cells)
        {
            var sample = Coordinates(surface.Cells[cell], center, xAxis, yAxis, radiusM);
            double area = surface.Cells[cell].AreaM2, x = sample.X - meanX, y = sample.Y - meanY;
            xx += area * x * x; xy += area * x * y; yy += area * y * y;
        }
        xx /= weight; xy /= weight; yy /= weight;
        double trace = xx + yy;
        double split = Math.Sqrt(Math.Max(0, (xx - yy) * (xx - yy) + 4 * xy * xy));
        double largest = (trace + split) * .5, smallest = (trace - split) * .5;
        return Math.Sqrt(largest / Math.Max(1e-12, smallest));
    }

    private static (double X, double Y) Coordinates(Cell cell, V center, V xAxis, V yAxis, double radiusM)
    {
        double angle = D.Angle(center, cell.Position);
        if (angle <= 1e-12) return (0, 0);
        V direction = V.Tangent(center, cell.Position);
        double distance = angle * radiusM;
        return (distance * V.Dot(direction, xAxis), distance * V.Dot(direction, yAxis));
    }

    private static double Percentile(double[] sorted, double fraction)
    {
        if (sorted.Length == 0) return 0;
        double position = (sorted.Length - 1) * fraction;
        int lower = (int)Math.Floor(position), upper = (int)Math.Ceiling(position);
        if (lower == upper) return sorted[lower];
        return sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
    }
}
