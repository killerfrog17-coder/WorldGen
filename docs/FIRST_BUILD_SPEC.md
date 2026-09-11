# First world-generator build: decisions, setup, and Codex implementation prompt

Prepared 10 September 2026. This brief carries the Map2/World Orogen analysis into the first implementation milestone.

## 1. Locked scope for the first build

Build a sphere-first procedural planet generator with two user interfaces:

- a Unity runtime interface;
- a browser interface using Blazor WebAssembly.

Both interfaces must call the same deterministic C# generation core and expose the same effective settings. The first milestone generates:

1. a spherical macro mesh;
2. tectonic microplates;
3. grouped parent plates;
4. preserved contested boundary microplates;
5. tectonic boundary context and named special features;
6. a physical elevation/bathymetry field;
7. moderate hydraulic, thermal, and glacial erosion;
8. persistent topographic hydrology: watersheds, rivers, lakes, outlets, and inland seas;
9. inspectable intermediate layers and deterministic exports.

Climate, biomes/ecology, people, settlements, civilization, religion, history, explorable local terrain, and flat-world topology are later milestones. The architecture must leave extension points for them without implementing placeholder simulations that appear complete.

The user's final reference to “heightmap/ecology/hydrology” is interpreted according to the explicit scope decision immediately before it: the first build includes **heightmap and hydrology**, while ecology is deferred.

### Hydrology limitation before climate

This milestone can calculate contributing area, drainage topology, river potential, lake surfaces, outlets, and a climate-neutral discharge potential. It cannot calculate realistic seasonal discharge without seasonal precipitation, temperature, snowpack, and evapotranspiration. The result should expose an input boundary through which a later four-season climate model supplies runoff. Until then, seasonal discharge must be marked unavailable rather than filled with invented seasonal values.

## 2. Selected terrain design

### Continuous tectonics with semantic feature records

Use an Orogen-style continuous tectonic elevation system as the base. Retain explicit records for features whose identity matters beyond the final elevation field, including:

- parent and microplate boundaries;
- preserved boundary microplates;
- active collision, subduction, divergent, and transform segments;
- ghost boundaries and ancient orogens;
- hotspot and island chains;
- volcanic arcs and large igneous provinces;
- major collision belts and foreland basins;
- fragmented escape-face zones;
- tectonic inland-sea blocks.

These records allow later climate, resources, ecology, settlement, hazards, and local terrain generation to use geological meaning directly.

### Configurable plate hierarchy

Generate a configurable number of tectonic microplates, supporting at least 4–500. The default may be 500 for stress-testing, with lower preview presets.

Group the microplates into a configurable number of parent plates. The user must be able to select:

- microplate count;
- parent plate count, with a typical/default range of 15–20;
- parent size distribution, from balanced to strongly unequal;
- directional inertia strength;
- directional curvature/coherence;
- target preserved boundary-microplate fraction, default 10%;
- seed.

Parent seeds should be spatially separated. Give each parent a preferred great-circle growth direction in its local tangent frame. During expansion over the microplate adjacency graph, score candidates using adjacency, target size, compactness, directional alignment, and controlled randomness. Directional inertia should mainly shape parent plates, producing elongated and curved macro domains instead of global “horizontal” or “vertical” strips.

Every microplate receives a parent affinity. Interior microplates fuse into the parent. Microplates contested by two or more expanding parents can remain independent tectonic actors. Select the preserved set from genuinely contested interface plates, targeting the requested fraction; do not freeze whichever plates happen to be processed last.

### Continents and connectedness

Let the user target continent count and land coverage while allowing seeded variation. A continent is a geological/identity group and does not have to equal one isolated connected landmass.

Expose a high-level **continental connectedness** setting. It should influence:

- the likelihood that nominal continents share broad land contact;
- the likelihood of narrow isthmuses and land bridges;
- the likelihood of near straits and shallow shelves;
- how often collision and rifting join or separate adjacent continental groups.

This makes it possible for a four-continent recipe to produce four isolated continents, two connected continental complexes, or a mostly connected supercontinental system. Continent identity, current connected landmass ID, crust type, and ocean connectivity should remain distinct concepts.

