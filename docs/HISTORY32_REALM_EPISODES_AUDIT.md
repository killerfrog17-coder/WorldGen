# History32: country continuity and imperial breakup audit

Read-only research and checkpoint audit, 17 October 2026. This is not a new
political rule or an acceptance claim. No Core physics was changed for this
audit. A country's name, its people, a particular court, and its present
sovereignty must not be treated as one lifetime.

## Reproduction and definitions

`--realm-episodes-audit WORLD NEW_OUTPUT` validates a saved world, inspects its
actor and event archives, and checks that its canonical state is unchanged.
It writes `summary.json`, `states.json` and bounded `selected-episodes.json`.
This is explicit offline analysis, not a recurring simulation census. It does
not enumerate territory cells or backdate current technology deployment.

Artifacts are under `D:/WorldGenBuildArtifacts/overnight-20261017/`:

- `realm-episodes2-{c,d}-{earth200,cp200}` and
  `realm-episodes3-e-earth200` inspect complete longitudinal final worlds.
- `realm-episodes3-e-cp200-compact` inspects the complete E5 Continents Plus
  endpoint after compact-file serialization succeeded; this is not a new
  simulation branch or an explicit rules upgrade.
- `episode-e-earth-iron/episode.json` and
  `episode-e-cp-medieval/episode.json` record 25-year political observations.
  Their `Source` names baseline world files, but the driver discards their
  loaded histories, calls ordinary `Prepare`/`Start`, and runs **fresh E history
  on the accepted saved terrain** until the requested era. They are separate
  observation runs, not explicit mid-history upgrades. This provenance was
  checked in `HistoryPoliticalEpisodes.Run`, not inferred from the path name.
  `realm-episodes3-e-cp-medieval` inspects the latter branch's saved endpoint.

Country founding, polity actor founding, culture founding, ordinary ruler
succession, last contested court transfer, and current patron/sovereign are
reported separately. `RegimeSince` currently records contested court changes,
not the beginning of a demonstrably unchanged dynasty or constitution.
The ordinary ruler's actual `LastRulerSuccession` comes from state dynamics;
the older aggregate `LastSuccession` field is also retained and explicitly
labeled, since it can remain at foundation.

Current administrative area, reported subject area and recognized external
titles are different ledgers. Ended actors have **zero current holdings** even
when their frozen historical record retains area or population. Observed peak
area has no saved date; it may include subjects and is not a reconstructed
simultaneous territorial census. Paired `secession` and
`provincial-independence` events can describe one court, so event totals are
not counts of independent countries. The diagnostic additionally counts
distinct separating counterpart IDs.

## Long-lived country IDs are not untouched empires

| Endpoint | Oldest living country | Actor age | Current own / subject km² | Observed peak km² | Last contested change: years ago / total changes |
| --- | --- | ---: | ---: | ---: | ---: |
| C Earth205500 | Lusisu su20 | 3868 | 2,574,851 / 3,583,667 | 6,793,144 | 478 / 9 |
| C CP186700 | Sasfasaši2 | 5683 | 746,491 / 0 | 3,694,918 | 26 / 16 |
| D Earth205100 | Biqis gi16 | 3832 | 262,104 / 0 | 685,190 | 47 / 12 |
| D CP187100 | Sasfasaši2 | 6083 | 1,026,082 / 0 | 2,739,549 | 58 / 18 |
| E Earth194200 | Fa haji34 | 5528 | 261,248 / 0 | 1,603,565 | 242 / 12 |
| E CP190800 | Chot noplefe66 | 6217 | 80,823 / 0 | 3,426,031 | 86 / 33 |

E Earth's oldest country is a **dependency of court206**, with that stored
relationship dating to194159. It controls only16.3% of its observed peak,
has266 recorded rulers, eleven succession crises and31 distinct recorded
separating courts. Its current capital Fa wifa430 remains inhabited by548,223
people and controlled by34; its last recorded territorial change is a conquest
at194170. That is neither immortal sovereignty nor a mere dynasty-name swap.

