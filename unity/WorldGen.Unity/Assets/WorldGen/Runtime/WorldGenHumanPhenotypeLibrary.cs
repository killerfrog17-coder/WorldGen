using System;
using UnityEngine;

public sealed class WorldGenHumanPhenotypeLibrary : ScriptableObject
{
    public HumanPhenotypeMesh[] Meshes=Array.Empty<HumanPhenotypeMesh>();
    public HumanPigmentReference[] Pigments=Array.Empty<HumanPigmentReference>();
    public HumanGroomedMesh[] Hair=Array.Empty<HumanGroomedMesh>();
}
[Serializable] public sealed class HumanGroomedMesh
{
    public string Variant,Part;public WorldGen.Core.HumanHairStyle Style;public int Curl;
    public HumanMorphSurface Surface;public Material Material;
}
[Serializable] public sealed class HumanPigmentReference
{
    public Texture Texture;public float Luminance=.15f;
}
[Serializable] public sealed class HumanPhenotypeMesh
{
    public string Variant,Part;
    public Mesh Original;
    public HumanMorphSurface Full,Middle,Far;
}
[Serializable] public sealed class HumanMorphSurface
{
    public Mesh Mesh;public HumanMorphDelta[] Frames=Array.Empty<HumanMorphDelta>();
}
[Serializable] public sealed class HumanMorphDelta
{
    public string Target;public int[] Vertices=Array.Empty<int>();
    public Vector3[] Positions=Array.Empty<Vector3>(),Normals=Array.Empty<Vector3>();
}
