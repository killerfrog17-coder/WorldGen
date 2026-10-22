# WorldGen repository instructions

Current OWNER-REVIEW launch,21October2026 local/22October UTC: shortcut selects
real C: `artifacts/player-tree-baked-visible/WorldGen.exe`, IL2CPP01:09:26Z.
Owner sees no rebuilt trees in preceding prototype; old-tree A/B restores them.
Root cause: player strips new shader's INSTANCING_ON variants because no asset
advertised runtime instancing. Resources TreeSurfaceMaterial with instancing1
now retained and cloned, matching established scatter path; forward VP4/4 FP8/8,
shadow/depth2/2 retained vs failed VP2/4 FP4/8 and1/2. Packaged GPU fixture exits0
on RTX4060, two instances per draw, broad/needle mesh and baked views all visible
(~28–33k changed pixels/half). Near native capture inspected. No world/FPS suite,
world/save edits or owner game closure; fixture13340 stopped. Previous Editor
checks were insufficient for this player-only issue. Compile/package exit0,
shortcut updated; owner review pending. Prototype scope/limits below unchanged.
See TREE_FLORA.md and VALIDATION.md.

Current OWNER-REVIEW launch,21October2026 local/22October UTC: shortcut selects
real C: `artifacts/player-tree-baked-prototype/WorldGen.exe`, IL2CPP00:56:27Z.
First replacement-tree ART checkpoint: broadleaf bounded space-colonisation and
apical needle anatomy, shared textured shoot atlas, identical foliage locations
across two mesh LODs,16-view relightable actual-model far impostors. One background
CPU bake at a time with complete middle mesh fallback; no per-frame CPU drawing,
GPU readbacks or disk bakes.48-model ~128MiB view cache, additional shared shoot
textures/source pixels. F3 shows progress. Session A/B rebuilt/previous and baked/
mesh-only toggles, explicit60/200m handoff button; existing distances unchanged
unless clicked. Existing tree count/ecology/speciation, exotics' old anatomy,
kilometre forest aggregates, mid plants/grass, terrain/water/history/saves unchanged.
NO denser populations or claimed measured FPS recovery yet. Small isolated D3D11
Editor asset checks pass for two archetypes/16 views/three yaws; initial shader
identifier and duplicated-silhouette depth reprojection corrected before package.
Sampled near/mid broad5250/2298tri, needle14064/3066; far2 after bake. No owner
game/world launched or stopped and no lengthy performance suite. Owner34224 GPU
verified NVIDIA RTX4060 by Player.log and nvidia-smi, not integrated. Compile/
package exit0; actual-world art/LOD/dense-FPS acceptance awaits owner. Old player-
vegetation-performance is rollback. See TREE_FLORA.md and VALIDATION.md.

Current OWNER-REVIEW launch,21October2026: shortcut selects real C:
`artifacts/player-vegetation-performance/WorldGen.exe` (IL2CPP23:46:59Z).
Owner explicitly authorized native benchmarks/profiling for this performance pass,
superseding compile-only for THIS task. Opt-in read-only offscreen1920x1080 suite
holds history/calendar/music and never writes saved settings/worlds. Six sites,
90 confirmation intervals plus81 exploratory/27 final-package/12 final-repeat/
5 diagnostic-repeat intervals; partial exploratory terrain rows are not accepted
steady baselines. Retained fixes: compact live instanced properties, conservative
128m broad phase, identity-matrix skip, exact ground/readiness/occlusion/fog memos,
bounded surface-pixel staging reuse and zero-moving-cover page pruning. Original
per-plant culling/LOD/shadows/density, approved shader/wind and quality defaults
remain. Shader early-rejection experiment was rejected/reverted. Native eight-
heading model/transform/tint/fade/shadow and property-length checks pass; readiness
memos compare to fresh predicates. Final desert108 legacy pages contain0 GPU
roots; pruning removes~68MiB grass buffers, not grass. Read-only snapshot unchanged.
Earlier plain161->202/steep104->123FPS CPU A/B is NOT a general200FPS recovery:
later normal-package plain82->91; unchanged earlier binary now78->87 too. Cause
of cross-run slowdown unproven; do not blame browser or claim build regression.
Final rainforest~106FPS/~136 no grass/~194 no trees;100min/165general targets NOT
achieved. No valid GPU timestamp samples; Update percentiles are not presents.
Build/package exit0, owned tests stopped; owner on-screen review pending. Previous
vegetation-quality-audio is rollback. World/history/saves/heights/water/placement
unchanged; old GPU-reset/AppHang is NOT certified fixed. See
docs/VEGETATION_PERFORMANCE.md and VALIDATION.md for evidence/limits.

Current OWNER-TEST-ONLY launch,21October2026: shortcut selects real C:
`artifacts/player-vegetation-quality-audio/WorldGen.exe` (IL2CPP21:46:55Z).
Game settings adds persistent Vegetation tab: stable per-ID0–100% visible density,
model quality, separate LOD handoff ends and physical draw caps for specialists/
mid plants/trees, separate mushroom range, mid/tree shadow switches plus existing
culling/global shadows/far-forest controls. Quick layer toggles remain in Grass
debug; grass settings/wind/art/placement are unchanged. Render-only invalidation,
not terrain planning/regeneration. Density/draw caps don't change far ecological
cover summaries; shorter tree ranges may expose the existing canopy handoff gap.
Preparation jobs/art caches remain, so controls are NOT generation/memory caps.
Overview/Audio share Master/Music sliders and independent persistent Mute flags
which preserve levels. Music Pause/UnPause holds track/gain/quiet gap throughout
exploration/simulation pause menus, including clip decode completion while paused.
Front-end menu/map behaviour and external soundtracks/effect audio stay literal.
New PlaySettings sun/moon visual defaults3x; explicit saved and legacy fallback1x
remain unchanged. No world/history/height/water, ecology or save/wire schema change.
Final IL2CPP compile/package exit0 including worker. NO agent tests/player launches,
captures/listening/profiling/hash audits. Shortcut updated; owner notified for
settings/FPS/audio review. Leave game/saves untouched; no measured speedup/UI/audio
acceptance. See SETTINGS/EXPLORATION_MUSIC/CELESTIAL_ATMOSPHERE/VALIDATION docs.

Current OWNER-TEST-ONLY launch,21October2026: shortcut selects real C:
`artifacts/player-tree-coverage-culling/WorldGen.exe` (IL2CPP21:28:32Z).
Owner rejects initial tree art/early clumping and reports60–70FPS with trees vs
190 grass-only; not an agent benchmark. Fixed endpoint-only leaf-strip collapse
in tree middle/far and mid-plant far art. Filtered procedural leaf-cluster array
with alpha-coverage mips replaces fragment ellipse/atan2 loops; persistent local
foliage packets replace early broad/needle solid crown blobs. Species materials
shared across variants. Kilometre forest aggregate volumes are unchanged.
Scatter gets wind-inclusive per-instance/patch frustum checks; stable finest
terrain lower bounds conservatively occlude scatter/grass pages behind hills.
Unknown/coarse/fading terrain stays visible; flat scenes early-out. This is NOT
full-scene GPU Hi-Z/Resident Drawer integration or multi-view tree impostors.
Offscreen relevant shadow casters retained separately, including valley drop;
near-horizontal sun can retain many. Batch closures/redundant lookups removed,
buffers reused, stationary batches refresh1Hz/motion invalidates, ground updates
only reproject affected patches. Grass debug adds frustum/occlusion/shadow toggles;
F3 rejection counts are diagnostics, not GPU savings. No placement/density, world/
history/saves, heights/water, wire5 or approved wind changes. Final compile/package
exit0; first package superseded by tightened shadow/flat-scene bounds before
publication. NO agent tests/player launches/captures/profiling/hash audits.
Shortcut updated; owner notified. Leave game/saves untouched. Art/FPS/culling/
shadow/LOD acceptance pending; see TREE_FLORA.md and VALIDATION.md.

Current OWNER-TEST-ONLY launch,21October2026: shortcut selects
`artifacts/player-generated-tree-lineages/WorldGen.exe` (IL2CPP20:54:43Z), real C:.
TreeFlora1/LocalScatter7/wire5. Seeded world founders → province → regional
anatomy; six shared variants/three LODs, folded procedural foliage sprigs, inherited
bark/wood metadata and /trees N. Near proxies replaced, no duplicate old trees.
Separate bounded distant forest crown triangles/colour preserve canopy mass,
not altered ground heights; approximate grouping can shift with terrain LOD/chart.
Ground/mid placement and approved wind remain. No dynamic seasons/growth/poison/
food-web/understory light solve, vines/deadwood or actual multiplayer changes.
Core/Presentation compile and final IL2CPP package exit0. No agent tests/player
launches/captures/profiling/hash audits under owner directive. Leave game/saves
untouched. Owner notified for tree art/LOD/FPS and kilometre-range forest review.
See TREE_FLORA.md and VALIDATION.md; no new art/FPS acceptance. Prior mid-height
checkpoint has owner first-pass appearance approval, not a benchmark certification.

Current OWNER-TEST-ONLY launch,21October2026: shortcut selects
`artifacts/player-mid-flora-ready/WorldGen.exe` (IL2CPP17:05:32Z), real C:.
Compile/package exit0; earlier player-mid-flora is an unpublished intermediate.
Seven procedural structural families/14 regional taxa, four instanced variants,
three geometry LODs, habitat colonies, owner-approved wind and distant material
coverage summaries. `/midflora N` and ecoregion profiles expose reproduction and
fictional relative flesh/seed/leaf hazards; no dose/poison/harvesting/seasonal or
food-web rule upgrade. Existing grass/trees/water/heights/history unchanged.
Wire4 transports six-float mid-cover grids; no save schema change. Far geometry
uses individual cheapest instanced models, NOT merged thicket/canopy proxies or
multi-view impostors. See MID_HEIGHT_FLORA.md for limits. Owner compile-only
direction: no agent tests/player launches/profiling. Leave their game untouched.
Owner notified for art/abundance/LOD/FPS review; no visual/performance acceptance.

Surface-effects foundation,21October2026: Core GroundSurfaceState.cs adds an
UNWIRED query/provider contract (unknown != dry) for independent soil moisture,
surface wetness, snow cover/depth/SWE, actual connected water levels and long-term
inundation/waterlogging exposure. No provider, generation/placement/renderer call,
save/worker schema or new water simulation. See docs/GROUND_SURFACE_STATE.md.
Shortcut remains player-layered-vegetation-wind; owner has approved its wind.
This interface-only checkpoint needs shared-project compilation, not a new player.

Current OWNER-TEST-ONLY launch,21October2026: shortcut selects
`artifacts/player-layered-vegetation-wind/WorldGen.exe` (IL2CPP16:10:19Z), real C:.
Owner approves following documented Crysis/Sucker Punch/SpeedTree architectures:
separate main bend, continuous detail waves and desired/effective controls.
Presentation controller smooths mean vector/gust controls (95% step~6s default),
integrates phase at changing rate and scrolling displacement. Shader70m coarse/
14m fine continuous value noise produces travelling gusts; same coarse input
for grass/herbs/tree proxies. No old FIR/spring texture field or per-blade CPU
physics. Rigid fungi/mats/rocks still. Grass debug A/B checkbox OFF restores
exact direction-only rollback in same build; response slider2–12s, F3 mode/
effective speed. Render-time motion continues in settings/map like old shader.
Geometry/density/placement/LOD/world/history/saves unchanged. Rain retains old
instantaneous sampler; tree rigs/leaf flutter remain future. Session controller
Capture/Restore supports future server state replication, NOT implemented MP
sync/bitwise determinism; join times/frame histories may differ. No imported
SpeedTree code/middleware or claimed exact game reproduction. See primary
sources/limits in PROCEDURAL_WEATHER/GROUND_FLORA/VALIDATION. Compile/package
ONLY, no suite, fixture, launch/captures/profiling; leave owner's game untouched.
Compile/package exit0; no agent player launches/profiling. Owner review pending.

Current OWNER-TEST-ONLY launch,21October2026: shortcut selects
`artifacts/player-grass-direction-only/WorldGen.exe` (IL2CPP15:49:22Z), real C:.
Owner REJECTS all three wind-motion attempts, including wave-envelope, as
identical-looking/jagged and explicitly requests literal pre-wind animation
with ONLY heading changed, also applied to flowers. Ground-cover shader is
source-compared to pre-wind dcf546c: only added direction uniform and replaced
fixed(.8,0,.4) sway axis. Exact old _Time.y, spatial coefficients,1.6/2.7 rates,
.14 strength, intrinsic curvature, Bezier and near/far behaviour are restored.
CPU normalizes broad weather heading to the original axis magnitude sqrt(.8);
wind speed/gust/history/species traits do NOT modify vegetation motion. Calm
still animates, intentionally, like the old shader. Flower/fern shoots use same
scalar and tip-delta curve with root-height mask; fungi/moss stay still. Existing
tree/shrub original placeholder sine restored. Delete experimental response
compute+runtime+metas: no GPU history field/update/texture allocation remains.
Core plant-motion metadata is retained but inactive in rendering. Weather/rain
and F3 remain unchanged; direction-only vegetation again uses Unity _Time, not
a synchronized multiplayer phase. No new MP claim. Source comparison only,
compile/package ONLY: no suite, fixture, game launch/captures/profiling. Leave
owner game/saves untouched. See PROCEDURAL_WEATHER/GROUND_FLORA/VALIDATION.
Compile/package exit0; no agent launches/profiling. Owner review pending.

Current OWNER-TEST-ONLY launch,21October2026: shortcut selects
`artifacts/player-wind-wave-envelope/WorldGen.exe` (IL2CPP15:38:54Z), real C:.
Owner REJECTED preceding spring pass: essentially unchanged jagged/vibrating
motion. Do not infer owner fluidity acceptance from its long buffer/build.
Plant-motion2 restores earlier1.6/2.7rad/s continuous sine-wave character, wind
controls envelope/direction/speed mix/bias rather than instantaneous position.
Positive Hann5/7/8/10s history windows replace short dominant spring impulse;
40x0.25s wind bins supply vector/mean-speed/energy. Slow/nominal/fast fixed
world-clock carrier bands blend without phase restarts. Broad trend smooths
chatter and maintains waves while gusts settle. Strong wind supplies biased
lean with waves, not symmetric strong-wind flapping. Traits/height still vary
response; fungi/mats/rocks still. Existing near/far placement/density/LOD,
weather-forcing/precipitation/history/saves unchanged. Field RGBAHalf pair1/4MiB
before overhead; cost unprofiled. No new spring-physics or bitwise MP claim.
Compile/package ONLY; no suite, numeric campaign, launch/captures/profiling.
See PROCEDURAL_WEATHER/GROUND_FLORA/VALIDATION; leave owner's game/saves untouched.
Compile/package exit0; no agent player/test launch or profiling. Owner review pending.

