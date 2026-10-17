# Bounded household associations and religious renewal (revision20)

Status: explicitly activated20 with focused actual-world and genuine cold
continuation checks. Longitudinal new-world outcomes remain under review;
this mechanism does not run under19 or earlier.
All preceding saved religion behavior must continue literally without upgrade.

## Observed problem, not a desired number of schisms

Actual Q17/R17 Earth saves at179800 have326/332 public traditions in towns and
36/35 recorded syntheses, but neither records a schism or ethical renewal.
That count alone is not a failure criterion. R17 has50 towns with dissent>=600
and224 with public-institution support>=600, but only one has both.

Eight controlled diagnostic assertions call the unchanged actual private
`TryReligiousRenewal` actor method without loading terrain or running demographic
processes. `--religion-renewal-audit-only` passes from
`artifacts/history32/religion19-actor-review-bin`; evidence log
`D:/WorldGenBuildArtifacts/overnight-20261017/religion-renewal-legacy-actor-review.log`.

- Sixty years of uninterrupted organized exclusion produce60 dissent, not600;
 120 calm years subsequently erase it.
- Below support600, the early return freezes an existing500 dissent even if
  exclusion is supplied to the actual local actor.
- Losing provision reduces900 support to331 in100 years, recording only40
  grievance years before the organizing gate closes.
- Starting from no institution,600 continuously provisioned exclusion years
  produce support868 but only410 dissent: early organization years are gated out.
- Changing only the retained household identity cannot select another organizing
  actor; the method represents the current public tradition.
- These counterfactuals create no people, food, treasury or religious actors.

The existing dissent index combines grievance with public organizing capacity.
It is not a record of a private minority's resources or organization. Merely
lowering its threshold until the two full worlds contain schisms would not
address this missing actor.

## Historical comparisons and limits

