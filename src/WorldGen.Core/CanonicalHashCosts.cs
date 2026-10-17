using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace WorldGen.Core
{
    // Explicit diagnostics only: no counters/times are saved or read by rules.
    public sealed class CanonicalHashMetric
    {
        public int Year;
        public bool Completed;
        public long UncachedTreeNodes,TreePayloadBytes,TreeFingerprints,NewFrozenRecords,NewFrozenBytes;
        public long DirtyTreeBranches,DirtyTreeBranchSlots,DirtyTreePresentChildren;
        public double SealMilliseconds,RootMilliseconds;
        public IReadOnlyList<CanonicalFrozenRecordCost> FrozenRecordsByType {get;internal set;}=Array.Empty<CanonicalFrozenRecordCost>();
        public IReadOnlyList<CanonicalComponentCost> Components {get;internal set;}=Array.Empty<CanonicalComponentCost>();
        public IReadOnlyList<CanonicalFieldCost> Fields {get;internal set;}=Array.Empty<CanonicalFieldCost>();
        public long RelationReadCalls,RelationReadsWithMemo,RelationForkInvalidations,RelationCapturedOriginals,UnchangedRelationFreezes,ChangedRelationFreezes,UncomparedRelationFreezes;
    }
    public sealed class CanonicalFrozenRecordCost
    {
        public string Type {get;}public long Count {get;}public long Bytes {get;}
        internal CanonicalFrozenRecordCost(string type,long count,long bytes){Type=type;Count=count;Bytes=bytes;}
    }
    public sealed class CanonicalComponentCost
    {
        public int Index {get;}public string Type {get;}public double Milliseconds {get;}public bool Completed {get;}
        internal CanonicalComponentCost(int index,string type,double milliseconds,bool completed){Index=index;Type=type;Milliseconds=milliseconds;Completed=completed;}
    }
    public sealed class CanonicalFieldCost
    {
        public string Container {get;}public string Field {get;}public string Type {get;}public double Milliseconds {get;}public bool Completed {get;}
        internal CanonicalFieldCost(string container,string field,object value,double milliseconds,bool completed){Container=container;Field=field;Type=value?.GetType().FullName??"<null>";Milliseconds=milliseconds;Completed=completed;}
    }
    internal static class CanonicalHashCosts
    {
        [ThreadStatic] static Scope current;
        [ThreadStatic] static RelationReadScope relationReads;
        internal sealed class RelationReadScope:IDisposable
        {
            readonly RelationReadScope previous;internal long Reads,ReadsWithMemo,ForkInvalidations;
            internal readonly Dictionary<HistoricalRelation,byte[]> Originals=new Dictionary<HistoricalRelation,byte[]>();bool disposed;
            internal RelationReadScope(){previous=relationReads;relationReads=this;}
            public void Dispose(){if(disposed)return;if(!ReferenceEquals(relationReads,this))throw new InvalidOperationException("Relation diagnostic scopes must close in reverse order.");relationReads=previous;disposed=true;Originals.Clear();}
        }
        internal sealed class Scope:IDisposable
        {
            readonly Scope previous;readonly Action<CanonicalHashMetric> observer;readonly long started;
            internal readonly CanonicalHashMetric Metric;internal long RootTicks;bool disposed;
            internal readonly bool TraceComponents;internal object RootValue;
            internal List<CanonicalComponentCost> Components;
            internal List<CanonicalFieldCost> Fields;
            readonly Dictionary<string,(long Count,long Bytes)> frozenTypes=new Dictionary<string,(long,long)>(StringComparer.Ordinal);
            internal Scope(Action<CanonicalHashMetric> observer,int year,bool traceComponents=false)
            {this.observer=observer;TraceComponents=traceComponents;previous=current;Metric=new CanonicalHashMetric{Year=year};started=Stopwatch.GetTimestamp();current=this;if(relationReads!=null){Metric.RelationReadCalls=relationReads.Reads;Metric.RelationReadsWithMemo=relationReads.ReadsWithMemo;Metric.RelationForkInvalidations=relationReads.ForkInvalidations;Metric.RelationCapturedOriginals=relationReads.Originals.Count;}}
            internal void Frozen(string type,byte[] bytes,object identity)
            {
                Metric.NewFrozenRecords++;Metric.NewFrozenBytes+=bytes.Length;
                frozenTypes.TryGetValue(type,out var prior);frozenTypes[type]=(prior.Count+1,prior.Bytes+bytes.Length);
                if(type!="WorldGen.Core.HistoricalRelation")return;
                if(identity is HistoricalRelation row&&relationReads!=null&&relationReads.Originals.TryGetValue(row,out var original))
                {
                    bool same=bytes.Length==original.Length;for(int i=0;same&&i<bytes.Length;i++)same=bytes[i]==original[i];
                    if(same)Metric.UnchangedRelationFreezes++;else Metric.ChangedRelationFreezes++;
                }
                else Metric.UncomparedRelationFreezes++;
            }
            internal void Complete(){Metric.Completed=true;}
            public void Dispose()
            {
                if(disposed)return;
                if(!ReferenceEquals(current,this))throw new InvalidOperationException("Canonical diagnostic scopes must close in reverse order.");
                disposed=true;current=previous;
                Metric.SealMilliseconds=(Stopwatch.GetTimestamp()-started)*1000.0/Stopwatch.Frequency;Metric.RootMilliseconds=RootTicks*1000.0/Stopwatch.Frequency;
                Metric.FrozenRecordsByType=Array.AsReadOnly(frozenTypes.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>new CanonicalFrozenRecordCost(p.Key,p.Value.Count,p.Value.Bytes)).ToArray());
                if(Components!=null)Metric.Components=Array.AsReadOnly(Components.ToArray());
                if(Fields!=null)Metric.Fields=Array.AsReadOnly(Fields.ToArray());
                RootValue=null;
                // Restore the thread-local scope before invoking external code,
                // even if an observer fails or begins another diagnostic scope.
                observer(Metric);
            }
        }
        internal static Scope Begin(Action<CanonicalHashMetric> observer,int year)=>observer==null?null:new Scope(observer,year);
        internal static Scope BeginFrame(Action<CanonicalHashMetric> observer,int year,bool components)=>observer==null?null:new Scope(observer,year,components);
        internal static RelationReadScope BeginRelationReads(bool enabled)=>enabled?new RelationReadScope():null;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static void RelationRead(HistoricalRelation row,HistoricalRelation source=null)
        {
            var scope=relationReads;if(scope==null)return;scope.Reads++;
            if(row==null)return;
            if(row.OwnedCanonicalRecord!=null){scope.ReadsWithMemo++;if(!scope.Originals.ContainsKey(row))scope.Originals.Add(row,row.OwnedCanonicalRecord);}
            else if(source!=null&&scope.Originals.TryGetValue(source,out var original)){scope.Originals.Remove(source);scope.Originals[row]=original;}
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static void RelationFork(HistoricalRelation source,HistoricalRelation copy)
        {var scope=relationReads;if(scope==null||copy==null)return;if(source?.OwnedCanonicalRecord!=null){scope.ForkInvalidations++;scope.Originals[copy]=source.OwnedCanonicalRecord;}}
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static void TreeNode(int bytes)
        {var scope=current;if(scope==null)return;scope.Metric.UncachedTreeNodes++;scope.Metric.TreePayloadBytes+=bytes;}
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static void TreeFingerprint()
        {var scope=current;if(scope==null)return;scope.Metric.TreeFingerprints++;scope.Metric.TreePayloadBytes+=36;}
        internal static bool TraceTreeBranches=>current!=null&&current.TraceComponents;
        internal static void TreeBranch(int present){var metric=current.Metric;metric.DirtyTreeBranches++;metric.DirtyTreeBranchSlots+=32;metric.DirtyTreePresentChildren+=present;}
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static void FrozenRecord(string type,byte[] bytes,object identity=null)
        {var scope=current;if(scope!=null)scope.Frozen(type,bytes,identity);}
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static void FrozenRecord(object identity,byte[] bytes)
        {var scope=current;if(scope!=null)scope.Frozen(identity?.GetType().FullName??"<null>",bytes,identity);}
        internal static long StartRoot(object value){var scope=current;if(scope==null)return 0;if(scope.TraceComponents&&scope.RootValue==null)scope.RootValue=value;return Stopwatch.GetTimestamp();}
        internal static void EndRoot(long started){if(started!=0&&current!=null)current.RootTicks+=Stopwatch.GetTimestamp()-started;}
        [MethodImpl(MethodImplOptions.AggressiveInlining)] internal static bool IsTimedRoot(object value)=>current!=null&&current.TraceComponents&&ReferenceEquals(current.RootValue,value);
        internal static string TimedFieldContainer(object value)
        {if(current==null||!current.TraceComponents)return null;return value is HistoricalAggregateState||value is HistoricalImperialSystem||value is HistoricalColonialConnections?value.GetType().FullName:null;}
        internal static void Field(string container,string field,object value,long started,bool completed)
        {var scope=current;if(scope.Fields==null)scope.Fields=new List<CanonicalFieldCost>();scope.Fields.Add(new CanonicalFieldCost(container,field,value,(Stopwatch.GetTimestamp()-started)*1000.0/Stopwatch.Frequency,completed));}
        internal static void Component(int index,object value,long started,bool completed)
        {var scope=current;if(scope==null)return;if(scope.Components==null)scope.Components=new List<CanonicalComponentCost>();scope.Components.Add(new CanonicalComponentCost(index,value?.GetType().FullName??"<null>",(Stopwatch.GetTimestamp()-started)*1000.0/Stopwatch.Frequency,completed));}
    }
    public sealed partial class Prehistory
    {
        // Like WorkObserver, this opt-in callback cannot affect authoritative
        // dates, scheduling, hashes or saved data. It reports once per seal.
        public Action<CanonicalHashMetric> CanonicalHashObserver;
        // Controlled diagnostic only: retains prior immutable memo bytes during
        // one transaction to distinguish reads from genuine byte changes.
        public bool TraceCanonicalRelationReads;
        public bool TraceCanonicalComponents;
    }
}