Current OWNER-TEST-ONLY launch,21October2026: shortcut selects
`artifacts/player-plant-spring-wind/WorldGen.exe` (IL2CPP15:14:40Z), real C:.
Plant-motion1 replaces quasi-static grass/herb targeting with bounded8s wind
history /32x0.25s analytic impulse-weight response. Four underdamped modes blend
by generated species/height response and damping; negative lobes give recovery,
not a perpetually oscillating wind-independent sine. Sustained wind keeps lean.
GPU128/256 four-layer response fields at adjacent absolute10Hz ticks interpolate
in shaders; reconstruct history on load/reanchor, never zero-start per plant.
Field pair0.5/2MiB before driver overhead; extra compute/texture cost unprofiled.
One-second regional parameter snapshots feed deterministic gust-detail history;
session weather-mode transitions retained over window. No new per-plant CPU
physics, wind save arrays, source/history/terrain/water/placement/density/LOD
changes. Existing weather-preview4/wind1 forcing and precipitation unchanged.
Herb traits from independent genotype stream (/flora N); grass uses phenotype,
form/height and canonical individual variation. Fungi/moss/rocks still. Existing
tree proxies NOT upgraded to spring branches. Forward/depth/shadow herb match.
Compute fallback remains quasi-static; no bitwise GPU/network acceptance or
botanical/obstacle-flow calibration. Owner no-agent-testing: compile/package
ONLY, no suite, fixture, numerical impulse checks, launch, captures or profiling.
See PROCEDURAL_WEATHER/GROUND_FLORA/VALIDATION. Leave owner's game/saves untouched.
Compile/package exit0; no agent testing/launch/profiling; owner review pending.

Current OWNER-TEST-ONLY launch,21October2026: shortcut selects
`artifacts/player-weather-shared-wind/WorldGen.exe` (IL2CPP14:43:12Z), real C:.
Adds weather-preview4/wind1: independent sustained strength, gust admission,
duration/envelope and turbulence respond to seasonal wind, smooth synoptic
proxies, heating/cloud and storm activity. Fixed geographic event fields use
seed+position+calendar seconds, NOT process/chunk time. Double anchor phases
and integer slots bind Core CPU and shared GPU formulas; no bitwise GPU or
networking acceptance. Cloud-window timing does not select wind parameters.
Grass and rooted whole-shoot herb deformation share it; fungi/rocks/moss still.
Existing tree/shrub primitive sway and precipitation drift consume same vector.
Forward/depth/shadow scatter match; Grass debug wind toggle covers herbs too,
F3 shows current/mean/envelope, bearing-from and gust chance/duration. Ground
placement/density/LOD/terrain/water/source history unchanged; fog responds to
new mean wind. No atmospheric-pressure/terrain-shelter/leaf-branch simulation,
wind-audio return or calibrated duration histogram. No regeneration/save change.
Owner testing direction: compile/package ONLY; no suite, fixture, player launch,
captures, profiling or hash audit. See PROCEDURAL_WEATHER/GROUND_FLORA/VALIDATION.
Leave owner's game and saves untouched; previous specialist build is rollback.
Compile/package exit0, no agent player/test launch; owner review pending.

Current OWNER-TEST-ONLY launch,21October2026: shortcut selects
`artifacts/player-generated-herb-flora/WorldGen.exe` (IL2CPP14:11:48Z), real C:.
Owner likes stable-page/habitat pass. Preserve its grass renderer/moss/materials,
distance-only scatter fades and existing trees/shrubs/rocks. New Core derived
ground-specialists1 lazily generates10 regional potential taxa with seeded
province founders/regional branches: names/IDs, bounded anatomical grammar,
size, niche traits and fungal role. GroundFloraRegion.Specialists is metadata;
it does NOT mutate saved EcologyResult taxa/foodwebs/history. /flora [N] inspects
regional candidates/traits. No hardcoded named Earth flowers/mushrooms.
Independent detail-scatter channel2 adds climate/soil/shade/salinity/flood/slope
gated physical colonies. No Desert/Ice/water; cold local heights capped16cm.
Ferns prefer damp shade; fungi organic/root opportunities. Root hosts are
proxies, not actual tree-species associations. Geometry is procedurally generated
flower/rosette/spike/fern/cap-and-stipe anatomy; fungi stay still, herbs sway.
Instanced12-22m LOD blend, herbs retire109-140m/fungi51-65m, no timed load fade
or specialist shadow casts.64-bit art/batch keys prevent morphology-code aliasing.
Grass debug adds specialist DRAW toggle (does not stop catalogue/data jobs);
F3 counts them. Existing art cache can retain visited species models. Small
rare blossoms do not tint distant terrain; no flower-carpet LOD is claimed.
Local-scatter-specialists5, ground-flora5, transport wire3. Static potential
bloom/fruit, not dynamic seasons, rain fruiting, populations or real evolution.
No source world/history, terrain/water or saved-world changes/regeneration.
Owner no-agent-testing: compile/package ONLY, no suite, fixture, game launch,
captures, profiling or hash audit. Owner checks art/abundance/loading/FPS.
See GROUND_FLORA.md/VALIDATION.md; leave owner's game and saves untouched.
Unity build exit0 and worker/manifest packaging completed. The outer PowerShell
console-title pipe error occurred after packaging; it was not a Unity failure.

Current OWNER-TEST-ONLY launch,21October2026: shortcut selects
`artifacts/player-ground-habitats-stable/WorldGen.exe` (IL2CPP12:54:21Z), real C:.
Owner likes progressive field appearance/performance, including15m transition
on flat ground. Keep that renderer; this is owner scene feedback, not benchmark.
Identified64m reset: floor(player/64) change destroyed near buffers on >64m
tiles, then2s birth fade restarted. Fixed48m near/192m distant pages retain
overlap, prefetch half-page/evict one-page beyond range, share one grid/tile,
and install2 pages/frame near-first. First-time loads/actual terrain replacement
can still rebuild affected data. Grass/scatter birth fades removed; distance
blends stay in colour/depth/shadows. Terrain temporal crossfades are unchanged.
Derived flora5 adds soil-supported physical-space tall/short/sparse stand
variation, explicit grass-free Desert with sparse crust (no groundwater
exception), and tundra moss/lichen/sedge pockets. Moss/lichen no longer enter
leaf geometry: separate stationary generated material/normal textures and
per-tile RGBAHalf weights handle them. All terrain LODs retain16-float flora
grids; local root-source size cap remains2048m/finest-tile. Wire3 unchanged;
transient grid/mask/page costs increase, not save storage. No source world,
climate/biome/history, height or water changes; no specialised flowers added.
Owner no-agent-testing direction: compile/package ONLY, no suite, fixture,
game launch, captures, profiling or source-hash audit. No new runtime/FPS or
realism acceptance. See GROUND_FLORA.md/VALIDATION.md. Leave game/saves untouched.
Compile/package exit0; shortcut updated. Owner boundary/habitat/FPS review pending.

Current OWNER-TEST-ONLY launch,21October2026: shortcut selects
`artifacts/player-grass-progressive-field/WorldGen.exe` (IL2CPP12:16:48Z), real C:.
Owner REJECTED prior sprites for their obvious close-field cutoff. Billboard
resource removed; same procedural leaf shader now covers the middle distance.
Research: AMD demonstrates fractional counts/inverse-count widening, billboards
are only proposed future work. Original Tsushima developer slides show geometry
LODs, glancing-angle support, calmer distant normals and a far texture ON terrain;
PDF read from mirror after original host403. Nintendo BOTW's exact far technique
is NOT established; don't claim this is a Nintendo reproduction.
Near0.60m fields hand over to1.20m fields interleaving canonical microcell leaf
prefixes, progressively reducing counts and compensating width. Five GPU lists,
capacity-only boundaries at2x/4x close distance; distant identity/geometry persists.
Filtered neutral bent-ribbon grass mat and matched mean terrain albedo replace
flat colour-only far ground. Defaults45m CLOSE DETAIL /350m total geometry;
debug50-500m geometry plus enable/density/detail/wind/off. Previous tundra/desert
rules stay; no saved-world/history, height, water or worker-wire change.
More geometry/root capacity may cost more than rejected sprites:116bytes/slot,
up to1million slots per clipped large distant tile. No FPS/memory gain claimed.
Owner no-agent-testing directive remains: compile/package ONLY, no suites,
fixtures, game launch, captures or profiling. No seamless/realistic acceptance.
See docs/GROUND_FLORA.md and VALIDATION.md. Leave owner's game/saves untouched.
Build/package completed exit0; shortcut updated. Owner visual/FPS review pending.

Current OWNER-TEST-ONLY launch,21October2026: shortcut selects
`artifacts/player-grass-sprites-tundra/WorldGen.exe` (IL2CPP11:54:36Z), real C:.
Owner reported green meadow-like tundra, gaps at feet and rejected the prior
horizontal canopy's appearance/shadow flicker. Derived ground-flora4 adds
shorter muted Tundra/Alpine mixes with moss/lichen, wet sedges and reduced grass.
Close blade counts double within4m, taper to baseline by14m. Upright staggered
camera-facing depth-writing alpha-cutout banks replace transparent caps and
height settling; ground-anchored shadow samples avoid moving shadow overlays.
Sparse2.4m sprite fields are separate from0.60m blades, support coarser resident
grids and suppress covered parents. Default45m blades/350m sprites, debug50-500m.
Sidecars retained on local tiles up to2048m physical size (or one finest tile
if larger); bounded192m blade/1152m sprite windows, one field install/frame.
Transient memory/load cost increases; no save raster, authoritative ecology,
world/history, terrain-height or water changes. Existing atlas reused distant
only; no return to near crossed cards, no finished moss art or seasonal colours.
Owner no-testing instruction remains: compilation/packaging ONLY, no test
suites, fixtures, game launch, captures, profiling or source-hash audit. No
shadow-fix, seamless transition, realism or performance acceptance is claimed.
See docs/GROUND_FLORA.md and VALIDATION.md; leave owner game/saves untouched.
Build/package completed exit0; shortcut updated. Owner review is pending.

Current OWNER-TEST-ONLY launch,21October2026: shortcut selects
`artifacts/player-grass-hybrid-canopy/WorldGen.exe` (IL2CPP07:27:24Z), real C:.
Owner requested near blades (~40–50m), cheaper distant representations, matching
far terrain, and no inappropriate desert grass. Default45m individual blades
(detail blend18–27m) hand over36–45m to12-vertex fringe/canopy proxies through
120m; new shared leafy lighting/occupancy-correct colour extends across all
terrain LODs. Proxies use smooth alpha, not screen-door fades; near blades
still dither. Existing atlas is reused DISTANT ONLY, no near crossed cards.
Proxy transparency/order, height settling and exact all-angle agreement remain
unverified. Near range/canopy toggle join existing live Grass debug controls.
Finest terrain can shorten proxy range; far shading continues to horizon.
Derived ground-flora3 suppresses non-crust forms in explicit dry Desert cells
unless saved groundwater/flood proxies indicate a refuge; no new oasis system.
Source world/history/climate/biomes, heights and water rules unchanged; derived
display colours/flora intentionally change. Worker wire remains3.
ONLY compilation/packaging performed, exit0. No suites, fixtures, native launch,
screenshots, FPS profiling or hash audit, following owner no-testing direction.
No measured speedup or accepted desert/realism/transition claim. Prior continuous
field build remains technical rollback. See GROUND_FLORA.md and VALIDATION.md.
Do not close owner game; retain continuous near fields rather than sparse tufts.

Current OWNER-TEST-ONLY launch,21October2026: shortcut selects
`artifacts/player-grass-continuous-field/WorldGen.exe` (IL2CPP07:03:18Z), real C:.
Owner found blade performance acceptable but REJECTED isolated tufts and the
large bare gaps; increasing leaves per clump did not solve distribution.
This revision fills adjacent0.60m field squares with low-discrepancy leaf roots,
removes random whole-tuft cover rejection, uses per-leaf species mixtures and
occupancy, and retires shared per-tuft random stature. High now64/21/5 leaves,
Low/Medium/Ultra near16/32/96; density scales the distributed field. Root records
96bytes plus12bytes/index capacity (108bytes/slot) with previous GPU culling,
fades, live debug controls, local disable and distant cover colour retained.
Sparse habitats still thin leaves through actual cover; no grass over erased
water/ice/cliff roots. No source ecology/world/history/terrain/water changes.
ONLY compile/package work performed, as owner requested: no tests, game launch,
captures, profiling or source-hash audit. Previous performance feedback does
not validate this denser revision. Owner tests fullness, realism and FPS.
See GROUND_FLORA.md and VALIDATION.md; prior blade player is technical rollback,
not accepted field coverage. Do not close owner game or revert to crossed cards.

Current OWNER-TEST-ONLY PROTOTYPE launch,21October2026: shortcut selects
`artifacts/player-grass-blades-prototype/WorldGen.exe` (IL2CPP06:50:07Z), real C:.
Owner REJECTED crossed-card grass as visibly flat/star-shaped, and requested
a quick AMD/Tsushima-inspired individual-blade prototype with NO AGENT TESTING.
Only compilation/packaging performed: no test suites, native fixture, player
launch, captures, profiling or saved-world audit. Old card test counts below
do NOT validate this renderer. Await owner appearance/performance review.
Ground-flora2 ecology/tallgrass and all source/history/water/height definitions
remain unchanged. Existing0.60m root patches now seed independent curved leaves;
GPU frustum/range culling compacts three root-index lists before indirect draws.
No per-blade objects/readbacks, hardware mesh shaders or individual shadows.
Pause > Game settings > Grass debug: session-only on/off,density0.25–2x,
range40–160m,four detail levels,wind,reset. DefaultsHigh/1x/120m. Disabling
releases local buffers/draws; distant terrain colour remains; re-enable fades2s.
Existing full-detail coverage can bound range. Non-grass forms use provisional
geometric leaves/mats, not finished fern/flower art. No 1–3ms/FPS or invisible
LOD claim. Prior dense-foliage player is comparison/technical rollback only,
NOT accepted grass art. See GROUND_FLORA.md and VALIDATION.md. Do not close
the owner's game or silently revert to crossed cards.

