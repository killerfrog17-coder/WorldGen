# Notable lives and ruling households

History32 optional realm35 adds nullable `HistoryState.Notables`. This is a
shared high-profile-person foundation, not a census or a completed domestic
politics overhaul. Existing saves remain literal until explicit rules upgrade.
Generation, climate, technology costs, observer eras and music are unchanged.

## People, not offices or bonus tokens

A stable person records a name, cultural/linguistic/religious/ethnic context,
first known place and current recorded location, birth estimate or actual birth,
lifespan, interests, six individual capacities, overlapping career roles,
reputation, patron/mentor, selected family links and recent achievements.
Birth/death and office tenure are different. A ruler can leave office and remain
alive. A living child can later occupy the throne under the **same person ID**.
The old ruler/office records remain available and link to the biography.

Names use the existing bounded local language generator. No ancestry walk or
terrain-membership query is used for a name. Capacities are individual procedural
aptitudes, not racial/intelligence rankings or bonuses derived from ethnicity.
Biological appearance, complete genetics and individual ideological beliefs are
not added here. Existing ethnicity is social identity; a tracked child presently
inherits the primary recorded parent's social context, not a genetic estimate.

Explicit upgrade records current living courts' office-holders under their
existing names, with visibly estimated adult births. It does not invent earlier
ancestors, spouses, deeds, named researchers or historical royal births.
Additional family and career records arise prospectively. The new named people
are a subset of existing aggregate populations, not a second population account;
their births/deaths do not separately add/subtract residents already covered by
aggregate demography.

## Ruling households

The existing political profile determines whether a court is dynastic. Such
courts can record a consort and prospective children, a named house, parents,
marriage/household events and potential heirs. At most four children are retained
per tracked parent, with recruitment aimed at two living high-profile descendants;
this is deliberately not every royal relative or an exponentially growing pedigree.
The current two living succession candidates are indexed directly. No universal
male-only inheritance is imposed.

A living recorded heir can succeed, rather than generating an unrelated ruler
with an invented parent. Minor accession can retain an actual living parent as
regent, otherwise a regency council. The ruler's personal aptitudes are not
overwritten with the regent's; the existing state actor can end the minority
and apply the ruler's own capacities. New dynastic reigns normally run to actual
lifespan rather than the preceding generic short-term office timer. Elections
and non-dynastic appointments retain their existing office logic. Ordinary loss
of office or collapse does not automatically kill the person.

This is not a full marriage market, exhaustive succession law, infertility model,
inheritance of private estates or developed pretender/faction system. The next
domestic-politics work can use these real relationships and offices. Lifespan,
birth and mortality rates are authored game priors, not fitted life tables.

## Careers have actual activity behind them

- Researchers enter through actual positive local research effort at a supported
  town. Effort and project are retained. Only a genuinely completed locally
  researched technical level can credit a contribution; imported lessons alone
  do not create a local inventor. Credit identifies a participant in collective
  work, not the sole inventor. Repeated levels are summarized at bounded
  milestones. No extra technology, research budget or deployment is granted.
- A named commander is assigned **before** an actual paid supplied engagement.
  Their command aptitude replaces the previous generic ruler command term in
  the funded field power; it is not a stacked bonus. One officer cannot command
  two simultaneous campaigns. Actual successes build recorded deeds and
  reputation; failed/zero-power expeditions do not mint fighting power.
- Solvent supported courts can pay for works of art, civic philosophy or
  theological commentary. Commission costs debit actual treasury. Existing local
  specialists can continue a career; patrons and living local mentors are named.
  These works currently supply history and biographical provenance, not free
  inventions or a complete philosophy/ideology diffusion model.
- Actual funded local religious schisms can identify a teacher/theologian and
  link the new tradition to them. Inspired-teacher doctrine permits a prophetic
  teaching role; this does not assert real miracles or grant automatic converts.
- Actual political reform credits the responsible recorded office-holder.

Family or office can make someone visible without making them accomplished.
The atlas explicitly says when a person has no recorded achievements. General,
scientist, inventor, artist, philosopher and religious careers use the same
person/relationship/deed representation, not incompatible separate registries.

## Cost, persistence and inspection

Only high-profile actors are represented. Fixed town specialist seats, court
household slots and campaign appointments use persistent sparse indices.
There is one future mortality event per living named person (process28), not
daily person turns or repeated scans of every living/dead person.
Individual mortality dates are deterministically distributed inside their year,
with distinct offsets for distinct person IDs in one mortality year; they do not
pile up at January1. Lifelong offices wait for the actual event, not the year's
first state update. Court work uses
existing state events and bounded5-year household/20–60-year commission reviews.
All participation, relationship and profession checks are bounded actor-local work.

