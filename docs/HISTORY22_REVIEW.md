# History-22 review - 15 October 2026

This pass changes the historical model, not generated terrain, climate, ecology or the climate classifier. New histories use `history-22`; existing saves keep their recorded rules until an explicit upgrade. An upgrade does not repair an implausible recorded past. The preceding history-20/21 mechanisms and performance work are described in [HISTORY21_REVIEW.md](HISTORY21_REVIEW.md).

## What changed and why

### People must be able to pass through poor habitat

An unintentional migration gate required each intermediate habitat patch to sustain 125 residents before a band could continue. Small resource patches could hold people but permanently block a larger, viable destination. History-22 contracts these small land patches into a bounded static passage graph during setup. It inspects at most 32 regional nodes per source, retains at most 12 passages, excludes ice and terrain above 4,500 m, and limits a passage to 500 km. No recurring event searches terrain membership. Four bounded departure opportunities still conserve actual migrating residents. Low-density frontier communities have more recovery room than established, crowded populations.

In the same Earth environment, this changed substantial American habitat occupancy before elapsed year 100,000 from 269/578 patches to 573/578, and Australian occupancy from 0/102 to 102/102. These are diagnostic regional counts, not literal archaeological dates. It fixes a mechanical choke point rather than granting every continent inhabitants on a global deadline.

### Agriculture must be practiced and ecologically plausible

Known, transferable crop adoption is easier than independent domestication. Colonists carry plant genetics and knowledge, but do not inherit perfect experience with a new habitat. Selective improvement only accrues when crops are actually cultivated and fit the local environment. Merely knowing a crop for ten millennia does not breed it.

Wild annual-grain opportunity now distinguishes seasonal/open environments from continuously warm closed forest. Crop-specific reproductive checks distinguish cool grains, heat-stressed potato tubers, frost-sensitive cassava/yams, olive winter requirements, dry date-ripening weather, and warm/moist coconut conditions. Irrigation can supply date palms with water without making humid harvest weather irrelevant. These are broad smooth priors with rare tails, not climate-label bans or a requirement to start in the Fertile Crescent. Existing soil, growing-season, flooding, salinity, harvest, food-storage and local-farm-account constraints remain.

### Seas can connect institutions as well as settlers

Successful voyages can establish lasting bounded port contacts. Two slots in a town's existing twelve-contact list can preserve earned distant coastal connections. Shipping reduces supply/administrative cost only for actual coastal endpoints, sufficient maritime capability, an eligible range and a physically checked ocean lane. An inland road does not become cheap because somebody invented sailing. A bounded sample of a state's actual ports can improve maritime administrative reach; no state-wide cell census or automatic overseas annexation is introduced.

The mechanism follows transport-cost and local-institution reasoning, not copied ORBIS code or Roman geography. Explicit client/tributary relationships and resolved vessel/army movement remain incomplete.

## Longitudinal evidence

Each world is run from its actual founding band through all 37 capabilities and at least 1,000 further years. Era snapshots are replayed independently from the same start; final history hashes must match. The nine observer eras do not force inventions or synchronize societies. Reports also count the people associated with a capability, rather than mistaking one inventor or a count of culture IDs for global access.

| Final run | First state, elapsed years | All capabilities | Final year | Population | Active states |
| --- | ---: | ---: | ---: | ---: | ---: |
| Earth, 200k cells | 106,936 | 113,919 | 115,000 | 2.923 billion | 229 |
| Continents Plus, 200k cells | 106,178 | 112,387 | 114,000 | 6.937 billion | 369 |
| Archipelago, 80k cells | 127,425 | 134,632 | 136,000 | 1.037 billion | 92 |

Earth era samples: Bronze Age 3 states / 60.4 million people; Iron Age 33 / 86.0 million; Medieval 39 / 108.4 million; Early Modern 107 / 196.9 million; Industrial 205 / 403.7 million; Modern 218 / 634.1 million. The first state's full capability set follows about 7,000 years later, not a fixed tech clock. At the Industrial sample steam access covers about 24% of the population; by the final sample about 99%. These are culture/institutional-access proxies, not measured literacy, household electricity or manufacturing supply chains.

Ignored evidence folders: `artifacts/history20/pass22d-{earth,continents,archipelago}` and `era22d-*`. Earth final hash: `0852c723de3e0519c32806d3810b1dd8c8186fae7efd9b749b17ef697ddff198`; Continents Plus: `c9ede1647f0a4cd13920e6bb95ed6a6ef66e64f97296cbcaba956018d1698c36`; Archipelago: `84a3b288759abfa9fc0c6541d16103d3ea03f950ec6b5adaef83a5becb68dd7f`.

