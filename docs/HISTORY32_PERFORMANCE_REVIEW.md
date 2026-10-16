# History32 runtime review

## Observed failed native control

The real Unity/Mono O Continents Plus 200,000-cell control advanced 1,511 years
in 595 seconds: **2.54 years/second**, minimum rolling **1.89**, worst summed
year **1,182 ms**. It fails the requested 3 years/second and sub-second-year
criteria. Smooth frames do not make this a passing simulation-throughput test.
Evidence: generated `unity-o-cp-performance1500` playback and work-stage CSVs.

| Observed stage | Calls | Total work |
| --- | ---: | ---: |
| City actors | 2,553,662 | 232.5 s |
| Regional actors | 404,495 | 108.7 s |
| Cultural actors | 204,102 | 108.6 s |
| State actors | 45,027 | 61.7 s |
| Checkpoint hashes | 196 | 53.6 s |

This is cumulative bounded actor work, not evidence of a recurring 200k-cell
population census. Nevertheless it is too expensive. All three reported GC
generations incremented 998 times under Mono; collection counts alone do not
identify which calculations allocated the memory.

## Exact reductions under review

- Reuse the same city's rural production capacity inside its actor update;
  avoid evaluating obsolete capacity estimates that are immediately replaced
  by the community-capacity model. Food, people and elapsed dates are unchanged.
- Short-circuit current-rule feature checks instead of repeatedly traversing
  the historical version-string chain.
- Keep more immutable cell/crop agronomic habitats in a bounded 64k cache;
  remove the temporary four-temperature array from each cache miss.
- Replace hot enum `HasFlag` checks with equivalent bit tests, avoiding Mono
  boxing on repeated crop/habitat calculations.
- Memoize directional environmental-route geometry in a bounded 64k value
  cache: distance, endpoint latitudes and the exact eight terrain-raster cell
  IDs. Recompute costs from live management, transport and communication;
  retain per-sample quantization and summation order. No barrier is weakened.
- Use timestamp counters, static stage labels and value-type work metrics
  instead of allocating a stopwatch, stage string and metric object for every
  observed event. Allocated-byte diagnostics distinguish instrumentation from
  model work; no diagnostic value may affect simulation outcomes.

The geometry test checks 24 distributed real route pairs in both directions,
all eight literal raster samples, unchanged and modified live capabilities,
and complete 20-year continuations with caching enabled versus disabled.
It passes 771 assertions each on official history31 Earth, candidate history32
Earth 200k, and Continents Plus 200k. Source snapshots remain unchanged.
Earlier exact 100-year Earth/Continents controls and official31 parity also
passed for the pre-geometry reductions.

