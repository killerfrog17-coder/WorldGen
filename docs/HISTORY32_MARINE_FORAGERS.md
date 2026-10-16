# History32: marine forager traditions

This narrow extension distinguishes an advanced regional forager culture from
agriculture, urbanism and a territorial state. It is enabled only by history32;
history31 and earlier retain their original origin probabilities and livelihoods.

## Historical motivation

An agricultural-resource filter alone misses productive aquatic communities.
Marquet et al.'s original Chinchorro study relates coastal hunter-gatherer
complexity to abundant marine food, freshwater and demographic opportunity:
[PNAS 2012, doi:10.1073/pnas.1116724109](https://pubmed.ncbi.nlm.nih.gov/22891345/).
Lipid evidence from early Japanese pottery demonstrates aquatic food processing
among hunter-gatherers before farming:
[Craig et al., Nature 2013, doi:10.1038/nature12109](https://www.nature.com/articles/nature12109).
These findings justify a nonagricultural pathway, not an automatic coast-to-state
progression. The coefficients below are authored game priors, not fitted
archaeological constants.

## Actual resource and performance contract

At environment preparation, the engine resolves two native3 feeding-pool taxon
IDs once. An ordinary regional origin event then checks at most eight immediate
neighbors for real ocean access and shallow shelf, and makes two O(1) local
species-bit queries. Neither a region-wide coastal label nor a distant ocean
creates seafood at an inland origin site. Older life packs without these feeding
pools simply receive no new marine opportunity.

Food support requires actual coastal forage fish and/or shellfish presence,
potable-water opportunity, and seasonal accessibility. Permanent ice has none.
Shallow neighboring ocean is only a shelf-productivity proxy; currents,
upwelling, estuaries and changing fish populations are not simulated here.
Aridity ratio, existing river access and sufficiently shallow nonsaline water
table provide the existing coarse freshwater proxies.

The independent-culture hazard uses the larger of the existing terrestrial
opportunity and a small aquatic opportunity. Both remain subject to the actual
occupied-residence opportunity, separate forager-recognition homeland prior, population threshold,
cultural-recognition maturity, elapsed event time and named deterministic roll.
This is not a second independent roll or a population generator. Diffusion still
uses the existing physical contact and environmental compatibility rules.

If an actual new culture originates at such a productive coast, its existing
fishing/hunting/gathering shares are initialized coherently, sum to 1000, and its
description identifies specialized coastal foraging. No agriculture, crop
progenitor, domestication, city, state or population is granted. Later farming,
civic institutions, fiscal support and town/state formation retain their own
strict existing requirements.

## Recognition versus states, and portable maritime craft

History32 forager recognition does not use the state-origin homeland-distance
penalty; the pristine-state homeland prior remains exactly unchanged, including
its distant-residence provenance.
An established homeland can therefore acquire a named fishing/storage tradition
without being granted a state. The 400-resident rule, local residence, actual
resources, physical diffusion and existing 80–150k-year soft recognition horizon
are unchanged. History31 and earlier keep their original culture probabilities.

The former urban-specialist deployment factor had an unintended nonurban trap:
Storage settled at .4 deployment, so Sailing settled below .4/.7*.4=.229,
less than the .250 threshold for practical use. Oral maritime research existed
but could not become usable. History32 now allows only Storage, Sailing,
Navigation and OceanNavigation to use a bounded alternative specialist-capacity
factor (.4+.4*support), where support requires actual native marine feeding
resources, drinking water/seasonal access, a fishing livelihood and an inhabited
local coast. It is not a general urban or industrial production bonus.

Residents are estimated over a fixed roughly 100km-radius coastal catchment
from the actual origin-cell rural density, capped by its real cached regional
resident cohort. This avoids a tenfold support penalty when the same population
is divided among ten times as many cells. Zero local density remains zero; the
region's entire population is not assigned to every shore. The current local
cultural identity must still match. This is a coarse local staging estimate,
not a census of boat crews, a transfer of people or new carrying capacity.

The native coastal-practice gate uses the same physical catchment instead of a
raw 200-person cell threshold. The helper is nonrecursive and does not use a
culture-wide population to conjure local crews. Existing served state rural
ports retain their separate site-level tests.

Research levels, costs, shared finite effort, prerequisite deployment, material
supply, elapsed construction and actual route/range checks remain necessary.
Unknown techniques are never granted. A marine-only provisioning index can
satisfy the existing oral OceanNavigation practice test; it does not modify
global fiscal surplus, grain yields, farming, city or state eligibility. It is
a support opportunity, not a claim to debit fish tonnage or individual voyage
rations. Voyage-specific accounts retain their existing separate checks.

The [Sahul voyaging study](https://www.nature.com/articles/s41598-019-42946-9)
supports considering intentional seafaring before literate agrarian states;
its interpretation does not establish our numerical thresholds or guarantee
routine prehistoric transoceanic travel. Existing feeding/processing evidence
above motivates specialized coastal livelihoods, not automatic farming.

## Checks and limitations

`--marine-forager-only` checks resource necessity, inland exclusion, potable
water/ice constraints, seasonal/shelf variation and the supplied recognition prior.
`--marine-forager-world <world>` finds an actual inhabited crop-progenitor-free
coast in a saved world, invokes the ordinary culture creation path, checks the
conserved livelihood and lack of free crops/towns/states/people, and verifies
save/load integrity while preserving its input world.

The optional `--origin-suitability-audit <world> <folder>` is an explicit offline
diagnostic census, never part of a simulation turn. It distinguishes a hard
impossibility from a very low origin hazard and records actual marine support,
crop access, climate, residence, homeland and culture origins. Its instantaneous
waiting-time estimate is not a deadline or a guarantee; population, environment,
contact and event history can change.

`--forager-maritime-only` checks the separate recognition prior, physical
coarse/fine catchment scaling and real regional population cap, cold-water and
empty/ice/waterless coasts, the corrected practical deployment chain, preserved
material limits and exclusion of unrelated technology. The corresponding
`--forager-maritime-world <world>` exercises an actual inhabited productive
shore, ordinary culture foundation, no free crops/cities/states/people or
unknown craft, saved integrity and source-world preservation. Aborea20k,
Earth200k and Continents Plus200k fixtures passed. The deliberately controlled
foundation fixture is not evidence that a natural origin occurred there.

## Controlled recognition experiments

The initial .05-floor experiment replayed N Aborea73's exact saved
terrain/ecology and still had three northern traditions and no towns/states at
700k years. Its productive homeland coast's 100-year recognition probability
increased from about 9.6e-10 to 4.8e-6, but remained extremely small, and the
region's selected site was not that coast. This unsuccessful intermediate
comparison is retained rather than described as a forest-world fix.

The subsequent B2 experiment removed only forager recognition's distance
penalty. Its first northern tradition remains at 361086; two warm homeland
traditions appear at 386462/399423 in temperate rainforest around 33.5/43.2
degrees north. Farming is observed at 428k, while the first state is much later,
at 583559: Nufusi's capital Lufusi, 38.67N/115.92W, is a seasonal-river
temperate-rainforest site on a **different continent** from the initial homeland
around 34N/105E. This is a delayed conditional outcome, not a calendar target.
The state-distance prior is not bypassed by recognizing ancestral foragers.

Actual saved technical dates give a stronger practical check than a formula
alone. Warm homeland culture1 installs Sailing at 459546, Navigation at 459826
and OceanNavigation at 479267, before any state exists; Writing is not known
until 587300. Nearby inland culture2 knows those maritime techniques but does
not install them until around 589k. A tiny northern culture knows Sailing much
earlier yet also lacks practical deployment until late. Knowledge is therefore
not equated to a free usable port or a literate state.

With the same frozen B2 source, measured-Earth200k observes farming at 114000
and first state119944, with one pristine origin; Continents Plus200k seed261015
observes farming at149000 and first state154770, with two pristine origins.
Aborea ends its review with three pristine origins. Controls continue 1,000
years beyond observed postmodern capabilities, not merely to first state.
No extra hazard uplift or state-probability change was made to obtain these
dates. These three controls do not establish a universal origin-time distribution
or complete political/technological realism; the long forest-world farming-to-state
gap and later expansion remain subjects for broader review.

Actual exported state and technology screenshots were also inspected, not just
the review tables. Early Continents Plus has two localized states around 230
years after its first origin, not simultaneous civilization on every continent.
Its late coastal settlement networks reach every major landmass, with sparse
harsh interiors and geographically shared technology. Earth still has a much
less satisfactory late settlement distribution: most towns cluster in eastern
North America, Egypt/Maghreb and southeastern South America, while India,
China and much of Europe remain sparse. Removing a misplaced forager-recognition
penalty does not by itself establish realistic late colonization or settlement
distribution. These controls were exact saved-terrain/ecology history replays;
they do not isolate other pending history32 changes from each other.

This feature does not claim that foraging cultures are inferior, that all
nonfarmers lack cultural complexity, or that fishing inevitably produces states.
The simulation's advanced-culture actor is a persistent regional exchange/storage
network, not the existence of culture or intelligence in otherwise ordinary humans.
