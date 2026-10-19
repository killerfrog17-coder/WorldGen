# Local terrain preview — first exploration milestone

Owner authorization: 18 October 2026. This supersedes the local-playable-terrain
deferral for this limited prototype. Natural-cover placeholders were subsequently
authorized; see `LOCAL_SCATTER_PREVIEW.md`. Fauna, structures and NPCs remain deferred.

## Boundary and use

The game-first shell now uses **Enter world / Resume game** in the simulation
top bar and pause menu. It opens a separate additive scene at the selected map
location or saved player position. Gameplay advances the existing historical
calendar at its saved game-time rate; Escape pauses and can reopen the simulation.
See `GAME_SHELL.md` for the save envelope, horizontal scale and clock ownership.
Existing worlds work without a historical rules upgrade or regeneration.

Exploration starts walking, with captured mouse-look (no drag), WASD, Space to
jump and Shift to run. F switches fly/walk; Space/Ctrl flies up/down and wheel
adjusts flight speed. The baseline avatar uses1.65m eye height,1.5m/s walking,
5m/s running, a0.45m jump and9.81m/s² gravity. These are chosen adult/gameplay
defaults, not a claim that every person's dimensions or abilities are identical.
Walking queries ground height, including underwater ground, rather than colliding
with a water plane. Uphill walking has a45° continuous-grade limit: a per-frame
step allowance cannot be reused to climb a cliff. Ordinary uphill/downhill motion
follows surface distance at steady eye height; real drops initiate a fall. Small
bounded substeps and analytic ballistic integration preserve speed/jump behaviour
across frame rates. This remains a height-field controller: stair/prop/capsule
collision, steep-slope sliding, swimming, stamina and full character physics
remain deferred. No silhouette, body mesh, extra HUD or camera bob was added.

