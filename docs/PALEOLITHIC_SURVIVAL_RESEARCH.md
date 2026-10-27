# Paleolithic simulation and survival prototype

Owner scope: 27 October 2026. Gameplay prototype plus an early-history overhaul;
no paid services, no new animal appearance work. Earlier saves retain their
historical rules unless explicitly starting/upgrading the early-history scenario.

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
