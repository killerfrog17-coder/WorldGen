# Paleolithic simulation and survival prototype

Owner scope: 27 October 2026. Gameplay prototype plus an early-history overhaul;
no paid services, no new animal appearance work. Earlier saves retain their
historical rules unless explicitly starting/upgrading the early-history scenario.

Current selected checkpoint: `artifacts/player-survival-storage`, IL2CPP
UTC `2026-10-27T15:53:48.9150230Z`, through the existing project shortcut.
Core checks: 153 survival and 403 hominin/clock/view assertions; 43 native geometry/
persistence checks, an editor mesh-lifetime regression and 20 unload cycles.
The 20,000-year early-history canonical hash matches between CLR and IL2CPP.
Earlier graphical survival review passed 24 functional checks; later changes
have the narrower verification described below, not fresh visual/FPS acceptance.

The owner-authorized cleanup retains the latest three players, latest per local
day, selected and running builds. Six superseded survival players were removed
(12.04 GiB); current latest rollback players are `player-survival-creative-food`
and `player-survival-hominin-atlas`, plus daily checkpoints. Older delivery paragraphs
are chronological records; their player folders may have been purged. Source
archives, review screenshots, native inputs and saves remain.

## Evidence and design decisions

* [Smithsonian stone tools](https://humanorigins.si.edu/evidence/behavior/stone-tools):
  flakes, handaxes, points, scrapers and awls support distinct jobs rather than a
  generic stone tool tier. Tool traditions are regional; dates are not global unlocks.
* [Schöningen wood assemblage, 2024](https://pubmed.ncbi.nlm.nih.gov/38557183/)
  documents sophisticated spears and woodworking. Its formerly used 300 ka age is
  challenged by [2025 redating](https://pmc.ncbi.nlm.nih.gov/articles/PMC12063642/)
  to approximately 200 ka. Do not copy old dates as settled facts.
* [Barnham deliberate ignition](https://www.nature.com/articles/s41586-025-09855-6):
  flint, transported pyrite and burned deposits support firemaking about 400 ka.
  Natural-fire collection, maintenance and deliberate ignition are separate skills.
  Friction fire is a plausible prototype option, not asserted for every early group.
* [Border Cave bedding](https://pubmed.ncbi.nlm.nih.gov/32792402/): grass bedding
  around 200 ka. Bedding reduces ground heat loss; it is not a modern bed frame.
* [Hearths and shelters](https://humanorigins.si.edu/evidence/behavior/hearths-shelters):
  organic shelters are poorly preserved. Windbreaks, brush and hide frames are
  plausible reconstructions. A hide pole shelter is not evidence for a Plains tipi;
  a dug snow shelter is not a claim for Paleolithic masonry igloos. Mammoth-bone
  dwellings are late and regional. Caves require actual terrain geometry, not a
  blueprint that falsely excavates the current heightfield.
* [USDA jerky safety](https://www.fsis.usda.gov/food-safety/safe-food-handling-and-preparation/meat-fish/jerky):
  drying alone does not guarantee pathogen removal. The prototype's preserved
  meat process includes heating. Cooking spoiled meat does not restore safety.
* [Neanderthal hearing/speech study](https://pubmed.ncbi.nlm.nih.gov/33649543/):
  archaic populations are not assumed mute. Speech anatomy is not proof of modern
  syntax. No scientifically established language invention at 80 ka exists.
* [Smithsonian ancestry/admixture](https://humanorigins.si.edu/evidence/genetics/ancient-dna-and-neanderthals):
  disappearance can include assimilation. Biological lineage, ancestry, language
  and cultural identity must remain separate; no ancestry-to-intelligence ranking.
* [Glacial regime transition](https://doi.org/10.1126/science.aat7067): early
  Pleistocene cycles differed from late roughly 100 kyr cycles. Use changing
  regional/seasonal climate envelopes, not a single global temperature subtraction.
* [AWI Quaternary](https://www.awi.de/en/science/climate-sciences/paleoclimate-dynamics/research-focus/quaternary.html):
  long glacial cycles contain shorter fluctuations; ice and ecological responses
  have lag. Geographic region identities persist while habitat cover changes.

## Gameplay abstractions

Default 24 playable solar days per 365-day historical year (365/24 calendar days
per sunrise). Needs, fire fuel, food ageing and wounds use playable physiological
time; seasons/history use compressed historical time. This is deliberate and
does not claim that two physiological days are literally a real calendar month.

Energy, water deficit, fatigue, stamina, core temperature, wetness, bleeding and
illness approximate useful pressures. No complete metabolism, micronutrient
ledger or clinical predictor. Animal meat/fat/organs/hide/bone yields are bounded
fractions of body mass; body condition and carcass age matter more than item caps.
Food composition and lifetimes are coarse gameplay priors, not food-safety advice.

Carrying uses actual equipment, combined mass/bulk and common spatial packing.
List and grid are views of one inventory. Bare hands have finite grip capacity;
there is no arbitrary separate stick/rock quota. Starter carrying kit and hide
waterskin are plausible reconstructions supplied to make the prototype playable.

No unsolicited control text on the gameplay screen. Controls belong in help and
the inventory/build interface. Needs feedback should remain compact and optional.

## Delivery checkpoints

1. Core survival/calendar/inventory and deterministic save tests.
2. Runtime interactions, held tools, hunting, articulated ragdolls, harvesting,
   crafting, fires/camps and both inventory interfaces.
3. Early-history scenario with branching lineages, persistent local traditions,
   glacial habitat responses and regional lithic-stage descriptions; honest limits.
4. Actual packaged play checks, save/reload and performance checks; source pushes.

This document records decisions and sources, not verification that every planned
system has shipped. Implementation evidence is recorded separately as it exists.

## Architecture checkpoint, 27 October

The owner withdrew the overnight deadline after an interruption and prioritised
coherent architecture over additional content. The implementation now separates:

* `SurvivalState`, `SurvivalItems` and `SurvivalCrafting`: serializable game rules,
  physical carrying/packing, needs, transactions, finite carcasses and camp plans.
  List and grid inventory share the same contents and constraints. Buildings
  accept partial deliveries over multiple trips. Food portions preserve mass and
  calories even for small birds and mice. Raw water can be carried and boiled.
* Unity adapters: world interactions, authored human action playback, equipment,
  procedural camp props, hunting and original-skeleton articulated ragdolls.
  Death uses physics rather than the untested death animations. Corpse identity,
  remaining yields, anchor and current bone pose are saved separately from the
  ephemeral wildlife planner. No population/corpse count ceiling was added.
* `HomininHistory`: sparse regional cohorts, branching biological lineages,
  ancestral mixtures, language-descent identities and inherited local traditions.
  The biological and cultural records are distinct. Derived map headcounts are
  reconciled against the authoritative cohorts. Private simulation sessions own
  mutable work; publications and saves detach their arrays.
* `PleistoceneClimate`: changing regional/seasonal climate envelopes, sea-level
  effects on migration and lagged resource/vegetation guilds. Geographic region
  IDs stay stable. Coarse scheduled boundaries are independent of UI request
  sizes; between boundaries only the clock advances.

The survival catalogue currently has 54 items, 34 recipes and 12 camp/shelter
blueprints. These are a broad playable prototype, not a claim that every early
human population possessed every listed technology. The kit/recipe availability
is not yet connected to the selected historical group's learned practices.

### Limits that remain explicit

The early-Homo scenario is a new, separately versioned simulation. Existing
history saves keep their established rules. The new scenario has regional
Lower/Middle/Upper Paleolithic, Epipaleolithic/Mesolithic, Neolithic and metal-age
descriptions; its handoff into the existing detailed later state/city simulation
is not implemented. Population, language and innovation coefficients are
calibrated gameplay priors, not validated evolutionary predictions. In particular,
innovation dates still need calibration across seeds; native copper working is
not equivalent to extractive metallurgy. There is no established language
invention date at 80 ka, and archaic groups are not treated as mute.

Glacial habitat changes currently affect historical resources and migration,
not the rendered terrain, coastlines, individual plant/animal taxa or moving ice
sheets. Rendered tribal NPCs are not yet backed by these cohorts. Basic wildlife
threat/flee behaviour is deliberately simple. Forage pickups are habitat-aware
generic proxies, not a complete botanical edibility/phenology system.

Shelter meshes and held tools are prototype art. Snow shelters do not excavate
the terrain, and caves require future geometry support. Worn hide insulation
works, but fitted visible Paleolithic clothes are unfinished. Generic authored
actions are reused; precise hand/tool contacts, animal contact IK and swimming
remain unfinished. These limits should not be presented as completed systems.

Core verification at this checkpoint: 110 survival/inventory/calendar checks,
315 hominin/climate checks including full compact-world save/resume, 45 existing
human-variation checks and 20 player-camera/save checks pass. Packaged runtime
verification and selected-build details are recorded in the checkpoint log.

The library session loader now accepts persistent survival data up to the shared
document memory budget; the old 64 KB camera-settings guard could reject valid
saves after only a few ragdolls. Both saving and loading enforce the same budget.
An actual library-loader test covers a session larger than the former limit.

### Playable build evidence

IL2CPP build UTC `2026-10-27T11:10:46.5743211Z`, folder
`artifacts/player-survival-prototype`. The previous human-refinement player is
retained. Actual packaged review `artifacts/survival-runtime-final3` passes 24
checks: initialization/actions, camp props, cooking, finite gathering, articulated
death/settling, finite harvest, saved pose restoration and floating-origin rebase.
The elevated-death test preserves an 8 m root offset, covering the placement rule
needed for flying wildlife rather than snapping it to ground at death.

List/grid/crafting/shelter screenshots were visually inspected after allowing
the test window to initialize. Earlier hidden-window captures were blank and are
not visual evidence. The grid is a text-only prototype with some short labels;
the corpse shot is partly occluded by grass. No broad FPS or anatomical-physics
acceptance claim follows from these checks.

Use I or Tab for equipment, switch List/Grid there; B opens camp plans, E gathers
or inspects nearby camps/carcasses/water. Left click strikes and right click throws
an appropriate held weapon. Detailed controls and mode switches are inside
Character & help. `/spawn deer 1`, `/give list`, `/survival`, `/heal` and `/mode
survival|creative|spectator` support prototype testing. No control hints were added
to the normal gameplay HUD.

### Subsequent core architecture work (not in the selected build yet)

Hominin domain version 2 records communication traditions with parent IDs,
founding region/date and disappearance dates, separately from biology and tool
traditions. These are lineage records, not generated vocabularies or proof that
the founding population possessed modern grammar. Version 1 upgrades preserve
its existing IDs and explicitly mark missing ancestry unknown. The legacy domain
hash remains valid; populations and learned practices are not regenerated.

323 core checks pass, including parentage, detached publication copies and
version migration. A compact world generated with the actually packaged version
1 worker was loaded and upgraded by the new code with every cohort and learned
tradition unchanged. Evidence: `artifacts/hominin-research/language-provenance-proof.txt`.
The selected playable build is still the separately verified survival checkpoint.

Reciprocal admixture now exchanges equal represented numbers of people rather
than applying the same fraction twice to different-sized cohorts and a modified
donor. Weighted ancestry and inherited-trait totals are conserved; language and
learned-culture IDs are not replaced by biological exchange. The new tests cover
this distinction explicitly. A current version-2, 5,000-cell Earth seed-42 run
advances 2.8 million years and validates in about 136 seconds at below-normal CPU
priority. This is one full-run check, not a cross-seed realism/performance claim.

### Chronology calibration in progress

That full run exposed severe anachronisms (including pottery hundreds of
thousands of years early). The next rules revision therefore separates broad
Earth-like plausibility weights from the open trajectory. These are explicit
game calibration priors; they are not claims that archaeology determines an
invention date, or that inherited traits predict a culture's capabilities.
The weights modify innovation probability continuously. Local prerequisites,
resources, retention and transmission still matter; there is no global date
that grants a technology to everyone. Open trajectories omit the timing weights.

Research anchors include:

* [Acheulean evidence](https://www.nature.com/articles/nature10372): a dated
  1.76-million-year assemblage, rather than the prototype's 2.55-million-year
  handaxe result. This is an evidence anchor, not an absolute invention ceiling.
* [Hafted spear points](https://pubmed.ncbi.nlm.nih.gov/23161998/): approximately
  500,000-year evidence. Organic woodworking and stone-tipped composite weapons
  need separate treatment.
* [Eyed-needle research](https://doi.org/10.1126/sciadv.adp2887): late Paleolithic
  sewing evidence is distinct from much earlier use of bones as implements.
* [Xianrendong pottery](https://scholars.lib.ntu.edu.tw/entities/publication/166ad619-7b8a-4ba9-a50b-4a406b7ea2e9):
  pottery by foragers about 20,000 years ago; pottery must not require agriculture.
* [Rice exploitation, cultivation and domestication](https://doi.org/10.1126/science.ade4487):
  separate stages in the archaeological record, rather than a single instant
  switch from foraging to fully domesticated crops.
* [Early goat management](https://pubmed.ncbi.nlm.nih.gov/10731145/): selective
  herd management around 10,000 years ago; this is not equivalent to an early
  association with dogs or occasional capture of wild animals.
* [Extractive copper metallurgy](https://www.sciencedirect.com/science/article/pii/S0305440310001986):
  evidence around 7,000 years ago. Cold-working native copper is a separate skill
  and does not by itself classify a culture as Chalcolithic.
* [Early tin-bronze evidence](https://discovery.ucl.ac.uk/id/eprint/1420706/):
  sporadic early alloy production differs from widespread Bronze Age technology.

Model coefficients in `HomininChronology` are tuning parameters, not numbers
copied from these papers. Perishable techniques have especially uncertain
histories. Rules versions are saved independently of the record schema: version
1 playable checkpoints retain their old rules when communication records are
added; pre-policy version 2 files retain rules 2. Chronology changes begin in
rules 3; the lineage-lifecycle follow-up below introduces rules 4.
Native copper and extractive smelting are separate flags, and new-rule bronze
requires smelting. Existing histories are not retroactively rewritten. The
selected playable build still predates this calibration work.

The first weighted run exposed two implementation problems beyond rate tuning.
Legacy random rolls used 24-bit values including exactly zero, so an extremely
unlikely positive hazard could still fire after quantization. Rules 3 use an
open-interval 32-bit roll for these rare events; established random algorithms
are unchanged. A regression reproduces the actual premature prepared-core event
(seed 42, stream `lithic-innovation-256`, entity 3516219). Rules 1/2 retain their
recorded random behaviour.

The old sedentism calculation also made some annual-plant habitats unable to
establish recurrent camps before they had farmed, while farming required camps.
Rules 3 allow concentrated fishing resources, storage, plant tending and local
pressure to support recurrent camps before cultivation. This represents an
opportunity, not a mandatory village or a completed settlement simulation.

The corrected seed-42, 5,000-cell Earth run validates after 2.8 million years in
95.638 seconds at below-normal CPU priority. First recorded needles are about
51.8 ka before reference, pottery 20 ka, herding 11.1 ka and cultivation 10.25 ka.
Native copper, smelting and bronze remain distinct; their first records in this
seed are 9.15, 4.75 and 3.15 ka. These are simulation outcomes, not substituted
historical dates or a cross-seed certification. Evidence is retained under
`artifacts/hominin-research/rules3-corrected*`; a separate seed-17 check follows.

The seed-17 check also completes and validates (140.457 seconds at below-normal
priority): first needles 45.8 ka, pottery 20 ka, herding 10.5 ka, cultivation
9.3 ka, native copper 9.6 ka, smelting 3.8 ka and bronze 3.1 ka. Metallurgy is late
in these two probes; the broader model still needs calibration. Two runs are not
validation of every world or a scientific prediction.

Rules-3 IL2CPP package UTC `2026-10-27T12:19:22.6394039Z` is
`artifacts/player-survival-history`. It passes ten focused native, no-graphics
checks of early-history creation, compact save/hash/rule preservation, fresh
engine resume and a survival session with multiple saved physics poses larger
than 64 KB. Core checks: 110 survival, 348 hominin/climate, 45 human variation,
20 player-camera/save. The 177 packaged fauna hashes match. Runtime survival,
rendering, skinning and ragdoll source files are unchanged from the visually
checked survival build; history startup/UI and the diagnostic entry point differ.
The existing shortcut is being moved to this checked follow-up; both earlier
players are retained. Native evidence: `artifacts/survival-history-native-final`.

### Local clock publication follow-up

A direct probe of the populated 1,779-cohort state measured about 7.22 ms and
6.48 MB allocated per detached snapshot on the CLR. The previous local loop
requested a publication after 60 historical seconds. With a one-hour solar day
and 24 solar days per historical year, that threshold is about 0.164 real seconds,
despite the next early-history demographic event being many years away.

The follow-up keeps fine time in the already saved PlaySession credit and waits
for an actual early-history event before publishing that domain during gameplay.
Leaving local play flushes empty elapsed years in one publication rather than
one copy per day. Calendar/season time still includes the retained credit. Public
simulation snapshots continue to detach their data; mutable records are not
shared with private simulation work as an optimization. Legacy local budgets
are preserved. Event-date labels also now use the event's year rather than the
current state's date. 357 hominin/clock checks pass, including saved credit,
calendar-offset conservation and an event-boundary slice; native checking of
this follow-up is in progress. This is not a measured universal FPS claim.

Final clock build UTC `2026-10-27T12:45:56.8764890Z`,
`artifacts/player-survival-clock`, passes 12 native no-graphics checks, including
ordinary-play clock deferral and a multi-year empty-time flush. 358 hominin/clock
checks pass; direct exploration of a prepared world still requests population
initialization. Package DLL inputs and all 177 fauna bundles are verified.
Survival/render/skin/ragdoll runtime sources remain identical to the visual
checkpoint. The shortcut will select this follow-up, with earlier players kept.

### Later-history handoff requirements

The handoff is still unfinished. A future versioned adapter must conserve
population totals, retain extinct as well as living identities, preserve
communication parentage and biological mixtures, and map learned practices and
local subsistence intensity rather than generating replacement cultures. Isolated
archaic cohorts need a continuing demographic owner. Existing
`UpgradeToLivingWorldHistory` calls `InitializeFreshHumanVariation`; it cannot be
used unchanged for this migration. Clock/schema conversion, ancestry provenance
and the existing later-history invariant checks should precede enabling that
handoff. None of this is implied by the current regional stage labels.

### NPC provenance interface

`HomininActorSampler` is a source-only boundary for future NPC creation. Build
one detached sampler per demographic publication, then sample an occupied cell
with a stable individual seed. Local cohorts are weighted by their represented
headcounts; a sapiens-only request returns unavailable if none live there. The
result records the world/history hashes, year, population, lineage, ancestry
mixture, learned tradition, communication identity and inherited appearance.
Persist that result when creating the actor rather than resampling it every
time history advances. Returned data do not alias the sampler or source state.

This does not create demographic people, tribes, language dialogue, archaic body
models or new rendered humans. The existing wandering NPCs are unchanged. The
interface and its tests were initially source-only after the clock package;
368 hominin/climate/clock assertions pass, including 1:3 cohort weighting,
explicit lineage filtering, deterministic sampling, isolation and JSON
round-trip. Ancestry and learned culture remain separate identities.

### Inventory transaction follow-up

Two save-integrity defects were found during review. A valid manually arranged
bag can defeat the greedy compactor; a failed packing attempt now leaves the
original slots untouched instead of publishing a partly rearranged, overlapping
layout. Exhausting a carcass yield also clamps subtraction round-off to zero,
preventing a tiny negative remainder from rejecting the next save. Both have
reproducing regressions; there are 115 survival checks.

Final IL2CPP build UTC `2026-10-27T13:13:44.0904970Z`,
`artifacts/player-survival-inventory`, passes 17 native no-graphics checks,
including these two regressions, NPC-profile serialization, clock publication
and survival save/load. The NPC interface is compiled but remains unused by
current wandering actors. All 177 fauna bundles and original-rig sidecars match
their package inputs. Rendering, human/animal animation, ragdoll and survival
adapter sources are unchanged from the visual checkpoint. Existing shortcut
selects this follow-up; the clock and previous players remain for rollback.
Owned build/test workers have exited; no owner applications were closed.
Evidence: `artifacts/survival-inventory-native-final` and
`artifacts/survival-inventory-package-proof.json`.

### Sprint recovery follow-up

Holding sprint at the old 5-point cutoff could alternate sprint/jog every frame
as a fraction of stamina returned. Exhaustion now remains latched until a useful
reserve returns (normally 25 points, adjusted for a severely reduced maximum).
That recovery state persists in saves and resets on respawn. Creative movement
ignores survival exhaustion. Footsteps/debug gait also respect the sprint gate
and crouch; no animation assets or camera-facing rules changed.

Selected build is now `artifacts/player-survival-endurance`, IL2CPP UTC
`2026-10-27T13:24:18.9225530Z`. It passes 21 native no-graphics checks; core
survival checks are 120. All 177 fauna bundles/rig sidecars and staged DLL inputs
are verified. This is logical/native validation of the gait gate, not a new
visual animation or FPS certification. Earlier packages are retained. Evidence:
`artifacts/survival-endurance-native-final` and its package proof JSON.

### Biological lineage lifecycle audit

The rules-3 seed-17 run contained one living population assigned to a lineage
already recorded extinct. When another ancestral cohort became sapiens-like,
the descendant lookup could reuse its extinct sibling's record. Rules 4 only
reuse an extant descendant and otherwise record a new one; validation rejects
living cohorts attached to extinct lineages. Existing rules 1/2/3 retain their
recorded behavior. Regression checks reproduce the legacy case and verify the
new descendant, unchanged old extinction date and validation failure.

The full 2.8-million-year seed-17 rules-4 run completes and validates: 1,806
cohorts, 64 extant biological lineages and no living/extinct contradiction.
First recorded technology dates match that seed's rules-3 probe. The run took
68.863 seconds at below-normal priority; the difference from prior wall times
is not evidence of a general speedup. Evidence is
`artifacts/hominin-research/rules4-lifecycle-proof.json`.

This audit also exposes a calibration limit: rules-3 seed 42 still has about
471,202 archaic-classified people at reference time, and seed 17 about 212,783;
roughly 157,864 and 107,536 respectively share regions with sapiens-like cohorts.
The identity fix does not make this a realistic extinction distribution. Coarse
lineage branching, persistent co-resident cohorts and assimilation need further
work before claiming the intended mostly isolated late archaic survival. No
global extermination rule or arbitrary forced end date has been added.

The next demographic revision needs mechanisms rather than a species-specific
death multiplier. [Vaesen et al. (2019)](https://journals.plos.org/plosone/article?id=10.1371/journal.pone.0225117)
show that small-population effects can produce extinction without assuming a
cognitive disadvantage; this is a sufficiency model, not proof of one universal
cause. [Li et al. (2024)](https://pubmed.ncbi.nlm.nih.gov/38991054/)
support recurrent gene flow and a role for assimilation.
However, [Bossoms Mesa et al. (2026)](https://www.nature.com/articles/s41586-026-10625-1)
find no close-relative mating signature or rising genetic load in their late
northwestern sample, and no evidence of recent modern-human gene flow there.
Regional histories differ; a universal inbreeding-collapse rule would ignore
this evidence.

Architecture inference: distinguish breeding/contact networks from total
ecoregion headcounts before applying mate-finding or small-band risk. Merely
sharing a huge geographic region should not imply universal contact or mixing.
Future integration must retain biological ancestry while allowing distinct
demographic lineages to disappear, and keep learned culture/language separate.
Those demographic mechanisms are not implemented by the lifecycle bug fix.

Selected lifecycle build: `artifacts/player-survival-lineages`, IL2CPP UTC
`2026-10-27T13:34:45.1043466Z`. All 22 native no-graphics checks pass, including
the extinct-child fixture and compact persistence of rules 4; 372 hominin tests
and 120 survival tests pass. Package DLLs, all 177 fauna bundles and 177 original
rig sidecars are verified. No graphics changes beyond the previously recorded
movement gate; no new render/FPS claim. Older selected players remain available.

### Consistent local time suspension

Survival physiology already stopped in chat, ecology inspection and while the
window was unfocused, but calendar credit did not share those conditions. A
single `LocalTimeRunning` gate now controls both. Existing menu/music controls
remain separate. Focus/suspend callbacks reset the local wall-clock sample to
avoid counting an inactive interval on return. Overview simulation behavior and
the diagnostic focus bypass are retained.

Selected build: `artifacts/player-survival-time-gate`, IL2CPP UTC
`2026-10-27T13:48:43.4725414Z`. The 22 native persistence checks still pass;
package DLLs and 177 fauna/rig-sidecar inputs match. The shared gate is compiled
and source-reviewed; this follow-up did not automate interactive focus changes
or repeat graphical acceptance. Evidence is under
`artifacts/survival-time-gate-native-final` and its package proof JSON.

### Physical hunting reach

Camera aim now chooses a hit point, while actual player-space 3D distance decides
weapon reach. The previous horizontal planet-distance test could accept targets
too far above the player, while the camera-ray limit made third-person reach
inconsistent. Terrain obstruction is tested from the player's upper body (originally the
eye in this checkpoint). Throws start there, aimed toward the camera's target, and consume the held
stack rather than a different stored copy. Precise hand release/contact remains
unfinished. Death cleanup now runs even when the shared clock gate is stopped.

Selected build: `artifacts/player-survival-reach`, IL2CPP UTC
`2026-10-27T13:59:11.6533108Z`. All 25 native checks pass, including nearby reach,
overhead rejection and a near target aimed at from a third-person camera 8 m
behind the player. Package DLLs and 177 fauna/rig-sidecar inputs match. These are
geometry/persistence checks; the changed throw appearance awaits owner review.
Animal source meshes, skins and animation clips remain unchanged.

### Streamed camp mesh ownership

Procedural cover meshes now belong explicitly to their streamed camp object,
so unloading the camp releases them while keeping shared primitive assets.
This follows [Unity's mesh ownership requirement](https://docs.unity.com/en-us/engine/6000.5/script-reference/unityengine/meshfilter/mesh).
An editor regression reproduces the original retained allocation, then checks
20 unload cycles with generated meshes freed and borrowed primitive meshes kept.

The same audit reproduces zero normals from the old opposite-winding triangles
sharing vertices. Cover materials already render both sides; a single face now
keeps valid normals without changing panel vertex positions. Native checks
verify the resulting normals. The current mesh-lifetime package passes all 26
checks and preserves 177 fauna bundles/rig sidecars and staged DLL inputs.
No new graphics or general memory/FPS benchmark is implied. Evidence:
`artifacts/survival-mesh-lifetime-check.log`,
`artifacts/survival-mesh-lifetime-native-final`, package proof JSON, and
`artifacts/hominin-research/rules4-clr-native-proof.json`.

The cleanup script now also retains the latest three, refuses to run during a
project editor/build, protects nested running workers and writes separate audit
manifests even on failure. Final applied cleanup:
`artifacts/daily-cleanup-20261027-143000-084.json`.

### Small-prey biomass and vitality

Carcasses now retain their positive recorded mass, including insects, rather
than forcing a 10 g minimum. The derived firefly uses an authored 20 mg proxy
instead of the unknown-animal fallback. Fractional food conserves the resulting
tiny meat quantities and remains valid saved data. Composition is still generic;
bird/fish/reptile-specific products and anatomical butchery remain unfinished.

Gameplay vitality now scales with body mass without an 18-point adult-sized
base. Mouse-sized prey can die from a strike and rabbit-sized prey from a spear;
the 100 kg deer retains its previous 68-point threshold and the wooden/stone
spear distinction. This is a game balance proxy, not anatomical injury modeling.
Physical reach now originates around the upper body so short tools can reach
ground-level prey; exact hand contact/release remains future work.

Selected build UTC `2026-10-27T14:45:55.8694366Z`,
`artifacts/player-survival-small-prey`, passes 30 native checks and 126 core
survival checks. Native tiny portions conserve mass and save/reload, and ground
reach passes. The 20 ka hominin hash remains CLR/IL2CPP-identical. Package DLLs,
177 fauna bundles and original-rig sidecars match. No new visual/FPS claim.
Evidence: `artifacts/survival-small-prey-native-final` and package proof JSON.


### Early-history atlas and ownership boundary

The early Homo state now has its own read-only atlas adapter. It exposes the
recorded regional populations, biological lineages, learned traditions,
communication identities and practices. These categories replace the later
city/state directories only while an early-history scenario is active. The
browser receives a scenario flag and uses the same layout policies; it does
not copy or reconstruct the demographic model in JavaScript.

Population articles link their actual three identities and geographic region.
Lineages retain biological ancestry and extinction records; traditions show
cultural descent and capabilities; communication records show only recorded
parentage. Old saves with unknown language origins remain explicitly unknown.
The timeline reads the actual event archive. An extinction event's default
tradition field is not treated as evidence that tradition zero caused it.
Older rules can contain a living population with an extinction date: the atlas
shows that contradiction rather than silently modifying the saved history.

Population, tradition and communication totals reconcile to the same published
headcount. Table construction builds its totals once per requested directory;
ordinary cell selection uses the existing publication and does not recount
world populations each frame. Returned documents have no mutable references
into the history state. There are 403 focused history/climate/clock/view checks;
both browser projects compile without warnings or errors. Native packaging and
inspection evidence are recorded after the package is verified below.

The architectural boundaries for the next integration remain explicit:

| Owner | Current contract | Next connection |
| --- | --- | --- |
| Core history | Versioned immutable publications, stable region IDs, lineage/tradition/communication identities | Breeding/contact networks and a conservative later-history handoff |
| Historical climate | Regional climate/resource envelopes and lagged habitat estimates | Rendered vegetation/ice/coast adapters; do not alter terrain heights from vegetation |
| Actor creation | Detached `HomininActorSampler` result with provenance, ancestry and inherited appearance | Persist once when making an actor; no resampling existing people on each publication |
| Survival | Saved needs, inventory, construction, corpses and playable-time quantities | Historical knowledge restrictions and social ownership after actor provenance is connected |
| Runtime presentation | Loaded models, poses, physics bodies and streamed props | Reconstruct from saved domain records; unloading a visual must not delete a camp or carcass |
| Atlas | On-demand documents and independent identity links | No demographic updates, invented cities, language grammars or NPC tribes during inspection |

In particular, connecting early history to the later city simulation must be a
new versioned, headcount-conserving transfer. Calling the existing fresh-human
initializer would lose inherited ancestry and invent cultures. That handoff is
not implemented or advertised as complete. The current demo humans likewise
remain demo actors, not the historical population census.


Atlas package verification: `artifacts/player-survival-hominin-atlas`, IL2CPP
UTC `2026-10-27T15:36:36.1414426Z`, passes all 36 native no-graphics checks.
Copied native world/population articles contain the recorded identities,
headcounts and before-reference dates, with reachable recorded-history sections.
The 20,000-year state and domain hashes still match the CLR control exactly.
All four staged DLLs, 177 animal bundles and original-rig sidecars match their
inputs. Existing shortcut selects this build; small-prey and other recent/daily
players remain. The package is not fresh graphical or FPS acceptance. Evidence:
`artifacts/survival-hominin-atlas-native-final` and package proof JSON.


### Creative food creation

Creative crafting bypasses ingredients, so it must not inspect an absent food
stack to determine the output portion. It now creates a full portion; Survival
mode retains the consumed portion's size. All food recipes are exercised from
an otherwise ingredient-free Creative inventory. There are 136 survival checks
and 38 native checks. IL2CPP checkpoint UTC `2026-10-27T15:44:07.2598576Z`,
`artifacts/player-survival-creative-food`, preserves the early-history hash,
177 fauna bundles/rig sidecars and four staged DLL inputs. Existing shortcut
selects this build; the atlas checkpoint remains available. No new visual/FPS
acceptance is claimed. Evidence: `artifacts/survival-creative-food-native-final`
and package proof JSON.


### Shared inventory and camp-transfer rules

`SurvivalStorage` owns carrying-to-cache, cache-to-carrying and dropped-item
transactions. Runtime menus call this Core interface instead of editing both
inventories independently. Transfers preserve age, condition and fractional food
portions. Trial packing must succeed before either side is published. Invalid
or stale entry references, foreign camp objects and full hands leave the whole
saved state unchanged. Taking the final dropped item retires its saved site;
streamed visuals clean up objects whose saved site has gone away.

New site-local entries use the largest remaining ID plus one, preventing the
old length-based allocator from colliding after an entry was removed. Taking
uses the actual entry reference, so old saves with duplicate site-local IDs do
not need an invented migration. Worn and pristine items cannot silently merge
and repair one another. Existing corpse/blueprint identities and save schemas
are unchanged. The focused suite now has 153 survival assertions; native package
verification is recorded after the build finishes.


Storage package UTC `2026-10-27T15:53:48.9150230Z`,
`artifacts/player-survival-storage`, passes all 43 native checks. Original fauna
bundles/rig sidecars, staged DLL inputs and CLR/native early-history hash are
verified. The shortcut selects it. No fresh graphical/FPS acceptance is claimed.
Native evidence is under `artifacts/survival-storage-native-final` and its package
proof JSON. Four further superseded players (8.03 GiB) were purged under the
same rule, retaining current/latest-three/daily/running builds, saves, native
inputs and review evidence; manifest `daily-cleanup-20261027-155506-548.json`.
