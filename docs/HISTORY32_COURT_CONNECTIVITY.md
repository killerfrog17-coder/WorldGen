# Court connectivity and state display — 17 October 2026

Owner-requested scope: keep distinct advanced/embryonic gray stages; remove pale recognized-claim shading; distinguish sovereign states by state identity rather than their ruling culture; make disconnected early possessions less durable and early direct administration more local. No terrain, climate, origins, technology costs, era names or music changes.

## Display

States map colors, cached/literal palette IDs and legend swatches now use the sovereign state ID. Constituent states still belong to their sovereign realm in States; Territories exposes their individual courts. Civilizations and Cultures retain their separate groupings. Recognized claims retain the holder's full color; the claim/control distinction remains in inspection text, not a pale tint or extra legend row. Advanced cultures use gray0.70; local embryonic centers0.46; unclassified occupied populations remain0.78. Local proto-center labels no longer contradict the parent tradition's regional AdvancedCulture classification. These are presentation changes and apply to existing saves without a rules upgrade.

## Optional realm revision30 (not history-30)

Fresh history32 uses saved `Connections.Realms.Version=30`. Existing29 and earlier saves preserve their exact dispatch until the existing explicit rules-upgrade action. The integer revision participates in the existing save/hash representation; no extra time gate, global census or repeating process is added.

The previous structural-live test accepted an owned town as a valid service source regardless of rival territory separating it from the court. Local politics mostly measured endpoint distance. The additional city-local review checks actual sovereignty along a bounded land graph. Own realm/constituent territory and unclaimed countryside permit courier passage; unrelated states and whole-cell water do not grant automatic transit. An ordered96-expansion probe, with at most384 queued/seen nodes, explicitly distinguishes connected, proven blocked, and unknown. Exhausting the bound is not evidence of isolation. It is an approximation, not an exhaustive global transport solver.

Supported existing harbors can bypass a land obstruction only with deployed state maritime administration, actual viable departure communities, funds, physical sea-lane/open-water checks and the existing fleet-network range. Nearby actual harbors can serve inland capitals. Distance remains an effort metric: river/coastal transport can make a long narrow state more maintainable than a shorter overland one.

Organized cut-off centers gain a separate decadal independence hazard; excessive effort relative to actual core service also contributes. Modern bureaucracy damps distance pressure without granting immunity to a real blockade. Recently transferred communities have a15-year grace period. Actual nearby reachable courts can receive a separating town; foreign-cultural alignment is less likely. A supported institutional town can establish a successor court. A smaller settlement can cease answering to its inaccessible ruler without automatically becoming a state. Existing ownership transactions and bounded district recovery preserve people, cultural identities and accounting; there is no whole-province membership scan.

Ordinary early voluntary incorporation and unopposed direct frontier growth must fit the capital's administrative core. Installed writing, roads, public works and transport expand this continuously. Existing mature ocean-navigation/railway delegated-administration systems are exempt from that ancient core envelope: this is not a new capital-centered cap on late colonies. War, conquest, vassal realms and existing costly military supply remain separate. Nonmaritime offices with a proven severed parent connection close through the existing source/district recovery mechanism. Unknown routes alone do not close offices.

## Historical rationale and limits

[The Met's Uruk account](https://www.metmuseum.org/essays/uruk-the-first-city) describes urban provisioning/accounts and subsequent competing city-states, alongside trade and conquest. It does not imply that every settlement immediately governs an entire cultural region. Transport-cost geography, not a universal empire-size ceiling, guides the design; [Stanford ORBIS](https://history.stanford.edu/publications/orbis-stanford-geospatial-network-model-roman-world) is a conceptual reference for the land/water distinction, not copied implementation or numerical calibration. The new coefficients are game priors. They do not establish that every world's early borders or full historical progression are now realistic.

## Validation

-118 focused connectivity/state-map assertions, including an actual Core route on a tiny ownership graph: same distance, rival corridor blocked; own/constituent corridor open; lake blocked. Bounded exhaustion returns unknown. Independent states of the same ruling culture have distinct colors and selections.
-188 combined territorial-continuity assertions,30 regional-stage assertions,83 claim/color assertions and23 territorial assertions pass. The small legacy territory fixture needed its missing historical culture records populated before its existing renderer tests could execute.
-Full solution build passes with zero warnings/errors. Browser JavaScript syntax check passes.
-Actual200k Earth explicit30 upgrade,10-year one-piece versus3+7 replay, world-file reload and subsequent2-year continuation pass4 assertions. A subsequent final-source rerun includes the nonmaritime-office check.
-Old29 actual200k Earth one-year continuation matches the frozen29 executable exactly: `6632cd077e16a3eca4095de9a847df9c9c746e4a0d722789b6be504bfa454666`.

Native packaging/review is recorded below when completed. These targeted checks are not a new all-era realism or dense performance acceptance. Previously documented overnight performance/geography failures remain open.
