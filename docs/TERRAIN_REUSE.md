# Terrain loading/reuse, step2

## Changes

- Retired active-chart meshes enter a bounded inactive LRU rather than being
  immediately destroyed. Limits:128 products and96MiB of accounted vertex/index/
  height buffers, in addition to active coverage. Exact keys include tile,
  resolution and all four stitch-edge requirements. Restoring an exact product
  reuses its existing GPU meshes without regeneration/upload. Cache textures are
  released while inactive; accepted resident coverage masks refresh on restore.
  Evicted buffers/chart products share a two-tile-per-frame retirement budget,
  so clearing the LRU during travel does not synchronously destroy128 meshes.
  The96MiB limit is accounted retained buffers, not total VRAM: active terrain,
  object metadata and temporarily deferred retirement are additional.
- The GPU cache is deliberately CHART-LOCAL: manual teleport, chart adoption
  and explorer destruction clear it. Reusing old projected vertices in another
  chart would be incorrect. This is not a persistent world-wide mesh cache.
- Each worker owns a32768-entry exact geographic-point sample cache shared by
  its two existing terrain lanes. Regular grid/halo samples survive overlapping
  requests and edge-only rebuilds. No rounded lookup positions, LOD-dependent
  substitute heights or new raster are used. Channel-specific refinement and
  scatter are not claimed to be cached by this mechanism.
- Edge stitching indexes matching physical edge lines/ranges instead of scanning
  every other tile for every tile. Literal old floating-point comparisons are
  retained. Both active and prepared chart plans use it. An integer-normalized /
  two-pointer draft failed exact old-edge checks and was rejected, not published.
- Resident coverage indexing now visits actual ancestors instead of testing all
  tile pairs. Retiring already-disabled charts no longer dirties active coverage
  every frame. Coarse/fine visibility behavior remains the accepted step1 rule.
- Movement prediction uses recent horizontal velocity, a nominal0.75s lookahead
  bounded to six finest-tile widths, and at most12 immediate future candidates.
  Only one speculative terrain job can occupy a spare existing lane; current
  immediate terrain remains first. Speculative products stay invisible in the
  same bounded cache until actually requested. Stopping/changing direction
  cancels obsolete predictions. This cannot guarantee instant detail at arbitrary
  flight speeds or across uncached chart changes.
- Completed current terrain precedes background products. Edge-obsolete jobs
  cancel early; pending jobs already built in an adopted prepared chart retain
  their work instead of being discarded solely because its generation changes.
  At most one mesh installation or cached activation occurs per frame. Two
  worker lanes and existing scatter limits remain unchanged; a single atomic
  upload still is not a hard millisecond guarantee.
- F3 reports retained buffers, mesh cache hits, requests and predictive requests.

## Unchanged

Hyper11 terrain/water, authoritative world/history, normal LOD tile selection and
resolutions, controller heights, material shading, fog, vegetation definitions,
navigation tools and the accepted partial-coverage shader are unchanged. No
sample/mesh cache is saved to a world file. This does not implement steps3–7.

## Focused evidence

6241 shared assertions compare indexed edges with literal all-pairs results on
16 plans across scales0.01/0.1/1/2, test LRU byte/count eviction and ownership,
edge-variant keys and bounded point samples, and compare actual river mesh
float/index buffers exactly with and without caching. Source hash is unchanged.
In the diagnostic, one cold/warm river-tile build measured47.9/30.9ms; an edge
variant measured32.9/23.2ms with3937 cumulative point hits and1227 retained points.
Indexed-edge diagnostics after first JIT were about0.5–3.1ms. These CLR timings
are host/build-contention diagnostics, NOT native frame-time/FPS guarantees.
13059 prior coverage/navigation pure checks pass. Native cache/coverage, actual
out-and-back loading, predictive request and final worker comparisons are
recorded in VALIDATION.md after completion; owner testing is acceptance.

The initial out-and-back harness incorrectly required an inactive-cache hit
even when original fine tiles were still resident as fallback. That assertion
was rejected: residency reuse is valid too. The revised route gives background
replacement a brief settling interval and reports resident versus inactive
reuse; the independent native shader/cache test explicitly forces retirement
and restoration of the SAME mesh object.
The first prediction probe also aimed east back into the already visited area;
correctly skipping resident products is not a prefetch failure. The final probe
aims west into an unvisited area, rather than weakening the no-redundant-work rule.

During the build C: fell below250MiB free. The old generated
`artifacts/player-local-terrain-controls2` and `player-local-terrain-controls3`
outputs were moved intact to their corresponding
`E:/WorldGenBuilds/relocated-player-local-terrain-controls2` / `controls3`
destinations, keeping junctions at the original paths and verifying executable
hashes. About2.6GB was freed before temporary compiler output was reclaimed;
no saves/source files or rollback player content were deleted.
