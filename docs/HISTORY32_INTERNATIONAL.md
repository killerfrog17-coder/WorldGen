# History32 international institutions and linked wars

`HistoryInternational.cs` adds an optional `HistoryState.International` extension.
It is separate from political ownership, civilization, cultural ancestry and
technical knowledge. History31 and earlier retain null and their previous
integrity hashes. Fresh history32 and explicit upgrades from earlier rules create
the extension; loading an old save never silently changes its rules. Interim
development history32 snapshots with a null extension remain readable, rather
than acquiring an invented diplomatic past.

## Actual contacts, not an omniscient state census

Every state has an index of its actually recorded bilateral relation IDs.
Creating an ordinary relation updates both indexes. A state event captures at
most eight IDs before changing any roster, resolves living counterparts and
prunes ended or superseded indexes. Capturing IDs first matters because the
persistent local bag removes entries by moving its last slot.

Constitutional/dynastic continuity transfers the persistent contact roster and
up to two institutional memberships without walking territory or all partners.
Historical relation records keep their original parties. A successor gradually
normalizes those contacts into current relations, inheriting diplomatic habits
and retiring the obsolete index from its active portfolio. Payment dates transfer
too, preventing duplicate dues after a change of government. Collapse without
continuity removes active memberships; a body with fewer than two members lapses.
Former organizations and diplomatic records remain available to historical lore.

A separate continuity-only successor marker also preserves an actual ongoing
war across a constitutional/dynastic replacement. Each existing campaign lazily
resolves its two participants at its next event, updates the current relation
and commander, and retains its original start date and supplied theater. Sorted
participant casualty accounts are preserved if the new actor IDs reverse their
ordering. Conquest, annexation and collapse do not set this marker and cannot
automatically inherit a defeated state's wars. No succession scans every war.

One-time explicit upgrade reconstruction may examine saved states, relations and
campaigns. Ordinary simulation events do not search all states, organizations,
members, campaigns or terrain cells. Validation and requested atlas/export work
are separate from that event-time contract.

## Negotiated, funded organizations

Two organization types are currently modeled. Each state can belong to one of
each type; overlapping regional/specialized bodies of the same type are deferred.

- Trade leagues require actual peaceful trade, trust, deployed accounting and
  coinage, and institutional capacity. They reduce friction only on already
  physically reachable, supplied foreign trade legs. The maximum benefit is
  12% to transport-range friction, not a raw output bonus. They create no ore,
  money, human population, shipping route or automatic technical transfer.
- Peace congresses require deployed printing/institutions and actual recent
  conflict/reconciliation among known partners. There is no universal modern-era
  deadline, automatic UN, global invitation sweep or inevitable world government.
  The model permits regional diplomatic forums before industrial technology.

Invitations follow known relations. Trust, leadership, exhaustion and opportunity
determine acceptance; governments can refuse, withdraw from a weakened body or
lose treaty privileges after sustained nonpayment. Founders and joining members
negotiate using the actual invitee's leadership; two founders are limited by
the less willing government's acceptance, not by a favorable unrelated ruler.
Their governments contribute actual treasury funds. Dues are capped by available treasury and a
finite reserve limit. Contributions are not tax receipts created from nothing.

### Operating budgets, rather than indefinite accumulation

Fresh history32 institutions additionally carry an optional `Finances` ledger.
Each actual member has a cached annual assessment based on fiscal receipts;
their sum sets the organization's operating scale. Current capacity-to-pay
assessment is approximately 0.1% of annual model receipts, with small integer
office overhead and explicit extreme-value caps. This is an authored fiscal
proxy, not a literal UN tax rate or a claim that UN and WTO budgets use identical
assessment formulae.

The normal reserve-funding target is three annual operating budgets. Contributions
are limited by the member's accrued assessment, actual treasury, and outstanding
operating work plus the deficit below that target. A fully funded reserve waives
additional dues without marking the member delinquent. Founding and accession
deposits also use bounded operating assessments, rather than years of the state's
entire taxation. No routine sweep recomputes a world's or organization's members:
joining, leaving, succession and each member's own update change cached sums in
constant time.

When a member's assessment increases, it pledges three times its own increase
toward the enlarged working-capital target. Its actual payment shares the same
5% current-treasury exposure cap as ordinary dues, and is still capped by the
common funding deficit. This is not permission to debit one country for the
whole organization's budget. Founding/accession payments credit that member's
pledge so startup cash is not charged twice. Lower assessments reduce outstanding
pledges as appropriate, but do not manufacture a cash refund. A sufficient common
reserve waives an unneeded remaining pledge.

A separate body-level date records the elapsed operating requirement exactly
once. Actual common cash funds that work and enters `ServiceSpending`; unfunded
work remains an unmet requirement, not magically paid staff or an income stream.
When member events are batched several years apart, a later member can fund the
same outstanding requirement without charging a second bill. This avoids a
three-year reserve cap spuriously starving an institution merely because its
members' actor updates arrive five or twenty years apart. Institutional effects
use funded operating coverage, including actual cash backing for newly elapsed
work. Already-paid current work remains real service even if it consumed the
last reserve unit. Unfunded new work gradually reduces that coverage; a high old
authority score or one leftover unit cannot buy permanent service.
The operating window also retains its already-applied authority adjustment.
Later-arriving member payments reconcile that same interval's funded outcome,
rather than permanently punishing the first payer's temporary cash shortfall or
granting a fresh authority bonus once per member.