The largest historical E Earth actors also contract: Fo milu257 holds693,905
km² against a9,759,186 peak; Fo losofu217 holds892,530 against7,644,450.
Fo sosini161, whose peak was8,904,325, actually ended194179. The first state,
Dussikral0, ended188410 after3109 actor-years and six crises. None of these
continued historical peak areas should be painted as current empires.

The complete E5 Continents Plus endpoint contains6311 historical polity actors,
435 surviving governments and375 presently independent courts. Its oldest
country Chot noplefe66 has302 recorded rulers, twenty-four crises and123
distinct recorded separating counterpart courts. It now holds2.36% of its
observed peak. Its inhabited capital To noplefe703 has1,234,583 residents;
its most recent narrated territorial event is another provincial separation
at190781. The stored independent-foreign-policy relationship dates only to
190766, not its6217-year founding age. The archive explicitly records forced
subordination to Chus fusnu2689 at190615 and independence amid that patron's
contested succession at190766. Earlier it was Chut sumihikh81's subject from
185278 to185503, then defeated and subordinated its former overlord at185957.
Those are changes of actual foreign-policy allegiance, not just new ruler names.
The largest historical actor Lamhaso107
actually ended190659 when Thaliso captured its remaining center Farfasal;
its6,987,238km² peak is not current territory. Other former large actors
Mahanso2600 and Mofyfte1099 ended190345 and189475 respectively. Conversely,
Khufoso130 survives with10,164km² against a1,265,956 peak,42 contested court
changes and285 rulers. These are reduced continuing countries, not preserved
old imperial maps.

The strongest remaining apparently static case, Fa lacani110, is2980 years
old with no recorded contested court transfer. But it has157 recorded rulers,
was Fo milu's constituent193080–193635, has six distinct separating courts,
25 recorded conquest gains and five losses. Its current capital is Li lofo705,
population358,538. At194200 its final observed integration is.8054 and
bureaucracy.511, public works1000, receipts366,588 against services37,076.
These present conditions help explain damping of disputes but cannot establish
its institutions throughout the preceding2980 years. Calling the field
"last contested court change" is more defensible than asserting one unchanged
regime. Active aggregate capital replacement is sometimes silent, so a complete
capital-move history cannot be recovered from the archive alone.

## Actual rapid preindustrial episodes

These country IDs and dates belong to the fresh-history episode runs above.

| Country | Before | After | Actual mechanism |
| --- | --- | --- | --- |
| Langola133 | 185375:130,842km²,8 cities,5.74M | 185400:32,490km²,2 cities,1.39M | Contested succession185375, multiple supported successor courts, provincial realignment and successor wars;75.2% own-area loss in25 years |
| Ganamu10 | 185425:277,625km²,5 cities,17.96M | Ended185444 | Succession crisis185429; Jasizaza secedes185437, retains another province185438, captures remaining centers185444 |
| Chot locho137 | 185575:249,555km²,8 cities,12.92M | 185600:81,562km²,2 cities,4.23M;185650:16,258km²,1 city | Crisis185578 after Tetko; courts188/189 separate185578–79; later protection compact and conquest by a successor;67.3% own-area loss in25 years |

Chot locho survives as a much smaller **subject**, not the original independent
empire. Ganamu's last-center loss really ends its actor. Langola133 ends185521;
it is not merely renamed after a succession. The episode also contains a
different Langola117 and wars between the two. Identical endonyms can obscure
actual separate governments in UI prose; IDs are essential in review evidence.

The Earth Iron branch, conversely, has only two countries and little change
over150 years. In the fresh-E archive the first independent peer is founded
1862 years after the first state. Sparse competing state origins, settlements
and physical contact can produce political stasis without an immortal-state
rule. This is a separate development/diffusion concern, not evidence that all
existing states should expire on a calendar.

## Historical constraints, not target lifetimes

