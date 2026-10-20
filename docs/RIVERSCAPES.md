# Bounded paired riverscapes — Hyper9 checkpoint

Hyper8's river geometry was rejected by the owner: its unlimited dry-corner
river potentials and cross-reach interpolation produced extensive water sheets
and vertical blue walls. Passing old edge-continuity checks did not establish
realism. That rendering path is not used by Hyper9 completed worlds.

## Reference and actual adaptation

The primary reference is Peytavie et al., [Procedural Riverscapes, PG2019](https://perso.liris.cnrs.fr/egalin/Articles/2019-riverscapes.pdf),
particularly its coordinated trajectory/profile, terrain primitives, compact
support and separate water primitives (Sections4–7). The author preprint was
read directly, not inferred from its video.

This is our CPU paired-profile adaptation, NOT a complete reproduction of the
authors' authoring tool, Rosgen classification catalogue or GPU blend-flow tree.
The existing planetary drainage graph supplies the topology and initial route;
we do not re-run their DEM drainage extraction. Specifically:

- Actual source graph endpoints have shared water datums. One source-first and
  reverse pass reconciles monotonicity and fixed standing-water endpoints.
- Canonical lazy trajectories resample the drainage-aware amplified graph.
  Centre and both prospective banks constrain a downhill longitudinal profile.
- Paired unit sections define bed depth, bank width and water together. Bend
  asymmetry changes the bed, not the cross-stream water level. Compact C2 collars
  retire terrain cuts. Terrain is never raised into a river causeway.
- Bed queries and near terrain constraints use the actual paired water sections
  and their miter planes. Internal stations do not have overlapping round caps.
  A final refined-bank/centre/midpoint pass lowers profiles after replacement,
  preventing shelves from being suspended above smaller bank hollows.
- Calm, turbulent and cascade regimes use gradient and visual discharge.
  Steep sections receive a C2 shelf/drop replacement template; bed and water
  share its refined stations. Junctions have a common flat endpoint collar.
- Water is tessellated as explicit finite cross-section ribbons and graph-node
  junction pools. Convex subtraction avoids ribbon/pool coplanar overlap.
  Unrelated rivers are never joined by a terrain-triangle interpolation plane.
- Intersection with the actual drawn ground hides covered portions without
  moving any water vertex to the ground. Standing-water ownership also excludes
  independent river overlays through a lake's dry bank collar.
- River render footprints include a bounded2m under-land overlap, analogous to
  accepted standing-water shores, still clipped against drawn terrain. It does not
  change the datum or project water across the surrounding landscape.
- The previous moving-ripple/Fresnel/glint shader is retained. It is approximate
  opaque water, not the paper's full animated-height/velocity blend-flow solver.

The paper itself explicitly excludes true3D waterfalls, breaking waves and
splashes (Section7.4). Its cascades are heightfields. Detached waterfall sheets,
spray and plunge-pool art remain separate extensions here too. Also deferred:
braiding, a complete Rosgen A–G taxonomy, sediment transport, hydraulic backwater,
adaptive fine catchment generation and exhaustive river/lake mouth resolution.

## Scale and amount of water

Visual discharge is source discharge times horizontal scale squared. Nominal
channel half-width follows the existing discharge rule times horizontal scale,
with a0.6m minimum. Channels below0.02m³/s visual discharge are not drawn.
The old extra subdivision tributaries may still shape small dry gullies, but
do not independently become full-sized watercourses. The0.1-scale world is no
longer supplied with the old1× channel widths or an infinite wet potential.

## Storage and runtime

Additional one-time preparation is a source-node datum/width/active table and
compact root-face incidence arrays, not a global fine raster. Local trajectories
are deterministic, generated on demand, and bounded by a128-reach strong cache
plus periodically swept weak entries. Patch spatial bins avoid scanning every
local span for every terrain/scatter query. Nothing is serialized into the
simulation world or visited on a historical tick. The bundled worker still owns
mesh/scatter jobs; the LOD scheduler, reanchor and player controls are unchanged.
Fine profiles are prepared only after a hierarchical distance test, rather than
eagerly for an entire distant tile. Far water tessellation omits features below
one tenth of that tile's ground spacing; ground sampling remains canonical.

Standing-water geometry retains the accepted lake7/ocean hierarchy rules.
Hyper8's hierarchy-level longitudinal/tributary alterations were reverted: the
new local profiles belong to the separate river representation instead.

This is a playable river checkpoint, not finished hydrology or all-scale visual
acceptance. Finite coarse-render river loss, river mouths across basin ownership
boundaries, polygonal banks and junction presentation remain owner-review areas.
See VALIDATION.md for measured evidence rather than inferring FPS from formulas.