Earlier interim history32 snapshots retain null `Finances`, their old hashes and
their previous financial continuation. The explicit
`UpgradeInternationalOperatingBudgets` operation creates the new ledger on a
copy, preserves every existing reserve unit, and begins assessment at the upgrade
boundary. It neither burns an old accumulated hoard nor invents a retrospective
refund or operating bill. The null field is omitted from canonical hashes.

The fiscal invariant is:

```text
total recorded contributions = current reserve + service spending + mediation spending
```

Per-member payment dates prevent duplicate contributions at one date. A separate
organization service date prevents each member from charging the same elapsed
secretariat costs. A separate mediation date permits a negotiation after that
year's dues processing, but never one duplicate attempt per member.

Funded, authoritative peace forums modestly reduce bilateral war hazard; the
maximum reduction is 35%, not immunity. An attack on a fellow member damages
bilateral trust and institutional authority. The body can spend its finite fund
on mediation. Exhaustion, trust and both leaders' diplomacy affect acceptance.
Success ends the **actual indexed campaign** with a recorded ceasefire. Failure
spends negotiation resources and damages authority. Neither membership nor the
existence of a charter compels belligerents to accept a proposal.

## Alliance chains and multiple fronts

A state samples at most three actual allied contacts within its existing contact
portfolio. For each, it inspects at most three actual known war relations.
Participation requires a live campaign, actual alliance, no current truce or
conflicting alliance, sufficient cohesion, available levy and treasury, and a
physically reachable supplied theater through the existing campaign system.
Nuclear deterrence remains in the ordinary war-entry path.

Defending a trusted attacked ally has a materially stronger response hazard than
volunteering for that ally's offensive conquest. Exhaustion weakens commitment.
Actual dispatches debit the participating state's treasury and add exhaustion.
There is no global simultaneous chain reaction, free allied force multiplier or
teleported army. Existing local supply, losses, peace and provincial resistance
still determine each front's outcome.

Successful entries record their originating campaign and ally. Consequently a
larger conflict can have connected fronts and a coherent narrative without
merging all participants into one country. This remains a linked bilateral-war
model: formal theater commands, multilateral peace conferences, detailed treaty
clauses, war reparations and a complete international legal system are deferred.

## Sources and priors

- The [UN Charter's regional-arrangement provisions](https://www.un.org/en/about-us/un-charter/chapter-8)
  distinguish negotiation and regional institutions from unrestricted world
  government. The [Charter](https://www.un.org/en/about-us/un-charter/full-text)
  also distinguishes collective self-defense from authorization of enforcement.
- The [UN's history of the League of Nations](https://www.un.org/en/about-us/history-of-the-un/predecessor)
  supplies a counterexample to treating the creation of a peace institution as
  guaranteed successful collective security.
- The [WTO's description of regional trade agreement scope](https://www.wto.org/English/Tratop_E/region_e/scope_rta_e.htm)
  supports modeling reduced trade barriers rather than free physical output or
  a magical route to every distant member.
- The [UN Committee on Contributions' assessment explanation](https://www.un.org/en/ga/contributions/assessments.shtml)
  ties member assessments to financing an approved appropriation. Its
  [scale criteria](https://www.un.org/en/ga/contributions/budget.shtml) include
  capacity to pay and adjustments, rather than equal unlimited cash collection.
- The [WTO budget description](https://www.wto.org/english/thewto_e/secre_e/budget_e.htm)
  weights contributions by international trade. The
  [2025 expenditure budget](https://www.wto.org/english/thewto_e/secre_e/budget_e/budget2025_e.pdf)
  allocates real staff, services and other expenditure rather than accumulating
  nearly all historical contributions indefinitely. Our three-year reserve
  target is a conservative game liquidity buffer, not a measured UN/WTO rule.

These institutional examples motivate mechanisms and failure modes, not our
numerical coefficients. All thresholds, finances and probabilities are explicit
game priors. No reference implementation was copied.

## Checks and observability

`--international-only` checks capability gates, uncertain acceptance, conserved
and overflow-safe contributions, finite treaty effects, defensive/offensive
alliance commitment and isolated copies. `--international-world <world>` checks
an actual eligible league foundation, treasury transfers, payment dates,
per-body service/mediation accounting, real campaign registration, successor
contacts, bounded cleanup of a stale-heavy portfolio, read-only profiles,
canonical save roundtrip and partition-independent continuation. The official
history31 Earth fixture passes 14 pure and 22 integration assertions, including
an actual contested regime replacement during war and scheduled continuation.

`--international-finance-only` adds nine pure budget assertions.
`--international-finance-world <interim-history32-world>` adds nineteen checks
against an actual saved organization: old-null integrity and fund preservation,
isolated copies, 2,000 years of bounded reserve and paid operation, exact cash
conservation, same-date idempotence, twenty-year versus annual fiscal batching,
reversed-payment-order authority reconciliation,
assessment growth and bounded own working-capital pledges, falling assessments
without refunds, current paid work versus unfunded future work,
unfunded effects, withdrawal and succession cached sums, read-only profiles,
new-ledger save/load integrity and actual-calendar partition determinism.

`InternationalProfile` exposes only recorded contacts and bounded membership
summaries for the atlas. Known-relation counts may temporarily include obsolete
entries awaiting bounded cleanup; they are not an asserted census of unique
living countries. Significant foundations, accessions, withdrawals, charter
breaches, mediated ceasefires and linked war entries enter the dated archive.
Routine dues do not flood the world chronicle.
