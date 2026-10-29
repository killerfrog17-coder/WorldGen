# Nearby scatter collision

Player movement now has a local solid layer for instanced scenery. Trees use
trunk capsules; boulders, outcrops and substantial loose stones use copies of
their near-render meshes. Foliage and small ground pebbles do not block walking.

The cache follows the player and the swept movement segment, including Creative
flight. Its 16 m margin is a physics working set, not a visibility setting or a
world population limit. Source projection revisions, floating-origin changes,
collection changes and movement refresh it. Collected rocks lose their solid.
No terrain heights or scatter positions are changed to create collisions.

The existing terrain movement solver still handles terrain grades, gravity and
fall damage. An optional callback resolves each movement substep against the
local solids using Unity's capsule controller, preserving sliding and small
steps. Support queries allow standing on rocks. Spectator bypasses both support
and solid movement resolution. The controller is disabled outside its movement
call, so it does not become an invisible target for weapons or other actors.

Combat queries retain the original scatter identity for physical hits, allowing
tree resource work to continue. Nearby rocks use the same mesh for movement and
combat rather than the previous broad AABB approximation.

Scope: player collision against scenery. This does not replace animal/NPC
navigation or introduce rigid physics for every plant. Trunks have approximate
straight shapes; leaves, twigs and elevated branches are not collision meshes.

Validation entry point: `run-item-use-check.ps1 -SolidScatter`, plus existing
`--local-terrain-only` movement regression checks. Native results belong to the
specific package/evidence directory, not every future source edit.

Selected checkpoint: `player-scatter-collision-ready`, IL2CPP release
2026-10-29T02:22:14.0412436Z. All 14 native collision checks passed without runtime
errors, alongside 745 existing Core terrain-movement checks. No FPS measurement.