[Nagasaki Prefectural Heritage's account of hidden communities](https://kirishitan.jp/histories_en/his002)
documents inherited village organizations, lay ritual/calendar roles and local
adaptation in the absence of priests. Its
[account of renewed missionary contact](https://kirishitan.jp/histories_en/his004)
describes different choices: rejoining a wider church, maintaining the inherited
local system, or changing affiliation. This supports continuity and differentiated
local decisions, not automatic extinction under prohibition or a compulsory
new religion after enough elapsed years. The account also distinguishes tacit
permission from public repression; a decree is not proof of uniform enforcement.

[Galambos's primary Dunhuang manuscripts](https://localsources-china.efeo.fr/index.php/dunhuang-documents-on-she-galambos/)
record local associations, officers, contributions for rituals and funerals,
withdrawal because of poverty, and renewed women's bylaws. These are concrete
forms of lay cooperation and material obligation, not proof that every lay
association was a separate religion or a branch of one centralized church.

[The Rubin Museum's resident-scholar account of Geluk formation](https://rubinmuseum.org/what-is-the-geluk-tradition/)
relates renewal to instruction, inherited learning, monastic discipline and
practice. It is a counterexample to treating all reform as a passive consequence
of court corruption. Its particular doctrines are not generic stages through
which all simulated religions must pass.

None of these sources calibrates the model's numerical support, expenditure,
commitment or selection rates. Those remain explicit scenario priors. The model
does not rank traditions, ancestry or modern identities.

## Minimal prospective contract

An optional `Religion.LayAssociations` extension stores at most one parallel
record per observed town cell, using eight integers per cell in an immutable
sparse `HistoricalCellTree`. At maximum detail this is20,480,000 keys, below
the existing33,554,432-key bound. The record holds faith, organizing-support
index, review/foundation dates, actual holder town, recorded dispute,
organizational commitment and last real gift. It is not a membership census.

Fresh/explicit20 setup creates an empty ledger. It recognizes no retroactive
association and invoices no arrears. Ordinary work addresses the actual local
city/contact/migration actor; no country, culture, ecoregion or cell census is
introduced. A reused physical cell cannot inherit an abandoned holder's active
organization merely by occupying the same coordinates.

Prospective evidence can seed a potential organization: retained incumbent
households after an actual local public change, actual migrant households, or
matching delivered supported contact. A remembered faith ID alone is not enough.
Potential support starts at zero. Continued organization requires extra real
local gifts from available reserves above the existing provisioning buffer;
receiving a visitor does not copy a working institution or create inhabitants.

Public predominance remains separate. Ordinary continued household rites,
minority resistance to a different court faith, suppression, and resumption of
permitted worship retain the existing faith ID. Enforced restrictions consume
actual government resources and can reduce organizational activity. They do not
automatically increase grievance, mint a child faith, eliminate household memory,
or convert the public layer. Poverty/abandonment can reduce activity as well.

A genuine internal renewal instead needs a recorded dispute within the same
tradition, actual local organizing capacity and dedicated material commitment.
Existing public institutions can supply an experienced organizing core only when
they belong to that same faith; a minority cannot borrow another religion's
institution index. A new lineage, if one actually forms, belongs initially to
the local association. Public predominance can change only through the existing
gradual local reception mechanism, not the act of founding the association.

Private organizational capacity cannot be a free missionary multiplier. A real
presently usable endpoint contact may carry it only from its actual current
holder, with recent real support and source provisions. Actual migration may
carry household continuity, not a ready-made endowed organization. The largest
religion IDs, country defaults and old graph edges are not substitute carriers.

Tests must include ordinary same-identity persistence, unfunded suppression,
funded suppression reducing activity, relief/reconciliation without identity
change, no automatic public conversion, an internally disputed funded renewal,
unsupported/empty/abandoned negatives, physical contact and real charges,
source immutability, serialization/cold partition parity, and literal old19
continuation. Any absence of new-lineage events in a long-world sample remains
an observation, not permission to tune to a quota.

## Implemented numerical priors and focused evidence

One review is due after ten actual years and credits at most twenty years of
work, never centuries of arrears. A real gift costs `ceil(residents*years/8000)`
additional reserve units, with a minimum of one. An internal-dispute assembly
additionally consumes `max(10,residents/100)`. These are bounded organizational
expense proxies, not inferred membership shares or staff counts. Government
enforcement/patronage separately uses the existing actual treasury charge above
immediate service costs; an unaffordable prohibition has no paid enforcement.

Funded support approaches750 under permitted practice or300 under funded
suppression, with a60-year organizing time constant; unsupported activity decays
on45 years. Internal commitment grows only through a genuinely disputed,
actually provisioned assembly, weighted by observed patronage capture/instability
or a recent related-lineage practice dispute. Otherwise it decays on60 years.
Eligibility requires commitment600, organizing capacity600, thirty observed
organization years,250-year lineage/previous-reform continuity and a seeded
local selection hazard `1-exp(-years/200)`, followed by the actual additional
`max(10,residents/50)` founding charge. Same-faith public institutions can provide
an experienced core of at most400 support; another faith's institutions cannot.
These are explicitly authored scenario coefficients, not historical measurements
or a requirement that any world produce a particular number of denominations.

Twenty-three focused tiny-actor assertions pass from `religion20-tests-bin`.
They cover actual gifts and court debits, zero-support evidence seeds,
suppression and unaffordable-prohibition negatives, poverty, identity-preserving
relief, no automatic public conversion, immutable copying, exact serialization,
maximum-detail sparse bounds, stale/abandoned carriers and literal19 inactivity.
The controlled seed123 internal-patronage scenario forms a parent-linked private
renewal after fifteen actually funded ten-year reviews; the identical unprovided
six-hundred-year scenario does not. This is a counterfactual local mechanism
proof, not an observed historical episode or a calibrated event frequency.
Ten actual-world assertions also pass against the saved20k-cell CP control.
This is a controlled minority-affiliation experiment on existing towns, their
real geography and their unaugmented stocks, not an observed historical
denomination. Town3679/cell4527 had362,123 residents and an actually usable
neighbor route to town752/cell12512. Five ten-year reviews debited2,265 real
reserve units and reached support424. Actual delivery then cost90 source
provisions, preserved its424 support payload, and seeded only a zero-support
potential at the receiving town. Both towns retained public faith21; the
carried private faith was0. Invalid departure endpoints and disrupted source
actors remain negative controls. No inhabitants, treasury funds or public map
layers changed. The late baseline Earth20 control had no currently viable
funded neighbor route and was correctly unsuitable for this positive fixture;
its failed precondition was not solved by manufacturing an endowment.

Actual frozen-rule continuation controls remain exact after the optional model
and canonical-null skip: original Q17 Earth plus12 years gives
`c8361a45c11b6001b15eb393f0d26c882059c0037989acc361ee8d69af5c7a0d`;
original S18 Earth plus12 gives
`591ef6aeea30f1ee697a808a6a2d87df8cef2bcfeed41c1ab1fa08edd01783ba`.
Files live under the external `overnight-20261017` evidence directory with
`lay20-current-q17-twelve.hash` and matching frozen reference, and
`lay20-{frozen18,current18}-earth12.hash` respectively. Genuine activated20
Frozen T19 Earth plus12
is also exactly preserved at
`43d7ce2a8dd1a4826123058004a112b8fc7790e3964a1bcb603804da24647458`
by `candidate-t19-freeze` and `religion20-actual2-bin`, recorded as
`lay20-{frozen19,current19}-earth12.hash`. These are canonical historical hashes
and continuation guarantees, not a claim of identical compressed wire bytes
across serializer/model builds.

After central activation, thirteen actual-world assertions pass from
`C:/Users/kille/Desktop/WorldGen/artifacts/history32/religion20-cold-bin`
with `--lay-associations-world
D:/WorldGenBuildArtifacts/overnight-20261017/baseline-cp20/final.world`.
The actual explicit20 upgrade preserves all stocks and inhabitants, starts an
empty prospective ledger and survives a validated full-world save/load.
Twelve actual simulation years exactly equal five years followed by a second
full-world intermediate save/load and seven further years, at hash
`cbdd7bee1898a38fe85aafe6bb6230a5ee24b5fc86e86fa2bef0dd9541ddb39f`.
Fresh `Prepare`/`Start` uses revision20 and its ledger; three years exactly equal
one plus two. These controls are separate from the controlled fifty-year actor
experiment and do not assert that a mature global faith or new schism emerged
in the twelve-year chronological replay.

The read-only religion evidence exporter additionally records current town-held
association identity, support, dispute and gift dates. When the optional ledger
exists it emits a separate opaque `associations.svg`/`.png`, coloring only
currently held support>=100 records. The original public-predominance and
retained-household-memory maps keep their existing meanings. A private renewal
need not appear on either old map until actual gradual public reception; its
local organization is observable on the third layer and in Atlas facts.
