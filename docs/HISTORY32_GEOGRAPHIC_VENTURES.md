# History 32: state geography and dated sea ventures

This is the nullable `Connections.Geography` version-1 review extension. It does
not change the heightmap, climate, crop suitability, or old saves on load.
Fresh history-32 simulations enable it. `UpgradeGeographicKnowledge` is explicit;
history-31 and predecessor null-geography checkpoints retain their old rules.

## Actual defects addressed

Previously a culture-wide working chart did not distinguish first-hand surveys
from copied directions. All its states could use it. An expedition then faced a
roughly 12-year departure lottery, another charter lottery (roughly 40+ years),
and another roughly 40-year port-founding lottery. A successful community contact
could instead transfer abstract national remote residents into an overseas
region, leaving an apparently independent primitive community. These mechanisms
could produce both implausibly early distant settlement and slow later colonies.

## Persistent knowledge, not omniscience

Each state keeps immutable sparse, indexed regional reports: hearsay (1), an
observed coast or copied directions (2), and first-hand verified navigation (3).
Reports retain their observation year, reported host, a coarse observed
opportunity rating, and a particular reported shore. These are dated accounts,
not live foreign army/resource/population queries. Lower-quality or older reports
cannot overwrite better recent information. A grade-1 report guides a search,
not a targeted colonial convoy. A route is still physically checked.

Actual trading relationships can forward at most two reports per state review,
with travel delay and reduced precision. Copied directions never become the
recipient's own survey. Political successors/local dependent courts can inherit
working maps but not duplicate their parent's ships, costs or voyage counts.
Explicit upgrade seeds only the capital and bounded actual owned town/post
records, not the cultural chart archive or the entire coastline.

Potential sea-route geometry is prepared once by existing generation/setup. It
provides bounded search directions to the engine, not pre-departure knowledge of
ideal undiscovered farms or weak foreign armies. A paid returning survey may
reconnoiter a bounded local landing neighborhood and then record its findings.
The approximation does not yet represent arbitrary drawn coastlines, inaccurate
historic cartography, individual navigators, or a continuous fog-of-war raster.

## Actual voyages and local constraints

State process 23 starts funded activity; optional process 27 wakes at actual
arrival/report dates. A state has at most four sailing missions and four reports
in transit (eight entries total). Preparation plus travel normally takes months,
with seeded uncertainty, deployed navigation/engines, and the actual distance.
Survey knowledge arrives on return. Successful discoveries can generate repeated
ventures within years; a second independent decades-long founding decision is
not applied at each paid landfall. Current funding, food and access are rechecked.

Fleets require a functioning court, fiscal surplus, real supported departure
nodes, deployed navigation, and food charged to an actual supplier. High
exhaustion restricts activity. Early administrative ventures are limited to
short regional crossings until ocean-going capability is deployed. Independent
human voyaging remains; early very short coastal community movements remain.
Long state-sponsored settlement no longer appears as an unrelated primitive
population transfer after an unreliable contact.

Survey and convoy fitting-out/crew rations are charged at dispatch. Actual
settlers, fields, crop practice, startup stock and further voyage food are still
the existing separately conserved port-foundation transaction. A flag is not a
population. Offices can receive organized follow-ups. A failed venture cools down
before reconsideration; remembered geography is not forgotten to create new work.
Exploration retains a share of the bounded search portfolio so known offices do
not permanently outscore all unknown seas. Recent reports and actual regional
ambitions affect priorities. Unknown target resources are not scored.

An inhabited independent port can negotiate or resist a protectorate. A funded
coercive venture retains ambition, exhaustion, real investment, force advantage
and local resistance gates; it does not repeat a dispatch lottery after arrival.
An actual foreign state still requires the existing supplied campaign machinery,
not instant map annexation. Recognized claims, direct service/control, native
culture, local councils and populated colonies remain distinct.

## Historical comparisons and limits

- [National Museum of Australia: convict cargo](https://www.nma.gov.au/defining-moments/resources/convict-cargo): the First Fleet is an organized, supplied
  venture, not unrelated foragers reinventing metropolitan institutions.
- [NPS: St. Augustine's communities](https://home.nps.gov/casa/learn/historyculture/african-americans-in-st-augustine-1565-1821.htm): an initial expedition can bring a
  substantial finite cohort; European observation and permanent settlement need
  not be simultaneous. Indigenous land is not empty because no state actor exists.
- [The Met: Afro-Portuguese ivories](https://www.metmuseum.org/essays/afro-portuguese-ivories): profitable coastwise exchange and African political control
  preceded extensive territorial colonial rule. Contact does not imply conquest.

The numeric crew costs, timings, portfolio sizes and probabilities are game
priors, not fitted historical estimates. Neither an Earth date nor a continental
or ethnic mask authorizes expansion. Comparative maps remain diagnostic, not a
claim that all historical development/technology pacing is now realistic.

## Review harness

`--geography-only` checks report grades, durable/copy-safe storage, timing,
capability/fiscal gates and null-policy canonical compatibility.
`--geography-world <null-policy32.world>` checks real maps, serialization,
20 versus 7+13-year determinism, active surveys and bounded outstanding events.
`--geography-compare <world> <new-folder> <years>` runs matched null/candidate
controls, recording actual ports/residents, offices, voyages and diagnostic
rejection reasons. `--geography-eras <world> <new-folder> <safety-years>` continues
accepted current-rule terrain/history through actual maximum technology +1000,
with selected early-modern/modern/final state and technology maps.

Local reports/screenshots are generated artifacts and are not committed. Keep
only the final selected review worlds/maps; discard superseded trial binaries.
