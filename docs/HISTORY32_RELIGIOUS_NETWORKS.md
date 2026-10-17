# Religion: local communities, institutions and court policies

17 October 2026 source experiment; not a declaration of longitudinal acceptance.

`Connections.Religion` is a nullable revision-1 extension, initialized by fresh
realm revision 2 or the explicit historical-rules upgrade. Realm revision 1 and
older saves retain their recorded religion rules until upgraded. Initialization
creates empty sparse working memory; it preserves existing names, affiliations,
religion lineages, events, population and geographical fields.

## Problem found in the active code

The aggregate dispatcher was using `AdvanceAggregateFaith`, not the older dense
`HistoricalReligionNetworks` solver. The aggregate method promoted a religion
from local to portable at two towns and 5,000 urban residents, rolled missionary
status after writing and 20,000 urban residents, randomly changed the religion of
an entire culture from a contact ID, and immediately painted one selected town
and one random neighboring terrain cell. A second independent culture event
also performed an immediate culture-default conversion. Reforms mostly renamed
the parent. The older documentation of gradual cell conversion did not describe
this active path.

Those mechanisms are bypassed only when the new optional extension is present.

## Implemented actor-local contract

Thirteen integers per *observed* cell record the strongest competing tradition,
influence, last review, last public-affiliation change, institutional support,
retained household tradition, an actually delivered visitor tradition, last
contact, ritual familiarity, local dissent, beginning of the contact interval,
last renewal and delivered carrier support. Sparse persistent radix storage fits
the maximum terrain: 2,560,000 x 13 = 33,280,000 entries, below its 33,554,432
limit. Court policies use a separate sparse actor table. Copies share immutable
tree nodes; changes detach their paths. Hash verification rechecks actual nodes.

Ordinary culture work samples its home community, one actual settlement and at
most two existing town contacts. Successful explicit endpoint contact also
revisits just its two local actors, allowing rural adoption without a recurring
regional census. Unspecified endpoint calls and remembered culture IDs alone
deliver nothing. Existing physical institutional-route checks apply to each
town/explicit endpoint delivery. No random neighboring map cell is converted.
One visit supports at most 80 years of exposure; repeated actual delivery can
maintain it. Unsupported influence decays. The 700-pressure predominance test
also requires 150 years of local continuity and 100 years since the tradition's
recorded existence or upgrade. All numbers here are scenario priors.

Public predominance and household continuity are distinct. A change updates one
observed cell and retains the previous tradition as household memory. It never
changes `HistoricalCulture.ReligionId`, so a local conversion cannot recolor an
entire ecoregion or culture. The strongest rival is not a minority census or a
percentage of believers. Ritual familiarity can grow without conversion.

Actual parent-town foundations carry their departure community's local faith,
not an unrelated remote cultural default. The foundation captures the site's
previous public affiliation and existing resident proxy before transferring
households. Existing indigenous public rites persist, with the carried migrant
tradition recorded as household/contact memory. A genuinely new migrant town
can begin with its departure faith. It receives zero inherited institutional
support, no converted hinterland and no duplicated people or provisions.

Supported settlement ritual institutions consume real food reserves above a
retained household provisioning buffer. Their support grows gradually and
decays without provisions. Portable traditions can be oral; writing is not a
mandatory religious stage. A stable seeded disposition allows some provisioned
institutions to choose deliberate outreach. Supported missionary trips consume
source provisions, and the received contact freezes the source support. An
unfunded household encounter cannot borrow a distant institution's missionary
bonus merely because both share a religion ID.

Courts separately favor a tradition, allow plural public worship, or restrict
competing public institutions. Policies respond to the incoming ruler's
disposition, diplomacy and the existing constitutional representation; neither a
calendar date nor a technology mechanically imposes secularism. Court patronage
spends the actual treasury above immediate service costs. Funded officials can
encourage their tradition in the particular administered community under review.
Restrictions slow rival public establishment and reduce the stability of a
locally excluded town when enforcement is funded; an unfunded proclamation has
weak local effect and creates no paid-enforcement dissent. They neither erase household memory nor instantly
convert subjects. A court may later adopt an already durable capital tradition,
paying a separate real cost.

Autonomous renewal needs a supported institution, existing local inhabitants,
sustained dissent from exclusion or contact-driven ritual synthesis, a local
250-year continuity interval, and actual provisions. Its new religious actor
retains the parent, founding community and place, with a name derived from the
local language. A synthesis also records the encountered tradition in its ritual
description. Reform changes the local institution, not its entire culture or
state. All consequences remain within the normal actor/event calendar.

