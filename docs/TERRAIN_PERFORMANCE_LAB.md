# Terrain performance lab — 22 October 2026

Game settings → Terrain lab adds session-only comparisons for the mountain FPS
investigation. These controls deliberately change rendering to isolate costs;
they are not proposed final quality settings or claims of a diagnosed bottleneck.
Normal settings and saved worlds are not written by the lab. Restore normal
rendering clears all lab overrides; leaving exploration also resets them.

## Comparing from the same position

1. Stop at the problematic view. Use Display to remove VSync/frame caps if those
   would hide a difference. Wait until terrain and tree preparation finish.
2. Open Terrain lab and pin the reference FPS. Change one switch, resume, and
   wait a few seconds for the averaged FPS to settle. Keep the same view.
3. Restore normal rendering between experiments. Frame rates with several
   switches enabled cannot be added together to estimate separate costs.

Start with Flat vertex colour. This bypasses ground material sampling and
lighting while preserving land geometry and ordinary coverage/fades. If that
helps substantially, compare the separate texture, lighting, shadow and overlay
switches. Disable the flat override first: it supersedes those individual tests.
Weather postprocessing and vegetation keep drawing unless explicitly hidden.

## Available experiments

- Ground colour textures, normal maps, and AO/roughness/metal masks separately.
- All surface texture reads together (not geometry coverage or shadow maps).
- One texture projection versus triplanar projection; one dominant ground layer
  versus soil/lithology blending.
- Detailed textures only within the protected full-detail radius versus850m.
- Procedural ground grain; simple lighting versus PBR; unlit materials.
- Terrain shadow receiving versus the entire sunlight shadow-map workload.
- Grass/moss/lichen ground-material overlays individually; distant shrub/forest
  tint separately. These do not remove physical vegetation.
- Terrain air/fog and the separate weather post-effect.
- Land/seabed, water, all scatter/vegetation, local+distant trees, or grass draws.
  Draw-only switches retain prepared data; hiding land also changes depth and
  shadow coverage, so the work of other layers can change.
- Land back-face culling; coverage/fade clipping bypass. The latter can expose
  overlapping fallback products and is an intentionally diagnostic image change.
- Distance-only LOD ignoring the regional error estimate; extra proximity
  refinement off while retaining the protected radius; finest grids64/32/16/8.
  Geometry switches replan/reload meshes, not canonical heights or saved worlds.
- Hold terrain planning/uploads/maintenance; hold plant preparation independently
  of drawing. Use while stationary after loading. In-flight tasks may finish;
  these switches do not forcibly abort jobs or hold the simulation calendar.

The menu reports cached resident vertex/triangle totals (including water and
previous products during fades), finest tiles entirely outside the protected
radius, requested/resident tile counts and pending work. These are not a camera
visible census, GPU timings or a frame-profiler substitute. FPS is the existing
half-second Update average, not an independent presented-frame measurement.

## Implementation and validation

The terrain shader reads camera-scoped diagnostic flags. A separate local
WORLDGEN_TERRAIN_LAB variant compiles those branches out of normal rendering;
it is enabled only on terrain materials while a shading experiment is active.
Shadow receiving has a retained local shader variant. Existing per-tile SRP
material buffers and the shared304-byte scene constants remain. The lab's LOD
options default to the prior planner and are applied only to copied presentation
options, never saved quality preferences. Mesh/readiness logic continues to use
the requested diagnostic tessellation, and coverage/edge rules remain enabled
unless the coverage experiment is selected explicitly.

The focused synthetic planner suite passes1,382 checks, including estimator
bypass, retained protected coverage, reduced diagnostic geometry and matching
stitching for all three coarser grids. No world was loaded by that suite.
The isolated Editor/D3D11 review passes65 surface/state checks. Normal shader
outputs match the pre-lab shader exactly at100/700/2000m on the synthetic
surface; each flag is exercised and restoration is checked. Actual cached-tile
visibility, shadow receiving, sunlight-shadow restoration and land-only culling
also pass. This is not an actual-world scene or menu-layout acceptance. The
fixture disables asynchronous shader compilation and environmental reflections
to prevent capture drift; those changes are confined to its Editor process.
Native package/compiler validation and any remaining limits are recorded in
VALIDATION.md. No FPS improvement or mountain diagnosis is claimed by adding
these switches.
