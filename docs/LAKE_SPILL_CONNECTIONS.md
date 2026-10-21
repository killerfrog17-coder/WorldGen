# Lake–river spill connections / Hyper10

## Reproduced owner case

The owner's saved 200k-cell world, seed11610541 at horizontal scale0.1,
has lake277 at328.067836m, outlet94291 and downstream94901. Initial terrain
incision lowered the spill control to292.92m while the accepted lake retained
its fixed datum. At the saved player point the ground was294.205m, leaving
the standing-water sheet roughly34m above it. This was not a shading defect.

## Correction

After accepted lake ownership is established, but before local amplification,
preserve each usable non-endorheic spill's initial lip at the lake datum,
bounded above by its unincised source terrain. Correct upstream river controls
that the old incision pass lowered beneath that shared spill datum. Keep the
accepted basin catalog; do not regenerate or discard lakes to hide the defect.

The paired outgoing channel starts at this same exact lake level. Local terrain
then cuts a finite, scale-correct submerged opening through the spill lip using
the river's physical width/depth. The lake remains flat; water never bends to
meet dry terrain. Relative to the broken preview, this restores excessively
excavated terrain, rather than building a causeway above original source land.
The authoritative world, history, saved pose and lake levels are unchanged.

Rejected drafts: retaining the legacy unscaled depth under the corrected datum
still left a broad hole beneath the narrower scaled river. Rebuilding basin
ownership from newly inundated controls could reject the original lake. Neither
draft is the published correction.

## Focused checks

- Owner snapshot:14 corrected spills; total derived terrain preparation1.329s
  in the CLI diagnostic. Saved-point ground328.904m, exact outlet bed326.35m /
  water328.067836m, with6 wet source lake controls retained. Near204.8m and819.2m
  mesh standing surfaces are0.84m below saved-point ground, not33m above it.
  The3276.8m/16-segment mesh does not resolve the narrow standing mouth there.
- Exact outgoing endpoint datum, submerged mouth, lake retention, no overhead
  sheet and unchanged source hash pass on the saved snapshot.
- Existing generated/Earth fixtures:6 lake shores each,1799/1320 contained
  interior shoreline edges;460/343 submerged ocean points; unchanged sources.
- Final packaged CoreCLR worker matches127639 mesh/water/index/bounds and
  cancellation comparisons on the owner's snapshot. Its selected scatter
  checks contain zero instances; this is not new scatter-placement acceptance.

Extra work is bounded by coarse cells/body counts, with one integer owner per
coarse control and no saved fine raster, hydrology tick or new global flood.
These timings are host diagnostics, not full-world generation guarantees.

## Limits

This addresses the reproduced over-excavated spill, not every possible basin
interface or seed. Coarse source ownership and unresolved distant mouths remain
limitations. Triangular bed artifacts, confluence shading seams, LOD/controller,
water art, waterfalls, flooding and seasonal rivers are unchanged. Native review
and the selected build are recorded in VALIDATION.md; owner review remains the
acceptance step.
