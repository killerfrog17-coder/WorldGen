# Streaming fog: visible footprints, not LOD readiness /22 October 2026

The preceding fog check only searched a requested key and its resident ancestors.
It ignored existing finer tiles. During movement/adaptive replanning, a newly
requested coarser tile could therefore be called “missing” even though its finer
predecessors were still drawn. Worse, the fog distance was measured to the whole
requested parent: a distant missing corner could be treated as a gap at the
player. This explains a code path for repeated close fog/reveal sweeps; the
owner's exact movement reproduction has not been run by the agent.

TerrainCoverageFootprint builds an indexed union of active-chart resident keys:

- A resident tile or ancestor covers a query, irrespective of segments, pending
  edge stitches, replacement jobs or requested quality.
- Complete sets of four children collapse to complete parent coverage, including
  mixed descendant depths. Negative tile coordinates and scale are part of keys.
- Partial coverage is descended only along occupied branches. Distance is to
  the nearest genuinely missing subrectangle, not the larger parent's boundary.
- Current-chart tile products remain indexed during complementary crossfades.
  Disabled caches, prepared hidden charts and retired hidden roots are excluded.

The index is rebuilt only when resident keys change. Timestamp/material-only
coverage changes compare/reuse it. Requested plans and camera movement query the
same union on the existing150ms cadence; changing a plan is not itself missing
coverage. No terrain samples or mesh/material updates are added by fog queries.

Startup/teleport reset, true-gap inward protection,94% gap margin, smooth outward
reveal, requested view limit, baseline switch and weather fog are unchanged.
Chart handoff still preserves the preceding reveal distance and resets the index
to the newly visible chart. No generation, mesh, LOD selection, retirement,
vegetation, water, SRP material or world/save change is included.

The old ancestor-only HasResidentCoverage remains exclusively in backdrop-job
scheduling; fog no longer calls it. This fix deliberately does not redesign the
loading planner or change its requested coverage/quality.

Verification: source tracing and shared-project compilation only under the
owner's restrictions; no suites, game launches, world loads, captures or profiling.
Runtime walking/flying/replanning and genuinely missing terrain need owner review.

Release IL2CPP build/package exits0, no C#/shader errors, BuiltUtc22Oct05:17:21Z.
Shortcut selects `artifacts/player-lod-fog-footprint/WorldGen.exe`, a separate
real C: build. Owner's running game, saves and preferences are untouched.
