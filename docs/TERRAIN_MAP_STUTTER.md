# Terrain/map GC stutter investigation /22 October 2026

The owner explicitly authorized isolated timing tests for recurrent freeze frames
and closed their player first. This narrowly supersedes the older compile-only
restriction; no saves/preferences or owner processes were changed. No captures,
full regression suite or broad vegetation/FPS campaign was run.

## Cause and implementation

The diagnostic IL2CPP player reproduces an idle pause about every 1.8 seconds;
all six slow frames in a 12-second window coincide with a GC counter increment.
The whole-world satellite map produces 79 collections in its 12-second bracket,
80 frames over 16.667 ms and a 119.435 ms maximum. The long map frames are outside
the map upload code; withholding texture uploads does not remove the local-map
collection pattern. This is shared-heap allocation/collection pressure, not
evidence of a terrain rebuild, F3 query or texture upload taking that long.

Changes:

- Loaded-tile job checks no longer create capturing LINQ closures per tile per
  frame. Scatter job inputs are captured only when actually scheduling a job.
  Generated IL2CPP C++ confirmed the old terrain closure was allocated before
  the early `continue` for already-loaded tiles. These loops predate the map
  revision; the new allocation-heavy map work aggravates an existing source.
- Terrain edge-array comparisons use direct indexed `Double.Equals`, preserving
  NaN/signed-zero semantics. Unity's generated LINQ implementation calls both
  array interface enumerators rather than using an allocation-free array path.
  These comparisons run hundreds of times per frame, even after loading ends.
- Hidden/expired chat does no GUI style/font work. Visible chat reuses its style.
  Backdrop job/coverage predicates avoid capturing query lambdas.
- Backdrop distance ordering reuses one list/comparison delegate and only sorts
  on plan changes or movement. Original dictionary order breaks equal-distance
  ties, exactly as the previous stable OrderBy. The stationary frame loop no
  longer rebuilds sort buffers; movement sorts the same reusable storage.
- Each map sampling context reuses bounded point/sample scratch arrays (up to
  4,096 elements). Distant coloured-map samples blend the same sorted material
  support kernel directly, rather than cloning complete material profile arrays
  into temporary patches. Height/water sampling is unchanged.
- On the packaged Windows player, progressive map production now runs in the
  **existing external terrain worker**, alongside its existing source terrain.
  Its subdivision heaps/GC no longer stop Unity's main thread. No second world
  worker is launched. Authenticated transient wire6 adds a cancellable stream
  of small raw RGBA updates, with bounds validation and revision rejection.
  Existing geometry/scatter messages and save formats otherwise stay unchanged.
- The worker retains the same four independent map contexts, 16 MiB/4,096 tile
  LRU/eight-view cache, selected palette and bounded sample caches. A serial map
  gate reserves the existing four CPU permits during map work, rather than
  running four additional map workers alongside four terrain workers. Closing,
  panning and zooming cancel queued/active work through the existing connection
  cancellation path. Player and worker output queues remain bounded.
- The in-process map producer remains the non-Windows/missing-worker fallback;
  that fallback does **not** have external-heap isolation. Sampling/preview/tile
  order/resolution, terrain geometry/LOD selection, vegetation, water behaviour,
  world generation/history and saves are not changed by this fix.
- `/seed` prints the source world's literal recipe seed, with completion/help;
  extra arguments are rejected and no regeneration is performed.

## Targeted evidence

Release IL2CPP; Ryzen 5700X, RTX 4060; 1920×1080 forced single offscreen URP
render, VSync off/unlimited. Seed11610541, scale0.1, desert cell59340,
latitude−23.952399/longitude−49.204102, clear noon, F3 closed. High terrain,
60 km view, 153.6 m full-detail radius, grass30/300 m, default non-grass.
All 320/320 terrain tiles are resident. Timings are frame intervals, **not**
GPU timings or normal desktop FPS certification. FrameTimingManager and
per-thread allocated-byte counters are unavailable/zero in this release;
GC.CollectionCount and cached managed-heap samples are the supported evidence.
Per-frame trace rows stay in RAM and are written only after timing windows.

