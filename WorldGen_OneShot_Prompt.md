Build the first complete milestone of this procedural world generator in the current repository. Work autonomously through implementation, testing, and review. Do not stop after scaffolding or a plan. Inspect all existing files and AGENTS.md first, preserve relevant existing work, and keep the repository buildable throughout.

The current directory is the WorldGen repository root. An existing Unity project should be at unity/WorldGen.Unity. Detect and preserve it if present. Create the shared solution, browser, CLI, tests, documentation, and supporting projects as siblings under this repository. A suitable layout is:

- WorldGen.sln
- AGENTS.md
- README.md
- docs/
- src/WorldGen.Core/
- src/WorldGen.Serialization/
- src/WorldGen.Cli/
- src/WorldGen.Web/
- src/WorldGen.Web.Worker/
- tests/WorldGen.Core.Tests/
- unity/WorldGen.Unity/

Adjust the layout only when the chosen Unity/.NET integration requires it. Do not place the browser app inside Unity Assets and do not create a second implementation of the generator inside Unity.

Read these documents as binding context if they are present:
- docs/FIRST_BUILD_SPEC.md
- docs/REFERENCE_ANALYSIS.md

WORLD OROGEN REFERENCE BASELINE

The overall terrain generator should be recognizably based on the architecture, generation stages, controls, diagnostic philosophy, and terrain behavior of World Orogen, with the changes in this prompt deliberately replacing or extending it. Do not create a generic noise globe that merely uses similar terminology.

Authoritative reference repository:
https://github.com/raguilar011095/planet_heightmap_generation

Use the audited reference commit unless I explicitly request a newer version:
cc2662b4edd52231c4f65d8765f3ef12cd82d9b7

Before designing the C# implementation, obtain and inspect that source if network access permits. Focus on the executed code in:
- js/sphere-mesh.js
- js/coarse-plates.js
- js/plates.js
- js/ocean-land.js
- js/super-plates.js
- js/plate-physics.js
- js/elevation.js
- js/terrain-post.js
- js/terrain-config.js
- js/terrain-metrics.js
- js/planet-worker.js
- js/planet-code.js
- js/planet-mesh.js
- js/main.js

Treat executed code as more authoritative than README prose or comments. Trace the actual stage order, inputs, retained fields, regeneration paths, physical-height conversions, and debug layers. Create docs/OROGEN_REFERENCE_MAP.md that maps each relevant Orogen stage to one of: preserved concept, deliberately modified concept, deferred concept, or rejected concept, and names the corresponding new C# component.

Preserve the following broad Orogen character:
- sphere-native irregular macro mesh;
- fixed canonical coarse tectonic construction independent of render detail;
- separated plate seeds and controlled graph growth;
- continent grouping and approximate land target;
- local spherical plate motion using Euler-pole-style rotation;
- density-aware subduction and locally evaluated boundary motion;
- parent/super-scale and fine-scale tectonic structure;
- stress and boundary distance fields;
- continuous terrain archetype/province blending;
- broad tectonic skeleton followed by directional internal mountain structure;
- arcs, hotspots, volcanic chains, large igneous features, rifts, trenches, shelves, slopes, abyssal floor, and coast detail;
- terrain warp/detail followed by hydraulic, thermal, and glacial shaping;
- inspectable intermediate layers, metrics, progress, seeded recipes, globe/map inspection, and heightmap export.

Apply the new microplate hierarchy, ghost boundaries, connected continents, collision-context rules, inland-sea preservation, and persistent hydrology described below. Do not preserve known Orogen orchestration inconsistencies: fresh generation, reapply, edits, cache reconstruction, browser, Unity, and CLI must call one pipeline.

World Orogen is GPL-3.0 licensed. Use it as a behavioral and architectural reference and implement the new C# system independently. Do not copy or mechanically translate its source into this repository unless the repository owner explicitly chooses GPL-3.0 compatibility. Record third-party dependencies and licenses.

