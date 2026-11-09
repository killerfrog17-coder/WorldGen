using System;
using System.Threading.Tasks;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    Task<float[][]> npcMumbleWork;AudioClip[] npcMumbleClips;
    void MumbleSpeech(SpokenLine line,int previous)
    {
        if(npcModel?.Config.MumbleSpeech!=true||line.Pace.Shown<=previous||campResting||HumanDistance(line.Actor.Planet,Position)>13)return;
        if(npcMumbleClips==null)
        {
            if(npcMumbleWork==null){npcMumbleWork=Task.Run(NpcMumble.Build);return;}
            if(!npcMumbleWork.IsCompleted)return;
            var pcm=npcMumbleWork.GetAwaiter().GetResult();npcMumbleClips=new AudioClip[pcm.Length];
            for(int i=0;i<pcm.Length;i++){var clip=AudioClip.Create("Prototype human mumble "+i,pcm[i].Length,1,NpcMumble.SampleRate,false);clip.SetData(pcm[i],0);npcMumbleClips[i]=clip;}
            npcMumbleWork=null;
        }
        if(Time.unscaledTimeAsDouble<line.NextMumble||line.Pace.Shown-line.LastMumble<3)return;
        line.LastMumble=line.Pace.Shown;line.NextMumble=Time.unscaledTimeAsDouble+.15;
        char c=line.Pace.Available[line.Pace.Shown-1];if(c=='.'||c=='!'||c=='?'||c==','){line.NextMumble+=.15;return;}
        if(line.Voice==null)
        {
            var root=new GameObject("NPC positional mumble");root.transform.SetParent(line.Actor.Root.transform,false);root.transform.localPosition=Vector3.up*(line.Actor.Definition.EyeHeight-.1f);
            line.Voice=root.AddComponent<AudioSource>();line.Voice.playOnAwake=false;line.Voice.spatialBlend=1;line.Voice.rolloffMode=AudioRolloffMode.Linear;line.Voice.minDistance=1.2f;line.Voice.maxDistance=14;line.Voice.dopplerLevel=0;line.Voice.priority=75;
            root.AddComponent<AudioLowPassFilter>().cutoffFrequency=1500;
        }
        uint hash=HumanHash(line.Actor.ResidentId+(line.LastMumble/3)*7919L);line.Voice.volume=ItemEffectVolume*.32f;
        line.Voice.pitch=(line.Actor.Resident.AgeYears<12?1.55f:line.Actor.Resident.Female?1.3f:.88f)+(hash%13)*.012f;
        line.Voice.clip=npcMumbleClips[hash%(uint)npcMumbleClips.Length];line.Voice.Play();
    }
    void RemoveSpeechVoice(SpokenLine line){if(line.Voice!=null)Destroy(line.Voice.gameObject);}
    void DisposeNpcMumble(){if(npcMumbleClips!=null)foreach(var clip in npcMumbleClips)Destroy(clip);npcMumbleClips=null;}
}
