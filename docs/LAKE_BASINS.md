# Connected lake basins: first review

## Scope

Hyper7 replaces inherited lake-weight cutoffs with connected basin ownership.
This is a bounded coarse-graph/refinement integration, NOT a full adaptive
flood of every8m sample on the planet, full pond generation or solved rivers.
World/history data are unchanged. Derived terrain changes around lake divides;
the LOD scheduler, controller and accepted ocean surface/underlap rules do not.

## Mechanism

-Existing source lake controls seed connected sublevel components on the shared
 root triangle graph. Levels are the existing per-body datums. Higher divides
 stop traversal; saved outlet/downstream controls identify river interfaces.
-Components that escape into the sea or conflict with a lower standing basin
 are rejected, rather than rendered as uncontained elevated sheets. Equal-level
 connected ownership can coalesce. Source histories/maps are not rewritten.
-Dry boundary edges constrain NEW terrain refinement. Shared displaced edges
 retain at least20% of their coarser dry clearance. A corresponding lower
 envelope across adjacent coarse faces avoids a one-vertex-wide levee; all
 relevant neighboring basin levels are considered. This limits negative detail
 and incision at established divides; it is not an after-the-fact flat dam or
 water-height warp. The appearance of this refinement needs owner review.
-Queries retain the same basin identity/datum through dry bank collars. Lake
 meshes no longer stop at an inherited body-tag boundary. They intersect the
 actual displayed land and use the same two-metre hidden underlap/no lift as
 the accepted ocean presentation. Different levels never interpolate together.
-The old random R3 rule that lowered a point and declared it an independent
 lake is removed. A random dip alone does not establish an enclosing basin.

## Cost and measurements

Global derived arrays cost12bytes/root face (about4.58MiB for399996 faces), plus
small per-body levels and bounded per-vertex/edge refinement fields. There is
no new saved fine raster, simulation tick or recurring global cell census.

Actual200k CLR generated/Earth checks:24/27 retained basins,2040/3546 owned faces,
4/9 rejected candidates. Lake graph preparation measured about35-80ms under
varying concurrent build load; full terrain construction about1.4-2.9s. These
are host diagnostics, not a universal loading guarantee or fresh native full-
world generation benchmark.

243 focused checks include a synthetic enclosed basin, fixed bank levels,
vanishing dry-vertex identity, separate lake levels and previous no-warp/ocean
regressions. Six actual lake shores on each world pass near204.8m/32-segment
and distant3276.8m/16-segment mesh reviews:1873 generated/1020 Earth genuinely
exposed target-level edges do not float above the displayed land. River levels
are excluded. The edge validator checks geometric coverage on both sides of
unequal underlap edges; index T-junctions hidden under overlapping water are
not incorrectly classified as exposed surface edges. Generated459/Earth341
submerged ocean points retain sea level with unchanged source hashes.

## Limits

The source bodies used here require usable lake controls (the existing half-
cell coverage admission). This does not add all smaller fractional-cell lakes
or a replacement catalog of validated tiny ponds. Open candidates can disappear
from the3D preview while remaining in the literal coarse source map. That
mismatch should ultimately be reconciled, not described as finished hydrology.

Fine internal subbasins are not independently flood-filled: new isolated low
pockets within a coarse basin can inherit its level. Narrow local connections,
overlapping source basins, extreme scales and coarse LODs need further review.
Outlet gates follow coarse outlet/downstream controls; detailed outlet shape,
flowing transitions, waterfalls and river bed matching are explicitly unfinished.
No hydraulic volume/seasonal-level model, full bathymetry erosion or water art
is added by this pass. Native visual results are recorded in VALIDATION.md.

## Research basis

The connected component/spill topology follows the established Priority-Flood
approach; the root topology is reused and protected through refinement instead
of importing an entire external raster-analysis pipeline.

-https://github.com/r-barnes/Barnes2013-Depressions
-https://fema-ffrd.github.io/overflow/algorithm-details/fill/
-https://esurf.copernicus.org/articles/9/105/2021/esurf-9-105-2021.html
-https://docs.crest.waveharmonic.com/Manual/Basics/WaterBodies.html

These are algorithm/rendering references, not newly installed dependencies or
claims that these tools automatically bound our streamed planetary lakes.
