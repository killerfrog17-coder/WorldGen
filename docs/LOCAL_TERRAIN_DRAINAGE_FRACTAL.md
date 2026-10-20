# Local terrain5: gradient-warped relief and integrated river sections

## Research decision

The constraints are fixed coarse planet data, deterministic arbitrary point
queries, no fine planet raster and no erosion simulation on every loaded chunk.
There is no single noise function that guarantees globally valid hydrology.

Primary references assessed:

- [Giliam de Carpentier, procedural extensions](https://www.decarpentier.nl/scape-procedural-extensions):
  analytic noise gradients, multiscale feedback and variable detail amplitude.
  Useful for inexpensive local relief; this alone does not solve river routing.
- [Génevaux et al., terrain generation based on hydrology](https://perso.liris.cnrs.fr/egalin/Articles/2013-river-networks.pdf):
  continuous terrain/river primitives and cross-sections constrained by a
  drainage network. Our existing saved receivers/discharge supply that network.
- [Gaillard et al., Dendry](https://github.com/mgaillard/Noise): locally evaluated
  branching trees are attractive for future sub-grid drainage. The full method
  is not adopted here: its reference CPU cost is materially above the current
  near-tile budget, and reproducing its hierarchy is a separate implementation.
- [Grenier et al., controlled procedural patterns](https://diglib.eg.org/items/43319e3d-ba25-4762-971b-ac838ec21fb8):
  an erosion-detail method, not a substitute for choosing a mountain mass or
  connecting a true river network. The preceding phasor-style approximation is
  no longer used by the local height sampler.

No external source code/assets were copied. This is an independent hybrid,
not an implementation or reproduction claim for any complete paper.

## Replacement, not more ridge weights

`local-terrain-preview-5` removes source-local sinusoidal ridge trains, the
dominant single ridged-noise mass and the old oriented phasor-ravine additions.
`TerrainFractal` instead evaluates a signed, irregular broad mass and five
subordinate octaves. Analytic simplex gradients steer finer octaves; inherited
amplitude and regional age/sharpness distinguish smoother worn terrain from
rugged young relief. A signed primary mass avoids making every noise zero
contour into an equally high ridge. No finite-difference neighbours are needed.

Coordinates are absolute planet coordinates in physical metres, independent
of tile identity, chart origin, LOD, visit order and threads. The existing
compact regional blending still controls relief amplitude, hills, plateau,
sediment and geology; cubic source-height interpolation, underwater terrain and
coastal detail remain. Different coarse profiles do not become discrete borders.
The noise has a fixed six-octave budget (plus two broad warp samples); actual
profile and river lookups use the existing local spatial indices. This is not
a claim that every possible spatial bucket has a formally fixed population.

## Rivers are part of the same ground field

`local-hydrology-2` retains the saved receiver network, flow-dependent widths,
connected curves and non-increasing water levels. Its cross-section now builds
the submerged bed, dry bank, valley sides and upland transition together. It
fully replaces unrelated noise beside the channel instead of only partly
filling low banks. River intersections do not let a neighbouring valley cut
away the bed selected for a wet channel. The outer support returns exactly to
the unmodified terrain without a raised edge. Lake/island primitives remain.

Water clipping now measures clearance against the actual rendered terrain
vertex, including collapsed/refined LOD edges, rather than assuming its ideal
sample and rendered height always agree. Channel-aware mesh splits still retain
narrow streams without widening them to a whole terrain grid square.

## Scope and compatibility

No coarse elevation, climate, species placement, historical rules or world-save
hash changes. Local terrain is intentionally re-derived; fine locations are
still not a pinned permanent construction-site contract. Loading priorities,
small near jobs, chart handoff, controls, shaders and audio are unchanged here.
The preceding near-first player remains rollback.

This remains procedural terrain, not a reconstruction of local Earth mountains
or a full erosion/deposition/fluid model. Sub-grid tributary watershed synthesis,
braided rivers, river-surface animation, specialist coasts and final terrain art
are not completed. Native captures are a short visual smoke review, not proof
of universal landscape realism. See VALIDATION.md for actual checks/timings.
