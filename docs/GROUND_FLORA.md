# Ground layer / ground-flora-2

## Continuous field revision /21October2026

Owner's blade-prototype review found isolated tufts and large bare gaps even
with acceptable performance. The density slider increased leaves within each
tuft but could not restore missing tufts. `player-grass-continuous-field`
changes spatial placement, not only that multiplier. As with the preceding
prototype, the owner handles testing: compilation/packaging only on agent side.

The0.60m grid is now an abutting field partition. Every vegetated square is
retained (only near-zero cover<.002 is rejected); the old whole-clump Bernoulli
cover rejection and extra moss-patch rejection are removed. Low-discrepancy
leaf coordinates fill the entire square rather than a disk around a jittered
centre. Density/LOD prefixes remain spread across that square. These squares
are GPU batching units, not intended visible botanical clumps.

Each root record carries the continuous nine-form mixture; form choice and
height variation occur independently per leaf. Entire squares no longer turn
into one short forb/moss tuft and leave a hole in an otherwise grassy field.
The shared per-tuft random height multiplier is retired; broad ecological
stature remains. Blade occupancy scales with cover/.45, capped at1: productive
sites fill continuously while arid/rocky margins stay thinner. These are authored
visual priors, not a new ecological population simulation. Grass width grows
modestly from.008 to.010m half-width before phenotype/LOD scaling.

Default High now has up to64/21/5 leaves per field square (formerly24/8/2),
still3/2/1 segments. Low/Medium/Ultra near counts16/32/96; density0.25–2x,
120m default range, wind and complete local disable remain live. New GPU root
stride96bytes plus12bytes/index capacity=108bytes/field slot, versus76 before,
plus the existing terrain grid/counters/arguments. More actual occupied squares
and higher blade counts can increase GPU cost; prior performance acceptance
does not apply automatically. No frame-time or seamless-density claim made.
Source world/history/ecology, terrain heights and water are unmodified.
The old individual-blade description below is historical where superseded.

## Individual-blade prototype /21October2026

Owner rejected the dense atlas banks as conspicuously flat crossed cards and
requested a quick geometric-blade prototype, explicitly WITHOUT agent tests.
`player-grass-blades-prototype` replaces the ground renderer with individually
rooted curved triangle leaves. The current ecology/tallgrass fields, terrain
mean cover colour and all world/history/height/water definitions remain literal.
The atlas/importer remain in source for rollback but are not sampled by this
prototype. Historical card implementation/evidence below is not its acceptance.

