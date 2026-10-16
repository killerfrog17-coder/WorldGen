# History32: unsettled colonial office reviews

This is an experimental history32 correction, not a crop, founding-cost or
population multiplier. Existing32 snapshots with a null extension retain the
old retry behavior until `Prehistory.UpgradeColonialPortRetries` is explicitly
called. Fresh32 worlds and older-rules-to32 upgrades initialize the extension.
Histories through31 and null canonical records are unchanged.

## Observed failure

The frozen O Earth200k checkpoint at elapsed year131700 had:

| Government | Empty maritime offices | Currently fully supplied/fundable offices | Oldest eligible office |
| --- | ---: | ---: | ---: |
| 49 | 246 | 12 | 1,087 years |
| 62 | 68 | 22 | 1,433 years |

The 1,433-year example is actual post205 at cell160132, with real source
town517. Its source-compatible crop yield was0.457299; the supplier had935424
food units against71875 required by the complete real provisioning check.
There was no need to grant crops, households, food or free shipping.

Previously a state sampled two entries from *all* its administrative posts,
including settled and unsuitable offices. A selected site's founding chance
then used only the most recent state interval, not its time between reviews.
None of the34 eligible offices above occupied the current two retry slots.
This combined random sampling and short elapsed credit could leave funded,
viable ports unreviewed for centuries.

## Changed mechanism

`Connections.PortSettlements` is a nullable saved extension with one pending
office bag and cursor per government, plus a persistent sparse last-test-date
tree keyed by actual office ID. Explicit setup may enumerate existing offices;
ordinary simulation events may not.

A government reviews at most eight pending offices, in round-robin order,
using its existing maximum of four actual departure locations. Successful
town foundations, actual abandonment, territorial withdrawal, office closure
and ownership changes maintain membership incrementally. A continuing regime
inherits its pending bag and dates; a dissolved court clears its bag in O(1).
Persistent bag/tree updates copy bounded radix paths, not the state's posts.

The opportunity interval is time since that office's last test (or founding),
**capped at forty years**. Every attempt still tests current actual ownership,
physical supply, real source culture and cultivars, viable destination fields,
available households, food reserves and treasury. A 1,433-year vacancy does
not receive 1,433 years of presumed valid conditions. Actual abandonment starts
a new opportunity period: occupied centuries are not banked. Multiple
departures share the same target/year lottery rather than multiplying it.

This is a conservative endpoint approximation, not a claim to have monitored
continuous funding or field viability throughout an unattended interval.
Backlogged queues may under-credit opportunity. Administrative review latency
depends on the actual state-event schedule, eligibility to undertake colonial
planning and pending count; it is not an unconditional five-year guarantee or
a dedicated per-office forty-year event scheduler.

No crop, terrain, charter eligibility, source migration or food/fiscal gate
was relaxed. Founding still transfers real people and separately debits startup
food, voyage rations and fiscal investment exactly once.

## Verification

Focused tests: `HistoryColonialPortRetryTests`, runnable with
`--colonial-port-retry-only` and `--colonial-port-retry-world <snapshot>`.

The isolated build completed with zero warnings. The pure suite passed1135
assertions, including1000-office fairness, removal/swap-back behavior, bounded
batches, persistent-copy isolation, first-review time caps and independently
measured frozen-O null-record canonical parity. The actual frozen O Earth
fixture passed22 integration assertions: the1,433-year site is genuinely ready
and receives a fair review; an eligible stochastic success conserves world
population and exact household/food/fiscal accounts; no funds, no food and no
maritime service cannot create a port. Real abandonment and withdrawal update
the index. Full validation, serialization, continuation and1+2 versus3-year
publication partition hashes pass. An independent critic read the source and
tests and found no accounting or causality blocker.

Read-only pre-fix diagnostic JSON is retained outside source control under
`D:/WorldGenBuildArtifacts/history32-port-planning-o-final/port-planning.json`.
This is evidence of a scheduler bottleneck, not acceptance of globally realistic
colonization; post-fix multi-era controls and map criticism remain necessary.

A100-year same-checkpoint follow-up used the same current source for both
branches (including the independently authored current-state maritime-practice
fix). Only the explicit retry extension differed. The old-null branch founded
one sponsored port; the indexed branch founded13. Total new towns were56 and70,
respectively; final living centers489 and505. This is consistent with removing
the identified retry starvation, not with granting arbitrary new populations.
The indexed branch processed79627 actor events in2.40 seconds, with105ms worst
summed work for one simulated year, under the native offline diagnostic. The
old-null run was first and colder (3.65 seconds/173ms); these serial timings do
**not** establish that extra retry work is intrinsically faster, nor substitute
for Unity performance acceptance. Compact A/B evidence is
`D:/WorldGenBuildArtifacts/history32-port-retry-100y.json`.
