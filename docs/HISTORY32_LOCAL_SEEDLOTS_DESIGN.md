# Local seedlots: proposed bounded revision14 scope

17 October 2026. Approved design; revision14 implementation and controlled
fixtures are in progress, hidden behind its separate rule guard. The central
marker is still owned by the coordinating agent; this is not longitudinal
acceptance. Revision10 selection is a separate completed repair, documented
in `HISTORY32_LOCAL_CROP_SELECTION.md`.

## Actual causal gap

G Earth culture51 Lifufi has its single practice field in China while its
actual Iberian capital farms near39.34N3.93W. Revision10 can continue selection
at one actual practice field; it cannot express both communities' independent
landraces. A locally adapted Iberian lineage also cannot be carried north if
planning still evaluates the distant culture-global profile. Merely adding
local records without changing the source of destination-fit calculations
would leave that second defect intact.

The smallest useful new unit is one actual town and its already-accounted farm
catchment, not one culture and not one terrain cell. Same-culture towns may
therefore retain different inherited traits and select them independently.
This does not claim to model every rural household, field or variety.

## Proposed authoritative data

Add one nullable, explicitly versioned `Connections.LocalSeeds` extension,
enabled only by the agreed new realm revision. Each recorded town has at most
six nullable group slots. A slot contains a snapshot of the existing cultivar
traits, acquired year, immediate source town or source cell, local practice
years, and last practice year. The containing record stores town ID and its
actual field representative (initially the town cell). Existing crop origin
and parent-culture provenance remain intact; immediate seed transport is a
different fact. Queries never walk ancestry.

Use a typed immutable sparse record tree, with immutable six-slot records at
the leaves. Seven32-way levels cover every nonnegative signed-int town ID;
do not pack `town*6+group` or `town*fieldCount` into the existing25-bit cell
tree. Lifetime town IDs need not be bounded by simultaneous terrain cells.
`With(town,record)` copies one bounded path; `Copy()` shares its immutable
root; read-only `TryGet` does not clone or materialize records. A cached
fingerprint at each node makes publication hashing proportional to changed
paths rather than all dead towns. This new tree needs explicit serializer and
canonical support; it must not silently fall back to generic reflection over
its complete node history on every publication.

At most six cultivar snapshots and one record are replaced on a town's review.
There is no growing list of every harvest, replacement or cross. Old abandoned
records remain finite historical evidence, but never receive practice. A town
refounded with a new ID does not inherit a ruin's seeds by geographic lookup.

## Access, production and actual transport

Two distinct interfaces are necessary:

- `SeedForProduction(state,culture,cell,group)` resolves the exact live
  `TownAt` record first. Otherwise it can inspect the already-recorded live
  `Market` and actual territorial `Sources[cell]`, following at most the
  existing eight administrative parents. It does not call the source-search
  routine, sample a country's cities, select the best distant genotype, or
  treat ownership as seed delivery. Foreign-culture sources need an actual
  delivered seed receipt, not merely a shared sovereign. A source outside its
  valid physical local service connection is not an agricultural neighbor.
- `SeedForJourney(state,sourceTown/sourceCell,group)` is captured before
  destination planning. Fit, capacity, provisions and the committed arrival
  record use that same snapshot. Land/sea migration, provisioned farm
  descendants and remote-farmer movement copy the actual departing inventory,
  never the remote culture origin and never the destination's hypothetical
  best crop. There is no global mutable "current source" context.

The exact-town choice is authoritative, not the maximum of local, market and
culture-global profiles. The latter remains a compatibility/default record
for genuinely unattached communities; it cannot overwrite an established
town's lot when the culture event runs. All six-group production consumers
must use the same selected lot, including `AggregateCropSupport`,
`CommunityCropLandscape`, and `SurveyAggregateCityCrops`. Local husbandry
also replaces the culture-global husbandry input in local wet-forest quality;
otherwise one Chinese farm's experience would still improve Iberian fields.

Local records alone must not introduce a new native species. Within-group
replacement is limited to an actually delivered seed lot or an already-known
local native group, evaluated with unchanged soil, water, thermal,
reproductive and physical-contact constraints. Initial implementation should
retain one effective lot per group, not invent seed mixing/genetic dominance.
Replacement requires a meaningful local advantage; it cannot happen on a
read-only fit query. Stored seed is not ready farmland or free harvest.

