using System;

namespace WorldGen.Presentation
{
    [Serializable] public sealed class NpcAiSettings
    {
        public string ServerPath="",GpuServerPath="",ModelPath="",ModelName="local",BaseUrl="",ApiKeyEnvironment="";
        public int Threads=2,ContextTokens=2048,MaxTokens=160,GpuLayers,HistoryRecords=2,Priority=-1,IdleMinutes=10,VisitorCooldownSeconds=20;
        public bool Enabled=true,BriefReplies,BackgroundReactions=true;
        public float Temperature=.5f;
        public NpcAiSettings Copy()=>(NpcAiSettings)MemberwiseClone();
        public void Normalize(int processors)
        {
            Threads=Math.Max(1,Math.Min(Math.Max(1,processors),Threads));GpuLayers=Math.Max(0,Math.Min(37,GpuLayers));
            ContextTokens=ContextTokens<=2048?2048:ContextTokens<=4096?4096:8192;
            MaxTokens=Math.Max(128,Math.Min(384,MaxTokens));HistoryRecords=Math.Max(0,Math.Min(12,HistoryRecords));
            Priority=Priority>=0?0:-1;IdleMinutes=Math.Max(0,Math.Min(30,IdleMinutes));VisitorCooldownSeconds=Math.Max(20,Math.Min(300,VisitorCooldownSeconds));
            Temperature=float.IsNaN(Temperature)||float.IsInfinity(Temperature)?.5f:Math.Max(0,Math.Min(1.2f,Temperature));
        }
        public void Preset(int choice,int processors)
        {
            Threads=choice==0?2:4;GpuLayers=choice==0?0:choice==1?18:37;
            Priority=-1;BriefReplies=choice!=0;ContextTokens=2048;MaxTokens=160;HistoryRecords=2;Normalize(processors);
        }
    }
}
