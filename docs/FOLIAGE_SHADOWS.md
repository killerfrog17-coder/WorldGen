# Foliage shadow and near-foot density checkpoint / 22 October 2026

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

**Simplified forest shadows** defaults on; switch it off for the previous
casters/filter/range. Separate Very low/Low/Medium/High/Ultra coverage is
60/90/140/180/220 m, default Medium. Softness 0.75–4 (default 2) scales the
foliage filter footprint, not its sample count. F3 full view reports mode,
coverage and submitted proxy casters/draws. Existing tree and vegetation shadow
toggles, and Terrain lab's global sunlight-shadow switch still apply.

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

The new camera-scoped `WorldGenFoliageShadows.hlsl` uses four comparisons for
grass, tree/forest surfaces and terrain ground-cover overlays, with an early
out beyond the shadow fade/frustum. Terrain's main PBR lighting and ordinary
object/herb/bush shaders retain their stock lighting/filter path. Wider filtering
and coarser casters approximate canopy shade, not accurate moving leaf outlines.

The coverage setting changes the **existing pipeline's directional shadow
distance**, not a second foliage-only atlas: other objects' shadow coverage also
extends, while their shader/filter stays unchanged. The original distance is
restored on mode-off/exit, without overwriting a different external writer's
last change. Scene globals avoid per-tile material updates. Shadow distance,
mode, density and softness are machine-local preferences saved only by the UI.

This is not baked/cached sunlight, a new ambient occlusion system, contact
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
acceptance. Compare the new shadow toggle at the same view and range, then
adjust coverage separately; extending from the old 50 m pipeline distance can
offset savings. Density 20x is explicitly an expensive experiment.