Current OWNER REVIEW launch,21October2026: shortcut selects
`artifacts/player-ground-foliage-dense/WorldGen.exe` (IL2CPP06:22:38Z), real C:.
Owner requested full textured ground cover instead of the preceding sparse fans,
and tallgrass in suitable prairie/savanna. Ground-flora2 adds site-dependent
stature and continuous broad stands; dense RGBA banks replace skinny ribbons.
Three crossed-card LODs54/36/12 vertices,0.60m candidate spacing, existing
distance/load fades, bounded384m large-tile window and450k-root safety cap.
Built-in imagegen atlas/provenance in docs/GROUND_FOLIAGE_ASSET.md; primary
NVIDIA/NPS/Unity research in GROUND_FLORA.md. Texture coverage mips are enabled;
global MSAA unchanged. Trees/shrubs, heights/water and world/history unchanged.
5863 saved200k flora checks and177346 final packaged-worker comparisons pass;
native GPU fixture exits0 with22907 roots/539206 changed pixels/1.85MiB.
Near/overhead/eye-height tallgrass captures inspected on white fixture ground,
NOT actual-world art/FPS, botanical realism or invisible-LOD acceptance.
Denser alpha overdraw can cost more despite fewer near vertices per root.
Own tests stopped; owner review pending. Prior ground-flora-reviewed is rollback.
No seasonal growth, clearing/grazing, persistent per-blade IDs or dynamic
evolution added. See docs/VALIDATION.md. Do not close the owner's game.

Current OWNER REVIEW launch,21October2026: shortcut selects
`artifacts/player-ground-flora-reviewed/WorldGen.exe` (IL2CPP05:58:13Z), real C:.
Ground-flora1 derives nine ground-cover growth forms and seeded province-founder/
regional-branch candidates from existing saved ecology/soil. Continuous support
and physical-space patches replace sparse placeholder grass; existing trees,
shrubs, rocks, terrain heights, water and world/history hashes are unchanged.
All terrain LODs carry mean cover color. Only finest tiles retain264KiB/64seg
root grids; worker wire3 matches. GPU compact clumps reuse drawn-grid heights,
exclude actual water triangles conservatively, use7/4/2 blades and3/2/1 segments,
fade at25–42/65–90/130–160m and load over2s. No plant GameObjects/readbacks in
normal rendering; no new fine saved raster. Terrain toggle disables nearby
cover only, F3 reports draw/buffer capacity. Smallest world scales can run out
of level0 coverage before160m; chart reanchors resample transient clumps.
5859 flora/4037 scatter checks pass; saved200k source unchanged. Flora-only
cold catalog+200k profiles41.8ms, total terrain+flora prep1.35s/259.5MiB retained
on host, not a native full-load/FPS guarantee. Native GPU fixture11433 clumps/
70812 changed pixels passes; white-plane captures inspected, NOT actual-world
art/performance acceptance. Owner notification sent; own test players stopped.
177346 final packaged-worker comparisons pass, including a fine flora sidecar.
See GROUND_FLORA.md and VALIDATION.md. Prior fps-average is rollback. Dynamic
evolution, seasonal phenology, fine botanical art, clearing/grazing/trampling,
per-tree shade, and a shrub/tree overhaul are NOT implemented by this pass.

FPS HUD follow-up,21October2026: `artifacts/player-fps-average/WorldGen.exe`
is the current shortcut checkpoint (IL2CPP21Oct05:14:09Z). F3 counts
Update frames over realtime-clock half-second windows and holds the rounded average
between updates; no frame limiter, VSync, terrain or world changes. Extracted
production-method checks cover publication timing, averaged rates, held display
after one slow frame and zero elapsed time. Prior surface-refined is rollback.
An independent PresentMon capture of the owner's preceding running player
records895 presents/~89.79FPS, sync interval1, median11.1132ms/present and
6.9882ms GPU busy. This confirms real half-refresh pacing for that VSync-on
view, not a target value of90 or merely the F3 number. Subsequent VSync-off/
target200 capture records1359 presents/~136.04FPS overall,113–156FPS per-second
windows, sync interval0: no unconditional target-halving. Owner's specific
angle-dependent100FPS lock is not yet reproduced. No preferences/game changed
by our diagnostics.

Current OWNER REVIEW launch,20October2026: shortcut selects
`artifacts/player-surface-refined/WorldGen.exe` (IL2CPP21Oct04:51:06Z), a real C:
folder. Derived Hyper12 adds bounded cubic reconstruction of the existing8m
height substrate and restrained27m/8m physical-scale detail. Render/controller
share the evaluator; bare river-profile inputs, water datums/gradients, actual
beds and standing shore edges remain protected. Source/history/saves unchanged.
Terrain uses three-second complementary opaque dither transitions, retained
fallback, nested timing/coverage, same-key previous products (cap32), coarsening
and a12m-minimum immediate-ground bubble. Scatter has spatial LOD/range blending
and patch fade-in, with matching depth/shadow clipping. Existing art/density and
LOD quality settings remain; no vegetation overhaul or new world erosion.
8874 surface/transition,6241 exact reuse,13059 coverage-navigation,243 water
checks pass;6 actual lake shores/1934 edges and308 submerged ocean points pass.
127639 final packaged-worker values and8 native/CLR height probes match. Final
GPU incoming/outgoing half-fades preserve coverage and nested/coarsening checks
pass. Actual0.1x30km near reviews: mountain8.225s/plain1.160s after sampler init;
worst21.95/23.31ms, no >100ms in these hidden fixed-camera intervals, NOT broad
FPS/travel acceptance. Distant coverage is PARTIAL18/1819 and16/298. Captures
inspected: mountain is a steep river-side view with visible dither; plain is
mostly water and is NOT dry-flatland art acceptance. No realism certification.
Owner test snapshot is read in its recoverable trash without restoring it.
Own tests/workers stopped; owner's current game was not closed. Prior adaptive-
terrain remains rollback. See docs/SURFACE_DETAIL_TRANSITIONS.md and VALIDATION.md.

Owner-authorized artifact retention,20October2026: playable build folders now
retain only the newest per local calendar date plus newest three overall.
Current adaptive-terrain, terrain-reuse, lod-coverage-debug and daily drainage-
fractal are restored as real C: folders (all copied file SHA256 hashes checked),
alongside daily local-scatter, history32-sovereignty34 and realms-autowarp.
Historical launch notes below do NOT imply those old build folders still exist.
All deletions are confined to artifacts: obsolete real C: builds and nonrecursive
junction removal only. E: original data and the separate music junction are not
deleted or relocated. World saves, non-player evidence, source and audio remain.
Use scripts/prune-player-checkpoints.ps1 dry run for inspection; an actual future
cleanup needs owner authorization. Detailed operation manifest is ignored
artifacts/checkpoint-retention.json. Do not recreate E: build junctions to work
around space pressure; build on C: and review obsolete outputs with the owner.

Current OWNER REVIEW launch,20October2026: shortcut selects
`artifacts/player-adaptive-terrain/WorldGen.exe` (IL2CPP21Oct03:42:45Z).
Owner-authorized steps4/5 add estimated screen-error terrain selection with a
protected fine radius, sparse coarse backdrops and coverage-driven haze reveal.
Five quality levels, four bounded loading efforts, view/fine distances and
Overview/Display/Terrain/Audio/Atmosphere tabs are machine-local. Master volume
is shared across tabs; footsteps/rain/thunder stay in Audio. Startup/checkbox/F11
fullscreen requests the current monitor dimensions; F11 also runs in exploration.
Hyper11 terrain/water, controller, world/history and vegetation are unchanged.
1298 adaptive/6241 reuse/13059 coverage-navigation checks pass;4 native/CLR probes
match;127639 final packaged-worker values and native production-shader/cache
checks pass. Owned tests/workers stopped; owner game untouched.
Native owner0.1x60km High/Balanced: near11.610s incl worker, horizon reveal
15.016s, full320 tiles46.395s; fixed-camera worst35.01ms/no >100ms. Same-view fixed
planner baseline: near10.907s/full416 tiles36.376s. New High is slower in this
scene, not a loading-speed win. Baseline's2.191s fog-distance report with zero
tiles is NOT horizon readiness. Existing owner game stayed open; not isolated
benchmark/full200km/travel/FPS acceptance. Actual terrain capture is inspected
but weather-obscured. Hidden settings captures are black, NOT UI-layout evidence;
actual fullscreen switching and visible quality/tab layout await owner review.
Estimates are not certified geometric bounds; all seam/popping elimination is
not claimed. Historical controls4 player relocated intact to E: with original
path junction/hash verification for build space; no saves/source deleted.
See docs/ADAPTIVE_TERRAIN.md and VALIDATION.md. Prior terrain-reuse is rollback.

Current OWNER REVIEW launch,20October2026: shortcut selects
`artifacts/player-terrain-reuse/WorldGen.exe` (IL2CPP21Oct02:46:20Z).
Owner-authorized LOD step2 adds chart-local inactive mesh LRU (128 products /
96MiB accounted buffers), worker-owned32768 exact-point sample reuse, indexed
literal stitch/ancestor coverage lookup and bounded movement-ahead preloading.
Immediate terrain remains first; two worker lanes, one installation/activation
per frame and two-tile deferred retirement remain bounded. Edge-obsolete work
cancels; still-valid prepared jobs adopt their new chart stamp. Teleport/chart
adoption clears projected mesh cache; no stale cross-chart geometry reuse.
Hyper11 terrain/water, normal LOD detail/selection, controller and accepted
partial-coverage shader are unchanged. F3 exposes cache/request counters.
6241 shared reuse checks /13059 pure coverage/navigation checks and127639 exact
packaged-worker comparisons pass; final3 native/CLR point probes pass. Native
production-shader cache restore/invalidation checks pass. Actual owner200k0.1x
near readiness:9.217s cold,0.542s after500m,0.004s return (resident reuse, zero
inactive-cache takes in this route). An unvisited westward probe queues1
predictive request. Source hash unchanged. Returned screenshot inspected but
largely fog-obscured; only42/492 tiles resident, NOT far-quality, travel/FPS or
full-distance acceptance. Native harness drafts wrongly required inactive hits
while resident data remained, then aimed prediction at visited terrain; corrected
to distinguish reuse and aim at unvisited terrain. Own tests stopped; owner's
running preceding player is untouched and owner review is next. Two historical
generated controls2/3 players relocated intact to E: with original-path junctions
to relieve compiler disk pressure; no saves/source/rollback content deleted.
Previous lod-coverage-debug remains rollback. See docs/TERRAIN_REUSE.md / VALIDATION.md.

Current OWNER REVIEW launch,20October2026: shortcut selects
`artifacts/player-lod-coverage-debug/WorldGen.exe` (IL2CPP21Oct02:06:03Z).
Owner-authorized LOD step1 only: sparse tile-local resident coverage masks stop
coarse ground/water obscuring loaded finer tiles; missing fine areas retain
fallback. Whole-tile retirement/planner/jobs/concurrency/reanchor preparation,
Hyper11 terrain/water heights and authoritative worlds/history are unchanged.
This new explicit coverage request supersedes the historical rejected-renderer
ban ONLY for this focused mechanism, not its old cache/morph/grounding changes.
F3 adds geographic compass/bearing; optional Rivers map estimates beside Clouds
admit smaller waterways with zoom. `/tp lat lon [height]`, `/tp random`, `/tp
random land` (`/teleport` alias) use the F3 official meridian and place the player
above ground/water. Random-land selection is bounded, verifies a dry local
sample, and does not promise uniform sampling of all marine islets.13063 shared
checks and892 sky/chat checks pass. Final native production-shader pixel/capture
checks show fine-only loaded quadrant, retained missing-area fallback and
restored fallback after fine removal. Earlier02:01 package native commands/F3/
async map/toggle checks preserve owner source hash; final map-only admission
tuning is shared-tested (209/131072 occupied global pixels). No duplicate native
command campaign for that threshold-only change. Map/GUI art, broad mountainous
travel/FPS, all-LOD seams and future quality/detail improvements await owner
review. Own tests stopped; owner testing now; no saves altered or owner game
closed. Lake-inlets remains rollback. See docs/LOD_COVERAGE_DEBUG.md / VALIDATION.md.

Current OWNER REVIEW launch,20October2026: shortcut selects
`artifacts/player-lake-inlets/WorldGen.exe` (IL2CPP21Oct01:12:03Z).
Hyper11 fixes dry lake-owned collars prematurely stopping paired channel cuts
and suppressing river ribbons. Actual submerged lake water takes precedence;
dry collars now permit finite cut-only channels. At/below a receiving lake
datum the cut retains its flat lake level/body and updates its depth mask.
No source/history, ocean Core, LOD/controller or shading changes. Owner snapshot
12 inlets/2526 points:62 dry samples before, zero after; two actual cutoff
meshes cover their probes. Original outlet regression,12 fixture lake shores /
3119 contained edges,308 owner submerged ocean points,127639 final worker values
and8 exact native/CLR probes pass with unchanged source. Two native inlet images
inspected: lake277 channel connects into flat standing water; lake186 view is
largely water/shore with limited channel context. Art seams/jags remain visible.
Native near7.971s cold/.696s warm, prep319.8ms; PARTIAL distant281/276 of492
tiles. These hidden fixed-camera views are not all-inlet/LOD/travel/performance
acceptance. Own tests stopped; owner is testing now. Previous lake-spill player
remains rollback. See docs/LAKE_SPILL_CONNECTIONS.md and VALIDATION.md.

Current OWNER REVIEW launch,20October2026: shortcut selects
`artifacts/player-lake-spill/WorldGen.exe` (IL2CPP21Oct00:40:04Z).
Hyper10 repairs the owner's saved lake277/outlet94291 in seed11610541,0.1scale:
legacy initial incision had removed the spill beneath its fixed328.068m lake.
Preserve the pre-amplification spill lip below unincised source terrain, anchor
the paired outgoing river at the exact lake datum, then cut a finite scaled
mouth. No water-to-ground warping, lake deletion or source/history changes.
Owner's approved snapshot/session are restored to the active library with exact
bytes and pose preserved. Saved-point ground294.205->328.904m; near lake mesh
is now0.84m below that ground, not roughly34m above. Six source controls remain
wet. Final native saved-interface and retained-lake images inspected: overhead
sheet gone in saved view; retained-lake view has limited shore coverage. Shading
seam/thin terrain strip remain. Near8.178s cold/.387s warm; PARTIAL distant
coverage274/288 of492 tiles, NOT full-distance/travel/FPS acceptance.322.5ms
native terrain prep,8 exact native/CLR probes,127639 packaged-worker comparisons,
12 fixture lake shores/3119 contained edges and460/343 ocean points pass.
LOD/controller, jag artifacts, water art and confluence shading are unchanged.
Own tests stopped; owner review is next. Previous riverscapes-final is rollback.
See docs/LAKE_SPILL_CONNECTIONS.md and VALIDATION.md. Do not close owner's game.

