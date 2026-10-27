using System;
using UnityEngine;

/// <summary>Offline MPFB skins and fitted public-domain motion, with native rigs.</summary>
public sealed class WorldGenHumanLibrary : ScriptableObject
{
    public HumanRuntimeDefinition[] People;
}

[Serializable] public sealed class HumanRuntimeDefinition
{
    public string Variant,SourceSha256;
    public GameObject Prefab;
    public float EyeHeight;
    public AnimationClip[] Clips;
    public string[] Roles;
}
