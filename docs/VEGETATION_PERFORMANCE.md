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

## 6 November: walking-query stalls and remaining collection spikes

Actual presented D3D11, 1080p, Medium vegetation, 6 km temperate forest,
90 seconds moving at 8 m/s with rapid turns, package 1129f68b:

| Path | Mean FPS | p95 frame ms | Worst ms | Frames over 16.7 ms |
|---|---:|---:|---:|---:|
| Unindexed walking rivers | 155.76 | 11.56 | 61.81 | 84 |
| Indexed walking rivers | 179.70 | 9.67 | 66.12 | 34 |

Evidence: artifacts/render-repair-20261106/presented-indexed-rivers.
Both travel windows continued streaming; neither was fully settled. Cases ran
sequentially, so cache warming can contribute to the difference. The traced
query cost itself fell from roughly 0.4-0.6 ms to 0.01-0.03 ms. Crossing the
500 m query-patch boundary cost 1.325 ms in a 5.872 ms frame. Exact terrain/water
comparisons passed at 1,025 probes across 41 actual biome/coastal patches, plus
3,682 probes at the measured site. Indexing is now enabled in source.

The earlier cached-river lock change was present in BOTH cases. Neither had
the earlier 90+ ms Ground call. This does not establish that all possible
query stalls are eliminated. Four of the seven largest indexed frames coincide
with completed garbage collections. The 100 FPS floor remains unmet.

The next package also reuses bounded wildlife-community output dictionaries:
6,089 arithmetic/ownership checks, 644 complete actual-world spawn records
across five location/time/weather scenarios, cancellation and retained-output
checks pass. A focused 1,000-calculation test allocates 552,000 bytes fresh versus
32,000 bytes with owned output reuse. Values are recomputed, not cached across
updates. Retention is limited to 128 communities of at most 512 input taxa.

Resident cover-buffer reuse is enabled after the earlier 409 pure checks and
native upload/cache/retirement/failed-preparation ownership proof. Its prior
presented comparison reused 40 MB with no material average-FPS regression; it
has not proved elimination of collection spikes. The free-buffer pool is capped
at 16 MiB. Combined broader performance validation is pending. Selected slot A
and the owner's shortcut remain unchanged.

The combined c7a9d3a6 package completed fresh presented 60-second moving tests:
jungle165.67 FPS /39.00 ms worst /39 frames over16.7 ms; temperate163.86 /51.33 /27;
savanna152.89 /50.37 /21. All continued streaming. These are partial improvements,
not a100FPS-floor pass.

Staged terrain was then tested FIRST in8102bdeb, followed by immediate updates on
the same jungle route. Staged160.77FPS /61.43ms worst /10 frames over16.7ms;
immediate167.12 /43.12 /41. Staging reduces recurring terrain hitches at a small
mean-throughput cost; the three largest staged frames completed garbage
collections. It is enabled for the owner's revised worst-frame priority.
Evidence: artifacts/render-repair-20261106/presented-staged-first.

Tree branch/path/normal scratch reuse passed372native model pairs /316,138vertices
with every position,normal,tangent,colour,index,bounds and all8UV channels exact.

A sparse root-cache reference-array experiment is retained only in Git926512ee,
reverted by d18e5f01. Pure1,061,328slot checks and550actual-world terrain/span
comparisons passed, but the actual reference-slot reduction was only999,990 to
912,707 after world preparation; no native FPS improvement was measured. About
76,000weak wrappers retained about29,000live targets in the pure-world probe;
that is not proof of the player collection-stall cause.

Next: coverage texture reuse removes repeated native Texture2D replacement and
Color[] upload allocation while preserving the shader payload. Native proof and
presented validation are pending. The worst61ms frame spent46.63ms in the coverage
phase coincident with collection; that identifies an allocation trigger, not a
claim that coverage arithmetic alone costs46ms.

Unity references consulted for these investigations:
https://docs.unity.com/en-us/engine/6000.0/manual/analysis/performance-memory/managed-memory/incremental-garbage-collection
https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/texture2d/setpixeldata
No additional manual collection scheduling or GC disabling was introduced.

Coverage texture reuse passed 10,240 bitwise native float comparisons, four
same-size native texture reuses, shrinking/growing/padding/empty cases, and the
actual production shader coverage, fade, cache and retirement fixture. Captures
were inspected. Package21ac6f73, presented jungle60s:181.70FPS,p959.38ms,
worst36.60ms,11frames over16.7ms. Largest three frames completed collections;
no obvious new texture-upload stall, but not a controlled throughput-gain claim.

Resident fog footprint reuse d20ab318 invalidates every spacing tier on resident
changes, rebuilding exact unions into retained sets. 6,873 checks passed,
including720 changing-resident/scale/quality/empty-case checks.100rebuilds allocate
45,424,800bytes fresh versus115,200reused. Current presented60s routes:

| Site | Mean FPS | p95 ms | Worst ms | Frames over16.7ms |
|---|---:|---:|---:|---:|
| Tropical forest |177.05|9.70|43.16|6|
| Temperate forest |168.75|10.19|83.65|8|
| Tall savanna |169.04|9.71|53.15|5|

