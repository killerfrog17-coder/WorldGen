using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;

// Offline only: inspect a short diagnostic player capture after that player
// closes. These inclusive samples are never presented as release throughput.
public static class WorldGenProfilerReview
{
    public static void Run()
    {
        var args=Environment.GetCommandLineArgs();
        string Argument(string key){int at=Array.IndexOf(args,key);if(at<0||at+1>=args.Length)throw new ArgumentException(key);return args[at+1];}
        string input=Argument("--worldgen-profile-input"),output=Argument("--worldgen-output");Directory.CreateDirectory(output);
        // This disposable offline Editor must not silently keep only the final
        // 2,000 frames and lose the hitch near the start of a captured route.
        var history=typeof(ProfilerDriver).GetMethod("SetMaxFrameHistoryLength",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        if(history==null)throw new NotSupportedException("Profiler history control is unavailable");
        history.Invoke(null,new object[]{20000});
        typeof(ProfilerDriver).GetMethod("SetAutomaticMemoryManagement",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)?.Invoke(null,new object[]{false});
        if(!ProfilerDriver.LoadProfile(input,false))throw new InvalidOperationException("Cannot load profiler capture: "+input);
        int first=ProfilerDriver.firstFrameIndex,last=ProfilerDriver.lastFrameIndex;
        if(first<0||last<first)throw new InvalidOperationException("Capture contains no readable frames");
        bool inspectGpu=Array.IndexOf(args,"--worldgen-profile-gpu")>=0;
        var gpuTimeColumn=typeof(HierarchyFrameDataView).GetField("columnTotalGpuTime",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
        if(inspectGpu&&gpuTimeColumn==null)throw new NotSupportedException("GPU hierarchy column unavailable in this Editor");
        int gpuColumn=inspectGpu?(int)gpuTimeColumn.GetRawConstantValue():0,gpuThreadFrames=0;
        using var gpuOutput=inspectGpu?new StreamWriter(Path.Combine(output,"gpu-samples.csv")):null;
        gpuOutput?.WriteLine("frame,thread,frame_gpu_ms,sample_gpu_ms,path");
        var gpuChildren=new List<int>();var gpuPending=new Stack<(int Id,string Path)>();
        var allocations=new Dictionary<string,(long Bytes,int Calls)>();
        var timings=new Dictionary<string,(double Ms,int Calls)>();
        var methods=new Dictionary<ulong,string>();var addresses=new List<ulong>();
        var parents=new List<(int End,string Name)>();int threads=0;long totalBytes=0;
        var frames=new List<string>{"frame,main_thread_inclusive_ms"};
        var slowSamples=new List<string>{"frame,frame_ms,sample_ms,path"};
        var collections=new List<string>{"frame,sample_ms,thread,path"};
        var uploads=new List<string>{"frame,sample_ms,thread,marker,metadata"};
        var buffers=new List<string>{"frame,sample_ms,thread,marker,metadata"};
        for(int frame=first;frame<=last;frame++)
        for(int thread=0;thread<256;thread++)
        {
            using var data=ProfilerDriver.GetRawFrameDataView(frame,thread);if(!data.valid)break;threads++;
            bool hasGpuSamples=false;
            if(thread==0&&data.sampleCount>0)frames.Add(frame+","+data.GetSampleTimeMs(0).ToString("R",CultureInfo.InvariantCulture));
            double frameMs=data.sampleCount>0?data.GetSampleTimeMs(0):0;
            parents.Clear();
            for(int sample=0;sample<data.sampleCount;sample++)
            {
                if(inspectGpu&&(data.GetSampleFlags(sample)&Unity.Profiling.LowLevel.MarkerFlags.SampleGPU)!=0)hasGpuSamples=true;
                while(parents.Count>0&&parents[parents.Count-1].End<sample)parents.RemoveAt(parents.Count-1);
                string name=data.GetSampleName(sample)??"",timer=data.threadName+" / "+name;
                bool textureMarker=name.Contains("Texture")||name.Contains("AsyncRead");
                bool bufferMarker=name.Contains("Buffer")||name.Contains("UploadMesh");
                if((textureMarker||bufferMarker)&&data.GetSampleMetadataCount(sample)>0)
                {
                    var descriptors=data.GetMarkerMetadataInfo(data.GetSampleMarkerId(sample));var values=new List<string>();
                    for(int m=0;descriptors!=null&&m<data.GetSampleMetadataCount(sample)&&m<descriptors.Length;m++)
                    {
                        var kind=(Unity.Profiling.LowLevel.ProfilerMarkerDataType)descriptors[m].type;string value="<"+kind+">";
                        if(kind==Unity.Profiling.LowLevel.ProfilerMarkerDataType.String16)value=data.GetSampleMetadataAsString(sample,m);
                        else if((byte)kind==8)value=System.Text.Encoding.UTF8.GetString(data.GetSampleMetadataAsSpan<byte>(sample,m).ToArray()).TrimEnd('\0');
                        else if(kind==Unity.Profiling.LowLevel.ProfilerMarkerDataType.Int32||kind==Unity.Profiling.LowLevel.ProfilerMarkerDataType.UInt32)value=data.GetSampleMetadataAsInt(sample,m).ToString();
                        else if(kind==Unity.Profiling.LowLevel.ProfilerMarkerDataType.Int64||kind==Unity.Profiling.LowLevel.ProfilerMarkerDataType.UInt64)value=data.GetSampleMetadataAsLong(sample,m).ToString();
                        values.Add(descriptors[m].name+"="+value);
                    }
                    string row=frame+","+data.GetSampleTimeMs(sample).ToString("R",CultureInfo.InvariantCulture)+",\""+data.threadName.Replace("\"","\"\"")+"\",\""+name.Replace("\"","\"\"")+"\",\""+string.Join("; ",values).Replace("\"","\"\"")+"\"";
                    if(textureMarker)uploads.Add(row);if(bufferMarker)buffers.Add(row);
                }
                if(name=="GC.Collect"||name=="GC.Resize"||name=="GarbageCollector.CollectIncremental")
                {
                    string path=string.Join(" / ",parents.Select(p=>p.Name).Concat(new[]{name}));
                    collections.Add(frame+","+data.GetSampleTimeMs(sample).ToString("R",CultureInfo.InvariantCulture)+",\""+data.threadName.Replace("\"","\"\"")+"\",\""+path.Replace("\"","\"\"")+"\"");
                }
                if((thread==0||data.threadName=="Render Thread")&&frameMs>=10&&data.GetSampleTimeMs(sample)>=.25)
                {
                    string path=(thread==0?"":data.threadName+" / ")+string.Join(" / ",parents.Select(p=>p.Name).Concat(new[]{name}));
                    slowSamples.Add(frame+","+frameMs.ToString("R",CultureInfo.InvariantCulture)+","+data.GetSampleTimeMs(sample).ToString("R",CultureInfo.InvariantCulture)+",\""+path.Replace("\"","\"\"")+"\"");
                }
                timings.TryGetValue(timer,out var timing);timings[timer]=(timing.Ms+data.GetSampleTimeMs(sample),timing.Calls+1);
                if(name=="GC.Alloc")
                {
                    long bytes=data.GetSampleMetadataCount(sample)>0?data.GetSampleMetadataAsLong(sample,0):0;
                    addresses.Clear();data.GetSampleCallstack(sample,addresses);var stack=new List<string>();
                    foreach(ulong address in addresses)
                    {
                        if(!methods.TryGetValue(address,out string method))
                        {var info=data.ResolveMethodInfo(address);method=string.IsNullOrEmpty(info.methodName)?"0x"+address.ToString("X"):info.methodName+" ("+info.sourceFileName+":"+info.sourceFileLine+")";methods[address]=method;}
                        if(!string.IsNullOrWhiteSpace(method))stack.Add(method);
                    }
                    string key=data.threadName+" / "+string.Join(" / ",parents.Select(p=>p.Name))+(stack.Count>0?" / "+string.Join(" <- ",stack):"");
                    allocations.TryGetValue(key,out var allocation);allocations[key]=(allocation.Bytes+bytes,allocation.Calls+1);totalBytes+=bytes;
                }
                int descendants=data.GetSampleChildrenCountRecursive(sample);if(descendants>0)parents.Add((sample+descendants,name));
            }
            // Unity 6000.6 exposes the SampleGPU marker flag, not the newer
            // hasGpuSamples property. GPU events may resolve on any thread.
            // Read actual GPU columns, never relabel CPU submission time.
            if(inspectGpu&&hasGpuSamples)
            {
                using var hierarchy=ProfilerDriver.GetHierarchyFrameDataView(frame,thread,HierarchyFrameDataView.ViewModes.Default,gpuColumn,false);
                if(!hierarchy.valid)throw new InvalidOperationException("Recorded GPU samples have no readable hierarchy");
                gpuThreadFrames++;gpuPending.Clear();gpuPending.Push((hierarchy.GetRootItemID(),""));
                while(gpuPending.Count>0)
                {
                    var item=gpuPending.Pop();string path=item.Path+hierarchy.GetItemName(item.Id);
                    double milliseconds=hierarchy.GetItemColumnDataAsDouble(item.Id,gpuColumn);
                    if(milliseconds>0)gpuOutput.WriteLine(frame+","+Csv(data.threadName)+","+hierarchy.frameGpuTimeMs.ToString("R",CultureInfo.InvariantCulture)+","+milliseconds.ToString("R",CultureInfo.InvariantCulture)+","+Csv(path));
                    hierarchy.GetItemChildren(item.Id,gpuChildren);
                    for(int child=gpuChildren.Count-1;child>=0;child--)gpuPending.Push((gpuChildren[child],path+" / "));
                }
            }
        }
        string Csv(string text)=>"\""+text.Replace("\"","\"\"")+"\"";
        File.WriteAllLines(Path.Combine(output,"allocations.csv"),new[]{"bytes,calls,stack"}.Concat(allocations.OrderByDescending(p=>p.Value.Bytes).Select(p=>p.Value.Bytes+","+p.Value.Calls+","+Csv(p.Key))));
        File.WriteAllLines(Path.Combine(output,"inclusive-timings.csv"),new[]{"inclusive_ms,calls,thread_and_marker"}.Concat(timings.OrderByDescending(p=>p.Value.Ms).Select(p=>p.Value.Ms.ToString("R",CultureInfo.InvariantCulture)+","+p.Value.Calls+","+Csv(p.Key))));
        File.WriteAllLines(Path.Combine(output,"frames.csv"),frames);
        File.WriteAllLines(Path.Combine(output,"slow-frame-samples.csv"),slowSamples);
        File.WriteAllLines(Path.Combine(output,"collections.csv"),collections);
        File.WriteAllLines(Path.Combine(output,"texture-uploads.csv"),uploads);
        File.WriteAllLines(Path.Combine(output,"buffer-uploads.csv"),buffers);
        File.WriteAllText(Path.Combine(output,"complete.txt"),$"Diagnostic DevelopmentPlayer only. Frames {first}..{last}; thread frames {threads}; GC.Alloc bytes {totalBytes}. Inclusive timings overlap; not release FPS.\n");
        if(inspectGpu)
        {
            gpuOutput.Flush();File.WriteAllText(Path.Combine(output,"gpu-status.txt"),"GPU thread frames with actual samples: "+gpuThreadFrames+". GPU profiling adds overhead; this is not normal-player throughput.\n");
            if(gpuThreadFrames==0)throw new InvalidOperationException("No actual GPU samples were recorded; this capture cannot attribute GPU cost");
        }
    }
}
