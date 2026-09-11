# Architecture and algorithm contracts

`Core` targets .NET Standard 2.1 and has no engine, renderer, browser, filesystem or operating-system dependencies. `Serialization` owns JSON/CSV/PGM. `Presentation` converts completed state into vertex colors/meshes without changing the state. CLI, the standalone .NET Web Worker, and the Unity runtime all construct `WorldPipeline`.

## Surface and plate hierarchy

The canonical mesh is a recursively subdivided icosahedron, normalized onto a sphere and jittered in seeded local tangent directions. Supported counts are 642, 2,562, 10,242 and 40,962 cells. A requested count is a minimum and is raised to the next supported count. The resolution requirement also includes `16 × microplate count`. The 500-microplate preset therefore uses 10,242 cells. Every actual microplate must retain at least four cells or generation fails; the sixteen-cell target is an **average density budget**, not a per-plate guarantee.

Cells are the spherical barycentric dual of the triangulation. Cell areas sum six spherical wedges per incident triangle. Shared boundary lengths use arcs between normalized triangle centers. Intercell distances use great-circle center distances; lengths and areas are separate fields. Radius changes real metrics. This is a jittered geodesic triangulation, not an exact spherical Voronoi construction or a global Delaunay flip solver.

Separated microplate seeds grow over metric cell adjacency with seeded positive edge costs. Connected growth preserves all seed IDs and provenance. Parents grow on the weighted microplate adjacency graph. A parent has a separated seed, target share, great-circle growth direction, curvature and last-direction state. Candidate scoring combines target deficit, transported directional alignment/continuity, shared-boundary compactness and seeded variation. Expansion is always adjacent to the parent, so connectivity is preserved by construction.

Target shares interpolate between equal weights and a strongly skewed seeded quartic distribution. Topological bottlenecks and indivisible microplates can prevent exact target areas; both requested and achieved areas are reported.

Contested eligibility records multi-parent frontier exposure and a deterministic final-interface look-ahead. Preserved candidates must still touch another parent domain. Scores combine contest degree, interface length, local motion contrast and a seeded term; stable IDs break ties. Quotas are a fraction of **all** microplates. Shortfalls produce a warning. Ordinary members retain their microplate IDs but use a shared parent active-actor ID and parent motion; preserved members keep parent affinity and independent blended rotation.

## Continental structure, motion and terrain

Continent identities grow on the microplate graph toward area targets. Connectedness influences seed proximity and contact/separation penalties. Identity groups are distinct from final connected landmasses. Shelves, near straits and short isthmuses operate near contacts between distinct identities. Land coverage is a crust-area target; relief, volcanism, shelves and erosion may change final dry land area. Achieved values are explicit.

Boundary motion is evaluated at every shared cell edge using `velocity = rotation × surfacePosition`. Its signed normal and tangential components classify convergence, divergence or transform. Continental density is lower than oceanic density; the denser side subducts at ocean-involved convergence. Parent stress and active microplate stress have an explicit normalized blend.

Ocean escape uses a graph distance field seeded only by the **largest connected oceanic-crust component**, not any negative-height pocket. Relative to the configured scale: less than half is an escape face; more than the full scale is a dead end; the interval blends continuously. This is a spatial accessibility heuristic, not a solved crustal extrusion model.

Terrain is built in metres from coast/shelf/slope/abyss profiles and a stress-based tectonic scaffold. Four retained permille weights (fold, craton, basin, plateau) sum to 1,000. Semantic compact kernels supply collision belts, plateau/escape blocks, density-sided forelands, rifts, ridges, trenches, arcs, ancient ranges, hotspots, chains and large igneous provinces. Kernels of one type blend by influence-weighted average, preventing relief from growing merely because more edge samples exist. Directional oscillations perpendicular to boundary axes, along-strike modulation and spatial warp provide parallel ranges, summits and passes. Every sampled kernel keeps its actual affected-cell membership.

