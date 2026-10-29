using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    sealed class CreatureVoice
    {public AudioSource Source;public AudioLowPassFilter Filter;public Animal Actor;public WildlifeSoundTake Pending;public double Expires;public int Priority;public float Level;public bool Flight;}
    sealed class CreatureVoiceState
    {public Animal Actor;public string Bank,LastFile="";public double Next,EventAt;public int Sequence;public bool Fleeing;}
    readonly List<CreatureVoice> creatureVoices=new List<CreatureVoice>();
    readonly Dictionary<ulong,CreatureVoiceState> creatureVoiceStates=new Dictionary<ulong,CreatureVoiceState>();
    readonly Dictionary<string,List<Vector3>> modeledCallers=new Dictionary<string,List<Vector3>>(StringComparer.Ordinal);
    double creatureThinkAt;
    int ActiveCreatureVoices=>creatureVoices.Count(v=>v.Source.isPlaying);
    void InitializeCreatureAudio()
    {
        for(int i=0;i<16;i++)
        {
            var root=new GameObject("Animal vocalisation "+i);root.transform.SetParent(transform,false);
            var source=root.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=1;source.loop=false;source.dopplerLevel=.15f;source.minDistance=2;source.maxDistance=200;source.rolloffMode=AudioRolloffMode.Linear;source.priority=100;
            var filter=root.AddComponent<AudioLowPassFilter>();filter.cutoffFrequency=18000;creatureVoices.Add(new CreatureVoice{Source=source,Filter=filter});
        }
    }
    bool ModeledCallerNear(string bank,Vector3 point,float distance)
    {if(!modeledCallers.TryGetValue(bank,out var list))return false;foreach(var position in list)if((point-position).sqrMagnitude<distance*distance)return true;return false;}
    static bool FlightBuzzing(Animal animal)=>!animal.Dead&&!animal.Perched&&animal.Clip?.role=="fly";
    CreatureVoiceState AnimalVoice(Animal animal)
    {
        if(creatureVoiceStates.TryGetValue(animal.Spawn.Id,out var state))return state;
        string bank=WildlifeVocalisation.Bank(animal.Model.Asset.Slug);
        state=new CreatureVoiceState{Actor=animal,Bank=bank,Next=Time.unscaledTimeAsDouble+4+animal.Spawn.Hash%25,Sequence=(int)(animal.Spawn.Hash%65521)};
        creatureVoiceStates[animal.Spawn.Id]=state;return state;
    }
    void UpdateCreatureAudio(bool audioPaused)
    {
        var options=FaunaAudioOptions.Current;double now=Time.unscaledTimeAsDouble;
        if(audioPaused||!options.AnimalsEnabled||!options.Enabled)
        {foreach(var voice in creatureVoices){voice.Source.Stop();voice.Source.clip=null;voice.Actor=null;voice.Pending=null;}return;}
        if(now>=creatureThinkAt)
        {
            creatureThinkAt=now+.65;
            bool night=ExplorationMusicContext.SolarAltitude(Math.Asin(Position.Z)*180/Math.PI,LocalSolarHour,skyYear)<-3;
            foreach(var list in modeledCallers.Values)list.Clear();
            foreach(var pair in creatureVoiceStates.Where(p=>p.Value.Actor.Retired||p.Value.Actor.Root==null).ToArray())creatureVoiceStates.Remove(pair.Key);
            foreach(var animal in animals.Values)
            {
                if(animal.Dead||animal.Retired||animal.Root==null)continue;
                float distance=Vector3.Distance(animal.Root.transform.position,view.transform.position);if(distance>210)continue;
                var state=AnimalVoice(animal);if(state.Bank==null)continue;
                if(!modeledCallers.TryGetValue(state.Bank,out var points)){points=new List<Vector3>();modeledCallers[state.Bank]=points;}
                points.Add(animal.Root.transform.localPosition);
                bool flight=WildlifeVocalisation.FlightNoise(state.Bank),fleeing=animal.ThreatUntil>animal.Life&&animal.TargetSpeed>animal.Spawn.Profile.walkMps*1.2;
                if(fleeing&&!state.Fleeing)AnimalVoiceEvent(animal,"alert");state.Fleeing=fleeing;
                if(now<state.Next||fleeing)continue;
                double gap=state.Bank=="hummingbird"&&distance<7&&FlightBuzzing(animal)?1:WildlifeVocalisation.CalmGap(state.Bank);state.Next=now+gap*(.65+(Rng.Mix(animal.Spawn.Hash+(uint)state.Sequence)%1000)*.001);
                bool nocturnal=state.Bank=="owl"||WildlifeBehaviour.IsNocturnal(animal.Model.Asset.Slug,animal.Spawn.Profile.nocturnal);
                if(!flight&&(nocturnal?!night:night))continue;
                if(CurrentWeather.RainMmPerHour>6)continue;
                string cue="contact";
                if(wildlifeSounds.Has(state.Bank,"ambient"))
                {
                    cue="ambient";
                    if(animal.Spawn.Profile.medium=="air"&&!animal.Perched&&animal.Speed>.3f&&state.Bank!="goose"&&state.Bank!="duck"&&state.Bank!="gull"&&state.Bank!="crane"&&!(state.Bank=="hummingbird"&&distance<7))continue;
                }
                else if(state.Bank=="rabbit")continue;
                else if(animal.Speed>animal.Spawn.Profile.walkMps*1.2)continue;
                if(flight){if(distance>10||!FlightBuzzing(animal))continue;cue="flight";}
                if((state.Bank=="deer"||state.Bank=="moose")&&(animal.Spawn.Hash&1)==0&&WildlifeBehaviour.Rut(skyYear,Math.Asin(animal.Planet.Z)*180/Math.PI))cue="rut";
                if(state.Bank=="wolf"&&night&&animal.Spawn.GroupSize>1&&animal.Speed<.1f)cue="howl";
                string bank=state.Bank;
                if(bank=="woodpecker"&&LocalSolarHour<11&&(state.Sequence&1)==0&&wildlifeSounds.Has("woodpecker-drumming"))bank="woodpecker-drumming";
                if(bank=="hummingbird"&&distance<7&&FlightBuzzing(animal)&&wildlifeSounds.Has("hummingbird-wings")){bank="hummingbird-wings";flight=true;}
                QueueAnimalVoice(animal,bank,cue,150,flight);
            }
        }
        foreach(var voice in creatureVoices)
        {
            var animal=voice.Actor;
            if(animal==null||animal.Retired||animal.Root==null){voice.Source.Stop();voice.Source.clip=null;voice.Actor=null;voice.Pending=null;continue;}
            if(voice.Pending!=null)
            {
                if(now>voice.Expires){voice.Pending=null;voice.Actor=null;continue;}
                var clip=WildlifeClip(voice.Pending);
                if(clip!=null)
                {
                    voice.Source.clip=clip;voice.Source.pitch=(float)Math.Max(.88,Math.Min(1.12,Math.Pow(WildlifeVocalisation.ReferenceMass(AnimalVoice(animal).Bank)/Math.Max(.001,animal.MassKg),.05)));
                    voice.Source.priority=voice.Priority;voice.Source.maxDistance=voice.Flight?20:voice.Pending.Cue=="howl"?300:200;
                    voice.Source.Play();voice.Pending=null;
                }
            }
            else if(!voice.Source.isPlaying){voice.Source.clip=null;voice.Actor=null;continue;}
            Vector3 point=animal.Renderer.bounds.center;voice.Source.transform.position=point;
            float distance=Vector3.Distance(point,view.transform.position);float target=options.CreatureVolume*voice.Level;
            if(voice.Flight&&(!FlightBuzzing(animal)||distance>12))target=0;
            voice.Source.volume=Mathf.MoveTowards(voice.Source.volume,target,Time.unscaledDeltaTime*3);
            if(target==0&&voice.Source.volume<=.001f){voice.Source.Stop();voice.Source.clip=null;voice.Actor=null;voice.Pending=null;}
            voice.Filter.cutoffFrequency=Mathf.Lerp(18000,5000,Mathf.Clamp01(distance/200));
        }
    }
    void AnimalVoiceEvent(Animal animal,string cue)
    {
        if(animal==null||animal.Retired||animal.Root==null||faunaDisposed||creatureVoices.Count==0)return;
        var state=AnimalVoice(animal);double now=Time.unscaledTimeAsDouble;
        if(state.Bank==null||now<state.EventAt||!FaunaAudioOptions.Current.Enabled||!FaunaAudioOptions.Current.AnimalsEnabled)return;
        if(!wildlifeSounds.Has(state.Bank,cue))cue="alert";
        if(!wildlifeSounds.Has(state.Bank,cue))return;
        state.EventAt=now+(cue=="pain"?1.6:7);state.Next=Math.Max(state.Next,now+15);
        QueueAnimalVoice(animal,state.Bank,cue,cue=="pain"?65:80,false);
    }
    bool QueueAnimalVoice(Animal animal,string bank,string cue,int priority,bool flight)
    {
        var state=AnimalVoice(animal);var list=wildlifeSounds.Compatible(bank,cue,(uint)animal.Spawn.Region^(uint)sourceWorld.Recipe.Seed);
        if(list.Count==0)return false;
        int choice=(int)(Rng.Mix(animal.Spawn.Hash+(uint)state.Sequence*7919u)%(uint)list.Count);
        if(list.Count>1&&list[choice].File==state.LastFile)choice=(choice+1)%list.Count;
        CreatureVoice selected=null;
        foreach(var voice in creatureVoices)
        {
            if(voice.Actor==animal&&(voice.Source.isPlaying||voice.Pending!=null))
            {if(voice.Priority<=priority)return false;selected=voice;break;}
            if(selected==null&&!voice.Source.isPlaying&&voice.Pending==null)selected=voice;
        }
        if(selected==null)selected=creatureVoices.Where(v=>v.Priority>priority).OrderByDescending(v=>v.Priority).FirstOrDefault();
        if(selected==null)return false;
        state.Sequence++;state.LastFile=list[choice].File;selected.Source.Stop();selected.Source.clip=null;selected.Source.volume=0;selected.Actor=animal;selected.Pending=list[choice];selected.Expires=Time.unscaledTimeAsDouble+3;
        selected.Priority=priority;selected.Flight=flight;selected.Level=flight?.35f:cue=="contact"?.55f:cue=="ambient"?.65f:.9f;WildlifeClip(selected.Pending,priority<100);return true;
    }
    void DisposeCreatureAudio()
    {foreach(var voice in creatureVoices){voice.Source.Stop();Destroy(voice.Source.gameObject);}creatureVoices.Clear();creatureVoiceStates.Clear();modeledCallers.Clear();}
}