Current OWNER REVIEW launch,20October2026: shortcut selects
`artifacts/player-riverscapes-final/WorldGen.exe` (IL2CPP23:26:33Z).
Hyper9 replaces OWNER-REJECTED Hyper8 unlimited river potentials/terrain-triangle
water interpolation with bounded paired channel/bed sections and explicit graph
junctions. Actual mitered sections constrain near terrain; profiles are monotone,
bank-conditioned after local cascade replacement, and cut-only. Water never bends
to ground. Visual widths/discharge account for horizontal scale; extra subdivision
tributaries do not independently render as full-sized rivers. Accepted lake7/ocean
hierarchy rules are restored. Standing dry collars exclude independent overlays;
zero-level mouths use sea ownership. Demand-driven route rejection and a far-water
size threshold replace a rejected eager-preparation experiment (7.3s distant jobs).
LOD scheduler/controller/world/history are unchanged. No global fine raster saved.
75226 generated/177063 Earth focused checks,12 lake shores/3111 interior edges,
460/343 submerged coastal points and103382 exact final bundled-worker comparisons
pass. One generated stream's final native overview/near-bank images are inspected:
no giant blue walls in these views, but thin distant water has gaps. Native quick
review is PARTIAL distant coverage (278/433 of492 tiles), not full-distance or
travel/performance acceptance. Near5.909s cold/.011s warm; worst16.15/14.60ms in
these hidden1280x800 fixed-camera intervals, not universal FPS. Own players/workers
are stopped; owner's previous player is untouched and owner is testing now.
This is a CPU paired-profile adaptation of PG2019, NOT the full authoring/Rosgen/
animated blend-flow implementation. True3D waterfall sheets, braiding, all-mouth
integration, fine tributary catchments and finished art remain open. See
docs/RIVERSCAPES.md and VALIDATION.md. Lake-basins is the accepted rollback;
river-primitives is owner-rejected, NOT a safe river realism baseline.

Historical OWNER-REJECTED river-primitives launch,20October2026: shortcut selected
`artifacts/player-river-primitives/WorldGen.exe` (IL2CPP22:20:47Z).
Hyper8 adds cut-only compact riverbed envelopes, monotone reach-scale profiles,
bounded short-tributary discharge, amplified-channel near tessellation with
stitched grid edges, shared dry reach datums and explicit receiving-body joins.
River-only single-pass moving ripples/glints/turbulent highlights replace flat
colour; standing-water geometry rules/appearance, source/history and LOD scheduler
remain unchanged. This is an initial Riverscapes-inspired integration, NOT the
complete paper implementation or detached waterfall sheets/spray. 9463 primitive,
243 water regressions,720 wet/downhill actual route samples,2715/1877 generated/
Earth contained bank edges and151691 exact worker comparisons pass;8 native/CLR
probes match. Actual native overview/bank images inspected: broad lowland water,
visible flow/detail seams, limited bank coverage; owner visual review is next.
Near5.694s cold/.463s warm; full44.464/37.170s, slower than lake7. Hidden fixed-
camera worst27.20/16.53ms, no >100ms; NOT broad travel/fullscreen performance
acceptance. Sampled accepted Earth lakes/oceans pass; omitted lake7 candidates
remain omitted. Coarse water/fine potential deviations, widths, close-reach
transitions and full morphology remain open. Own tests stopped; owner's game
untouched. Lake-basins remains rollback. See docs/RIVER_PRIMITIVES.md / VALIDATION.md.

Current lake-basin REVIEW launch,20October2026: shortcut selects
`artifacts/player-lake-basins/WorldGen.exe` (IL2CPP21:15:29Z).
Hyper7 adds connected coarse sublevel lake components with fixed body datums,
dry-divide refinement constraints and terrain-clipped two-metre hidden underlap.
NO terrain-following water returns; ocean rules, LOD/controller and source/history
remain unchanged. Random unvalidated R3 ponds are removed; open lake candidates
are omitted, not solved. Generated/Earth retain24/27 basins with4/9 rejected;
derived arrays add4.58MiB and graph preparation measured35-80ms.243 focused,
1873/1020 exposed target-lake shore edges,459/341 submerged ocean probes,
105924 exact worker comparisons and8 native/CLR heights pass. Native highland
lake captures are inspected but largely flat-water views with limited shoreline
coverage, NOT broad visual acceptance. Hidden fixed-camera worst17.58/16.33ms;
no fullscreen/travel/low-end performance acceptance. Rivers/outlets and a complete
small-pond catalog remain unfinished. Owner review is next; ocean-underlap remains
rollback. See docs/LAKE_BASINS.md / VALIDATION.md. Do not close the owner's game.

Current review launch,20October2026: shortcut selects
`artifacts/player-ocean-underlap/WorldGen.exe` (IL2CPP20:08:39Z).
Owner approved hyper6 oceans but requested hiding tiny shoreline gaps. Ocean-
adjacent mesh polygons now overlap two horizontal metres underneath shore land;
gradient-based allowance avoids excessive spread on shallow beaches. Ocean-only
2.5cm display lift is removed; the sea remains at zero level without warping.
Wholly dry triangles do not start underground water.125 focused assertions pass,
including shallow/steep underlap distance, constant height, dry rejection and
existing no-warp tests. This is presentation-only: Core hyper6, terrain, inland
water, world/history, LOD and controller are unchanged. Owner visual review is
pending; no new native screenshot/performance acceptance. Unwarped remains the
rollback. Lakes still have owner-reported uncontained edges and rivers remain
unfinished. See docs/WATER_CONTAINMENT.md / VALIDATION.md.

Current EXPERIMENT launch,20October2026: shortcut selects
`artifacts/player-water-unwarped/WorldGen.exe` (IL2CPP17:26:08Z).
Owner explicitly requested removing water-to-terrain adjustment and examining
remaining river defects separately. Hyper6 removes the ground-driven water
blend and shared-potential mesher bypass. Standing lakes use exact basin levels;
oceans use the hyper4 sea-level/body-specific rendering path. Rivers again use
actual channel-reach levels/gradients, not interpolated dry-ground potentials.
Terrain-only cut profiles, LOD/controller and source/history rules are unchanged.
111 focused checks include stale-flag/dry2000m-potential standing-water regression;
459 generated/340 Earth submerged ocean points retain zero level and source hashes;
105924 exact bundled-worker mesh/scatter/index/bounds/cancellation comparisons pass.
This is an isolation experiment, not completed hydrology: independent river masks
and basin interfaces can expose gaps/floating edges. No new native visual or
broad performance acceptance is claimed; owner review is the next step.
The owner REJECTED hyper5 below after finding warped lakes AND oceans; its
previous successful edge checks/selected screenshots are not realism acceptance.
See docs/WATER_CONTAINMENT.md and VALIDATION.md. Do not close the owner's game.

Current CHECKPOINT launch,20October2026: shortcut selects
`artifacts/player-inland-containment/WorldGen.exe` (IL2CPP17:04:40Z).
Hyper5 uses the open reference's paired shared-vertex terrain/water profiles;
actual drawn-land intersections replace independently truncated inland masks.
Cut-only river collars retire both fields; standing lake datums remain constant
through submerged peripheries. Initial raised-bank and sloping-lake experiments
were rejected before publication.109 focused checks,200k generated/Earth audits
of12232/13848 water vertices and1546/796 contained shoreline edges pass with zero
sampled terrain raising. Both worlds pass13110 hierarchy/drainage checks;745
controller/LOD checks,110460 exact worker comparisons and8 exact final native/CLR
height probes pass. Final native river/lake BANK images are inspected; no sheets
or uphill lake ramp in these selected views. Controls359ms, near5.715/0.215s,
full29.593/24.995s, worst21.03/17.60ms with no >100ms frames in hidden1280x800
fixed-camera diagnostics, NOT broad performance/shoreline realism acceptance.
Owner-approved ocean4 remains rollback;459 generated/340 Earth submerged sea
points still pass. Distant linear water can substantially deviate from the fine
surface; scale/tributary tuning, every-basin hydraulics and water art remain open.
No generation/history, LOD scheduler or controller changes. Own tests are stopped;
owner inland review is pending. See docs/WATER_CONTAINMENT.md and VALIDATION.md.

Current CHECKPOINT launch,20October2026: shortcut selects
`artifacts/player-ocean-containment/WorldGen.exe` (IL2CPP16:31:14Z).
Hyper4 repairs source-connected ocean coverage across subdivided submarine
island slopes; node Kind is no longer the sea boundary.106 focused checks
include the reproduced old -67m dry-shelf failure;459 generated/341 Earth
actual coastal submerged points retain sea level, with unchanged source hashes.
Two native shore/shelf captures are inspected; native controls313ms, near5.039s
(first)/0.180s (warm), full29.043/24.404s; worst16.69/14.63ms in hidden1280x800
fixed-camera diagnostics, not broad performance acceptance. Inland water geometry
is UNCHANGED and still owner-rejected for uncontained edges. This is ocean-only,
not finished hydrology. R.A.M manual reviewed: runtime/collider APIs exist, custom
mesh carving/planet compatibility not established; owner chose open-source
reference instead of purchasing. See docs/WATER_CONTAINMENT.md. Prior water-
coupled remains rollback; existing generation/history/LOD/controller unchanged.

Current review launch,20October2026: `WorldGen.exe - Shortcut.lnk` selects
`artifacts/player-water-coupled/WorldGen.exe` (IL2CPP15:56:44Z).
Local-terrain-hyper-3 / amplification3 adds body-aware wet footprints, connected
river identities, bounded local river surface gradients, independent footprint/
drawn-ground clipping and continuous cut-only compact beds. The old mesher's
dry-corner2000m-potential floating-sheet failure is reproduced and regression-
tested; unrelated standing levels no longer interpolate together. An attempted
nearest-reach bed cut failed continuity and was corrected before publication.
Both200k worlds pass13110 hyper checks;15 new water/745 terrain/3227 legacy
hydrology checks,105924 exact worker comparisons and8 native/CLR probes pass.
Native generated river/lake captures are inspected; loading worst9.66/44.32ms,
no >100ms frames in these two hidden fixed-camera scenes, NOT a broad FPS claim.
All owned tests/workers stopped; old hyper-continuous remains rollback. Existing
world/history hashes, mountain controls and LOD/controller behavior are unchanged.
Owner visual testing is pending. Coarse jagged shorelines, distant narrow-water
loss, scale-dependent water widths and short tributaries remain open; no dynamic
floods, underwater art or complete hydrology acceptance is implied. See
docs/HYPER_AMPLIFICATION.md and docs/VALIDATION.md.

Current review launch,20October2026: `WorldGen.exe - Shortcut.lnk` selects
`artifacts/player-hyper-continuous/WorldGen.exe` (IL2CPP09:57:28Z).
Source f263643 / local-terrain-hyper-2 implements drainage-aware CPU planetary
amplification with canonical conforming heights, continuous cut-only valley
fields, restrained fine relief and a Relief layer immediately below Terrain.
Numeric coarse controls replace the eager planet object graph; matching mesh
AND scatter calculations run in a bundled, hidden local CoreCLR worker to avoid
Unity GC pauses. Existing near-first planner/controller/reanchor behavior and
authoritative world/history hashes remain unchanged. Fresh worlds warm the worker
from a verified recipe (~13s measured once); loaded binary saves use their actual
snapshot. Main-process precise ground queries remain; no fine raster is saved.
Both200k worlds pass13110 focused checks, all492 LOD tiles pass, eager/lazy probe
files are identical, worker mesh/scatter comparisons pass, and12 exact native/CLR
probes pass. Final200k0.1-scale Continents Plus native river/mountain/lowland
reviews record81924 loading frames: worst8.01/8.60/12.91ms, no >100ms frames;
warm near rings0.109–0.145s, full coverage22–25s. Actual screenshots are inspected;
the river cold start includes13.273s worker warming. These hidden1280x800 fixed-
camera measurements are NOT fullscreen/low-end/sustained-travel acceptance.
No test/worker processes remain. Art/horizon presentation and finished terrain
realism remain open; no all-era/history performance acceptance is added. The
hyper-final and cut-only-fast players remain rollbacks. See
docs/HYPER_AMPLIFICATION.md / docs/VALIDATION.md for adaptations, limits, rejected
GC experiments, licensing, worker tradeoffs and reproduction.

Current review launch,20October2026: `WorldGen.exe - Shortcut.lnk` selects
`artifacts/player-hyper-final/WorldGen.exe` (IL2CPP08:03:18Z).
Source920035c replaces the local scaffold with shared CPU drainage-aware
hyper-amplification (adapted from Cortial et al./Arches-Team, NOT their GPU
pipeline). Terrain/drainage subdivide together; render LOD samples canonical
heights, avoiding exposed tile seams. Existing loading/reanchor/controller,
generation/history and source-world hashes remain unchanged.12518 Earth/12485
Continents Plus focused200k checks,492 full-range two-worker LOD tiles,
745terrain/4037scatter/3227legacy-water checks pass. Earlier native curved-river
and mountain renders plus24 exact CLR/native probes were reviewed; that earlier
build exposed LOD seams and is NOT the current acceptance. Current final native
Earth/generated review is IN PROGRESS: do not claim final screenshot/performance
acceptance until docs/VALIDATION.md is updated. Prior cut-only-fast is rollback.
See docs/HYPER_AMPLIFICATION.md for source/license, adaptations and limits.

Current review launch,20October2026: `WorldGen.exe - Shortcut.lnk` selects
`artifacts/player-rivers-cut-only-fast/WorldGen.exe` (IL2CPP05:58:57Z).
Local terrain7/hydrology4 corrects raised river beds/banks: cut-only envelopes,
actual uncarved path/bank samples, midpoint-dip checks and shared downstream
profile conditioning. Existing source worlds/history and loading scheduler are
unchanged. Height-only probes preserve quantized ledge inputs and avoid large
struct copies; no new raster/profile serialization or recurring history census.
3227 water/745terrain assertions; actual200k Earth/Continents Plus graph/profile/
confluence/source checks pass. Two native river captures inspected and8 exact
native/CLR probes match; mountain water is lowered, but straight reaches and
terracing remain visible. NOT finished realistic routing/canyons or all-LOD
acceptance. Final native scaffold4.34s /conditioning28.86s (down from48.75s);
fresh CLR Continents Plus full pipeline+prep48.23s during compilation. These
are separate diagnostics, NOT proof of native full-world completion under60s.
Derived managed retention264–277MiB; ~20MiB extra profile data. See docs/
RIVER_INCISION.md and VALIDATION.md. Previous scaffold player remains rollback;
existing worlds need no regeneration. Do not restore raising-bank assertions.

