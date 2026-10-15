using System;

namespace WorldGen.Presentation
{
    /// <summary>Logarithmic UI travel, with exact zero and four decimal places; not generation math.</summary>
    public static class PercentageControl
    {
        public static double Position(double percent) => Math.Log10(1 + Math.Max(0, Math.Min(100000, percent)) / .0001) / Math.Log10(1000000001);
        public static double Value(double position) => Math.Round(.0001 * (Math.Pow(1000000001, Math.Max(0, Math.Min(1, position))) - 1), 4, MidpointRounding.AwayFromZero);
    }
}