## Existing calendars and causal practice

Use the existing city event and its twenty-year harvest-review guard. Review
only the town's six lots, using actual local rural farm households, farming
effort, current fit and elapsed prospective practice. The physiology remains
the shared revision10 helper, including fractional rates and finite bounds.
Local selection cannot change another town or the culture fallback profile.
Imported food in a provisioned port does not imply local cultivation: ports
with no rural fields may hold carried seed but gain zero local selection.

The upgrade should snapshot existing living towns' current profiles once, with
last practice equal to the upgrade year. That explicit actor-only migration
is permitted; it does not reconstruct breeding in the past or scan terrain.
Fresh town creation writes its committed carried profile immediately, before
queries can fall back to culture-global seed. No new recurring actor queue is
required. For a newly created rural daughter culture, the actual migration
must pass the source snapshot through `InheritDevelopmentCommunity` rather
than letting it recopy global donor seed. A rural community can retain its
existing culture profile until it develops a named local town; this initial
scope deliberately does not pretend to distinguish every remote hamlet.

Seed receipt needs real delivery, not generic knowledge contact. Hook actual
successful endpoint exchange or paid supply, with actual source and recipient
towns and a bounded one-group trial. Teacher-only regional memories and
unfulfilled shipping links cannot deliver seed. Existing migration and
shipping expenditures remain paid once; selection creates no food, people,
treasury or independent material stock. A separate seed-mass economy would
be a larger future contract, not hidden in a provenance record.

## Hook count and expected work

The minimal implementation has eight integration families, not a new
worldwide agriculture pass:

| Integration | Existing local work reused |
| --- | --- |
| Fresh/explicit upgrade/validation/copy | One optional root, setup-only actor records |
| Shared production lookup | Three six-group consumers named above |
| City harvest review | One review guard, six slots maximum |
| Committed town founding | One `FoundLivingTownPrepared` hook |
| Land/sea planning | Explicit carrier parameter through existing planners |
| Actual rural daughter inheritance | One helper plus its endpoint-aware callers |
| Successful seed delivery | Actual local exchange/supply endpoint hooks |
| Public inspection | Local versus fallback source and dated practice |

At2000 live towns and a twenty-year review cadence, the upper bound is about
600 lot evaluations per year, before no-field/no-crop negatives. Each shared
production lookup can resolve its holder once outside the six-group loop.
Published-state copying adds one root reference. An updated town copies one
record and at most seven radix paths, not all town arrays. This is a work
bound, not a measured performance claim; the heavily contended overnight runs
are unsuitable for isolated timing acceptance.

Existing transfer-fit cache keys already include every relevant physiological
trait and target cell, so independent identical lots can reuse fit results.
No additional allocation-heavy per-query cultivar construction is needed.
Any holder cache must also include the immutable seed root and current
market/source/service identities, or simply remain uncached initially.
Weather, current service validity and seed delivery cannot be frozen into the
immutable climate cache.

## Save, compatibility and acceptance contracts

Absent optional extension must be skipped by canonical hashing, as with the
other reviewed additions. Old saves through the preceding revision retain
literal old branches, null extension and exact continuation. Fresh/explicit
upgrade initializes the extension and its own version. Serializer round trips
must preserve immutable tree structure semantically; caches are unsaved.
Canonical bytes must depend on ordered semantic records, not allocation or
sharing topology. Specialized fingerprinting must be verified against a
literal reference, including sparse keys and deliberate trait mutation.

Required causal tests include two same-culture towns in distinct climates
diverging independently; source-town seed carried to a third actual town;
planning and commitment agreeing on that source genotype; a disconnected
same-culture town and a teacher-only contact not supplying it; an unfarmed
port, empty frontier and abandoned town gaining no selection; local changes
leaving unrelated fallback/parent profiles untouched; no people/food/currency
creation; cache-on/off, split/cold/save equivalence; publication mutation
isolation; exact frozen preceding-rule continuation. Actual Lifufi's occupied
Iberian field should be the first real-world control, not a promise that its
unoccupied French target becomes viable.

## Evidence constraining the proposal

