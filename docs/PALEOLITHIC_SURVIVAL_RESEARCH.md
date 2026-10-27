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
