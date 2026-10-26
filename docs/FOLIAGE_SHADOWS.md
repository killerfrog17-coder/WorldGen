# Foliage shadow and near-foot density checkpoint / 22 October 2026

## Dense forest render LOD follow-up / 26 October 2026

Two separately saved Vegetation controls default on: **Cheaper distant tree
shadow cards** and **Cheaper distant tree shading**. Both can be disabled for
an owner A/B comparison. Population, placement, shadow distance/cascades and
sun motion remain unchanged; the stable loading-fog fix is retained.

Shadow proxies are retained nearby, then crossfade over 64–96 m measured from
the existing conservative tree bounds. Beyond that interval a tree can cast
from one two-triangle card facing the light. It samples three alpha captures
from an existing shared tree bake, without colour/normal/depth reconstruction.
The old proxy remains until that bake is ready; a missing texture never removes
the tree's shadow. Near/far shadow fragments use complementary ordered coverage
through the overlap. Both use the existing main-light atlas, not a new shadow
camera. Borrowed bake models are pinned against eviction while used by shadow
batches; card materials do not own those textures. F3 reports card instances and
submitted caster triangles before any cascade repetition. Cards approximate
light-space depth/self-shadowing and require visual review.

Visible impostors share one sampling implementation between CPU-instanced trees
and GPU forest fields. Close impostors retain three-view colour/normal/depth
reconstruction (normally 12 texture samples). Beyond quality-dependent ranges
they blend to three colour/alpha samples and smooth crown-envelope normals;
captures only a few pixels across can blend further to one colour/alpha sample.
Very low/Low/Medium/High/Ultra start this distance transition at 80/100/120/160/
220 m and finish at 1.5 times that distance. One-view projected-diameter starts
at 12/10.25/8.5/6.75/5 pixels and finishes at half those diameters. Mixed transition
bands evaluate both representations; these are steady-path sample reductions,
not a guaranteed scene-wide FPS improvement. Detailed lighting, alpha clipping,
canopy shade, shadows and opaque distance fog still apply.

Middle-distance palm and tree-fern leaflets retain the same roots/tips/counts,
but use two-triangle flat kites instead of folded strips. Fan palm middle meshes
drop 3312→1062 triangles; feather palms 6372→2052; tree ferns 5116→1684 (67–68%).
Mesh-only Unity inspection of 256 specimens verifies every close-mesh vertex,
normal, index and UV1 signature is unchanged. No scenes/worlds/rendering were
run for that inspection. All 658 pure rendering-policy checks pass. Release
IL2CPP `artifacts/player-forest-render-lod`, BuiltUtc
`2026-10-26T21:04:14.3021051Z`, compiled/packaged with no C#/shader errors.
128 runtime/shader source hashes and staged shared DLLs remained stable; all177
packaged fauna bundles and177 native animal hashes match. Existing shortcut
selects this package. No game launch/capture/profiling; shadow transitions,
material-property instancing, visual compromises and FPS remain owner-review
pending. Sun timing and shadow-map caching were not changed in this render pass.

## Owner controls

Game settings > Vegetation keeps the five **Grass quality** presets and adds
an independent **Near-foot grass density** slider. `1x` adds nothing; default
`2x` preserves the former maximum boost; `20x` is ten times that former total.
The full multiplier applies through 5 horizontal metres, then smoothstep falls
to the ordinary field by 10 m. The former fixed boost covered 4–14 m. Grass
quality changes neither the new setting nor the surrounding field's density.
Existing ecological occupancy still limits grass; this does not grow desert
grass, move roots, change habitats or add the proposed near-foot ground mesh.

A sixth compact GPU index list selects nearby roots, and a separate indirect
draw generates disjoint, stable blade IDs. Slider changes update draw capacity
and invalidate cached culling, not terrain meshes or root placement. Extra
capacity is NOT submitted over the entire close-detail radius. Ordinary roots
remain unchanged while individual extra blades retire fractionally. One extra
4-byte index slot per candidate is retained (120 vs 116 accounted bytes/root,
plus one indirect command); high density increases vertex and fill cost and is
not free. The previous short-blade split and accepted wind remain active.

