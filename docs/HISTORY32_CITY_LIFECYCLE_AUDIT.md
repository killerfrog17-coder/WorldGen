# History 32 city-lifecycle causal audit

17 October 2026, C control worlds. This is an offline diagnostic and research
record, not a new simulation path or a calibrated historical city-survival law.
Core changes and their explicit rule marker are owned by the subsequent urban
survival experiment. Frozen C behavior must remain reproducible.

## Question and method

The independent map critic found high active-town ID turnover over matched
300-year intervals. `HistoryCityLifecycleAudit` compares the complete saved
active-town lists with final actor life records and dated archive entries. Its
explicit scans are diagnostic only; ordinary simulation does not call it.
Original input checkpoints are read and their hashes checked before and after.

| C control | Interval | Before / after active towns | Lost IDs | Their prior urban residents |
| --- | --- | --- | --- | --- |
| Continents Plus 200k | 185900–186200 | 422 / 515 | 211 | 50,175,574 |
| Earth 200k | 204200–204500 | 461 / 501 | 166 | 32,226,753 |

Every lost ID had an actual abandonment in the interval; none was merely a
renamed still-active actor. Ten Continents Plus and eight Earth lost sites had
an active same-cell refoundation by the later checkpoint. This demonstrates
reoccupation, not proven continuity of households or institutions.

`AbandonLivingTown` conserves and transfers the surviving urban, suburban and
rural households to regional countryside, unregisters the town and zeroes its
food stocks/capacity. However, its preceding demographic update can already
have removed urban residents. The prior population column is therefore neither
a death count nor a count of refugees. Final life records freeze, but terminal
food/capacity and the terminal demographic calculation were not archived.

The initial long-disruption hypothesis explains only a minority. Continents
Plus had 17 lost actors still in their disruption period, 16 with productivity
below .4 and 18 with at least 80 decline years. Earth had 18, 20 and 22
respectively. Only six Continents Plus and two Earth cases used the separate
early-village dispersal path. Most were state-owned at abandonment: 204 and 163.
Losses were not confined to marginal climates: Cfb/Cfa account for 91 of 211
Continents Plus and 69 of 166 Earth cases.

The terminal life fields are compatible with these source branches:

| Terminal branch | Continents Plus | Earth |
| --- | --- | --- |
| Decline 30–79 and below peak-relative threshold, or below25 residents | 166 | 120 |
| At least80 decline years | 18 | 22 |
| Below25 residents before30 decline years | 21 | 22 |
| Early-village dispersal | 6 | 2 |

The first row cannot be further separated from final records alone. It is not
automatically evidence of a viable terminal city.

## Exact frozen-C replays

A diagnostic harness referenced the actual `candidate-c-integration-bin`
assemblies, reset only history through the existing public preparation/start
API, and traced the 20 largest lost towns annually. Both original endpoint
cohorts matched exactly, including every saved ID and population, in BOTH
worlds. It did not alter Core or change the accepted geography. Traces are in
`D:/WorldGenBuildArtifacts/overnight-20261017/city-trace-c-earth200/` and
`city-trace-c2-cp200/`; final-record audits with corrected labels are in
`city-lifecycle-c3-*`. These generated files are outside source control.

Three distinct active causal failures appear:

1. **Capacity shock erases stored food and causes a numerical population crash.**
   Gicfupismu had 2,999,500 urban residents, old food capacity 3,226,732 and
   stored food 9,013,572 at 186014, with productivity 1 and no decline. Secession
   changed its governor and the currently computed food capacity fell to 25,600.
   The old update caps stocks at three times the NEW capacity before evaluating
   supply, despite the existing warehouse stock. Its negative discrete
   logistic term can then exceed the entire urban population as P/K grows.
   The next observation has zero urban population and one decline year.
   Dekhlimyt and Lylkhulesy show similar secession/instant-zero histories.

2. **Local productivity deletes an imported-food city.** Nalekčin still had
   1,069,265 residents, capacity 1,045,112 and reserve 3,135,336 at its last alive
   observation. Its productivity was .216 and decline 79. It was retired at 80,
   although its recent population peak was only twelve years earlier and
   imports continued to feed it. Earth’s Silifmum mi similarly had 1,394,287
   residents, capacity 1,551,109 and reserve 4,652,442 at decline 79. Earth’s
   Hasqinluhom had 882,855 residents, capacity 897,485 and reserve 2,692,455.

3. **Historical peak becomes a permanent extinction threshold.** Yufihočin
   had 282,367 residents, capacity 307,938 and reserve 313,220, productivity 1 and
   decline 29 at its last alive observation. Its older peak was 2.20 million.
   Being below 20% of that peak perpetuates decline, then below 35% at 30 years
   retires the still provisioned reduced town. Other large towns had genuine
   current deficits: Femumi sim had 1.294 million people but 659,000 capacity
   and no reserve before retirement at 30. These require adjustment and
   relocation; the trace does not show that they should remain their old size.

`AdvanceLivingCityCondition` is also incomplete: sanitation reduces hazard,
but after triggering, the .18–.58 productivity loss, 40–179-year duration and
.004/year post-disruption recovery do not respond to repair resources or
infrastructure. The potential local urban-food ceiling can be reduced twice
by productivity, through rural capacity and produce. Repair is a real causal
gap, but repair alone would not fix the observed three failures.

### Deployment and institutional continuity caution

For technical cultures `HistoricalModernEffects.Adoption(c, technology, year)`
reads the CURRENT saved `Technical.Deployment`; its year argument does not
reconstruct earlier deployment. Initial final-record comparisons were caught
and relabeled `ObservedFinal`, not represented as abandonment-date evidence.
The annual frozen replays record actual contemporary practice instead.

