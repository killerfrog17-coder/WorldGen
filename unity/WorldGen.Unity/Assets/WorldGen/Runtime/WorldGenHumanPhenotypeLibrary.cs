using System;
using UnityEngine;

public sealed class WorldGenHumanPhenotypeLibrary : ScriptableObject
{
    public HumanPhenotypeMesh[] Meshes=Array.Empty<HumanPhenotypeMesh>();
}
[Serializable] public sealed class HumanPhenotypeMesh
{
    public string Variant,Part;
    public Mesh Original,Morphs,Middle,Far;
}
