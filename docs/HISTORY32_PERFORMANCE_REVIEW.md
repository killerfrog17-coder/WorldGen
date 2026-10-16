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
