> Current scope is schema 3 / worldgen-3.0.0. See docs/OROGEN_ALIGNMENT.md and docs/ALGORITHMS.md: fixed coarse plates, detail-dependent terrain, persistent hydrology deferred. Earlier fixed-all-20k or hydrology requirements below are historical. The owner declined GPL translation.

> Historical version-1 brief/audit. The 17 September 2026 owner instructions and [OROGEN_ALIGNMENT.md](OROGEN_ALIGNMENT.md) define the current independent version-2 implementation and supersede conflicting scope/algorithm decisions below. No GPL source translation is authorized.

# Map2 and World Orogen: comparative world-generation analysis

Prepared 10 September 2026. Scope: analysis and specification preparation; no successor implementation or changes to either source project.

## 1. Principal findings

**The strongest combination is Orogen's spherical tectonic and terrain construction, Map2's explicit hydrology and ecological data, and a new deterministic execution architecture.** Neither project is already the full foundation you described. Both synthesize plausible geography through rules and noise; neither evolves a planet through geological time, simulates civilizations, or generates streamed, explorable local terrain.

Map2 is a broader environmental world model. It carries generation beyond elevation and climate into drainage, lakes, wetlands, biome distinctions, ecoregions, producers, and animal assemblages. These are useful inputs to settlement and historical simulation. Its spherical support is uneven: topology is genuinely spherical, but several later geometric operations still use projected coordinates or approximations. Terrain construction is largely the composition of named geological features, rather than erosion of a tectonic surface.

World Orogen is a more developed spherical terrain authoring system. Local spherical plate motion, coarse reference plates, continent grouping, super-plates, differentiated tectonic terrain, directional ridge synthesis, ocean-floor structure, and several erosion operators give it a richer terrain-shaping vocabulary. Its browser worker, diagnostic layers, import/export, and calibration tools are also valuable precedents. Its climate is substantially engineered and empirically tuned, rather than a general physical climate solver. Its erosion drainage is not a persistent river-and-lake world model.

Three findings especially affect your future architecture:

1. **Fresh generation and incremental regeneration must call one authoritative pipeline.** Small headless checks reproduced fresh results in both projects, but Orogen's same-parameter reapply and unchanged edit-recompute produced different elevation arrays. The corresponding paths omit or change inputs and physics stages.
2. **A shared seed is insufficient.** The complete recipe, algorithm version, dependency behavior, random streams, ordering, and numerical rules must also match. Sharing C# source between Unity and WebAssembly does not, by itself, establish bitwise equality.
3. **The macro world must remain authoritative as detail increases.** Orogen stabilizes coarse plate construction across output resolutions, but still performs consequential terrain and climate work on the requested mesh. Map2's visual LOD retains simulation cells, but does not provide a regional-to-local terrain hierarchy. Your successor needs a stronger separation between canonical macro simulation and deterministic local realization.

### Capability comparison

| Area | Map2 | World Orogen | Provisional direction for your project |
|---|---|---|---|
| World topology | Plane with configurable wraps; sphere supported with mixed downstream geometry | Sphere throughout primary generation | Explicit topology/metric abstraction; sphere first |
| Plate layout | Random weighted frontier growth | Coarse reference mesh, controlled growth, projection and cleanup | Orogen's canonical coarse-plate idea |
| Continents | Independent land/ocean classification per plate | Whole-plate continent groups and approximate land budget | Orogen's grouping, with separate crust and water state |
| Plate motion | Constant projected direction/speed per plate | Euler-pole rotation, local boundary motion, density, super-plates | Orogen's local spherical formulation |
| Landforms | Named additive/multiplicative feature fields | Stress/distance skeleton, provinces, oriented ridges, arcs, mantle and noise | Hybrid semantic geology plus continuous field construction |
| Erosion | No general elevation-eroding pass | Hydraulic, thermal, glacial, drainage conditioning, creep | Orogen-inspired operators with physical units and explicit basin policy |
| Rivers and lakes | Persistent downstream/runoff/lake/wetland fields | Internal erosion flow; no comparable persistent hydrology product | New hydrology model, informed by Map2's outputs |
| Climate | Seasonal belts, winds, current paths, temperature, moisture | Spherical winds/currents, advection plus tuned precipitation, continentality | Compare mechanisms separately; standardize units and time first |
| Ecology | Detailed biomes/ecoregions and static trophic assemblages | Köppen classes and visual biome coloring | Map2's separation, with resource suitability outputs |
| Civilization/history | Absent | Absent | New system consuming geography and ecology |
| Browser workflow | Desktop Python/WGPU | JavaScript worker and Three.js | Shared C# core with separate Unity and browser hosts |
| Explorable local terrain | Absent; polygon-border display LOD | Absent; globe/map mesh and raster exports | New constrained regional/chunk generation |

## 2. Evidence, scope, and how to read this report

### Source snapshots

**Map2:** supplied archive `Map2(20260910-065519).zip`, extracted under its `Map2/` directory. Archive SHA-256:

`d420043b748c6f86567beb03c8159c2562738de97dffab76ed452c14dcd2cc80`

**World Orogen:** repository `raguilar011095/planet_heightmap_generation`, inspected at commit:

`cc2662b4edd52231c4f65d8765f3ef12cd82d9b7`