## Historical research and interpretation

- The archaeological park's [Boscoreale Antiquarium guide](https://pompeiisites.org/wp-content/uploads/GUIDA-BOSCO-ENG_STAMPA.pdf)
  reports domestic shrines and their material settings. This supports treating
  household practice as a distinct local observation, rather than inferring it
  from a state's public temples.
- [Frankfurter, *Christianizing Egypt*, original publisher chapter](https://assets.press.princeton.edu/chapters/s11202.pdf)
  analyzes documentary and material evidence for active reinterpretation of
  inherited rites within local Christian settings. This motivates household
  continuity and contact-based synthesis. The model's single retained lineage
  is a deliberately much smaller representation of that diversity.
- [Buddhism and trade: rock-cut monasteries and least-cost paths](https://www.sciencedirect.com/science/article/pii/S2352226721000532)
  is an original archaeological spatial study of routes, monastic sites and
  merchant support. [The Met's Silk Road collection account](https://www.metmuseum.org/exhibitions/listings/2012/buddhism)
  independently describes continued exchange and patronage across changing
  political boundaries. These support provisioned physical networks, not
  writing-triggered universal missionary status.
- [Yelle's original analysis of Ashoka's edicts](https://www.cambridge.org/core/product/identifier/S0026749X21000160/type/journal_article)
  examines toleration and the dangers of projecting modern secular categories
  onto ancient policies. This motivates permitting several policy responses
  before modernity, while keeping favored court affiliation separate from
  household belief.
- [Wood's study of Syriac consecration lists](https://doi.org/10.1017/S0009640725102400)
  investigates patronage networks that continued through changes in political
  authority. This motivates institutional continuity and warns against making
  every ruler change an automatic schism.

The sources inform mechanism choices and counterexamples. They do not provide
the numerical probabilities, establish one universal path, or rank religious
traditions, ancestry or ethnicity. No external simulation code is used.

## Validation and limits

`HistoryReligiousNetworkTests` exercises sparse maximum-detail storage, canonical
copy/serialization, null-extension compatibility, finite contact memory,
supported accumulation and decay, continuity thresholds and real provision
costs. Its actual-world mode checks explicit upgrade, validated save/load,
publication-partition replay and a controlled existing physical town contact.
The latter holds the donor institution supported to isolate conversion and
provision accounting from unrelated demographic processes.

The public `ReligiousCommunity` projection reports local public/household/visitor
identities, institutional support, contact date, pressure, ritual familiarity
and court policy without mutating the simulation. These are model indices,
not measured adherence percentages. Unobserved countryside keeps the existing
fallback affiliation; it is not fabricated into a complete minority map.
The model has no detailed doctrine, full clergy biographies, religious law,
competing organizational hierarchies, reliable believer counts or universal
causal theory of religious change. Dense 200k longitudinal geography, resulting
religious maps and runtime still require independent visual review.

## Realm revision 3 follow-up: supplied rural contact

The frozen realm-2 experiment receives religion through explicit cultural/town
contact and migrant foundations. A later code-path review found that ordinary
successful regional exchange bypassed that explicit contact hook: it delivered
technical offers directly. Realm revision 3 adds a religious receipt after the
existing physical route, supplied voyage and probabilistic transit succeed.
The source religion comes from the actual supplied endpoint, not the potentially
unrelated culture credited with an advanced technical lesson. Realm 2 retains
its preceding continuation; its new helper returns before any write.

On-demand atlas profiles now distinguish local rites, portable teaching and
supported outreach; separate public predominance, retained household tradition
and encountered teaching; and show up to twelve actual town examples. Local
cards expose dated contact, institutional support and court policy. These
indices are not believer counts. This presentation is read-only and can explain
either experimental revision without upgrading it.

## Religious institution revision 2 (post-B source experiment)

`Connections.Religion.Version=2` is an independent explicit rule marker. Fresh
realm revision 4 initializes it; frozen realms 1-3 retain their preceding
religion continuation. The explicit upgrade changes future rules without
rewriting established ethos, ritual, names or actors. Sparse storage is unchanged.

Ordinary sampled city carriers now check live war, both endpoints' disruption,
foreign-community stability, source provisions and actual economic travel
range. A graph neighbor alone is insufficient. Actual migrating households and
already supplied regional deliveries remain distinct receipts; interruption of
ordinary trade does not erase the religion of refugees or emigrants.

A stable seeded vocation distinguishes sacred-place/ancestral institutions,
portable household tradition and traveling teaching. These are choices with
different transmission forms, not ranked stages. Large provisioned sacred-place
institutions may remain local indefinitely. Household portability can follow an
actual migrant foundation; teaching institutions require recent real contact,
funding and local support. Existing recorded outreach is not removed on upgrade.
No fixed number or global share of any form is imposed.

Court religious settlements have institutional inertia. The new ruler can
propose a policy, but continuing capital ritual support and a long-established
settlement damp automatic reversal. Broader political representation, war stress
and low legitimacy can provide concrete pressure for revision. A change of
leader is not itself a new religion or a compulsory ban/toleration cycle.

Fresh profiles describe six combinations of ancestral obligation, sacred-place
care, civic oaths, household commemoration, hospitality and ethical instruction.
They follow vocation and a stable observance root, never biome or ethnicity.
An actual migrant-community lineage copies its immediate stored obligations
and practice; it does not walk ancestry or inherit a ready-made institution.
Exclusion can produce local ritual autonomy; sustained contact can produce a
synthesis; actual elite capture together with local instability can support an
ethical renewal independent of court patronage. Each renewal remains locally
provisioned, dated and parent-linked. These are limited procedural profiles,
not full doctrines, a moral ranking or believer counts.

Additional mechanism comparisons:

- The Met's [Inca/Chincha serpent object research](https://www.metmuseum.org/art/collection/search/316938)
  documents an imperial temple incorporating local forms in a polity retaining
  some autonomy. This supports local/imperial coexistence and synthesis without
  requiring complete public or household replacement.
- The Met's [capac-hucha archaeological account](https://www.metmuseum.org/essays/capac-hucha-as-an-inca-assemblage)
  relates long-distance offerings and sacred places to royal accession, death
  and wider ritual networks. It is a counterexample to equating extensive
  institutions exclusively with portable missionary doctrine.
- [Original Buddhist monastery/trade spatial research](https://www.sciencedirect.com/science/article/pii/S2352226721000532)
  and [Wood's original Syriac patronage study](https://doi.org/10.1017/S0009640725102400)
  support provisioned connections and institutional continuity through changing
  political authority. They do not establish the model's numerical priors.

Independent A diagnostics found 179 local/97 portable/22 missionary religious
actors on Earth200 and 156/78/23 on Continents Plus200. Earth had 2,651 court
religious-policy changes but 386 public-community changes; the generated control
had 3,647 versus 452. These observations motivated institutional inertia and
live-carrier checks, not a target global religion count. Their optional detailed
diagnostic exports now include actor forms, parent traditions and religious
event counts alongside local-town observations.

### C longitudinal observations, 17 October 2026

The matched `candidate-c-integration-bin` reader inspected the two completed
200k-cell worlds without upgrading or resealing them. Earth at simulation year
205,500 had 551 active towns, 309 institutions with support at least 400 and
373 towns retaining household traditions distinct from public predominance.
Its 369 recorded religious actors comprised 262 local, 79 portable and 28
missionary forms; 105 had public predominance in an observed active town.
Continents Plus at year 186,700 had 529 active towns, 282 supported institutions
and 359 distinct household inheritances. Its 212 actors comprised 147 local,
54 portable and 11 missionary forms; 77 had observed public town predominance.
These are town/actor observations, not a census of believers or countryside.

Continents Plus included 89 supported public institutions whose religion still
had local form. All six new ethos profiles occurred. Earth recorded 39 court
religious-policy changes, four schisms and eleven syntheses; Continents Plus
recorded sixteen policy changes and seven syntheses. Neither recorded an
ethical-renewal event. The latter mechanism is therefore a causal possibility,
not a demonstrated common outcome in these samples. Differences from A are
not controlled estimates of religious effects: C also changes political and
maritime rules and produces different settlement and state histories.

Independent opaque-map inspection found contiguous regional clusters and some
coastal/island satellites. Extensive unrecorded countryside remains. A few
Antarctic patches inherit the wider simulation's settlement geography; religion
does not independently establish their people or validate their plausibility.
Colors alone cannot establish a particular route's causal history. Detailed
diagnostics live under `D:/WorldGenBuildArtifacts/overnight-20261017/` in
`religion-rich-c-*` and `religion-opaque-c-*`, outside source control.

The final focused suite passed 35 checks, plus 27 actual-world checks on each
of the saved Earth20 and Continents Plus20 fixtures. These cover explicit
upgrade, full save/load, 20 versus 7+13-year continuation, inherited profiles,
actual endpoint disruption, sustained local adoption, household continuity and
real provision/treasury debits. Current C source also reproduced frozen B's
Earth200 20-year continuation exactly:
`7e2ae002323539cd3c9aa3cc0c99fd647f56261d2554bacfa6f911d37988ec4e`.
This compatibility comparison retains religion revision 1 rather than
silently applying the new institution rules.

## F saved-world visual and carrier audit

Read-only `--religion-evidence-audit WORLD NEWFOLDER` exports actual public
predominance beside **recorded** household traditions. Grey household cells
mean unobserved, not unbelieving or uniform. Geometry/actor enumerations belong
only to this explicit diagnostic, never ordinary simulation events. Run
`node scripts/history-religion-images.mjs NEWFOLDER` to render its SVGs using
the installed Edge browser. Screenshots and saved worlds remain external.

| Saved F observation | Earth final | Continents Plus Medieval episode |
| --- | ---: | ---: |
| Simulation year | 180200 | 168775 |
| Active towns | 2004 | 618 |
| Public traditions observed in towns | 96 | 51 |
| Supported institutions, index >=400 | 405 | 333 |
| Towns retaining a distinct household tradition | 1176 | 254 |
| Recorded household cells | 4075 | 1931 |
| Currently permitted directed town-carrier links | 4440 | 1787 |
| Of those, cross-faith links | 1590 | 525 |

The CP episode is a fresh F history on accepted saved terrain, continued
168675–168775, not an old civilization upgraded at the Medieval milestone.
Its full native 200k run had not yet saved a final world when inspected. Source
hashes were preserved before and after all exports:

- Earth `74a2223de5ac874550624ccfdfb4ab3ff69568563bfd97d1002e2894e0fddc65`.
- CP episode `b8082622da551e4fef8ad1736b95e0f887600ad38c244256d972daa58f8320d9`.

The paired maps show coherent regional cores, unlike public/household local
identities, and dispersed maritime satellites. They do not show wholesale
global public convergence. CP's portable Kungnangngat has 79 towns across five
cultures and 31 courts; 57 towns form its largest presently permitted same-faith
town-contact component. Missionary Halhimepap has 50 towns, 45 in its largest
such component. Sacred-place/local Gunila also has 50 towns, including 20
supported institutions. A widely supported regional tradition therefore need
not become missionary, and provisioned portable traditions can become large.

Earth's most extensive identity, local/sacred-place Remlungsim, predominates
in 458 towns with 21.04M urban residents, 421 towns of its founding culture,
across 34 landmasses (including small islands). Its public layer is explicit
in 1099 of 1101 cells; only two use culture-default fallback. The archive records
90 diaspora events and 14 local-adoption events for it. 436 of its towns have
an actual saved parent city whose **current** public identity is also 46;
that is evidence of a settlement genealogy, not proof of the parent's faith
at departure. Missionary Mone sal reaches 160 towns and 7.74M urban residents
across ten landmasses. These population figures describe residents under a
public predominance, never counted believers. Contemporary disconnected
same-faith graph components cannot disprove historical migration or travel
through communities of other traditions.

A real limitation remains: one ID identifies a transmitted tradition, not a
single centrally governed church. Remlungsim's shared color must not imply
that hundreds of independent ritual houses obey one global priesthood.
No such common religious government is modeled. Later local institutional
affiliations or reforms should arise from recorded patronage, contact and
dispute, not mandatory geographical splits or a global religion quota.
F records 13 Earth/eight CP syntheses, but neither sample records a schism or
ethical-renewal event. Their causal branches are not demonstrated by these
longitudinal samples and should not be described as common observed outcomes.

External paired images: `religion-evidence-f-earth200/comparison.png` and
`religion-evidence-f-cp-medieval/comparison.png`, under
`D:/WorldGenBuildArtifacts/overnight-20261017/`. Richer parent/review-date/event
exports use the corresponding `religion-evidence2-f-*` directories.

## Diagnosed city clock and prospective revision8

The independent audit found a concrete actor-clock defect, not simply poor
modern funding. `AdvanceReligiousNetworks` selected one town per culture event.
A culture with hundreds of towns could leave particular ritual houses
unreviewed for millennia. Same-faith contact returns without reviewing either
community, so coherent same-faith regions cannot rely on rival visitors to
incidentally maintain their institutions.

| Saved review age, years | Earth | CP Medieval |
| --- | ---: | ---: |
| Median | 358 | 441 |
| 90th percentile | 1155 | 1853 |
| Maximum | 5216 | 4229 |
| Towns more than 100 years since review | 1469 | 477 |
| Towns more than 500 years since review | 822 | 294 |
| Currently affordable next-ten-year institutions below 400 support | 397 | 181 |

Current affordability is not a historical payment ledger, so it cannot prove
all those towns were continuously able to fund institutions. The dates and
active call path nevertheless establish that local upkeep was culture-sample
starved. It also delayed or lumped local public-adoption/renewal decisions.

Authorized revision8 places one guarded local review on the existing city
event, using the same saved review date as contact/culture callers and a
minimum ten-year interval. It does not add a queue, per-state/culture census,
automatic visitor, free food, unconditional missionary institution, or global
religion target. Rural actual-contact reviews continue separately. All
existing religious costs and local reception tests remain in force.
Explicit pre-8 upgrade rebases live-town clocks once without retroactive
century invoices; names, households, current support and contacts survive.
Realm7 and earlier retain their exact preceding event behavior.

### Revision8 checks and actual twelve-year maps

`--religion-city-clocks-world WORLD [NEWOUTPUT]` passed 21 actual-world checks
on each of the saved F Earth final and F CP Medieval fixtures. The existing
35 focused religion checks also passed. The actual multi-city controlled
fixtures address 12 already existing towns of the same culture: a nine-year
negative guard, exact real ten-year food debits, no double charge from a second
city/contact/culture call, a second due invoice, no created inhabitants or
treasury, unchanged abandoned towns, and unaffected one-year rural reviews.
Explicit migration preserves identities/support and charges nothing; a second
upgrade is idempotent. Full validated save/load and cold 5+7-year continuation
equal the complete twelve-year run, and every surviving pre-existing town
actually completes a new review. Source hashes remain unchanged.

Actual post-upgrade exports, not fabricated expected maps:

| Twelve-year observation | Earth 180200–180212 | CP 168775–168787 |
| --- | ---: | ---: |
| Surviving original towns | 2000 | 618 |
| All living towns at end | 2020 | 621 |
| Median review age, surviving original towns | 2 | 2 |
| Maximum review age, all living towns | 9 | 8 |
| Original institutions with increased support | 578 | 324 |
| Original institutions with decreased support | 496 | 120 |
| Support >=400, surviving original cohort, before→after | 403→389 | 333→334 |
| Zero support, surviving original cohort, before→after | 1197→827 | 198→62 |

Thus timely review is not an unconditional institution upgrade: actual
unfunded institutions decline. Paired opaque maps retain regional cores,
household/public differences and diaspora satellites. They show no sudden
global convergence over twelve years. This is a saved-world upgraded
continuation, not a fresh long-world calibration or an isolated intervention;
other explicitly selected realm8 rules also apply.

External folders `religion-clock8-earth200-12` and
`religion-clock8-cp-medieval-12` contain `review-ages.json`, `public.svg`,
`household.svg` and rendered `comparison.png`. Endpoint hashes:

- Earth `935795f9849ec20376095088c41b668db6f520965fb628ae7e33062a3510c7f6`.
- CP `5884a115a1ba0e04aa971722295b4d01c5bd1440da05e31c6c78eff1b6e63429`.

Literal old-rule continuations were compared against named frozen native
executables, not merely two runs of current source:

- Realm7 CP twelve years: frozen `candidate-g7-freeze` and current source both
  `4a5a9e69e26819ef5aa248864384a495294eaca9494541309e6b0d0691270673`.
  Input `religion-clock-old7-cp-medieval.world` was created by that frozen G7
  executable's explicit upgrade of the F CP episode. Named outputs are
  `religion-clock-frozen-old7-cp12.hash` and
  `religion-clock-current-old7-cp12.hash`.
- Realm5 Earth twenty years: frozen `candidate-e-final-bin` and current source
  both `5d9abdbe27ba63a32b0f9e24f71177ac35c0e73081c89de16d77594b39ba2882`.
  Input `candidate-e-earth200/final.world`; named outputs are
  `religion-clock-frozen-old5-earth20.hash` and
  `religion-clock-current-old5-earth20.hash`.

All named artifacts are under
`D:/WorldGenBuildArtifacts/overnight-20261017/`, outside source control.
The claimant archive's displaced-before-own-successor wording remains queued
for a separately scoped transition-context fix; this clock change does not
silently alter old political narratives or physics.