Current review launch,20October2026: `WorldGen.exe - Shortcut.lnk` selects
`artifacts/player-geographic-scaffold/WorldGen.exe` (IL2CPP05:11:15Z).
Local terrain6/geography-scaffold1 adds a shared drainage-conditioned mesh:
200k cells ->799994 controls/1599984 triangles, two bounded incision/hillslope
sweeps, shared ridge/valley interpolation and intermediate-graph river curves
with tapered headwaters. Large independent mountain noise is removed; small
physical-scale roughness remains. Preparation warms after generation/loading;
no scaffold/fine raster is saved and coarse worlds/history hashes are unchanged.
Actual200k Earth/Continents Plus DAG/downhill/patch/source-hash checks,221water/
745terrain/4037scatter assertions and12 exact native/CLR probes pass. Native
Himalaya/Andes/river images inspected; source unchanged. CLR full generation+
terrain prep: Earth21.05s /Continents Plus17.21s; native scaffold3.44s. Arrays
83.2MiB; total derived-terrain managed retention243.9MiB on Earth. These are host
diagnostics, not universal/native fresh-generation or all-LOD guarantees. Near
CLR tiles20.6ms mean/42.6ms worst; native distant jobs reached657ms. Some slopes
remain too smooth; standing-water/coast prototypes and placeholder art remain.
No fine watershed hierarchy, complete erosion, new ponds/oases/fjords or universal
landscape-realism acceptance. Recent near-first loading is unchanged. Previous
drainage-fractal player remains rollback. See docs/GEOGRAPHIC_SCAFFOLD.md and
VALIDATION.md. Existing coarse saves need no regeneration.

Current review launch,19October2026: `WorldGen.exe - Shortcut.lnk` selects
`artifacts/player-terrain-drainage-fractal/WorldGen.exe` (IL2CPP20Oct04:07:51Z).
Local terrain5 replaces periodic/source-local ridge trains and phasor ravines
with fixed-budget analytic-gradient multifractal relief. Hydrology2 defines
bed/banks/valley as one continuous ground section; water clips against drawn
land heights. Coarse world/history rules and recent near-first loading remain
unchanged.221 local-water/745 terrain/4037 scatter assertions pass; actual200k
Earth Himalaya/Andes/river captures inspected, source hash unchanged,12 exact
native/CLR probes match. CLR near-tile diagnostic25.4ms mean/46.0ms worst,
not native wall-time acceptance. Materials/water shading remain placeholder;
some large valleys remain sharp. No full hydraulic erosion, fine watershed
synthesis, universal realism or all-LOD acceptance. See LOCAL_TERRAIN_DRAINAGE_
FRACTAL.md and VALIDATION.md in docs. Near-first player is rollback. Owner will
review appearance; existing saved coarse worlds need no regeneration.

Current review launch,19October2026: `WorldGen.exe - Shortcut.lnk` selects
`artifacts/player-terrain-near-first/WorldGen.exe` (IL2CPP20Oct03:46:38Z).
Focused loading correction follows geometry-clipmap/CDLOD/Cesium streaming
research, not a new renderer. Replans scale with fine tile width; smaller near
jobs preserve full-detail sample spacing. Nearby terrain precedes scatter and
hidden reanchor work; obsolete jobs have cooperative cancellation. Active
coverage stays resident during replacement.745 terrain/navigation assertions
pass.200k Earth native Himalaya near-ring readiness is0.721s after sampler
initialization; three180m crossings take0.355/0.367/0.281s, preserving source hash.
No screenshot campaign, broad high-speed/reanchor acceptance, river visual fix,
height/rule/scatter definition change or universal latency claim. Owner tests
the visual result. See docs/TERRAIN_STREAMING_RESEARCH.md and VALIDATION.md.
Landforms-reviewed remains rollback; root shortcut was changed, not running games.

Current review launch,19October2026: `WorldGen.exe - Shortcut.lnk` selects
`artifacts/player-landforms-reviewed/WorldGen.exe` (IL2CPP20Oct03:17:17Z).
New procedural recipes use3.33 natural continental uplift without the legacy
sixfold boost, narrower fronts and separately broad high plateaus. Old3.32 and
earlier generation remains literal; measured Earth heights are unchanged.
Local terrain4 adds nested continuous ridges/ravines, initial indexed mean-flow
channels, fractional lake/island beds and clipped inland water. Near triangles
gain bank/thalweg vertices, including2m streams; TerrainLod/navigation, prepared
reanchor/scatter handoff, shaders, rock art, footsteps and controls are unchanged.
196 local-water/687 terrain/4037 scatter/3131 ground,5087 control and2504 preset
assertions pass; actual old-assembly Preview/Earth/Terra match all15 stages each,
and current CLI all-stage reconstruction passes.200k Earth and two generated
worlds receive numerical review. Full native Earth/generated landform captures
are inspected; final native water-only reviews preserve both worlds and match
16 exact0.1x CoreCLR probes. See docs/LOCAL_LANDFORMS_HYDROLOGY.md and VALIDATION.md
for separate build evidence, timings and limits. Art/water shading and some
render seams remain rough; basin outlines/tributaries are approximate. No full
erosion, fjords, weather accumulation, vegetation overhaul, permanent city-site
contract or new all-era/history-performance acceptance. Terrain-handoff remains
the preceding rollback. Generate anew to see changed macro relief.

Current review launch,19 October2026: `WorldGen.exe - Shortcut.lnk` selects
`artifacts/player-terrain-handoff/WorldGen.exe` (IL2CPP20Oct00:25:24Z).
Owner-authorized focused automatic-reanchor fix: one hidden frame prepares while
the original terrain remains visible; handoff adopts prepared coverage/plans,
retains geographic scatter planning, and retires meshes two tiles per frame.
Grounded scatter matrices prepare in1ms batches and match ordinary placement
exactly. No new masks, morphs, terrain cache, fog, quality settings, grounding or
height changes. Original TerrainLod/mesh/shaders/rock art/footsteps remain literal.
687 terrain/4037 scatter/3131 ground and12 exact rollback-height probes pass.
Final200k Earth/Continents Plus native0.1x reviews each pass three700m/s flight
crossings and an actual W-grounded crossing; all overlapping rendered land
surfaces retain eye clearance within1.6mm, scatter matrix differences are ZERO,
and runtime/input-file hashes stay unchanged. Same-position before/after and
flight captures are inspected. Largest atomic handoffs9.063/19.631ms; hidden
diagnostic intervals are NOT fullscreen/GPU-FPS or low-end guarantees. Ordinary
LOD pop-in, extreme-speed detail lag and manual-teleport resets remain separate.
Source/worldgen/history/audio are otherwise unchanged. Original-LOD player stays
the rollback. See docs/LOCAL_GEOLOGY_STREAMING.md and VALIDATION.md.

Current review launch,19 October2026: `WorldGen.exe - Shortcut.lnk` selects
`artifacts/player-rocks-original-lod/WorldGen.exe` (IL2CPP23:18:31Z).
OWNER-REQUESTED COMPLETE LOD ROLLBACK to20810b6: original planning/sampling,
tile lifetime/jobs, analytic grounding, chart resets, fog and debug distance
control are restored. Both preceding geology-era streaming attempts are rejected,
regardless of their recorded test passes. Do NOT reintroduce their caching,
coverage roots/masks, geomorphing, rendered-mesh grounding or Video LOD options.
Latest irregular rocks, regional placement, lithology materials and centered
footfalls remain.582 terrain/4037 scatter/3131 ground and14 actual200k terrain
assertions pass; native200k Continents Plus ground/input/centered-footfall review
passes across three sites with unchanged world hash and no render errors.
This restores the old behaviour INCLUDING its original reanchor reload limits,
not a new seamless-flight/performance acceptance. Source comparisons pin the
rollback; explorer/shader baseline differences only carry rock materials.
See docs/LOCAL_GEOLOGY_STREAMING.md and VALIDATION.md. No history/generation change.

Current review launch,19 October2026: `WorldGen.exe - Shortcut.lnk` selects
`artifacts/player-ground-lod-corrected/WorldGen.exe` (IL2CPP23:00:12Z).
This supersedes the OWNER-REJECTED geology-streaming renderer below: coarse-root
blocking and render/player height disagreement were real regressions, not
accepted by the earlier tile-count tests. Fine meshes now display immediately,
near-player morphing is disabled, grounded movement follows resident near-mesh
triangles, recent detail and three prior charts are cached, and fog recognizes
complete finer coverage. Default pre-geology LOD sampling is restored; High
increases detail. Squared rock geometry is replaced with asymmetric rounded
forms/welded seam normals. 1879 terrain/coverage and3131 ground checks pass.
Both200k Earth/Continents Plus native reviews pass at1x and default0.1x: rendered
foot clearance1.6500m, High/2x gait within15mm, cached revisit zero builds,
70km reanchor retaining coverage, unchanged source hashes and48 exact CLR probes.
Screens are inspected; preferences/audio/history/generation remain unchanged.
Bounded cache eviction can still require regeneration; this is not universal
seamless-planet/fast-flight/FPS acceptance. See LOCAL_GEOLOGY_STREAMING.md and
VALIDATION.md under docs. Centered-footsteps remains the pre-geology rollback.

Current review launch,19 October2026: `WorldGen.exe - Shortcut.lnk` selects
`artifacts/player-geology-streaming/WorldGen.exe` (IL2CPP21:26:37Z).
Local-geology1 / scatter-preview3 adds six blended lithologies, burial/noise-
conditioned loose rocks and rare outcrops, retaining existing quiet centered
stone contacts. Automatic chart reanchors retain/reproject terrain, coarse roots
load before detail, replacement groups geomorph, protective fog hides missing
coverage, and Video exposes distance/quality/transition controls. See
`docs/LOCAL_GEOLOGY_STREAMING.md`. 9042 focused assertions, both200k native
Earth/Continents Plus geology/input/70km reanchor reviews,24 exact native/CLR
height probes and CLI all-stage reconstruction pass. Screens were inspected;
rock art remains placeholder, with no prop collision or actual cliff shaping.
Accepted heights/history rules are unchanged; no all-era/performance claim.
The centered-footsteps player remains a rollback.

Current review launch,19 October2026: `WorldGen.exe - Shortcut.lnk` selects
`artifacts/player-footsteps-centered/WorldGen.exe`. Player footfalls now use
centered mono 2D playback rather than spatial world impacts; heading/movement
cannot pan their tails into one ear. All28 admitted mono assets and a native
5000-cell ground/input check pass, including actual walk/jog/sprint contacts,
centered source configuration and unchanged source-world hash. Rain/thunder,
generation/history rules and assets are unchanged. The preceding ground player
remains a rollback; this is not new all-era/performance acceptance.

Current review launch,19 October2026: `WorldGen.exe - Shortcut.lnk` selects
`artifacts/player-ground-environment/WorldGen.exe` (IL2CPP20:40:15Z).
Ground-environment1 adds derived parent geology/deposits, soil properties,
smooth eight-material local ground and optional historical realm38 agronomy.
Old histories remain literal until explicit upgrade; accepted generation hashes
are unchanged. C-held walk/default jog/Shift sprint, quiet local AI material
footfalls, fixed high-FPS ground contact and a footsteps Audio slider are verified.
Original pre-AI rain is restored, current AI thunder stays, wind remains absent.
3131 ground/582 terrain/1397 scatter/24680 actor/cache/replay assertions pass;
actual200k Earth/Continents Plus native screenshots/input/unchanged-world checks,
32 native/CLR height probes and71 native weather/24 numerical checks pass.
Earth geology is synthetic, art remains placeholder; detailed vegetation,
landform geometry and ground weather accumulation are NOT completed by this.
The broad suite's existing11-versus15-stage Earth golden mismatch is recorded,
not regenerated. No new all-era realism or historical performance acceptance is
implied. See ECOREGIONS.md, LOCAL_TERRAIN_PREVIEW.md and VALIDATION.md under docs.
`player-rain-restored` remains the preceding review/rollback build.

Current review launch,19 October2026: `WorldGen.exe - Shortcut.lnk` selects
`artifacts/player-rain-restored/WorldGen.exe` (IL2CPP,19:29:00Z; E: build junction).
Owner-requested rain rollback restores the exact RainSynth source from29d82da
and its original22050Hz streaming/volume behavior. AI rain beds, recent low-pass
and height attenuation are not used. Current AI thunder and removed wind are
unchanged.587 primitive/65 native/24 native-CLR checks and media hashes pass;
native checks are muted, not new timbre acceptance. No generation/history change.
This is the rain-only checkpoint before the separately requested soil/ground work.

Current review launch,19 October2026: `WorldGen.exe - Shortcut.lnk` selects
`artifacts/player-weather-rain-grounded/WorldGen.exe` (IL2CPP,19:06:03Z; output
junction to E:/WorldGenBuilds/player-weather-rain-grounded). Owner requested
complete wind-audio removal and rejected rain playback in midair. Wind has no
runtime source/bank/UI slider or active WAV. Do not restore it without request.
Seven unchanged rain/thunder recordings remain; rain uses one quieter filtered
bed with surface-distance attenuation (full below5m, silent above35m), not
stacked intensity beds.65 native/24 native-CLR/7 media assertions pass, including
actual near-ground playback and airborne silence. Automated checks are muted,
not subjective rain-timbre acceptance. Existing weather/history/music unchanged.
The previous isolated output encountered mapped-file packaging failure while
the owner's game was open; do not use it as a reliable rollback build. The new
output is separate; the owner's game was not closed. Source recovery remains
available in Git. See docs/WEATHER_AUDIO.md and VALIDATION.md.

Current review launch,19 October2026: `WorldGen.exe - Shortcut.lnk` selects
`artifacts/player-weather-isolated/WorldGen.exe` (IL2CPP,18:37:26Z; output
junction to E:/WorldGenBuilds/player-weather-isolated). This supersedes the
owner-rejected SA3 pack below: diagnostic scans corroborated baked-in weather
and unwanted scene sources. Eight curated AI recordings now include the exact
owner-auditioned/approved Small SFX wind gust and seven Medium rain/thunder clips.
Wind has one nonlooping voice and quiet gaps, no boosted/layered noise beds.
Rain/thunder have explicit local-weather guards and cancel in dry conditions;
SHA256-screened admission prevents accidental replacement.8 media/585 primitive/
5891 actual-world/24 native-CLR/64 native checks and Preview reconstruction pass.
Only wind has owner listening approval; classifier/muted tests are not timbre
certification. Rejected clips are recoverably quarantined in review artifacts.
Music, weather-water, generation and history are unchanged. No contextual
ambience or runtime synthesis/inference. See docs/WEATHER_AUDIO.md/VALIDATION.md.
Git backs up source, prompts and admission hashes, not recordings/model files.

