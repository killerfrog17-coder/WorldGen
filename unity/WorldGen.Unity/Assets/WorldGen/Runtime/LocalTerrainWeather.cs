using System;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    LocalWeatherScreen weatherScreen;WorldWeather.Sample weatherProvider;WorldWeather worldWeather;V weatherPosition;
    double weatherDays,nextWeatherAt,profileElevation;float nextFlashAt,flashEnd,weatherAudioClock;bool weatherAudioPaused;
    long lastLightningSlot=long.MinValue;ParticleSystem rainParticles,snowParticles;AudioSource windAudio,rainAudio;
    AudioClip windClip,rainClip;AudioClip[] thunderClips;Material rainMaterial,snowMaterial,boltMaterial;LineRenderer lightningBolt;
    sealed class ThunderVoice {public AudioSource Source;public AudioLowPassFilter Filter;public float Due=-1;}
    readonly ThunderVoice[] thunderVoices=new ThunderVoice[4];int nextThunderVoice;
    public int ThunderVariantCount=>thunderClips?.Length??0;
    public int PendingThunderCount {get{int count=0;foreach(var voice in thunderVoices)if(voice!=null&&voice.Due>=0)count++;return count;}}
    public int ActiveThunderCount {get{int count=0;foreach(var voice in thunderVoices)if(voice!=null&&voice.Source.isPlaying)count++;return count;}}
    public float LastThunderDistanceM {get;private set;}
    public float LastThunderDelaySeconds {get;private set;}
    public float LastThunderLowPassHz {get;private set;}
    public WeatherFrame CurrentWeather {get;private set;}
    public WeatherControl WeatherControl {get;}=new WeatherControl();
    WindSynth windSynth;RainSynth rainSynth;int appliedWeatherRevision=-1;
    public WeatherFrame? TestWeatherOverride {get;set;}
    public bool TestAnimateWeather {get;set;}
    public LocalWeatherScreen WeatherScreen=>weatherScreen;
    public int WeatherParticleCount=>(rainParticles?.particleCount??0)+(snowParticles?.particleCount??0);
    public WeatherSurfaceEstimate WeatherSurface=>weatherProvider==null?default:weatherProvider.SurfaceAt(weatherDays);
    public WeatherWater WeatherRecent(double days)=>weatherProvider==null?default:weatherProvider.Between(weatherDays-days,weatherDays);
    public WeatherFrame TestNaturalWeather(double days)=>weatherProvider.At(days,includeFog:false);
    public void TestWeatherMove(double eastM){motion.X+=eastM;UpdateCamera();nextDaylightAt=0;SetDaylight(LocalSolarHour,skyYear);}
    bool LegacySkyDiagnostic=>passiveSkyCheck&&!Array.Exists(Environment.GetCommandLineArgs(),s=>s=="--worldgen-weather-check");
    void ResetWeatherFrame()
    {
        weatherProvider=null;nextWeatherAt=0;nextDaylightAt=0;
        if(rainParticles!=null)rainParticles.Clear();if(snowParticles!=null)snowParticles.Clear();
        if(weatherScreen!=null){weatherScreen.Ready=false;weatherScreen.Flash=0;}flashEnd=0;
        foreach(var voice in thunderVoices)if(voice!=null){voice.Due=-1;voice.Source.Stop();}
    }
    void InitializeWeather()
    {
        if(sourceWorld==null||LegacySkyDiagnostic)return;
        weatherScreen=view.gameObject.AddComponent<LocalWeatherScreen>();
        var shader=Resources.Load<Shader>("WorldGenWeatherParticle");if(shader==null||!shader.isSupported)throw new InvalidOperationException("Weather particle shader unavailable.");
        rainMaterial=new Material(shader);snowMaterial=new Material(shader);snowMaterial.SetFloat("_Snow",1);
        rainParticles=CreatePrecipitation("Local rain",rainMaterial,false);snowParticles=CreatePrecipitation("Local snow",snowMaterial,true);
        windAudio=view.gameObject.AddComponent<AudioSource>();windAudio.loop=true;windAudio.playOnAwake=false;windAudio.spatialBlend=0;windAudio.volume=0;
        thunderClips=new[]{MakeThunderSound(198u),MakeThunderSound(913u),MakeThunderSound(4817u)};
        for(int i=0;i<thunderVoices.Length;i++)
        {
            var emitter=new GameObject("Weather thunder "+i);emitter.transform.SetParent(transform,false);
            var audio=emitter.AddComponent<AudioSource>();audio.playOnAwake=false;audio.spatialBlend=1;audio.dopplerLevel=0;
            audio.rolloffMode=AudioRolloffMode.Linear;audio.minDistance=100;audio.maxDistance=12000;
            thunderVoices[i]=new ThunderVoice{Source=audio,Filter=emitter.AddComponent<AudioLowPassFilter>()};
        }
        windSynth=new WindSynth();windClip=AudioClip.Create("Continuous wind gusts",22050*60,2,22050,true,windSynth.Read);
        windAudio.clip=windClip;windAudio.Play();
        rainSynth=new RainSynth();rainAudio=view.gameObject.AddComponent<AudioSource>();rainAudio.loop=true;rainAudio.playOnAwake=false;rainAudio.spatialBlend=0;rainAudio.volume=0;
        rainClip=AudioClip.Create("Continuous rainfall",22050*60,2,22050,true,rainSynth.Read);rainAudio.clip=rainClip;rainAudio.Play();
        var bolt=new GameObject("Local lightning",typeof(LineRenderer));bolt.layer=30;bolt.transform.SetParent(transform,false);lightningBolt=bolt.GetComponent<LineRenderer>();
        boltMaterial=new Material(shader);boltMaterial.SetFloat("_Bolt",1);lightningBolt.sharedMaterial=boltMaterial;lightningBolt.useWorldSpace=true;lightningBolt.widthMultiplier=3;lightningBolt.enabled=false;
        lightningBolt.startColor=new Color(.8f,.85f,1);lightningBolt.endColor=new Color(.8f,.85f,1);lightningBolt.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
    }
    ParticleSystem CreatePrecipitation(string name,Material material,bool snow)
    {
        var root=new GameObject(name,typeof(ParticleSystem));root.layer=30;root.transform.SetParent(transform,false);var ps=root.GetComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.useAutoRandomSeed=false;ps.randomSeed=snow?719u:413u;
        var main=ps.main;main.playOnAwake=false;main.loop=true;main.startLifetime=snow?9:1.1f;main.startSpeed=0;main.maxParticles=12000;main.simulationSpace=ParticleSystemSimulationSpace.World;main.startSize=snow?.06f:.025f;main.startColor=snow?new Color(.9f,.94f,1,.75f):new Color(.74f,.78f,.8f,.4f);
        var shape=ps.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(18,5,18);
        var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;
        var emission=ps.emission;emission.rateOverTime=0;
        var renderer=root.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
        renderer.renderMode=snow?ParticleSystemRenderMode.Billboard:ParticleSystemRenderMode.Stretch;renderer.velocityScale=.025f;renderer.lengthScale=3;renderer.maxParticleSize=.04f;
        ps.Play();return ps;
    }
    static AudioClip MakeThunderSound(uint seed)
    {
        const int rate=22050;int length=rate*8;var samples=new float[length];uint state=seed;float low=0,deep=0,peak=0;
        double secondClap=.25+(seed%997)/997d*.9;
        double Clap(double t,double start){double dt=t-start;return dt<0?0:(1-Math.Exp(-dt*200))*Math.Exp(-dt*6);}
        for(int i=0;i<length;i++)
        {
            state=Rng.Mix(state+0x9e3779b9u);float white=state/2147483648f-1;low+=(white-low)*.07f;deep+=(low-deep)*.015f;
            double t=i/(double)rate;
            double crack=Clap(t,.015)+Clap(t,secondClap)*.6;
            double rumble=(1-Math.Exp(-t*3))*Math.Exp(-t*(.65+(seed%137)/500d));
            double tail=Math.Min(1,8-t);tail*=tail;
            samples[i]=(float)(((white-low)*crack*.24+low*rumble*.65+deep*rumble*2.4)*tail);peak=Math.Max(peak,Math.Abs(samples[i]));
        }
        if(peak>0)for(int i=0;i<length;i++)samples[i]*=.72f/peak;
        var clip=AudioClip.Create("Thunder variation "+seed,length,1,rate,false);clip.SetData(samples,0);return clip;
    }
    void UpdateLocalWeather(double days,double altitude)
    {
        if(weatherScreen==null)return;weatherDays=days;
        var at=Position;
        worldWeather=worldWeather??new WorldWeather(sourceWorld,terrain);UpdateWeatherField(days);
        if(weatherProvider==null||(at-weatherPosition).Length*frame.RadiusM>.05)
        {
            weatherPosition=at;profileElevation=walkingPatch.Sample(at).ElevationM;
            weatherProvider=worldWeather.AtPosition(at,profileElevation);nextWeatherAt=0;
        }
        if(Time.realtimeSinceStartupAsDouble>=nextWeatherAt||TestWeatherOverride.HasValue||appliedWeatherRevision!=WeatherControl.Revision)
        {double sampledDays=weatherScreen.UseField?(previousWeatherTile==null?weatherTile.Days:previousWeatherTile.Days+(weatherTile.Days-previousWeatherTile.Days)*weatherScreen.FieldBlend):days;CurrentWeather=TestWeatherOverride??WeatherControl.Apply(weatherProvider.At(sampledDays,LocalSolarHour),profileElevation);appliedWeatherRevision=WeatherControl.Revision;nextWeatherAt=Time.realtimeSinceStartupAsDouble+.1;}
        weatherScreen.Weather=CurrentWeather;weatherScreen.Days=days;weatherScreen.GroundM=profileElevation;
        var drift=worldWeather.WindDisplacement(days);
        weatherScreen.WindDisplacement=new Vector2((float)(drift.X*HorizontalScale%65536),(float)(drift.Y*HorizontalScale%65536));
    }
    void ConfigureWeatherScreen(PlayableSkyFrame sky,Vector3 sun,Color color)
    {
        if(weatherScreen==null)return;
        Vector3 Vector(V p)=>new Vector3((float)p.X,(float)p.Y,(float)p.Z);
        var anchor=frame.Origin*(frame.RadiusM+frame.AltitudeM)+frame.East*originX+frame.North*originZ;
        weatherScreen.Anchor=new Vector3((float)(anchor.X%65536),(float)(anchor.Y%65536),(float)(anchor.Z%65536));
        weatherScreen.PlanetCenter=new Vector3((float)-originX,(float)(-frame.RadiusM-frame.AltitudeM),(float)-originZ);weatherScreen.RadiusM=(float)frame.RadiusM;
        weatherScreen.PlanetEast=Vector(frame.East);weatherScreen.PlanetNorth=Vector(frame.North);weatherScreen.PlanetUp=Vector(frame.Origin);
        weatherScreen.SunDirection=sun;weatherScreen.SunColor=color;weatherScreen.Daylight=(float)sky.Daylight;
        weatherScreen.Ambient=new Color((float)sky.Ambient.X,(float)sky.Ambient.Y,(float)sky.Ambient.Z);weatherScreen.LightLevel=(float)sky.Daylight+(float)sky.NightIlluminanceLux*.1f;weatherScreen.Ready=true;
        var tint=weatherScreen.Ambient*2+color*(float)sky.Daylight*.7f;rainMaterial.SetColor("_LightTint",tint);snowMaterial.SetColor("_LightTint",tint);
    }
    void UpdateWeatherPresentation()
    {
        if(weatherScreen==null||!weatherScreen.Ready)return;var options=WeatherVideoOptions.Current;float now=Time.unscaledTime;
        Vector3 Wind()
        {
            var at=Position;var east=V.Cross(Math.Abs(at.Z)>.999999?new V(0,1,0):new V(0,0,1),at).Unit;var north=V.Cross(at,east).Unit;
            var p=east*CurrentWeather.WindEastMps+north*CurrentWeather.WindNorthMps;return new Vector3((float)V.Dot(p,frame.East),(float)V.Dot(p,frame.Origin),(float)V.Dot(p,frame.North));
        }
        bool audioPaused=paused&&!TestAnimateWeather;
        if(!audioPaused)weatherAudioClock+=Time.unscaledDeltaTime;
        if(audioPaused!=weatherAudioPaused)
        {foreach(var voice in thunderVoices)if(voice!=null){if(audioPaused)voice.Source.Pause();else voice.Source.UnPause();}weatherAudioPaused=audioPaused;}
        foreach(var voice in thunderVoices)if(voice!=null)
        {
            voice.Source.volume=options.ThunderVolume*.8f;
            if(!audioPaused&&voice.Due>=0&&weatherAudioClock>=voice.Due){voice.Source.Play();voice.Due=-1;}
        }
        var wind=Wind();int cap=options.Particles==2?12000:6000;
        float altitude=(view.transform.position-weatherScreen.PlanetCenter).magnitude-weatherScreen.RadiusM;
        bool aboveCloud=altitude>CurrentWeather.CloudBaseM+CurrentWeather.CloudThicknessM||CurrentWeather.CloudCover<.1;
        void Particles(ParticleSystem ps,bool snow)
        {
            // Concentrate the same bounded budget around the observer, not over a45m-wide box.
            ps.transform.position=view.transform.position+Vector3.up*7-wind*(snow?1.3f:.25f);
            var main=ps.main;main.maxParticles=cap;main.simulationSpeed=paused&&!TestAnimateWeather?0:1;
            var velocity=ps.velocityOverLifetime;velocity.x=wind.x*.65f;velocity.z=wind.z*.65f;velocity.y=wind.y*.65f-(snow?1.6f:22);
            var emission=ps.emission;double rate=snow?CurrentWeather.SnowWaterMmPerHour:CurrentWeather.RainMmPerHour;
            main.startSize=snow?.06f:Mathf.Lerp(.018f,.032f,1-Mathf.Exp(-(float)rate/10));
            emission.rateOverTime=options.Particles==0||aboveCloud?0:(float)Math.Min(cap/(snow?9d:1.1),snow?Math.Pow(rate,.65)*240:Math.Pow(rate,.7)*300+rate*100);
            if(options.Particles==0&&ps.particleCount>0)ps.Clear();
        }
        Particles(rainParticles,false);Particles(snowParticles,true);
        windSynth.Strength=Mathf.Clamp01(((float)CurrentWeather.GustMps-3)/25);
        windSynth.Storm=Mathf.Max((float)CurrentWeather.Thunderstorm,Mathf.Clamp01(((float)CurrentWeather.GustMps-17)/14));
        float target=paused?0:options.WindVolume;windAudio.volume=Mathf.Lerp(windAudio.volume,target,1-Mathf.Exp(-Time.unscaledDeltaTime*2));
        rainSynth.MillimetresPerHour=aboveCloud?0:(float)CurrentWeather.RainMmPerHour;
        rainAudio.volume=Mathf.Lerp(rainAudio.volume,paused?0:options.RainVolume,1-Mathf.Exp(-Time.unscaledDeltaTime*2));
        long slot=(long)Math.Floor(weatherDays*8640); // Ten-second dated opportunity; no global storm list.
        if(!paused&&slot!=lastLightningSlot&&now>nextFlashAt)
        {
            lastLightningSlot=slot;uint hash=Rng.Mix(unchecked((uint)slot^(uint)(slot>>32)^(uint)sourceWorld.Recipe.Seed));
            if(CurrentWeather.Thunderstorm>.2&&hash/4294967295d<CurrentWeather.Thunderstorm*.4)TriggerLightning(hash);
        }
        weatherScreen.Flash=now<flashEnd?Mathf.Clamp01((flashEnd-now)/.2f):0;lightningBolt.enabled=now<flashEnd&&options.Lightning>0;
    }
    public void TestLightning(uint seed=443u)=>TriggerLightning(seed);
    public bool ThunderSignalsValid
    {
        get
        {
            if(thunderClips==null||thunderClips.Length!=3)return false;
            double lastSignature=double.NaN;
            foreach(var clip in thunderClips)
            {
                var samples=new float[clip.samples];clip.GetData(samples,0);double signature=0,power=0;
                for(int i=0;i<samples.Length;i++){float v=samples[i];if(float.IsNaN(v)||float.IsInfinity(v)||Math.Abs(v)>.73)return false;power+=v*v;signature+=v*((i%127)+1);}
                if(power<1||signature==lastSignature||Math.Abs(samples[0])>.0001||Math.Abs(samples[samples.Length-1])>.0001)return false;lastSignature=signature;
            }
            return true;
        }
    }
    void TriggerLightning(uint hash)
    {
        nextFlashAt=Time.unscaledTime+8;flashEnd=Time.unscaledTime+.2f;weatherScreen.Flash=1;uint soundSeed=hash;
        var start=view.transform.position+view.transform.forward*2200+Vector3.up*(float)CurrentWeather.CloudBaseM;start.x+=(int)(hash%1000)-500;
        lightningBolt.positionCount=9;for(int i=0;i<9;i++){hash=Rng.Mix(hash+1);var p=start+Vector3.down*(i*(float)CurrentWeather.CloudBaseM/8);if(i>0&&i<8)p+=view.transform.right*((int)(hash%180)-90);lightningBolt.SetPosition(i,p);}
        var soundPoint=start-Vector3.up*(float)CurrentWeather.CloudBaseM*.75f;
        LastThunderDistanceM=Vector3.Distance(view.transform.position,soundPoint);LastThunderDelaySeconds=LastThunderDistanceM/343f;
        LastThunderLowPassHz=650+3850*Mathf.Exp(-LastThunderDistanceM/1400);
        // Fixed-size pool: nearby strikes may overlap rather than replacing the
        // only pending rumble. Reuse a rotating slot only if all four are busy.
        ThunderVoice voice=null;
        for(int i=0;i<thunderVoices.Length;i++){int index=(nextThunderVoice+i)%thunderVoices.Length;var candidate=thunderVoices[index];if(candidate.Due<0&&!candidate.Source.isPlaying){voice=candidate;nextThunderVoice=(index+1)%thunderVoices.Length;break;}}
        if(voice==null){voice=thunderVoices[nextThunderVoice];nextThunderVoice=(nextThunderVoice+1)%thunderVoices.Length;}
        voice.Source.Stop();voice.Source.transform.position=soundPoint;voice.Source.clip=thunderClips[soundSeed%(uint)thunderClips.Length];
        voice.Source.pitch=.93f+(soundSeed%1000)/1000f*.14f;voice.Filter.cutoffFrequency=LastThunderLowPassHz;
        voice.Due=weatherAudioClock+LastThunderDelaySeconds;
    }
    void DisposeWeather()
    {
        DisposeWeatherField();
        if(weatherCheckTarget!=null){view.targetTexture=null;weatherCheckTarget.Release();Destroy(weatherCheckTarget);}
        if(weatherCheckPixel!=null)Destroy(weatherCheckPixel);
        foreach(var voice in thunderVoices)if(voice!=null)voice.Source.Stop();
        if(windClip!=null)Destroy(windClip);if(thunderClips!=null)foreach(var clip in thunderClips)if(clip!=null)Destroy(clip);if(rainClip!=null)Destroy(rainClip);if(rainMaterial!=null)Destroy(rainMaterial);if(snowMaterial!=null)Destroy(snowMaterial);if(boltMaterial!=null)Destroy(boltMaterial);
    }
}
