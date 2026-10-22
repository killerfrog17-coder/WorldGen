# Performance audit — 22 October 2026

The owner's desktop report is a hypothesis list, not a benchmark. This audit
inspected the installed URP 17.6 sources and current shipping paths. The owner
explicitly authorized tests and profiling for this assignment, superseding the
historical compile-only restriction for this work. Saves are read-only; benchmark
options are in-memory and do not call the game's setting-save methods. Unity's
own session/window registry bookkeeping changes on player launches, so a whole
registry-key hash is NOT claimed unchanged. No speculative registry restore.

## Dispositions

| Item | Finding / action |
| --- | --- |
| A1 | Confirmed unused exploration SSAO publication: custom shaders have neither receiving variants nor AO reads; no volume override was found. A separate exploration renderer excludes SSAO. Camera opaque-texture override AND weather pass Color input removed; real depth and intermediate active-color requirements retained. Menu renderer unchanged. |
| A2 | Unresolved. Root-shared wind arithmetic is repeated, but precomputation adds storage, dispatch/synchronization and must retain canonical microcell identity. No coarse wind approximation or speculative replacement implemented. |
| A3 | Implemented exact final-height query. Amplified walking height retains the same final evaluator/quantization; legacy path retains literal Sample. No dry/map/mesh substitute. |
| A4 | Disabled-grass cleanup runs on state/residency changes. The subsequent forest redesign uses pending cursors and dirty readiness/retirement events. Ready empty fields still suppress coarse trees, but skip hill-visibility queries entirely. Ownership caches have a separate epoch from camera/hill visibility. Terrain transitions remain time-dependent; not every terrain maintenance scan was rewritten. |
| A5 | Exact stationary grass/forest visibility-list reuse; camera/projection/eye, roots/topology, ranges/options, origin and coverage/readiness revisions invalidate it. Forest holes compact only above 256 slots and one-third waste, remapping ownership references. No normal-play GPU readbacks. Grass live-count indirect dispatch remains unproven: an extra dispatch is not automatically cheaper. |
| A6 | Common vegetation lighting/atmosphere reads the existing camera-scoped 304-byte terrain buffer. Species/instance properties and wind stay separate. Runtime materials opt in once; unsupported external materials retain legacy updates. No per-frame loop through all cached vegetation materials. |
| A7 | Identical alpha-coverage-preserving mip preparation moved into the existing bake task. Main thread only uploads prepared levels. Removed redundant tree tangent/bounds work. Missing source meshes/snapshots still require main-thread work: partial, not a complete asynchronous geometry rewrite. |
| B1 | Confirmed capacity arithmetic, NOT measured actual root count. Split blades retain submitted vertices and can increase fill. No density/segment/range reduction disguised as a free optimization. |
| B2 | Confirmed: budget-full far models can retain middle geometry. The subsequent tree overhaul shares canonical family/strategy art instead of multiplying regional/individual topology. Complete fallback remains until bakes are ready. Native working-set/readiness counts are reported in the forest validation. |
| B3 | Confirmed source sampling workload, not guaranteed memory transactions. Three-view depth reprojection retained. A naive pixel-only early-alpha gate FAILED an image comparison and was rejected: parallax changes mip gradients. The conservative alternative requires the entire derivative quad to have zero parallax offsets. Packaged forest fixture RGB delta is zero; whole-scene performance/equivalence is measured separately. Capable devices select an optional SM4.5 local-tree variant; the original SM4 variant is retained for other devices and A/B checks. |
| B4 | GPU Resident Drawer is enabled in the asset; camera occlusion is not. Current terrain shader does not advertise the DOTS-instancing variant that GRD requires: asset enabled is NOT proof of participation. No blind occlusion toggle or terrain reshaping. Final mesh buffers use UInt16 only when all final vertices/indices fit, otherwise UInt32. Thousands of mountain products remain a possible submission/geometry cost; source evidence alone does not isolate it or prove a worthwhile GRD integration. |
| B5 | Clouds/history are intentional separate work. Neither disabled in normal play. Benchmark holds are documented below. |
| C | Unresolved visual diagnosis without a reproducible exact ridge. No smoothing, substrate, normal, geometry or LOD-selection change made by this audit. |

