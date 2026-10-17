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

## Exact storage and canonical hot paths — 17 October overnight

These changes preserve canonical bytes and historical rules; they do not reduce
cadence or cull actors. See [compact tree transport](HISTORY32_COMPACT_TREE_TRANSPORT.md)
for the save-size and binary/JSON compatibility evidence.

The four aligned campaign histories now use256-record copy-on-write pages with
the preceding array contracts.137 pure assertions cover old binary and JSON
wire, typed/literal hashes, retained references, nested captured-town arrays,
concurrency and bounded append/fork allocation. Actual F6 Earth100-year replay
remains `93add43f84c56e5241d9c474a9e7d56538b654b069b898c6bc55e10ccf3801b4`
with the tree transport and closed-sidecar byte caches.

Theater/progress/military-operation memoization reuses exact bytes only for owned
private hashing with a matching closed parent campaign. Public hashing reads
actual fields; mutable access and exposed-record forks invalidate memo bytes.
89 focused assertions cover retained edits, replacement, active-parent exclusion,
concurrent public hashing and warm allocation guards. Existing relation56,
leader35, campaign137, small-canonical123 and tree-transport279 pure fixtures pass.
Actual P native passes the12-vector tree transport and13-vector SHA probes;
throughput acceptance is separate.

Captured loop variables still allocated on every campaign/relation iteration,
even when record bytes were cached. Cold lambdas now live in helpers called only
on cache misses. Warm12000-row serialization allocation drops from450,558 to
66,534 bytes for campaigns, and450,020 to65,996 for relations:384,024 bytes removed
per loop, without another cache or byte-format change. This is later than frozen
P16 and must not be credited to that player's results.

### Reusable Windows hash contexts

The immutable Windows CNG algorithm provider remains shared, with independent
hash contexts. Dirty radix fingerprints call ComputeHash repeatedly on one SHA256
object, but Initialize formerly destroyed/recreated the native hash handle after
every node. Microsoft's [BCRYPT_HASH_REUSABLE_FLAG documentation](https://learn.microsoft.com/en-us/windows/win32/api/bcrypt/nf-bcrypt-bcryptcreatehash)
permits another message immediately after FinishHash. It changes neither the
canonical input nor SHA-256 output.

The implementation retains a reusable context only after completed FinishHash.
Explicit Initialize during an unfinished message still discards/recreates it.
STATUS_NOT_SUPPORTED / STATUS_INVALID_PARAMETER retain the preceding native path
and memoize missing capability, including Windows7/Vista-era providers. Other
platforms and unavailable CNG retain the managed fallback. No global mutable
hash context, simulation observer, saved field or clock input was added.

A test-local prototype runs60 actual persistent-tree fingerprints, each after64
deterministic `.With` updates, using the unchanged129-byte leaf and1025-byte branch
payloads. Both modes match the authoritative Merkle fingerprint:7,804 hash-data
calls over4,499,120 bytes. Native hash creations fall from7,864 to60. One comparison
is18.40ms versus15.66ms; another40.06ms versus17.08ms is GC/contention-sensitive.
These are small CoreCLR diagnostics, not native acceptance. Production provider147
assertions cover known vectors, offsets/chunk boundaries, repeated messages,
unfinished resets,128 concurrent contexts, disposal isolation and forced legacy
native fallback. Tree transport279 assertions also pass with the new provider.

A separate isolated-assembly comparison calls the actual preceding and revised
production tree implementations (240 fingerprints,64 real updates each). Every
result equals `40a7c0476e756783119514371b5373d3a8ec1f43f6033e7f516fe633ec62a879`.
Allocation, including tree writes, falls from29,445,672 to28,197,032 bytes
(4.24%). Warm comparisons38.61/41.84ms versus37.16/36.75ms indicate a modest
roughly8% mean gain on this CoreCLR fixture, not a native throughput claim.

### Limits of exact incremental hashing

Owned aggregate sealing already commits event archives and leaders through
ArchiveHash and the LeaderDigests tree. It does not stream their full archives on
every private checkpoint. Public ComputeHash also verifies them, so the broad
SimulationHashProfile.Capture is not a faithful private-sealing profile.

