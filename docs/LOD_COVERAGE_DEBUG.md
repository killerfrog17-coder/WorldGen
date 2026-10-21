# Resident LOD coverage and explorer navigation

## Scope

Owner-authorized step1 only: stop obsolete coarse terrain obscuring already
resident finer terrain. Do not change terrain heights, river/lake generation,
LOD planning, worker concurrency, loading priorities, reanchor preparation,
distance/quality controls or procedural detail as part of this checkpoint.

The previous renderer installed fine meshes immediately but retained the ENTIRE
coarse fallback until all overlapping wanted replacements were ready. This
allowed both surfaces to draw through the same geographical area. Retention
itself was useful; whole-tile visibility was the error.

## Partial fallback representation

Each resident tile receives a sparse quadtree of regions actually covered by
finer resident tiles. Missing/requested-but-unloaded tiles do not create holes.
Both ground and water shaders discard only those covered regions. The masks
use tile-local coordinates, independent of floating render origins, and are
refreshed when the resident set changes or a prepared chart is adopted.

Four float entries per node encode visible/covered/child regions in a point-
addressed texture, rather than allocating a fine-resolution raster across a
large coarse tile. This handles multiple refinement depths and negative tile
indices. Removing fine coverage restores its fallback. Original whole-tile
retirement still runs once all replacements exist; the masks change visibility,
not the lifetime/job scheduler. Coverage updates are not a new FPS guarantee;
broader caching and planning improvements remain future work.

## Debug navigation

- F3 reports geographic compass direction and bearing: north0, east90, south180,
  west270 degrees. It projects the camera's chart heading into the local
  geographic tangent frame instead of displaying raw chart yaw.
- `/tp <latitude> <longitude> [height above ground/water]` uses degrees in the
  SAME official meridian as F3. The optional height is1.65–1000000m; higher
  destinations enable flight. `/teleport` is an alias. Invalid/nonfinite inputs
  do not move the player. Current flight clearance is retained by default.
- `/tp random` samples a spherical-area-uniform world position, placing the
  observer above ground or water, not at the bottom of an ocean.
- `/tp random land` uses at most512 attempts, rejects source ocean cells and
  checks the actual local destination is dry. This is a bounded convenience
  sampler, not exact area-uniform sampling of all fine land or marine islets.
  It reports failure without moving if no destination is found. Its named RNG
  is presentation-only; no authoritative generation/history stream changes.

## Optional waterways map

The Rivers checkbox beside Clouds starts a separate cancellable background
overlay, off by default. It does not rebuild the cached base map when toggled.
Rivers are cyan line symbols; lake areas are estimated from coarse coverage.
The minimum channel width decreases with map zoom; symbol widths are deliberately
exaggerated for navigation. Zoomed-in channel curves use the amplification's
existing route; broad maps use cheaper source curve estimates. This is NOT a
new hydrology solver or a promise of exact local shorelines/seasonal streams.
The overlay is drawn above Clouds for legibility, labelled as estimates, and
its texture/task resources are cancelled/disposed with the explorer.

## Checks

13063 focused checks cover partial/missing/deep/negative-index coverage,
geographic bearings, coordinate validation/meridian mapping, random sphere
positions, zoom thresholds, actual-world waterway output/cancellation and
unchanged source hashes.892 existing sky/calendar/chat checks pass; their help
expectation intentionally adds the requested teleport family. Native shader
and explorer command checks are recorded in VALIDATION.md after completion.

Existing triangular river artifacts, material seams, distant quality, terrain
facet detail and vegetation LOD are explicitly unchanged.
