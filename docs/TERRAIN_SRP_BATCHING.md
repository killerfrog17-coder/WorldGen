# Terrain SRP Batcher material path /22 October 2026 UTC

This is a steady-state CPU draw-setup change, not generation, loading, terrain
geometry, LOD selection, water simulation or vegetation work. Pipeline batching
was already enabled. Both previously incompatible components are addressed.

## Binding and ownership

- WorldGenLocalTerrain.shader includes one UnityPerMaterial constant buffer
  containing its numeric material properties and texture-size metadata. Former
  non-serialized coordinate/air values have explicit hidden property declarations;
  merely moving undeclared values into the buffer is not compatible.
- Each installed terrain product owns persistent land/water material instances.
  They share the existing textures; no texture atlas copy is introduced. Tile
  flora/mid/forest textures, coverage trees/timings, size, safety bubble and LOD
  fade parameters bind directly to those materials. Terrain renderers receive
  no property blocks; factory clearing uses SetPropertyBlock(null) once.
- A cached binding stamp skips unchanged material bindings, even when an unrelated
  resident coverage event runs. Fade timestamps, not per-frame fade weights, are
  stored; the existing shader clock/dither computes transitions unchanged. The
  outgoing same-key product uses incoming coverage/flora and reverse fade as
  before. Cached products retain their materials; DestroyTile releases them.

## Scene state without per-tile churn

LocalTerrainMaterials.cs publishes one304-byte,19-float4 WorldGenTerrainScene
constant buffer. Fog, daylight/ambient, distant forest flag, floating origin,
planet-oriented texture phase/bases, atmospheric inputs and secondary celestial
lights live there. Sky/fog/origin/setting events publish only changed contents.
The exploration camera binds this existing buffer once when rendering; unchanged
frames do not upload scene contents or rewrite thousands of materials.

ShaderLab Color properties normally convert on material upload in a Linear
project; raw scene-buffer writes explicitly reproduce that conversion. Secondary
light vectors/colors retain their preceding raw-vector interpretation. Land and
water local-origin inputs remain separate, deliberately preserving existing
river animation rather than silently repairing/rephasing it here. Water formulas,
the safety bubble, coverage traversal and LOD fade equations are unchanged.

Shared ground-texture resources and repeat/rock parameters are cloned at install,
not rewritten each frame. Extra Unity material/native constant-buffer objects
are the bounded per-product memory tradeoff; existing mesh-cache byte counters
are not total native material memory. The shipping LocalTerrainArtSet has no
custom GroundMaterial override; arbitrary external shaders require their own
equivalent material/scene contract and are not certified by this verification.

WorldGenAir.hlsl adds a terrain-only input hook. Expanding its default macros
produces the exact previous air/secondary-light function bodies for vegetation;
grass/scatter instancing property blocks and shaders remain on their old path.
No new per-blade/tree work, density change, terrain normals or mesh edits occur.

## Verification and limits

WorldGenTerrainBatchReview is an isolated Editor check with four synthetic tiles
and the production binding methods/shader, not an actual-world or FPS run:

- Unity's native ShaderUtil SRP compatibility result is0 (same API the shader
  Inspector uses, queried after compilation; precompile1 is Not initialized).
- Four distinct retained tile materials, correct independent texture references,
  and no renderer property blocks. Incoming/outgoing binding and coverage masks
  are checked, including a clipped quadrant in the actual comparison render.
- Twenty identical binding/scene publications cause zero additional material
  bindings or scene-buffer uploads. A changed fog value causes one shared upload
  and zero tile rebindings.
- The actual pinned pre-change shader from75a4d5c is compiled in memory as the
  visual reference, using its original shared-material/property-block path.
  Ground/cover/ocean/static-river samples render pixel-identically (max RGB delta0).
- Batch-mode UnityStats draw counters are0 for BOTH old and new paths, including
  all standard/hybrid/SRP categories after separate Editor frames. They cannot
  substantiate actual submission counts here. Compatibility/eligibility and
  appearance are verified; actual-world batching captures and FPS savings are
  NOT measured or inferred. No world/game/save/preferences are opened or changed.

No general terrain GPU optimisation or mountain performance attribution is claimed.

Release IL2CPP packaging succeeded without shader errors; shortcut selects
`artifacts/player-terrain-srp-batched/WorldGen.exe`, BuiltUtc22Oct04:21:50Z.
It includes the short-grass split. No native game or owner save was opened.

## Primary documentation

- [Unity6.6 SRP Batcher compatibility](https://docs.unity.com/en-us/engine/6000.6/manual/analysis/graphics-performance-profiling/in-urp/reduce-draw-calls-urp/srpbatcher/srpbatcher-materials):
  compatible renderer/shader requirements, including no property blocks and the
  UnityPerMaterial layout. Different persistent materials may share a shader path.
- [Unity material-property batching guidance](https://docs.unity.com/en-us/engine/6000.3/manual/analysis/graphics-performance-profiling/reduce-draw-calls/draw-call-batching/properties):
  use materials rather than renderer property blocks for SRP batching.
- [Unity shader Inspector reference](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/Inspector/ShaderInspector.cs)
  and [ShaderUtil API reference](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/ShaderUtil.bindings.cs):
  same compatibility query as the Inspector. Reference APIs only; no Unity source
  or engine implementation is copied into this project.