No actor dates, event frequency, population representation, ecology, crop
requirements or historical decisions are simplified by these optimizations.
The geometry cache is runtime-only and absent from saves/canonical hashes.
T native acceptance completed1510-era-tail years on the same200k O input:
1506.78 simulated years in361.43s,4.169years/s overall,909.68ms worst summed
year and17.44ms P95 frame. It nevertheless FAILS the strict gate because its
minimum rolling throughput is2.98985years/s. It is not rounded up into a pass.
T processed2.426million city events in84.1s (versus O's2.554million/232.5s),
404220 region events in90.3s,210104 culture events in74.0s and188 hashes in48.9s.
O/T also differ in historical mechanisms and actor trajectories, so those
totals are diagnostic, not an isolated per-coefficient causal speedup claim.

U2's native gate PASSES on the same200k O input:1509.73 simulated years in308.06s,
4.901years/s overall, minimum rolling3.628,716.63ms worst summed-year work and
17.40ms P95 frame. Its final year166309 is2213years after maximum technology;
170 governments remain active among4099 recorded settlement records (including
abandoned sites). There were178 displayed publications. The actual native map
camera PNG was viewed: coastal/interior political networks span major landmasses,
with substantial unruled cold/dry interiors. This is not a full-HUD capture or
proof that every world's history is realistic.

It includes exact geometry memoization, unboxed forest flags and value-type
diagnostic metrics. The recorded allocation-byte columns were all zero despite
807 Mono collections. Those zeros are not evidence of allocation-free model
work: the runtime counter may be unavailable. The next diagnostic checks a
known allocation before using this counter and emits `unavailable` where it
cannot establish support. Collection counts and wall-clock timings remain valid.
The first U build stalled during C: disk exhaustion as Windows expanded its
paging file. Only the verified owned diagnostic editor and its Unity children
were stopped; the failed log is retained. Older ignored test players and old
history18 snapshot exports were moved recoverably to D:, not deleted; source,
user saves, music and the official31 launcher target were not moved. U2 then
built successfully with its packaged Core matching the frozen candidate.

CLI timings or exact-output tests do **not** establish Unity throughput or a
passing gate. The separately measured native publication/rendering run remains
required after every material source change, including the later regional
outreach and provisioned-port experiments.

## Candidate V and exact agronomic memoization

V's actual fresh-rule Continents Plus200k tail failed the strict native gate:
1,508.17 years in456.61s (3.303years/s overall), minimum rolling2.119years/s,
worst summed-year986.89ms, P95 frame17.90ms. It ended in173308,2,513years past
maximum technology, with6,480 recorded settlements and406 active governments.
There were253 displayed publications; the allocation counter correctly reported
unsupported rather than claiming zero allocation. The source world remained
unchanged. Evidence is `D:/WorldGenBuildArtifacts/artifacts/history32/unity-v-cp-performance1500`.

Its main recorded costs were city events121.5s, region events85.0s, culture
events76.6s, state events73.9s and checkpoint hashing72.0s. An overall average
above3 did not make its slow rolling windows pass.

The next exact experiment memoizes quantized `CommunityTransferFit` results by
cell, crop identity, maturity, photoperiod origin, water requirement, warmth
requirement, selection years and soil-management choice. It also caches immutable
growing-window area yield. Historical weather, live farming effort, food stocks,
people, budgets, institutions and research remain evaluated by their ordinary
actors. Runtime caches are bounded (262,144 complete fits and262,144 area fits),
are not saved and have a disabled literal path for differential checks.

12,339 assertions each passed on actual V Earth200k and Continents Plus200k:
all18 crop definitions,24 distributed cells,both management choices,in-place
changes to every relevant cultivar trait,complete field-area arithmetic,and
twenty-year cached versus literal full-history continuations. Official31's
20-year continuation remains `f4e260d19883e9dfd894c63c9ff8215646b023fdb25090d3795819e6f48d2fef`.
These exact-output checks do not themselves establish native throughput.

## Optional native-code backend

`scripts/unity-check.ps1 -BuildPlayer -Il2Cpp -PlayerFolder <unique-folder>`
tests Unity's installed IL2CPP backend without changing the normal development
backend. The build restores previous settings and records hashes of the staged
authoritative DLL inputs in `build-inputs.json`; an input changing during the
build invalidates acceptance. `Assets/WorldGen/link.xml` preserves all shared
schema assemblies and their reflection-discovered fields. This is a compiler
experiment, not a simpler or second simulation implementation.

The installed Visual Studio C++ tools, Windows SDK and Unity Windows module
meet [Unity's documented build requirements](https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/windows/requirements-and-compatibility).
The first experimental build failed from C: disk exhaustion; its log remains.
Only generated build/cache artifacts were moved recoverably to D:, and the
owned compiler's temporary directory was redirected there. Source, user worlds
and music were not moved. The retry built successfully from the identical V
Core SHA457A3AA1… and passed the actual saved-world dock checks: unchanged
history, contextual selections, Atlas/language pages, resize/clamping, era/music
behavior and zero GUI errors. Native performance and cross-host continuation
still require separate evidence before choosing this backend for release.

The subsequent full IL2CPP V tail **failed**, rather than establishing a speed
win:1,508.17 years in452.95s,3.330years/s overall,minimum rolling1.572years/s,
worst summed-year1,489.80ms and P95 frame20.52ms. Its final time,6,480 recorded
settlements,406 active governments and full history hash exactly match Mono V
(`c28fe3f78d83906655e96bddca46f932c6e55ce173f73b8afb45959172fe2908`).
This is useful cross-backend determinism evidence, not performance acceptance.
The normal Mono backend remains the candidate; no compiler-only speed claim is
made. Metrics survive cleanup under `unity-v-il2cpp-cp-performance1500`; the
large experimental player was removed.