**Simplified forest shadows** defaults on; switch it off for the original
casters/filter/pipeline settings. All presets now share four configured cascade
ends at16/48/100/180m, with a wider final fade. Very low/Low/Medium/High/Ultra
use atlas sizes1024/1024/2048/4096/4096 and foliage filter counts2/4/4/4/9.
Four cascades share the atlas: individual tiles are approximately half its width.
Default is Medium. Quality no longer dilutes near resolution by expanding range.
Softness0.75–4/default2 scales filter footprint; Ultra uses a nine-comparison tent
filter. F3 reports atlas size/filter count, casters/draws and canopy-cache state.
Existing tree/vegetation toggles and the lab sunlight-shadow switch still apply.

**Broad distant canopy shade** is separately switchable, default on. It supplies
approximate shelter past the detailed shadow range; see the bounded coverage
and approximation limits below. Previous60/90/140/180/220m presets are historical.

## Research and choice

- [CryEngine: Shadow Proxies](https://www.cryengine.com/docs/static/engines/cryengine-3/categories/1114113/pages/21268752)
  documents simplified geometry used exclusively for shadow casting and warns
  that alignment matters for self-shadowing. This supports independently
  simplifying a tree's caster, rather than tying shade to its visible leaf LOD.
- [Epic: Fortnite Chapter 4 shadow engineering](https://www.unrealengine.com/tech-blog/virtual-shadow-maps-in-fortnite-battle-royale-chapter-4?lang=en-US)
  describes shipped separate tree shadow proxies, plus difficulties caching
  shadows with moving foliage and a changing sun. Its Nanite/VSM-specific
  findings are not a performance guarantee for our conventional URP renderer.
  We adopt separate casters, not VSM, Nanite or page-cache update throttling.
- [NVIDIA/Crytek: Vegetation Procedural Animation and Shading in Crysis](https://developer.nvidia.com/gpugems/gpugems3/part-iii-rendering/chapter-16-vegetation-procedural-animation-and-shading-crysis)
  discusses vegetation fill/shading cost and inexpensive grass lighting. Our
  first receiving-side change reduces repeated shadow filtering; it does not
  claim Crysis's exact lighting or move all lighting to vertices.
- [Unity: Screen Space Shadows](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/renderer-feature-screen-space-shadows.html)
  explains resolving main shadows to a screen texture, with an extra depth
  prepass and memory cost. This might later reduce grass overdraw's repeated
  lookups, but is deliberately NOT enabled blindly on this procedural renderer.
- [Unity: Cascade configuration](https://docs.unity.com/en-us/engine/6000.0/manual/lighting-overview/shadows/shadow-realtime/shadow-cascades/use)
  and [resolution budgets](https://docs.unity.com/en-us/engine/6000.3/manual/lighting-overview/lighting/shadows-in-urp/shadow-resolution-urp)
  support separating near/far coverage allocation from atlas resolution.
- [CryEngine: Height-map-based outdoor occlusion](https://www.cryengine.com/docs/static/engines/cryengine-3/categories/1114113/pages/21890652)
  documents a broad, approximate layer complementing detailed lighting. Our
  crown-coverage field is an independent, simpler engineering approximation,
  NOT CryEngine's algorithm, a directional shadow map or a full AO solution.

Bundled URP 17.6 `Shadows.hlsl` was inspected directly: its low/medium/high soft
filters use 4/9/16 comparison samples. The current pipeline selects high soft
shadows, four cascades and a 2048 atlas. Simplifying casters and receiver filters
addresses different costs; neither makes ordinary foliage shading/overdraw free.

## Implementation and boundaries

`LocalFoliageShadows.cs` derives an opaque, texture-free proxy from the shared
tree's actual middle model foliage bounds. At most 64 occupied spatial bins
become eight-triangle volumes, plus a coarse trunk. Frond/needle/broad-crown
families follow their actual generated envelope; leaf-mask-free forms use a
coarse source-envelope fallback. The proxy uses the **existing tree matrix and
admitted root**, never an independently guessed forest population.

Meshes are shared by render code, capped at 96 resident types; at most one new
type is built per scatter-batch update. Existing casting remains until its proxy
exists. Reused batches contain up to 256 matrices. They draw ShadowsOnly with
a Resources material explicitly retaining instancing variants. Normal visible
tree models stop casting when their proxy is available. The proxy does not
crossfade/disappear when a visible tree changes to its baked view. Existing
conservative caster bounds plus light-space receiving-frustum bounds retain
offscreen trees that can shade the view and reject irrelevant groups first.

Camera-scoped `WorldGenFoliageShadows.hlsl` uses the selected2/4/9 comparisons
for grass, tree/forest surfaces and terrain ground-cover overlays, with an early
out beyond the shadow fade/frustum. Terrain and ordinary object/herb/bush PBR
retain the stock BRDF/GI/additional-light/filter path; a narrowly scoped main-light
hook adds broad canopy visibility without duplicating their lighting evaluation.
`WorldGenLighting.hlsl` includes bundled URP RealtimeLights before temporarily
wrapping GetMainLight during Lighting.hlsl inclusion. This relies on reviewed
URP17.6 include/call structure and needs review on package upgrades. No URP
package source is edited. Wider filtering/coarse casters are not leaf silhouettes.

The fixed180m coverage changes the **existing pipeline's directional atlas**,
not a second foliage-only atlas: other objects' coverage also extends. Distance,
resolution, four-cascade split/count and final-border settings are restored on
mode-off/exit when they still match this controller's published values. Scene
globals avoid per-tile material updates. Quality/mode/density/softness and canopy
toggle are machine-local preferences saved only by the UI.

## Night-time rough-surface reflections follow the generated sky

The camera-only sky override did not replace the scene default skybox/reflection
environment. Bare terrain and LocalScatter use URP PBR environment specular,
whereas grass/cover use custom ambient lighting. That mismatch is a concrete
source-level suspect for the owner's grey-blue glowing cliffs at night; its
exact screenshot contribution has not been isolated in a running player.

WorldGenLighting now selects URP's constant-environment reflection path and
substitutes `_WorldGenSkyReflection`: the same already-linear generated
SkyFrame.Ambient illumination used for terrain diffuse GI and vegetation. One
global vector is published at the existing10Hz sky update; w enables it, and
exit clears it. Standalone/editor materials fall back to engine environment
colour when unbound. Macros are scoped around bundled Lighting/GI inclusion,
without changing Unity packages, the BRDF, direct lighting or shadow visibility.
This is a cheap spatially uniform approximation for rough terrain/scatter, NOT
a directional sky cubemap, local reflection probe system, SSR or new wet-surface
renderer. It intentionally changes indirect specular in daytime as well as at
night, avoiding a mismatched static cubemap and any reflection-camera captures.
Textures, roughness, geometry, SRP material layout, water's separate shader path,
vegetation generation and all world/save data remain unchanged. No emission was
found or added. Compile/package-only review; owner compares day/night cliffs
and rocks, and retained moonlight. No visual or FPS acceptance follows from build.

## Smooth sunlight and broad canopy shelter revision

Source review found SetDaylight assigning the light rotation only every0.1s.
The first revision interpolated those poses; owner reports worse flutter/flashing
and requests direct position updates instead. CURRENT presentation evaluates the
actual SunDirection/MoonDirection orbit/parallax queries every rendered frame,
using current clock inputs and observer position. No previous/target poses,
history, Slerp or0.1s presentation delay. Full sky/weather, colour and intensity
calculation stays10Hz. MoonDirection is an allocation-free query using the same
orbit/sidereal/observer formulas as the full sample; no extra full sky arrays or
weather queries per frame. Visible sun/moon discs and primary light use the
direct directions; light roll stays continuous instead of switching an up axis
near the zenith. No shadow cache or extra shadow render pass is added.

Terrain lab > Lighting adds session-only Freeze sun/light direction and
brightness. It holds the directional light's rotation/colour/intensity while
sky discs, clock, weather, wind and camera continue. Reset/exit clears it; chart
changes reinitialize the pose in the new frame. It is a diagnostic, not another
shadow technique or a persistent setting. Continued flashing while stationary
with this enabled means moving sunlight alone does not explain the issue;
ambient/weather/wind and shadow aliasing can still vary. No confirmed universal
flutter cure or pixel-equivalence/performance claim. Cascades, shadow filters,
caster meshes and canopy-cache replacement blending remain unchanged by this
follow-up. No actual sunlight-map interpolation/caching is introduced.

`LocalCanopyShade.cs` summarizes existing resident GeneratedTree roots, dimensions
and crown-base traits into a world-anchored256² field covering1536m. It is not
biome-wide darkening or independently guessed tree placement. Optical coverage
and premultiplied mean crown-top/base heights allow bilinear filtering without
height leaks from empty texels. Narrow crowns receive fractional area weights.
Each pixel's height attenuates shelter up through the crown: upper surfaces are
not darkened as if they were the forest floor. Sparse openings retain light.

One incremental main-thread build is bounded to1024 inspected instances and a
target0.6ms raster work/frame, checked between small root batches; a single large
crown/upload can exceed that target. Finalization advances2048 pixels/frame.
No per-pixel CPU terrain queries, tasks/worker permits, new root generation or
per-renderer property updates. Reusable CPU pixels take1MiB; two RGBAHalf GPU
textures take1MiB total before overhead. Upload occurs only after a whole field
finishes. Idle, unchanged fields do no rebuild work; resident root/projection,
population/settings changes or128m anchor shifts refresh after completion with
at least1s spacing. Map-open pauses construction. Old/new fields blend0.5s at
their own origins; borders feather over96m. Floating origins translate valid
field rectangles; full chart changes reject old fields rather than shade the
wrong location. Fresh chart/load coverage requires a newly built field.

Shading blends in90–160m before detailed shadows finish. It limits main-light
visibility rather than multiplying the final image or stacking two black shadows;
terrain, grass, trunks/lower tree foliage, herbs/bushes and rocks share it. It
does not affect unlit diagnostics, shadow-disabled modes or unrelated bake
cameras. Ambient/secondary light remains. Quality-independent broad coverage
extends roughly600–800m around the player, depending on anchor position, from
resident tree sources. This is NOT20km sunlight, directional ray tracing,
building/mountain occlusion, a true AO hemisphere, or accurate lateral shadows
at low sun. Existing horizon canopy colour/representation remains beyond it.
These limits avoid turning a large last cascade into millions of tree draws.
Steady shading makes one canopy texture lookup inside valid coverage, none outside the
field/near90m. Only0.5s replacement blending reads both fields. Grass/shadow
quality changes alone do not regenerate the canopy cache; population changes do.
Proxy upper-face winding was also corrected so shadow-bias normals point outward.

This is not baked/cached sunlight, a full ambient occlusion system, contact
shadows, ray tracing, distance fields, physically computed penumbrae or per-leaf
wind shadows. URP still draws the atlas each frame. Proxies are rigid and coarse;
near self-shadow mismatch, canopy solidity, cascade softness/resolution and
greater-range cost require owner review. No pixel-identical appearance or FPS
gain is promised. Terrain geometry/LOD, water, ecology, placement, world/history,
save/worker schemas and vegetation animation are unchanged.

## Validation policy

Owner's compile/package-only restriction remains: no game/world launches,
fixtures, automated suites, screenshots or profiling. Review shader/material
GUID bindings, compiler results and retained instancing variants before
publishing; these establish build integration, not runtime visual/performance
acceptance. Compare shadow quality and the separate canopy toggle from the
same view. Fixed180m coverage can cost more than the original50m pipeline or
preceding Very-low60m range. Density20x is an expensive experiment.
