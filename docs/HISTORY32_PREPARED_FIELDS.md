# History32 paid prepared fields (revisions16/17 review)

Status: guarded implementation and causal tests in progress; not a released
player or an accepted longitudinal result. Revision15 and earlier retain their
preceding behavior and canonical bytes when the optional extension is null.

Important long-run finding: the P16 fresh Earth replay reached175900 but failed
the strict final prepared-field conservation validator. Its short cold tests
below are genuine, but do not constitute longitudinal acceptance. The original
Q17/ledger-Version1 Earth replay separately completed179800 with saved hash
`30b1bca54ca8f374b1ff0b044fcec53d6ab5d572f9fe957ec7a4102fff4f5210`.
That world is retained as an explicit preceding-rule control, not silently
converted during ordinary continuation.

## Causal finding

The exact G French arrival cell176034 is ordinary boreal/temperate woodland, not
the neighboring peat-classified departure cell175424. Its fully latitude-matched
carried wheat has normalized landscape yield.090489; removing only the existing
forest area(.70) and crop-transfer(.85) factors gives.151777. This is a controlled
physical decomposition, not proof that the actual replay delivered suitable
seed. Critic's subsequent M14 audit finds actual carried profiles still too
poorly matched: the same target's current wheat is.021373, or.035849 after cover
removal. Clearing alone therefore does not fix that frontier.

See [the exact field audit](HISTORY32_CROP_FIELD_AUDIT.md). There is no active
water-table, flood-duration or peat-label penalty to waive. Fertility, seasonal
warmth, seed maturation, moisture, photoperiod and reproductive fit remain.

## Small explicit mechanism

`HistoricalPreparedPlots` is an optional saved extension using existing
immutable sparse `HistoricalCellTree` roots. Six integers per physically used
cell store prepared capacity, establishment/review/vacancy dates, local town
holder, and rural-account epoch. Two sparse regional roots hold additive rural
plot capacity and empty-account reset tokens (Version1 dates, Version2 strict
generations). Six times the maximum terrain
count is15,360,000, below the existing33,554,432-key tree limit.

Capacity units are **prepared farming-person equivalents**, not hectares or
square kilometres. The present town `AreaKm2` is a large mixed farm/forager
catchment, not measured cleared cropland. Claiming that entire catchment was
cleared would invent a physical quantity the model does not have.

Preparation requires an actual farming cohort, its physical carried seed, and
additional food. The authored scenario cost prior is one half farmer-year of
additional rations per newly prepared farming-person equivalent. A funded court
also pays one half treasury unit per equivalent. Existing first-growing-season
food, journey provisions and settlement investment remain separate and are not
refunded or counted twice. These coefficients are explicit simulation priors,
not a measured universal historical labor rate.

An affordable quote is used for planning and retained through the actual
household transfer. Commitment debits exactly the quoted existing warehouse
and treasury, then records the bounded prepared capacity. A bare query,
teacher memory, seed receipt or import-dependent harbor cannot materialize
fields. No people, town, state, sovereignty or actual food harvest are granted
by recording a plot.

Only ordinary woodland can remove its existing forest factors. Ever-wet
conditions and tropical rainforest are excluded. Soil fertility and leaching,
rainfall/alluvial area fractions, slope, salinity, warmth, crop availability,
physical seed fit and native-origin biases remain unchanged. On alluvial land
the already-exempt alluvial area is not rewarded a second time.

Town production weights this benefit by prepared capacity / actual rural
farmers. Its extra rural carrying capacity is limited by prepared capacity,
not multiplied by the whole town catchment. A500-person prepared cohort in a
30,000-person countryside cannot make all30,000 residents' land cleared.

The initial end-to-end route scope is ordinary inland farm-town founding,
paid coastal farming, and the existing real Remote farmer frontier of at most
500 migrants. Generic unpaid voyage planning retains the preceding physical
limits. An existing paid rural plot transfers out of the regional allowance
when its actual households become a town, avoiding double counting.

Existing town city-events can gradually prepare additional actual occupied
fields, funded from real warehouse surplus. Ten-year review guards prevent
same-year double billing, and one review never carries centuries of deferred
work. Explicit upgrade recognizes only already occupied, cultivated actual
rural households, not empty catchment capacity. It changes no cultivar,
population, cash or food stock.