Already addressed before this work: terrain SRP material layout/bindings, cheap
far material branch, cached F3, coverage-footprint fog. These were not reverted.

## Validation and measurements

Native same-Update renders held poses, lights and shader time constant. Original
renderer/SSAO/opaque copy versus exploration cleanup: **max RGB delta 0**.
Legacy per-material vegetation lighting versus shared camera scene: **delta 0**.
Repeated at rainforest, mountain and desert sites (see artifact equivalence logs).
This demonstrates tested image equivalence, not every possible third-party art
material/camera configuration. Installed URP schedules global AO publication with
side effects; it is not safe to assume RenderGraph culls it simply because the
custom shader ignores it.

Exact query tests: 8,044 assertions, physical scales 0.1 and 1, wet samples,
legacy non-amplified path, unit-input validation, literal forest-potential blends
and unchanged source StateHash.
The first native audit attempt failed in the NEW capture harness because a null
output path reached WriteAllBytes; corrected before accepting any measurements.

Offscreen IL2CPP/D3D11 runs, RTX 4060, 1920×1080, High terrain, 60 km view,
153.6 m protected detail, Medium vegetation / grass 30 m close and 300 m field,
clear noon, F3 closed. Calendar/history/music held ONLY by the existing benchmark.
15-second windows; identical saved input. FrameTimingManager returned **zero
valid samples**, so GPU milliseconds are unavailable, not zero. These are manual
URP render throughput measurements, NOT promises of normal presented gameplay FPS.

| Settled terrain-only site | Before FPS | After A1/A3/A4/A6/A7 FPS |
| --- | ---: | ---: |
| Mountain, 1,993 products | 234.10 | 260.40 |
| Desert, 320 products | 453.68 | 509.56 |

Cross-run numbers can include scheduling/background variance; benefits do not
add linearly. Partial mountain idle/travel rows are not settled comparisons.
The after-run rainforest (373 products) measured 420.12 FPS terrain-only but
39.32 FPS with 25,965 submitted trees and other vegetation: forest work remains
substantial. This is NOT evidence that trees alone account for the entire gap.

Evidence: `artifacts/perf-audit-baseline`, `perf-audit-after-checked` and the
separate culling checkpoint. Input snapshot remains byte-identical. Owner game
not launched/stopped/modified. Shader compatibility and exact pixels are verified
separately from unmeasured normal-play frame benefit.

Stationary grass cache: 40 native fresh/cached GPU root-set comparisons passed,
including camera-turn invalidation. Same-scene 8-second A/B throughput was
39.28 FPS forced-fresh versus 41.36 FPS cached, with identical populations.
Separate trace verification measured 38.73 FPS: run variance is material; this
small comparison is NOT a certified 5% general gameplay gain. No live-count
DispatchIndirect buffer or extra wind-precompute dispatch was added speculatively.

The forest stress run then identified a second, measured CPU issue: turning
rebuilt CPU scatter admissions for hundreds of thousands of trees already owned
by distant GPU fields. Tree-only spatial bins now prove whole-bin rejection from
root-distance bounds and complete ready ownership before visiting members. This
does not shrink distances or discard incomplete fallback. A prepared-chart handoff
also retained old spatial-bin bounds while replacing instance matrices; bins now
travel with their prepared frame. Both fixes are validated separately from the
intentional density/art changes in the forest report.

## Forest follow-up measurements

The separately authorized [forest overhaul](FOREST_PLACEMENT_2026_10_22.md)
INTENTIONALLY changes tree density/anatomy. Do not present a before/after across
those changes as identical product quality. Within the completed diagnostic
build (`perf-forest-final-review`, 15 s per row, configuration above):

These rows precede the final opaque-queue correction below; do not combine
their layer-off values with a later build's faster full baseline.

| Scene | Full | No grass | No specialists | No mid plants | No trees | Terrain/sky only |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Dense flat rainforest | 56.4 | 67.3 | 56.7 | 57.2 | 181.4 | 420.0 |
| Hilly rainforest / river cliffs | 73.7 | 85.0 | 77.8 | 84.0 | 138.4 | 220.9 |