Connectedness is a target tendency, not a guarantee that coastline noise will exactly hit a requested number. Return achieved metrics to both interfaces: identity-group count, connected landmass count, narrow connections, near straits, and land fraction.

### Boundary microplates and collision context

Preserved contested microplates should concentrate along parent boundaries and retain independent motion or locally perturbed parent motion. They must participate in collision classification.

For continental parent collisions, classify boundary neighborhoods before constructing elevation:

- **Escape face:** a defined neighborhood has a connected route to the global ocean. Preserve/reactivate microplates, allow block rotation and fragmented basins, lower selected crustal blocks, and create irregular arcs/contacts resembling Mediterranean-style escape tectonics.
- **Dead end:** compression is enclosed by continental material and lacks a nearby ocean escape route. Generate maximum-class compression, a wide high plateau, parallel ranges, and major relief resembling Himalayan-style collision.
- **Intermediate:** blend the two behaviors continuously when context is ambiguous.

Open ocean must mean connectivity to the main ocean, not merely one nearby negative-elevation cell. Expose special-collision frequency/strength and context radius at a high level, while keeping safe defaults grounded in realistic frequency.

Dead-end collision should mean maximum **uplift class**, not a perfectly flat wall at one maximum elevation. Preserve summit variation, passes, parallel ranges, and plateau relief.

### Ghost boundaries and ancient mountains

Store every fused microplate seam in a ghost-boundary collection. Preserve former boundary type, former compression/shear strength, age, activity, and the original plate pair.

Only geologically eligible seams should create mountain relief:

- former convergent seams produce broad eroded uplift;
- former divergent seams may produce old rifts or sedimentary corridors;
- former transform seams may produce weak linear structure;
- inactive seams can remain visually and topographically negligible.

Construct ghost relief as a deterministic geodesic distance field with a wide profile and strong erosion/low-frequency texture. Do not implement it as a renderer-only blur. Ancient ranges should occur often enough to affect rivers, passes, resources, and later settlement while roughly resembling their prevalence on Earth.

### Foreland basins

Major continental collision belts create a narrow-to-moderate negative elevation offset parallel to the appropriate mountain front. Basin width and depth depend on mountain load, crust/context, and collision strength. Preserve the basin as a named geological feature for future sediment, resource, river, ecology, and settlement systems.

### Ocean floor and land bridges

Ocean-floor visual detail is secondary in this milestone, but bathymetry must support future sea-level scenarios. Generate at least:

- continental shelves and slopes;
- abyssal basins;
- mid-ocean ridges;
- trenches near subduction;
- volcanic/island arcs;
- shallow sills, near straits, and submerged land-bridge candidates.

Store physical bathymetry in meters. Provide a diagnostic sea-level slider or preview layer that changes the displayed land mask without regenerating tectonics. Export a land-bridge connectivity metric or graph for selected lower sea-level offsets. It is a preview/analysis output now and will later inform animal and human migration.

### Erosion and lakes

Apply moderate erosion while retaining tectonic identity. Include bounded, deterministic hydraulic, thermal, and glacial operators. Preserve diagnostic pre-erosion and post-erosion elevation layers.

Glacial erosion may create overdeepened basins, fjords, and chains of lake depressions. The final hydrology pass decides which depressions hold water, their common water surfaces, spill points, and downstream connections.

### Inland seas

Before terrain topology cleanup fills submerged continental interiors, identify enclosed oceanic or marine-remnant blocks. Preserve a block as a tectonic inland sea when it remains below sea level, is enclosed by collision/continental crust, lacks connection to the main ocean, and exceeds configurable size/depth criteria.

Keep tectonic inland seas distinct from freshwater hydrological lakes. Later climate will calculate salinity and long-term water balance. In this milestone, label their origin and preserve negative bathymetry.

### Persistent hydrology

After final terrain mutation, calculate:

