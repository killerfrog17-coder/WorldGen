# Local scatter — replaceable first-pass art

Owner-authorized on18 October2026: ground materials and natural cover add physical
scale to the local terrain explorer. This is a visual prototype, not final art,
an ecological population simulation, or a promise of Skyrim-level graphics.

19 October geology revision: `local-scatter-preview-3` adds continuous rock
exposure, buried/rock-free deposits, lithology-conditioned stones/boulders and
rare larger outcrops. Six blended rock textures extend the default substrate
arrays without invalidating custom eight-layer sets. See LOCAL_GEOLOGY_STREAMING.md
for model, art and collision limitations. All terrain LOD behaviour was subsequently
restored to the pre-geology20810b6 implementation at the owner's request. Vegetation taxonomy
and placement have not been overhauled in this geology pass.

## Placement and streaming

`WorldGen.Core.LocalScatter`, version `local-scatter-preview-3`, derives stable
planet-addressed instances from the existing `local-terrain-preview-3` surface.
Macro512m and detail64m cube-sphere addresses are independent of Unity chunks,
floating origins and the order of background work. Named project PRNG streams,
fixed candidate strata, ordered keys and explicit size/yaw quantization preserve
reconstruction. Cube-projection area correction avoids dense face centres versus
sparse cube corners. These are approximate nominal spacings, not a Cartesian
grid of equal-area squares on a sphere.

Biome, moisture/aridity, summer warmth, permafrost, soil rockiness, salinity and
locally sampled slope condition cover. Shared low-frequency patches provide
clearings and clumps. Forest, savanna, grassland, desert and treeline differ in
density, tree form, colour and size. Ordinary trees are excluded from ocean beds,
lakes, ice and sufficiently saline ground. Objects have physical metre-scale
dimensions: juvenile/mature trees, shrubs, boulders, small stones and grass clumps.
This is not a native species catalogue, a validated vegetation model, or resolved
wetland/lake/coastal ecology. In particular, coarse land cover remains the source
of habitat; no agricultural clearing, fire history or settlement land use is
invented here.

No world, ecology, saved terrain or history data is changed. The coarse spatial
index is built once; each patch uses bounded local candidates and noise queries,
not a recurring world-cell scan. Scatter and terrain share at most two background
jobs; one scatter completion is projected per frame. Macro cover streams within
1.8km, grass/stones only around100m. Tree geometry switches at85/350m; distant
trees retain their original positions, not a freshly reseeded approximation.
Above160m ground detail is omitted, above2.2km scatter is omitted. Batches cache
matrices and use GPU instancing, at most256 nonuniformly scaled objects per draw.
View-dependent batch rebuilding is bounded by this local cover, not planetary
cell counts. Exit cancels work and destroys owned render assets.

Rendering roots are seated on the resident terrain triangles to avoid floating
against a simplified LOD. This adjusts only disposable presentation transforms;
the shared instance retains its actual sampled terrain height. The prototype
has no tree/rock collision, harvesting, growth or plant persistence/editing.
The walker remains the preceding terrain-height-field controller.

## Upgrading the art without changing placement

`Assets/WorldGen/Resources/LocalTerrainArtSet.asset` is the replaceable art catalogue.
Its default empty bindings select code-native placeholders. A binding may assign
Near/Middle/Far prefabs for each `ScatterKind`; prefab LODGroups, child MeshFilters,
submeshes and URP materials are supported. Use a ground-root pivot. Reference
height/width can be explicit; zero measures prefab bounds. Instance dimensions
are preserved in metres rather than assuming every model was authored at the
same arbitrary scale. Skinned meshes, interactive objects and species-specific
art catalogues remain future work. Custom materials must support instancing;
the default `_Tint` property is optional for replacement shaders.
Enable GPU instancing on the source materials before building, not only on a
runtime clone: otherwise Unity may strip the required instancing variants. The
bundled `LocalScatterMaterial` explicitly retains these variants. Both default
shaders compile the installed URP Forward+ cluster-light-loop variant.

Terrain exposes eight substrate blend channels in UV1/UV2, separate from future grass/litter cover.
The default triplanar URP PBR shader accepts replaceable Texture2DArrays:

| Array | Interpretation |
| --- | --- |
| Albedo | sRGB neutral detail; eight layers of matching dimensions, multiplied by regional mineral/organic color |
| Normals | linear raw RGB tangent normals, not Unity-compressed normal-map packing |
| Masks | linear R ambient occlusion, G roughness, B reserved height, A metallic |

`TextureRepeatMetres` sets the physical repeat scale. A GroundMaterial override
uses UV1(soil,sand,gravel,clay) and UV2(rock,organic,salt,permanent ice); all eight
weights sum to one. Old four-layer custom arrays safely use the new defaults
instead of indexing missing layers. The preceding four-material layout is
superseded by this ground foundation. Scatter preview2 uses the same blended
root depth/water/nutrition/drainage and parent rock tint; it does not add the
planned richer vegetation taxonomy or change accepted macro ecology.

A GroundMaterial override
can replace this simple shader altogether while retaining surface weights.
Planet-oriented phase and modulo anchors keep the material continuous across
floating-origin changes. The default generated128px tileable textures, procedural
tree crowns and grass blades are inexpensive placeholders. They have UVs,
normals, tangents, material/shadow support and separate LOD meshes; they are not
scanned assets, photoreal foliage, a fully reconciled terrain material system,
or a guarantee that arbitrary high-end asset packs will work without adaptation.

The default scatter shader supports albedo, normal, AO/roughness/metallic masks,
alpha clipping, instance tint and light wind. Author-created URP materials and
prefabs are not required to use it. Lighting is local to exploration and its
previous render settings are restored on exit.

## Repeatable review

```
dotnet run --project tests/WorldGen.Core.Tests -c Release -- --local-scatter-only
dotnet run --project tests/WorldGen.Core.Tests -c Release -- --local-terrain-only
WorldGen.exe --worldgen-local-terrain-check <snapshot.world> --worldgen-local-scatter-review --worldgen-output <evidence-folder>
```

The focused checks cover reconstruction, parallel ordering, global tile seams
and poles, cancellation, biome/cold/saline exclusions, physical dimensions, unique
identities, normalized surface blends and complete source-hash preservation.
The optional native review selects open, temperate-forest and boreal sites,
waits for terrain and vegetation streaming, captures ground/overhead views and
records local draw counts/frame timing. Same-camera cover-on/off pixel comparisons
ensure submitted objects really render. It also exercises the existing input,
debug-map teleport and return-to-world checks. Current measured acceptance is
recorded in VALIDATION.md, not inferred from this design.

Implementation references: Unity's
[GPU instancing API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Graphics.RenderMeshInstanced.html)
and [URP Lit material conventions](https://docs.unity.cn/Packages/com.unity.render-pipelines.universal%4017.0/manual/lit-shader.html).
The installed6000.6 API takes an `in RenderParams` argument. No external meshes,
textures, reference implementation or new art licensing dependency was added.
