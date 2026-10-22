# Vegetation / terrain rendering performance, 21 October 2026

The owner explicitly authorized native tests, profiling and quality-preserving
optimizations for this pass, superseding the earlier compile-only restriction.
The goal is a repeatable attribution benchmark, not a claim that every possible
scene now sustains 100 FPS or that 165/200 FPS has been guaranteed.

## Protocol and reproduction

`scripts/vegetation-performance.ps1` launches one owned release IL2CPP D3D11
player, refuses to start alongside another WorldGen player, writes a separate
log/evidence directory and verifies the read-only input snapshot SHA256 afterward.
It neither saves the world nor calls the settings persistence APIs. Benchmark
startup skips library and music initialization, holds history/calendar progression,
and uses temporary rendering options. Diagnostic builds use a separate product
name to isolate Unity's own preferences as well.

Example (choose fresh evidence paths):

```powershell
& scripts/unity-check.ps1 -BuildPlayer -Il2Cpp -PerformancePlayer -PlayerFolder player-performance-check
& scripts/vegetation-performance.ps1 -PlayerFolder player-performance-check -Output artifacts/performance-check -Seconds 6
& scripts/summarize-vegetation-performance.ps1 -Evidence artifacts/performance-check
```

`-Sites 'plains,forest-flat'` and `-Cases 'full,no-grass,no-trees'` select subsets;
`-Original` selects retained original CPU paths for comparison. Do not reuse an
existing evidence directory. Machine details, seed, coordinates, raw frame times,
per-layer counts/draws, load completion, comparison checks and summary CSV are
kept with the evidence. Artifacts, screenshots and saves are not source backups.

The camera renders exactly once per frame into a 1920x1080 target, with automatic
camera rendering disabled. This is real URP rendering, **not** `-nographics`.
Normal threaded graphics mode is used; `-batchmode` forced non-threaded D3D11 in
the setup check and is not used for the matrix. Hidden-window update-only samples
and the failed visible-window setup were rejected, not counted as FPS evidence.
Captures must contain non-uniform rendered pixels. Captures, file IO, model
equivalence checks and settings changes are outside measured intervals. The
confirmation pass drains queued GPU work before timing and includes a final
one-pixel GPU readback/drain in elapsed time, so mean FPS counts completed
rendering, not just CPU queueing. Its `gpu_drain_ms` is the final drain duration,
not a per-frame GPU time. Update-interval percentiles are not presentation times.

These are offscreen rendering-throughput measurements, not desktop presentation,
fullscreen, VSync, end-to-end input latency or full historical-simulation results.
The first setup check proved that a hidden normal camera can return a black image
and misleadingly high update FPS. Unity's FrameTimingManager returned no valid
GPU samples in the non-presenting setup: `gpu_ms=0,timing_samples=0` means
**unavailable**, not zero GPU cost. No fabricated GPU breakdown is inferred.

Each baseline matrix interval lasts four seconds after readiness and a four-second
warm-up; initial captures/comparisons add more settling time. This gives approximate
means and percentiles, not long-duration worst-case certification. `full-repeat`
checks drift; terrain refinement may require minutes. Unsettled rows are labelled
and excluded as baseline references. The report uses settled `full-repeat` when
the initial `full` was still loading. Travel intentionally includes streaming.
The confirmation pass additionally waits for plan completion, terrain transitions
and deferred retirement queues. The exploratory pass stopped intentionally after
81 intervals / three sites rather than repeatedly rebuilding expensive terrain
for every quality setting. It is not represented as a completed six-site suite.

Defaults: scale 0.1, clear local noon, High terrain, Balanced loading, 60km view,
153.6m configured full-detail radius, 30m close / 300m total grass, existing
non-grass defaults and densities, no F3 overlay, no history advancement/music.
Biome sites are selected deterministically from one saved procedural world:
plain/savanna, flat rainforest, steep rainforest, mountain, desert and tundra.
The minimum configured full-detail radius is 1.5 base tiles; a genuine 50m terrain
radius is not supported by the current planner, and is not claimed as a test.
The planner's independent tile-size distance floor can retain more fine ground.

Layer removals are separate ablations, not additive costs. Removing trees can
expose more grass/terrain pixels. Draw toggles do not necessarily remove catalogue
generation, worker data or cached model memory. Far Forest off clips its existing
geometry rather than removing its vertex buffers. Quality and distance cases are
explicit visual tradeoffs, not counted as free optimizations.

## Quality-preserving implementation

- Reuse unchanged fog publication, including correct initialization of new plant
  materials; do not resend the same value to every cached material every frame.
- Memoize exact repeated immutable ground queries by tangent-frame identity and
  exact coordinates; this is not approximate height sampling.
- Store each immutable terrain occlusion grid's maximum lower bound once.
- Add conservative 128m vegetation broad-phase bins. Full original per-plant
  frustum, LOD and shadow rules remain. Bins reuse membership across Y-only ground
  reprojection and uniform floating-origin shifts; a new tangent chart rebuilds it.
