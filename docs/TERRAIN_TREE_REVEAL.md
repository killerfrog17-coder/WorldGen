# Terrain and tree loading frontier — 26 October 2026

Follow-up: the area-only readiness guarantee below was insufficient for coarse
backdrops. [The rendering repair](TERRAIN_RENDERING_REPAIR.md) adds requested-detail
readiness, removes exposed perimeter walls and unifies shared fog colour handling.

The owner reports trees temporarily floating and terrain becoming visible long
before distant trees. The requested behavior is one outward fog border that
waits for enabled tree coverage, accepting slower terrain reveal.

## Causes found in source

* `UpdateStreamingFogBody` previously checked only resident terrain footprints.
  A coarse backdrop could therefore certify a clear horizon without forest pages.
* `ScheduleForestWork` was called after current terrain, backdrop, predictive and
  prepared-chart requests. Those requests could repeatedly occupy every worker.
* Ground-change notifications only included level-zero terrain. Local scatter
  projection also waited up to 250 ms, and forest reprojection skipped all
  nonzero forest levels. Coarse terrain and aggregate stands could consequently
  disagree in height until a later refresh.

These are concrete loading/grounding defects, not a runtime capture proving the
sole cause of every reported floating silhouette.

## Changes

The ordinary reveal distance is now the minimum of terrain and tree readiness.
Nearby required coarse scatter chunks count only after their payload is installed.
The distant forest counts only ready pages whose actual-tree view textures are
baked. An installed empty page counts as ready: treeless terrain does not need
fake trees. Disabled trees, zero tree density and unavailable/disabled distant
forest rendering do not impose impossible forest requirements.

`ForestCoverageFootprint` indexes ready cube-face pages. A retained ready ancestor
or a complete finer union can satisfy a requested page. Partial unions descend
only into missing quadrants; clipped cube-face edges are handled explicitly.
Unbaked pages and jobs in flight cannot certify coverage. Bounds are conservative,
so the border can wait for some nearby offscreen or treeless pages. This is an
intentional camera-wide guarantee, not a frustum-only loading trick.

The previous forest plan remains usable during a camera-movement replan, bounded
by its remaining covered range. Coverage checks retain the existing 150 ms
cadence, safety margin around gaps and smooth outward fog expansion. They do not
wait for every tile across the entire horizon before exposing the next ring.
The explicit distance-only diagnostic baseline retains its bypass.

When trees are holding back reveal, one forest request can precede distant
terrain scheduling. Single-worker loading alternates successful terrain and
early forest requests. Immediate ground and overdue chart handoffs retain
priority. The existing worker permits, page-install bound, root budget, tree
bake budget and hierarchy remain. Failed forest requests retry after a two-second
backoff instead of being permanently skipped behind the new readiness gate.

Terrain upload, cache restore and retirement now notify grounding at every LOD.
Affected local scatter and all affected forest levels reproject before that
frame's draws, without the old 250 ms throttle. Aggregate stands additionally
use bounded finite-difference ground slopes; individual trees remain upright.
Slope-aware field bounds remain conservative. Work is driven by terrain changes,
not a new full-root projection pass every settled frame. Terrain changes can
still incur projection/upload cost; no new FPS or frame-time guarantee is made.

F3 reports separate **Ready coverage: ground / trees** distances beneath the
existing reveal line. No new user setting is required.

## Validation and limits

Validation follows the existing owner compile/package-only restriction. Shared
Presentation compilation succeeded with zero warnings/errors. Release IL2CPP
packaging exited 0, BuiltUtc `2026-10-26T16:45:42.8145960Z`, with no C#/shader
errors; existing obsolete/unused-field and shader advisories remain. The build
script verified authoritative DLL inputs, and eight changed source hashes stayed
identical through packaging. The existing shortcut now selects
`artifacts/player-terrain-tree-reveal/WorldGen.exe`; the preceding
`player-terrain-surface-art-v1` build remains the running rollback. Build-generated
project preference defaults and unrelated importer whitespace were restored.
No suites, generated-world fixtures, player launches, gameplay captures or FPS
profiling are run for this change. Visual behavior and loading cost await owner
review; terrain crossfades and approximate aggregate crowns are not an exact
tree/terrain collision system.

Placement rules, tree identities/density, terrain shape, surface materials,
shadows, fauna, simulation data and saves are unchanged. The running previous
player stays open; the new player uses a separate artifact directory.

For owner review after relaunch: watch the shared border expand in a forest,
check distant slopes while tiles refine, and use F3's two ready distances to see
which layer is limiting reveal. Turning trees off should remove the tree gate.