Evidence: artifacts/render-repair-20261106/presented-fog-storage-reuse.
All moving windows were unsettled. Worst-frame target remains unmet. The83ms
temperate frame has only1.34ms Explorer work and no completed collection in that
trace row. Do not attribute every remaining stall to GC. Separate diagnostic
full-CPU and managed-heap capture is next. Heap bytes around1.1-1.2GiB alone do
not identify live reference-scanning cost or prove a leak.

The official Memory Profiler1.2.0 is pinned for offline analysis. Explicit
`-MemorySnapshot` captures only AFTER the timed window and requires a diagnostic
DevelopmentPlayer. Such player FPS is never release-throughput evidence. Source
8c2bb808 includes the adapter, not a completed heap diagnosis. Selected slot A
and the owner shortcut remain unchanged.

The diagnostic capture and official heap crawl are now complete. The temperate
route reproduced a69.81ms frame at9.75seconds, with only1.39ms Explorer work.
The previous render-thread frame spent69.056ms in Async.UploadTexture, including
27.875ms Gfx.CreateTexture. Its native metadata identifies a1024x1024,11mip,
seven-layer array; archive CAB11918477d14a643ed04663fdd1e9493e matches hare.fauna.
This is a native asset-upload stall, independently of the32.049ms incremental
collection in frame2705. The render-thread/main-thread frame offset matters.

Evidence: temperate-cpu-heap-current, temperate-cpu-render-independent,
temperate-upload-paths under artifacts/render-repair-20261106. The official heap
crawl reports3,154,573managed objects/1,053,489,206object bytes. Large value-only
buffers do not by themselves establish the collector scanning cost.

Source3d3ebe0e adds an isolated64-row BC7 upload experiment and full native
all-mip/all-layer byte-equivalence check. It is not enabled for normal fauna.
Production bundles, coat resolution and rendering remain unchanged pending
correctness and measured cost. Unity asynchronous upload documentation consulted:
https://unity.com/blog/engine-platform/understanding-the-async-upload-pipeline
https://docs.unity3d.com/ScriptReference/Texture-allowThreadedTextureCreation.html

Bounded upload native fixture6e3c21d7 passed39,146,828 decoded RGBA-channel
comparisons across every hare layer/mip. Original load worst79.95ms versus
bounded upload5.30ms,259pieces. These are isolated diagnostic timings, not game
FPS. The crucial fix is DontUploadUponCreate with no destination Apply; an
earlier prototype mistakenly uploaded the unused full CPU image and took137ms.
Direct compressed-texture GPU readback is unsupported on this platform, so the
final fixture compares texels in a compute shader and reads back the mismatch
counter. Failed prototypes are retained in the evidence directories.

22be22c1 adds an explicit trial-directory override and a streamed hare bundle
generated from approved authoring inputs. Production assets are unchanged.
The next acceptance gate is actual presented travel with this bundle, including
its loading delay and lifetime cleanup. Dynamic array CPU-shadow storage is a
memory tradeoff still to measure; do not imply this prototype is ready globally.

Actual same-package22be22c1 temperate60s moving/whipping comparison:
streamed hare first176.42FPS,p9510.58ms,max41.44ms,19frames>16.7ms;
original second183.22FPS,p959.42ms,max76.17ms,7frames>16.7ms.
Original's76ms is at9.756seconds, matching the identified upload; streamed's
two largest41/37ms frames coincide with completed collections. This removes the
largest observed hitch but is not a universal tail/mean improvement or100FPS floor.
Evidence: presented-streamed-hare and presented-hare-original. All windows remain
unsettled. Sequential cache warming can favor the second run; no broad mean-gain claim.

Source107634e5 changes the experiment to four recycled64KiB read pages, rather
than a9MiB managed payload, and upload batches capped256KiB/.35ms. Native texel
equivalence must be repeated because batching reuses staging textures within a
frame. Added wrong-layer positive control, cancellation and truncated-source checks.

d390bd0e makes the fixed six-integer mesh Edge a value record. No identity or
post-construction object-reference mutation consumers were found.4105 current
storage/canonical-hash/JSON/worker-wire checks pass;600k edges allocate14,400,024
bytes rather than600k separately allocated objects. Actual200k world integrity
and current save/load tests pass. Native GC/frame-time improvement is unmeasured.

Buffered native fixture107634e5 passed39,146,828 exact decoded RGBA channels,
including a deliberately wrong-layer positive control, canceled producer/native
array cleanup and truncated-file rejection. Reader retention is256KiB. Isolated
original upload worst68.545ms versus3.768ms bounded upload; these are not game FPS.

Actual presented107634e5 with original fauna loading and inline mesh-edge storage:
179.25FPS, p959.19ms, max81.535ms. The81ms frame remains at9.762seconds (known
hare upload); completed-GC frames35.94/39.83ms remain. Object-count reduction is
verified, but this does not establish a material GC-pause gain. Next comparison
uses the buffered hare transport on the same package and route.