This adapts curved-blade, fractional retirement and bounded width-compensation
principles from [AMD's primary reference](https://gpuopen.com/learn/mesh_shaders/mesh_shaders-procedural_grass_rendering/).
It uses existing Unity compute + indirect vertex drawing, NOT AMD's mesh-shader
pipeline or a complete reproduction of Ghost of Tsushima. There is no new
hardware mesh-shader/DX12 requirement or per-leaf GameObject/physics simulation.

Each existing0.60m lattice root seeds a small patch of independently scattered
blade roots (0.34m radius), random orientations, width, lean, height and colour.
Quadratic curved leaf strips move with root-anchored shader wind. Other growth
forms use provisional broader geometric leaves/low mats; finished fern pinnae,
seed heads and botanical materials are not part of this quick prototype.
Slope gradients now use the actual root triangle on either side of the grid
diagonal, improving local grounding without modifying the terrain.

A GPU pass reads the root counter and compacts three visibility/distance lists
before drawing. It rejects off-frustum/out-of-range roots so the whole160m
area does not submit maximum-detail blade vertices. No normal-runtime readback.
Existing0.1x full-detail coverage limits still apply. Three bands overlap and
use existing opaque complementary dither fades, with fractional last-blade
width and modest distance compensation. No imperceptible LOD claim is made.
Grass receives light/shadows but casts no individual shadows; root darkening
and simple transmission approximate leafy shading.

Pause > Game settings > Grass debug exposes session-only density0.25–2x,
range40–160m, four detail levels, wind, enable/disable and reset. Defaults are
High,1x,120m,wind on. High uses up to24/8/2 blades per root,3/2/1 segments
(432/96/12 triangle-list vertices), before fractional/form reduction; Ultra
uses32 near blades/four segments. Density scales blade counts, not ecological
cover or the terrain map. Near geometry is limited to the near distance band;
the bands scale with selected range. GPU index capacity adds12bytes per root
slot (now76bytes/root slot plus cover grid and small counters/arguments).

Disabling releases local grass buffers and removes its draw/cull work; distant
terrain colours remain so the same-view comparison does not rebuild terrain.
Re-enable prepares a patch per frame and fades in over2s. Give it a few seconds
before comparing, ideally with the same camera, cap/VSync and settled loading.
Controls do not change terrain revision, saves, ecology or historical rules.
No native visual, GPU-time/FPS, automated correctness or gameplay test was run
for this owner-requested prototype; compilation/packaging is not such a test.

This pass replaces the sparse placeholder grass, not trees, shrubs, rocks,
terrain geometry or water. Ground cover is a separate layer over the accepted
mineral/organic substrate. It is not a new biome classifier.

## Ecology research and representation

| Growth form | First-pass niche and appearance |
| --- | --- |
| Cool-season grass | Cooler open mesic ground; slender curved blades |
| Warm-season grass | Warmer open ground; taller blades and warmer palette |
| Dry bunchgrass | Seasonally dry ground; separated, straw-toned tufts |
| Sedge/rush | Wet margins and saturated soils; upright narrow leaves |
| Low forb | Low broadleaf herbs and rosettes mixed into open/woodland cover |
| Fern | Moist, shaded understory; broader spreading fronds |
| Moss/liverwort mat | Moist sheltered ground; low hummocks, not tall blades |
| Lichen/biocrust | Sparse cold/exposed or dry ground; mostly substrate cover |
| Low succulent | Sparse warm dry ground; fleshy low rosettes |

These are functional growth forms, not a claim that lichens are plants, all
sedges require wetlands, or all members of a group share one ecological niche.
The owner found the first procedural blade/leaf fans too sparse. The second
pass uses dense transparent-cutout banks of grass, herbs and resolved fern
fronds on crossed curved cards. Tiny mats remain mostly substrate cover;
succulents still use provisional broadleaf art rather than finished rosettes.
Flowering phenology, cactus spines and individual moss shoots remain future art.

Research used to choose the distinctions:

- [USDA/Native cool-season grasses](https://www.nrs.fs.usda.gov/pubs/jrnl/2010/nrs_2010_navarrete-tindall_002.pdf):
  cool- and warm-season grasses coexist, with different seasonal growth patterns.
  The current generator uses thermal-niche mixtures, not a dynamic phenology model.
- [USFS/Geyer's sedge](https://research.fs.usda.gov/feis/species-reviews/cargey):
  sedges also occur in dry forest understories. The wet sedge/rush group is a
  first specialization, not an exhaustive sedge taxonomy.
- [USFS/feather moss](https://research.fs.usda.gov/feis/species-reviews/hylspl):
  sheltered, moist forest floors differ from meadow grass. Nonvascular cover
  does not use the same root-depth gate as vascular plants.
- [NPS/biological soil crusts](https://www.nps.gov/grca/learn/nature/bio-soil.htm):
  dryland crust communities include lichens, mosses, algae/cyanobacteria and
  some liverworts. Crust appearance is represented without pretending all
  components are blade-shaped vascular species or simulating nutrient fixation.

Numbers are authored ecological priors, not fitted species-distribution models.
Temperature, aridity, inferred canopy, wetness/flooding, salt, soil depth,
fertility and stones affect cover. Joint cover is capped, rather than filling
every square metre with nine independently dense layers. Actual local slope,
water and ice exclude unsuitable roots. Sparse desert ground stays sparse.

Stature now includes site productivity: warm, moist, deep, fertile open sites
can support waist/head-high grass, while dry or shallow soils remain short.
Wetland sedges can also be tall. Broad continuous stand variation supplements
seeded regional stature, without abrupt biome edges. Individual rendered banks
are capped at3.2m; this is potential mature vegetation, not seasonal growth.
[USFS big bluestem](https://research.fs.usda.gov/feis/species-reviews/andger)
and [NPS tallgrass prairie](https://www.nps.gov/tapr/faqs.htm) inform this
distinction: tallgrass potential varies with site, and tall flowering culms
must not be confused with an everywhere-equally-tall leafy canopy.

## Generated regional flora

`GroundFlora.For(world)` supplies a compact, reconstructible ecological overlay.
Province-level seeded founder lineages branch into regional candidates with
related names, leaf width, stature and color. Different worlds differ; related
ecoregions share an ancestor and have bounded variation. Local suitability
determines which forms actually occur: nine catalog candidates do not imply
nine resident species in every region. Identifiers are region plus growth form;
fabricated display names are not unique taxonomic identifiers.

This is static procedural ancestry/biogeography, **not** evolving genetics,
historical speciation, population abundance or competition. Accepted saved
ecoregions/provinces are reused; their borders and existing food webs/taxa are
not rewritten. The overlay is derived from seed and saved ecology, so old saves
work immediately without an ecology/history upgrade. It is not serialized as
a new global fine map and does not add recurring all-cell simulation work.

Source profiles blend using the terrain's compact continuous support kernel.
A physical-space noise field adds local patch variation, without per-cell
random reseeding or hard biome-color borders. The same mixture tints every
terrain LOD. This is present natural potential vegetation, not seasonal
senescence, grazing, fire succession, farming/clearing or observed Earth flora.
Future tree/grass work can consume this layer without replacing substrate soils.

## Rendering research and implementation

- [AMD GPUOpen procedural grass](https://gpuopen.com/learn/mesh_shaders/mesh_shaders-procedural_grass_rendering/):
  curved blades, patch generation, fewer/wider distant blades, transition
  masking and shader wind. We adapt these principles; this is **not** AMD's
  mesh-shader implementation and requires no DX12 mesh-shader hardware.
- [NVIDIA GPU Gems grass](https://developer.nvidia.com/gpugems/gpugems/part-i-natural-effects/chapter-7-rendering-countless-blades-waving-grass):
  dense grass textures on crossed cards, anchored roots and GPU wind. The
  second pass follows this strategy instead of the original skinny ribbons.
  Its old hardware limits should not be read as current performance limits.
- [NVIDIA virtual botany](https://developer.nvidia.com/gpugems/gpugems2/part-i-geometric-complexity/chapter-1-toward-photorealism-virtual-botany):
  bounded planting grids, instancing, opaque alpha testing rather than sorted
  translucent grass, and distance dissolve. We do not claim to reproduce
  Skyrim or Just Cause3's undocumented implementations.
- [Unity mip alpha coverage](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/TextureImporter-mipMapsPreserveCoverage.html):
  the isolated foliage importer preserves alpha coverage at the shader's.35
  cutoff, with trilinear filtering, alpha-edge dilation and4x anisotropy.
  Global MSAA remains unchanged; this pass does not promise alpha-to-coverage
  antialiasing on the existing single-sample render pipeline.
- [Unity RenderPrimitivesIndirect](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Graphics.RenderPrimitivesIndirect.html):
  supported GPU-driven primitive submission; tight patch bounds and explicit
  resource lifetime remain the application's responsibility.

Only fine tiles retain a 16-float regular-grid sidecar (264KiB at64 segments).
It contains mean color, occupancy, nine form weights, two morphology traits
and actual rendered height. Far tiles retain color but no fine-cover sidecar.
Worker wire v3 carries it; mesh cache accounting includes it. No worker/world
snapshot stores a planet-sized blade map.

On local activation, a compute pass interpolates the actual terrain triangles,
chooses growth forms and compacts accepted clumps into a GPU buffer. The root
lattice has0.60m candidate spacing with stable chart-addressed jitter. Large
world-scale tiles are restricted to a bounded moving window, not filled across
their whole kilometre area. There are no individual plant GameObjects or
CPU-updated blade transforms and no recurring GPU readback in normal rendering.
One patch is installed per frame. A large-tile moving window spans384m with a
450k-candidate safety cap (64bytes per root, at most27.5MiB capacity per window,
plus the tile grid/arguments; normal clipped windows are smaller). Inactive patches release GPU resources;
the existing terrain cache still accounts retained CPU sidecars.

Drawn-water triangle overlap conservatively clears root-grid corners, including
sub-grid streams. This sacrifices some shore density to avoid water plants
floating over channels; it does not change water levels or carve ground.

Three geometry levels use3/3/2 crossed cards and3/2/1 segments per card
(54/36/12 vertices per root, previously126/48/12). Each card contains dozens
of overlapping blades/leaves rather than a single blade. More dense roots and
alpha overdraw can still increase GPU cost; fewer vertices is not an FPS claim.
They blend over25–42m and65–90m; final visibility falls over130–160m.
Card silhouettes stay the same across levels. Root-to-tip shading, shader wind,
light transmission and received sunlight shadows supply depth. Grass does not
cast individual shadows in this pass. Forward/depth passes share visibility.
Loading fades over two seconds; transitions use opaque ordered dithering.
Some transition grain, silhouette/detail changes and geometric simplification
can remain visible; imperceptible LOD is a goal, not an acceptance claim.

The2x2 RGBA atlas is generated with the built-in image-generation tool and
committed as an ordinary Resources asset. Its source PNG is1254x1254, not the
2048 requested in the prompt; Unity applies normal texture importing/mips.
See GROUND_FOLIAGE_ASSET.md for the exact prompt and provenance. Form palettes
and generated regional traits remain shader-controlled; this is not a separate
hand-painted texture for every fabricated species.

Supported default0.1x and1x scales retain physical plant sizes. Geometry follows
resident level0 terrain; very small scales/short full-detail radii may end the
3D layer earlier. Regional fields are planet-addressed, but transient clump
lattice positions can resample on tangent-chart reanchors, fading in anew.
These are not persistent harvested plant identities. Per-tree shade and ground
decals, trampling, snow cover and weather-dependent growth remain future work.

Terrain settings can turn nearby ground vegetation off while retaining distant
cover color. F3 reports submitted cover draws and allocated candidate/buffer
capacity, **not** a synchronous GPU count of surviving clumps.

## Checkpoint evidence

Dense/tall follow-up: `player-ground-foliage-dense`, IL2CPP21Oct06:22:38Z.
5863 focused checks pass with the same saved200k authoritative hash. Native
GPU fixture exits0 with22907 compact roots,539206 changed pixels versus bare
and1.85MiB buffers. Near, overhead and eye-height tallgrass images are inspected:
substantially fuller cover, but visible card silhouettes/repetition and no
real-world or broad GPU-FPS acceptance. Details in VALIDATION.md. The following
numbers describe the historical first pass, not the new renderer's performance.

5859 focused ecological/determinism/transport checks and4037 existing scatter
checks pass. The source hash of the saved200k Continents Plus world is unchanged.
Final diagnostic terrain+flora preparation1.35s (earlier1.82s), retained derived
total259.5MiB. An isolated cold flora catalog plus200k profile calculations
took41.8ms; this excludes soils/terrain that already exist and does not claim
the complete rendering addition costs only41.8ms. First cold fine tile,
including amplification/control creation and transport,1.588s; repeated tile
with warmed amplification165.4ms. Compilation was also running; these are not
a controlled baseline comparison or universal load-time guarantees.

Final IL2CPP checkpoint `player-ground-flora-reviewed` built21Oct05:58:13Z,
on C:. Native compute/indirect-render fixture produces11433 compact clumps and
70812 changed pixels versus cover disabled, using0.96MiB of fixture GPU buffers.
Near/overhead captures are inspected: curved grass/leaf clumps render, with
visible separated tufts against the deliberately white test plane. This is
not an actual-world terrain-color, dense-scene FPS, reanchor or realism review.
No native performance win or imperceptible LOD is claimed. Owner review is the
art/performance checkpoint, with the nearby-cover toggle for comparison.

Packaged-worker checks first passed127639 exact floats/topology/bounds and
cancellation values, with selected scatter probes empty. A subsequent added
fine grassland probe passes177346 exact values/topology/bounds/cancellation
checks and exercises the new sidecar explicitly. GPU fixture/test-player
processes are stopped. Prior
`player-fps-average` remains the rollback; saves/history are untouched.
