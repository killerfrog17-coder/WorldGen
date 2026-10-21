# Generated mid-height vegetation / mid-flora-1 / 21 October 2026

This is the owner's visual/performance prototype, not finished botanical art,
seasonal ecology, a food-web rules upgrade or certified seamless vegetation LOD.
Compilation/packaging only: no agent test suites, game launches or profiling.

## Species and ecology

`MidFlora` derives fourteen regional potential taxa (two per structural family):
leafy bushes, open scrub, dwarf heath, arching canes, large ferns, reed stands and
succulents. Province founders acquire regional branches via separate named streams.
They are fictional naturalistic taxa, not renamed literal Earth plant templates.
This is static biogeographic synthesis, not population evolution over time.

Traits include stature/spread, leaf/wood/blossom/fruit pigments, branching and leaf
shape, flexibility, thorniness, colony size, temperature/moisture/shade/pH/salinity
niches and drought/flood tolerance. Placement uses actual dry ground and slope,
soil, coarse habitat/canopy opportunity, physical-scale colony noise and a shared
planet-addressed broad stand mask. Desert admits appropriate open scrub/succulents,
not grass or humid fern fields. Cold ground favours dwarf shrubs, not tall warm
scrub. Reeds require a wetland/water-table opportunity, not every waterway margin.
Actual individual tree shade/competition remains the subsequent combined-layer pass.

`MidReproduction` records flowers, fruit form, wind/animal/gravity dispersal,
pollination, fruit diameter, relative nutrition, maturation delay and persistence.
Separate fruit-flesh, seed and leaf ingestion hazards plus contact irritation are
generated independently of fruit colour. Hazard indices are fictional 0..1
relative potential, NOT mg/kg, lethal-dose predictions or universal animal safety.
No animal-specific tolerance, poison effects, harvesting, seasonal bloom/fruit
availability or population/food-web feeding links are implemented. Visual flowering
and fruiting ornaments are static samples of potential anatomy, not today's crop.
Ferns are spore-bearing rather than flowering/berry-bearing.

The ecoregion profile lists these as DERIVED potential flora separately from its
saved recorded taxa. `/midflora` and `/midflora N` inspect catalogue/niches/hazards.
Not all catalogue candidates are suitable at the player's exact location.
GroundSurfaceState remains an unwired future boundary; no new wetness, snow,
puddles, seasonal water levels or flooding simulation is added here.

## Model generation and local LOD

The existing placeholder shrub channel is replaced, not layered underneath new
bushes. Existing tree/rock/grass/herb channels retain their own streams. Coarse
512m nominal geographic scatter patches use a jittered48x48 candidate lattice,
cube projection's area Jacobian, early broad-stand rejection and suitability/
species-colony selection. Heath/canes/reeds/ferns have small same-species satellite
colonies; each member is checked against actual ground/water. No global fine raster.

Four structural variants per taxon reuse three anatomy LOD meshes. Model codes
encode shared genotype/form plus two variant bits, and use collision-free existing
64-bit art keys. Individual size/spread/tint/yaw vary within the species. Branches
and leaves are generated procedurally, no per-instance GameObjects or uniquely
generated mesh per individual. This pass uses actual opaque leaf/frond geometry;
no new alpha atlas, billboard baking or poorly fitted crossed full-bush cards.

Bushes/scrub/heath/canes use rooted branch skeletons, shaped folded leaves,
optional flowers/fruit/thorns. Ferns have arched rachises/paired leaflets, reeds
have stems/ribbons/seed heads, and succulents use rosettes/columns/pads. Coarser
meshes keep the branch layout, reduce curves/leaf counts and enlarge surviving
leaf area. Common model-space bounds avoid independent LOD resizing.

Default transitions are28–55m and110–200m. Size-dependent geometric retirement is
280–1400m, fading across the final20%; the range control scales these distances.
GPU instancing batches shared variants across resident chunks, including lowest
geometry LOD. Near plants use the owner-approved shared shader wind and species
flexibility; rigid succulents do not bend. Cheapest LOD is still, and only near
mid-height models cast shadows. Existing grass/wind animation itself is unchanged.
Transitions remain distance-only, with no timed birth fade. Dither grain and
species-specific LOD appearance require owner review, not assumed invisible.

Generated-art caching prunes up to eight unused models per batch rebuild above
a384-model working-set target, retaining active models. This is a soft target,
not a hard cap on simultaneously visible regional diversity. Freed meshes and
materials are removed from owner lists and disposed. No disk model cache added.

## Distant terrain

Each coarse terrain control carries a continuous mid-cover summary; sampling blends
these with the existing geographic support weights. Summary RGB, projected crown
coverage, height and width travel on a six-float regular grid at every terrain LOD.
Coverage uses the same broad stand mask as nearby placement. Heights are metadata,
NEVER added to physical/rendered ground elevation. Underwater/steep surface samples
have suppressed coverage. A filtered RGBAHalf tile texture supplies colour/coverage;
the terrain shader blends unresolved foliage contribution over180–1000m rather than
painting local soil permanently shrub green. Added grid24bytes/node + texture8bytes/
node, before Unity object overhead; tracked in terrain cache byte accounting.

The summary is a statistical expected footprint, not an exact census of crowns.
Local geometry supplies real silhouettes until its draw limit. This pass DOES NOT
build merged thicket proxy meshes, multi-view impostors or kilometre-scale canopy
skyline geometry; that remains the shared shrub/tree distant-volume follow-up.
Terrain/worker wire protocol advances3->4 and both are packaged together. No source
world, saved ecology/history, save schema, terrain height or hydrology rule change.

Grass debug adds mid-height on/off, Low/Medium/High geometry and0.4–1.25x range.
Those controls affect drawing, not species/catalogue generation or scatter jobs;
distant material colour stays when local geometry is disabled. F3 shows visible
mid-height instance count. Seasonal representation, art quality, abundance and FPS
are intentionally left to the owner's interactive review.

## References informing the implementation

- [Crysis vegetation anatomy/shading/wind](https://developer.nvidia.com/gpugems/gpugems3/part-iii-rendering/chapter-16-vegetation-procedural-animation-and-shading-crysis)
  documents shared animation/shading over differently structured plant geometry.
  No Crytek code or SpeedTree middleware is imported by this pass.
- [Epic HLOD layer types](https://dev.epicgames.com/documentation/en-us/unreal-engine/world-partition---hierarchical-level-of-detail-in-unreal-engine)
  distinguishes lowest-LOD instancing from merged/simplified proxy meshes. This
  checkpoint uses the former concept, not Epic's offline world-baking pipeline.
- [Epic multi-view impostors](https://dev.epicgames.com/documentation/en-us/unreal-engine/impostor-baker-plugin-in-unreal-engine)
  remain a possible later representation, not a feature claimed here.
- [NPS tundra](https://www.nps.gov/subjects/nnlandmarks/tundra.htm) and
  [NPS desert plants](https://www.nps.gov/zion/learn/nature/cacti.htm) ground the
  varied plant structures; fictional regional taxa need not reproduce those species.
