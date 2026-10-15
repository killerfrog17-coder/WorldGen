# Ecoregions and the Earth starter pack (3.28)

## Current content extension — 14 October 2026

New recipes select `earth-native-2`: 96 entries, including additional forest mammals, reptiles, birds and wild husbandry candidates, serialized behavior/utility traits and regional forms. Missing/null profile preserves the original 50-entry pack below. Existing saved ecology is never silently replaced: regenerate to acquire the new native assemblages. Updating old history rules alone does not invent missing fauna.

The expanded pack still uses native homelands and bounded dispersal, not global climate-only placement. Temperate envelopes and added regional forest communities address implausible tropical assemblages. Procedural homeland selection discourages stranding a whole continental fauna on a tiny first-species islet. Food links remain diet/stratum/size/co-occurrence constrained and basal-rooted, with no fixed trophic depth. The diagram lays out actual dependency components, including omnivory/cycles; it does not estimate population or biomass.

Regional forms retain taxon identity with bounded size, insulation, lightness, eligible seasonal whitening and modest behavioral differences. Historical husbandry uses the local behavior priors. These are provisional morphotypes, not speciation or measured range maps. The native horse entry is not a claim that domestic horses descend from Przewalski's horse. The pack remains representative and lacks many actual crop/livestock progenitors. ALGORITHMS.md records current contracts, history integration, old-hash behavior and research context.

The owner authorizes static ecological synthesis: abiotic habitat fields, potential-natural biomes, regional identities, native assemblages and inspectable food webs. Modern native species, no human introductions or land-use destruction; on procedural planets homelands are generated rather than copied from Earth. This is not population dynamics, evolution, civilization or an exhaustive Earth biodiversity inventory.

## What to use

Select **Ecoregions** for regional colors/borders, **Biomes** for vegetation structure, or the new Floodplains, Soils, Water Table, Permafrost, Aridity, Salinity and Fire Potential diagnostic layers. Click a cell and expand/scroll the inspector for regional species and resource-to-consumer links. An asterisk identifies taxa actually present in the selected cell, not just elsewhere in its region. Older saved worlds remain unchanged; regenerate and save a new copy to acquire ecology.

## Layer 1: habitat conditions

All fields are downstream of existing climate and hydrology. No terrain, coastline, precipitation, temperature, runoff, water storage or receiver is rewritten.

| Stored quantity | Meaning / limitation |
| --- | --- |
| PET, seasonal water deficit, P/PET | Oudin-style radiation/temperature demand estimated at four quarterly means; P/PET capped at 20 for storage. Not actual evapotranspiration or added moisture. |
| Flood fraction, days/year, pulses/year, regime | Discharge/width, local relief, lake margins and coastal elevation estimate inundation potential; river and tidal regimes are separate. These are priors, not fitted flood return periods. Fractional floodplain information remains below display thresholds. |
| Water table, soil class/fertility | Recharge, local drainage relief and wetness proxies; not a groundwater head solution, lithological map, measured soil profile or agricultural suitability guarantee. |
| Permafrost likelihood | Mean-temperature climatological proxy; not ground thermal physics or an active-layer depth. |
| Water-body and cell salinity | Dissolved weathering-load proxy, storage, throughflow and synthetic accumulation history; ocean 35 ppt, closed waters can be hypersaline. Not measured Caspian/Dead Sea chemistry or an ion/sediment budget. |
| Fire potential | Fuel productivity combined with seasonal moisture deficit; no actual fires or fitted return intervals. Supports open/wooded habitat distinctions. |
| Potential productivity | A precipitation/temperature/growing-season proxy in gC/m2/year. Not net resource supply allocated among populations. |

Tidal classification is a low, flat, directly coastal subgrid prior. 730 potential pulses/year represents a generic semidiurnal coast, not a global tidal model; duration is the number of days with recurring tidal influence, not 365 continuously submerged days. River pulse count is currently a one-season prior. Quarterly averages cannot establish real annual flood probabilities. Do not consume these fields as calibrated civil-engineering hazards.

## Layer 2: regionalization

Biome classification uses thermal limits, evaporative demand, seasonality, flooding, salinity, soil wetness and fire suitability—not just the Köppen code. The accepted climate classifier remains unchanged. Seasonal floodplains, freshwater, saline lakes, tidal marshes and mangroves have distinct habitat categories. Whole-cell freshwater classification retains the existing 90% lake coverage rule.

Area-weighted, named-seed province sources grow over non-ocean adjacency with distance, relief and aridity resistance. Every disconnected land component has a source. Each province/biome combination becomes an ecoregion with stable ID, area, cell membership, representative cell, neighbors and patch count. Multiple same-biome patches may share a region; oceans cannot join them. There is no one-region-per-climate rule, no plate-polygon mask and no copied observed Earth ecoregion boundaries. Small islands and small habitat patches are retained, so region count varies with resolution. Boundaries are a procedural first approximation, not an inferred reconstruction of Earth's actual named regions.

## Layer 3: native biogeography and trophic structure

`EarthLifePack.Create()` supplies **50 entries**, a representative mix of real plant/animal species, an explicit wolf subspecies and labeled functional guilds. The latter represent unresolved local diversity; they are not single cosmopolitan species. In particular basal grasses, invertebrates and aquatic guilds avoid claiming complete plant/insect/fish inventories. Detailed marine ecology is not included.

Traits are explicit, serialized `Taxon` records, making future packs possible without changing the food-web/dispersal interfaces:

| Trait | Use |
| --- | --- |
| Scientific identity, rank, parent species | Separate species, subspecies and unresolved guilds. Arctic wolf is `Canis lupus arctos`, with parent `Canis lupus`; parent/subspecies are not counted twice per cell. |
| Habitat flags, coldest/warmest-quarter limits, aridity, elevation, flooding and salinity tolerances | Resident habitat suitability; tolerances are procedural ecological priors, not measured physiological extrema. |
| Passage cold/heat limits, habitat-gap distance, ocean-crossing distance, spread budget, historical-interchange eligibility | More permissive passage envelope and finite accessibility; no unlimited climate-only global flood fill. Cold tolerance alone does not assert that every modern species used ancient bridges. |
| Origin group and native Earth anchor | Earth provenance; procedural worlds place separated group homelands and keep group origins on their homeland landmass. Species may be absent when no suitable homeland exists. |
| Body mass, min/max routine prey mass, diet, foraging/occupied strata | Gates trophic links. Pack hunting is represented by expanded routine prey envelopes, not pack demographics. |
| Required resource | Specialized diets, currently koala dependence on the represented eucalyptus. |

Earth anchors constrain origins, not climate, habitat or final ranges. They are approximate provenance points, not range polygons. Current and historical accessibility are separate graph passes: eligible taxa can use a historical scenario exposing bathymetry down to -120 m and cooling route temperatures by 5 C. Holarctic taxa and lion are eligible in this starter pack; the lion's cold limit still blocks Bering-like routes. Named plants and other regional taxa are not automatically granted historic interchange merely for tolerating cold. Coldest-quarter upper bounds exclude generic gray wolves/foxes from hot year-round rainforest and keep cold-forest specialists out of tropical lowlands. These are authored pack priors, not reconstructed dispersal events. The historical scenario is **not** a reconstructed ice-age climate/ice-sheet/sea-level chronology. A deep ocean cannot become a land bridge. Coarse grids can miss narrow real straits.

Each pass stores a best resistance route per cell, with per-route unsuitable-gap and sea-run limits. This is a conservative bounded routing heuristic, not a complete multiobjective reachability solver: a longer valid alternative can be missed. The historical pass does not model ecological succession or extinction following climate change.

Presence is retained as a compact per-cell bit matrix plus area-weighted regional summaries. Food links require actual cell co-occurrence, diet compatibility, physical access to the prey's stratum, prey-size compatibility and specialist resource compatibility. Examples exercised by tests: wolf-to-hawk and wolf-to-fly are excluded; hawk-to-mouse, web-spider-to-fly and lion-to-zebra are allowed. Healthy adult elephant is outside the routine lion prey envelope; calf/egg/larval stages are not yet represented. All consumers must be connected to a local basal producer/detrital resource; unsupported predator cycles cannot support themselves.

An additional adult-predator risk gate limits predatory prey to one quarter of the consumer's own mass. This prevents the wolf's pack-sized ungulate envelope from making adult lions routine prey; it is a conservative authored defense proxy, not a combat simulation.

The displayed web describes **plausible feeding opportunities**, not observed diet percentages, population carrying capacities, abundance, competitive exclusion, migration calendars or dynamical stability. Several predators sharing one prey species does not prove enough prey biomass exists. Those need a subsequent resource/energy-budget and population model; the current implementation deliberately does not fabricate abundance numbers.

## Determinism and persistence

`WorldPipeline` alone runs habitats → ecoregions → biogeography after hydrology. New recipes are 3.28; 3.27/3.26/3.25/3.24 remain supported. An optional `World.Ecology` field is omitted from canonical hashing when null, preserving historical snapshot hashes. The 3.27 mountain field remains hashed in both 3.27 and 3.28. Numeric fields are quantized, graph costs/ties ordered, and random samples use named project PRNG streams. No external life-pack file, observed range dataset, worker timing or renderer changes authoritative state.

The Earth starter pack version is persisted with its complete trait records. Changes to default traits or ecological algorithms require versioning. New packs are a future recipe/content-version extension, not a renderer-only replacement. Tests cover fresh/reapply/recipe/cached reconstruction, binary/JSON persistence, native/browser parity, resident suitability, trophic co-occurrence, and bridge barriers. No old golden expectations are automatically regenerated.

## References and provenance

Concepts were independently implemented; no reference-project source or external species table was copied. Numerical trait thresholds and proxy coefficients are initial authored priors, **not** an imported/calibrated trait database.

- [Ecoregions 2017 map](https://ecoregions.appspot.com/) and [Olson et al., terrestrial ecoregion definition](https://digitalcommons.usf.edu/kip_articles/4884/): geographic assemblages, not merely climate labels. The map's downloadable CC-BY dataset is not bundled here.
- [EltonTraits data paper](https://esajournals.onlinelibrary.wiley.com/doi/10.1890/13-1917.1): body size, diet and foraging-stratum trait framework; its dataset is not imported.
- [FAO dryland definition](https://www.fao.org/dryland-forestry/background/en) and [INRAE Oudin PET model](https://webgr.inrae.fr/eng/tools/hydrological-models/evapotranspiration-model): evaporative demand versus precipitation; quarter-mean application is our approximation.
- [US Fish and Wildlife wetland classification](https://www.fws.gov/policy-library/660fw2): distinguish water regimes and chemistry.
- [USGS closed-lake salinity](https://www.usgs.gov/publications/salinity-and-hydrology-closed-lakes): salts, throughflow and concentration concepts, not justification for our synthetic accumulation times.
- University of Michigan Animal Diversity Web: [lion](https://animaldiversity.org/accounts/Panthera_leo/) and [gray wolf](https://animaldiversity.org/accounts/Canis_lupus/) habitat/diet context. Older taxonomy in these accounts is not treated as a complete current taxonomic authority.
