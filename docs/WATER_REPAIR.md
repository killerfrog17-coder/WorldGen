# River and ocean repair

This pass follows the camp/bedding/sleep/planting implementation. The existing
shortcut now selects the verified combined IL2CPP player.

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

## River scale follow-up

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

The subsequent loading investigation reproduced 48.50s in four 104.9km fallback
tiles at a requested 1km view range. Four 3.28km fallback tiles took 2.90s, using
the same terrain sampler, while the 104 requested detail tiles took 22.67s total
in a sequential core benchmark. Fallback tile level now follows view distance,
up to the previous far-horizon maximum. 3,834 policy/coverage checks pass. This
specifically helps short view ranges; it does not claim the same reduction at
the maximum view distance. The final packaged Earth camp start settled its 104
requested tiles in 82.16s (worker ready in 4.99s, worst cold tile 1.92s), compared
with the earlier 130.22s/15.3s run. The warmed procedural repro settled in 35.46s
in one later run. These are startup measurements, not general FPS comparisons.

## Final combined checkpoint

The combined IL2CPP camp/water player passes the actual procedural mouth,
confluence and major-river render checks, deep-river floating/current tests and
GPU ocean displacement/motion controls. Its 11-check water pass measured 172,490
pixels differing from the same-frame flat-ocean control and 200,005 changing
across the timed sequence. Streamed views and three off-grid mesh LODs at each
site were inspected. Existing naked-eye blur, drowning and fresh/salt drinking
have a separate 18-check native pass; camp save-library coverage is separate.

Additional 80,000-cell generated worlds (Continental seed29101, Glacial seed29102,
Ocean world seed29103) pass 21,674 / 57,362 / 47,610 river geometry assertions.
Continental scale0.05 and0.25 pass 10,735 / 18,500; Earth scale1 passes55,091.
These retain the exposed-edge clearance guard and source-world identity checks.
They broaden sampled coverage; they do not prove every possible river or seed.

Exact logs, inputs and captures are under `artifacts/camp-water-20261029`.

## Follow-up bank-view audit

The 14-check `resumed-river-views` run adds dry-bank cameras 1.65 m above the
ground, records camera clearances, and raises isolated-LOD cameras above their
actual triangles. Earlier low-angle mouth screenshots were inside the bank and
are not valid evidence of appearance. The new views show continuous water at
the mouth and junction, but expose thin slits along the streamed mouth banks.
That remaining visual issue is under investigation; the test count does not
establish that all seams are fixed. A diagnostic candidate exports the actual
streamed geometry and camera poses to investigate it.

The exported pre-fix scene contains 689 unmatched internal terrain edges across
22 tiles. Stored float coordinates were tested against grid/diagonal lines with
smaller tolerances than their rounding error, skipping shared bank vertices.
Stitching now uses a distance tolerance scaled to float precision. A focused
test reproduced open edges in all nine sampled meshes before the fix and none
afterwards, retaining total projected land area. Submillimetre welding can
already produce almost-collinear slivers; the added orientation guard rejects
folds beyond that coordinate uncertainty rather than claiming perfect exact
arithmetic. The original 10,318 water geometry probes and 192 indexed sampling
comparisons still pass. Fresh native bank rendering remains pending.

The first native precision fix reduced unmatched edges from 689 to 39, but the
remaining ones lay on previous channel-cut lines. The bank slits remained
visible. The candidate now stitches all coincident triangle edges using local
point bins instead of recognizing only grid directions. Fifteen sampled meshes
have no internal open edges, retain projected area and pass the orientation
uncertainty guard; the 10,318 river probes and 1,510 major-river probes still
pass, including the original 8 cm exposed-boundary guard. At the owner's request
the shortcut selects that compiled Mono candidate for immediate testing. Its
final native bank render is pending; do not treat the earlier renders as proof
that this last correction resolved the visible slits.

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
