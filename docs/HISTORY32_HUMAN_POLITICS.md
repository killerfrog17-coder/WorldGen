# History32: human variation and political programs

These are history32 rules, independently versioned from geological generation
and the `earth-native-3` static fauna pack. Existing history31 and earlier saves
retain their former behavior until an explicit rules upgrade. No new canonical
history fields, actor arrays or terrain memberships are introduced here.

## Human appearance ledger

`HistoryHumanVariation.cs` interprets the existing eight regional ancestry means
as skin pigmentation, hair pigmentation, iris pigmentation, hair curl, body
compactness, facial variation, nasal variation and stature potential. Every
index is bounded to 0..10000. These are coarse phenotype proxies, not simulated
genes, a census of individuals, fixed racial categories or measurements in cm.
Only Homo sapiens is implemented. The species profile is a future extension
boundary, not permission to silently apply human adaptations to fantasy species.

Fresh, unstarted, zero-population history32 initializes only the human origin
region. Unoccupied regional values have no phenotypic meaning; first residents
bring their source ancestry. Upgrading a progressed older save does **not** reset
its existing neutral ancestry values or invent a retrospective genetic history.
Their new interpretation is consequently approximate and evolves prospectively.

At each existing occupied-region event, exactly eight traits are updated. The
existing cached contact-resident estimate includes the region's proportional
share of state-owned remote countryside, so rural residents do not disappear
from adaptation when their account moves into a state's remote ledger:

- Skin pigmentation approaches a smooth UV target over approximately18000
  years. The unitless UV proxy includes latitude, elevation and modest cloud and
  canopy shielding; it does not equate cold temperature with light skin.
- Body compactness has a much slower, deliberately modest thermal tendency.
- Hair, iris, facial and nasal variation, hair curl and stature potential are
  primarily inherited means with seeded, effective-population-scaled neutral
  drift. The model does not assert that a particular climate causes an eye fold,
  face shape or a named modern ethnicity.
- Installed material buffering weakens environmental selection. Diet, clothing,
  ancestry and migration confound any direct climate-to-appearance inference.

Physical migration, voyages, exchange and secondary settlement founding mix the
eight source and destination means by actual resident/migrant weights. The
destination includes settled residents as well as remote regional residents;
a few arrivals cannot overwrite the appearance of an established metropolis.
The existing demographic transfers remain authoritative. No appearance trait
changes intelligence, technological aptitude, political capacity or fertility.

The API provides `RegionalAppearance` and a bounded local `CultureAppearance`
sample for atlas swatches. A cultural sample explicitly does not claim that all
members of a culture are phenotypically identical. Old-rule queries report
unavailable rather than mislabeling an old neutral ancestry vector.

Costs are O(8) per existing region or migration event, with deterministic named
streams, quantized gains and absolute event dates. There is no cell census,
ancestry-tree traversal, individual genome or ecological population process.

## Political interests, constitutions and funded policies

`HistoryPoliticalPrograms.cs` derives five interest blocs from existing state
accounts and constitutional axes: landholders/provincial notables, merchants and
artisans, military/court, ritual institutions, and urban/rural commons. The five
normalized weights are **not measured party membership**. They describe modeled
political leverage; modern named electoral parties, elections by district and
individual politicians remain future work.

Government descriptions distinguish dynastic administrations, military courts,
local confederations, councils of estates, merchant oligarchies and representative
or federated governments. Ancient participation means negotiated voice, not an
assertion of universal suffrage. Printing increases bargaining pressure but
does not guarantee democracy. Leaders can negotiate privileges or concentrate
authority, with meaningful effects on legitimacy and cohesion. Existing local
community inclusion and autonomy consume the resulting representation and
centralization axes. Contextual founder priors have wide seeded overlap:
irrigation does not force despotism, nor does every port become Venice.

The existing state event divides a finite public budget into defense, works,
relief and learning. Shares are nonnegative and sum to1000; integer allocation
conserves the full budget. Available spending is limited by actual treasury and
receipts over the elapsed interval, then debited once. Effects include:

- Small bounded taxation changes from administration and negotiated participation.
- Defense priorities affect mobilization only insofar as actual allocated funds
  support the levy. A bankrupt state receives no free military-policy bonus.
- Works and learning improve the existing public-works capacity, with leader
  administration and elapsed-time caps; unfunded infrastructure decays.
- Funded relief modestly supports legitimacy and cohesion.
- A stable sampled cultural center supplies a bounded research multiplier from
  learning priorities and existing funded public-works capacity. This is a joint
  infrastructure/learning proxy, **not** a saved ledger of dedicated laboratories
  or a promise that every ear-marked coin has separately persisted effects.

Four spending priorities compete: increasing defense leaves less for works,
relief and learning. Government reform changes these incentives rather than
creating an unconnected decorative statistic. A disputed succession preserves
the country's endonym instead of automatically renaming every successor as
another capital Kingdom, Empire or Commonwealth. Political form belongs in the
government profile and need not be appended to each map label.

