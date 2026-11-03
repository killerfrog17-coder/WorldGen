# Paleolithic rebuild: evidence and acceptance

The older engines and results below are historical diagnostics. Current worlds
use `foragers-regional-3`. Backward compatibility is not a requirement; Git
provides rollback. Source consistency alone does not establish realistic outcomes.

## Current regional architecture correction (3 November)

The detailed global community ledger and the synchronized `regional-1` sweeps
were rejected. The current model keeps broad regional history and generates
local detail when queried.

- One population aggregate per connected ecoregion, with regional species
  proportions, shared cultural profiles and technology-adoption distributions.
- Regional speech/belief shares use1% precision rather than retaining traces of
  every historical migrant. Finer camp/dialect variation is procedural detail.
- Each region schedules its own independently seeded events: population200–400
  years, technology150–350, migration200–600, language300–900, traditions200–800,
  ecology700–1,700 and culture300–900. First events have independent random phases.
- A routine event uses direct regional lookup and relevant adjacent routes.
  Scheduling uses a persistent O(log E) heap. It does not scan the world.
- Regional migration merges population into its destination aggregate. It does
  not append a new tribe record. Profiles remain bounded by the region count.
- Camps, households, residents and seasonal positions are deterministic local
  queries. Calling them does not add global actors or histories.
- Fine calendar updates share frozen regional data. Full canonical hashing is
  lazy and requested for integrity/save checks, not every date publication.
- In the managed Windows player the shipped CoreCLR worker executes the same
  simulation and returns typed binary regional changes. Immutable geography and
  species provenance are not resent on each update. There is one authoritative
  history; local NPCs are not a second global simulation.
- GPU cell-to-region lookup is built once. Visible map updates change a small
  regional colour table rather than reconstructing200,000-cell overlay arrays.
- Broad ecology tracks resource stocks and forest/climate context. Detailed
  local species lists are derived from native realm, current climate and stocks.

The current200,000-cell fixture has2,312 connected regions and562 population
records after190,000 years. Its23 checks cover event locality/staggering, exact
initial and delta transport, identical257-year continuation, bounded records,
current save/read and deterministic local generation. Worker transport preserves
the numerical trajectory. Regional-3 affiliation coarsening intentionally changes
the approximation; it is not presented as an unchanged regional-2 history.

The selected managed player measured13,031 actual years/second over12.09 seconds
on this200k fixture. Maximum committed-update gap was243ms; five map switches
took at most4.63ms. Switching from fast playback back to one day/second produced
six game days in6.004 seconds. No runtime errors were reported. The batch190k
history separately took9.87 seconds; these are different measurements.

Fast map presentation runs with a60Hz redraw budget so it does not consume CPU
and shared-cache bandwidth at hundreds of FPS. The history worker is independent
of that redraw rate. Calculation overlaps decoding of the preceding committed
update; pause/speed changes finish at a bounded event boundary. Gameplay display
preferences are retained. Paleolithic Auto requests20,000 years/second.

Foragers is the authoritative regional state. Legacy population/culture cell
arrays are not rebuilt on every event; adapters use ForagerMaps/ForagerSampling.
World generation, saves, map summaries and rare family-registry maintenance still
scale with their data. Routine entity handlers never scan all geographic cells.

Actual player acceptance requires at least10,000 measured years/second at200k
cells, map switches under200ms, publication gaps under250ms, a working one-day/
second clock and no runtime errors. Batch throughput is reported separately.
Earlier416/1,000/322/4,823-year/second attempts failed; they are not acceptance.

The current recorded acceptance, exact timings, package hashes and selection are
in `assets/verification/regional-simulation-20261103.json`. Do not infer current
acceptance from old tests below. No unrelated gathering/crafting/corpse tests are
part of this performance repair. Physical NPC camp integration remains separate.

## Rejected detailed-ledger review (3 November)

### Further outcome correction: demand-led training (version5)

Owner clarification: later eras should overlap regionally. Mesolithic development
must not wait for every community to master every Paleolithic technique, and
modest overlap near transitions is acceptable. This review prioritises believable
development before that transition; it does not require all43 methods everywhere
by the final date. A future optional world-era cap is separate from local progress.

Version4's100k review exposed a second material failure: almost every group
approached perfect trained prevalence in nearly every method. Actual end use
varied, but that did not satisfy the requested variation in making capability.
The190k version4 run was stopped as a failed diagnostic at113k metrics; its last
complete110k snapshot is retained, and it is **not a completed190k acceptance run**.

New version5 separates hearing of a method, maintaining trained makers and
using their products. No automatic competence grows from familiarity alone:
instruction or a genuine innovation must supply makers. Recruitment and its
target prevalence depend on actual demand, materials and practice. Small unused
maker stocks can disappear while oral familiarity persists. Shared production
lets non-makers use goods; winter preparation and travel use the same distinction.
The four-consumers-per-maker coverage scale is a coarse game prior, not a measured
universal production rate. Versions2/3/4 retain their own numerical rules.

Focused50 mechanisms and13 temporal/save checks pass, including sustained useful
invention, loss without automatic reinvention, rare expert teaching, consumption
without resident craft expertise, and a real save carrying more users than makers.
Equal-teacher clothing fixtures end with about3.5% trained makers in the warm
habitat versus77.9% in the cold habitat. A full250-year continuation of the old
version4 world at110k is identical under its frozen engine and the current
version5 reader/engine: `25d836f3500fd62ba98ade292ea5ca500ddf1e2ba5681009ef6eafe38cdd593c`.
The old version3 full190k world also retains its exact37-year continuation.

Current candidate21 is a managed non-development build from `d89993d`, built
2 November23:33:44 UTC. Earth30k, Archipelago30k and Continents190k current-model
histories are complete. Current packaged views at Year0, Continents40k/100k/
180k/190k and Archipelago30k pass222 checks, including real live playback and
local100m camp queries. The same package passes32 gameplay/nature checks.
The final253MB save passes33 lossless world/compatibility checks. The final
external37-year packaged/CoreCLR continuation is exact:
`6bf25d5ba8ae49166b5c0ded1c9ae88372026a2aabefecc6ae714fa4babc5c38`.
The existing root shortcut now selects candidate21 in slot-a. Owner visual
approval is pending; generate a new world for the new model.

Earth30k, Archipelago30k and Continents100k have respectively1,926,2,102 and2,163
local band/household/query checks. These are integration checks, not empirical
proof of historical calibration. Actual regional maps and multiseed trajectories
were reviewed separately. Five100k concurrent transaction checks also preserve
source immutability, caller boundaries and cancellation rollback.

The corrected local-tradition maps and anchored labels remain in place. Candidate20
is an unselected version4 control; its mechanical passes do not validate version5.

### Completed current-model outcomes

All three version5 histories now reached their requested final date under the
same frozen numerical source. The following are measured fixture outcomes,
not prescribed targets for every generated planet.