[Pinned repository snapshot](https://github.com/raguilar011095/planet_heightmap_generation/tree/cc2662b4edd52231c4f65d8765f3ef12cd82d9b7).

The review traced the executed generation pipelines and their geometry, plate, elevation, climate, hydrology, ecology, worker, rendering/export, configuration, and tuning dependencies. UI and rendering code were inspected for their relationship to generation, data ownership, LOD, editing, and persistence; this is not a shader correctness audit. Stored tuning experiments were distinguished from the currently imported configuration. Comments and README descriptions were checked against implementation where relevant.

This report uses three evidence levels:

- **Implemented behavior:** established from the source path and called functions.
- **Reproduced finding:** demonstrated by the small headless checks in section 7.
- **Design judgment or risk:** an inference or proposed successor choice, explicitly presented as such.

References such as **[M4]** and **[O8]** point to the source index in section 10. Map2 references use paths inside the supplied archive. Orogen references link to the pinned commit. Function names and selected line numbers make claims traceable without relying on current repository HEAD.

No visual-quality ranking, cross-platform determinism guarantee, full-resolution performance comparison, or scientific climate-validation score is claimed. The runtime checks intentionally exercised small worlds and omitted renderers. Those limits matter: a plausible mechanism is not proof of a better-looking map, and successful repetition in one runtime is not proof of matching Unity and browser output.

## 3. What actually runs, in what order

### Map2

`main.py` performs the following sequence:

1. Read global configuration and initialize/use the configured random generator.
2. Generate cell seeds and optionally relax them.
3. Build shared Voronoi geometry, cell centers, the initial render mesh, adjacency, and neighbor geometry.
4. Grow plates; smooth selected land/ocean boundaries.
5. Assign initial plate-derived land/ocean flags and elevations.
6. Compute solar-heat fields.
7. Apply coast-distance baseline topography.
8. Compile and apply synthetic geological features; recompute land from final elevation.
9. Build seasonal climate belts, winds, and ocean currents.
10. Compute temperature, then precipitation.
11. Compute drainage, runoff, lakes, and wetlands.
12. Populate cells; classify climate, biomes, and ecoregions/ecology.
13. Display the world and build additional polygon display LODs asynchronously.

Some object creation and the LOD worker setup are interleaved with classification. The console's generation-complete message appears before all ecological classification has finished, so its elapsed time is not a complete end-to-end benchmark. Most importantly, rivers do not feed an elevation-erosion pass back into the terrain, and the new lakes do not trigger a fresh climate solution. **[M1, M7–M10]**

### World Orogen

The main worker generation path performs:

1. Build the requested spherical mesh.
2. Generate a fixed-resolution coarse sphere, plates, and land/ocean assignments.
3. Project coarse plate labels to the requested mesh; smooth labels and apply edits.
4. Assign plate densities and modify plate motion with the plate-physics rules.
5. Build super-plates where enabled and apply their physics.
6. Transfer mantle-related information into the elevation calculation.
7. Construct tectonic elevation through the layered elevation pipeline.
8. Warp, smooth, add detail, erode, sharpen, and creep the terrain.
9. Compute wind, ocean circulation proxies, precipitation, temperature, and Köppen classes.
10. Build triangle/render data, retain regeneration state, calculate terrain metrics, and return output.

Climate may be deferred for large meshes and requested later. Reapply, edit-recompute, import, and main-thread fallback have separate orchestration code; they are not all equivalent to fresh generation. This is an important distinction between the intended dependency graph and the implemented application paths. **[O1, O2]**

Within step 7, `assignElevation` itself executes these thirteen sub-stages in this order. This is distinct from the later postprocessing/erosion stack:

| Internal stage | Transformation |
|---|---|
| 1. Tectonic state | Local collisions, propagated stress, small/super-plate contributions, mantle inputs |
| 2. Spatial fields | Boundary/interior influence distances and related spatial masks |
| 3. Terrain classification | Continuous terrain/province weights and noise amplitude controls |
| 4. Skeleton | Broad plate relief, mountain/coastal/ocean structure, interior forms |
| 5. Phasor ridges | Directional internal mountain structure |
| 6. Discrete edifices | Island arcs, volcanic arcs, hotspots and large igneous provinces |
| 7. Tectonic-band noise | Texture associated with tectonic spatial bands |
| 8. Detail texture | Elevation- and tectonic-condition-dependent detail |
| 9. Coastal detail | Coastal relief variations |
| 10. Background land noise | Additional broad land texture |
| 11. Dynamic topography | Mantle-related vertical deflection |
| 12. Final shaping | Peak compression and empirical height-distribution adjustments |
| 13. Topology fixup | Raise selected stranded sub-sea-level interiors based on ocean connectivity |

The thirteenth stage is a land/water topology correction, not a general river/lake solution. Its meaning differs from the priority-flood operation later used during erosion. **[O8, O9]**

### Shared conceptual pipeline, different dependencies

Both can be described as geometry → plates → relief → climate → environmental interpretation. That similarity hides important differences:

- Map2 uses temperature before precipitation; Orogen uses precipitation to affect temperature, including lapse rates and cloud-related moderation.
- Orogen's erosion precedes its final precipitation calculation and uses a terrain-derived flow proxy. Map2's hydrology follows precipitation, but does not erode terrain.
- Map2 continues into ecology; Orogen largely stops at climate classification and presentation.
- Neither implements a converged climate–erosion–hydrology feedback system. It would be misleading to describe either as doing so merely because all three concepts appear in the code.

## 4. Phase-by-phase comparison

### Phase 1 — Recipe, seed, units, and reproducibility

**Purpose:** define exactly which world is requested and what its numbers mean.

**Map2.** Configuration is collected into a dataclass, with a NumPy generator stored in the global configuration. A configured seed of zero or less selects fresh entropy. The attached `config.toml` uses `seed = 0`, so its out-of-the-box behavior intentionally produces a new seed rather than a replayable fixed world. An explicit positive seed supports repetition, provided the generator is reset. Reusing the same already-consumed `CFG.rng` is not a fresh seeded generation. Different subsystems use direct seed offsets or fixed seeds. Lengths and environmental parameters mix miles, meters, feet, Fahrenheit inputs, and Celsius outputs. **[M1, M2]**

**Orogen.** A local JavaScript PRNG and seeded simplex noise support deterministic construction. Several domains use separate seed offsets; coarse plate generation explicitly isolates its random stream. A compact planet code encodes a seed and quantized settings, plus plate toggles. The current encoder supports a 24-bit seed and a 22-character base code, while documentation still refers to an older length. Legacy decoding preserves older parameter formats, but does not freeze the old terrain algorithm or all tuned constants. Elevation is generally stored as a normalized artistic variable and converted to physical height for selected operations. **[O3, O4, O8, O16]**

**Same:** seeded procedural generation, many configuration controls, and reproducibility that depends on more than the visible seed.

**Different:** Map2 exposes a broad development configuration; Orogen has a much more developed shareable browser recipe. Neither defines a comprehensive, versioned, cross-runtime world contract.

**Successor implication:** preserve both a recipe and a generated snapshot. The recipe should include algorithm/content versions, topology and dimensions, canonical simulation resolution, seed, all effective parameters, and ordered edits. Choose physical units at field boundaries, ideally meters, square meters, seconds/days, degrees Celsius, and precipitation depth per specified interval. A slider may be dimensionless; the stored authoritative field should not be ambiguously so.

### Phase 2 — Surface sampling and cell geometry

**Purpose:** establish a stable set of locations on which global simulation operates.

**Map2.** Plane seeds are random points. Wrapped Voronoi construction uses tiled copies. Sphere seeds are sampled in longitude and vertical sphere coordinate, with two explicit pole points and exclusion caps around them. Spherical Voronoi geometry is actually computed on the sphere; this is more than drawing a wrapped rectangle as a globe. However, cell centers and optional relaxation use projected polygon geometry rather than a fully spherical centroid operation. The polar exclusion angle is clamped to a minimum of three degrees. At high cell counts, that creates oversized polar cells relative to surrounding cells. The default relaxation count is zero, so relaxation concerns are not active in the supplied normal configuration. **[M3, M4]**

**Orogen.** Uses a near-uniform Fibonacci sphere with controllable jitter. Stereographic projection feeds Delaunator, then the hull is closed with an additional pole region. Consequently, a request for N points produces N+1 regions in the tested path. Positions are held in typed arrays, with a triangular/half-edge mesh and compact adjacency. Its dual rendering points are arithmetic triangle centers, rather than exact spherical Voronoi circumcenters. **[O5]**

**Same:** irregular cell graphs and a triangulation/Voronoi-like representation underpin simulation and display.

**Different:** Map2 has genuine plane/sphere branches and true spherical Voronoi polygon construction; Orogen has a more consistently sphere-native, roughly uniform sampling scheme. Neither fact alone establishes numerical superiority: cell area, metric quality, graph conditioning, and stable IDs also matter.

**Successor implication:** choose the canonical macro mesh independently of render tessellation. Evaluate a deterministic, approximately equal-area sphere representation against the operations you need. An icosphere, a cube-based spherical grid, or a carefully specified irregular mesh could all be candidates; this review does not establish that one must be chosen. Persist cell IDs and adjacency, and compute actual cell areas. Avoid building settlement or runoff formulas on the assumption that every irregular cell represents equal area.

### Phase 3 — Topology, adjacency, and metric operations

**Map2.** Plane adjacency comes from Voronoi ridges; sphere adjacency comes from the convex hull/Delaunay graph of unit sphere points. Great-circle distance helpers exist. Nevertheless, local spherical neighbor vectors use a longitude/latitude approximation with a floor on the cosine latitude factor. Several feature kernels operate on projected XY. An especially important semantic mismatch is that planar edge weights represent shared Voronoi boundary lengths, whereas the spherical adjacency path supplies center-to-center great-circle lengths in the corresponding slot. A later perimeter-weighted coastline calculation therefore does not mean the same thing on both topologies. **[M3–M5]**

**Orogen.** Uses unit sphere positions, local tangent directions, three-dimensional neighbor differences, and sphere-based noise. Some distances are chord lengths, some are graph hops, and some are mapped to kilometers with an Earth-radius assumption. It is internally more spherical, but is not an all-purpose topology library. There is no implemented nonwrapping or toroidal plane counterpart. **[O5, O8, O11–O14]**

**Successor implication:** a topology interface needs more than `GetNeighbors`. It should define surface position, cell area, shared-edge length, intercell distance, local tangent displacement/direction, boundary condition, and a stable way to transport directions between tangent frames. Sphere velocity is rotational; flat velocity is translational. Those should be explicit implementations of a shared semantic operation. Flat maps also need their own climate boundary/forcing policy; latitude-based Earth climate does not automatically make sense on every flat world.

### Phase 4 — Plate seeding, growth, and connectivity

**Map2.** Chooses plate seeds and lognormal growth weights, then repeatedly selects active plates and random frontier cells. Land assignment comes later. This creates heterogeneous plate areas with comparatively simple logic. Frontier sets are converted to lists for random selection; integer-set iteration often repeats in a particular runtime, but this is not a portable ordering specification. A fallback can reseed a plate into disconnected leftovers. **[M5]**

**Orogen.** Uses spatially separated seeds with randomness among good candidates. Plate growth includes rate differences, directional bias, compactness, and an area governor. Cleanup applies majority smoothing and reconnects plate components. It first runs this on a fixed 20,000-point reference sphere, then maps plate labels to the requested mesh through seeded, three-dimensional noisy nearest-region lookup. **[O4, O6]**

**Same:** both partition a neighbor graph by seeded region growth; neither simulates fragments physically moving and colliding to discover the partition.

**Different:** Map2's model is compact and flexible; Orogen gives more explicit control over plate shape and resolution stability. Orogen's canonical coarse graph is especially useful for your need to show the same world at different detail levels.

**Successor implication:** adopt the separation between tectonic resolution and display resolution. Keep connectivity as a tested invariant. Define all tie-breaking by stable IDs, and make plate-size distributions explicit. Do not rely on a random choice from unspecified container order.

### Phase 5 — Continental crust, continent grouping, and initial ocean allocation

**Map2.** Each generated plate receives a land/ocean type using a probability. The actual file sets a land chance of 0.20. This is a probability per plate, not a promise of 20% land area, and it does not explicitly request a continent count. Since plates have different sizes, land fraction can vary substantially. **[M2, M5]**

**Orogen.** Operates on the coarse plate graph to select continent seeds and grow groups of whole plates. Seed selection and growth account for area and spatial distribution; separate growing continents are discouraged from touching. Small trapped seas may be absorbed. Targets and budget tolerances make continent count and land coverage approximate, not exact final-surface guarantees. Later elevation and erosion can change coastlines. **[O7]**

**Same:** both initially tie broad land/ocean character to plate identity.

**Different:** Orogen directly models the user's desired continent grouping and land budget. Map2 lets continental arrangement emerge from independently typed plates.

**Successor implication:** borrow Orogen's controllable grouping, but separate `CrustType`, `Elevation`, `OceanConnected`, and `WaterBodyId`. Continental crust can be submerged; oceanic crust can support an island. A tectonic plate should eventually be allowed to contain both crust types if mixed plates are a design goal. Avoid making plate type the universal definition of land throughout the pipeline.

### Phase 6 — Coast and plate-boundary cleanup

**Map2.** Coast smoothing considers the fraction of neighboring shared edge weight belonging to land or sea. It can reassign a cell to an adjacent plate of the desired type. A deterministic cell/pass hash and latitude-based resistance reduce smoothing near the poles. The parameter is called glaciation, but it is **resistance to coastline reassignment**, not glacial carving, deposition, ice flow, or sea-level history. It does not smooth every internal plate boundary. **[M5]**

**Orogen.** Plate-boundary roughness enters through noisy projection from the coarse sphere, followed by label cleanup. Later elevation detail, coast-specific terms, erosion, and ocean connectivity fixups further shape the actual coast. Glaciation is a separate elevation-modifying erosion operator. **[O4, O6, O8, O9]**

**Successor implication:** make boundary cleanup and geomorphic coastline formation separate phases. Preserve crust/plate labels independently from small shoreline detail. Use actual shared-edge length for perimeter-weighted operations, including on the sphere. Do not transfer similarly named sliders between these projects on the assumption that they perform the same operation.

### Phase 7 — Motion, convergence, subduction, and stress

**Map2.** A plate has one speed and one projected direction. For a pair of adjacent plates, relative motion is compared with a centroid-to-centroid direction to classify convergence, divergence, or transform behavior. The result is effectively a pair-level classification. It extracts boundary segments, retains the largest connected component, and constructs a smoothed chain for terrain features. Smaller disconnected contacts can consequently be omitted from the feature system. **[M5, M6]**

**Orogen.** A plate has an Euler pole and angular velocity. At a surface position p, local motion follows the rotational form `v = ω × p`. Convergence is evaluated locally across neighboring cells using the change in their separation under motion. Seeded plate density and local variation influence which side subducts. Stress, direction, and subduction information propagate into plate interiors. A plate-physics stage adjusts motion using size/continental drag, mantle-related forces, slab pull, and ridge push heuristics. These are one-shot rules, not a geological time integration. **[O8, O10]**

**Orogen's additional scale:** same-type connected plate groups are partitioned into super-plates; combined rotations support broader tectonic structure. The current blend is not the simple ratio suggested by older documentation. With `PLATE_BLEND_T = 0.7`, the implemented weighting formula produces approximately 0.88 for super-plate stress and 0.58 for small-plate stress. They are not normalized to sum to one. Super-plate boundaries supply the broad distance skeleton, while small-plate detail remains influential. **[O8, O10, O17]**

**Same:** both infer present terrain from prescribed plate geometry and motion; neither reconstructs authentic plate history.

**Different:** Orogen handles local spherical boundary kinematics and asymmetric subduction much more explicitly. Map2's pair-level classification is easier to understand and attach named features to, but cannot naturally represent convergence along one part of a curved contact and transform motion elsewhere.

**Successor implication:** use local boundary segments with signed normal and tangential relative velocity. Keep derived fields inspectable: compression, shear, subduction side, uplift potential, and age/province tags. Treat the optional super-plate scale as a controllable source of broad structure, with documented blend semantics. Integrate every connected boundary component, rather than silently selecting only the largest.

### Phase 8 — Baseline elevation and distance fields

**Map2.** Computes coast distance with multi-source Dijkstra, starting coastal cells partway across their adjacent edge. Land and ocean are processed separately. Distances are scaled by a fudge factor before applying relief profiles. The currently executed land profile is a logistic expression with broad and local multiplicative noise; the sea profile asymptotes toward roughly 1,524 meters depth. Several arguments/configuration names suggest different plateau/depth controls but are not used in these formulas. Broad/local baseline noise uses fixed seeds 501 and 805 rather than the world seed. **[M7]**

For example, the active land expression includes `300 / (1 + exp(-0.005 * (d_miles / 2 - 800)))`; passing a nominal 500-meter plateau does not make that expression use 500. This is a code/configuration contract issue, not merely a tuning preference.

**Orogen.** Builds a terrain skeleton from distances to mountain, coastline, and ocean-related seed sets, combined with plate base offsets and propagated stress. A reciprocal-distance blend creates broad relief transitions. Its `assignDistanceField` shuffles the pending frontier and commits a cell on first visit, so that particular field is an artistic stochastic growth distance rather than guaranteed shortest-path BFS. Other breadth-first helpers elsewhere in the project should not be conflated with this routine. **[O8]**

**Same:** neither starts with unconstrained global noise as the sole terrain structure. Both construct broad terrain in relation to tectonic or coastal features.

**Different:** Map2's coast distance has a clearer physical metric; Orogen has a richer tectonic scaffold but some “distance” fields are deliberately or incidentally stylized.

**Successor implication:** distinguish physical distance fields from artistic influence fields in names and types. Build physical elevation in meters from baseline crust/bathymetry and tectonic contributions. If stylized stochastic growth gives attractive shapes, preserve it as a separate field rather than allowing rivers, travel, or climate to interpret it as kilometers.

### Phase 9 — Orogens, shields, basins, plateaus, and tectonic provinces

**Map2.** Has an extensive explicit feature vocabulary: ancient and reactivated orogens, shields, sedimentary basins, hotspot chains, hotspot plateaus, subduction systems, continental collision systems, and divergent systems. Features are compiled into spatially indexed ellipse-like compact-support kernels and sampled into cell elevations. Many use chains, secondary ridges, shoulders, pockets, rims, or saddles to avoid one smooth lump. Ancient/reactivated features are synthetic placements; “reactivated” does not mean the program first generated an ancient belt and then evolved that same belt through time. **[M6]**

Collision systems can combine broad asymmetric uplift and edge ridges. Subduction adds a ridge with different treatment of land–ocean and ocean–ocean contacts. Divergence carves valleys, with optional shoulders. Transform classification does not dispatch an equivalent dedicated terrain-feature compiler. Shields are applied multiplicatively to the initial baseline rather than simply added with all other blobs.

There is an implementation mismatch in shield handling: feature compilation creates shield objects for diagnostic metadata, but application separately regenerates shield geometry from a reset random stream. It also omits the world-mode argument at that regeneration call. Thus shield labels and applied shield influence need not describe the same geometry. This was established by tracing the calls; it was not a rendered overlay comparison.

**Orogen.** Blends continuous fold/craton/basin/plateau archetype weights, boundary stress, plate bases, foreland/back-arc depressions, passive continental plains, and plateaus into the skeleton. Oriented phasor-style ridge synthesis gives mountains a directional internal grain rather than relying solely on isotropic noise. Additional texture bands and elevation-gated detail break up otherwise smooth terrain. Its “orogenic power” is a remapped low-frequency noise field used as an uplift/detail control; the name should not be interpreted as measured mechanical energy. **[O8, O17]**

**Same:** both layer broad geological character and smaller landform structure onto a tectonic framework.

**Different:** Map2 excels at named, inspectable feature objects and environmental metadata. Orogen more deeply integrates broad provinces, directional mountain texture, and the resulting elevation field.

**Successor implication:** retain geological feature identities and province masks as first-class outputs, while using Orogen-like field synthesis to realize them. A basin should remain identifiable after noise and erosion. That identity can later inform sediment, aquifers, soils, mineral resources, route corridors, and settlement suitability. Avoid reconstructing semantic geology from final height alone.

### Phase 10 — Volcanism, hotspots, ocean floor, and coastal relief

**Map2.** Hotspot chains trail against plate motion, with age-related tapering of feature size/amplitude. Larger volcanic plateaus use lobed forms and cuts. They add useful geology beyond active plate contacts, but remain synthetic placements in largely projected feature coordinates. Ocean depth is primarily the baseline profile plus any overlapping geological features; there is no comparably developed, separate shelf–slope–abyss–ridge–trench system. **[M6, M7]**

**Orogen.** Includes oceanic island arcs and volcanic arcs, hotspot swell and peak structure, drift trails, large igneous provinces, and shape modifiers such as elongation, calderas, and radial ridges. Bathymetry distinguishes shelves, slopes, abyssal regions, mid-ocean ridges, fracture-related depression, and trenches. Active and passive coasts can have different shelf widths and relief. These are constructed landform rules, not magma or seafloor-age simulations. **[O8]**

**Successor implication:** Orogen is the stronger reference for detailed ocean-floor and arc morphology. Combine this with Map2's explicit feature records. Store volcanic age/activity and lithology proxies separately from elevation if resources, hazards, soil fertility, or historical events will depend on them. Those outputs would be additions, not capabilities already fully modeled by either project.

### Phase 11 — Noise, terrain remapping, and scale

**Map2.** Baseline relief uses FBM-like multiplicative variation; geological features supply most major elevation changes. Another conspicuous noise system perturbs Voronoi polygon borders for visual presentation. That border noise is not a local terrain height generator. The optional `noise` package changes its implementation: if absent, a sine/cosine fallback is used. This silently changes appearance across environments unless dependencies are fixed. **[M4, M6, M7]**

**Orogen.** Uses three-dimensional sphere noise for plate-boundary projection, broad province variation, stress-associated texture, coastal detail, domain warp, and fine relief. Phasor ridges add anisotropy. Peak compression and empirical hypsometric remapping adjust the height distribution. Postprocessing adds detail even when several visible effect sliders are zero. **[O4, O8, O9]**

Orogen's land-height conversion in `elevToHeightKm` is `6 * t^4 * (5 - 4*t)` for clamped land elevation t; below zero it uses a different linear scale. Thus t=0.5 means about 1.125 km, not half of the maximum height. Some erosion operates on raw elevation, while some climate/detail calculations operate in converted kilometers. Its grayscale import uses a square-root inverse assumption associated with a different height curve, so import and current physical-height mapping are not a clean numerical round trip. **[O1, O16]**

**Successor implication:** specify wavelengths and amplitudes in world units. Keep rendering exaggeration separate from physical relief. For detailed exploration, use a deterministic spatial noise function constrained by the macro surface, geological province, river corridors, coastlines, and settlement grading. Neither project's current noise stack already supplies that constraint hierarchy.

### Phase 12 — Erosion, glaciation, and drainage conditioning

**Map2.** Does not run a general hydraulic, thermal, or glacial elevation-erosion stage. River and wetland classification occur after terrain construction and do not carve valleys into the height field. Its coast-smoothing glaciation control is not an equivalent to Orogen's glacial erosion. **[M1, M5, M8]**

**Orogen.** The worker's postprocessing order is terrain warp → ocean-mask capture and pre-erosion snapshot → bilateral smoothing → detail noise → interleaved glacial/hydraulic/thermal erosion → ridge sharpening → soil creep. Detail noise remains active and soil creep uses fixed settings in the current path, so “all relevant sliders zero” is not an unprocessed tectonic baseline. **[O1, O9]**

Hydraulic erosion computes receivers and accumulated contributing cells, then uses an implicit stream-power-style elevation update. Local flow initially contributes one per land cell; it does not use the later climate precipitation field. Receiver selection is based on drop, while distance enters the erosion equation. Sediment deposition is simplified. Thermal erosion redistributes height above a talus-like threshold. Glacial rules use latitude/elevation-derived ice, flow accumulation, valley widening/overdeepening, moraine-like deposition, and fjord-related shaping.

Priority-flood conditioning identifies the main ocean, fills depressions and traces/carves spill routes with a blend and epsilon rules. It is useful terrain conditioning, but does not export a complete basin/lake model. Later sharpening and creep occur after conditioning; there is no final invariant check proving that all desired land cells still drain to an intended outlet. Physical water and sediment conservation should not be assumed.

**Successor implication:** adopt erosion as an explicit terrain-changing phase, but first decide which basins must be preserved. Rivers should not be forced to drain every inland basin to the sea, nor should arbitrary numerical pits automatically become gameplay lakes. Use a preliminary climate/runoff estimate if rainfall should influence erosion, followed by final drainage and climate updates. Expose coarse erosion intensity separately from realistic hydrological state.

### Phase 13 — Seasonal forcing, pressure, and winds

**Map2.** Supports several solar heat modes, including axis-, noise-, point-, and edge-related fields; sphere mode selects axis behavior. However, final temperature uses the seasonal metadata and its own latitude/insolation calculation, rather than consuming the generated `heat_by_season` field as the authoritative heat input. Consequently, alternative solar heat layers should not be assumed to control the final climate consistently. **[M9]**

Seasonal belt construction samples longitude, responds differently over land and ocean, and creates distorted ITCZ/subtropical/subpolar/polar structures. Pressure influences are built from these belt splines. Winds are solved on a coarse approximately five-degree longitude/latitude grid from smoothed pressure gradients with hemispheric turning and fallback belt flow, then interpolated to cells. Some routines use periodic longitude operations regardless of the plane's nonwrap setting. That weakens end-to-end support for an unwrapped flat climate.

**Orogen.** Builds tangent frames on the sphere, uses thermal maxima/ITCZ information, zonal and land/ocean thermal pressure effects, and elevation influences. Pressure gradients and Coriolis/friction rules produce seasonal winds. Geographic grids are still used for some intermediate summaries; it is not a global atmospheric PDE solver. The standard output consists of northern summer and northern winter, rather than a resolved annual cycle. **[O11]**

**Same:** both generate plausible seasonal wind geography from pressure and circulation heuristics.

**Different:** Map2 emphasizes explicitly constructed belt splines and configurable seasonal sampling. Orogen couples more of its circulation machinery directly to spherical/tangent geometry and tuned geography.

**Successor implication:** define climate forcing independently of the map projection. Decide whether two seasonal snapshots are enough for the first historical simulation. If crop calendars, monsoons, snowmelt, and seasonally navigable routes are central, a modest monthly or multi-season climatology may be worth the extra cost. Whatever the sample count, every quantity needs an explicit time interval and weighted annual aggregation.

### Phase 14 — Ocean currents and maritime influence

**Map2.** Builds ocean sections of circulation-belt paths, then follows coastal deflections and associates warm/poleward and cool/equatorward behavior with them. Current-related thermal influence can extend inland near coasts. Winds and ocean currents share belt inputs; the current solver is not simply advecting ocean water under the computed wind vector field. **[M9]**

**Orogen.** Creates rule-based zonal flows and ITCZ countercurrents, with coastline/basin-direction information distinguishing western/eastern boundary currents. Warmth is a geographic proxy rather than an advected, energy-conserving sea-surface temperature field. Circumpolar detection uses longitude-bin water coverage; that does not by itself prove a continuous ocean channel around the planet. Smoothing makes the current field more coherent. **[O12]**

**Same:** both seek useful climate effects without a full ocean model.

**Different:** Map2's path representation is well suited to visible current routes; Orogen's cell field is naturally consumed by its other spherical climate calculations.

**Successor implication:** begin with a cell-based ocean warmth/current approximation, and derive paths for visualization if useful. Validate basin connectivity before applying global current regimes. Keep current direction, speed proxy, and thermal anomaly distinct. A detailed ocean solver is not necessary to get useful settlement and biome geography.

### Phase 15 — Precipitation and moisture transport

**Map2.** Selects one best-aligned upwind neighbor for each cell. A functional-graph solver estimates moisture from local sources and retained upwind supply, including analytically handled cycles. Ocean/coastal supply, recycling, uplift, circulation, barriers, currents, and inland drying influence precipitation. **[M9]**

A reproduced defect occurs when a chain reaches a cycle that is solved during that traversal: the tail is propagated with a zero base instead of the solved cycle budget. In a three-cell example with unit sources and 50% retention, the correct result is `[2, 2, 2]`, while the function returns `[1, 2, 2]`. This is a local solver defect, not evidence that all Map2 rainfall is unusable.

There is also an annualization mismatch: hydrology averages seasonal precipitation while climate/biome/ecoregion paths sum it. Since precipitation production does not consistently define duration-weighted seasonal totals, changing sample count changes semantics. The same array should not silently mean both an average and an annual sum.

**Orogen.** Advects moisture from multiple aligned upwind neighbors with double buffering, coastal/ocean moisture supply, and elevation-dependent depletion. It combines that with ITCZ, convergence, orography, rain-shadow, subtropical drying, monsoon, current, and geographic heuristic terms. The final blend is heavily empirical: the detailed precipitation contribution is weighted about 0.3433 and the heuristic contribution about 0.6567. Seasonal percentile normalization and other contrast/cap adjustments further shape the result. Advection hop counts are bounded, so physical reach is not perfectly resolution-invariant. **[O13, O17]**

**Same:** both combine directional moisture transport with intentionally stylized climatic tendencies.

**Different:** Map2 offers a compact one-parent graph formulation; Orogen combines multi-neighbor transport with a much larger tuned heuristic system. Orogen's precipitation output is a relative/tuned quantity before later scaling, not a solved physical water budget.

**Successor implication:** use explicit precipitation depth per time interval, and accumulate water using cell area. Multi-neighbor transport can reduce one-neighbor directional artifacts, but neither algorithm should be copied without conservation/normalization decisions. Preserve separate diagnostics for moisture availability, orographic enhancement, rain shadow, and final precipitation. Those will make calibration and debugging much easier.

### Phase 16 — Temperature, seasonal contrast, and continentality

**Map2.** Combines a latitude-shaped baseline with daily-insolation information, damps seasonal variation according to maritime influence, applies lapse rate, and incorporates current/coastal and wind-related effects. The active latitude/insolation combination is weighted predominantly toward the latitude baseline. Temperature is returned in Celsius, although some configuration controls use Fahrenheit and feet. **[M9]**

**Orogen.** Computes temperature after precipitation. It uses ITCZ-distance and zonal terms, moisture-dependent lapse rate, currents, coastal propagation, and cloud/clear-sky moderation. Continentality is more elaborate than a single distance-to-coast curve: landmass area and dimensions, coastal shaves, island overrides, smoothing, and oceanic-to-hypercontinental classes influence seasonal swings and winter bias. Values are then normalized to a bounded temperature representation for downstream use. **[O14]**

**Same:** both seek latitude, altitude, ocean moderation, and seasonality without a full energy-balance atmosphere.

**Different:** Orogen has a more developed continentality model and makes precipitation an input to temperature. Map2's ordering avoids that dependency but makes its own solar-layer semantics less direct than their presentation might suggest.

**Successor implication:** Orogen's continentality decomposition is worth retaining as a design reference. Store Celsius directly in authoritative output, keep display normalization outside the model, and choose a bounded dependency schedule if temperature and moisture affect each other. A fixed deterministic number of coupled passes is easier to reproduce and budget than an unspecified convergence loop.

### Phase 17 — Rivers, lakes, wetlands, and watershed state

**Map2.** This is a major additional capability. Land routes to a lower neighbor, with ocean adjacency prioritized; otherwise sinks remain. Downstream accumulation proceeds in descending elevation order. Local contribution combines precipitation and runoff response to environmental conditions. The exported fields support visible river networks and later ecological rules. **[M8]**

Important limitations:

- Receiver choice favors the lowest neighbor rather than consistently the steepest distance-normalized slope.
- Contributions and drainage area use cell-based quantities rather than actual physical cell area. “Discharge” is therefore a useful index, not cubic meters per second.
- Endorheic lake size follows an inflow/evaporation heuristic, with upstream-tree backfilling. It does not solve a general depression hierarchy with a common water surface, storage curve, spill elevation, and outlet.
- Partial-cell and ephemeral water, majority lake cells, wetlandness, peat, bog, and marsh distinctions are useful environmental outputs, but are not a fully conservative hydrological simulation.

**Orogen.** Computes receiver/flow information internally for erosion, but does not carry forward a comparable permanent river graph, lake storage/outlet system, watershed record, and wetland ecology. Valley-like terrain is not the same deliverable as a navigable river with seasonal discharge and a persistent identity. **[O9]**

**Successor implication:** take Map2's breadth of hydrology outputs, then rebuild the numerical foundation. Export stable watersheds, river reaches, lake IDs, lake surface elevation, spill/outlet, contributing area, and seasonal discharge. Decide explicitly between ocean-draining basins, endorheic basins, and small removable numerical pits. This stage is critical for city placement, agriculture, trade routes, floods, and local terrain continuity.

### Phase 18 — Climate classification, biomes, ecoregions, and biological content

**Map2.** Uses a custom, simplified seasonal Köppen-style classifier, then a much richer biome system consuming climate, elevation, roughness, water, wetlands, peat, flow, coast, and current information. Biomes include finer distinctions such as floodplains, dryland variants, alpine environments, freshwater, and marine types. Ecoregions group connected cells sharing environmental/biome categories and derive environmental statistics. Producer and animal templates are selected by habitat and trophic relationships, with stable procedural naming/identity mechanisms and abundance proxies. **[M10]**

This is static ecological synthesis, not populations breeding, migrating, competing, and evolving through time. Lake cells can receive an ocean code in the climate-classification layer as a water-category convention; separate freshwater/biome state must be consulted to understand them correctly.

**Orogen.** Converts two seasonal temperature/precipitation fields into Köppen categories with fitted scaling and shoulder/driest-month proxies. It includes geographic adjustments, so it should not be described as a pure standard monthly Köppen calculation. Biome appearance is then largely a color interpretation of Köppen and elevation, including alpine/snow treatment. There is no comparable explicit ecoregion/trophic data model. **[O15, O16]**

**Same:** climate categories are useful summaries and visualization layers.

**Different:** Map2 substantially exceeds Orogen in environmental state beyond a climate label. Orogen's climate tooling is more developed for empirical tuning, discussed below.

**Successor implication:** preserve separate climate class, potential natural biome, actual land cover, and ecological region. Civilization will change land cover through farming, grazing, forestry, and urbanization; that should not erase underlying climate. Convert environmental suitability into resource potentials before placing people. Neither project currently supplies the complete economic resource model you need.

### Phase 19 — Presentation, editing, persistence, and validation

**Map2.** WGPU viewer layers, river overlays, inspection, and progressively refined polygon edges make environmental fields explorable on a map. The extra LODs do not create new simulation regions or local mountains, valleys, roads, buildings, or collision terrain. Startup is coupled to the viewer import; a headless core can be exercised with a stub, but there is no packaged engine-independent generation service in the supplied archive. **[M1, M11]**

**Orogen.** Has a globe/map browser experience, worker progress, debug layers, plate editing, parameter reapply, planet codes, image import, and raster exports including 16-bit grayscale PNG paths. Those are valuable product precedents for your world-generation menu. Heightmap import is a grayscale interpretation and synthetic plate reconstruction, not a complete world-state import. A raster or seed code cannot preserve every future historical entity, river identity, edit, and resource. **[O1, O2, O16, O18]**

The repository also contains terrain metrics and climate tuning tools. Climate evaluation compares generated fields from imported Earth terrain against reference classifications, with land-mask agreement and mismatch handling. The currently implemented aggregate score combines graded correctness, macro F1, group balance, and a watchlist with weights 0.60/0.12/0.15/0.13, rather than the simpler older README description. This review did not rerun that evaluation or validate its published scores. **[O19]**

**Successor implication:** make inspection and calibration part of the core project from the beginning. Every stage should be viewable, hashable, exportable, and independently diagnosable. Port the idea of Orogen's tuning harness, not its tuned Earth constants without evaluation. Test synthetic worlds, alternate seeds, resolutions, and topology cases; use held-out reference cases to avoid optimizing only one Earth map.

### Phase 20 — People, civilizations, history, and explorable terrain

**Neither implementation provides these systems.** Map2's animal assemblages are not a civilization simulator; Orogen's globe mesh and heightmap export are not a streamed playable world.

Your added stages would consume the earlier outputs:

1. Derive habitability, water security, food potential, construction suitability, resources, hazards, and travel costs.
2. Place initial populations or settlements according to a specified historical starting model.
3. Simulate settlement growth, trade, states, conflict, migration, culture, and religion through explicit time steps and rules.
4. Preserve an authoritative historical snapshot and stable settlement/route IDs.
5. Realize local terrain from macro/regional surfaces, geology, water, settlement footprints, and spatial noise.
6. Generate roads, buildings, vegetation, and encounter/gameplay content with their own deterministic inputs.

These are proposed successor requirements, not a claim that either inspiration already contains hidden versions of them. The environmental richness in Map2 makes steps 1–3 easier to define; Orogen's terrain and browser workflow make useful references for the geographic and authoring portions.

## 5. Proposed stage contracts for the successor

The following is a starting specification for discussion, not a final selection of algorithms. It deliberately states the output each phase must provide. That lets us change an algorithm without forcing the rest of the project to understand its internals.

| Stage | Authoritative output | Starting choice | Acceptance condition |
|---|---|---|---|
| 0. Recipe | Seed, algorithm/content versions, topology, dimensions, parameters, edits | New versioned recipe | Serialize/deserialize without changing effective inputs |
| 1. Macro surface | Stable cell IDs, positions, areas, adjacency, metric data | Sphere-first deterministic mesh | Closed sphere; symmetric adjacency; valid areas and distances |
| 2. Tectonic partition | Plates, crust types, continent groups | Orogen-style canonical coarse partition | Connected intended plates; bounded land/continent target error |
| 3. Boundary mechanics | Local convergence/shear, subduction side, stress, broad tectonic scale | Orogen-style local motion | Rotation/coordinate changes do not introduce projection artifacts |
| 4. Geological provinces | Belts, shields, basins, volcanic systems, stable feature IDs | Map2 semantics plus Orogen fields | Diagnostic features and applied geometry are identical objects |
| 5. Physical relief | Elevation in meters, bathymetry, province masks | Hybrid tectonic skeleton and constrained noise | Units explicit; coast/crust distinction preserved |
| 6. Preliminary water/climate | Provisional runoff and erosion forcing | Simplified deterministic climatology | Defined precipitation interval and cell-area weighting |
| 7. Geomorphic shaping | Eroded relief and sediment/province changes | Orogen-inspired erosion | Protected basins respected; no accidental unit mixing |
| 8. Final drainage | Watersheds, receivers, reaches, lake surfaces/outlets/storage | New model informed by Map2 | No unintended cycles; consistent lake level; water accounting |
| 9. Final climate | Seasonal temperature, precipitation, wind, ocean influence | Selected mechanisms from both | No season-count ambiguity; topology-aware boundaries |
| 10. Environment/resources | Potential biome, soils, water security, resource suitability | Map2-inspired ecology | Water and climate labels remain distinct |
| 11. Initial people | Population/settlement seeds and carrying-capacity inputs | New | Reproducible placement; explicit starting assumptions |
| 12. History | Towns, states, cultures, religions, routes, dated events | New shared simulation | Same recipe, action log, and tick count produce same state |
| 13. Regional realization | Stable regional terrain and route/water constraints | New | Macro geography preserved; shared borders agree |
| 14. Playable chunks | Terrain, water, roads, city grading, vegetation/building instances | New Unity realization over shared data | Load order does not change chunks; seams and rivers match |

Stages 8 and 9 may need a bounded feedback loop if final lake coverage materially affects climate or runoff. The iteration count and stopping policy must be part of the recipe. Begin with a simple explicit schedule before adding more coupling. Do not let a wall-clock timeout decide how many numerical passes one client completes.

### Which concepts I would carry forward first

**From Orogen:** canonical coarse plate layout; local Euler-pole motion; density-aware subduction; broad tectonic scale; differentiated ocean floor; oriented mountain synthesis; configurable erosion; worker progress; intermediate diagnostic layers; terrain/climate calibration harnesses.

**From Map2:** named geological feature records; physical-height output; separation of drainage and ecological interpretation; lakes/wetlands/peat/floodplain concepts; detailed biome and ecoregion data; environmental inputs suitable for resource and settlement models; the intention to support multiple topologies.

**Rebuild rather than carry forward unchanged:** execution orchestration, exact reproducibility contract, physical units, final hydrology, cross-topology metrics, save schema, and macro-to-local constraint hierarchy. Those are structural requirements of your intended game, not cosmetic improvements to either inspiration.

## 6. Unity, C#, and Blazor WebAssembly assessment

### Your tentative language split is viable in principle

A shared C# generation/history library with a Unity host and a Blazor WebAssembly host is a sensible architecture to prototype. The core must contain no Unity scene objects, editor APIs, Blazor components, DOM state, or renderer-owned randomness. Both hosts should request the same generation stages and receive the same snapshots.

For a concrete compatibility starting point, Unity 6.0 documents .NET Standard 2.1 as its default API compatibility level and recommends the smaller Standard surface for cross-platform work. A compatible shared library or shared-source target is therefore a candidate; this is not a claim that Unity and browser .NET use identical runtimes. Select the actual Unity version and test the precise common API/dependency subset before committing the full port. [Unity API compatibility documentation](https://docs.unity3d.com/6000.0/Documentation/Manual/dotnet-profile-support.html).

Browser computation should run outside the UI thread. Microsoft's Blazor/.NET Web Worker guidance explicitly covers executing C# in a worker while the interface remains responsive. The worker host should reference the same core and expose coarse requests, progress, cancellation, and results. This is different from merely adding `async` to a long CPU loop. [Blazor with .NET on Web Workers](https://learn.microsoft.com/en-us/aspnet/core/blazor/blazor-with-dotnet-on-web-workers?view=aspnetcore-10.0).

### Recommended responsibilities

| Component | Owns | Must not decide |
|---|---|---|
| Shared generation core | Geometry, plates, terrain, climate, hydrology, ecology; deterministic algorithms | Camera, mesh display LOD, UI selection |
| Shared history core | Population, settlement, economy/politics/culture/religion rules and state transitions | Frame rate or render ordering |
| Shared serialization | Recipes, snapshots, action logs, versions, canonical hashes | Platform-specific visual state as world truth |
| Unity runtime host | Main-menu generator screen, task scheduling, cancellation, progress, snapshot loading | An alternate world-generation algorithm |
| Unity world renderer | Globe/map views, local chunks, collisions, visual assets, gameplay integration | Macro geography through unseeded or load-order-dependent randomness |
| Browser worker host | Background generation/history execution, bounded messages | A simplified fallback that silently produces another world |
| Blazor interface and map renderer | Controls, inspectors, overlays, save/load UI | Authoritative simulation changes caused by view resolution |

Generation must be runtime-capable from the start. A Unity Editor utility can help development, but cannot be the only host if the shipped game needs a world-generation menu. Main-thread Unity mesh/object work should consume completed data; computational stages should work on plain data with a platform-specific scheduler.

### Define what “same seed equals same results” means

For your stated requirement, I recommend the stronger contract:

**Same seed + complete recipe + algorithm/content versions + ordered edits/action log + simulation tick count = identical authoritative world state.**

Different renderers may use different shaders, tessellation, or cosmetic particles. They must agree about land, rivers, towns, populations, borders, cultures, and religions. If exact local terrain is also shared, its height and instance generation belongs under the same deterministic contract.

The main failure modes are:

1. **Random implementation drift.** Own and version a small PRNG with explicitly defined integer arithmetic, range sampling, and seed derivation. Do not assume `System.Random` is a permanent file format: Microsoft explicitly does not guarantee the same implementation across major .NET versions. [System.Random documentation](https://learn.microsoft.com/en-us/dotnet/api/system.random?view=net-10.0).
2. **Random consumption order.** Use independent stage/entity streams or a counter/hash-based random function. Adding an animal name should not change tectonic plates. Region and chunk randomness should derive from stable spatial/entity keys, not the order in which the player visits them.
3. **Unspecified container ordering.** Sort or otherwise canonically order sets, ties, queues, and reductions. Specify equal-priority heap behavior and equal-cost path selection.
4. **Floating-point divergence.** Shared C# source does not guarantee identical transcendental math, triangulation decisions, fused operations, or reductions under all Unity and WebAssembly backends. For strict equality, use controlled deterministic math or integer/fixed-point representations where authoritative branching demands it, then verify actual compiled targets. Rounding arbitrary floating-point results at the end is not a proof: values can already have crossed a threshold or selected different neighbors.
5. **Parallel scheduling.** Parallel work is safe only where order cannot affect the result or where reductions are explicitly ordered. Keep GPU/fast-math computation out of authoritative state until demonstrated compatible with the chosen contract.
6. **Incremental cache differences.** Each cache entry should identify the exact stage version and input hashes. A cached reapply must produce the same result as invalidating those stages and running from the recipe. Orogen demonstrates why this needs a test.

Do not try to solve all performance problems before validating this contract. A small deterministic core running on the real Unity player backend and real browser WebAssembly is the first architectural experiment I would make.

### Macro-to-local terrain for a Daggerfall-like world

The macro generator should establish the location and identity of continents, mountain systems, drainage basins, major rivers, lakes, and settlements. Increasing exploration detail must not rerun those decisions at a new cell count.

Use a hierarchy with explicit constraints:

- **Macro:** elevation envelopes, geology, climate, watershed and political geography.
- **Regional:** river courses, valley floors, passes, coast/lake shorelines, major routes, settlement footprints.
- **Local:** terrain noise, erosion-like detail, banks, cliffs, soil/vegetation, roads, city grading, structures.

Local noise should sample a continuous world coordinate system. On a sphere, three-dimensional sphere coordinates avoid a longitude seam in scalar noise. Chunk boundaries need canonical ownership or identical shared samples, and tangent frames must agree where orientation matters. A river crossing a chunk edge must retain its identity, bed profile, width/discharge rules, and water surface. A lake must have one surface elevation across all affected chunks. A town cannot change its elevation or footprint because the terrain chunk was generated before the settlement.

History can alter local realization through stable, versioned constraints: a new road, drained wetland, terrace, town expansion, or abandoned site. Store those as authoritative edits/events, not incidental renderer modifications. This is the bridge between Civ-like macro simulation and explorable terrain that both source projects currently lack.

### Browser budget and deployment implications

Orogen demonstrates the practical value of retained worker arrays, deferred climate, and progress stages. It also retains and copies multiple large arrays. Your browser memory budget must account for simulation fields, stage snapshots, serialization, transfer/copy buffers, and GPU buffers—not just one heightmap. Make debug fields optional and cache only stages whose recomputation cost justifies them.

AOT WebAssembly is a performance option for CPU-heavy work, with a larger download tradeoff; it should be measured on representative workloads rather than assumed to solve algorithmic scaling. [Blazor performance documentation](https://learn.microsoft.com/en-us/aspnet/core/blazor/performance/?view=aspnetcore-10.0).

No maximum supported world size is recommended from these probes. Map2 was tested far below its attached 100,000-cell setting, and Orogen's rendering/browser memory behavior was not benchmarked. Establish target generation time, memory, history length, and lowest supported browser hardware before selecting production resolutions.

## 7. Headless verification performed

The probes exercised the real generation code with small substitutions only at the application boundary. No fixes were applied to either source project.

### Map2 checks

The actual `main.main()` was invoked with a stubbed viewer endpoint that captured its output arrays. The test used seed 42, 1,000 cells, exactly 20 plates, no additional viewer LODs, and otherwise retained the supplied configuration. It ran both plane and sphere modes twice, resetting the NumPy generator for every fresh run. The optional `noise` package was absent, so the code's own fallback was active. WGPU/rendercanvas were not exercised.

| Check | Plane | Sphere |
|---|---:|---:|
| Requested cells | 1,000 | 1,000 |
| Elevation finite | Yes | Yes |
| Fresh-run hashes equal | Yes | Yes |
| Land cells at elevation ≥ 0 | 65 | 115 |
| Elevation range, meters | −2,226.866 to 3,293.263 | −1,764.246 to 2,489.634 |
| Distinct biome codes | 29 | 23 |

Hashes were compared for elevation, plate assignment, seasonal temperature, seasonal precipitation, downstream receiver, biome codes, and ecoregion IDs. These counts describe two small samples; they are not a claim that one topology generates better terrain or that the plate land probability predicts those precise area fractions.

**Moisture solver regression case:** let each cell have a source of 1, retention of 0.5, and upwind parent indices `[1, 2, 1]`. The intended equations are `B[i] = 1 + 0.5 * B[parent[i]]`. Cells 1 and 2 form a cycle and both solve to 2; cell 0 must then also solve to 2. `_functional_upwind_budget` returns `[1, 2, 2]`. This directly confirms the tail-into-newly-solved-cycle defect described in phase 15.

### Orogen checks

The actual worker module ran under Node 24.19.0 with a mocked message endpoint. Its relative imports still referenced the real project modules. The pinned Delaunator 5.0.1 and robust-predicates dependency were made locally available in place of CDN imports. No renderer or actual worker transfer behavior was tested.

Inputs were: seed 42; N=5,000; P=20; jitter=0.75; noise magnitude=0.4; four requested continents; continent-size variety=0.35; land coverage=0.3; terrain warp=0.75; smoothing=0.1; hydraulic erosion=0.5; thermal erosion=0.1; glacial erosion=0.5; ridge sharpening=0.5; temperature/precipitation offsets zero; climate enabled.

| Operation | Result |
|---|---|
| Fresh generation | 5,001 regions, finite elevation |
| Fresh generation repeated | Elevation, summer temperature, and summer precipitation arrays exactly equal |
| Reapply with the same parameters | 2,060 of 5,001 elevation entries differed; maximum raw-elevation difference 0.426169 |
| Edit-recompute with unchanged plate types/densities | 5,001 of 5,001 elevation entries differed; maximum raw-elevation difference 0.863829 |

The differences above are in Orogen's raw elevation representation, not meters. Source tracing shows that same-parameter reapply omits the hotspot field used to damp terrain warp. The edit-recompute route also differs in motion/physics and mantle inputs. The tests confirm unequal outputs; they do not attribute every changed cell exclusively to one omitted argument.

The command sequence was fresh generate → fresh generate → same-parameter reapply → edit-recompute using the second fresh result's unchanged plate types/densities. Comparisons use the second fresh result as the baseline. Reapply does not reconstruct the retained tectonic inputs; the edit handler rebuilds elevation from those retained inputs through its distinct path.

For reference, the initial Orogen elevation-array SHA-256 was:

`508013c308550a8a27a4e5b9b5c0cd72b8a327186ebb8811e347b47f2097d09f`

**What these tests establish:** both fresh pipelines can execute headlessly at small scale and repeat in the tested environment; three specific correctness/consistency concerns were reproduced.

**What they do not establish:** browser-versus-Node identity, Python-version identity, Unity parity, visual correctness, all seed/configuration behavior, large-world stability, or performance superiority. The unchanged edit-recompute test exercises the handler directly; it is not a claim that every UI action invokes that handler with unchanged inputs.

## 8. Findings to resolve before reusing algorithms

Priority here means importance to your successor's stated requirements, not a judgment of the projects as artistic tools.

| Priority | Project | Finding | Evidence | Consequence for reuse |
|---|---|---|---|---|
| High | Orogen | Fresh/reapply/edit pipelines differ | Source + reproduced output differences | Use a single dependency-driven orchestrator |
| High | Map2 | Moisture chain loses newly solved cycle contribution | Three-cell reproduced case | Correct solver before adopting it |
| High | Map2 | Seasonal precipitation mean/sum semantics differ by consumer | Source | Define duration-weighted precipitation once |
| High | Map2 | Spherical metrics/projection and edge-weight semantics are inconsistent | Source | Rebuild metric-dependent operations behind topology contracts |
| High | Both | Neither establishes cross-runtime bitwise identity | Scope of implementation and tests | Version math/order/RNG and test real target backends |
| High | Both | No complete final physical watershed/lake model | Source | New authoritative hydrology stage |
| Medium | Map2 | Several baseline configuration arguments do not affect active formulas | Source | Trace effective parameters; remove or implement inert controls |
| Medium | Map2 | Applied shield geometry is regenerated separately from diagnostic features | Source | Generate one feature record, sample and display the same record |
| Medium | Map2 | Alternate solar heat field is not the final temperature driver's field | Source | Make climate forcing dependencies explicit |
| Medium | Map2 | Minimum polar exclusion cap creates oversized high-resolution pole regions | Source + sampling check | Use more uniform polar sampling/cells |
| Medium | Map2 | Optional border-noise implementation changes with installed dependencies | Source | Pin the algorithm; no silent output-changing fallback |
| Medium | Orogen | Canonical coarse plates do not make full terrain/climate resolution independent | Source | Freeze macro simulation resolution separately from render detail |
| Medium | Orogen | Main-thread fallback omits worker physics/super-plate/mantle stages | Source | Do not maintain a second approximate pipeline |
| Medium | Orogen | A fallback erosion call omits the neighbor-distance argument used by erosion | Source; fallback not runtime-tested | Fix or remove inconsistent fallback before relying on it |
| Medium | Orogen | Raw height, converted kilometers, and import inverse are inconsistent concepts | Source | One physical-height contract and tested import/export mapping |
| Medium | Orogen | Drainage conditioning precedes later terrain changes | Source | Validate/rebuild drainage after final terrain mutation |
| Medium | Orogen | Soil creep/detail remain active despite a seemingly disabled post stack | Source | Expose true no-op/baseline behavior and effective parameters |
| Medium | Both | Cell counts and hop limits stand in for physical measures in some algorithms | Source | Area/distance-aware formulas and resolution sensitivity tests |

There are additional source-level details worth investigating during any port, such as directional/sign conventions in force and rain-shadow helpers. They are not promoted here to confirmed bugs without targeted validation. The table focuses on findings with a clear implementation basis or direct reproduction.

## 9. Specification decisions to make next

These decisions can be made phase by phase; there is no need to choose either project wholesale.

| Decision | Recommended starting position | Still to specify |
|---|---|---|
| Authoritative resolution | One canonical macro resolution per recipe | Planet size, cell area, memory/time targets |
| Geometry | Sphere-native metric API; plane implementations later | Mesh family and deterministic construction method |
| Plate model | Local spherical rotation and density-aware boundaries | Mixed-crust plates now or later; whether super-plates are needed |
| Continental layout | Explicit continent groups and land-area budget | Strictness of targets versus emergent coastline variation |
| Terrain | Physical-height field with semantic geological features | Desired realism/stylization, relief ranges, feature counts |
| Erosion | Bounded optional shaping passes | Rainfall coupling, sediment accounting, basin preservation |
| Climate | Seasonal climatology with explicit units | Two seasons versus more samples; supported planetary variation |
| Hydrology | Watersheds, river reaches, common-level lakes, seasonal discharge | Endorheic policy, snowmelt/storage complexity |
| Ecology | Potential biome and resource suitability separate from land use | Species detail needed by gameplay rather than only flavor |
| History | Fixed ticks, stable entities and ordered events | Population model, political agents, culture/religion rules |
| Local terrain | Constrained realization of macro/regional state | Chunk scale, river constraints, city grading and persistence |
| Reproducibility | Exact authoritative state across supported clients | Numeric representation, PRNG, version/migration policy |

### Suggested first implementation experiments, after this analysis stage

1. **Deterministic cross-host proof:** a tiny sphere, one noise field, a simple graph operation, serialization, and stage hashes in Unity player and browser WebAssembly. This addresses the most consequential technical assumption before a large port.
2. **Terrain comparison harness:** implement or port one selected tectonic skeleton and one semantic feature system behind common outputs; compare on the same canonical geometry. Inspect height histograms, coast/plate overlays, and terrain profiles rather than only final colors.
3. **Hydrology proof:** synthetic slope, bowl, nested basin, coastal shelf, two-outlet tie, and endorheic examples. Require consistent water levels, deterministic receivers, correct area accounting, and stable outlets.
4. **Regeneration proof:** compare fresh output with cached reapply and edited-recipe reconstruction. Include serialization round trips and changed render resolution.
5. **Macro-to-local proof:** a small area containing a river, lake, mountain pass, and town footprint. Generate neighboring chunks in different orders and verify matching boundaries and preserved macro constraints.

These experiments are recommended sequencing, not work performed in this review. They let us choose specifications on evidence before committing to a complete generator, civilization simulator, and explorable world.

## 10. Source index

### Map2 — paths inside the supplied archive

| Ref | Files and entry points | Supports |
|---|---|---|
| M1 | `Map2/main.py`, `main()` and LOD worker orchestration | Executed order, array handoff, masks, timing, classifications |
| M2 | `Map2/config.py`; `Map2/config.toml` | Seed behavior, defaults, units, topology and stage controls |
| M3 | `Map2/src/world_geometry.py`, especially `generate_seed_points` at line 108 and neighbor vector/distance helpers | Sampling, poles, wrap and spherical metrics |
| M4 | `Map2/src/topology.py`; `Map2/src/voronoi_mesh.py` | Adjacency, edge weights, Voronoi geometry, centroids, relaxation and display noise |
| M5 | `Map2/src/plates.py`, `generate_plates` at line 55 and coastline smoothing | Plate growth, classification, velocities, glaciation resistance |
| M6 | `Map2/src/terrain_features.py`, compiler near line 437; boundary helpers near 929; subduction near 1012; collision near 1443; divergence near 1924; `apply_terrain_features` at 4175 | Geological kernels, boundary classification, feature application and shields |
| M7 | `Map2/src/baseline_topography.py`, `apply_baseline_topography` at line 193 | Coast Dijkstra, active elevation profiles, noise and unused parameters |
| M8 | `Map2/src/rivers.py`, `compute_river_fields` at line 30 | Receivers, accumulation, sinks, lakes, wetlands and runoff semantics |
| M9 | `Map2/src/climate.py`: solar 246; belts 674; currents 1440; temperature 2206; moisture solver 2354; precipitation 2437; wind 2744 | Climate dependencies, geometry, moisture defect and seasonal values |
| M10 | `Map2/src/climate_regions.py`; `Map2/src/biomes.py`, `classify_biomes` at 341 and `classify_ecoregions` at 1957; `Map2/src/cell.py` | Climate/biome/ecoregion outputs, static ecological assemblages, data model |
| M11 | `Map2/src/wgpu_viewer.py`; `Map2/src/render_modes.py`; `Map2/src/polyline_renderer.py`; mesh helpers in M4 | Presentation, inspection, rivers and visual LOD |

The ZIP also includes Python bytecode caches and a launch batch file. Generated bytecode is not an independent source of world-generation behavior and was not used in place of the Python source.

### World Orogen — pinned source links

All links below are fixed to commit `cc2662b4edd52231c4f65d8765f3ef12cd82d9b7`.

| Ref | Source | Supports |
|---|---|---|
| O1 | [planet-worker.js](https://github.com/raguilar011095/planet_heightmap_generation/blob/cc2662b4edd52231c4f65d8765f3ef12cd82d9b7/js/planet-worker.js), postprocessing line 73, generate 195, reapply 477, edit 578, deferred climate 721, import 913 | Authoritative worker path and regeneration differences |
| O2 | [generate.js](https://github.com/raguilar011095/planet_heightmap_generation/blob/cc2662b4edd52231c4f65d8765f3ef12cd82d9b7/js/generate.js) | Main-thread/worker handoff, fallback and duplicated paths |
| O3 | [rng.js](https://github.com/raguilar011095/planet_heightmap_generation/blob/cc2662b4edd52231c4f65d8765f3ef12cd82d9b7/js/rng.js), [simplex-noise.js](https://github.com/raguilar011095/planet_heightmap_generation/blob/cc2662b4edd52231c4f65d8765f3ef12cd82d9b7/js/simplex-noise.js), [planet-code.js](https://github.com/raguilar011095/planet_heightmap_generation/blob/cc2662b4edd52231c4f65d8765f3ef12cd82d9b7/js/planet-code.js) | Randomness, noise, encoded recipes and versions |
| O4 | [coarse-plates.js](https://github.com/raguilar011095/planet_heightmap_generation/blob/cc2662b4edd52231c4f65d8765f3ef12cd82d9b7/js/coarse-plates.js) | Fixed reference mesh, independent seed stream, noisy projection |
| O5 | [sphere-mesh.js](https://github.com/raguilar011095/planet_heightmap_generation/blob/cc2662b4edd52231c4f65d8765f3ef12cd82d9b7/js/sphere-mesh.js) | Fibonacci sphere, Delaunay mesh, adjacency and dual centers |
| O6 | [plates.js](https://github.com/raguilar011095/planet_heightmap_generation/blob/cc2662b4edd52231c4f65d8765f3ef12cd82d9b7/js/plates.js) | Plate seed/growth controls, cleanup, Euler motion |
| O7 | [ocean-land.js](https://github.com/raguilar011095/planet_heightmap_generation/blob/cc2662b4edd52231c4f65d8765f3ef12cd82d9b7/js/ocean-land.js) | Continent grouping, budget and trapped seas |
| O8 | [elevation.js](https://github.com/raguilar011095/planet_heightmap_generation/blob/cc2662b4edd52231c4f65d8765f3ef12cd82d9b7/js/elevation.js), velocity 127, collisions 138, stress 241, distance 335, assignment 2516 | Layered elevation, boundary mechanics, provinces, phasors, bathymetry and volcanism |
| O9 | [terrain-post.js](https://github.com/raguilar011095/planet_heightmap_generation/blob/cc2662b4edd52231c4f65d8765f3ef12cd82d9b7/js/terrain-post.js), warp 252, smooth 343, composite erosion 399, sharpen 762, detail 831, creep 922 | Erosion, flood/carving, terrain detail and final mutations |
| O10 | [plate-physics.js](https://github.com/raguilar011095/planet_heightmap_generation/blob/cc2662b4edd52231c4f65d8765f3ef12cd82d9b7/js/plate-physics.js), [super-plates.js](https://github.com/raguilar011095/planet_heightmap_generation/blob/cc2662b4edd52231c4f65d8765f3ef12cd82d9b7/js/super-plates.js) | Motion heuristics, mantle fields, super-plate construction |
| O11 | [wind.js](https://github.com/raguilar011095/planet_heightmap_generation/blob/cc2662b4edd52231c4f65d8765f3ef12cd82d9b7/js/wind.js), [climate-util.js](https://github.com/raguilar011095/planet_heightmap_generation/blob/cc2662b4edd52231c4f65d8765f3ef12cd82d9b7/js/climate-util.js) | Seasonal pressure, tangent wind and climate geometry |
| O12 | [ocean.js](https://github.com/raguilar011095/planet_heightmap_generation/blob/cc2662b4edd52231c4f65d8765f3ef12cd82d9b7/js/ocean.js) | Current rules, coast direction and warmth proxies |
| O13 | [precipitation.js](https://github.com/raguilar011095/planet_heightmap_generation/blob/cc2662b4edd52231c4f65d8765f3ef12cd82d9b7/js/precipitation.js), advection near 61, compute 197; [heuristic-precip.js](https://github.com/raguilar011095/planet_heightmap_generation/blob/cc2662b4edd52231c4f65d8765f3ef12cd82d9b7/js/heuristic-precip.js) | Transport, empirical mixture, normalization and geographic rules |
| O14 | [temperature.js](https://github.com/raguilar011095/planet_heightmap_generation/blob/cc2662b4edd52231c4f65d8765f3ef12cd82d9b7/js/temperature.js), compute 780 | Temperature, lapse rate, cloud/current effects and continentality |
| O15 | [koppen.js](https://github.com/raguilar011095/planet_heightmap_generation/blob/cc2662b4edd52231c4f65d8765f3ef12cd82d9b7/js/koppen.js), classify 70 | Two-season climate categories and conversions |
| O16 | [color-map.js](https://github.com/raguilar011095/planet_heightmap_generation/blob/cc2662b4edd52231c4f65d8765f3ef12cd82d9b7/js/color-map.js), [planet-mesh.js](https://github.com/raguilar011095/planet_heightmap_generation/blob/cc2662b4edd52231c4f65d8765f3ef12cd82d9b7/js/planet-mesh.js) | Height conversion, biome coloring, display and 16-bit exports |
| O17 | [terrain-config.js](https://github.com/raguilar011095/planet_heightmap_generation/blob/cc2662b4edd52231c4f65d8765f3ef12cd82d9b7/js/terrain-config.js), [climate-config.js](https://github.com/raguilar011095/planet_heightmap_generation/blob/cc2662b4edd52231c4f65d8765f3ef12cd82d9b7/js/climate-config.js), [detail-scale.js](https://github.com/raguilar011095/planet_heightmap_generation/blob/cc2662b4edd52231c4f65d8765f3ef12cd82d9b7/js/detail-scale.js) | Active tuned values and scale rules |
| O18 | [main.js](https://github.com/raguilar011095/planet_heightmap_generation/blob/cc2662b4edd52231c4f65d8765f3ef12cd82d9b7/js/main.js), [edit-mode.js](https://github.com/raguilar011095/planet_heightmap_generation/blob/cc2662b4edd52231c4f65d8765f3ef12cd82d9b7/js/edit-mode.js), [import-main.js](https://github.com/raguilar011095/planet_heightmap_generation/blob/cc2662b4edd52231c4f65d8765f3ef12cd82d9b7/js/import-main.js), [state.js](https://github.com/raguilar011095/planet_heightmap_generation/blob/cc2662b4edd52231c4f65d8765f3ef12cd82d9b7/js/state.js), [scene.js](https://github.com/raguilar011095/planet_heightmap_generation/blob/cc2662b4edd52231c4f65d8765f3ef12cd82d9b7/js/scene.js) | Browser application, editing, import, state and scene |
| O19 | [terrain-metrics.js](https://github.com/raguilar011095/planet_heightmap_generation/blob/cc2662b4edd52231c4f65d8765f3ef12cd82d9b7/js/terrain-metrics.js), [tuning tools](https://github.com/raguilar011095/planet_heightmap_generation/tree/cc2662b4edd52231c4f65d8765f3ef12cd82d9b7/tuning), especially [climate score.mjs](https://github.com/raguilar011095/planet_heightmap_generation/blob/cc2662b4edd52231c4f65d8765f3ef12cd82d9b7/tuning/climate/lib/score.mjs) | Metrics, empirical calibration and active score formula |

This report treats the executable implementations as the authority for what each generator does. Its proposed successor choices remain deliberately separable so that we can select and specify one phase at a time.