The Earth archive records four independent institutional origins in the first2,000 years after the first state (fourteen over the complete run),281 contact-supported foundations,1,223 abandoned sites and5,760 settlement conquests. Median surviving government age is161 years. A government succession is not automatically a vanished civilization; inherited local communities retain their culture. Surviving towns have a much longer median age of3,298 years, with one site11,201 years old: settlement persistence still deserves comparative attention rather than declaring every ancient site a bug or every survival realistic.

Final native Unity200k acceptance runs continued another1,000-plus years beyond those saved endpoints: Continents Plus4.780years/s, worst calculated year706ms; Earth7.874years/s, worst calculated year442ms. Both passed the requested3years/s / no year above1second gates. See VALIDATION.md for frame/rolling metrics and evidence paths. These are host-specific samples, not unlimited actor-count guarantees.

## Visual atlas, not fabricated local reconstruction

Eight original illustrative habitat paintings accompany actual local seasonal/flooding conditions and suitable recorded native fauna. They are clearly labeled illustrations, not photographs or reconstructed city landscapes. Pack taxa, variants and body masses come from actual ecological records; the paintings contain no animals whose presence could be invented. Unsupported tidal, saline, ocean and ice habitats deliberately omit an illustration. Human appearance and architecture are not guessed from biome. The asset provenance and complete production prompt set are in [assets/atlas/README.txt](../assets/atlas/README.txt), mode: built-in image generation.

The atlas also shows population-weighted reach of practices. These are on-demand, read-only actor/account summaries, not additional simulation turns. Reader use must not mutate history. Images load lazily and do not affect historical or generation hashes.

## What is still not satisfactory

The maps are reviewed, not declared a finished historical simulator. Premodern direct states remain relatively small beside major historical empires; client/tributary power and indirect rule need deeper treatment. World-scale label candidates are collision-filtered for readability, not evidence that an omitted label has no inhabitants. Institutional culture-level capability is coarser than actual economic adoption. Earth uses a simulated native crop geography, not an inventory of actual Earth progenitor ranges, so its first state in this run is in temperate eastern North America; the second is on a South American seasonal-tropical floodplain. That is not a reconstruction of Earth history. No Köppen label or literal homeland script is used to conceal the difference. States, towns, transport, food systems and political turnover remain model approximations; there is no scientific-validation claim.

The default full test run still stops at an existing stale Earth v3 stage-hash fixture (11 expected stages versus the current 15-stage ecology/subcell pipeline). It has not been regenerated to make this pass green. Focused integrity, accounting, version-continuation, work-bound, crop and presentation tests are reported separately in VALIDATION.md. Hidden native-window ScreenCapture HUD images are black on this host; native offscreen map captures and browser UI captures are separate evidence, not interchangeable claims.

## Independent research used

These support qualitative mechanisms, not the numerical gameplay coefficients:

- [Stanford ORBIS project](https://history.stanford.edu/publications/orbis-stanford-geospatial-network-model-roman-world) and [Roman transport-cost study](https://www.cambridge.org/core/journals/european-journal-of-archaeology/article/transport-costs-and-economic-change-in-roman-britain/09FD867DD0C956EC284E538DDD72F36B).
- [HYDE 3.2 population and land-use reconstruction](https://doi.org/10.5194/essd-9-927-2017) and [early agricultural boom/bust](https://doi.org/10.1073/pnas.1602504113).
- [Population connectivity and cultural accumulation](https://www.nature.com/articles/ncomms9398), [social scale and information processing](https://www.nature.com/articles/s41467-020-16035-9), and [demic/cultural agricultural diffusion](https://pmc.ncbi.nlm.nih.gov/articles/PMC4424695/).
- [Beringian migration research](https://pmc.ncbi.nlm.nih.gov/articles/PMC9832545/), used as mechanism context, not a fitted migration timetable.
- [FAO date-palm climate requirements](https://www.fao.org/4/y4360e/y4360e08.htm), [olive winter experiments](https://pmc.ncbi.nlm.nih.gov/articles/PMC10143890/), [potato thermal experiment](https://pmc.ncbi.nlm.nih.gov/articles/PMC8385365/), [FAO cassava](https://www.fao.org/4/x5032e/x5032e01.htm), and [FAO coconut](https://ecocrop.apps.fao.org/ecocrop/srv/en/cropView?id=744).
- [Cliopatria primary dataset, CC BY 4.0](https://github.com/Seshat-Global-History-Databank/cliopatria), independent comparison maps only; not shipped gameplay data.
- [Ollie Bye animation](https://www.youtube.com/watch?v=-6Wu0Q7x5D0), sampled at 15-second intervals for visual comparison. An unlabeled area in that video is not evidence that it lacked a culture.