Values are FPS throughput, not additive component timings. Removing a layer
also exposes other pixels. Terrain ecological tint/mat shading remains, as with
the corresponding UI geometry toggles. `no-trees` now removes BOTH local and distant trees;
older tests that removed only local models are not equivalent. Diagnostic
`no-far-forest` suppresses far drawing without expanding local fallback (81.3 /
85.1 FPS); the ordinary FarForest checkbox can expand that fallback and is NOT
this pure ablation. `no-local-trees` retains fields (93.2 / 117.4 FPS).

Same-population shader A/B/A/B: flat 56.41 / 44.81 / 56.37 / 44.74 FPS; hilly
73.69 / 71.27 / 73.47 / 70.93. Optimized captures have RGB delta zero. This is
about 26% in the first view and only 3–4% in the second, not a universal gain.
Turning CPU scatter admission fell 16.17 to 3.66 ms in the dense scene and
2.44 to 1.30 ms in the hilly scene. Four-heading native checks preserve visible
counts and pixels. The release package repeats the dense fixed view at 55.36 FPS.

### Long offscreen frames: investigated, not hidden

The faster turning run averages 47.18 FPS but has a 219.30 ms maximum. Release
repetition gives 217.35 ms; the ORIGINAL sampling path also gives 283.94 ms.
These bursts occur outside the tracked CPU explorer/scatter work and are not
explained by the recorded completed-GC count. To distinguish queued offscreen
work from shader cost, diagnostic-only cases synchronously complete each GPU
render with the existing one-pixel barrier:

| Release turning case | FPS | p99 ms | Maximum ms | CPU scatter ms |
| --- | ---: | ---: | ---: | ---: |
| Optimized, synchronous | 41.86 | 26.67 | 31.74 | 4.53 |
| Original sampling, synchronous | 35.24 | 31.83 | 52.01 | 4.62 |
| Literal per-tree admission, synchronous | 27.87 | 39.68 | 50.76 | 16.24 |

This supports an offscreen queue/backpressure explanation, NOT a newly introduced
shader-specific 200 ms hitch. It does not establish normal presented-game pacing.
The barrier deliberately removes CPU/GPU overlap and has readback overhead; it
is NEVER used in normal play. PresentMon was attempted on this release run but
recorded no presents/output from the offscreen camera. GPU timing remains
unavailable, not zero. No unverified device-busy measurements are claimed.

Travel is unsettled: dense release 57.53 FPS /187.03 ms worst; hilly diagnostic
107.25 FPS /31.68 ms worst. Streaming/source mesh preparation and chart projection
can still hitch. Denser actual forests remain expensive: **a universal 100-FPS
floor or 165-FPS general case has NOT been achieved or certified**. Clouds and
normal background history are additional workloads excluded by this benchmark.

### Opaque tree sorting: a separate verified candidate