- Avoid multiplying unit-normalized tree/mid transforms by identity.
- Compact live tint/fade uploads rather than uploading the 256-entry capacity of
  every small batch. Clear property blocks before changing their array length.
  Reused managed lists cost about 8KiB per pooled batch; model density is unchanged.
- Memoize the repeatedly asked near-terrain readiness predicate within a frame,
  invalidating for movement, plan/chart changes, residency changes and uploads.
- Reuse main-thread terrain surface-color staging arrays. The three factories
  overwrite every pixel and upload it before reuse, retaining the same RGBAHalf
  textures and values. Four normal grid sizes retain about 89KiB of staging;
  each 64-segment upload previously allocated three 67,600-byte Color arrays.
  This targets allocation/GC during loading/travel, not a claimed steady FPS gain.
- Skip grass pages only when their immutable support grid has no positive moving
  cover at any vertex (moss/lichen remain in their stationary terrain materials).
  Linear interpolation cannot admit roots from this empty support. The benchmark
  additionally runs the old placement path and reads its GPU root counts to check
  that every pruned page really contains zero admitted roots. Unknown/positive
  support remains on the original rendering path; this is not density thinning.

A grass vertex early-rejection prototype was tested, but did not show a consistent
win (including the GPU-limited rainforest). It was reverted; the approved grass
shader remains unchanged. Its exploratory `grass-shader-original` rows are
diagnostic evidence, not an optimization shipped to the owner.

Native comparison checks compare submitted model/transform/tint/fade/shadow
multisets across eight headings. Compact property-block values and lengths are
read back through Unity's CPU API for equality. Loading/idle frames compare the
readiness memo with the uncached predicate outside FPS windows. These checks are
scoped evidence, not exhaustive art, arbitrary-world or performance acceptance.

## Results

Host: Ryzen 7 5700X, RTX 4060 8GB, 32GB RAM, Unity 6000.6.0f1 / URP17.6,
D3D11, seed11610541. The completed confirmation pass contains 90 intervals:
15 cases at each of six sites. All standing baselines reached their requested
terrain coverage, including 1,900 and 1,993 tiles in the two rugged sites.

Rounded FPS from matched original-CPU / optimized-CPU paths in that pass:

| Environment | Original standing | Optimized standing | Original turning | Optimized turning |
|---|---:|---:|---:|---:|
| Savanna/plain | 161 | 202 | 125 | 150 |
| Dense flat rainforest | 108 | 108 | 85 | 90 |
| Extreme-relief rainforest | 104 | 123 | 50 | 67 |
| Mountain ground | 151 | 152 | 73 | 90 |
| Desert | 273 | 329 | 238 | 300 |
| Tundra | 120 | 120 | 78 | 77 |

The largest measured CPU saving was compact property uploads: plain scatter
submission fell from1.076ms to0.353ms/frame; steep rainforest1.104ms to0.359ms;
dense rainforest1.555ms to0.686ms. The latter did not improve overall standing
FPS: GPU utilization sampled around96%, consistent with a GPU bottleneck rather
than further CPU submission savings being the immediate limiter. This is an
inference, not a per-draw GPU timestamp trace.

Optimized-path layer ablations (rounded approximate FPS):

| Environment | Full | No grass | No specialists | No mid | No trees | Terrain/sky only |
|---|---:|---:|---:|---:|---:|---:|
| Savanna/plain | 202 | 235 | 213 | 222 | 219 | 379 |
| Dense rainforest | 108 | 133 | 108 | 107 | 198 | 292 |
| Rugged rainforest | 123 | 134 | 121 | 131 | 148 | 184 |
| Mountain ground | 152 | 184 | 150 | 156 | 155 | 198 |
| Desert | 329 | 382 | 294 | 364 | 328 | 418 |
| Tundra | 120 | 144 | 118 | 119 | 118 | 166 |

**Do not interpret every small difference as causal.** Some toggles concern
absent plants: the mountain view had zero generated trees, despite its macro
habitat being BorealForest. Even inactive-layer cases varied between short
intervals (notably desert specialists); they do not prove an optimization or a
regression. Longer repeated runs are needed for small differences. These are
approximate workload comparisons, not precise additive component costs.

Practical tuning, not default changes:

- Earlier tree/mid LODs with grass15m/200m measured ~207FPS plain,129 dense forest,
  129 rugged forest,166 mountain,358 desert and141 tundra. This retains densities,
  but is an explicit visual-detail/range tradeoff.
- Adding vegetation-shadow disable to that combination measured ~243,151,136,
  170,360 and142 respectively. Neither combination certifies the owner's targets.
- The exploratory flat-forest run measured151.8FPS with lowest tree models versus
  110.9 full,137.5 without vegetation shadows, and123.1 with trees capped700m.
  Plain mid plants were much more costly than rainforest mid plants.
- Frustum culling was already essential: disabling it reduced the exploratory
  plain view from167.5 to75.5FPS. This pass did not discover an absent frustum culler.