MICROPLATE DOMAIN MODEL

In this project, a microplate is not a terrain cell and not merely a decorative crack. It is the smallest generated tectonic region. It is created by seeded Voronoi/Delaunay-associated graph growth over the canonical spherical cells before parent clustering.

Each microplate must be a contiguous set of macro surface cells with a stable ID and persistent tectonic properties sufficient for later stages, including its seed/representative position, area, centroid, cell membership, neighboring microplate IDs, shared boundary segments/lengths, crust/oceanic character, density, and independent or inherited rotational motion. Exact storage types are an implementation decision.

A parent plate is a macro tectonic domain made from adjacent microplates. It is not another surface tessellation. Each parent has a stable ID, parent seed microplate, target size/share, growth direction and curvature/coherence, aggregated crust character, and broad motion/stress contribution. Parent growth operates on the weighted microplate adjacency graph, not directly on arbitrary individual surface cells.

Every microplate receives ParentAffinityId. After clustering:
- fused interior microplates retain their IDs and provenance but inherit/blend the parent tectonic motion for active-boundary purposes; seams between fused members become inactive GhostBoundaries;
- preserved contested microplates keep ParentAffinityId for grouping/visualization but remain independent active tectonic actors with their own or perturbed motion and active boundaries;
- active tectonic identity and parent affinity must therefore be separate concepts;
- no microplate may be unclassified, disconnected from its recorded cells, or silently deleted.

A candidate is contested when it lies on a frontier reached by at least two different growing parents, or when assigning it would place it along a shared parent interface according to a deterministic look-ahead rule. Select the requested preserved fraction from eligible contested candidates using seeded scores based on degree of contest, boundary length, local kinematic contrast, and stable ID. The default target is 10 percent of all microplates, but preserve fewer with a reported warning/metric if too few genuinely contested candidates exist. Do not preserve random interior microplates merely to hit the quota.

Directional inertia applies primarily to parent clustering. Score a parent-to-candidate expansion using alignment with the parent's local transported great-circle growth direction, continuity with the previous growth direction, requested parent area, boundary compactness, and seeded variation. Transport directions between tangent frames correctly so axes curve across the sphere. Avoid global latitude/longitude vertical-horizontal bias.

The parent-size setting should control a deterministic target-weight distribution ranging from nearly equal parents through varied and dominant-parent worlds. Growth must attempt these target shares while maintaining connectivity. Report requested versus achieved parent areas.

The canonical macro mesh must provide enough cells for the requested microplate count. Establish and document a minimum useful cells-per-microplate threshold. Automatically raise the required resolution, clamp the plate count, or block generation with a clear validation message when a recipe would produce degenerate one-cell or tiny microplates. A 500-microplate setting is a real tectonic stress case, not permission to create 500 isolated labels.

Goal

Create a deterministic, sphere-first C# planet generator with two functional faces: a Unity runtime application and a Blazor WebAssembly browser application. Both must use one authoritative C# core and expose the same generation settings. Both must display the generated sphere and inspectable generation layers.

First-build scope

Implement only:
- spherical macro geometry;
- configurable tectonic microplates, supporting up to 500;
- configurable 15–20-ish parent plates, with user-selected count and size distribution;
- curved directional parent growth;
- contested boundary microplates, default target 10%;
- continuous Orogen-inspired tectonic terrain with explicit semantic feature records;
- continents with target identity count, land coverage, connectedness, land-bridge/isthmus and near-strait tendencies;
- active boundary classification and local stress;
- escape-face, dead-end, and intermediate continental-collision contexts;
- fused ghost boundaries and ancient eroded ranges;
- hotspot/island chains, volcanic arcs, foreland basins, shelves, slopes, ridges, trenches, and enough bathymetry to analyze land bridges at lower sea levels;
- protected tectonic inland seas;
- moderate deterministic hydraulic, thermal, and glacial erosion;
- final persistent hydrology: ocean connectivity, depression hierarchy, common-level lakes, outlets, endorheic basins, watersheds, river reaches, contributing area, and climate-neutral discharge potential;
- versioned recipes/results, exports, progress/cancellation, stage metrics/hashes, and diagnostic layers.