All policy calculations use fixed-size vectors and existing actor aggregates.
No policy event enumerates every city, province, culture, ecoregion or terrain
cell. No new persisted fields require reinterpreting the old canonical records.

### Round Q: remove one-way constitutional drift

Two late controls converged on representation950 and centralization150. The
old concession test added low centralization to the same leader-skill sum that
decided whether to concede. Each concession then raised representation60 and
lowered centralization35, making the next concession easier. In a uniform
200..850 leader-skill comparison its concession probability rose from about
7.4% at centralization950 to92.6% at150. This was an endogenous feedback defect,
not evidence that all prosperous societies should become federations.

The revised rare bargaining opportunity retains its actor schedule. A bounded
coalition probability now uses the five existing interest weights, leader
inclinations, elite capture, war/exhaustion and modest literacy pressure.
Representation supplies negative feedback; decentralization is not a positive
input to another concession. Administrative centralization instead approaches
a slow, bounded target from actual fiscal capacity, geographical load divided
by current effective reach, court/notable influence and leadership. Concessions
and restrictions can both be accepted or contested. Repression still creates
exhaustion. These are explicit simulation priors, not fitted constitutional
predictions or a claim that printing inevitably produces democracy.

A matched1500-year continuation of the same O Earth131700 checkpoint held the
other P1 rules constant. The old branch ended with19 states,11 labeled federated
representative and161 reforms versus38 restrictions. Round Q ended with29
states:7 representative,2 federated representative,16 merchant/civic oligarchies,
3 dynastic/provincial administrations and1 central dynastic administration;
141 reforms versus62 restrictions. Both branches had approximately1.35 billion
people. Actual Q state-map imagery was reviewed: broader political and town
networks appeared, but this single control is not a validated distribution of
real governments. Eurasian settlement gaps and some very remote coastal titles
remain review questions. Counts describe the modeled labels, not measured
popular participation or universal suffrage.

The Q assertions exercise counter-pressure, war/coalition effects, finite
funding and administrative reach. The focused political suite currently passes
153 pure and7 world/save/replay assertions. The history31 continuation hash is
unchanged. Independent source review found the positive-feedback defect removed;
larger multi-world controls remain necessary.

## Sources and limits

Mechanisms and counterexamples, not the numeric game priors, are informed by:

- [Jablonski and Chaplin, PNAS2010](https://doi.org/10.1073/pnas.0914628107),
  on human pigmentation and UV; and original body-form/population-history
  studies [PMC5003290](https://pmc.ncbi.nlm.nih.gov/articles/PMC5003290/) and
  [PMC7801440](https://pmc.ncbi.nlm.nih.gov/articles/PMC7801440/).
- [UK Parliament: representation in England](https://erskinemay.parliament.uk/section/4500/history-of-representation-in-england/)
  and [taxation](https://www.parliament.uk/about/living-heritage/transformingsociety/private-lives/taxation/overview/),
  on bargaining, taxation and civic/estate participation.
- [House of Commons Library: history of the parliamentary franchise](https://commonslibrary.parliament.uk/research-briefings/rp13-14/),
  distinguishing limited historical representation from universal adult suffrage.
- [Metropolitan Museum: Venice and the Islamic World](https://www.metmuseum.org/essays/venice-and-the-islamic-world-828-1797),
  a comparative maritime-trade case, not a rule that all coastal communities
  inevitably develop identical governments.

- [Acemoglu, Ticchi and Vindigni: A Theory of Military Dictatorships](https://www.nber.org/papers/w13915)
  and [Acemoglu, Egorov and Sonin: Democracy By Mistake](https://www.nber.org/papers/w23944),
  for contingent institutional bargaining and conflict rather than automatic
  technology-to-regime transitions. Their models do not supply the numeric
  game coefficients above.

No source code was copied. These reduced actor models do not claim scientific
genetic reconstruction, a complete parliament, population-level electoral
preferences or a validated economic budget model.

## Validation

Targeted runner flags in `WorldGen.Core.Tests`:

```text
--human-variation-only
--human-variation-world <world>
--politics-only
--politics-world <world>
--living-world-test <world>
```

The human suite covers 45 pure assertions and 12 world/save/replay assertions,
plus an owned-rural-region query assertion when that fixture contains one.
read-only queries and cold/warm cache equivalence preserve saved read counters.
Politics covers 132 pure assertions and 4 fiscal/save integration assertions.
The official history31 Earth fixture additionally passes 8 lifecycle/founder
integration assertions, including actual secondary founding with conservation
of urban, suburban, rural and remote households. A fresh old-rule checkpoint
remains unchanged by upgrade and diagnostics. The targeted isolated Release
build completes without warnings. These checks do not substitute for the
separate multi-world screenshot and long-run historical reviews.
