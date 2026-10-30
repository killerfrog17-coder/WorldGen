using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldGen.Core;

public sealed partial class LocalTerrainExplorer
{
    readonly Dictionary<string,AudioClip[]> itemEffectBanks=new Dictionary<string,AudioClip[]>();readonly List<AudioSource> itemEffectVoices=new List<AudioSource>();
    uint itemEffectChoice=281028;static float itemEffectVolume=-1;
    public static float ItemEffectVolume{get{if(itemEffectVolume<0)itemEffectVolume=PlayerPrefs.GetFloat("item-effects-volume",.7f);return itemEffectVolume;}set{itemEffectVolume=Mathf.Clamp01(value);PlayerPrefs.SetFloat("item-effects-volume",itemEffectVolume);}}
    public void PreviewItemEffect(string category,int take)=>PlayItemEffect(category,view.transform.position,.6f,true,take);
    void PlayItemEffect(string category,Vector3 position,float volume=.5f,bool owner=false,int variant=-1)
    {
        if(!owner)ReportSleepNoise(position,volume*2);
        if(category=="stone-contact"||category=="wood-chop"||category=="body-hit")ReportHuntingNoise(position,volume*2);
        if(ItemEffectVolume<=0||!owner&&(position-view.transform.position).sqrMagnitude>65*65)return;
        if(!itemEffectBanks.TryGetValue(category,out var clips))
        {
            clips=category=="stone-contact"?Resources.LoadAll<AudioClip>("Crafting/Stone"):Resources.LoadAll<AudioClip>("Items/Audio").Where(c=>c.name.StartsWith(category+"-",StringComparison.Ordinal)).OrderBy(c=>c.name).ToArray();itemEffectBanks[category]=clips;
        }
        if(clips.Length==0)return;var voice=itemEffectVoices.FirstOrDefault(v=>!v.isPlaying);
        if(voice==null){var root=new GameObject("Item sound");root.transform.SetParent(transform,false);voice=root.AddComponent<AudioSource>();voice.playOnAwake=false;voice.loop=false;voice.dopplerLevel=0;voice.rolloffMode=AudioRolloffMode.Logarithmic;voice.minDistance=1.5f;voice.maxDistance=65;voice.priority=125;itemEffectVoices.Add(voice);}
        itemEffectChoice=Rng.Mix(itemEffectChoice+1);voice.transform.position=position;voice.spatialBlend=owner?0:1;voice.panStereo=0;voice.volume=volume*ItemEffectVolume;voice.pitch=variant>=0?1:.96f+(itemEffectChoice%9)*.01f;voice.clip=clips[variant>=0?variant%clips.Length:(int)(itemEffectChoice%(uint)clips.Length)];voice.Play();
    }
    void PlayDroppedImpact(Possession item,Collision collision)
    {
        if(item==null||collision.contactCount==0||collision.relativeVelocity.sqrMagnitude<.36f)return;
        string group=StoneCrafting.Rock(item)||item.Kind==PossessionKind.StoneFlake?"stone-contact":PortableTechnology.Item(item.Kind)?.Form==PortableForm.Hide?"fibre-handle":"wood-chop";
        PlayItemEffect(group,collision.GetContact(0).point,Mathf.Clamp((float)Math.Sqrt(item.MassKg)*collision.relativeVelocity.magnitude*.1f,.08f,.5f));
    }
    void DisposeItemAudio(){foreach(var voice in itemEffectVoices)if(voice!=null){voice.Stop();Destroy(voice.gameObject);}itemEffectVoices.Clear();itemEffectBanks.Clear();}
}
