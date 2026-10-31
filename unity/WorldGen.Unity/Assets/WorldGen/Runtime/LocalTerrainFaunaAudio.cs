using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    sealed class FaunaSite
    {
        public FaunaSoundAnchor Anchor;public FaunaVoiceProfile Profile;
        public V Position;public double Ground,Height,Habitat,SilentUntil,AlarmUntil;public float Target;public bool Occluded,AlarmPending;
        public LocalTerrainFrame PointFrame;public Vector3 Point;public int PointRevision=-1;public double PointOriginX,PointOriginZ;
    }
    sealed class FaunaVoice
    {
        public GameObject Root;public readonly AudioSource[] Sources=new AudioSource[2];
        public readonly AudioLowPassFilter[] Filters=new AudioLowPassFilter[2];
        public readonly double[] Start=new double[2],End=new double[2];
        public FaunaSite Site;public float Gain,Pitch=1;public int Sequence,NextBank;public string LastFile="",Cue="";public double Next;
    }
    readonly Dictionary<ulong,FaunaSite> faunaSites=new Dictionary<ulong,FaunaSite>();
    readonly List<ulong> faunaEvict=new List<ulong>();readonly List<FaunaSite> faunaRanked=new List<FaunaSite>();
    readonly FaunaVoice[] faunaVoices=new FaunaVoice[24];
    readonly CancellationTokenSource faunaCancel=new CancellationTokenSource();
    Task<List<FaunaSite>> faunaPlan;V faunaPlanCenter;bool faunaPlanned,faunaDisposed,faunaWasPaused;
    float nextFaunaMix,nextFaunaPlan;int faunaPreviewSequence;Coroutine faunaPreviewFade;AudioSource faunaPreview;
    static readonly AnimationCurve wildlifeFlatRolloff=AnimationCurve.Linear(0,1,1,1);
    static void ConfigureWildlifeProjection(AudioSource source,double range)
    {source.rolloffMode=AudioRolloffMode.Custom;source.SetCustomCurve(AudioSourceCurveType.CustomRolloff,wildlifeFlatRolloff);source.minDistance=.01f;source.maxDistance=(float)range;}
    WildlifeSoundTake faunaPendingPreview;float faunaPreviewLevel;
    public string FaunaAudioDebug
    {
        get
        {
            int count=0;foreach(var v in faunaVoices)if(v!=null&&v.Site!=null&&v.Gain>.001f)count++;
            return "Wildlife audio ? "+count+" ambient / "+ActiveCreatureVoices+" animal voices ? "+wildlifeSounds.Takes.Count+" takes ? "+(wildlifeLibraryReady?"ready":"loading")+
                " ? cache "+(WildlifeCachedBytes/(1024d*1024)).ToString("F1")+" MiB"+(faunaPlan!=null?" ? habitat pending":"")+(wildlifeMissing>0?" ? "+wildlifeMissing+" deleted/missing":"");
        }
    }
    void InitializeFaunaAudio()
    {
        FaunaAudioOptions.Current.Load();
        for(int i=0;i<faunaVoices.Length;i++)
        {
            var v=faunaVoices[i]=new FaunaVoice{Root=new GameObject("Habitat caller "+i)};v.Root.transform.SetParent(transform,false);
            for(int b=0;b<2;b++)
            {
                var child=new GameObject("Call overlap "+b);child.transform.SetParent(v.Root.transform,false);
                var audio=v.Sources[b]=child.AddComponent<AudioSource>();audio.playOnAwake=false;audio.loop=false;audio.spatialBlend=1;audio.dopplerLevel=0;
                ConfigureWildlifeProjection(audio,180);audio.volume=0;audio.priority=170;
                v.Filters[b]=child.AddComponent<AudioLowPassFilter>();v.Filters[b].cutoffFrequency=14000;
            }
        }
        faunaPreview=view.gameObject.AddComponent<AudioSource>();faunaPreview.playOnAwake=false;faunaPreview.spatialBlend=0;faunaPreview.loop=false;faunaPreview.priority=45;
        InitializeCreatureAudio();ServiceWildlifeLibrary();
    }
    List<FaunaSite> BuildFaunaSites(V center,double radius,double scale,HashSet<ulong> known,CancellationToken token)
    {
        var anchors=new List<FaunaSoundAnchor>();FaunaAmbience.Anchors(sourceWorld.Recipe.Seed,center,radius,210,anchors);
        var result=new List<FaunaSite>();var patch=terrain.Patch(center,250/scale);var ecology=sourceWorld.Ecology;var model=FaunaAmbience.For(sourceWorld);var emitters=new HashSet<ulong>();
        foreach(var anchor in anchors)
        {
            token.ThrowIfCancellationRequested();if(known.Contains(anchor.Id)||(anchor.Position-center).Length*radius>WildlifeAcoustics.AmbientRange(anchor.Kind)+16)continue;
            var site=new FaunaSite{Anchor=anchor,Position=anchor.Position};result.Add(site);
            int cell=terrain.NearestCell(anchor.Position);if(!model.TryProfile(cell,anchor.Kind,out var p))continue;
            var sample=patch.Sample(anchor.Position);cell=sample.Cell;if(!model.TryProfile(cell,anchor.Kind,out p))continue;
            double gain=FaunaAmbience.HabitatGain(p,ecology.Habitat[cell],sample);if(gain<.075)continue;
            site.Ground=sample.ElevationM;site.Height=anchor.Kind==FaunaSoundKind.Cricket?.12:anchor.Kind==FaunaSoundKind.Frog?.15:.8;
            if(anchor.Kind==FaunaSoundKind.Cicada||anchor.Kind==FaunaSoundKind.UnderstoryBird||anchor.Kind==FaunaSoundKind.Cockatoo||anchor.Kind==FaunaSoundKind.Hornbill||anchor.Kind==FaunaSoundKind.Parrot||anchor.Kind==FaunaSoundKind.Toucan||anchor.Kind==FaunaSoundKind.Turaco||anchor.Kind==FaunaSoundKind.Woodpecker||anchor.Kind==FaunaSoundKind.Owl)
            {
                if(scatter==null||!scatter.TryFaunaTree(anchor.Position,patch,out var tree))continue;
                // Same actual planting predicate as rendered trees, including
                // when graphics toggles/LODs hide them. Never perch in bare air.
                int treeCell=terrain.NearestCell(tree.Position);if(!model.TryProfile(treeCell,anchor.Kind,out p))continue;
                var treeSample=patch.Sample(tree.Position);gain=FaunaAmbience.HabitatGain(p,ecology.Habitat[treeSample.Cell],treeSample);if(gain<.075)continue;
                ulong id=WildlifeAcoustics.TreeIdentity(tree.Id,anchor.Kind);if(known.Contains(id)||!emitters.Add(id))continue;
                uint identity=Rng.Mix((uint)id^(uint)(id>>32));
                // The tree owns this caller, not the grid opportunity that
                // discovered it. Nearby queries cannot stack or relocate it.
                site=new FaunaSite{Anchor=new FaunaSoundAnchor(id,identity,tree.Position,anchor.Kind),Position=tree.Position,Ground=tree.GroundM};result.Add(site);
                site.Height=Math.Max(.5,Math.Min(tree.HeightM*(anchor.Kind==FaunaSoundKind.Cicada?.3:.55),anchor.Kind==FaunaSoundKind.Cicada?12:20));
            }
            else if(anchor.Kind==FaunaSoundKind.Katydid)
            {if(sample.Cover.Coverage<.08&&sample.MidCover.Coverage<.08)continue;site.Height=Math.Max(.2,Math.Min(1.4,sample.Cover.Stature*.5));}
            site.Profile=p;site.Habitat=gain;
        }
        return result;
    }
    Vector3 FaunaPoint(FaunaSite s)
    {
        if(ReferenceEquals(s.PointFrame,frame)&&s.PointRevision==canopySourceRevision&&s.PointOriginX==originX&&s.PointOriginZ==originZ)return s.Point;
        double d=V.Dot(s.Position,frame.Origin);if(d<=0)return new Vector3(0,-100000,0);
        double x=V.Dot(s.Position,frame.East)*frame.RadiusM/d,z=V.Dot(s.Position,frame.North)*frame.RadiusM/d;
        s.Point=new Vector3((float)(x-originX),(float)(VisibleGround(x,z,frame.SurfaceY(s.Position,s.Ground))+s.Height),(float)(z-originZ));
        s.PointFrame=frame;s.PointRevision=canopySourceRevision;s.PointOriginX=originX;s.PointOriginZ=originZ;return s.Point;
    }
    void UpdateFaunaAudio(bool audioPaused)
    {
        if(faunaDisposed||faunaVoices[0]==null)return;ServiceWildlifeLibrary();
        var o=FaunaAudioOptions.Current;float now=Time.unscaledTime;
        if(faunaPendingPreview!=null)
        {
            var clip=WildlifeClip(faunaPendingPreview);
            if(clip!=null){faunaPendingPreview=null;if(faunaPreviewFade!=null)StopCoroutine(faunaPreviewFade);faunaPreviewFade=StartCoroutine(FadeFaunaPreview(clip));}
        }
        if(!paused&&faunaPreview!=null&&(faunaPreview.isPlaying||faunaPreviewFade!=null||faunaPendingPreview!=null))StopFaunaPreview();
        if(faunaPreview!=null&&faunaPreview.isPlaying&&faunaPreviewFade==null)faunaPreview.volume=faunaPreviewLevel;
        UpdateCreatureAudio(audioPaused);
        if(audioPaused){if(!faunaWasPaused)foreach(var v in faunaVoices)StopFaunaVoice(v);faunaWasPaused=true;return;}
        faunaWasPaused=false;if(sourceWorld?.Ecology==null)return;
        if(faunaPlan!=null&&faunaPlan.IsCompleted)
        {
            if(faunaPlan.Status==TaskStatus.RanToCompletion)foreach(var s in faunaPlan.Result)faunaSites[s.Anchor.Id]=s;
            else if(faunaPlan.IsFaulted)Debug.LogWarning("Fauna habitat pass failed: "+faunaPlan.Exception.GetBaseException().Message);
            faunaPlan=null;nextFaunaMix=0;
        }
        V center=Position;
        if(o.Enabled&&!mapOpen&&!ecologyOpen&&faunaPlan==null&&now>=nextFaunaPlan&&(!faunaPlanned||(center-faunaPlanCenter).Length*frame.RadiusM>24))
        {
            faunaEvict.Clear();foreach(var pair in faunaSites)if((pair.Value.Position-center).Length*frame.RadiusM>265)faunaEvict.Add(pair.Key);
            foreach(var id in faunaEvict)faunaSites.Remove(id);
            var known=new HashSet<ulong>(faunaSites.Keys);faunaPlanCenter=center;faunaPlanned=true;nextFaunaPlan=now+1;
            double radius=frame.RadiusM,scale=HorizontalScale;var token=faunaCancel.Token;
            faunaPlan=Task.Run(()=>BuildFaunaSites(center,radius,scale,known,token),token);
        }
        if(now>=nextFaunaMix)
        {
            nextFaunaMix=now+.6f;faunaRanked.Clear();double latitude=Math.Asin(center.Z)*180/Math.PI;
            double wind=Math.Sqrt(CurrentWeather.WindEastMps*CurrentWeather.WindEastMps+CurrentWeather.WindNorthMps*CurrentWeather.WindNorthMps);
            foreach(var s in faunaSites.Values)
            {
                float previousTarget=s.Target;s.Target=0;if(s.Profile==null)continue;int kind=(int)s.Anchor.Kind;string bank=FaunaAmbience.Banks[kind];
                if(!o.Enabled||!o.GroupsEnabled[kind]||!wildlifeSounds.Has(bank))continue;
                Vector3 point=FaunaPoint(s);float distance=Vector3.Distance(transform.TransformPoint(point),view.transform.position);double range=WildlifeAcoustics.AmbientRange(s.Anchor.Kind);if(distance>=range)continue;
                if(!Spectating&&motion.Grounded&&distance<(kind<3?6:10)&&Math.Abs(playerDeathVelocity.x)+Math.Abs(playerDeathVelocity.z)>.2)
                {
                    if(kind>=4&&previousTarget>.003f&&now>=s.SilentUntil&&wildlifeSounds.Has(bank,"alert")){s.AlarmPending=true;s.AlarmUntil=now+8;}
                    s.SilentUntil=now+4+s.Anchor.Hash%9;
                }
                if(s.AlarmPending&&now>s.AlarmUntil)s.AlarmPending=false;
                if(now<s.SilentUntil&&now>=s.AlarmUntil)continue;
                if(kind>=4&&ModeledCallerNear(bank,point,24))continue;
                double temperature=CurrentWeather.TemperatureC-(s.Ground-profileElevation)*.006;
                double activity=FaunaAmbience.ActivityGain(s.Profile,WildlifeAcoustics.CallingHour(LocalSolarHour,s.Anchor.Hash),skyYear,latitude,temperature,CurrentWeather.Humidity,CurrentWeather.RainMmPerHour,wind);
                s.Target=(float)(s.Habitat*activity*s.Profile.Acoustics.RelativeLoudness*WildlifeAcoustics.AmbientLevel(s.Anchor.Kind))*o.Volume;
                if(s.Target*WildlifeAcoustics.Gain(distance,WildlifeAcoustics.AmbientReference(s.Anchor.Kind),range)>.001)faunaRanked.Add(s);
            }
            faunaRanked.Sort((a,b)=>
            {
                double Score(FaunaSite s)=>s.Target*WildlifeAcoustics.Gain(Vector3.Distance(transform.TransformPoint(FaunaPoint(s)),view.transform.position),WildlifeAcoustics.AmbientReference(s.Anchor.Kind),WildlifeAcoustics.AmbientRange(s.Anchor.Kind));
                int comparison=Score(b).CompareTo(Score(a));return comparison!=0?comparison:a.Anchor.Id.CompareTo(b.Anchor.Id);
            });
            foreach(var v in faunaVoices)if(v.Site!=null)
            {
                if(!faunaSites.ContainsKey(v.Site.Anchor.Id))v.Site=null;
                else{if(v.Site.AlarmPending&&v.Cue!="alert")StopFaunaVoice(v);v.Site.Occluded=TerrainOccluded(new Bounds(FaunaPoint(v.Site),Vector3.one*2),view.transform.position,true);}
            }
            var perGroup=new int[FaunaAmbience.KindCount];foreach(var v in faunaVoices)if(v.Site!=null&&v.Site.Target>.003f)perGroup[(int)v.Site.Anchor.Kind]++;
            foreach(var site in faunaRanked)
            {
                int kind=(int)site.Anchor.Kind;if(perGroup[kind]>=(kind==1?2:kind==(int)FaunaSoundKind.Owl?1:3))continue;
                bool occupied=false;foreach(var v in faunaVoices)if(v.Site==site){occupied=true;break;}if(occupied)continue;
                foreach(var v in faunaVoices)if((v.Site==null||v.Site.Target<=.003f)&&v.Gain<.001f)
                {
                    StopFaunaVoice(v);v.Site=site;v.Sequence=(int)(site.Anchor.Hash%65521);v.LastFile="";
                    foreach(var source in v.Sources)ConfigureWildlifeProjection(source,WildlifeAcoustics.AmbientRange(site.Anchor.Kind));
                    double reference=kind==0?4300:kind==1?5200:kind==2?6500:kind==3?650:kind==(int)FaunaSoundKind.Pigeon?380:kind==(int)FaunaSoundKind.Owl?600:kind==(int)FaunaSoundKind.Raven?700:kind==(int)FaunaSoundKind.Hornbill?500:kind==(int)FaunaSoundKind.Hummingbird?6500:kind==(int)FaunaSoundKind.Parrot||kind==(int)FaunaSoundKind.Cockatoo?1700:3500;
                    v.Pitch=(float)Math.Max(.88,Math.Min(1.12,Math.Pow(site.Profile.Acoustics.CarrierHz/reference,.18)));
                    v.Pitch*=(float)Math.Pow(2,((site.Anchor.Hash%101)*.02-1)*site.Profile.Acoustics.IndividualPitchSpreadCents/1200);
                    site.Occluded=TerrainOccluded(new Bounds(FaunaPoint(site),Vector3.one*2),view.transform.position,true);perGroup[kind]++;break;
                }
            }
        }
        double dsp=AudioSettings.dspTime;float smooth=1-Mathf.Exp(-Time.unscaledDeltaTime*1.4f);
        foreach(var v in faunaVoices)
        {
            float target=v.Site==null?0:v.Site.Target*(v.Site.Occluded?.25f:1);v.Gain=Mathf.Lerp(v.Gain,target,1-Mathf.Exp(-Time.unscaledDeltaTime/(target<v.Gain?12:2.5f)));
            if(v.Site!=null)v.Root.transform.localPosition=FaunaPoint(v.Site);
            float distance=Vector3.Distance(v.Root.transform.position,view.transform.position);
            double range=v.Site==null?0:WildlifeAcoustics.AmbientRange(v.Site.Anchor.Kind);
            if(v.Site!=null&&distance>=range){StopFaunaVoice(v);v.Site=null;continue;}
            if(v.Gain<.001f&&target<=.001f){StopFaunaVoice(v);continue;}
            if(v.Site!=null&&v.Site.Target>.001f)ScheduleFauna(v,dsp);
            float fadeSeconds=v.Site!=null&&(int)v.Site.Anchor.Kind<3?.5f:.1f;
            for(int b=0;b<2;b++)
            {
                double age=dsp-v.Start[b],remaining=v.End[b]-dsp;
                float fade=age<=0||remaining<=0?0:Mathf.Sin(Mathf.Min(1,(float)Math.Min(age/fadeSeconds,remaining/fadeSeconds))*Mathf.PI*.5f);
                v.Sources[b].volume=v.Gain*fade*(float)(v.Site==null?0:WildlifeAcoustics.Gain(distance,WildlifeAcoustics.AmbientReference(v.Site.Anchor.Kind),range));
                if(remaining<-.1&&v.Sources[b].clip!=null){v.Sources[b].Stop();v.Sources[b].clip=null;}
                float cutoff=v.Site!=null&&v.Site.Occluded?2400:Mathf.Lerp(16000,6500,Mathf.Clamp01(Vector3.Distance(v.Root.transform.position,view.transform.position)/190));
                v.Filters[b].cutoffFrequency=Mathf.Lerp(v.Filters[b].cutoffFrequency,cutoff,smooth);
            }
        }
    }
    void ScheduleFauna(FaunaVoice v,double dsp)
    {
        var site=v.Site;if(v.Next>dsp+.3&&!site.AlarmPending)return;string bank=FaunaAmbience.Banks[(int)site.Anchor.Kind],cue=site.AlarmPending?"alert":"ambient";
        uint species=Rng.Mix((uint)site.Profile.RegionId^(uint)site.Profile.NativeTaxonId*7919u^(uint)sourceWorld.Recipe.Seed);
        // Existing broad calling guilds can contain several coexisting voices.
        // Each world-addressed individual retains its own form across takes.
        var list=wildlifeSounds.Compatible(bank,cue,species,(int)site.Anchor.Kind<5?(int)(site.Anchor.Hash%3):0);if(list.Count==0)return;
        int choice=(int)(Rng.Mix(site.Anchor.Hash+(uint)v.Sequence*7919u)%(uint)list.Count);
        if(list.Count>1&&list[choice].File==v.LastFile)choice=(choice+1)%list.Count;
        var take=list[choice];var clip=WildlifeClip(take);if(clip==null)return;
        v.Sequence++;v.LastFile=take.File;v.Cue=cue;int b=v.NextBank;v.NextBank=1-b;var source=v.Sources[b];source.Stop();source.clip=clip;source.pitch=v.Pitch;
        bool bed=(int)site.Anchor.Kind<3;double begin=Math.Max(dsp+.06,v.Next);
        float offset=v.Next==0&&bed?clip.length*(site.Anchor.Hash%601)*.001f:0;
        source.time=offset;v.Start[b]=begin;v.End[b]=begin+(clip.length-offset)/v.Pitch;source.PlayScheduled(begin);source.SetScheduledEndTime(v.End[b]);
        if(bed)v.Next=v.End[b]+2+(Rng.Mix(site.Anchor.Hash+(uint)v.Sequence)%700)*.01;else v.Next=v.End[b]+site.Profile.Acoustics.PhraseGapSeconds*(.7+(Rng.Mix(site.Anchor.Hash+(uint)v.Sequence)%1000)*.001);
        if(cue=="alert"){site.AlarmPending=false;site.AlarmUntil=Time.unscaledTimeAsDouble+clip.length/v.Pitch;site.SilentUntil=Math.Max(site.SilentUntil,site.AlarmUntil+8);v.Next=v.End[b]+8;}
    }
    static void StopFaunaVoice(FaunaVoice v)
    {
        if(v==null)return;foreach(var source in v.Sources){source.Stop();source.clip=null;source.volume=0;}
        v.Gain=0;v.Next=0;v.NextBank=0;v.Cue="";for(int b=0;b<2;b++){v.Start[b]=0;v.End[b]=0;}
    }
    public void PreviewFauna(int group,int method=0){if(group>=0&&group<FaunaAmbience.KindCount)PreviewWildlife(FaunaAmbience.Banks[group],"ambient");}
    public void PreviewWildlife(string bank,string cue)
    {
        var list=wildlifeSounds.Takes.FindAll(t=>t.Bank==bank&&t.Cue==cue);if(list.Count==0)return;
        list.Sort((a,b)=>{int c=a.Form.CompareTo(b.Form);return c!=0?c:a.Take.CompareTo(b.Take);});
        faunaPendingPreview=list[faunaPreviewSequence++%list.Count];faunaPreviewLevel=cue=="ambient"?FaunaAudioOptions.Current.Volume:FaunaAudioOptions.Current.CreatureVolume;
    }
    IEnumerator FadeFaunaPreview(AudioClip clip)
    {
        float initial=faunaPreview.volume;for(float t=0;t<.06f;t+=Time.unscaledDeltaTime){faunaPreview.volume=initial*(1-t/.06f);yield return null;}
        faunaPreview.Stop();faunaPreview.clip=clip;faunaPreview.pitch=1;faunaPreview.volume=0;faunaPreview.Play();
        for(float t=0;t<.1f;t+=Time.unscaledDeltaTime){faunaPreview.volume=faunaPreviewLevel*Mathf.Clamp01(t/.1f);yield return null;}
        faunaPreview.volume=faunaPreviewLevel;faunaPreviewFade=null;
    }
    public void StopFaunaPreview(){faunaPendingPreview=null;if(faunaPreviewFade!=null)StopCoroutine(faunaPreviewFade);faunaPreviewFade=null;if(faunaPreview!=null){faunaPreview.Stop();faunaPreview.clip=null;}}
    void ResetFaunaFrame(){faunaPlanned=false;nextFaunaMix=nextFaunaPlan=0;}
    void DisposeFaunaAudio()
    {
        if(faunaDisposed)return;faunaDisposed=true;faunaCancel.Cancel();StopFaunaPreview();
        foreach(var v in faunaVoices){if(v==null)continue;StopFaunaVoice(v);Destroy(v.Root);}
        DisposeCreatureAudio();DisposeWildlifeLibrary();
        if(faunaPlan!=null)faunaPlan.ContinueWith(t=>{if(t.IsFaulted){var observed=t.Exception;}},TaskScheduler.Default);
        faunaSites.Clear();faunaCancel.Dispose();
    }
}