Hasqinluhom’s last alive sanitation deployment was .582 locally and .720 for
its actual governor; Gicfupismu’s was .326 at its new successor court. There
are genuine local/governing capability gaps, but neither uniform advanced
practice nor uniform absence of practice can be inferred from the world’s
observer era. Public works, roads, pipes and warehouses also need not vanish
physically when a dynasty or sovereign title changes. Current state imports
depend on its own rural ledger; title changes can sharply reduce access even
without an independently recorded physical destruction of stores or farms.

## Bounded correction proposed to the main implementation

- Preserve pre-existing food through a production/capacity shock. Consumption,
  finite spoilage, actual destruction and real transfers may reduce it; a new
  annual yield estimate alone must not destroy a warehouse’s contents.
- Compute supply from actual rations available/consumed. Separate constrained
  births, bounded scarcity mortality and actual household outmigration from
  the carrying-capacity ratio; do not allow an unbounded negative logistic
  term to kill millions in one update while stocks still exist.
- Evaluate sustained distress from actual provisioning and households. A
  supplied city may depend on imports, and a reduced but supported town may
  persist below its old peak. Keep the peak as historical information, not a
  permanent retirement command. Truly emptied, unsupported settlements can
  still disappear.
- Keep work actor-local on the existing city calendar. Any infrastructure
  persistence, paid repair, governing capability contribution and actual
  migration must use existing local service/routes/accounts, not a recurring
  terrain or country census. New outcomes require an explicit optional rule
  revision; old C continuation must not change.

No universal survival percentage or immortal-city rule is proposed. Numerical
coefficients remain transparent scenario priors, not empirically fitted rates.

### Targeted survival validation and old-rule continuity

The subsequent revision-5 survival implementation passed 18 focused checks,
including 81 finite food-flow combinations, the exact Gicfupismu warehouse
balance (reserve 6,039,672 and supply 1), finite urban/rural losses and the
Nalekčin/Yufihočin retirement controls. Eight actual-world checks passed on
each C Earth200 and C Continents Plus200 fixture, and separately on D
Continents Plus200. They cover explicit upgrade, serialization, ten versus
four-plus-six-year replay and the guarded old/new city event. The source
fixtures remain unchanged.

The late depot-supply helper refinement was independently identified in frozen
IL and moved behind revision 5. Named output files, rather than arrival order
of parallel tool responses, confirm exact 20-year old-rule continuation:

| Source checkpoint | Frozen and corrected-current hash |
| --- | --- |
| C Continents Plus 200k | `aef596395cfbb4ddd9accf722e0a231c265ffee8c3dcff8f63accb0ed724e301` |
| D Continents Plus 200k | `f567c515a07b42e3eb4f3c79bd0bb460c2c0ba04d033ff96c851267b2c799516` |
| D Earth 200k | `14728516be67d39ec72b049d20c19fb16def4d5213cb7798200d7a9376371065` |

All hash files are under `D:/WorldGenBuildArtifacts/overnight-20261017/`.
Frozen C is `urban-survival-frozen-c-cp200.hash`; frozen D files are
`urban-survival-frozen-d-d-{cp200,earth200}.hash`; corrected current files are
`urban-survival-e-fixed3-source-{c-cp200,d-cp200,d-earth200}.hash`. There is no
remaining demonstrated C-to-D or D-to-current compatibility failure in these
checks. An earlier verbal report mixed unlabeled parallel completion order;
the named files above supersede that report.

## Historical mechanism comparisons

[Smith et al.’s original PNAS perspective](https://nick-gauthier.github.io/files/pdf/Smith_etal_Persistence.pdf)
distinguishes persistence of occupation, demographic scale, institutions and
cultural practices, and warns that nominal continuity can mislead. It does not
establish a universal ancient city lifetime. This audit similarly separates
IDs, reoccupation, households and population size.

[Field’s original London relief study](https://www.cambridge.org/core/journals/urban-history/article/abs/charitable-giving-and-its-distribution-to-londoners-after-the-great-fire-16661676/ACEF72F08A2A97509D5D366BEDBA17B6)
finds continuity and recovery through actual charitable networks after 1666.
[Archer’s Oxford historical account](https://www.history.ox.ac.uk/facing-catastrophe-great-fire-london)
describes improvised markets, continuing civic institutions, legal arbitration,
building labor and funded reconstruction. Severe destruction therefore need
not mean the instant end of the settlement actor.

[Davis and Weinstein’s original Japan research](https://www.nber.org/papers/w8517)
finds long-run relative city size robust to major wartime bombing shocks in
that setting. This supports testing infrastructure/network recovery; it does
not imply every city or every economic base must recover.

[Penny et al.’s original Angkor hydraulic study](https://doi.org/10.1126/sciadv.aau4029)
and its [author-university account](https://www.sydney.edu.au/news-opinion/news/2018/10/18/climate-stress-will-make-cities-more-vulnerable--new-angkor-rese.html)
show how climate extremes interacting with infrastructure topology could
produce cascading failure. Long disruption can be appropriate when tied to a
specific vulnerable system, not equally imposed on all prosperous cities.

The [Pompeii archaeological authority’s account](https://pompeiisites.org/en/pompeii-map/analysis/pompeii-after-the-eruption/)
describes true post-eruption abandonment despite relief efforts, alongside
survivor recovery, salvage and later limited reuse. It is a useful contrast:
physical burial and loss of the wider provisioning landscape are not equivalent
to a changed court name or a smaller urban population.
