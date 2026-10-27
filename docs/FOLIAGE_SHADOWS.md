# Foliage shadow and near-foot density checkpoint / 22 October 2026

## Vegetation communities and coarse foliage shade / 26 October 2026

Owner explicitly permits different representations and small visual compromises,
and requests realistic habitat structure rather than identical dense forests.
Detailed foliage shadow outlines are unnecessary even nearby; animal shadows
still need their actual geometry. This pass changes placement as well as rendering.

The previous closed-stand gap cutoff(.16) and high density floor produced very
little variation through much of closed forest. A shared planet-stable stand field now
uses community gap/variation traits blended across source profiles. Wet closed
rainforest/mangrove communities have smaller/rarer openings; temperate/boreal
stands vary more, with the existing fire potential increasing structural variation.
The fields approximate static natural gap/recovery/competition conditions, not
simulated dated fires, actual treefall events or a guaranteed clearing every N metres.
Climate/soil/productivity/salinity/permafrost/rooting support continue to constrain
forest occurrence. No trees are planted just to fill visual holes.

Tree recruitment now budgets expected crown area using lineage width and existing
size cohorts. Large crowns self-thin instead of piling the same fixed stem density
into an opaque ceiling. Sparse woodland/savanna is exempt from closed-stand
competition; the old savanna placement was itself excessively sparse, so its
moisture/fire-dependent opportunity was recalibrated. Ground-level flora, small
specialists and mid-height taxa sample the SAME local canopy field: grasses and
open scrub favour light; moist ferns/moss and shade specialists favour shelter.
This is approximate potential canopy light, not a per-plant ray-traced census.

Grass light demand is nonlinear: open-ground grasses fall off strongly beneath
closed crowns, rather than maintaining a knee-high forest lawn after a small
linear shade discount. The original descriptive biome response is removed before
applying this local response; normalizing against the new curve would cancel the
suppression. Ferns, moss, sedges and specialist taxa retain their own responses.
Open-ground quality, near-foot density controls and LOD ranges are unchanged.

Mangrove suitability excludes unrelated conifer/palm/tree-fern lineages despite
shared regional flood tolerance. Warm lowland seasonal forest limits conifers;
existing warmth/cold limits retain palms/canes/tree ferns in suitable warm habitats.
Cold or shallow-soil trees are stunted; shade-sensitive young cohorts remain
smaller under cover. Regional fictional lineages and their climate/soil tolerances
remain, rather than replacing them with literal Earth species everywhere.

Rooted ground vegetation now fades on soil-bearing slopes, reaching zero near52deg;
moss/lichen crusts retain a separate rock-friendly rule. GroundCoverPlacement checks
the ACTUAL drawn grid triangle, so interpolating grassy corner samples cannot
spawn blades across a cliff. Coarse ground sampling still cannot resolve every
small soil pocket; no claim of centimetre-scale botanical accuracy.

Generic ground ribbons and the grass material no longer duplicate fern and
succulent coverage; those retain their specialist/mid-plant representations.
Wet sedges can still make suitable riparian forest floors lush. This pass does
not assume that every forest should have a bare floor or scattered clearings.

Vegetation -> **Soft coarse foliage shadows**, together with **Canopy shade**,
uses the already retained256-square placed-crown field from the player's feet
through its existing600-800m coverage. Bilinear spatial filtering and fractional
crown area give soft partial shelter, with height-aware release upward through
foliage. Crown shadow geometry retires once this field is ready; only tiny12tri
solid-trunk proxies and actual animal/other-object casters remain in the sunlight
atlas. The field refreshes on existing placement/anchor changes, not every sun
sample, and does not follow individual wind-blown leaves or predict long lateral
foliage shadows. This intentionally approximates overhead forest shelter.

Shaped crown proxies remain a warm-up/off fallback. Cache readiness changes switch
submission in the same frame. The old saved cheaper-shadow key is retained; soft
shade off restores shaped proxies, Canopy shade off disables the field, and
Simplified forest shadows off restores original actual-tree casting. Atlas/ranges
remain; foliage receivers use at most4filter samples (2lowest/low-sun). No
interpolation or cache of sunlight shadow maps is added. Existing spatial field
replacement blending remains.

Bare terrain PBR, rocks and fauna previously bypassed cached canopy shading in
their main lighting term. They now attenuate an approximate sunlit component,
retaining indirect sky light, actual solid-object shadow outlines, secondary
lights and emissions. This is an inexpensive lighting approximation, not a full
separation of PBR lobes or measured forest radiative transfer. Grass, herbs,
shrubs and rebuilt trees already use the shared light path. Animal meshes,
rigs, animation, opacity masks and textures remain unchanged.