That buffered107634e5 comparison completed:178.75FPS,p959.14ms,p9911.48ms,
max43.73ms,296frames>10ms and4>16.7ms, versus original179.25FPS,p959.19ms,
max81.54ms,325>10ms and8>16.7ms. Largest three buffered frames33.07/33.61/43.73ms
coincide with completed collections; no other frame exceeded16.69ms. Still
unsettled and sequential, so this establishes removal of the targeted upload
hitch, not a100FPS floor or a general average gain.

Sourceef550cbb uses explicit GraphicsTexture CopyTexture overloads to avoid
attempting unsupported BC7 CPU-region copies and logging a warning per page.
Unity documents that GraphicsTexture copies operate on GPU data only:
https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Graphics.CopyTexture.html
All177 streamed animal sets were derived in scratch from canonical approved
inputs for further native comparison. Production bundles/asset lock unchanged.
The native fixture now accepts a whole folder and records native memory usage.

Source4c8d9895 shares one immutable weak handle per fine terrain face rather than
one per incident edge. Expired addresses switch to a reconstructed face's handle;
they never retarget a shared handle. Lifetime/refresh checks pass.750 complete
terrain samples across30biome/coastal sites match a pre-change capture, including
reverse-order reconstruction after cache retirement and collection. Actual
native collection-time improvement remains unmeasured. This does not revive the
previously rejected sparse root-cache paging trial.

Native4c8d9895 all-species proof passed177 arrays /4,138,378,840 exact decoded
RGBA channels, each positive control/cancellation/truncation check, without copy
warnings. Engine-reported native texture bytes total1,034,617,120 for each path;
the streamed readable flag differs, so this metric alone does not prove that no
CPU shadow allocation exists. Assets are still derived trial outputs, not yet
published production inputs.

Actual4c all-streamed fauna/shared-face-handle travel:178.44FPS,p959.21ms,
p9911.49ms,max45.70ms,349frames>10ms,5>16.7ms. Completed-collection frames were
24.21/32.57/34.40ms; largest45.70ms at34.606s was not a completed collection and
Explorer.Update was3.36ms. Neither the100FPS floor nor elimination of all uploads
is established. New52771150 records model loading stages in memory (writes only
after measurement) and adds native vertex/index-byte plus authored-motion checks
for the repacked animal bundles. Next: focused CPU profile of the remaining pause.

The complete 40-second Development trace (temperate-streamed-cpu) retains all
6,577 frames after e15dcfd1 fixed the offline reader's 2,000-frame history limit.
This is diagnostic, not release FPS. Collection work costs22.01/20.06ms in
frames1320/5097. Frame1610 contains25.58ms of render-thread Gfx.CreateTexture
for the hare's1024x1024x7 array: bounding pixel uploads did not eliminate GPU
resource creation. Frame1291 spends31.91ms waiting for rendering, without an
attributed corresponding expensive render sample. Absence of a completed-GC
counter increment does not rule out incremental collector work.

Sequential native model/coat proof e15dcfd1 passes all177 repacks: identical
authored motion, metadata and native mesh vertex/index buffers, plus all coat
layer/mip pixels and cancellation/truncation/positive controls. The first527
fixture incorrectly loaded two bundles with identical internal file identities
simultaneously; its failure is retained. The corrected fixture snapshots and
unloads the original before loading the repack. Production assets remain intact.

Source2d95b42e stores planetary root terrain controls in a value array and
materializes reference-identity vertices only for locally queried terrain.
All200,000 complete control records retain their SHA256
683D517E08CCFF249BA4D4836E042E7CA27FF8DE2893C1A9ED7776B1C3B4984A.
Cached preparation creates0/200,000 root vertex objects;201 sampled vertices
pass complete field/identity checks.750 complete terrain samples across30
biome/coastal sites remain identical, including reconstruction after collection.
Lake-spill mutations explicitly write into the value array. This targets GC
object count; native collection-time improvement is not yet established.

Actual2d95 moving temperate60s:180.12FPS,p9510.22ms,p9913.20ms,max33.01ms,
584frames>10ms and19>16.7ms. Completed collections29.39/24.11/26.81ms.
The maximum is lower than the earlier45.70ms, but moderate spikes are more
frequent; this is NOT a clear frame-tail improvement. Input snapshot unchanged.

Queued-resource trial5a3df0c6 is REJECTED. Removing DontUploadUponCreate and
waiting for native initialization passes the same39,146,828 hare pixel checks,
but isolated upload max increases4.69->58.87ms and reported native bytes double
9,786,896->19,573,792. Both runs confirm threaded texture creation already true.
No real-game performance claim; option and code removed after the failed trial.
Original bounded upload remains. Diagnostic allocation capture now uses the
same retention/staging flags as current gameplay and accepts an explicitly
recorded shorter warmup; ordinary performance measurement warmup is unchanged.