The [Met's curatorial synthesis of Hellenistic political and cultural history](https://www.metmuseum.org/essays/art-of-the-hellenistic-age-and-the-hellenistic-tradition)
distinguishes Alexander's short conquest from successor kingdoms and continuing
Hellenistic cultural exchange. Its [succession account](https://www.metmuseum.org/perspectives/mediterranean-game-of-thrones)
describes rival generals, disputed royal authority and regional power bases.
The useful model constraint is competing supplied authorities inheriting real
resources, not synchronous disappearance of cities or cultures at a founder's
death. Formal claims to unity need not equal effective unified government.

[Michael Hope's original study of Ariq Böke's appanage](https://doi.org/10.1017/S0041977X24000612)
shows regional power growing from allocated households, pastures and revenues.
During1260–64, interrupted supply and Alghu's locally based revenue/military
authority undermined the central claimant. Famine, desertion and changing
alliances mattered; defeat did not erase every surviving family resource or
regional institution. This is not one uniform Mongol breakup date or a claim
that all pastoral states follow the same path. It supports saved local fiscal
and military allegiance, credible alternative centers and sustained supply
constraints rather than ethnicity-based revolt or an age-based timer.

## Active causal gaps and a bounded proposal

The existing model already resolves real city-led separations, accounts for
conquest debt, inclusion, policy, ordinary succession, distance and modern
administrative capacity, and permits successor war/reconquest. Preserve these
demonstrated capabilities. The audit does **not** justify blanket expiration.

Four narrower gaps warrant a separately versioned design review:

1. **Personal conquest is under-observed.** Revised town capture adds conquest
   debt but does not credit the conqueror's victories/renown. Those counters
   increment only when both field armies are at least20. Rapid unopposed sieges
   can therefore create a large conquest without equally recording the actual
   leader's personal coalition. Record a capture's actual leader and existing
   acquired resources, without inventing soldiers or treating every empty cell
   as a triumph. Compare supported personal conquests with delegated enduring
   institutions at the next real succession.
2. **A crisis is mainly a central clock.** The8–25-year contest can produce
   permanent successor states, but there is no saved local commander/claimant
   loyalty or revenue withholding that persists while a regional dispute is
   unresolved. Use a small record on existing town/office/constituent actors:
   claimant, acknowledged center, withheld actual dues, locally available
   soldiers, last settlement/contact and a bounded claim lifetime. Advance it
   only on that actor's existing calendar/contact. Reconcile real army and
   treasury stocks; neither declare a whole region's culture disloyal nor scan
   the empire to allocate factions. A claimant can bargain, lose its resource
   base, rejoin, become a subject or establish an actual successor court.
3. **Eligibility and recovery are too categorical.** Any positive town decline
   exits community politics before a crisis check; a new court also needs an
   eligible supported noncapital town aged50. Genuine insufficient resources
   should constrain autonomous government, but distress alone should not erase
   all local factional agency. A funded camp/office with actual soldiers and
   supplies may be a viable center without inventing a mature city. Conversely,
   a capital should not automatically recover control merely because the
   central contest deadline passed. Resolve the saved local resource/loyalty
   relationship. This is a proposal, not implemented behavior.
4. **Court relocation has no demonstrated continuity cost.** Losing the capital
   immediately assigns the first remaining city in the local bag. This is
   deterministic, but it neither selects a physically connected loyal refuge
   nor records whether court personnel, stores or command survive. Fa haji's
   founding center410 differs from current capital430; Fa lacani's538 differs
   from705. Those differences alone do not prove an implausible relocation,
   but the active shortcut permits it. A future bounded refuge choice should
   inspect a few existing supported/connected centers and actual resources,
   allowing credible relocated government, negotiated regional succession or
   genuine defeat. Do not simply kill every state when its capital falls.

A narrower first implementation could persist only a named local claimant and
support on existing eligible cities, letting that support outlast the central
crisis and feed the existing separation path. Actual separation would still
use the already conserved treasury/army split, not newly minted factional
forces. Bargaining or restored reliable service would reduce support, and a
real separating court would inherit its claimant's identity. Independent
mobile camps, new local military ledgers and court-relocation changes should
not be smuggled into that smaller revision.

Presentation can immediately distinguish age of country, current sovereignty,
ordinary ruler term and last contested court change. A future rule should be
tested with a rapidly conquered poorly integrated realm, a settled fiscal
kingdom, an externally defeated bureaucracy and a reduced surviving country.
Success is causal divergence and conservation, not a required empire-death
quota, universal dynasty duration or a guarantee that every realm fragments.
