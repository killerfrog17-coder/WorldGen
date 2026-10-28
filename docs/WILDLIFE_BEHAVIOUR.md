# Wildlife behaviour prototype ? 28 October 2026

This runtime layer uses the existing ecological taxa, native regions and accepted
177 fauna assets. It does not change the simulated world census or add a global
population cap. Species rules are broad natural-history priors, not a complete
individual ecology or a validated population model.

## Tracking and decisions

| Tier | Typical animals | Local tracking radius |
|---|---|---:|
| Fleeting | tiny insects | 35 m |
| Local | small animals below about 2 kg | 220 m |
| Resident | medium animals, large flying groups | 900?2,000 m |
| Notable | large animals and pack hunters | 2,200?2,400 m |

Visibility is separate: tiny animals keep their short visible radius; large
flying animals may remain visible to 600 m, large aquatic animals to 450?1,000 m.
These are quality/perception choices rather than count limits. Original motions
are evaluated every frame nearby, at 20 Hz further out and 10 Hz in the distance;
world movement remains smooth each frame. Distant tracked proposals are removed
from the per-frame visual admission list, and cannot load a model or collision
skin until visible. Regional ground queries outside the walking patch use a
separate background-loaded patch, without changing the terrain.

Resident/notable groups retain home, current position, membership, sex where
relevant, hunger/thirst, goal and intent in saves. A planet-coordinate spatial
index finds nearby groups. The roughly once-a-second group pass is sliced across
frames with a 0.75 ms work budget; queued groups are retained. Outside the tracked
region groups stop ticking. This is explicitly not a world-wide predator sim or
unlimited offline catch-up.

Groups choose rest, forage, travel, hunt, water seeking, flee, seasonal travel or
shelter goals. Herbivore groups loosely follow their centre. Pack hunters have
wider ranging goals and seek appropriate nearby prey groups. Basic adult deer
same-sex grouping changes during the rut. No pregnancies, births, age structure,
carcass feeding, territory ownership or population feedback is simulated yet.

## Visible reactions

Local spatial buckets avoid all-pairs animal scans. Detection considers distance,
movement, canopy cover, wind direction, rain and a terrain/obstacle sight test.
Deer flee. Large defensive herbivores may stand, then charge if crowded/injured.
Predators usually avoid people but pursue edible nearby animals. Hurt animals
react to the attacker direction, not automatically the player. Contact attacks
use actual health, blood and ragdoll death. No authored attack motions are added.
Creative players remain immune; Spectators do not provoke animals.

Cheap local obstacle steering avoids trunks/rocks/corpses, with wet/dry and slope
movement checks. This is not a full navigation mesh or guaranteed pathfinder.

Suitable birds can select actual branch points from generated tree wood geometry,
land and idle, or walk on the ground when their movement bank supports it. Some
migratory species use higher V-shaped travel goals. Hemisphere/season/native cold
conditions affect migratory presence and temperate bear denning. Polar bears are
excluded from blanket winter hibernation. No nesting, complete migration routes,
underground dens or hanging-bat animation system is included.

## Sources informing the priors

- [Oregon DFW: wolves and people](https://www.dfw.state.or.us/Wolves/human_interactions.asp).
- [Washington DFW: gray wolf](https://wdfw.wa.gov/species-habitats/species/canis-lupus).
- [Alaska DFG: aggressive moose](https://www.adfg.alaska.gov/index.cfm?adfg=livewith.aggressivemoose).
- [Yellowstone: wildlife safety, including elk](https://www.nps.gov/yell/planyourvisit/safety.htm).
- [National Park Service: bears in winter](https://www.nps.gov/articles/bears-winter.htm).
- [North Carolina Wildlife: variable bear hibernation](https://www.ncwildlife.gov/wildlife-habitat/species/black-bear/black-bear-hibernation).
- [Washington DFW: deer activity](https://wdfw.wa.gov/species-habitats/living/species-facts/deer).
- [Cornell: sleeping/perching birds](https://www.allaboutbirds.org/news/do-birds-sleep-and-how/).
- [Cornell: small birds during storms](https://www.allaboutbirds.org/news/what-do-small-birds-do-in-a-storm/).
- [Nature: V-formation flight](https://www.nature.com/articles/nature12939).

The numerical thresholds and grouping ranges are gameplay approximations. The
native tests cover nearby deer flight, wolf pursuit/contact death, retained group
movement, restored group indexing and distant terrain-query safety. They do not
certify universal species behaviour, bird landing or measured forest FPS.

## Verified follow-up, 28 October 2026

Edits prepared during competitive play are now compiled, checked and included
in the selected `player-item-followup` IL2CPP build:

- Runtime behaviour now uses the canonical model ID, matching the spawn planner.
  Appending arbitrary display names could read `western` as `tern`, or
  `rattlesnake` as `rat`, changing the apparent animal family.
- Contact attacks now require approximate three-dimensional body reach before
  damage or cooldown. This rejects vertically unreachable prey; it is a body
  bounds check, not anatomical mouth/limb collision. Creative player encounters
  retain approach/cooldown behaviour without applying survival damage.
- Prey selection respects explicit native minimum/maximum masses inclusively,
  matching Biogeography. Body/pack-size estimates apply only to unknown upper
  limits. Herbivores and satisfied predators skip unnecessary edible-prey checks.
- Drinking searches reject ocean water and dry river/lake influence footprints.
  Seven shoreline subdivisions replace the arbitrary goal 15 metres inland,
  selecting a dry bank within about .625 m of a wet sample on an 80 m ray.
  Drinking arrival is within 1 m; the previous threshold was 3 m. Search uses
  at most 16 initial probes plus eight bank probes per decision, rotating the
  search direction between decisions. This does not solve obstacle navigation,
  lake salinity, freezing water or individual herd-member drinking positions.

The initial [patch](../scripts/drafts/wildlife-contact-and-labels.patch) is archived
and superseded by the current source. **Do not apply it again.** Two native
regressions cover label independence and vertically unreachable prey; the old
positive attack fixture is moved into actual contact. Fourteen new Core checks
cover prey limits and water classification. **77 focused item-use and 132 native
checks pass**, including later equipment/ammunition checks. The 13 coastal
digging/fishing checks pass separately; they do not exercise animals navigating
to freshwater banks. That approach and herd-member positioning remain unverified.

The selected release is **132 +13-check** IL2CPP, built
`2026-10-28T19:53:28.2701097Z`. The previous complete release remains available;
`checkpoint/items-20261028` still identifies that earlier verified checkpoint.
Source freeze and asset preservation proofs pass. The owner lifted the resource
restriction and follow-up source `964cbe1` has been pushed to GitHub.

One further review item remains: `Food.Fish` is distinct from `Food.Vertebrate`,
but the current predator flag only includes the latter. Before broadening it,
check aquatic pursuit and fish-eating birds' vertical movement; enabling pursuit
alone could leave birds circling targets they cannot reach. Bird perch targets
use readable procedural wood geometry, but actual landing and attachment during
tree sway remain unverified. No new models, skins or animation work is proposed.