Defer climate, seasonal discharge, biomes/ecology, people, settlements, civilization, religion, historical simulation, playable local terrain, and flat-world topology. Define clean extension boundaries for later four-season climate and runoff. Do not label climate-neutral flow accumulation as seasonal discharge.

Architecture requirements

- Use one repository and one authoritative pipeline.
- Keep core generation free of Unity, Blazor, JavaScript, rendering, scene, editor, and operating-system dependencies.
- Keep the core callable surface compatible with Unity's selected .NET API level, initially .NET Standard 2.1 compatible; multi-target when useful.
- Include a fast headless CLI host.
- Run browser generation in a .NET Web Worker so the UI remains responsive.
- The Unity generator must run at application runtime from a generation menu/scene, not only from editor tooling.
- Browser and Unity rendering may differ visually, but rendering resolution may not affect authoritative world state.
- Do not maintain separate simplified Unity, browser, fallback, reapply, or edit algorithms.

Determinism requirements

The supported Unity and browser targets must satisfy:
seed + complete recipe + algorithm version = identical authoritative output.

Implement a versioned project-owned integer PRNG and named independent random streams. Do not use System.Random, Unity random state, JavaScript randomness, frame order, worker scheduling, unordered collection iteration, or GPU computation for authoritative decisions. Define stable tie-breaking and ordered reductions. Use deterministic numerical representations or explicit quantization/fixed-point rules wherever floating-point differences could change topology or branching. Produce a stable hash after every major stage.

Fresh generation, same-parameter reapply, saved-recipe reconstruction, cache reconstruction, CLI, browser, and Unity must use the same pipeline and produce matching hashes for authoritative stages.

Algorithm requirements

1. Generate the canonical spherical surface and real cell areas/metric adjacency.
2. Generate the requested microplate count on a canonical tectonic mesh.
3. Choose separated parent seeds and expand them over the microplate graph. Bias growth along each parent's smoothly curved great-circle direction and toward its requested size. Let users control count, size inequality, inertia, curvature, and irregularity.
4. Give all microplates a parent affinity. Fuse ordinary interior plates. Preserve a requested fraction of genuinely contested plates at parent interfaces as independently identifiable tectonic microplates.
5. Save fused internal seams as ghost-boundary records. Apply broad eroded relief only according to former boundary type, strength, and age.
6. Target continent identity count and land coverage with variation. Model continent identity separately from connected landmass. Use connectedness/isthmus/near-strait controls so nominal continents may form connected complexes or remain separate. Report achieved connectivity.
7. Compute local spherical plate motion and boundary normal/tangential motion. Retain collision, divergence, transform, subduction side, stress, and feature identities as inspectable outputs.
8. For continental collision, classify ocean escape by connectivity to the main ocean within a configured scale. Escape faces create fragmented microplate/block/basin structure; dead ends create maximum-class plateau/range uplift; ambiguous regions blend them.
9. Generate continuous tectonic elevation in physical meters with detailed mountain structure. Maximum-class ranges must still contain summits, passes, parallel ranges, and plateau variation.
10. Generate parallel negative foreland basins on the correct side of massive collisions and keep them as named features.
11. Generate adequate physical bathymetry and stable submerged land-bridge candidates. Support sea-level preview without regenerating tectonics.
12. Detect enclosed oceanic blocks before any topology fill. Preserve qualifying collision-enclosed negative-elevation blocks as tectonic inland seas.
13. Apply moderate hydraulic, thermal, and glacial erosion while retaining pre-erosion terrain and geological identities. Allow glacial overdeepening, fjords, and lake basins.
14. After all elevation mutation, run the authoritative hydrology pass. Use actual cell area, deterministic receivers/ties, depression hierarchy, common lake surfaces, spill/outlet data, persistent river reach and watershed IDs, and explicit endorheic basins. Do not force intended inland basins to drain to the ocean.

