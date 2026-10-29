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
motion and a real-time open-ocean sequence. The fixed-camera packaged motion run passes, with 206889 pixels differing
from a same-frame flat displacement control and 265494 changing over the timed
ocean sequence. Continuous mouth and junction captures were inspected. The
combined camp polish run passes 35 native checks, including real contact burns
and ember-glow expiry. Final release-backend checks and preservation proof remain
required before shortcut selection.

## River scale follow-up (source implemented; final native check pending)

Width, depth and velocity now respond together to discharge. Small catchments
follow horizontal world compression; major rivers smoothly recover physical
dimensions over source discharge 100–10,000 m³/s. Width is bounded by available
reach length and a 4km full-width ceiling, rather than the previous 120m ceiling
at the default scale. Depth varies continuously through pool/riffle sections,
with shared node depths and a 55m ceiling. These are game approximations.

Network admission is separate: raising the tiny-channel threshold from 0.02 to
0.1 scaled m³/s removes about 23% of source proposals in the reproduced world.
It does not multiply stream occurrence to compensate for wider major rivers.
The final local network has 48,373 active reaches in that world; a reach is a
drainage graph segment, not an independently spawned whole river.

Wide junctions use shared circular bed/water footprints, graph-coherent flat
collars, and bank fitting before graph-level reconciliation. Sparse distant
water triangles subdivide to follow the spherical datum and avoid visible
steps against the junction pool. The final core audit passes 10,318 off-grid
repair probes plus 1,510 major-channel probes, including open-edge bank checks.
Three actual major-channel samples are 1.32–1.39km wide, 30.7–32.2m deep and
1.81–1.98m/s. The Earth and procedural regression sets pass 52,420 and 62,441
checks respectively. Those are geometry/sampling results; rendered acceptance
is tracked separately.

Cold wide-bank fitting is shared between the player and terrain worker using
an optional 1.6MB derived cache in `%LOCALAPPDATA%/WorldGen/DerivedTerrain`.
Source fingerprint, horizontal scale, model version, record length and SHA256
must match. A file lease prevents duplicate simultaneous fitting; an invalid
or unavailable cache falls back to derivation. Saved worlds are never modified.
The 21 cache checks verify concurrency, exact graph and water/ground results,
corruption recovery, source/scale separation and cancellation. Measured test
construction was 35.88s cold with concurrent callers and 1.21s warm in the final
key-format run; this is not a frame-rate guarantee. Native startup confirms the
player actually reuses the worker's fitted data. Full camp-site terrain settling
still took 130.22s (104 tiles), with a 15.3s cold tile; startup fitting and tile
meshing are separate costs. Neither this nor the geometry checks is an FPS claim.

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
