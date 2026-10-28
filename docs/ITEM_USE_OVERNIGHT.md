# Portable items and wildlife checkpoint ? 28 October 2026

The existing shortcut selects `player-item-functionality-complete`, release
IL2CPP built **2026-10-28T17:54:13.3472470Z**. That exact package passes **123
combat/item checks and 13 shoreline checks**, including the owner's same-frame
stone-flake drop regression and actual bow, sling and spear-thrower shots.
The first item release is retained as a fallback. Heavy workers have exited.
GitHub synchronization of this final handoff is deferred during the owner's
competitive Minecraft session; the feature-code checkpoint is already remote.

Subsequent small wildlife fixes exist in source only, ahead of this tested build.
They have not been compiled or tested during competitive play; see
[the wildlife follow-up](WILDLIFE_BEHAVIOUR.md#source-follow-up-after-the-tested-checkpoint--not-yet-tested-or-packaged).

Additional source-only corrections serialize hand borrowing when switching
between charged weapons, gathering and either crafting path. Previously, starting
a craft during gathering could leave two actions restoring incompatible hand
snapshots. Tool work now also cancels on survival incapacitation, matching crafting.
Two native regression checks are written for gathering-to-crafting handoff and
restoration of the original spear. These are unrun: the next native target is
127 checks, while the selected release still has its verified 123. Focused Core
target remains 77. No updated package or performance claim is implied.

This update does not resume the scrapped history/building overhaul. Animal
skinning, butchery, processing and meat drops are deliberately deferred.

## Items and equipment

| Item group | Working function |
|---|---|
| Small stones, spears, harpoon | Melee and physical charge/release throws; mass, speed, gravity and drag affect the result. |
| Bow and arrows | Charged shots using carried arrows; recoverable arrows can lodge in bodies. |
| Sling and stones | Charged shots with suitably small carried stones. |
| Spear-thrower and darts | Physical launch of a carried dart. |
| Bones, large stones, solid tools | Improvised strikes and directional blocking where appropriate. |
| Cutting/chopping tools | Contextual crafting, maintenance, accessible tree bark/branches; no full tree felling. |
| Digging stick | Timed root-fibre gathering from suitable unfrozen soil. |
| Hand-line and fibre net | Timed catch of an actual nearby small aquatic animal, landed whole on shore. |
| Packs, pouches, basket, bark container | Dry storage using existing size/weight and packing rules. |
| Belts, sheath, cases, carry frame, quiver | Appropriate worn attachments/storage; bows have a separate slung-back position. |
| Hide, fibre, sinew, feathers, points, shafts, other parts | Crafting components; not every component is a weapon. |

The 63 portable definitions now participate in 83 contextual actions, including
26 maintenance actions. Existing early stone/wood items remain in the same
inventory. Hides, bones and similar processing inputs remain command-supplied.

Crafting/tool work borrows required equipment and stows displaced items into a
suitable attachment, then a worn carrier. A held bag is not worn storage. Items
that cannot be stowed are set down physically above visible ground without a
toss; manual G dropping still uses physics. Completion/cancellation restores
both previous hands when items remain reachable. Q, actual damage and death
interrupt world tool work. Interrupted crafting equipment is saved/restored.

For testing, `/give list` lists accepted names; `/give materials` supplies raw
crafting inputs. Inventory/context actions remain the crafting interface: no
new recipe book. LMB uses the right hand, RMB the independently occupied left.
With one held item, RMB invokes its secondary action. Keep the other hand free
when testing throwing, digging or fishing. Hold/release charges throws/shots.
Controls remain in Character & help, without an unsolicited controls HUD.
`/give pebble` supplies a smaller ordinary rock (about 45 g); the default
700 g `/give small rock` is intentionally too heavy for the sling.

At a 60-real-minute solar day, the current gathering actions last approximately
5 seconds for root fibres, 10 for hand-line fishing and 3.75 for a net. These are
simple timed prototype interactions, affected by the crafting multiplier.

## Combat, blood and death

Melee/projectiles test original posed triangles after bounds filtering; terrain,
rocks and trees obstruct them. Projectile damage is a gameplay health abstraction
using work/impulse, mass, speed and penetrative form, not a clinical injury model.
Blocking depends on direction and stamina. Recoverable projectiles can follow the
hit bone into a corpse; an unsecured stone spear can lose its point.

Animals have mass-scaled health. Human demo NPCs can be hurt, flee and die; player
damage uses the existing Survival/death system. Death starts physical collapse,
not a death animation. Wounded/dead animal identity, coat/options, blood and corpse
pose persist in saves and stream back at the recorded location without reviving.
Explicit spawn clearing removes command-spawn corpse records too. Healthy manual
NPC/animal demo persistence otherwise remains unchanged.

Blood uses a 256-square per-wounded-actor UV mask, leaving original coat assets
unchanged. Ordinary vertebrates, including fish, use red. Invertebrate colours
are broad pigment approximations; classification avoids substring mistakes such
as treating spider-monkeys or wildebeest as insects.

Corpse bodies fit accepted skin influences. Common roots are always retained:
the old short-bone filter could disconnect limb branches on 28 accepted rigs.
Small animals and flying profiles use coarse articulated body/limb/head branches;
fine spine/toe/feather bones follow a retained physical ancestor. Bodies sleep
once quiet, and the local collision floor follows rolling bodies. Original skins,
weights, rest rigs, actions and coats are unchanged. Local folds/intersections and
imperfect ground contact can remain; these are not anatomical final ragdolls.
Structural neck chains stay articulated. Limb-bearing torso joints are treated
as body supports, and solver masses/joint separation are conditioned to avoid
extreme ratios between a large body and tiny terminal capsules.

## Art and sound

Original procedural item meshes now have shared generated material surfaces,
revised silhouettes, actual-model inventory thumbnails and twelve broad white
pickup icon categories. The item shader shares the world lighting/canopy helpers
with simple rough nonmetallic albedo materials. This remains a prototype art pass,
not a claim of Forest-level visual parity or owner acceptance. Sources and image
briefs are in [PORTABLE_ITEM_ART.md](PORTABLE_ITEM_ART.md).

Eighteen short mono Foley clips were generated locally with the existing Stable
Audio 3 installation: three variants each for impacts, swishes, bow release and
gathering categories. Existing stone contacts are reused. Audio settings provide
volume and previews. No model/server is shipped or run during gameplay. Prompts,
seeds, crops, hashes and licensing are recorded in
[the audio provenance](../assets/item-audio/README.md). Automated diagnostics were
used; the agent could not listen, so owner listening approval remains pending.

## Wildlife behaviour

Four tiers separate nearby fleeting creatures (~35 m), local small animals
(~220 m), residents (~900?2,000 m) and notable large animals/pack hunters
(~2,200?2,400 m). Visual range is separate. Saved regional groups decide roughly
once per second through a sliced work queue; distant groups do not load render
or collision skins until visually relevant. Far animation poses update less often
while world movement remains smooth. No arbitrary population cap was added.

Visible reactions consider movement, distance, canopy, wind, rain and line of
sight. Deer flee, large defensive herbivores can stand/charge, and predators pursue
suitable prey. Groups have basic hunger/thirst, rest/forage/travel/hunt/drink/flee
and shelter intents, plus adult grouping/rut, seasonal migration and temperate
bear denning priors. Suitable birds can seek actual branch/ground perches and
migrating groups use V goals. Full details and natural-history references are in
[WILDLIFE_BEHAVIOUR.md](WILDLIFE_BEHAVIOUR.md).

This is not world-wide continuous individual ecology, reproduction, complete
migration routes, nest/den simulation or a full navigation system. Groups outside
the tracked region stop ticking; no unlimited offline catch-up is attempted.

## Validation and preservation

| Evidence | Result |
|---|---|
| Core item use | 63 checks pass. |
| Portable crafting | 747 checks across 63 definitions / 83 contextual actions. |
| Existing inventory, stone, wood, camera suites | 75 / 37 / 37 / 22 checks pass. |
| First selected IL2CPP item release | 102 native checks pass. |
| Intermediate IL2CPP polished candidate | 107 native + 13 actual shoreline checks pass. |
| Latest Mono ranged-check candidate | 123 native checks pass: all 63 model thumbnails, actual knapping/drop regression, bow/sling/thrower hits, connected corpses, skin-extension guards, saves, predation and player death. |
| Selected release IL2CPP | 123 combat/item +13 actual shoreline checks pass, no runtime errors/exceptions. |

Inspected captures include the squirrel and goose, with the latter retaining its
structural neck joints. The 15%-of-length extension guard and deer 99th-percentile
stretch guard were retained. Earlier angle/mass-only trials
and failing captures remain recorded, not counted as passes. Physics varies with
pose/terrain; this does not certify every animal or slope. Ordinary batch
ScreenCapture images were black and rejected; actual render-texture captures
were inspected instead.

The selected package passes 578 protected asset/assembly hashes, all 177 derived
collision-skin package hashes/source identities and 18 selected audio source hashes.
Its 146 runtime/shader source inputs match the files frozen before packaging.
All 177 accepted animal bundles/native sources,
42 human PNGs and protected MPFB native remain preserved. Derived CPU collision
skins total 26.4 MiB and stream per species; physics metadata covers 5,807 bones.
No measured dense-forest FPS improvement is claimed.

Evidence is under `artifacts/overnight-items-20261028`. Source checkpoints are
pushed to GitHub. Approved cleanup removed 19 superseded generated packages,
about 14.34 GiB, retaining daily/latest-three/selected/running protection, release
fallbacks, source, saves, native art and review evidence.

Read-only monitoring of the owner's live playtest caught a stone-flake creation
exception: an empty MeshFilter awaiting deferred deletion was visited by a
same-frame physical drop. Source now constructs only the actual worked surface
and ignores optional mesh-less filters during bounds collection. Five new native
checks complete real knapping and immediately drop the fresh flake/worked core;
all five pass in both the diagnostic run and the selected release package.

## Deliberately unfinished systems

No skinning, butchery, meat drops, processing, construction, fire, full tree
felling, trapping, clothing, fluid storage, bait/reeling minigame, buoyancy, injury
anatomy or bleeding-out. No new crafting/attack animation pass. Hide working is
an abstract crafting sequence, not a complete tanning simulation. Creative still
uses physical ammunition/materials and wear; `/give` supplies test resources.

## Physical references

- [Experimental hand-thrown spear performance](https://pmc.ncbi.nlm.nih.gov/articles/PMC6347593/).
- [Stone-tipped spear experiment](https://pmc.ncbi.nlm.nih.gov/articles/PMC4146534/).
- [Hadza archery mechanics](https://asu.elsevierpure.com/en/publications/mechanics-of-archery-among-hadza-hunter-gatherers/).
- [Striking tool energy and mass](https://pubmed.ncbi.nlm.nih.gov/26449359/).
- [Insect hemolymph](https://genent.cals.ncsu.edu/bug-bytes/circulatory-system/).
- [Antarctic fish blood exception](https://www.bas.ac.uk/news/fish-survival-in-the-extreme-cold/).
- [Unity joint inverse-mass conditioning](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/joint/connectedmassscale).
