using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

// Offline diagnostic only. The pinned official Memory Profiler does the heap
// parsing and reachability crawl; this adapter exports its measured object sizes.
// Reflection keeps package-internal types out of the game's assembly contracts.
public static class WorldGenHeapReview
{
    public static void RunDiagnostics(){WorldGenProfilerReview.Run();Run();}
    public static void RunBufferDiagnostics(){WorldGenProfilerReview.Run();RunGraphicsResources();}
    public static void RunGraphicsResources()
    {
        var args=Environment.GetCommandLineArgs();
        string Argument(string name){int at=Array.IndexOf(args,name);if(at<0||at+1>=args.Length)throw new ArgumentException(name);return args[at+1];}
        string input=Argument("--worldgen-heap-input"),output=Argument("--worldgen-output");Directory.CreateDirectory(output);
        var assembly=Assembly.Load("Unity.MemoryProfiler.Editor");
        var readerType=assembly.GetType("Unity.MemoryProfiler.Editor.Format.QueriedSnapshot.FileReader",true);
        var snapshotType=assembly.GetType("Unity.MemoryProfiler.Editor.CachedSnapshot",true);
        object reader=Activator.CreateInstance(readerType,true),snapshot=null;
        try
        {
            var status=readerType.GetMethod("Open",Members).Invoke(reader,new object[]{input});
            if(status.ToString()!="Success")throw new InvalidOperationException("Snapshot reader: "+status);
            snapshot=Activator.CreateInstance(snapshotType,Members,null,new[]{reader,(object)true},null);
            // The package completes asynchronous native tables and initializes
            // disposal state in PostProcess; reading before that is incomplete.
            var process=(IEnumerator)snapshotType.GetMethod("PostProcess",Members).Invoke(snapshot,new object[]{true});
            try{while(process.MoveNext()){};}finally{(process as IDisposable)?.Dispose();}
            var resources=Read(snapshot,"NativeGfxResourceReferences");var roots=Read(snapshot,"NativeRootReferences");
            var ids=((IEnumerable)Read(resources,"GfxResourceId")).Cast<ulong>().ToArray();
            var sizes=((IEnumerable)Read(resources,"GfxSize")).Cast<ulong>().ToArray();
            var links=((IEnumerable)Read(resources,"RootId")).Cast<long>().ToArray();
            var rootIds=((IEnumerable)Read(roots,"Id")).Cast<long>().ToArray();
            var names=(string[])Read(roots,"ObjectName");var areas=(string[])Read(roots,"AreaName");
            var index=new Dictionary<long,int>();for(int i=0;i<rootIds.Length;i++)index[rootIds[i]]=i;
            var nativeObjects=Read(snapshot,"NativeObjects");
            var objectRoots=((IEnumerable)Read(nativeObjects,"RootReferenceId")).Cast<long>().ToArray();
            var objectNames=(string[])Read(nativeObjects,"ObjectName");
            var objectTypes=((IEnumerable)Read(nativeObjects,"NativeTypeArrayIndex")).Cast<int>().ToArray();
            var typeNames=(string[])Read(Read(snapshot,"NativeTypes"),"TypeName");
            var objectsByRoot=new Dictionary<long,int>();
            for(int i=0;i<objectRoots.Length;i++)if(objectRoots[i]>0)objectsByRoot[objectRoots[i]]=i;
            string Csv(string text)=>"\""+(text??"").Replace("\"","\"\"")+"\"";
            using var writer=new StreamWriter(Path.Combine(output,"graphics-resources.csv"));
            writer.WriteLine("resource_id,bytes,root_id,area,name,object_type,object_name");
            for(int i=0;i<ids.Length;i++)
            {
                bool found=index.TryGetValue(links[i],out int at);
                bool objectFound=objectsByRoot.TryGetValue(links[i],out int objectAt);
                writer.WriteLine(ids[i]+","+sizes[i]+","+links[i]+","+Csv(found?areas[at]:"")+","+Csv(found?names[at]:"")+","+Csv(objectFound?typeNames[objectTypes[objectAt]]:"")+","+Csv(objectFound?objectNames[objectAt]:""));
            }
            File.WriteAllText(Path.Combine(output,"complete.txt"),ids.Length+" graphics-resource identities exported from the existing snapshot.\n");
        }
        finally{(snapshot as IDisposable)?.Dispose();(reader as IDisposable)?.Dispose();}
    }
    const BindingFlags Members=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
    static object Read(object owner,string name)
    {
        var type=owner.GetType();var field=type.GetField(name,Members);
        if(field!=null)return field.GetValue(owner);
        var property=type.GetProperty(name,Members);
        if(property!=null)return property.GetValue(owner);
        throw new MissingMemberException(type.FullName,name);
    }
    public static void Run()
    {
        var args=Environment.GetCommandLineArgs();
        string Argument(string name){int at=Array.IndexOf(args,name);if(at<0||at+1>=args.Length)throw new ArgumentException(name);return args[at+1];}
        string input=Argument("--worldgen-heap-input"),output=Argument(args.Contains("--worldgen-heap-output")?"--worldgen-heap-output":"--worldgen-output");Directory.CreateDirectory(output);
        var assembly=Assembly.Load("Unity.MemoryProfiler.Editor");
        var readerType=assembly.GetType("Unity.MemoryProfiler.Editor.Format.QueriedSnapshot.FileReader",true);
        var snapshotType=assembly.GetType("Unity.MemoryProfiler.Editor.CachedSnapshot",true);
        object reader=Activator.CreateInstance(readerType,true),snapshot=null;
        try
        {
            var status=readerType.GetMethod("Open",Members).Invoke(reader,new object[]{input});
            if(status.ToString()!="Success")throw new InvalidOperationException("Snapshot reader: "+status);
            snapshot=Activator.CreateInstance(snapshotType,Members,null,new[]{reader,(object)true},null);
            var crawl=(IEnumerator)snapshotType.GetMethod("PostProcess",Members).Invoke(snapshot,new object[]{true});
            try{while(crawl.MoveNext())Debug.Log("HEAP REVIEW "+crawl.Current);}finally{(crawl as IDisposable)?.Dispose();}
            if(!(bool)Read(snapshot,"Valid"))throw new InvalidOperationException("Snapshot crawl incomplete");
            var names=(string[])Read(Read(snapshot,"TypeDescriptions"),"TypeDescriptionName");
            var data=Read(snapshot,"CrawledData");
            var objects=(IEnumerable)Read(data,"m_ManagedObjects");
            var totals=new Dictionary<string,(long Bytes,long Count,long Largest)>();
            var largest=new List<(long Bytes,string Type,ulong Address,int Refs)>();
            long objectCount=0,totalBytes=0;FieldInfo size=null,typeIndex=null,address=null,refs=null;
            foreach(var item in objects)
            {
                if(size==null){var t=item.GetType();size=t.GetField("Size",Members);typeIndex=t.GetField("ITypeDescription",Members);address=t.GetField("PtrObject",Members);refs=t.GetField("RefCount",Members);}
                long bytes=(long)size.GetValue(item);int index=(int)typeIndex.GetValue(item);ulong pointer=(ulong)address.GetValue(item);
                if(pointer==0||bytes<=0)continue;
                string name=index>=0&&index<names.Length?names[index]:"<unknown type "+index+">";
                totals.TryGetValue(name,out var total);totals[name]=(total.Bytes+bytes,total.Count+1,Math.Max(total.Largest,bytes));
                objectCount++;totalBytes+=bytes;
                if(bytes>=1024*1024)largest.Add((bytes,name,pointer,(int)refs.GetValue(item)));
            }
            string Csv(string value)=>"\""+value.Replace("\"","\"\"")+"\"";
            File.WriteAllLines(Path.Combine(output,"managed-types.csv"),new[]{"bytes,count,largest_object_bytes,type"}.Concat(totals.OrderByDescending(p=>p.Value.Bytes).Select(p=>$"{p.Value.Bytes},{p.Value.Count},{p.Value.Largest},{Csv(p.Key)}")));
            File.WriteAllLines(Path.Combine(output,"large-managed-objects.csv"),new[]{"bytes,address,ref_count,type"}.Concat(largest.OrderByDescending(p=>p.Bytes).Select(p=>$"{p.Bytes},0x{p.Address:X},{p.Refs},{Csv(p.Type)}")));
            File.WriteAllText(Path.Combine(output,"complete.txt"),$"Official Memory Profiler 1.2.0: {objectCount} managed objects, {totalBytes} object bytes. Snapshot/crawl is diagnostic only, never frame-time evidence.\n");
        }
        finally{(snapshot as IDisposable)?.Dispose();(reader as IDisposable)?.Dispose();}
    }
}