Town abandonment records vacancy. Unmaintained clearing decays exponentially
with a40-year scenario time constant and no usable credit after160 years.
Conquest alone does not erase working fields. Empty rural accounts reset their
saved epoch, invalidating ancient unoccupied rural credit without searching
every plot. Rural occupancy remains a regional/cohort approximation rather
than a new per-household simulation; its total additive allowance is bounded
by the actual paid cohort and current regional population.

Revision17 additionally withdraws unused maintained-capacity credit after a
partial rural collapse, on the same existing town review. It retains only the
actual remaining farming-person equivalents, rather than letting a tiny cohort
maintain a huge abandoned estate indefinitely. This conservative withdrawal
creates or consumes no food, money or people and adds no new actor or timer.
Frozen16 retains its preceding partial-collapse behavior exactly.

## Saved ledger Version2 repair (explicit revision17 upgrade)

Actor-level reproduction found a concrete accounting inconsistency. If a town
vacated a different rural holding at its physical cell, a500-person recorded
contribution could decay to303 usable capacity. Replacing it with353 removed
only303 from the regional counter, leaving recorded550 against actual raw353.
This exact197 residual is reproduced without a world load. A second fixture
proves that two empty/refill cycles in one year can reuse a year-valued epoch,
leaving a stale50-person row valid against an empty counter. These mechanisms
are proven independently. The subsequent actual replay identifies residual
credit, not integer overflow or epoch reuse, as the observed P16 failure.

The forced16/Version1 audit reproduces all27 original P16 exported demographic
and technology milestones through175400 without differences, then first
observes failure at175700 in region441. Population is24, recorded rural credit
184, but the exact current raw rural sum is0. Its only allocated plot row is
cell61191: raw2005, currently usable366, holder/current town3733 (Nuxixi),
prepared-record year175559, review175630, vacancy175632, plot epoch0. The town is alive
and is not an import-dependent port. Regional epoch remains the original
missing-value -1, ruling out an account-reset/epoch collision in this actual case;
the184 credit is also nowhere near integer saturation. A now town-held row with
leftover rural credit is consistent with the independently reproduced
effective-versus-raw debit defect. The precise predecessor row was not retained,
so its original capacity and debit are not invented from the final residual.

Evidence:
`prepared-forced16-firstdrift/year-175700/prepared-failure.json`, saved diagnostic
hash `1e5e2f83a2ed6bdc2ac977a378348ad3de134c412bc2d64f42293fdcdb5a8e31`,
and `prepared-forced16-firstdrift.log`, under the external overnight directory.
The last explicitly passed observation is175400;175700 is the first observed
failure, not a claim that the exact bad mutation occurred in that year.

Version2 distinguishes raw ledger contributions from currently usable field
capacity, debits the complete former contribution on replacement, and permits
only the actual holder town to mark a holding vacant. Empty-account epochs are
strict monotonic generation tokens, not calendar dates. No validator equality
is relaxed.

Its existing `RegionalCapacity` tree stores two nonnegative31-bit words per
region, giving exact long totals without the tree's−1 missing-value collision.
The old scalar could saturate at2,147,483,647 and then under-debit remaining
plots. Two fully paid maximum-int plots are retained as4,294,967,294; no work,
capacity, food or people are clipped to fit the old counter. The maximum sum
over2,560,000 cell records is comfortably inside this packed long range.

Only explicit setup/upgrade visits allocated plot rows once, preserves their
raw paid capacities and physical chronology, rebuilds the exact long sum and
rebases active/inactive epoch tokens. Ordinary turns never census plot rows.
Version1 snapshots retain their preceding counter, vacancy and epoch semantics
until explicit upgrade; this includes the original17/Version1 partial-rural-
collapse rule. Existing16 literal continuation must remain unchanged.

