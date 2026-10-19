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
    [Tooltip("Optional replacement URP terrain material. UV1 soil/sand/gravel/clay; UV2 rock/organic/salt/ice.")]
    public Material GroundMaterial;
    [Tooltip("Eight layers: soil,sand,gravel,clay,rock,organic,salt,ice. Albedo sRGB; normals/masks linear. Old four-layer arrays use fallback textures.")]
    public Texture2DArray Albedo,Normals,Masks;
    [Tooltip("Mask channels: R occlusion, G roughness, B height (reserved), A metallic.")]
    public float TextureRepeatMetres=2;
}