- A10km view and307m configured full-detail radius, plus Low/Ultra quality, were
  sampled in the exploratory matrix. Rugged Ultra/expanded-radius rows that were
  still loading are rejected as steady-state comparisons. Flat quality changes
  sometimes chose the same tiles; no universal FPS improvement is established.

The 100FPS-at-all-times /165FPS-general targets are **not achieved across all
workloads**. Dense forests, turning and streaming have material remaining costs;
several99th-percentile intervals exceed10ms. The earlier CPU-limited plain measured
200+FPS without density/geometry reductions, but that absolute rate was not
reproduced in the later checks below. No claim is made about arbitrary
seeds, other resolutions, storms, active history, fullscreen/VSync or long play.

All six sites passed eight-heading render-data comparisons, including compact
property-block lengths/values, and233,260 loading/idle readiness comparisons.
The read-only snapshot SHA256 remained
`6727F08B6F7B8AE62A04F6A65E9F8C4193413A77CF8D02B7947568862A1EDB89`.
The confirmation binary still included the subsequently rejected grass-shader
prototype; the retained CPU A/B comparisons used the same shader path. Final
packaged-player checks cover the restored shader and later allocation/empty-cover
changes separately, rather than pretending these numbers came from that binary.

Raw reports: `artifacts/performance-confirmed/report.md` (completed six-site pass)
and `artifacts/performance-matrix/report.md` (81-interval exploratory pass).
Machine/configuration, per-frame CSVs, screenshots and exact-case values accompany
each. The plain non-threaded setup check is separate, not a matrix baseline.

## Final package and repeatability checks

The actual normal release package is
`artifacts/player-vegetation-performance/WorldGen.exe`, built
`2026-10-21T23:46:59.2303693Z` (IL2CPP, normal product name, frame timing stats off).
It restores the original grass shader and includes the allocation/empty-cover
fixes. `artifacts/performance-packaged` completed27 intervals across plains,
rainforest and desert. Eight-heading instance/property checks passed at all three
sites;61,443 loading/idle readiness comparisons passed. All108 legacy grass pages
in the desert had zero actual GPU roots. Skipping them reduced accounted local
grass buffers from71,141,136 bytes to zero; stationary ground materials remain.

The first final-package check was substantially slower in plains/forest than the
earlier confirmation pass. Chrome activity was observed and the owner reported
YouTube throughout plus a lagging Akinator tab. After the owner requested a rerun,
`artifacts/performance-idle-recheck` completed12 longer eight-second intervals:

| Final-package site | Original CPU | Optimized | No grass | No trees | Terrain/sky only |
|---|---:|---:|---:|---:|---:|
| Plains | 81.9 | 90.6 | 103.7 | 111.2 | 387.2 |
| Dense flat rainforest | 107.2 | 106.3 | 136.1 | 193.8 | 290.3 |

Full repeats were96.6/105.2FPS. The CPU scatter-submission sections were reduced
from1.17 to0.42ms in plains and1.56 to0.65ms in rainforest. Rainforest is not an
overall FPS win within the noise, despite its lower CPU submission cost. Another
34,630 loading/idle comparisons and both eight-heading checks passed.

Crucially, rerunning the **earlier diagnostic binary**, unchanged, in
`artifacts/performance-diagnostic-recheck` also produced only86.8FPS in the same
plain view (old CPU77.9; no grass102.7), rather than its earlier202FPS. This points
away from a new-package-only regression, but does not prove the root cause or
that Akinator caused it. System/background load and benchmark sensitivity remain
unresolved. Do not compare the early202 directly against the later90 as an
optimization gain/loss, and do not promise200FPS from this checkpoint. The
matched CPU A/B benefit and empty-buffer removal are narrower supported results.

The final package passed render-content checks, complete terrain coverage and
read-only snapshot integrity. Repeated screenshots show the same placement and
scene, with expected animated vegetation differences; this is not exhaustive
pixel-equivalence or long-play/crash acceptance. All raw runs are retained,
including the slow ones. Owner on-screen play is the next check; no settings,
densities, render distances or saved worlds were changed to manufacture a win.

## Scope and follow-up

No saved schema, generator, ecology, placement/density, water, terrain heights,
approved wind or normal quality/distance defaults are changed. Existing frustum
culling and conservative terrain-only occlusion are already present. This is not
general GPU Hi-Z occlusion, vegetation self-occlusion, a GPU-driven scatter rewrite,
merged shrub proxies or new tree impostors. The earlier GPU-reset/AppHang incident
must not be declared fixed merely because this benchmark completes.

Primary API references:

- [Unity RenderMeshInstanced group culling and instance limits](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Graphics.RenderMeshInstanced.html)
- [Unity frame timing requirements and limits](https://unity.com/blog/engine-platform/detecting-performance-bottlenecks-with-unity-frame-timing-manager)
- [Unity MaterialPropertyBlock.SetVectorArray array behavior](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/MaterialPropertyBlock.SetVectorArray.html)
