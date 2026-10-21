# Ground layer / ground-flora-5

## Future ground-effects boundary /21October2026

See GROUND_SURFACE_STATE.md and Core's `IGroundSurfaceStateProvider`: independent
soil moisture/surface wetness, snow cover/depth, actual connected surface water
and long-term inundation exposure. Interface only, no provider or consumer wired
in, no changes to flora generation/placement/rendering. Shrubs/trees can adopt
this boundary later without inventing separate wetness/snow interpretations.

## Documented layered wind /21October2026

`player-layered-vegetation-wind` adapts the researched Crysis main-bend/detail
split, Sucker Punch's coarse/fine advected gust fields and SpeedTree's desired/
effective response controls. Continuous waves remain the animation; smoothed
controls and shared, moving noise patches set its strength/lean/pace. No revival
of the rejected spring/FIR response textures. Wind-advected70m/14m value-noise
layers are shared by grass, flowers and existing tree/shrub proxies; fungi/mats
stay still. Flower flexibility/actual height condition displacement. New Grass
debug checkbox switches live to exact direction-only rollback, plus2–12s response
slider (default6s). F3 shows effective speed/mode. Ordinary grass wind checkbox
still disables grass/herbs; densities/geometry/placement/LOD are unchanged.
No player launch/profiling: owner judges fluidity and FPS. See
PROCEDURAL_WEATHER.md for primary sources, animation/noise details, uncalibrated
gust statistics and session controller/multiplayer synchronization limitations.

## Literal original movement / direction-only rollback /21October2026

Owner rejects wave-envelope too and demands the original animation, not another
approximation. `player-grass-direction-only` restores literal pre-wind dcf546c
grass shader, with ONLY a direction uniform and its use replacing(.8,0,.4).
Old scalar waveform/clock/speed/strength/curvature/LOD are unchanged; CPU heading
preserves the old vector magnitude. Flowers/ferns use the same scalar and
root-pinned tip displacement; fungi/mats remain still. No weather speed, gust,
history or species mechanics affect this movement. Generated mechanical traits
remain metadata only. Experimental field compute/runtime removed, no associated
texture or dispatch. Calm still waves intentionally. See PROCEDURAL_WEATHER.md;
compile/package/source comparison only, no agent launches or profiling.

## Continuous waving / slow wind-envelope correction /21October2026

Owner rejected the prior spring pass as jagged/vibrating and essentially no
improvement. `player-wind-wave-envelope` / plant-motion2 restores the earlier
two-sine animation character. Wind's5–10s positive-window trend controls its
direction, amplitude, speed-band mix and strong-wind leaning bias, not immediate
blade positions. Shorter/stiffer plant differences remain; mushrooms and mats
stay still. The old spring history length did not imply a5–10s dominant
response. See PROCEDURAL_WEATHER.md for the correction, fixed world-time carrier
bands and field costs. Built only; appearance/performance await owner review.

## Inertial species/height-conditioned sway /21October2026

`player-plant-spring-wind` replaces direct wind-to-angle grass/herb movement
with a shared GPU response to8s of recent wind. Sustained winds keep a biased
lean; gust changes excite an underdamped response that can overshoot and settle.
No permanent wind-independent swaying under completely calm forcing. Procedural
herb lineages gain relative flexibility, response-speed and damping traits from
an independent stream, inspectable via `/flora N`; existing anatomy is unchanged.
Grass uses form/phenotype/height-conditioned mechanics and small stable per-leaf
variation. Short/stiff cover generally bends less/faster than tall flexible cover.
Near/far blades retain matching traits. Fungi/moss/lichen stay still. This does
not overhaul tree branches/leaves, density, placement or LODs. See the plant
spring-response section in PROCEDURAL_WEATHER.md for the sampled four-mode
response bank, bounded GPU field, sources, compute fallback and cost limits.
Only compilation/packaging; no agent tests, native launches or profiling.

## Shared weather wind /21October2026

`player-weather-shared-wind` preserves accepted grass geometry, field density,
placement and distance-only LOD, replacing independent fixed-direction shader
sines with one seeded geographic/calendar wind field. Weather controls sustained
speed/direction, gust probability/duration/envelope and turbulence. Flowers,
ferns and flexible shoots bend visibly with grass, rooted by whole-shoot height
weights rather than per-part UVs. Fungi/moss/lichen stay still. The Grass debug
wind toggle now covers specialists too. Existing tree/shrub primitive sway also
uses the weather vector; articulated leaf/branch motion remains future work.
See PROCEDURAL_WEATHER.md for sources, time-occupancy model, multiplayer clock
contract and limits. Only compiled/packaged, no agent playtests or profiling.