Current review launch,19 October2026: `WorldGen.exe - Shortcut.lnk` selects
`artifacts/player-weather-sa3/WorldGen.exe` (IL2CPP,17:51:25Z; output junction
to E:/WorldGenBuilds/player-weather-sa3). All existing non-music sources now use
17 offline SA3 Medium CUDA recordings: isolated wind/rain/thunder, six smoothly
crossfading intensity banks and four delayed spatial thunder voices. Runtime
synthesizers and PCM generation are removed; missing recordings stay silent.
No foliage, wildlife, footsteps or context-specific ambience is added.17 media
checks/581 primitive/5891 actual-world/24 native-CLR/54 native assertions and
Preview all-stage reconstruction pass. Muted checks are not listening/timbre
acceptance. Source/prompt scripts are backed up; recordings/models are not in
Git. See docs/WEATHER_AUDIO.md and VALIDATION.md. Sound-final remains rollback;
music/generation/history/weather-water rules are unchanged.

Current review launch,19 October2026: `WorldGen.exe - Shortcut.lnk` selects
`artifacts/player-weather-sound-final/WorldGen.exe` (IL2CPP,17:21:16Z).
Weather audio keeps wind/rain and adds three procedural thunder variants,
four pooled spatial voices, distance-based delay/filtering and pause-aware
scheduling.81 primitive/24 native-CLR and46 final native checks pass, including
queued sounds held beyond their arrival times while paused and actual playback
after resume. No footsteps, soil, animal calls, new model installation or new
AI-generated sound assets. Subjective listening quality is not established by
signal tests. Textured-final remains rollback. See docs/PROCEDURAL_WEATHER.md,
docs/EXPLORATION_MUSIC.md and docs/VALIDATION.md for the future SFX direction.

Current review launch,19 October2026: `WorldGen.exe - Shortcut.lnk` selects
`artifacts/player-weather-textured-final/WorldGen.exe` (IL2CPP,16:55:34Z).
Presentation-only cloud revision: connected masses, actual3D boundary erosion,
density-slope surface shading, bounded/reused directional lighting and
quality-correct upsampling. Main ray/resolution budgets and weather/water/history
queries are unchanged.81 primitive/24 native-CLR and41 final native checks pass;
midday/low-sun/scattered/under/above-cloud captures are inspected. Whole-frame
RTX4060 diagnostic medians: Off4.81ms, Low5.34ms, High5.55ms. These are host/scene
measurements, not low-end guarantees or photorealistic/cloud-science acceptance.
Clouds-final remains rollback; see docs/PROCEDURAL_WEATHER.md and VALIDATION.md.

Current review launch,19 October2026: `WorldGen.exe - Shortcut.lnk` selects
`artifacts/player-weather-clouds-final/WorldGen.exe` (IL2CPP,16:31:04Z).
Cloud shapes use three-dimensional rounded billows, deeper density profiles
and near-weighted ray steps; rain-bearing nimbus belongs to the same volume,
not a separate storm shape. Rain/snow visibility is intentionally stronger.
81 primitive/5891 actual-world/24 native-CLR assertions and38 native checks
pass, including rendered overhead opacity with haze/particles disabled.
Scattered-cloud, above-cloud, storm, natural-rain and snow captures are inspected.
Weather-spatial-final remains rollback. No climate/history/water-budget changes
or complete cloud taxonomy/scientific calibration are implied. See
docs/PROCEDURAL_WEATHER.md and docs/VALIDATION.md.

Current review launch,19 October2026: `WorldGen.exe - Shortcut.lnk` selects
`artifacts/player-weather-spatial-final/WorldGen.exe`
(IL2CPP,2026-10-19T15:54:08Z). Derived weather-preview-3 uses fixed, interpolated
global nodes for local weather, distant clouds and optional M-map cloud darkness.
Regional windows prefetch before their edges and crossfade; chart reanchoring
does not replace the whole sky. Rain enforces cloud cover, shared phase cascades
preserve storm water/location, and local dawn fog, rain/snow curtains, downpours
and continuous rain audio have separate visibility/volume controls. No ground
water/snow visuals, climate-generation changes or historical-rule upgrades.
81 primitive/5890 actual-world/24 native-CLR weather assertions and37 final native
checks pass; see docs/PROCEDURAL_WEATHER.md and docs/VALIDATION.md for diagnostic
counts, inspected captures, approximation limits and host-dependent timings.
Weather-controls-final is rollback. This is not all-era realism or dense-world
simulation performance acceptance.

Current review launch,19 October2026: `WorldGen.exe - Shortcut.lnk` selects
`artifacts/player-weather-controls-final/WorldGen.exe`
(IL2CPP,2026-10-19T10:55:09Z). Derived weather-preview-2 adds wider regional
cloud systems and dry overcast periods without changing precipitation, temperature
or retained-water values. Explorer chat has session-only `/weather` test modes
and `auto` restoration. The M map has an optional Clouds checkbox, off by default,
with its own bounded/cancellable background texture, leaving the base map cached.
Continuous stereo wind gusts replace the old repeating noise clip; storm howls
and quiet ordinary-weather gaps are signal-tested, not subjective audio acceptance.
77 focused/11 actual-world/24 native-CLR weather checks and31 hidden native checks
pass; natural rain, closed overcast and the cloud-map diagnostic composite are
inspected. Final synchronized1280x800 frame median: Off2.73ms/Low2.91ms/High3.17ms
on RTX4060, not a low-end/fullscreen guarantee. Terrain/history remain unchanged;
no ground effects or global weather-cell ticks are added. Weather-atmosphere-final
is rollback. See docs/PROCEDURAL_WEATHER.md and docs/VALIDATION.md.

Current review launch,19 October2026: `WorldGen.exe - Shortcut.lnk` selects
`artifacts/player-weather-atmosphere-final/WorldGen.exe`
(IL2CPP,2026-10-19T10:12:47Z). Derived `weather-preview-1` supplies coherent
random-access current/interval precipitation and bounded snow/wetness estimates,
without global cell ticks or changes to terrain/climate/history. Local curved
volumetric clouds, thin high clouds, rain/snow, wind/thunder, controlled lightning,
sun effects and transient adaptation have machine-local quality/effect controls.
No ground accumulation/puddles/dynamic water are added.21 native weather and28
prior sky/display assertions,47 focused/5 actual-world/24 native-CLR weather
checks and2608 celestial assertions pass. Final GPU-synchronized1280x800 total
median frame times: Off2.89ms, Low3.18ms, High3.52ms on RTX4060; not a low-end
or interactive full-screen guarantee. See `docs/PROCEDURAL_WEATHER.md` and
VALIDATION.md for limits, climate-input caveats and depth/alpha contracts.
Celestial-display remains the rollback; no historical rules upgrade is needed.

Current review launch,19 October2026: `WorldGen.exe - Shortcut.lnk` selects
`artifacts/player-celestial-display/WorldGen.exe` (IL2CPP,2026-10-19T08:49:04Z).
Optional saved play settings expose independent sun/moon visual exaggeration,
editable after history starts; defaults1× preserve old displays.5× enlarges only
the disk, not lighting, phase, orbit or cultural astronomy. Main-menu and both
pause settings share persistent VSync/maximum FPS controls (0=Unlimited;
VSync overrides the independent desktop cap).2606 focused/6 actual-world and
33 exact native/CLR celestial assertions,28 hidden native display/atmosphere
checks pass; actual1×/5× moon and unchanged-terrain captures are inspected.
FPS controls are tested at the engine-setting level, not as throughput benchmarks
or new UI-layout proof. No generation/history rules change. The celestial-atmosphere
player is rollback; see `docs/CELESTIAL_ATMOSPHERE.md` and VALIDATION.md.

Current review launch,19 October2026: `WorldGen.exe - Shortcut.lnk` selects
`artifacts/player-celestial-atmosphere/WorldGen.exe` (IL2CPP,2026-10-19T08:25:45Z).
New-game Sky & atmosphere configures0–4 moons, six arrangements, physical circular
orbital periods/sun-relative phases, colored moonlight, three seeded stellar
environments and analytic altitude-dependent aerial perspective. F3 reports
northern solstice/equinox progress; sunrise/sunset commands use local latitude/date.
Fresh Unity histories opt into independent `HistoricalSkyContext.Version=1`,
with inherited actual-sky calendar/faith/festival profiles and Atlas inspection.
Missing sky contexts remain null/literal; old sidecars missing sky settings remain
moonless. No generation change, recurring territorial census or daily festival
queue.2600 focused/6 actual-world/33 exact native-CLR profile assertions,21 native
atmosphere and32 native sky/chat checks pass; screenshots are inspected. Hidden
captures do not prove setup/chat UI layout. Weather, eclipse lighting, tides,
long-term N-body stability, dynamic calendar reform and new all-era/performance
acceptance are not claimed. World-meridian is rollback. Read
`docs/CELESTIAL_ATMOSPHERE.md` and `docs/VALIDATION.md` for scope/evidence.

Current review launch,19 October2026: `WorldGen.exe - Shortcut.lnk` selects
`artifacts/player-world-meridian/WorldGen.exe` (IL2CPP,2026-10-19T07:17:13Z).
Presentation-only official chart seam/prime meridian is chosen once per world
and saved in optional play-session fields. Default maps/graticules use this
reference; local sun, stars, music, F3 and time commands are longitude-aware.
The23.44° tilt remains fixed.113 focused meridian/892sky/182music/42play/
1008presentation/334interface assertions and32 hidden native checks pass,
including actual noon-to-opposite-longitude night, live local chat setters and
saved-prime continuation. Preview all-stage reconstruction hashes match.
Earth20k chooses seam177.75°W/prime2.25°E; native sky images are inspected.
Browser changes compile but have not had a new interactive acceptance pass.
Generation/history rules are unchanged; sky-chat is rollback. See
`docs/WORLD_MERIDIAN.md` and `docs/VALIDATION.md` for approximation/validation limits.

Current review launch,19 October2026: `WorldGen.exe - Shortcut.lnk` selects
`artifacts/player-sky-chat/WorldGen.exe` (IL2CPP,2026-10-19T06:43:59Z).
Adds a camera-local one-sun sky, twilight/rotating stars, shared sky/music solar
altitude, optional saved playable-calendar offset and Minecraft-style local chat
(Enter, slash, alphabetical help, time/date commands). No moons/weather or
generation/history rules change.892 focused/182music/42play/1008presentation/
334interface assertions and28 hidden native input/sky/audio checks pass. Actual
noon/twilight/night camera images are reviewed; hidden GUI capture is black, so
chat layout is not claimed visually verified. Original multi-biome music names
now decode correctly; all30 exploration tracks completed with zero failures.
The exploration-music player remains rollback. See `docs/SKY_CHAT.md` and
`docs/VALIDATION.md`; no new all-era/performance acceptance is implied.

Current review launch,19 October2026: `WorldGen.exe - Shortcut.lnk` selects
`artifacts/player-exploration-music/WorldGen.exe` (IL2CPP,2026-10-19T06:04:37Z).
Music-only atmosphere pass adds streamed `music/exploration` with biome/time
filename tags, weighted eligibility, recent-three avoidance and real-time gaps.
It uses the existing solar/daylight clock; no sky/weather/ambience/footsteps,
generation or history mechanics change.182 focused/1008presentation/42play checks
and18 native audio assertions pass, including nonzero actual generated-OGG output,
night/day switching, no era leakage, mute and clean simulation return.30-track
local SA3 Medium CUDA production is running at publication; consult
`artifacts/exploration-music/status.json` for real completion, not the plan count.
Tracks/masters stay out of Git; prompts and resumable producer are source-backed.
Game-shell-final remains rollback. Read `docs/EXPLORATION_MUSIC.md` and VALIDATION.

Current review launch,19 October2026: `WorldGen.exe - Shortcut.lnk` selects
`artifacts/player-game-shell-final/WorldGen.exe` (IL2CPP,2026-10-19T05:20:04Z).
Game-first menus use separate setup categories; World generation defaults to
type/seed/200000cells with advanced controls closed. Versioned play-session1
sidecars save scale/time/player pose atomically; old absent sidecars retain scale1.
Default0.1horizontal scale preserves vertical metres; local-terrain-preview3
preserves physical detail wavelengths to avoid compressed spires and retains
preview2 heights exactly at scale1. F3 is nonblocking text; M opens a themed
teleport/layer map. Escape reopens simulation without restoring fast-forward.
42 focused/581terrain/2880scatter/1008presentation checks, all-stage Preview
reconstruction and23 native flow checks with13 visible5-second-held screens pass.
Structure density remains reserved; no villages/NPCs/fine hydrology were added.
Polar snow is overbright, distant LOD is visible and terrain realism is not
accepted as complete. Scatter player remains rollback. See `docs/GAME_SHELL.md`
and VALIDATION.md; no new historical performance/realism acceptance is implied.

Current review launch,18 October2026: `WorldGen.exe - Shortcut.lnk` selects
`artifacts/player-local-scatter/WorldGen.exe` (IL2CPP,2026-10-19T04:12:36Z).
Owner-authorized `local-scatter-preview-1` adds stable Core-derived biome/slope
conditioned trees, shrubs, grass and rocks; bounded instanced streaming, separate
prefab/LOD bindings and replaceable layered URP PBR textures. Heights remain
local-terrain-preview2; worldgen/history/saves are unchanged.2,880 scatter/581
terrain/1,008 presentation checks, Preview all-stage reconstruction, six actual
native Earth/generated scenes with visible-on/off pixel checks, controls/streaming/
return/world-hash preservation and24 exact native/CLR terrain probes pass.
Warm stationary scenes target60FPS on RTX4060; this is not a broad travel/GPU or
historical-speed acceptance. Art remains low-poly placeholders, without prop
collision, plant simulation or land-use clearing. Grounded player is rollback.
Read `docs/LOCAL_SCATTER_PREVIEW.md` and VALIDATION.md before extending this work.

Current review launch,18 October2026: `WorldGen.exe - Shortcut.lnk` selects
`artifacts/player-local-grounded/WorldGen.exe` (IL2CPP,2026-10-19T03:33:07Z).
Controller-only correction uses1.65m eye height,1.5m/s walk,5m/s run and0.45m
jump; true continuous-grade ascent limits replace the per-frame step loophole,
ordinary downhill motion stays grounded and bounded substeps retain real-time
movement through slower frames. No silhouette/body/HUD/vegetation, local-height
algorithm, generation or history change.579 focused/1,008 presentation assertions,
three native Earth scenes with mouse/jump/debug/streaming/return and unchanged
world hash pass. Full capsule/prop/stair collision and steep-slope sliding remain
deferred. The controls4 player is rollback; see LOCAL_TERRAIN_PREVIEW.md/VALIDATION.