Allocation stacks d989/e1ba use the same45s diagnostic warmup and10s capture,
not release FPS. NearTerrainPendingFresh/ActiveItemCraft/NextVisibleAnimalModel
previously allocated0.303/0.303/0.247MiB and14,223 objects combined; those call
sites allocate nothing in the second capture after direct enumerator loops.
Total allocation41.54->41.76MB does not improve: the later capture creates more
resident terrain data. Floats includes both transient transport and required
resident cover grids; do not attribute every such allocation to a pool miss.
The transient pool does saturate its16MiB budget in the longer route. e1ba lets
old sizes yield to the current working set instead of permanently retaining the
first128 lengths.414 ownership/pressure/concurrency/byte checks and actual native
upload/cancel/cache/overwrite proofs pass. No native FPS gain established yet.

Production rollout preserves all177 verified mesh/pose payloads and830 exact
BC7 coat layers. New .coats files live in canonical StreamingAssets/fauna, never
scratch; original .fauna.meta GUIDs are retained. The exporter defaults to
streaming; --fauna-embedded-coats explicitly produces reference arrays. Source
receipts predate the repository path reorganization, so packaging accepts their
exact approved asset-lock hashes while preserving the original exporter hash;
all underlying source/export file hashes are still checked. The package verifies
541 movement clips, maximum sampled skin error4.8923e-7. Asset inventory now also
explicitly preserves the four live terrain textures/arrays already in the prior
lock, which the generic inventory had omitted. Immutable-pack publication and
the normal-player test without GPU polling are next.

Publication completed in source827c7984: game-assets-20261106-streamed-fauna
contains19 new immutable payload packs, each downloaded again and hash-verified.
The current lock covers5,769 files/165 packs; layout audit passes6,376 checks.
Matching per-bundle and root dependency manifests were copied with the bundles.
No required runtime or authoring input was placed in scratch. Source and lock
are pushed; selected playerA/shortcut remains unchanged. B is being restored to
FULL NONDEVELOPMENT for the ordinary-path moving comparison without NVML polling.
# 6 November: remaining frame tails and terrain coverage reuse

The normal player with canonical streamed fauna and no NVML polling completed
`presented-production-no-nvml`: 168.94 FPS, p95 10.71 ms, p99 12.81 ms,
maximum 39.61 ms; 694 frames over 10 ms and 11 over 16.667 ms in 60 seconds.
This is not the requested 100 FPS floor. Removing telemetry did not eliminate
the unexplained stalls. Three completed collections coincided with 22.99,
24.09 and 31.61 ms frames. A separate coverage refresh cost 5.98 ms inside a
17.56 ms frame. The largest frame had only 1.89 ms of measured Explorer work;
its remaining cost is not attributed by that trace.

Source 74e0234a stores each tile's exact coverage inputs and reuses its existing
tree when they are unchanged. Ancestor lists and surface/timing scratch storage
are reused as well. Topology, fade timing and the production shader are unchanged.
1,501 pure checks compare against the original builder, including changed
inputs, immutable prior outputs and allocation-free repeated queries. The
`coverage-cache-native` player fixture passed actual shader coverage, incoming
and outgoing fades, cache restoration, partial replacement, coarsening and
retirement guards. The normal `presented-coverage-cache` comparison completed:
171.65 FPS, p95 9.77 ms, p99 12.16 ms, maximum 29.91 ms; 433 frames over 10 ms
and 10 over 16.667 ms. Coverage maximum fell from 7.47 to 1.43 ms, with total
coverage CPU 349.83 to 312.50 ms over the minute. Different streaming completion
and timing prevent attributing every frame improvement to this change. Neither
run satisfies the floor.

Source cfc37b76 limits fauna population scratch to the current lattice location.
Every region needed by that location still receives an independent result, and
no ecological values persist between builds. An independently recorded baseline
matches all 644 complete spawn records across five location/time/weather cases,
including retained results and cancellation. Reused Build allocations fell from
21,522,176 to 1,666,632 bytes in that fixture. This is allocation evidence, not
a claim about normal-player collection times.

Source 6795de4c shares one weak topology address between a refined terrain face's
incident edges instead of allocating separate equivalent addresses. Shared-handle
lifetime/reattachment checks pass, as do 750 complete terrain samples across
30 biome/coastal sites against the pre-change baseline and after clearing caches
and reconstructing in reverse order. Normal-player timing remains pending.

Source 4bbb0290 adds optional `-CaptureAfter` to the benchmark runner. Its normal
presented-frame screenshot is taken after timing and counters are finalized;
readback/PNG encoding cannot contaminate the measurement window.

November 6, latest checkpoint: the normal NONDEV b791f5ab temperate forest
moving/rapid-turn test completed at 168.98 FPS, p95 10.67 ms, p99 12.94 ms,
maximum 40.42 ms; 709 frames exceeded 10 ms and eight exceeded 16.667 ms.
The preceding normal c9 test was 177.32 FPS /29.03 ms maximum. Both routes
streamed terrain during travel. No frame-tail improvement is established by
the latest allocation changes; the 100 FPS floor remains unmet.

