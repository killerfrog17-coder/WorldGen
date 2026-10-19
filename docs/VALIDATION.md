# Version 3 validation evidence

## AI-generated non-music effects - 19 October 2026

The IL2CPP `player-weather-sa3`, built17:51:25Z, replaces the previous wind,
rain and thunder synthesizers with17 offline SA3 Medium CUDA recordings.
Music, generation, weather-water budgets and historical rules are unchanged.
Wind is prompted as unobstructed air; vegetation, wildlife, voices, structures,
surf and other environment-dependent sources are excluded from the briefs.
This is a source-isolation intent, not an automated acoustic guarantee.

Completed checks:17/17 generated-file provenance/PCM/hash/duration/layout/
energy/boundary checks;581 shared primitive assertions;5891 actual-world weather
assertions;24 native/CLR numerical comparisons;54 final native assertions;
and Preview fresh/reapply/recipe/cached all-stage hash reconstruction. Native
checks load all17 actual WAVs, read their finite nonclipping PCM, play real
recorded rain sources, traverse overlapping variant banks, pause/resume beds,
avoid immediate thunder repeats and preserve bounded delayed spatial arrivals.
The native listener is muted. These checks do not establish convincing timbre,
perfect audible joins or the absence of every unwanted generated source;
listening/owner feedback remains necessary. No new visual acceptance is claimed.
Evidence is ignored under `artifacts/weather-sounds` and
`artifacts/weather-sa3-verified`. See `WEATHER_AUDIO.md`.

The first two native packaging attempts exhausted transient C: capacity while
copying debug symbols; two incomplete audio exports were not published. The
new build output was moved, not duplicated, to E:/WorldGenBuilds/player-weather-sa3
and is reached through the existing artifacts path via a junction. The final
build and all17 atomic exports succeed. Model weights remain on D:, the
preceding sound-final player is retained, and the user's running game was not
closed. Generated recordings, raw masters, model caches and players stay out
of Git. No footsteps/soil, animal calls or other contextual ambience is added.

## Weather sound refinement and future SFX direction - 19 October 2026

Weather audio retains the existing wind/rain streams and adds three procedural
mono thunder variants, four pooled spatial voices, bolt-distance arrival times,
distance filtering/rolloff and pause-aware scheduling. No footsteps, soil,
animal calls, new generative model, audio downloads or inference during gameplay
are added. The prospective offline SFX workflow and primary model/license
references are documented in EXPLORATION_MUSIC.md. It does not assert that a
new model has been installed, auditioned or demonstrated superior quality.

The IL2CPP `player-weather-sound-final`, built17:21:16Z, passes46 native checks,
81 shared primitive assertions and24 native/CLR weather comparisons. Three
distinct mono signals have finite samples, peaks normalized to about0.72 and silent
boundaries; source settings follow measured bolt-proxy distance. Three nearby
arrivals remain queued during a pause longer than their sound-travel delay,
then consume their queue slots and have an actually playing AudioSource after
resume. The pool is bounded at four. Terrain/history hashes remain unchanged,
and no shader/runtime errors are reported. Evidence is ignored under
`artifacts/weather-sound-verified`. Native tests mute their AudioListener:
these signal/scheduling/playback checks are not a subjective listening review,
certified mastering, proof of convincing thunder timbre or a full acoustic model.
The preceding textured-final build is retained as rollback.

## Textured cloud surfaces and bounded lighting - 19 October 2026

The cloud-lighting revision separates connected broad masses from fine3D
erosion, applies erosion to the actual height-shaped volume boundary, and
shades its density-derived surface slopes. Lighting uses bounded sunward
probes and is reused through view-ray sections, with at most two evaluations
on Low and four on Medium/High. Main ray counts, resolution caps, the existing
small noise volume and all authoritative weather/history queries are unchanged.
Distance mip selection and quality-correct upsampling limit distant aliasing
without blurring High as if it were quarter resolution.

The first smoother-looking lighting experiment was rejected by screenshot
review despite passing its original broad-contrast test. The revised native
harness separately measures local underside detail, compares actual lighting
against a flat-lighting control, and verifies overhead opacity with rain haze
and particles disabled. These are targeted rendered-image diagnostics, not
scientific validation or a claim of photorealism. The added midday scattered
cloud and evening captures exercise more than one coverage/sun angle.

The final IL2CPP `player-weather-textured-final`, built16:55:34Z, passes41 native
checks,81 shared primitive weather assertions and24 native/CLR weather
comparisons. Terrain/history hashes remain unchanged and no shader/runtime
errors are reported. Final captures under ignored `artifacts/weather-textured-final-review`
are inspected at Low/High, scattered cover, evening light and above-cloud height,
including storm underside with haze/particles disabled. That roof's luminance
standard deviation is8.35 and local detail contrast1.55 on the0-255 image scale;
the flat-lighting control changes no geometry. These numbers guard against
featureless interiors in this fixture, not general perceptual quality.

At1280x800 on this RTX4060, GPU-synchronized whole-frame medians/p95 were:
clouds Off4.81/5.50ms, Low5.34/6.12ms, High5.55/6.31ms, spatial Low5.35/6.03ms.
The differences include ordinary CPU/driver variation and are not isolated
GPU shader timings or guarantees for lesser hardware. No new dynamic fluid
solver, precomputed full-world cloud atlas, streaming volume or ground effects
are introduced. The preceding `player-weather-clouds-final` remains rollback.

## Rounded cloud volumes and stronger precipitation visibility - 19 October 2026

The IL2CPP `player-weather-clouds-final` built16:31:04Z replaces the dense-cover
slab with three-dimensional Worley billows and a deeper vertical profile.
Near-weighted ray steps reduce missed local cloud volumes on long sightlines.
The actual wet column supplies a nimbus interior in the same cloud volume;
local particles require cover and stop above the nominal cloud top. Rain/snow
visibility coefficients are shared numerically between the reported visibility
and shader: moderate4mm/h rain is about850m, storms about180m and blizzards
about285m. This is intentionally strong game presentation, not measured optics.

81 primitive,5891 actual-world and24 native/CLR assertions pass. The final
player passes38 native checks with unchanged geography/history hashes and no
shader/runtime errors. The new overhead-opacity capture disables rain haze
and particles, so mist cannot conceal a missing cloud. Rounded scattered
clouds, above-cloud tops, storm volume without mist, ordinary/natural rain
and snow captures are inspected under ignored `artifacts/weather-clouds-verified`.
Screenshot iteration rejected a flat-sided footprint extrusion before release.
Final GPU-synchronized1280x800 measurements on this RTX4060 host: Low5.25ms
median/5.89ms p95, High5.56/6.26ms, spatial Low5.45/6.03ms. These are whole-frame
diagnostic timings, not isolated shader costs or low-end hardware guarantees.
Generation, climate, dated precipitation totals and history rules are unchanged.
Cloud families remain rudimentary; this is not a complete meteorological model.
The previous weather-spatial-final player remains rollback.

## Spatial weather, fog and downpours — 19 October 2026

Derived weather-preview-3 replaces viewer-uniform weather rendering with a lazy,
fixed one-degree planetary lattice and bounded regional GPU windows. Source
climate profiles use compact C2 spatial weights; rain, clouds, distant curtains
and the map share interpolation. Decks darken with coverage/water/storm strength.
Background updates crossfade; chart reanchoring preserves the field. Morning
height fog, precipitation visibility, stronger rain particles, seamless rain
audio and independent rain/visibility options are added. `/weather downpour`
and `/weather fog` are new temporary testing modes. Map clouds remain optional.

81 primitive assertions and5890 actual-world assertions pass, including source
cell/grid/meridian/pole continuity, rate-versus-interval water, altitude-invariant
storm water/location, fixed layer heights, renderer-field interpolation agreement,
bounded/polar windows, dawn fog/dry-world rejection, visibility and rain audio.
Of these,5840 are individual rain-column/cloud-cover checks, not5840 independent
worlds.892 sky/chat,2608 celestial and42 saved-play assertions also pass.
Actual representative BWh/Af climate interiors yielded176/2775mm over the sampled
year,2.4%/26.1% rainy samples and3.5%/56.0% overcast samples. These demonstrate
climate-conditioned contrast, not observed-Earth weather validation or universal
frequency targets. Midlatitude cached windows of73×35 nodes measured32–60ms
on this host; a first64×32 global overlay with lazy profile construction took
roughly150–210ms. Queries occur off the frame thread, not as world-cell ticks.

An initial Mono player passes37 native checks and its fog, ordinary/downpour,
natural-rain, two-metre movement and distant-cloud captures are inspected. These
were used to refine haze darkness, field coherence and temporal blending before
the final IL2CPP review. Signal checks establish continuous/unclipped audio,
not subjective acceptance of synthesized rain sounds. No ground accumulation, puddles or dynamic
hydrological terrain changes are introduced.

The final IL2CPP player `player-weather-spatial-final`, built2026-10-19T15:54:08Z,
passes37 native checks and24 native/CLR numerical comparisons within1e-7. Its
fog, downpour, natural-rain, two-metre movement, map-darkness and distant-weather
captures are inspected. Final evidence is ignored under
`artifacts/weather-spatial-final-review`; prior exploratory evidence is separate.
It checks command restoration, unchanged base-map cache, actual volume/depth
occlusion, adaptation/sun effects, fixed cloud heights, natural snow data and
unchanged terrain/history hashes. Shared DLL input hashes match the reviewed build.

Two IL2CPP runs on RTX4060 at1280×800 with a one-pixel blocking GPU readback
give spatial Low total median/p95 of3.86/5.54ms and5.54/6.34ms. The final run's
Off/Low/High medians are all near5.5ms, so external frame pacing/scheduling
limits meaningful isolated overhead comparisons. These are available-host total
frame times, not low-end/fullscreen guarantees or claims of zero weather cost.
The bounded background windows do not establish overall dense-history performance.

Preview3 intentionally changes derived-weather samples: the precipitation field
has broader spatial coherence, fixed-node climate interpolation replaces the old
visitor-dependent bag, and rain/snow share conservative splits. Primitive32-day
water budgets remain conserved; this is not literal preview2 weather replay.
Accepted worldgen/climate/hydrology/ecology and historical records/rules remain
unchanged. Existing playable snapshots are not upgraded to a new historical rule.
Radiation fog and optical visibility are empirical approximations, not a full
dew-point/advection/microphysics solver. See `PROCEDURAL_WEATHER.md` for sources
and limits. The GPU window is temporally cached (five weather minutes plus its
short update/crossfade); this is explicitly visual approximation, not simulated
ground-water history. Climate-input errors still propagate into derived weather.

## Weather testing, optional cloud map and gusts — 19 October 2026

Derived `weather-preview-2` adds extended non-raining cloud decks and regional
coverage fields; the precipitation cascade and all numerical rain/snow/temperature
and retention values remain unchanged.77 focused and11 actual-Earth weather
assertions pass;24 comparisons against the preceding preview1 native fixtures
remain exact within1e-7.892 sky/chat,2608 atmosphere,334 interface,1008 historical
presentation/summary and42 play-session assertions pass. No generation or
historical rules upgrade and no golden updates occur.

Annual3-hour samples of the stored45N/4W maritime profile give5.8% rainy samples
(unchanged), with36.4% overcast versus5.8% previously. The synthetic wet/dry
profiles give73.6%/1.4% overcast and13.3%/0.6% rainy samples. These are regression
diagnostics, not observed-Earth calibration. The cloud map intentionally exposed
the initial overly fine synoptic field; its frequency was reduced from24 to5
in the actual shared provider, not just blurred on the map.

`/weather` overrides are session-only and tested for valid/invalid input,
auto restoration, cold snowfall, high-ground cloud clearance and unchanged play
state. Wind checks cover quiet gaps, greater storm energy, bounded samples,
absence of the old8-second repetition and exact continuity across audio buffers.
These signal tests do not establish subjective recorded-wind sound quality.
The optional128×64 cloud worker is cancellation-tested, spatially varied and
repeatable, and shares natural/override sampling with the explorer. A64×32
actual-world query measured22–26ms on this host; no per-frame world-cell scan.

IL2CPP `player-weather-controls-final`, built2026-10-19T10:55:09Z, passes31 hidden
native checks. These include the actual command-driven overcast/rain/storm and
auto controls, a naturally dated rain event, optional cloud overlays updated by
commands, unchanged cached-base-map build count, cloud/terrain occlusion, snow,
lightning, sun effects/adaptation and unchanged terrain/history hashes. Natural
rain, closed overcast and the broader cloud-map diagnostic composite are inspected.
The composite is a diagnostic texture preview, not proof of native checkbox layout.
The earlier exploratory foreground comparison used different solar dates; the
harness now restores the original date and clears residual drops before that test.
Final evidence is ignored under `artifacts/weather-controls-release`.

Final GPU-synchronized1280×800 total frame median/p95 on RTX4060: clouds Off
2.73/3.69ms, Low2.91/3.90ms, High3.17/4.14ms. These include blocking readback and
are not low-end/fullscreen or dense-world simulation acceptance. The project
shortcut selects this player; weather-atmosphere-final remains rollback.

## Random-access local weather — 19 October 2026

The IL2CPP `player-weather-atmosphere-final` build2026-10-19T10:12:47Z passes
21 hidden native checks: actual low/high volumetric cloud bodies and high thin
clouds, opaque-terrain occlusion, rain/snow particles, controlled lightning,
recovering night-to-day adaptation, glare/ray toggles, above-cloud travel,
fixed physical layer heights during flight, natural polar snowfall/retained
snow-water data, and unchanged terrain/history hashes. Final clear, cloudy,
cirrus, precipitation and polar captures were inspected. This is a soft,
budget procedural visual treatment, not photorealistic cloud acceptance.
Evidence is ignored under `artifacts/weather-release-review`.

47 focused weather assertions pass, including continuous-rate versus interval
water agreement, negative/root-boundary/ancient dates, nearby-point continuity,
wind integration and arbitrary-date retained-water estimates.5 actual-world
profile/hash assertions and24 native/CLR numerical comparisons (tolerance1e-7)
pass.2,608 celestial,892 sky/chat,113 meridian,182 exploration music,334 world
interface,42 saved-play and6 actual-world celestial assertions pass. Release
solution/browser/shared-asset builds pass; native packaging checks staged DLL
hashes before/after building. No goldens are changed.
The final player also passes28 preceding native atmosphere/display regressions
and33 exact native/CLR celestial-tradition checks, with weather disabled only
inside those isolated legacy visual fixtures. Their evidence is ignored under
`artifacts/weather-sky-regression`; the weather-specific fixtures use the new
feature and actual opaque-depth checks.

Core current queries measured roughly0.016–0.023ms; bounded surface estimates
roughly0.11–0.21ms on this host. The final hidden1280×800 RTX4060 check uses a
one-pixel blocking readback each frame to synchronize prior GPU work. Total
median/p95 frame times: Off2.89/4.76ms, Low3.18/5.10ms, High3.52/5.48ms.
These include readback overhead and are not isolated interactive/full-screen,
high-DPI or low-end-PC guarantees; unrelated desktop GPU activity remained.
Low is the default, with Off/Medium/High and independent stylistic controls.
The earlier unsynchronized off-screen timings are not FPS acceptance evidence.

Weather follows the **stored simulated climate**, not observed Earth weather.
An initial Finland/day350 snow-dominance assumption was rejected: that stored
profile has mild seasonal inputs and yielded mostly rain, correctly. The
snow-dominance renderer/data check uses genuinely cold stored polar conditions
instead; no climate weights were changed to force a latitude-based expectation.
Surface snow/wetness/runoff are bounded estimates from matching precipitation,
not an exact snowpack/catchment solver. No ground accumulation, puddles, dynamic
water, hurricane tracks or historical weather events are implemented. Generation
and historical versions/rules remain unchanged. See PROCEDURAL_WEATHER.md.

## Visual sky exaggeration and display preferences — 19 October 2026

The IL2CPP `player-celestial-display` build2026-10-19T08:49:04Z passes28 hidden
native atmosphere/display assertions. Actual1×/5× moon captures are inspected:
the enlarged disk occupies over12times the bright pixels while night illumination
and actual lower-terrain brightness remain unchanged. Sun size/light independence,
saved multipliers and preceding-save1× defaults pass focused checks.2,606 focused
atmosphere,6 actual-world celestial,892 sky/chat,113 meridian,182 music,334
interface,42 saved-play and33 exact native/CLR profile assertions pass. The
Release solution/browser build and shared-asset staging pass without errors.

Native process-local tests verify120FPS/VSync-off, VSync-on, and Unlimited
application settings, restoring the preceding settings without writing the
owner's preferences. This is configuration verification, not a measured frame-rate
benchmark or new interactive UI-layout acceptance. Preference persistence uses
PlayerPrefs; no new cross-launch preference acceptance is claimed. Evidence is
ignored under `artifacts/celestial-display-review`. No generation, climate,
historical calendars or historical algorithms change. Existing saves need no
rules upgrade; celestial-atmosphere is rollback. See CELESTIAL_ATMOSPHERE.md.

## Celestial atmosphere — 19 October 2026

See `CELESTIAL_ATMOSPHERE.md`.2,600 focused orbital/phase/lighting/stellar/haze/
calendar assertions and6 actual-world creation/Atlas/persistence/continuation
checks pass. The latter use a controlled culture-creation hook, not a claim of
calibrated natural cultural emergence.892 sky/chat,113 meridian,182 music,
42 saved-play,1,008 historical presentation and334 interface assertions pass.
Release solution/browser builds pass without warnings/errors. The normal build
script stages current shared worker assets and Unity DLLs. Preview repeated,
reapply, recipe and cached reconstruction retain all stage hashes. No generation
recipe/version, terrain, climate, old goldens or existing historical rules changed.

IL2CPP build2026-10-19T08:25:45Z passes21 hidden native atmosphere checks and
32 existing local sky/chat/meridian checks. Emitted32 authoritative calendar/
tradition profiles additionally pass33 exact native/CLR canonical assertions,
including the explicit deterministic/quantized orbital periods. Ignored evidence
is in `artifacts/atmosphere-review` and `artifacts/atmosphere-chat-review`.
Actual final camera images show the Earth-size full/quarter moon, dark albedo,
rust/ice surfaces, independently orbiting large twins, three seeded stellar
environments, and clear/hazy long-distance terrain. The initial hard cluster cap
and clipping-induced flat ice disk were rejected and refined before this pass.
Full/new moon lower-terrain means are4.768/0.000; clear/haze mean absolute pixel
difference is1.928 on the longer coastal view. These are fixture diagnostics,
not calibrated human visual exposure or scientific atmospheric validation.

F3 text explicitly reports northern seasonal turning-point progress. Actual
native input still verifies chat keys/local time, opposite-longitude night,
date edits, unchanged recorded history, saved settings and absence of shader/
runtime errors. Tests are hidden and do not take cursor/focus or close the
owner's game. Camera render targets are not IMGUI-layout proof; hidden chat UI
captures remain black. No new interactive browser or setup-layout acceptance is
claimed. Authored stellar brightness, multi-moon long-term stability, eclipse
lighting/tides, dynamic calendar reform and clouds/weather remain outside this
pass. The optional saved sky context supplies inherited conventions/observances,
not daily festival economics or a world-wide event queue. Existing saves without
it remain literal. No new all-era historical or dense-world performance acceptance
is implied; the archived Earth golden mismatch remains. World-meridian is rollback.

## Official meridian and local solar time — 19 October 2026

See `WORLD_MERIDIAN.md`.113 focused meridian/solar/persistence/grid assertions,
892 sky/chat assertions,182 music,42 saved-play,1,008 presentation and334
interface assertions pass. Release solution/browser builds pass with zero
warnings/errors; the normal build script stages matching worker/Unity DLLs.
Preview repeated/reapply/recipe/cached reconstruction retains all stage hashes.
No generation/history rules or expectations were changed.

IL2CPP build2026-10-19T07:17:13Z passes32 hidden native assertions in ignored
`artifacts/meridian-review/result.txt`. Earth20k selects the Pacific seam at
177.75°W and opposite prime at2.25°E in stored coordinates, with16° approximate
unavoidable meridional non-ocean crossing. The initial map centers on that prime.
Actual sky images at35°N,4°W show local noon/twilight/night. Without advancing
the shared solar phase, teleporting to35°N,176°E changes local noon to midnight;
the actual explorer supplies nighttime solar altitude/stars and its captured
sky is inspected. Returning to the original site, real chat input sets local
noon correctly. Saved prime, solar phase and calendar roundtrip, recorded history
is unchanged by commands, and no Unity shader/runtime errors occur.

Tests do not change the owner's current app or take cursor focus. Hidden chat
UI screenshots remain black and are not new layout proof. Browser default-center
callbacks compile but have no new interactive browser acceptance. The seam is a
cell-footprint/raster approximation, not a guarantee about tiny subcell islands
or exact coastlines. No new physical atmosphere, orbital/calendar completeness,
all-era history or dense-world performance acceptance is implied. The preceding
sky-chat build is retained as rollback; the archived Earth golden mismatch remains.

## Playable sky and local chat — 19 October 2026

See `SKY_CHAT.md`. The Release solution builds with zero warnings/errors;
892 focused sky/calendar/command assertions,182 music assertions,42 saved-play
assertions,1,008 historical presentation assertions and334 interface assertions
pass. The IL2CPP build2026-10-19T06:43:59Z passes28 native assertions in ignored
`artifacts/sky-chat-final-review/result.txt`. Actual injected keyboard/text events
verify Enter, prefilled slash, help, command execution, history, Tab completion,
chat-first Escape and movement blocking. Calendar/solar offsets cold-roundtrip;
commands do not rewrite recorded historical state. An original multi-biome
generated OGG, with `+` and other filename tags intact, decodes successfully.

Camera render-target images at35°N,4°W were inspected at noon, sunrise, sunset
and midnight. The final night image has179 bright stellar pixels rather than
the first candidate's oversized soft stars; unrelated directional lights no
longer illuminate night terrain. Lower-half daylight/night means are136.40/5.45.
The first coastal test was below the local water surface and was rejected, not
treated as a sky validation. Noon shows one solar disk; no moon is implemented.
Tests ran hidden without taking input/cursor focus from the owner's game.

Hidden-window GUI screenshots are black, and an off-screen/no-activate attempt
encountered DX11 presentation/resolution failures and was discarded. Therefore
the native chat controls are verified but its visual layout is **not** accepted
on the basis of these screenshots. This is not an all-site sky, physical
atmosphere, weather or all-era/performance acceptance claim. Generation/history
rules and goldens remain unchanged; the preceding archived Earth full-suite
golden mismatch remains unresolved. The preceding exploration-music player is
retained as rollback. All30 planned exploration tracks are now complete with
zero generation failures; recordings remain outside Git.

## Exploration music — 19 October 2026

The music-only atmosphere milestone adds a separate streamed gameplay library,
biome/day-period filename requirements, weighted selection, recent-three
avoidance,10–35real-second gaps and collection-switch fades. It consumes the
existing saved solar/seasonal clock, including latitude-dependent night, rather
than introducing weather or changing the historical calendar. See
`EXPLORATION_MUSIC.md` for names, production and explicit limitations.

182 focused selection/catalog assertions,1,008 earlier presentation assertions
and42 saved-play/scale assertions pass; the Release solution builds with zero
warnings/errors. IL2CPP build2026-10-19T06:04:37Z passes18 native assertions
(19 report lines including the sample measurement). It decodes an actual
130.50s generated OGG and measures nonzero AudioSource output RMS0.000312395.
The native check verifies daylight/night eligibility, allowing a playing song
to finish across a context change, silence intervals, volume muting, no era
leakage, return to the original era collection, unchanged coarse geography and
no Unity audio/runtime exceptions. Diagnostic fixtures live under ignored
`artifacts/exploration-player-review`; they do not change the owner's music.

The30-track SA3 Medium CUDA batch is separate from native-player validation.
At checkpoint publication it is still running; use ignored
`artifacts/exploration-music/status.json` for actual completion/failures, not the
planned30 count. Every published OGG passes stereo/finite/duration/energy/
nonclipping/duplicate checks. No claim of listening-based artistic acceptance,
certified loudness mastering or absence of every vocal-like artifact is made.
No generation/history goldens were changed; the preceding full-suite archived
Earth golden mismatch is not resolved or hidden by these focused passes.

## Game-first shell, saved play rules and scale — 19 October 2026

See `GAME_SHELL.md`. Release solution build passes with zero warnings/errors.
42 focused game/save/scale assertions,581 local-terrain/controller assertions,
2,880 scatter assertions and1,008 historical presentation assertions pass.
Preview's repeated/reapply/recipe/cached reconstruction retains every stage hash.
No generation goldens were changed. The pre-existing full Unity Validate failure
(`CLI/Unity stage hash mismatch: earth`, archived stage-count mismatch) remains;
this is not a full-suite-green claim.

The first visible native walkthrough held each screen for5seconds and captured
11 actual1920×1017 UI frames, including menus, setup, simulation, play, F3,
satellite/climate maps and pause/settings.20 native flow checks passed. It exposed
needle-like compressed local terrain and an unthemed map window; owner feedback
also requested distinct setup category tabs rather than nested game-rule groups.
The subsequent revision addresses these; it preserves macro heights exactly and
uses physical local wavelengths. A fixed mountain transect's residual-gradient
RMS ratio is0.099 versus the earlier compressed detail. This narrow metric is not
an all-landform realism acceptance. Final native evidence is recorded below.

Final IL2CPP build2026-10-19T05:20:04Z passes23 native checks and13 visible
screens, each held for5seconds. The owner resized the window during review;
captures include1280×800 and1920×1017. Actual images confirm the default three
world inputs, distinct settings tabs, advanced foldout, themed M map, F3 text,
pause/settings and restored game. At the same polar spawn, broad snowy slopes
replace the earlier needles; snow remains overbright and coarse distant LOD
silhouettes remain visible. This is shell acceptance, not photoreal terrain.
Cold world loading, exact position/scale restoration, preserved saved history
before clock publication, both pause clocks and no return-to-fast-forward pass.
No Unity rendering errors/exceptions were recorded. Evidence is ignored under
`artifacts/game-shell-final-review`; player is `player-game-shell-final`.

New saves carry a versioned sidecar; absent old sidecars remain scale1. Structure
density is reserved metadata, not implemented village generation. Daylight is
visual, not a new seasonal climate/weather model. None of this establishes dense
historical3yr/s performance or resolves the earlier simulation-realism findings.

## Natural cover and replaceable local art — 18 October 2026

`local-scatter-preview-1` is a separate, disposable Core-derived placement layer
over unchanged local-terrain-preview2/worldgen3.32/history32. It uses stable global
addresses and habitat/slope conditioning, with near grass/stones and longer-range
trees/shrubs/boulders. Rendering has separate prefab/LOD bindings, physical instance
dimensions, UVs/normals/tangents, a source instancing material and replaceable
herb/soil/rock/snow texture arrays with PBR channels. No external art/model download,
new licensing dependency, normal HUD or silhouette was added.

Solution Release build: zero warnings/errors.2,880 focused scatter assertions pass,
including repeated/cold/parallel reconstruction, poles/cube seams, unique IDs,
biome/cold/saline exclusions, metre dimensions, cancellation, bounded planning and
complete source-hash preservation.581 local terrain/controller assertions include
normalized ground/skirts surface channels.1,008 historical presentation assertions
pass. Preview CLI repeat/reapply/recipe/cached reconstruction matches all15 stages.
No goldens were regenerated; the preceding unrelated full-suite Earth golden
stage-count mismatch remains unresolved and this is not a full-suite green claim.

Final native IL2CPP build2026-10-19T04:12:36Z passes six sites:80k Earth savanna,
temperate forest and boreal forest, then200k generated grassland, temperate forest
and boreal forest. Each ground-level view is compared with exactly the same
camera after disabling scatter for two frames;24,542–806,606 pixels differ above
the colour threshold. This is an actual rendered-cover check, not merely a count
of objects submitted to the GPU. Ground and overhead images were inspected:
distinct umbrella/broadleaf/conifer forms, sparse versus dense cover, clumps,
clearings, physical scale and seated roots are visible. They are clearly low-poly
placeholders, not photoreal foliage. Six scene input/streaming/teleport/return
checks pass without rendering errors. All24 native terrain-height probes match
independent CLR queries exactly. World hashes remain `f8c11ace…` Earth and
`f2c733d3…` generated; no save or historical rule upgrade is involved.

Brief90-frame warm, stationary1280×800 samples on this RTX4060/Direct3D11 host:

| Scene | Submitted local instances | Instanced draws | FPS | Worst sampled frame |
| --- | ---: | ---: | ---: | ---: |
| Earth savanna | 7,390 | 50 | 59.6 | 20.2ms |
| Earth temperate forest | 51,546 | 225 | 59.8 | 20.8ms |
| Earth boreal forest | 19,087 | 94 | 59.7 | 19.7ms |
| Generated grassland | 4,994 | 41 | 59.7 | 17.8ms |
| Generated temperate forest | 49,138 | 215 | 59.7 | 21.5ms |
| Generated boreal forest | 43,033 | 191 | 59.7 | 20.3ms |

Counts are range-submitted instances, including offscreen objects, not visible
pixel counts. FPS is capped near60; these short stationary samples do not establish
uncapped performance, every streaming-frame bound, arbitrary hardware/high-end art
performance, or the outstanding dense historical-simulation speed acceptance.
Ignored evidence is `artifacts/local-scatter-review` and
`artifacts/local-scatter-generated`, with only this small final capture set.

An initial native review was rejected: objects were generated but absent from
captures, and ground lighting was dark. A Resources material now preserves the
runtime GPU-instancing variants that Unity otherwise stripped; both shaders
compile the installed URP Forward+ cluster-light-loop variant. New visible-on/off
image assertions catch this regression. Captures use explicit Camera.Render and
nonuniform RenderTexture pixels, not black hidden-window ScreenCapture results;
they do not establish OnGUI layout correctness.

The grounded rollback remains available and no running user app was closed.
Prop collisions, plant growth/edit persistence, history-driven land-use clearing,
local reconciled water and species-specific/final art remain future work. Art can
be replaced independently; persistent structures still require a pinned local
surface contract and further terrain/hydrology work.

## Human-scale grounded movement — 18 October 2026

Controller-only revision:1.65m eye baseline,1.5m/s walk,5m/s run,0.45m jump and
9.81m/s² gravity. The old0.35m allowance was applied every frame, permitting
continuous very steep climbing. Uphill moves now test grade, not that allowance.
Normal descents follow terrain at steady eye height instead of repeatedly falling
and landing. Surface-distance budgets avoid an uphill speed boost; actual ledges
start falls. Analytic ballistic integration and bounded20ms substeps cover10–120FPS
and prevent the old50ms time clamp from slowing ordinary movement at lower FPS.
Sideways flight also no longer slows just because the camera points downward.

Solution build has zero warnings/errors.579 focused local terrain/controller
assertions and1,008 historical presentation assertions pass, including walk/run
distance and jump/landing across frame rates, both slope directions, steep-ramp
blocking and real drops. Native IL2CPP build2026-10-19T03:33:07Z passes the existing
three-scene80k Earth input/streaming/debug-map/teleport/return harness, including
actual mouse-without-drag and Space jump assertions. Initialized-world hash
`f8c11ace…` is unchanged, all12 height probes match CLR exactly and no render
errors are recorded. Evidence is in ignored
`artifacts/local-grounded-check`. The current source changes only Presentation's
controller and its tests/documentation, not Core/Serialization or local heights.
DLL informational versions include the source Git revision, so binary checksums
across source checkpoints alone are not a semantic generation-regression test.

The preceding controls4 player is retained and the user's running process was
not closed. No silhouette, body mesh, head bob, new HUD or vegetation was added.
This is still a height-field controller, not finished capsule, obstacle, stairs,
swimming or steep-slope sliding physics. It does not assert completion of the
outstanding historical performance/realism targets or the old golden mismatch.

## HUD-free terrain controls and distance LOD — 18 October 2026

Local terrain preview2 remains separate from worldgen3.32/history32. Solution
build: zero warnings/errors.319 focused checks cover deterministic local samples,
landform separation, walk/jump/landing/cliff blocking, mouse-pitch flight, deep
debug-map sampling/cancellation, cursor-anchor zoom, antimeridian wrapping,
bounded altitude-dependent LOD, horizon coverage/nonoverlap and coarse-edge
collapse.1,008 historical presentation/51 lake assertions pass. Preview CLI
repeat/reapply/recipe/cached reconstruction retains identical all-stage hashes.
The archived Earth golden mismatch documented below is not claimed fixed.

Both200k Earth and Continents Plus pass14 actual-world checks, retaining complete
saved world hashes. Final CPU preparation took588/554ms once; mixed-detail mesh
means26.5/25.6ms, worst96.6/97.1ms. Fixed Earth16km neighbourhood relief spans
3,695m Himalaya,3,653m Andes and421m Tibetan plateau. These are synthetic local
relief over measured coarse inputs, not actual mountain/valley reconstructions.

Final IL2CPP player build2026-10-19T03:11:25Z passes four Earth and three generated
scenes, each with284/284 resident LOD leaves and a200km requested range. At most
two mesh jobs run and one tile pair uploads per frame. Native maximum background
tile times104.8ms Earth/92.0ms generated. This is bounded streaming evidence,
not a universal60FPS guarantee. Native synthetic input asserts F3, F3+M, map
render/zoom/precise teleport, mouse-look with no drag, Space jumping, F/W and
Escape pause. Return restores the map and complete initialized-world hashes
`20f226fb…`/`f2c733d3…`. All28 sampled elevations match CLR exactly. No render
error/exception is recorded. Diagnostic code also exercises movement/re-streaming.

Native images in ignored `artifacts/local-terrain-controls/accepted-earth` and
`accepted-generated` were actually inspected, alongside NASA/NPS reference images
linked in LOCAL_TERRAIN_PREVIEW.md. Earlier views exposed small rounded lumps,
then over-sharp curved ridge walls; those experiments were revised, not passed
as final realism. The final fields use broader irregular mountain masses and
smaller subordinate ridge/spur relief. Cliff faces shed more of the placeholder
snow cover. Coarse/fine edges collapse onto shared coarse segments rather than
relying only on visible vertical skirts. Fine shading/joins remain provisional.

The nominal Point Reyes location resolves below sea level; the review therefore
finds a nearby actual sampler shore at38.069879N,122.790720W instead of accepting
an all-ocean capture. Its coast remains simple and bare: waves, beach sediments,
local erosion/headlands and fine real-world DEM fidelity are not implemented.
The plateau remains comparatively smooth. Camera render-texture captures prove
geometry, not hidden-window GUI layout; keyboard/visibility checks exercise the
HUD-free/debug branches separately. No black screenshots are used as evidence.
The prior player remains rollback. No permanent terrain placement, vegetation,
fauna, inland hydrology integration or historical-rule change is implied.

## First local terrain exploration — 18 October 2026

`local-terrain-preview-1` is a downstream, replaceable local surface and Unity
explorer, not a new WorldPipeline route or historical-rules revision. Base height
uses bounded cubic triangular patches with exact source anchors/shared curves;
compact kernels blend terrain character separately. Visual review caught and
removed an earlier averaging bug that submerged a coastal mountain. The focused
regression now tests a3,000m island surrounded by−1,000m seabed. See
[scope, controls and limitations](LOCAL_TERRAIN_PREVIEW.md).

The solution builds without warnings/errors.199 focused terrain assertions pass:
equal-altitude plateau versus mountain relief, overlapping/cold/concurrent
queries, both poles, finite geometry/normals, identical shared LOD samples,
submerged beds, source anchors, bounds and cancellation.14 actual-world checks
each pass on80k Earth and200k Earth/Continents Plus, retaining complete world
hashes. Final200k CPU checks took612ms once for descriptors/index and32.3/32.4ms
average per mixed-detail chunk (worst147.5/140.4ms). Those jobs run off the UI
thread; these are local mesh measurements, NOT historical throughput claims.

1,008 historical presentation/summary and51 lake-presentation assertions pass.
Preview repeat/reapply/serialized-recipe/cached reconstruction retains identical
all-stage hashes. The full test runner exposed obsolete reflection-constructor
and six-vertex river assumptions; tests now exercise the production SHA factory
and the existing curved/capped river representation. No production hash/river
rules changed. The full runner subsequently reaches an archived Earth golden
mismatch (that fixture lists11 stages, before the current ecology/subcell
stages); it is NOT reported green and no golden was regenerated or approved.

Native review uses direct camera render-texture captures. Hidden-window screen
captures were black and were rejected, not counted as visual proof. Final native
IL2CPP build2026-10-19T01:58:43Z passes three sites each on Earth and200k
Continents Plus:169-tile windows, movement/re-streaming, walking, F/W/Esc input,
return and unchanged complete initialized-world hashes (`f8c11ace…`/`f2c733d3…`).
All24 captured height probes match CLR exactly. Inspected images show gentle
plains, exposed Earth mountains and snow-covered generated ranges; missing-tile
capture gaps and a colour-space fog seam were corrected. Ground remains bare,
and small ridge patterns are provisional, not resolved drainage. Native maximum
background tile build times were145.4/134.1ms; one22ms main-thread upload was
logged. No error/exception was recorded. Evidence is in ignored
`artifacts/local-terrain-preview` and `artifacts/local-terrain-generated`;
images are camera/geometry evidence, not hidden-window HUD layout proof.
The selected launcher is recorded in AGENTS. An explicit shader-error log guard now prevents Unity's
otherwise-successful exit code from accepting a player with a broken shader.

This first pass has placeholder ground materials, ocean surface/bathymetry,
bounded streaming and a ground-following walker/flyer. Inland hydrology,
fractional islands, vegetation, full erosion and persistent placement remain
deferred. Render skirts conceal mixed-LOD joins; finite-difference shading and
ridge profiles remain visibly provisional. No scientific/all-landform realism
or arbitrary-resolution gameplay acceptance is implied.

## War-ending headlines - 18 October 2026

Presentation-only peace summaries distinguish recorded government defeat/fall,
imperial submission with retained local government, tribute, frontier settlements,
trade-route agreements, supply failure and ceasefires. Dated current records may
identify the parties to a defeat/submission; a later fall, patron or conquest is
not projected backward into an earlier peace. Unknown archived terms remain
explicitly unknown. No history events, simulation rules, news windows or saved
hashes are rewritten; no rule upgrade is needed for the wording change.

18 focused cases are included in1,003 passing presentation assertions. Actual
Earth (1,696 archived events) and forest (276,068 events) news audits finish with
unchanged historical hashes. Their latest political summaries correctly report
named defeats ending independent governments rather than generic equal peace.
This is presentation evidence, not new historical-realism/performance acceptance.

IL2CPP `player-history-32-peace-news` was built2026-10-19T00:06:08Z and its
manifest matches staged assemblies. Native representative Atlas sections pass
without changing paused history; its12-year Earth continuation remains exactly
`88d093657d492a7e7621aff9d5fa9c426510a0a62afbc72085baaf1e15243c05`.
The project shortcut selects this presentation refresh, retaining domestic37 as
rollback. Implementation checkpoint `adf0a5d` is backed up on origin/main.

## Persistent domestic institutions - 18 October 2026

[Optional realm37](HISTORY32_DOMESTIC_POLITICS.md) adds saved power groups,
coalitions, funded bargaining and resource-conditioned office contests.14 focused,
985 presentation and25 checks on each actual Earth/forest history pass, including
60-year partition/cold persistence. Old36 remains exact; new37 native and CLR
12-year continuations match. Actual browser power/bargaining screenshots are
reviewed; native representative Atlas sections pass on both inputs without
changing history. The shortcut selects `player-history-32-domestic37` (IL2CPP,
2026-10-18T23:42:03Z), retaining atlas36. This is mechanism/adapter verification,
not all-era historical calibration, a dense browser-import fix or satisfaction
of the outstanding dense200k/max-tech+1000 speed criterion.

## Visual Atlas and pre-cultural heritage - 18 October 2026

The [tab-by-tab review](ATLAS_OVERHAUL_REVIEW.md) and
[realm36 heritage/NPC contract](HISTORY32_FORAGER_HERITAGE.md) distinguish the
presentation overhaul from the new identity Version2 rules.119 focused/971
combined presentation,208 identity,511 sovereignty and4 notable assertions pass;
315 Earth/338 forest actual-world checks cover read-only queries, persistence,
partitioned/cold replay and forager inheritance/divergence. Fresh16k-year Earth
and forest histories have21/8 identities with zero advanced cultures.
Old35 replay remains exact until explicit upgrade.

Actual browser Earth passes all12 category tables, numeric sorting, visible-row
navigation, SVG links and appearance/phonetic/research data; prehistory passes
with21 identities but no advanced cultures. Screenshots were opened and refined.
Native IL2CPP traverses all categories and their representative articles' full
sections on both mature inputs and prehistory, with no GUI errors or changed
historical hashes. Native new36 Earth12-year replay equals CLR exactly. Release
staging and CLI repeat/reapply/recipe/cached reconstruction pass.

Selected player: `player-history-32-atlas36`, IL2CPP, built2026-10-18T19:58:33Z;
world-panel remains rollback. Desktop checks pass, but the dense forest browser
import times out before Atlas entry and is NOT accepted. No all-era ethnic/
political calibration or new200k/max-tech+1000-year performance pass is claimed.

## Unified World panel - 18 October 2026

Presentation-only: one World/W entry, category tables even before recorded
history, combined map settings, selected-subject/overview W navigation and Esc
closure.852 presentation assertions and actual browser interactions on populated
and fresh Earth pass. Empty-city/settings screenshots were inspected; native
IL2CPP keyboard/category/settings checks preserve the full historical hash.
Release staging and CLI reconstruction pass. Selected player:
`player-history-32-world-panel`, built2026-10-18T19:05:25Z, with notables35 retained.
No history version/upgrade or new realism/performance acceptance is involved.
See [the navigation review](WORLD_PANEL_NAVIGATION.md).

## High-profile lives and dynastic households - 18 October 2026

Optional history32 realm35 is documented in
[the notable-person review](HISTORY32_NOTABLE_PEOPLE.md). Implementation9326a1f
and exact-output batching95801a7 are backed up on origin/main. Four focused,
22 actual Earth/22 forest and826 presentation assertions pass, with prospective
kinship, actual-work achievements, mortality, binary/JSON persistence, split-run
and cold replay. Browser household/achievement/ability screens were inspected.
Final native IL2CPP Earth replay matches CLR exactly; old34 replay stays literal.
Native maps/selection/stripped biographies preserve the paused history hash.
Selected review player: `player-history-32-notables35`, built2026-10-18T05:50:49Z;
sovereignty34 remains the rollback. No new all-era realism or dense-world
maximum-technology-plus1000-year performance acceptance is claimed.

## Contested local sovereignty - 17 October 2026

Optional history32 realm34 is described in
[the local-sovereignty review](HISTORY32_CONTESTED_SOVEREIGNTY.md).
Implementation checkpoint1bc6896 is pushed.511 focused assertions,32 actual
Earth/31 actual forest checks and826 presentation assertions pass, including
real-account secession/restoration and refused/paid protection counterfactuals.
Both natural80-year continuations retain split-run and cold-save determinism.
Old33 remains literal. Release build, generation reconstruction, actual browser
political article, native map/stripped-article and exact native/CLR12-year replay
pass. The native States and browser city dossier screenshots were opened.
The reviewed IL2CPP player is `player-history-32-sovereignty34`; identity33
remains the rollback. This is causal-mechanism/persistence/adapter evidence,
not all-era historical calibration or a new200k late-world performance acceptance.

## Peoples and local historical memory - 17 October 2026

Optional history32 realm33 is described in
[the identity/memory contract](HISTORY32_IDENTITIES.md). Implementation checkpoint
1c818ba and active-smoke/chart follow-up77d474e are backed up on origin/main.
Selected player: `artifacts/player-history-32-identity33/WorldGen.exe`, IL2CPP,
built2026-10-17T22:40:02Z. `build-inputs.json` records staged-DLL hashes. Speech32
and earlier reviewed players remain rollbacks. Old saves stay literal until
the explicit historical-rules upgrade; generation/observer eras/music are unchanged.

Executed Release evidence:

- 208 `--identity-only` assertions: graded compiled social inheritance,
  inclusion versus exclusion, coethnic grievances, learned proficiency of
  unrelated languages, deep-copy isolation and bounded effects.
- 26 Earth and26 generated forest-world `--identity-world` assertions, using
  the preceding speech32 actual review saves. These distinguish controlled
  migrant/visitor/language/transfer/violence/fusion counterfactuals from naturally
  occurring history. Actual80 versus31+49-year continuation, first-cultural-actor
  registration, idempotent nonmutating upgrade, binary/JSON/cold continuation,
  separate court/resident identity, read-only atlas/focus and fixed1,000-scale
  household charts pass. Regional clocks cannot overwrite separate town records.
- 826 historical-presentation,377 structured-religion and306 existing spoken
  assertions pass. `scripts/build.ps1` passes0warnings/errors; CLI Preview
  repeat/reapply/recipe/cached reconstruction has identical all-stage hashes.

The prior32 native player on its UNUPGRADED saved world and current CLR match
the same12-year continuation:
`4c6e97023736a704b96dc73da9bdb5d98e53ae17584f7aa5a361aee35f8d2382`.
Current native33 and CLR on the upgraded80-year Earth review match exactly:
`6c260f869ca569907ce647d188a644a5728952060ef24bc9592dbc88ac01b5e8`.

The final native `--worldgen-religion-check` exercises Religions, Religious
institutions, Languages and Ethnicities, correct subject selection, available
doctrine/sound/identity atlas data, and unchanged authoritative history. The
ethnic map was visually inspected; native captures are map-only, not HUD-layout
proof. The native document confirms the corrected fixed household denominator.
Actual headless Edge/.NET worker `scripts/identity-browser-check.mjs` passes
saved-world import, separate ethnic directory, inheritance article, an actual
town's attributed capture/rights memory, hotkey9, exact ethnic focus, paused
time and zero JS errors. All three initial captures and final memory/native-map
captures were inspected. Text, links and inherited-tradition charts are readable.
The final browser rerun also passes after the household-chart normalization fix.

The200000-cell existing late-world fixture with4951 recorded towns was explicitly
upgraded, then advanced48years BEFORE timing12more years, so new40-year local
reviews were active. `--identity-large-world` validates final state and records
517people and3842town-memory accounts. Measured CLR processing:7187.02ms for12
years, worst computed year534.74ms (~1.67yr/s); final hash
`f6d20b8a364fa2edf92146c399ed7ca769be91b74bced5230135c8d440788ce8`.
This is an active-system smoke, NOT the required native max-tech+1000-year
acceptance run and NOT satisfaction of the3yr/s target. No claim of an
outcome-equivalent performance improvement or complete political realism is made.

Inspection also caught and corrected two meaningful issues: relatedness must
affect language learning speed rather than impose a permanent competence cap,
and a regional representative located in a town must not overwrite that town's
separate minority accounts. Both corrections are confined to new33 rules;
frozen32 remains exact. A new large-test compile attempt incorrectly called an
internal tree visitor; the final harness counts memory records through the public
town query in explicit diagnostics, never a recurring simulation census.

Local evidence is under `D:/WorldGenBuildArtifacts/identity33-review`. No review
saves, screenshots, binary players, caches or user content are source commits.
These are mechanism/persistence/UI checks, not calibrated world-scale ethnic
history, exhaustive minority censuses or completion of all historical realism.

## Spoken communities / religious plurality - 17 October 2026

History32 optional realm32 is documented in
[the spoken-community contract](HISTORY32_SPOKEN_COMMUNITIES.md).
The selected IL2CPP player is `artifacts/player-history-32-speech32/WorldGen.exe`,
built2026-10-17T21:56:34Z; its staged-DLL hashes are in `build-inputs.json`.
The previous religion31 player is retained. Source checkpoint8edaf87 contains
the implementation; evidence/launch notes follow separately.

Executed Release checks:306 `--spoken-only`,267 existing `--languages-only`,
377 `--religion-doctrine-only`,799 `--history-presentation-only`,23 actual
Earth and23 generated Aborea `--spoken-world` assertions. Both world checks use
the preceding religion31 review saves, not altered terrain. Explicit upgrade
preserves people, food, public-faith/control layers and the source save. Controlled
migrant ratios, bilingual learning (including80 contact-years inside a100-year
rural interval), isolated inherited divergence, local public/lay religious
coexistence, visitor-versus-resident distinction, first-cultural-actor speech
registration, actual60 versus23+37-year replay, binary/JSON persistence and cold
12-year replay pass. The earlier invalid pure canonical comparison used a
different test-class type name and was removed; actual old-save hashes and the
frozen executable are the compatibility evidence, not that failed comparison.

`scripts/build.ps1` passes0warnings/errors and stages current adapters/Unity DLLs.
The new IL2CPP player's12-year continuation matches CLR exactly:
`4c6e97023736a704b96dc73da9bdb5d98e53ae17584f7aa5a361aee35f8d2382`.
The preceding religion31 native player and current CLR on its UNUPGRADED save
match exactly:
`32d3200dcbecf929365f5f8e52820b6ecc235f06dbb72bec10cdc5f6d273b6d4`.

Native `--worldgen-religion-check` now also checks Languages when the extension
is present: three real map exports, correct religious-body/language selection,
available doctrine/sound atlas data and unchanged history pass. Languages and
institution-map screenshots were actually inspected. These map-only captures
are not native HUD-layout proof. Headless Edge's actual .NET worker/UI passes
`scripts/speech-browser-check.mjs`: saved-world import, language directory,
sound/root and local-domain articles, hotkey8, exact language subject, paused
time and zero JS errors. All three browser screenshots were inspected; language
article fields and pronunciation guides are readable. The map is the existing
historical fixture, not a new geographical/pacing calibration result.

The existing large-world smoke harness also passes3 assertions on200000cells /
4951 recorded towns with the new extension: explicit upgrade/integrity and12
actual simulated years,7840.39ms total,676.85ms worst computed year. This is
about1.53yr/s and explicitly DOES NOT satisfy the separate3yr/s/max-tech+1000
acceptance criterion. No new recurring territory/membership census is used;
snapshot hashing/actor work remains a separate performance concern.

Small local evidence is under `D:/WorldGenBuildArtifacts/speech32-review`;
no screenshots, review saves, compiled outputs or user data are committed.
These checks establish causal mechanics, compatibility and presentation, not
calibrated world-scale language/religion histories or a complete minority census.

## State geography / dated overseas ventures - 16 October 2026

The optional history32 `Connections.Geography` revision2 is described in
[the implementation review](HISTORY32_GEOGRAPHIC_VENTURES.md). Final mechanisms
include witnessed contact, fair vacant-office selection and the recorded actual
parent service route. These are review rules, not a declaration of historical
calibration. No geological/climate generation algorithm was changed.

Focused checks:15 geography assertions,13 actual-world geography assertions
(explicit upgrades, copy/save integrity,20 versus7+13-year partition/reload
determinism, real voyages and bounded event portfolios),27 regional tradition,
807 provisioned-port,129 coastal-farm,423 receiver-frontier,2953 living mechanism,
84 presentation,313 interface and304 historical assertions. Two stale historical
tests were repaired: nullable political-cycle integrity is now actually tested,
and the current livelihood wording requirement no longer rewrites legacy prose.
This does not claim a passing complete generation/golden suite.

Matched200k100-year final comparisons, using exactly the same predecessor
snapshots/terrain on both sides (`geography-release-{earth,cp}100` local reports):

| Map | New ports, control / candidate | Surviving candidate ports | Candidate new-port residents | Other observations |
| --- | --- | --- | --- | --- |
| Earth73421 |9 /19 |19 |50,934 |Control56,476 port residents; more small starts is not uniformly greater population or territory. |
| Continents Plus261015 |5 /52 |49 |400,054 |Control11,864 port residents; colony survival is not guaranteed. |

The count includes real new towns at previously paid offices, not just towns at
new flags. New offices, resident settlements, recognized titles, political
protectorates and cultural identities remain distinct. The targeted Earth audit
found89 physically supplied vacant offices, including11 currently eligible for
normal/import settlement; a dispatch-count-dependent selection could starve
them. Revision2's saved rotating cursor fixes that without lowering eligibility.

Accepted current-regional-rule200k Earth and Continents Plus continuations also
ran through actual maximum technology +1000 (not merely observer Postmodern).
Selected state/climate and technology PNGs were directly viewed. Current local
review directories are `geography-contact-earth200` and
`geography-contact-cp200`; they retain final worlds and sparse milestone captures.
Earth ends163610, actual max-tech162521,2.438billion people,2719 live towns and88
living states. Continents Plus ends174515, actual max-tech173458,1.177billion
people,875 live towns and40 living states. These continue accepted terrain and
the earlier proto/state checks; they are not fresh independent ecological seeds.

The maps show genuine overseas port/settlement networks rather than permanently
empty distant coasts, but remain **a partial realism failure**: development is
uneven, Earth strongly concentrates settlement in Brazil/Africa/Australia, and
the earlier technology model stalls too long before compressing later advances.
The sampled Earth run remains Iron Age until roughly13k years after its first
state, then crosses the industrial/modern packages far too quickly. Continents
Plus reaches Early Modern about8600 years after its first state. This pass does
not certify those timelines or the resulting geopolitical geography as realistic.

Legacy31 +20 years retains exact hash
`f4e260d19883e9dfd894c63c9ff8215646b023fdb25090d3795819e6f48d2fef`.
The version1 geographic control also retains its exact1000+year continuation
`9f0cf2c31c979c4beff52dbfcc8dc3e59c7d908a74c977c49176bba32638acb7`
after revision2 was added; its matching Unity/.NET endpoint is5654839933012
seconds. Upgrades are explicit, not silently applied to saves.

Current revision2 native Unity acceptance:200,000 cells,1012.75949 simulated
years in170.28259 seconds,**5.94752 years/s average**, minimum rolling5.17197
years/s, worst simulated year643.9194ms; all gates pass.5472 recorded towns,
150 living states, actual max-tech178251, final179312. Input hash
`f52da21b20b0f1767ef2c6386b0cbbf8cea9cc2ac69394849ffd4c256a813af0`,
final hash `ad3d49e9dbc5797cfb6dda1c30917539b04ba6a84bf4c8d9816d889d5e42dd8d`,
endpoint5654807183284 seconds. Evidence:
`D:/WorldGenBuildArtifacts/geographic-ventures-v2-performance1000/playback.json`.
This is an explicitly upgraded retained modern performance fixture, separate
from the earlier fresh-state longitudinal reviews. It does not establish a
universal speed bound on every possible denser future history. The packaged
review player is `player-history-32-geographic-ventures`; its build-input
manifest records Core SHA256
`12e8394ddac9581b37f30c62a7db357bf1b4245c2e8558aa9d69c48311f96faa`.
An exact .NET continuation to5654807183284 seconds independently matches the
native final hash above and passes full public-state validation. The normal
project shortcut now targets this review player; existing saves keep their rules.

## Regional traditions and nested proto-centers - 16 October 2026

The32 review's optional `RegionalTraditions` revision was exercised on fresh histories over the unchanged200k Earth73421 and Continents Plus261015 terrain/ecology. First proto/state dates were144399/145710 and162922/163915 respectively. Ordinary earlier villages retained observer era2. Current reproducible worlds and compact States/Cultures screenshots are under ignored `artifacts/history32/regional-traditions-current-{earth200,cp200}`; the primary agent actually viewed full world maps of the proto and first-state periods. Regions are distinguishable from local centers; the sparse state colors do not falsely turn whole cultural homelands into states. This is not an independent visual review or a claim to reproduce Earth's actual origin geography.

Targeted checks pass:27 pure tradition/institution/era assertions;12 actual20k-world assertions covering explicit opt-in, no population/technology grants, saved hash round trip,20-year versus7+13 publication parity, cached/literal maps and bounded post-state frontier seeding;84 historical presentation checks;313 interface checks;2953 existing living-mechanism checks;51 lake display checks. An older atlas assertion still expected wording removed before this pass; it now checks the current explicit people/city continuity sentence, without weakening that continuity requirement. No golden generation expectations were rewritten. Full solution/worker staging and the Unity review-player build succeeded with zero compiler warnings. Old31+20yr continuation remains exactly `f4e260d19883e9dfd894c63c9ff8215646b023fdb25090d3795819e6f48d2fef`.

At the owner's explicit request, the normal shortcut now launches `artifacts/player-history-32-review-w2/WorldGen.exe`, containing this revision for fresh simulations;31 remains the verified rollback baseline. Saved32 histories without the optional marker retain their previous rules unless explicitly upgraded. The broader32 realism/performance qualification limits below still apply; a focused prehistory revision does not validate every later historical trajectory. Redundant trial worlds/exports were removed, retaining small comparison reports, the current two reproduction worlds, and selected screenshots rather than accumulating bulk test outputs.

The final Unity build also passed an explicitly opted-in200k modern regression:1009.48 years in181.98s,5.547yr/s average,4.614yr/s minimum rolling rate,568.67ms worst combined simulated year and17.38ms P95 frame. It began at178300, after actual maximum technology178251, and ended179309 with5402 recorded towns/122 active states. Report: ignored `D:/WorldGenBuildArtifacts/regional-traditions-current-performance1000/playback.json`; final exact time5654703767411seconds, hash `5c0af4d6211932601272e453c55c12c86eb5845229dda8e2ea8d819549b281da`. An independent .NET continuation to those exact seconds passed full public final validation and matched that entire hash. This continuation was explicitly upgraded from the retained W2 max-tech fixture; it is a regression check, not a fresh full-lifespan or all-seed acceptance claim. Roughly1.44GiB of redundant outputs from this pass were cleaned.

## History32 overnight experiments - 16 October 2026, still under review

### Later overnight review: S1/B2 and a failed native performance gate

The combined subject-shipyard/rural-boatbuilding, fair pending-port review and
political bargaining candidate was replayed on the identical saved geography
and ecology, not a succession of differently generated maps. Independent agents
actually viewed first-state/Bronze/early-modern/late state and technology PNGs.
Continents Plus200k reached all major landmasses with differentiated coastal
networks; Archipelago42 reached83 cumulative sponsored port foundations by early
modern versus O's10. Cumulative foundations are not a current active-port count.
Earth still has excessive late concentration in North America and weak Indian/
Chinese urbanization; this remains a rejected realism outcome, not hidden by a
stronger generated-world result.

The B2 forager-only recognition correction retains the exact pristine-state
homeland/ecology prior. Matched200k Earth and Continents Plus keep first state
119944 and154770, with one and two independent origins. The hostile20k Aborea73
control instead reaches a state at583559 (three independent origins by the late
tail), rather than no state by700000. Its first state is in temperate rainforest
at38.67N/115.92W, on a different continent from the original human homeland. Actual
PNGs show localized early traditions, not simultaneous global states. Apparent
ocean town markers were checked against saved subcell records and are inhabited
islands, not settlements invented on empty water. See HISTORY32_MARINE_FORAGERS.

A stricter O native Unity200k Continents Plus stress run FAILED:1510.96 years at
2.54 years/s, minimum rolling1.89, worst summed-year1182ms. P95 frames remained
17.13ms, so smooth rendering did not imply acceptable simulation throughput.
The run ended with4733 recorded settlements and242 active governments. Measured
work was232.5s city actors,108.7s regions,108.6s cultures,61.7s states and53.6s
checkpoint hashing;998 Mono collections were observed. Evidence is on D under
artifacts/history32/unity-o-cp-performance1500. This failed gate supersedes any
inference that the earlier H8.91yr/s control proves the latest source fast enough.

Round T removes discarded legacy capacity calculations and reuses an unchanged
actor-local rural capacity, adds current-version fast-positive feature checks,
retains64k bounded immutable agronomic niches, and avoids per-event stopwatch/
metric-label allocations. Exact100-year continuation hashes match the frozen
pre-T build on both200k Earth (`df71fc8f78839f51a96eb83ca3055cd431442075ba6d5a416e286d9511937c76`)
and Continents Plus (`2c486fd3cfd2d144dd12b80486dbc09df021d41a68032ee44c97d99bbafb9a0d`).
Official31+20years still matches its released fingerprint below. These prove
output preservation for the controls, not native speed; a new native gate is
required. Focused combined mechanisms currently pass2108 assertions. No golden
expectations or user saves were changed.

Source candidate32 is not yet the desktop release. Focused current checks include sponsored colonial foundations12, institutional transmission15, local-carrier provenance3, nuclear technology99, resource economy126, political programs132, appearance45, living-atlas13 and general historical presentation78 assertions. International continuity adds an actual mid-campaign regime replacement regression; annexation is separately excluded. Exact official31 continuation remains `f4e260d19883e9dfd894c63c9ff8215646b023fdb25090d3795819e6f48d2fef`, compared against the old released31 shared DLLs, not just another new build. No golden was changed.

Frozen roundG Continents Plus261015/200000 cells reached its first state at176813 elapsed years and Postmodern at183600; it continued to184600. The184500 image has48 active states,587 populated towns and4.19 billion total population, with two recorded independent institutional origins. Actual state/technology screenshots at Early Modern183100 and Postmodern184500 were viewed by root and an independent critic: populated coastal networks now span all major landmasses, with competitive regional ruling colors rather than a solitary modern enclave. Desert/mountain interiors remain less organized. Archipelago42/20000 reached first state267962 and Postmodern279800, with137 active towns but only two surviving states by280700; a second seed is required before deciding whether naval hegemony is an outlier or excessive lock-in.

The native diagnostic runner observed174.6 seconds for the complete G200k trajectory including snapshot exports; its worst **summed observed work per simulated year** was233.7ms. Earlier single-event maxima were not worst-year timings. These are Core diagnostics and rasterized shared-presentation maps, **not proof of live Unity throughput or final UI quality**. Actual player/browser tests and further longitudinal review are still pending. Failed candidates and diagnostic world files remain ignored artifacts; redundant derived SVGs are removed only after their PNG screenshots are successfully produced. User worlds are untouched.

## Additional history32 overnight checkpoint (candidate, not launcher release)

`--living-mechanisms-only` now runs842 fixture-free assertions across climate/protovillages, physical transmission, settlement networks, military geography, resources, appearance, politics, marine foragers, nuclear accounts, diplomacy/operating budgets and the native-3 lineage catalog. These also run in the default test entry point; the separate pre-existing Earth golden mismatch is not hidden or regenerated. Focused presentation/strict-summary tests pass84 assertions; read-only living atlas18; actual coarse prestate founding8; operating finance19; nuclear world27. The full solution/staging build completed with zero warnings/errors, and the released31 continuation still exactly matches the prior player fingerprint recorded above.

Actual native Unity H200000-cell Earth/native-2 playback ran1529.93 years,1323 past maximum technology, at8.91 simulated years/s (minimum rolling6.52), worst summed-year work453ms, P95 frame17.2ms. This passed the requested throughput/year budget but is explicitly an H revision control, not proof for every later source revision. Unattended hidden-window full-HUD captures were black and do not count as visual verification; actual shared map-camera and browser atlas screenshots were independently viewed. The launcher is still31 pending a later accepted candidate.

Native-3 K Earth screenshots show an early Chinese origin and later populated China/Japan/India networks. Native-3 K Continents Plus has84 active governments including28 constituents at192200 elapsed years, broad populated regional/coastal networks and physically shared technology; the first-state-to-postmodern interval is14373 years, with the main delay before bronze/iron. Identity-only map exports were also inspected so bright climate colors were not mistaken for state control. Archipelago42 remains a rejected control: J's first state630619 and L's638449 occur hundreds of thousands of years after agriculture182000. Correcting kilometer-scaled local settlement proposals and founder/daughter links passes actual transactions but does not by itself fix that outcome. Independent reviewers identified an artificial no-neighbor proto-cap and repeated fiscal/concentration penalties as the next diagnosis; no successful-fixture claim substitutes for that failed history.

An optional browser AOT experiment retains full save integrity and exactly matches native H after a real one-year continuation. It reduced the same saved world's usable import from171.60s to10.64s. Current cross-host generation controls, publication reproduction and limitations are recorded in BROWSER_AOT_REVIEW.md. Separately an all-ocean browser check exposed nonfinite *display metrics*: the summary now explicitly omits undefined ratios with a warning rather than emitting invalid JSON or pretending they are zero. Authoritative metrics, hashes and warnings remain unchanged. Its browser checker now rejects parse errors rather than leaving an unresolved loading promise. Generated diagnostic controls are not substituted for historical repository goldens.

## History31 ecological origins and physical exchange - 16 October 2026

Generation remains3.32, naming language-1, research costs29. Focused mechanism tests pass16 assertions: ever-wet lowland river habitat establishment factor0.000052063 versus1 for productive seasonal alluvium; cold/short and long-but-cool seasons remain poor; irrigated alluvium differs from waterless desert; seasonal tropical highlands remain possible; selected crops/management ease costs. Equal viable near-homeland pristine state hazard is about0.00001 of distant hazard once distant residence exists. This is a conditional per-opportunity ratio, not a measured99%-of-worlds guarantee. Pure tests also cover rainforest traversal, latitude transfer and suitability-sensitive adoption rather than an irreducible diffusion floor.

`--environment-world artifacts/languages/history30-review.world` passes43 checks for explicit migration, original checkpoint integrity, retained population/names, detached origin reach,10 versus4+6-year replay, binary world roundtrip/continuation, all37 research costs and stale-contact exclusion. Frozen player30 Core and the new build both continue the original30 fixture10years to `4b4d1d95edfe984f4c745ced2d762b5c20e565afbfc2ed1561cb869aa18e7787`; comparison uses the actual preserved old DLL, not two copies of the new code. Focused language267, language/world13, presentation73, interface313, canonical82 and aggregate1022 checks pass. The31 aggregate fixture's observed maximum is150 local cell reads/event, not a territory scan. CLI Preview repeat/reapply/serialized-recipe/cache reconstruction matches all15 stage hashes. No goldens were changed; the pre-existing complete-suite Earth golden mismatch remains separate.

Fresh20k-cell release controls:

| Preset / seed | First advanced culture (elapsed years) | First independent state (elapsed years) | State latitude | Distance from human spawn |
|---|---:|---:|---:|---:|
| Earth /73421 |108092|140869|29.23 S|9754km|
| Continents plus /261015 |138582|181465|31.46 S|6959km|
| Earth /42 |143687|220484|30.32 S|9294km|

Each has one recorded independent institutional origin in the reviewed interval; other courts descend from/diffuse those institutions. First state climates are Cfa/Csa/Cfa, not Af or cold D. Snapshots were rendered and visually inspected about2131/2535/2516years after first state, under `artifacts/history31/{earth-release,continents-release,earth42-release}/states.png`; detailed origins and complete recipes are in their `origins.json`. Earlier experimental directories are retained as diagnostics, not accepted release controls. The diagnostic now resolves actual setup presets; an earlier unrecognized ContinentsPlus spelling silently fell back to the default recipe and was discarded as a preset comparison. Three controls are not statistical validation. State origins in southern Australia/South America reflect synthetic crop/ecology/climate priors, not a reconstructed historical Earth agricultural distribution. Some inherited maritime advanced communities still reach implausibly polar habitats; they are not pristine states and remain a carrying-capacity/provisioning limitation outside this origin correction. Large-scale historical pacing and expansion are not scientifically validated by this pass.

On the existing200k Earth post-max-technology control upgraded explicitly to31,25 separately published years take4.633s (5.40years/s), maximum measured annual event work183.7ms. Setup, file loading and explicit integrity validation are outside this timer. This is a late-game regression control, not a fresh31 simulation through every era nor a guarantee for arbitrary hardware/save sizes. New route work uses8 static raster samples and existing bounded actors/crops/contacts; no recurring terrain census is added.

Final solution/staging build passes without warnings/errors. Unity builds `artifacts/player-history-31-environment`; packaged/source Core SHA256 both `48F73DD0FBCAB72753FF7F36BC27D99F43C9D83612CB0A32E8BE172F5F36E16E`, Presentation both `BF652BB99DF95298B3A13D27486193621926B64B13D343421BC74927C2D7EEED`. The native dock diagnostic loads the new31 Earth checkpoint and passes every panel, era/audio, contextual selection, atlas/language and unchanged-checkpoint check with no GUI errors; report `artifacts/history31/native-final/dock-check.json`. Hidden-window capture is not claimed as visual evidence; the three map renders above were inspected instead. The rebuilt actual WASM worker loads/validates that same31 world, reports31 rules, exposes its saved language roots and preserves time/population during inspection (`scripts/language-worker-check.mjs ... history-31`). The script's expected-rules argument defaults30 for the existing fixture. Root shortcut now targets the verified31 player; previous30 remains available. Test worlds, screenshots, players and logs stay ignored rather than being committed as source.

## History30 language foundation - 15 October 2026

New naming rules are history30/language-1; worldgen3.32 terrain/climate/hydrology/ecology and29's material/research rules remain unchanged. `scripts/build.ps1` completes with no warnings/errors after the final code changes. `--languages-only` passes267 assertions covering seeded profiles, structural bounds, inherited roots, stable compounds, sound changes, donor-attributed bounded loans, query-order independence, distinct senses, foreign names and malformed profiles.100,000 uncached settlement names take181-185ms in the local .NET runner (a diagnostic, not a cross-host throughput guarantee); a500-name river-town sample has392 distinct names with meaningful repeated roots. Tests run in the regular suite as well as the focused entry point.

`--language-world artifacts/history29/upgraded-earth20/final.world artifacts/languages/history30-review.world` passes13 integration checks: explicit migration without renaming existing actors, original checkpoint integrity, deep snapshot detachment, continued29 population/control/settlement-count/technology equivalence over10years, partition-independent4+6-year continuation, full world-file and JSON round trips, future continuation equality, native-family migrant foundation/archive names, atlas roots and fresh30 preparation. The saved review fixture contains the legitimate10-year continuation; the separate forced-foundation probe is not saved. Focused regressions pass73 presentation,313 interface and82 canonical-byte assertions. Preview CLI repeat/reapply/recipe/cached reconstruction again matches all15 stage hashes. The complete suite still stops at the pre-existing Earth golden-stage-hash mismatch; no expectations were changed.

Unity builds `artifacts/player-history-30-languages`; its packaged Core and Presentation DLL SHA256 hashes match the validated shared build. The final hidden-window diagnostic at `artifacts/languages/native-final/dock-check.json` reports all checks true, including LanguageProfiles, AtlasProfiles, contextual selections, unchanged checkpoint, panel resizing, era and audio, with no GUI errors. The diagnostic music fixture was corrected to clear an already-loaded real-library track in the disposable player before testing its own silent WAV; production audio behavior and preferences are untouched. Hidden-window screenshots are not claimed as visual-review evidence. The project shortcut targets this verified30 player; previous29 remains available.

After restarting the rebuilt browser server, `scripts/language-worker-check.mjs` loads the saved30 fixture in the actual WASM worker, validates it, reads a culture's six root/pronunciation examples and confirms inspection leaves population/time unchanged. Both final .NET and Unity world-file readers also validate the same stored historical hash. This is a focused worker check, not a rerun of the entire visual screenshot corpus or a claim of comprehensive cross-platform linguistic validation. No long-run demographic calibration was needed or claimed for this naming-only model extension. Language-lens UI, historical exonym registries, fictional glyph rendering, full grammar, translation and audio synthesis remain future work.

## History29 visualization refinement - 15 October 2026

Presentation only: Core, Serialization, generation rules, simulation rules and golden expectations are unchanged. `scripts/build.ps1` and the final Unity build into `artifacts/player-history-29-visual` succeed. The project shortcut targets this player; the earlier29 release remains available. Focused checks pass73 historical presentation,313 interface and82 historical canonical-byte assertions. Preview CLI repeat/reapply/recipe/cache reconstruction again matches every stage hash. The pre-existing full-suite Earth golden stage-count mismatch remains separate and unresolved.

News auditing reads the existing200k Earth/Continents Plus archives without advancing them:926,545 and696,974 complete records. After the first state, raw archive rates per year are Politics5.401/2.670, Society2.482/1.457, Discoveries124.337/101.516; these are archive records, not a claim that the old UI displayed all of them. Most discovery-category records are routine charts; technology adoption alone accounts for19,307/13,789 records across the complete archives. The editorial pass preserves only world-first capabilities, short major-event stories and first permanent settlement/state milestones. In the busy final1000-year audit, approximately191 political and29–31 society lead windows remain; discovery news is correctly quiet after the implemented technology set has been exhausted. Importance in this retrospective audit uses the loaded checkpoint's populations/peaks, not a reconstructed past census. The policy caps political leads at one per five-year window and society leads at one per twenty-year window. Full records are retained. Native cold feed reads took about25–31ms in these controls; repeated unchanged reads about0.002–0.003ms. These are diagnostic feed timings, not a new simulation-throughput benchmark. Both saved-history hashes remain unchanged.

`scripts/visualization-browser-check.mjs` exercises six distinct historical subject types, numbered map keys, Shift background keys,25% opacity controls, technology cards, atlas chapters, relationship diagrams, archive paging and illustrated ecoregion selection at1600×1000 and1024×720. Screenshots under `artifacts/visualization/final-browser` were inspected. Cold historical loading remains slow. Harness issues (the short original timeout, leading-dot slider strings and premature async assertions) were corrected; a retained browser session avoids reloading the large save during the screenshot checks. A real stale-selection race discovered by those checks is fixed by awaiting the pending mode/location before opening Atlas. Final polish moves facts/charts ahead of optional habitat art, removes habitat art from technology articles, and disables stale tab-color transitions.

The final native `--worldgen-dock-check` passes panel geometry/resizing, era/audio behavior, technology selection, technology/state atlas rendering, ecoregion cards and checkpoint integrity, with no captured GUI errors. Its report is `artifacts/visualization/native-final/dock-check.json`; hidden-window capture is black and is not claimed as visual evidence. Browser screenshots provide visual evidence. `scripts/news-worker-check.mjs` separately passes the final read-only news RPC route on a fresh worker without changing year or population; news replies cannot be mistaken for simulation-state updates. Reading a headline pauses playback until its modal closes. No new long-duration realism or throughput acceptance is claimed for this presentation-only pass. Reports, screenshots, players and silent test audio remain ignored artifacts, not source backups.

## History29 capability-era and playback/audio pass - 15 October 2026

Current historical version29; generation remains worldgen-3.32.0/schema3. Root shortcut: `artifacts/player-history-29-release/WorldGen.exe`;28 remains available. Full solution/staging build and final Unity player build succeed. Packaged and source Core SHA256 both `8DFBC1F60241847462FA30350D4CAF34DBE0C0150354DC2B7257AD843163BF2A`; Presentation both `42AF12A1072E9351B45C96B33F2568A8FDAA95D3A59EDD00DC77945F4AE632C7`. A redirected PowerShell build initially reported a native stderr/progress error despite successful compilation/staging; the unredirected complete rerun exits0. No golden expectations changed.

Fresh200k Earth and Continents Plus controls, seeds73421/261015, run1000years beyond all37 discoveries. Selected29 trialB ends with hashes `318f12d19964567332a758abee2082180029e8c2d3ce93639f58962e594a13c5` / `090c470fcb62f76b130ab60609530d1ff05d4c4b1332794d98110aadf2aec02d`, populations7.275B /5.785B. Date reports: `artifacts/history29/b-earth-technical.json` and `b-continents-technical.json`; first installed era dates relative to the first state:

| Package | Earth | Continents Plus |
|---|---:|---:|
| Neolithic | -6,135 | -6,467 |
| Bronze |645|708|
| Iron |1,965|2,028|
| Medieval |3,298|3,144|
| Early Modern |5,421|5,106|
| Industrial |5,714|5,346|
| Modern |5,916|5,509|
| Postmodern |5,975|5,587|

TrialA was retained, not hidden: industry5226/5089 but iron1479/1590. TrialB better separates initial iron production from later refinements; Earth is now somewhat later than the rough3500BCE-anchored industrial benchmark, and scholarly/medieval capability remains comparatively early. These are procedural comparisons, not exact Earth dates, scientific validation or proof of complete historical realism. No calendar gate or country-specific technology script is used.

Passing focused checks:313 interface/era/rate/music assertions;130 technical/imperial;82 canonical;40 atlas;35 calendar;19 regional ambitions. The29-upgraded20k aggregate suite passes1,022 assertions with maximum584 local cell reads/event under the unchanged64,000 supported-model limit. CLI repeat/reapply/serialized-recipe/cache reconstruction matches all stage hashes. Frozen27 and current code continuing the original20k save+3years still match `9e5a60386da870fd90027b683b75b442cc72790a36f48a6bdd71aa651e668854`.

Actual Unity continuation of the new29 fresh200k Earth+3years matches.NET: `9444f182f4051a79471246adc376f83ebac41521818c199e5ca3da2f703af485`. Native dock integration passes: era, resize/clamp/adjacency, silent-until-Play context, actual asynchronous WAV decoding, and unchanged simulation checkpoint. Browser tests exercise the Auto checkbox/keyboard, absent step/rate editors, collapsed age caption, Esc and main-menu volume preferences, dock layout and resizing at1440x1000 and1024x720; screenshots `artifacts/history29/browser-ui-b/{expanded,small-window}.png` were visually inspected. The separate browser audio test measures compatible continuation, timed incompatible fades, quiet context and resumed volume-scaled playback using a silent synthetic WAV, not the owner's music library.

The first browser UI attempt timed out at180seconds loading the large historical save; the420-second run passes with no page errors. Initial browser load remains slow and is not presented as improved throughput. No new200k native+1000year wall-clock acceptance is claimed here; the older27 performance evidence below remains27 evidence. The pre-existing whole-suite Earth golden stage-count mismatch also remains as documented. Generated reports, worlds, screenshots, fixtures and binaries remain ignored source artifacts.

## History28 regional intent - 15 October 2026

Focused change: inland whole-ecoregional recognition and separate regional/subcontinental/continental ambitions. `scripts/build.ps1` and native `scripts/unity-check.ps1 -BuildPlayer -PlayerFolder player-history-28-release` succeed. Generation is unchanged. The root shortcut now opens this28 build; earlier27 remains available. This pass does not rerun the lengthy global realism or200k native throughput acceptance; those27 results below remain27 evidence, not28 performance claims.

On the existing20k Earth27 checkpoint:19 focused ambition assertions pass, including multi-cell district extent, distinct continental/subcontinental targeting, war exhaustion/funding gates, save roundtrip, partitioned replay and malformed-record rejection. Aggregate suite:1,022 assertions pass with maximum584 local cell reads/event under the unchanged64,000 supported-model bound. No recurring territorial membership scan was added.

Explicit28-upgrade checkpoint: `460a4f8e52e60e12cb9b86736d7a04e49d8f370f04ef6f7e3ff291b333d7ced4`. Its+3year continuation is identical in packaged Unity and.NET: `ab2a21f8d81f833c42a649e0acf43ef8cbfc85cf775ff4f09b8c3a00f1a47e7c`. Frozen27 versus new executable continuing the original27 checkpoint+3years both yield `9e5a60386da870fd90027b683b75b442cc72790a36f48a6bdd71aa651e668854`; loading does not silently change historical rules. Evidence is under ignored `artifacts/history28/`. The inherited whole-suite Earth golden stage-count mismatch documented below is not repaired or waived by these focused passes.

## History27 handoff — 15 October 2026

Current historical rules are27; generation remains worldgen-3.32.0/schema3. The root shortcut opens `artifacts/player-history-27-release/WorldGen.exe`. Shared and packaged Core DLLs both have SHA256 `05BF7015F66C18C314EA14BB9249C40A1647334655A0D0B2D32068FBF3DF9971`; Presentation DLLs both have `B692937CD4EB41FE4D7FAEB5D4D323D5A9DF3A1D9495DB2C10D8AA18BAF5E76F`. See [HISTORY24_REVIEW.md](HISTORY24_REVIEW.md) for the chronological24–27 development, including rejected experiments and failed checks.

Actual200k-cell Unity playback, another1000-plus years after review saves that already end1000years beyond the first complete37-capability knowledge milestone; no actors skipped or inventions forced. The two27 players ran concurrently:

| World | Simulated years | Years/second | Minimum rolling | Worst year |
|---|---:|---:|---:|---:|
| Earth | 1021.957 | 5.657 | 5.420 | 845.817ms |
| Continents Plus261015 | 1028.793 | 5.879 | 5.260 | 695.533ms |

Evidence: `artifacts/history27/native-earth/playback.json` and `native-continents/playback.json`. Independent.NET continuations to the exact native final seconds matched `930fbc4c0941ab8aa12f6b670c519d03989b787dfa6db639f4854b94ce4214b8` (Earth) and `aa3466b561b5af071c1e518419c3dc2828c48f0536ab62ab0641e5934b0fe52c` (Continents Plus). Earlier26 also passed both worlds; an earlier26 trial failed the one-second-year requirement and remains recorded, not hidden.

Fresh27 endpoint hashes: Earth `9f78cf25c5e714edab6e02d1176a1449d838b84b5baae3698152f516bd18a0e1`; Continents Plus `f485f1746e715036a069afed489f97694f2b179fdb97c2fb4d18f0a68fe733d1`. Full independent dated CLI replays match both; both era-overview image sequences were actually viewed. Native offscreen maps are valid; hidden-window HUD captures are not accepted as native HUD evidence.

The final packaged27 native Earth replay also completed all nine observer eras and matched `9f78cf25c5e714edab6e02d1176a1449d838b84b5baae3698152f516bd18a0e1` (`artifacts/history27/native-era-earth/verified.txt`). Its final `10-Postmodern-map.png` was actually viewed and retained for the owner. The final fresh-world audits record6.404B versus5.873B people, three versus two independent institutional origins, and46 versus86 recognized territorial titles; these counts are simulation outcomes, not fitted targets or a claim to reproduce Earth's actual geography of states.

Browser27 passed all seven overlays, matching legends/selection, numeric rankings, full campaign export,25% opacity shortcuts, split1+2year advancement, and a browser-written saved world. That saved checkpoint and independent.NET+3year continuation both hash to `9e5a60386da870fd90027b683b75b442cc72790a36f48a6bdd71aa651e668854`. Browser cold startup remains slow: first session/year116.724s; subsequent two-year batch10.783s. Native playback results are not presented as browser performance results.

Focused tests: 11 new coastal-index assertions,23 colonial-continuity assertions,129 technical/imperial assertions,1022 aggregate assertions (maximum373local cell reads/event on27),29 display assertions,82 literal-canonical assertions,40 atlas assertions and18 archive assertions across this pass. The existing whole-suite Earth golden stage-count mismatch remains pre-existing and was not repaired by regenerating goldens. CLI Preview reconstruction verification passes repeat/reapply/serialized-recipe/cache with identical all-stage hashes. Legacy24+3years stays `cf14d57b263cb5ba01adfa0e5cb734c55c2652d20b69c2e3b0c40c3f081540ac`; legacy26+3years matches the frozen26 runner at `125198c0118c8078876c0ce30878381a19eb8abdca8d9ac26e8a2bb7e2c27948`.

These checks support determinism, specific modeling corrections and observed performance—not scientific validation or a claim that every generated history is realistic. Early agrarian population and the timing/geography of colonial expansion remain balance-sensitive.

## Final history23 handoff - 15 October 2026

The final rules are23, a narrow correction on the22 pass below. See [HISTORY23_REVIEW.md](HISTORY23_REVIEW.md). Generation stays worldgen-3.32.0/schema3. Current Core SHA256 is `d3f6d36f3f987784ac8a9fce4c99ba8da73744aa226e1840a529a35ee845ae38`, identical in the compiled shared assembly and `artifacts/player-history-23-final/WorldGen_Data/Managed/WorldGen.Core.dll`.

Fresh Earth200k, Continents Plus200k and Archipelago80k reached all37 capabilities and another1000-plus years. Earth and Archipelago retain22's same demographic/actor outcomes, with the required new version/hash. Continents Plus changes modestly. Independent nine-era replays of final Earth and Continents Plus match their endpoint hashes; both overviews were actually viewed. The cold-world continuation demonstrates the specific deadlock is removed without retroactively changing its22 past. It is an explicitly upgraded continuation, not a claim of a fresh23 replay over600000 years.

Final native Unity acceptance, actual current model, no skipped actors/events or forced inventions:

| 200k world | Additional simulated years | Overall years/s | Minimum rolling years/s | Worst calculated year | Mean / P95 frame |
| --- | ---: | ---: | ---: | ---: | ---: |
| Continents Plus | 1008.20 | 4.410 | 4.238 | 757.60ms | 17.27 / 20.60ms |
| Earth | 1009.37 | 7.226 | 7.042 | 462.66ms | 17.01 / 18.43ms |

Both passed the requested minimum3years/s, no calculated year above1second,1000-year tail and folded-selection gates. Maximum frames54.15ms /32.73ms respectively. Evidence: `artifacts/history20/unity23-final-{continents,earth}-performance/playback.json`. Loading/setup is excluded and remains slow; these two host measurements are not an unlimited-actor scalability guarantee.

The final23 fresh Earth checkpoint passed1022 aggregate assertions; final23 generated terrain passed19 dense-display assertions across all overlays. The33 local-community checks include actual-farmer support, rejection without crop records,22 compatibility, accounting, literal-cache equivalence, detached publications and cancellation. Canonical73, crop8 and atlas39 checks passed. A20-year22 continuation matches the frozen22 executable exactly, as recorded in HISTORY23_REVIEW.md. The old21 continuation also remains unchanged. Final solution and Unity player builds passed. The preexisting full-suite Earth golden mismatch documented below remains; no golden was silently regenerated.

The browser passed all five overlays, matching selections/legends, numeric population/area rankings, opaque-score hiding,25% opacity keys/clamps, chronicle export and an irregular37-year continuation. Its saved history hash `b30d748b7a9af811cf6ff26eea6afe957bdbf067133110938d5c3b7ffe9fb3b8` matches an independent .NET continuation exactly. Current staged23 worker additionally passed the default-layer/opacity/next-event check, explicitly asserting its reported rule version. The atlas's five profile types, lazy habitat images, real-condition tiles, search/history paging/focus/copy/compact layout passed with unchanged campaign lore. Browser loading is still roughly200seconds for this20k late save; phase reporting does not make that fast. Native hidden-window HUD capture remains black on this host; valid offscreen maps and browser UI captures are the visual evidence.

The complete three review recipes are intentionally retained input fixtures in `tests/fixtures/history23`, including accepted climate weights. Each was exported only after independently generating it and matching every named original stage hash. Their README documents reproduction without old external validation folders. They are not regenerated golden expectations. Raw worlds, screenshots and binaries remain ignored.

The root shortcut targets the final23 player; the22 player/checkpoint remains available for rollback. Existing histories require an explicit upgrade, which changes future behavior and cannot repair already-recorded implausible past events. Premodern direct/indirect state scale, local economy detail and several political/technical abstractions remain unfinished; this pass is not a declaration of a scientifically validated or complete historical simulator.

Final23 native Earth200k subsequently replayed all nine observer eras to the same final hash `bc30aa450ebb3229c16df695145246f53d34027f1d9714d4376fd4821965bd94`. Evidence: `artifacts/history20/native-era23-earth/verified.txt`. The actual native final offscreen map and final browser illustrated-city profile were viewed; the latter shows narrative introduction before illustration. The remaining political-scale limitation includes modern sparse-region claims, not only ancient empires; the concrete diagnosis/next mechanism is recorded in HISTORY23_REVIEW.md.

## Current history22 release candidate - 15 October 2026

See [HISTORY22_REVIEW.md](HISTORY22_REVIEW.md) for actual mechanisms, longitudinal outputs, research and unresolved realism limitations. Generation remains worldgen-3.32.0; historical output changes are separately versioned. Final accepted experimental runner is `review22d-bin`, not the earlier unshipped22a/b/c trials.

Three fresh longitudinal histories reach all37 capabilities plus at least1000 further years. Earth and Continents Plus use200000 cells, and the Archipelago control uses its existing lower-detail geography. Earth and Continents Plus independently replayed all nine observer-era snapshots to identical final history hashes. Those era maps were actually viewed against the independent reference frames/maps, not inferred only from final counters. Premodern state scale still needs improvement; this is not a complete scientific or historical validation.

Final native Unity6000.6 player: `artifacts/player-history-22-final/WorldGen.exe`. Its Core DLL matches the compiled .NET Standard Core and staged editor plugin, SHA256 `eabf9c0b3fdc7166834855c6fe025f4adcd3dc0959ab68c96a67fbf2a8b03bab`. Generated Continents Plus200k, actual current model after all37 capabilities: **1008.21 additional years, 4.780years/s, minimum rolling4.600years/s, maximum calculated year705.55ms**. Mean frame17.29ms, P95 21.33ms, maximum48.96ms; selection stayed folded; acceptance passed. Evidence: `artifacts/history20/unity22d-continents-performance/playback.json`. Loading/index construction is excluded. No forced technology, skipped actor events or suppressed political systems were used.

The same final player on Earth200k passed **1009.37 additional years, 7.874years/s, minimum rolling7.591years/s, maximum calculated year441.72ms**. Mean frame17.05ms, P95 18.86ms, maximum34.55ms; selection stayed folded. Evidence: `artifacts/history20/unity22d-earth-performance/playback.json`. These are two actual late-world samples on this host, not a guaranteed floor for arbitrary centuries of accumulated actors.

Focused current checks completed before the final native build:1022 aggregate assertions on the latest200k Earth checkpoint;73 canonical-writer equivalence assertions;35 calendar;115 modern mechanisms;39 atlas presentation;8 crop ecology; local community accounting/cancellation/cache tests. Final repeated host-specific checks are recorded below as they finish. The default whole suite still fails its existing stale Earth v3 stage fixture (11 expected stages versus the current15-stage pipeline); no golden was changed to conceal that failure.

Final staging build passed with zero warnings/errors. CLI Preview repeat/reapply/serialized-recipe/cached reconstruction produced identical all-stage hashes. Repeated current focused runs passed39 atlas,8 crop,73 canonical,35 calendar,115 modern,30 local-community,15 provincial and15 connected-development assertions. An older living-history test incorrectly assumed the first archived town was still occupied; it now selects a genuinely occupied town, preserving the intentional ability to reoccupy ruins. Its abandonment fixture likewise selects a living fallback rather than accidentally testing an already abandoned record.

The native22b nine-era offscreen-map replay matched its own final hash, and native22c profile navigation preserved its recorded input hash. Those are earlier candidate checks, not proof of final22d replay. Hidden-window HUD ScreenCapture returns black images on this host even with a valid GPU; only valid offscreen maps and separately checked browser UI images count as visual evidence. The canonical eight habitat JPEG assets add about5.25MB, are staged by `prepare-atlas.ps1`, and do not enter simulation hashes. Generated screenshots, worlds and build outputs remain ignored.

Final22d native Earth200k replay subsequently matched all nine eras and final hash `0852c723de3e0519c32806d3810b1dd8c8186fae7efd9b749b17ef697ddff198`; valid map captures are in `native-era22d-earth`. The80k Archipelago replay also matched `84a3b288759abfa9fc0c6541d16103d3ea03f950ec6b5adaef83a5becb68dd7f`; its nine-era overview was actually viewed. Final native profile-navigation check on a20k Earth history passed with unchanged hash `406ad10223f9fd1a444727ba65c1f2ce13c427e296625da943ac999808ec34ac`. Shared Core stays byte-identical after later presentation/load-status changes. Serialization DLL in the final player and compiled adapter match SHA256 `5af9f47fef5c1dae67db6b6cdfa5e5c1755eaeeB5453cfcadf4404e8aa0c7080`.

Final repeated living-history suite passed23 assertions and dense presentation passed19 assertions across200k cells/all overlays. A read-only atlas audit exercised1500 sampled profiles and102079 cross-links on the200k Earth checkpoint without changing its hash. Older21 continuation,20 additional years, matches the frozen prior executable: `cba93aec6094b24f1902ddb79d2d7d335bf9d16dd5cfcbc31a645bf9330f9ed3`.

The updated browser atlas passed twice, including all five profile types, history/directory paging, diagrams, search/back, copy, focus trap, map return/selection, Escape,1024x768 bounds, loaded habitat images and actual-condition tiles, and unchanged campaign lore. Screenshots are in `browser22d-atlas-final` and `browser22d-atlas-retry`; the habitat and diplomacy images were actually viewed. The reader introduction now precedes the illustration. Browser loading is **not** fast: a20k late-history world took about200-220seconds through binary decode, terrain hash and historical validation; an80k control exceeded the earlier300second test deadline. Phase reporting distinguishes those costs but does not remove them. Do not confuse this with the separate native simulation-throughput acceptance result.

The whole default suite was repeated and still stops at `earth golden stage hashes`, after the generation/ecology/subcell checks. No expectations were regenerated. One attempted in-place test rebuild hit a DLL held by the owned Boreal diagnostic; a separate frozen output runner then built with zero warnings/errors. This was a diagnostic-process file lock, not an access/permissions defect in the project.

## Connected development — history19, 15 October 2026

Owner-requested realism pass corrects transport-independent frontier throughput, urban-port-only sea expansion, single sampled learning donors, birthplace-bound crop practice, cold-winter surplus bias and excessive compounding of independent state readiness. New histories use19;18 remains an explicit opt-in upgrade. No geography, climate classifier, ecology pack, recipe, world-generation version or golden expectations changed. See HISTORY_SIMULATION.md and ALGORITHMS.md for bounded operations, research context and model limitations.

Fresh longitudinal checks used the existing measured-Earth environment (20000 cells) and the existing procedural Continents-plus environment (200000), retaining their terrain/ecology. Earth: first cultivation101695, first state105707 (Csb, approximately28S), complete modern capability set110575; final111750 has182 cultures and19 active states, with94 cultures knowing digital telecommunications. Further independent origins include Dfb, BSh and Cfa: continental climates are neither privileged labels nor banned. The first state now follows cultivation by4012 years in this accepted run, rather than the first candidate's26239-year wait. This is not a fitted expectation for where or when real Earth history must start.

The procedural run reaches its first state105042 and complete capability set109617; its111000 endpoint has340 cultures and85 active states. All eight landmasses above5million km2 are inhabited and have political centers.206 cultures know digital telecommunications, and92 overseas-community/settlement events are recorded. Earth has states in both Americas and Australia. An archipelago control continued through the network-support correction, reaching the full capability set122535 and ending124000 with119 digital-capable cultures and197 sea-community events; it did not receive an artificial global invention-date unlock.

Mechanical checks:16 connected-development assertions,23 living-history assertions,36 UI/era assertions and27 physical-contact assertions passed. They cover bounded actor work, growing-season/storage differences, technology-dependent budgets/range, multiple candidate teachers, joint research support, eager full-mesh cache initialization, explicit upgrade, conservation, cancellation/retry, publication partitioning and save/load continuation. An original history18 Earth checkpoint was advanced20years by both the previous standalone test executable and the new one: identical final hash `ecabbec780abb1be05395f3b0b9fc2b1bb4e7e0a891ad11db2405976c19495d0`. The whole historical test suite was not rerun; these are the focused checks for this pass.

Release solution/worker/browser staging and the Windows Unity player build passed. The first actual200000-cell Unity run advanced1012.20years in45.60seconds (**22.20years/s**, slowest50-year interval19.77years/s); maximum aggregated simulation work per year302.36ms. Mean frame16.93ms, P95 17.69ms. It ended beyond1000years after the full modern capability set, retained folded selection and passed the built-in acceptance gates. Loading/index setup is excluded from that playback measurement and remains noticeably slower than .NET startup. A final rebuild also eagerly initializes the cultivar latitude cache before actor events, rather than allowing its first access inside crop transfer. Its separate evidence is in `artifacts/history19/unity-final`.

Viewed the accepted Earth states maps near first state, later regional expansion and the modern endpoint, the procedural final map, and the actual Unity backbuffer map. Evidence: `artifacts/history19/accepted-earth`, `accepted-continents`, `network-archipelago`, `unity-performance`, `unity-final`; existing18 comparison data remains outside the project. The root shortcut targets `artifacts/player-history-19/WorldGen.exe`. Do not substitute a pinned old executable when reviewing the new model.

Final shipped-player rerun:1012.20years in45.55seconds (**22.22years/s**), minimum50-year interval20.81years/s, maximum aggregated year277.55ms; mean frame16.92ms/P95 17.63ms. All acceptance gates passed; final year112012 has87 active states. Player and shared Core DLL hashes match: `5d941d5685497ab8232d2b6e938606b0bf5eb396781d1504a3d84c20a89bd81d`. Full-mesh latitude-cache preparation now occurs only in setup. No gameplay processes, simulated years or events were skipped for the benchmark.

Remaining visible limitations: some late Earth realms are too large/unified, local livelihoods and late demographic totals are not observationally calibrated, indigenous cultural adoption remains an aggregate approximation, and coarse sea masks miss some straits/indirect routes. These are improvements to identifiable model failures, not acceptance of a finished realistic geopolitical simulator. No predetermined country count, Earth-origin coordinates or crop/climate-label bypass was introduced. Intermediate reviews were generated only under ignored artifacts, not added to GitHub.

## Artifact retention cleanup — 14 October 2026

Owner-authorized cleanup removed 193 obsolete local artifact entries (64.26 GiB of logical file data): earlier SVG exports, simulation snapshots, superseded player builds and diagnostic runs. Older `artifacts/` evidence links below are historical records and may no longer exist. No validation result was rerun or changed by this cleanup. Source, fixtures, saved worlds, Unity caches, the current `player-history-18-realism` build and `history18-review` evidence were preserved, as were climate-learning checkpoints and small provenance/rollback records. External directory links were not traversed or deleted. `scripts/clean-artifacts.ps1` defaults to preview, checks tracked files and running executables, and requires `-Apply` for deletion.

Before cleanup, a verified source-and-Git snapshot including uncommitted/untracked source was saved to `D:/WorldGen-Backups/WorldGen-source-20261014-231834.zip`. This is a local recovery copy, not an off-machine backup. At inspection, GitHub `origin/main` still pointed to the 10 September initial commit (`e82a761`); local `main` was 13 commits ahead with additional uncommitted work. Nothing was committed or pushed during cleanup.

The owner subsequently requested GitHub checkpoints instead of duplicate local backups. The 14 October source checkpoint was prepared with generated outputs and local credentials excluded: Release solution build passed (zero warnings/errors), 36 interface assertions and 27 physical-contact assertions passed, and a high-signal credential scan found no matches. These focused checks do not replace the historical full simulation validations. The temporary archive is to be removed once the remote checkpoint is verified; future completed work should be committed, pushed and checked against the remote as specified in `AGENTS.md`.

## Resizable history dock and era music — 14 October 2026

Presentation-only change: all three bottom panels have independent top-edge height grips; the states panel occupies its full 26% dock width and fills down into two columns with scrolling overflow. Heights survive folding and clamp to the viewport. The global population column shows an observer era derived from adopted capabilities, not elapsed years or a simulation unlock. Root `music/README.txt` describes the inclusive 1–10 filename ranges. Desktop files are scanned asynchronously between tracks; the browser uses a build-staged catalog. Playback randomness never touches simulation streams. Empty catalogs remain silent; eligible-song repeat exclusions relax oldest-first when necessary.

Validation: 36 focused `--interface-only` assertions cover all ten eras, read-only state access, filename rejection, inclusive ranges, empty catalogs and recent-three fallback. `scripts/interface-check.mjs` loaded the history18 Earth checkpoint with 33 states in the actual browser, dragged all three grips, checked adjoining edges and visible-height column wrapping, retained height through fold/unfold, and checked a narrower window with no page errors. The expanded and small-window screenshots in `E:/WorldGen-history18-review/dock-ui-final/` were viewed. Native `--worldgen-dock-check` checked the actual panel geometry/resize handler, clamps, Postmodern label, asynchronous decoding/playback of an isolated silent WAV, and unchanged checkpoint hash (`dock-native-final/dock-check.json`). Its hidden backbuffer screenshot is black, so native visual inspection is not claimed. Solution/browser staging and Unity player build pass. Core DLL SHA256 remains `1b5b3ccc9ea860735526079fa4aede323d629bac80bed2b21e6c4e04a3103fa5`; no simulation, generation, recipe, or integrity version changes.

## History18 behavioral repair and performance — 14 October 2026

Final Core SHA256: `1b5b3ccc9ea860735526079fa4aede323d629bac80bed2b21e6c4e04a3103fa5`. Source output, staged plugin and the `player-history-18-realism` player's DLL match. The existing root shortcut now targets that player; the history17 player remains available separately. Solution build, browser asset staging and Unity player build pass. No terrain/climate/ecology algorithms or golden expectations were changed in this pass.

Fresh longitudinal runs start with the200-person founding band, not an artificially populated or technologically boosted fixture. Dated states/cultures/technology maps were rendered and inspected throughout iteration. The review caught and corrected missing settlement retirement, unbudgeted settlement duplication, inevitable elite-pressure growth, broken relay through decentralized/settled regions, under-sampled high-degree contact routes, and exclusion of subject cultures from institutional adoption. Quantitative observations from the final build:

| Observation | Earth20k | Generated200k | Archipelago80k |
| --- | --- | --- | --- |
| First state year |116611|110672|139912|
| First complete37-capability set |127764|119802|151457|
| Last reviewed year |129000|121000|153000|
| Active states at that checkpoint |33|90|30|
| State-bearing cultures before first gunpowder |6|8| see archived report |
| Recorded abandoned sites |915|1263| see archived report |
| Pristine institutional origins over the run |5|6| see archived report |
| Pristine origins within first2000 state-era years |2|1| see archived report |

These are observations, not golden expectations or proof of historical accuracy. Long-lived settlement outliers still exist, development tempo is not calibrated to Earth's chronology, and the Earth run does not reproduce Earth's geographic sequence. The reported failures are tested as mechanisms rather than hidden with an Earth-specific origin script or a global civilization quota. Administration, transport and rural distribution remain explicitly approximate; see HISTORY_SIMULATION.md.

Final evidence is under the ignored `artifacts/history18-review` junction (diagnostic files live on E: to avoid the almost-full C:/D: drives): `release-final-earth`, `release-final-continents`, and `release-final-archipelago`. Each contains the timeline, full event and town records, accounts, saved checkpoint and integrity hash. The Earth final-pass screenshot is `release-final-earth/states-129000.png`; the generated-world screenshot is `release-final-continents/states-121000.png`. Historical prototype folders are not the final results.

**Actual normal Unity player acceptance passes**, hidden window with a graphics device and normal presentation, on the200k fresh-history checkpoint. It advances1004.927 additional years, ending at122004, more than2000years after the first full modern set:

| Criterion | Final measurement |
| --- | --- |
| Achieved simulated years/real second |12.3116|
| Slowest completed50-year wall-time window |11.5198years/s|
| Maximum accumulated processing time for one simulated year |377.843ms|
| Frame p95 / maximum |18.054ms /29.216ms|
| Final active states / recorded settlement identities |106 /2336|
| Errors / selection panel unexpectedly opening |none /none|

`release-final-unity1000/playback.json`, `year-work.csv`, and `player.log` contain the measurements. Initial loading/preparation is excluded; publication copies and authoritative processing overhead are included. Do not confuse recorded settlement identities with live towns. As previously, a hidden-window backbuffer capture does not establish readable HUD styling; explicit map renders were inspected instead.

Independent .NET and headless Unity runs from the same200k checkpoint advance exactly1000years and both validate to `ee1b26401927509588fae8ef016d199718432035a3892fd786c1339f5a0e8ac8` (`release-parity-dotnet`, `release-parity-unity`). The .NET single-publication throughput is not an interactive UI performance claim.

Targeted tests pass:23 living-history assertions;1022 aggregate assertions (including the repeated frontier-index invariants);48 calendar;114 modern-history;27 contact. These include budget-partition determinism, cancellation/retry, save/load continuation, retirement and removal of annual work, population-account conservation, and exchange through inhabited regions lacking a named culture or unallocated rural population. The previously documented Earth generation golden mismatch is not repaired or rebaselined by this history-only pass. Browser/WASM compilation/staging passes; the new history18 mechanics have not been execution-tested in WASM.

## History17 aggregate simulation acceptance — 14 October 2026

Historical history17 Core SHA256: `3a8a91cb513b58b56f0c8a70b169729b788e32028db5f78b03183231829e4d29`. Source output, staged Unity plugin and tested player DLL match. Solution/browser asset staging and Unity player builds succeed. At that time the root shortcut pointed to this rebuilt `player-history-16-performance` folder; its historical folder name does not identify its current rules. History17 is an explicit output-changing actor/account model; prior history16 execution-only results below are not the current architecture.

**Acceptance passes in the actual normal Unity player**, launched hidden with a graphics device and the normal simulation/presentation pipeline, on Ryzen7 5700X / RTX4060. The input is a separately migrated copy of the real dense200k history13 snapshot at year114238:1240towns,556cultures,101live/570recorded states,109150events,27709leaders. No technology or population was injected. Its earliest complete37-capability set dates to113488. Original saves and the benchmark input are unchanged. Prepared fixture state hash: `6e77fc527c4adf68754e9901dc49a86df062d633753dbe058407c76bf718f499`.

Final normal-player evidence: `artifacts/aggregate-performance/unity-final/playback.json`, `year-work.csv`, `player.log`, and the verified offscreen `playback-map.png`. The hidden-window backbuffer/HUD capture is black and is not claimed as visual UI validation.

| Criterion | Measured result |
| --- | --- |
| Fine terrain cells | 200000 |
| Additional modern-era simulation | 1003.7066 years |
| End of run | Year115241;1753years after the complete modern set |
| Total measured playback | 130.2986seconds |
| Sustained throughput | 7.7031years/s |
| Slowest completed50-year window | 6.4753years/s |
| Worst accumulated processing cost for any year | 414.7676ms |
| Mean / p95 / max frame | 17.0374 /18.8794 /32.8454ms |
| Published checkpoints | 379 |
| Final towns / active states | 1692 /99 |
| Errors / selection unexpectedly unfolded | None /no |

Year costs include all local actor events, private transaction copying, queue publication/hashing and public snapshot copying; overhead is conservatively charged to a publication's ending year. Initialization/load/initial integrity validation are separate. Playback never skips events or interpolates uncomputed years. Rendering/inspection may still traverse cells. The result establishes this dense modern workload on this host, not a guarantee for arbitrary future actor counts or hardware.

Independent exact1000-year continuations in .NET and Unity both fully validate and produce `e2c30ba3f2b808c966b6eab7e1b4075176d5a9d4c880d9b3797d0ee345dd867a` (`cross-dotnet1000` /`cross-unity1000`). The unbounded .NET continuation takes5.3031seconds, excluding load/validation; it is not interchangeable with interactive publication throughput. New1022aggregate assertions pass, including real binary save/reload, irregular event budgets, private snapshot isolation, cancellation inside a transaction, canonical spatial deletion, conservation and a fresh forager start. Legacy27contact,82routing,114modern,48calendar,53delegation and22institutional assertions pass. Calendar/routing compatibility fixtures explicitly retain their old rules instead of being accidentally upgraded to17.

CLI Preview verification passes repeat/reapply/recipe/cached all-stage reconstruction. The full test run still stops at the inherited `earth golden stage hashes` mismatch; no expectations were regenerated. New history17 WASM execution was not tested; browser compilation/staging succeeded. Archived history1–16 algorithms remain available, including their slower paths, and reading an old world does not silently change its history. Use **Esc → Update simulation rules** to opt an old world into the new recurring event path.

Re-run with `scripts/history-performance-check.ps1`; prepare a separate current fixture with the test executable's `--aggregate-fixture` option. The script enforces200k cells, at least1000additional years after modern capability, ≥3years/s overall and in completed50-year windows, and ≤1000ms processing per year, and verifies that its input file is untouched. Temporary superseded diagnostic snapshots were removed to conserve disk space; they can be regenerated from the original input. No user save was removed.

## Dense modern routing performance follow-up — 14 October 2026

Execution-only fixes are documented in HISTORY_SIMULATION.md. Final Core SHA256 is `403c153389945ec0c01755363b984d46d9275f025e2e66db567f056476bc16b8`; source, staged Unity plugin and player DLLs match. Solution/browser staging and Unity player builds succeed. The existing root shortcut launches the rebuilt `player-history-16-performance` player; the older realism player is retained. No historical rule/version, save migration, generation output or golden expectation changed.

The dense200k fixture has1240towns,556cultures,101live/570recorded states,109150events and27709leaders at114238. Its original history13 snapshot is read-only; current-rule measurements explicitly upgrade an in-memory copy to history16. The identical20-year bounded .NET continuation improves64.540→17.500seconds (about3.69x), with cumulative allocation11927.9→2788.4MB (76.6% less). Hash remains `a44ea526d564b22a0b882f81a4623fa8fe66ed7a886eb4af1537cc2cc8e86773`. Final120-year continuation takes57.386seconds (2.091years/s),31publications and10163.7MB cumulative allocation; its hash matches earlier optimization iterations at `c2494660f14b0ded690c8341e2e0a5fb27ea0c33281654bfb807602cde747217`. Full timing/latency/checksum records are in `artifacts/modern-performance-fixes/release-current20` and `release-current120`. These short/long windows are distinct measurements, not interchangeable steady-state estimates.

Final actual Unity playback of that current-rules fixture over120seconds publishes21checkpoints and advances82years: **0.683years/s**, mean frame16.82ms, p95frame17.08ms, max28.33ms, no errors, selection stays folded. Evidence: `artifacts/modern-performance-fixes/native-release`. **The desired5years/s dense-modern target is not met.** The earlier7.65years/s result below is a smaller, mixed-tech history, not evidence of modern scalability. Administrative message routes, annual office work and large culture/ocean-exchange events remain expensive; hashing is not the leading bottleneck. Parallel CPU timings overlap annual wall timings and must not be added to them. No fake clock interpolation or dropped events is used.

New27-contact and82-routing assertions pass, including directed/mode-specific paths, crossing the old8192-answer eviction point, exact warm/fresh paths, changed reach, office funding, successive conquests, live regional population changes and cancellation. Existing12-case performance checks pass on Earth20k and generated200k;13actual200kserial/parallel assertions pass and the mixed-tech20-year hash remains `854f13224abb74b9f59eaeafe13931fdc3e46ebe1396520751a37246d2900be9`. Final114-modern,48-calendar,22-institutional and53-delegation suites also pass. Final Unity current-rules20-year continuation matches the .NET `a44ea...` hash; accepted Earth3-year native continuation retains `484a8ff6dee1994e3b18059361c4022fc8398b7de1b2debef90f6175755291cf` (`native-release20` / `native-release-earth`). UI view-cache code is unchanged. WASM continuation was not rerun in this follow-up; the previously recorded cross-host evidence below is historical. The inherited full-suite Earth generation golden mismatch remains unresolved and expectations were not rebased.

## Exact playback performance pass — 14 October 2026

Solution/native builds succeed. Shared/native Core DLLs match SHA256 `23406579a2de6ea627b6229e8998cf544dc79708e3d47531d795bfc5232089bf`; Presentation DLLs match `e8f45e97355e60805b88efa6c627411ac2e4af5098fb90fce3c8162e2b2aaa98`. Actual .NET, Unity and WASM playback continuation of accepted Earth through140003 retains `484a8ff6dee1994e3b18059361c4022fc8398b7de1b2debef90f6175755291cf`. Browser load/integrity/export still takes roughly128seconds total; this pass does not solve loading. The53-case delegation integration suite also passes. Root shortcut now targets `artifacts/player-history-16-performance/WorldGen.exe`; that dedicated artifact folder is a junction to `D:/WorldGen-history16-performance-20261014` because C: has insufficient space for another player. Previous shortcut is backed up in `performance-audit17/previous-shortcut.lnk`; old player remains untouched.

On the same200,000-cell Continents-plus seed73421 checkpoint (`D:/WorldGen-evidence-archive/history16-200k/current-run/final.world`), actual Unity playback at1000years/s selected improved3.697→7.650achieved years/s over20-second runs. Mean frames16.88→16.86ms, p95frames19.14→17.94ms; maximum frames27.74→32.21ms. Old run74years/75publications; new153years/39publications. Tests start from the same checkpoint but reach different endpoints, and host load/warmup affects timings: this is observed throughput, not a universal guarantee or an isolated causal estimate. Evidence: `artifacts/performance-audit17/native-playback` and `native-optimized`.

The identical20-year .NET session continuation retains hash `854f13224abb74b9f59eaeafe13931fdc3e46ebe1396520751a37246d2900be9`. Bounded playback timing1.929→1.202seconds, cumulative allocation1015→432MB, publications20→5. The120-year continuation retains pre-change hash `d93bab835ad70bfc15b1dde1b53ab547a4914531286b73a67c8442103d7a5f87`. Annual administrative accounts visit sorted holdings (about600–700cells here), not all200000; market migration membership rebuilds only on actual market rebuilds, not every publication. Full hashes/public snapshot isolation remain. Numerical history versions, PRNG streams, crop/warfare/formation rules and geography are unchanged; no save upgrade is required.

New12-case performance suite passes on both Earth20k and generated200k, comparing literal/indexed, partitioned and batched continuations, edited ownership/market branches, detached public actor/archive mutations, and cancellation/recovery. Existing54index,13actual200kserial/parallel,114modern,48calendar and22institutional assertions pass. A sparse-account cancellation check initially failed because IDs no longer guaranteed a polling point; explicit entry/visited-count polling corrected it and the full serial/parallel check passes. No golden expectations were regenerated. Large indivisible annual/culture events, fine-grained region aggregation, archive growth and full checkpoint hashes remain targets; reaching1000years/s on dense histories is not claimed.

## History16 / native ecology 2 — 14 October 2026

Implementation and content contracts are in ALGORITHMS.md. Fresh/reapply/serialized-recipe/cached reconstruction pass with the explicit new content profile; separate comparison confirms unchanged heights, climate, hydrology and coarse partition. The new focused suite passes25 assertions (native habitat/prey constraints, horse/zebra trait contrast, ever-wet floodplain constraints, culture contrasts, deeper web layout, multi-town technology support, complete persistence and all reconstruction routes). Political realism passes12 assertions, including a one-city contested succession with conserved people/army/treasury, successor ownership, individual-state labels, distinct ruling/local culture inspection, capital-cell inspection, old-checkpoint preservation and deterministic saved continuation. Earlier mechanism suites pass114 modern,48 calendar,53 delegation and22 institutional assertions. CLI Preview --verify passes all-stage reconstruction. Golden files were not updated; the previously documented full-suite Earth golden mismatch remains a separate limitation.

Final fresh Earth20k checkpoint: `artifacts/history16-realism/accepted-earth/final.world` (through140000, first state132842 at39.178N in grassland). It records56 states,55 ended,215-year median among ended states, and24 war declarations. The retained5000-cell fixture is under `accepted-check`. The native player is built in `artifacts/player-history-16-realism`; prior15 remains available for rollback. Long exploration CSV/review/image evidence is under `release-earth` (first state132842, Modern capstones150669,750-year tail) and `final-continents`. Those longer exploratory runs preceded final fauna-envelope/fiscal refinements and are not final-model era calibration. No completed final-model modern tail is claimed.

A200k, seed73421, Continents-plus audit uses final native content in `D:/WorldGen-evidence-archive/history16-200k/current-run`. An earlier long exploratory200k run was stopped after its127000 sample because late-era throughput had become very expensive; its CSVs survive in `run`. It did not complete the intended modern tail. Modern/late-dense-history throughput and geographic reach are not claimed solved by this realism pass. The20k geometry spaces candidate cities much farther apart than200k; no claim of resolution-independent settlement placement is made.

That200k checkpoint ends126000, first state120665 in savanna at5.229S:293 inhabited sites,39 live/417 recorded states,378 ended,97-year median among ended states,470 war declarations. This is not a claim that real historical state lifetimes follow that distribution. The final fiscal100-year projection records39→42 live states and1,531,070→1,787,179km² controlled, without rewriting its past; `fiscal-200k-100yr.json` preserves the observation. It is not a controlled causal estimate. Named-site residents are roughly38–41% in these samples and include villages; urbanization/food allocation remain coarse, uncalibrated and potentially too concentrated.

Native and actual WASM three-year continuation of the accepted Earth checkpoint match shared .NET hash `484a8ff6dee1994e3b18059361c4022fc8398b7de1b2debef90f6175755291cf`. Final Core DLL SHA256 is `830fc17107c681d867027dc40863ced406656ff3a33a04c09e1f1d3a7115b49f`. Browser `history16-browser-check.mjs` verifies state names in civilization mode, folded panels and no simulation advance during inspection; readable screenshots are under `browser-ui`. Native hidden-window HUD captures were black and are not valid UI visual evidence; offscreen native map rendering and native continuation succeeded. WASM full-world load/integrity remains slow (about110s in this check).

Final Unity build succeeds with matching shared DLLs; Presentation SHA256 is `6a515e014e390d32eaa1f534b490304720d84a54ce0f241b01aa70d9d64ceed8`. Root shortcut targets `player-history-16-realism/WorldGen.exe`; its prior target is recoverable via `history16-realism/previous-shortcut.lnk`. Own diagnostic players/servers have exited. Duplicate200k source/large SVG intermediates were also removed after retaining the complete checkpoint, PNGs and reports; these can be regenerated. Old taskbar pins may still target previous players.

Disk-space housekeeping moved only agent-created prior intermediates to `D:/WorldGen-evidence-archive/history15-intermediates` and `history16-trials`. Superseded own16 binary snapshots/duplicate history JSON and the superseded200k input were removed; CSVs/reviews/images remain and removed trials require regeneration. No user world/save or accepted player was deleted. Both drives remain low on free space.

## History15 contingent origins and institutional transmission — 14 October 2026

Evidence root `artifacts/history15-emergence`. Only history rules, shared historical presentation/upgrade controls, tests and documentation change; accepted geography/climate/ecology are retained. The final player is `artifacts/player-history-15-emergence/WorldGen.exe`, selected by the root shortcut. Its predecessor shortcut is saved under the evidence root; earlier players and user world saves are untouched. New simulations use15; older histories require an explicit future-model update.

Final accepted Earth20k audit uses `release3-earth20k` through130000 and its exact continuation `release3-earth-tail` through150000: first persistent regional culture70857, first locally domesticated crop106859, first state141535, first complete Modern capstone set149137, then863more years. At120000 it has53cultures,177inhabited named sites and0active states, compared with retained14's142/369/28on the same geography. Final records contain2independent foundations,23locally transmitted foundations and36secessions;14states survive. There are13cultural records of local crop origins over the much longer agricultural interval; independent domestication and independent central government are different transitions. No quota forces these counts. The50-taxon life pack does not reconstruct actual Earth crop/livestock origins, so these are conditional-model tests, not a reconstruction of Mesopotamia/Egypt's locations or dates. The long farming-to-state interval is a gameplay-prior outcome, not a successful calibration to Earth's chronology.

The isolated archipelago80k control (`accepted-archipelago80k`) remains at0persistent specialist cultures and0states through130000. Final changes to urban provisioning/research/formation do not affect its empty-cultural continuation. Ordinary generated continents200k (`release3-continents200k`) reaches120000 with278cultures,630inhabited sites,16active/62recorded states; first culture43779, first state112764. Its origins are5independent/22locally transmitted/35secessions, versus13independent origins in the rejected `release2` trial. The final native factor `.0001+.025*core^6` preferentially suppresses adequate rather than exceptional sites. Its modern ceiling is not yet reached at the120000 safety limit; it is not a completed post-modern-tail test. Earlier `earth20k`, `accepted-*`, `final-*`, `release-*` and `release2-*` experiments are exploratory, except the unchanged empty-cultural control above. Several open-ended diagnostics were deliberately canceled. `release3-*` identifies the final model. No aborted run is counted as completed evidence.

Focused checks pass29new emergence/provisioning assertions,114modern,48calendar,22institutional and53delegation assertions. The accepted15Earth checkpoint also passes54indexed/literal bookkeeping assertions. New checks cover selective ecological opportunity, competitive foraging, fractional practice opportunity, rare versus transmitted institutions, persistent/decaying local teaching, actual teacher save/hash integrity, restored continuation, smaller pre-transport catchments, borrowed versus independently supported writing, post-medieval institutional support, and unchanged14technology behavior. Legacy14three-year continuation matches the retained native14player at `6c605abc2824bc86e91fe3129c40e5e337fa25d17400fb2fa5df69c0064808bb`.

Actual new-player Mono, Core and actual WASM bounded-playback/binary-save continuations agree over10years from the accepted final Earth checkpoint at `f9137954f94f45b580d3b21e1f5a2001d9f884bfdac7f1ed0d5cf722c31e370d`. The earlier release2WASM continuation agreed at `9a2fcf113f2c9b7f2cfaa24a93e1ed6f1cbc94f173ca0d6311f26cc32321159c`; that is retained intermediate evidence, not the final coefficient check. Packaged final Core SHA256matches shared and Unity-plugin outputs: `f75e634fbb76a4633ca2da067b4f0b90d681b6f21cc5d29bc473c4a733765073`. Final shared solution/worker staging and Unity player builds succeed. A concurrent rebuild attempt encountered Windows locks held by our own diagnostic process; an unchanged final full build subsequently passed. No user application was stopped for this.

A30-second rendered final20k modern-state check advances171years, averaging5.70years/s at the requested1000target. Mean frames16.78ms,p9517.17ms,max20.59ms; selection remains folded and no error is reported. This demonstrates responsive rendering, not fast1000years/s late-history throughput. The earlier smaller release2checkpoint achieved16.13years/s; it is not a like-for-like throughput comparison. The regional indexes remain, and expensive whole-checkpoint/global work remains a limitation. Actual audit state/religion/technology screenshots were rasterized; final Earth and200k generated state images were inspected. The earlier pre-state industrial-technology defect was found during visual/data review and fixed before acceptance:15requires a court for independent formal accounts (or real contact for borrowed writing) and supported political urban institutions for post-medieval research.

CLI Preview repeat/reapply/serialized-recipe/cached reconstruction passes all stage hashes. The pre-existing obsolete Earth generation golden remains outstanding; no goldens were regenerated, and the complete ordinary test suite is not claimed green. Coefficients are explicit gameplay priors. Pastoral economies, complete domesticable-animal inventories, detailed labor/nutrition optimization and validation against archaeological datasets remain unimplemented limitations, not implied accomplishments.

Low disk space was handled by compressing this pass's diagnostic evidence and deleting three reproducible intermediate binary snapshots: canceled `release-earth20k/checkpoint.world`, canceled `release-continents200k/checkpoint.world`, and rejected `release2-continents200k/final.world`. Their CSV/JSON/review evidence remains. Final release3snapshots, accepted controls, previous player binaries and every user save remain; deleted intermediate binaries require regeneration, not trash recovery.

## History14 regional bookkeeping and culture pacing — 14 October 2026

Evidence root `artifacts/history14-index-pass`. This pass replaces repeated terrain-wide actor bookkeeping with derived region/member/frontier indexes, local stage dirtiness, exact regional rural-allocation spans, cached site priorities, spatial town screening and append-only actor registration. Public/literal continuations remain the reference; indexes are execution-only and are discarded on incompatible branches/cancellation. Whole checkpoint copying/hashing, some faith buffers and expensive active graph searches remain. No generator/climate/ecology changes or golden updates were made.

Focused checks pass114modern,48calendar,22institutional,53delegation and415retained history9 assertions. New index checks pass54assertions on the dense200k source,55on the pre-state200k source and56on the townless cultural source. They cover exact per-cell versus regional integer censuses, very large populations, interleaved ownership/reassignment, changed region populations, conquest/withdrawal state totals, stable member order, literal/indexed continuation, private idle-check identity/mutation isolation, multi-year small-town playback and explicit upgrade date preservation. A first sparse-stage experiment missed loaded controlled cells whose saved stage was still1; the exact comparison rejected it, and initial dirty-set construction was corrected before promotion.

Old history13 Earth3-year continuation retains `25ee399951da51714d34791e5eb0765fad1b2ddd4d92049b0cb8db7472af762f`. The dense200k100-year playback continuation retains `37c3e3185bc47e9e63ded5c8b5aa8ae9d42af40202b697d4bca4fdba976d09d1`. Cultural200k1000-year literal/indexed profiles agree at `e3571f2085ab256715a0c01ede1ff3ae4997aff15e4e3fad514d8ecc613bcbcd`; pre-state200k100-year profiles agree at `43372cbf46ea10f930550dcf28938daf454f71a348c41834d331a99c167be5e0`. Regional state aggregation/frontier/casualty changes retain these outcomes. Snapshot publication budgets may change intermediate stop points, not fixed-time histories.

Matched30-second rendered tests start from the same history13 checkpoint at50000years,200k cells,14cultures and no towns/states. The previous player achieves242.46years/s; final indexed/weighted playback achieves528.71years/s at the1000target (15862actual years advanced). Mean frames are16.82ms in both; p95is17.11/17.26ms and maximum35.92/24.30ms. Both keep selection folded and report no error. An intermediate indexed build achieved368.12years/s; that is not the final result. Runs share the host with the owner's existing player and other checks; these are available-host observations, not hardware-independent or late-history guarantees.

Three new14dispersal audits retain their original geography: Earth20k first persistent culture22096, generated continents200k43779, isolated archipelago80k none by95000. At95000they have2/7/0cultures and no states. The extended Earth run forms its first state106644and reaches120000with142cultures,369settlements and28active states. These are opportunity/pacing examples, not fitted archaeological targets; native specialization remains possible without a common culture unlock. An open-ended archipelago continuation was stopped rather than spending time simulating an unchecked modern tail; it is not completion evidence.

Native and Core match a complete95000-year new14Earth run at `548de987abb4cae3a86e5b6808594fb2fb772a42b185de8905ef01baed60d9ba`, and a civilized14ten-year continuation crossing the decadal state update at `dd10920adbc5000c9dd09acbfcbf39afa06bdca7cc9666ed528707973bf946e0`. Actual WASM new14cultural100-year playback/save matches Core `f74583db835f188bcb88cf14356a367ba4299521ffd1538e44766fcfaa762b01`. Shared solution/staging and ordinary Unity player builds succeed without warnings; the final packaged Core DLL matches the shared DLL. CLI Preview repeat/reapply/serialized-recipe/cached reconstruction passes all stage hashes. The previously documented obsolete Earth-golden issue remains outstanding; the full historical generator corpus has not been rebaselined or claimed passing.

Final actual WASM civilized14ten-year playback/save also matches `dd10920adbc5000c9dd09acbfcbf39afa06bdca7cc9666ed528707973bf946e0`, exercising regional state census/frontiers at the decadal boundary. The root shortcut now targets `artifacts/player-history-14-indexed/WorldGen.exe`; its predecessor and the owner's running history13player remain untouched. Final Core DLL SHA256is `68f34ee6abfada26d5a4e4745f66087ee692a1c40a904ca244e3d1e010ecd553`, Presentation `309f6841b03d0f5210e6646610f98892fd6a9abb7f3d6fabea696a12600c03b2`, matching shared outputs. Temporary diagnostic players and web server exited; no user world saves were removed.

## Playback throughput regression fix — 14 October 2026

The earlier history13 year-boundary publication policy imposed an artificial ceiling of roughly one simulated year per rendered frame, even during sparse early prehistory. Shared presentation now requests all accumulated time credit, bounded by the existing32-event budget; the browser worker accepts the same multi-year batches. The authoritative solver, queue, model versions and historical decisions are unchanged. Coincident events remain atomic and slow-clock publication retains its one-event budget. Expensive individual events remain a limitation: an event-count budget is not a wall-time deadline.

Focused tests pass48calendar assertions and114modern-history assertions. The new regression simulates60presentation frames at1000years/s and verifies the requested full1000years, rather than60. On an actual200k fresh starting checkpoint, direct1000-year continuation and bounded multi-year private-session continuation match `fa6c60fab31dc34cdec5f480e7713fdd48c30d2b81dc63b2fc708f2c5d422987`. A dense final Earth20k three-year continuation retains the previously recorded `25ee399951da51714d34791e5eb0765fad1b2ddd4d92049b0cb8db7472af762f`. No golden expectations were updated. Evidence is under `artifacts/playback-throughput-fix`; the sections below retain the earlier measurements and their then-current year-bounded policy as historical evidence, not the corrected playback contract.

Matched rendered30-second tests on the same fresh200k checkpoint measure59.0years/s before and812.4years/s after, at the1000years/s target. Mean frame times are16.78/16.79ms,95th-percentile17.05/17.27ms and maximum37.67/33.01ms; both report no error and keep selection folded. The corrected player advances24383actual years, not fabricated/interpolated dates. The machine also has the owner's separate running player and other validation work, so this is available-host evidence rather than a hardware-independent speed guarantee or a promise for dense civilized histories. The ordinary non-development player builds successfully, its packaged presentation DLL matches the shared build, and the root shortcut targets `artifacts/player-history-13-playback/WorldGen.exe`; the owner's already-running player was not stopped or overwritten.

Actual WASM playback also accepts a100-year multi-year request and produces a binary checkpoint matching direct Core: `206d2a28b91bfdfb874303b86f329b9437f41587a3dc469cfd1203d181257181` on the fresh Earth20k fixture. This exercises the removed worker-side one-year restriction, not just shared presentation unit tests. Browser load/validation/serialization remains costly; this is an integrity/adapter check, not a browser speed claim.

## History13 crops, modern capabilities and runtime — 14 October 2026

Owner-authorized eight-hour pass,06:10–14:10UTC. Evidence root `D:/WorldGen-modern-20261014`; `release-*` identifies final combined-rule fresh histories rerun after contact-cache dependency checks. `accepted-*` retains the earlier complete controls used for matched runtime/continuation comparisons. Earlier experimental folders isolate individual changes and are not compatibility goldens. Require completed audit exit/DONE and checkpoint hashes: an output folder being rerun can still contain a previous summary. Generator3.32, accepted climate/hydrology/ecology, prehistory6/calendar2, observer Year0 and the permanent agricultural opening remain unchanged. New histories select13; saved1–12 retain their own rules unless explicitly upgraded. No golden expectations were regenerated.

Shared build/staging and the packaged Unity player succeed. Focused checks pass114 modern mechanism assertions,29 canonical-byte/hash-provider assertions,46 calendar assertions,57 history presentation assertions,22 institutional succession assertions,53 delegated administration/demography assertions,30 local formation/administration assertions,415 retained history9 assertions,305 retained general history assertions, and20 serial/parallel assertions on the complete release200k history. The parallel checks cover actual owners/communication/accounts, office-funding and control-barrier invalidation, changed/removed shipping edges, nearby non-port access, restored branches, canceled/retried work and three-year continuation equality. Additional modern tests cover a replacement shipping pair with unchanged route count, explicit ocean-navigation cache dependencies, fresh-start record isolation, namesake-city narrative links and no-culture reports. The calendar test retains old-save continuation checks but reconstructs an early growth reference for its new-settlement assertion; the general suite's later diagnostic save had legitimately exhausted nearby sites. CLI Preview repeat/reapply/serialized-recipe/cached reconstruction retains matching all-stage hashes. The full normal generator suite still fails its previously documented obsolete Earth golden; native Validate has the same inherited mismatch. Neither is reported as passing or hidden by regenerated expectations.

All six final fresh audits reach the first culture's Modern capstones and continue750years afterward:

| Map | First state / modern capstones / endpoint | Active / recorded / ended states | Inhabited named sites | Wars |
| --- | --- | --- | --- | --- |
| Earth20k, seed73421 |105583 /113362 /114112|50 /172 /122|432|358|
| Continents80k, seed17 |104419 /113282 /114032|46 /251 /205|528|725|
| Archipelago80k, seed42 |107423 /116141 /116891|16 /69 /53|311|157|
| Forest5k |106897 /115368 /116118|3 /11 /8|30|13|
| Oceanless Lakes5k |104353 /113072 /113822|63 /279 /216|569|478|
| Generated200k |105094 /113488 /114238|101 /570 /469|1240|1573|

The forest fixture is mixed forest, not a uniform-rainforest planet; it does not establish a600000-year emergence calibration. The Earth fixture uses measured relief but simulated climate/habitats and procedural plausible crop provinces, not literal Earth crops or a reconstruction of Earth history. First200k states are neighboring floodplain communities at27°S, followed by regional exceptions; first Earth state is grassland at49°N. Thus emergence is not locked to the equator or original homeland. All rainforest first-foundations in the final Earth/continents/archipelago audits record introduced crop traditions. The forest control also permits later native tropical development. These are observations, not desired continental rankings or a geographic prohibition.

Additional generated controls use seed261014 and5k detail. Boreal develops its first state114895, modern capstones123184 and ends123934, with4active/13recorded states and46inhabited sites. Oceanless Desert reaches200000 with3383foragers, no regional cultures, no towns and no states. The latter is a negative control, not a completed modern progression. Its valid empty culture/faith maps exposed an audit-report indexing bug; the report now supports that case, with a zero-culture regression. A fresh13 start also retains only the saved ceiling, never old shipping/civic-memory records referring to absent new towns. This fixes new-history preparation, not the continuation rules of an existing valid history.

Final200k review records235764890 people,127 provisioned remote communities,59 successful land-avoiding sailing detours,1748 named crop traditions,3160 occupied-port shipping links and992 dated city-rivalry memories;660 rival city pairs currently share a ruler. Food provisioning conserves sent=delivered+transit losses. State/religion/technology PNGs and actual logs were visually reviewed; turnover counts are not validation of a target conflict frequency. Atlas traversal resolves1500 sampled profiles and122034 cross-links without changing checkpoint `b0a90f4e5531889bb919837fd537826586b0cd8f3a01f384a6f6c239f0bf7633`.

Four separate saved-ceiling scenarios begin from the same pre-modern Earth checkpoint and reach Medieval/EarlyModern/Industrial/Modern capstones, continue at least750years after them, and record zero forbidden adoptions. The Medieval source already had750post-capstone years; its additional750-year continuation ends1500after its capstones. Above-ceiling existing knowledge is not erased when a user lowers the limit.

Runtime evidence is separate from historical plausibility. On the same200k checkpoint, initial actual rendered playback was approximately0.30years/s; final year-bounded publication reaches1.82years/s, mean16.98ms frames,95th-percentile18.49ms, maximum29.99ms in60seconds. A different denser full-modern200k fixture improves0.19→0.39years/s with year-bounded publication; its final90second run averages16.85ms frames,95th-percentile17.12ms and maximum36.43ms. Both keep selection folded and report no error. Runs share this machine with audits, so these are not controlled hardware-independent speed guarantees. Native annual integration on that dense fixture decreases from about3.0to2.0seconds after indexing live diplomatic relations; actual ten-year output remains exactly `8d6eea4637050aafe403ce546865eb081ecad4ce7ea552064742daa94af20714`. Independent administrative searches use at most4workers, with ordered reductions. Publication hashes/copies are reduced, not authoritative events.

Final native/Core and actual WASM year-batched playback all continue final Earth20k by exactly3years to `25ee399951da51714d34791e5eb0765fad1b2ddd4d92049b0cb8db7472af762f`. Native binary reload plus37years passes `961572874c9d8300f912eeca0a35dc2b3b911b9cae0b808535374d0ba03458b4`. Actual WASM final continents80k three-year continuation also matches Core `4fc8a88d7b96aa93a4897308db5d6f8781125b13e756efee32fb4d4149c1081a`. Retained history12 ten-year200k continuation matches both Core and the final native player: `0f9a85d54d07e389feecdd4b68048ff542285863b1d22ed7c088d033231e4807`. Canonical optimizations read every field and preserve old bytes; they do not reuse a stale integrity verdict.

Final browser interaction checks pass actual map-click folded selection, read-only linked articles, city crop surveys, cultivar traits, dated civic-memory narrative, ecological English summaries/species/food-web, and changing/saving/reloading the technology ceiling with unchanged exact clock/population. Browser screenshots and actual native map/atlas/ranking/HUD captures were inspected. Tests synchronize article identity and the selected tab's CSS transition; an earlier premature screenshot was a harness timing error, not accepted evidence of correct highlighting.

Important remaining limits: individual annual/global events are not yet fully resumable sub-jobs and can still hold the displayed date while background work completes. Native map responsiveness does not mean1000years/s. Browser WASM is substantially slower: final20k load takes114seconds, its three-year batched continuation53seconds and serialization18seconds; final80k load341seconds, continuation89seconds and serialization46seconds in the recorded runs. Save buffering preserves format/integrity but has not solved browser loading. Modern resource/energy supply chains, tactical armies, detailed industrial demographics, evolved language and dynamic prey-naivety are not implemented. Modern populations and remote settlement timing remain uncalibrated; the Earth Australia cohort remains particularly late/small. No unsupported climate-to-phoneme rule or blanket animal-fear bonus was added.

The normal, non-development release player is `artifacts/player-history-13-release` → `D:/WorldGen-modern-20261014/player-release`. Packaged Core SHA256 `62131D02FC6AB19E05AC7451B81CD92D435C703B5AFDFDC7FED2541A77EF94DA` and Presentation `81F6CB97B117315DA0C58055C02135294273A896F1FF1E61FA8237833F29943B` match the shared outputs. Development builds are now an explicit `-DevelopmentPlayer` option, not the silent default. The root shortcut targets this verified13 player; `D:/WorldGen-modern-20261014/previous-history12-shortcut.lnk` preserves the previous launcher and the older12 player remains intact. Final visible Earth capture is also copied to ignored `artifacts/history13-final-earth.png`.

Extended stress evidence: direct .NET,501 year-bounded private-session publications and the native500-year continuation all match `eb8bbff44701cf5ac76297b14a23a66484492a4315ce59f10068975660d188ed` from the dated accepted200k checkpoint114238. Localized shipping invalidation retains that same500-year Core hash. Before localized invalidation, the full native profile averages5.57seconds per annual event and reaches30.08seconds at its most expensive annual boundary; short ten-year figures alone therefore understate the worst case. This is a reason to continue splitting global jobs, not to fabricate interpolated years. The final ten-year native port-aware check still matches `8d6eea4637050aafe403ce546865eb081ecad4ce7ea552064742daa94af20714`; route misses decline415→375, with no reliable wall-time gain claimed from concurrent runs. Its final90second dense-world playback is0.37years/s, mean16.89ms frames,95th-percentile17.12ms, maximum56.70ms, with selection folded and no error. The full runtime story includes both improved throughput and remaining expensive events.

The final port-aware native100-year continuation matches Core `37c3e3185bc47e9e63ded5c8b5aa8ae9d42af40202b697d4bca4fdba976d09d1`. Its annual mean5.15seconds and maximum26.44seconds confirm the remaining expensive-boundary limitation; these are not an apples-to-apples comparison with the longer500-year profile. Final normal-player Earth3-year continuation/save+37-year roundtrip and retained12ten-year checks also match the recorded hashes. Final WASM playback repeats the3-year25ee3999… hash with the port-aware cache; its observed load/continuation/serialization remain about130/58/19seconds under concurrent load, not an improvement claim over the earlier measurements.

The last shipping-contact optimization indexes actual endpoint rows and uses a conservative151km spatial broad phase followed by the original150km predicate and live populations. The first experiment accidentally used integer division for its radius; the100-year hash check rejected it (`6f46f1…`), and no such player was promoted. `contact-index-*` is therefore rejected evidence. Corrected `contact-exact-*` and `exact-final-*` preserve37c3e318… at100years andeb8bbff4… at500years in Core. Literal-scan tests and a real200k non-port/near-port test cover the missed case. The final native100-year run also matches37c3e318…, with annual mean1.60seconds/maximum10.89seconds versus5.15/26.44before indexing on the same checkpoint; religion passes total8.23seconds in that run. Concurrent workloads prevent treating this as a controlled speed guarantee. Final rendered dense200k playback is0.53years/s, mean16.89ms frames,95th-percentile17.34ms and maximum37.45ms, with48real year-boundary publications, selection folded and no error. Global cultural jobs remain expensive. Final actual WASM3-year playback, native Earth3-year/save+37-year and native atlas viewing again match the earlier exact hashes with the indexed build.

The final indexed player repeats the original matched200k fixture at1.87years/s over60seconds/112actual years, mean16.86ms frames,95th-percentile17.56ms, maximum35.39ms, folded selection and no error (`playback-exact-comparable200k`). This supersedes the earlier1.82candidate measurement for the delivered binary, but is still not a promise for denser later histories. Final browser UI checks pass again under `browser-exact-final-ui`; the actual final WASM binary save validates25ee3999…. The ordinary full repository test run was repeated and still stops at the known Earth golden (`Program.cs:326`), not a new history assertion failure.

The delivered indexed native player's complete500-year stress continuation also matcheseb8bbff4… (`native-contact-exact500`). Annual events average1.38seconds, with maximum10.89seconds, versus5.57/30.08in the earlier complete500-year profile of the same source/history. This removes the repeated global shipping-scan cost without changing the recorded outcome. Independent actor search work remains substantial, and global events are still not all subdivided into resumable actor jobs. Final200k fresh regeneration again ends114238 with checkpointb0a90f4e…, first state105094, modern capstones113488 and exactly750post-capstone years; the complete sampled population/state/event trajectory agrees with the preceding control.

Closing cross-host continuations use two other final worlds for500additional years: continents80k matches`b153aac8a61b2d7c58ecb92dea8aae9d96445c84452da473d93f4455c0984bd0`; archipelago80k matches`e7e990e2cdd65696bcf4c9a00d1154ed660d950fb18491bd7dcab2ee43d029a9`. These are supplemental stress tests beyond the standard capstone+750 audits, not a claim that the technology ceiling occurs simultaneously everywhere. All final test players exited; the temporary5094browser server was stopped. User worlds were not deleted or overwritten. Closed diagnostic builds were NTFS-compressed to conserve space; earlier accepted12 remains available.

## History12 institutional continuity and local/realm strain — 14 October 2026

Owner-authorized additional50-minute pass; evidence root `D:/WorldGen-history12-20261014`. Only `release-*` identifies final combined-rule audits. Earlier unprefixed12 experiments isolate succession and are not compatibility goldens. Generation3.32, accepted climate/hydrology/ecology, prehistory6/calendar2, agricultural opening, observer Year0 and medieval technology ceiling are unchanged. Histories1–11 retain their rules; existing9/10/11 may explicitly update future rules. No golden expectations were regenerated.

The two diagnosed causes and their priors are documented in HISTORY_SIMULATION.md: routine council turnover previously lost45 legitimacy every approximately14-year term despite at most28 recovery; one unsupported cell previously imposed a maximum-distance whole-realm strain. History12 distinguishes institutional handovers and aggregates resident service burdens, while retaining severe local provincial grievances. A real scheduled handover probe produces657 legitimacy under11 versus687 under12 from the same supported starting conditions. No general increase to administrative radius or free claims was introduced.

Build/staging succeeds with0 warnings/errors. Focused final checks:22 institutional assertions (actual scheduled succession, local versus realm burden, partitioning, fresh/warm cache, JSON/binary continuation, precise clock/epoch, geographic immutability and cancellation);53 pinned history11 assertions;7 actual network assertions on the final200k world (3849 office-supported extra cells, unfunded and remote offices fail);415 history9 assertions;30 history10 formation/administration assertions;45 calendar assertions;57 history presentation assertions. CLI Preview repeat/reapply/recipe/cached reconstruction passes with identical all-stage hashes. The published history11 three-year200k hash still matches `555cb81eec659db4e9565538b4f5f4d1844201fbc0f41f042bf0d8c8907d3205`. Full generator-suite coverage is not claimed: the previously recorded obsolete Earth golden remains unresolved and untouched.

Fresh final audits all run to the three-capstone medieval ceiling plus750 years:

| Map | First state / capstones / endpoint | Active / recorded / ended states | Wars |
| --- | --- | --- | --- |
| Earth20k, seed73421 |105354 /109796 /110546|28 /56 /28|37|
| Continents80k, seed17 |103835 /108234 /108984|16 /31 /15|16|
| Archipelago80k, seed42 |107423 /111950 /112700|2 /5 /3|4|

Earth's largest current state has395179km² and16 towns; the archipelago retains9 provisioned remote communities. These are observations, not desired historical quotas. Dated state/religion/technology PNGs and the Earth era storyboard are saved beside the SVGs and actual site/chronicle records; final Earth/Continents/Archipelago maps were visually inspected.

Additional final extreme-map regressions also complete the capstone+750 interval: forest5k first state108211/capstones110334/end111084,15 sites and2 active states; oceanless Lakes5k first state104644/capstones108876/end109626,412 sites and110 active states. On an oceanless world the audit correctly omits irrelevant ocean navigation from its capstone requirement. These checks establish continued development and valid saves, not that every tropical forest world should follow the forest fixture's exact timetable.

Matched200k continuations use the same released11 checkpoint at110357 and end111107 (2250 years after the first medieval capstones). Baseline11 has139 active states,592 recorded/453 ended,684 lifetime wars and385 secessions. Combined12 has116 active,545 recorded/429 ended,642 lifetime wars and343 secessions; its750-year interval therefore includes146 new wars and34 secessions, not frozen politics. The largest current area is166518km²/17 towns versus baseline136402km². Religion contact disagreement is5.4%, isolated dominant faith0.3%, with8 remote communities and conserved provisioning. These runs do not establish broadly realistic empire sizes or a calibrated conflict frequency.

Final atlas checks leave checkpoint integrity unchanged:813 sampled Earth profiles/19245 cross-links;1469 sampled200k profiles/78396 cross-links, hash `1d2e69dabf40072aba0b2c31e8dd92937834ad912353c189cad2c36981824960`. Added leadership/continuity sections report actual selection methods, completed tenures and disputes rather than invented genealogy. Reviews include appointment-method tenure summaries and `state-pressures.csv`.

Final browser reader checks pass population flows, administrative accounts, leadership continuity, campaigns, consistent DOM selection/accessibility and unchanged simulation time/population; explicit11→12 pause-menu upgrade retains exact seconds/epoch and writes a binary save. The saved browser world continues identically in Core and native:37-year hash `0af13fc5bb482e7a9b29b5f3cbf4e97b1967b75c0f117695a38da06b540711d9`. Native/Core final mechanism fixture37-year hash `ca8061f85535208646e559a5f2f662e7f425b28f85a24590a310321df36b1fd8`; native binary roundtrip plus another37 years `0f4ab5321ffa0d724e13b93fd016df7b70308565bb2d11e1de52b3c275544d4f`. Native checks are headless logic/save checks, not proof of visible GPU UI correctness.

Final packaged Core SHA256 `24CC39704BF7281DB3013AC3577BDC512112C9A80A86B12111F067263BE7073E`; Presentation `3302C40770862A28D19C31E50291F2185ACD60040DFDEBE48DF4E50FA3153E58`, matching shared DLLs. Player location is `artifacts/player-history-12-release` → `D:/WorldGen-history12-20261014/player-release`; prior11 remains intact.

Final200k native/Core three-year continuation also matches `250f8f96db2a90bc195611f7f59ccb37418959cfaa02f53e0915d2fddeb9feda`; native binary save/reload plus another37 years matches `cbcdcc77992d8f724c21774990ff4f6fc2392d08e078d321a3991f485b495f3d`. The root `WorldGen.exe - Shortcut.lnk` now targets the verified12 release; the previous11 shortcut is backed up at `D:/WorldGen-history12-20261014/previous-history11-shortcut.lnk`. Existing saves remain on their saved rules unless explicitly upgraded; a new simulation provides a clean comparison.

Remaining investigation: very large territorial empires are still limited, and fiscal opportunities disproportionately favor named settlements. For example, the final Earth leading state's annual account has29842 urban receipts but136 rural receipts; this alone is not a real-world tax-rate comparison because these are game account units and political holdings exclude much countryside. Nevertheless, [Cicero's grain-tax evidence for Sicily](https://beta.perseus.tufts.edu/urn%3Acts%3AlatinLit%3Aphi0474.phi005.perseus-eng2%3A2.3/) supports investigating agricultural revenue and rural incorporation, rather than blindly increasing empire reach. No hurried tax rebalance was made in this pass. Late200k native interactive throughput also remains a performance limitation; preliminary diagnostic annual-step timings must not be advertised as1000 years/s.

## History11 connected institutions, local demography and ocean communities — 13 October 2026

Owner-authorized three-hour pass; evidence root `D:/WorldGen-history11-20261013`. New simulations use history11; geography3.32, accepted climate/hydrology/ecology, calendar2, the permanent agriculture opportunity and medieval technology ceiling are unchanged. Mechanisms, units, research motivations and uncalibrated priors are in HISTORY_SIMULATION.md and ALGORITHMS.md. Intermediate history11 experiments are private diagnostics, not accepted compatibility goldens; `release-*` identifies the final competitive-faith runs.

- Focused mechanism checks pass53 assertions, actual200k delegated-network checks7, retained history10 local checks30, history9/formation/territory/era checks415, calendar45 and historical presentation57. Coverage includes exact warm/fresh/partitioned replay, binary/JSON continuation, immutable/cancelled sources, unused generic-knowledge independence, bounded local migration, crowding response, conserved subsistence allocations, funded connected offices, disconnected urban revenue, institutional predecessors, physical-area district cost/opportunity, deep membership copies, overlapping/disconnected record rejection, contextual war restraints and real provisioned voyages. The dated history10 fixture still resumes37years to `627a8570fe35f1b23f27e1d909c14959aa55ab92b90f91b2b118b9f2379a406e`.
- Full shared build/worker staging succeeds without warnings. CLI Preview fresh/repeat/reapply/serialized-recipe/cached reconstruction has matching all-stage hashes. The full repository suite was run and still fails its previously documented obsolete Earth generation golden (`Program.cs:299`); it is not reported as passing, and no expectations were regenerated. The corpus Earth file has only11historical generation stages, while current3.32 includes subsequent sub-cell/ecology stages. This pass does not repair or bless that inherited discrepancy.
- Fresh final controls: Earth20k first state105354, medieval capstones109796, end110546; Archipelago80k(seed42)107423/111950/112950; Continents80k(seed17)103835/108234/108984; forest5k108211/110334/111084; oceanless Lakes5k(seed42)104644/108876/109626. All continue750years after capstones, except archipelago1000. For an oceanless audit, navigation is not a required capstone; this is diagnostic stopping logic only, not a new technology shortcut. Actual maps, settlement/transition records and archives were reviewed, rather than inferred from final counts alone. First-state maps remain local; broad grey occupation is not direct state ownership.
- The physical-district correction was motivated by an actual200k experiment: largest surviving state grew from26174km² before area-normalized district incorporation to94449km² afterward;215multi-cell projects were recorded,180completed. Earth20k coarse districts remain indivisible cells; archipelago/continents80k also use multi-cell projects. This corrects one mesh-dependent bias, not a promise of equivalent histories across resolutions. Final200k rerun and later continuations are recorded below when complete.
- Actual religion-map review exposed reciprocal cellular replacement producing alternating bands. The final model counts incumbent support and competing contact; it does not blur/recolor the map to hide the issue. Earth adjacent-faith disagreement changes49.8%→14.5%, isolated dominant cells10.0%→1.9%, with106dominant traditions retained. Final archipelago disagreement10.3% and continents5.2%. These are artifact diagnostics, not desired religious homogeneity or historical accuracy scores. Images are retained alongside authoritative snapshots.
- The city/state atlas explains actual annual/cumulative demographic flows, accessible urban/rural revenue, regional office provenance, campaign intent and overseas communities. Former states retain dated last-active population/area estimates; absent historical budgets are labeled unpreserved rather than fabricated from zero current accounts. Final Earth article audit resolves841sampled profiles/22513cross-links; archipelago437/4671, without checkpoint mutation. Browser tests verify article content, explicit history9→11 and10→11 upgrades, unchanged precise time/Year0 and binary saving. Accessibility/current-tab assertions and paint synchronization support screenshot checks. A browser test run during asset replacement stalled at bootstrap; restarting the fingerprinted-asset server and rerunning passed.
- Compiled native/Core37-year continuation of the new mechanism checkpoint matches `7203674d95f8cc3ea57911a877fcb5ccf7aa6cfa5011539c7e6d1b22d562585a`; native binary roundtrip and another37years pass. Player DLL digests are checked against shared outputs. Hidden native viewer logic and CPU political-map exports pass, but framebuffer screenshots are black; they are **not** claimed as native UI evidence. Browser HUD/articles and diagnostic maps were visually inspected instead. A transient nested PowerShell console-title/pipe error occurred during one build invocation; direct PowerShell invocation and deployed-DLL verification succeeded.
- Read-only profile on Earth20k: hash35.1ms, validation41.9ms, copy4.6ms, annual step174.6ms on this host/run. Administrative connectivity uses exact signatures and public diagnostics return isolated copies; annual finances/population are never cached. No new full2.56Mhistorical performance claim is made. Larger histories remain expensive, and annual warfare is still abstract rather than simulated tactical armies.

Generated worlds/screenshots/caches/binaries stay outside source control. Obsolete assistant-produced intermediate SVGs were compressed into verified recoverable `.maps.zip` archives on D:; their worlds, JSON and logs remain, and no user save was removed. The earlier history10 executable is retained. Generic crops, regional rural populations, simplified finance/government, sampled navigation and dominant-belief proxies remain substantive limitations, not a scientifically calibrated historical reconstruction.

Final200k competitive-faith rerun completes at109607: first state104728, medieval capstones108857,750post-capstone years. It records561inhabited sites,109active/370total states,261ended states,327wars,231secessions,42contested successions and5remote ocean communities (467existing founders; longest3207km). Largest surviving state119621km²;200incorporation projects,198multi-cell and178completed. Faith disagreement5.2%, locally isolated dominant cells0.3%;244dominant traditions remain. Provision expenditure and transferred residents are retained in saves; provisioning remains conserved. This outcome is evidence of turnover and removal of the observed striping, not a historical benchmark.

Final native200k three-year replay matches Core at `555cb81eec659db4e9565538b4f5f4d1844201fbc0f41f042bf0d8c8907d3205`; binary roundtrip and another37years match `1ea26a9e7a8d3200176d0f733b261829866e450e07895e91bce1e70c69178273`. The browser-written history9→11 save resumes37years identically in native/Core at `f99c6c771cb189ed7640cd158e162d8a8fc69699079054aee81313f03a789e60`. Final shared/deployed Core SHA256 is `2B5DFD18566F2DC856267FCDD307F1C6D48C01F63B5E73D5D79BADF7C9EC8E55`; Presentation `14785EB68977410CCBF5E61E4954D1C0CC7515765ED638025DA981C210F1D7EF`. The root shortcut targets `artifacts/player-history-11/WorldGen.exe`; its prior history10 shortcut is preserved at the evidence root. Existing histories are not automatically migrated by this launcher change.

Additional final200k stress continuation reaches110357 (1500years beyond capstones, deliberately longer than the normal750year cohort tail):627inhabited sites,123active/480total states,357ended states,496wars,309secessions and63contested successions. Seven long-range communities retain655founders. Provisioning remains conserved; faith disagreement5.3% and isolated dominance0.3%, without a return of alternating bands. Both political and religion maps were rendered and inspected. Earlier pre-competitive-faith longer continuations are labeled separately and are not final-model evidence. `scripts/history-check.ps1` now exposes sampled audit observation/map intervals (defaults250/2500years) while retaining the500–1000year normal tail and immutable fresh output folders.

The final later200k atlas audit resolves1464sampled profiles and71807cross-links without changing its checkpoint (`847cddfad200c9d2bb8bd70fe1630d2b3dab433f869dafa114667a702cc31611`). Political transition logs record both released territory and changed ownership/ruling culture; new claim totals in the first resumed observation include the starting holdings and must not be mistaken for that interval's annexations. Maximum surviving area is133946km² in this continuation. Larger empires remain a balance/abstraction limitation; turnover counts alone do not establish historical plausibility.

## History10 local formation and administration — 13 October 2026

Evidence lives under `D:/WorldGen-local-history-20261013`. New historical version10 removes the generic knowledge progression/gates/migration multiplier and historical glacial capacity forcing. Generated terrain, climate, hydrology, ecology, political cell coloring and worldgen3.32 are unchanged. Agriculture uses the owner-requested shared opening, with subsequent local crop adoption requirements. Old histories retain their original rules; history9 has an explicit future-model upgrade in the pause menu.

- `--local-history-only` passes30 assertions: actual knowledge-value independence through120000years (all zero versus all10000, then equal entire authoritative outcomes), no new accumulation, no glacial food-capacity penalty, diverse actual formation dates, no crop adoption before the shared opening, continued medieval development, unchanged whole geography/climate, irregular-clock partitions, deep binary continuation, readable administrative atlas records, no free remote-town influence origin, preserved events/dates on opt-in upgrade, disconnected costs, funded incorporation delay/completion, unfunded abandonment, corrupt account rejection and immutable sources. `LOCAL_HISTORY_FIXTURE` selects the archived history9 Earth mechanism fixture; `LOCAL_HISTORY_TEST_OUTPUT` keeps generated evidence outside source. Test records are not new generation goldens.
- The historical9 suite still passes413 assertions. Calendar checks pass45 and historical presentation checks56. The earlier calendar1 native save still resumes37years to its original `f97d6215abf5aa6c9933770a30eaf2f08047e2907d7ba59b7d9b3c37533a9f29`. CLI Preview all-stage repeat/reapply/recipe/cache reconstruction passes. Solution/worker staging and Unity player compilation pass. Intermediate build attempts collided with agent-owned running audit binaries; isolated diagnostic output folders resolved those locks without touching user processes.
- Fresh audits include measured Earth at5k/20k, a generated200k world, and a5k forest world. First cultures occur at9624/16969/8601/11853 rather than a shared58–70k gate. Final Earth20k first state104305, medieval capstones108207, end109000; final forest first state110910, capstones114584, end115334. All audits intentionally inspect500–1000post-capstone years. Intermediate and pre-sponsorship-fix runs remain labeled pass1/pass2/pass3, not release expectations.
- Actual maps were inspected at the first-state period and later development. Generated200k early states are small local centers rather than instant culture-wide territorial blobs. Non-state occupation and advanced-cultural areas remain separate grey layers. Both Earth and generated results include cultures outside the tropics; their locations are evidence of no latitude-only gate, not proof of scientific geographic calibration. The independent forest control still produces states, not a jungle veto. Generic crop/resource distributions and regional population aggregates remain limitations.
- The review found a sponsored-settlement bypass after initial territory tests: old sponsorship directly granted control. Final code routes it through the same supported, funded incorporation projects as normal expansion. Existing-border town affiliation remains legitimate. Paid district offices retain the possibility of larger later administrations rather than imposing a permanent small-state cap.
- Browser checks exercise the actual pause-menu upgrade button, unchanged time/Year0, atlas and binary save. Source/deployed final Core SHA256 is `425EEE4B9B55455658ED02EB84AE9EAE4763309324FFE6404B842B2CEC7D367F`. Final native continuation evidence is recorded below when complete. No screenshots/caches/binaries are intended source files. The new player is stored on D: through `artifacts/player-history-10`; the older assistant-built history9 player was preserved on D: with a junction at its original path because C: was nearly full. Earlier calendar releases remain intact.

Final compiled native/Core37-year continuation matches `627a8570fe35f1b23f27e1d909c14959aa55ab92b90f91b2b118b9f2379a406e`. Native binary roundtrip and another37years match `59149c54ec8438693603ae8788195053007acd2dc27ae707c6e0272f187a9ece`. The final browser-written upgraded save resumes37years identically in native/Core at `a19a3d1123e6e1e12eb3a2e6f531351922e7ab1f60a49c8b6cdc4e35709ae970`. Browser checks were repeated after the sponsorship correction. Project shortcut now launches the history10 player. `git diff --check` passes with inherited line-ending warnings.

The final200k run also completes: first state103789, medieval capstones108023, end109000 (977post-capstone years). Final Earth20k and generated200k political maps were rendered and inspected after sponsorship was corrected. These show localized controlled states, not immediate culture-wide blobs; broad advanced-cultural grey regions remain visually separate from political control. This is an available-host sanity check, not a fitted state-count/area target or proof that all future history is realistic.

## Civilization Year0 display — 13 October 2026

Presentation-only observer epoch: minimum recorded state founding year, including ended states, defines Year0. Existing saves derive it without changing Core fields, simulation dates, scheduling or hashes. Clock, chronicle, atlas profiles/timelines and lore export use the shared formatter. Before-epoch records retain their real ordering and display as before civilization. Calendar tests pass45 assertions, including zero-year behavior, pre-epoch records, fine clock precision, collapse/reload persistence, unchanged raw dates and matching atlas city/state founding labels; historical presentation tests pass56 assertions. Solution/worker staging succeeds with no warnings. Core SHA256 remains `0C4AEF1C856021EA9EA78E18E7C05BB918B36C6A36C43D59A793BD43D61D1E45`. No generation expectations changed. Browser next-event control check has been adjusted to compare the displayed relative year; the browser runtime check was not repeated for this small display-only update.

## Stochastic calendar2 — 13 October 2026

Evidence: `D:/WorldGen-stochastic-20261013`. New temporal version2; no generation goldens changed. Calendar checks pass36 assertions on both Earth and generated Continents fixtures, including genuine requeued random times, independently scheduled culture actors, actual settlement creation, irregular partitions spanning newly created actors, single-event work budgets, source immutability, migration, binary/JSON continuation and invalid schedules. The Earth2000-year continuation creates29 additional settlements; generated Continents creates17. All observed new founding years are off the old50-year grid, with varied residues (Earth examples70120,70140,70167,70202,70211; generated68904,68952,68989,69014,69025). Tests inspect actual settlement records, not a timing-window utility alone.

The legacy history9 suite passes413 assertions. Continuing the preceding calendar1 native save for37years still gives the previously recorded `f97d6215abf5aa6c9933770a30eaf2f08047e2907d7ba59b7d9b3c37533a9f29`. Browser calendar controls, clock-only rendering, real-time play/pause, next-event stepping and save checks pass; geography-only defaults/opacity/first-event checks also pass with an actual random sub-year first event rather than an expected50-year boundary. Screenshots are diagnostic control checks, not a new validation of historical realism. Existing founding dates are deliberately not rewritten.

Population aggregates are refreshed before stochastic authoritative readers, including religious population reads, so UI checkpoint refreshes cannot influence later trials. Annual harvest/economy/campaign and century demographic integration remain periodic; no claim is made that all historical subsystems or tactical warfare have been converted to independent actors.

Full solution/worker staging and final Unity player build pass. The final Core and native2000-year randomized Earth continuation match `09ad1e5c2d1b209631679cefe3590171c7481a5a6d39cbb15b54dffa209e29ad`; native binary save/reload and37-year continuation match `7566a49a52e41cea2fb61d8b28c02a7305c44ad149511a35af3dc2495cea311f`. Browser-written calendar2 checkpoint continuation in final Core/native matches `d136dde6e46acba9be9c91997cdb1d0d57fc46b064f9ad53a8cde403b4c4212e`. Source/deployed Core SHA256 is `0C4AEF1C856021EA9EA78E18E7C05BB918B36C6A36C43D59A793BD43D61D1E45`. CLI Preview all-stage repeat/reapply/recipe/cache reconstruction passes. Root shortcut now targets `artifacts/player-calendar-2/WorldGen.exe`; its prior shortcut is preserved in the evidence folder and calendar1 player remains available. `git diff --check` passes with inherited line-ending warnings.

## Event-calendar-1 and time-rate controls — 13 October 2026

Evidence is under `D:/WorldGen-calendar-20261013`. History9 and worldgen3.32 remain unchanged; optional saved calendar metadata has its own temporal version. No generation goldens were updated.

- `--calendar-only` passes26 assertions covering deterministic heap/window ordering, all requested date granularities, bounded playback credit, pause/manual phase overrides, arbitrary-second batching, one-event budgets/coincident phases, cancellation, immutable inputs, corrupt schedules, binary/JSON continuation, preserved annual outcomes and clock-only buffer reuse. The optional civilization fixture is present for this run. The existing `--history-9-only` still passes413 assertions.
- Full solution/worker staging and Windows Unity player builds pass. Native/CoreCLR advancing90061seconds from the same history agree at `68cdbd0f3d8ad85c3b5aeb40e6cbbcae9f99a000bcd9b136bd8785ec7c51d617`; native binary save/reload and another37years agree at `f97d6215abf5aa6c9933770a30eaf2f08047e2907d7ba59b7d9b3c37533a9f29`.
- Browser calendar checks pass exact fractional time, real-time→millennium rate controls, year/month/day/hour/minute labels, no seconds, clock-only updates, bounded actual real-time progress, pause, next-event stepping and a saved checkpoint. The browser-written world continues90061seconds identically in CoreCLR/native at `4e319ef6c29e415b39725122f7d2c2eaf9305e748f7d1f4e0da6d72d7a57e1a1`.
- The full existing historical browser workflow passes five overlays/legends, locality inspection, headlines, ranks, lore export, a37-year request resumed across event budgets, opacity shortcuts and binary saving. Geography-only setup/default/first-event checks also pass. Early browser test attempts raced an outstanding request; explicit busy-state renders and waiting for completed initialization/request work fixed the issue. Failed screenshots remain diagnostic evidence, not passing outputs.
- Native fractional-date HUD screenshots were actually inspected; the year/month/day/hour/minute clock stays in the existing top bar. Browser captures are under `browser/real-time.png`, `fast-time.png` and `next-event.png`; the final native HUD is copied to `artifacts/calendar/clock.png`.
- CLI Preview still verifies every generation-stage hash across repeat/reapply/recipe/cached reconstruction. Source/deployed Core SHA-256 matches `62C91B7639E7782A83E36B1CDBB4B434C44BE67546C9802AA087B4C409C4BBB7`. `git diff --check` passes apart from existing line-ending warnings.

The project shortcut now selects `artifacts/player-calendar-1/WorldGen.exe`; its previous link/player are retained. Existing prehistory4–6 histories can acquire a calendar without changing historical rules. Current processes are still global century/fifty-year/annual integrations; per-state assemblies and daily/hourly tactical armies are not implemented. Queue infrastructure supports actor identities and seeded windows, but future handlers must be written/versioned. Calendar years have365days, no leap days, and rate-input months mean30days. Throughput at1000years/second is a requested rate, not a guaranteed benchmark; processing never skips events. Existing obsolete Earth goldens and maximum-detail historical performance are not newly validated.

## History-9 regional learning and contingent administration — 13 October 2026

Two-hour owner-authorized pass beginning15:27:45UTC. New histories use9; histories1–8 retain their rules. Authoritative final evidence is `D:/WorldGen-history9-20261013/accepted-*`. Earlier `pass*`, `final-*` and `release-*` directories are intermediate experiments, not released compatibility fixtures; do not substitute their hashes or counts for the accepted results. Generation/goldens were not changed by this pass.

- Final `--history-9-only` passes **413 assertions**: regional relay/adoption boundaries, local administration, finite food/reserves, unique persistent sites, complete era progression, annual/irregular replay, cancellation, immutable checkpoints, corrupt-record rejection, serialization and the unproductive-desert control. Presentation passes56. The prior history8 suite passes369; its saved Earth37-year continuation remains `682cbe0507a4ce355d30fe265706f017942e6e0bca72c01b63c30e0bb8438fa4` after the final change.
- Earth20k: first state63925; medieval capstones68850; review ends69600 with156 inhabited named sites and35 active states. Generated200k: first63140, capstones68000, end68750,378 sites/61 active states. Archipelago20k: first62641, capstones67650, end68400,58 sites/11 states. Forest5k: first63580, capstones68300, end69050,27 sites/2 states. Each ends750years after its first culture reaches all three capstones. Dates are fictional elapsed years, not BCE/CE predictions; named sites include villages.
- The200k control records244 total states,183 ended,51 ending within50years and195-year median among ended states,604 campaigns/294 with captures. This demonstrates turnover, not a calibrated conflict frequency or universal expected state lifetime. Exported food951,592,341person-years equals delivered680,300,032 plus transit loss271,292,309. No donor food is duplicated by trade. This is not a complete empirical subsistence/labor model.
- Matched Earth checkpoints at80000 show local uptake rather than obligatory descent: South Asia has3 established farming traditions versus0 in8; Europe18 versus13, and98 named sites versus52; East Asia19 versus18,92 sites versus72. Africa remains strongly advantaged:30 active courts versus46 elsewhere in9, compared with43 versus36 in8. First primary dates move later in Africa/Europe/East Asia but slightly earlier in West Asia. This is mixed evidence, not a uniformly accelerated or solved historical chronology. Region labels are broad diagnostic boxes, never simulation rules; the corrected review separates Arabia from Africa and India from East Asia.
- The origin-sensitivity experiment places humans in Mississippi-region patch598 on the same Earth geography: first state62330 in the Americas, not Africa. This experiment precedes the final existing-culture relay correction and is not a release hash fixture. It supports the narrower diagnosis of arrival/population/knowledge advantage rather than a hard-coded African invention rule.
- Rejected food pass: treating a bumper harvest as permanent carrying capacity kept towns just above sustainable production, repeatedly eroding administrative preparation; Earth formed no states and the focused suite failed. Removing the300-person floor also exposed duplicate towns after abandonment. Final rules separate annual weather from sustainable production/actual imports, retain real reserves and reoccupy one persistent site record. Rejected outputs are retained, not counted as successful checks.
- Full solution/worker staging and Windows Unity player builds pass. Source/deployed Core SHA-256 is `CB0952883789B741C14B29924AC6AED6C7C058E063CB4347208521BE69758BCE`. CLR/native Earth37-year continuation agrees at `97f48b27c03293f6e4d409c0df3f17369aba52b51a02175b8d0074537680a282`; native save/reload and a second37years agree at `cd6a2a02a28ad9b5de57bb72c927bf5034aeadd5b1f6fec4c81b7788b63bfbd3`.
- Final browser workflow passes library loading, five overlays/legends, local inspection, routine-headline filtering, numeric ranks, lore export, irregular37-year manual step,25% opacity keys and save roundtrip. Its written checkpoint continues another37years identically in CLR/native at `3122ca3263dd2d7627b31987b054299c777d1e0d5d517b2f696d095afb044ba6`. Atlas link/search/paging/copy/map-focus/Escape/compact-viewport checks pass, as do geography-only first-century/default-layer checks. Those latter presentation checks preceded the final Core-only relay correction; UI code did not change afterward.
- Native200k37-year continuation also matches CLR at `63960e8b7cbf942d9e6fe19ce9344e25d4d687eabd0e82f0e2800875322a2b29`. The ordinary final Earth records two existing-culture indirect crop adoptions (years62150/68400), exercising the relay path beyond pure helper tests. Later state/religion maps at80000 were inspected and saved beside the native Earth HUD under `artifacts/history-9`.
- Additional long replay: advancing the accepted69600Earth checkpoint directly10400years matches the independent,500-year-sampled fresh audit at80000: `8928e776ee022173a0c2b10880c39b718a5149cb01cd4b9e74146c5d69b9a15e`. An attempted zero-year hash-extraction command was correctly rejected by the positive-year API; comparison instead reads the saved checkpoint's actual integrity field. This was a diagnostic invocation error, not a failed positive-year replay.
- Actual native Earth HUD/atlas screenshots and Earth/200k/archipelago/forest era storyboards were inspected alongside the sampled Ollie Bye reference. Small initial state cores, retained non-state occupation, shared ruling-culture colors with distinct state borders, and later territorial turnover remain visible. Screenshots are not scientific validation. Browser world decoding still takes roughly95seconds for this diagnostic historical save; it is not an instant-loading claim.
- Final CLI Preview verifies identical all-stage hashes across repeat/reapply/recipe/cached reconstruction. `git diff --check` passes apart from existing line-ending warnings. The broader previously obsolete Earth golden corpus and maximum-detail historical performance remain unverified; no goldens were regenerated to pass this work.

The project shortcut targets `artifacts/player-history-9/WorldGen.exe`; the previous link and players8/7 are retained. Start a **new simulation** to use9: old saved histories deliberately retain their versions. Early homeland concentration, generic crop availability, region-level transmission, coarse village catchment forecasts and an unfitted forest-world chronology remain limitations. Farm knowledge does not fabricate ancestry, language/religion, human migration or sovereignty.

## History-8 local formation / exchange — 13 October 2026

New histories use history8. Evidence is under `D:/WorldGen-formation-20261013`; `final-*` directories supersede the intermediate `earth8/forest8/large8` diagnostics. Geography/goldens are unchanged.

- Final `--history-8-only` passes **369 assertions**, including17 formation/exchange boundaries, actual accumulated preparation and invalid saved counters, complete era progression, annual/irregular replay, cancellation, save continuation, conservation and the unproductive-desert control. Historical presentation passes56. Full solution/worker staging and Windows Unity player builds pass; an intermediate rebuild encountered an owned audit's DLL lock and was repeated after the audit finished, not reported as a successful build.
- The saved history7 Earth still yields its exact37-year continuation `c0efa88dc57c712cdcd98d7df6bb21e971047ab4808d75b4bc96ac430eb7a0e3`. Absent/default preparation is not added to older integrity streams.
- Earth20k reaches its first state at63511 versus62908 in the previous release, and ends69400 (750 years after its medieval ceiling) with108 inhabited named sites/19 active states. It records64 introduced crop-adoption events and30 local-domestication events. These are fictional elapsed years and descriptive outcomes, not fits to Earth's chronology. The homeland concentration is **still excessive**; this pass does not claim it eliminated that bias.
- The forest-only control (Aborea seed91,5k; inhabited named sites are rainforest/forest habitats) reaches its first state at63299, versus62520 under history7 on identical geography. The first tropical-rainforest state is63937, proving there is no blanket rainforest veto. It ends69050 with28 named sites/4 active states; this is **not** a demonstrated400k-year delay. Generated continents200k reach a first state63371 and end68950,750 years after the ceiling, with266 named sites/64 active states. Final Earth/forest/generated developmental storyboards were visually inspected. Food exports/deliveries/losses remain conserved in these controls.
- The native history8 Earth37-year continuation matches CLR at `682cbe0507a4ce355d30fe265706f017942e6e0bca72c01b63c30e0bb8438fa4`; native save/reload and subsequent continuation also pass. Source and deployed player Core DLL SHA-256 match `518E6B833269F587092FFDF1591E52F45BC69F6D3C2B63E3CF801A256E8517D5`.
- Browser library loading of the new5k history passes all five overlays, ruling/local selection, legends, headlines, rank values, lore export, irregular37-year manual step,25% opacity controls and saved-world roundtrip. The browser-written checkpoint continues another37 years identically in CLR and native Mono: `4a18e128ed213195a660c91645cf12ccb6559103b92564c9a3cbf165f5f0afda`.
- CLI Preview still matches every generation-stage hash across repeat/reapply/recipe/cached reconstruction. This is not a new claim that the broader obsolete Earth golden corpus or maximum-detail historical playback is verified.

The project shortcut targets `artifacts/player-history-8/WorldGen.exe`; its previous link is retained under `artifacts/history-8/previous-shortcut.lnk`, and the history7 player remains available. Existing saved histories are not upgraded: start a new simulation to select8.

## History-7 political realism and map presentation — 13 October 2026

New histories now select history-7 in the existing prehistory-6 envelope. Geography is unchanged. The available-host evidence is retained at `D:/WorldGen-realism-20261013`; screenshots and binaries are ignored, not source-control artifacts. This is a procedural plausibility review, not archaeological validation.

- The full solution/browser asset staging and Unity player build pass. `--history-7-only` passes **348 assertions**, including era progression through the medieval ceiling plus 750 years, irregular and annual batching, immutable source checkpoints, cancellation, serialized continuation, valid capitals/territory, distinct active court names, crop/faith/disease integrity and physical food conservation. Renaming courts does not alter demographic or political outcomes. `--history-presentation-only` passes 56; `--territory-only` passes 23. The separate held-crisis autonomy scenario passes 10 conservation/linkage/serialization/continuation assertions and creates one supported court, not fabricated inhabitants or levies.
- Saved history-5 Earth and archipelago retain exact 37-year continuations `e6d7fc433b347f6bfc4d8bfcfb7c56d37dc0400b7512379473a56df4e195f328` and `fe5f308eb76f67b0d64289acfb49a733ba103916868d73057996bac59f57af48`. The saved history-6 Earth control retains `11514e0c356cbef2d4c3f14928290041712f0495dcbfa5485f4d25bacfbb6400`.
- Browser testing loads the new 5k history through the world library, exercises all five overlays, legends and selection, opacity controls, rank values, complete lore export, an irregular 37-year step and a saved-world round trip. The browser-written continuation then matches CLR and native Windows Mono for another 37 years: `407d79bae531175ec611af869bd7ce229ab235105bfda9802a5f161586917112`.
- The final `history-defaults-browser-check.mjs` passes geography-only Earth loading, Climate/States defaults, zero background and full political opacity, 25-point opacity shortcuts, and starting the first 100-year step. The final atlas workflow also passes without changing historical state.
- Visible native tests inspect the default white/blue map, state/culture/religion/technology views, ranking values, full chronicle, folded panels and linked atlas articles. No GUI exceptions remain. The 20k Earth UI pass retains checkpoint `76d7449508aa4b91105ab5d1ed623890d4f9402fbfef0a9c9fcd7b7e5776a90e`. Tone-mapping was identified from actual screenshots and is bypassed only in simulation. Hidden-window HUD captures were not used as visual evidence.
- The reference video was sampled at 15-second intervals (76 frames), with four contact sheets inspected alongside developmental-era simulation maps. Earth20k, generated continents200k and archipelago20k pass through the medieval ceiling plus 750 years. Dated maps and area-transition CSVs distinguish reversible administration from cultural identity. A 200,000-year desert negative control retains sparse inhabitants but produces zero towns and states. The excessively fragmented first autonomy experiment was rejected; the refined version retains viable regional coalitions and records unsuccessful movements without spawning states.
- A fresh Earth replay using `--sample-years 75 --map-interval 75` produces the same final checkpoint as the ordinary 50-year audit: `76d7449508aa4b91105ab5d1ed623890d4f9402fbfef0a9c9fcd7b7e5776a90e`. Finer observations and their read-only maps do not affect the simulation. The intervals correspond to 15 seconds at the default five annual ticks per second after state formation.
- CLI Preview verification passes repeat, reapply, serialized recipe and cached reconstruction with all generation-stage hashes identical. Existing Earth golden-fixture mismatches are not resolved or silently regenerated by this pass. The inherited 2.56M geometry tests are earlier evidence; no new full 2.56M historical simulation or sustained 20-tick/s performance claim is made.

Remaining limitations and research interpretation are documented in `HISTORY_SIMULATION.md`, including first-origin concentration, generic crops, regional foragers and absent pastoral/cavalry and mixed-faith systems. Existing saved histories are never silently upgraded. Start a new simulation to use the new rules.

## Read-only world atlas — 12 October 2026

Scope: native/browser UI, read-only profile/diagram/timeline adapters, and UI diagnostics. No Core source or generation/simulation algorithm changed. Shared Core SHA-256 before and after this pass is `E3F8813C0A0B00D1091D31D415EA905741027D614EB673081293AD3D390F7441`; the native package's Core DLL matches it. Production remains history-5.

- `--history-presentation-only` passes 56 assertions: linked profiles, narrative source values, empty/invalid selections, archive paging and optional routine records, related event actors, copied article limits, geography lookup, markup escaping, DTO serialization, and unchanged checkpoint/geography hashes. Existing map mesh ownership checks remain included.
- The saved 5k UI control yields 481 sampled profiles and 15,821 verified profile/event cross-links, without changing checkpoint `01e00eacceb81757a8e49262012becd9e31b97e603593c34d817d43ed97660ee`. The separate 200k history-6 diagnostic control remains readable; it is not a release/default promotion.
- Legacy history-5 Earth continuation for 37 years remains `e6d7fc433b347f6bfc4d8bfcfb7c56d37dc0400b7512379473a56df4e195f328`. CLI Preview fresh/reapply/serialized-recipe/cached reconstruction still matches every stage hash.
- The 200k-cell mesh-only display regression passes 1,203,713 assertions with map area `8.00000000`. This checks geometry/projection/ownership, not a new GPU frame-rate or complete 2.56M historical simulation claim.
- Native Windows player compilation succeeds. Real visible-window captures of world/state/city/people/religion/ruler articles, diplomatic diagrams, population comparisons, paged linked history, and folded HUD were inspected under `D:/WorldGen-ui-20261012/native-final`. The capture run recomputes and verifies the unchanged historical checkpoint. Hidden-window full-HUD captures are black on this host and are not counted as visual evidence; offscreen map captures remain separate.

- Final browser atlas workflow passes five linked profile types, environmental context, population comparisons, diplomacy, name search, back navigation, directory/history paging, optional routine records, copy-payload wiring, keyboard focus, map focusing/selection, Escape, and 1024×768 bounds. The full campaign lore string is identical before and after browsing. Screenshots under `D:/WorldGen-ui-20261012/browser-final` were inspected. The clipboard API is stubbed only in the test, so this check does not overwrite the user's OS clipboard or claim validation of every browser's clipboard permissions.
- Final native package at `artifacts/player-atlas-ui-20261012` passes its saved-world atlas/HUD capture run and exact legacy continuation. Its Core DLL matches the unchanged shared DLL; its Presentation DLL matches shared SHA-256 `DF37594251137F970A79D1693F0433943320CD25B4A16071E71BF3F7A3DA9FE2`. Final release captures are under `D:/WorldGen-ui-20261012/native-release`; earlier `native-final` images show the same reader before the final political-lineage and food-comparison additions.
- The 200k history-6 read-only fixture yields 1,012 sampled profiles and 46,673 checked profile/event links, preserving checkpoint `246b625b9d6b0cf13bd15a87a2b175454b628e55fd7b39dedc7f89673a24f93c`.
- The broader browser library workflow also passes all five historical overlays, corresponding legends/selections, complete chronicle export, a manually chosen 37-year step, restoration of automatic stepping, population/area ranking values, and both opacity keyboard controls in 25-point increments. Its saved continuation reopens on CoreCLR and native Mono with identical subsequent 37-year hash `b0ee0e1847350ef54034e13b34b76a945a333f11696b0c74f5371336e4353fbb`. Evidence: `D:/WorldGen-ui-20261012/workflow-regression`. Cold 5k saved-world completion took about 93 seconds and initial presentation about 3 additional seconds; that existing decoding/integrity cost is not claimed fixed by this UI pass.
- Maximum-detail mesh-only checks pass 15,381,547 assertions at 2.56M cells: mesh 10.56 seconds, dual geometry 12.65 seconds, projected area `8.00000008`. This preserves the cached projection contract; it does not establish complete maximum-detail historical playback or GPU memory limits.
- The final native 1024×768 capture/recomputed-checkpoint run also passes (`native-compact-final`). Long table-of-contents captions are shortened only in the native tabs, with full section titles retained in the article. The inspected state/diplomacy captures no longer clip the long dependent-court caption. Native navigation, filter, and paging changes restart the IMGUI event layout rather than consuming a previous page's control geometry.

Screenshots, test worlds, generated workers, and binary packages remain outside source control. Earlier corpus golden mismatches and unrelated dirty-worktree edits are not changed to make this UI pass green.

The root `WorldGen.exe - Shortcut.lnk` now targets `artifacts/player-atlas-ui-20261012/WorldGen.exe`, with the project root as working directory. The previous shortcut is retained at `artifacts/atlas-ui-20261012/previous-shortcut.lnk`; the previous history-5 player and user worlds are preserved. Existing taskbar pins are not assumed to follow the edited shortcut.

## Institutional/political pass — history-5, 8 October 2026

New simulations use history-5/prehistory-6; geography remains worldgen-3.32. Mechanisms, independent sources, fixed game priors and limitations are in HISTORY_SIMULATION.md and ALGORITHMS.md. No generator goldens were changed in this pass. The dirty worktree contains earlier unrelated golden edits; they are not new expectations for these tests.

Accepted-model controls reuse the preceding saved geography and automatically selected homelands:

| World | Cells | First state | Medieval ceiling | End | Cultures | Towns >=100 | Active states |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Earth | 20,000 | 62,908 | 69,550 | 70,300 | 51 | 101 | 13 |
| Continents Plus, seed73421 | 200,000 | 62,504 | 68,750 | 69,500 | 131 | 267 | 23 |
| Archipelago, seed42 | 20,000 | 61,753 | 68,250 | 69,000 | 14 | 33 | 3 |
| Boreal | 20,000 | 67,748 | 73,150 | 74,150 | 21 | 34 | 2 |
| Earth, separate detail control | 80,000 | 62,002 | 68,750 | 69,500 | 87 | 190 | 17 |

Dates are fictional elapsed years, not BCE/CE fits. The first three and 80k controls end750years after the first complete capstone culture; Boreal uses1000. The oceanless desert remains at zero institutions/towns/states after200,000years, with1,306 surviving foragers. Evidence is at `D:/WorldGen-validation-20261008/accepted-*`, used because C: has little free space. `earth-pass1/2/3`, `*-pass2`, `*-final`, `release-*`, `final5-*` and `contacts-*` are separately retained **intermediate** experiments, not published compatibility fixtures.

The three main endpoints record23/77/4 states in total and10/54/1 ended states. The large control records122 campaigns,46 secessions, nine leaders reaching renown250, and captures of up to nine settlements in one campaign. Twenty-five one-time settlements retain independent courts without annexation. Earth records six such settlements; the archipelago one. These are descriptive procedural outcomes, not historical calibration targets. Named settlement residents comprise19.7%/34.3%/20.6% of people, including villages; these are not archaeological urbanization rates. Coarse dry-without-water, short-growing-season and acute-food-stress checks at the main endpoints report no flagged sites, but do not validate all catchments or cities.

The four-era state/climate storyboards were actually inspected for Earth20k, generated continents200k, archipelago, Boreal and Earth80k. Early states stay concentrated around settlements; large unurbanized areas persist. The initial homeland still dominates, and Earth remains too concentrated in sub-Saharan regions. An alternative Nile homeland on the **same80k geography** (patch1101) ends at70,300 with121cultures/205towns/16states, but its first state is still elsewhere at64,273. It changes the spread of institutions toward Europe/Asia without reproducing Egypt/Fertile Crescent timing. No Egypt coordinate bonus or blanket rainforest prohibition was added. This is evidence of a remaining provisioning/domestication/diffusion weakness, not a reason to claim the model matches Earth.

Rejected/intermediate observations informed corrections: fixed formation chance ignored context; city-market reach restricted political expansion; city relay growth made large empires excessively durable; cumulative succession penalties caused hundreds of almost automatic crises; towns could disagree with their political map owner; capital-only contact missed provincial interaction; geometric nearest-target selection could stall despite another reachable target. The accepted model corrects these mechanisms without rewriting geography. See state-foundations.csv, review.txt, candidate-sites.csv, era maps and the full archive for investigation. Typical labels and scalar thresholds remain game priors.

Focused tests pass **228 historical assertions**, including all available retained history1–4 continuation fixtures, annual/irregular replay, serialization, cancellation/immutability, institutional/leadership effects, physical contact and cache behavior, ownership alignment, every public new-record field participating in integrity hashing, and an isolated tribute victory retaining its defeated court. The latter is deliberately configured to test the mechanism, not an empirical scenario or screenshot. Additional coastal14, canonical hash27 and historical presentation20 assertions pass. Shared build succeeds without warnings. CLI Preview verifies fresh/repeat/reapply/recipe/cached reconstruction with all stage hashes matching. The full generator suite's previously documented obsolete Earth golden has not been fixed or reported as passing.

The final native200k continuation of137years matches CoreCLR: `d561747244aa04887d5ee890c2e68d4897a5245857b64e7b642264047c4f6486`. Native save/reload and independent .NET continuation of the native save by37 more years agree: `cb8c446767b6fd54416d52a02c74617785b649427cdf910ca8a7563acac26fe5`. Annual native stepping at that checkpoint measured132.1ms median/196.9ms p95;20ticks/s cannot be promised at this size. These are available-host timings, not cross-platform guarantees.

The browser workflow passes loading via the world library, all five overlays and linked inspection, correct numeric population/area ranking, hidden abstract score, filtered headlines, expanded campaign export, manual/Auto year steps, and25% opacity shortcuts. A browser-written save continued37years agrees with uninterrupted .NET74years: `b0ee0e1847350ef54034e13b34b76a945a333f11696b0c74f5371336e4353fbb`. Cold5k snapshot loading remains slow: approximately60seconds decoding and29seconds integrity checking before initial view completion around96seconds. No improvement to that serializer is claimed. Screenshots and timings are under `browser-final`.

Native200k interaction passes generation, cancellation32.63ms, resizing, north-up, all cached display toggles0.804ms together, and globe/map switching0.134ms per switch, with maximum busy-frame interval33.33ms. The initial concurrent fullscreen check failed **only its resize assertion**; its switches/cancellation passed. A separate explicit-windowed run passes every assertion. This distinction is retained in native-interactions.json versus native-interactions-solo.json. Native HUD capture succeeded on this host during this pass; screenshot review exposed clipped ranking digits and timer text, leading to a final presentation-only layout correction. Generated artifacts/screenshots remain outside source control.

Final hidden/windowed HUD captures are black, despite successful camera-map captures and the native view check exiting0. They do **not** visually verify the last native layout correction. Earlier native HUDs and final browser ranking/overlay screenshots were reviewed; the corrected native layout compiles, and the interaction test passes, but its final appearance needs an ordinary interactive confirmation. No hidden black screenshot is counted as evidence of a good UI.

Additional5k-cell seed31415 sensitivity controls complete ceiling+750: Archipelago ends72,550 with3cultures/5towns/2states; Continents Plus ends69,750 with32/105/13; Shuffle ends73,400 with10/27/3; Lakes ends68,300 with266/466/57. Wet oceanless scenarios still produce much broader institutional development. These are scenario sensitivity outcomes, not calibrated targets or proof of resolution invariance.

The root shortcut now targets `artifacts/player-history-5-release/WorldGen.exe`. Its previous history-4 shortcut is preserved at `artifacts/history-5/previous-shortcut-20261008.lnk`; the previous executable remains available. Core/Presentation/Serialization DLL SHA-256 digests match the shared build. No running user application was closed, and existing histories were not migrated. The main-menu footer identifies history-5 so stale pinned executables can be distinguished.

The audit wrapper itself passes a fresh5k Earth/Continents Plus seed17/Archipelago seed42 run with PNG/SVG snapshots and exact750-year tails, using `-OutputRoot D:/WorldGen-validation-20261008/wrapper-final`. Absolute/relative output-root resolution and PowerShell syntax were checked; each run creates its own folder. This confirms the documented review workflow, not only ad hoc saved-geography comparisons.

## Final evening pass — history-4, 4 October 2026

New simulations use history-4 / prehistory-6 with unchanged worldgen-3.32 geography. This narrow pass adds bounded low-density expansion, sublinear population exposure for eligible regional institutions, and peaceful informal cross-border provisioning with transport friction. Earlier history versions retain exact continuation. Sources, coefficients and limitations are in `HISTORY_SIMULATION.md`; no generator golden was regenerated.

Final-model controls reuse exactly the preceding saved geography and founding locations:

| World | Cells | First state | Medieval ceiling | End year | Cultures | Towns | Active states |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Earth | 20,000 | 62,511 | 69,550 | 70,300 | 51 | 101 | 43 |
| Continents Plus, seed 73421 | 200,000 | 62,395 | 68,750 | 69,500 | 131 | 267 | 71 |
| Archipelago, seed 42 | 20,000 | 60,990 | 68,250 | 69,000 | 14 | 33 | 12 |

Evidence is under `artifacts/history-4/final-*`; elapsed dates are fictional, not BCE/CE fits. Growth-only and growth-plus-exposure intermediate passes remain separately labeled `earth`, `large`, `archipelago` and `exposure-*`. Those development checkpoints are not a published compatibility contract. Final era storyboards are inspected at cultivation, first state, iron and ceiling+750. Large unurbanized areas persist. East Asia is populated sooner and develops localized institutions; India and the first-homeland bias remain weak points. No region/ancestry development coefficients were added. The cold control uses a 1,000-year tail; the oceanless desert retains no institutions, towns or states after 200,000 years.

The annual town-trace diagnostic investigated one food-stressed settlement. Its pre-correction state formation had abruptly cut access to neighboring food-producing cells. From the same experimental checkpoint, the revised market restores capacity at the next decadal update (1,483 to 14,566 people supported), rather than requiring a later formal trade link. It retains war blockades and transport limits; this is not an explicit commodity economy. `food-stress-trace.csv` and `food-stress-corrected-trace.csv` preserve the comparison.

Focused tests pass 139 historical assertions, including retained history-1/2/3 continuation on this host, irregular/annual replay, save/reload, cancellation and immutability, plus the new bounded response/exposure and market-access tests. Earlier legacy fixture availability remains conditional as documented below. Shared build passes without warnings. The full generator suite's previously documented obsolete Earth golden is not fixed or reported as passing.

Final coastal (14), historical presentation (15) and hash/provider (27) assertions pass too. Native 200k continuation of 137 years matches CoreCLR: `3fff5799b12043ea98da669ca1325dcd991e310117a955dd1a504fc8d8b85cb3`. Native save/reload and independent .NET continuation of that save by another 37 years agree: `d171441f58343d107c9788cb655aa89cbeaa95fa7475bb0891509568df9aa02e`. Final browser library loading, all five overlays/legends, lore export, irregular timestep, Auto and both 25% opacity shortcuts pass. The browser-written save matches the uninterrupted 74-year .NET reference after a further 37 years: `7b370acbca8a9e0326c8a9683fcb2d461f380df1e3a20e707028868302d67c47`.

The 200k native endpoint benchmark measured 124.4 ms median / 213.2 ms p95 per annual tick under concurrent validation load, with 21,746 archived events and 267 towns. This is more historical work than the preceding 4,885-event benchmark and is not a controlled speed comparison. Final native interaction at 200,000 cells passes completed generation, cancellation (30.32 ms), resize, north-up, cached switches (1.029 ms together) and map/globe projection (0.140 ms). Maximum busy-frame interval is 26.28 ms; these are CPU/interaction measurements, not sustained GPU FPS. Camera map captures were reviewed; hidden HUD screenshots remain black and are not counted as native UI visual evidence.

Five additional final-model 5k controls complete ceiling+750: Archipelago 31415 ends at 72,550 with 3 cultures / 5 towns / 3 states; Boreal 0 at 82,250 with 26 / 43 / 7; Continents Plus 31415 at 69,750 with 32 / 105 / 39; Lakes 31415 at 68,300 with 266 / 466 / 198; Shuffle 31415 at 73,400 with 10 / 27 / 12. The formerly stalled archipelago now develops locally after greater demographic establishment; it is not still a no-state control. The desert remains the negative control. These counts are sensitivity outcomes, not calibrated targets. The final 20k Boreal control ends at 74,150, exactly 1,000 years after its cap, with 21 cultures / 34 towns / 7 states.

Both food-stressed towns at the final 200k endpoint recover provisioning in separate 100-year traces (`final-town-239.csv`, `final-town-241.csv`). Temporary wartime/border/market disruptions still occur; no claim is made that every food budget is realistic. Browser cold loading remains slow: approximately 53 seconds decoding, 31 seconds integrity checking and 3 seconds initial presentation in the final 5k test. The first-homeland advantage, India, sparse sea-route networks and local settlement placement remain unresolved limitations.

The root shortcut now targets `artifacts/player-history-4-release/WorldGen.exe`; its prior shortcut is preserved at `artifacts/history-4/previous-shortcut.lnk`. Shared Core, Presentation and Serialization DLL hashes match the final player. Existing saved histories retain their model version; a new simulation is needed for history-4. No running user application was closed.

The final read-only political review reports 65 recorded / 22 ended states on Earth, 166 / 95 on the 200k continents, and 32 / 20 on the archipelago. Median lifetimes among ended states are 514 / 473 / 860 years respectively, excluding survivors; states ended before 50 years number 2 / 7 / 3. Named settlement residents comprise 19.0% / 31.2% / 19.7% of the populations. These include villages and must not be presented as archaeological urbanization rates. These descriptive quantities now appear in `review.txt`, alongside conflict exposure and religious-tradition counts, for future calibration rather than as automatic pass/fail expectations.

An additional freshly generated 80k-cell Earth control (seed 73421, automatically selected origin patch 418) reaches its first state at 61,976, medieval capstones at 68,750 and stops at 69,500: 12,241,811 people, 88 cultures, 190 towns and 63 active states. Its four-era storyboard was inspected. It shows local East Asian development but continued excess concentration near the initial homeland; India remains underdeveloped. No coarse review flags for waterless dry sites, short-season sites or acute food stress occur at this endpoint. Different detail changes climate/ecological partitions, available sites and the selected homeland; these are not identical-geography resolution-invariance tests. Evidence: `artifacts/history-4/final-earth80`.

## Era-aware historical iteration — 4 October 2026, second session

New simulations use **history-3 / prehistory-6**, retaining worldgen-3.32 geography and exact history-1/history-2 continuation. This pass adds regional crop-system availability/adoption, supported specialist practices, habitat-weighted rural allocation, crop-transfer frontiers, sailing across submerged shelves, and practical knowledge carried by successful founding groups. See `HISTORY_SIMULATION.md` for sources, game priors and rejected experiments. No golden files were regenerated.

The repeatable audit now ends **750 years after one culture attains steel, medieval navigation and scholastic learning**, rather than a fixed distant year. The configurable tail is 500–1000 years, bounded by a reported safety limit. Each run writes exact milestone dates, sampled state/religion/technology maps, an era storyboard, candidate-site and settlement diagnostics, a review report, campaign gazetteer and resumable world. The 5k Earth / Continents Plus / Archipelago workflow, including screenshots, passes end to end. The 200k storyboard renderer now rasterizes one SVG at a time to avoid multiplying browser geometry memory.

Final main controls, including the founding-knowledge correction:

| World | Cells | First state | Medieval ceiling | End year | Cultures | Towns ≥100 | Active states |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Earth | 20,000 | 66,101 | 72,150 | 72,900 | 43 | 67 | 22 |
| Continents Plus, seed 73421 | 200,000 | 65,960 | 72,500 | 73,250 | 74 | 110 | 36 |
| Archipelago, seed 42 | 20,000 | 65,216 | 71,200 | 71,950 | 17 | 30 | 9 |

Elapsed years are fictional model dates, not BCE/CE reconstructions. Evidence: `artifacts/history-3/founders-earth`, `founders-large`, `founders-archipelago`. All three retain the same state/town counts as the immediately preceding pass; knowledge inheritance changes some demographic totals, not automatic institution adoption. Reviewed era maps show local city-centered states, distinct technological uptake and large non-state areas. Earth still concentrates development too strongly near the initial homeland; India and East Asia are underdeveloped in the default-origin run. Alternative-origin controls show both arrival-time and habitat/storage-proxy effects. No geographic or ancestry development bonus was added.

Additional pre-knowledge-transfer controls: Pangaea seed 17 reaches year 73,850 with 71 cultures / 146 towns / 46 states; Inland Sea seed 42 reaches 70,750 with 53 / 123 / 34; Boreal seed 17 reaches 76,850 with 20 / 31 / 5. Each is 750 years after its ceiling. Pangaea's dry interior remains largely non-state; Inland Sea institutions cluster along productive coastline; Boreal towns concentrate in milder habitats. Oceanless Desert seed 17 reaches 200,000 years with 1,271 people and no cultures, towns or states, explicitly reporting that the technology ceiling was not reached. These are diagnostic controls, not expected historical counts.

The new read-only review finds no inhabited town in the three main controls simultaneously having aridity below 0.2, negligible modeled local river access and no coast. The two desert-biome towns in the 200k run have modeled water access. These filters are investigation prompts, not a claim that all sites or food yields are realistic. `review.txt` also lists cold-site/food-stress flags, technology distributions and empty sea destinations for manual inspection; `settlement-review.csv` retains the underlying quantities.

Regression checks pass **132 history**, **14 coastal**, **15 historical presentation** and **27 canonical hash/provider** assertions. Coverage includes exact old-version continuation, irregular/annual batching, save/reload, cancellation and input immutability, crop climate/storage constraints and source integrity, sustainable specialist requirements, compact archive integrity for every event field, exact weighted rural totals, ceiling/tail detection, founding knowledge transfer, language-reference bounds, empty loading selections and accurate generic cultivation labels. CLI Preview all-stage fresh/repeat/reapply/serialized-recipe/cached verification passes. The full generator suite still fails the previously documented obsolete Earth golden at `Program.cs:278`; it is not reported as passing.

Final native 200k history-3 playback measures **53.7 ms median / 65.5 ms p95 per annual tick** on this host at the era endpoint (4,885 events). This is not directly comparable with earlier larger-archive benchmarks and is not a rendered FPS claim. Native and CoreCLR reproduce the same 137-year continuation hash: `dffc6aa1047641c4e4cb67cade0d639a64d921d7cd1368764fdbc0b6fd8e84de`. The full archive is still verified at every checkpoint; indefinitely long campaigns remain progressively more expensive.

The native 200k interaction check passes complete generation, cancellation (32.23 ms), resize, north-up, cached main-layer switches (0.697 ms together), and stable map/globe projection (0.122 ms). Maximum busy-frame interval was 120.25 ms under concurrent audit load; this is not a sustained frame-rate guarantee. Native camera map images were inspected; hidden-window HUD captures remain black and are not counted as UI visual evidence. Browser checks cover all five overlays/legends, linked inspection, the expanded settlement/religion campaign export, manual 37-year stepping, automatic-step restoration and 25% opacity shortcuts/clamps. Full browser snapshot deserialization still takes minutes and remains an explicit performance limitation.

The preceding history-3 pass additionally ran ten 5k seed/preset controls: Continents Plus, Archipelago, Boreal and Lakes at seeds 0/31415, plus Desert and Shuffle at 31415. Eight completed ceiling+750; the isolated archipelago retained two small settlements without a state through year 200,000, and the desert remained nonurban. Boreal state/ceiling dates were later, while the two wet oceanless worlds supported more towns and states. Cold, wet and stalled-archipelago storyboards were inspected. No invalid checkpoints occurred; evidence is under `artifacts/history-3/seed-matrix`. These are sensitivity controls, not resolution-independent historical targets.

Native history-3 save validation: `--worldgen-history-save` writes the advanced checkpoint, reloads it and checks another 37 years against uninterrupted continuation. The native-written 200k world also reloads on CoreCLR with the identical continuation hash `c9af56d6bc9693d5ff9471ac462543be86d4401b5fe8c051c05581f6664b3447`. The browser's actual library “Load last save” route passes all five overlay/inspection/export/timestep/opacity checks; a cold 5k run measured roughly 53 seconds decoding, 32 seconds integrity checking and 2–3 seconds initial presentation. This local timing isolates the remaining saved-world loading cost from cached map/globe toggling.

Release packaging: the shared solution and `player-history-3-release` build pass with no warnings. Core and Presentation DLL hashes match the tested shared binaries. The root `WorldGen.exe - Shortcut.lnk` now targets that player; its previous target is preserved in `artifacts/history-3/previous-shortcut.lnk`. Existing saved histories keep their original model version; new worlds select history-3. Pinned taskbar shortcuts can still point to an older player.

The browser also writes its advanced checkpoint through the actual library adapter. Loading that file on CoreCLR and advancing another 37 years matches an uninterrupted 74-year continuation from the original checkpoint: `34bf97000a835305de4b6a5b9165f8cdc9f1102704e2b0798657d1fa2aed18fe`. This tests browser serialization and historical continuation, not merely successful rendering of a .NET-produced save.

The 1,000-year tail option is exercised separately: resumed default Earth stops at 73,150 (ceiling 72,150); alternative South American origin patch 195 ends at 74,200 (ceiling 73,200), with 24 cultures / 64 towns / 21 states; North American patch 600 ends at 72,450 (ceiling 71,450), with 16 / 42 / 16. These placement controls change only the initial homeland, not geography-specific model coefficients.

The final native interaction run used **204,000 cells**, reflecting the resolved UI settings: 1,215 chunks, 3,142 responsive busy frames, 33.26 ms maximum busy-frame interval, cancellation 32.02 ms, cached switches 0.662 ms together and map/globe toggles 0.107 ms. Complete/cancel/resize/north-up/instant-display/stable-projection flags all pass. Evidence: `artifacts/history-3/release-ui-check.json`. This remains an interaction/CPU test, not a sustained GPU FPS measurement.

The exact history-1/history-2 continuation checks ran against retained local artifacts. Those fixtures are ignored generated files, and the tests conditionally skip them when absent; a clean checkout therefore does not independently reproduce that compatibility evidence without the fixtures. The assertion counts above describe this host, not an unconditional clean-checkout count.

## Two-hour historical iteration — 4 October 2026

New simulations: `prehistory-6` / `history-2`, with unchanged worldgen-3.32 geography. Iteration evidence and rejected passes are under `artifacts/history-2`; `HISTORY_SIMULATION.md` records researched mechanisms, game priors and remaining limits. The audit workflow is reproducible with `scripts/history-check.ps1`; its three-world 5k/10k-year screenshot smoke pass succeeds. No golden expectations were regenerated.

Final-model histories reviewed at multiple dates and against climate/river/biome context:

| World | Cells | End year | Regional cultures | Towns ≥100 people | Active states |
| --- | ---: | ---: | ---: | ---: | ---: |
| Earth, current bundled relief/climate | 20,000 | 120,000 | 42 | 448 | 66 |
| Archipelago, seed 42 | 20,000 | 140,000 | 9 | 211 | 24 |
| Continents Plus, seed 73421 | 200,000 | 100,000 | 56 | 371 | 71 |

These are diagnostic model outputs, not target archaeological counts. The earlier fixed-per-cell food allowance produced severe resolution sensitivity. Physical catchments and riparian site/area separation improve that behavior but do not eliminate the coarse-cell limit on city locations. The Earth run still has a substantial first-populated-region advantage. Native 200k state/religion/culture camera captures and same-seed diagnostic maps were inspected; categories remain crisp. Hidden-window full-screen HUD captures are black and are **not** native UI visual evidence. Browser captures validate the HUD instead.

Focused tests: **68 history assertions**, including exact history-1 continuation, immutable inputs/geography, annual/irregular step parity, save reconstruction, cancellation without source mutation, local urban stages, capital ownership, island colors/marker locations, religion population accounting, current territory legend selection, medieval technology/prerequisites, lifetime event IDs and post-collapse calendar behavior, and the oceanless-desert negative control. **12 presentation**, **7 coastal-detour**, and **27 canonical hash/provider** assertions pass. The provider tests cover integer batch boundaries, independent event serialization with nulls/Unicode, empty/block-boundary/large buffers, segmented offsets and reused Windows SHA-256 instances. Old prehistory-5 Earth replays exactly to `4870ef5b2270aaa38166940ae0afd0b23ff31209c3b930fa86e83cc3e01dedb6`.

Browser historical checks pass loaded history, all five overlays, selection/local chronicle, export, manual 37-year advance, automatic-step restoration and 25-percentage-point keyboard controls including clamps. The native 200k history-2 checkpoint also resumed with the same 37-year hash as CoreCLR. CLI Preview repeat/reapply/serialized-recipe/cached reconstruction matches every generation stage. The full generator suite reaches but fails the Earth golden comparison: the checked-in Earth expectation is worldgen-3.22 with 11 stages, whereas the current generator has later ecology/subcell stages. This failure predates the hash optimizations; expectations were deliberately left untouched.

Performance investigation: on CoreCLR, the representative 200k annual checkpoint fell from roughly 171 ms to 75 ms after byte-identical integer/event batching. Windows Mono's standard SHA-256/CNG/CSP facades all measured ~150 ms per 16 MiB buffer; direct system CNG measured ~11 ms with the identical digest. A Windows-Mono-only provider adapter uses the documented CNG lifecycle with safe handles; all other hosts retain the BCL backend, with fallback when CNG is unavailable. This changes neither canonical bytes nor digest format. The final native 200k checkpoint measures **191.2 ms median / 234.4 ms p95 per annual tick**, versus roughly 1,006 ms before optimization. This is a simulation benchmark, not a rendered frame-rate claim. Its 137-year continuation matches CoreCLR: `5edefeaebc5ddb629dcead59ef24ec2032c3117a8d254e68a9dcf08143d8aefd`.

The final Earth continuation reaches **200,000 years**, with 29,495,150 people, 63 regional cultures, 972 towns of at least 100 inhabitants, 179 active states and 1,467,642 lifetime events. The 61 MB world file reloads and advances 37 years to the same `b2446a2be12084781aa5c0b5351b9cf4d105bf9a2f74059ad34ebe524df9cc05` hash as the uninterrupted audit. Annual diplomatic/settlement lookup indexing preserves the exact 2,000-year continuation hash and all 38 overlapping 1,000-year summaries against the unoptimized implementation; the optimized 120k–200k continuation takes 211.9 seconds including audit output on this host. Later Earth state/religion maps were inspected: India remains underdeveloped and first-mover effects remain excessive. These are documented weaknesses, not claims of archaeological accuracy.

Full projection/ownership checks pass **1,203,713 assertions at 200k cells** and **15,381,547 at 2.56M cells**. The latter takes 11.45 seconds for the mesh and 14.95 seconds for the cached dual surface, with map area 8.00000008 against the expected 8. These are mesh-only checks; maximum-detail history generation, GPU memory and interactive playback are not established. Final browser checks pass all five overlays, legends, locality inspection, export, manual/automatic steps and both opacity controls. Final native/browser captures are under `artifacts/history-2/release-earth-native` and `artifacts/history-2/browser`; the long-run diagnostic images are under `earth-longrun-fast`. Native chronicle export now captures an immutable checkpoint and writes in the background rather than blocking the UI.

Deployment: the final shared build and Unity `player-history-2-release` build pass; player Core/Presentation DLL SHA-256 values match the tested shared binaries. The native player also reloads the 200,000-year / 1.47-million-event Earth archive and reproduces its 37-year continuation hash. Root `WorldGen.exe - Shortcut.lnk` targets this release, with the previous shortcut backed up at `artifacts/history-2/previous-shortcut.lnk`. No running user application was closed. New simulations select history-2; existing saved histories retain their original rules. Pinned taskbar shortcuts may still target an older release.

Additional controls: a 20k-cell oceanless Desert world (seed 17) reaches year 200,000 with 1,271 people across nine patches and **no regional institutions, towns or states**. Elapsed time alone does not force urbanization. The long-lived Earth archive exposes a remaining performance limit: with 1.47 million events, CoreCLR annual ticks measure 1,008.9 ms median / 1,070.5 ms p95, despite only 20k cells; overlay recoloring takes 5.7 ms. The full archive still participates in every checkpoint hash. Chunked/persistent archive integrity is future work, not a reason to skip verification.

Contrasting cold-world control: 20k-cell Boreal seed 17 reaches year 160,000 with 7,675,701 people, 22 regional cultures, 246 towns and 33 active states. Reviewed maps show urbanization concentrated in the milder coastal belt rather than the extensive frozen interior; town sites include boreal/temperate forests, wetlands and four tundra cells. This demonstrates that cold is a constraint rather than a categorical institution ban, not that individual town yields are scientifically calibrated. Artifacts: `artifacts/history-2/boreal-control`.

Final native interaction check at 200k cells passes: 1,212 mesh chunks; 2,690 responsive busy frames; maximum busy-frame interval 34.727 ms; presentation cancellation 32.3729 ms; all cached main-layer/grid/river/wire switches together 0.6029 ms; map/globe toggles average 0.10001 ms without replacing the root geometry. Resize, north-up orientation, complete generation, immediate display switching and stable projection flags are all true. Evidence: `artifacts/history-2/final-ui-check.json`. This is a native interaction/CPU check, not a full-screen HUD visual capture or sustained GPU frame-rate measurement.

## Historical simulation — 4 October 2026

`prehistory-6` / `history-1`, with unchanged generated geography. See [HISTORY_SIMULATION.md](HISTORY_SIMULATION.md) for independent research, game-model assumptions and limits. The shared solution and `player-history-1` Unity player build pass. `--history-only` passes 40 assertions on 5k Earth and Continents through 120,000 years: independent version, locally bounded urban stages, irregular/annual batching equivalence, immutable input checkpoints, complete save/reload/continuation, unchanged terrain hashes, all overlay data, linked lifetime events, medieval technology bounds/prerequisites, corrupted-prerequisite rejection, saved-overlay rendering before engine preparation, and development-stage integrity hashing. Earlier development builds were also stress-run to 200k years; those are not calibrated historical dates or final golden outputs.

Final 120k results: Earth 7,950,510 people, 143 urban/state cells versus 263 cultural cells, 494 recorded wars and 15 conquests; generated Continents 6,652,526 people, 144 urban/state cells versus 184 cultural cells, 322 wars and 43 conquests. Both reach the 15-practice medieval ceiling somewhere. These are model outputs, not historically fitted populations or target global fractions. Seven coastal-detour regressions pass. The prehistory-5 native Earth still replays exactly to `4870ef5b2270aaa38166940ae0afd0b23ff31209c3b930fa86e83cc3e01dedb6`.

Unity/Mono loads the final Earth history and advances 37 years to the same hash as CoreCLR: `08ea5f1c27def6cff3ca95d8b7a033b7614a3eeb6f663afbc4cc59f4c6f22319`. The final core schema uses integer local-stage arrays; an early byte-array experiment was removed because the canonical hasher did not encode byte values. A typed historical-event hash writer retains canonical field order and is protected by an explicit record-layout check.

`scripts/history-browser-check.mjs` verifies loaded history, five selectable overlays and matching legend types, local lore inspection, full chronicle download, and a manual 37-year step. Reviewed state, civilization and religion captures under `artifacts/history-1/browser`. The visual check found that a religion's original holy site can convert; legend targeting was corrected to select a current follower cell. Compact 5k cells visibly interpolate overlay colors at boundaries. Browser saved-history loading is slow (minutes in these tests); maximum-detail historical performance, archaeology-level calibration, native HUD appearance and exhaustive campaign balance are not established by these checks. The full archive intentionally grows with elapsed urban history.

Root shortcut targets `artifacts/player-history-1-release/WorldGen.exe`; previous target is backed up in `artifacts/history-1/previous-shortcut.lnk`. Existing histories are not migrated automatically. No golden expectations were changed for this update. The corrected religion legend additionally passes 26 current-follower-cell reference assertions. CLI Preview repeat, reapply, serialized recipe and cached-result reconstructions match at every generation stage.

## Compact societies and coastal detours — 4 October 2026

Shared build and `player-societies-2` Unity build pass. Core prehistory tests pass 90 assertions on Earth/Continents; seven focused coastal tests demonstrate actual expansion around a zero-capacity coastal mountain patch without settling it, no invented inland coastal route, version-4 route preservation, stable civilization colors and bounded captions. The prior prehistory-4 Earth checkpoint still replays to `332454498363d73a0353ec292650cdd9de6cabf5876f5c8c6de3dbbb362a8960`. New histories use prehistory-5; old histories are not automatically migrated.

`scripts/societies-compact-browser-check.mjs` passes: 105px chronicle with at most one event per category, civilization-only compact list and color swatches, selection-based folded caption, both opacity shortcut targets, original step/speed/pause controls, save restoration, event popups, independent layers, projection switching and city markers. Reviewed `artifacts/societies-2/browser/societies.png` and the final native map capture. The small test mesh makes color interpolation visible at cultural edges; local playable coastlines and scientific migration calibration remain out of scope. Native Earth completed 200,000 years without runtime errors. As before, browser screenshots validate browser UI rather than native IMGUI appearance.

The root shortcut now points to `artifacts/player-societies-2/WorldGen.exe`; its prior version is backed up under `artifacts/societies-2/previous-shortcut.lnk`.

## Early societies — 3 October 2026

New checkpoints are `prehistory-4` / `early-societies-1`; geography remains worldgen-3.32.0. Shared solution and Unity player builds succeed. `--prehistory-only` passes 86 assertions on 5k Earth and Continents: phase progression, independent city polities, population accounting, bounded food reserves, exact annual/irregular/century batching, persistent manual timer override, 100,000 additional years of frozen civilization technology, immutability, and save/load continuation. `--presets-only` passes 2,504 assertions including Earthlike and shared UI/Core preset resolution. CLI Preview repeat/reapply/recipe/cache reconstruction matches all stage hashes. No golden expectations were changed in this milestone.

Final native 5k Earth and Continents both reach civilization after 200k simulated years and match independent CoreCLR replay: `332454498363d73a0353ec292650cdd9de6cabf5876f5c8c6de3dbbb362a8960` and `873dcceef07633104207132e744f323f814f745c0d6c4135047d0aa88402523a`. Existing prehistory-3 Earth still replays to its historical `91655b09ede7205145fe804c824e35b690e5cbe5ba1fa68d4e22cfad416d9977` hash. Native map captures and checkpoints are under `artifacts/societies-1/release-*`. Visual review of the initial run prompted distance attenuation and more local culture-emergence opportunities: final 5k Earth has 95 cultures / 345 settlements; Continents has 36 / 129. These are game-model results, not historical fits. City positions are coarse-cell centers, not local-terrain town plans; large-detail simulation performance and archaeological calibration are not established by these smoke checks.

`node scripts/societies-browser-check.mjs` passes snapshot restoration, folded viewport bounds, stage-ranked lists and sorting, lifestyle bars, editable persistent year steps, keyboard speeds/play/pause, Escape menu, chronicle popups, independent climate/society opacity and map/globe changes. Reviewed final browser screenshots: `artifacts/societies-1/browser/folded.png`, `societies.png`, `climate.png`, `cities.png` and `pause.png`. City markers appear on zoom; culture labels suppress overlaps. Browser testing found and fixed anonymous-marker DTO serialization and a start/status callback ordering race. Hidden-player map captures do not constitute native IMGUI screenshot evidence; native UI compilation and runtime checks pass, but its panel layout was not visually captured on this host.

Root shortcut targets `artifacts/player-societies-1/WorldGen.exe`; previous shortcut is preserved in `artifacts/societies-1/previous-shortcut.lnk`. No running user app was closed.

## Prehistory 3 — 3 October 2026

Final native Earth map capture was reviewed after the shader correction: no categorical speckling, coherent occupancy over smooth terrain, water excluded. Root shortcut now targets `artifacts/player-prehistory-3/WorldGen.exe`; previous shortcut is retained in `artifacts/prehistory-3/previous-shortcut.lnk`. Running user applications were not closed.

Shared solution and Unity player builds succeed. Targeted tests pass 62 demographic assertions on 5k Earth/Continents and 161 shore-sampler assertions. Checks include ordered batch/single replay, immutable geography, saved continuation, savanna preference, island-node provenance, bounded routes, shelf-depth opening, no deep-ocean glacial bonus, habitat similarity/crowding effects, variable carrying capacity and population bounds. Existing v2 native Earth replays to its original hash. No goldens were regenerated.

Native 20k Earth and Continents complete 200k years. Independent CoreCLR replay matches hashes `91655b09ede7205145fe804c824e35b690e5cbe5ba1fa68d4e22cfad416d9977` and `dcc0254c910b7261c2ff9bc3a6009408bf592045eeda2188dfcec6e1ba3b4046`. Earth: 788 occupied patches / 3,210,055 people; Continents: 374 / 2,678,690. These are uncalibrated game-model results.

Browser smoke check passes with a 5k Earth snapshot: placement, start, 100-year and configurable 500-year advances, independent opacity controls on Satellite/Climate, bounded sidebar/map viewport, Escape pause/resume with no new time advance while paused. Reviewed screenshots under `artifacts/prehistory-3/browser/`: compact HUD, side-panel climate overlay, satellite-only and pause menu. An earlier 20k browser check reached climate switching before a test-script decimal-format error; the corrected smaller test completed without page errors. Native screenshot review caught and corrected interpolated categorical texture-address speckling: smooth surfaces now interpolate decoded occupancy weights, while signed small-feature display tags prevent lake water receiving land population tint. The hidden-player limitation for capturing native IMGUI itself remains; browser HUD screenshots are not native HUD evidence.

## Prehistory 2 — 3 October 2026

Browser smoke test also passes placement/reroll/move, start, a 100-year advance, 50k-year batch, opacity and cached layer switching without page errors. The redesigned control panel and inspection text were visually reviewed at 1600×1000 (`artifacts/prehistory/browser/year-50000.png`); an explicit light foreground was subsequently added to the status text. Browser cold world import remains slow and is outside this change.

Solution build and native player build succeed. `--prehistory-only` passes 46 assertions across 5k Earth and Continents, including savanna priority across rerolls, default 100-year steps, distance-sensitive crossing probabilities, batch-size determinism, save/resume and legacy 250-year compatibility. The prior native Earth v1 checkpoint still replays exactly to `84ff0e31bfbf48d7245c0acc7d0c7b4707d8359e2aa31d1f843fbf35ff1e5d9c`.

New native 20k Earth and Continents runs complete 200,000 years; both replay exactly in CoreCLR (`db2f90229fc9c1f7d6ff107cba3943249667d6b3bb0ce91126b177f89e7f9770` and `f8953e1dd0f94004fe806237eb6838f66d2b5387fbfe20d30c40ab09564c5cef`). Earth has 567 occupied patches / 2,014,010 people; Continents 358 / 2,679,872. Native map captures are in `artifacts/prehistory-2`; the hidden-window screen capture is black, so it is not evidence of native HUD appearance. No terrain or climate changes, and no golden updates. Root shortcut targets `artifacts/player-prehistory-2/WorldGen.exe`; its previous version is backed up in `artifacts/prehistory-2/previous-shortcut.lnk`.

## Prehistory prototype 1 — 3 October 2026

`--prehistory-only` passes 38 assertions across Earth and Continents at 5,000 cells: setup-only placement, no instant propagation, bounded sea routes, land/ecoregion patches, 250-year versus batched replay, unchanged input checkpoints, 200k-year expansion, bounded populations, neutral drift, separate world/simulation integrity, compact save/load continuation, tamper rejection, origin relocation, sea-level windows and connected lifestyle groups. Generation hashes are unchanged; no goldens were regenerated.

Native 20k Earth and Continents runs reach 200,000 years and save complete worlds. Earth: 579 occupied patches, 2,087,318 people; Continents: 356 patches, 2,678,768 people. Loading each native checkpoint and independently replaying from its origin under CoreCLR matches exactly: Earth `84ff0e31bfbf48d7245c0acc7d0c7b4707d8359e2aa31d1f843fbf35ff1e5d9c`, Continents `ec4b169b836afbf43b39cb08253dbafe32fb0d7d3d6224f3245fd52fafa8e9db`. These are game-model results, not a calibration to historical dates/populations. Native globe picking roundtrips the origin cell.

Browser smoke test imports the 20k Earth world, chooses another origin, focuses it, clicks/moves the origin, starts, steps 250 years and advances to 50k, adjusts opacity and toggles cached views. It passes without page errors; the 49,750-year batch plus control/screenshot checks took 872 ms once loaded. Cold WASM import is still slow; no claim of improved browser startup performance. Browser placement/opacity screenshots and native map screenshots are under ignored `artifacts/prehistory/`. Maximum-detail demographic setup/per-frame palette uploads have not been benchmarked. Existing full-suite historical golden/version mismatch remains separate from these targeted checks.

Final native Earth images visually reviewed: starting patch highlighted in Africa; 50k-year expansion covers much of Africa; 200k-year occupancy spreads into Eurasia/North America while some distant regions remain unoccupied. Tint contrast was corrected for Unity's linear color space. Plain mode removes satellite color variation, and full opacity restores the satellite cell palette (not the smooth natural-surface geometry). Old 3.31 world JSON still passes integrity validation. Root shortcut targets `artifacts/player-prehistory-1/WorldGen.exe`, with its previous target backed up in `artifacts/prehistory/previous-shortcut.lnk`.

Final 20k native interaction regression passes completion, cancellation, resize, north-up and stable projection: cached switch 0.4564 ms, projection switch 0.038125 ms, cancellation 32.6776 ms. Final browser startup/WebGL shader compilation and all 20 preset selections pass after the tint adjustment (`preset-browser-check.mjs`).

## Map presets — 3.32, 3 October 2026

`--presets-only` passes 2,381 assertions: all 20 presets generate at 5,000 cells; each preset matches UI/core resolution over 30 seeds; windows remain bounded; all six shape-based presets preserve their ocean mask and match all-stage hashes through reapply, serialized recipe and reconstruction. Ordinary 3.32 relief, climate, hydrology and subcell geography match 3.31. No goldens were regenerated.

Representative seed: Donut 32.3% land, Crater Sea 79.4%, Inland Sea 83.5%, Terra 33.5%. Lakes and Desert world contain no ocean cells; fractional lake cover is 1.81% and 0.00% respectively. The initial Lakes retention of 140% made enormous polar lakes in the native screenshot; reducing the preset to 20% passed the repeated 2,381-assertion check. Aborea's four leading forest classes cover 1,401 of approximately 1,615 land cells; Boreal taiga/tundra covers 1,300. These are coarse smoke checks, not a guarantee for every seed or resolution. Existing full-suite historical golden/version mismatch remains separate from this targeted pass.

Shared solution/worker build and native Unity player build pass. The existing 3.31 native world JSON loads with its integrity hashes intact. Browser catalog selection smoke check passes all 20 presets plus Custom; range minima display correctly (Archipelago 350 plates, 20 continents), including after switching repeatedly between presets. This is a UI check, not new browser generation parity evidence.

Native 20k Terrain screenshots in `artifacts/presets-332/{donut,terra,crater-sea}.png` were visually inspected: continuous ring, recognizable broad Terra layout, and central sea/island respectively. Spherical circles stretch in the equirectangular map projection; the perfect templates deliberately retain geometric shorelines. CLI Preview repeat/reapply/serialized-recipe/cached-result reconstruction matches every stage hash.

Native Donut 20k interaction evidence: completed, cancellation, resize, north-up and stable projection all pass; cached switch 0.472 ms, projection switch 0.034425 ms, cancellation 47.4658 ms (`artifacts/presets-332/ui.json`). These timings are for the smoke-test resolution, not maximum detail.

Final native Lakes screenshot (`lakes-final.png`) inspected after lowering retention: interior lakes are substantially smaller; polar basins remain visually stretched in map projection. No guarantee of a maximum individual lake area was added. Root shortcut targets `artifacts/player-presets-332/WorldGen.exe`; previous shortcut retained in `artifacts/presets-332/previous-shortcut.lnk`.

## Replaceable shoreline sampler — 3 October 2026

- `--shore-only`: 161 assertions passed. Checks cover repeated derived features after snapshot loading, every saved island retained including multi-island cells, matching shoreline positions at 32/128 outline samples, field/outline agreement, repeatable point queries, 30%/2%/30% water not forcing a neck, aggregate area, shallow wetlands distinct from open water, climate-sensitive island colors, cancellation and unchanged world hash.
- Updated `--subcell-only`: 11,039 assertions passed on Preview/Earth. The former shared-cell-edge-neck assertion is intentionally replaced by a coherent basin-feature assertion. Tiny-area measurement now uses an unquantized scalar triple product, avoiding a test-only area precision floor as outlines gain segments.
- Shared build succeeded. Unity prototype/player compiled. Browser JavaScript syntax checked; a new browser runtime/fresh-generation comparison and maximum-detail benchmark were not run for this deliberately bounded prototype. Earlier golden-corpus version mismatch remains unresolved; golden hashes are untouched.
- The prototype does not claim 10m planning accuracy or playable local terrain. Outline/query agreement is tested; renderer relief exaggeration, feature overlap and approximate redistribution of cell water are documented in ALGORITHMS.md.
- Final 80k Earth close-up inspected at `artifacts/shore-prototype/earth-close-final.png`: open-water outlines no longer contain the connected-cell cutouts. An initial wetland overlay overlap was corrected by moving marsh shading into the continuous ground palette. Coarse ocean coasts and some fine display seams remain visible at this zoom; the prototype is not a high-resolution Earth reconstruction.
- Final 20k Unity interaction check passed (`final-ui.json`): cached switch sequence 0.6573ms, projection 0.053745ms, cancellation 32.05ms, stable geometry and responsive presentation. Root shortcut targets `artifacts/player-shore-final/WorldGen.exe`; previous shortcut preserved in `artifacts/shore-prototype/previous-shortcut.lnk`. Existing saves need not be regenerated for this derived shoreline prototype.

## Natural terrain and unresolved islands — 3 October 2026 (3.31)

- Focused sub-cell suite: 11,039 assertions passed on 20k Preview and measured Earth. Checks include per-island/cell accounting, contained outlines, binary/JSON persistence, rejection of tampered area totals, finite projected geometry, complete map coverage, fractional lake area and connected shared lake edges. 3.31 terrain, climate, hydrology and ecology values exactly match 3.30 at identical settings. Preview has 5,251 unresolved islands in 2,021 ocean cells; Earth has 726 in 492 cells. These are proxies, not a validated real-island inventory.
- Native CLI 20k Preview all-stage repeat/reapply/recipe/cached reconstruction passed. Complete hash `c267f1a754fccf938cecb86519e21b20e8b24c747a501ce90d485882ba668d57`. No golden expectations were regenerated. Historical test assertions were corrected for the previously rebased lake-control fixture, appended ecology CSV columns, indexed triangle color lookup and the dedicated Plates view.
- Native 20k interaction check passed: cached main-view/overlay sequence 0.4294ms, globe/map switch 0.034ms, unchanged geometry, presentation cancellation 32.79ms, maximum busy frame 19.71ms. This is not a new 2.56M benchmark; the additional natural mesh and small-feature buffers increase resident memory.
- Browser rendered a native 3.31 snapshot, smooth/categorical switches and the smooth-display toggle without remeshing (0–0.1ms). Whole-world and zoomed island-group screenshots were inspected. Browser fresh-generation comparison timed out at 300 seconds; new-generation WASM parity is therefore unverified, despite successful native-snapshot validation/rendering. Earlier cached layers/overlays, projection and grouped-save regressions also passed.
- Native 80k Earth Satellite and generated Terrain captures were visually inspected; fractional features are suppressed below pixel scale and appear on zoom. Evidence is under ignored `artifacts/natural-331`: `earth-final.png`, `terrain-final.png`, `browser-smooth.png`, `browser-islands-close.png`, `native-ui.json`. Coastlines remain a coarse interpolated approximation at close zoom; island outlines and lake footprints are proxies, not local terrain.
- Shared solution and Unity player build succeeded. Core/player DLL SHA256: `6CD625E0CFA50EB56372DF514E4191652FEBAEA66BADB3F0BDB00D37DD6C2D47`. Existing user worlds are not rewritten; new island metadata requires a newly generated world. Smooth rendering/fractional lakes also work on older snapshots.
- The full native suite completed the mechanism/presentation/reconstruction checks but stopped at `earth golden stage hashes`: `GoldenCorpus.Recipes()` now produces current 3.31 recipes whereas the checked-in Earth golden is 3.22. This historical baseline requires a separate reviewed migration; its hashes were not rewritten to force a pass.
- Final packaged `player-ui-331-release` interaction recheck passed (`release-ui.json`): cached sequence 0.4338ms, projection 0.031665ms, cancellation 33.12ms. The root `WorldGen.exe - Shortcut.lnk` now points to this build; its prior target is backed up as `artifacts/natural-331/previous-shortcut.lnk`. Shared/player Presentation DLLs also match (`2D0A18C5CA1BB8E7C6AE5CDE0DCA902B5BBA3503EF3061DFB44688D26FD8341D`).

## Indexed dual projection / terrain refinements — 3 October 2026 (3.30)

- Native 2,560,000-cell Earth interaction test completed with unchanged mesh root / no presentation job on projection switching; all cached main layers and overlays passed (2.49ms for the complete switch sequence). Cancellation 50ms; maximum measured busy frame 184ms. `artifacts/performance-330/unity-max.json`. This full-size run used the first 3.30 player, before the final additional ecology/hash optimizations; the projection implementation is unchanged.
- Final release 80,000-cell native check: projection calls averaged 0.054ms, cached switch sequence 0.301ms; cancellation132ms, maximum busy frame19.93ms, stable projection/resize/orientation flags passed. `unity-release.json`. These are CPU switch measurements, not a GPU performance guarantee. Initial maximum-detail generation remains minutes, not instant.
- Streaming 2.56M-cell mesh test:11.34s geometry construction,11.98s dual presentation,24,640,231 surface vertices versus46,110,822 indices; exact full-map coverage within1e-7, including seam and poles.15,381,547 assertions. Roughly46.6% fewer surface vertices than triangle-expanded rendering; no full-resolution downsampling.
- 80k procedural test:482,136 assertions;99 hotspot/trail nodes.33 older nodes contained exposed land, but only2 were on oceanic crust (the others were continental volcanism); this distinction is explicit in the test output. Terrain globe and map camera renders inspected (`terrain-globe.png`, `terrain-map.png`); no projection holes or color interpolation across cell owners observed. Small sub-grid islands remain resolution-limited.
- Single-core and multicore20k generation produced identical complete hashes.80k complete hash remained `34c57475bd1bc21f2f751b41b8d9d410726a04459166f97013cf7ba86ef6639f` before/after dispersal parallelism and canonical habitat hashing. Current80k CLI all-stage repeat/reapply/recipe/reconstruction and historical3.29 snapshot reconstruction passed.
- Focused setup1,523 assertions, terrain controls41, tectonic mechanisms1,370 and ecology72 passed. Old mechanism tests now compare mountain forcing at the pre-rebase equivalent; ecology isolation checks compare before/after ecology within one algorithm, rather than requiring new terrain to match3.27. Golden files were not regenerated.
- Browser cached layers/overlays and projection regression plus grouped-save deletion checks passed; cached switches0–0.1ms. Browser still asynchronously uploads projection geometry, while benefiting from indexed projection. No claim of instant browser map/globe switching.
- Shared build and Unity player builds succeeded. Root shortcut targets `artifacts/player-ui-330-release/WorldGen.exe`; prior shortcut backed up under `artifacts/performance-330`. Player/Core DLL SHA256 matches `A179189E55B018C4CE5762B95C150586751F0C8B6A9CB061C4DDB563814B700F`. Screenshots, binaries, logs and test worlds remain under ignored artifacts. No scientific-validation claim or complete end-to-end generation speedup benchmark is made.

## Optional ranges / mountain variability — 3 October 2026

Version3.29 is intentional; historical goldens were not regenerated.1523 setup assertions pass, including signed optional ranges, linear/low/high roll behavior, deterministic named draws, significant-digit rounding, locked endpoints, actual food-web link membership, preview rendering and all-stage reconstruction equality.41 terrain-control assertions pass, including baseline conversion, old3.28 terrain/climate compatibility, extreme/zero percentages, Earth-height invariance and reconstruction. A frozen3.28 Earth snapshot loads and reproduces every stage through repeat/reapply/recipe/cache reconstruction. New3.29 CLI Preview at5k likewise passes `--verify`.

Two5k procedural samples (73421,42) contain a broad distribution of mountain-feature contributions; final highest elevations are6938m and7831m. This is a distribution check, not a claim of scientific terrain validation or high-resolution geographic acceptance. Coarse partition algorithms and measured Earth heights are unchanged; climate may respond to changed procedural relief, and ecology to changed lakes.

`scripts/controls-329-check.mjs` passes optional-range UI, separate min/max, roll choice, numeric precision, old snapshot loading, real food-web SVG, and selecting Terrain while another presentation is in flight (0ms measured, requested layer survives commit). Range and ecological-detail screenshots were reviewed; dice buttons were subsequently made font-independent and duplicate control captions removed. Native builds compile the compact Inspect popup and food-web layout; automated desktop interaction evidence is recorded under `artifacts/controls-329`.

Final browser UI-only rerun passes after the icon/caption cleanup.43 world-library assertions pass. Desktop5k interaction passes cached switching during an in-flight projection, cancellation, resize and north-up checks (19.65ms maximum busy frame,31.10ms cancellation). Final shared Core DLL checksums match the native player. Root shortcut targets `artifacts/player-ui-329/WorldGen.exe`; its previous target is backed up in `artifacts/previous-shortcut-before-329.lnk`. Running user apps and saved worlds were left intact.

## Grouped saves and cached display controls — 2 October 2026

Generator output/version remain 3.28; no goldens changed. World-library tests pass 43 assertions, including multiple immutable saves per world, deletion of one save versus a whole group, continued saves after deleting the initial snapshot, path validation, and recoverability under `world/.trash`. Legacy metadata without a group ID is treated as a single-save world. Browser deletion retains tombstoned records in IndexedDB. No existing user saves were deleted or migrated.

The setup suite passes 314 assertions including completed-stage display-cache construction, independent river/plate/ecoregion/wire/grid overlays, per-vertex palette ownership in both projections, precomputed main layer palettes and unchanged reconstruction hashes. Main display changes swap a cell-color texture/overlay visibility rather than reconstructing geometry. Diagnostic palettes are cached after first use. Both adapters prebuild Off/5/10/15/30-degree grid choices. Projection changes retain committed camera/input state until the new geometry commits.

Unity player builds successfully. Native interaction checks pass at 5,000 and 80,000 cells: all six main layers plus river/wire/grid switches retain the same mesh root and start no presentation job. Total switch time was 0.070 ms / 0.136 ms respectively; maximum busy frame 18.05 ms / 35.05 ms; cancellation 31.03 ms / 31.11 ms. These are single-run host observations, not maximum-detail benchmarks. The native Earth climate render was captured through the camera and visually inspected; cell palettes and the grid render correctly. Native menu/confirmation GUI is compiled; library behavior is covered by shared tests and the browser interaction flow, not automated Unity mouse clicks.

`node scripts/display-library-check.mjs` passes against port 5094: real Earth snapshot upload, cached switches (0–0.1 ms each), globe/map transition, two saves grouped under one world, deletion cancellation, individual-save deletion, disappearance of the expander when one save remains, and whole-world deletion. Browser setup, climate map and grouped-world menu screenshots under ignored `artifacts/display-review` were inspected. Menus have opaque backgrounds, brighter sliders, adjacent Inspect, and no setup/refresh/save/folder controls in Load World. The final root shortcut continues to target the rebuilt `artifacts/player-ui/WorldGen.exe`; an already-running instance must be restarted to use it.

## Game-shell/setup update — 2 October 2026

The generator remains 3.28. `--setup-only` passes 279 assertions: deterministic logarithmic range resolution and invalid ranges, completed preview ordering, globe/map mesh construction at every published stage, snapshot stability after later stages, identical all-stage fresh/observer/reapply/recipe/cache reconstruction hashes, compact inspection, cancellation, and removal of plate coloring from Terrain. Existing `--ecology-only` still passes 72 assertions with unchanged Earth/one50/six30 counts. No golden expectations were regenerated.

The solution and staged browser worker build successfully. Unity standalone `player-ui` builds successfully. Its `--worldgen-screen-check` passes Earth setup, progressive display, climate view and simulation-view transitions. `--worldgen-ui-check` at 5,000 cells passes cancellation, regeneration, bounded presentation, map longitude changes, window resizing and north-up controls: 521 busy frames, 19.45 ms maximum measured busy frame, 32.89 ms cancellation, 16.82 ms/longitude frame. World-library tests pass 34 assertions. Hidden-window screen captures were black and are **not** accepted as visual evidence. The final browser flow passes fresh Earth generation, a rendered intermediate plate preview, all-stage native/browser hash equality, climate and ecoregion view switching, the ecological detail dialog, a -94.5° wrapped-map drag without mesh regeneration, Start World, saving a new world and reopening it from Load World. Screenshots under `artifacts/setup-review` were inspected (menu, setup, plate preview, Earth climate, ecological dialog and viewer). Earlier map-check failures were a test wait issue; the final check waits synchronously for committed layer/projection readiness. Large-detail preview memory/performance remains unbenchmarked; browser snapshot transport uses an additional .NET presentation worker and can lag native previews.

The root shortcut now targets `artifacts/player-ui/WorldGen.exe`; its previous target is recoverably backed up at `artifacts/previous-shortcut-before-game-shell.lnk`. Existing world folders/saves and already-running apps are untouched. Native/staged desktop Core and Presentation DLL checksums match. The UI update intentionally retains generator version 3.28 and its accepted output hashes.

Reproduce: `dotnet run --project tests/WorldGen.Core.Tests -c Release -- --setup-only`; `node scripts/setup-browser-check.mjs` against port5094 after `scripts/build.ps1`. The browser check compares `artifacts/setup-review/native-earth/hashes.json`, created by CLI `--preset Earth --detail 5000 --output artifacts/setup-review/native-earth`. `--load` tests the compact snapshot import path instead of fresh generation. Desktop screen checks use `--worldgen-screen-check <artifact-directory>`; interaction checks use the existing `--worldgen-ui-check <evidence-json>` entry point.


## Ecoregions and native life — 3.28

72 focused ecology assertions pass: habitat membership/bounds; actual cell-level suitability; compatible/co-occurring trophic links and basal support; specialist koala food; adult-predator risk gating; species/subspecies identity; cold wolf versus warm lion bridge access; blocked deep-ocean routes; temperature-sensitive PET; JSON/binary snapshots; actual frozen 3.27 Earth snapshot compatibility; unchanged terrain/climate/hydrology; and identical fresh/reapply/serialized-recipe/cached stage hashes.36 terrain-control and34 world-library assertions also pass. CLI Preview `--verify` matches every stage in all reconstruction paths. Old golden files were not regenerated.

20k visual review: measured Earth terrain with simulated ecology has608 regions; seed73421/one continent/50% target land has710; seed42/six continents/30% target land has501. All three shared-presentation maps and browser canvas renders were inspected. They have distinct regional patches within climate/biome zones, complete coverage and unchanged accepted coastlines. The supplied Ecoregions2017 website was viewed; its map renders behind a Google Maps API warning. No boundary fit to its observed polygons is claimed. Small patches remain and regional boundaries are provisional, not reconstructed Earth ecoregions.

The three20k snapshots load/render through the browser and retain native stage hashes. A fresh20k browser comparison exceeded the240-second check budget; it was not claimed as passing. A final5k Earth recipe generated in the actual .NET browser worker matches **all** native stage hashes, including habitats/ecoregions/biogeography, and returns the habitat/food-web inspector text. Large browser imports/generation remain slow. Both solution/worker staging and Unity standalone compilation succeed. Expanded Unity inspector interaction was compiled, not manually clicked in this pass.

80k bundled Earth generation took about8.2 seconds on this host, with approximately0.9 seconds between habitats and finalization; this is one run, not a maximum-detail benchmark. The desktop core DLL matches the staged native DLL. Root shortcut targets `artifacts/player-3-28/WorldGen.exe`; its prior target is backed up in `artifacts/previous-shortcut-before-3-28.lnk`. A new named `Earth - Ecoregions 3.28` save is installed without overwriting existing Earth/user saves. The initial development copy created during this task was recoverably archived under `artifacts/ecology-review/first-development-earth-save`, and the final reviewed copy uses library ID `2dba8391107d493bbd0fc6fa2a679ec2`. Running apps are not closed.

Reproduce: `dotnet run --project tests/WorldGen.Core.Tests -c Release -- --ecology-only`. Generate intentional temporary review fixtures with `--ecology-review`, then run `node scripts/ecology-browser-check.mjs` against the rebuilt server (default port5093), or `--worker-only` for the focused5k cross-host check. Artifacts/screenshots live under ignored `artifacts/ecology-review`; the algorithm/trait limitations are in ECOREGIONS.md. These checks establish consistency and specific plausibility constraints, not scientifically validated ecosystem dynamics, population balance or complete species ranges.

## Wide lake/mountain controls —3.27

Both Unity/browser group lake amount and mountain strength under Terrain sculpting, without a Hydrology-only panel. Shared log-scale slider conversion and exact numeric inputs support0,0.0001 through100,000%. Mountain100% preserves accepted terrain and learned climate; Earth relief remains measured and unchanged.

`--terrain-controls-only` passes36 assertions: percentage/log-slider roundtrips, neutral terrain/climate equality against3.26, coarse invariance, actual relief response at0/.0001/400/100000, peak increases, finite lake coverage, zero lakes, extreme snapshot roundtrips, and all-stage reapply/recipe/reconstruction equality at400%. Earth terrain is unchanged at100000% mountain strength. Focused calibration checks pass against legacy3.23 and frozen3.26 Earth snapshots, confirming missing mountain fields restore100 and legacy hashes remain valid. Full old goldens were not regenerated. Solution/browser staging passed; physical realism at extreme values is deliberately not claimed.

Unity standalone build succeeds at `artifacts/player-3-27/WorldGen.exe`; the root shortcut now targets it. The prior shortcut is retained at `artifacts/previous-shortcut-before-3-27.lnk`. All34 focused world-library assertions pass. Slider GUI interaction was compiled but not manually exercised; existing saves and running apps were left untouched.

## Approved trained default promotion —3.26

Owner approved the aggressive run's selected iteration277 for newly generated worlds. Defaults/presets now carry that exact ten-weight vector, without another fit. Existing3.23/3.24 snapshots retain their original arrays/hashes, and3.25/3.26 explicit profiles remain supported. Reset uses explicit untrained3.24. Both UIs expose Use current trained climate defaults for opt-in regeneration of older saves; Unity's New generated world selects the approved profile.

Focused calibration tests pass for approved-vector equality, independent per-recipe arrays, preset coverage, roundtrips and old snapshot compatibility.34 world-library assertions pass. CLI Preview fresh/reapply/serialized-recipe/cached reconstruction matches every stage. Solution/browser staging succeeds. No goldens were updated and no additional learning was run. Historical synthetic climate fixtures explicitly retain their original untrained settings; their expectations were not loosened.

The new80k default Earth matches every stage hash of the reviewed80k learned Earth snapshot. Unity standalone build succeeds; root shortcut targets `artifacts/player-trained-default/WorldGen.exe`. Prior shortcut is retained at `artifacts/climate-learning/previous-shortcut-before-trained-default.lnk`. Existing running apps and world saves were not altered. New button interactions were compiled but not exercised in a GUI session.

## Aggressive learning mode — optional3.26

Owner requested substantially wider parameter freedom and deferred overfitting safeguards. A separate aggressive launcher uses expanded parameter bounds, zero regularization, no component regression veto, joint training+validation acceptance/selection, log-scale multiscale/global proposals, and no plateau early stop. The numerical moisture-budget gate remains; test scores remain diagnostic. Conservative runs cannot be resumed as aggressive runs. Existing saves/runs/defaults are untouched.

Conservative and aggressive self-tests pass, including new-version persistence through Upgrade, expanded-bound validation, reproducible proposals and actual proposals beyond10x. Aggressive nonneutral5k procedural fresh/reapply/recipe/full-snapshot reconstruction matched every stage; terrain/ocean invariants passed. Solution/browser staging passed. No aggressive Earth optimization or visual-quality evaluation was run, and no improved result is claimed. See CLIMATE_LEARNING.md. Generated baseline Earth retains the earlier3.24 stage hashes.

Unity standalone build passed. Root WorldGen shortcut targets `artifacts/player-climate-aggressive/WorldGen.exe`; its previous shortcut is preserved at `artifacts/climate-learning/previous-shortcut-before-aggressive.lnk`. Old executables remain available. Trained3.26 GUI loading/cross-host generation have not been runtime-tested.

## Bounded user-run climate learning — optional3.25

No fitting/learning iterations were run. `scripts/learn-climate.ps1 -PrepareOnly` successfully prepared3,313 observed two-degree land blocks from the official WorldClim2.1 monthly/elevation downloads; URLs and SHA-256 provenance are retained in ignored artifacts. The root `Learn Climate.lnk` prompts for a budget/resume choice. No learned profile is selected by default.

Solution, browser/worker staging and Unity standalone builds passed. Contract tests (`WorldGen.ClimateTraining --self-test --pipeline-check`) passed: bounded finite weights, deep recipe cloning, recipe serialization, old recipe/snapshot reads, profile hashing, deterministic proposals, regularization, reset, component regression guards, existing synthetic climate suite, neutral-profile identity, extreme-profile ocean-free dryness, geographic scoring/month alignment, and held-out score isolation. A nonneutral5k procedural profile matched every stage across fresh/reapply/serialized-recipe/full-snapshot reconstruction, changed climate, and preserved coarse hash/elevation/ocean mask. The new80k untrained Earth matched **all stage hashes** of the frozen3.24 player's bundled Earth. No goldens were changed.

Normal packaging generated unchanged baseline Earth snapshots (80k7.1s,20k5.3s here); it did not calibrate or evaluate Earth against observations. The fitter's real candidate loop, cancellation/resume after real fitting, observed objective improvement, trained-world GUI interaction, and cross-host trained generation have **not** been run. Focused tests exercise the contracts, not proof of climate realism or absence of overfitting. The latest trainer self-tests include standalone synthetic scoring and proposal checks, not an optimizer run.

Root WorldGen shortcut now targets `artifacts/player-climate-learning/WorldGen.exe`, which can load explicit3.25 profiles and reset them. Previous shortcut: `artifacts/climate-learning/previous-worldgen-shortcut.lnk`; previous3.24 executable remains intact. Existing worlds/defaults are unchanged. See [CLIMATE_LEARNING.md](CLIMATE_LEARNING.md) for operation, estimated budgets, output files and limitations.

## Quick untested climate pass and Unity globe orientation —3.24

At the owner's request, no test suite or three-world comparison was run for this pass. Unity globe depth is reflected at the host boundary with reversed triangle winding and matching line/picking transforms; map geometry and authoritative terrain remain unchanged. Climate changes use candidate13 evidence: broader/stronger temperate-boreal moisture mixing, weaker background mixing, slower frontal rainout, partial rather than complete maritime override of subtropical suppression, and a modest reduction in equatorial convective rainout. Their geographic effect is **unverified**. Algorithm output is versioned3.24; goldens are not regenerated. Full3.23 snapshots remain readable without altering stored fields/hashes; explicit regeneration upgrades their recipe to3.24. Existing saved Earth retains its previous climate until regenerated. Packaging builds new bundled Earth snapshots, not a validation run.

Solution/worker staging and standalone3.24 build succeed. Root shortcut targets `artifacts/player-3-24/WorldGen.exe`; prior shortcut backup is `artifacts/previous-shortcut-before-3-24.lnk`. No application was closed or existing save overwritten. Runtime orientation, migration and climate quality were not newly tested in this expedited pass.

## Named world library — 2 October 2026

Climate work is paused. The generator DLL is byte-identical to experimental candidate13 (`b19a9ad0dfca492e016bef02e55b75b48e20cb4c092d0ebc7c785d8a7448f040`); this update changes storage, packaging and UI, not climate. Desktop library entries live in the project `world` directory; each named entry contains a complete compact snapshot, metadata, recipe and PGM heightmap export. New saves are immutable copies, published by directory rename after all files finish. Names are never paths. Hidden incomplete staging directories remain recoverable and are not listed. Browser libraries retain full binary snapshots in IndexedDB, with explicit backup/export and browser-local persistence caveats.

The bundled80k Earth is an actual precomputed snapshot built via `WorldPipeline`, not a recipe the user must import. `scripts/prepare-world-library.ps1` produces the desktop/browser bundle and installs the project's reserved Earth entry without overwriting an existing one. `scripts/build.ps1` includes this packaging step. Exact elevations and all calculated fields are authoritative in the snapshot; exported PGM is sampled/clipped to its documented range. Unsupported historical algorithm snapshots are rejected rather than silently regenerated.

Native focused library tests pass34 assertions (`dotnet run --project tests/WorldGen.Core.Tests -c Release -- --world-library-only`), covering full-state hashes, monthly climate, hydrology, terrain, duplicate names, traversal rejection, cancellation, corrupt metadata/snapshots and idempotent Earth installation. Full3.22 generator goldens have not been replaced by experimental3.23 outputs. UI/host evidence for this storage update is recorded separately below; it does not establish climate realism.

Desktop end-to-end testing exposed a real compact-reader defect: Mono reads packed primitive arrays in one block and rejected the old1MB `MaxBytesPerRead` limit. The allowance now matches the existing80-million-item maximum at8bytes/item; compressed256MB and decoded4GB guards remain. The rebuilt standalone successfully installed80k Earth, loaded it, saved a named copy, reloaded with an identical complete-state hash, and rendered its climate map (exit0). Log and inspected screenshot: `artifacts/world-library-check/player.log` and `earth-player.png`. Full-state hash is `0314f3e9d613559ec7387046f82bdded825d7ca7698a942c410cdc8b5ca912cc`. Final player `artifacts/player-world-library-final` includes the same tested Core/Serialization DLLs plus loaded-preset/new-procedural-world UI label corrections; build exits0. Root shortcut now points there; its previous target is backed up in the evidence folder. No user app was closed or saved world deleted. Previously pinned taskbar links may still launch older builds.

The browser starter snapshot is20k (desktop80k) because WASM snapshot decoding is slow. Opening the20k Earth through the browser UI succeeded but took about2–3minutes per read on this host; do not treat this as acceptable optimized load performance. `scripts/world-library-browser-check.mjs` passes the actual Open Earth → named save → page reload → open named save workflow, checking every stage hash and the full recipe, then rendering a climate map without page errors. Screenshot: `artifacts/world-library/browser/earth-library.png`. Browser-local binary recovery/duplicate-name/error tests also pass (`scripts/world-library-check.mjs`); storage uses Blob payloads and can still read earlier Uint8Array records. Final Web build passes. The temporary test server/browser were stopped. Native golden parity is not inferred from the save/read checks.

## Short climate checkpoint: bounded land evaporation — 2 October 2026

The owner requested shorter, inspectable implementation bursts. Candidate13 changes one moisture mechanism: land return is now the smaller of available recycled precipitation and a wind-/temperature-/humidity-dependent bulk evaporation flux. No terrain, classification thresholds, circulation coefficients or lake-control defaults were changed in this pass. The existing global temperature offset remains upstream of moisture capacity; new cooling regression checks cover temperature and precipitation response. It is a control for colder climate scenarios, not a coupled ice-sheet simulation.

The CLI builds without warnings; focused climate checks pass137/137. Preview repeat/reapply/serialized-recipe/cached reconstruction matches all stage hashes. The80k Earth/one50/six30 run takes approximately7.0/6.2/5.8seconds; terrain/ocean digests exactly match candidate12. Earth rainfall changes (mm/year): Amazon3547→1764, Congo1821→1095, EastAfrica5214→4094, India993→849, Siberia143→124. Thus the unlimited fractional feedback is reduced, but regional circulation remains unacceptable and some already-dry areas worsen. This is a numerical/mechanism checkpoint, **not geographic acceptance or a release**. Installed3.22, existing goldens and host assets are not updated. Three rendered maps and numerical reports are in ignored `artifacts/climate-323/candidate1380`; the Earth image is `earth-Climate.png`.

Open-source reference review: [Climlab surface flux documentation](https://climlab.readthedocs.io/en/latest/api/climlab.surface.turbulent.html) motivates separating evaporation from precipitation through humidity-deficit-dependent surface exchange. This implementation uses an independently chosen column conductance and water-supply closure, not Climlab's full physical parameterization or copied source. [WorldEngine's precipitation implementation](https://github.com/Mindwerks/worldengine/blob/master/worldengine/simulations/precipitation.py) was also inspected; its noise/temperature-normalized rainfall is not suitable as a replacement for the requested ocean-fed transport. No third-party implementation was incorporated.

## Unaccepted climate experiment — 2 October 2026

The working source currently identifies itself as `worldgen-3.23.0`, but **it is not a validated release or a satisfactory climate fix**. The installed player and shortcut remain on3.22. Golden fixtures remain3.22 and have deliberately not been blessed against this experiment. Full native-golden/browser/Unity parity has not been established for3.23. Earlier acceptance statements below describe earlier builds, not this source state.

The experiment replaces inferred monthly extrema with12 simulated months, aggregates four displayed seasons, and uses a bounded atmospheric mesh with conservative ocean-sourced moisture transport. Atmospheric relief/ocean fractions are aggregated from the actual fine terrain; the20k grid is a numerical atmosphere grid, not a replacement terrain route. It retains monthly fields and local moisture-budget residuals. Basin streamfunction fluxes are nondivergent; sea-ice air temperatures are separated from the seawater freezing limit. These numerical improvements do **not** establish geographic realism.

Twelve candidates were reviewed against the observed1980–2016 panel of [Beck et al.2018](https://pmc.ncbi.nlm.nih.gov/articles/PMC6207062/), using measured Earth relief and the same80k procedural worlds (seed73421,1continent/50%targetland; seed42,6continents/30%). The independent reviewer inspected all three maps per candidate. The final candidate12 is rejected: EastAfrica5214mm/year and99%Af, Arabia840mm/year and45%Cfa, India59%arid, and Scandinavia24%steppe are unacceptable regional errors. Congo1821mm/year and westernEurope81%Cfb improve, but do not offset those failures. Terrain elevation/ocean-mask digests remain identical to the frozen3.22 baseline for all three worlds. The reported lowland C below12degrees/D below30degrees was not reproduced in this particular three-world baseline audit; that does not prove absence on other recipes.

Final focused climate checks pass134/134, including monthly classification, quarterly aggregation, oceanless dryness, rain shadows, polar coastal air, and moisture-budget bounds. These are algorithm checks, not acceptance of the maps. All-stage production goldens were not regenerated. Frozen candidates, regional reports, screenshots and review notes are retained in ignored `artifacts/climate-323`; candidate12 results are in `candidate1280`. Remaining structural limitations include prescribed near-surface circulation, missing realistic monsoon/frontal transport, and marine thermal/instability diagnostics still selecting a single least-cost coastal source rather than the moisture solver's mixed transport. No geographic climate-label masks or reference-code translations were used. Further circulation work is needed before release.

## Climate geography and20-fold lake rebase - 2 October 2026

Current algorithm: **worldgen-3.22.0**, schema3. The owner requested iterative climate review against Earth and two generated configurations, and explicitly raised the lake-control baseline20-fold. Frozen3.21 binaries/goldens, five candidate binaries, numerical reports, screenshots and independent notes are retained in ignored `artifacts/climate-322`. No Orogen GPL implementation or parameter table was translated. Its coastal-exposure and distinct thermal/moisture concepts were reviewed in place; production rules are independent.

The same three80k worlds were generated and visually reviewed each iteration: measured Earth; seed73421 with1 continent/50% target land; seed42 with6 continents/30% target land. Allocation targets are not exact final coastline/count guarantees. Review used latitude/elevation windows, source moisture, seasonal rainfall/temperatures, inland distance and current/coastal orientation. Initial corrections broke the Mediterranean bands but revealed northern cooling; subsequent candidates exposed polar warming and overly broad ice buffering. Candidate5 was accepted for owner review after all three screenshots and polar diagnostics were checked. Final elevation and ocean-mask SHA256 digests match3.21 exactly on all three worlds.

| Area-weighted80k Earth diagnostic | 3.21 | 3.22 |
|---|---:|---:|
| Southeast US annual rainfall |604mm|1,157mm|
| Southeast US Mediterranean Csa share |89%|about1%|
| Southeast US humid subtropical Cfa share |0%|87%|
| Southeast China annual rainfall |394mm|1,079mm|
| Amazon annual rainfall |1,890mm|2,004mm|
| Congo annual rainfall |1,309mm|1,438mm|
| Lowland30-40 degree Csa share |79%|24%|

The two generated worlds' lowland30-40 Csa shares decline70→25% and85→39%. Large dry continental interiors remain; the six-continent case stays comparatively maritime/wet. Lowland tropical C climates occupy only0.03/0.30/0.02% in Earth/one50/six30, while tropical highland C remains possible. C/D adjacency is not inherently a defect: colder inland winters may coexist with the same precipitation seasonality. Antarctic highlands are98%EF with mean local-summer temperature-11.1C in the final candidate. These broad comparisons are not an observational raster score or scientific climate validation.

**Remaining regional errors:** Mediterranean lowlands are still overly humid (63%Cfa,29%Csa); Sahara average370mm/year is too wet, with some spurious winter-dry temperate patches. Scandinavian lowland summer11.7C remains too cool, with insufficient coastal moderation. Some Antarctic lowlands remain too warm (3.2C local summer,9%Dfc). Rainfall remains partly zonal. The model uses directed moisture/heat availability and empirical condensation, not conserved atmospheric/ocean budgets; thermal transport still follows humidity-selected paths, and snow/sea-ice effects are proxies. No geographic climate-label patches conceal these errors.

The source checks supporting the mechanisms include [NOAA boundary currents](https://oceanservice.noaa.gov/education/tutorial_currents/04currents3.html), [Met Office climate distinctions](https://weather.metoffice.gov.uk/climate/climate-explained/climate-zones), [monsoons](https://weather.metoffice.gov.uk/learn-about/weather/types-of-weather/wind/monsoons), and [orographic rain shadows](https://weather.metoffice.gov.uk/learn-about/weather/types-of-weather/wind/foehn-effect). Full region definitions and iteration notes are in `artifacts/climate-322/QA.md` and its audit harness.

Lake scale is exactly20 times the previous forcing at each slider value, not20 times area. Default80k actual lake fractions are1.463/4.817/0.475% of non-ocean area; whole-cell display fractions0.686/3.536/0.165%. No lake-area fitting was performed. **Coarse20k Earth at100% has a424,252km2 Amazon lake**, an unrealistic basin exposed by the requested higher storage. At5% (old default forcing under new climate) it is26,321km2; Congo is9,728/3,019km2 at100/5%, while Caspian saturates at443,113km2 in both. The old500,000/30,000km2 regression ceilings are retained explicitly at5%, not silently raised at100%. Default100% remains subject to water-balance, graph, storage, snapshot and exact-golden checks; the size problem is not claimed fixed.

Single-run generation timings stay comparable: Earth3,913ms, one503,291ms, six302,930ms versus3.21's3,843/3,151/2,846ms. This is not a controlled benchmark. Focused climate/hydrology/Earth checks pass1,983 assertions including new coast/current, tropical highland, polar ice, disconnected ocean-heat, slider scaling and zero fixtures. Final solution/worker/Unity staging passes with zero warnings/errors. Frozen Core SHA256: `dd1f5f40987432754f1ab5b672e6fe4cfe8320903ecdadf5db58a12968f39927`. All22 recipe/hash fixtures were deliberately reviewed/updated outside passing tests. CLI repeat/reapply/recipe/cached reconstruction matches all stages, exit0.

- Full native suite passes **5,666,083 assertions** and all11 reviewed goldens, exit0 (`native-tests-final.log`). The earlier default-size regression failure is preserved in `native-tests.log`; its test-input change to equivalent5% forcing is explained above rather than hidden.
- Native maximum Detail2,560,000 passes in102,497ms. River-inclusive globe/map presentations emit942 bounded chunks in3,473/14,657ms, exit0 (`max-detail.log`). This does not establish maximum-detail browser/Unity interaction or snapshot performance.
- Unity editor passes all11 native all-stage corpus comparisons and reconstruction checks, exit0; log `artifacts/climate-322/unity-validation.log`.
- Chrome/.NET WebAssembly passes Earth20k all-stage parity, four-season climate/water snapshot preservation, river-map rendering and hidden-lake inspection. The actual0-1000 slider drives a replacement5k world to0 with no inland water, before full Earth save/load/settings restoration. Test exits0; screenshot/exports/log are under `artifacts/climate-322/browser` and `browser-check.log`. The temporary test server was stopped afterward.
- Standalone `artifacts/player-3-22/WorldGen.exe` builds and passes all11 native stage-hash and compact-snapshot comparisons, exit0. Native/staged/player Core DLL hashes match the frozen SHA256 above. Logs are `unity-build.log`, `player-build-check.log` and `player-check.log` under `artifacts/climate-322`. Other browsers/operating systems, IL2CPP and ARM remain unverified; older evidence below is historical.
- Root `WorldGen.exe - Shortcut.lnk` now targets `player-3-22`, with previous link backed up at `artifacts/climate-322/previous-shortcut.lnk`. A launch through that actual shortcut generated an80k river map, captured `player-map.png` and exited0; the screenshot was inspected for full map coverage and visible river/lake rendering. No user app was closed, and previous builds/world files remain. Existing taskbar pins are not assumed to follow the changed shortcut; re-pin the new executable if a pin still opens an older version.

## Four-season climate, shallow drainage incision and lake slider - 2 October 2026

Current algorithm: **worldgen-3.21.0**, schema3. Owner requested climate-first changes and observational Earth sanity checks, then explicitly stopped fitting the lake baseline to a global coverage target. Default100% is a conservative estimate, not a calibrated1.8% lake inventory. Frozen3.20 CLI and previous goldens are retained in ignored `artifacts/climate-321`; all22 recipe/hash fixtures were deliberately updated outside tests after independent review.

- Final solution/worker build and Unity staging pass with zero warnings/errors. Core SHA256: `19649e65d5b73554f6a54e3c9a51be3445b6bac13d0af959f0ce45dc91eece89`.
- Full native suite passes **5,665,252 assertions** and all11 goldens. Focused climate fixtures test all four quarters, hemisphere reversal, distinct shoulders, directional rain shadow, a10,000km inland transect, non-polar lowland65N summers, oceanless zero rainfall and classification. Hydrology tests cover all-four-season units/conservation/snowmelt, direct-rainfall-fed terminal lakes, slider1/50/100/1000 monotonicity and zero-lake drainage/losses. Physical incision tests prove descending shallow outlets, exclusion of deep/wide/glacial barriers, zero hydraulic behavior and cumulative original-height cut bounds. New arrays survive snapshots and CSV. Independent CLI repeat/reapply/recipe/cached-result reconstruction matches every stage.
- Independent reviewer compared the observed Beck2018 map with Earth and two procedural seeds in Climate/Terrain+rivers views, plus numerical reports for four procedural seeds. These are broad checks, not pointwise validation or climate-skill scores. The latest incision-cap fix changes only six cells across four80k worlds (at most23m), two climate classifications and negligible lake area; the reviewed screenshots remain representative. Earth measured relief is untouched.

Comparable80k Earth diagnostics, area-weighted over the same broad land-region windows:

| Diagnostic | 3.20 | 3.21 |
|---|---:|---:|
| Annual land precipitation |393mm|891mm|
| Amazon-window annual precipitation |655mm|1,890mm|
| Congo-window annual precipitation |468mm|1,309mm|
| India-window annual precipitation |400mm|956mm|
| Lowland60-70N northern-summer temperature |3.4C|15.3C|
| Lowland60-70N northern-winter temperature |-15.2C|-15.4C|

Rainforest rainfall and boreal summer temperatures improve in the expected direction without geographic production masks. Evidence sources include the [observational classification map](https://www.nature.com/articles/sdata2018214), [NOAA air masses](https://www.noaa.gov/jetstream/synoptic/air-masses), and [orographic rain-shadow mechanism](https://weather.metoffice.gov.uk/learn-about/weather/types-of-weather/wind/foehn-effect). Specific source/region definitions and frozen reports are in `artifacts/climate-321/QA.md` and its audit harness.

Single-run native80k timing remains comparable: Earth3,438ms versus3,508ms baseline; procedural worlds2,560-2,789ms versus2,453-2,786ms. This is not a controlled microbenchmark, but shows no order-of-magnitude slowdown at this detail. Four climate quarters replace two, while physical-distance graph transport replaces48-hop iterations.

Lake slider verification on final source: Earth20k actual fractions at0/50/100/1000% are0/0.0774/0.4083/0.7391% of non-ocean area; procedural73421 gives0/0.0970/0.1202/0.2372%. Earth20k Caspian drops from443,113km2 at100% to9,338km2 at50%, confirming the effective-loss bias handles lakes fed directly by rainfall. Zero retains river data but no inland WaterBody/coverage/volume. Basin capacity can saturate, so10x the parameter is not10x lake area. Default80k Earth is0.2284% lake area; procedural samples are roughly0.13-0.47%. The owner chose adjustability rather than further baseline fitting. [HydroLAKES' approximately1.8% natural-lake reference](https://www.nature.com/articles/ncomms13603) is contextual only, not an achieved calibration claim.

**Remaining limitations:** the moisture model is a least-attenuated ocean-origin availability proxy, not a globally conserved vapour budget/GCM. Rainfall remains too zonal; eastern-US summer dryness, cold Scandinavian winters, dry Congo and overly mild Antarctic coastal classifications/runoff remain visible. Ocean currents, historical glaciation and land recycling are simplified. Four-quarter interpolation cannot resolve actual monthly extremes. Default Earth20k still has an oversized443,113km2 Caspian at+48m because modelled local precipitation exceeds evaporation (948 versus845mm/year); Earth80k has an undersized83,040km2 Caspian. The80k Congo still has17,092km2 of inferred water. Thus neither realistic lake shorelines nor complete hydrological correction is claimed.

The previous Earth20k test limits (370,000km2 largest and10,000km2 Amazon/Congo) do not survive the changed rainfall. They were **explicitly reviewed and changed** to500,000/30,000km2 regression ceilings after documenting443,113/21,319km2 residuals; those ceilings are not observed targets. Raising retention128 was rejected because it recreated a259,000km2 Congo; further calibration stopped on owner instruction. Shallow actual incision and bounded virtual outlets cannot resolve every wide/coarse basin.

Native maximum Detail2,560,000 completes in112,391ms, exit0. River-inclusive globe/map presentations emit942 bounded chunks in3,737/15,440ms. This verifies native generation/presentation, not maximum-detail browser/Unity interaction or snapshot persistence.

- Chrome/.NET WebAssembly passes Earth20k all-stage native parity, river-map presentation, full compact-world save/load and complete four-season climate/water metadata preservation. The actual0-1000 range control drives a replacement5k world's authoritative recipe to0, producing no inland water/coverage. Test exits0; screenshots and exports are in `artifacts/climate-321/browser`, with `browser-check.log`. The test server was stopped afterward.
- Unity editor passes all11 reviewed native all-stage corpus comparisons and reconstruction checks. Evidence: `artifacts/climate-321/unity-editor-runtime.log`.
- Standalone `artifacts/player-3-21/WorldGen.exe` builds and passes all11 CLI stage-hash comparisons and compact snapshot roundtrips. Player/staged Core DLL SHA256 matches the frozen native hash above. Evidence: `player-build-check.log`, `player-check-runtime.log` and `unity-build-runtime.log` under `artifacts/climate-321`. IL2CPP, ARM and other operating systems/browsers remain unverified; earlier-version evidence below is historical.
- Root shortcut targets `player-3-21`; the previous link is preserved in `artifacts/climate-321/previous-shortcut.lnk`. Launching through that shortcut generated an80k river map, captured `player-map.png` and exited0. The screenshot was visually inspected: complete map coverage, visible branching rivers, no obvious seam or rendering failure. Old builds/world files were preserved and no user app was closed. Regeneration is required for new settings; old versioned snapshots still require their original build.

## Mountain frequency and conservative lake storage - 2 October 2026

Current algorithm: **worldgen-3.20.0**, schema 3. The owner rejected 3.19's residual giant lakes and excessive massive ranges. Frozen 3.19 runtime/baselines and previous goldens are preserved under ignored `artifacts/relief-lakes-320`. All 22 recipe/hash expectations were explicitly updated outside tests after three candidate reviews. Earlier visual acceptance below is superseded.

- Solution/worker build and Unity DLL staging pass with zero warnings/errors. Core SHA256: `4d41ce72359584bf2fa7584d2814533efbcb8f857fb34c78204fb29d247645a4`.
- Full native suite passes **5,664,824 assertions**, all 11 goldens, complete snapshot roundtrips, connectivity/coarse-detail contracts, cancellation and presentation. Focused hydrology tests cover whole-basin conservation including explicit transmission loss, frozen terminal basins, genuine above-sea arid closed basins, subgrid throughflow, fractional storage and terrain immutability. Controlled margin-history tests retain subdued and strong mountain classes. Added permanent Earth20k actual-area bounds: no lake >=370,000 km2, and no Amazon/Congo-window lake >=10,000 km2; geography appears only in tests, not solver rules.
- Independent CLI Preview fresh/repeat/reapply/recipe/cached reconstruction matches every stage. New outlet/retention/loss fields survive JSON/compact snapshots and alter authoritative hashes.
- Independent review covers Earth20k/80k/204k and four procedural80k seeds, with five final Terrain+rivers screenshots. High immediate-coastal land (>1,500 m) drops from 7.02-14.15% to 1.25-2.29%, versus Earth80k 2.08%. Strong inland belts remain in seeds17/98765;42/73421 are lower-relief variants. A first over-flattened collision candidate was rejected before restoring some inland strength. No blanket interior uplift was applied.
- All five coarse hashes and explicit coarse plate/continent/geometry digests match3.19. All three measured Earth height/ocean digests match exactly. Procedural fine ocean classification changes443/576/462/521 of80,000 cells (0.55-0.72%) as uplift changes; exact procedural coastline preservation is not claimed.

| Earth detail | Caspian inferred area, km2 | Largest Amazon-region lake, km2 | Largest Congo-region lake, km2 |
|---|---:|---:|---:|
| 20,000 | 292,447 | 3,504 | 1,192 |
| 80,000 | 120,702 | 1,567 | 3,208 |
| 204,000 | 123,098 | 2,399 | 2,429 |

These are actual fractional areas, not hidden full reservoirs. Baseline80k Caspian/Congo were861,301/258,990 km2; baseline20k Amazon was755,945 km2. The conservative370,000km2 upper guide is deliberately approximate; [NASA describes the Caspian at roughly371,000km2](https://science.nasa.gov/earth/earth-observatory/the-caspian-seas-shrinking-coastline/). No climate calibration or measured shoreline reconstruction is claimed.

Additional Earth80k seeds1/42/317/9999 all pass the same conservative bounds: largest lakes104,842/118,673/96,709/140,778km2; largest Amazon/Congo lakes279-3,711km2. These checks sample variation; they do not prove every possible recipe or seed. Same-seed Earth relief is unchanged across versions; changing seed changes the sampling mesh itself.

Conservative-bias limitation: procedural80k actual lake area is0.197/0.116/0.124/0.260% of land for seeds73421/42/17/98765; visible whole-lake cells are5/0/0/19. Two worlds therefore have **no full-cell lake tiles**, although fractional lake records remain. Earth80k has0.230% estimated lake area and9 visible cells; Great Lakes and other real lakes are underrepresented. This honors the requested undersize preference but is not an accurate global lake inventory. The subgrid outlet is a strong drainage prior, not a proven channel across the sampled ridge; terminal delivery loss is a conservative heuristic, not resolved groundwater physics. Both are exposed in saved/inspected metadata. Glacial provenance does not reconstruct Earth's past ice sheets. Full independent evidence: `artifacts/relief-lakes-320/QA.md`.

Native maximum Detail 2,560,000 passes in98,505ms. River-inclusive globe/map output emits942 bounded chunks in3,433/15,800ms. This tests native maximum-detail generation/presentation, not maximum-detail snapshot persistence or interactive browser/Unity performance. Other host checks for this frozen build are recorded below as completed; prior-version results must not be read as current verification.

- Chrome/.NET WebAssembly Earth20k matches all native stage hashes, exercises river-map presentation and hidden-lake inspection, saves a complete snapshot, generates a replacement world and reloads the snapshot. Fractional area/volume, all WaterBody metadata, rivers, settings and final hash survive unchanged. `artifacts/relief-lakes-320/browser-check.log` and browser exports/screenshots retain evidence. The temporary test server was stopped after validation.
- Unity editor passes all11 native all-stage corpus comparisons, repeat/reapply and full JSON snapshot roundtrips. Evidence: `artifacts/relief-lakes-320/unity-validation.log`.
- Standalone `artifacts/player-3-20/WorldGen.exe` builds successfully and passes all11 CLI stage-hash comparisons plus compact snapshot checks. Native, staged Unity and player Core DLL SHA256 match the frozen hash above. Evidence: `unity-build.log`, `player-check.log` and `unity-player-console.log` under `artifacts/relief-lakes-320`. Untested targets remain IL2CPP/ARM/other operating systems and browsers; agreement here does not establish them.
- Root shortcut now targets `player-3-20`; its previous link is backed up at `artifacts/relief-lakes-320/previous-shortcut.lnk`. A launch through the actual shortcut generates an80k river map, captures `player-map.png` and exits0. Old executables/world files were preserved; no user app was closed. Regenerate under3.20 to see changed terrain/hydrology; older versioned snapshots still require their original executable.

## Fractional lakes and selective outlets - 2 October 2026

Current algorithm: **worldgen-3.19.0**, schema 3. The owner rejected 3.18's excessive lake coverage. Old expectations and a runnable 3.18 CLI were preserved under ignored `artifacts/lake-correction`; all 22 current recipe/hash fixtures were explicitly regenerated outside passing tests after the candidate review. Terrain, erosion, tectonics and climate algorithms were not changed.

- Full native suite passes **5,663,372 assertions**, including all 11 reviewed corpus cases; fractional lake area/storage, continuous levels, closed equilibrium, multi-exit/cascade conservation, bounded/pinned spillways, frozen precipitation/snowmelt, 90% presentation threshold, hidden-lake inspection, CSV and JSON/compact snapshot preservation, cancellation and deterministic reconstruction. Independent CLI Preview repeat/reapply/recipe/cached-result reconstruction matches every stage.
- Release solution/worker build passes with zero warnings/errors. Current host-specific results are recorded below as completed.
- Native maximum Detail 2,560,000 completes in 93,978 ms; river-inclusive globe/map emit 941 bounded chunks in 3,184 / 15,434 ms. This tests the new solver and ribbons at maximum detail, not maximum-detail snapshot persistence or interactive browser/Unity performance. Core DLL SHA256: `7aef44a93ed6e464870dfdd21fe7856d82e181f06e391651557b49a090202283`.
- Unity editor passes all 11 native stage-hash comparisons, including reapply and JSON snapshot reconstruction. A 204k Earth compact snapshot (70,591,309 bytes) loads without generation in 7,554 ms and renders river-inclusive globe/map in 375 / 1,216 ms (77 bounded chunks each), preserving fractional water state.
- Actual Chrome/WASM Earth20k generation matches all native stage hashes. The targeted browser check passes river toggle/map, hidden-lake inspection (including a 0.4% cell), finite coverage/storage and weighted-area assertions, compact download, replacement-world generation, reimport and exact preservation of fractions, volumes, continuous levels, water records, rivers and recipe settings. Before/after JSON and screenshot evidence are in `artifacts/lake-correction/browser`; the temporary server was stopped. Full browser corpus/max-detail browser interaction was not rerun.
- Windows standalone build `artifacts/player-3-19/WorldGen.exe` passes all 11 all-stage native corpus comparisons and compact snapshot roundtrips. Its Core DLL matches the native/staged checksum above. The existing root shortcut now targets this build; the previous link is retained at `artifacts/lake-correction/previous-shortcut.lnk`. The user's running 3.18 app was not closed or overwritten. Older versioned snapshots still require the matching older executable; corrected hydrology requires regeneration in 3.19.
- The actual updated shortcut launched an 80k procedural Terrain map with rivers, saved `artifacts/lake-correction/player-map.png` and exited with code 0. The capture was visually inspected: fewer lake dots, readable rivers, intact relief and remaining larger lakes. The user's original 3.18 process remained running afterward.
- Independent before/after review used procedural seeds 73421, 42, 17, 98765 plus Earth at 80k detail. Every integer-elevation plus ocean-mask digest matches the frozen 3.18 baseline exactly. Five final screenshots show much less lake-dot coverage, readable rivers, coherent unchanged continents/mountains, and varied retained larger lakes. Evidence: `artifacts/lake-correction/QA.md`, `baseline`, `candidate7`.

Percentages below use non-ocean terrain land area. Estimated area includes hidden sub-cell lakes; displayed area counts complete cells meeting 90% coverage. Terminal drainage includes dry closed catchments, not just lakes.

| World (80k) | Old lake area | New estimated area | New displayed area | Old terminal drainage | New terminal drainage |
|---|---:|---:|---:|---:|---:|
| 73421 | 5.019% | 2.268% | 1.468% | 29.279% | 11.931% |
| 42 | 4.058% | 1.869% | 1.110% | 28.338% | 20.122% |
| 17 | 5.267% | 2.569% | 1.695% | 42.603% | 19.829% |
| 98765 | 6.016% | 1.534% | 0.786% | 27.027% | 11.306% |
| Earth | 2.583% | 1.890% | 0.666% | 21.607% | 15.185% |

Early candidates were rejected despite cleaner displays because they created large artificial Amazon/Congo reservoirs underneath. The accepted bounded channel search removes the Amazon reservoir and lets the Congo drain onward. A uniform 20% evaporation adjustment was assessed separately after outlet improvements; climate fields and observed-climate calibration remain untouched.

Separate Earth 204k check retains 2.564% estimated / 0.870% displayed lake area, 3,826 hidden wet cells, 532 visible lake cells and 4,136 river reaches. This checks resolution sensitivity, not lake-area convergence.

Residuals: the inferred Caspian remains oversized (861,301 km2 at 80k; 801,704 km2 at 204k), although now closed and smaller than before. The Congo still has excessive inferred storage (258,990 / 162,365 km2) despite its restored outlet. The 90% rule suppresses substantial parts of coarse Great Lakes imagery. At the lower 20k browser-test resolution, an artificial central Amazon lake remains; the 80k improvement must not be extrapolated to every detail setting. Fractions are inferred from sampled relief, not measured water masks; the bounded cost/length search can conservatively miss eligible channels. These limitations were not hidden by fitting individual Earth regions or further raising evaporation. Earlier 3.18 acceptance below is historical, not evidence that its rejected lake coverage was correct.

## Hydrology, inherited terrain and bundled Earth — 2 October 2026

Current algorithm: **worldgen-3.18.0**, schema 3. Owner authorization supersedes earlier hydrology deferral. The 3.17 coarse partition is preserved; fine terrain and persistent state intentionally change. The preceding 3.17 goldens were copied to ignored `artifacts/hydrology/previous-goldens`; the reviewed current corpus adds Earth. Expectations were generated explicitly with CLI outside the passing test run.

- Full native tests pass **5,660,265 assertions**, including all 11 corpus cases, reconstruction/recipe/JSON/compact-snapshot roundtrips, coarse/detail contracts, connected geology, hydrology and ribbon presentation. Preview CLI independently verifies all-stage fresh/reapply/recipe/cached-result parity.
- Focused hydrology/Earth tests pass **1,179 assertions**: acyclic adjacent receivers; common lake levels and spill edges; closed arid basins; frozen precipitation versus warm-season snowmelt; cascaded outflow; glacial provenance; retained measured heights; source longitude wrapping and land-area sanity; compact snapshot roundtrip, cancellation and rejection of altered state.
- NOAA ETOPO2022 60-arcsecond ice-surface data were subselected at 10-arcminute spacing, checked for coordinate order/completeness/missing values, quantized to metres and bundled offline. SHA256 `12422c5fc5be4196adc505d8a6c0fc327ee1c1ec2157b2b7011f149c4a23fb62`. Source metadata, permission link and conversion procedure are retained beside the data. No observed climate comparison/tuning was performed.
- Final 20k corpus lake fractions range from zero on the water-world to 7.71% of land on sparse-plates; default procedural is 3.26%, Earth 1.03%. These are resolution-dependent simulated water bodies, not claimed observed lake inventories. Separate Earth80k seeds 1/42/317/9999 complete at 2.83/2.56/2.52/2.64% lake/land area.
- Solution/browser-worker build passes with zero warnings/errors. Cross-host and visual review evidence is recorded below when completed.

- Independent final visual review covers procedural seeds 73421, 42, 17, 98765 plus Earth at 80k detail, Terrain with rivers. The first overly wet pass was rejected. Final review passes with minor concerns: coherent continents with varied lakes, fine dendritic river hierarchy and no seam break, smaller hotspot chains, visible subdued inland ranges, recognizable Earth relief. Several procedural lakes remain unusually large/jagged. Final procedural lake/land fractions are 5.02%, 4.06%, 5.27%, 6.02%; Earth 2.58% (376 lakes). Evidence: ignored `artifacts/hydrology/review/visual-qa.json` and PNG/SVG/CLI reports. No climate comparison was made.
- Unity editor passes all 11 all-stage corpus comparisons, reapply and JSON serialization checks. Core DLL SHA256 `a9763314293c7df5f486ca916529d25628da6f8e835c31472f718600e08d45e1` matches visual review.
- Native maximum Detail 2,560,000 completes generation in 97,793 ms, plus 939 bounded globe/map chunks in 3,451/15,437 ms. This maximum-detail render did not enable rivers; river-inclusive globe/map rendering is separately verified at default Detail 204,000 (77 chunks).
- Default-detail compact snapshot roundtrip: 204,000 cells, 71,869,246-byte `.world` file, integrity-checked load without generation in 7,569 ms; globe/map plus rivers render in 349/1,233 ms. Format is an eight-byte `WGWRLD01` header followed by gzip-compressed .NET binary XML, shared across hosts, with no intermediate full-world JSON string. Saves retain all model fields, including water surfaces/discharges and source recipe. Tests reject invalid magic and support cancellation.
- Standalone Windows Mono player build and all 11 corpus checks pass, including full compact snapshot roundtrips with exact state hashes. The existing shortcut now targets `artifacts/player-3-18/WorldGen.exe`; the prior link was backed up to `artifacts/hydrology/previous-shortcut.lnk`. Actual shortcut launch rendered an 80k Terrain map and exited successfully (`artifacts/hydrology/player-map.png`). No existing user player was closed or overwritten.
- Targeted browser/WASM check passes Earth20k all-stage native golden parity, river toggle, compact world download, replacement-world generation, compact Earth re-import and restoration of its settings/final hash. The 11,150,372-byte browser `.world` also loads in native CLI with exact Earth hash `517bcd546272089be3f5f48b5ab7bfb6072a73e1cbc514c41c69f9bfcb105be5`. Evidence is under `artifacts/hydrology/browser`. Full browser corpus/max-detail browser interaction was not rerun for this update.
- Final actual-browser map inspection caught overly faint ribbons, despite correct physical width hierarchy. Presentation-only width exaggeration was increased to `12 km + 28 * WidthM`, with a muted-to-richer color scale. Restaged browser assets pass a direct compact-world reload, river map rendering and restored hash/settings checks; the final screenshot shows readable tributaries/trunks without obscuring relief or crossing the map seam. Two-column file actions retain full labels. The temporary web server was stopped.

- The final width-adjusted standalone rebuild passes all 11 player corpus cases with exact stage hashes. The actual updated shortcut also launches Earth at 80k detail with rivers, captures a readable map and exits with code 0; visually inspected evidence: `artifacts/hydrology/earth-player.png` and `earth-player.log`.

Limits: the local unresolved-spillway rule is a geomorphic heuristic, not actual channel excavation; glacial lake provenance is not a historical ice-sheet reconstruction. Earth is 10-arcminute relief with procedural diagnostic plate labels, not an observed climate/river/plate dataset. Small lakes/islands below grid resolution remain unresolved. Compact world files are bounded to 256 MB compressed/4 GB decoded; legacy JSON to 250 million characters. Maximum-detail full snapshot persistence is not verified and may exceed those limits. Daily floods, groundwater and iterative lake-climate feedback are not simulated.

## Irregular plate fronts — 1 October 2026

Current algorithm: **worldgen-3.17.0**, schema 3. This intentionally changes the coarse plate partition and its resulting continents for a seed. The 20k coarse/fine split, whole-plate assignment, Continents/Interconnectedness controls and single authoritative pipeline are preserved.

- Three candidates were compared against frozen 3.16 maps at 80k detail for seeds 0, 17, 42, 73421 and 98765; C was also reviewed on held-out seed 2026102. Independent visual review rejected simple directional/resistance growth (A) and A plus variable margins (B) as incomplete fixes: submerged annuli and circular embayments remained. Candidate C's barrier-plus-travel fronts generalized across all six seeds without severe needle, worm or checkerboard regressions. Isolated round bays and curled ridges remain; this is a reduction of recurring template bias, not a prohibition on natural curved features. Source maps live under `artifacts/plate-shapes/{before,candidate-a,candidate-b,candidate-c}`. All six final reduced-mottling maps were independently reviewed under `artifacts/plate-shapes/final`.
- The independent reviewer passed final reduced-noise maps for 42, 98765 and held-out 2026102 with residuals explicitly recorded in `artifacts/plate-shapes/visual-review.json`: compact islands, isolated circular coastal cutouts and some near-closed submarine loops remain. Lower abyss-noise amplitude cleaned up mottling without exposing new dominant concentric halos. Visual acceptance is an implementation review, not owner approval or a guarantee for every seed.
- Test-only shape measurements across five seeds and 80/500 plates confirm nonempty connected regions and increased directional/noncompact shapes. C's median axis ratios are approximately 1.64–1.76 (80 plates) and 1.72–1.84 (500); per-run p90 perimeter/area ratios remain below the broad reviewed bound of 8. These geometric statistics are regression diagnostics, not scientific or aesthetic realism scores. Baseline and experimental evidence is retained under ignored `artifacts/plate-shapes`; the checked test code contains no duplicate legacy generator.
- **84 plate-growth assertions** pass: requested count, complete membership, connectivity, deterministic labels/records, broad shape bounds and full-pipeline 500-plate projection onto 5k detail. The full native suite passes **5,479,468 assertions**, including controlled tectonic fixtures and all ten goldens. Fixed coarse partition versus Detail/Irregularity, presentation coverage, cancellation and deferred hydrology checks remain in the suite.
- Release solution/worker build passed with zero warnings/errors; shared DLLs staged into Unity. Ten candidate recipe/hash filesets were explicitly generated and reviewed in `artifacts/plate-shapes/final-corpus` before updating expectations. The preceding 3.16 binaries/core/goldens are preserved under `artifacts/plate-shapes/baseline-*`; tests never regenerate expectations.
- Preview repeat, reapply, serialized recipe and cached-result reconstruction match every stage. Complete hash: `7e4cd28528a1e207db0fa170ecc7ead513fc7fa62387e3eedbd7e21a98301d9e`.
- Maximum-detail native generation passed at **2,560,000 cells** in 101,908 ms; globe/map each emitted 939 bounded chunks (3,217 / 15,442 ms). Complete hash: `a0bea64e3b258d0bb782b7c2975305e3dab284a15a41f695d2556850a2b48c3c`. This does not establish maximum-detail browser/Unity interaction performance.
- Browser worker/UI passed seed 42 / Interconnectedness 10 with every native stage hash matching, recipe save/load, invalid-count feedback, diagnostic maps and compact layout. Unity editor passed all ten corpus cases with exact native hashes, reapply and snapshot reconstruction. Evidence: `artifacts/continent-browser`, `artifacts/unity-validation.log`, `artifacts/unity-editor`. Full browser corpus and maximum-detail browser/Unity interactions were not rerun.
- Rebuilt Windows Mono player **`artifacts/player-3-17/WorldGen.exe`** passed all stages of all ten native golden cases. Core DLL matches the staged build, SHA-256 `30e9b122d847645648f3303a8f81cf8d88336bf943b7930f546d6a0ece565176`. The owner approved updating the existing root shortcut to this executable; the previously running player was not closed or overwritten. Build/check evidence: `artifacts/unity-build.log`, `artifacts/player-check.log`, `artifacts/unity-player`.
- The updated **actual root shortcut** launched successfully, rendered an 80k Terrain map and exited with code 0; capture visually inspected at `artifacts/plate-shapes/shortcut-check/map.png`. Previous shortcut retained at `artifacts/plate-shapes/previous-shortcut.lnk` for recovery. The shortcut now targets the 3.17 executable and its working directory.

## Tectonic realism — 1 October 2026

Historical algorithm: **worldgen-3.16.0**, schema 3. Whole-plate allocation and Interconnectedness are unchanged. Fine tectonic relief, feature provenance and erosion retention intentionally change; 3.15 restoration evidence below is historical. Geological comparisons and limitations are recorded in [TECTONIC_REALISM.md](TECTONIC_REALISM.md).

- Release solution/worker build passed with zero warnings/errors; shared DLLs staged into Unity.
- Native suite passed **5,479,452 assertions**, including **917 targeted tectonic assertions** and all ten reviewed golden cases. Controlled fixtures test parent ownership, third-plate leakage, stale-density polarity, consistent parent polarity, collision front/plateau/foreland relationships, pure-transform exclusions, ordinary submerged ridges, deep-ocean arc emergence without hotspot assistance, hotspot age/direction and deterministic repetition. Arc survival, submerged ridges and front/foreland contrast are also checked after default erosion.
- Preview repeat, reapply, serialized recipe and cached-result reconstruction match at every stage. Complete hash: `4c44cd4d0a809884cf7b77fde61f9e2e97c03898a7de421eaa0fc63acdbb31c9`.
- Ten candidate outputs were explicitly generated in `artifacts/tectonic-realism/final-corpus`, reviewed for elevation/land/feature metrics, then their recipe/hash files applied to the current fixtures. Previous expectations remain under `artifacts/pre-tectonic-realism/goldens`. Tests do not regenerate expectations.
- Same-seed Terrain maps at 80k detail were visually reviewed for seeds 42 and 98765. Coherent high collision fronts, open arc chains and intervening basins are visible. This is implementation review, not owner aesthetic acceptance or proof that every named Earth analogue has been reproduced.
- A separate six-seed 80k morphology audit (73421, 42, 0, 901, 12345, 808) found respectively 185, 81, 182, 144, 108 and 109 final emerged deep-ocean arc cells more than 900 km from continental crust, excluding all hotspot/trail/ridge-hotspot/igneous-province footprints. Plateau footprints were broader than collision fronts in these cases. This confirms that ring suppression did not simply eliminate non-hotspot ocean islands. Narrow Panama-style connections and exact Mediterranean/New Zealand layouts are possible process combinations, not individually established topology guarantees in this corpus.
- Five matched seeds (42, 73421, 0, 17, 98765) were assessed on final emerged ocean-crust land, excluding attached continental landmasses. The review metric measures island proximity within 400 km of small ocean-plate perimeters and angular coverage; it is not an authoritative generation rule. At 20k, suspicious near-circumferential flags fell **3 to 0**, detached islands 254 to 174, and weighted perimeter proximity 15.95% to 9.33%. At 80k, flags fell **5 to 1**, islands 461 to 302, and proximity 24.32% to 13.58%. The remaining seed-98765 plate-16 flag was inspected: a western arc leaves the eastern plate oceanic, so the conservative proximity test overcounts a partial arc. Reports and cell plot: `artifacts/tectonic-realism/island-rings-final-{20k,80k}.json`, `inspect-98765.png`. The isolated before-build matches the saved 3.15 ordinary golden.
- Maximum-detail native generation passed at **2,560,000 cells** in 97,570 ms. Globe/map each emitted 939 bounded chunks (3,809 / 17,220 ms). Final hash: `c61ad8e6a48f498fb180de948f190e7aefffd758bd6ce1f7392dd96d4daafe35`. This is not a maximum-detail browser or Unity interaction test.
- Browser worker/UI passed seed 42 / Interconnectedness 10 with every stage matching the native golden, invalid-count feedback, exact recipe save/load, Terrain/Continents views and compact layout (`artifacts/continent-browser`). Complete hash: `60c5fdb9429113e822062c9c76f706dd3bfd78a3424dbbae47cb30d191a8c926`. The full browser corpus and maximum-detail browser interactions were not rerun.
- Unity editor passed all ten cases with native all-stage equality, reapply and snapshot roundtrips (`artifacts/unity-validation.log`, `artifacts/unity-editor`).
- Rebuilt Windows Mono player passed all stages of all ten native golden cases. The current build is **`artifacts/player-3-16/WorldGen.exe`**. A previously open player at `artifacts/player/WorldGen.exe` and its root shortcut were left unchanged; use the new executable for this update. Player build/check evidence: `artifacts/unity-build.log`, `artifacts/player-check.log`, `artifacts/unity-player`.
- The new executable launched, rendered an 80k Terrain map and exited successfully; the capture was visually inspected (`artifacts/tectonic-realism/player-launch/map.png`). Its Core DLL matches the shared build, SHA-256 `624e79f6c4243ca166b94bc8c3ff1b4c4f6f9e07644348a5106ae452f6a45441`. Maximum-detail Unity interaction/cancellation was not rerun for this update.

## Plate-allocation search — 1 October 2026

Historical algorithm: **worldgen-3.15.0**, schema 3. The owner superseded complexes and requested restoration of the pre-redesign generator, adding only best-of-N plate land/sea selection (world types deferred). Tests must not regenerate expected hashes.

Baseline restoration proof: the original eight worldgen-3.12.0 cases were reconstructed against the saved `artifacts/pre-complex-goldens` evidence, then compared with current one-candidate outputs. All authoritative world-data field hashes and coarse hashes match, including geometry, plates, terrain, erosion, hydrology state and climate. New recipe/version fields, candidate diagnostics and their dependent stage/final hashes are intentionally excluded from this raw-data equivalence claim. Evidence: `artifacts/baseline-proof/baseline.json` and `current.json`; the reconstructed baseline also matches the original saved stage hashes.

- Release solution/worker build passed with zero warnings/errors; shared DLLs staged into Unity.
- Native suite passed **5,478,535 assertions**, including exact iteration count, stable seed prefixes, nondecreasing best score, earliest ties, whole-plate assignment, detail independence, validation, cancellation and all ten goldens.
- Seed 42 / ten iterations passed repeat, reapply, serialized recipe and cached-result reconstruction at every stage. Final hash: `8f6572fba19acc5bd28d4b8341217b988f4371ae8f4426f13ef44a42acec76c4`.
- Ten candidates were explicitly generated/reviewed in `artifacts/interconnectedness-final` before copying recipe/hash expectations. Original eight scenarios plus ten- and fifty-attempt search cases comprise the corpus. Superseded complex goldens were backed up in `artifacts/pre-interconnectedness-goldens` and removed from current fixtures.
- Unity editor passed the ten-attempt case including all-stage native equality, reapply and snapshot roundtrip (`artifacts/unity-interconnection.log`).
- Browser worker/UI matched every stage of the same seed-42 ten-attempt case. Invalid count feedback, recipe save/load, Continents/Terrain maps and 390px layout passed; screenshots were inspected (`artifacts/continent-browser`). Complex and world-type controls are absent. Full browser corpus and maximum-detail checks were not rerun for this scoped update.
- Rebuilt Windows Mono player matched all stages of all ten current goldens. The actual root shortcut launched and captured a map successfully (`artifacts/shortcut-check-3-15/map.png`). Its target remains `artifacts/player/WorldGen.exe`; Core DLL matches the shared build (SHA-256 `904bbf857f1204ea28159742e28f5ea221484a46391a5378377b856e160f8d0c`).

## Historical continental contact correction — 1 October 2026

Historical algorithm: **worldgen-3.14.0**, schema 3, now superseded. The owner rejected the 3.13 silhouettes and sea-only topology; passing technical checks below did not establish visual acceptance.

- Removed seed-distance territorial clipping. Shapes follow connected spatial-resistance fronts, then receive narrow tapered bridge or strait contacts.
- All six required compositions passed fresh/reapply/serialized-recipe/cached reconstruction and final contact topology at 20k and 5k detail across seeds 0, 42 and 73421. Tests distinguish narrow connections from broad fusion; separate complexes cannot share a landmass. Planned straits must separate primary landmasses, while planned bridges must connect them.
- Identity maps for seeds 0, 42 and 73421 and a seed-42 terrain map were visually inspected. Evidence: `artifacts/continent-correction`. These are implementation review previews, not owner aesthetic approval.
- Fourteen candidates were explicitly generated into `artifacts/continent-correction/corpus`, reviewed for spatial checks and survival (minimum .933433 across the corpus), then copied to `tests/golden/v3`. Previous 3.13 goldens are retained under `artifacts/v3-13-goldens`; the pre-existing user goldens remain in `artifacts/pre-complex-goldens`. Tests never generate expectations.
- Expanded final-seed tests caught an eroded shore attachment in seed 0 / composition `4`. The fix reconnects bridges to their primary bodies through their own crust. All original fourteen native all-stage hashes stayed unchanged. A reviewed fifteenth case, `complex-4-seed-0`, was added explicitly from `artifacts/continent-correction/final-corpus`; final hash `30c045207d7c33b20085ed9ee5fd2b2a40c1cb561c8f1375c6d89a77490b7938`, minimum land survival .974378. Unity editor separately passed this regression (`artifacts/unity-bridge-anchor.log`).
- Release solution/worker build passed with zero warnings/errors and staged shared DLLs into Unity.
- Final native suite passed **6,387,061 assertions**. The eleven-composition, three-seed, five-coverage sweep also checks actual connectivity between seeded primary bodies for every planned bridge/strait, beyond merely checking neck labels.
- CLI Preview repeat/reapply/recipe/cached reconstruction matched every stage. Ordinary final hash: `58ee633c4e8f8cfd707dc1d9782770ffd8e698d15c8f85d615ace6da7e6a6b2a`.
- Custom browser checks passed invalid composition feedback, exact save/load, all-stage CLI equality, identity/terrain maps and mobile layout. Evidence: `artifacts/continent-browser`; `4,1,1` final hash: `2429b2a2f440af321ef01452a9e2ad36691df7026d269ed93e051767c0767c30`.
- Native maximum-detail `4,1,1` check passed again after the attachment fix at 2,560,000 terrain cells: 125,595 ms generation, minimum identity land survival .968770, both spatial checks passed. Globe/map each used 939 bounded chunks (2,777 / 14,698 ms). Final hash stayed `84d5a4d6a22c70c978556d041453c6a217a508ddd94921735776f27d63403780`. This is not a maximum-detail browser test.
- Unity editor matched all ten stage hashes of all fourteen cases, including reapply/snapshot roundtrips. Evidence: `artifacts/unity-validation.log`, `artifacts/unity-editor`.
- Final rebuilt Windows Mono player matched all fifteen all-stage goldens. The actual root shortcut launched successfully, generated a map capture and exited cleanly (`artifacts/shortcut-check-3-14/map.png`, visually inspected). Target remains `artifacts/player/WorldGen.exe`; its Core DLL matches the shared build, SHA-256 `718f0689e1cb38b86dd0473d79923181bae16a6619998ed8e7111352b98f6828`.
- Final rebuilt-player 20k interaction checks passed generation/presentation, cancellation (17.6498 ms), longitude scrolling (16.7745 ms average), resize/north-up and Windows message handling (31 probes, zero timeouts). Maximum busy frame was 18.3042 ms across 622 busy frames. Evidence: `artifacts/unity-ui-v3`.
- Full browser UI suite passed all 24 layers, globe/map coverage, cell colors, overlays, longitude translation, picking, generation cancellation, responsive layout, recipe/result/heightmap exports and reconstruction. Evidence: `artifacts/browser`.
- Chrome .NET worker matched all stages of the fourteen-case corpus (103,627–364,766 ms per case under concurrent checks), then matched the added bridge-attachment regression after rebuilding/restarting the worker (113,243 ms). Evidence: `artifacts/browser-worker/*.hashes.json`, `artifacts/continent-correction/initial-worker-timings.json`. The final native corpus confirms the original fourteen hashes are unchanged by the attachment fix.

Distance diagnostics remain coarse-sampled; fine-cell topology tests supplement them. Maximum-detail browser and non-Windows player checks remain unverified. Historical evidence below is not evidence of current geometry acceptance.

## Historical 3.13 redesign evidence — visually rejected

Historical algorithm: **worldgen-3.13.0**, schema 3. This section does not identify current goldens or current contact behavior.

- Release solution/worker build passed with zero warnings/errors; shared DLLs were staged into Unity.
- Final native core suite passed **6,203,649 assertions**. Spatial sweeps covered eleven ordered compositions, three seeds and five land targets (.02, .12, .3, .65, .8), plus size-variety extremes. All six required examples passed fresh/reapply/serialized-recipe/cached-result all-stage reconstruction and fixed-coarse checks at 20k versus 5k terrain.
- Coarse shapes are connected, have a nontrivial area, and have no broad cross-identity contact. Final checks require every continent's largest connected land component to retain at least 60% of its crust area, and prohibit final fusion between identities. Graph-distance grouping/separation checks pass. Malformed, nonpositive, wrong-length, wrong-sum, missing-composition and excess-complex inputs are rejected; exact composition text survives serialization.
- CLI Preview `--verify --summary` passed every stage for repeat/reapply/recipe/cached reconstruction. Ordinary final hash: `3d7aa0a966773ac6ded10373306965128abce7f878efbd1e56e86b3d45f028c4`.
- The custom browser suite (`scripts/continent-browser-check.mjs`) passed invalid-input feedback, `4,1,1` save/load, all-stage CLI equality, both identity layers and mobile layout. Its maps were visually inspected. Evidence: `artifacts/continent-browser`. The `4,1,1` final hash is `f087f2cb81f4390dce24426458308b618ba0e85478e7ba5c76ccda40197cf8d9`; its minimum retained land fraction is .963906.
- Maximum-detail `4,1,1` native smoke passed at **2,560,000** terrain cells: generation 111,898 ms, minimum continent land survival .971244, and both final spatial checks passed. Globe and map each used 939 bounded chunks; preparation took 2,987 / 16,091 ms. Final hash: `f0c80cee3564c3a762f39312c4ce4184a18db11bc9d56d1c673d8ef8375e1206`. This is a native generation/presentation check, not a maximum-detail browser test.
- Full browser UI suite passed all 24 diagnostic layers, globe/map views, crisp colors, longitude translation, overlays, picking, generation cancellation, recipe/result/heightmap export and recipe reconstruction. Evidence: `artifacts/browser`.
- Chrome .NET worker matched every stage of all **fourteen** golden cases, including all six required complex compositions. Recorded worker generation times were 94,603–155,360 ms with two worker pages. Evidence: `artifacts/browser-worker/*.hashes.json` and `v3-timings.json`; command: `node scripts/worker-check.mjs`.
- Unity editor validation and the rebuilt Windows Mono player matched all ten stages of all fourteen golden cases. Editor reapply and snapshot roundtrips passed. Commands: `scripts/unity-check.ps1` and `scripts/unity-check.ps1 -BuildPlayer -PlayerCheck`. Evidence: `artifacts/unity-validation.log`, `artifacts/player-check.log`, `artifacts/unity-editor`, `artifacts/unity-player`.
- The actual root `WorldGen.exe - Shortcut.lnk` was launched with map-capture arguments, generated successfully and exited cleanly. Its target remains `artifacts/player/WorldGen.exe`; its Core DLL is byte-identical to the newly built shared DLL (SHA-256 `4165645623d39319473d95e2dedf2eff126b43de78197b0484538c571ec7c816`). The captured map was visually inspected: `artifacts/shortcut-check/map.png`.
- Rebuilt-player 20k interaction checks passed: generation/presentation, cancellation **17.7616 ms**, maximum busy frame **19.1888 ms**, longitude average **16.7540 ms**, resize/north-up and Windows message handling. Evidence: `artifacts/unity-ui-v3/detail-20000.json` and `window-20000.json`. Final Release solution build passed again with zero warnings/errors.

Golden review: candidates were generated explicitly into `artifacts/complex-corpus`, reviewed against spatial metrics and structural tests, then copied into `tests/golden/v3`. Fourteen cases now include all six required compositions. Pre-existing worktree goldens were preserved under `artifacts/pre-complex-goldens` before replacement. The water/land stress cases now use .02/.8, since positive requested continents plus required separating oceans rule out all-water/all-land recipes. High area requests can underfill and emit a warning; the .8 stress case achieved .699210 continental crust and .711735 final land. Tests never regenerate their own expectations.

Distance diagnostics use the fixed coarse sampling grid even for final fine terrain; they are macro-scale checks, not exact shoreline surveys. Fine-cell primary-component tests and no-fusion tests supplement them. This implementation uses close seas between complex members, not generated isthmuses. Persistent hydrology remains absent.

Host: Windows x64, .NET SDK 10.0.401, Unity 6000.6.0f1 / URP 17.6.0 (Windows Mono player), Chrome / Playwright 1.58.2. Tests were run on 17 September 2026 local time. Schema 3 / worldgen-3.0.0 replaces v2's incorrect display-only detail interpretation.

## Recorded evidence

- Release solution and worker build passed. Shared Core/Serialization/Presentation DLLs and the MIT triangulation dependency were staged into Unity.
- Core suite: **5,117,170 assertions passed**, eight fixed v3 recipes. Topology and spherical area, connected plates/superplates, per-cell provenance, climate bounds, no persistent hydrology, strict serialization, cancellation, flat cell colors, overlays, map seam/pole coverage and immutable presentation passed.
- Fresh/repeat/reapply/serialized-recipe/cached reconstruction match all **10** stage hashes. Explicit CLI Preview --verify passed again on the final hash implementation (2,199 ms native generation in that run).
- CoarseHash stayed equal when detail changed from 20,000 to 5,000 and irregularity from .75 to .1. Actual fine terrain array length changed; over 500 low-detail sample heights differed by more than 25 m from nearest high-detail samples. This rejects the old display-resampling behavior.
- Default **204,000** native terrain: generation 13,207 ms; globe preparation 239 ms; map preparation 1,274 ms in a recorded run. World land area 38.0154%, height range -7,399 to 6,712 m. All terrain/climate arrays use the fine mesh.
- Maximum **2,560,000** native terrain: generation **165,146 ms**; globe 939 bounded pieces / 46,079,964 vertices in 3,127 ms; map 939 pieces / 46,110,822 vertices in 15,883 ms. Land area 37.6634%, height range -7,328 to 6,823 m. Run: `--detail 2560000 --summary --render-check`. A second run after the output-preserving climate optimization completed generation in **114,213 ms**, globe preparation in 3,484 ms and map preparation in 17,546 ms, with the identical final hash `088007b8ea35548a74361008a1459864f6dcd8b9654f5758abd2ef402812786a`. These verify CPU generation and presentation, not a maximum-detail browser session.
- Unity Windows player built successfully and matched every CLI stage for all eight v3 recipes. Evidence: artifacts/unity-v2/artifacts/player-check.log and unity-player/*.hashes.json. The folder name reuses an existing isolated Library cache; its contents are the newly built v3 player.
- A 20,000-cell Unity Terrain capture completed and was inspected: crisp distinct cell surfaces, no interpolated-color blur. The first capture exposed excessive grid height, subsequently corrected to follow terrain height.

- Unity editor validation passed all eight recipes, every CLI stage and recipe/snapshot round trips in the isolated project.
- The final Unity player passed all eight CLI hash sets after the climate and hash optimizations. The isolated player is artifacts/unity-v2/artifacts/player-final/WorldGen.exe. The same build is staged at **artifacts/player/WorldGen.exe**; all 325 copied files matched their source hashes, and launch/generation/map capture passed from that normal location.
- All eight Unity Globe/Map x Terrain/Satellite/Climate/Heightmap captures completed at 20k terrain and were refreshed after the final pole-grid correction. Visual inspection confirmed crisp cells and straight map meridians/parallels. Additional Terrain/Satellite captures with wireframe, plate borders and a 15-degree grid confirmed plate colors only replace Terrain colors. A regression assertion covers pole-grid alignment.
- 20k Unity interaction validation: complete regeneration and map presentation, cancellation during presentation **17.758 ms**, longest busy frame **19.4396 ms**, longitude frames **16.7282 ms** average. Window resize and north-up checks passed. The actual Unity window answered all **40** Windows message probes, maximum **48 ms**, with zero timeouts. Evidence: artifacts/unity-ui-v3/detail-20000.json and window-20000.json. The probe initially selected an unrelated helper window; filtering for the player window fixed that test defect.
- Unity **2,000,000-cell** interaction validation **passed** generation, cancellation during presentation, full regeneration, map presentation, longitude scrolling and resizing. Across **41,901** busy frames, the longest was **58.3871 ms**; cancellation took **17.7675 ms**; longitude frames averaged **16.7427 ms**. The north-up and resize checks passed. The actual player window answered all **1,359** Windows message probes, maximum **196 ms**, with **zero timeouts**. Peak process working set was **2,611,257,344 bytes**. Evidence: artifacts/unity-ui-v3/detail-2000000.json and window-2000000.json. An earlier run also completed, but the separate probe's Int32 memory counter overflowed above 2 GB; the final successful probe uses Int64 counters.
- Browser full UI suite passed generation/repeat/saved-recipe hashes, recipe/result/PGM exports and mobile overflow. Stricter view checks exposed a test defect: this Playwright version treats an asynchronous wait predicate's Promise as truthy before its result is known. The final check imports the view inspector once and uses a synchronous predicate for the requested projection/layer and completed revision. That final interaction suite **passed** all 23 diagnostic layers, all eight main views, longitude without a new mesh revision, wireframe/plates/grid options, actual cell inspection, mid-generation cancellation with the previous world retained, and the 390-pixel mobile layout.
- Chrome worker: all eight cases match all **10** stages on the final implementation, including the specialized canonical hash writer; recorded times **106,134-153,712 ms** with two worker pages. Packed byte buffers replaced JSON mesh transfer. Evidence: artifacts/browser-worker/*.hashes.json and v3-timings.json.

## Golden review

Candidates were explicitly generated into artifacts/v3-candidate-corpus, inspected for plate count, terrain size, height bounds, land coverage and the structural tests, then copied into tests/golden/v3. Passing tests never regenerate expectations. All corpus cases intentionally use 20k terrain for portable regression coverage; separate resolution tests cover coarse invariance and changing fine terrain, and the maximum-detail smoke exercises 2.56m. V1/V2 fixtures remain historical.

The ordinary final hash is `2ccfec96a725dbabdd174fd226fcb855d17ca3628dd1ff4b7865ed9e4af076fa`. The full set of stage hashes is in tests/golden/v3. Algorithm outputs are intentionally different from v2 because terrain runs at actual detail, the persistent hydrology stage and collision-context route are removed, and the stage hash format is chained.

## River and settlement presentation, 17 October 2026

Presentation-only map-mark revision: R toggles cached river visibility; C cycles
Off, Capitals only, All, Cities only, Large cities only. Default: Capitals only.
Shared renderer geometry uses connected curved strokes with antialiased rounded
caps, shoreline-trimmed mouths and screen-footprint-based width/channel visibility.
It reveals existing recorded tributaries, not newly simulated local drainage.
Settlement lists are filtered once per renderer publication and reused by frames.
Large cities selects approximately the largest tenth, capped at20, with a relative
population floor. Smaller marks and labels appear only at closer zoom.

The Release solution build passes without warnings/errors;24 focused map-mark
assertions and368 combined historical-presentation assertions pass. The browser
check loads an actual saved20k Earth history and passes real R/C keyboard input,
cached switching, shader/protocol checks and all five modes (0/2/36/24/3 marks).
Reviewed full-UI browser captures and native close-ups at three distinct zooms
show smooth thin strokes and uncluttered capital markers. River preparation uses
bounded1024-reach chunks (26 vertices per reach); zoom requires no mesh rebuild.
This is not a new2.56M benchmark or a hydrological/terrain realism validation.
Generation and historical rules are unchanged; existing saves need no upgrade.
The selected IL2CPP player also passes synthetic R/C input through its normal
Update path, preserves the live geometry root and the saved historical hash,
and captures three distinct zooms. Hidden diagnostic players must opt out of OS
focus-based Input System event suppression; this override is diagnostic-only.
Accepted evidence: `D:/WorldGenBuildArtifacts/overnight-20261017/native-map-marks-accepted`
and `browser-map-marks-final` in the same review directory. The project shortcut
now selects `artifacts/player-history-32-map-marks/WorldGen.exe`.

## Structured religion review, 17 October 2026

History32 optional realm31 is implemented in Core, with saved inherited doctrine,
particular denomination/synthesis issues and separate provisioned local public/
lay bodies. Religious authority is not political ownership or public belief.
See HISTORY32_RELIGIOUS_TRADITIONS.md for actual effects and limitations.

Release solution/staged worker/shared-DLL builds pass with0 warnings/errors.
377 focused doctrine/record-update assertions and772 historical-presentation
assertions pass. The latter include both cached and literal eight-overlay
selection/highlight checks.24 actual20k Earth and22 actual20k generated forest
world assertions pass: non-mutating explicit upgrade, particular coercion and
authority counterfactuals, specific paid branching, actual100-year versus37+63
replay, binary world roundtrip, cold12-year continuation, extension-level JSON,
fresh activation/first actor registration, and read-only atlas records.

The first dense CP multi-counterfactual harness attempt exhausted memory and is
not accepted evidence. Full mature-history JSON also exceeded the existing
250MB document limit in the forest fixture; this limit was not relaxed. Full
world persistence is tested in the supported binary format, with JSON scoped to
the new optional extension. A separate low-copy200k Earth compatibility smoke
passes upgrade/validation and12 due years on4951 recorded towns/139 faiths:
7867.93ms continuation, worst computed year702.05ms. That approximately1.53yr/s
CLR observation is not acceptance of the outstanding3yr/s or max-tech+1000-year
performance requirement, nor an all-era religious realism calibration.

The IL2CPP review player builds successfully. Native Religion and Religious
institutions camera captures, actual selection/atlas documents and unchanged
historical hash pass. Both maps were visually inspected. The actual new12-year
native and CLR continuation is exactly
`32d3200dcbecf929365f5f8e52820b6ecc235f06dbb72bec10cdc5f6d273b6d4`.
Unupgraded source30 continuation still matches the frozen preceding native
player exactly (`8166b3c5c4e5b83bbd65a96867354d94b7a7f80a65d7fdcfb11304aaf7118119`).

The browser UI check loads the actual new-rules Earth save and passes doctrine,
observance and institution article navigation, distinct institutional overlay,
correct focused body, unchanged paused time and no JavaScript errors.38 public
institution seats are present. Doctrine/observance and native maps were opened
for visual review. The initial180s loading deadline timed out; a240s retry
passed without a runtime-code workaround.
This is not a browser loading-speed acceptance claim.

Evidence: `D:/WorldGenBuildArtifacts/religion31-review` (Earth, forest,200k,
browser and native subdirectories). Generated outputs stay out of Git.
The selected project shortcut is `artifacts/player-history-32-religion31/WorldGen.exe`;
fresh histories enable it, while existing saves use Esc -> Update simulation rules.

## Practical limits

This is independently written C#, not a source translation or numerically identical World Orogen port. The reference comparison and outstanding parity gaps are in OROGEN_ALIGNMENT.md. In particular, climate is still eager at high detail; editing/import/planet codes and extended export modes are absent. Old recipes are explicitly rejected rather than silently reinterpreted.

Chrome uses interpreted .NET WebAssembly and remains slower than native Unity. Native 2.56m success does not establish browser memory/performance at that detail. Full JSON result export is expensive; the high-detail smoke deliberately uses --summary. IL2CPP, ARM/mobile, macOS/Linux players, Firefox and Safari remain unverified. Timing observations are not guarantees.

Unity cancellation is cooperative. Presentation and project-owned loops check it frequently; an in-flight triangulation or framework array sort finishes before observing cancellation. These operations run on the worker, so the loading UI remains available.

## Reproduce

Use README.md's build/core/CLI/browser commands. For an already-open Unity project use `scripts/unity-check.ps1 -Isolated` and `-Isolated -BuildPlayer -PlayerCheck -PlayerFolder player-final`. Never launch two editors against one project. Automated players use Start-Process -WindowStyle Hidden.

Player flags: --worldgen-detail N, --worldgen-view Globe|Map, --worldgen-layer Terrain|Satellite|Climate|Heightmap, --worldgen-wireframe, --worldgen-plates, --worldgen-grid 0|15|30, --worldgen-screenshot <absolute PNG path>. Run `scripts/unity-interaction-check.ps1 -Isolated -Detail 2000000` for the actual window-message probe. The player harness uses --worldgen-ui-check <absolute JSON path> and exercises presentation cancellation, complete regeneration, map scrolling and resizing while recording frame latency.

Generated binaries, logs, captures, candidate corpora and reference results are ignored artifacts. The supplied reference remains outside this repository and is never modified.

## Review-output cleanup, 16 October 2026

At the owner's request, obsolete historical captures, retired players and
superseded saved test runs were permanently removed from the project artifacts
and the separate build-artifact drives. Earlier review documents retain their
original diagnostic paths as historical provenance; most intermediate outputs
at those paths are intentionally no longer retained. Those outputs can be
regenerated, but are not backed up by source-control checkpoints.

The project `artifacts` directory is approximately0.87GiB after cleanup. The
retained evidence consists of fourteen selected review screenshots, compact
reports, the current reproduction/failed-holdout worlds, the official31 player,
the V review player and the U2 rollback player. User worlds, source, music,
training records and the current shared compiler cache were not deleted.
`scripts/clean-review-artifacts.ps1` previews this reviewed retention manifest
by default; `-Apply` deletes its obsolete outputs. It refuses active players,
unexpected junctions, embedded Git checkouts and newer history-version folders.
It is not automatically run or scheduled.
