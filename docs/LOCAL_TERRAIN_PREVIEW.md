# Local terrain preview — first exploration milestone

Owner authorization: 18 October 2026. This supersedes the local-playable-terrain
deferral for this limited prototype, not for vegetation, fauna, structures or NPCs.

## Boundary and use

Unity's **Explore terrain** button, in world setup and the simulation top bar,
opens a separate additive exploration scene at the selected map location. The
historical player pauses and its map camera is retained. Escape / World returns
to that same world. Existing worlds work without a rules upgrade or regeneration.

WASD moves; right-drag looks; F switches fly/walk; Shift accelerates;
Space/Ctrl flies up/down; wheel adjusts flight speed. Walking queries ground
height, including underwater ground, rather than colliding with a water plane.
This is an exploration controller, not a finished physics/character controller.

`LocalTerrain` in WorldGen.Core is the sole local terrain definition, separate
from and downstream of WorldPipeline's accepted world. Neither sampler nor
viewer mutates geography/history, appends stages or changes existing hashes.
Its explicit version is `local-terrain-preview-1`. No permanent structure sites
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
descriptors; there is no closest-three switching or per-cell constant height. Fixed source-local ridge
frames preserve geological orientation without rotating a planet-sized noise
domain at every query. Wavy ridge profiles, rounded troughs and shared global
simplex bands supply subordinate relief. Low-relief plateau interiors and
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
placeholders, not photoreal textures or vegetation.

## Bounded rendering and cost

The 200k world is not resampled or rescanned per chunk or historical event.
Descriptor/index construction is once per active World in RAM. Heights are
queried from the local candidate set and a fixed number of noise bands.
No fine terrain is saved to disk. Chunks are disposable products of that query.

The viewer retains at most a13×13 tile window (2,048m tiles), with128/32/16
segments by distance. At most two CPU mesh jobs run concurrently; one completed
tile pair uploads per frame. Departed tiles and meshes are destroyed. Existing
tiles remain visible during LOD replacement. Cancellation prevents late uploads
after exit. A floating Unity origin preserves local transform precision while
the exploration chart and planet anchor remain double precision.

Shared-edge heights are identical at shared sample coordinates. Render-only
skirts conceal mixed-resolution T-junctions; this is not a claim of finished
stitched LOD topology. Finite-difference normals can differ between resolutions.
The tangent exploration chart is a local viewer, not yet seamless planetary
circumnavigation or a persistent floating-origin structure/world system.

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

Tests cover equal-altitude landform distinction, exact overlapping patches,
concurrent/cold deterministic queries, submerged beds, finite geometry/normals,
equal/mixed-LOD shared samples, valid skirts, cancellation, bounds and complete
world-hash preservation. Performance numbers and native visual evidence are
recorded in VALIDATION.md; they are not historical simulation speed acceptance.
