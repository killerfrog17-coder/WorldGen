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

`InternationalProfile` exposes only recorded contacts and bounded membership
summaries for the atlas. Known-relation counts may temporarily include obsolete
entries awaiting bounded cleanup; they are not an asserted census of unique
living countries. Significant foundations, accessions, withdrawals, charter
breaches, mediated ceasefires and linked war entries enter the dated archive.
Routine dues do not flood the world chronicle.
