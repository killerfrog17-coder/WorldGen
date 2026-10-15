using System.Globalization;
using WorldGen.Core;

namespace WorldGen.ClimateTraining;

public sealed record Observation(int Block, string Split, int Region, double Land, double Height, double[] T, double[] P);
public sealed record Score(double TemperatureMaeC, double RainLogMae, double RainMaeMm, double Loss, int Blocks);
public sealed class Checkpoint
{
    public string Algorithm = ClimateCalibration.Algorithm, DataHash = "";
    public int Completed, SelectedIteration, StaleChecks;
    public double[] Current = (double[])ClimateCalibration.Baseline.Clone(), Selected = (double[])ClimateCalibration.Baseline.Clone();
    public double CurrentLoss, SelectedValidation;
    public Score BaselineTrain, BaselineValidation;
    public int BaselineResidual;
}

public static class Calibration
{
    public static Observation[] ReadObservations(string path)
    {
        var lines = File.ReadAllLines(path);
        if (lines.Length < 2 || !lines[0].StartsWith("block,latitude,longitude,split,region,land_fraction,height_m,t01,")) throw new InvalidDataException("Unsupported observations format.");
        var rows = lines.Skip(1).Where(l => !string.IsNullOrWhiteSpace(l)).Select(line =>
        {
            var p = line.Split(','); if (p.Length != 31) throw new InvalidDataException("Expected 31 observation columns.");
            double Num(int i) { double v = double.Parse(p[i], CultureInfo.InvariantCulture); if (!double.IsFinite(v)) throw new InvalidDataException("Non-finite observation."); return v; }
            int block = int.Parse(p[0]), region = int.Parse(p[4]);
            if (block < 0 || block >= 16200 || region < 0 || region >= 36 || !new[] { "train", "validation", "test" }.Contains(p[3])) throw new InvalidDataException("Invalid observation identity.");
            var t = Enumerable.Range(7, 12).Select(Num).ToArray(); var rain = Enumerable.Range(19, 12).Select(Num).ToArray();
            if (t.Any(v => v < -90 || v > 65) || rain.Any(v => v < 0 || v > 5000) || Num(5) < .7 || Num(5) > 1) throw new InvalidDataException("Invalid observation units/range.");
            return new Observation(block, p[3], region, Num(5), Num(6), t, rain);
        }).OrderBy(o => o.Block).ToArray();
        if (rows.Select(o => o.Block).Distinct().Count() != rows.Length) throw new InvalidDataException("Duplicate observation block.");
        foreach (string split in new[] { "train", "validation", "test" }) if (rows.Count(o => o.Split == split) < 100) throw new InvalidDataException("Insufficient geographic coverage: " + split);
        return rows;
    }