Ten focused ledger assertions pass: the exact197 residual, holder isolation,
replacement of inherited valid-but-decaying16 rows, same-year epoch reuse,
integer saturation/removal, and explicit migration of an already valid
saturated16 snapshot without deleting either paid plot. The actual saved
Q17/Version1 Earth world also retains exact preceding continuation: original
`candidate-q17-freeze` and `prepared-ledger-r17-tests-bin` each advance its
year179800 saved hash above by12 years to
`c8361a45c11b6001b15eb393f0d26c882059c0037989acc361ee8d69af5c7a0d`.
Evidence is `prepared-ledger-frozen-q17-twelve.hash` and
`prepared-ledger-current-q17-twelve.hash` in the external overnight directory.
The same actual Q17 world passes22 explicit-V2-upgrade field assertions from
`prepared-ledger-r17-tests-bin`: upgraded full-save initial hash
`e6e464766091f74a5c84accc0941e63442618b704f619b241010f1f7705d5955`;
actual12-year and genuine cold5+7 continuations both produce
`41b3a16335098e0d9c7c494a432ebcb3ec8ea35b1ee3479a776db24dfd38f9de`.
The actual occupied quote is town232/cell152305,186 farmers; original source,
global cultivars, adoption and practice remain unchanged. Evidence log:
`prepared-ledger-v2-q17-upgrade-cold.log`. Macro V2 replay remains a separate
acceptance check; the short cold proof is not its substitute.

An additional23-assertion actual upgrade-only run from
`prepared-ledger-r17-explicit-tests-bin` explicitly verifies that the public
upgrade API selects ledger2 before diagnostic setup, preserves every preceding
plot's raw paid capacity, holder and physical dates, and leaves every actual
warehouse reserve, court treasury and world population unchanged. Evidence:
`prepared-ledger-v2-q17-explicit-assertions.log`; no second compression was needed
for these additional state-migration checks.

The corrected fresh R17/ledger2 Earth replay completes its strict final
validation and full save at179800, over1,000 years after maximum technology
178754. Final hash:
`19964b5effc07859fdee622d09c99c3ce75f817f4989f109f84a85e2e4804970`;
`candidate-r17-earth200/final.world`,155,274,330 bytes,421.1 seconds wall time.
Every explicit export-checkpoint prepared-field audit passes. First state171106
and Early Modern178000 match the preceding Q17 trajectory; the ledger repair
does not masquerade as a cure for deficient western Eurasian settlement.
This accepts the demonstrated Earth ledger correctness, not geographical
realism or isolated performance. The corresponding CP longitudinal replay also
finishes its bounded tail and full save: `candidate-r17-cp200/final.world`,
246,959,083 bytes, final172800,837.6 seconds wall time. Its final review has
4,707 live towns,5,356,430,819 inhabitants and maximum-technology year−1:
this is explicitly a completed bounded-tail run, not a claim of reaching the
maximum package plus1,000 years. The strict final ledger validation passes.

For offline first-failure diagnosis, use `--world-realism-replay SOURCE NEWDIR
200000 --prepared-audit --prepared-rules 16`. This checks the private validator
at ordinary export checkpoints, writes a detailed `prepared-failure.json` on
the first observed mismatch, and forces the actual empty Version1 setup after
fresh Start. This diagnostic is not a simulation turn or a performance test.

## Historical grounding and limits