| World | Years simulated | Residents | Toolkit patterns | Speech varieties / families |
|---|---:|---:|---:|---:|
| Continents, seed73421 | 190,000 | 795,066 | 1,066 | 1,064 / 702 |
| Earth preset | 30,000 | 900,826 | 1,214 | 725 / 343 |
| Archipelago, seed61819 | 30,000 | 675,878 | 690 | 479 / 229 |

The full Continents history has43 thousand-year intervals with population loss
greater than2%,91 range expansions and82 range retreats. Its population remains
below available support, rather than reaching a permanent ceiling. Dated
assemblages record local species arrivals and disappearances. Current maps at
180k and190k were actually inspected: languages, toolkits and local traditions
have different patterns, species have coherent ranges, and territories are empty.

Some outcomes still need calibration. This Continents seed maintains only13.8%
sapiens ancestry and does not reproduce Earth's eventual sapiens replacement.
It has39 techniques with living makers, but no established fitted or insulated
clothing; sewing and bows remain rare. The other fixtures differ substantially,
but their30k endpoints do not establish a later clothing chronology. Neither
these counts nor the controlled clothing-learning fixture prove that every cold
NPC settlement will be equipped appropriately. Physical NPC integration must
validate the actual sampled equipment and local exposure before spawning camps.

The full190k run took about73.5 minutes at5,000 world cells, alongside verification
jobs; this is not an uncontended benchmark. Its final save is253,013,684 bytes.
The implementation is a richer reviewable model, not a60-second history generator
or a claim of finished scientific calibration.

### Superseded version4 review

The version3 Continents190k history is now complete, hash
`0ed1be60a1e7f099e1456d1ce6fcdd46d68c790cf1b7e045ed382a6c8537c824`.
Its broad behavioural gates and33 packed-world checks pass, and actual managed
runtime37-year continuation exactly matches CoreCLR
`21b46a1bbfd1b70baba74cb57fd182a72f6b554ed07588c3cbf676978a6b7d16`.
However, the detailed outcome is **not accepted as final calibration**: no fitted
or insulated clothing is established in that run. Rare-maker teaching treated
maker prevalence as each maker's proficiency. The earlier high-prevalence tests
did not cover that error. The190k visible map run exited0 without its completion
record and is incomplete evidence;180k completed36 checks. Neither an exit code
alone nor a broad consistency gate establishes final visual/behavioural quality.