Initial diagnostic: `artifacts/stutter-trace-before`. Partial material/scratch/
closure fix: `artifacts/stutter-trace-after`. The partial fix is explicitly
insufficient: whole-world map collections fall to19 but the worst pause grows
to305.081 ms. This motivated worker isolation rather than accepting fewer GCs
as proof of a finished fix. Intermediate worker-map run:
`artifacts/stutter-trace-worker-map`, whole-world12 s: zero frames over16.667 ms,
max9.089 ms. Idle still has two slow frames/max21.039 ms, motivating the final
allocation-free edge comparison.

Independent actual packaged-worker check compared all refinement bytes for
Satellite, Heightmap, TreeDensity and Biomes at32×16 against the local tile
sampler: 12 tiles identical. Cache reuse, cancellation followed by a new request,
completion and snapshot SHA preservation pass. Evidence/fixture:
`artifacts/stutter-map-wire-check`. Native checks additionally compare48 global
material/height/water probes exactly (floating scalars within1e−9),25 edge-array
cases against LINQ equality, and `/seed` plus invalid-argument behaviour.

Source snapshot SHA256:
`6727F08B6F7B8AE62A04F6A65E9F8C4193413A77CF8D02B7947568862A1EDB89`.

## Final checkpoint

`player-stutter-ready`, release IL2CPP BuiltUtc2026-10-22T07:46:15.1433297Z;
compile/package exit0, matching worker/assembly manifest. Shortcut selects this
real C: build. Evidence: `artifacts/stutter-trace-ready`.

| Case | Before window | Before >16.667 ms / GC cycles | Before worst | Final window | Final >16.667 ms / GC cycles | Final worst |
|---|---:|---:|---:|---:|---:|---:|
| Idle, no map ever opened | 12 s | 6 / 6 | 24.955 ms | 30 s | 1 / 1 | 17.425 ms |
| Whole-world satellite refinement | 12 s | 80 / 79 | 119.435 ms | 30 s | 1 / 1 | 21.459 ms |
| Local satellite refinement,0.05° | 12 s | 7 / 7 | 160.358 ms | 30 s | 1 / 1 | 21.150 ms |

The longer final windows are intentional: not a claim of identical-duration
counts or zero GC. The final idle collection occurs at25.53 s, versus the old
roughly1.8 s cycle. Map output is actually received/uploaded: whole-world view
is still refining1024/2048 with1,131 built tiles at30 s, whereas the local view
finishes2048×1024 with3,875 cumulative built tiles. No complete world-map latency
or universal half-second-sharpness claim. All three timing windows have320/320
resident terrain;48 material probes,25 edge cases, `/seed` validation and28,485
loading/idle readiness comparisons pass. Snapshot hash is unchanged. All own
players/workers are stopped; no owner game was closed by the agent.

Intermediate edge-fixed checkpoint `stutter-trace-final` also checks turning
and travel: turning12 s,zero >16.667 ms/max8.371 ms; travel12 s,two >16.667 ms/
max21.463 ms, with loading still partial at the end. These are diagnostic sanity
checks, not fully settled movement/loading performance acceptance. Its repeated
local-map request records2,728 remote tile-cache hits. No art review performed.

Reproduce the final check from the repository with:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/vegetation-performance.ps1 -PlayerFolder player-stutter-ready -Output artifacts/stutter-trace-new -Sites desert -Cases stutter-idle,stutter-map,stutter-map-local -Seconds 30 -StutterTrace
```

The improvement concerns hitch frequency/severity, not a certified gain in
steady-state FPS. Occasional17–22 ms GC frames remain in these runs. No claim is
made that all loading, map UI, driver or OS hitches are eliminated; broader
in-game travel/biome/visible-map acceptance remains with the owner.