[Dyer and López-Feldman2013](https://journals.plos.org/plosone/article?id=10.1371/journal.pone.0068320)
analyze859 actual maize seedlots held by606 Mexican households. Saving,
replacement, introduction and diffusion differ across regions; the authors
warn against inferring uniform management rates from altitude alone. This
supports saved local provenance and real transport, not a deterministic
biome-to-variety rule or a universal replacement timer.

[Mercer, Martínez-Vásquez and Perales2008](https://onlinelibrary.wiley.com/doi/10.1111/j.1752-4571.2008.00038.x)
test21 maize populations in common gardens. Their local-adaptation result is
asymmetric: some populations tolerate transfer better than others. A local
lot therefore need not beat every imported lot, and latitude is not a
universal measure of agricultural progress.

[FAO's seed-system account](https://www.fao.org/agriculture/crops/thematic-sitemap/theme/compendium/tools-guidelines/what-are-seed-systems/en/)
distinguishes locally integrated saving and exchange from formal distribution.
Neither is equivalent to technical knowledge appearing in every subject
community. Town catchments and explicit delivery are a bounded simulation
approximation to those systems, not a census or a calibrated genetic model.

## Implementation evidence and further revision14-only review

The isolated seedlot build compiles with no warnings. Eleven pure assertions
cover sparse lifetime IDs through `int.MaxValue`, immutable donor/public
snapshots, independent town updates, insertion-order-independent canonical
fingerprints, explicit fingerprint recomputation, serialization, branch/root
isolation, and the literal preceding null-extension colonial hash.

The F CP medieval fixture passes thirteen controlled mechanism assertions;
the later G Earth suite passes sixteen including disconnected-market and
teacher-only-memory negatives. Whole-world cold continuation is deliberately
pending central revision14 enablement, rather than counted as passed.

The real G Lifufi capital is town2395 at field163381. With its own actual
occupied rural fields held fixed, fifty twenty-year practice steps change
wheat selection4000 to4336, daylength latitude316 to322 and local landscape
yield.101492 to.107522; the distant culture51 Chinese practice profile stays
unchanged. This is a controlled1000-year mechanism probe, not a historical
replay. It is still below the actual.12 migration gate and does not establish
that France can be settled. These numbers precede the separately approved
revision14 rate/fresh-seed-floor correction below.

Review identified a fresh-seed regression in revision10's reused dense curve:
preserving inherited strong traits protects old1450-yield seed, but a fresh
index2000 seed receives yield812 rather than the preceding aggregate1050,
and weaker maturity/warmth selection. Revision14 will therefore take the
preceding aggregate yield/maturity/warmth curves as a floor alongside bounded
physiology. Both new local lots and the global fallback selection need this
floor. Revisions10 through13 remain exact, including their existing curve.

The inherited.02 tenths of latitude per effective practice year is an authored
legacy rate, not a measured genetic velocity. At fit.336, a degree requires
about1488 calendar years before improving-fit feedback. Revision14's approved
rate is.1 tenths/effective practice year, about298 years/degree under that
fixed-fit illustration, with unchanged no-fields/zero-fit barriers and a
hard clamp to the actual field target. This is a conservative simulation
prior, not a universal crop evolution constant; it does not remove drought,
cold-season maturation, native-plant occurrence, route or soil limitations.

[Teixeira et al.2015](https://www.nature.com/articles/hdy201490) followed ten
deliberately selected generations of genetically diverse tropical Tusón maize
in Iowa. Flowering became about twenty days earlier, with performance tested
across18 to43 degrees latitude. [Wisser et al.2019](https://doi.org/10.1534/genetics.119.302780)
examined the genomic response in those generations. This demonstrates the
potential of standing variation under focused selection, not that Neolithic
farmers ran modern experimental trials or that every lot adapts in a decade.

[Pinhasi, Fort and Ammerman2005](https://journals.plos.org/plosbiology/article?id=10.1371/journal.pbio.0030410)
analyze735 dated sites and estimate a European farming-front average of.6 to
1.3 km/year. That includes human movement and contact and does not directly
measure a plant-only adaptation coefficient. It constrains implausibly slow
compulsory genetic timers without justifying an instantaneous latitude waiver.
