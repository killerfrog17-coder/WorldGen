using System;
using UnityEngine;
using WorldGen.Core;

/// <summary>Art-only bindings. Prefabs use a ground-root pivot; source instance dimensions are in metres.</summary>
[CreateAssetMenu(menuName="WorldGen/Local terrain art set")]
public sealed class LocalTerrainArtSet : ScriptableObject
{
    [Serializable] public sealed class Binding
    {
        public ScatterKind Kind;
        public GameObject Near,Middle,Far;
        [Tooltip("Zero measures the prefab bounds. Positive values describe its authored, unscaled dimensions in metres.")]
        public float ReferenceHeightM,ReferenceWidthM;
    }
    public Binding[] Scatter=Array.Empty<Binding>();
    [Tooltip("Optional replacement URP terrain material. The default accepts UV1 herb/soil/rock/snow weights.")]
    public Material GroundMaterial;
    [Tooltip("Four equally sized layers: herb, soil/litter, rock, snow. Albedo is sRGB; normals and masks are linear.")]
    public Texture2DArray Albedo,Normals,Masks;
    [Tooltip("Mask channels: R occlusion, G roughness, B height (reserved), A metallic.")]
    public float TextureRepeatMetres=2;
}
