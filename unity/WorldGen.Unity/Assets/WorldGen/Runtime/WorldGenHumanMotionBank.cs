using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Generated offline; the original character skins and legacy clips stay intact.</summary>
public sealed class WorldGenHumanMotionBank : ScriptableObject
{
    public KimodoPersonMotion[] People;
    public KimodoPersonMotion Find(string variant)=>Array.Find(People,p=>p.Variant==variant);
}

[Serializable] public sealed class KimodoPersonMotion
{
    public string Variant;
    public float EyeForward;
    public KimodoHumanClip[] Clips;
}

[Serializable] public sealed class KimodoHumanClip
{
    public string Role;
    public AnimationClip Clip;
    public bool Loop;
    public float Contact=-1,Release=-1,TravelMeters;
    public double Duration=>Math.Max(.01,Clip==null?1:Clip.length);
}

public static class HumanAnimationOptions
{
    public static bool Kimodo=true;
    public static void Load()=>Kimodo=PlayerPrefs.GetInt("human-kimodo-animation",1)!=0;
    public static void Set(bool enabled)
    {Kimodo=enabled;PlayerPrefs.SetInt("human-kimodo-animation",enabled?1:0);PlayerPrefs.Save();}
}