New worlds now use version4, with version2/3 numerical replay preserved. Teacher
access depends on actual expected maker counts, while conditional proficiency is
separate. Prerequisite experts can enable an invention without an arbitrary22%
population threshold. Complex explanation uses the language's practised technical
register as well as mutual comprehension. Shared language registers develop from
all speaker communities rather than one largest demographic record. These are
model priors; the numeric access scale is not an empirical three-teacher law.
The mechanisms follow the distinction between learning networks and individual
skills in [Migliano et al.](https://doi.org/10.1126/sciadv.aax5913) and the multiple
transmission paths reviewed by [Hewlett et al.](https://doi.org/10.1073/pnas.2322883121).

Focused43 mechanism and12 temporal/save/budget checks pass. Old version3 full
190k37-year continuation remains exact, including the packaged version4 reader.
The numerical source is `f16c75f`. Earth30k and Archipelago30k are complete; the
Continents190k run remains in progress. Actual candidate17 views at Year0,
Continents20k/40k and Archipelago30k pass, including40k->40050 live playback.
The same build passes32 gameplay/nature checks and the existing69 named-save
checks. Those are mechanical evidence, separate from the outcome review.

Substantive interim findings: Earth30k has5,095 fitted-clothing-trained communities
and9,490 needle-trained communities, compared with no established fitted clothing
in the completed version3 run. Continents60k shows fitted-clothing routine use
about4.1times higher in cold than warm habitats, and cold-weather shelter use
about24times higher. Aquatic and inland populations have clearly different
fishing/boat use. Both30k worlds have more than1,000 actual-use toolkit patterns,
distinct local speech communities/families, and real population/range retreats.
These are measured contrasts, not a requirement to force every seed into those
exact counts. The long-run outcome remains pending.

The actual screenshots exposed another display error: the religion map shaded
remote founding families rather than distinguishing its named local traditions.
Presentation-only source `bb5efed` fixes the hues; candidate18 is being packaged
and its maps require fresh inspection. The numerical model and histories are
unchanged. A412-check observer review includes a regression against disguising
different local traditions as brightness shades of one ancestral religion.

The shortcut remains unchanged. No sapiens-only demographic bonus or scripted
extinction has been added to force a preferred species outcome.

### Superseded version3 review checkpoint (18:15 UTC)

Production source `41f4bd7` is pushed. New worlds use `foragers-3`; existing
version2 saves deliberately preserve their original resource-competition rule.
Internal candidate15 is a non-development managed build in slot-a. The existing
shortcut still selects the rejected baseline until the remaining review finishes.

Current-model evidence, distinct from the historical experiments below:

- Continents40k:38 actual player map/atlas/playback checks, exit0; live playback
  to40050 exactly matches direct simulation. Actual regional screenshots inspected.
- Archipelago30k:35 actual player checks, exit0, clean runtime log.
- Earth20k:32 actual gameplay/nature checks, exit0: terrain-worker loading,
  gathering, corpse loot, eating, held-tool/weapon use and save round-trip.
- Complete Earth30k and Archipelago30k histories pass the9 broad coupled-world
  error gates each. They show1,244 and823 actual-use toolkit combinations,
  respectively. These gates are not a claim of scientific calibration by themselves.
- Earth30k local samples:1,807 checks over30 bands/656 people, including227
  multilingual residents. These are selected-community samples, not global rates.
- Same-world0->60k comparison:23,708 dated species arrivals and2,510 local
  disappearances across1,595 changed districts; actual species/abundance records,
  not merely recoloured map layers.
- Package proof:1,008 frozen inputs;3,254 original assets and all fauna/audio
  unchanged. The packaged terrain worker hash is pinned. Current production source
  has not changed since this build.

The new-model190k Continents history is still running. The completed old-rule
190k comparison is explicitly retired; it does not satisfy this pending check.
The owner has returned and revoked mandatory until-wrap continuation: finish
substantive remaining verification, then hand off without time padding.

Physical NPC camps/dialogue, new hominin meshes, physical vegetation/shoreline
reconstruction and post-Paleolithic progression are not part of this implementation.
Long high-detail histories are still computationally expensive; no60-second full
history performance claim is made.

## Changes to the model

| System | Failure being removed | Replacement and NPC-facing result |
|---|---|---|
| Geography | A migrant immediately occupies an entire ecoregion | Smaller connected demographic districts, real travel distance, occupied fraction and dated arrival; coherent local bands rather than uniform presence |
| Ecology | Productivity is almost a static population ceiling | Seasonal plant/game/aquatic opportunities, winter bottlenecks, water, pathogens, resource pressure, glacial ice and regional drought/refugia |
| Demography | Smooth growth to an everlasting ceiling | Age structure, births/deaths, food stress, local shocks, isolation, recovery, retreat and recolonisation |
| Technology | Every group independently fills the same discovery timer | Separate familiarity, making skill and actual use; stochastic innovations, teaching, prerequisites, material constraints, adaptive returns and loss |
| Language | One eternal family with cosmetic sound changes | Inherited basic lexicon, unequal replacement rates, contact and borrowing, intelligibility, bilingual minorities and recognisable family branches distinct from ultimate ancestry |
| Beliefs | No recorded pre-state religion | Local spiritual traditions with inherited/borrowed themes, rites, sacred places and resource taboos; no invented church/state institutions |
| Human lineages | Two identical thermal categories seeded across the world | Geographically connected ancestral dispersal, isolation and distinct descendant histories/traits; contact, admixture, coexistence and possible extinction |
| Social structure | One anonymous regional population | Cultural identities, exchange/kin contacts, residence patterns, shared-resource access, fission/fusion and stable camp/band/household samples |
| Gameplay bridge | A query rerolls arbitrary traits per cell | Stateless spatial sampling from saved district/cohort data, persistent identities, seasonal itineraries and explicit material/knowledge availability |
| Observer | Identical maps and meaningless technology counts | Distinct layers, minority inspection, meaningful toolkit composition and individual-technique views, language/family and belief/local-tradition detail |

## NPC implementation contract

Use one captured, immutable published `PrehistoryState` for a streaming request.
`Prehistory.ForagerBandsNear(state, unitSpherePosition, radiusKm, maximum)` returns
nearby **current seasonal positions**, ordered by distance, then stable IDs.
`maximum` is a caller work limit, not a population/spawn cap. The locator uses
snapshot-owned district/contact indexes and generates households only for selected
results. `ForagerBand(state, cohortId, ordinal)` retrieves a specific stable band.

| Product | Meaning for the NPC host |
|---|---|
| `Id`, `Members`, `FamilyRecords` | Stable band/person/household identity and parent/partner records; preserve instantiated NPC edits rather than rerolling them |
| `Position`, `Home`, `SeasonalCamps`, `ContactBands` | Current itinerary position, remembered home, seasonal destinations and actual social contacts |
| `ClimateSuitable`, `RequiresLocalGroundValidation` | Reject unusable climate; still validate real ground, water, slope and collisions before placing a physical camp |
| `Techniques` | At least one actual resident can make this; does not mean everybody can or everybody carries it |
| `RoutineTechniques`, `WinterTechniques` | Ordinary equipment practice versus winter preparation; map these to existing game item/structure IDs |
| Person `Knowledge`, `Skills` | Familiarity and trained proficiency are separate; a low community prevalence creates rare competent makers |
| Person `Languages` | Individual language identities, native status and fluency, including real contact-derived multilingual access |
| `Culture`, `Belief`, `Lineage` | References into saved culture, spiritual-tradition and ancestry/body-profile data; no fictional political state |

The next phase must persist instantiated camps, inventory and relationships, then
reconcile player changes with aggregate population/resource budgets. This pass
supplies candidate data; it does not secretly spawn the physical tribes yet.

## Evidence ledger

Summaries below support mechanisms, not exact numerical game coefficients.
Ethnographic societies are comparative evidence, not unchanged copies of
Paleolithic societies. Parameters that are design priors are labelled separately.

| Evidence | Mechanism used | Acceptance implication |
|---|---|---|
| [Tallavaara et al., human population dynamics over the LGM](https://pmc.ncbi.nlm.nih.gov/articles/PMC4500234/) | Winter temperature and water/energy balance jointly constrain mobile forager presence and density | Cold simulations must contract into viable refugia; recovery is geographically constrained |
| [Tallavaara et al., productivity, biodiversity and pathogens](https://pubmed.ncbi.nlm.nih.gov/29282314/) | High productivity alone does not imply maximal human density; pathogen stress matters in productive environments | Tropical rainforest cannot automatically dominate population everywhere |
| [Posth et al., ancient hunter-gatherer genomes](https://www.nature.com/articles/s41586-023-05726-0) | Climatic disruption, population turnover, refugial survival and later mixing | Test decline, survival and recolonisation, not only cumulative expansion |
| [Timmermann et al., climate and archaic human habitats](https://www.nature.com/articles/s41586-022-04600-9) | Shifting connected habitat opportunities shape dispersal and isolation | Lineages must follow routes and changing habitat; no teleported matching species on unrelated continents |
| [Ragsdale et al., a weakly structured African stem](https://www.nature.com/articles/s41586-023-06055-y) | Established sapiens ancestry includes population structure and continuing connections | Start with an established regional metapopulation, not one newly founded speech community |
| [Migliano et al., multilevel hunter-gatherer sociality](https://pmc.ncbi.nlm.nih.gov/articles/7048420/) | Families, camps and intercamp connections support cultural transmission | Contact is not equivalent to population size; bounded exchange and marriage networks matter |
| [Collard et al., risk and technological richness](https://pmc.ncbi.nlm.nih.gov/articles/PMC4027409/) | Environment and resource risk help explain toolkit differences | Equal population/time must not force equal toolkits; compare use and skill separately |
| [Vaesen et al., limits of population-only explanations](https://www.pnas.org/doi/10.1073/pnas.1520288113) | Demographic explanations alone are insufficient for cultural complexity | Do not replace the old time counter with a population-size counter |
| [Greenhill et al., language system dynamics](https://pmc.ncbi.nlm.nih.gov/articles/PMC5651730/) | Lexical/grammatical features vary in stability; contact and deep-time signal loss differ | Track actual inherited lexical evidence; do not label every 40,000-year descendant a dialect of one observable family |
| [Hua et al., ecological drivers of language diversity](https://www.nature.com/articles/s41467-019-09842-2) | Geography and ecological conditions help structure linguistic diversity | Compare isolated, connected, productive and seasonal regions; avoid uniform per-cell language speckling |
| [Peoples et al., hunter-gatherer religion](https://pmc.ncbi.nlm.nih.gov/articles/PMC4958132/) | Spiritual themes, ritual specialists and ancestral beliefs vary independently | Model local traditions and connections, not a universal organised-religion stage ladder |
| [Kelly, hunter-gatherer human evolution review](https://doi.org/10.1146/ANNUREV-ANTHRO-102116-041448) | Mobility, reciprocal access and seasonal aggregation extend beyond a single camp | Distinguish home ranges from political territory; derive dispersed and aggregated camp states |
| [Grove, social network maintenance model](https://pmc.ncbi.nlm.nih.gov/articles/PMC4157219/) | Residential mobility and seasonal meetings sustain social ties | Test coherent seasonal itineraries and contacts rather than random camp positions |

Additional demographic evidence:
- [Gurven & Kaplan, longevity and mortality profiles](https://gurven.anth.ucsb.edu/sites/secure.lsit.ucsb.edu.anth.d7_gurven/files/sitefiles/papers/GurvenKaplan2007pdr.pdf): high early mortality is compatible with substantial adult longevity; do not make everyone die at a supposed maximum age of 30.
- [Gurven & Davison, periodic catastrophes and the forager population paradox](https://pmc.ncbi.nlm.nih.gov/articles/PMC6600907/): observed vital rates can yield appreciable short-run growth even when long-run growth is very low. Test local crashes, recovery and metapopulation turnover rather than forcing an unrealistically tiny birth rate everywhere.
- [Collard et al., clothing and cold-weather fauna](https://doi.org/10.1016/j.jaa.2016.07.010): clothing strategies and available fur sources matter separately from body shape.
- [Backwell et al., Sibudu projectile evidence](https://doi.org/10.15184/aqy.2018.11): sophisticated projectiles can be local traditions; pressure-flaked stone arrowheads are not the sole possible construction pathway.

## Review matrix

1. Run multiple procedural seeds, Earth geography and deliberately isolated,
   connected, cold, arid and resource-rich fixtures. Include 0, 5k, 20k, 40k,
   80k, glacial peak, deglaciation and end-of-Paleolithic checkpoints.
2. Measure occupied area, frontier speed, population, age structure, local
   density, bottlenecks, extinctions/recolonisation and climate sensitivity.
3. Measure knowledge/use distributions, toolkit similarity versus distance and
   contact, independent invention, loss and material bottlenecks. Identical
   counts are not accepted as equivalent toolkits.
4. Measure lexical retention, mutual intelligibility, identifiable families,
   contact borrowing and minorities; compare nearby and isolated communities.
5. Measure belief fragmentation/inheritance/contact, social network structure,
   ancestry and lineage geography. Do not manufacture variation by recolouring
   otherwise identical data.
6. Sample bands, households and seasonal camps at local and district boundaries;
   test stable identities, plausible composition, occupancy and spatial coherence.
7. Check deterministic split/budgeted stepping, cancellation, save/load, old-rule
   continuation and bounded work. Benchmark both short live updates and long runs.
8. Inspect actual player screenshots of every relevant overlay at several dates
   and scales, including visible inspection/atlas panels. Blank/hidden-window
   captures and mechanical check counts do not constitute visual acceptance.

NPC spawning and full agent AI are the next owner-supervised pass. This rebuild
must provide their inputs and stable sampling contracts; it must not claim those
actors have been implemented merely because the data exists.


## Historical development ledger (superseded checkpoints)

The dated notes below document experiments and failures. They are not the current
release status; use the current review and final manifest above.

### Working implementation, 2 November

New-world ledger: `prehistory-8` / `foragers-1` (unselected, still under development).
The selected slot-b player and both earlier Paleolithic save rules remain intact.
The 43-technique graph preserves the original item-tech IDs, supports alternative
manufacturing pathways, and separates familiarity, trained practice and use.
Learning advances with the five-year demographic review. Incoming teaching
contexts are saved at contact reviews, so caller budgets and reloads do not
choose different teachers. Local invention provenance and first-learning/loss
dates survive the bounded recent-event feed.

Demographic communities can share a district, species and heritage without being
merged into one population. Resource claims have diminishing marginal access,
with a conserved per-food allocation. This replaces a fully mixed competitive
model that incorrectly turned a small body-mass difference into a universal
winner over deep time. Body mass is not an arbitrary species fitness multiplier.
Distinct cultural communities retain their knowledge and speech after migration.

Climate now includes a geographically constrained wet-period envelope. Warm-ocean
moisture access follows connected land paths with mountain costs; orbital-season
forcing changes rainfall amounts, not only a multiplier on modern desert rain.
Water opportunity, plant productivity, habitat suitability and dispersal costs
respond together. This is a regional game climate model, not a GCM or a claim of
planetary moisture-budget closure. All numerical amplitudes are model priors.
Original terrain, source ecoregion boundaries and native provenance stay unchanged.

Sparse saved biota record local relative abundance, thermal/moisture/habitat
limits, refuges and recolonisation. A region cannot conjure a missing species
because it becomes suitable. Extinction removes that food opportunity. Native
visible-fauna admission now reads the historical roster; complete in-game
vegetation/hydrology geometry reconstruction remains a separate integration
question to audit, not an implied consequence of these records.

### Evidence added during implementation

- [Groucutt et al.2021, repeated dispersals into Southwest Asia](https://www.nature.com/articles/s41586-021-03863-y): wet windows, different material cultures, local population discontinuity and mixed faunal opportunities.
- [African climate response to orbital/glacial forcing](https://www.pnas.org/content/pnas/117/5/2255.full.pdf): seasonally distinct rainfall regimes can narrow Saharo-Arabian barriers; not merely a colder modern map.
- [Humid Sahara corridor evidence](https://pmc.ncbi.nlm.nih.gov/articles/PMC2575439/), [Green Sahara dynamics](https://pmc.ncbi.nlm.nih.gov/articles/PMC3797788/): regional freshwater/vegetation corridors matter to dispersal.
- [Lorenzen et al.2011](https://www.nature.com/articles/nature10574): different large mammals respond differently to climate, habitat redistribution and human pressure.
- [Postglacial tree-range simulations](https://www.frontiersin.org/journals/ecology-and-evolution/articles/10.3389/fevo.2023.1321104/full): dispersal/establishment lags differ from instantaneous climatic suitability.
- [Madjedbebe primary report](https://www.nature.com/articles/nature22968), [grinding/use-wear study](https://pmc.ncbi.nlm.nih.gov/articles/PMC9273753/): axes, grinding and pigments are not a universal Neolithic package.
- [Early pottery primary report](https://pubmed.ncbi.nlm.nih.gov/22745428/): fired vessels can belong to forager lifeways, not an automatic farming unlock. Exact earliest dates are not simulation triggers.
- [Jerimalai fishing primary report](https://researchportalplus.anu.edu.au/en/publications/pelagic-fishing-at-42000-years-before-the-present-and-the-maritim/): maritime fishing and hooks have distinct evidence; the42ka fish assemblage does not date every hook to42ka.
- [Blombos ochre processing](https://pubmed.ncbi.nlm.nih.gov/21998386/): local material-processing and social traditions need not wait for a universal modernity switch.
- [Neanderthal speech reassessment](https://pubmed.ncbi.nlm.nih.gov/23847571/): do not use a fictional inability to speak as an empirical explanation for all archaic-human outcomes. Starting registers are gameplay priors; no claim of reconstructed prehistoric grammar.

### Diagnostic history: not final acceptance

Early40k and190k runs identified compounding skill loss, diluted teacher quality,
body-mass-driven competitive exclusion and automatic newcomer assimilation.
Those mechanisms were changed; their older metrics are experimental evidence,
not acceptance of the current revision. The first native observer capture also
failed: its fixture left the pause panel open and selected an invalid atlas
section, producing UI exceptions. The fixture and atlas guard are corrected;
fresh clean native screenshots are still required.

Latest short coexistence run validates births/deaths/migration and multiple
resident communities. Long-run calibration, distribution/multilingual details,
wet/dry corridor tests, several seeds/detail levels, performance, save/budget
invariance, old-rule replay and clean actual UI capture remain in progress.
The owner has explicitly forbidden stopping without a wrap-up request.


### Working checkpoint, 2 November 12:10 UTC

Canonical speech and ritual affiliation distributions now preserve three or more
resident communities through migration; cached majority/minority fields are only
observer conveniences. Local contact can gradually change the main language,
while isolation cannot draw from a global popularity list. Regional mutually
intelligible varieties can share a dialect. The first20k run of this revision
has409 dominant speech varieties/112 families at19k, versus thousands of tiny
families in the previous diagnostic. This is not final calibration or an exact
reconstruction of prehistoric language counts.

Family sampling now includes stable birth/death and parent records, checks
parental survival at conception/birth, conditions introduced adult parents on
reaching adulthood, keeps household units and provides foster guardians. Homes
remain exactly unchanged under frontier/population expansion.1,152 sampled
household/camp assertions passed across30 communities at20k. A failed earlier
home test compared a rounded vector dot product at finer precision than its
representation; the corrected test compares coordinates exactly.

Nine temporal checks pass: split/public/private advances, event/work budgets,
real save/reload, deep-copy isolation, and cancellation after migration. Nine
climate/weather checks verify seasonal rain conservation, southern seasons,
new rain in dry baselines, snow and stationary weather-provider refresh. The
playable weather generator now receives historical seasonal inputs; physical
river/shore/vegetation geometry is not thereby reconstructed.

The20k technique audit exposed a separate establishment failure: an invention
created familiarity without trained makers, then lost the nascent practice.
New insights now introduce a few makers and a temporary learning/experimentation
window, with skilled makers counted separately from daily use.26 isolated
mechanism checks pass. Fresh long histories of this change remain REQUIRED.

Read-only profiling of a250-year20k checkpoint measured8.63s on this PC.
Caching unchanged RNG stream hashes/material opportunities removed about98% of
practice-stage allocation (753MB to12MB). More direct affiliation normalization
reduced its allocation1,048MB to623MB. The before/after full simulation hash was
EXACTLY9e1f5096045a18a9a1c32a1acb88386f3845c5e661aab56a33379c166352d75d.
These are scoped allocation/profile measurements, not a promised full-world
runtime. The subsequent invention-establishment change intentionally changes
model outcomes and is not covered by that unchanged-hash comparison.

Additional sources: [Hill et al.2011 co-residence](https://doi.org/10.1126/science.1199071),
[Gurven/Kaplan life histories](https://doi.org/10.1111/j.1728-4457.2007.00171.x),
[Evans2017 multilingual settings](https://openresearch-repository.anu.edu.au/handle/1885/227220),
[Conde-Valverde et al.2021 speech capacities](https://www.nature.com/articles/s41559-021-01391-6).
Only the designated sapiens lineage is marked sapient in the current fictional
preset. Other lineages' symbolic/learning capacities are explicit world-design
priors, not empirical claims that real archaic humans lacked speech or culture.


### Working checkpoint, 2 November 13:00 UTC

Clean actual managed player capture of the50bcc0a observer completed13 scoped
checks and no runtime errors; language/toolkit HUDs and the language atlas were
visually inspected. This is an internal UI checkpoint using the earlier20k
history, not acceptance of the latest model or the final release. The original
selected slot-b remains frozen. The next candidate additionally projects dated
climate/biome/aridity/permafrost backgrounds and current ecology assemblages,
including later colonists absent from the source regional roster.1,679 derived
habitat/map/assemblage checks pass: the isolated warm/glacial comparison changes
1,030/1,674 land climate classes and91 cells gain persistent ice. These are
controlled climate-date probes, not claims about independently simulated people.
Native screenshots of the new backgrounds remain required.

Population-weighted, conservative intergenerational ancestry exchange replaces
equal fractional mixing. Smaller groups change more from the same exchanged
ancestry; islands cannot receive distant genes without contacts. Compatibility
uses actual lineage genealogy and mixed ancestry rather than a binary nominal
label.14 affiliation/contact/ancestry checks pass. The implementation deliberately
does not infer interbreeding rates from skull form or claim an exact Earth
reconstruction. [Li et al.2024 recurrent gene flow](https://pubmed.ncbi.nlm.nih.gov/38991054/)
and [ancient modern genomes2024](https://www.nature.com/articles/s41586-024-08420-x)
support contact/assimilation as distinct from simple immediate replacement.

Complex teaching now depends on mutual speech/bilingual access, while observation
still permits slower learning across language barriers. Earlier long runs also
showed that apprenticeship within a small community was taking implausibly many
generations; practice periods are now10% of the old regional-scale defaults
(typically several to a few dozen years). Rare invention and geographical spread
remain separate. Travel gates use the ability to prepare boats/winter equipment,
not their annual routine-use fraction.30 component mechanism checks now pass.
New long histories are REQUIRED for these interactions; the earlier50bcc0a and
2999fa3 histories are diagnostic baselines only.

Playable time continues through saved pending calendar seconds. New forager
worlds publish full historical checkpoints in day-sized gameplay batches instead
of copying/resealing all ancestral records every two real seconds. This does not
change five-year demographic reviews, continuous weather/needs/time, or the
older save engines. Current full temporal/budget/save/cancellation tests must be
rerun after the final interaction checkpoint.


### Minority-family correction

The first complete minority totals exposed a classification defect hidden by
majority-only counts: at16k, the0834d5d diagnostic recorded136 dominant families
but2,080 families among all represented minorities. Closely related varieties
were only allowed to share a new recognisable family if currently in direct
contact. Family recognition now uses recorded ancestry and reference-root
similarity even for isolated cousins/minorities; it does not relabel unrelated
languages to hit a target. A direct no-contact cousin test passes.11 independent
language checks pass. The three0834d5d runs were stopped and marked rejected;
they are not final validation. Failed initial habitat candidates now roll back
provisional registry entries, so historical directories do not invent peoples
who never lived anywhere. Initial test:214 real communities,89 families; no
unowned initial cultures/peoples/languages. New histories start from scratch.

The0834d5d managed internal landscape candidate completed20 real native map/
atlas captures with clean logs. Satellite/climate/aridity images were inspected.
This proves UI wiring for that checkpoint, not final current-model realism.


### Speech affiliations versus bilingual knowledge

The ecad71e runs showed that improved family classification alone was insufficient:
contact with visitors was generating small native-speaking fractions of every
minority dialect they knew. This conflated bilingual competence with mother-tongue
community membership. New speech affiliation targets are a clear local convention
or the existing resident distribution; actual migrants still preserve all their
minorities. Choosing another regional language is gradual. A new regional dialect
starts with identical words across neighbouring camps and diverges afterwards,
instead of creating a separate language record for each band. Existing distinct
local varieties attract gradual adoption rather than an immediate relabel.
The ecad71e runs are retained as rejected diagnostics. Fresh long runs follow.


### Regional communities, retired data and rare teachers (14:30Z WIP)

The missing speech-review cooldown allowed repeated regional forks. Current
registration requires a real connected speech community (or sustained isolation),
reuses existing local branches, and preserves the identical vocabulary at the
moment a regional variety is first recorded. The 15k diagnostic now has614
resident varieties rather than the10,397 produced by the rejected churn run.
This is a mechanism correction, not a language-count target or final acceptance.

The older50bcc0a/2999fa3 histories FAILED at140k/120k when compressed saves crossed
256MB; their incomplete outputs are not passes. Retired communities now discard
only incoming teaching/invention caches, preserving ancestry, demographic totals,
final toolkits and learning dates. Native674d7eb candidate (managed nondev,
14:27Z) has1003 frozen inputs/3254 original assets verified,20 real map/atlas
checks and clean logs; it remains UNSELECTED and precedes the next teacher fix.

A20k audit found a further real defect: rare expert instruction was averaged
with people who did not know a technique, imposing a permanent low familiarity
ceiling. Saved incoming context now separates technique-specific teacher quality
from access frequency. Rare contacts teach slowly; unrelated unskilled neighbours
cannot reduce the expert's competence.34 mechanism checks pass, including rare
expert establishment, and12 temporal/budget/reload checks pass. These broad
mechanisms are consistent with [Hewlett et al.2024](https://pmc.ncbi.nlm.nih.gov/articles/PMC11621818/)
and [Migliano et al.2020](https://pmc.ncbi.nlm.nih.gov/articles/PMC7048420/);
those papers do not establish the game's numerical rates. Observation remains
possible without fluent shared speech; teaching and observation coexist in
[Garfield et al.2025](https://pmc.ncbi.nlm.nih.gov/articles/PMC11982242/).

Current long histories must be rerun for the teacher correction. Coupled outcome
reports now export habitat/toolkit contrasts, density, ice exposure, near/far
speech similarity, ancestry and changed biota. A passing broad error gate does
not establish universal realism. No full physical glacial terrain/coast/river
or vegetation reconstruction is claimed; dated data/weather/fauna/maps are wired.


### Current outcome and transport checks (15:00Z, still active)

The e045fdd internal managed player passes35 real map/atlas checks at20k, including
three separated regional closeups and loaded-world default Cultures. Actual
language, toolkit, species and ritual images inspected.1314 household/camp and
423 observer checks pass against the corrected-teacher20k snapshot. These are
scoped results, not full-history acceptance; long histories are still running.

At20k the coupled audit has453 distinct actual-use toolkits,770 resident speech
varieties and461 recognisable families. Singleton families account for24.4% of
people; several larger families have10-20 living varieties. Fishing-line use is
24.7% in aquatic communities versus0.52% inland. Invented techniques now establish
and spread rather than being limited by unskilled neighbours. Full190k behaviour,
late toolkits, refugia, ancestry and all-map review remain required.

Independent cohort learning now uses up to four CPU workers, with all discoveries
and events published in original order. The measured250yr10k fixture retains
exactcc3dbfcb... after sequential allocation changes and parallel arithmetic.
12 temporal/reload/budget/cancellation checks retain89ff1c09... . Allocation
profiles on the calling thread no longer represent worker-thread allocation;
the diagnostic now also records process-wide allocation and CPU time.

Lossless transport prototype passes13 actual numeric/world/named-save checks:
Earth30k77,348,664 ->61,654,755 bytes, identical complete historical hash and
identical future25yr hash. New forager snapshots identifyWGWRLD02; separated
named histories useWGHIST02. OriginalWGWRLD01/WGHIST01 stay readable; non-forager
writes retain their old formats. Already-quantised doubles use32-bit integers
only when reconstruction preserves every IEEE64 bit; otherwise raw64-bit values
remain. Signed zero, NaN/infinities and unquantised values round-trip.69 existing
named/quicksave/legacy/shared-base checks pass. Native packed-format proof is
still pending. The old256MB compressed cap was inadequate for a legitimate rich
history; supported compressed size is now1GB, with the existing4GB decoded guard
unchanged. Packing and cache retirement reduce size; this is not a claim that
all-detail worlds fit arbitrary memory budgets or now generate in60seconds.


### Native multi-world review and canonical cost (15:20Z WIP)

The same3041073 internal managed package passes35 native map/atlas checks each
for Earth40k (new packed snapshot), seed99173 at30k and Archipelago30k (original
snapshot format). Runtime logs clean; actual regional toolkit/language/species
and climate images inspected. Neither the selected slot-b nor shortcut changed.
The two30k histories complete and pass coupled-world error gates. The two190k
histories remain INCOMPLETE. Continuations preserve prior metrics/source metadata;
Earth40k/41k outcome records match the original serial run exactly. The former
674d7eb pre-teaching comparison was stopped; it is not a final-model pass.

New typed canonical writers read live cohort/culture/affiliation fields in the
same checked order as the original reflection writer.328 exact-reference tests
pass, including null/empty arrays and mutation; full saved hashes unchanged.
The generator reproduces its source exactly. The250yr10k fixture remains exact
cc3dbfcb... and the12 temporal checks remain89ff1c09... . Five large-cohort
transaction tests cover the concurrent path, split callers/work budgets and
cancellation after worker arithmetic. Empty retired populations now avoid useless
fractional-date projection allocations. A new native check will advance real UI
playback, compare its result with direct simulation, and require current human
and dated landscape palettes; this proof is pending, not yet claimed passing.


### Live-observer diagnostic and dated camp candidates (15:40Z WIP)

The first live-playback run advanced the actual UI world and matched direct
simulation, then FAILED an overly strict observer assertion. The diagnostic9
rerun records finalSeconds1893741791202 / legendSeconds1893737477708,
sameDomain=true, observed/expected climate epoch2402, no pending/job/ready work.
This is about50 simulated days of clock-only reuse, with no intervening five-year
demographic review. It is not evidence that population/climate failed to update.
The revised assertion requires the same demographic domain, less than a five-year
clock gap, and the exact current climate epoch. A fresh native passing rerun is
STILL REQUIRED. Keep8/9 failures as diagnostics; do not count them as passes.

Seasonal camp candidates now score dated local vegetation, water and ice instead
of source-modern habitat. Actual food opportunities include the saved local biota.
Unusable fallback climates are marked ClimateSuitable=false with zero food/water
and maximum exposure; all candidates still require physical ground validation.
This changes query products for the next NPC pass, not historical demography or
learning trajectories.1344 actual20k household/camp checks pass. Further checking
of full histories and local projection remains in progress.

Additional primary language references: [Bouckaert et al.2018](https://www.nature.com/articles/s41559-018-0489-3)
and the [authors' model/results](https://pamanyungan.compevol.auckland.ac.nz/what-we-found/)
show that a large forager language-family expansion need not require farming.
[Hua et al.2019](https://www.nature.com/articles/s41467-019-09842-2) cautions against
attributing every ecological language-diversity correlation to isolation. These
are checks on model assumptions, not grounds for enforcing a fixed family count.


### Current native playback and late-record performance (16:00Z active)

The7ac7ab8 managed internal candidate (built15:42Z) passes38 native checks at60k,
including actual UI playback to60050, direct-reference hash equality and current
human/climate palettes. Live image inspected; clean runtime log.1005 frozen inputs,
3254 original assets/177fauna/audio exact. Still UNSELECTED. The8/9 strict-clock
failures remain recorded; the corrected assertion follows the existing domain
cache contract and still requires current climate and bounded clock skew.

A true0->80k same-world comparison has28,170 taxon-district arrivals,463 local
disappearances and1,597 changed districts, with maximum forest-cover change0.645.
These are local relative-presence records, not absolute wildlife census counts.

Further allocation changes skip retired groups in teaching lookups, use stack
buffers for demographic integration, and use checked dense-ID event lookup.
The large70k fixture (125,383 total/19,920 active records), advanced25yr, retains
exactad373830... versus3041073. Process allocation1.962GB->1.480GB; measured wall
11.28->8.50s under concurrent work, not an uncontended speed guarantee. The10k
250yr fixture remains exactcc3dbfcb..., demographic allocations33MB->2.4MB.
34 mechanism and12 temporal checks pass. The new allocation source has not yet
been rebuilt into a native candidate. Full190k runs are still active/incomplete;
no60-second full-world generation claim.


### Lossless record transport and continuing full histories (16:13Z WIP)

The new WGWRLD03/WGHIST03 transport writes the numerous cohort/culture records
as checked binary blobs, retaining all fields and raw floating-point bits. The
01 and 02 readers remain available; non-forager writes retain their old format.
The generated codec checks the complete public schema and embeds a schema stamp;
record/collection sizes, cancellation, truncation and trailing data are bounded.
It preserves null arrays, signed zero, NaN/infinities and UTF16 code units.

Two actual migrations pass28 checks each, including a raw-bit fingerprint of
the ENTIRE forager state, unchanged canonical hashes, identical future25yr
simulation and the real named-save path. Seed99173 at10k shrinks32,179,388 to
26,454,857bytes, read4.03 to1.61s; Continents60k shrinks65,147,162 to60,801,325,
read5.86 to2.54s. These are concurrent-work measurements, not uncontended promises.
69 named-save/legacy and49 old Paleolithic checks pass. Native Mono proof for
the03 format remains PENDING. The selected game and shortcut remain unchanged.

The two current190k histories have reached approximately Earth90k/Continents122k;
these are NOT complete-history passes. Current plotted histories show real
contractions and retreats. Large late-world records remain expensive; no claim
that the full current simulation finishes in60seconds or that NPC camps are
already physically spawned.


### Native late-world and NPC query preparation (16:35Z active)

The2e33aa2 internal managed candidate (built16:15Z) passes38 native checks on
the60k03-format snapshot, including real UI playback/direct replay. It also
passes35 native checks on the120k02-format world; actual regional species,
religion and technology images inspected, clean error logs. The selected slot-b
and shortcut remain frozen. A genuine previous WGHIST02 named save loads with
its original full hash; the compatibility test does not manufacture that fixture.

For local NPC/atlas sampling, snapshot-owned district/contact indexes replace
repeated all-world/retired scans. Six complete query results match the former
exhaustive lookup bit-for-bit, including sampled families and contact tie order.
On115,535 records/14,872 active groups, six queries measure69.47 vs30.07ms under
concurrent work.1,968 household/camp/equipment checks pass across30 communities.
Routine equipment and prepared winter equipment now have separate band sample
arrays, both constrained to actual knowledge. Conditional prevalence preserves
routine Use rather than multiplying it by Skill twice; winter preparation shares
the demographic protection rule. The exact250yr cc3dbfcb... and12 temporal
89ff1c09... results remain unchanged;34 mechanism checks pass. This prepares
data for the next supervised NPC phase, not physical NPC camp generation.

The long histories resume from verified persisted Earth100k/Continents130k
checkpoints using2e33aa2's exact arithmetic and smaller transport. Old logs and
source chains are retained; all measured prefix records match, excluding elapsed
test time. The initial zero-year cost probe failed because zero advancement is
invalid, AFTER the130k snapshot read/validation succeeded; it is not a passing
advance test. Both190k histories remain incomplete.


### Journal/migration allocation and interpretive checks (16:47Z active)

Private advancement now batches recent-event ring writes and persistent discovery
appends, publishing the same ordered records once per transaction. Migration
groups immutable route alternatives and resident totals once per planning review,
retaining first-minimum tie order and all existing choices. The10k250yr cc3dbfcb...
and70k25yr ad373830... hashes remain exact. Migration allocations137.2->88.2MB
in the former fixture; large-world migration157.0->127.7MB. Whole-kernel wall
time is not claimed improved beyond measurement noise.12 temporal/budget/cancel
checks pass; native build13 and large transaction rerun remain pending.

The2298e25 internal candidate12 (built16:37Z) passes38 native checks on original
01-format seed99173 at30k, actual UI playback/direct comparison, exit0/clean log;
proof1007 frozen inputs/3254 original assets exact. It predates the journal and
route batching source. The current selected game remains the rejected baseline.

Additional calibration references: [Wakano et al.2018](https://pubmed.ncbi.nlm.nih.gov/29032037/)
models cultural/resource-linked replacement and assimilation without requiring
intrinsic demographic or cognitive superiority. [Burke et al.2026](https://www.repository.cam.ac.uk/items/c6f5abe3-fec1-4783-ade6-cbbcb994fd8d)
finds regionally different refugia/connectivity and cautions against climate-only
replacement. These support investigating the actual connected resource network,
rather than adding a scripted species extinction date or universal racial bonus.

The120k world currently has about82.6% of people in singleton recognised language
families and very low between-variety core cognacy even nearby. That remains an
explicit outcome to review, not a claimed realistic target. A deep-time isolated
lineage and an immediately intelligible dialect are distinct measures.
[Blasi et al.2026](https://doi.org/10.1126/science.adx4343), as described by the
[authors' institute](https://www.mpg.de/26871018/the-lost-golden-age-of-language-diversity),
uses171 forager/fisher societies and estimates several thousand early-Holocene
languages; modern post-state family/language counts are not a census target for
the Paleolithic. No model parameter was changed just to force a modern count.


### Individual language knowledge and actual makers (17:10Z active)

Local person samples now carry explicit native/contact-language identities and
age-dependent fluency. Secondary languages derive only from resident households
and actual saved exchange links; they are stable ID-based samples. In the reviewed
30 largest120k communities,190 of687 sampled people are multilingual. This is a
selected-community sample, not a claimed worldwide or ethnographic rate.

A further query defect assigned every resident the community's low average craft
proficiency, so a rare specialist appeared as many incapable weak workers. Samples
now draw actual trained makers with age-dependent proficiency and necessary related
skills; familiarity is a separate array. Band capability requires actual resident
makers, while routine/winter equipment remain separate. This supersedes the prior
2298e25 band-skill quantile and conditional Use/Skill equipment sampling. No global
demographic/technology trajectory changes.2,176 sampling checks pass at120k,
including actual expertise under rare-maker prevalence, prerequisites, household
relations, locality and reference-query equality. Reviewed output has37 proficient
adult bow makers, across8 of30 bands, rather than uniformly weak bow skill.

A reproducible1.45MB band/person/camp sample export is under npc-data-review-120k.
The first export FAILED because generic JSON followed V.Unit recursively; the
corrected explicit XYZ converter writes real vector fields and passes. Keep the
failed export log as a diagnostic, not a successful run.

Internal candidate13 (87e1902,16:49Z) passes38 actual150k native checks including
UI playback to150050, direct-reference hashd0187a35..., current human/climate
palettes, explicitexit0/cleanlog. Regional language and winter images inspected.
Proof1008 frozen/3254 original assets/177fauna/audio exact. It predates the newest
individual sampling changes, which still need their own candidate/proof.


### Planet-only terrain input and dated local-map cache (17:30Z WIP)

The owner has returned and revoked the mandatory keep-working-until-wrap rule:
finish the remaining substantive work/checks, then hand off; no time padding.

Terrain workers now read the exact saved planet while streaming past the human
archive. Shared named saves read their planetary base without opening history.bin.
Normal game reads still require and validate history.01/02/03 worlds remain
readable; no save formats or planetary geometry change in this step.
Two actual migrations each pass33 checks, including missing-history separation
and full-history/future replay. The60k planetary read measures0.836s versus2.806s
for full03 history under concurrent work. The real TCP worker matches186,077
mesh/scatter values plus terrain/water indices/bounds/cancellation on a saved
Earth40k forager world.69 save and49 legacy Paleolithic checks still pass.

Because the worker no longer owns historical climate, dated forager local-map
colours render from the player's captured historical snapshot. Progressive tile
and palette cache keys include the25yr climate epoch; each request captures an
immutable World shell before asynchronous rendering.13 actual map/cache checks
pass: warmed reuse, date invalidation, exact fresh-reference pixels and mid-render
publication isolation. The first fixture failed by assuming adaptive tile sizes
never change; corrected diagnostics show0 changed pixel bytes while partitions
warm8->10tiles. That failure is retained, not counted as an earlier pass.

Build stamps now pin the packaged worker executable hash, and source freezes
include CLI source. The next player/package14 and real gameplay checks remain
pending. Main selected shortcut is still unchanged.


### Resource-competition diagnosis and version3 (17:35Z active)

The completed190k Continents run is now a LEGACY COMPARISON, not final-model
acceptance. Earth was stopped after125k of validated metrics (last saved120k);
it remains explicitly incomplete. Both useforagers-2's concave resource claims.

A concrete aggregation defect explains part of their excessive persistence:
claim=(population*diet)^0.75 grants one population extra food access when its
records split, even with identical people, diet, place and tools.100 identical
people competing against100 receive50%; splitting one side into40+60 records
raises its share without adding people or habitat. This subsidises fragmented
small groups independently of real niches.

New worlds useforagers-3 with additive population*diet claims. Actual technology
returns, resource guilds, climate and culture still determine relative success;
there is no sapiens-specific victory bonus or forced extinction date. Version2
checkpoints retain their original rule for faithful legacy replay. Four explicit
partition/diet regression checks pass alongside34 other mechanisms; temporal
checks use the new rules. New-model full-history outcomes are PENDING and must
not be substituted with the completed legacy run.

Candidate14 (9a85faf, built17:31Z) has passed its frozen-input/original-asset and
new packaged-worker-hash proof but predates version3. The selected shortcut
remains the rejected old baseline. A further candidate is required for version3.


### Current-camp lookup correction (18:31 UTC)

A new100m/one-result regression failed on a known real Earth30k camp. The local
locator had two independent defects: rounded dot-product/acos distance can have
an approximately200m floor even at the same point, and it capped nearest permanent
homes before testing current seasonal positions. Its area-equivalent district
radius was also not a reliable enclosing bound for irregular coastal districts.

Local admission now uses raw atan2 angular distance, actual geometry-derived
conservative bounds, and ranks current itinerary positions before applying the
caller limit. A shared lightweight layout avoids generating whole households for
rejected candidates. The historical migration/contact distance arithmetic is
unchanged. New tests compare against all25,935 Earth band positions without
spatial pruning. The actual30-band/656-person export is exactly unchanged.
Earth30k passes1,867 sample/household/nearest-camp checks; the earlier initial and
first partial-fix failures are retained. Other-world final sampling and a new
actual-player candidate/check follow. Exact40000->40050 history hash before/after:
`133e6446580b1808e6e23aeb085fdbab5406edf16bc421b36921c93511a1af5c`.
This is an integer50-year probe; native playback advances a fractional final year
and is checked separately against its own exact elapsed interval.


Final locator sampling also passes2,369 checks on Continents100k (706 people)
and1,994 on Archipelago30k (678 people). The exhaustive comparison prepares
26,148 and17,375 actual band layouts respectively, without using the production
query's spatial pruning or result cap. Six indexed queries measure36.4ms and
54.5ms total under concurrent work; these are query timings, not whole-game FPS.
Frozen-input comparison between candidates15 and16 identifies exactly two changed
files: the local sampler and its native verification fixture. All historical
numerical model sources are unchanged. New candidate16 built18:33:14Z,
proof1008/3254/177+audio exact; actual-player verification is in progress.


### Owner-requested restart pause (18:38 UTC)

The owner explicitly requested a pause. All owned history/native/Unity jobs are
stopped. Current-rule Continents history is incomplete at130k of190k; a130k world
checkpoint and120k fallback are retained. The130k file SHA256 is
`f562f1a894a425f51598971292136f2b921aaa2384d7f8d99295d270b8f1dc8f`;
read validation remains the first resume step. This is not a full-history pass.

Candidate16 actual-player reviews passed39 checks at40k (including live playback),
36 atArchipelago30k,36 atContinents100k and32 gameplay/nature checks atEarth20k;
all exit0 with clean runtime errors. Final16 image review and later-history checks
remain. Selected slot-b shortcut stays unchanged pending completion. Resume only
when the owner authorizes it.


### Resumed review and fresh-world entry (21:22 UTC)

The130k checkpoint survived restart with its exact recorded file hash and passed
full read/structural/accounting validation; statehash308f68bcb6b501ff3595da8ba5f7f089e8084903f595255b18ce814cefc440d3.
Continuation preserves the existing metrics/source chain and is still running.

Candidate16's actual early/middle and Archipelago captures have now been reviewed,
including culture, ethnicity, language, belief, toolkit, lineage, winter climate
and atlas details. All21 raw overlay map PNGs are byte-identical to the previously
reviewed candidate15 counterparts; the new locator does not alter the world maps.

A fresh generation through the actual player also passes36 map/atlas/local-camp
checks, exit0 and clean errors. It starts atYear0 with83,662 people,32,794 sapiens,
214 resident districts,214 recorded speech varieties/82 families and214 local
traditions. Actual initial toolkit/species/region/atlas images inspected. This is
fresh preparation and map entry, not a claim to have completed its long history.
Generate a new world to test the revision: loading/continuing an older save keeps
that save's simulation model. The concise report has been updated and opened.
