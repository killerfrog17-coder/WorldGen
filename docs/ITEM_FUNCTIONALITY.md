# Item use and wildlife interaction

Work in progress for the owner-authorized overnight update, 28 October 2026.
The selected player remains the preceding portable-crafting checkpoint until
this work can be built and checked normally. No skinning, butchery or processing
of animals is authorized in this pass. No in-game recipe book or controls HUD.

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

Automatic equipment passes539 portable checks plus75 inventory and37 stone/37
woodworking checks. Physical launch/block/wildlife persistence passes44 targeted
checks. The normal Mono hands checkpoint builds successfully after the owner
restored Unity Personal activation. Its native batch flow passes38 checks,
including original-hand restoration on pause/completion/cancel, real drop/retrieve,
slung bows, source coat binding and save/load. Batch screenshot files are black;
these establish functional behaviour, not a graphical layout review. The current
virtual-monitor setup intermittently prevents native windowed launches. Editor
render-texture reviews remain available for artwork and shader checks.

A same-frame drop/retrieve/resume exposed Unity's deferred component destruction;
item rigidbodies/colliders are now reused and disabled while carried, rather than
being destroyed and immediately re-added in the same frame.

The physics metadata exporter inspected177 original native files/5807 bones in57
seconds, with all source hashes preserved. Bone parents precede children in all
records. Metadata supplies the original rest frames for palette-driven ragdolls.
Combat foundations are in progress; this checkpoint does not yet expose attacks.
