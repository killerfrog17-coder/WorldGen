# Landforms and local water — 20 October 2026

Owner-authorized large-scale relief correction plus the first shared local
river/lake implementation. This is not a finished erosion/coastal simulation.
Streaming, LOD selection, view distance, reanchor handoff and movement controls
are not redesigned. Terrain and water mesh topology necessarily gain water edges.

## World generation and compatibility

New recipes use `worldgen-3.33.0`; `Recipe.PresetAlgorithm` retains3.32.
The preceding versions remain readable and reconstructible. Opening an old
world does not replace its accepted heights, climate, water budgets or history.
Generate a new procedural world to see the changed macro mountain model.
Measured Earth relief is not multiplied or sculpted.

The old default continental uplift included a factor6. New relief removes that
factor and uses retained shortening `(.28 + 2.05*u²)*(.72 + .38*v)` across belts
and along strike. Collision fronts use230km support times existing width
variation, rather than520km; broad hinterland plateaus rise behind the front
and roll off gradually. Subduction ranges use150–310km support times width
variation. Fold provenance is narrower too. Synthetic tectonic ages/coupling
are still priors, not reconstructed plate histories.

At100%, individual collision-front, plateau and subduction mechanisms saturate
smoothly at6500/4500/5800m contributions before spatial envelopes. Their summed
final height is not a hard uniform peak cap. The owner's mountain percentage
scales the resulting mechanism: zero disables these contributions and extreme
percentages still produce deliberately extreme worlds. Old versions keep their
original expression order and sixfold baseline.

## One authoritative local surface

`local-terrain-preview-4` continues bounded cubic interpolation of coarse source
heights and compact C2 blending of regional descriptors. Absolute altitude never
activates mountain relief. Actual neighbour relief, fold/plateau/sediment masks,
fabric and age inform hills versus mountains. High, low-relief Earth interiors
can infer plateau character; no named Earth region is special-cased.

Mountain amplitude is no longer an additional3km everywhere. Shared
planet-coordinate, softly ridged, warped bands survive overlapping source
frames instead of averaging away. Broad masses modulate subordinate crests;
hill-scale detail is separate. Lithology weights supply restrained bedding
ledges; steep surfaces use the existing exposed-rock texture/scatter system.
All noise is continuous in absolute planet coordinates, not chunk/cell seeds.
At0.1horizontal scale, local wavelengths stay in physical metres. A narrow
coastal noise band displaces the formerly locked triangle-height zero contour.

The sampler changes local heights intentionally. Fine locations are not yet
permanent structure sites. Saved worlds retain coarse maps; derived local
terrain is reconstructed, not silently written into those maps.

## Drainage constraints, not blue decals

`local-hydrology-1` reads saved receiver edges, four-season discharge, lake
levels/coverage/storage, wetland evidence and individual island records.
One-time O(cell-count) preparation produces a sparse channel index and regional
shore primitives, never a fine planet raster or recurring simulation census.
Small flows below the global-map river threshold can produce local streams.

Source-first drainage reduction conditions unresolved coarse spillways to
non-increasing channel levels. Shared main-stem tangents and endpoint-flat
meanders keep connected junctions; steep reaches have less meander freedom.
Widths/depths scale sublinearly with flow, bounded to physical sizes. Valleys
are cut into the same height sampler that walking, ground materials and scatter
query. Local alluvial fill also removes incidental noise hollows beneath a
prescribed channel; standing lake/ocean water takes precedence over crossing
channels. Glacial provenance permits wider-bottomed cross sections without
claiming a reconstruction of glacial history. Short gaps from retained lake
footprints to their actual outlets can receive a connector; arbitrary long
canals between unrelated bodies are not generated.

Lakes share saved levels and allocated aggregate area through the existing
shore prototype's irregular basin footprint. Beds and shores replace terrain
continuously. Metre-scale shoreline noise adds smaller coves. Wetlands are NOT
turned into blue cell-centred holes. Saved islands retain separate footprints
and real above-sea relief rather than one island per cell. These remain
approximate star-shaped component footprints, not a complete branching-basin
reconstruction or exact per-cell coverage reintegration.

