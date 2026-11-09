using System;
using WorldGen.Presentation;

static class NpcAiSettingsTests
{
    public static void Run()
    {
        int n=0;void Check(bool value,string text){if(!value)throw new Exception(text);n++;}
        var settings=new NpcAiSettings{ServerPath="cpu",GpuServerPath="gpu",ModelPath="same-model",Threads=999,GpuLayers=999,ContextTokens=99999,MaxTokens=1,HistoryRecords=999,Temperature=float.NaN,IdleMinutes=-1,VisitorCooldownSeconds=0};
        settings.Normalize(16);Check(settings.Threads==16&&settings.GpuLayers==999&&settings.BatchThreads==16,"Hardware controls allow automatic GPU fitting and available CPU threads");
        Check(settings.ContextTokens==8192&&settings.MaxTokens==128&&settings.HistoryRecords==12,"Prompt and output capacities bounded");
        Check(settings.Temperature==.5f&&settings.IdleMinutes==0&&settings.VisitorCooldownSeconds==20,"Invalid timing/sampling values normalized");
        settings.Preset(1,16);Check(settings.GpuLayers==18&&settings.Threads==4&&settings.BriefReplies,"Hybrid preset uses actual GPU offload");
        Check(settings.ModelPath=="same-model"&&settings.ServerPath=="cpu"&&settings.GpuServerPath=="gpu","Presets never change model or installed backend paths");
        var copy=settings.Copy();copy.GpuLayers=0;Check(settings.GpuLayers==18,"Draft settings independent of saved profile");
        settings.Preset(2,2);Check(settings.Threads==2&&settings.GpuLayers==999,"GPU preset respects small CPU count");
        settings.Preset(0,16);Check(settings.GpuLayers==0&&settings.Threads==2,"CPU preset retains conservative baseline");
        settings.Threads=-5;settings.Temperature=10;settings.Normalize(0);Check(settings.Threads==1&&settings.Temperature==1.2f,"Extreme manual settings constrained");
        settings.SelectModel(new NpcAiModel{Name="Gemma",Path="gemma.gguf",Family="gemma4",Layers=49});settings.GpuLayers=49;settings.Normalize(16);
        Check(settings.GpuLayers==49&&settings.ModelLayers==49,"Larger models are not limited to the original 37 layers");
        settings.Preset(3,16);Check(settings.Threads==8&&settings.BatchThreads==16&&settings.Priority==2&&settings.GpuReserveMiB==0&&settings.Polling==100,"Maximum-speed preset gives AI CPU and VRAM priority");
        Check(settings.HardwareArguments().Contains("-ngl auto --fit on --fit-target 0")&&settings.HardwareArguments().Contains("-tb 16"),"Automatic fit and independent prefill threads reach the host");
        settings.SelectModel(new NpcAiModel{Name="GPT",Path="gpt.gguf",Family="gpt-oss",Layers=25,ExpertLayers=24});
        Check(settings.CpuExpertLayers==24&&settings.HardwareArguments().Contains("--n-cpu-moe 24")&&settings.HardwareArguments().Contains("--reasoning-effort low"),"Expert model receives model-specific memory and reasoning configuration");
        settings.SelectModel(new NpcAiModel{Name="Qwen",Path="qwen.gguf",Family="qwen35",Layers=33});
        Check(settings.CpuExpertLayers==0&&!settings.HardwareArguments().Contains("--n-cpu-moe")&&!settings.HardwareArguments().Contains("--reasoning-effort"),"Switching to a dense model clears expert-only configuration");
        settings.Preset(2,16);Check(settings.Polling==0&&settings.Priority==-1&&settings.GpuReserveMiB==1024,"Ordinary GPU preset reverses maximum-resource settings");
        Console.WriteLine("AI settings checks PASS: "+n);
    }
}
