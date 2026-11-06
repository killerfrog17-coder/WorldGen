using System;
using System.Diagnostics;

namespace WorldGen.Core
{
    /// <summary>Opt-in, calling-thread-only query attribution. Nested queries
    /// are inclusive; no clocks or allocations when disabled. Gameplay never
    /// enables this; the native performance runner drains it after each frame.</summary>
    public static class TerrainQueryTiming
    {
        [ThreadStatic] public static bool Enabled;
        [ThreadStatic] static long[]? ticks;
        public static long Begin()=>Enabled?Stopwatch.GetTimestamp():0;
        public static void End(int phase,long started)
        {if(started!=0){ticks??=new long[6];ticks[phase]+=Stopwatch.GetTimestamp()-started;}}
        public static void Clear(){if(ticks!=null)Array.Clear(ticks,0,ticks.Length);}
        public static double Milliseconds(int phase)=>ticks==null?0:ticks[phase]*1000d/Stopwatch.Frequency;
    }
}