Native pickup checks passed all19 assertions, including asynchronous readiness,
original imported texture selection and actual GPU rendering. The diagnostic
capture after asynchronous preparation contains no synchronous pickup atlas
Load/Upload wait in GUI.Repaint. Beforehand, that path cost9.28 ms on the main
thread and7.59 ms waiting for texture upload on the render thread. Seven idle
polling methods also now have zero unconditional entry display-class allocations
in the built player, versus one each before; scheduled jobs still allocate their
captured inputs when needed. These are specific correctness/allocation findings,
not evidence that collection pauses are solved.

Actual GPU profiler analysis uses the installed Unity version's SampleGPU marker
and GPU-time column. Two large diagnostic CPU frames spent22.19/19.75 ms in
CollectIncremental. Their reported GPU frame durations include19.31/17.38 ms
PresentAfterDraw waits; actual camera GPU work was4.26/3.39 ms. Do not mistake
those frame totals for expensive foliage shaders. Other presentation stalls
remain unresolved and cannot all be attributed to garbage collection.

Evidence is under artifacts/render-repair-20261106: async-pickup-native,
polling-il-before/after.json, async-icons-allocation-analysis,
temperate-gpu-analysis-fixed, presented-no-timing-stats and
presented-async-icons-cold-jobs. The new screenshot still shows repetitive bare
ground and green patches; it is not visual acceptance. The owner shortcut still
selects A/f833 (November4); B is a separate, unselected test build.

November 6, native buffer attribution: the current-native-ownership diagnostic
snapshot includes NativeAllocations and exports6,193 graphics-resource identities.
The offline reader joins profiler BufferID to graphics resource/root and native
object type. Its twelve slowest retained buffer uploads belong to Mesh objects;
most are439,400 bytes, matching the65x65 terrain grid's104-byte vertex format.
The maximum initialization in this capture is2.55ms. The previous capture's
25.66ms initialization did not recur, and its process-specific ID cannot be
joined to the new snapshot. Do not treat that old outlier as conclusively
attributed or fixed. No mesh/buffer pooling has been implemented.

The latest capture is a30-second DEVELOPMENT CPU diagnostic, not release FPS.
Its15.683ms maximum is not evidence of a100FPS floor. The coverage-ancestor
early-out still needs a normal-player comparison. Native object naming is
exported offline only and adds no gameplay overhead.

November 6, retired mesh reuse trial9b7ce4c5: two independent normal-player
launches ran the same temperate6kmMedium1080 route,60seconds moving/rapidturn.
Control:166.56FPS,p9510.83,p9913.49,max43.61ms,13frames over16.667ms.
Reuse:164.75FPS,p9510.38,p9912.96,max25.25ms,5frames over16.667ms.
The two largest reuse hitches(25.25/24.76ms) coincide with completedGC; the
control's43.61ms outlier does not. This is a promising first comparison, not
proof that all GPU stalls are solved. Both routes were streaming/unsettled,
with different resident/grass counts; do not attribute the entire delta solely
to reuse. Native counters show507created/54reused meshes and7.24MB idle storage.

Reuse remains DEFAULT OFF pending followup. It keeps exact vertex/index sizes
and formats, waits4frames after retirement, and bounds idle retention to32MiB/
64meshes. New buffers use MarkDynamic; reused buffers receive data without
resetting their capacity. Scene disposal releases both idle and leased meshes.
Native reused-mesh-native passed20,915,506 CPU/GPU byte comparisons, wet/dry
geometry,16/32bit indices, delayed reuse, size bounds and disposal. No terrain
geometry/material changes. Reference: https://docs.unity.com/en-us/engine/6000.5/script-reference/unityengine/mesh/markdynamic

Followupc27f57f4 shares the existing terrain/tree installation allowance with
fauna coat uploads, and bounds pool eviction to one old mesh per return. Added
120FPS diagnostic cases to test actual idle headroom(the prior240FPS trial
did not leave headroom below measured throughput). User FPS preferences are
unchanged. Unity documents incremental GC use of idle target-frame time:
https://docs.unity3d.com/cn/6000.0/ScriptReference/Scripting.GarbageCollector-incrementalTimeSliceNanoseconds.html

November 6 follow-up: the first mesh-pool improvement did not reproduce reliably.
Fresh c27f57f4 uncapped travel averaged163.80FPS with a51.44ms maximum and17
frames over16.667ms. Its120FPS comparison averaged117.57FPS, maximum24.53ms,
with9 frames over16.667ms. The largest capped frame completed a collection;
idle headroom did not remove the GC pause. All15 fauna coat sets loaded with
the shared upload allowance. Neither run establishes a general frame-time win.

The faf96b7c size-class mesh pool passed20,916,350 CPU/GPU byte checks, including
reuse across changed payload sizes, exact submesh ranges/bounds and index-buffer
readback. Its normal-player temperate60s run averaged163.07FPS,p9511.18ms,
p9913.67ms,max40.36ms,12 frames over16.667ms. Cumulative mesh counters were
492 created/87 reused,12.47MB idle. Two collection frames reached29.73/29.50ms;
the40.36ms frame had no collection-counter change and its Explorer.Update cost
was1.57ms. Mesh reuse therefore remains OFF in ordinary gameplay. Spare mesh
capacity is included in the terrain-cache byte accounting.

