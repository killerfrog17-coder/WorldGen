# Portable item use — 28 October checkpoint

This is the owner-authorized continuation of portable technology. It does not
resume the scrapped historical-simulation/building overhaul. No animal skinning,
butchery, processing or meat drops are implemented.

## Working combat and equipment

Crafting borrows hands, stows into an appropriate attachment/worn carrier, and
otherwise physically drops displaced equipment. Pause/cancel/completion restores
the prior hands when items remain reachable. A bag held in a hand is not a worn
carrier. Bound cases/belt loops have priority. Bows can be slung separately.

LMB uses the right-hand item; RMB uses the left when independently occupied.
With one item, RMB is its secondary: stones/spears throw, most solid improvised
weapons block. Hold/release to throw or shoot. Bows use carried arrows, slings
small stones and spear-throwers darts; ammunition is a physical recoverable item.
Empty hands can strike/guard. Components, hides and carriers are not generic clubs.

Hits test the actual posed triangle surface, including original optional parts,
after a spatial broad phase. Trees/rocks/ground obstruct attacks. Physical thrown
items use mass, current speed, gravity, drag and swept collision. Penetrating
projectiles follow the original hit bone and can be retrieved. Unbound stone
spears can shed their point. Tool/edge wear has contextual timed maintenance.

Animals have mass-scaled health; human demo NPCs use human health. Survivors
react to their actual attacker. Species priors distinguish flight, defensive
charges and predation; see WILDLIFE_BEHAVIOUR.md for the prototype limits.
Death starts capsule/joint ragdoll physics using the original skin palette.
Animal injury, coat, blood and corpse pose retain procedural identity in saves;
streaming a killed identity back in does not revive it. NPC demo persistence is
unchanged. Player combat damage uses the existing survival/death system.

Blood uses a per-wounded-actor 256-square UV mask, not replacement coat textures.
Vertebrates (including ordinary fish) use red. Invertebrate pigments are coarse
group approximations. No injury anatomy, bleeding-out or gore dismemberment.

## Data and performance

All 177 accepted rendered bundles/native rigs/animations remain unchanged.
`scripts/export-fauna-collision.py` derives 26.4 MiB of compressed CPU collision
skins from exact accepted exports. They stream per species only when needed;
the original render bundles deliberately discard CPU mesh data. Skeleton metadata
supports ragdolls without modifying the original rig or motion library.
No arbitrary animal/person count caps are added. Ragdoll bodies sleep once quiet.
This is not a measured dense-forest FPS improvement or full-world performance test.

## Evidence

`artifacts/overnight-items-20261028/combat-runtime-final/checks.txt`: 22 native
checks, actual hits/blood, articulated death, identity save, stone throw, bow
embedding/retrieval, directional guard and player death; no runtime exceptions.
The first screenshot set is useful for hit/arrow inspection but its corpse image
does not frame the body: dedicated visual ragdoll review remains necessary.
`combat-package-proof.json`: 578 accepted source/bundle/human/DLL hash checks.
Core portable crafting: 747 checks across 63 item definitions / 83 contextual
actions (includes maintenance). Core physical-use tests: 59 checks.

Later combined builds add tree-derived bark/branches, root-fibre digging and
simple timed hand-line/net fishing. Fishing lands the actual whole animal as a
corpse; it does not supply processed food. Tools borrow and restore hands.

New shared generated material atlases and revised original item meshes cover
portable items. Inventory uses actual-model thumbnails; ground pickup markers
use twelve broad white categories. Eighteen short locally generated Stable Audio
Foley clips cover impacts, swishes, bow release and gathering. Source prompts,
seeds, crops and hashes are recorded in assets/item-audio/provenance.json.
Owner listening/art acceptance is pending.

The 40-check wildlife-review checkpoint tested deer flight, wolf pursuit and
predation, human NPC death and nine actual model thumbnails in addition to combat.
The combined-review2 run passed 41 checks including all eighteen mono Foley
clips. The save-review run passed 45 checks including direct saved-corpse
readmission, background group movement/index restoration and explicit command
corpse clearing. No native runtime errors/exceptions or active-RT warning.
The shape-review run passed 100 checks, including all 63 actual item thumbnails
and asynchronous regional ground sampling beyond the walking patch. An earlier
all-item run exposed an overly thin bare arrow shaft; its real diameter was
corrected before the passing run. Latest distant-model admission checks are
awaiting the final IL2CPP rerun.
The selected user shortcut is still the preceding portable-technology build until
the full next checkpoint is ready for review.

## Physical/biological references

These inform gameplay priors, not clinical injury predictions or exact lethality.

- [Experimental hand-thrown spear performance](https://pmc.ncbi.nlm.nih.gov/articles/PMC6347593/).
- [Stone-tipped spear experiment](https://pmc.ncbi.nlm.nih.gov/articles/PMC4146534/).
- [Hadza archery mechanics](https://asu.elsevierpure.com/en/publications/mechanics-of-archery-among-hadza-hunter-gatherers/).
- [Striking tool energy and mass](https://pubmed.ncbi.nlm.nih.gov/26449359/).
- [Insect hemolymph](https://genent.cals.ncsu.edu/bug-bytes/circulatory-system/).
- [Antarctic fish blood exception](https://www.bas.ac.uk/news/fish-survival-in-the-extreme-cold/).
- [Moose aggression and safety](https://www.adfg.alaska.gov/index.cfm?adfg=livewith.aggressivemoose).

## Explicit remaining limits

No crafting/combat animation pass, skinning, butchery, meat drops, construction,
full tree felling, clothing, trapping, fluid containers, bait/reeling minigame,
anatomical injury or bleeding-out system. Materials such as hides/bones remain
command-supplied until processing is designed. Hide working is the existing
abstract sequence, not a complete tanning simulation. Creative still uses real
physical ammo/items and wear; /give supplies test resources. Item shaders are
shared rough nonmetallic PBR albedo materials, not a claim of final Forest-quality artwork.
