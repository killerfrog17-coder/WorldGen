# Generated tree lineages / tree-flora-1 / 21 October 2026

## Rebuilt tree / baked-view prototype

### Player-only invisibility correction

Owner reports rebuilt trees absent; switching to old models restores them in the
same world. The preceding Editor asset check did NOT catch player shader
stripping. The failed package retained only non-instanced variants of TreeSurface2
(forward vertex4 settings variants reduced to2; shadow/depth2 reduced to1).
Runtime-created materials alone did not advertise instancing at build time.
Added Resources/TreeSurfaceMaterial.mat with instancing enabled, and runtime now
clones that template just like the established LocalScatterMaterial path.
Corrected build retains4/4 forward vertex,8/8 fragment and2/2 shadow/depth variants.
An opt-in --worldgen-tree-shader-check exercises the actual packaged material,
two independently transformed instances per draw, both broad/needle families and
mesh/baked representations. It bypasses display-settings/library/music startup
and writes only its explicit artifact output; no world is loaded. Editor checks
alone must not be cited as packaged-render acceptance.
Unity documents automatic instancing-variant stripping when the build cannot
see instancing usage; see [Unity's stripping explanation](https://unity.com/blog/engine-platform/stripping-scriptable-shader-variants).
The fix is per-shader material retention, not globally keeping every shader variant.

Owner rejected the previous tree art and approved reusable generated models with
actual-model distant representations. This is a FIRST ART CHECKPOINT, not a
completed all-family/dense-forest overhaul or measured FPS improvement.

Broad crowns now use bounded space-colonisation branching, supported-tip trunk
taper and textured folded leafy shoots. Needle crowns use apical trunks with
irregular side limbs/twigs. Both mesh LODs share their skeleton and foliage
locations: middle detail simplifies the shoot surfaces without deleting whole
foliage packets. Existing regional anatomy/palettes and six shared variants stay.
Other structural families still use preceding anatomy; actual tree populations,
kilometre-scale forest aggregates, shrubs, grass, world/history, heights/water,
worker protocol and saved ecology are unchanged. Forest density is NOT increased.

Local far trees use a two-triangle billboard with sixteen upper-hemisphere views
of their own detailed model, albedo plus object normal/depth, three-view blending,
depth-assisted reprojection and live light/shadow reception. Alpha-weighted mips
preserve cutoff coverage. This is not a licensed SpeedTree/Simplygon integration,
an exact Endnight reproduction or pixel-perfect mesh equivalence. Stored depth
helps parallax but does not replace raster SV_Depth. Wind is retained on meshes;
impostors are still. Coarse views can still soften/detail-shift silhouettes.

Meshes are generated on first art lookup and reused in the existing RAM cache.
One background CPU task rasterises one model's views at a time; the main thread
snapshots meshes, uploads completed textures and swaps representations. There is
no per-frame CPU tree rasterisation, GPU capture/readback or per-tree object.
Until ready, a complete middle mesh is drawn, not an empty billboard. Bakes are
session-local, not disk-persistent. At most48 baked models (~128MiB RGBA8 mipmapped
view textures); excess active variants retain meshes, unused bakes can retire.
Shared shoot textures add ~21MiB GPU and retained source pixels ~16MiB CPU, plus
temporary bake/mesh memory. Cache numbers are not total VRAM measurements.
Editor samples took ~1.5–2.4s per model including validation/capture; a diverse
stand can therefore take tens of seconds to finish. Startup FPS may differ from
settled baked FPS. F3 shows baked/pending models and accounted view bytes.

Game settings → Vegetation offers session-only rebuilt/previous-model A/B and
baked/mesh-only A/B. Existing LOD distances remain literal; an explicit button
tries60/200m handoffs (these distances persist through existing settings). There
is no new timed load fade; normal spatial LOD blends remain. No full GPU Hi-Z or
new GPU-driven visibility system is claimed; existing conservative culling stays.

Isolated Editor checks validate two archetypes' topology, finite/bounded vertices,
sixteen nonempty bakes and real-D3D11 near/baked renders at three yaws. Near/middle
triangles: broad5250/2298, needle14064/3066, far2 after baking. This checks plumbing,
not dense-forest FPS, every species, no-popping, scientific growth or owner art
acceptance. Initial shader compilation and duplicated-silhouette reprojection
defects were corrected before final packaging. No owner player/world was opened
or stopped. RTX4060 is confirmed in the owner's Player.log and NVIDIA process list.

### Primary reference architecture

- [Self-organizing tree models, SIGGRAPH2009](https://algorithmicbotany.org/papers/selforg.sig2009.html):
  inspiration for spatial competition and branching, NOT a full implementation
  of its light/bud model or a real biological growth simulation.
- [Microsoft/Simplygon vegetation optimisation](https://developer.microsoft.com/en-us/games/articles/2026/01/how-to-optimize-vegetation-with-simplygon/):
  retain structural geometry, simplify foliage representations appropriately,
  use baked distant impostors rather than arbitrary crown decimation.
- [Epic Impostor Baker](https://dev.epicgames.com/documentation/en-us/unreal-engine/impostor-baker-plugin-in-unreal-engine):
  upper-hemisphere multiview captures, neighbouring-view blending and depth.
- [Endnight The Forest v0.18](https://endnightgames.com/updates/v018-destructible-buildings-controller-support-more-world-opened-and-much-more):
  multi-angle billboard blending and normal/translucency improvements. This does
  not establish Sons of the Forest's precise tree asset/rendering implementation.

## Tree coverage / vegetation visibility follow-up

Subsequent quality-control checkpoint: Game settings → Vegetation now supplies
persistent per-layer visible density, quality ceilings, independent LOD handoff
distances and draw caps plus shadow/culling controls. See SETTINGS.md for defaults
and limits. These adjust rendering only; far ecological summaries remain literal.

Owner rejected the initial tree art/early clumping and reported approximately
190FPS grass-only,160–170 with specialists,120–130 with mid plants and60–70 with
trees. These are owner scene comparisons, not an isolated agent benchmark.
Source inspection found a definite defect: the coarse tree `Leaf` strip used
one segment with width `sin(pi*t)`, i.e. two zero-width endpoint rows and no
interior width. The shared mid-plant `HerbLeaf` had the same far-LOD defect.
Both now retain an interior row. This is a source-level diagnosis, not runtime
certification of every reported leaf/LOD symptom.

Current local broad crowns retain the branch/twig locations and folded leaf
packets at all three LODs instead of switching to rough opaque balls at260–450m.
Six/four/two packets per twig use stable prefixes and inverse-count area scaling;
both length and width compensate. Near packets use two segments rather than three.
Needle tiers/limbs retain their layout across LOD, with simplified sprays. Tiny
ornaments remain near-only. Succulent solid anatomy and distant forest aggregate
volumes are unchanged. No assertion of perfectly matching projected coverage
from every angle: reduced packet counts, frond simplification and far forest
statistics can still visibly differ and need owner review.

The analytic twelve-ellipse/optional-lobed-atan2 fragment loop is replaced by one
filtered procedural cluster texture-array lookup in colour, depth and shadow.
32 shared128² RGBA8 slices = eight leaf-family slots/four twig layouts, about
2.67MiB including mips before driver overhead. Shapes currently used by textured
packets are the broadleaf/lanceolate families; needles/fronds retain geometric
foliage. The array is generated once per explorer, not per tree or frame. Mips
rescale alpha to preserve the fraction passing the same0.4 cutoff, with neutral
transparent-edge colour. Species still provide inherited palette/leaf aspect,
while the atlas is shared rather than a bespoke photograph per fictional taxon.
Small mip levels necessarily quantize coverage; this is not temporal AA or a
guarantee against all shimmer. Structural variants now share species materials
(one for near/middle motion and one for still far foliage), with safe shared
ownership during mesh-cache eviction.

### Visibility and CPU work

- Grass already had GPU root frustum/range compaction plus CPU page frustum checks.
  Other scatter previously had distance filtering and Unity's whole-draw group
  culling, NOT individual plant frustum/occlusion culling. Instancing alone does
  not solve invisible instance submission.
- Herbs, fungi, mid plants, trees and rocks now have cached wind-inclusive AABBs,
  coarse patch rejection and individual camera frustum tests before art lookup/
  batching. Bounds include crown extent, height, tilt and motion, not root points.
  Explicit draw bounds also protect against Unity's tighter undeformed group bound.
- Conservative terrain-only occlusion applies to scatter and grass pages. A
  line-to-AABB cone is tested at seven slabs. A whole slab must lie in one stable,
  resident finest tile and below a lower bound of ALL overlapping drawn terrain
  triangles. Cached16x16 min blocks include all cell vertices. Unknown/coarse/
  transitioning terrain stays visible; moving eye/bounds get safety margins.
  A ceiling of the eligible lower bounds skips slab work where no hill can
  occlude the plant (especially flat open terrain). Per-tile min grids add at
  most1KiB and participate in terrain-cache byte accounting.
  No baked occlusion, GPU readback or individual plant raycast. It intentionally
  misses occluders between slabs, cross-tile slabs, coarse hills and foliage.
  This is NOT a full-scene depth-pyramid/Hi-Z occlusion implementation. Terrain
  renderers and embedded far forest volumes retain their previous visibility path.
- Relevant offscreen/hidden nearby shadow casters are retained as shadow-only
  instances using sun direction and a sphere enclosing the active shadow-distance
  receiving frustum. Maximum caster-to-receiver drop includes valley elevation,
  not just the tree's own height; near-horizontal sun retains all possible casters.
  Hidden tree shadows use at least middle detail. This conservative reach can
  retain many casters at low sun; shadow quality/performance needs owner review.
- Batch arrays/dictionaries are reused; the per-candidate captured Add closure and
  two unconditional art lookups are removed. Batches rebuild on meaningful motion
  (>0.5m or1°), projection/options/data changes, plus a1s lighting refresh, rather
  than10Hz while stationary. Frustum and occlusion safety margins cover that
  small camera slack; no slow load fade is added.
- Finest terrain installation/restoration marks affected ground tiles. Only
  intersecting scatter patches reproject; transform/root arrays are reused.
  Reanchor/teleport still fully project. Ecology, placement/density, world/history,
  worker wire5, saves, terrain heights/water and approved wind are unchanged.

Grass debug exposes separate extra-scatter-frustum, hill-occlusion and vegetation
shadow switches (default on). Grass's existing GPU frustum stays enabled even
with the extra scatter switch off. F3 shows rejection and shadow-only/page counts;
patch-level counts can include out-of-range/disabled instances. Counts are
submission diagnostics, not measured GPU savings. Disabling a vegetation draw
layer still does not stop its ecological preparation jobs.

### Research and scope

- [GPU Gems2 / SpeedTreeRT tree and shrub layers](https://developer.nvidia.com/gpugems/gpugems2/part-i-geometric-complexity/chapter-1-toward-photorealism-virtual-botany):
  3D trunks/primary branches, clustered textured foliage, fewer/larger clusters
  with distance and view-dependent far representations. Adapted approach, not
  licensed/imported SpeedTree code or a claim of matching a particular game.
- [Unity alpha-coverage mipmaps](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/TextureImporter-mipMapsPreserveCoverage.html):
  motivates coverage-aware filtering. Runtime-generated arrays use our explicit
  mip construction, not an importer checkbox on an absent image asset.
- [Unity RenderMeshInstanced](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Graphics.RenderMeshInstanced.html):
  instances are culled/sorted as one group; our extra individual selection must
  happen before submission.
- [Unity GPU Resident Drawer](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/gpu-resident-drawer.html)
  and [GPU occlusion](https://docs.unity.com/en-us/engine/6000.3/manual/analysis/graphics-performance-profiling/in-urp/gpu-culling):
  built-in GPU occlusion works through the Resident Drawer/MeshRenderer route.
  Our manual instanced scatter/indirect grass are not automatically enrolled by
  checking the renderer option. No such checkbox-only migration is claimed.
- [Epic multi-view tree impostors](https://dev.epicgames.com/documentation/en-us/unreal-engine/impostor-baker-plugin-in-unreal-engine):
  a good next stage for matching far individual trees to their near models;
  not included in this bounded fix. Full GPU-driven indirect/BRG culling and
  scene-depth occlusion are also follow-ups if owner FPS remains inadequate.

Only compilation/packaging is authorized/performed. No test campaign, launch,
capture or profiling; owner reviews same-camera FPS, leaf continuity, art and
hill/camera-turn/shadow popping. CPU culling itself has a cost; flat open scenes
may get little benefit from hill occlusion. No numeric speedup promised.

## Original tree-lineage checkpoint (superseded rendering details below)

Owner-review prototype. Compilation/packaging only; no agent tests, player launch,
screenshots, profiling or source-world hash audits. Do not infer art, realism,
seamlessness or performance acceptance from compilation. Existing accepted ground/
mid-height cover and approved shared wind remain the baseline.

## Inheritance, not a named Earth tree library

Thirteen seed-generated world founder slots span broad crowns (six), needle
crowns (three), frond crowns, tree ferns, arborescent succulents and tall canes.
These are construction families, not hardcoded oak/birch/pine species. A global
family gene fixes recognizable branching/leaf/bark/wood tendencies. Province
branches make small shifts; regional descendants make smaller anatomical shifts
and acquire bounded climate/niche adaptation. Warm frond/fern/cane families are
not offered in cold regional catalogues; tall succulents require warm dry opportunity.

`TreeMorphology` codes pack founder16bits, province4bits, regional5bits, form3bits
and structural variant3bits. Six variants are sampled per species. Variant bits
do not alter species materials or wood metadata. Genetic quantization can let
related taxa share art; identity remains region+family, not the art-code integer.
This is static derived biogeography, NOT simulated speciation/growth over time.

Traits include crown-base height, aspect, branch lift, forking/openness potential,
trunk thickness, leaf shape (oval/lanceolate/lobed/compound/sprays/fronds/fleshy),
bark pattern and leaf habit. Not all anatomical metadata have equally detailed
visual implementations in this first art pass: compound leaf packets are still
simplified; fork habit is not a full botanically calibrated growth grammar.

Inherited bark/leaf, heartwood and sapwood colours accompany wood density, relative
hardness, decay resistance and growth potential. These are fictional, bounded
material/life-history proxies, NOT timber grades or measured mechanical values.
Palm/fern/cane/succulent tissues are especially simplified by this shared metadata.
Heartwood/sapwood are data for future exposed sections, not currently visible
chopping/crafting. Density/hardness/growth relationships are broad priors, not a
universal botanical law. No named Earth wood species is required.

Regional reproductive metadata record cones/spores/fleshy fruit/nuts/capsules/
winged seeds, potential flowers, maturation/persistence and relative flesh/seed/
leaf toxicity, contact irritation and nutrition. Reproductive traits are currently
regional potential, not a full inherited phylogenetic or pollination simulation.
Hazard indices are0..1 fictional potential, never calibrated doses/animal safety.
No seasonal leaf loss/bloom/crops, poison effects, harvesting, dynamic population
growth or new historical food-web links. Leaves currently depict a foliated state.

`GroundFloraRegion.Trees` is a lazy derived catalogue, not a saved EcologyResult
taxa mutation. Ecoregion profiles list it separately. `/trees` lists local candidates;
`/trees N` exposes ancestry, wood, anatomy, niche and reproductive traits. Catalogue
membership is not proof of suitability at the exact player location.

## Placement and reusable art

LocalScatter7 replaces the old tree channel; trees are not duplicated over the
old broadleaf/conifer/savanna proxies. Other channels retain separate streams.
512m nominal geographic patches use48x48 jittered candidates, cube-face area
Jacobian, actual dry land, soil/root support, slope, climate/altitude, salinity,
flood/permafrost opportunity, broad stand noise and species-scale colony noise.
The same broad stand field feeds far cover summaries. Static cohorts include
sapling/juvenile/adult sizes from the same species. No actual ages or growth ticks.
Canopy shade/competition still uses existing habitat opportunities, not a new
individual-tree light or root solve. Existing grass/mid-height placement is unchanged.

Six shared skeleton variants get three local anatomy LODs, not a unique tree mesh
per instance. Curved tapered continuous branch sweeps and shaped foliage surfaces
replace the old near solid-crown placeholders. Broadleaf foliage uses folded,
tightly bounded shader-cutout sprigs (12 leaf silhouettes per fitted surface),
not a mesh for every leaf or full-tree crossed sprite cards. Needle sprays and
fronds use folded geometry. Cheapest broad/needle LOD uses rough crown groups;
succulents retain opaque structure and do not bend. Procedural bark shading and
restrained leaf transmission complement inherited vertex colours.

The shared shader wind drives rooted tree bending plus small foliage motion;
the accepted grass/herb wind formula/controller is not replaced. Forward/depth/
shadow passes share the tree masks and movement. Cheapest tree LOD has no wind.
Local detail transitions60–120m and260–450m; size-dependent retirement450–1400m,
with large trees handing off over950–1400m. These are prototype values, not a
promise of invisible transitions. Near models cast shadows; cheapest LOD does not.
Procedural cutout leaf edges are not a prefiltered mip atlas; aliasing/fill cost
need owner review. No multi-view impostor baking is included.

GPU instances reuse the existing art/batch cache, including its soft384-model
unused-working-set target. Active diversity can exceed it. Low/Medium/High tree
quality clamps detail before art lookup, avoiding generating unused high-detail
models and redundant paired draws when both LODs clamp to the same representation.
That shared lookup correction also applies to the mid-height quality control.

## Distant forest shape, separate from terrain

Coarse controls store continuous cover RGB, projected coverage, mean height/width,
openness and thermal reference. Queries blend them with the existing C2 geographic
support, then apply stand noise, terrain slope, water exclusion and thermal lapse.
Every terrain LOD carries an eight-float forest grid plus a filtered RGBAHalf
colour/coverage texture (32+8bytes/node before object overhead). Grid/texture and
extra mesh buffers participate in existing terrain memory accounting.

`ForestCanopyMesh` generates bounded statistical crown clusters above the drawn
ground. At most8x8 aggregate sites/tile, up to three crowns/site; nominal minimum
spacing64m. These are extra render-only land-mesh triangles tagged with negative
alpha, not altered ground vertices, walking heights, river beds or water datums.
They follow existing drawn terrain to avoid sampling a second mismatched ground.
No near vegetation census is required to produce distant forest mass.

Far volumes hand in over950–1400m and distant material contribution over700–1800m.
Volumes share terrain resident-coverage masks, its temporal terrain crossfades and
atmosphere. Individual trees retain distance-only fades, no timed birth animation.
Camera can distinguish wooded from bare terrain through height/silhouette as well
as colour, but these initial lumped proxies are NOT exact crown positions/species
silhouettes. Different terrain LOD groupings/reanchors can change proxy grouping;
terrain crossfades soften rather than mathematically eliminate that discrepancy.
Dense/sparse handoff, skylines, slopes and long-range loading cost need owner review.
Broad aggregated crown groups do not yet preserve detailed needle/frond topology.

Transport wire4->5 packages the forest grid with the same worker/player sources.
No save schema, authoritative generation/history, terrain-height or hydrology
change. More derived preparation/mesh/memory work is expected; no universal speedup.

Grass debug adds nearby generated-tree toggle, separate far forest colour/volume
toggle and three geometry quality levels. These are draw controls, not switches
to remove ecological preparation or scatter jobs. F3 reports visible tree count.
Vines, epiphytes, dead wood, collision, actual tree-dependent understory competition,
seasonal visuals and full interaction remain subsequent work.

## Primary references

- [Crysis vegetation rendering and animation](https://developer.nvidia.com/gpugems/gpugems3/part-iii-rendering/chapter-16-vegetation-procedural-animation-and-shading-crysis):
  foliage surfaces, main/detail bending and economical foliage lighting. Architecture
  adapted, not imported Crytek or SpeedTree code.
- [Epic foliage HLOD strategies](https://dev.epicgames.com/documentation/en-us/unreal-engine/world-partition---hierarchical-level-of-detail-in-unreal-engine):
  lowest-LOD instancing and grouped proxies. This pass does not import that engine's
  offline world baking or claim exact game reproduction.
- [USFS regional/climatic wood-density study](https://research.fs.usda.gov/treesearch/45814)
  motivates environmental variation, not the numerical calibration of fictional wood.
- [USFS biomass/wood-density overview](https://research.fs.usda.gov/programs/fia/nsvb)
  distinguishes structural volume and tissue density; no biomass simulation added.