Current review launch,18 October2026: `WorldGen.exe - Shortcut.lnk` selects
`artifacts/player-local-terrain-controls4/WorldGen.exe` (IL2CPP,
2026-10-19T03:11:25Z). Local terrain preview2 adds captured mouse-look, default
walking/Space jump, F fly, HUD-free exploration, F3 diagnostics and F3+M zoom/pan/
precise teleport map. View/altitude-dependent quadtree LOD requests200km by default,
with bounded background work, coarse-edge collapse and chart reanchoring.
Native Earth Himalaya/Andes/nearby coastal/plateau images rejected the initial
lumpy relief and an over-sharp curved-wall experiment; final broad irregular
mountain masses retain smooth plateau interiors.319 focused/28 actual-world,
1,008 presentation/51 lake assertions, seven native200k scenes, F3/chord/map/
teleport/mouse-without-drag/Space/F/W/Esc controls, unchanged initialized-world
hashes and28 exact CLR/native probes pass. Existing world/history algorithms are
unchanged. This is a replaceable bare-terrain prototype, not a measured local
Earth reconstruction, full erosion, finished materials or dense-history speed
acceptance. The preceding local-terrain-preview player remains rollback.
See `docs/LOCAL_TERRAIN_PREVIEW.md` and the latest VALIDATION entry.

Current review launch,18 October2026: `WorldGen.exe - Shortcut.lnk` selects
`artifacts/player-local-terrain-preview/WorldGen.exe` (IL2CPP,
2026-10-19T01:58:43Z). Owner-authorized `local-terrain-preview-1` adds a separate
additive terrain exploration scene via **Explore terrain** in setup/simulation.
Core-derived, versioned local terrain preserves coarse height anchors and blends
landform descriptors; disposable bounded CPU-generated LOD tiles, biome/rock
placeholder materials and ocean beds/surface stream without saving fine maps.
WASD/right-drag/F/Shift/Space/Ctrl/wheel; Esc returns and resets playback elapsed
time so exploring causes no catch-up jump.199 focused/42 actual-world terrain,
1,008 presentation/51 lake assertions, six native Earth/200k Continents Plus
sites, F/W/Esc input, unchanged world hashes and24 exact native/CLR probes pass.
Direct camera images are inspected; hidden-window black captures are rejected.
One22ms tile upload was observed; this is not a universal frame-budget claim.
The full runner still fails an archived11-stage Earth golden; expectations were
not regenerated. Existing generation/history rules are unchanged; no save upgrade
is needed. Peace-news is rollback. Inland hydrology, fractional islands, specialist
landforms, vegetation, NPCs and permanent structure placement remain deferred.
Read `docs/LOCAL_TERRAIN_PREVIEW.md` and the local-terrain entry in VALIDATION.md.

Current review launch,18 October2026: `WorldGen.exe - Shortcut.lnk` selects `artifacts/player-history-32-peace-news/WorldGen.exe`. Presentation-only war-ending headlines now distinguish actual defeat/government fall, imperial submission retaining local government, tribute, frontier/trade settlements, supply failure and ceasefires. Dated evidence identifies parties without projecting later conquests or patrons backward.18 focused/1,003 presentation assertions, actual Earth/forest unchanged-hash news audits, native reader/unchanged-history and unchanged12-year replay pass. No simulation-rules upgrade is needed for this wording change; history32 optional realm37 and generation rules are unchanged. Domestic37 is rollback. See the war-ending headlines entry in `docs/VALIDATION.md`.

Current review launch,18 October2026: `WorldGen.exe - Shortcut.lnk` selects `artifacts/player-history-32-domestic37/WorldGen.exe`. Optional history32 realm37 adds persistent institutional power groups and governing coalitions, actual financed bargaining, conserved spending priorities, organized opposition, representative public-figure selection and resource-conditioned nonlethal office contests. State Atlas articles expose power and dated bargaining under Power & relations. Two sparse immutable ledgers add fixed five-group actor work, no recurring territorial/member census. Old36 remains literal until explicit rules upgrade; atlas36 is rollback.14 focused/985 presentation/25 Earth/25 forest assertions, actual browser politics screenshots, native all-section/unchanged-history and exact native/CLR continuation pass. This is mechanism/persistence/UI review, not all-era realism, dense browser import or new dense-world speed acceptance. See `docs/HISTORY32_DOMESTIC_POLITICS.md`.

Current review launch,18 October2026: `WorldGen.exe - Shortcut.lnk` selects `artifacts/player-history-32-atlas36/WorldGen.exe`. Atlas has typed sortable tables, tab-specific visual dashboards, relationships, phonetic cards, research/deployment panels, dated achievements and illustrative regional appearance samples. Optional history32 realm36 / identity Version2 also records regional heritage before advanced cultures, physical migrant inheritance and slow peripheral differentiation; Culture=-1 is valid for these identities. Existing8-trait regional physical variation stays separate from social identity; `SampleNpcAppearance` provides bounded seeded future model parameters, not complete NPC meshes or genetic pedigrees. Old35 continuations remain literal until explicit upgrade.119 focused/971 presentation/315 Earth/338 forest assertions, actual Earth/prehistory browser checks and native all-section/unchanged-history/exact CLR continuation checks pass. Dense forest browser import times out and is not accepted; no new all-era realism or dense-world speed acceptance is implied. World-panel is rollback. See `docs/ATLAS_OVERHAUL_REVIEW.md` and `docs/HISTORY32_FORAGER_HERITAGE.md`.

Current review launch,18 October2026: `WorldGen.exe - Shortcut.lnk` selects `artifacts/player-history-32-world-panel/WorldGen.exe`. Presentation-only unified World [W] panel includes category tables and map settings; W opens the selected focus then returns to world overview, Esc closes even from search. Empty categories never retain the previous article.852 presentation assertions, populated/fresh Earth browser interactions and native IL2CPP navigation/unchanged-history checks pass. No simulation rules upgrade is needed. Notables35 remains the rollback; historical/generation versions and unresolved realism/performance criteria are unchanged. See `docs/WORLD_PANEL_NAVIGATION.md`.

Current review launch,18 October2026: `WorldGen.exe - Shortcut.lnk` selects `artifacts/player-history-32-notables35/WorldGen.exe`. Optional history32 realm35 adds shared high-profile lives, prospective selected royal households, heirs/regencies, actual funded/earned research/cultural/religious/military careers and Atlas biographies. Offices and lives are separate; no whole-population person simulation or recurring territorial census. Old saves remain literal until explicit upgrade; sovereignty34 is the rollback. Four focused/22 Earth/22 forest/826 presentation assertions, inspected browser screens and native maps/stripped biographies/exact CLR replay pass. See `docs/HISTORY32_NOTABLE_PEOPLE.md`. No new all-era realism or dense-world performance acceptance is implied.

Current review launch,17 October2026: `WorldGen.exe - Shortcut.lnk` selects `artifacts/player-history-32-sovereignty34/WorldGen.exe`. Optional realm34 adds prospective local sovereignty petitions, real financed/accepted protection, organized secession/reconciliation/withdrawal, actual campaign-attributed town transfers and an atlas dossier under Power & relations. Old33 and earlier saves remain literal until explicit upgrade; identity33 is the rollback.511 focused/32 Earth/31 forest/826 presentation assertions, actual browser article and native maps/stripped-article/exact CLR continuation pass. See `docs/HISTORY32_CONTESTED_SOVEREIGNTY.md`. No recurring territorial census, generation change or new all-era/performance acceptance is implied.

Current review launch,17 October2026: `WorldGen.exe - Shortcut.lnk` selects `artifacts/player-history-32-identity33/WorldGen.exe`. Optional realm33 adds separate ethnic identities (map9), resident minorities, inherited/mixed local identities, independently saved ruling heritage and bounded salient town memories. Actual capture, treatment and rights condition local politics; ethnic difference alone is not rebellion. New33 fixes regional-clock overwriting of local minority records and treats language relatedness as learning speed, not a proficiency ceiling. No recurring territory/membership census is added; old saves stay literal until explicit upgrade.208 focused/26 Earth/26 forest assertions,826 presentation assertions, native/browser maps/atlas/focus and exact native/CLR continuation pass. A200k48-year warm+12-year measured check activates517people/3842town memories;1.67yr/s, worst0.535s. This does NOT satisfy the outstanding3yr/s/max-tech+1000 criterion or establish all-era ethnic/political calibration. Read `docs/HISTORY32_IDENTITIES.md` and VALIDATION.md. Speech32 remains the rollback.

Current review launch,17 October2026: `WorldGen.exe - Shortcut.lnk` selects `artifacts/player-history-32-speech32/WorldGen.exe`. Optional realm32 adds saved spoken communities over the existing language30 sound/naming foundation, independent Languages map (hotkey8), resident home-language minorities, bilingual competence, administrative/ritual domains, actual contact/migrant transmission and inherited local divergence. Religion inspection distinguishes resident household/lay/institutional presence from visiting teachings. Read `docs/HISTORY32_SPOKEN_COMMUNITIES.md`; no recurring territorial census is introduced. Existing saves remain literal until explicit upgrade.306 focused/23 Earth/23 forest-world assertions,799 presentation assertions, a200k12-year smoke, browser atlas/hotkey/selection and native map/atlas/selection/CLR continuation pass. The religion31 player remains a rollback. This is mechanism/persistence/UI verification, not all-era linguistic calibration or satisfaction of the outstanding3yr/s target; see VALIDATION.md.

Current review launch,17 October2026: `WorldGen.exe - Shortcut.lnk` selects `artifacts/player-history-32-religion31/WorldGen.exe`. This adds history32 optional realm31's structured religious doctrines, specific inherited denominations and separately provisioned public/lay institutions; see `docs/HISTORY32_RELIGIOUS_TRADITIONS.md`. Old saves keep their old rules until explicit upgrade.377 focused/772 combined presentation assertions,24 Earth/22 generated forest-world causal/replay/persistence checks, a200k-cell12-year smoke, native maps/selection/atlas/unchanged-history and exact native/CLR continuation pass. Browser doctrine/observance/institution UI and map selection are reviewed. The map-marks and verified31 players remain rollbacks. This is religious-system verification, not all-era religious calibration or acceptance of the outstanding3yr/s late-world performance target.

Current review launch,17 October2026: `WorldGen.exe - Shortcut.lnk` selects `artifacts/player-history-32-map-marks/WorldGen.exe`. Presentation-only river/settlement revision: R toggles cached rivers; C cycles Off/Capitals only/All/Cities only/Large cities only, default Capitals only. Curved antialiased zoom-aware river strokes and cached marker lists preserve generation/history rules. Release build,24 focused/368 combined presentation assertions, real browser keyboard checks and native IL2CPP R/C/three-zoom/unchanged-history checks pass. Existing saves need no rules upgrade for these display changes. Court30 and verified31 remain rollbacks; no new all-era realism or high-detail performance acceptance is implied.

Current review launch,17 October2026: `WorldGen.exe - Shortcut.lnk` now selects `artifacts/player-history-32-court-connectivity30/WorldGen.exe`, superseding earlier launch notes. AF29 and verified31 remain rollbacks. This includes the requested sovereign-state colors, no pale claims, clearer local embryonic stages and optional realm30 court-connectivity rules. The IL2CPP build and six-overlay early-world native view check pass; native/CLR one-year continuation matches exactly. Existing saves need the explicit historical-rules upgrade for new behavior, not for the presentation fixes. This is targeted verification, not complete all-era realism or performance acceptance. See `docs/HISTORY32_COURT_CONNECTIVITY.md`.

Owner-requested source review,17 October2026: optional realm30 adds bounded actual court connectivity, local early direct-administration reach and withdrawal of offices cut off by other states. Existing29 continuations remain exact until explicit upgrade. States are now colored by sovereign state ID; pale claim styling is removed, with title/control still distinguished in inspection. Advanced/embryonic local gray stages remain distinct.118 focused/188 combined continuity assertions and4 actual200k upgrade/partition/save assertions pass; old29 one-year continuation matches the frozen executable exactly. See `docs/HISTORY32_COURT_CONNECTIVITY.md`. Native packaging and the selected court30 launcher are verified as described in the current launch note above. No all-era realism or performance acceptance is implied.

Current review launch,17 October2026: the project `WorldGen.exe - Shortcut.lnk` selects `artifacts/player-history-32-overnight-af29-review/WorldGen.exe`, superseding all older launch notes below. The preceding realms-autowarp player and verified31 remain rollback builds. This is a reviewed checkpoint, NOT finished realism or performance acceptance. Fresh200k Earth/Continents Plus29 histories and native era replays match exactly; both dense native1000-year speed tests FAIL (1.29/1.16yr/s, worst2.66/2.45s). AF29 adds tested presentation-only pale recognized claims versus administered territory; both six-overlay native final-world checks preserve history hashes/input files. Hidden-window HUD captures are black and are not UI-layout proof. See `docs/HISTORY32_OVERNIGHT_REVIEW.md` and `docs/HISTORY32_PERFORMANCE_REVIEW.md` for the current acceptance record.

Current source uses `history-32` optional realm29, not historical version29. Physically migrating households retain actual departing public and remembered faith identities without creating a religion per culture. Resident rites, funded institutional provenance and shared metadata remain protected; pristine/contact-recognized communities stay separate.24 focused,14 actual-identity and11 genuine cold-save assertions pass, plus exact full fresh28 legacy continuation. Claim-display styling passes85 focused assertions. All older optional realm continuations remain literal until explicit upgrade; start a fresh simulation for the new rules. Severe Asian institutional timing, late geographical uptake and excessive final microcourt fragmentation remain open. Generation remains worldgen-3.32.0; do not hide these historical failures with unversioned climate or free-resource changes. See `docs/HISTORY32_MIGRANT_FAITH_CONTINUITY.md` and `docs/HISTORY32_CLAIM_DISPLAY.md`.

Latest overnight source checkpoint28,17 October2026:27 aligns paid civilian maritime planning and commitment while retaining hostile-conquest and refused-consent protections;28 separates recognition of a strongly, physically contacted inhabited community from the pristine wild-surplus test. It grants no crops, towns, people or sovereignty. Old27 continuation and genuine28 cold save/load match exactly. Current28 Earth reaches the full maximum-tech+1000-year tail; current native era/performance review is still pending, and the selected shortcut remains the preceding rollback. Exact runtime seed-landscape and eight-scalar relation memos preserve old outcomes. Read `docs/HISTORY32_OVERNIGHT_REVIEW.md`, `docs/HISTORY32_CIVIL_MARITIME_CONTINUITY.md`, `docs/HISTORY32_CONTACT_COMMUNITY_RECOGNITION.md` and `docs/HISTORY32_PERFORMANCE_REVIEW.md` for current evidence; older checkpoint notes below are historical, not current acceptance. Do not confuse history-32 optional realm28 with history-28.

