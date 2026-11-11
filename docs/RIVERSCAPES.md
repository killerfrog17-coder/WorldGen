# Prepared riverscapes

The playable river model derives finite channel beds and water surfaces from
the planetary drainage graph. Shared prepared sections drive terrain, water
queries and explicit render ribbons. Unrelated waterways never acquire a
shared surface by interpolating arbitrary dry terrain samples.

## Reference and actual adaptation

The primary reference is Peytavie et al., [Procedural Riverscapes, PG2019](https://www.cs.purdue.edu/cgvlab/www/resources/papers/Peytavie-Computer_Graphics_Forum-2019-Procedural_Riverscapes.pdf),
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

Admission and physical dimensions are separate. Admission still requires
source discharge times horizontal scale squared to reach 0.1m³/s, which limits
encounter density on compressed worlds. Once admitted, a river uses its original
source-globe discharge. Half-width is bounded to 0.6–2,000m and follows
3 × sqrt(discharge). Depth follows a bounded hydraulic geometry estimate, up to
55m; compressed graph-edge length no longer caps width or depth. These are game
approximations, not site-specific hydraulic predictions.

Prepared stations use deterministic physical-distance noise for width and
pool/riffle depth. Confined resistant reaches vary less. Smooth endpoint tapers
preserve the exact shared section at ordinary graph joins. Flow speed follows
the actual section area and final conditioned gradient; a flat confluence pool
does not imply stopped water. No history tick or per-vertex noise evaluates this
prepared longitudinal profile.

## Storage and runtime

Additional one-time preparation is a source-node datum/width/active table and
compact root-face incidence arrays, not a global fine raster. Local trajectories
are deterministic, generated on demand, and bounded by a128-reach strong cache
plus periodically swept weak entries. Patch spatial bins avoid scanning every
local span for every terrain/scatter query. Nothing is serialized into the
simulation world or visited on a historical tick. The bundled worker still owns
mesh/scatter jobs; the LOD scheduler, reanchor and player controls are unchanged.
Fine profiles are prepared only after a hierarchical distance test, rather than
eagerly for an entire distant tile. Actual junction stages are evaluated lazily
against their incident valley banks. A receiving node must also fit hollows
along its incoming channels; an iterative downstream dependency walk keeps
query order deterministic without modifying the cached broad graph datums.
Ordinary nodes share exact position, tangent, width, depth and stage. True
confluences retain bounded shared-stage collars. Inserted collar stations
interpolate the original section directions instead of rotating nearby banks.
Within each paired quad, a continuous barycentric lateral coordinate drives
the bed cross-section. Euclidean distance still defines footprint support,
but cannot introduce a depth jump at a curved section seam.

Physical wet channel beds take priority over another stream's dry valley
support. Dry bank protection applies only outside every actual wet footprint,
and cannot raise original uncut terrain. Canonical shoreline heights survive
tile-edge collapse. Shared mixed-LOD boundaries use common coarse-grid anchors
and all finite river constraints, with event positions quantized to 1 mm. Both
chunks insert those events before stitching and evaluate the same piecewise
height curve; this closes the formerly visible 5 cm bank gap without skirts.
Unrelated dry terrain keeps the ordinary LOD policy.

Far water tessellation omits features below one tenth of the tile's ground
spacing. Overloaded distant terrain triangles are locally partitioned before
applying bank constraints. Far bathymetry retains the thalweg and actual outer
shoreline, omitting the redundant inner bank ring. Distant added vertices keep
exact height/water queries but interpolate soil and shading from the existing
grid. Close meshes retain full sampling. These are rendering LOD choices;
gameplay geometry and hydrology remain canonical.

Standing-water geometry retains the accepted lake/ocean hierarchy rules.
Rejected local lake basins cannot remain ghost fixed stages in the river graph.
Accepted spill controls retain their lake level; a spill that is also a river
confluence still needs its shared pool. Standing lake vertices themselves do
not receive an extra circular river collar outside their basin. Newly inundated
controls use the actual standing-water level, not their former river potential.
Connected lake heads reconcile downstream before hierarchy construction, and
merged basins retain their accepted original spills. Open below-sea catalog
lakes connect at sea level; genuinely endorheic depressions remain below sea.
Hyper8's hierarchy-level longitudinal/tributary alterations were reverted: the
new local profiles belong to the separate river representation instead.

This is a playable river checkpoint, not finished hydrology or all-scale visual
acceptance. Finite coarse-render river loss, river mouths across basin ownership
boundaries, polygonal banks and junction presentation remain owner-review areas.
See VALIDATION.md for measured evidence rather than inferring FPS from formulas.