    // Bounds are global mechanism weights; no latitude/longitude coefficients are fitted.
    public static double[] Propose(double[] current, int iteration, bool aggressive = false)
    {
        var candidate = (double[])current.Clone();
        if (aggressive)
        {
            var explore = new Rng(73421, "climate-calibration-aggressive-v1", iteration);
            int dimensionToChange = (iteration / 2) % candidate.Length;
            bool restart = iteration % 12 == 11, multiple = iteration % 4 == 3;
            for (int i = 0; i < candidate.Length; i++)
            {
                if (!multiple && i != dimensionToChange) continue;
                double min = ClimateCalibration.AggressiveMinimum[i], max = ClimateCalibration.AggressiveMaximum[i];
                double direction = multiple ? explore.Unit() * 2 - 1 : ((iteration & 1) == 0 ? 1 : -1);
                double scale = new[] { .15, .5, 1.5 }[(iteration / 20) % 3];
                double value = i == 0 ? (restart ? min + explore.Unit() * (max - min) : candidate[i] + direction * scale * 5)
                    : Math.Exp(restart ? Math.Log(min) + explore.Unit() * Math.Log(max / min) : Math.Log(candidate[i]) + direction * scale);
                candidate[i] = D.Q(D.Clamp(value, min, max), 1000000);
            }
            return candidate;
        }
        var rng = new Rng(73421, "climate-calibration-proposals-v1", iteration);
        int dimension = (iteration / 2) % candidate.Length;
        double step = .18 / (1 + iteration / 120.0);
        for (int i = 0; i < candidate.Length; i++)
        {
            if (iteration % 5 != 4 && i != dimension) continue;
            double sign = iteration % 5 == 4 ? (rng.Unit() < .5 ? -1 : 1) : ((iteration & 1) == 0 ? 1 : -1);
            double delta = sign * step * (ClimateCalibration.Maximum[i] - ClimateCalibration.Minimum[i]);
            candidate[i] = D.Q(D.Clamp(candidate[i] + delta, ClimateCalibration.Minimum[i], ClimateCalibration.Maximum[i]), 1000000);
        }
        return candidate;
    }
    public static double Penalty(double[] weights)
    {
        double sum = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            double scale = Math.Max(ClimateCalibration.Maximum[i] - ClimateCalibration.Baseline[i], ClimateCalibration.Baseline[i] - ClimateCalibration.Minimum[i]);
            double deviation = (weights[i] - ClimateCalibration.Baseline[i]) / scale;
            sum += deviation * deviation;
        }
        return .10 * sum / weights.Length;
    }
    public static bool NoMaterialRegression(Score candidate, Score baseline) =>
        candidate.TemperatureMaeC <= baseline.TemperatureMaeC * 1.03 + .05 && candidate.RainLogMae <= baseline.RainLogMae * 1.03 + .005;

    public static Dictionary<string, Score> Evaluate(World world, Observation[] observations, bool includeTest = false)
    {
        // The training mesh is fixed at20k, so these are the actual monthly atmospheric values.
        if (world.Surface.Cells.Length != 20000 || world.Climate.AtmosphericMonths.Length != 12) throw new InvalidDataException("Calibration requires the fixed 20k monthly grid.");
        var lookup = observations.Where(o => includeTest || o.Split != "test").ToDictionary(o => o.Block);
        var blocks = new SortedDictionary<int, Aggregate>();
        foreach (var cell in world.Surface.Cells)
        {
            if (world.Hydrology.OceanConnected[cell.Id]) continue;
            double latitude = 90 - D.Angle(cell.Position, new V(0, 0, 1)) * 180 / D.Pi;
            double longitude = Math.Atan2(cell.Position.Y, cell.Position.X) * 180 / Math.PI;
            int y = Math.Clamp((int)Math.Floor((90 - latitude) / 2), 0, 89), x = Math.Clamp((int)Math.Floor((longitude + 180) / 2), 0, 179);
            int block = y * 180 + x;
            if (!lookup.ContainsKey(block)) continue;
            if (!blocks.TryGetValue(block, out var a)) blocks[block] = a = new Aggregate();
            double area = cell.AreaM2; a.Area += area; a.Height += Math.Max(0, world.ElevationM[cell.Id]) * area;
            for (int m = 0; m < 12; m++)
            {
                // Observations Jan..Dec; simulator March..Feb.
                int simMonth = (m + 10) % 12;
                a.T[m] += world.Climate.AtmosphericMonths[simMonth].TemperatureTenthsC[cell.Id] / 10.0 * area;
                a.P[m] += world.Climate.AtmosphericMonths[simMonth].PrecipitationMm[cell.Id] * area;
            }
        }
        var errors = new List<(Observation O, double Area, double T, double LogP, double P)>();
        foreach (var pair in blocks)
        {
            var o = lookup[pair.Key]; var a = pair.Value;
            // Do not train the climate weights to compensate for mismatched coarse mountain heights.
            if (Math.Abs(a.Height / a.Area - Math.Max(0, o.Height)) > 600) continue;
            double t = 0, p = 0, logP = 0;
            for (int m = 0; m < 12; m++)
            {
                t += Math.Abs(a.T[m] / a.Area - o.T[m]) / 12;
                p += Math.Abs(a.P[m] / a.Area - o.P[m]) / 12;
                logP += Math.Abs(Math.Log(1 + a.P[m] / a.Area) - Math.Log(1 + o.P[m])) / 12;
            }
            errors.Add((o, a.Area, t, logP, p));
        }
        var result = new Dictionary<string, Score>();
        foreach (var split in errors.GroupBy(e => e.O.Split))
        {
            // Equal large geographic-region weight, area weighting WITHIN each region.
            // Prevents Eurasia alone or wet tropical cells from dominating the fit.
            var regions = split.GroupBy(e => e.O.Region).Where(g => g.Count() >= 3).Select(g =>
            {
                double area = g.Sum(e => e.Area);
                return (T: g.Sum(e => e.T * e.Area) / area, L: g.Sum(e => e.LogP * e.Area) / area, P: g.Sum(e => e.P * e.Area) / area);
            }).ToArray();
            if (regions.Length < 8 || split.Count() < 100) throw new InvalidDataException("Insufficient matched land coverage for " + split.Key);
            double t = regions.Average(e => e.T), l = regions.Average(e => e.L), p = regions.Average(e => e.P);
            result[split.Key] = new Score(t, l, p, .5 * t / 5 + .5 * l / .7, split.Count());
        }
        foreach (var split in includeTest ? new[] { "train", "validation", "test" } : new[] { "train", "validation" })
            if (!result.ContainsKey(split)) throw new InvalidDataException("Missing matched " + split + " observations.");
        return result;
    }
    sealed class Aggregate { public double Area, Height; public double[] T = new double[12], P = new double[12]; }
}