## Generated specialist herb layer /21October2026

`player-generated-herb-flora` adds derived `ground-specialists-1`, leaving
ground-flora5, the owner-liked progressive grass renderer, stable pages and
stationary mats unchanged. No hardcoded named flower/mushroom species or plant
prefab catalogue. Functional anatomy is intentionally bounded: flowering stems,
basal rosettes, flower/seed-like spikes, fern fronds and ground fungal fruiting
bodies. Seeded province founders acquire bounded regional branches. The same
lineage keeps basic petal/head/leaf/frond counts; branches vary proportions/tint,
size and habitat niche. Pigment ranges are naturalistic rather than alien/glowing.

`GroundFloraRegion.Specialists` lazily exposes ten candidate taxa per visited
region: stable world-scoped ID, generated binomial/founder, morphology code,
form, fungal role, size, moisture/shade/temperature/pH/salinity preferences and
colony scale. This is inspectable Core ecological metadata, rebuilt from seed
and saved ecoregions, NOT a mutation of saved EcologyResult.Taxa/food webs or a
historical-rule upgrade. `/flora` lists regional candidates; `/flora N` shows
traits. Candidates are a regional potential pool, not a promise of occurrence
at every point. No permanent per-plant population or evolutionary simulation.

Placement appends an independent channel2 on existing64m geographic detail
patches (`local-scatter-specialists-5`). Existing tree/shrub/rock channels,
seeds, geometry, and instance IDs are unchanged in source. A24x24 jittered
candidate grid uses actual cube-sphere area Jacobian and physical-metre colony
noise. Temperature/lapse, climatic aridity, soil water storage/drainage,
shade-potential patches, pH, fertility, salinity, flooding and depth gate
establishment. No water, Ice or Desert placement. Cold local biomes cap height
at16cm. Ferns favour moist shade; decomposer fungi favour organic substrate,
root-associated fungi require forest-root/canopy opportunity, not arbitrary
open desert. Salt-marsh plant branches can tolerate salt; fungi retain low salt
tolerance. Noise is a site proxy, not a new fine hydrologic solver or measured
tree-root occupancy; actual partner-tree species/log hosts remain future work.

Models are generated mesh anatomy with independently coloured leaves/stems/
petals/caps, not billboards or a recolour of grass. Mushroom caps/stipes remain
still; herbs have restrained sway. Instanced batches reuse two mesh LODs with
distance-only transitions12-22m; herbs retire109-140m, fungi51-65m. Low LOD
petals/florets simplify rather than adding time-based load fades; no specialist
shadow-casting passes. Full morphology codes use collision-free64-bit art/batch
keys instead of the old small-variant arithmetic. Roots follow resident ground
in ordinary and prepared handoffs; no new plant collisions. Existing shared
scatter transport fields carry codes/dimensions/tint; wire3 is unchanged.

Grass debug adds a separate specialist-render toggle; F3 counts visible
specialist objects. Disabling it skips their draws, not catalogue generation or
resident placement data; this is a render-cost comparison, not a zero-CPU
ecology-off mode. Shapes/materials cache with the explorer's existing art cache;
long exploration can retain more models. Small rare plants do not tint distant
terrain; no large flower-carpet aggregate LOD is claimed in this pass.

