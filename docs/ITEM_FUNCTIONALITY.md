# Item use and wildlife interaction

Owner-authorized overnight update, 28 October 2026. The selected player and exact
validation status are recorded in [ITEM_USE_OVERNIGHT.md](ITEM_USE_OVERNIGHT.md).
No skinning, butchery or processing of animals is included. No in-game recipe
book or controls HUD is added.

## Delivery order

1. Automatic hand staging, temporary tool use and return of the previous hands.
   Respect worn tool cases, belts and slung bows; worn storage only, then a real
   drop. Restore on completion, cancellation and interruption. Save the state.
2. Common item-use capabilities, melee, mass/velocity projectiles, blocking,
   animal/NPC health and death. Persistent killed identities prevent immediate
   respawns. Blood marks are the specifically requested visual exception while
   models and animation remain unchanged.
3. Functional arrows/bows, darts/throwers and slings; recoverable physical ammo,
   fishing line/net interactions, tool actions and maintenance. Audit every item:
   raw components need a meaningful crafting role, not an invented combat action.
4. Once functions work, overhaul item meshes/materials and inventory renders in
   priority order. White category pickup icons; no unique icon for every hide.
   Original owned procedural art/free distributable inputs only; no paid calls.
5. If time remains, staged wildlife simulation: ambient/local small creatures,
   ordinary visible animals, retained large individuals/social groups, and cheap
   distant predator/herd decisions. Species-specific danger responses first,
   then groups, daily/seasonal activity, migration/hibernation and perching.

This is a functional sequence, not a promise that any one step is complete just
because architecture exists. Keep explicit evidence and remaining limitations.

## Input interpretation

The owner's duplicated "right hand" is interpreted as left click for the right
hand and right click for the left. With one held item or a two-hand item, the
second button uses that item's secondary action. Small stones and spears can be
thrown; bones and bulky improvised objects block. Bow/sling/thrower input must be
documented in Character & help, without adding gameplay instruction text.

## Physical and ecological assumptions

Projectile energy is `0.5 * mass * speed^2`; drag uses frontal area and mass.
Launch speeds are bounded by available work/impulse, not inversely unbounded for
tiny objects. Health damage is gameplay balance based on energy and penetrative
form; it is not a clinical injury prediction. Later locational injuries remain
separate. Blocking has facing and stamina costs rather than granting immunity.

Most vertebrate blood, including fish, is red; invertebrates need appropriate
hemolymph colour/amount rather than treating every butterfly as a red mammal.
Fauna native meshes, skins, materials and existing clips remain unchanged during
functional work. Supplementary bone rest metadata is read from the original
native files with exact before/after hashes and used to articulate corpses.

References consulted for implementation decisions:

- Unity continuous collision detection and its collider/performance limits:
  <https://docs.unity.com/en-us/engine/6000.7/script-reference/unityengine/collisiondetectionmode>.
- Alaska Department of Fish and Game, defensive moose behaviour and season:
  <https://www.adfg.alaska.gov/index.cfm?adfg=livewith.aggressivemoose>.
- Minnesota DNR, solitary moose and rut/calf defence:
  <https://www.dnr.state.mn.us/mammals/moose.html>.
- Michigan DNR, moose reproduction and predators:
  <https://www.michigan.gov/dnr/education/michigan-species/mammals/moose/moose-biology-and-habitat>.

## Current validation

The combined item-use rules pass 63 checks; portable crafting passes 747 checks
across 63 item definitions and 83 contextual actions. Separate inventory,
stone-working, woodworking and camera suites cover the existing flow.

Licensed Unity builds now succeed after the owner restored activation. Earlier
native checkpoints passed 38 automatic-equipment checks, then 22 combat checks,
then 40 combined combat/tool/art/wildlife checks. Real render-texture captures
were inspected; ordinary batch ScreenCapture files were black and were rejected.
A later release IL2CPP run passes 107 checks, including all 63 actual item renders,
saved corpse/group/index behaviour, background ground queries and distant model
admission. A separate 13-check shoreline run exercises actual timed digging,
hand-line and net catches and restoration of both original hands. Ragdoll visual
review found additional small-animal stretching despite finite-matrix checks;
further corrections and final selection are recorded in ITEM_USE_OVERNIGHT.md.

177 original native fauna files, their render bundles, rigs and animation banks
are preserved. Supplementary 26.4 MiB collision skins provide actual posed surface
hits without making the original GPU meshes readable. Rest metadata covers 5,807
bones. This is not a guarantee of exact biological injury or anatomical ragdolls.