- ocean connectivity and water-body IDs;
- depression hierarchy;
- common lake water levels;
- lake volume/area curves or sufficient data to derive them;
- lake spill elevations and outlets;
- flow receiver with deterministic tie-breaking;
- contributing area using actual cell area;
- watershed IDs;
- river reaches with stable IDs;
- river order and climate-neutral discharge potential;
- endorheic basin IDs;
- glacial-lake and tectonic-inland-sea origin tags.

Do not force every basin to drain to the ocean. Distinguish intended endorheic/tectonic/glacial basins from tiny numerical pits. Erosion and ridge sharpening must finish before this authoritative drainage pass, or drainage must be revalidated afterward.

## 3. Settings visible in both interfaces

Both faces should expose the same recipe, grouped for usability. Values and exact safe bounds may be tuned during implementation, but neither face may silently omit an effective setting.

### World and output

- seed;
- generation quality/resolution preset and explicit canonical macro-cell count;
- planet radius;
- sea level preview offset;
- deterministic recipe version;
- regenerate, cancel, save recipe, load recipe, export result;
- output/debug layer selection.

### Plate hierarchy

- microplate count, maximum 500;
- parent plate count;
- parent size distribution/inequality;
- microplate irregularity;
- parent directional inertia;
- parent growth-axis curvature/coherence;
- preserved contested microplate fraction;
- boundary roughness.

### Continents

- target continent identity count;
- target land coverage;
- continent size variety;
- continental connectedness;
- land-bridge/isthmus tendency;
- near-strait/shallow-shelf tendency.

### Tectonics and relief

- overall tectonic intensity;
- parent versus microplate stress contribution;
- special collision frequency/strength;
- escape-context search scale;
- ghost-boundary/ancient-orogen frequency, strength, age/erosion tendency, and width;
- hotspot/volcanic activity;
- foreland-basin strength;
- relief/noise amount;
- ocean-floor detail strength;
- inland-sea preservation and minimum size/depth.

### Erosion and hydrology

- hydraulic erosion;
- thermal erosion;
- glacial erosion;
- ridge preservation/sharpening;
- endorheic-basin tendency or policy;
- minimum persistent lake size;
- river display/selection threshold.

The interfaces should offer sensible presets and advanced panels so 30 controls do not confront a new user at once. Presets write ordinary recipe values and must never invoke a different hidden algorithm.

### Diagnostic layers

At minimum, display:

- microplate IDs;
- parent plate IDs and parent affinity;
- preserved boundary microplates;
- continent identities and connected landmasses;
- crust/ocean connectivity;
- boundary type and stress;
- escape-face/dead-end collision context;
- ghost boundaries;
- tectonic elevation;
- pre-erosion and final elevation;
- bathymetry and lower-sea-level land bridges;
- erosion delta;
- water bodies, lakes and inland seas;
- watersheds, rivers and contributing area.

Both clients should show a rotatable 3D sphere. A 2D equirectangular inspection/export view is useful but optional for the first complete milestone.

## 4. Technical architecture

Use one repository and one authoritative generation pipeline. A practical structure is:

```text
Worldgen/
  AGENTS.md
  README.md
  docs/
    FIRST_BUILD_SPEC.md
    DETERMINISM.md
  src/
    Worldgen.Core/
    Worldgen.Serialization/
    Worldgen.Cli/
    Worldgen.Web/
    Worldgen.Web.Worker/
  unity/
    Worldgen.Unity/
  tests/
    Worldgen.Core.Tests/
    Worldgen.CrossHost.Tests/
```

`Worldgen.Core` contains plain C# with no Unity, Blazor, JavaScript, UI, scene, or renderer dependencies. Keep its callable surface compatible with the chosen Unity API profile, initially .NET Standard 2.1 compatible. It may multi-target a newer .NET version for browser/CLI builds as long as both targets run the same algorithms.

`Worldgen.Serialization` owns versioned recipe and result formats. `Worldgen.Cli` provides the fastest headless generation, metrics, export, and test path. The Blazor application executes generation in a .NET Web Worker. The Unity application executes the same core at runtime from a generation screen accessible without editor code.