171ea744 tests coarser Burst vegetation jobs and direct consumption of their
native admission output. Previously each patch scheduled four work items and
copied native results into managed lists before assembling the same draws.
This is an opt-in experiment, not yet an accepted performance improvement.
Existing exact instance/order and paired-render checks now exercise this path.
Evidence directories: presented-shared-budget-uncapped,
presented-shared-budget-cap120,presented-size-class-meshes,
compact-scatter-native-fixed, all under artifacts/render-repair-20261106.
The first compact-scatter-native launch was aborted because its command-line
case list was incorrectly space-separated; it supplies no test evidence.

The compact scatter experiment passed eight exact instance sets and32 paired
renders with zero pixel difference, but failed its performance gate. Fresh
171ea744 control169.45FPS/max28.34ms/8frames>16.667ms versus trial169.71/max45.77/
6frames>16.667ms. Rebuild median3.132->3.309ms,p954.384->4.491ms;
classification median0.915->0.985ms,append1.734->1.837ms. It is retired, not
enabled. Both routes remained streaming and this is not a global worst-case test.

The optional native GC observer records Mono profiler API3 events into a bounded
native buffer. Its callbacks do no managed work, allocation, locks or file IO.
This is needed because collection-counter changes alone do not identify exact
pause duration; earlier wording calling these "completed collections" was too
strong. The observer records stop/suspend/restart phases and cycle boundaries
separately, using native QPC to align with frame traces. A forced collection
outside gameplay is the required positive control before accepting any data.
Sources: Unity's public profiler ABI and Boehm event mapping:
https://github.com/Unity-Technologies/mono/blob/unity-main/mono/metadata/profiler.h
https://github.com/Unity-Technologies/mono/blob/unity-main/mono/metadata/boehm-gc.c

Native observer positive control now PASS in NONDEVbced3044:16 events across
two actual forced collections, including each stop-lock/suspend/restart phase.
The CSV uses native QPC; synthetic offset/cross-frame overlap checks also pass.
The actual gameplay diagnostic remains separate: no forced collections or GC
setting changes during its measurement. Native callbacks cannot allocate or
reenter managed code, and the helper DLL remains loaded until process exit.

6 November, native GC attribution and work scheduling:

- `presented-native-gc-phases`: normal bced3044, 168.86 FPS, p95 10.49 ms,
  p99 13.56 ms, maximum 34.80 ms. The two largest frames include 8.77 and
  7.16 ms between native stop/restart events; suspension itself takes about
  0.26 ms. This is only stop-the-world time, NOT the total cost of incremental
  collection on the main thread. The earlier development profile's 22 ms
  CollectIncremental sample is still relevant. Incremental captures emitted no
  cycle-start/end events, unlike the forced-full-collection positive control.
- The collector-statistics environment-variable trial produced no statistics
  in the installed player. No environment or collector setting was retained.
- `presented-separated-streaming-control` versus `-trial`: fresh normal 1798f72e
  processes, temperate forest, Medium, 6 km, 1080p, 60 seconds of travel/turning.
  Control: 170.98 FPS, p95 10.265 / p99 13.128 / maximum 49.060 ms,
  579 frames over 10 ms and 13 over 16.667 ms. Trial: 170.06 FPS,
  p95 9.618 / p99 11.877 / maximum 37.470 ms, 409 and 6 frames respectively.
  Terrain-upload/rebuild overlap fell from 20 frames to zero, but Update p99
  remained 6.63 ms and its maximum increased from 10.61 to 11.44 ms. Loading
  progressed normally and all coat sets loaded. Tail improvement is promising,
  not yet a conclusive overall CPU improvement: the scheduling trial stays OFF.
- d5aab0ba replaces separate face weak handles with the family's existing weak
  handle plus the face's two-bit sibling index. Existing face leases already
  keep siblings alive together; this adds no strong lifetime retention. Tests
  passed live sibling identity, collection of expired families, independent
  refresh, 46,708 actual face addresses plus 11,677 parent lookups sharing
  11,677 handles, and 750 exact complete samples across 30 biome/coastal sites.
  Samples also match after cache eviction/reconstruction and concurrent queries.
  Its normal-player 60-second test (`presented-family-handles`, 0d3678ea,
  scheduling trial OFF) averaged 169.00 FPS, p95 10.706 / p99 14.049 /
  maximum 25.172 ms, with 13 frames over 16.667 ms. Collection-associated frames
  were 18.663 and 25.172 ms, containing 4.802 and 9.773 ms of native stop/restart
  time respectively. This suggests lower collector overhead than the preceding
  29–32 ms collection frames, but ordinary tail variation remains and one run
  does not establish a complete stutter fix. Native 48 profile probes and 55,541
  readiness comparisons also passed.

