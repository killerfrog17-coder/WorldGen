# History 32: nuclear capabilities and physical arsenals

This is a versioned historical-simulation extension, not a generation stage or a weapons-engineering model. Numerical budgets, production rates, range, deterrence probabilities and casualty fractions are abstract game priors. They are not calibrated estimates of real weapon costs, yields or effects.

## Technology and compatibility

Nuclear Fission and Nuclear Weapons are separate, appended technology IDs 37 and 38. The 37 older IDs retain their meaning. History 32 uses 39-entry research records; histories through 31 retain their original array lengths, research priorities, catalog-count behavior and null nuclear extension. Earlier 15-capability histories retain their own count. New prerequisites do not alter the legacy useful-level table.

Civilian fission needs advanced installed scientific, electrical and metallurgical practice. Weapons research additionally needs developed fission/science/metallurgy, and independent weapons research receives no allocation without actual war or hostile security demand. Imported design knowledge follows existing dated physical-contact learning budgets. Knowledge does not conjure industry, power stations, material or a state arsenal. The civilian energy query requires both fission and electrical deployment.

Explicit upgrade appends unknown technology entries and zero physical inventories. It does not award past discoveries or retrospective weapons. `HistoryState.Nuclear` is nullable and omitted from old null canonical hashes. New records are deep-copied, validated and appended on both foundation and actual regime replacement.

## Physical state accounts

Each state has its own saved program dates, stockpile, production work, investment, maintenance, operational readiness, aircraft delivery maturity, use/loss totals and custody history. Cultural design knowledge and political possession are distinct.

Production requires deployed fission, weapons, electricity and industrial metallurgy; institutional/public-works capacity; a supported urban specialist workforce; a positive fiscal surplus; actual treasury; hostile demand; and uranium access from the existing bounded, geographically connected resource market. Potential deposits are not automatically mines. Only the program's paid industrial capacity turns market access into production. The resource proxy does not measure known reserves or real mining inventories.

Each update samples at most three already indexed owned cities and their at-most-twelve existing city neighbors. It does not census cells, territorial holdings, cultures, all states or all relations. Eligible programs use the pre-existing bounded material-market query. Inventory is capped at 8,192 represented weapons per state. Same-date updates cannot repeat production.

Maintenance spends actual treasury. Funding shortfalls reduce readiness and eventually dismantle weapons. A conquest/succession transfers physical custody without granting an untrained successor operational crews. Existing trained weapons of the successor are not erased by that transfer. A continuing institution under a new regime transfers its arsenal, deployment/readiness and unfinished paid program once. No weapon is duplicated. Unsupported inventory without a successor is abstractly dismantled rather than becoming an untracked permanent arsenal.

The continuing arsenal also retains its actual last-maintained date. Its next
update settles elapsed upkeep from that date, not the younger government's
shorter state-event interval. Regime replacement cannot forgive unpaid upkeep;
same-date processing cannot charge it twice. This avoids the early-return path
for a state succession silently skipping the predecessor's maintenance interval.

The custody invariant is:

`stockpile = produced + inherited - transferred - dismantled - used`

## War and consequences

Credible, maintained weapons with deployed aircraft and sufficient actual distance range reduce the probability of a conventional attack. Mutual arsenals deter more strongly, but limited conventional wars remain possible. A design string, an unmaintained cache or unreachable weapons do not provide a global defensive aura.

Atomic use is restricted to actual campaigns, severe military disadvantage, high exhaustion and low cohesion. Retaliation risk substantially lowers an already rare escalation probability. The current delivery approximation is aircraft from the political capital, not separately simulated missiles, bombers, air bases or submarine patrols.

A successful strike expends one actual weapon and damages one actual opposing administrative center. Urban, suburban and nearby rural deaths are bounded fractions of the selected city's existing accounts. The ordinary population-delta helper updates cultural, regional, political and world ledgers once. Campaign casualty totals record those deaths without subtracting population again. Food reserves, local stability and productivity suffer; the attacking government loses legitimacy and actual neighboring diplomatic relationships deteriorate. The historical archive records the attack. No territory is automatically annexed.

There is no resolved blast footprint, fallout transport, global climate effect, nuclear-winter model, inferred remote casualty population or extinction shortcut. Those effects must not be inferred from this implementation. Campaign presentation in history 32 says recorded casualties rather than describing all losses as military levies.

`Prehistory.NuclearProfile` is a read-only O(1) query distinguishing design knowledge, funded programs, maintained arsenals, limited-readiness custody and former arsenals. Counts, dates, spending, inheritance, deterrence episodes and inflicted civilian deaths can be inspected without changing the simulation.

## Evidence and model boundaries

- [IAEA infrastructure methodology](https://nucleus.iaea.org/sites/INPRO/Pages/methodology.aspx): institutions, finance, industrial infrastructure and skilled people are necessary inputs to a real nuclear program. These inform prerequisites, not the game's numerical coefficients.
- [IAEA history of atomic energy](https://www-pub.iaea.org/MTCD/Publications/PDF/Pub1032_web.pdf): nuclear science, civilian energy and military application have related but distinct institutional histories.
- [US Department of Energy: stockpile stewardship](https://www.energy.gov/nnsa/us-nuclear-weapons-stockpile): recurring maintenance, surveillance and replacement support keeping physical inventory separate from funded operational readiness.
- [United Nations: nuclear disarmament](https://www.un.org/en/peaceandsecurity/what-nuclear-disarmament): nuclear weapons have major humanitarian and international-political consequences. This does not validate a particular deterrence theory or numerical escalation model.

## Focused verification

`--nuclear-only` covers stable IDs, versioned catalog lengths/useful levels, finite prerequisites and cost growth, conserved research allocation, knowledge-versus-deployment, resource/fiscal/security production constraints, delivery-dependent deterrence and rare escalation gates.

`--nuclear-world artifacts/history31/earth-release/final.world` checks an explicitly upgraded old checkpoint, unchanged old integrity, zero free weapons, 39-entry continuation/save-load, imported designs without free deployment, actual paid state production, same-date idempotence, unfunded dismantling, custody conservation, real regime-change account alignment, an actual city's casualty deltas, diplomatic consequences, read-only profile integrity, nonempty arsenal serialization and exact 10-year versus 3+7-year continuation hashes.

The current suites pass 99 pure and 27 integration assertions. The latter include
actual successor state-event upkeep across an inherited unpaid interval,
same-date successor idempotence, unfunded inherited retirement despite a short
parent-event argument, and prevention of future-date negative-cost cash credits.

The independent code critic found no duplicated population deduction or free custody creation. A misleading levy-only casualty label was corrected. Longitudinal fresh-world pacing and the frequency of nuclear programs remain separate validation tasks: mechanism tests do not prove realistic world-history outcomes.
