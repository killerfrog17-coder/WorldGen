# History 32: origin-cap and opportunity review

This records the October 16 M/N experiments, not a claim that three worlds
validate historical realism. Years below are elapsed simulation years, not
Earth calendar dates. Existing history-31 saves retain their original rules.

## Mechanisms tested

M changes only the soft population ceiling for a prestate center. Its local
network factor is now:

```
.5 + .5 * max(clamp(actualTownLinks / 3),
              .7 * clamp(actualSurroundingFarmingResidents / 10000))
```

The second input is the town's existing `city.Rural` resident cohort, including
dependents, not a count of agricultural workers, potential farmland or the
population of its whole culture. A substantial real rural provisioning
community can support coordination even without another separately tracked
town. The food/worker-supported `CommunityUrbanCapacity` remains a separate
minimum: this change cannot create people, crops, food or institutions. A
zero-rural-population four-argument source call retains the former ceiling;
compiled callers must still use matching assemblies after the optional fifth
parameter was added.

N changes the independent-state hazard's compounded coordination factor from
`core^4` to `core^2`, retaining the same `.00025` coefficient, tiny base term,
environment multiplier, squared staple suitability and human-homeland prior.
The maximum-core prior is unchanged. Poor local surplus, unsuitable ecology,
unavailable domesticates, isolation and the homeland penalty remain separate
constraints. There is no date unlock, worldwide civilization grant or new
rainforest exception. Actual provisioning, stability, civic preparation and
farming checks still precede the origin opportunity. Physical diffusion and
learned institutions are not replaced by this independent-origin hazard.

## Actual M/N controls

| World | Neolithic observation | First state, M | First state, N |
| --- | ---: | ---: | ---: |
| Earth, seed 73421, 200k cells | 114000 | not this paired control | 119920 |
| Continents Plus, seed 261015, 200k cells | 149000 | 165143 | 164087 |
| Archipelago, seed 42, 20k cells | 182000 | 313164 | 313164 |

Earth's roughly 5,920-year and Continents Plus's 15,087-year observed delays
are encouraging controls, not calibration targets imposed on generated maps.
Archipelago still has a 131,164-year observed farming-to-state delay; it is
not described as fixed. M alone improved that seed over the earlier L control
(first state 638449), but later historical trajectories diverge, so counts of
states or final populations are not monotonic measures of success.

Actual M state/technology/culture PNGs were inspected around first state and
late history, including Archipelago `year-314000` and `year-326700`, and
Continents Plus `year-166000` and `year-172900`. The Archipelago first center
is a localized Csa site around 28 degrees north, not a rainforest origin;
late maritime networks reach numerous islands. The Continents Plus early
capital's rapid urban growth remains a separate concern, not proof of a
realistic early urban system. Earth N and the broader independent map
criticism are separate evidence from the origin arithmetic below. Screenshots
cannot establish that an origin lottery or its calendar is correct.

## Why Archipelago M and N have the same first year

A diagnostic replay used the ordinary prepare/start/event calendar on the
same final N world's terrain and complete recipe, with only history reset.
It verified the loaded Core SHA-256 against frozen N2:

`B40D212B2E3307AEEDBBB68A7529B67AFA4F7D3AC2FCCB116175896173AC3BE9`.

The read-only diagnostic samples active town actors every 50 years; it does
not add production simulation turns, force resources or change thresholds.
Three standard validated saves retain the actual 200000/240000/280000 states.
It then follows the first founder's real scheduled event to the exact origin.

At year 313164, town 1131, He leno, has 3,361 urban and 25,396 surrounding
rural residents. Its pre-origin support values are approximately `core=.4985`,
`environment=.4761`, `staples=.8101`, with homeland multiplier 1 and 10 elapsed
political years. The named 53-bit origin draw is **.0000393763**. The event's
N probability is **.0001950052**; its counterfactual M probability is
**.0000484665**. The draw passes both. No earlier N-only success occurred, so
the identical first year does not imply the new formula was unused.

Before that event, sampled cumulative eligible hazard is approximately
**1.01993 for N**, versus **.20455 for M**. Under the model's conditional
hazard interpretation, `exp(-1.01993)` gives about **36%** survival without an
origin. This is a coarse exposure estimate, not an exact event likelihood,
and is not a probability that the model itself is historically correct.

The chronology explains much of the long delay:

- The first sampled qualifying town appears at 190500, but no active towns
  remain at the actual 200000 checkpoint.
