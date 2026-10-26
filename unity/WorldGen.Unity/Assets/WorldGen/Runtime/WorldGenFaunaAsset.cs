using System;
using UnityEngine;

/// <summary>Offline-exported original skinning palettes, not a newly retargeted rig.</summary>
[PreferBinarySerialization]
public sealed class WorldGenFaunaAsset : ScriptableObject
{
    public Mesh Mesh;
    public Texture2DArray Coats;
    public Texture2D Mask,Normal;
    public string Slug,SourceSha256;
    public int BoneCount;
    public float Height;
    public Matrix4x4[] Poses;
    public float[] Morphs;
    public FaunaRuntimeClip[] Clips;
    public FaunaRuntimeCoat[] Variations;
}

[Serializable] public sealed class FaunaRuntimeClip
{
    public string name,role;
    public int start,frames;
    public float duration;
}
[Serializable] public sealed class FaunaRuntimeCoat
{
    public string label;
    public FaunaRuntimeParts[] options;
}
[Serializable] public sealed class FaunaRuntimeParts
{
    public string label;
    public int[] parts;
}
