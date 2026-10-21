# Generated tree lineages / tree-flora-1 / 21 October 2026

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
