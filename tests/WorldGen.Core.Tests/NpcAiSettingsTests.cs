using System;
using WorldGen.Presentation;

static class NpcAiSettingsTests
{
    public static void Run()
    {
        int n=0;void Check(bool value,string text){if(!value)throw new Exception(text);n++;}
        var settings=new NpcAiSettings{ServerPath="cpu",GpuServerPath="gpu",ModelPath="same-model",Threads=999,GpuLayers=999,ContextTokens=99999,MaxTokens=1,HistoryRecords=999,Temperature=float.NaN,IdleMinutes=-1,VisitorCooldownSeconds=0};
        settings.Normalize(16);Check(settings.Threads==16&&settings.GpuLayers==37,"Hardware controls bounded to available threads and current model");
        Check(settings.ContextTokens==8192&&settings.MaxTokens==128&&settings.HistoryRecords==12,"Prompt and output capacities bounded");
        Check(settings.Temperature==.5f&&settings.IdleMinutes==0&&settings.VisitorCooldownSeconds==20,"Invalid timing/sampling values normalized");
        settings.Preset(1,16);Check(settings.GpuLayers==18&&settings.Threads==4&&settings.BriefReplies,"Hybrid preset uses actual GPU offload");
        Check(settings.ModelPath=="same-model"&&settings.ServerPath=="cpu"&&settings.GpuServerPath=="gpu","Presets never change model or installed backend paths");
        var copy=settings.Copy();copy.GpuLayers=0;Check(settings.GpuLayers==18,"Draft settings independent of saved profile");
        settings.Preset(2,2);Check(settings.Threads==2&&settings.GpuLayers==37,"GPU preset respects small CPU count");
        settings.Preset(0,16);Check(settings.GpuLayers==0&&settings.Threads==2,"CPU preset retains conservative baseline");
        settings.Threads=-5;settings.Temperature=10;settings.Normalize(0);Check(settings.Threads==1&&settings.Temperature==1.2f,"Extreme manual settings constrained");
        Console.WriteLine("AI settings checks PASS: "+n);
    }
}
