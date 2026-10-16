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
occupied-residence opportunity, human-homeland penalty, population threshold,
cultural-recognition maturity, elapsed event time and named deterministic roll.
This is not a second independent roll or a population generator. Diffusion still
uses the existing physical contact and environmental compatibility rules.

If an actual new culture originates at such a productive coast, its existing
fishing/hunting/gathering shares are initialized coherently, sum to 1000, and its
description identifies specialized coastal foraging. No agriculture, crop
progenitor, domestication, city, state or population is granted. Later farming,
civic institutions, fiscal support and town/state formation retain their own
strict existing requirements.

## Checks and limitations

`--marine-forager-only` checks resource necessity, inland exclusion, potable
water/ice constraints, seasonal/shelf variation and the preserved homeland prior.
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

This feature does not claim that foraging cultures are inferior, that all
nonfarmers lack cultural complexity, or that fishing inevitably produces states.
The simulation's advanced-culture actor is a persistent regional exchange/storage
network, not the existence of culture or intelligence in otherwise ordinary humans.
