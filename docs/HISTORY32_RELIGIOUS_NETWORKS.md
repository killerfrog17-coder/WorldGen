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
