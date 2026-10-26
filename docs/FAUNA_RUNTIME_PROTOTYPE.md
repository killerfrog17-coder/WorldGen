# Visible fauna prototype

The owner's selected set of 177 animal placeholders is integrated into local
exploration. Existing source Blender files, artwork, rigs and animations remain
read-only. This pass adds runtime conversion, regional placement and simple
wandering; it does not revise the accepted art or motion.

## Spawn commands

Press Enter or `/` to open chat, then use:

- `/spawn deer` - spawn one deer nearby.
- `/spawn red fox 5` - names with spaces and catalogue aliases work too.
- `/spawn list` or `/spawn list 2` - browse the 177 animal names in pages.
- `/spawn list freshwater` - search names and aliases.
- `/spawn clear` - remove command-spawned animals and cancel their pending spawns.

Tab completes animal names. Counts are 1–32 per command, with at most 64 active
or queued command spawns and 24 distinct loaded animal types. Commands bypass
regional presence and time-of-day restrictions for testing; ground animals still
require dry ground ahead, and swimmers require sufficiently deep water. Flying
animals appear above the nearby surface, close to camera height. Unsuitable
spots are skipped with a count in chat.

Models load asynchronously with command requests taking priority over regional
wildlife. Chat confirms both the queued request and completed spawn, or reports
an asset failure. These animals keep wandering through regional refreshes and
despawn after leaving their normal local range; they are not saved. Clearing
them retains regional wildlife. Manual spawns share the existing 128-actor budget
and may displace distant regional actors/models to make room.

## Behaviour and placement

Animals choose a new heading/activity every 2–8 seconds. Heading and speed change
gradually, and movement clips crossfade over 0.55 seconds. The allowed roles are
idle, walk, trot, run, fly, glide and swim. Eating, sleeping, death, attack and
other demonstration actions are excluded. Flying animals stay above nearby
ground; aquatic animals remain below their local water surface. Ground animals
avoid large height steps and obvious water crossings. There is no pathfinding,
obstacle collision, pursuit, fear, hunger, breeding or persistent entity state.

The planner selects from **existing ecoregion species-presence records**, matching
176 asset profiles to the native fauna pack. Fireflies use their existing
derived-lineage representation in warm, moist regions at night. Spawn candidates
have stable planet addresses; turning the camera does not reroll them. Nearby
ungulates, tiny insects and small fish can appear in groups. Birds and bats
with nocturnal profiles are suppressed during daylight.

Numbers use practical density priors, adjusted by regional productivity and,
for terrestrial mammals, body mass and broad diet. These are plausible starting
values rather than measured species populations. The mammal mass exponent is
informed by [Damuth's population-density relationship](https://www.nature.com/articles/290699a0);
predators receive substantially lower densities, consistent with the broad
[carnivore/herbivore difference](https://pubmed.ncbi.nlm.nih.gov/28310539/).
**Body size and density have no world-compression multiplier.** Displayed metres
are used for the placement lattice, movement speeds and visibility radii.

Local representation is capped at 128 animals: 48 ground, 24 flying, 32 aquatic
and 24 tiny flying insects. At most 24 distinct models remain loaded at once.
Radii range from 22 m for tiny aerial insects to 650 m for larger ground animals.
These caps bound prototype cost; they do not alter the saved ecosystem's census.
Coats are chosen deterministically by region/taxon with conservative cold/dry
filters; optional racks are retained, and deer have a 50% antler chance. The first
approved body morph is fixed for this pass, so review-only ear/tail/body shape
sliders are not runtime variation.

## Rendering and assets

`scripts/prepare-fauna-runtime.py` pins the selected native hashes and authors the
catalogue. `scripts/run-fauna-runtime-export.py` runs bounded CPU Blender workers.
`FaunaRuntimeBuilder.Build` creates one independently streamed AssetBundle per
animal. `scripts/package-fauna-runtime.py` verifies hashes, skinning receipts,
catalogue/bundle coverage and stages attribution alongside the bundles.

Native global deformation matrices are sampled at 30 Hz, with planar root travel
removed. Runtime interpolates and crossfades those palettes; a shared GPU buffer
skins the mesh. This preserves the selected rig, including its inherited
scale/shear, instead of attempting another retarget. Up to eight original
influences are retained where repaired facial surfaces need them. The gull's
native driven wing-fold morph is exported separately and applied before skinning.
Original placeholder motion defects remain; this is not an animation repair pass.

Meshes retain the selected surfaces with seam splits/atlas UV remapping. Body and
optional parts share one material/draw per animal. Existing pigment graphs are
baked without lighting into BC7 coat arrays (512 or 1024 square), with separate
512-square roughness/opacity and normal atlases. The CPU fallback reproduces the
source pigment image/mix/attribute graph and avoids Blender's failing multi-UV
Cycles bakes. Normal transfer is approximate: procedural micro-bump and detailed
source/destination tangent-basis correction are not reproduced. Near animal
shadows use the scene's regular shadow maps; tiny insects do not cast them.

Models stream asynchronously as animals become locally relevant. The renderer
uses the existing scene lighting, fog, floating origin and terrain-grounding
queries. Placement runs in the shared bounded background-worker budget. The
diagnostic command-line flag `--worldgen-no-animals` disables this prototype.
F3 reports active animals, model cache, mapped taxa and asset failures.

## Validation scope

The conversion checks native/output hashes, allowed clip roles, bone/part counts,
weights and finite matrices. For every clip it compares sampled exported surface
positions against Blender's evaluated native at the beginning, middle and end,
with normalized error below 0.0001. This catches import deformation errors; it
does not certify natural movement, all-frame contacts or original animation
quality. Bundle building checks the complete ecology mapping and binary formats.

Game validation remains compile/package-only under the owner's existing
restriction. No player/world launch, gameplay capture, fixture suite or profiling
is performed. Runtime visual correctness, density, movement transitions and FPS
still require an owner playtest. No save schema, world generation or ecology
presence record is changed by the prototype.

The fauna spawn-command checkpoint is `artifacts/player-fauna-spawn-command`, BuiltUtc
`2026-10-26T19:08:13.7652438Z`. It contains all 177 bundles, 541 movement clips
and 830 coat layers (376.0 MiB of bundles). The packaged hashes and sampled native
skinning checks pass; the largest sampled normalized position error is
`4.89230791495413e-7`. Detailed build evidence is in [VALIDATION](VALIDATION.md).
