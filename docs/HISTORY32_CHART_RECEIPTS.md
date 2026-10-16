# Durable chart receipts, candidate history32

The frozen K Continents Plus audit found 206,863 `charted-coast` records among 255,536 archived events. Only 106,832 culture/destination pairs were distinct: 100,031 records repeated an acquisition already recorded for the same culture and destination. One pair repeated 55 times over 3,152 years. The working chart cache correctly evicted old entries to stay bounded, but reacquiring one incorrectly presented it as a new historical acquisition forever.

Candidate history32 now separates the two concerns:

- `HistoricalCulturalContinuity.Charts` remains the existing bounded working navigation cache. Its budget, eviction order, dated source, route availability and successful reacquisition are unchanged.
- Optional `SeenCharts` stores a compact immutable receipt, keyed by destination region with its first recorded year. A repeat still restores the working chart through the real delivery caller, but does not append another acquisition event.

The receipt is **not** a chart, voyage, entitlement, discovered route or contact. No travel or colonization query grants access by consulting it. No global or cross-culture knowledge is awarded. Different cultures can still record their own first acquisitions. There are no new random draws or calendar events.

The existing persistent radix tree gives a fixed five-level lookup/update. Only a newly recorded destination copies its small path. A copy safely shares immutable nodes. Explicit save integrity validation visits actual allocated entries, checks region/date bounds and rebuilds fingerprints; it does not allocate or enumerate every possible region for every culture. Recurring actor events never enumerate the tree.

Old fields stay null and are omitted from canonical hashing. Histories through31 keep their exact prior chart and event behavior. Older candidate32 saves lazily initialize receipts from at most256 current working charts on the next real acquisition, never by scanning or rewriting the event archive. Thus some previously forgotten destinations may receive one further acquisition record after migration; subsequent repeats are suppressed. Existing events and archive reference IDs are never deleted or renumbered retroactively.

## Verification

`HistoryChartMemoryTests` passed six pure and thirteen official-history31-Earth integration assertions in an isolated current N build with zero warnings. Coverage includes the independently measured frozen-H null-record fingerprint, immutable copies, insertion-order equivalence, sparse validation bounds, invalid keys/dates, working-cache eviction/reacquisition, bounded migration, full binary snapshot round-trip and deterministic continuation/publication partitioning.

The mechanical regression runs both branches under the **same history32 rules**. One branch remembers an evicted destination; the other represents an older candidate that must bootstrap and records one extra narrative event. After delivery, active charts and receipt trees are identical. Queues, populations, cities, states, research, territory and diplomacy match both immediately and after three simulated years. Chronicle records and disease-wave archive attribution indices are intentionally excluded from that comparison; outbreak probabilities use town/kind/year, not those reference IDs. No comparison switches to older geographical or technological rules.

The offline audit scanned historical records solely to diagnose this problem. No such archive census was added to runtime events. This change removes exact repeat acquisitions; it does not suppress the remaining legitimate first acquisitions or redesign chronicle editorial selection.
