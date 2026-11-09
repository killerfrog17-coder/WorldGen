using System;

namespace WorldGen.Presentation
{
    [Serializable] public sealed class NpcAiModel
    {
        public string Name="",Path="",Family="";
        public int Layers=37,ExpertLayers;
        public long Bytes;
    }
    [Serializable] public sealed class NpcAiCatalog {public NpcAiModel[] Models=Array.Empty<NpcAiModel>();}
    [Serializable] public sealed class NpcAiSettings
    {
        public string ServerPath="",GpuServerPath="",ModelPath="",ModelName="local",BaseUrl="",ApiKeyEnvironment="";
        public string ModelFamily="";
        public string CudaServerPath="",GpuBackend="vulkan";
        public int Threads=2,ContextTokens=2048,MaxTokens=160,GpuLayers,HistoryRecords=2,Priority=-1,IdleMinutes=10,VisitorCooldownSeconds=20;
        public int BatchThreads,ModelLayers=37,ModelExpertLayers,CpuExpertLayers,GpuReserveMiB=1024,Polling;
        public bool Enabled=true,BriefReplies,BackgroundReactions=true;
        public float Temperature=.5f;
        public NpcAiSettings Copy()=>(NpcAiSettings)MemberwiseClone();
        public string ServerExecutable=>GpuLayers==0?ServerPath:GpuBackend=="cuda"?CudaServerPath:GpuServerPath;
        public void Normalize(int processors)
        {
            processors=Math.Max(1,processors);Threads=Math.Max(1,Math.Min(processors,Threads));
            GpuBackend=GpuBackend=="cuda"?"cuda":"vulkan";
            BatchThreads=BatchThreads<=0?Threads:Math.Max(1,Math.Min(processors,BatchThreads));
            ModelLayers=Math.Max(1,Math.Min(256,ModelLayers));ModelExpertLayers=Math.Max(0,Math.Min(ModelLayers,ModelExpertLayers));
            GpuLayers=GpuLayers>=999?999:Math.Max(0,Math.Min(ModelLayers,GpuLayers));
            CpuExpertLayers=Math.Max(0,Math.Min(ModelExpertLayers,CpuExpertLayers));
            GpuReserveMiB=Math.Max(0,Math.Min(2048,GpuReserveMiB));Polling=Math.Max(0,Math.Min(100,Polling));
            ContextTokens=ContextTokens<=2048?2048:ContextTokens<=4096?4096:8192;
            MaxTokens=Math.Max(128,Math.Min(384,MaxTokens));HistoryRecords=Math.Max(0,Math.Min(12,HistoryRecords));
            Priority=Math.Max(-1,Math.Min(2,Priority));IdleMinutes=Math.Max(0,Math.Min(30,IdleMinutes));VisitorCooldownSeconds=Math.Max(20,Math.Min(300,VisitorCooldownSeconds));
            Temperature=float.IsNaN(Temperature)||float.IsInfinity(Temperature)?.5f:Math.Max(0,Math.Min(1.2f,Temperature));
        }
        public void Preset(int choice,int processors)
        {
            bool maximum=choice==3;
            Threads=maximum?Math.Max(1,processors/2):choice==0?2:4;BatchThreads=maximum?processors:Threads;
            GpuLayers=choice==0?0:choice==1?Math.Min(18,ModelLayers):999;
            Priority=maximum?2:-1;Polling=maximum?100:0;GpuReserveMiB=maximum?0:1024;
            if(maximum&&!string.IsNullOrEmpty(CudaServerPath))GpuBackend="cuda";
            BriefReplies=choice!=0;ContextTokens=2048;MaxTokens=160;HistoryRecords=2;Normalize(processors);
        }
        public void SelectModel(NpcAiModel model)
        {
            if(model==null)throw new ArgumentNullException(nameof(model));
            ModelPath=model.Path;ModelName=model.Name;ModelFamily=model.Family;ModelLayers=model.Layers;ModelExpertLayers=model.ExpertLayers;
            if(GpuLayers>0)GpuLayers=999;
            CpuExpertLayers=model.ExpertLayers;
            Normalize(Environment.ProcessorCount);
        }
        public string HardwareArguments()=>" -t "+Threads+" -tb "+BatchThreads+" -c "+ContextTokens+" -np 1 -ngl "+(GpuLayers==999?"auto":GpuLayers.ToString())+
            " --fit on --fit-target "+GpuReserveMiB+" --poll "+Polling+" --poll-batch "+(Polling>0?1:0)+" --prio "+Priority+" --prio-batch "+Math.Max(0,Priority)+
            " --flash-attn auto"+(GpuLayers>0&&CpuExpertLayers>0?" --n-cpu-moe "+CpuExpertLayers:"")+" --no-webui --verbosity 5 --reasoning off --reasoning-budget 0"+
            (ModelFamily=="gpt-oss"?" --reasoning-effort low":"");
        }
}
