# Ground layer / ground-flora-1

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
Fine botanical structures (fern pinnae, flowers/seed heads, cactus spines and
individual moss shoots) remain art improvements; the first pass uses procedural
blade/leaf fans with different proportions, bending and stature.

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
  clustered geometry, anchored roots and GPU wind. We use opaque procedural
  ribbons instead of sorted transparent textures; its old hardware limits
  should not be read as current performance limits.
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
lattice has0.85m candidate spacing with stable chart-addressed jitter. Large
world-scale tiles are restricted to a bounded moving window, not filled across
their whole kilometre area. There are no individual plant GameObjects or
CPU-updated blade transforms and no recurring GPU readback in normal rendering.
One patch is installed per frame. Inactive patches release GPU resources;
the existing terrain cache still accounts retained CPU sidecars.

Drawn-water triangle overlap conservatively clears root-grid corners, including
sub-grid streams. This sacrifices some shore density to avoid water plants
floating over channels; it does not change water levels or carve ground.

Three procedural geometry levels use7/4/2 blades and3/2/1 segments per blade.
They blend over25–42m and65–90m; final visibility falls over130–160m.
Lower-poly distant blades widen modestly. Root-to-tip shading, shader wind,
light transmission and received sunlight shadows supply depth. Grass does not
cast individual shadows in this pass. Forward/depth passes share visibility.
Loading fades over two seconds; transitions use opaque ordered dithering.
Some transition grain, silhouette/detail changes and geometric simplification
can remain visible; imperceptible LOD is a goal, not an acceptance claim.

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
