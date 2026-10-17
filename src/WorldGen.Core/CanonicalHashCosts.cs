using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace WorldGen.Core
{
    // Explicit diagnostics only: no counters/times are saved or read by rules.
    public sealed class CanonicalHashMetric
    {
        public int Year;
        public bool Completed;
        public long UncachedTreeNodes,TreePayloadBytes,TreeFingerprints,NewFrozenRecords,NewFrozenBytes;
        public double SealMilliseconds,RootMilliseconds;
    }
    internal static class CanonicalHashCosts
    {
        [ThreadStatic] static Scope current;
        internal sealed class Scope:IDisposable
        {
            readonly Scope previous;readonly Action<CanonicalHashMetric> observer;readonly long started;
            internal readonly CanonicalHashMetric Metric;internal long RootTicks;bool disposed;
            internal Scope(Action<CanonicalHashMetric> observer,int year)
            {this.observer=observer;previous=current;Metric=new CanonicalHashMetric{Year=year};started=Stopwatch.GetTimestamp();current=this;}
            internal void Complete(){Metric.Completed=true;}
            public void Dispose()
            {
                if(disposed)return;
                if(!ReferenceEquals(current,this))throw new InvalidOperationException("Canonical diagnostic scopes must close in reverse order.");
                disposed=true;current=previous;
                Metric.SealMilliseconds=(Stopwatch.GetTimestamp()-started)*1000.0/Stopwatch.Frequency;Metric.RootMilliseconds=RootTicks*1000.0/Stopwatch.Frequency;
                // Restore the thread-local scope before invoking external code,
                // even if an observer fails or begins another diagnostic scope.
                observer(Metric);
            }
        }
        internal static Scope Begin(Action<CanonicalHashMetric> observer,int year)=>observer==null?null:new Scope(observer,year);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static void TreeNode(int bytes)
        {var scope=current;if(scope==null)return;scope.Metric.UncachedTreeNodes++;scope.Metric.TreePayloadBytes+=bytes;}
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static void TreeFingerprint()
        {var scope=current;if(scope==null)return;scope.Metric.TreeFingerprints++;scope.Metric.TreePayloadBytes+=36;}
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static void FrozenRecord(int bytes)
        {var scope=current;if(scope==null)return;scope.Metric.NewFrozenRecords++;scope.Metric.NewFrozenBytes+=bytes;}
        internal static long StartRoot()=>current==null?0:Stopwatch.GetTimestamp();
        internal static void EndRoot(long started){if(started!=0&&current!=null)current.RootTicks+=Stopwatch.GetTimestamp()-started;}
    }
    public sealed partial class Prehistory
    {
        // Like WorkObserver, this opt-in callback cannot affect authoritative
        // dates, scheduling, hashes or saved data. It reports once per seal.
        public Action<CanonicalHashMetric> CanonicalHashObserver;
    }
}
