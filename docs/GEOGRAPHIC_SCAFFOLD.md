# Geographic scaffold1 / local terrain6

## Scope

This is the first drainage-conditioned geographic control-mesh milestone,
not finished terrain art, a fine DEM of Earth, or complete watershed erosion.
Coarse simulation geography/history and saved world hashes remain unchanged.
Completed worlds derive an additional immutable mesh once; local rendering,
grounding and river sampling then read that same field. World generation and
snapshot loading start preparing it in the background, before Explore is used.

## Preparation

At up to250000 source cells, add one deterministically displaced midpoint per
shared mesh edge and split each triangle into four. A200000-cell world becomes
799994 control vertices /1599984 triangles. Both sides share the same vertices;
this is not independent per-cell height/noise generation. Above that limit the
base mesh supplies controls rather than multiplying an already large world.

Source heights, ocean connectivity, major lake levels, broad relief,
lithology/sediment proxies and positive local runoff initialize the controls.
Runoff is reconstructed from saved seasonal discharge and redistributed among
the original and new vertices. Negative coarse residuals are clamped: this is
not a new calibrated evaporation/discharge budget. Saved receiver corridors
apply a small preference, not a requirement to pass through every cell center.

Two bounded priority-flood / implicit stream-power-like incision / hillslope
sweeps condition the surface; a final route defines the drainage DAG. Closed
lake sinks and ocean outlets are retained. This is a rapid procedural solve,
not a geological-time sediment-transport simulation. Shared gradients and
cubic patches interpolate the controls, with divides on non-channel edges.
Final local hills/rock roughness uses small bounded physical-scale noise,
not the former kilometre-amplitude mountain noise. Coastline/standing-water
primitives retain their previous prototype behavior.

River curves use the new mesh's actual receivers/discharge. Water profiles
descend; headwaters taper from zero width. The same point query forms the
channel bed, dry banks and broader valley. Channel-aware mesh refinement and
water clipping still use the drawn terrain. No separate floating water strip
is used as a substitute for a terrain bed. Fine meanders remain a bounded
curve approximation; erosion is not iteratively rerouted at metre resolution.

## Cost and persistence

The extra scaffold arrays occupy83.2MiB at200k cells. This excludes the source
world, regional profiles, channel objects/spatial indices, renderer and temporary
construction buffers. No fine raster or scaffold is written to the world file.
All preparation is one-time or on a scale/world change, never a history tick.
Sampling uses bounded local indexed patches and fixed noise octaves, not a
planet-wide search or an upstream catchment traversal.

Control construction uses a mixed edge-key hash. Default UInt64 upper/lower XOR
hashing causes long collision chains on nearby mesh IDs and was rejected after
timing. A focused diagnostic reports separate mesh/routing/total prep times.
See VALIDATION.md for measurements and the specific native review performed.

## Research informing the architecture

- [Génevaux et al.2013](https://perso.liris.cnrs.fr/egalin/Articles/2013-river-networks.pdf):
  drainage/ridge primitives and terrain constrained by hydrology.
- [Derzapf et al.2011](https://onlinelibrary.wiley.com/doi/10.1111/j.1467-8659.2011.02052.x):
  adaptive planetary terrain refinement with drainage metadata.
- [Cordonnier et al.2016](https://onlinelibrary.wiley.com/doi/10.1111/cgf.12820):
  drainage-based implicit stream-power terrain evolution.
- [Multiscale erosion2024](https://research.adobe.com/publication/terrain-amplification-using-multi-scale-erosion/):
  useful evidence for separating broad and fine scales; its iterative raster
  solver is not run for every loaded chunk here.

Independent hybrid implementation; no paper's complete method or copied
external generator is claimed. Fine tributary hierarchies, true lake-basin
reconstruction, oases/groundwater, braided channels, detailed islands, fjords
and specialist coastal morphology remain future work. The scaffold provides
a shared home for them; it does not implement all of them by implication.