- At 240000 there are three active towns, two qualifying; their coordination
  cores are only about .127-.166.
- At 280000 only one of five active towns qualifies. A strong crop site has
  only 493 urban residents and insufficient preparation.
- At 300000 one of four active towns qualifies, now with 3,669 urban and
  67,663 rural residents and core .535.
- Across the replay, 255 distinct towns qualify at some sampled time, but
  generally only zero to six qualify simultaneously. Approximately 241,315
  qualifying town-years accumulate. Most exposure occurs late.

Overlapping failed-opportunity totals are dominated by small towns and short
civic preparation; lack of storable crops accounts for only about 6,100
sampled town-years. These totals are not independent causes that can be added
together: a young small town can fail both checks. The evidence supports a
combination of repeated fragile/dispersed protocentres and a plausible random
tail, rather than a universally missing crop or an uncalled hazard formula.

## Reproduction and remaining limits

The diagnostic is `HistoryOriginOpportunityAudit` in the test project:

```
--origin-opportunity-audit <saved-world> <output-folder> <frozen-core-dll>
```

It rejects a mismatched Core, preserves the input world's history hash, and
writes `origin-opportunity-history.json` plus three actual `.world` saves.
This run is under `artifacts/history32/arc42-n-origin-history`; M/N review
outputs are under their corresponding `round-m-*` and `round-n-*` directories.
These generated artifacts are deliberately not source-controlled.

The six pure cap contrasts and 17 actual K-Archipelago cap assertions retain
food-cap binding and zero-local-rural behavior. The opportunity replay adds
causal evidence, not a passing golden that should be regenerated on every
change. Three seeds are inadequate for origin timing distributions. A broader
cohort, including censored no-origin worlds, is required before more tuning;
no additional generic hazard uplift is justified by this single long tail.

## Broader N cohort

A frozen N2 runner tested eight Earth and eight Continents Plus seeds
(`101,202,303,404,505,606,707,808`) plus four Archipelago seeds
(`101,202,303,404`). Each uses 20,000 detail cells and explicit native-3
content. The ordinary history calendar stops at the first sampled point at
least 2,000 years after the first state, or at 450,000 years without one.
This is an origin cohort, not a full-history geopolitical or performance test.

| Preset | States reached / worlds | First-state minimum | Median among successes | Maximum among successes |
| --- | ---: | ---: | ---: | ---: |
| Earth | 8 / 8 | 138487 | 187726 | 223942 |
| Continents Plus | 8 / 8 | 121457 | 183999.5 | 290709 |
| Archipelago | 3 / 4 | 141952 | 177662 | 239433 |

Archipelago 404 is retained as a right-censored no-origin result at 450,000,
not excluded from the cohort or assigned an invented founding date. The
successful worlds have 20 independent-origin events in total: one per world
except Archipelago 202, with a second event 1,341 years after its first. This
does not establish how many independent origins exist over a complete history.

All observed origins are Aw, BSh/BSk or C climates; none is Af or a frozen
continental/tundra site. Aw origins remain possible in seasonal savannas,
including low latitudes. This is not a hard 30-degree whitelist, and a good
habitat label alone is not a guarantee of sufficient native domesticates.
Observed site establishment factors range approximately .32-.88 and actual
staple factors .267-.904. Most sites are thousands of kilometers from the
human origin. Continents Plus 808's origin is only 738 km away, with homeland
factor 1: no qualifying distant human residence has activated the anti-origin
prior. The documented isolated-world exception remains real, rather than
quietly forbidding civilization forever on that island.

The reports, recipes, validated worlds and maps are retained under the ignored
diagnostic folder `D:\WorldGenBuildArtifacts\history32-n-origin-controls`.
This is generated test output, not a second source or user-world backup.
Twenty worlds are insufficient to assert a 99-percent probability, estimate
extreme tails accurately, or validate a historical Earth crop distribution.
Earth terrain is measured, but its generated climate, fauna and domesticates
remain procedural; the cohort must not be presented as a literal Earth-origin
reconstruction.

The later matched-world replay harness resets only the loaded copy's history,
using ordinary prepare/start and retaining the exact accepted abiotic/ecology
arrays. Replaying Archipelago 42 with the frozen N2 assemblies reproduces the
original final year 324700 and its exact one-year continuation hash
`a09020d6a64d2e8da6e9905f463738c34ef7edc4274a31c947aa199e91209d13`.
This verifies the diagnostic refactor, not a new model improvement.
