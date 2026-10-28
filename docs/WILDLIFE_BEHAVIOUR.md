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

## Local follow-up draft after the tested checkpoint

During the owner's competitive-play resource restriction, static review found
two corrections worth preparing without rebuilding or changing the tested game:

- Runtime behaviour currently combines the canonical model ID with the display
  name, while the spawn planner uses the canonical ID alone. That can interpret
  `western` as `tern`, or `rattlesnake` as `rat`. The draft aligns runtime lookup
  with the planner; display labels no longer imply a different animal family.
- Contact attacks are admitted using horizontal proximity, without a final
  three-dimensional reach check. The draft rejects vertically unreachable prey
  before applying damage or spending the attack cooldown. Its player check uses
  an approximate body volume; this is not anatomical mouth/limb collision.

The unapplied draft is [wildlife-contact-and-labels.patch](../scripts/drafts/wildlife-contact-and-labels.patch).
It also prepares native regressions for the owl-label and unreachable-prey cases,
and moves the existing positive contact fixture into actual contact range.
`git apply --check --ignore-space-change` passes against current source, including
the workspace's mixed line endings. **The patch has not been applied, compiled or
runtime tested**, and is not included in the selected 123 +13-check release.
Resume with application and the focused/native checks only after competitive
play ends. The tested local checkpoint is tagged `checkpoint/items-20261028`.

One further review item remains: `Food.Fish` is distinct from `Food.Vertebrate`,
but the current predator flag only includes the latter. Before broadening it,
check aquatic pursuit and fish-eating birds' vertical movement; enabling pursuit
alone could leave birds circling targets they cannot reach. Bird perch targets
use readable procedural wood geometry, but actual landing and attachment during
tree sway remain unverified. No new models, skins or animation work is proposed.
