# WorldGen repository instructions

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
