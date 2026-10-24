# Soil and granite surface art — 24 October2026

This first pass replaces placeholder soil and granite detail with generated,
offline-baked material swatches. It uses the existing terrain art-set bindings.
GroundMaterial.Soil is slot0; generic rock is slot4; RockFamily.Granite is slot8.
The other eleven slots retain resampled/compressed versions of their procedural
patterns. This is not a replacement for all six geology families or all ground
materials yet. Existing geology/soil weights and ecological colours still decide
which material appears where.

The built-in imagegen tool generated two square, orthographic, evenly lit
albedo swatches: fine mineral loam and fractured granular granite. Exact prompts
are in `unity/WorldGen.Unity/Assets/WorldGen/Editor/TerrainSurfaceArtV1/prompts.json`.
Masters and repeat previews are ignored in `artifacts/terrain-surface-art-20261024`;
the processed PNGs and published arrays are source assets. Deer inference and
reviews are left to the other conversation.

`scripts/bake-terrain-surface-art.py` removes boundary discontinuities with a
periodic-plus-smooth decomposition, reduces broad baked illumination, and
normalizes linear mean intensity to sRGB0.5 (linear0.21404114). The shader already
multiplies neutral detail into the ecological palette; these grey maps are
intentional. Real-world soil and rock colours remain controlled by that palette.
Two-by-two repeat previews were inspected. Edge-gradient/interior-gradient ratios
are about0.545; this is evidence against a hard edge seam, not invisible repetition.

Normals use conservative periodic luminance-derived relief:1.5mm standard
deviation for soil,3mm for granite at the existing2m repeat. They are approximate
detail normals, not reconstructed or measured geometry/PBR depth. Roughness is
mostly uniform0.92/0.86; masks use near-neutral AO and zero metallic. No mesh
displacement or parallax is added. Original AI lighting/physical correctness is
not guaranteed by these operations.

## Runtime budget

| Shared array | Resolution | Layers | Mips | GPU payload |
| --- | ---: | ---: | ---: | ---: |
| Albedo, sRGB BC7 |512²|14|10|4,893,728 bytes|
| RGB normals, linear BC7 |256²|14|9|1,223,712 bytes|
| Masks, linear BC7 |128²|14|8|306,208 bytes|

Total6,423,648bytes, about6.126MiB, versus roughly3.5MiB for the previous three
128² RGBA32 arrays. All arrays repeat, use trilinear filtering/aniso4, and are
non-readable after upload. This is one shared set, not a copy per terrain tile.
Source PNGs live in an Editor folder and are not runtime texture dependencies.

The existing shader, sampling loops, projection/layer limits, constant buffers,
coverage masks, texture repeat, lighting/shadows and material binding logic are
unchanged. Surface-quality settings still control cost: Medium detail ends90m,
normals25m; Ultra detail160m/normals50m. Distant terrain keeps its existing cheap
path. Terrain geometry, vegetation, world data, settings defaults and saves are
unchanged. `LocalGroundTextures.Create` only gains an optional editor-readable
argument, defaultfalse; its procedural fallback remains available.

Higher-resolution compressed textures can still change cache/bandwidth costs.
Identical shader operation counts do not prove identical FPS. No new measured
FPS claim or runtime SRP capture is made.

## Reproduction and validation

1. Keep the generated masters as `soil-source.png` and `granite-source.png` in
   the artifact root, alongside its prompt file.
2. Run `python scripts/bake-terrain-surface-art.py --root artifacts/terrain-surface-art-20261024 --output unity/WorldGen.Unity/Assets/WorldGen/Editor/TerrainSurfaceArtV1`.
3. Run Unity in batchmode/nographics with `-executeMethod TerrainSurfaceArtBuilder.Build`.
   It copies the procedural fallback's other slots, compresses all arrays with
   Unity's BC7 encoder, includes every mip and updates LocalTerrainArtSet.
4. Package with `scripts/unity-check.ps1 -BuildPlayer -Il2Cpp -PlayerFolder player-terrain-surface-art-v1`.

The native editor asset build exits0. Serialized arrays confirm resolutions,
layer/mip counts, payload sizes, sRGB/linear formats and readability disabled.
C# compilation has no errors; existing unrelated obsolete/unused-field warnings
remain. Release IL2CPP compile/package exits0, BuiltUtc2026-10-24T07:00:13Z,
with no shader/C# errors. The existing game shortcut selects
`artifacts/player-terrain-surface-art-v1/WorldGen.exe`; prior fauna-ambience player
is retained. Build-created explicit default entries in ProjectSettings and
unrelated importer whitespace were restored; project preferences stay unchanged.
Owner testing restrictions remain compile/package-only: no suites, fixtures,
game/world launches, gameplay captures, sound audition or FPS profiling. Owner
appearance and performance review are required before broader material expansion.

References: [Unity raw array upload](https://docs.unity.com/en-us/engine/6000.5/script-reference/unityengine/texture2darray/setpixeldata),
[Microsoft BC7 format](https://learn.microsoft.com/en-us/windows/uwp/graphics-concepts/bc7-format),
[existing distance budget](TERRAIN_SURFACE_QUALITY.md).