The Unity and browser renderers may tessellate and shade differently. They must agree on authoritative fields and hashes. Rendering resolution cannot alter plates, elevation, water bodies, rivers, or feature IDs.

### Determinism contract

For the supported Unity and browser targets:

`seed + complete recipe + algorithm version = identical authoritative output`

Implement and version a project-owned integer PRNG. Derive independent streams by named stage and stable feature/entity ID. Do not use `System.Random`, Unity random state, JavaScript randomness, unordered set iteration, frame order, worker scheduling, or GPU computation to decide authoritative state.

Define stable tie-breaking for plate growth, heaps, flood queues, receivers, region growth, and component IDs. Order reductions explicitly. Use deterministic numerical methods and quantized/fixed-point authoritative thresholds where cross-runtime floating-point differences can change branches. Hash each major stage.

Fresh generation, same-parameter reapply, cache reconstruction, recipe load, Unity generation, CLI generation, and browser generation must reach the same stage hashes.

## 5. Step-by-step setup from a new PC/repository

### Step 1 — Install development tools

1. Install Git.
2. Install Unity Hub.
3. Through Unity Hub, install a current Unity 6 LTS editor. Record the exact editor version in the repository.
4. Install the desktop build module for the operating system on which the Unity prototype will run.
5. Install the current supported .NET SDK selected for the browser/CLI projects. Pin it in `global.json` after the project is created.
6. Install an editor such as Visual Studio, VS Code, or Rider if desired. Codex can work through the desktop app, CLI, or supported IDE extension.

No Blender, Unity AI asset generation, image generator, video generator, model-generation service, or paid asset package is needed for this milestone.

### Step 2 — Create and initialize the repository

1. Create an empty directory such as `Worldgen`.
2. Run `git init` in that directory, or create an empty GitHub repository and clone it.
3. Put this brief in `docs/FIRST_BUILD_SPEC.md`.
4. Put the earlier Map2/Orogen analysis in `docs/REFERENCE_ANALYSIS.md` if desired.
5. Do not copy either inspiration project into production source as the starting architecture. Keep local/reference copies under a clearly marked research directory if the agent needs them.
6. Create a first empty commit or let the implementation agent scaffold and commit after the initial verified build.

### Step 3 — Set up Codex

Choose one of these workflows:

- **Codex desktop app:** open the repository as a local project. This is the most convenient route when later using local environments, worktrees, diffs, and GUI/Computer Use.
- **Codex CLI:** install it using the current official installer, open a terminal in the repository root, run `codex`, and choose **Sign in with ChatGPT**.
- **Codex IDE extension:** open the repository in the IDE and start Codex there.

Official CLI installer commands at the time this brief was prepared:

Windows PowerShell:

```powershell
powershell -ExecutionPolicy ByPass -c "irm https://chatgpt.com/codex/install.ps1 | iex"
```

macOS or Linux:

```bash
curl -fsSL https://chatgpt.com/codex/install.sh | sh
```

After installation, verify `git --version`, `dotnet --info`, and `codex --version`. Open the repository directory and run `codex` there. The desktop app, CLI, and IDE extension are alternative faces over the workflow; installing every one is unnecessary.

Keep default sandbox and approval behavior initially. Grant write access to this repository and permission to run its build/test tools. There is no reason for the first build to have unrestricted access to the whole machine.

If using the desktop app, configure a local environment after the solution exists. Add setup/actions for restoring .NET dependencies, running core tests, starting the browser app, and invoking Unity batch-mode validation. Check the generated `.codex` project configuration into Git when stable.

### Step 4 — Give Codex durable project instructions

Ask Codex to create a concise root `AGENTS.md` containing:

- the repository layout;
- the single-core/no-duplicated-generator rule;
- determinism contract;
- current first-build scope and explicit deferred systems;
- build/test commands;
- Unity version and render pipeline;
- how to run browser and Unity validation;
- required stage-hash and fresh/reapply equivalence checks;
- instruction to preserve inspectable geological/hydrological fields.

