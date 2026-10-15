# Historical review in progress — 15 October 2026

This is an experimental history-21 checkpoint, not a claim of a finished historical model. Generation, terrain, climate, ecology and their version are unchanged. Older histories retain their recorded rules unless explicitly upgraded. Existing history-19 continuation was compared against the previous executable, with identical hashes.

## Mechanisms under review

* Actual rural producers and transported surplus support towns; potential empty acreage is not food. Regional and country ledgers conserve migrants and settlers.
* Crops use a plausible adjacent-season growing window rather than necessarily the hottest quarter. Soil, growing-season length, leaching, flooding, cultivar transfer and storage still matter. There are no Köppen-label bans or scripted Earth homelands.
* Knowledge offers have dates and delivered capabilities. Knowing a person centuries ago does not transmit that person's future discoveries. Research networks count relevant supported communities, not arbitrary old acquaintances.
* Campaign throughput uses physical area and supply rather than a fixed count of cells. Local inclusion, succession, representation and administrative strain affect political continuity. No fixed state expiry is imposed.
* A fixed-resolution ocean graph connects actual shores. Recurring voyages inspect at most four routes and require actual source residents, maritime practice and provisions.

## Measured performance checkpoint

Native Unity player `player-history-21-throughput`, 200,000 cells, the actual same actor calendar and model (no skipped years/events):

| Fresh longitudinal world | Extra years after modern capabilities | Overall years/s | Slowest rolling window | Maximum work charged to one year |
| --- | ---: | ---: | ---: | ---: |
| Earth | 1,007 | 8.88 | 8.62 | 484 ms |
| Continents Plus | 1,005 | 3.60 | 3.45 | 977 ms |

Evidence is in ignored `artifacts/history20/unity21h-{earth,continents}-performance/playback.json`. The generated case began with 534 active states and 2,181 living centers. These are host-specific measurements, not an unlimited scalability promise.

Execution improvements include typed canonical writers checked against the reference encoder, immutable Merkle-node reuse, trusted terminal-record byte reuse inside private transactions, detached public snapshots, bounded geography/contact caches and less frequent fast-playback publication. Explicit validation recomputes actual public fields, including terminal records; editing and resealing remains supported. Publication still has actor/archive costs; it is not mathematically constant-time for arbitrary histories. Rendering may enumerate cells; ordinary historical events do not census terrain holdings.

## Evidence and outstanding issues

Nine observer-era maps are replayed from the founding band, and the final hash must match the longitudinal run. Earth and generated worlds are examined separately. Actual Ollie Bye frames at 15-second intervals and independent Cliopatria historical political maps are reference observations, not simulation inputs or fixed targets.

The current Earth run still concentrates early agricultural institutions around the first homeland for too long, with late overseas catch-up. Its modern final map alone is not sufficient evidence of realism. Global diffusion, regional settlement, demographic scale and political turnover remain under review. Small labeled cultures are not automatically centralized states, and an area absent from a reference video's labels is not evidence that it had no culture.

Focused tests currently cover canonical byte equivalence, old-history continuation, cancellation/rollback, public isolation and edit/reseal, bounded event work, crop calendars, accounting and dense-render equivalence. The default full test run stops at an existing outdated v3 Earth stage-hash fixture (11 expected stages versus the current ecology/subcell pipeline); that golden has **not** been regenerated. Final host/UI validation remains outstanding.

## Research context

These support qualitative mechanisms, not the numerical gameplay coefficients:

* [HYDE 3.2 land use and population reconstruction](https://doi.org/10.5194/essd-9-927-2017).
* [Early agricultural population boom/bust](https://doi.org/10.1073/pnas.1602504113).
* [Population connectivity and cultural accumulation](https://www.nature.com/articles/ncomms9398).
* [Social scale and information-processing institutions](https://www.nature.com/articles/s41467-020-16035-9).
* [Stanford ORBIS transport model](https://orbis.stanford.edu/orbis2012/ORBIS_v1paper_20120501.pdf).
* [Cliopatria primary dataset, CC BY 4.0](https://github.com/Seshat-Global-History-Databank/cliopatria), used independently for visual comparison, not copied into the game.
* [Ollie Bye's historical animation](https://www.youtube.com/watch?v=-6Wu0Q7x5D0), viewed as an illustrative geographic/chronological reference, not an exhaustive classification of societies.