Ghost seams retain former motion/type, age, pair and strength. Convergent former seams can produce broad worn uplift; divergent seams produce negative corridors; transform seams produce weak structure. Seeded frequency gates are applied per original microplate pair. Ghost distance profiles are authoritative terrain influences, not a display blur.

Hotspots use local plate motion to form age-decaying trailing edifices. Ocean floor features retain physical bathymetry. The lower-sea preview changes only rendering/connectivity, not tectonics or persistent hydrology; metrics report landmass counts at −120, −250 and −500 m. Reaches and lakes continue to describe the recipe's zero-sea-level world.

## Erosion and protected basins

Before terrain warp, connected negative blocks are measured. Marine remnants outside the main water component qualify when they contain oceanic crust, touch stressed continental enclosure, and meet recipe area/depth limits. Their negative terrain survives warp and all erosion passes. No topology-filling terrain mutation follows detection.

Five synchronous erosion passes use deterministic downhill receiver/area proxies. Hydraulic incision is bounded by local relief and transfers a fraction of removed material downstream. Thermal transfer uses talus excess and area-scaled deposition, moderated by ridge preservation. Glacial carving uses latitude/elevation susceptibility and topographic accumulation; it may overdeepen valleys and coastal cells. These are moderate morphological operators, not mass-conserving geophysical time integration. Glacial origin records survive the final water pass. Hydraulic and glacial removal do not claim a closed sediment budget.

Tectonic, warped pre-erosion, and final elevations are all retained. Zero hydraulic/thermal/glacial strengths leave the pre-erosion terrain unchanged. There are no subsequent elevation mutations after hydrology.

## Persistent hydrology

An ascending, stable elevation sweep constructs a union tree of local minima and merge saddles. Depression records retain children, parent, pit, spill and outlet. This hierarchy includes small depressions even when they are below the display/persistence lake threshold.

The main connected negative-water component seeds an integer priority flood. Protected tectonic seas are excluded from ocean seeds. Selected sufficiently deep/large endorheic leaf basins instead seed terminal common-level reservoirs at two-thirds of their pit-to-spill depth. This explicit, climate-neutral storage policy is a topographic scenario, not an evaporation/salinity prediction.

Wet connected cells with equal conditioned fill levels become lakes. One outlet is chosen by the original receiver topological order and stable IDs, then a breadth-first lake-internal routing tree connects all water cells to it. Choosing the topologically earliest outlet prevents cycles across equal-level saddles. All receivers, watersheds, orders and contributing areas are rebuilt after this consolidation. The final bed height is never filled; conditioning has a separate `FloodLevelM` field.

Actual square-metre cell areas accumulate down the acyclic receiver graph. Each terminal sink owns a watershed ID; coastal ocean cells are distinct drainage terminals. River records are stable one-edge reaches with endpoints, watershed, Strahler order and contributing area. Adjacent edge reaches can be aggregated by later consumers. Potential flow uses **uniform 1 mm/day runoff**, in m³/s; this is explicitly climate-neutral and not seasonal discharge.

Water-body records contain common surface, spill, single outlet/downstream cell, cells, area, volume and origin. Complete member bed elevations and areas permit arbitrary stage/area/volume curves to be derived. Freshwater lake, glacial lake, endorheic lake, tectonic inland sea and ocean remain distinct. Protected seas are terminal; ordinary lakes route through their recorded outlet. Tiny unpersisted pits retain conditioned receivers but are not promoted to named lakes.

## Scope limits

Macro cells cannot resolve narrow real-world channels or every 50 km mountain ridge at preview resolution. Detailed local terrain, exact shoreline intersection, continuous plate evolution, mantle convection and climate-driven erosion remain outside this milestone. Continental grouping and collision contexts are controllable heuristics. Browser builds use interpreted .NET WebAssembly, making high-count generation substantially slower than the native CLI; cancellation remains responsive because computation lives in a dedicated worker. JSON state transfer and integrity validation also have a measurable UI completion cost.

`ISeasonalRunoffProvider` is the clean boundary for future four-season runoff. Nothing currently populates seasons, biomes, ecology, populations, settlements, culture, religion, history or playable local terrain.
