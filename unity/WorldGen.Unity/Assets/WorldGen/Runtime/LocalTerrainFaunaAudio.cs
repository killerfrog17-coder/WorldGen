using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    // A bounded set of sound sources, NOT simulated/visible animal entities.
    // The off-thread habitat pass owns its own terrain patch. No per-frame
    // emitter ground queries or dependence on visible vegetation/terrain LOD.
    sealed class FaunaSite
    {
        public FaunaSoundAnchor Anchor;public FaunaVoiceProfile Profile;
        public V Position;public double Ground,Height,Habitat;
        public float Target;public bool Occluded;
    }
    sealed class FaunaVoice
    {
        public GameObject Root;public readonly AudioSource[] Sources=new AudioSource[2];
        public AudioLowPassFilter[] Filters=new AudioLowPassFilter[2];
        public readonly double[] Start=new double[2],End=new double[2];
        public FaunaSite Site;public float Gain;public int Method,Sequence,LastClip=-1,NextBank;
        public double Next;public float Pitch=1;
    }
    sealed class FaunaFile {public int Group,Method;public string Path,Name;}
    readonly List<AudioClip>[,] faunaBanks=new List<AudioClip>[5,3];
    readonly Dictionary<ulong,FaunaSite> faunaSites=new Dictionary<ulong,FaunaSite>();
    readonly List<ulong> faunaEvict=new List<ulong>();readonly List<FaunaSite> faunaRanked=new List<FaunaSite>();
    readonly FaunaVoice[] faunaVoices=new FaunaVoice[12];
    readonly CancellationTokenSource faunaCancel=new CancellationTokenSource();
    Task<List<FaunaSite>> faunaPlan;V faunaPlanCenter;bool faunaPlanned,faunaDisposed,faunaWasPaused,faunaLoaded;
    float nextFaunaMix,nextFaunaPlan;int faunaClipCount,faunaRejected,faunaPreviewSequence;
    Coroutine faunaLoading,faunaPreviewFade;UnityWebRequest faunaRequest;AudioSource faunaPreview;
    public int FaunaClipCount(int group,int method)=>faunaBanks[group,method]?.Count??0;
    public string FaunaAudioDebug
    {
        get
        {
            int count=0;foreach(var v in faunaVoices)if(v!=null&&v.Site!=null&&v.Gain>.001f)count++;
            return "Fauna audio · "+count+" / 12 sources · "+faunaClipCount+" / 45 clips · "+(faunaLoaded?"ready":"loading")+
                (faunaPlan!=null?" · habitat pending":"")+(faunaRejected>0?" · "+faunaRejected+" files skipped":"");
        }
    }
    void InitializeFaunaAudio()
    {
        FaunaAudioOptions.Current.Load();for(int g=0;g<5;g++)for(int m=0;m<3;m++)faunaBanks[g,m]=new List<AudioClip>(3);
        for(int i=0;i<faunaVoices.Length;i++)
        {
            var v=faunaVoices[i]=new FaunaVoice{Root=new GameObject("Habitat caller "+i)};v.Root.transform.SetParent(transform,false);
            for(int b=0;b<2;b++)
            {
                var child=new GameObject("Overlap "+b);child.transform.SetParent(v.Root.transform,false);
                var s=v.Sources[b]=child.AddComponent<AudioSource>();s.playOnAwake=false;s.loop=false;s.spatialBlend=1;
                s.dopplerLevel=0;s.rolloffMode=AudioRolloffMode.Linear;s.minDistance=4;s.maxDistance=150;s.volume=0;s.priority=160;
                v.Filters[b]=child.AddComponent<AudioLowPassFilter>();v.Filters[b].cutoffFrequency=14000;
            }
        }
        faunaPreview=view.gameObject.AddComponent<AudioSource>();faunaPreview.playOnAwake=false;faunaPreview.spatialBlend=0;faunaPreview.loop=false;faunaPreview.priority=80;
        faunaLoading=StartCoroutine(LoadFaunaAudio());
    }
    static List<FaunaFile> ScreenFaunaFiles(string root)
    {
        var result=new List<FaunaFile>();string path=Path.Combine(root,"library.tsv");if(!File.Exists(path))return result;
        string[] groups={"cricket","cicada","katydid","frog","bird"},methods={"ai","code","recorded"};
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var line in File.ReadAllLines(path))
        {
            if(result.Count>=45)break;var f=line.Split('\t');if(f.Length!=5)continue;
            int g=Array.IndexOf(groups,f[0]),m=Array.IndexOf(methods,f[1]);if(g<0||m<0)continue;
            string name=f[2];if(name!=Path.GetFileName(name)||name.IndexOfAny(new[]{'/','\\',':'})>=0||!name.EndsWith(".wav",StringComparison.OrdinalIgnoreCase)||!seen.Add(name))continue;
            string full=Path.Combine(root,name);var info=new FileInfo(full);if(!info.Exists||info.Length<100||info.Length>5*1024*1024)continue;
            string hash;using(var sha=SHA256.Create())using(var stream=File.OpenRead(full))hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
            if(hash!=f[3])continue;result.Add(new FaunaFile{Group=g,Method=m,Name=name,Path=full});
        }
        return result;
    }
    IEnumerator LoadFaunaAudio()
    {
        string root=Path.Combine(WeatherSoundsRoot(),"fauna");var scan=Task.Run(()=>ScreenFaunaFiles(root));
        while(!scan.IsCompleted)yield return null;
        if(scan.IsFaulted){Debug.LogWarning("Fauna audio manifest could not be read: "+scan.Exception.GetBaseException().Message);faunaLoaded=true;faunaLoading=null;yield break;}
        faunaRejected=45-scan.Result.Count;
        foreach(var entry in scan.Result)
        {
            faunaRequest=UnityWebRequestMultimedia.GetAudioClip(new Uri(entry.Path).AbsoluteUri,AudioType.WAV);
            ((DownloadHandlerAudioClip)faunaRequest.downloadHandler).streamAudio=false;yield return faunaRequest.SendWebRequest();
            if(faunaDisposed)yield break;
            if(faunaRequest.result==UnityWebRequest.Result.Success)
            {
                var clip=DownloadHandlerAudioClip.GetContent(faunaRequest);
                if(clip!=null&&clip.channels==1&&clip.length>=.3f&&clip.length<=45){clip.name=entry.Name;faunaBanks[entry.Group,entry.Method].Add(clip);faunaClipCount++;}
                else{if(clip!=null)Destroy(clip);faunaRejected++;}
            }
            else faunaRejected++;
            faunaRequest.Dispose();faunaRequest=null;
        }
        faunaLoaded=true;faunaLoading=null;
    }
    List<FaunaSite> BuildFaunaSites(V center,double radius,double scale,HashSet<ulong> known,CancellationToken token)
    {
        var anchors=new List<FaunaSoundAnchor>();FaunaAmbience.Anchors(sourceWorld.Recipe.Seed,center,radius,175,anchors);
        var result=new List<FaunaSite>();var patch=terrain.Patch(center,512/scale);var ecology=sourceWorld.Ecology;var model=FaunaAmbience.For(sourceWorld);
        foreach(var anchor in anchors)
        {
            token.ThrowIfCancellationRequested();if(known.Contains(anchor.Id))continue;
            var site=new FaunaSite{Anchor=anchor,Position=anchor.Position};result.Add(site);
            int cell=terrain.NearestCell(anchor.Position);if(!model.TryProfile(cell,anchor.Kind,out var p))continue;
            var sample=patch.Sample(anchor.Position);cell=sample.Cell;if(!model.TryProfile(cell,anchor.Kind,out p))continue;
            double gain=FaunaAmbience.HabitatGain(p,ecology.Habitat[cell],sample);if(gain<.075)continue;
            site.Ground=sample.ElevationM;site.Height=anchor.Kind==FaunaSoundKind.Cricket?.12:anchor.Kind==FaunaSoundKind.Frog?.15:.8;
            if(anchor.Kind==FaunaSoundKind.Cicada||anchor.Kind==FaunaSoundKind.UnderstoryBird)
            {
                if(scatter==null||!scatter.TryFaunaTree(anchor.Position,patch,out var tree))continue;
                // Same actual planting predicate as rendered trees, including
                // when graphics toggles/LODs hide them. Never perch in bare air.
                int treeCell=terrain.NearestCell(tree.Position);if(!model.TryProfile(treeCell,anchor.Kind,out p))continue;
                var treeSample=patch.Sample(tree.Position);gain=FaunaAmbience.HabitatGain(p,ecology.Habitat[treeSample.Cell],treeSample);if(gain<.075)continue;
                site.Position=tree.Position;site.Ground=tree.GroundM;
                site.Height=Math.Max(.5,Math.Min(tree.HeightM*.65,anchor.Kind==FaunaSoundKind.Cicada?18:7));
            }
            else if(anchor.Kind==FaunaSoundKind.Katydid)site.Height=.3+(anchor.Hash%1000)*.001;
            site.Profile=p;site.Habitat=gain;
        }
        return result;
    }
    Vector3 FaunaPoint(FaunaSite s)
    {
        double d=V.Dot(s.Position,frame.Origin);if(d<=0)return new Vector3(0,-100000,0);
        return new Vector3((float)(V.Dot(s.Position,frame.East)*frame.RadiusM/d-originX),
            (float)(frame.SurfaceY(s.Position,s.Ground)+s.Height),(float)(V.Dot(s.Position,frame.North)*frame.RadiusM/d-originZ));
    }
    void UpdateFaunaAudio(bool audioPaused)
    {
        if(faunaDisposed||faunaVoices[0]==null||sourceWorld?.Ecology==null)return;
        var o=FaunaAudioOptions.Current;float now=Time.unscaledTime;
        if(!paused&&faunaPreview!=null&&(faunaPreview.isPlaying||faunaPreviewFade!=null))StopFaunaPreview();
        if(faunaPreview!=null&&faunaPreview.isPlaying&&faunaPreviewFade==null)faunaPreview.volume=o.Volume;
        if(audioPaused)
        {
            if(!faunaWasPaused)foreach(var v in faunaVoices)StopFaunaVoice(v);
            faunaWasPaused=true;return;
        }
        faunaWasPaused=false;
        if(faunaPlan!=null&&faunaPlan.IsCompleted)
        {
            if(faunaPlan.Status==TaskStatus.RanToCompletion)foreach(var s in faunaPlan.Result)faunaSites[s.Anchor.Id]=s;
            else if(faunaPlan.IsFaulted)Debug.LogWarning("Fauna habitat pass failed: "+faunaPlan.Exception.GetBaseException().Message);
            faunaPlan=null;nextFaunaMix=0;
        }
        V center=Position;
        if(o.Enabled&&!mapOpen&&!ecologyOpen&&faunaPlan==null&&now>=nextFaunaPlan&&(!faunaPlanned||(center-faunaPlanCenter).Length*frame.RadiusM>24))
        {
            faunaEvict.Clear();foreach(var pair in faunaSites)if((pair.Value.Position-center).Length*frame.RadiusM>230)faunaEvict.Add(pair.Key);
            foreach(var id in faunaEvict)faunaSites.Remove(id);
            var known=new HashSet<ulong>(faunaSites.Keys);faunaPlanCenter=center;faunaPlanned=true;nextFaunaPlan=now+1;
            double radius=frame.RadiusM,scale=HorizontalScale;var token=faunaCancel.Token;
            faunaPlan=Task.Run(()=>BuildFaunaSites(center,radius,scale,known,token),token);
        }
        if(now>=nextFaunaMix)
        {
            nextFaunaMix=now+.5f;faunaRanked.Clear();double latitude=Math.Asin(center.Z)*180/Math.PI;
            foreach(var s in faunaSites.Values)
            {
                s.Target=0;if(s.Profile==null)continue;int kind=(int)s.Anchor.Kind;
                if(!o.Enabled||!o.GroupsEnabled[kind]||FaunaClipCount(kind,o.Source[kind])==0)continue;
                Vector3 point=FaunaPoint(s);float distance=Vector3.Distance(point,view.transform.position);if(distance>=150)continue;
                double wind=Math.Sqrt(CurrentWeather.WindEastMps*CurrentWeather.WindEastMps+CurrentWeather.WindNorthMps*CurrentWeather.WindNorthMps);
                double temperature=CurrentWeather.TemperatureC-(s.Ground-profileElevation)*.006;
                double activity=FaunaAmbience.ActivityGain(s.Profile,LocalSolarHour,skyYear,latitude,temperature,CurrentWeather.Humidity,CurrentWeather.RainMmPerHour,wind);
                s.Target=(float)(s.Habitat*activity*s.Profile.Acoustics.RelativeLoudness)*o.Volume*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(115,150,distance)));
                if(s.Target>.003f)faunaRanked.Add(s);
            }
            faunaRanked.Sort((a,b)=>
            {
                float Score(FaunaSite s)=>s.Target/(8+Vector3.Distance(FaunaPoint(s),view.transform.position));
                int cmp=Score(b).CompareTo(Score(a));return cmp!=0?cmp:a.Anchor.Id.CompareTo(b.Anchor.Id);
            });
            // Preserve audible identities. Hysteresis prevents camera movement
            // from continually replacing/chopping nearby calls.
            foreach(var v in faunaVoices)if(v.Site!=null)
            {
                if(!faunaSites.ContainsKey(v.Site.Anchor.Id)||v.Method!=o.Source[(int)v.Site.Anchor.Kind])v.Site=null;
                else
                {
                    var point=FaunaPoint(v.Site);v.Site.Occluded=TerrainOccluded(new Bounds(point,Vector3.one*2),view.transform.position,true);
                }
            }
            var perGroup=new int[5];foreach(var v in faunaVoices)if(v.Site!=null&&v.Site.Target>.003f)perGroup[(int)v.Site.Anchor.Kind]++;
            foreach(var site in faunaRanked)
            {
                int kind=(int)site.Anchor.Kind;if(perGroup[kind]>=3)continue;
                bool occupied=false;foreach(var v in faunaVoices)if(v.Site==site){occupied=true;break;}if(occupied)continue;
                foreach(var v in faunaVoices)if((v.Site==null||v.Site.Target<=.003f)&&v.Gain<.001f)
                {
                    StopFaunaVoice(v);v.Site=site;v.Method=o.Source[kind];v.Sequence=(int)(site.Anchor.Hash%3);v.LastClip=-1;v.Next=0;
                    double reference=kind==0?4300:kind==1?5200:kind==2?6500:kind==3?650:3500;
                    v.Pitch=(float)Math.Max(.88,Math.Min(1.12,Math.Pow(site.Profile.Acoustics.CarrierHz/reference,.18)));
                    v.Pitch*=(float)Math.Pow(2,((site.Anchor.Hash%101)*.02-1)*site.Profile.Acoustics.IndividualPitchSpreadCents/1200);
                    site.Occluded=TerrainOccluded(new Bounds(FaunaPoint(site),Vector3.one*2),view.transform.position,true);perGroup[kind]++;break;
                }
            }
        }
        double dsp=AudioSettings.dspTime;float smooth=1-Mathf.Exp(-Time.unscaledDeltaTime*1.4f);
        foreach(var v in faunaVoices)
        {
            float target=v.Site==null?0:v.Site.Target*(v.Site.Occluded?.28f:1);
            v.Gain=Mathf.Lerp(v.Gain,target,smooth);
            if(v.Site!=null)v.Root.transform.position=FaunaPoint(v.Site);
            if(v.Gain<.001f&&target<=.001f){StopFaunaVoice(v);continue;}
            if(v.Site!=null)ScheduleFauna(v,dsp);
            for(int b=0;b<2;b++)
            {
                double t=dsp-v.Start[b],remaining=v.End[b]-dsp;
                float fade=t<=0||remaining<=0?0:Mathf.Sin(Mathf.Min(1,(float)Math.Min(t/.65,remaining/.65))*Mathf.PI*.5f);
                v.Sources[b].volume=v.Gain*fade;
                float cutoff=v.Site!=null&&v.Site.Occluded?2200:Mathf.Lerp(14000,7000,Mathf.Clamp01(Vector3.Distance(v.Root.transform.position,view.transform.position)/150));
                v.Filters[b].cutoffFrequency=Mathf.Lerp(v.Filters[b].cutoffFrequency,cutoff,smooth);
            }
        }
    }
    void ScheduleFauna(FaunaVoice v,double dsp)
    {
        if(v.Next>dsp+.2)return;var list=faunaBanks[(int)v.Site.Anchor.Kind,v.Method];if(list.Count==0)return;
        int choice=v.Sequence++%list.Count;if(choice==v.LastClip&&list.Count>1)choice=(choice+1)%list.Count;v.LastClip=choice;
        int b=v.NextBank;v.NextBank=1-b;var source=v.Sources[b];source.Stop();source.clip=list[choice];source.pitch=v.Pitch;
        bool bed=(int)v.Site.Anchor.Kind<3;double begin=Math.Max(dsp+.06,v.Next);
        // Stable offsets desynchronise different callers using the same bank.
        float offset=v.Next==0&&bed?source.clip.length*(v.Site.Anchor.Hash%601)*.001f:0;
        source.time=offset;v.Start[b]=begin;v.End[b]=begin+(source.clip.length-offset)/v.Pitch;
        source.PlayScheduled(begin);source.SetScheduledEndTime(v.End[b]);
        if(bed)v.Next=v.End[b]-.65;
        else v.Next=v.End[b]+v.Site.Profile.Acoustics.PhraseGapSeconds*(.7+(Rng.Mix(v.Site.Anchor.Hash+(uint)v.Sequence)%1000)*.001);
    }
    static void StopFaunaVoice(FaunaVoice v)
    {
        if(v==null)return;foreach(var s in v.Sources){s.Stop();s.volume=0;}v.Gain=0;v.Next=0;v.NextBank=0;
        for(int b=0;b<2;b++){v.Start[b]=0;v.End[b]=0;}
    }
    public void PreviewFauna(int group,int method)
    {
        var bank=faunaBanks[group,method];if(faunaPreview==null||bank==null||bank.Count==0)return;
        if(faunaPreviewFade!=null)StopCoroutine(faunaPreviewFade);
        faunaPreviewFade=StartCoroutine(FadeFaunaPreview(bank[faunaPreviewSequence++%bank.Count]));
    }
    IEnumerator FadeFaunaPreview(AudioClip clip)
    {
        float initial=faunaPreview.volume;
        for(float t=0;t<.08f;t+=Time.unscaledDeltaTime){faunaPreview.volume=initial*(1-t/.08f);yield return null;}
        faunaPreview.Stop();faunaPreview.clip=clip;faunaPreview.pitch=1;faunaPreview.volume=0;faunaPreview.Play();
        for(float t=0;t<.15f;t+=Time.unscaledDeltaTime){faunaPreview.volume=FaunaAudioOptions.Current.Volume*Mathf.Clamp01(t/.15f);yield return null;}
        faunaPreview.volume=FaunaAudioOptions.Current.Volume;faunaPreviewFade=null;
    }
    public void StopFaunaPreview(){if(faunaPreviewFade!=null)StopCoroutine(faunaPreviewFade);faunaPreviewFade=null;if(faunaPreview!=null)faunaPreview.Stop();}
    void ResetFaunaFrame(){faunaPlanned=false;nextFaunaMix=nextFaunaPlan=0;} // Retain nearby world-addressed callers on chart changes.
    void DisposeFaunaAudio()
    {
        if(faunaDisposed)return;faunaDisposed=true;faunaCancel.Cancel();
        if(faunaLoading!=null)StopCoroutine(faunaLoading);faunaLoading=null;
        if(faunaRequest!=null){faunaRequest.Abort();faunaRequest.Dispose();faunaRequest=null;}
        StopFaunaPreview();foreach(var v in faunaVoices){if(v==null)continue;StopFaunaVoice(v);Destroy(v.Root);}
        foreach(var bank in faunaBanks)if(bank!=null){foreach(var clip in bank)if(clip!=null)Destroy(clip);bank.Clear();}
        if(faunaPlan!=null)faunaPlan.ContinueWith(t=>{if(t.IsFaulted){var observed=t.Exception;}},TaskScheduler.Default);
        faunaSites.Clear();faunaCancel.Dispose();
    }
}