References: NPS [prairie niches](https://www.nps.gov/tapr/learn/nature/a-complex-prairie-ecosystem.htm)
and [wildflowers](https://www.nps.gov/tapr/learn/nature/wildflowers.htm) describe
site variation and non-grass diversity; [alpine flora](https://www.nps.gov/grte/learn/nature/wildflowers.htm)
describes low/small flowers; [fungi](https://www.nps.gov/pore/learn/nature/mushrooms.htm)
distinguishes fruit bodies, underground networks/root relationships and weather-
dependent fruiting. Current placement is STATIC POTENTIAL flowering/fruiting,
not seasonal phenology, rain-triggered emergence, mycelial growth or an updated
food web. Existing worlds need no regeneration and saves are not changed.

Owner no-agent-testing instruction remains: compilation/packaging only; no
suites, native fixtures, game launch, screenshots, profiling or hash audits.
Art quality, abundance, loading cost and FPS remain for owner review. Catalogue
and phenotype generation are deterministic; this revision has no measured
speedup, botanical accuracy or all-LOD acceptance claim.

## Stable pages, distance-only scatter and habitat mosaics /21October2026

Owner reports the progressive field looks good and is performance-friendly,
including a convincing15m detail handoff on flat ground. This is owner scene
feedback, not a broad benchmark. New `player-ground-habitats-stable` preserves
that renderer while addressing movement-triggered resets and ground habitats.

Identified reset: floor(player/64) changed at every64m boundary and destroyed
all near buffers on tiles wider than64m (including normal102.4m finest tiles).
Their new birth times then imposed a2s fade. Fixed world-addressed48m near /
192m distant pages replace the moving window. Pages share one source grid per
resident terrain tile; each holds at most6400/25600 candidate slots. Adjacent
pages prefetch half a page beyond range, remain cached one page beyond range,
and are installed near-first, at most2 per frame. Crossing a boundary adds edge
pages, never invalidates all retained pages. Terrain replacement, actual tile
eviction, teleport/reanchor and explicit disabling may still rebuild affected
pages. No zero-latency first-time generation claim. All vegetation/scatter birth
fades are removed in forward/depth/shadow paths; distance LOD/draw-limit blends
remain. Terrain's separately requested temporal replacement fades stay intact.

Derived flora5 adds physical-space(~26/53/167m noise wavelengths) stand/site
variation, conditioned by soil water capacity, gravel, fertility, drainage and
organic content. Savanna/grassland mature height varies in coherent tall/short
stands, with additional broad sparse/bare savanna patches, not missing random
0.60m tufts. Existing mature-height potential still comes from climate/soil.
Explicit Desert now has zero upright forms and only sparse low crust, without
the previous coarse-groundwater exception. Density/occupancy, not height
shrinking to zero, handles presence/absence. This deliberately conservative
Desert classification rule is not a claim that real deserts never have grass.

Tundra/alpine have varied moss/lichen and graminoid/sedge pockets. Moss and
lichen are REMOVED from the leaf-placement/animation pipeline, and rendered as
stationary material layers: separate neutral generated256px cushion/flake
textures, mip/anisotropic filtering, palette tint and rough diffuse normals.
No wind, objects or per-frame simulation for these mats; no true3D moss pillows,
rock/log colonisation or botanical close-up accuracy is claimed. No specialised
flower/shrub/tree model expansion in this pass.

Existing16-float flora grids now accompany all terrain LODs. Runtime derives a
small RGBAHalf material-weight texture from each (total,moss,lichen,upright),
blended with the same terrain colours; these textures remain when local leaves
are disabled. Root buffers still restrict eligible source tile sizes to local
2048m/one-finest-tile limits, preventing grass allocation on planetary fallback
tiles. Grid/material retention adds transient mesh/cache/transport cost, not
save storage; cache accounting includes the new8bytes/vertex mask. Worker wire3
is unchanged, no authoritative world/climate/biome/history/height/water changes.

Ecological references: NPS [prairie variation](https://www.nps.gov/tapr/learn/nature/a-complex-prairie-ecosystem.htm)
describes shortgrass in dry sites/tallgrass in wetter sites and variation with
soil/depth/moisture/slope; [Arctic ecosystems](https://www.nps.gov/im/arcn/ecosystems.htm)
distinguishes moss, lichen and wet sedge/tussock communities; [desert soil crust](https://www.nps.gov/cany/learn/photosmultimedia/inside_soilcrust.htm)
describes stationary mixed cryptogamic surface communities. Noise is a derived
site proxy, not a new fine water-table solver or seasonal growth/fire/grazing
simulation. Owner no-agent-testing instruction remains: compile/package only,
no suites, fixture, game launch, captures or profiling. Owner checks the same
boundary, initial loading, FPS, grassland variation, desert and tundra mats.

## Progressive leaf geometry instead of rejected sprites /21October2026

Owner rejected `player-grass-sprites-tundra`: the visible close-field cutoff
remained. Do not treat the billboard experiment or its compilation as accepted
appearance. `player-grass-progressive-field` removes that resource shader and
uses the SAME procedural leaf shader for the middle distance. No new stock or
AI bitmap art is introduced. This is an adapted pipeline, not Nintendo code.

Research first: Nintendo's exact BOTW far-grass renderer was not established
by accessible first-party material; recreations are not implementation evidence.
AMD's [procedural grass article](https://gpuopen.com/learn/mesh_shaders/mesh_shaders-procedural_grass_rendering/)
demonstrates fractional blade retirement plus inverse-count widening; sparse
billboards are FUTURE WORK, not its demonstrated solution. Eric Wohllaib's
[GDC2021 slides](https://media.gdcvault.com/GDC%2B2021/gdc_2021_procedural_grass_in_got.pdf),
read from this [20MB mirror of the developer's PDF](https://archive.thedatadungeon.com/ghost_of_tsushima_2020/documents/gdc_2021/gdc_2021_procedural_grass_in_got.pdf),
show geometry LODs (p24-27), glancing-angle adjustment (p34-35), calmer distant
normals (p36-37), and a far texture ON TERRAIN (p42-43). Original host returned403;
mirror PDF was downloaded/extracted locally, not inferred from AI summaries.

Implementation: near0.60m fields stay. Middle-distance1.20m fields interleave
four canonical0.60m cells' leaf prefixes with the same deterministic hashes,
species choices and local root offsets. Terrain-grid interpolation/phenotypes
can differ between resident mesh levels; exact cross-LOD heights are not claimed.
Near curves straighten before handoff; near/middle geometry shares complementary
36-45m coverage at defaults. Beyond45m, count falls continuously with inverse
distance squared and fractional last-leaf width, compensated up to24x. Three
GPU compact lists use smaller draw capacities at90/180m without restarting leaf
identities or changing their geometry. No alpha-textured bank replaces the field.
Thin/edge-on leaves get bounded view-space coverage support; normals and albedo
converge to the statistical ground canopy instead of shimmering independently.

Far terrain now has a one-time generated256x256 mipped neutral grass mat:
6000 rasterised bent ribbons, mean-normalised brightness and packed surface
slopes, repeat8m, anisotropic/trilinear sampling, planet-space triplanar mapping.
Regional flora still supplies colour; this is not a species-specific 3D bake.
Mean terrain albedo matches the distant leaves' target, and subtle broad field
variation remains after fine texture detail minifies. The final geometry fade
occupies the last35% of range rather than ending at45m. Default45m CLOSE DETAIL,
350m geometry; debug50-500m geometry, enable/density/detail/wind/off remain live.

Five visible lists grow capacity overhead to116bytes/root slot plus shared
grid/args; distant lattice density increases4x from rejected2.4m banks. The
bounded1152m window can use up to1million slots per clipped large tile, not an
unlimited allocation. Near window192m and coarser sidecar limits remain. More
geometry/memory can cost more than sprites; no FPS benefit is claimed. Texture
creation adds one-time CPU work; the GPU still avoids per-frame readbacks and
per-leaf objects. Prior tundra/desert rules, saves, history, heights and water
stay unchanged. Owner no-agent-testing direction remains: build/package only,
no fixtures, suites, player launches, captures or profiling. Seamlessness,
far-field fullness, minification and performance require owner review.

## Close fill, tundra and upright middle-distance sprites /21October2026

Owner rejected the previous horizontal canopy's appearance and reported tree
shadow flicker. This pass removes those caps, transparent overlay sorting and
distance-based height flattening. Two staggered camera-facing alpha-cutout
banks per 2.4m field square provide actual upright silhouettes, write depth,
and sample tree shadows at their ground anchor. Banks narrow on arrival/exit
instead of screen-door fading. Existing coverage-preserving atlas mips remain;
this is not a bake of each generated species and close grass stays geometry.

Default blade range45m, sprite range350m; debug sprite range50-500m. Sprite
fields are separate from 0.60m blade fields, frustum/range compacted, and use
coarser resident terrain grids too (sidecars retained up to2048m physical tile
size, or one finest tile if larger). Parent fields are suppressed by the
resident finer-coverage tree; fully covered parents allocate no grass buffers.
Near fields use a bounded192m moving window, sprites1152m. Only one field is
installed per frame. This adds transient sidecar/buffer cost, not save storage.
The finest-terrain-only120m ceiling is removed for normal0.1/1 scale settings;
missing terrain/sidecars can still delay or limit coverage at unusual scales.

Within4m, near blade counts double, returning smoothly to baseline by14m;
outer counts/widths stay the same. No random whole-square gaps are introduced.
Ground-flora4 adds explicit Tundra/Alpine selection: reduced cool grass,
no warm grass/fern/succulent, moist-site moss without forest-shade dependence,
dry exposed lichens, wet-site sedges retained, shorter stature and muted
olive/straw/grey palette. This is potential summer cover, not seasonal browning
or finished moss/cushion art; real tundra may also be green and sedge-dominated.
Source biome/climate/history, terrain heights and water rules stay unchanged.

Reference: NVIDIA's [grass billboard chapter](https://developer.nvidia.com/gpugems/gpugems2/part-i-geometric-complexity/chapter-1-toward-photorealism-virtual-botany)
describes camera-facing representations; NPS [Arctic ecosystems](https://www.nps.gov/im/arcn/ecosystems.htm)
distinguishes sedge, tussock, moss/lichen and dwarf-shrub tundra. These motivate
the approach; they do not validate this implementation. Owner requested a quick
pass without agent testing: compilation/packaging only, no player launches,
captures or profiling. All-angle sprite appearance, transitions, memory/GPU cost
and whether the reported shadow flicker is resolved remain owner-review work.


## Hybrid near blades / canopy / far terrain and dry deserts /21October2026

Owner requested replacing grainy distant blades with aggregate LOD objects,
then matching distant ground. `player-grass-hybrid-canopy` retains continuous
near fields but changes representation: default detailed/simplified geometric
leaves end at45m;12-vertex canopy patches take over36–45m and continue to120m.
The very-near blade detail blend is18–27m. Width compensation now accounts for
the near/simplified count ratio, capped3.5x, rather than reducing many leaves
with only slight widening. GPU compaction sends only near roots to blade draws.

The distant proxy is one camera-facing alpha-textured fringe plus a shallow
terrain-following canopy cap per field square, NOT six-way crossed cards near
the player. It reuses the existing RGBA atlas solely at distance; no new image
generation. It is an authored aggregate approximation, not a multiview bake of
each world's actual individual leaves. It uses smooth opacity (no grass
screen-door retirement) and settles height into the ground over the final
canopy range. Near blades retain opaque LOD/load dithering. Transparent proxies
do not write depth or cast individual shadows; overlapping proxies are not
sorted individually, a prototype artifact/performance risk for owner review.

All terrain LODs now mix mean foliage colour using the same cover/.45 visual
occupancy as local fields, with.90 mean colour scaling. Shared leafy-volume
sunlight/transmission is used for blades, canopy and grass-covered land, instead
of fading grass onto unrelated substrate/PBR lighting. Broad grass shading
detail is derivative-filtered; new grass shading is enabled only on the land
material, not rocks or water. This changes display palettes/shading, not heights,
terrain LOD topology or source worlds/history. Fine-cover wire layout remains3.
Far terrain is a shaded statistical surface, not distant individual geometry
or a new grass-volume horizon system. Very tall grass silhouettes eventually
settle into terrain; exact all-angle agreement is not claimed.

Grass debug adds individual blade range15–60m and a canopy toggle; existing
range slider controls the aggregate extent40–160m. Effective blade range is
capped at75% of aggregate range, leaving room for handoff. Defaults45/120m,
High/1x/wind on. Existing finest-terrain coverage may shorten aggregate extent;
far terrain shading still extends to the horizon. The old enable toggle removes
all local geometry/culling/buffers, retaining the statistical terrain display.

Ground-flora3 fixes a second owner observation: desert profiles previously
retained small grass/forb probabilities which the continuous-field renderer
spread everywhere. In explicit dry Desert habitats, only low lichen/crust
remains unless existing groundwater/flood proxies indicate a wet refuge.
Suppression for other forms scales with clamp((flood-.10)/.55). This is derived
flora only; no saved biome, climate or historical ecology is rewritten. Oases
are not newly generated; wet refuges use the existing habitat data. Desert-looking
non-Desert cells and biome-edge blending are not independently reproduced or
certified. Generic geometric non-grass leaves remain provisional.

Owner-testing workflow continues: build/package only. No agent-run test suites,
game launch, visual inspection, FPS profiling or saved-world hash audit for this
revision. No measured performance win, invisible handoff or desert acceptance
is claimed. Earlier renderer descriptions/evidence below are historical.

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