Both user interfaces

Expose the same effective settings, organized into World, Plates, Continents, Tectonics, Erosion, and Hydrology panels. Supply sensible presets plus advanced controls; presets must write ordinary recipe values rather than choose a hidden algorithm.

World and output settings:
- seed;
- generation quality/resolution preset;
- explicit canonical macro-cell count;
- planet radius;
- sea-level preview offset;
- recipe/algorithm version;
- regenerate, cancel, save recipe, load recipe, and export result actions;
- diagnostic layer selection.

Plate settings:
- microplate count from 4 through 500;
- parent plate count;
- parent size distribution/inequality;
- microplate irregularity;
- parent directional inertia;
- parent growth-axis curvature/coherence;
- preserved contested-microplate fraction, default 10 percent;
- boundary roughness.

Continent settings:
- target continent identity count;
- target land coverage;
- continent size variation;
- continental connectedness;
- isthmus/land-bridge tendency;
- near-strait/shallow-shelf tendency.

Tectonic and relief settings:
- overall tectonic intensity;
- parent-versus-microplate stress contribution;
- special collision frequency/strength;
- ocean-escape context search scale;
- ghost-boundary/ancient-orogen frequency, strength, age/erosion tendency, and width;
- hotspot/volcanic activity;
- foreland-basin strength;
- relief/noise amount;
- ocean-floor detail strength;
- inland-sea preservation plus minimum size/depth.

Erosion and hydrology settings:
- hydraulic erosion;
- thermal erosion;
- glacial erosion;
- ridge preservation/sharpening;
- endorheic-basin policy/tendency;
- minimum persistent lake size;
- river display/selection threshold.

Show a rotatable 3D globe and selectable diagnostic overlays for microplates, parent plates, contested microplates, continent identities, connected landmasses, boundary type/stress, collision context, ghost boundaries, tectonic/pre-erosion/final elevation, bathymetry and lower-sea-level land bridges, erosion delta, water bodies, lakes/inland seas, watersheds, rivers, and contributing area. Support seeded regenerate, cancellation, progress, recipe save/load, and result export.

Unity may render procedurally with primitives, meshes, shaders/materials, and runtime UI. Do not add AI asset-generation, Blender, paid assets, or unrelated gameplay packages. The browser may use a small established WebGL rendering dependency if necessary, pinned and documented.

Verification and done criteria

- Create meaningful unit/property tests for topology, deterministic parent growth, connected components, inland-sea preservation, lake levels/outlets, hydrology tie-breaking, and serialization.
- Add a small golden-seed corpus covering ordinary, highly connected, isolated-continent, escape-face, dead-end, inland-sea, glacial-basin, and 500-microplate cases.
- Verify repeat generation in the same host.
- Verify fresh versus same-parameter reapply and serialized-recipe reconstruction.
- Add a cross-host hash procedure and run every target available in this environment. If Unity or browser execution is unavailable, finish their code/configuration, run all available builds, and document the exact command the user must run plus expected hashes. Do not claim an unavailable target was tested.
- Build and run the browser face, inspect it visually if browser tools are available, and correct functional/layout problems.
- Compile and run Unity batch tests/player checks if Unity is available. If not, make the project import-ready and clearly report the unverified editor/player step.
- Document setup, architecture, algorithms, effective settings, known limitations, and commands in README/docs.
- Review the final diff for duplicated generation paths, nondeterministic collections, unversioned parameters, stale settings, and accidental implementation of deferred systems.

Use incremental milestones internally: deterministic core and CLI; plate hierarchy; tectonic elevation/features; erosion/hydrology; serialization/hashes; browser face; Unity face; cross-host validation. Continue until the requested milestone is functional and reviewable. Report what was implemented, exact validation performed, any target that could not be executed, and the most important remaining risks.