Patch construction finds spatially relevant primitives, discretizes just those
curves, and bins nearby channel segments/shores. Per-point evaluation never
walks upstream, scans the world's cells or recomputes climate/erosion. All
distances/levels use canonical global segments so independent overlapping
patches agree exactly. Generated mesh/patch data is disposable and unsaved.

## Water rendering

Water clips against the shared bed and shoreline fields, with independent
indices and actual inland levels instead of a sea-level rectangle for every
tile. Full ocean grids share vertices; clipping doesn't multiply their uploads.
On existing near tiles (spacing<=32m), only triangles intersecting channels
split along banks and thalwegs. This preserves streams narrower than the16m
base grid without enlarging them to a cell. Regular grid vertices, skirts,
streaming requests and tile-resolution selection remain intact. Added perimeter
vertices retain the existing linear/collapsed edge geometry.

## Research and limits

Independent implementation; no reference source or assets copied.

- [NASA Himalayan topography](https://science.nasa.gov/earth/earth-observatory/himalayan-topography-39160/): distinguish elevated plateau from the rugged southern front.
- [USGS Appalachian geology](https://www.usgs.gov/geology-and-ecology-of-national-parks/geology-shenandoah-national-park): older eroded ranges and resistant bedrock.
- [NPS Texas geology](https://home.nps.gov/articles/nps-geodiversity-atlas-lyndon-b-johnson-national-historical-park-texas.htm): dissected limestone uplands, not small copies of alpine ranges.
- [Génevaux et al.2013](https://cs.purdue.edu/homes/bbenes/papers/Genevaux13ToG.pdf): terrain/river primitives and blended carving; inspiration, not a reproduction of the paper's complete watershed synthesis.
- [Grenier et al.2024](https://onlinelibrary.wiley.com/doi/10.1111/cgf.14992): directed multiscale patterns constrained by low-resolution relief. An independently implemented compact27-kernel oriented phasor field supplies nested ravines; it does not reproduce their Gaussian-field construction or complete method.
- [USGS hydraulic geometry](https://pubs.usgs.gov/sir/2004/5042/): sublinear width/depth relationships; local coefficients here are illustrative, not calibrated regional regressions.
- [NPS rocky coasts](https://www.nps.gov/articles/rocky-coast-landforms.htm) and [glacial valleys](https://www.nps.gov/articles/ushapedvalleysfjordshangingvalleys.htm): cliff coasts and fjords need structural/glacial valley provenance, not just extra beach noise.

Current coast fields do not flatten everything into a beach and retain slope,
lithology, bathymetry, drainage and glacial inputs for future cliffs/fjords.
Actual fjord carving, wave erosion, sediment transport, deltas, braided channels,
groundwater springs/karst networks, caves/overhangs, transient flooding,
snow/wet ground and a vegetation overhaul remain deferred. Static mean-flow
channels are not a dynamic fluid simulation. Earth drainage and lithology remain
simulated/synthetic, and the bundled coarse elevation cannot reconstruct real
individual peaks or local river paths. Texture/prop art remains placeholder.

## Checks

`--landforms-only` tests mechanism limits, physical percentage scaling, connected
downhill channels, exact patch agreement, fractional lake beds/shore crossings,
bounded geometry and a2m stream through the16m base mesh. `--landforms-review
<snapshot.world> <folder>` audits actual200k worlds, preserves input hashes and
records setup/mesh timings and regional relief. Existing ground/scatter/terrain
suites remain required. Native `--worldgen-landform-review` captures Earth
Himalaya/Tibet/Andes/Appalachians/Texas/coast plus river/lake views, or generated
collision/cordillera/old-range/plain/water sites. Screenshots must be inspected;
passing scalar checks alone is not visual acceptance. See VALIDATION.md for the
final build and evidence, not an intermediate development capture.