87ba4ea6 adds optional `-CpuTiming` to the existing runner. It attaches only
documented release CPU timing counters during the diagnostic; no permanent
Frame Timing Stats setting or GPU instrumentation is enabled. Observed counter
values and source-timestamped engine frames are stored separately, because the
former must not be assumed to describe the current observed frame. Validation
is pending. Primary reference:
https://docs.unity.com/en-us/engine/6000.6/manual/analysis/graphics-performance-profiling/profile-rendering/frame-timing-manager/record-timing-data

6 November, two-minute source-timestamped CPU comparison (0d3678ea):
`presented-family-cpu-timing` completed both temperate6kmMedium1080 cases.
Normal:175.55FPS,p9510.27,p9913.34,max32.24ms,26frames>16.667ms.
Separated streaming:167.69FPS,p9510.76,p9913.30,max51.94ms,13frames>16.667ms.
Second case was warmer and had different resident counts. This does NOT confirm
an overall improvement; separation remains OFF. Both were streaming/unsettled.
Native observer138events has zero unmatched stop/resume sequences. CPU main/render
recorders are valid, but present-wait has no ProfilerRecorder counter; the valid
source-timestamped FrameTiming records provide it. b6c replaces the unsupported
recorder with CPU Total Frame Time, without changing normal gameplay settings.
Normal32.24ms hitch contains a29.32ms main-thread frame and7.31ms native GC pause.
Other30.33/28.74ms hitches have25.02/23.93ms presentation waits, with only3.56/
4.23ms main work. Trial51.94ms hitch has44.30ms present wait,6.76ms main work,
2.13ms render CPU and10.72ms GPU. Thus neither gameplay scripts nor foliage GPU
work alone explains every hitch. Existing flip/BitBlt/fullscreen/queue trials
already failed to eliminate these waits; do not repeat them without new evidence.
`analyze-frame-phases.py` uses native source timestamps, never observation time,
and explicitly labels nearby engine frames rather than asserting exact boundary
pairing. Clock uncertainty is recorded. No global worst-case/100FPS claim.

afd50e8a adds an UNVALIDATED benchmark-only manual-GC experiment. It owns GC
scheduling (unlike the retired extra-step experiment with automatic GC enabled),
uses0.5ms requested slices after16MiB heap growth, and restores automatic behavior
if heap grows256MiB. Actual call durations and remaining-work results are recorded;
a requested budget is not assumed to be a hard execution cap. IDisposable restores
the incoming collector mode/slice. Normal gameplay does not enter this mode.
Unity API references: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Scripting.GarbageCollector.GCMode.html
and https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Scripting.GarbageCollector.CollectIncremental.html

Manual scheduling test afd50e8a (`presented-manual-gc`) completed120s:
177.62FPS,p959.37,p9911.67,max35.56ms,31frames>16.667ms.383 explicit calls
consumed355.12ms; the maximum requested0.5ms slice actually took15.35ms.
Several final calls took13-15ms; native collection-associated frames still
reached35.56/31.82ms. Independent presentation waits reached29.75/29.34ms.
Heap growth guard did not trip and the original mode was restored. Not accepted
for normal gameplay: lowerp99 alone does not solve the long pauses. The native
collection cost cannot be treated as a preemptible hard-budget task.
Raw frames remain valid. This package wrote the small diagnostic CSV before
finalizing aggregate elapsed time; f859 moves all evidence IO and mode restoration
after the measured GPU drain/end timestamp. Existing captures are not rewritten.
234 adds optional out-and-back travel distance for long tests within one biome;
default remains the original eastward streaming route. Neither change affects
ordinary gameplay. Next acceptance coverage should include other dense contexts,
with explicit route/readiness reporting; this is not a global worst-case proof.

Broader34e27143 native baseline (`presented-dense-loops`),90s each,Medium6km1080,
180m out-and-back with rapid turns; normal automaticGC, scheduling trialOFF:
- Jungle166.53FPS,p9510.16,p9912.65,max25.47ms,847frames>10ms,8>16.667ms.
  Largest25.47/19.54ms frames overlap6.51/5.46ms native stops; other17ms frames
  include visibility/update work. Screenshot remains within the forest.
- Savanna151.78FPS,p9511.16,p9913.57,max34.64ms,1270>10ms,12>16.667ms.
  Largest34.64/32.15ms frames have31.80/25.01ms present waits;20.17ms frame
  overlaps6.74ms native stop. This is not a foliage-shader attribution.
Both are streaming/unsettled during movement, not a global worst-case guarantee.
48profile comparisons per site and24,232/23,121 readiness comparisons passed.
Both screenshots inspected; remaining coarse dark buttresses and repetitive
vegetation are not newly visually accepted. No visual changes in this pass.

50a94f50 sparse weak-cache storage passes its standalone million-address/index/
concurrent-publication/weak-lifetime/no-allocation checks. Actual750complete terrain
samples across30biome/coastal sites remain identical to the pre-change baseline,
including reverse reconstruction after eviction and4way concurrent queries.
Logical root references399,996 now occupy36,650slots; edge references599,994 now
occupy51,039slots, including page directories. Original weak lifetime and eviction
policy remain unchanged. Root vertex storage is unchanged. Actual native GC/frame
benefit remains UNMEASURED; fewer reference slots alone is not proof of a hitch fix.