The forest draw's conservative world bounds are centred on the camera. Unity
uses [RenderParams.worldBounds to cull AND sort the batch](https://docs.unity.com/en-us/engine/6000.5/script-reference/unityengine/graphics/rendermeshinstanced),
not to sort its individual instances. The old queue2000 let distant billboard
pixels be submitted ahead of nearer terrain/structural depth. The correction
keeps all geometry opaque and depth-writing: terrain/structural meshes2000,
individual impostors2001, field impostors2002. No extra depth prepass, geometry,
texture reduction, model removal, range change or per-frame material updates.

In `perf-forest-queue-reviewed`, the completed dense-view A/B/A/B is
74.29 /55.63 /74.16 /55.67 FPS, with max RGB delta0. Thus the changed ordering
is worthwhile in this view (about33%), not just a hypothetical cheaper sort key.
Hilly rainforest repeats 85.20 /79.18 /84.63 /76.40; tundra repeats
94.79 /91.58 /94.59 /92.72. All tested same-pose captures have max RGB delta0.
The tundra run is slower overall than its earlier pre-queue run (125.90), but
the paired old/new ordering comparison still favors the change. Cross-run
differences are not attributed to this optimization. The final layer-ablation
table is recorded below; gains must not be multiplied into older gains as a promise.

### Delivery-renderer layer costs

`perf-forest-delivery`, normal release IL2CPP `player-forest-review-ready`
(BuiltUtc13:40:45Z), same 15-second configuration, all rows settled. This package
contains the final tree geometry, planting, shaders, source identities and queue
ordering. A later loading-only correction includes forest requests in every
dispatcher's effort accounting and supports cancellation/retry; it does not
change these settled render products.

| Scene | Full | No grass | No specialists | No mid plants | No trees | Terrain/sky only |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Dense flat rainforest | 74.6 | 96.4 | 76.1 | 77.5 | 180.5 | 420.8 |
| Hilly rainforest / river cliffs | 82.5 | 94.7 | 83.9 | 89.9 | 137.3 | 217.6 |

All values are nonadditive offscreen FPS throughput. GPU completion is included
in each window's mean; terrain-only/no-tree windows sometimes accumulate long
offscreen queue drains (84–194 ms), so their per-frame distributions are not
normal presented pacing. FrameTimingManager still supplies no valid GPU samples.
Each site also passes exact 128 worker/player tree identities, 512 ownership
cache answers, retained empty-page ownership, and four-heading image comparisons
for the tree-bin admission and opaque queue changes (max RGB delta0). Final
same-view alpha comparisons also remain RGB0. Dense/hilly resident hierarchies
use 418/676 pages and 335,380/113,381 actual-root representatives, respectively;
their baked-view working sets are 72/90.7 MiB before driver overhead. These
counts are resident, not a claim that every representative is visible/drawn.

The denser forest overhaul is NOT an identical-quality comparison with the old
sparse forest. Neither these results nor terrain-only gains certify 100 FPS
everywhere. Distant fill, grass, streaming preparation and chart reprojection
remain measured or source-supported areas for further work; a full terrain GRD
integration still needs its own isolated experiment.

### Publication executable repeat and movement limits

`player-forest-audit-complete`, release IL2CPP BuiltUtc14:25:32Z, completes
`perf-forest-publication` at 67.62 FPS dense /69.01 FPS hilly. Both scenes are
settled and retain the same 418/676 pages, 335,380/113,381 representatives and
5,555/789 submitted local trees as the earlier delivery-renderer run. Thus the
earlier 74.6/82.5 figures are NOT a guaranteed final rate. Cross-run variance is
material; no cause or extra regression percentage is invented from these
unpaired numbers. The final paired repeat is recorded separately below.

The publication executable passes prepared-versus-ordinary matrices and spatial
bins for 9,776 dense-scene /2,096 hilly-scene trees in three alternate charts,
including full chart-limit offsets. It restores all temporary test state before
rendering. Shared forest permit accounting, completed-payload permit release and
sent-socket cancellation checks pass. Tree preparation now fills idle permits
AFTER current, backdrop, prediction and prepared-chart terrain scheduling; it
cannot repeatedly take the last slot before a hidden chart gets scheduled.

Actual walking (`forest-final-handoff-walk`, production code of the preceding
14:11:13Z build, same planting/geometry/shaders): prepared matrices/bins match
exactly; maximum rendered eye-clearance error 0.0131 m, post-handoff1.6505 m,
largest atomic handoff1.531 ms /mesh upload0.893 ms. Capture inspected: sparse
desert, NOT dense-forest walking art acceptance. The separate dense projection
fixture above supplies non-vacuous tree-bin validation.

The unchanged three-crossing 700 m/s flight stress test FAILS before and after
the scheduling-priority correction: zero completed crossings within100 s and
only16–17 prepared products after travelling about69.6 km. Active near terrain
keeps taking capacity; prepared coverage is not ready. Tests use the persisted
Medium terrain/Balanced effort/60 km view/153.6 m protected-detail configuration.
This is an unresolved extreme-speed terrain-streaming limit, not a passing
travel test or evidence that ordinary walking lost ground coverage. No terrain
LOD/planner/height change was made to relax this task's preservation contract.

### Final executable: paired repeat

`perf-forest-publication-paired` completes on the exact release executable
above. All ten rows are settled; the input snapshot remains byte-identical.
Same settings and 15-second windows, alternating new/old/new/old opaque queues:

| Scene | New ordering | Old ordering | New repeat | Old repeat | Terrain/sky only |
| --- | ---: | ---: | ---: | ---: | ---: |
| Dense rainforest | 69.99 | 49.29 | 69.60 | 51.02 | 418.26 |
| Hilly rainforest | 72.00 | 63.26 | 70.35 | 64.66 | 214.64 |

FPS here is offscreen render throughput, not normal presented gameplay FPS.
The paired means improve about 39% and 11% respectively for this ordering
change, with identical populations, settings and tested images (RGB delta0).
This does not multiply into the older gains or attribute every cross-run
difference to code. Optimized p99 frame intervals are 18.35-19.89 ms dense and
17.13-19.24 ms hilly; maxima are 30.20-34.24 and 19.23-22.56 ms. The terrain-only
means include 80.64/108.84 ms final GPU drains; their distributions must not be
advertised as normal presented pacing. GPU timestamp samples remain unavailable.

Packaged tree, forest-cull/shader and final terrain-upload fixtures pass on
this exact executable before publishing the shortcut. The tree fixture draws
eight structural/strategy forms at local and baked levels; local-tree old/new
shader comparisons remain RGB0. Forest checks retain disable/range/face/empty
ownership and density-zero behavior. Index upload checks cover the UInt16/UInt32
boundary and retain all tested terrain/water attributes and controller heights.

The largest next lever is representation/fill, not a claim that remaining CPU
loops alone can deliver 100 FPS. A separately measured canopy-group experiment
should preserve actual occupied footprints and sparse clearings, reduce hidden
overlapping cutout coverage, and retain the individual handoff. Tighter view
bounds or depth-aware group selection also need image/pacing comparisons before
acceptance. No such quality-changing approximation is silently included here.

## Primary implementation references

Version-specific pass decisions use bundled `UniversalRendererRenderGraph.cs`,
`ScreenSpaceAmbientOcclusion.cs`, `SSAOUtils.cs` and `AmbientOcclusion.hlsl`.
[Unity SRP material requirements](https://docs.unity.com/en-us/engine/6000.6/manual/analysis/graphics-performance-profiling/in-urp/reduce-draw-calls-urp/srpbatcher/srpbatcher-materials)
support retaining the terrain batching path, not removing instanced vegetation
properties. [Unity DispatchIndirect](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/ComputeShader.DispatchIndirect.html)
describes the available mechanism, not proof that an extra dispatch is worthwhile.

[Unity conditional shader targets](https://docs.unity.com/en-us/engine/6000.6/manual/materials-and-shaders/shaders/writing-custom/shader-writing/writing-shader-tags/sl-shader-compile-targets)
allow retaining the original local-tree variant while adding fine derivatives
only to the capable-device keyword variant. [Microsoft discard behavior](https://learn.microsoft.com/en-us/windows/win32/direct3dhlsl/discard--sm4---asm-)
explains why killed lanes may still supply derivatives; a per-pixel zero offset
alone is not a proof that neighbouring texture gradients are unchanged.

Installed Core `GPUDriven/SceneProcessors/WorldProcessor.cs` excludes unsupported
materials and documents missing `DOTS_INSTANCING_ON` as its usual shader reason.
The current terrain shader supplies no such variant. [Unity GRD compatibility](https://docs.unity.com/en-us/engine/6000.6/manual/analysis/graphics-performance-profiling/in-urp/reduce-draw-calls-urp/reduce-rendering-work-on-cpu/make-object-compatible-gpu-rendering)
requires that support. [Unity's performance guidance](https://docs.unity.com/en-us/engine/6000.6/manual/analysis/graphics-performance-profiling/in-urp/reduce-draw-calls-urp/reduce-rendering-work-on-cpu/gpu-resident-drawer-performance)
also warns that GRD adds GPU work and benefits repeated meshes/materials most;
unique terrain meshes/per-product textures are not automatically merged. A
proper integration and terrain occlusion remain a separately measured project,
not a purported free camera checkbox fix. Vegetation's explicit indirect/instanced
draws are a different path and were not converted into terrain SRP batching.
