# Shared ground surface state / interface only / 21 October 2026

`WorldGen.Core/GroundSurfaceState.cs` reserves a single future query boundary for
ground materials and grass/herb, shrub and tree consumers. It is NOT wired into
generation, placement, rendering, weather updates, serialization or worker messages.
There is deliberately no default provider pretending the world is dry. Existing
worlds, ecology, vegetation, wind and all current visual behaviour remain unchanged.

## Contract

- `GroundSurfaceQuery`: geographic unit vector, actual ground elevation in physical
  metres and absolute weather-calendar days. Never render-chunk coordinates,
  scaled Unity metres, camera time or real-world wall time.
- `GroundSurfaceState`: independent 0..1 soil moisture, surface wetness and snow
  cover; snow depth in metres and snow water equivalent in millimetres. Nullable
  measurements distinguish unavailable information from a known zero.
- `SurfaceWaterState`: unknown/dry or an actual ocean/lake/river/puddle/floodwater
  sample with a common-datum level and optional provider-scoped body identity.
  Depth is measured above queried terrain. A water level alone DOES NOT establish
  connected coverage: providers must resolve footprint/connectivity first.
- `GroundSurfaceRegime`: long-term inundation/waterlogging time fractions, separate
  from instantaneous wetness. Future habitat placement may use these without
  resampling population placement every rainstorm. The averaging interval belongs
  to the provider's documented climate regime, not an implicit current-day count.
- `IGroundSurfaceStateProvider.Sample`: read-only, safe for concurrent workers;
  explicit world inputs/time, stable geography across LOD/reanchor boundaries.
  Future stateful solvers must supply a consistent read snapshot to all consumers.

## Integration order (not implemented in this checkpoint)

1. Shrub/tree generation can reserve habitat tolerances and rendering hooks for
   these inputs; do not alter current habitat rules just to fill an unknown field.
2. Adapt weather retention and terrain/soil information into actual wetness/snow
   state. Existing `WeatherSurfaceEstimate.SoilWetness` is an estimate, and its
   `SnowWaterMm` is NOT snow depth. No guessed snow-density conversion is added.
3. Drive ground shading, snow burial and vegetation response from the same sampled
   fields. Keep immutable base terrain/plant traits separate from transient cover.
   Near geometry and distant materials must agree; no per-blade CPU weather ticks.
4. Puddles need local depression/storage/drainage. Variable lake/river levels and
   flooding need connected hydrology, outlets and storage. Never infer standing
   water simply from rain/runoff, coarse basin ownership or elevation below an
   unrelated water datum. Version any future output-changing generation rules.

No dynamic flooding, seasonal levels, puddles, wet-ground shading, snow deposition,
save schema, multiplayer replication or new vegetation behaviour is implemented.
This checkpoint needs only shared-project compilation, not a new playable build.