### 6 November: sparse-cache comparison and habitat-array candidate

The sparse-cache native comparison completed in `presented-sparse-cache-loops`.
Against `presented-dense-loops`, same 90-second 180m routes and Medium settings:
- Jungle 166.53 -> 162.51 FPS; p99 12.65 -> 13.78ms, maximum 25.47 -> 42.29ms.
  The new maximum has a 38.09ms presentation wait, 4.24ms main-thread work and
  no native collector overlap. Explorer mean/p99 improved slightly, but total
  frame tails did not. No general stutter gain is established.
- Savanna 151.78 -> 154.76 FPS; p99 13.57 -> 12.87ms, maximum 34.64 -> 23.37ms.
  Frames above 16.667ms increased 12 -> 13. Several remaining peaks overlap
  native collector stops. Identical grass counts; streamed tile counts vary.
The sparse cache is retained as a verified reference-storage reduction, not a
claimed hitch fix. Both sites remain below the requested 100 FPS floor on tails.

Next candidate stores the reference-free 88-byte HabitatCell records inline,
removing 200,000 separately allocated objects from this world's graph. Optional
inputs explicitly retain nullable/fallback behavior. Ecology construction writes
back only after populating each record. Mutation sites were audited; no other
production habitat mutation requires reference aliasing.
`--habitat-storage-world` passes pre-change whole-planet, habitat, regenerated
habitat and 65 dated-projection hashes; current JSON, worker transport and full
world save/reload pass. A 200,000-record array allocates 17,600,024 bytes.
All 750 complete terrain probes across 30 sites match, including eviction and
concurrent reconstruction. Focused water (142+37), sound (69), wildlife (6089),
ground flora (5759), landscape (35733), fauna (104), crop (8) checks pass.
Native frame-time benefit is still unmeasured; this is a storage candidate.


Packed-habitat player701458d7: native current90s180m routes complete.
Jungle167.06FPS,p9913.14/max20.56ms,11frames>16.667; savanna151.46FPS,
p9912.74/max27.73ms,7frames>16.667. Compared with sparse-only: mixed mean/tail
results, no isolated GC improvement established. Savanna's largest peaks still
include6.0-7.4ms native stops and19-26ms main-thread frames. Jungle's remaining
largest observed frames are not collector-overlapping. Native96map probes and
24,623/22,802 readiness comparisons pass; no player errors. The inspected jungle
capture retains the existing dark/coarse trunks; this is not a visual acceptance.
LateUpdate p99 is2.10ms jungle/2.62ms savanna; OnGUI p99 .89/1.13ms. These scopes
overlap their children and must not be added indiscriminately.

Several large Explorer updates align with32m scatter-neighbourhood refreshes:
4.02s,33.02s,37.02s on the jungle route have8.9-11.0ms scatter work but only
3.8-4.1ms batch rebuilding. Refresh recomputes spherical tile positions inside
every comparison of its distance sort. A diagnostic candidate computes each
squared distance once, retains the identical tie-break and reuses its workspace.
15,702 entries at70world/pole/cube-boundary positions match literal order, including
reversed input. Warm ordering allocates0bytes. Native timing is pending; the
candidate remains OFF except `travel-whip-cached-scatter-priority`. Added explicit
`scatter_plan_ms` timing to establish actual cost of the whole refresh.

Frame-timing interpretation checked against Unity6.6 counter reference:
https://docs.unity.com/en-us/engine/6000.6/manual/analysis/graphics-performance-profiling/profile-rendering/frame-timing-manager/counter-reference
Active main/render timings exclude presentation waits. A large present-wait value
alone does not establish which device/OS/driver operation caused the delay.


### Paused for owner restart, 6 November 23:40 UTC

`presented-scatter-priority` completed both same-package 90s jungle routes.
Whole neighbourhood refresh: literal17calls mean4.9517/max6.4488ms; precomputed
16calls mean2.0061/max3.2233ms. This confirms a localized reduction of roughly3ms
per refresh. Literal refresh-associated frames reached15.78ms; optimized largest
refresh frame12.06ms. Exact key order was already verified independently.
Whole-run results are mixed: literal167.67FPS,p9912.60/max27.61ms,7frames>16.667;
optimized163.34FPS,p9913.52/max21.57ms,18frames>16.667. Do not describe the localized
saving as a general elimination of stutter. The optimized run's worst Explorer
update combines6.26ms visibility batches and3.33ms fauna; those costs remain.
Candidate is compiled in B0c6e90f1 but DEFAULT OFF outside its explicit test case.
No new build or shortcut change during wrap-up. Next resume: inspect remaining
batch/GC causes, repeat the new ordering in another context before promotion,
and consider retaining computed reveal bounds to reduce the remaining2ms refresh
without changing selected neighbourhoods. No reveal-bound cache has been written.
All benchmark players exited; MSBuild/compiler servers were shut down.
