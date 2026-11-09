using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using WorldGen.Presentation;
using WorldGen.Core;

// A single lazy process for the active explorer. No inference in Update(), no
// background inference in Update(), and no weights in either build slot.
sealed class NpcModelClient : IDisposable
{
    [Serializable] sealed class Delta {public string content;}
    [Serializable] sealed class Choice {public NpcChatMessage message;public Delta delta;public string finish_reason;}
    [Serializable] sealed class Usage {public int prompt_tokens,completion_tokens;}
    [Serializable] sealed class Timings {public double predicted_per_second;}
    [Serializable] sealed class Response {public Choice[] choices;public Usage usage;public Timings timings;}
    [Serializable] sealed class Template {public bool enable_thinking=false;}
    [Serializable] sealed class StreamOptions {public bool include_usage=true;}
    [Serializable] sealed class Request {public NpcChatMessage[] messages;public string model;public int max_tokens;public float temperature=.5f;public bool stream=true;public StreamOptions stream_options=new StreamOptions();public Template chat_template_kwargs=new Template();}
    [Serializable] sealed class AppliedTemplate {public string prompt;}
    [Serializable] sealed class Tokenized {public int[] tokens;}
    [Serializable] sealed class TokenizeRequest {public string content;public bool add_special=false,parse_special=true;}
    readonly CancellationTokenSource lifetime=new CancellationTokenSource();
    readonly HttpClient http=new HttpClient{Timeout=TimeSpan.FromSeconds(180)};
    readonly NpcOwnedJob job=new NpcOwnedJob();
    public int OwnedProcessId=>owned?.Id??0;
    Process owned;string endpoint,key,hostError="";bool disposed;
    public int LastPromptTokens,LastGeneratedTokens;
    public NpcAiSettings Config;public string Status="Waiting for an event";public int Requests;public double LastSeconds;
    public double LastLoadSeconds,LastTokensPerSecond;
    public double LastFirstSpeechSeconds;
    internal NpcChatMessage[] LastMessages;
    volatile string streamedSpeech="";CancellationTokenSource requestCancellation;
    public string StreamedSpeech=>streamedSpeech;
    public void CancelCurrent()=>requestCancellation?.Cancel();
    internal static string TestSettingsPath;
    public string BackendInfo="CPU / not loaded";
    public static string ConfigPath=>TestSettingsPath??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WorldGen","NpcAI","settings.json");
    public static string CatalogPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WorldGen","NpcAI","models.json");
    public static NpcAiModel[] InstalledModels(NpcAiSettings current)
    {
        var result=new List<NpcAiModel>();
        void Add(NpcAiModel model){if(model!=null&&File.Exists(model.Path)&&!result.Exists(m=>string.Equals(m.Path,model.Path,StringComparison.OrdinalIgnoreCase)))result.Add(model);}
        string baseline=Path.Combine(Path.GetDirectoryName(CatalogPath),"Qwen3-4B-Instruct-2507-Q4_K_M.gguf");
        Add(new NpcAiModel{Name="Qwen3-4B-Instruct-2507",Path=baseline,Family="qwen3",Layers=37});
        try{if(File.Exists(CatalogPath)){var catalog=JsonUtility.FromJson<NpcAiCatalog>(File.ReadAllText(CatalogPath));if(catalog?.Models!=null)foreach(var model in catalog.Models)Add(model);}}catch(Exception ex){UnityEngine.Debug.LogWarning("NPC model catalog: "+ex.Message);}
        Add(new NpcAiModel{Name=current.ModelName,Path=current.ModelPath,Family=current.ModelFamily,Layers=current.ModelLayers,ExpertLayers=current.ModelExpertLayers});
        return result.ToArray();
    }
    public static int SettingsRevision;
    public bool IsBusy {get;private set;}
    long lastUse=Stopwatch.GetTimestamp();
    public NpcModelClient(NpcAiSettings settings=null){Config=settings?.Copy()??LoadSettings();Config.Normalize(Environment.ProcessorCount);}
    public static NpcAiSettings LoadSettings(string path=null)
    {
        try
        {
            var config=new NpcAiSettings();if(File.Exists(path??ConfigPath))JsonUtility.FromJsonOverwrite(File.ReadAllText(path??ConfigPath),config);else config.Enabled=false;
            string cuda=Path.Combine(Path.GetDirectoryName(CatalogPath),"llama-b12211-cuda","llama-server.exe");if(string.IsNullOrEmpty(config.CudaServerPath)&&File.Exists(cuda))config.CudaServerPath=cuda;
            config.Normalize(Environment.ProcessorCount);return config;
        }
        catch{return new NpcAiSettings{Enabled=false};}
    }
    public static void SaveSettings(NpcAiSettings config,string path=null)
    {
        config.Normalize(Environment.ProcessorCount);path??=ConfigPath;Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";File.WriteAllText(temp,JsonUtility.ToJson(config,true));
        try{if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}
        SettingsRevision++;
    }
    public async Task WarmUp()
    {
        if(IsBusy)throw new InvalidOperationException("Wait for the current AI request.");
        if(!Config.Enabled)throw new InvalidOperationException("NPC AI is disabled.");
        IsBusy=true;try{await Start();Status="Ready";}catch(Exception ex){ReleaseHost();Status="Error: "+ex.Message;throw;}finally{IsBusy=false;lastUse=Stopwatch.GetTimestamp();}
    }
    public void ReleaseIfIdle()
    {
        if(!IsBusy&&owned!=null&&Config.IdleMinutes>0&&(Stopwatch.GetTimestamp()-lastUse)/(double)Stopwatch.Frequency>Config.IdleMinutes*60){ReleaseHost();Status="Unloaded after inactivity";}
    }
    void ReleaseHost(){job.Dispose();try{if(owned!=null&&!owned.HasExited)owned.Kill();}catch{}owned?.Dispose();owned=null;endpoint=null;key=null;BackendInfo="Not loaded";}
    public async Task<string> Ask(string context,string targets,NpcDialogueTurn[] history=null,NpcDialogueTurn current=null,NpcHeardStatement[] statements=null)
    {
        if(disposed||!Config.Enabled)throw new InvalidOperationException("NPC model is disabled or missing. Run scripts/setup-npc-ai.py, then /npcai on.");
        if(IsBusy)throw new InvalidOperationException("An AI request is already running.");
        IsBusy=true;streamedSpeech="";LastFirstSpeechSeconds=LastSeconds=LastTokensPerSecond=0;LastGeneratedTokens=LastPromptTokens=0;requestCancellation=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);var watch=Stopwatch.StartNew();try{await Start();requestCancellation.Token.ThrowIfCancellationRequested();LastLoadSeconds=watch.Elapsed.TotalSeconds;Status="Thinking";Requests++;
        bool conversation=history!=null||current!=null||statements!=null;
        var request=new Request{model=Config.ModelName,max_tokens=Config.MaxTokens,temperature=Config.Temperature};
        int historyLimit=history?.Length??0,statementLimit=8;
        request.messages=conversation?NpcConversationPrompt.Build(context,current,history,statements,Config.BriefReplies,Config.ContextTokens,Config.MaxTokens):new[]{
            new NpcChatMessage{role="system",content=SystemPrompt+(Config.BriefReplies?" Use one or two concise sentences, without padding.":"")},new NpcChatMessage{role="user",content=context}};
        if(conversation&&string.IsNullOrWhiteSpace(Config.BaseUrl))
        {
            while(await CountPromptTokens(request)+Config.MaxTokens+32>Config.ContextTokens)
            {
                if(historyLimit>0)historyLimit=Math.Max(0,historyLimit-2);
                else if(statementLimit>0)statementLimit/=2;
                else throw new InvalidOperationException("NPC facts exceed the context window. Select a larger context window in AI settings.");
                request.messages=NpcConversationPrompt.Build(context,current,history,statements,Config.BriefReplies,Config.ContextTokens,Config.MaxTokens,historyLimit,statementLimit);
            }
        }
        LastMessages=request.messages;
        string fields="{\"type\":\"object\",\"properties\":{\"speech\":{\"type\":\"string\",\"maxLength\":"+(Config.BriefReplies?220:280)+"},\"thought\":{\"type\":\"string\",\"maxLength\":"+(Config.BriefReplies?70:120)+"},\"action\":{\"type\":\"string\",\"enum\":ACTIONS},\"target\":{\"type\":\"string\",\"enum\":TARGETS},\"seconds\":{\"type\":\"integer\",\"minimum\":2,\"maximum\":30}},\"required\":[\"speech\",\"thought\",\"action\",\"target\",\"seconds\"],\"additionalProperties\":false}";
        string directed=targets.Replace("\"none\",","").Replace(",\"none\"","").Replace("\"none\"","");
        string moving=fields.Replace("ACTIONS","[\"speak\",\"approach\",\"flee\"]").Replace("TARGETS",directed);
        string resting=fields.Replace("ACTIONS","[\"wait\",\"rest\",\"resume\"]").Replace("TARGETS",targets);
        string schema="{\"type\":\"json_schema\",\"json_schema\":{\"name\":\"decision\",\"strict\":true,\"schema\":{\"anyOf\":["+moving+","+resting+"]}}}";
        string json=JsonUtility.ToJson(request);json=json.Substring(0,json.Length-1)+",\"response_format\":"+schema+"}";
        using(var message=new HttpRequestMessage(HttpMethod.Post,endpoint+"/v1/chat/completions"))
        {
            if(!string.IsNullOrEmpty(key))message.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",key);
            message.Content=new StringContent(json,Encoding.UTF8,"application/json");
            using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(requestCancellation.Token))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(180));
            using(var response=await http.SendAsync(message,HttpCompletionOption.ResponseHeadersRead,timeout.Token))
            {
                if(!response.IsSuccessStatusCode)throw new InvalidOperationException("NPC model HTTP "+(int)response.StatusCode);
                var decoder=new NpcSpeechStream();bool finished=false,done=false;Usage usage=null;Timings timing=null;double firstToken=0;
                using(var stream=await response.Content.ReadAsStreamAsync())
                using(var cancel=timeout.Token.Register(()=>stream.Dispose()))
                using(var reader=new StreamReader(stream,Encoding.UTF8))
                {
                    string line;while((line=await reader.ReadLineAsync())!=null)
                    {
                        timeout.Token.ThrowIfCancellationRequested();if(!line.StartsWith("data:",StringComparison.Ordinal))continue;
                        string payload=line.Substring(5).Trim();if(payload=="[DONE]"){done=true;break;}if(payload.Length==0)continue;
                        if(payload.Length>65536)throw new InvalidOperationException("NPC stream event too large");
                        var part=JsonUtility.FromJson<Response>(payload);if(part?.usage!=null)usage=part.usage;if(part?.timings!=null)timing=part.timings;
                        if(part?.choices==null)continue;
                        foreach(var choice in part.choices){if(choice.finish_reason=="length")throw new InvalidOperationException("NPC response incomplete");if(choice.finish_reason=="stop")finished=true;if(firstToken==0&&!string.IsNullOrEmpty(choice.delta?.content))firstToken=watch.Elapsed.TotalSeconds;decoder.Append(choice.delta?.content);streamedSpeech=decoder.Speech;if(LastFirstSpeechSeconds==0&&streamedSpeech.Length>0)LastFirstSpeechSeconds=watch.Elapsed.TotalSeconds;}
                    }
                }
                if(!done||!finished||decoder.Json.Length==0)throw new InvalidOperationException("NPC stream ended before a complete reply");
                LastSeconds=watch.Elapsed.TotalSeconds;LastPromptTokens=usage?.prompt_tokens??0;LastGeneratedTokens=usage?.completion_tokens??0;LastTokensPerSecond=timing?.predicted_per_second??LastGeneratedTokens/Math.Max(.01,LastSeconds-firstToken);Status="Ready";return decoder.Json;
            }
            }
        }
            }catch(Exception ex){if(endpoint==null)ReleaseHost();Status="Error: "+ex.Message;throw;}finally{requestCancellation?.Dispose();requestCancellation=null;IsBusy=false;lastUse=Stopwatch.GetTimestamp();}
    }
    const string SystemPrompt=NpcConversationPrompt.SystemPrompt;
    async Task<string> LocalJson(string route,string json)
    {
        using(var message=new HttpRequestMessage(HttpMethod.Post,endpoint+route))
        {
            message.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",key);
            message.Content=new StringContent(json,Encoding.UTF8,"application/json");
            using(var response=await http.SendAsync(message,lifetime.Token))
            {
                if(!response.IsSuccessStatusCode)throw new InvalidOperationException("NPC prompt preparation HTTP "+(int)response.StatusCode);
                return await response.Content.ReadAsStringAsync();
            }
        }
    }
    async Task<int> CountPromptTokens(Request request)
    {
        var applied=JsonUtility.FromJson<AppliedTemplate>(await LocalJson("/apply-template",JsonUtility.ToJson(request)));
        var tokenized=JsonUtility.FromJson<Tokenized>(await LocalJson("/tokenize",JsonUtility.ToJson(new TokenizeRequest{content=applied.prompt})));
        return tokenized.tokens.Length;
    }
    async Task Start()
    {
        if(owned!=null&&owned.HasExited)ReleaseHost();
        if(endpoint!=null)return;
        if(!string.IsNullOrWhiteSpace(Config.BaseUrl))
        {endpoint=Config.BaseUrl.TrimEnd('/');if(endpoint.EndsWith("/v1"))endpoint=endpoint.Substring(0,endpoint.Length-3);key=string.IsNullOrWhiteSpace(Config.ApiKeyEnvironment)?null:Environment.GetEnvironmentVariable(Config.ApiKeyEnvironment);return;}
        string executable=Config.ServerExecutable;
        if(!File.Exists(executable)||!File.Exists(Config.ModelPath))throw new FileNotFoundException("Local NPC model is not installed; run scripts/setup-npc-ai.py.");
        Status="Loading local model";int port;var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();key=Guid.NewGuid().ToString("N");
        string root=Path.GetDirectoryName(ConfigPath);string args="-m \""+Config.ModelPath.Replace("\"","")+"\" --host 127.0.0.1 --port "+port+" --api-key "+key+Config.HardwareArguments();hostError="";
        BackendInfo=Config.GpuLayers>0?Config.GpuBackend+" requested; loading":"CPU";
        owned=new Process{StartInfo=new ProcessStartInfo(executable,args){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=root,RedirectStandardOutput=true,RedirectStandardError=true}};
        owned.OutputDataReceived+=(s,e)=>{};owned.ErrorDataReceived+=(s,e)=>{if(e.Data==null)return;if(e.Data.Contains("offloaded "))BackendInfo=Config.GpuBackend+": "+e.Data.Substring(e.Data.IndexOf("offloaded "))+(Config.CpuExpertLayers>0?"; "+Config.CpuExpertLayers+" expert layers on CPU":"");if(e.Data.IndexOf("error",StringComparison.OrdinalIgnoreCase)>=0||e.Data.IndexOf("failed",StringComparison.OrdinalIgnoreCase)>=0)hostError=e.Data.Length>400?e.Data.Substring(0,400):e.Data;};owned.Start();job.Attach(owned);owned.BeginOutputReadLine();owned.BeginErrorReadLine();
        string url="http://127.0.0.1:"+port;
        for(int i=0;i<720;i++)
        {
            lifetime.Token.ThrowIfCancellationRequested();if(owned.HasExited)throw new InvalidOperationException("Local NPC host exited ("+owned.ExitCode+"). "+hostError);
            try{using(var result=await http.GetAsync(url+"/health",lifetime.Token)){if(result.IsSuccessStatusCode){endpoint=url;return;}}}catch(HttpRequestException){}
            await Task.Delay(250,lifetime.Token);
        }
        throw new TimeoutException("Local NPC model did not become ready");
    }
    public void Dispose()
    {
        if(disposed)return;disposed=true;lifetime.Cancel();http.Dispose();ReleaseHost();lifetime.Dispose();
    }
}
