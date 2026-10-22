# Forest placement and identity-preserving distance rendering

This derived tree overhaul follows the owner's 22 October request. It changes
tree placement and tree art intentionally; the preceding performance audit must
not describe those changes as quality-identical optimizations. Terrain geometry,
water, grass/herbs/shrubs, saved ecology/food webs and world/history data are not
changed. Existing saves work without regeneration or schema migration.

## Research decisions

- [Guerrilla's Horizon placement presentation](https://www.guerrilla-games.com/read/gpu-based-procedural-placement-in-horizon-zero-dawn)
  emphasizes deterministic, locally stable placement, habitat/density fields and
  scalable sampling. We adopt the stable-address principle, not their authored
  ecotope pipeline or GPU implementation. The official 50-slide PDF was read,
  including forest-edge density, footprint-aware patterns and scheduling caveats.
- [GPU Gems 2, tree/shrub layers](https://developer.nvidia.com/gpugems/gpugems2/part-i-geometric-complexity/chapter-1-toward-photorealism-virtual-botany)
  supports meshes for primary structure, textured secondary foliage and
  angle-dependent far representations. It also describes stable planting grids.
  This is not imported SpeedTree middleware or an exact reproduction of a game.
- [Epic's impostor documentation](https://dev.epicgames.com/documentation/en-us/unreal-engine/impostor-baker-plugin-in-unreal-engine)
  explicitly documents upper-hemisphere tree impostors in Fortnite's non-Nanite
  path and three-view interpolation. We retain our existing depth-assisted,
  relightable three-view shader, rather than replacing trees with unaligned discs
  or a single fixed camera image. Smaller triangles do not make fill cost free.
- [Silva ecosystem research](https://www.jvrb.org/jvrb/article/download/10.2013.1/html?inline=1)
  separates site suitability, neighborhood effects and reusable distributions.
  Its ray-traced Wang-tile implementation is NOT our implementation; importantly,
  excessive multiplication of suitability factors can accidentally erase forests.
- [Smithsonian rainforest layers](https://striresearch.si.edu/rainforest/home/rainforest-layers/)
  motivates a connected canopy, smaller understory and sparse emergents—not
  equally sized generic trees at one height. These are static size cohorts,
  not simulated tree ages or competitive growth.
- [FAO woody formations](https://www.fao.org/4/w4442E/w4442e05.htm)
  distinguishes closed forest, open woodland and varying savannah formations.
  No universal stems/ha target applies to all biomes or measurement thresholds.
- [SANBI umbrella-thorn description](https://pza.sanbi.org/vachellia-tortilis)
  informs short spreading drought-adapted forms. Not every savannah tree is an
  umbrella, and no literal Earth species is hardcoded into the catalogue.

## One planting definition

`LocalScatter.TryTree` is the sole candidate predicate for both local scatter and
distant pages: same cube-sphere address, seeded jitter, surface/water exclusion,
blended forest potential, species niche, slope, size, colour, yaw and identity.
The 128×128 lattice in each approximately 512 m source patch supports dense
stands without a global tree list. IDs remain disjoint from rock/shrub channels.
Derived versions: tree-flora-3, local-scatter-tree-flora-9; worker wire7.

Closed forests use a high connected noise floor and occasional gaps; open cover
uses a more perforated stand field. Blended source profiles avoid cell-edge
yes/no placement. Productivity/fire and existing soil/moisture/climate/permafrost/
salinity/flood suitability condition regional support. Grasslands and savannahs
have much lower tree potential. Warm lowland jungle excludes needle crowns.
Regional species remain generated descendants, not named Earth model assignments.

Tropical broadleaf lineages use taller clear-bole crowns, basal buttresses,
29–39 m nominal canopy adults, thinner boles, smaller understory cohorts and rare
larger emergents. Static broadleaf cohorts are 69% smaller, 30% canopy-sized and
1% emergent; this is renderer calibration, not a universal rainforest census.
The large number of small individuals and scarcity of large stems is informed
by [primary size-structure research](https://pmc.ncbi.nlm.nih.gov/articles/PMC4679035/).
Open-woodland lineages are shorter and broader with a high spreading crown.
Their adult models seed several load-bearing outward limbs before bounded
colonisation fills the crown; this is not merely stretching a narrow forest
crown. The same 220-node limit remains, with shared near/middle anatomy and
views baked from that actual geometry.
Founder14/province4/region5/form3/strategy2/variant3 anatomy bits preserve distinct
biological identity; cached render topology can share a family/strategy view.

## Hierarchy, not an unrelated distant population

World-addressed forest pages are independent of terrain tile resolution and
chart origins. Level zero enumerates exact candidates. Through 4×4 nodes the
aggregation census is exact; sparse nodes remain exact through 16×16. Larger
nodes use a bounded deterministic permutation (8 probes in closed cover, 32 in
sparse cover). Every representative
must be an actually admitted local tree. Selection favors projected crown area,
so numerous small understory stems do not replace the visible canopy. Estimated aggregate count is NOT a full
far-world census and can miss tiny isolated distant plants. Crown coverage may
widen only on aggregated nodes, bounded by sampled admitted spread, node size
and estimated population; one isolated admitted tree cannot become a giant
invented stand. Roots/heights/yaws never move to an invented
stand centre. This is a bounded approximation, not a pre-baked exact group HLOD.

The CPU/GPU handoff is 280–350 m at Medium, still individual and exact, with a
further 256 m exact-page guard before any
aggregation. Beyond it, distance/error selects coarser nodes. Planner targets
roughly 0.008×distance candidate footprints, relaxing this when the page budget
requires it. Expected live occupancy also limits the plan, reserving replacement
capacity below the resident pool ceiling. Occupancy uses Bernoulli candidate
admission, not the incorrect Poisson estimate that under-budgeted dense level-zero
pages during an intermediate test. This is a heuristic, not a certified pixel-error bound, and does
not depend on which way the player faces. The normal plan targets 4,096 pages
and requests stop at 524,288 resident representatives (up to two already in-flight
1,024-root pages can finish beyond that soft ceiling). Exceptional budget pressure can delay further
coverage rather than submit millions of full trees.

External worker command3 performs tree-page preparation in the existing bounded
worker pool (at most two forest requests within the existing total effort limit,
never ahead of pending near terrain). All terrain, backdrop, prediction, prepared
chart and scatter dispatchers now count in-flight forest requests too. Nearby
ground pressure can cancel a forest request, and cancelled coverage is retried;
obsolete-plan jobs cancel rather than occupying a worker until completion.
Completed forest payloads awaiting installation do not keep a CPU permit when
drawing is disabled. A worker-owned exact tree-channel LRU (128 source patches / 96 MiB
accounted records) reuses already generated near results, including exact
negative results; source arrays are immutable and lookups are synchronized.
It is transient RAM, not save storage. Main thread installs at most one completed page per update. Empty
ready pages retain ownership. Coarser fallback survives until replacements are
ready; geographic payloads are retained/reprojected across chart changes.
Ready finer ownership suppresses coarser records using the REAL representative's
cube address, including cube face—not the temporary terrain-chart coordinate.

Local and far renderers share canonical family/strategy art, preserving each
instance's root, height, width, yaw and regional pigment. Fine branch variation
can simplify. This sharply reduces the active bake working set: the old six
variants × many regions could exhaust 96 slots and leave far trees as complete
middle meshes forever. Stand bakes are now individual trees, not unrelated 4×4
clusters. Near/far screen-door intervals are complementary. Readiness does not
restart a timed vegetation fade. Page-level approximation changes still need
owner visual review; perfect invisible transitions are not certified.

GPU frustum/range/ready-finer culling remains; existing conservative hill tests
remain. Exact unchanged visibility inputs reuse GPU lists. Historical holes
compact when at least 256 slots and one-third of a batch are free, remapping
field slot references. Common scene publication and approved wind are untouched
by placement. No per-tree GameObjects or normal-play GPU readbacks are added.

CPU scatter bins separate trees from the other unchanged plant channels. A
tree-only bin can skip individual admission only if its root AABB is entirely
beyond the local limit, or entirely beyond the individual handoff AND every
member's source page has ready forest ownership. Incomplete pages retain the
literal per-tree fallback. A small conservative numerical margin protects range
boundaries. Ownership caches invalidate on installed/removed/readiness changes,
not camera turns or the unrelated 0.2-second hill-visibility publication. Crown
and shadow bounds still control ordinary frustum/shadow tests; root bounds are
used only to prove the existing root-distance admission cannot contribute.

## Initial validation

165,723 Core assertions cover exact near/far census, actual-root representatives,
unchanged visits, transport, cube seams/poles, partitioned bounded plans and
unchanged source hashes and the tree-density map's shared open/closed stand
field. The map clamps the combined admission probability, matching planting even
when a productive raw profile exceeds one. Controlled face-centre fixtures: tropical rainforest
479.5 placements/ha, temperate forest446.6, warm boreal fixture400.7, savannah1.3,
grassland0.1, desert/tundra0. These are broad renderer calibration fixtures, NOT
ecological field measurements or all-region guarantees. A rendered cane stand
can contain multiple culms, so these are not literal botanical stem censuses. Existing tree-field
suite: 220 assertions pass. Exact height/potential audit: 8,044 checks pass.

Packaged RTX4060 checks draw eight forms/strategies and their baked counterparts.
Images inspected for canopy/needle/savannah shape and retained crown occupancy.
Those neutral-background images do NOT establish actual-world art acceptance
or performance. Native full-world results and remaining limits are recorded in
the final validation entry, not inferred from a successful build. The first
full-range identity fixture stopped on an overly strict 1e-9 chart-roundtrip
tolerance: Core's V math itself quantizes at 1e-9. Isolated 60 km projection
checks measured 0.000637–0.001274 m error; the native test now reports physical
error against a 2 cm limit while retaining bit-exact source-tree comparisons.
No FPS rows from that failed run are accepted. An earlier loading-only run was
stopped to add progress telemetry; it supplies no accepted performance result.

Full Core hierarchy stress checks on the unchanged saved world:

| Source-cell site | Range | Pages | Actual representatives | Empty pages | Candidate probes |
| --- | ---: | ---: | ---: | ---: | ---: |
| Rainforest 98666 | 60 km | 454 | 366,996 | 4 | 3,105,728 |
| Hilly rainforest 88860 | 60 km | 679 | 109,989 | 325 | 19,842,448 |
| Rainforest 98666 | 200 km | 471 | 339,633 | 33 | 4,219,040 |
| Hilly rainforest 88860 | 200 km | 822 | 116,669 | 420 | 24,320,144 |

Every hierarchy fits the runtime representative budget and has no duplicate
tree IDs. The longer range can select fewer total representatives because the
planner relaxes projected footprint to preserve its resource budget. Native
benchmarks move to a nearby dry spawn; their page/root counts are therefore not
expected to equal a source-cell-centre census. The 200 km checks took 272.11 s
and 82.32 s in Core on this host, with compilation initially running alongside;
these are functional workload timings, not isolated game-loading benchmarks.

## Cost and approximation limits

The hierarchy stores actual representatives, not millions of full tree meshes.
Each GPU root is 64 bytes, plus visible-index capacity and page/CPU source records.
The working set is larger than the rejected sparse/disconnected forest fields;
the exact-source LRU adds up to 96 MiB accounted records. Shared local and far
views remain separate material/shader paths, so identical atlases can exist in
both caches. Tested full 60 km scenes use about 72–91 MiB of baked-view texels
before driver overhead. No disk/save tree raster is added.

Large nodes use a bounded sample, not a global census. Tiny isolated far trees
can be missed; aggregated crowns approximate coverage inside a projected node,
not the exact positions of every leaf/gap. Population estimates and planner
occupancy are heuristics. Exact near/far roots, water exclusions, empty fine-page
ownership and the individual handoff are tested independently of that distant
approximation. Reprojecting retained source payloads is synchronous on chart
replacement; source mesh creation and final texture uploads can still hitch.
No universal no-pop, jungle-realism, memory-budget or 100-FPS certification is
inferred from a finite test set.

## Delivery and next work

The release checkpoint is `artifacts/player-forest-audit-complete`, IL2CPP
BuiltUtc 2026-10-22T14:25:32Z. Native full-world checks compare 9,776 dense-scene
and 2,096 hilly-scene trees in three alternate charts: prepared and ordinary
matrices/spatial bins match exactly. Every tested worker/player tree identity
matches; empty ready pages retain ownership. The tests use actual roots, not
only a synthetic visibility count. Shared worker-permit and in-flight forest
socket-cancellation checks also pass. Forest preparation fills idle permits
after current, backdrop, prediction and prepared-chart terrain scheduling.

Ordinary walking passes the handoff fixture with 0.0131 m maximum clearance
error. Its captured habitat is sparse desert, not dense-forest art acceptance.
The unchanged 700 m/s flight stress FAILS: prepared terrain does not complete
three chart crossings within 100 seconds. That extreme-speed streaming limit
is reported, not hidden by relaxing the test or changing terrain LOD selection.

See [the performance audit](PERFORMANCE_AUDIT_2026_10_22.md) for final paired
measurements, layer-off comparisons, settings and caveats. The 100-FPS floor
is not met. Further forest gains likely require reducing transparent/cutout
fill and redundant canopy coverage through a separately validated representation
change; another round of small CPU cleanups alone is not a supported promise.
Coarse actual-root groups must continue to preserve clearings and sparse trees.
Dense-canopy grouping, tighter view bounds and screen-coverage-based selection
are candidates for measured experiments, not features claimed in this delivery.
