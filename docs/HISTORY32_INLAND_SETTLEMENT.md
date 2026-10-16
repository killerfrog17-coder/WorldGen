# History32: bounded, provisioned inland settlement reach

Experimental S2; generation, ecology, technology research weights and historical
versions through31 are unchanged. This corrects proposal geometry and migration
costs, not worldwide diffusion or historical realism as a whole.

## Diagnosis and matched controls

The saved N Lakes17 world already had **per-city** settlement opportunities.
N used an area-scaled local random walk; O replaced that geometry with three
spherical probes. It is incorrect to attribute this particular N result to the
older, removed culture-wide parent lottery.

On the same year123500 checkpoint, N and O both had206 eligible parents among
291 living centers and the exact same summed annual opportunity2.204921096.
Their actual current-year commit-ready proposals were79 and63, respectively:
O is not uniformly superior in every individual geometry sample. After another
1500 simulated years, N had619 living towns and O734. Actually viewed state and
technology PNGs showed that both remained predominantly an eastern-hemisphere
urban/technological network. More waiting alone did not solve that separation.

The exact existing land-route, crop, household, service, political-access,
food and treasury tests admitted considerably farther targets which the fixed
120/180/240km proposal windows could never directly select. An offline
counterfactual at five radii found1226 fully eligible target/source pairs,
including11 near2000km; this diagnostic is not a new runtime search. For example,
actual parent325 at cell14489 could supply cell15612 at34.39N,90.90E,2008km
away: source rail deployment0.912, land transport4.436, crop yield0.307,
transmission0.885, real service/route/capital/households all passed. The region
was unruled, not a freely conquered foreign state.

S2 intentionally does not use every such distant route. Its proposal cap is
1080km and every actual attempt still needs the complete existing gates.

## Runtime mechanism

There are still only three probes, with the same seeded bearings, up to four
nearest-cell refinements and the same annual actor opportunity. The first
probe remains120km. The second and third interpolate towards expanded physical
reach from the source culture's deployed land-transport capabilities, moderated
by the currently funded state's public works. No state, no funded fiscal
surplus, or no deployed transport retains the original distances. This is an
effective service/logistics proxy, not a claim to simulate individual railway
lines or possess trains merely because an invention is known.

Actual source/destination route, landmass, terrain, service radius, claim,
political access, cultivar fit, transferable households, spacing and farmland
checks are unchanged. Longer proposal geometry cannot erase a jungle or desert
barrier or make new cultivated species appear.

Each committed household migration now also pays quantized inland logistics:

- Effective journey days:7 staging days plus terrain-adjusted travel effort
  divided by15km/day times deployed land transport.
- Additional food:1.5 times the cohort's person-years during that journey,
  in the same food-account convention as maritime voyage rations.
- Fiscal cost: the former construction investment plus carriage proportional
  to cohort size and terrain-adjusted distance, reduced by actual transport.

These are transparent authored priors, not fitted historical ration prices or
train timetables. Preflight uses the exact same founder quantity as commit and
retains source food. Ordinary founder transfer already debits actual people and
startup food; the caller charges only additional journey food and the combined
construction/carriage investment after a genuinely successful foundation.
Failures do not charge resources. Migration remains abstract relocation during
the local city update, not a new continuously tracked travel actor.

The historical motivation is improved funded transport enabling farther
settlement while still requiring real provisions, labor and infrastructure.
The National Park Service documents both the settlement/supply effects and
the labor and material costs of the transcontinental railroad:
[Promontory Station](https://www.nps.gov/gosp/learn/historyculture/promontory-station.htm),
[Chinese labor and the Iron Road](https://www.nps.gov/gosp/learn/historyculture/chinese-labor-and-the-iron-road.htm),
[supplied railroad camps](https://home.nps.gov/articles/archeology-of-chinese-laborers-connected-country.htm).
These support the mechanism, not the numerical game constants above.

## Verification and limits

The S2 isolated build has zero warnings. `HistoryInlandSettlementTests` passes
32 pure assertions and16 actual Lakes integration assertions: real farther
targets, no unfunded reach, no undeployed transport, public-works moderation,
no extra-ration waiver, no missing-crop waiver, physical-route rejection,
unchanged annual hazard, exact population/food/fiscal conservation, no repeated
debit, save/load and publication-partition continuation. The existing actual
settlement-network integration suite passes15 assertions with its explicit new
travel-cost expectation. Old31 Earth+20 years retains exactly
`f4e260d19883e9dfd894c63c9ff8215646b023fdb25090d3795819e6f48d2fef`.

Matched pre-S2 T versus S2, starting from the identical N Lakes17 snapshot and
running1500 more years:

| Native offline observation | T | S2 |
| --- | ---: | ---: |
| Living towns | 557 | 1103 |
| New foundations | 647 | 1038 |
| Abandonments | 381 | 226 |
| Living governments | 78 | 30 |
| World population | 4.508 billion | 4.619 billion |
| Summed worst-year work | 86.8ms | 93.6ms |
| Continuation wall time | 9.43s | 11.54s |

Political and demographic trajectories can diverge following actual different
foundations; the table does not attribute every later difference directly to
one food charge. More towns is not itself the acceptance criterion. Both the
author and an independent critic actually viewed state and technology PNGs:
S2 broadens the real urban footprint north/south/west of the dense original
core, but most of the opposite hemisphere remains technologically disconnected.
This is **not** acceptance of a globally realistic postmodern map. Separate
long-range contact, technical dissemination and food/support restrictions still
need causal investigation; this change does not waive them.

Compact reports, world checkpoints and viewed maps are outside source control
under `D:/WorldGenBuildArtifacts/history32-lakes17-{n,o,t,s1,s2}-continuation`.
## Full saved-world history replays

Both additional controls reset history through ordinary preparation/start while
preserving the exact saved N200k terrain, climate and ecology. Both completed
1000 years after the first postmodern observation and passed final validation.
The table uses the last routine image sample,900 years after that observation:

| Observation | Earth73421 | Continents Plus261015 |
| --- | ---: | ---: |
| First state, elapsed year | 119944 | 154770 |
| First Bronze observation | 122900 | 156600 |
| First early-modern observation | 128100 | 162400 |
| First postmodern observation | 128700 | 163000 |
| Last image year | 129600 | 163900 |
| World population at that image | 699823945 | 1253697844 |
| Living centers / governments | 306 / 40 | 754 / 38 |
| Sponsored port foundations | 23 | 148 |
| Worst summed-year native work | 82.1ms | 255.7ms |

Wall times were112 and141 seconds, including repeated four-layer compressed
200k-cell map exports; these are not Unity measurements. The author actually
viewed Bronze, early-modern and late state images and late technology maps.
The Continents Plus world has actual populated overseas clusters on every major
landmass, with sparse desert/mountain interiors. Earth has a denser eastern
North American urban network and actual Atlantic, Iberian, Maghreb and
Mediterranean ports, but India and much of Asia remain extraordinarily sparse
and outside the modern technical network. This is an unresolved realism failure,
not a successful global-postmodern validation. The first-state-to-postmodern
intervals are about8756 and8230 years, also longer than the owner's approximate
Earth-history comparison. No research weights were changed to hide that result.

Viewed images and complete frozen-world histories are retained under
`D:/WorldGenBuildArtifacts/history32-s2-earth200-replay` and
`D:/WorldGenBuildArtifacts/history32-s2-cp200-replay`. Their accepted abiotic and
ecological snapshots were not regenerated to obtain a preferred historical
outcome. Target selection, physical contact and real local supply remain the
next diagnostic questions; a generalized free food/crop bypass was not added.