Other lifetime arrays and government sidecars still stream canonical bytes.
`Years` changes near the message start, before those arrays. SHA-256 processes
blocks in order using the preceding chaining state
([FIPS180-4 section6.2.2](https://csrc.nist.gov/files/pubs/fips/180-4/final/docs/fips180-4.pdf)).
Independent record/page digests therefore cannot replace unchanged middle
segments while retaining the existing digest. CNG [DuplicateHash](https://learn.microsoft.com/en-us/windows/win32/api/bcrypt/nf-bcrypt-bcryptduplicatehash)
only reuses identical prefixes; the current layout's common prefix is tiny.
Merkle substitution or moving the clock requires an explicit new integrity
contract, not an exact optimization.

Test-only `--private-hash-synthetic` and `--private-hash-cost <world> <json>` separate
canonical byte count, serialization-only cost, full owned hashing and raw CNG over
equal byte volume. The world diagnostic asserts that its reconstructed root
produces the saved aggregate hash. Warm component profiles do not measure dirty
Merkle work. These first component profiles used test-only reflection.

At12000 rows, synthetic canonical byte counts are7,944,927 for campaigns,
1,404,044 theaters,1,416,044 progress,7,128,046 military operations and2,796,037
relations. Reusing exact256-row canonical byte blocks could reduce encoding/lock
calls, but cannot remove SHA byte volume. This remains an unimplemented proposal
requiring actual private-profile evidence.

The isolated pre-P N15 control still **fails**:2.4666years/s, minimum rolling
1.8544years/s, worst summed year2,223.44ms and301 Boehm collections. Its final
`eb1a49f19de71711533357288c2db276363f7188e5d57d067504e4c07a002de6`
matches prior controls. These optimizations do not themselves establish
>=3years/s and every computed year<1second.

The subsequent isolated P16 IL2CPP run also **fails**:1,007.6129 simulated years
in392.946s,2.56425years/s, minimum rolling1.90668years/s and worst summed year
2,177.436ms at181158, with412 Boehm collections. The final history hash remains
the same `eb1a49...` control. Compared with N15 this is only about4% overall.
P includes compact tree transport, sidecar byte caches and typed geographical
writers, but not the later closure removal or reusable CNG contexts. Its191
private hashes total144,502.6ms (maximum1,419.8ms); city events84,815.7ms,
state events52,632.2ms, region events39,949.3ms and sea events21,858.6ms.
Private/publication copies add11,315.4/9,739.3ms. These are measured native
costs, not an acceptance claim or a reason to reduce actor/event cadence.

The actual F6 private-root diagnostic matches its saved hash
`74a2223de5ac874550624ccfdfb4ab3ff69568563bfd97d1002e2894e0fddc65`.
It streams103,433,181 canonical bytes per checkpoint. Warm CoreCLR serialization
alone takes76.73ms with9.03MB allocated; complete owned hashing110.87ms with
8.89MB allocated; raw CNG over equal byte volume45.76ms. Inclusive component
measurements must not be summed twice. International records encode8.29ms with
1.86MB allocated, and connections21.94ms with2.02MB; timeline3.15ms/.90MB,
connections' ambitions2.40ms/.71MB and sea service2.29ms/.61MB remain generic
reflection paths. Cached campaigns encode11.86MB in0.67ms, relations19.85MB in
3.47ms. All46 complete256-row campaign pages are closed, with no mixed complete
page. Exact page-byte aggregation could reduce calls, but these warm figures
prioritize the remaining generic record encoding over another campaign cache.
CoreCLR timing is not a substitute for an isolated native profile, and this warm
diagnostic excludes dirty-tree fingerprint work at a real advancing checkpoint.

The measured generic paths now have exact schema-checked scalar writers:
international accounts/organizations/finances and simulation-calendar records
pass234 independent-literal-byte, field mutation, null/optional, concurrency and
cancellation assertions. Service/ambition/colonial-government writers pass403
assertions. The combined F6 replay100 remains the same `93add43f...` control.
Warm international encoding falls8.291 to2.371ms and1,858,493 to67,397 allocated
bytes; timeline3.152 to0.309ms and900,477 to66,045 bytes. Connections inclusive
encoding falls21.94 to13.52ms and2,023,157 to623,965 bytes. Overall warm root
encoding falls76.73 to67.66ms and9,028,253 to5,003,533 bytes; complete owned
hashing110.87 to100.27ms. These CoreCLR measurements are qualified, not native
acceptance. The expanded profile identifies nuclear accounts as a further generic
hotspot:2.61MB canonical bytes,6.26ms encoding and1.99MB allocation.

An explicitly opt-in `Prehistory.CanonicalHashObserver` now reports once around
the existing private checkpoint seal. Its thread-local, nested/restored scope
counts newly digested radix nodes/payload bytes, tree fingerprints, newly frozen
record bytes and root/total elapsed time. It changes no saved fields, canonical
bytes, dates or cadence; ordinary playback leaves the observer null. The explicit
native playback diagnostic writes `canonical-costs.csv`. Tiny fixtures cover
nested/concurrent scopes, cancellation, observer exceptions and identical hashes.
This separates advancing dirty/cold work from the warm serialization profiles;
it is not permission to hide checkpoint work outside the timed simulation.

The actual F6 fixture advances20 consecutive5-year SimulationSession checkpoints
with the observer active, including the nuclear scalar writer. Its100-year final
hash is exactly `93add43f84c56e5241d9c474a9e7d56538b654b069b898c6bc55e10ccf3801b4`,
and the public source remains unchanged. The first cold seal takes636.18ms on
CoreCLR and constructs142,955 frozen records totaling47,017,320 bytes. Subsequent
19 seals take146.9–190.4ms and construct9,553–10,567 frozen records totaling
2.34–2.58MB each. Those warm advancing seals still digest21,745–30,395 previously
uncached radix nodes,14.28–19.61MB of tree-hash payload, and2,737–3,731 tree-root
fingerprints per5-year checkpoint. The20 hash stages allocate796.74MB in total
(39.84MB mean per5-year seal, including the initial cold one);
the preceding292MB single100-year cold checkpoint must not be treated as a
recurring5-year cost. Native R must quantify the reusable-CNG and typed-writer
effects before attributing a performance win. Scope fixtures pass43 assertions.

### Isolated native R result — still fails

The R IL2CPP player includes reusable CNG contexts, the typed international,
calendar, service and nuclear writers, and the opt-in canonical cost observer.
The130,748,595-byte F6 input is unchanged; native provider13 and tree-transport12
smoke vectors pass. Other agents' world loads, builds and rendering were held
until the player exited. Temporary Standalone/stripping build overrides were
restored after the build, before this test.

R advances1,007.6129 years in369.487s:2.72706years/s, minimum rolling2.03946years/s,
worst summed computed year1,694.6509ms at180794, and395 Boehm collections.
It **fails** the3years/s and every-year<1second requirements. The final year181207,
3,676 recorded towns,761 active governments and hash
`eb1a49f19de71711533357288c2db276363f7188e5d57d067504e4c07a002de6`
exactly match the preceding F6 controls. Overall throughput improves6.35% over
P, worst-year time falls22.2%, and hash-stage CPU falls18.85%; no acceptance claim
follows from those improvements.

The191 hash stages total117,265.1ms (maximum1,131.12ms). Other totals remain
similar:city85,948.8ms, state53,443.0ms, region41,350.1ms, sea22,070.8ms,
campaign13,611.1ms and culture6,179.2ms. Private/publication copies total
9,883.9/9,486.3ms. The190 noninitial seals average611.94ms, median598.75ms;
their counters average30,118 newly digested nodes,20.13MB of tree-hash payload,
3,928 root fingerprints,12,777 newly frozen records and3.11MB of frozen bytes.
The first seal constructs143,130 records/47.07MB and takes996.10ms.

The worst computed year is not the maximum-hash year:180794 contains a558.72ms
hash together with several state/campaign spikes and two observed collection
increments. Initial canonical-cache warming alone therefore cannot establish
the strict year bound. Native allocation counters are unavailable, and GC
after-counts do not attribute an individual collection's full cost. Evidence is
under `native-r-reusable-f6-isolated/{playback.json,work-stages.csv,work-spikes.csv,
year-work.csv,canonical-costs.csv}` in the external overnight artifacts.

### Measured post-R exact allocation corrections

The observer now publishes read-only per-record-type count/byte summaries. An
additional relation-read trace is independently opt-in and off in ordinary
playback/native diagnostics unless `--worldgen-canonical-relations 1` is supplied.
It retains prior immutable memo bytes only inside one transaction and compares
fresh bytes; no metadata is added to historical relation records. It counts
fork invalidation as well as mutable getters, then drops its dictionary. The
scope/trace fixture passes51 assertions.

Actual19 warm F6 checkpoints construct192,706 frozen records/47.08MB; relations
account for182,922/42.44MB (94.9% of count,90.1% of bytes). Of those relation
refreezes,148,663 are byte-identical,25,416 differ and8,843 lack a prior memo.
That is85.4% unchanged among compared rows, or81.3% of all refreezes—not proof
that every unchanged row can safely skip invalidation. The mean checkpoint
performs50,513 mutable relation reads and clears6,986 memo-bearing rows during
forks. This is actual advancing evidence, not an inference from average row size.

The correction has a deliberately narrow trust boundary. `ForkOwned` clones
mutable relation values but may retain their immutable memo bytes only from the
simulation's already sealed private input. Ordinary/public `Copy` still discards
memo bytes for exposed rows, and mutable `Get` still invalidates them. Bounded
read-only diplomacy, trade/war gates and economic queries use `Peek`; mutating
relation helpers still request mutable records. No actor cadence or historical
content changes.47 assertions cover edits through retained references before and
after public/private forks, copy isolation, getters, read-only peeks, cancellation,
cold binary reads and concurrent public/private hashing. A5000-exposed-row fixture
allocates68,400 bytes for a trusted hash versus3,748,400 after an untrusted fork.

Actual20x5-year F6 playback remains exactly `93add43f...`, including every
intermediate control hash. Mean warm mutable reads fall50,513 to9,442; frozen
rows10,142 to3,869 and frozen bytes2.478 to1.022MB. Hash-stage allocation falls
796.78 to709.36MB across20 checkpoints (11.0%). A separate actual cold world
roundtrip/12 versus5+7/cancellation fixture passes6 assertions with unchanged
`2b8cc0c4818537d94437d80ede5bff3021d1a79355070de75045ad8cf4a53b0f`.

The next correction reuses a secondary tree SHA and five depth buffers only
inside a canonical root computation. The root CryptoStream SHA remains entirely
independent. Entry uses an allocation-free value scope; its secondary context is
lazy, nested hashes get their own context, and success/failure/cancellation restores
the caller's context and disposes the new one. Standalone tree fingerprints still
own/dispose their context. No tree/node/value is retained by the scope, and the
immutable logical Node remains its original three fields. No tree verification
is skipped; node counts/payloads are unchanged.60 assertions cover independent
literal bytes, nested/reentrant success and failure, concurrent contexts, disposal
and cancellation; provider147 and transport279 also pass.1000 standalone tree
fingerprints allocate5,904,960 bytes versus1,906,544 for a scoped full root hash.

The final actual20x5-year F6 fixture also fully validates the final public
publication, independently of trusted memo bytes, and preserves its source.
It retains the exact100-year control hash. Hash allocation falls further from
709.36 to431.36MB across20 checkpoints (39.2%, about13.9MB saved per checkpoint).
Combined savings against the pre-correction type-traced fixture are365.43MB/45.9%.
CoreCLR hash times3.913 to3.716 to3.566s are contention-qualified; they are not
native acceptance.

### Isolated native S: allocation corrections active, acceptance still fails

The S19 IL2CPP player includes both corrections above. Its isolated F6 tail
advances1007.6129 years in362.9217 seconds:2.77639yr/s, minimum rolling2.11288,
worst computed year1910.65ms at181121. It therefore still fails both requested
acceptance limits. Final hash remains exactly
`eb1a49f19de71711533357288c2db276363f7188e5d57d067504e4c07a002de6`.
Throughput improves only1.81% over R; observed Boehm collections fall395 to365.
Native allocation counters remain unavailable.

Hash CPU totals109999.95ms/191 stages, down6.20%; warm seals average573.84ms.
Warm new frozen rows fall12777 to4743 and bytes3.11MB to1.25MB, proving the
corrections are active. Tree counts/payloads remain unchanged. Other large costs
remain city85487.5ms, state53225.8ms, region41080.7ms and sea21837.0ms.
Private/publication copies total10412.9/10936.9ms. The worst computed year combines
a655.57ms hash with a605.98ms publication and a collection increment; it is not
an initial cold-cache year. Evidence is under
`native-s-trusted-scratch-f6-isolated` in the external overnight artifacts.

Further diagnosis is explicitly opt-in: top-level private canonical component
timers perform only one timing pair per root component, not per record, and do
not change bytes or cadence. The scope fixture now passes56 assertions. A
read-only warmed F6 copy audit measures13.91MB/private copy and15.07MB/public
publication. Allocation is spread across aggregate/imperial containers, geography,
port settlements and other records; these inclusive totals overlap. CoreCLR
times are qualified by concurrent replay/GC and are not native acceptance.

The actual20x5-year F6 component trace also retains `93add43f...` and validates
the source/publication independently. Its19 warm CoreCLR root hashes average
158.80ms under shared CPU: aggregate state85.13ms, relations22.73ms,
international19.78ms, campaigns8.12ms and towns5.73ms. These are existing
advancing private seals, not repeated hashing of a warmed stand-alone root.
Native component timing is still required before selecting another exact
optimization. Evidence: `canonical-f6-root-components-costs.csv` and sidecars.

A short V IL2CPP diagnostic (concurrent replays; not acceptance) advances106.83
years in31.04s. Its16 warm root components average aggregate260.87ms,
relations54.96ms, towns31.33ms, international29.27ms and campaigns12.23ms.
The first computed year still reaches1265.36ms. Evidence is under
`native-v-components-f6-shared`. This short/early tail cannot replace the failed
1000-year isolated S result.

The subsequent optional nested trace times fields only on three fixed containers
(aggregate, imperial, connections); it does not time each world cell or actor.
Its actual100-year F6 control remains exact. Warm checkpoints visit14977 dirty
branches, with only18.56% of their32 child slots present. Thus81.44% of branch
copies/recursive calls merely obtain the common zero digest. The narrow next
experiment clears a branch's1024-byte payload once and copies present children
only. The independent Merkle-byte/radix-boundary/removal/extreme-value tests,
dense-to-sparse reuse, malformed-shape rejection, nested/cancel/concurrency and
transport checks pass76/62/290 assertions, including explicit no-op identity and
complete ancestor pruning. Actual20x5-year playback retains
`93add43f...` with identical node/payload/fingerprint/frozen-record counters and
independent source/final-publication validation. Warm CoreCLR root time falls
136.10 to134.07ms (shared CPU); allocation is unchanged. The actual old-world
roundtrip/12 versus5+7/cancellation fixture passes6 assertions with unchanged
`2b8cc0c4...`. Native measurement remains required; this is not a throughput
acceptance claim.

The W short diagnostic reaches the exact same year180306/hash`7294322e...` as
V with unchanged towns/states. Under different concurrent load it achieves
2.975yr/s, with1448.75ms worst year; neither short run establishes acceptance.
Its warm fixed-container timings identify geography56.72ms, aggregate polities
41.20ms, imperial governments32.09ms, port retries27.75ms and service recovery
17.19ms. These inclusive fields contain many local bags; imperial government
records already have typed writers, while pending-port records still reflected.

Two further exact corrections are tested separately from W. The persistent-tree
setter returns immediately when the changed value/child proves the node nonempty;
deletions still check/prune empty nodes. A mixed65536-edit benchmark includes13312
no-ops,15859 removals/default values and7097 other signed values. Before/after
allocation and hash are exactly81,198,256B/`ed2e1826...`; shared-CPU timings overlap,
so no speed claim is made. Actual100-year and cold12-year controls remain exact.

Local bags now bypass unrelated type-dispatch families and use immutable literal
framing plus thread-local scalar scratch; every live count/tree is still read.
Pending-port arrays receive an equivalent typed writer, preserving ordered null
and duplicate entries.83 independent literal/public-mutation/copy/reentrant/
concurrent/cancellation assertions pass. The actual100-year control remains
`93add43f...`; CoreCLR pending-port encoding falls3.40 to1.70ms/check and overall
warm root153.27 to149.12ms under shared CPU. Hash allocation falls431.43 to429.31MB
over20 checkpoints. The combined old-world binary roundtrip/12 versus5+7/source
isolation/cancellation fixture also passes6 assertions with exact`2b8cc0c4...`.
Final native/dense-world measurement is still pending.

A later fused single-traversal`With`/setter experiment also preserves the actual
100-year control, but its mixed-operation benchmark regresses: process-warm
medians about40.8 to45.3ms; optimized non-tiered medians40.1 to42.6ms. Hash and
allocation are unchanged. It was rejected and reverted before checkpoint;
the nonempty-node shortcut above remains. No extra native build or cold-world
load is spent on this rejected candidate.

## Current26 dense native acceptance,17October

The frozen nondevelopment IL2CPP review player
`player-history-32-overnight-aa-review` includes the retained exact bag,
nonempty-node, sparse-branch, trusted-row, scratch, typed-codec and reusable-CNG
optimizations. Its build-input manifest SHA256 is
`b3f3c2067d3758d972341f434e6e70853f644827424dd3c5a7b6324f87f83501`;
Core is `059a0dcc4db0cb3e6f057842577ce0c22ca41419d5336dbcb803225cca9f5e01`.
Component timers remain off. Native provider13 and tree-transport12 smoke
vectors pass. All other team builds, world loads and renderers were held.

Current ContinentsPlus26 **fails** strict1000-year acceptance:1004.0776 years
in859.8857s,1.167687yr/s, minimum50-year rolling1.027484yr/s, worst computed
year2279.691ms at166693,473 Boehm collections. Input165700 is
`4113cad9963a9bc700899d05cc728ad52e924f81e3a446298d63d137cfb521fe`;
final166704 is `7925d810ab8f84da14b956f266ca0303f5f6650af0321d8b30ac8b238787c791`.
Courts grow565 to715 during the measured span. The input has4251 inhabited
towns plus945 abandoned records; the player's final6631 is lifetime settlement
records, NOT final active towns, and must not be compared directly with4251.
No simulation error occurs; the input file remains unchanged.

Full-run CPU totals are hash246.290s/366 checkpoints, city244.693s/5.078million
events, sea107.210s, state72.447s, region60.945s, checkpoint copies29.996s and
publication copies28.143s. Maximum single observed city event is321.957ms,
state195.189ms, sea173.781ms and region150.119ms. Native allocated-byte counters
are unavailable; these measurements must not be represented as allocation data.

Cold sealing costs1665.877ms and freezes289461 rows/94.14MB. Excluding it,
warm root hashes average670.156ms (p95 888.206,max1455.152), rising from545.217ms
in the first50 warm checks to752.232ms in the last50. Each warm seal processes
about19.95MB of dirty-tree payload and4082 newly frozen rows/1.025MB. Warm newly
frozen bytes are chiefly relations318.66MB across1.374million row freezes;
this alone does not show whether those rows' actual fields changed. The worst
computed year includes a782.826ms hash and323.031ms publication copy crossing
a GC, not simply one pathological battle. Evidence is retained in external
`native-aa26-cp-current-isolated` CSV/JSON files. Current Earth26 runs next,
sequentially under the same isolation; no performance acceptance is claimed.

The sequential current Earth26 run also **fails**:1004.1966 years in716.4112s,
1.401704yr/s, minimum rolling1.127319yr/s, worst2531.836ms at158661,487 Boehm
collections, no simulation error. Input157700 is
`153e69ae9800322c28105ba0e355affa921ab673360387c65af524ec5d1df096`;
final158704 is `211eea136385f6c3cf6263c2d8b54ce33ada2b6c04f80cfb531042c35b420a36`.
Final5918 is again lifetime settlement records, not final active towns;
active courts are642. CPU totals: city190.752s/4.075million events,
hash170.285s/310 checks, sea115.006s, state68.553s, region64.137s,
culture28.824s, campaigns22.013s, copies21.523+17.052s. Cold root944.277ms
freezes138596 rows/44.20MB; warm root averages547.952ms(p95 815.062,
max1352.748), with23.58MB dirty-tree payload and5210 new rows/1.310MB per
check. Worst-year spikes include624.655ms hashing and233.238ms campaigning
across a GC, alongside actual city/sea/region/state work. Neither initial
setup timing nor excluding the first hash can turn either run into a pass.
Both player processes exited and isolation was released at12:17:21UTC.

## Exact relation read-versus-edit memo,17October

The subsequent relation optimization distinguishes exposing a mutable record
from actually changing one. Its public getter still detaches the record for
the receiving branch, but no longer discards exact private bytes merely because
the caller read it. Before **every** owned canonical reuse, a nonserialized
snapshot compares all six canonical integers (`A`, `B`, `Trust`, `WarSince`,
`TruceUntil`, `LastBattle`) and both booleans (`Trade`, `Alliance`). Missing
bytes or any changed scalar rebuild the full preceding canonical record.
This is exact field comparison, not collision-prone hashing or trust in an
unchanged object reference.

An independent source/test review checks that public hashing still reads actual
fields, public collection forks clear exposed-record memo bytes, owned forks
detach exposed mutable values, and frozen byte arrays do not alias writable
encoding scratch. Both memo bytes and the scalar snapshot are internal and
`NonSerialized`; neither enters saved history or canonical public-field output.
The owned-history context remains thread-local and is restored after failures.
No dates, actor frequency, rule markers, RNG, relation order or diplomatic
scalars change.

The main build passes71 trusted-memo and82 canonical assertions. Coverage includes
each of the eight fields edited through a reference retained across an owned
hash, restoration to its original value, read-only reuse, source/public/owned
fork isolation, concurrent public edits, cancellation and cold serialization.
See `HistoryTrustedRelationMemoTests` and `HistoricalCanonicalTests`.
The actual preceding26 final-Earth twelve-year continuation remains exactly
`4232e559789f84884b94b87a2609a9b22892fb7ab26e9c210ad3feaa109c755a`.
The activated27 build containing the memo also passes the separate genuine
earlier-frame full-world12 versus5+save/load+7 cold control documented in
[civil maritime continuity](HISTORY32_CIVIL_MARITIME_CONTINUITY.md). Neither
these exactness checks nor the pure tests establish a strict native speed gain.

## Native28 control replay (17 October,13:09UTC)

`player-history-32-overnight-ad28-review` is a nondevelopment IL2CPP build.
Its staged Core exactly matches frozen28 SHA256
`20213669a322a6ba2dbe830a2191b59b1b9004df4bf9da370da95205060b2495`;
the build manifest hash is
`322bdae45d2134fc085aca52231918f592ce00ec701095b0d774aa1c96cedc38`.
Temporary Standalone/stripping overrides were removed with the project settings
returning to a clean diff; the preloaded-asset setting was restored by the builder.

The actual current28 Earth final world is freshly replayed through all nine
observer eras in the native player, ending157700 at exactly
`db43d25fa111a430f5fbead5491738f3467091c83a84d6b4188c1fea98a34cff`.
The input world file remains byte-identical. Valid offscreen camera-map images
and `verified.txt` are in `native-ad28-earth-eras`. Hidden-window HUD images are
black under the known host capture limitation: they are NOT HUD validation.

This is a native determinism/rendering control, not isolated speed acceptance.
Native28 CP and strict benchmarks were deliberately deferred while the separately
versioned29 candidate was prepared. The two current26 strict failures above
remain the latest completed dense acceptance measurements at this checkpoint.

## Final29b native replay and strict current-world checks

`player-history-32-overnight-ae29-review` is a nondevelopment Release IL2CPP
build, completed13:15UTC on17 October. Its authoritative Core SHA256 is
`4eca1ddd14068acff1c2fa71a30ea069970a28c1ee06fab60c47a954e1e28428`,
Serialization is `cb1605e92ac892eb97c3d6e636d3dc6b21ac41fcda5982d68691b81ef6932499`,
and input-manifest SHA256 is
`582056fcd761f0e430117cefa1efc45783efaab1c54bc0e2fc3950b4670383f8`.
Temporary Unity Standalone/stripping overrides were removed after the build;
project settings returned to a clean diff. The player verifies13 SHA-provider
and12 immutable-tree transport vectors before timing. Both actual current29b
worlds replay through all nine observer eras to their exact saved hashes:

- Earth158000: `aadce44b43b5823970e45c80d53ec9ebfb415294cf65893b8a032327606936e3`.
- CP165400: `c69af098ae184c9f8492e2f180e0337fe7527467179fcc74f92430ce600d1703`.

`native-ae29-{earth,cp}-eras` contains camera-map images and `verified.txt`.
Input files remain byte-identical. Hidden-window HUD captures remain black
under the existing host limitation and are not HUD acceptance. These replay
checks are not speed measurements. A separate full fresh28 control under29b
also preserves the preceding native28 Earth hash exactly.

The strict CP29 run in `native-ae29-cp-current-isolated` **fails**:
1003.675823 years in863.104198s,1.162868yr/s, minimum rolling0.946059yr/s,
worst2449.4802ms at166259,446 Boehm collections, no simulation error.
It ends166403 at
`1276ec51551167fafdc3373cd8c87a4f0a41fb1e0b75b1111dcedd019dd07e59`.
The starting world has4093 active towns and556 courts; final6787 is lifetime
settlement records, not an active-town count. Final active courts are727.
Both the initial cold seal and all publication copies are included. Team
heavy jobs are held throughout the sequential CP/Earth pair; sampled unrelated
Python/GPU contexts are idle, with ordinary desktop processes still present.

CP CPU totals are hashing236.707s/361 checks, cities231.322s/4.933million
events, sea123.030s, states82.000s, regions61.576s, checkpoint copies32.085s
and publication26.718s. Cold hashing takes1588.54ms with263268 newly frozen
records/86.36MB plus20.00MB dirty-tree payload. Warm roots average653.065ms
(p95 827.849,max1406.725),21.14MB dirty-tree payload and1638.54 new records/
459126 bytes per check. Seed-landscape memo counters are4689597 hits and
415139 misses. Native allocation counters are unavailable; the three GC
columns represent the same Boehm collection counter and must not be summed.
The current29 and preceding26 trajectories differ, so their speed ratio is
not an isolated causal estimate of any individual optimization.

The immediately sequential Earth29 run in
`native-ae29-earth-current-isolated` also **fails**:1004.931026 years in
781.472793s,1.285945yr/s, minimum rolling0.996464yr/s, worst2659.6676ms
at158935,478 Boehm collections, no simulation error. It ends159004 at
`e340137a0175f4004840ebb2b31ae4ca2e50cc54bafe5332168be0a9bb0dc49c`.
The input has3748 active towns and578 courts; final6366 is lifetime settlement
records, while final active courts are748. Input bytes remain unchanged.

Earth CPU totals are cities202.606s/4.445million events, hashing197.248s/
335 checks, sea119.250s, states76.686s, regions66.102s, cultures32.316s,
checkpoint copies24.180s and publication21.203s. Maximum individual city
work is400.779ms; hash1477.181ms; checkpoint copy788.054ms; publication
822.826ms. The cold root takes1082.318ms for167114 new records/54.19MB
plus25.47MB dirty-tree payload. Warm roots average587.277ms
(p95 811.524,max1477.144),23.12MB dirty-tree payload and2098.67 new records/
590149 bytes per check. Landscape counters are4202142 hits/381984 misses.
Worst-year work combines a663.948ms seal with real city/campaign/culture work
and a collection; neither a single slow constructor nor unrelated load
contention explains away this strict failure. Both owned player processes
have exited and the isolation hold was released14:03:18UTC. These complete
current-world failures supersede any earlier near-target small/old-world
measurement as evidence of the remaining native performance gap.

## Display-only AF29 native handoff

The subsequent `player-history-32-overnight-af29-review` changes recognized-
claim styling, not historical rules. It is a nondevelopment Release IL2CPP
build completed14:07:43UTC. It embeds the **same byte-identical** frozen29b
Core `4eca1ddd14068acff1c2fa71a30ea069970a28c1ee06fab60c47a954e1e28428`
and Serialization
`cb1605e92ac892eb97c3d6e636d3dc6b21ac41fcda5982d68691b81ef6932499`.
Only the reviewed Presentation DLL changes, to
`337627a87c60a97d56d6b76f2ddfc326f98a09ef2a9b68c6279dd6d083211ed6`;
its isolated external tests pass85 assertions against those exact frozen
references. The AF input-manifest hash is
`98e3add6554f393399ad9cb799ef5a6513c54cd5f0cdd30ab180afe2d548dd9d`,
and GameAssembly hash is
`0fd9bde6d7f5637244e712f1dd66a85da329c445067bc0c1b46ce862da4174e0`.
The editor exits successfully, automatic Standalone/stripping entries are
removed, and project settings return to a clean diff.

Both current final worlds pass the bounded native `--worldgen-history-view`
check without advancing history. Six offscreen camera overlays per world are
written in `native-af29-{earth,cp}-final-views-verified`, with exact unchanged
Earth `aadce44b...` and CP `c69af098...` simulation hashes. Both States maps
were actually viewed: pale recognized-only claims are distinct from saturated
control, and the map remains complete and legible. This is a display check,
not evidence that the remaining geographic or political realism gaps are fixed.
Input save-file SHA256 remains
`3305575a1ea099b4da6bdc03d66d31f35b20a16b8cbd66d166f997c1cc82bbfb`
for Earth and
`3e75784ac3da8666d2ffadbdb92d9e44f7d387e8841d96708f5ff24f3f50f985`
for CP. No new save is written by this static check: it proves preservation
of the existing saved input and live simulation hash, not another cold-save
roundtrip. Hidden-window HUD captures are still not a visual pass.

One Earth startup log contains an unattributed native `Curl error 23` warning.
World loading uses local file reads, not curl; the exact background request
cannot be identified from that line alone. Both processes subsequently render
all six overlays, pass their unchanged-hash checks and exit0. Thus the warning
is nonfatal for this check, not an ignored world-load/render failure. The two
earlier empty output directories without the `-verified` suffix came from an
incorrectly escaped custom launcher before player startup, and are not proof
artifacts. All owned native jobs are released by14:11:06UTC. AE remains the
untouched strict-benchmark artifact, and neither AF nor the styling fix changes
the two recorded performance failures.
