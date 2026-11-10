using System.Diagnostics;

namespace WorldGen.Presentation
{
    /// <summary>Explicit offline profiling only. Mutually exclusive mesh stages.</summary>
    public sealed class TerrainBuildTiming
    {
        public static readonly string[] Names={"patch/index setup","height/water sampling","surface materials","edge stitching","channel refinement","water meshes","cover masks"};
        public readonly double[] Milliseconds=new double[7];
        readonly Stopwatch clock=Stopwatch.StartNew();double last;
        public long GeometrySamples,MaterialSamples,FullSurfaceQueries,GeometryQueries,SurfaceFallbacks;
        public void Mark(int stage){double now=clock.Elapsed.TotalMilliseconds;Milliseconds[stage]+=now-last;last=now;}
    }
}
