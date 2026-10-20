# Water containment: source review and checkpoints

## Library assessment

Owner chose the available open-source reference on20October2026. No commercial
package was purchased/installed and no rendering pipeline was switched.

R.A.M3's public81-page River and Lava3 manual was read, specifically simulation,
terrain and API sections (pages19-21,28,30-31,36-37). It documents runtime river/
lake generation, collider snapping, terrain carving and an API demo. Snapping
water onto arbitrary colliders is NOT evidence that carving modifies arbitrary
streamed mesh height functions. The public API lists TerrainManager.CarveTerrain;
the workflow describes terrain height/splat data. No verified spherical streaming
adapter is supplied by this review. Without a licensed package, source inspection
and an executable compatibility test are not possible. This is a documented
candidate, not a measured integration rejection or claim that integration cannot
be done.

- Product: https://naturemanufacture.com/river-auto-material-3/
- Manual link maintained by vendor: https://naturemanufacture.com/support/
- Crest water-body/level integration: https://docs.crest.waveharmonic.com/Manual/Basics/WaterBodies.html

## Open reference and the integration defect

MIT reference revision b3341c336b2b3070def911fd0c5df196de2bfc64:
https://github.com/Arches-Team/Real-Time-Hyper-Amplification-of-Planets

profiles.comp treats submerged geometry by elevation, uses paired bed/water
attributes and constructs bed/bank profiles. It also raises terrain above the
water potential outside river locations. Our adaptation replaced that bank
enclosure with cut-only profiles AND independent footprint limits; that is not
an equivalent containment guarantee. Reintroducing arbitrary large raised
banks would restore the owner's earlier rejected embankment problem, so the
inland integration remains under review.

A correct lateral water boundary meets terrain at its surface level, joins
another water surface, or has an intentional waterfall. Checking only that
water is above the bed does not establish this.

## Hyper4 ocean-only checkpoint

Previously the final refined face had to retain an Ocean-typed vertex. Ordinary
subdivision terrain could still be2000m below sea level but lose that tag and
therefore lose the ocean. Sea potential now follows the source-connected coarse
sea domain; its actual visible shore is the level-zero/terrain intersection.
Closed inland below-sea controls are not globally reclassified as ocean.

The synthetic island regression fails the previous implementation at -67.1m
on its submerged slope. The correction passes106 focused checks,459 generated/
341 Earth submerged coastal points and unchanged-world hashes. A native generated
shore/shelf review uses actual camera images and completes both492-tile views.
See artifacts/water-library-review/ocean-native for host-local diagnostics.
IL2CPP build16:31:14Z is player-ocean-containment; owner testing is pending.

This checkpoint changes no inland footprints, mountains, loading planner,
controller or historical rules. It does not fix the remaining river/pond
uncontained lateral edges. Those require coupled bank/profile/surface logic,
not another material upgrade or simply hiding the exposed underside.
