using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Networking;
using WorldGen.Core;
using WorldGen.Presentation;

public sealed partial class LocalTerrainExplorer
{
    readonly FootstepCadence stepCadence=new FootstepCadence();
    readonly AudioClip[][] stepBanks=new AudioClip[7][];
    readonly List<AudioClip> stepClips=new List<AudioClip>();
    AudioSource[] stepVoices;Coroutine stepLoading;UnityWebRequest stepRequest;
    double stepX,stepY,stepZ;uint stepChoice=782319;int stepVoice,lastStep=-1;
    public int FootstepPlaybackCount {get;private set;}
    int wetContacts,airFrames,supportedFrames,cadenceContacts;double supportedMetres,largestStep;
    public string FootstepDebug=>"Footfalls "+FootstepPlaybackCount+" · airborne frames "+airFrames+" · supported "+supportedFrames+" / "+supportedMetres+"m · cadence "+cadenceContacts+" · largest delta "+largestStep+" · underwater contacts "+wetContacts+" · grounded "+motion.Grounded+" · flying "+motion.Flying+" · volume "+FootstepVolume;
    public bool FootstepsReady {get;private set;}
    public int FootstepClipCount=>stepClips.Count;
    public bool FootstepsCentered
    {
        get
        {
            if(stepVoices==null)return false;
            foreach(var voice in stepVoices)if(voice.spatialBlend!=0||voice.panStereo!=0||voice.spatialize)return false;
            foreach(var clip in stepClips)if(clip.channels!=1)return false;
            return true;
        }
    }
    public int FootstepVoices=>stepVoices==null?0:(stepVoices[0].isPlaying?1:0)+(stepVoices[1].isPlaying?1:0);
    static float stepVolume=-1;
    public static float FootstepVolume {get{if(stepVolume<0)stepVolume=PlayerPrefs.GetFloat("footstep-volume",.65f);return stepVolume;}set{stepVolume=Mathf.Clamp01(value);PlayerPrefs.SetFloat("footstep-volume",stepVolume);}}
    void InitializeFootsteps()
    {
        stepVoices=new AudioSource[2];for(int i=0;i<2;i++)
        {
            // Own-footstep Foley must remain centered as the listener moves/turns.
            // World-positioned impact tails otherwise drift to one ear.
            var root=new GameObject("Player footfall "+i);root.transform.SetParent(transform,false);
            var voice=root.AddComponent<AudioSource>();voice.playOnAwake=false;voice.loop=false;voice.dopplerLevel=0;
            voice.spatialBlend=0;voice.panStereo=0;voice.spatialize=false;stepVoices[i]=voice;
        }
        stepLoading=StartCoroutine(LoadFootsteps());ResetFootsteps();
    }
    IEnumerator LoadFootsteps()
    {
        string root=Path.Combine(WeatherSoundsRoot(),"footsteps"),manifest=Path.Combine(root,"screened.tsv");var hashes=new Dictionary<string,string>();
        if(File.Exists(manifest))foreach(string line in File.ReadAllLines(manifest)){var f=line.Split('\t');if(f.Length==2)hashes[f[0]]=f[1];}
        string[] groups={"soil","sand","gravel","stone","mud","organic","snow"};
        for(int group=0;group<groups.Length;group++)
        {
            var clips=new List<AudioClip>();for(int i=1;i<=4;i++)
            {
                string name=groups[group]+"-"+i.ToString("D2")+".wav",path=Path.Combine(root,name);
                if(!hashes.TryGetValue(name,out string expected)||!File.Exists(path))continue;
                string actual;using(var stream=File.OpenRead(path))using(var sha=SHA256.Create())actual=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();if(actual!=expected)continue;
                var request=stepRequest=UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri,AudioType.WAV);yield return request.SendWebRequest();
                if(request.result==UnityWebRequest.Result.Success){var clip=DownloadHandlerAudioClip.GetContent(request);if(clip!=null&&clip.length>=.12&&clip.length<=1.2){clip.name=name;clips.Add(clip);stepClips.Add(clip);}else if(clip!=null)Destroy(clip);}
                request.Dispose();stepRequest=null;
            }
            stepBanks[group]=clips.ToArray();
        }
        FootstepsReady=true;foreach(var bank in stepBanks)if(bank.Length==0){FootstepsReady=false;Debug.LogWarning("Footstep bank missing; no synthetic fallback.");break;}stepLoading=null;
    }
    void ResetFootsteps(){stepCadence.Reset();stepX=motion.X;stepY=motion.Y;stepZ=motion.Z;if(stepVoices!=null)foreach(var voice in stepVoices)voice.Stop();}
    void UpdateFootsteps()
    {
        double dx=motion.X-stepX,dy=motion.Y-stepY,dz=motion.Z-stepZ;stepX=motion.X;stepY=motion.Y;stepZ=motion.Z;
        bool suspended=paused||mapOpen||chatOpen||motion.Flying,active=!suspended&&motion.Grounded;
        if(suspended)stepCadence.Reset();
        if(!motion.Grounded)airFrames++;
        // A past impact may finish naturally after takeoff. Cutting it on every
        // tiny contact gap causes crackling; only context changes silence voices.
        if(suspended&&stepVoices!=null)foreach(var voice in stepVoices)voice.Stop();
        var keyboard=ActiveKeyboard;var gait=keyboard!=null&&keyboard.cKey.isPressed?TravelGait.Walk:keyboard!=null&&keyboard.shiftKey.isPressed?TravelGait.Sprint:TravelGait.Jog;
        double travelled=Math.Sqrt(dx*dx+dy*dy+dz*dz);if(active){supportedFrames++;supportedMetres+=travelled;largestStep=Math.Max(largestStep,travelled);}
        if(!stepCadence.Advance(travelled,active,gait))return;cadenceContacts++;
        if(!FootstepsReady||FootstepVolume<=0)return;
        var sample=walkingPatch.Sample(Position);if(sample.Underwater){wetContacts++;return;}var surface=GroundContact();
        stepChoice=Rng.Mix(stepChoice+0x9e3779b9u);double roll=stepChoice/4294967296d,total=0;int material=0;
        for(int i=0;i<8;i++){total+=surface.Weight(i);if(roll<total){material=i;break;}}
        int bank=material==1?1:material==2||material==6?2:material==4?3:material==5?5:material==7?6:sample.Ground.Drainage<.2?4:0;PlayFootstep(bank,gait);
    }
    void PlayFootstep(int bank,TravelGait gait)
    {
        var clips=stepBanks[bank];if(clips==null||clips.Length==0)return;
        stepChoice=Rng.Mix(stepChoice+0x9e3779b9u);int index=(int)(stepChoice%(uint)clips.Length);if(index==lastStep&&clips.Length>1)index=(index+1)%clips.Length;lastStep=index;
        float materialGain=bank==2?.50f:bank==3||bank==6?.36f:bank==5?.22f:.30f;
        var voice=stepVoices[stepVoice];stepVoice=1-stepVoice;voice.Stop();voice.clip=clips[index];
        voice.volume=materialGain*FootstepVolume*(float)FootstepCadence.Gain(gait)*(.94f+(stepChoice%100)*.0012f);voice.pitch=.98f+(stepChoice%100)*.0004f;voice.Play();FootstepPlaybackCount++;
    }
    public void TestFootstep(int material,TravelGait gait){if(FootstepsReady)PlayFootstep(Math.Max(0,Math.Min(6,material)),gait);}
    public GroundSurface GroundContact()
    {
        var s=walkingPatch.Sample(Position);double gx=Ground(motion.X+.5,motion.Z)-Ground(motion.X-.5,motion.Z),gz=Ground(motion.X,motion.Z+.5)-Ground(motion.X,motion.Z-.5),up=1/Math.Sqrt(1+gx*gx+gz*gz);
        return GroundEnvironment.Surface(s.Ground,Position,sourceWorld.Recipe.Seed,s.ElevationM,up,s.OceanInfluence,s.Biome==Biome.Ice?Math.Max(0,Math.Min(1,(up-.55)/.4)):0,terrain.RadiusM*HorizontalScale);
    }
    public string GroundDebugText {get{var s=GroundContact();var g=s.Profile;return "Ground "+s.Material+" · "+g.TextureName+" · "+g.Bedrock+" · pH "+g.PH.ToString("F1")+" · root depth "+g.DepthM.ToString("F2")+"m · available water "+g.WaterCapacityMm.ToString("F0")+"mm";}}
    void DisposeFootsteps(){if(stepLoading!=null)StopCoroutine(stepLoading);if(stepRequest!=null){stepRequest.Abort();stepRequest.Dispose();}if(stepVoices!=null)foreach(var voice in stepVoices)voice.Stop();foreach(var clip in stepClips)if(clip!=null)Destroy(clip);stepClips.Clear();}
}