[Halstead's original north-Greek oral-history study](https://eprints.whiterose.ac.uk/id/eprint/114726/)
compares manual clearance and long-term cultivation with experimental
slash-and-burn results. It supports treating cleared plots and pioneer food
requirements as real, local investments. It does not support a universal
sequence from woodland to permanent open countryside, nor a single labor
coefficient for every soil or farming system. The paper specifically highlights
the potentially difficult interval between clearance and reliable harvests.

[FAO AquaCrop's official manual](https://www.fao.org/fileadmin/user_upload/faowater/docs/AquaCropV40Chapter3.pdf)
models root-zone water/oxygen stress and groundwater limitations. Clearing is
not drainage, and neither a small surface flood fraction nor a changed biome
label is evidence that roots are adequately aerated. This revision introduces
no drainage project, altered hydrology, fertility manufacture or climate waiver.

## Validation ledger

- Fourteen pure assertions passed: exact forest-factor decomposition,
  alluvial non-double-counting, ever-wet exclusion, unpaid/zero-fit negatives,
  cohort mixing and capacity bounds, finite quoted costs, vacancy/regrowth,
  persistent-root copying, serialization and literal old-null canonical hash.
- Thirty-two actual G field assertions passed from the coherent frozen
  `candidate-p16-freeze`, including full world save/read and actual12-year
  continuation identical to cold5+7. These cover prospective occupied-field
  bounds, actual local seed quote, exact food/treasury debit without people,
  unfunded negative, ancient vacancy, bounded rural allowance/epoch reset,
  old15 guard, occupied-plot non-double-booking, same-year billing and unchanged
  source. The actual unendowed funded forest frontier at route1184,
  cells46054→46287, owner4703, moves500 real Remote households in year176101,
  consumes the original warehouse/treasury and saves500 prepared capacity,
  without a town, state or sovereignty grant.
- The actual unendowed inland caller also succeeds: parent1132, target79635,
  new town3556,2,000 real rural farmers and2,000 prepared capacity. The original
  source pays1,000 extra preparation rations and1,000 extra treasury units,
  plus the existing separately accounted household startup/carriage charges,
  exactly once. Total persons are conserved. Actual live import-dependent
  harbors remain without prepared fields; abandoned port records are not
  mistaken for current land use when a physical cell has been reused.
- Nineteen CP16 assertions pass from `prepared-fields-fresh16-bin`, including
  actual fresh Prepare/Start/three-year partition parity, prospective explicit
  upgrade, full save/read and12 versus cold5+7 continuation. Empty regions do
  not fill an epoch ledger when no paid plot ever existed.
- The controlled French physical route12479 sends301 actual households after
  two explicitly artificial fixture conditions: locally matched copied wheat
  and a separately financed warehouse. Virgin.090489 becomes paid.151777.
  Its original real supplier2395 is alive and supported, but has31,151 urban
  residents and only10,864 reserve, below the existing half-city safety reserve
  even before193 journey and151 preparation rations. Country treasury6,571,764
  is ample. The unendowed actual French funding negative is preserved; this is
  **not** an observed successful historical French diffusion episode.
- True frozen15 control: `candidate-n15-freeze` explicitly upgrades the CP13
  medieval fixture to15. Input hash
  `5ba81f75f283491443f55980ef676e7cb63be4365a7c3fa9dab28cd6bb768521`;
  frozen15 and current guarded16 source both continue12 years to
  `009e803ae2dfa670858da4d8001fd46d7514c8cd044aa3975b1b1c5b50f0067a`.
  Evidence under `D:/WorldGenBuildArtifacts/overnight-20261017`:
  `prepared-fields-old15-cp.world`,
  `prepared-fields-frozen15-cp12.hash`,
  `prepared-fields-current15-cp12.hash`.
- True frozen16 control: explicit16 input
  `07bc10807f276fab5df7694d1cdcf0ec09416903704d97a686fcab5c0f72e9c5`;
  frozen `candidate-p16-freeze` and current guarded17 source both continue12
  years to
  `60666bc87b7dbdfc50ab9fde16f6d371a2a7fbc9f83b130ae651eba570e68773`.
  The corresponding evidence filenames are `prepared-fields-old16-cp.world`,
  `prepared-fields-frozen16-cp12.hash` and
  `prepared-fields-current16-cp12.hash` in the same external directory.
  The later ledger2 implementation repeats this exact frozen16 continuation
  from `prepared-ledger-r17-explicit-tests-bin`, with matching output in
  `prepared-ledger-v2-current16-cp12.hash`.
- Twenty-two original CP17/ledger-Version1 assertions pass from
  `prepared-fields-q17-tests-bin`,
  including explicit upgrade with every global cultivar/adoption/practice
  record unchanged; real fresh-history setup; partial-collapse withdrawal
  without material/demographic effects; literal preceding16 review-tree
  updates; full world save/read; and actual12 versus cold5+7. Saved initial
  hash `db30a2969a7e521336bf9388e47db380ac28484e23b40d5a7362e157251303d0`;
  both continuations end at
  `e7d5766b62e82fec5010007b5a9b59720f9c8b20452b20aedfc038e177823ac7`.
- Full longitudinal review remains separate. No successful European coverage
  claim follows from these actor, conservation or cold-continuation tests.

Reproduce using the focused runner flags `--prepared-fields-only` and
`--prepared-fields-world WORLD [--skip-cold]`. Build/test artifacts belong under
`artifacts/history32` or the external overnight evidence directory, not Git.
Use `--prepared-fields-17-world WORLD` for a genuine17 save/continuation rather
than the deliberately frozen16 field rule exercised by the preceding flag.
