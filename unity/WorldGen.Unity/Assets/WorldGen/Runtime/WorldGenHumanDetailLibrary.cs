using System;
using UnityEngine;

/// <summary>Offline, skin-weight-preserving distance meshes. Originals remain available for close views and clothing.</summary>
public sealed class WorldGenHumanDetailLibrary : ScriptableObject
{
    public HumanDetailMesh[] Meshes=Array.Empty<HumanDetailMesh>();
}
[Serializable] public sealed class HumanDetailMesh
{
    public Mesh Original,Middle,Far;
}
