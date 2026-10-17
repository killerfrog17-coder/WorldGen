# Proportionate acquisition and funded integration (realm25)

Status: introduced under the explicit realm25 upgrade; longitudinal acceptance
remains separate from the focused checks. No new saved fields, archive walks, member-cell
censuses, state-expiry lottery, calendar gate or culture-unity exemption.

## Actual old-rule failure

The active revised-military capture loop charges90 conquest-debt units for
every town, regardless of its residents or the recipient's administered scale.
Terrain instead costs8 times inhabitable area/25000. The aggregate state actor
removes only1/year, or2/year after writing; any realm crisis or fiscal deficit
pauses that baseline. Actual integration, bureaucracy, funded works and modern
transport do not accelerate this debt retirement. Debt contributes to strain,
legitimacy and local autonomy pressure even after centuries of local inclusion.
Ordinary-secession changes in23 do not remove this separate feedback.

The saved W22 Earth review at164400 contains1251 living courts;362 with debt
at least600 hold38.88% of exclusively owned population. Court693 has934 debt,
10.44million residents,14 towns, full public works,169570 receipts versus16343
service cost, and no exhaustion. Its own existing town inclusion values are
685–817/1000. Its eleven observed town wins in the inclusive last100 years
include ten previously captured towns; this is not proof of legitimate unity.
Most losing courts are old nested descendants, not recent direct secessions.

`scripts/history-conquest-burden-review.mjs` consumes the existing JSON audit
only. Applying the fastest legal old recovery2/year throughout observed town
wins, omitting all recovery pauses and terrain/client additions, gives972 debt
before the five captures at164300 and930 at164400, versus the saved934. Omitting
those eleven wins gives772. This is explicitly optimistic arithmetic, not a
reconstruction of historical budgets or backdated modern technology. Current
population/capacity must not be used as if known at those earlier captures.

## Guarded mechanism

All preceding revisions retain the literal capture and state-decay branches.
For25 the pre-capture quote repeats exactly the remote cohort fraction and town
urban/suburban/rural residents that `ClaimAggregatePolity` actually transfers.
An already recipient-owned cohort or town is not counted twice. No new people
or military-service cohort are created or removed.

Acquisition load is `500 * acquired/(exclusiveServedBefore+acquired) * route`,
capped1000. The recipient denominator is its own cached urban, suburban and
rural residents, plus remote residents weighted by the same existing served
fraction used for receipts. Subjects and recognized claims are not included.
Route effort above actual local service reach increases the multiplier from1
to at most3, including the supply-center detour. A recent direct return only
(`loser.ParentId == recipient`, age<=50) multiplies by.75. Actual local inclusion
can multiply by.75–1 only when that local ruler already resolves to this
recipient; the previous foreign ruler's inclusion is not transferable credit.
Same culture, distant ancestry and a former capture event grant no discount.

Existing free1/2-year baseline decay and its crisis/deficit pause remain. Extra
repair uses the existing deterministic sampled administrative city, not a new
membership scan. That inhabited, nonabandoned center must really be served,
have an allowed route and recognize this recipient. Capacity is the product
of actual realm integration, `.5+.5*bureaucracy`, funded public works, its
recipient-specific inclusion, physical delivery, `1-.75*crisis`, and
`1-exhaustion/1000`. Delivery is `1/(1+max(0,effort/reach-1)^2)`.

Potential extra reduction is `floor(remainingDebt*(1-exp(-.04*capacity*years)))`.
It costs `max(1,annualReceipts/1000)` treasury units per debt unit, limited by
real treasury and5% of the current positive fiscal margin times elapsed years.
Payment is charged once and included in the service-cost ledger. There is no
free unfunded repair, future borrowing or technology-label immunity. A crisis
reduces extra repair but does not prohibit paid institutional repair altogether;
total exhaustion does. Foreign exclusion, unrest and other crisis mechanisms
remain. Debt stock inheritance on political splits is deliberately unchanged.

All coefficients are transparent authored simulation priors, not empirical
constants inferred from the sources below. A small ancient court taking a
large or inaccessible acquisition can still incur severe burden. A large,
funded and physically delivered state need not treat a small town as an entire
country or take450 years to retire that same fixed90-unit charge.

## Historical basis and limits

Besley and Persson model fiscal and legal capacity as investments whose
effectiveness depends on political institutions and past development, rather
than a free benefit from possessing a modern technology label. This motivates
conditioning repair on deployed and financed capacity, not their exact
coefficients. [Published research](https://benny.aeaweb.org/articles?id=10.1257/aer.99.4.1218).

Brautigam's study introduction connects state-building to fiscal and
administrative institutions and negotiated state–society relations, while
warning that taxation is not uniformly beneficial. This supports retaining
recipient inclusion and real spending rather than treating mere revenue as
automatic legitimacy. [Author's published chapter](https://assets.cambridge.org/97805217/16192/excerpt/9780521716192_excerpt.pdf).

## Validation scope

`HistoryConquestIntegrationTests.Run` passes64 assertions covering proportional burden, demanding
ancient/remote cases, narrow return/inclusion discounts, service/capacity
requirements, paid repair during reduced-capacity crises, real budget limits,
integer extremes and the revision guard. `World` uses controlled transfers of
actual saved-world towns to compare the quoted acquisition with the actual
exclusive population increase and unchanged global/source totals. These probes
are not a claim of a valid future-rule cold save. On genuine Y24 Earth at159300,
hash `8079bc1492469f21d53007da3cf715009a400c667386c257936dd3dc05b9b232`,
all16 actual-world assertions pass, including three exact quote-to-transfer
comparisons and the private caller's ruler/return checks. The coherent26
assembly running these checks builds with zero warnings/errors. Genuine cold
continuation, literal old24 continuation and longitudinal map review are
separate required checks; controlled probes do not substitute for them.