Overnight source checkpoint26,17 October2026:25 replaces flat per-town conquest debt with actual transferred-resident/served-capacity burden and physically delivered, explicitly paid integration repair;26 recalibrates repeated generic scholarly ranks while retaining foundational cost, prerequisites, conserved research/lesson budgets and deployment. Existing24 saves keep literal rules until explicit upgrade. Frozen25-only Earth and full26 Earth/Continents Plus reviews distinguish the political and research experiments.64 pure and16 actual transfer checks pass;58,949 scholarly checks pass. Actual old24/cold26 continuation and final native performance remain separate pending acceptance. See `docs/HISTORY32_CONQUEST_INTEGRATION.md` and `docs/HISTORY32_SCHOLARLY_RANK_COSTS.md`. The shortcut remains the preceding rollback until the new native review is complete.

Overnight source checkpoint24,17 October2026: first unparented farmsteads use a continuous opportunity curve below the comfortable0.10 crop-landscape threshold, while retaining the0.02 physical floor, actual people/provisioning, virgin clearing costs and separate restrictive proto-city/state qualification. Parented settlement and overseas port field gates are unchanged. This addresses a measured0.098367 field rejection, not a guarantee of civilization in a named region. Pure gates pass; fresh Earth and genuine cold/old23 continuation checks are underway.25/26 experiments may be present but are dormant until explicitly activated. See `docs/HISTORY32_MARGINAL_FARMSTEADS.md`; the shortcut remains the preceding rollback pending native review.

Overnight source checkpoint23,17 October2026:22 uses each planted annual cultivar's maturity duration for its seasonal field window while retaining the preceding perennial/orchard rules;23 conserves partial research and dated physically received lessons when completing a technical level, and distinguishes territorial secession from autonomy demands where real funded, inclusive administration is actually reachable. Recent regional successor towns may join a living nearby sibling government rather than automatically minting another flag. Existing22 and earlier saves retain their rules until explicit upgrade. Full fresh23 Earth/Continents Plus comparisons and actual cold-save controls are ongoing; do not infer validated realism from activation. Exact sparse tree hashing, nonempty-set pruning and canonical local-bag dispatch preserve outcomes. The selected shortcut remains the preceding rollback pending final native review; the strict performance criterion is not yet met. See the crop-maturity, technical-learning-credit, territorial-continuity and performance reviews.

Overnight source checkpoint21,17 October2026:20 activates prospective, locally financed religious lay associations, distinct from public faith and household inheritance;21 recognizes an already-qualified living proto-centre in the continuity rule rather than treating it as an unorganized village. Real food distress and institutional dequalification remain. Existing19/20 saves keep their literal rules until explicit upgrade. Fresh Earth/Continents Plus21 replays and independent era review are in progress; no final realism claim is implied. Binary first-write content interning is lossless and retains WGWRLD01/old-reader compatibility and existing size guards. The selected shortcut is still the preceding rollback. NativeS remains below the strict performance target; diagnostic component timers are optional/off by default. See `docs/HISTORY32_PROTO_CONTINUITY.md`, `docs/HISTORY32_LAY_ASSOCIATIONS_DESIGN.md` and `docs/HISTORY32_COMPACT_TREE_TRANSPORT.md`.

Overnight source checkpoint19,17 October2026:18 removes the hard exchange wall below100 real district residents while retaining physical routes and sparse encounter probability;19 fixes the cool-season potato/tropical-root niche mismatch. Existing17/18 continuations remain literal. Actual route and recorded-crop counterfactuals pass; full geographical/pacing review remains ongoing. Prospective religious lay associations are present but dormant until explicit20 activation/review. Exact trusted-private relation memoization and nested secondary tree-hash scratch preserve canonical bytes; native acceptance must be remeasured. The selected desktop shortcut is still the preceding rollback. See `docs/HISTORY32_SPARSE_EXCHANGE.md`, `docs/HISTORY32_CROP_SPECIES_NICHES.md`, `docs/HISTORY32_LAY_ASSOCIATIONS_DESIGN.md` and performance evidence; no final realism claim follows from the revision number.

Overnight source checkpoint17,17 October2026: revision16 adds cohort-sized, locally paid prepared fields;17 separates successful planted-crop phenology generations from yield-weighted practice and withdraws unmaintained excess clearing after rural decline. Neither grants free crops, people, stocks, towns or sovereignty. See `docs/HISTORY32_PREPARED_FIELDS.md` and `docs/HISTORY32_LOCAL_PHENOLOGY.md`; fresh Earth replay and native performance acceptance remain under review. Binary-only immutable-tree transport preserves legacy/JSON compatibility and recovers sharing on read without dropping history; see `docs/HISTORY32_COMPACT_TREE_TRANSPORT.md`. The selected desktop player is still the preceding rollback, not this experimental source.

Overnight source checkpoint15,17 October2026: local physical seed inventories and bounded actual-field selection are enabled by optional14;15 lets administered farmers attempt their own paid adjacent-land departures even alongside an unrelated regional population. Each account debits its own people; field/resource/route gates remain. The four aligned campaign collections now use copy-on-access pages with the old array wire/canonical bytes. Actual cold continuations and old-reader roundtrips pass; native throughput and European settlement remain under review. No overnight player has been selected yet. Prepared-plot16 work is not enabled by this checkpoint.

Overnight source checkpoint13,17 October2026: fresh experiments include continued local crop selection(10), physically updated maritime intelligence and preparable convoys(11), real free-field reception of farmer households(12), and resource-conditioned peacetime urban succession competition(13). Seedlot14 infrastructure is present but inactive until explicitly enabled and validated. Leader records are paged with legacy wire/canonical compatibility; owned relation canonical bytes and shared Windows CNG provider handles are exact-outcome optimizations. Native acceptance and Eurasian geographical realism remain unresolved; the selected rollback shortcut is unchanged. See the overnight review and causal evidence, not source revision numbers alone.

Overnight source checkpoint9,17 October2026: administered rural farmers can fund bounded, adjacent-land household departures from their actual state Remote ledger even when the old regional pool is empty. Actual local presence, viable free fields, a live supplied service center and paid food/investment are required; no town, state, sovereignty or people are granted. This remains a reviewed experiment, not a promise of corrected Eurasian settlement. Old8 continuation remains exact; the selected shortcut remains unchanged pending final native review.

Overnight source checkpoint8,17 October2026: optional realm8 adds guarded actual-city religious upkeep and conserved regional migrant cultural counts, after7's physical land household inheritance. Diplomatic relation/campaign-ID storage is now paged copy-on-access with the preceding array wire/canonical representation; this is an exact-outcome performance change, not another historical rule. Full runtime/adapters must be rebuilt together after the managed collection-signature change. Source/native review is ongoing; the selected rollback shortcut has not changed. See `docs/HISTORY32_OVERNIGHT_REVIEW.md` and religious-network evidence.

Overnight source checkpoint6,17 October2026: source experiments now use optional realm revision6: actual land-border contacts with internal travel costs, crop-aware paid farmer settlement, finite shared physical-port transport and supported local succession claimants. See `docs/HISTORY32_OVERNIGHT_REVIEW.md` and `docs/HISTORY32_LOCAL_SUCCESSION_CLAIMS.md`. Old saves retain preceding rules until explicit upgrade; native player selection remains unchanged pending review.

Overnight source checkpoint,17 October2026: fresh `history-32` experiments now use optional realm revision5 (conserved city-food/survival, paid local inland relays and a targeted iron-cost correction), religious network revision2 and service recovery revision2. Source history is not the selected player: the shortcut remains `player-history-32-realms-autowarp` until native/visual review is complete. Old realm continuations keep exact arithmetic and depot-leg rules; new outcomes require explicit upgrade. See `docs/HISTORY32_OVERNIGHT_REVIEW.md` and the city/realm causal audits. Compact writes use Optimal gzip within the existing WGWRLD01 format/limits; do not discard history to meet a file-size limit.

Auto-only launch refresh,16 October2026: the project shortcut now selects `artifacts/player-history-32-realms-autowarp/WorldGen.exe`, retaining the prior realms-review player. This supersedes only the launch target below. Auto requests2000yr/s initially,500 after advanced cultures,100 after embryonic civilization,10 after first states,5 at Bronze,2 from Iron through Early Modern, and1 from Industrial onward. Observer era names, music and authoritative simulation rules are unchanged.

Launch-target update,16 October2026: following the requested empire review, the project `WorldGen.exe - Shortcut.lnk` now targets `artifacts/player-history-32-realms-review/WorldGen.exe`. It includes optional realms revision1, continuing country foundations, real regional succession contests and funded sea/land administration. Geographic-ventures, W2 and verified31 remain rollback builds. This is explicitly a review, not a claim that realism or performance acceptance is complete: ordinary Earth late-world coverage remains deficient, and the dense native200k test averaged3.53yr/s but had a worst computed year of1.80s. Source and evidence are documented in `docs/HISTORY32_REALM_LIFECYCLES.md`. Existing saves keep their preceding rules until explicit upgrade; fresh simulations use the new extension. Treat this reviewed realms1 behavior as a checkpoint: future output-changing work needs a new explicit rule marker/upgrade, not silent mutation of existing saves.

Read `docs/OROGEN_ALIGNMENT.md` (current version-3 scope), `WorldGen_OneShot_Prompt.md`, `docs/FIRST_BUILD_SPEC.md`, `docs/REFERENCE_ANALYSIS.md` and the implementation contracts in `docs/ALGORITHMS.md` before changing generation behavior. `docs/OROGEN_REFERENCE_MAP.md` records independent use of the pinned behavioral reference; never copy GPL reference code without the owner's explicit license decision.

`src/WorldGen.Core` is the sole authoritative generator and targets .NET Standard 2.1. `Serialization` and `Presentation` are shared adapters. CLI, .NET browser worker and Unity runtime call `WorldPipeline`; reapply/cache/edit changes must not create another generation route. Keep geological provenance and the coarse/fine resolution split inspectable. The owner authorized persistent hydrology and bundled measured Earth terrain on 1 October 2026; use the new climate-driven solver, not the removed legacy solver.

Historical simulation is owner-authorized through modern technology. Verified player rules use `history-31`; source experiments use `history-32` (not a packaged release until longitudinal review) over the aggregate actor/event calendar; old saves require an explicit rules upgrade.31 adds strongly environment/resource-conditioned origins, an explicit anti-human-homeland origin prior after distant residence, barrier/latitude-aware physical diffusion and dated contact delivery;30 adds saved bounded language profiles, inherited roots/sound changes, contact loans and culture-specific actor names;29's economic, political, research and geographical rules remain. Naming queries never walk ancestry or terrain memberships. The desktop shortcut targets verified `player-history-31-environment`;30,29 and earlier players remain available.29 recalibrates material/research costs without calendar gates; observer eras require coherent capability packages and Auto playback follows these eras.28's regional/continental ambitions remain distinct from recognized sovereignty and actual control. See `docs/HISTORY_SIMULATION.md`, `docs/HISTORY24_REVIEW.md`, `docs/HISTORY23_REVIEW.md` and the historical contracts in `docs/ALGORITHMS.md`. Do not reintroduce recurring world/empire/culture/ecoregion cell censuses. Generation versions and historical versions are separate.

The determinism contract is seed + complete recipe + algorithm version. Use project-owned named PRNG streams, explicit numerical quantization, ordered graph ties/reductions and stage hashes. Avoid System.Random, engine randomness, GPU decisions, wall time and unordered authoritative iteration. Version output-changing algorithms after the initial release. Golden updates must be intentional and reviewed; never regenerate expectations as part of a passing test run.

Current scope: a fixed 20,000-cell coarse plate/continent base projected onto a detail-dependent 5,000-2,560,000-cell terrain mesh; independent terrain/sculpting, four-season climate, persistent hydrology, saved worlds, bundled Earth relief, habitat proxies, ecoregions, static native species/food-web synthesis, historical actors through modern capabilities, and Unity/browser globe/map views. Ecology is owner-authorized on 2 October 2026; see `docs/ECOREGIONS.md` for its units, trait priors and limitations. The owner explicitly declined GPL translation on 17 September 2026. Map2 collision contexts, dynamic ecological populations and local playable terrain remain deferred. Preserve `ISeasonalRunoffProvider`; broad observational Earth climate sanity checks are authorized, not a claim of scientific validation.

Build: `powershell -ExecutionPolicy Bypass -File scripts/build.ps1`. This compiles the solution, stages worker assets, and copies shared .NET Standard DLLs into Unity. Test: `dotnet run --project tests/WorldGen.Core.Tests -c Release`. Verify CLI reconstruction: `dotnet run --project src/WorldGen.Cli -c Release -- --preset Preview --verify`.

Browser: `dotnet run --project src/WorldGen.Web -c Release --no-build --urls http://127.0.0.1:5080`; then `npm ci`, `node scripts/browser-check.mjs`, `node scripts/worker-check.mjs`. Restart the server after rebuilding fingerprinted assets. Unity is **6000.6.0f1**, URP **17.6.0**, under `unity/WorldGen.Unity`. Run `scripts/unity-check.ps1` and `scripts/unity-check.ps1 -BuildPlayer -PlayerCheck`. Do not run two editor processes against the same project.

Current recipes use schema 3 / worldgen-3.32.0. Version-1/2 fixtures are historical; version-3 goldens live in `tests/golden/v3` and are not automatically updated for new algorithms. Detail and Irregularity are generation settings and participate in hashes. `World.CoarseHash` must remain invariant when only those settings change. Earlier first-build instructions are superseded where they conflict with `docs/OROGEN_ALIGNMENT.md`. Fractional lake coverage/storage remain authoritative below the 90% whole-cell display threshold; hydrological outlet conditioning and ecology must not reshape the accepted terrain or ocean mask. Old snapshots retain null ecology and their historical integrity hashes.

Required review: matching all-stage hashes across fresh/reapply/serialized-recipe/cached reconstruction; connected plates/superplates; stable coarse partition but genuinely detail-dependent terrain; full globe/map coverage and crisp cell colors; acyclic drainage, coherent lake surfaces and water balance; settings parity; cancellation during generation and presentation; bounded Unity uploads; no generator code in JavaScript or Unity. `docs/VALIDATION.md` records available-host evidence and unverified targets. Keep generated caches, binary outputs and screenshots out of source control. The attributed, reproducibly derived bundled Earth elevation asset is intentional source data, not a generated build cache.

## GitHub checkpoints

The owner requested GitHub as the project backup on 14 October 2026. Commit and push completed work to the existing `origin/main` after appropriate validation, checking staged contents for secrets and generated outputs first. Preserve existing history; never force-push or rewrite it without explicit permission. Verify the remote commit before reporting a successful backup. Do not create routine duplicate local backups unless requested. User saves, music recordings, model weights, caches and build/test artifacts do not belong in source commits; GitHub source checkpoints do not back up those files.