Two intermediate geometric-only versions were rejected in root visual review:
a grid of octahedra cast obvious diamonds; a rounded crown still cast a solid
blob. Their ignored review directories are failed/interim evidence, not the
selected final player or claimed final performance.

Research basis: [USFS crown competition and canopy gaps](https://research.fs.usda.gov/treesearch/6637),
[NPS old-growth gap regeneration](https://irmadev.nps.gov/DataStore/Reference/Profile/2257057),
[USFS coastal temperate rainforest structure](https://research.fs.usda.gov/download/treesearch/67202.pdf),
[Missouri native savanna disturbance](https://mdc.mo.gov/discover-nature/habitats/grasslands-prairies-savannas/savannas).
These inform the qualitative relationships, not the project's numerical tuning.

Focused checks:23053community,152772canonical forest/transport,220tree-field and
5758ground-flora assertions pass. Local and distant exact census still agree;
source world hash unchanged. Fixture densities: rainforest316.8,temperate139.5,
boreal192.5,savanna9.7,grassland0.1stems/ha;desert/tundra0. These are fixture
observations, not universal standards or saved ecological population counts.

Final IL2CPP player `player-vegetation-communities` built
2026-10-27T01:33:42.7381041Z. Verified532source hashes, staged assemblies and all
177corrected fauna bundle hashes. GPU fixtures produce64flat-grass roots and
zero cliff/duplicate-fern/succulent roots.48map probes retain material/profile/
height/water equivalence. Actual rendered savanna, tropical and temperate views
were inspected from four ground headings and an overview; this is not an
all-biome or seasonal visual certification.

Performance is NOT a certified improvement. Earlier short hidden-player forest
windows reached117-125FPS, but later repeats were58-62FPS; those earlier numbers
must not be advertised as a reliable gain. The final same-build comparison gave
58.43FPS cached shelter versus60.09FPS shaped crown proxies. Per-frame submission
plus explicit GPU completion averaged15.867ms versus15.447ms, excluding Update/
presentation/frame-loop waits; these are not isolated GPU timestamps. Thus the
soft representation removes crown caster work and changes the appearance, but
does not establish a net standalone shadow speedup in this fixture. PresentMon
produced no usable present data. Its owned orphan trace was stopped. The dense
forest performance problem remains open; hidden-window pacing was a hypothesis,
not a confirmed explanation. Evidence: ignored
`artifacts/vegetation-communities-20261026`, especially `render-cost` and the
15-view `community-review-contact.jpg`. Existing shortcut selects this build;
`player-tree-efficiency` remains available for rollback.

## Retained tree submission and shared shading / 26 October 2026

The saved Vegetation control **Efficient tree rendering** defaults on and can
restore the prior submission/shader/wind path for comparison. Tree density,
mesh geometry, grass quality and shadow ranges are unchanged.

Existing persistent forest GPU buffers now also handle exact individual trees
just beyond local mesh LOD: the local-to-field blend ends at120m on Medium and
162m on Ultra, instead of350m. These are billboard submission distances, not
mesh-detail or forest-population reductions. The old exact-census radius is
retained. Near pages extend to the player and must ALL be level-zero/resident
and finish their actual tree bakes before the closer handoff activates. Coarse
ancestors cannot retire close CPU trees. Incomplete coverage retains fallback.
This reduces roots copied/culled/rebatched on camera motion without changing
which tree census supplies the surrounding forest.

Billboard vertices prepare view choice and affine ray projections. Fragments
reuse initial colour/coverage samples across detailed/simplified LOD blending;
the prior path redundantly sampled those textures again. Tiny horizon crowns
retain their one-view path. Coverage, depth correction, normal reconstruction,
alpha rejection and dithered transitions remain. The shader keeps a literal
legacy variant for same-build image and timing checks.

Near mesh trees dispatch a small compute buffer once per frame: geographic wind
noise, local strength and primary trunk bend are evaluated per submitted tree
instance rather than per vertex/pass. Leaf flutter still evaluates on individual
vertices after the primary bend. Root inputs upload only when batches change.
The cache is camera-scoped; bake/fixture cameras and unsupported compute/SM4.5
hardware retain the old vertex path. No wind-update throttling or temporal
interpolation is introduced. Duplicated mesh LOD instances can each have a root
entry, still much less work than evaluating the root field at every vertex.

Short same-build1080p/6km tropical-forest checks with Ultra vegetation passed
image equivalence: shader0pixels>2RGB (maximum2), shared wind exact, nearer
handoff7/2,073,600pixels>2 (mean maximum-channel difference0.00017).
Local CPU-submitted tree count5514->1225; the persistent field retains the
same321,654root records, and grass remains896,000candidate slots. The closer
handoff changes submission, not census density. Root inspected the actual
render; this is not an all-scenes visual certification.

Settled10s cases: steady41.49->43.02FPS; turning29.28->30.70FPS. Turning
batch work9.00->6.07ms and scatter update9.09->6.16ms; shadow batch work
remains~2.1ms. This is a modest~4-5%frame-rate gain, not a fix for the remaining
heavy foliage pixel cost. Isolated legacy-wind43.74FPS and legacy-shader43.52FPS
cases did NOT establish independent gains for either GPU change in this view;
keep their literal A/B paths and do not infer a universal win from fewer shader
operations. FrameTimingManager was unavailable; these are completed-frame wall
times with an end-window GPU barrier, not isolated GPU timings. The initial
verification window was not fully settled and is excluded from timing claims.
Evidence: ignored artifacts/tree-efficiency-20261026/probe. Release IL2CPP
build and132source hashes/stagedDLLs checked;177packaged fauna bundles retained.

## Forest overlap and streaming hitch work / 26 October 2026

Owner reports grass-off still only reaches the low100s and asks to prioritize
overlapping foliage plus intermittent walking freezes. They report the freezes
do not recur when traversing the same fully loaded patch. The no-launch/test
restriction is explicitly lifted; use short tests proportional to the change.

Baseline hidden-player tropical forest turning/walking traces record maxima
42/52ms, including vegetation updates around20–25ms. Those60km scenes were NOT
fully settled, so average FPS is not a clean isolated comparison. Evidence is
under ignored forest-overlap-stutter-20261026/baseline. Input Earth is unchanged.

Terrain changes previously reprojected every root in an overlapping source
chunk, rebuilt all matrices and all bounds. The new path visits affected spatial
groups, queries ground only for roots in changed tile footprints and updates
their height/matrix/bounds immediately. Rotation, scale and X/Z remain valid.
Chart/origin changes still use full reprojection. Distant individual roots get
the same footprint test; aggregated stands include their neighbouring slope
sample footprint. Enlarged historical group bounds remain conservative. No
height corrections are delayed and terrain is not moved to match trees.

An experimental saved Vegetation control, Tree depth prepass,
adds a tree-only silhouette depth pass before opaque shading. It reuses actual
geometry, wind, alpha/parallax coverage and instance LOD dithering; no solid
crown substitutes hide gaps. Forward rendering keeps LEqual and all non-tree
surfaces keep their prior path. The pass writes the existing depth attachment,
not a new full-screen colour target. Near and forest-packet shaders opt in via
WorldGenFoliageDepth; depth-only output omits lighting. It duplicates silhouette
work, so visual equivalence and on/off timing must decide whether to enable it.
Grass quality, blade distances, density, populations and tree geometry unchanged.

The first completed probe preserved pixels exactly with the depth pass on/off,
but was slower (turning36.4vs38.6FPS; travel43.7vs49.7FPS). It defaults OFF.
These are same-build short windows, not a universal GPU result. A replacement
saved control, Prioritize nearby trees when drawing, splits visible local tree
batches into64m radial bands; Unity otherwise sorts the entire species batch
as one broad object. The admitted roots/models/LOD coverage remain unchanged;
more draws trade CPU submission against hidden-pixel work. This needs its own
same-build comparison before a performance claim. Its first short turning run
did not establish a gain (66.4FPS with bands, not fully settled, versus70.4FPS
without, settled); it now also defaults OFF. Both experiments remain optional.

The probe also reproduced106–112ms travel frames around6.7s with45–49ms in
projection and80–88ms in total scatter updates. Slicing incoming installation
alone did NOT fix these frames; do not claim it did. Incoming chunk projection is now
time-sliced, at most384roots and about1.5ms per frame (checks every16roots).
Unfinished chunks do not count as ready and do not render; existing fallback
coverage remains until complete. Already processed roots queue rechecks if
their ground changes during installation. Chart/origin changes restart the
unpublished chunk. Final grouping remains a bounded-per-chunk operation.

Further inspection found a missing terrain ownership test: a changed COARSE
parent could invalidate every root in its rectangle even though an unchanged
finer surface still supplied its visible height. Each root now tracks its
actual ground LOD; changes coarser than that owner do not reproject it. Removed
fine owners and arriving finer surfaces still invalidate immediately. Distant
stands retain the coarsest level among their centre/four slope samples. These
CPU-only levels follow field slot reuse/compaction and prepared chart adoption;
the GPU root layout remains64bytes.

Selective/full projection verification passed32724matrices, including1621
deliberately displaced roots repaired in4chunks. First probe pixels had zero
RGB difference between depth on/off; actual forest image inspected. Validation
of the newer sliced installation and distance batches is recorded below. The first
probe build had been intentionally cancelled for the owner's uncontended test.
Detailed opt-in trace slots now separate batch/bake/projection/forest/shadow/
ground-cover/canopy/forest-draw work. The benchmark also supports a shorter
--worldgen-perf-view without changing ordinary game view distance.

Final ownership run on Ryzen5700X/RTX4060,1080p, medium vegetation,6km benchmark
view,12s travel: the6.5–7.2s boundary window maximum fell110.90->37.45ms versus
the preceding same-route normal release; projection49.29->7.23ms and total
scatter87.84->21.31ms at the reproduced spike. Whole-walk maximum46.55ms still
includes an isolated frame with only.083ms scatter, so this is NOT hitch-free.
Mean travel85.36FPS versus85.04 in the prior normal release is essentially
unchanged; the demonstrated gain is the repeated streaming hitch, not a general
forest GPU speedup. Some streaming remains during travel; do not compare these
means with the earlier60km/partially loaded baseline as a clean FPS claim.

32724selective/full matrices and32724coarse-parent ownership guards pass;
1621displaced roots repair. Both optional render experiments match0RGB pixels
at>1channel difference;6466admitted objects unchanged by distance batches.
Release IL2CPP player-forest-overlap-streaming, BuiltUtc2026-10-26T22:56:12.6444738Z,
compiles/packages without C#/shader errors;130runtime/shader inputs and staged
DLLs match,177fauna bundles/native sources unchanged. Existing shortcut selects
this tested package. Broader forest GPU optimization and owner Ultra-scene
review remain outstanding; no200FPS claim. All owned test/build workers exited.

## Steady sunlight and bounded low-sun casters / 26 October 2026

The owner reports about10 FPS at sunrise/sunset versus80–90 at noon and130+ at
night. These are owner observations, not agent GPU profiling. Source inspection
found an unbounded culling policy: below light-up .01 every loaded vegetation
caster was accepted; slightly above it, a conservative vertical-drop estimate
multiplied by the inverse light elevation. At one degree and the default180m
shadow range the old broad-phase reach was nearly20km. The local forest is
smaller than that, but the estimate effectively admitted its entire loaded area.
The frustum-diagonal receiver bound also exceeded URP's actual radial shadow fade.

Vegetation caster selection now uses the radial receiver range and a finite
offscreen allowance:256m at the full180m setting. Tree caster coverage fades
over224–256m at that range. This intentionally approximates very long, distant
cast shadows; existing cached broad canopy shelter remains available while the
direct light is active. This pass does not add ambient canopy occlusion.
Visible fallback scatter meshes obey the same bound. Shadow batches are divided
into128m spatial bins so one huge instanced-group bound does not keep unrelated
casters active in every cascade. Coverage property blocks update with batches,
rather than being uploaded again on every stationary draw.

Sun-facing alpha cards can have poor fill cost at grazing angles despite their
two-triangle geometry. Distant trees now use 40–72-triangle opaque crown casters
under low light, smoothly transitioning to cards as light-up rises .20→.30
(roughly11.5–17.5degrees). Near proxies remain. Near/coarse/card representations
partition complementary ordered coverage; coarse crowns also serve as the
fallback before an atlas is ready. This adds no shadow texture/camera/cache.

New saved **Reduce detailed shadows at dawn/dusk**, default on, reduces the
detailed receiver range smoothly from180m to60m toward the horizon. The four
cascade endpoints change from16/48/100/180m to16/32/46/60m. At the shortest range
the outer caster allowance ends at136m. Atlas resolution is capped at2048px
below light-up .28 and1024px below .10; foliage filtering caps at4 then2 samples.
Direct sunlight fades smoothly between8 and2.5degrees elevation, reaching zero
below2.5degrees. Existing sky/ambient light remains; no new ambient light is
invented. Continuous range changes refresh vegetation batches at most once per
second; normal movement/settings invalidation remains immediate.

The installed original The Forest's serialized controller has
LowerShadowsAtExtremeLightAngles enabled. Its controller shortens horizon shadow
range to about35% of the configured range, reduces higher-preset shadow
resolution at low angles, and fades direct sunlight to zero near2.67degrees.
Ambient SH presets and bounce contributions remain. These are local code/data
observations, not an inference that it caches all shadows or stops shading.
Inspection/settings evidence is retained under ignored sun-shadow-budget-20261026.

New saved **Steady daylight shadows**, default on, is a presentation-only light
pose. Daylight above12degrees can hold while the observer remains still, then
catch up over five seconds during movement/looking or after5degrees of drift.
Near-horizon daylight follows through an eased catch-up; moonlight, explicit
resets and large clock jumps use the current pose. The visible sun and weather
sun/flare direction follow that same presented pose. The clock, weather dates,
physical day length and celestial simulation remain unchanged. This was written
independently from behavior observed in the installed original The Forest's
lighting controller; proprietary source is not copied into the game.

Holding a light direction does not itself cache shadow maps. URP continues to
render the existing main-light atlas; the performance fixes here bound admitted
casters, reduce grazing-angle alpha overdraw, taper the dawn/dusk budget and
improve batch culling. Dynamic
animals keep their existing realtime shadow path. F3 shows held/following status,
caster limit and card/coarse counts.

2,475 pure sun-motion/caster-budget checks pass, including the old inverse-angle
failure, high hills/below-horizon light, finite reach, distance fades, stable
daylight, catch-up/reset behavior, dawn/dusk budgets and four frame rates. Mesh-only inspection
measures256coarse casters (maximum72triangles) and verifies512visible near/middle
mesh signatures unchanged from the preceding build. No world/player/rendering
was launched by these checks. Release IL2CPP artifacts/player-sun-shadow-budget,
BuiltUtc2026-10-26T21:50:34.0671788Z, compiled/packaged without C#/shader errors.
128runtime/shader source hashes and staged DLLs match; all177 fauna bundles and
native sources are preserved. Existing shortcut selects this package. No game
launch or profiling; actual lighting transitions and FPS remain owner-review
pending. The earlier render-LOD section below describes the preceding build.

## Dense forest render LOD follow-up / 26 October 2026

Two separately saved Vegetation controls default on: **Cheaper distant tree
shadow cards** and **Cheaper distant tree shading**. Both can be disabled for
an owner A/B comparison. Population, placement, shadow distance/cascades and
sun motion remain unchanged; the stable loading-fog fix is retained.

Shadow proxies are retained nearby, then crossfade over 64–96 m measured from
the existing conservative tree bounds. Beyond that interval a tree can cast
from one two-triangle card facing the light. It samples three alpha captures
from an existing shared tree bake, without colour/normal/depth reconstruction.
The old proxy remains until that bake is ready; a missing texture never removes
the tree's shadow. Near/far shadow fragments use complementary ordered coverage
through the overlap. Both use the existing main-light atlas, not a new shadow
camera. Borrowed bake models are pinned against eviction while used by shadow
batches; card materials do not own those textures. F3 reports card instances and
submitted caster triangles before any cascade repetition. Cards approximate
light-space depth/self-shadowing and require visual review.

Visible impostors share one sampling implementation between CPU-instanced trees
and GPU forest fields. Close impostors retain three-view colour/normal/depth
reconstruction (normally 12 texture samples). Beyond quality-dependent ranges
they blend to three colour/alpha samples and smooth crown-envelope normals;
captures only a few pixels across can blend further to one colour/alpha sample.
Very low/Low/Medium/High/Ultra start this distance transition at 80/100/120/160/
220 m and finish at 1.5 times that distance. One-view projected-diameter starts
at 12/10.25/8.5/6.75/5 pixels and finishes at half those diameters. Mixed transition
bands evaluate both representations; these are steady-path sample reductions,
not a guaranteed scene-wide FPS improvement. Detailed lighting, alpha clipping,
canopy shade, shadows and opaque distance fog still apply.

Middle-distance palm and tree-fern leaflets retain the same roots/tips/counts,
but use two-triangle flat kites instead of folded strips. Fan palm middle meshes
drop 3312→1062 triangles; feather palms 6372→2052; tree ferns 5116→1684 (67–68%).
Mesh-only Unity inspection of 256 specimens verifies every close-mesh vertex,
normal, index and UV1 signature is unchanged. No scenes/worlds/rendering were
run for that inspection. All 658 pure rendering-policy checks pass. Release
IL2CPP `artifacts/player-forest-render-lod`, BuiltUtc
`2026-10-26T21:04:14.3021051Z`, compiled/packaged with no C#/shader errors.
128 runtime/shader source hashes and staged shared DLLs remained stable; all177
packaged fauna bundles and177 native animal hashes match. Existing shortcut
selects this package. No game launch/capture/profiling; shadow transitions,
material-property instancing, visual compromises and FPS remain owner-review
pending. Sun timing and shadow-map caching were not changed in this render pass.

## Owner controls

Game settings > Vegetation keeps the five **Grass quality** presets and adds
an independent **Near-foot grass density** slider. `1x` adds nothing; default
`2x` preserves the former maximum boost; `20x` is ten times that former total.
The full multiplier applies through 5 horizontal metres, then smoothstep falls
to the ordinary field by 10 m. The former fixed boost covered 4–14 m. Grass
quality changes neither the new setting nor the surrounding field's density.
Existing ecological occupancy still limits grass; this does not grow desert
grass, move roots, change habitats or add the proposed near-foot ground mesh.

A sixth compact GPU index list selects nearby roots, and a separate indirect
draw generates disjoint, stable blade IDs. Slider changes update draw capacity
and invalidate cached culling, not terrain meshes or root placement. Extra
capacity is NOT submitted over the entire close-detail radius. Ordinary roots
remain unchanged while individual extra blades retire fractionally. One extra
4-byte index slot per candidate is retained (120 vs 116 accounted bytes/root,
plus one indirect command); high density increases vertex and fill cost and is
not free. The previous short-blade split and accepted wind remain active.

**Simplified forest shadows** defaults on; switch it off for the original
casters/filter/pipeline settings. All presets now share four configured cascade
ends at16/48/100/180m, with a wider final fade. Very low/Low/Medium/High/Ultra
use atlas sizes1024/1024/2048/4096/4096 and foliage filter counts2/4/4/4/9.
Four cascades share the atlas: individual tiles are approximately half its width.
Default is Medium. Quality no longer dilutes near resolution by expanding range.
Softness0.75–4/default2 scales filter footprint; Ultra uses a nine-comparison tent
filter. F3 reports atlas size/filter count, casters/draws and canopy-cache state.
Existing tree/vegetation toggles and the lab sunlight-shadow switch still apply.

**Broad distant canopy shade** is separately switchable, default on. It supplies
approximate shelter past the detailed shadow range; see the bounded coverage
and approximation limits below. Previous60/90/140/180/220m presets are historical.

## Research and choice

- [CryEngine: Shadow Proxies](https://www.cryengine.com/docs/static/engines/cryengine-3/categories/1114113/pages/21268752)
  documents simplified geometry used exclusively for shadow casting and warns
  that alignment matters for self-shadowing. This supports independently
  simplifying a tree's caster, rather than tying shade to its visible leaf LOD.
- [Epic: Fortnite Chapter 4 shadow engineering](https://www.unrealengine.com/tech-blog/virtual-shadow-maps-in-fortnite-battle-royale-chapter-4?lang=en-US)
  describes shipped separate tree shadow proxies, plus difficulties caching
  shadows with moving foliage and a changing sun. Its Nanite/VSM-specific
  findings are not a performance guarantee for our conventional URP renderer.
  We adopt separate casters, not VSM, Nanite or page-cache update throttling.
- [NVIDIA/Crytek: Vegetation Procedural Animation and Shading in Crysis](https://developer.nvidia.com/gpugems/gpugems3/part-iii-rendering/chapter-16-vegetation-procedural-animation-and-shading-crysis)
  discusses vegetation fill/shading cost and inexpensive grass lighting. Our
  first receiving-side change reduces repeated shadow filtering; it does not
  claim Crysis's exact lighting or move all lighting to vertices.
- [Unity: Screen Space Shadows](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/renderer-feature-screen-space-shadows.html)
  explains resolving main shadows to a screen texture, with an extra depth
  prepass and memory cost. This might later reduce grass overdraw's repeated
  lookups, but is deliberately NOT enabled blindly on this procedural renderer.
- [Unity: Cascade configuration](https://docs.unity.com/en-us/engine/6000.0/manual/lighting-overview/shadows/shadow-realtime/shadow-cascades/use)
  and [resolution budgets](https://docs.unity.com/en-us/engine/6000.3/manual/lighting-overview/lighting/shadows-in-urp/shadow-resolution-urp)
  support separating near/far coverage allocation from atlas resolution.
- [CryEngine: Height-map-based outdoor occlusion](https://www.cryengine.com/docs/static/engines/cryengine-3/categories/1114113/pages/21890652)
  documents a broad, approximate layer complementing detailed lighting. Our
  crown-coverage field is an independent, simpler engineering approximation,
  NOT CryEngine's algorithm, a directional shadow map or a full AO solution.

Bundled URP 17.6 `Shadows.hlsl` was inspected directly: its low/medium/high soft
filters use 4/9/16 comparison samples. The current pipeline selects high soft
shadows, four cascades and a 2048 atlas. Simplifying casters and receiver filters
addresses different costs; neither makes ordinary foliage shading/overdraw free.

## Implementation and boundaries

`LocalFoliageShadows.cs` derives an opaque, texture-free proxy from the shared
tree's actual middle model foliage bounds. At most 64 occupied spatial bins
become eight-triangle volumes, plus a coarse trunk. Frond/needle/broad-crown
families follow their actual generated envelope; leaf-mask-free forms use a
coarse source-envelope fallback. The proxy uses the **existing tree matrix and
admitted root**, never an independently guessed forest population.

Meshes are shared by render code, capped at 96 resident types; at most one new
type is built per scatter-batch update. Existing casting remains until its proxy
exists. Reused batches contain up to 256 matrices. They draw ShadowsOnly with
a Resources material explicitly retaining instancing variants. Normal visible
tree models stop casting when their proxy is available. The proxy does not
crossfade/disappear when a visible tree changes to its baked view. Existing
conservative caster bounds plus light-space receiving-frustum bounds retain
offscreen trees that can shade the view and reject irrelevant groups first.

Camera-scoped `WorldGenFoliageShadows.hlsl` uses the selected2/4/9 comparisons
for grass, tree/forest surfaces and terrain ground-cover overlays, with an early
out beyond the shadow fade/frustum. Terrain and ordinary object/herb/bush PBR
retain the stock BRDF/GI/additional-light/filter path; a narrowly scoped main-light
hook adds broad canopy visibility without duplicating their lighting evaluation.
`WorldGenLighting.hlsl` includes bundled URP RealtimeLights before temporarily
wrapping GetMainLight during Lighting.hlsl inclusion. This relies on reviewed
URP17.6 include/call structure and needs review on package upgrades. No URP
package source is edited. Wider filtering/coarse casters are not leaf silhouettes.

The fixed180m coverage changes the **existing pipeline's directional atlas**,
not a second foliage-only atlas: other objects' coverage also extends. Distance,
resolution, four-cascade split/count and final-border settings are restored on
mode-off/exit when they still match this controller's published values. Scene
globals avoid per-tile material updates. Quality/mode/density/softness and canopy
toggle are machine-local preferences saved only by the UI.

## Night-time rough-surface reflections follow the generated sky

The camera-only sky override did not replace the scene default skybox/reflection
environment. Bare terrain and LocalScatter use URP PBR environment specular,
whereas grass/cover use custom ambient lighting. That mismatch is a concrete
source-level suspect for the owner's grey-blue glowing cliffs at night; its
exact screenshot contribution has not been isolated in a running player.

WorldGenLighting now selects URP's constant-environment reflection path and
substitutes `_WorldGenSkyReflection`: the same already-linear generated
SkyFrame.Ambient illumination used for terrain diffuse GI and vegetation. One
global vector is published at the existing10Hz sky update; w enables it, and
exit clears it. Standalone/editor materials fall back to engine environment
colour when unbound. Macros are scoped around bundled Lighting/GI inclusion,
without changing Unity packages, the BRDF, direct lighting or shadow visibility.
This is a cheap spatially uniform approximation for rough terrain/scatter, NOT
a directional sky cubemap, local reflection probe system, SSR or new wet-surface
renderer. It intentionally changes indirect specular in daytime as well as at
night, avoiding a mismatched static cubemap and any reflection-camera captures.
Textures, roughness, geometry, SRP material layout, water's separate shader path,
vegetation generation and all world/save data remain unchanged. No emission was
found or added. Compile/package-only review; owner compares day/night cliffs
and rocks, and retained moonlight. No visual or FPS acceptance follows from build.

## Smooth sunlight and broad canopy shelter revision

Source review found SetDaylight assigning the light rotation only every0.1s.
The first revision interpolated those poses; owner reports worse flutter/flashing
and requests direct position updates instead. CURRENT presentation evaluates the
actual SunDirection/MoonDirection orbit/parallax queries every rendered frame,
using current clock inputs and observer position. No previous/target poses,
history, Slerp or0.1s presentation delay. Full sky/weather, colour and intensity
calculation stays10Hz. MoonDirection is an allocation-free query using the same
orbit/sidereal/observer formulas as the full sample; no extra full sky arrays or
weather queries per frame. Visible sun/moon discs and primary light use the
direct directions; light roll stays continuous instead of switching an up axis
near the zenith. No shadow cache or extra shadow render pass is added.

Terrain lab > Lighting adds session-only Freeze sun/light direction and
brightness. It holds the directional light's rotation/colour/intensity while
sky discs, clock, weather, wind and camera continue. Reset/exit clears it; chart
changes reinitialize the pose in the new frame. It is a diagnostic, not another
shadow technique or a persistent setting. Continued flashing while stationary
with this enabled means moving sunlight alone does not explain the issue;
ambient/weather/wind and shadow aliasing can still vary. No confirmed universal
flutter cure or pixel-equivalence/performance claim. Cascades, shadow filters,
caster meshes and canopy-cache replacement blending remain unchanged by this
follow-up. No actual sunlight-map interpolation/caching is introduced.

`LocalCanopyShade.cs` summarizes existing resident GeneratedTree roots, dimensions
and crown-base traits into a world-anchored256² field covering1536m. It is not
biome-wide darkening or independently guessed tree placement. Optical coverage
and premultiplied mean crown-top/base heights allow bilinear filtering without
height leaks from empty texels. Narrow crowns receive fractional area weights.
Each pixel's height attenuates shelter up through the crown: upper surfaces are
not darkened as if they were the forest floor. Sparse openings retain light.

One incremental main-thread build is bounded to1024 inspected instances and a
target0.6ms raster work/frame, checked between small root batches; a single large
crown/upload can exceed that target. Finalization advances2048 pixels/frame.
No per-pixel CPU terrain queries, tasks/worker permits, new root generation or
per-renderer property updates. Reusable CPU pixels take1MiB; two RGBAHalf GPU
textures take1MiB total before overhead. Upload occurs only after a whole field
finishes. Idle, unchanged fields do no rebuild work; resident root/projection,
population/settings changes or128m anchor shifts refresh after completion with
at least1s spacing. Map-open pauses construction. Old/new fields blend0.5s at
their own origins; borders feather over96m. Floating origins translate valid
field rectangles; full chart changes reject old fields rather than shade the
wrong location. Fresh chart/load coverage requires a newly built field.

Shading blends in90–160m before detailed shadows finish. It limits main-light
visibility rather than multiplying the final image or stacking two black shadows;
terrain, grass, trunks/lower tree foliage, herbs/bushes and rocks share it. It
does not affect unlit diagnostics, shadow-disabled modes or unrelated bake
cameras. Ambient/secondary light remains. Quality-independent broad coverage
extends roughly600–800m around the player, depending on anchor position, from
resident tree sources. This is NOT20km sunlight, directional ray tracing,
building/mountain occlusion, a true AO hemisphere, or accurate lateral shadows
at low sun. Existing horizon canopy colour/representation remains beyond it.
These limits avoid turning a large last cascade into millions of tree draws.
Steady shading makes one canopy texture lookup inside valid coverage, none outside the
field/near90m. Only0.5s replacement blending reads both fields. Grass/shadow
quality changes alone do not regenerate the canopy cache; population changes do.
Proxy upper-face winding was also corrected so shadow-bias normals point outward.

This is not baked/cached sunlight, a full ambient occlusion system, contact
shadows, ray tracing, distance fields, physically computed penumbrae or per-leaf
wind shadows. URP still draws the atlas each frame. Proxies are rigid and coarse;
near self-shadow mismatch, canopy solidity, cascade softness/resolution and
greater-range cost require owner review. No pixel-identical appearance or FPS
gain is promised. Terrain geometry/LOD, water, ecology, placement, world/history,
save/worker schemas and vegetation animation are unchanged.

## Validation policy

Owner lifted the old compile/package-only restriction on26October2026. Use
short runtime image/performance checks proportional to the change; owner game
processes remain untouched. Compiler/package checks alone do not establish
visual or performance acceptance. Compare shadow quality and the separate canopy toggle from the
same view. Fixed180m coverage can cost more than the original50m pipeline or
preceding Very-low60m range. Density20x is an expensive experiment.
