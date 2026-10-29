# River and ocean repair

This pass follows the camp/bedding/sleep/planting implementation. The existing
shortcut still selects the preceding water release until combined verification.

## Reproduction and causes

The owner's procedural world (seed 10103612, detail 200000, horizontal scale 0.1)
exposed failures not reproduced by the earlier Earth-only water checks. An
isolated snapshot references the original base read-only; the owner's deleted
save was not restored or modified. Reproduction inputs and hashes are recorded
under `artifacts/camp-water-20261029/repro-inputs.json`.

- Ocean ownership includes dry land around the interpolated shore. Returning
  immediately for every ocean-owned point prevented river carving there.
- A final bank-fitting pass lowered the ends of supposedly flat junction
  collars, leaving the ribbon and its shared junction pool at different heights.
- `V.Dot` deliberately quantizes world-simulation math to 1e-9. Squared angular
  lengths of short local channel spans can be smaller than that. Rounding those
  projections created intermittent dry channel sections and inconsistent beds.
  Local rivers now use double-precision dot products; canonical world math is
  unchanged. The exact paired quad owns its full interior, including bends.
- Coarse terrain could bridge channels which the water renderer still drew.
  Refinement now follows the same width admission, retains submerged bed points
  along tile boundaries, and no longer abandons later junction constraints when
  a cell reaches 32 polygons. Finite-segment rejection limits unrelated cuts.
- Sea-level channel cuts use the connected standing ocean surface. Water writes
  depth, so an appended lower river cannot paint over a higher lake surface.
- Flat endpoint sections retain discharge-driven transport instead of an
  unconditional 0.08m/s crawl. Fine advected slope detail is more visible.
- Ocean swell is larger, CPU buoyancy and shader displacement agree, analytic
  wave normals preserve the shape between vertices, and render bounds include
  displacement. Near-shore depth still attenuates the swell.

## Evidence so far

`river-wide-strict` passes 6147 off-grid geometry probes at three actual sites,
including the previously buried mouth, a confluence and a wide receiving-body
case. It checks near/coarse geometry, visible coverage, water height, exact graph
collars, 192 sampling comparisons before/after spatial indexing, and source-world
preservation. Zero dry/hidden/missing/wrong-level cases remain in that set.

Baseline native images reproduce the buried mouth and intermittent channel
triangles. One revised native run failed its initial terrain-loading timeout
before reaching water; it is not a visual pass. The replacement fixture starts
at the actual mouth and captures streamed views, three off-grid mesh LODs, river
motion and a real-time open-ocean sequence. Packaged visual verification and
combined camp regression remain required before selection.

## River scale follow-up

Width, depth and velocity should respond together to discharge, with habitat,
slope and bed roughness producing variation. Major rivers must not all hit a
120m cap at the default world scale. Network admission and drainage spacing must
be considered separately from cross-section size; simply widening every current
stream would make the world excessively wet. No new sizing policy is certified
by the repair checks above.

Research for that follow-up:

- [USGS, hydraulic geometry of stream channels](https://pubs.usgs.gov/publication/pp252):
  measured relations between discharge, width, depth and velocity.
- [USGS, braided/meandering/straight channels](https://pubs.usgs.gov/publication/pp282B):
  relationships among sediment, discharge, slope and channel form.
- [Andreadis et al., global bankfull width/depth database](https://agupubs.onlinelibrary.wiley.com/doi/full/10.1002/wrcr.20440):
  kilometre-scale major channels; empirical estimates carry substantial uncertainty.
- [NPS, Mississippi River facts](https://home.nps.gov/miss/riverfacts.htm):
  major-river discharge and width context, distinct from widened impoundments.

These inform a bounded game approximation, not a hydrodynamic solver or an
assertion that one universal empirical equation reproduces all rivers.