The correction distinguishes walking from running and step height from slope,
consistent with the [Unity controller documentation](https://docs.unity.com/en-us/engine/6000.0/manual/physics-section/physics-overview/character-control-section/class-character-controller).
Walking-scale sanity reference: [experimental self-selected walking speed study](https://pmc.ncbi.nlm.nih.gov/articles/PMC7268460/).
No reference controller implementation was copied.

There is no normal exploration HUD. F3 opens plain diagnostics without releasing
the cursor or interrupting movement. M toggles the interactive world map
independently of F3 and releases the cursor. Wheel zooms about the cursor, dragging pans
with longitude wrap, and clicking teleports to the exact planetary position, not
the nearest cell center. Map imagery updates asynchronously; the cached image
transforms while new pixels are prepared. Deep zoom samples the same local terrain
function. Logical zoom extends below terrain numerical precision; this does not
invent measured detail at that scale. Escape first dismisses the map, then opens
the pause menu. Flight speed and view distance are in pause → Game settings.

`LocalTerrain` in WorldGen.Core is the sole local terrain definition, separate
from and downstream of WorldPipeline's accepted world. Neither sampler nor
viewer mutates geography/history, appends stages or changes existing hashes.
Its explicit version is `local-terrain-preview-3`: scale1 retains preview2's
heights, while scaled worlds preserve physical local-noise wavelengths rather
than compressing these with the macro map. No permanent structure sites
are promised across future local terrain revisions. A future persisted local
recipe must pin this version before city footprints / edits become authoritative.

The core/preparation adapters remain engine independent. The Unity component
only schedules generation, uploads meshes, handles input and manages objects.
No generator implementation is duplicated in Unity or JavaScript. The browser
does not yet include this first-person explorer.

## Terrain definition

One-time derived descriptors use actual world elevation/neighbour relief,
fold/plateau/basin influences, source belt axes and synthetic ages, sediment /
flood proxies, biome and the existing glaciation flag. Absolute altitude alone
does not create mountains. Earth has no synthetic belts; measured coarse relief
provides its ruggedness fallback. The existing habitat ReliefM is a neighbour
height range, not a measured subcell hillslope or arbitrary-resolution DEM.

An immutable spatial index selects supporting cells once per small patch.
Bounded cubic triangular patches retain the source height anchors and shared
edge curves without height overshoot. Separately, compact C2 kernels blend
descriptors; there is no closest-three switching or per-cell constant height.
Where source geological belts exist, their cells now share a fabric origin,
axis, phase and spacing. Earth falls back to source-local coarse-relief frames.
Broad irregular mountain masses, subordinate directional soft-absolute crests,
and smaller summit/spur bands replace the v1 repeated sinusoidal domes. An
intermediate experiment with dominant sharp crests was rejected after native
screens showed curved kilometre-high walls. The100m band is reduced substantially, rather
than covering every hillslope in artificial lumps. Mountain amplitude and4–13km
ridge spacing support larger local valley/crest relief. These are cheap analytic
patterns, not a reconstructed drainage tree. Low-relief plateau interiors and
sediment-rich plains suppress mountain/hill amplitude. This is a phenomenological
terrain surface, NOT a hydraulic erosion or tectonic-history simulation.

Spatial wavelengths and amplitudes use metres. Noise is addressed in global
planetary Cartesian coordinates with versioned project-owned seeds; chunks do
not reseed. Patch candidates are ordered. Elevation results are quantized to
millimetres; this numerical precision does not imply measured millimetre detail.
The smallest geometric band is approximately 100m; near meshes sample at16m.
Shader grain is only a placeholder surface, not additional collision relief.

Current ocean beds continue below a separate sea-level surface. Ocean influence
limits that water to ocean-connected source areas. Inland lakes, river beds,
fractional islands, wetlands and resolved local outlets are NOT integrated yet;
do not use this preview for water-sensitive city/road placement. The old shore
sampler remains a distinct global-map display prototype, not a second playable
terrain implementation.

Glacial influence is exposed but specialist glacial forms are deferred; so are
rock strata, talus simulation, dunes, oases, fjords, caves and overhangs. Biome
surface colours blend continuously; slope adds exposed rock. These are
placeholders, not photoreal textures. The separate local-scatter milestone adds
replaceable natural-cover meshes and layered PBR materials without changing heights.

## Bounded rendering and cost

The 200k world is not resampled or rescanned per chunk or historical event.
Descriptor/index construction is once per active World in RAM. Heights are
queried from the local candidate set and a fixed number of noise bands.
No fine terrain is saved to disk. Chunks are disposable products of that query.

The viewer uses a view/altitude-dependent quadtree with2,048–131,072m tiles and
128/64/32/16/8 segments. Default requested range is200km (debug adjustable30–250km);
planet curvature and terrain occlusion still limit what is actually visible.
Ground-level planning uses281 leaves at the tested origin, falling to140 at60km
altitude. At most two CPU mesh jobs run concurrently; one completed tile pair
uploads per frame. Departed tiles and meshes are destroyed. Old LOD coverage stays
visible while replacement leaves load. Teleports invalidate old-frame uploads;
exit cancels work. A floating Unity origin preserves local transform precision
while the exploration chart and planet anchor remain double precision. Long travel
reanchors the tangent chart, preserving geographic heading and height above terrain.

Shared-coordinate unmodified height samples are identical. Render-only edge
collapse makes a finer tile boundary follow the adjacent coarse edge segment;
skirt geometry remains a fallback. This changes disposable render vertices,
never height queries, walking ground or saved geography. Coarser sampling can
still lose small relief and finite-difference shading can differ between levels.
The tangent exploration chart is still a prototype, not a validated seamless
planetary circumnavigation or persistent structure/world system.

## Research basis (independent implementation)

- Génevaux et al., [Terrain Generation Using Procedural Models Based on
  Hydrology](https://cs.purdue.edu/homes/bbenes/papers/Genevaux13ToG.pdf):
  connected terrain/river feature primitives rather than undirected fractals.
- Grenier et al., [Real-time Terrain Enhancement with Controlled Procedural
  Patterns](https://onlinelibrary.wiley.com/doi/10.1111/cgf.14992): oriented,
  nested terrain details; candidate for a later structured-ravine improvement,
  not an implementation claim for this first wavy-ridge prototype.
- Cordonnier et al., [Forming Terrains by Glacial Erosion](https://www.cs.purdue.edu/cgvlab/www/resources/papers/Cordonnier-ToG-2023-forming.pdf):
  reason to preserve separate glacial provenance for later landforms.
- [FastNoiseLite documentation](https://github.com/Auburn/FastNoiseLite/wiki/Documentation):
  useful noise/warp alternatives. No dependency was added; existing project
  simplex implementation is used. No GPL reference code was copied/translated.

## Repeatable checks

```
dotnet run --project tests/WorldGen.Core.Tests -c Release -- --local-terrain-only
dotnet run --project tests/WorldGen.Core.Tests -c Release -- --local-terrain-world <snapshot.world>
```

Native review entry point (normal graphics, not `-nographics`):

```
WorldGen.exe --worldgen-local-terrain-check <snapshot.world> --worldgen-output <evidence-directory>
```

Add `--worldgen-local-earth-sites` for fixed Himalaya(27.98,86.92), Andes
(-32.65,-70.01), Point Reyes(38.07,-122.96) and Tibetan plateau(32,88) review sites.
These are regional approximations: the bundled10-arcminute relief and coarse
world samples cannot reproduce actual individual peaks, narrow valleys or beaches.
The coastal review searches for a nearby resolved sea-level crossing; the nominal
real-world beach can be under water at this resolution. Its actual review
coordinate is recorded, rather than passing an all-ocean image as a beach test.

Real-image comparisons used NASA's [Himalayan topography](https://science.nasa.gov/earth/earth-observatory/himalayan-topography-39160/)
and [Cerro Aconcagua](https://science.nasa.gov/earth/earth-observatory/cerro-aconcagua-3542/),
plus NPS's [McClures Beach](https://home.nps.gov/places/point-reyes-mcclures-beach.htm).
These motivate connected sharper crests, broad valleys and a resolved coastal
transition; they are not calibration data or a claim of matching actual local DEMs.

Tests cover equal-altitude landform distinction, exact overlapping patches,
concurrent/cold deterministic queries, submerged beds, finite geometry/normals,
equal/mixed-LOD shared samples, valid skirts, cancellation, bounds and complete
world-hash preservation. Performance numbers and native visual evidence are
recorded in VALIDATION.md; they are not historical simulation speed acceptance.