Codex reads repository `AGENTS.md` automatically at the start of work. The detailed scientific specification should remain in `docs/FIRST_BUILD_SPEC.md`; `AGENTS.md` should point to it rather than duplicate all of it.

### Step 5 — Create the Unity project

Let the implementation agent create or adopt a Unity 6 project under `unity/Worldgen.Unity`. Use a basic 3D/URP project. The first sphere, ocean, lighting, camera, UI, and debug overlays can be procedural or use Unity primitives.

The project must expose generation through runtime scripts and a scene/menu, not only through Unity Editor extensions. Unity editor integration or a Unity MCP can be added after the project opens successfully. A structured Unity integration is useful for running the scene, inspecting objects, taking screenshots, and iterating, but is not required for core generation.

### Step 6 — Add Unity access for Codex when useful

Once the project exists and opens locally, choose one:

1. Let Codex run Unity through its command-line executable in batch mode for compile/test checks.
2. Install and configure a trusted Unity MCP integration after reviewing its repository, permissions, and exact Unity/Codex version support.
3. Use Codex Computer Use for visual editor/player checks when structured integration is unavailable.
4. Run Unity yourself and give Codex the Console output/screenshots when no direct editor access is available.

The uploaded video recommends a particular third-party Unity MCP but does not provide its exact repository URL in the transcript. Inspect that exact package before installing it. Blender and asset-generation MCPs from the video should be deferred.

### Step 7 — Start the implementation task

If this same Codex thread is attached to the new repository/local environment, continue here and say that the repository is ready. The current reasoning and decisions remain in the thread.

If starting a new local Codex/CLI session, place this brief in the repository and use the standalone prompt in section 6. A fresh local session should not be assumed to have this ChatGPT conversation unless you explicitly resume the same Codex session and verify the context. The repository specification is the durable source of truth.

## 6. Standalone implementation prompt for GPT-6 Astra Medium

Copy everything inside the following block into a new Codex session opened at the repository root. Attach or place this brief and the earlier comparative analysis in `docs/` first.

```text
Build the first complete milestone of this procedural world generator in the current repository. Work autonomously through implementation, testing, and review. Do not stop after scaffolding or a plan. Inspect all existing files and AGENTS.md first, preserve relevant existing work, and keep the repository buildable throughout.

Read these documents as binding context:
- docs/FIRST_BUILD_SPEC.md
- docs/REFERENCE_ANALYSIS.md, if present

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

Expose all effective settings listed in docs/FIRST_BUILD_SPEC.md, organized into World, Plates, Continents, Tectonics, Erosion, and Hydrology panels. Supply sensible presets plus advanced controls; presets must write ordinary recipe values rather than choose a hidden algorithm.

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
```

## 7. Assessment of the uploaded video advice

The transcript's recommendations fall into three groups for this project.

**Useful now:** give Codex the real repository; let it run builds and tests; provide Unity editor access when visual validation begins; work in concrete, verified milestones; steer based on running results.

**Useful later:** a reviewed Unity MCP or Codex Computer Use can shorten the loop for opening scenes, entering play mode, inspecting the hierarchy, reading the Console, and judging visuals.

**Not needed for the first build:** Blender MCP, image generation, video generation, 3D model generation, texture generators, animation/rigging tools, and paid asset services. The initial globe, meshes, overlays, lighting, UI, and terrain visualization can be generated by code.

The video's unrestricted bypass-approval command should not be the default setup. Repository-scoped write access plus permission to run known build/test commands gives Codex what this project needs while preserving review of unrelated machine access.

## 8. What to say in this existing thread

If the repository is attached to this same thread and the required tools are installed, the next prompt can simply be:

```text
The new repository is attached and ready. Use Worldgen_First_Build_Setup_and_Prompt.md as the binding specification. Build the complete first milestone now, including the shared deterministic C# core, headless CLI, Blazor WebAssembly face, Unity runtime face, tests, documentation, and every available validation. Persist until it is functional and reviewable. Do not implement the deferred climate/ecology/civilization/local-terrain systems.
```

If a new Codex session is used, use the full standalone prompt in section 6.