Biographies use immutable copy-on-write128-row pages. A changed biography updates
eight digest words in an immutable Merkle tree. Normal history checkpoint hashing
uses its roots and count, not the lifetime biography collection.
Research turns coalesce repeated project/deed edits into one final biography
write, without changing outcomes; no temporary accounting enters a save/hash.
Explicit upgrade,
public integrity/load validation, serialization and on-demand atlas directories
may enumerate actors, as with existing historical records. Public reads detach
arrays and deeds; copying a publication does not copy every old biography.

Copies, hashes, binary/JSON persistence and event schedules include the extension.
Null is omitted from old canonical bytes. Each biography retains eight recent
deeds with real event/campaign/technical references; earlier named contributions
remain in the ordinary archive. Predicted death dates are not shown to players.

**Atlas → Notable people** offers biographies, family/household links, personal
aptitude charts, patrons/mentors, office links and recorded achievements.
State articles link their recorded household and capital's notable specialists;
older Rulers articles link the separate person. The same shared articles are
used in browser and Unity. Routine personal work does not flood global news.

## Historical guidance and remaining scope

[The Met's courtly-patronage material](https://www.metmuseum.org/-/media/files/learn/for%20educators/publications%20for%20educators/islamic%20teacher%20resource/unit5.pdf)
and [its account of Jean de Berry's manuscript](https://blog.metmuseum.org/artofillumination/about-the-exhibition/)
motivate actual patrons and funded works rather than free cultural points.
[The British Library's discussion of Alfred's will](https://www.bl.uk/stories/blogs/posts/the-last-will-and-testament-of-alfred-the-great)
illustrates that kinship, councils and contested rights interact; our early
household machinery is not a claim of universally automatic primogeniture.
No reference code is copied. Full factional politics, detailed artistic styles,
lifelong movement, contested regencies, exact civil calendars and individual
disease/combat mortality remain future work. This feature does not establish
all-era historical realism or satisfy the outstanding dense-world speed target.

## Review evidence - 18 October 2026

Implementation checkpoint `9326a1f` and exact-output research-write batching
`95801a7` are backed up on origin/main. Four focused notable assertions and826
historical-presentation assertions pass. Actual Earth and generated forest-world
checks each pass22 assertions covering prospective families, real research and
campaign attribution, mortality, immutable input, binary/JSON persistence,
partitioned continuation and cold replay. Old realm34 remains literal.

Natural80-year continuations recorded115 people/14 descendants/53 recent deeds
on Earth and2817 people/265 descendants/774 deeds on the forest world. These are
selected records inside the existing aggregate population, not extra residents.
Earth's full continuation hash is
`884ee73f5933f534ae26ffb00f745b33cd78a19ce75af4cbfed701f1ec506e12`.
Batching preserves that entire continuation exactly; its next12-year replay is
`3527d1cf661fbf1dbd7157ce4750560beb26725756f8eb9e9e8fd31aa7885c94`,
also reproduced by the frozen pre-batching native build. Forest cold12-year
replay remains exactly
`048b5a8d871078c54f9a0a307c1e59691d06fd708a13a3a1cb89aaa99928f4eb`.

The real browser/.NET worker passed biography-directory, achievement, ability
chart and family-link checks without script errors or changing paused time.
Household and campaign-achievement screenshots were opened and inspected.
Release staging, generation reconstruction, identities, languages, sovereignty
and religious-doctrine focused regressions pass. Evidence is under
`D:/WorldGenBuildArtifacts/notables35-review`, excluded from source control.
The final IL2CPP player was built2026-10-18T05:50:49Z; its manifest records staged
assembly hashes. Native Earth12-year replay matches the CLR hash above, and
literal old34 replay remains
`6a10244e023964c1e3d3674c347d5b4bc822ca118a942ea68414c5b3f51e25f3`.
Native map/selection and stripped biography/household article checks pass without
changing the paused historical hash. The selected review player is
`artifacts/player-history-32-notables35/WorldGen.exe`; sovereignty34 is retained
as the previous rollback. Start fresh or explicitly update simulation rules.
These are mechanism/persistence/adapter checks, not a new all-era realism or
200k-cell maximum-technology-plus1000-year performance acceptance.
