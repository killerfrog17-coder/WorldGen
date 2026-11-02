# Paleolithic simulation

New Unity simulations start in `prehistory-7 / paleolithic-2`, about 200,000
years before the present. Existing historical saves retain their original
rules. The rollback tag is `pre-paleolithic-overhaul-20261102`.

The illustrated owner plan is [WorldGen - Paleolithic Simulation Plan.pdf](<reports/WorldGen - Paleolithic Simulation Plan.pdf>),
reproduced by `scripts/build-paleolithic-sim-plan.py`.

## State and time

`PrehistoryState.Paleolithic` owns the new sparse communities. The old farming,
urban, state and industrial engines are bypassed, rather than allowed to run
behind an artificial era label. Homo sapiens has the cultural/knowledge engine.
Separate warm- and cold-adapted archaic human lineages have lightweight regional
population ledgers, independently seeded in suitable reachable ecological patches.
These are procedural analogues, not fixed Earth species or real-world maps.
Species IDs are separate from culture, language, appearance and behaviour.

`SimulationTime.Seconds` and `SimulationSession` are shared with gameplay.
There is no second day/year conversion. Every 50 simulated years a deterministic
decision boundary updates demographics, routes and learning rates. Between
boundaries, knowledge and speech follow analytic continuous curves. Public
checkpoints are detached; view/query order never changes outcomes. Clock-only
updates retain their map-domain identity to avoid recurring border rebuilds.

50 years is a scheduling interval, not a language/clothing unlock. Effective
population and adoption can be queried between boundaries. Language changes
retain an older profile and spread through stable band cohorts over 1,000
simulated years instead of replacing everyone's speech overnight.

## Ecology and expansion

Existing connected ecological patches and route geometry remain authoritative.
An ancestral area favours warm, viable savanna/grassland with the existing
geographic origin preferences. A founding regional community represents bands
of already-established sapiens; it is not a single founder couple or a simulated
speciation event.

Glacial forcing interpolates broad dated cold/warm states. Temperature response
is stronger towards the poles; moisture and sea level change too. Actual route
depth thresholds govern exposed shelves. Deep ocean never becomes a land bridge;
raft knowledge permits only short sea crossings. Winter exposure, inherited
clothing/fire techniques, forage capacity, crowding and distance affect migration.
Climate can cause retreat and local extinction as well as expansion. Neighbour
contacts use the same accessible route network as migrants.

The migration model's sea level is **not** a change to rendered ocean height.
Region IDs, terrain, immutable fauna packs and vegetation geometry are untouched.
Gameplay automatic weather receives the temperature/moisture anomaly and a
consistent rain/snow repartition. Full visual ecosystem replacement and physical
ice-sheet terrain are not implemented by this pass.

## Knowledge and local bands

The 24-technique DAG covers basic flaking/core work/scraping, wooden spears,
hafting, fire, hide work, bindings, wraps, friction fire, compound adhesives,
bone tools, sewing, fitted/insulated clothing, pressure flaking, bows, barbed
points, lines, rafts, needles, spear throwers and preservation. Numerical research,
teaching and loss rates are explicit gameplay priors, not archaeological dates.
There are no agriculture, metallurgy or writing entries.

`PaleolithicRules.Adoption` respects prerequisites and interpolates between
decisions. Local discovery depends on practice prerequisites, population and
need; neighbours transmit knowledge, migrants carry it, and very small isolated
populations can lose practice. Knowing a technique is separate from owning
materials or equipment. The existing item/crafting systems retain their material
requirements; this does not silently assign a player's personal tribal knowledge.

`Prehistory.PaleolithicBandAt(state, cell, localBand)` returns a stable identity,
regional parent, technique access, continuous practice, speech capacities,
related dialect and inherited appearance sample. Coherent seeded cohort ranks
turn prevalence into repeatable local access. A 90% share represents approximately
90% of samples, not a fresh 90% coin flip on each visit. This is the integration
contract for future real NPC tribes; it does not spawn those tribes yet.

## Language

The existing language generator supplies inherited roots, sound systems, word
order and bounded borrowing. New Paleolithic v2 branches use bounded regular
chain shifts, local lexical replacement and rare grammatical changes. These
preserve differences through long histories rather than exhausting the old
finite one-way sound-merger list and converging everyone to identical speech.
The previous Paleolithic v1 language evolution is retained for existing saves. New speech capacities represent
vocabulary, sequencing, displaced reference and clause combination. These evolve
gradually, with contact influencing development. Simple utterance generation
demonstrates the distinction; finished dialogue/audio is not included.

Early expressive limits are an explicitly hypothetical game scenario. They are
not a reconstruction of early sapiens grammar, a claim that human language began
on a known date, or a ranking of living cultures. All language profiles are oral;
the old generator's writing scaffold grants no literacy.

## Observer and compatibility

The map uses the existing in-world Year 0 calendar, with no BC/BP epoch.
The observer era is labelled Paleolithic. Culture, language/dialect, technology
and human-species overlays have independent identities, colours, legends and
inspection cards. State/territory/province/civilization layers remain empty
until actual institutions exist; other unmodelled identity layers are also empty.
Technology colours count techniques established in at least half the local bands;
the existing technology panel and atlas show all 24 adoption percentages and
prerequisites. This does not turn those percentages into global on/off unlocks.
Human-species ranges use independent saved populations, gradual climate-sensitive
demography and shared-food pressure. Overlap has a mixed colour and lists every
resident lineage; no sapiens-arrival replacement or calendar-forced extinction.
New worlds seed them outside the initial sapiens patch through accessible routes.
Old Paleolithic saves without the optional range ledger keep their original
hash and behaviour; their maps honestly contain only their recorded sapiens.
No new bodies, tribal AI or dialogue are implied.

The map also exposes climate anomalies and local adoption percentages. Ecoregion inspection includes a dynamic
sapient-fauna section sourced from the population state; no duplicated static
human spawn range is added to the immutable ecological pack.

Old saves are not automatically converted. The exact 200,000-year legacy
continuation was compared with the untouched pre-overhaul packaged Core assembly.
The older broad prehistory suite still fails its existing city-emergence
expectation; that suite is not reported as passing. New focused tests cover time,
determinism, continuity, geography, local sampling, ownership, saves and ceilings.

## Research

- African population structure: https://www.nature.com/articles/s41586-023-06055-y
- Glacial cycles: https://www.nature.com/articles/nature19798
- Multicamp learning: https://pubmed.ncbi.nlm.nih.gov/32158935/
- Language-origin hypothesis: https://doi.org/10.3389/fpsyg.2025.1503900
- Hide-working tools: https://doi.org/10.1016/j.isci.2021.102988

- Human coexistence and interbreeding: https://humanorigins.si.edu/evidence/human-fossils/species/homo-neanderthalensis

Archaic thermal envelopes, initial dispersion, density and competition rates are
explicit procedural gameplay priors. They are not empirically fitted population
estimates or claims about the cognitive capabilities of extinct human groups.
