# River primitives: first playable review

OWNER-REJECTED GEOMETRY. The owner found extensive water walls and flooding in
this Hyper8 checkpoint. Its edge checks did not establish realistic rivers.
Hyper9 replaces its river surface generation; see RIVERSCAPES.md. The water
shader and channel-refinement stitching remain useful, not the unlimited river
potential or cross-reach meshing behavior described below.

Derived local terrain8 builds on the owner-approved lake7/ocean checkpoints.
This is a first integration of terrain/water primitives, NOT a complete port of
Peytavie et al.'s Procedural Riverscapes or a finished hydraulic simulation.

## Implemented

- Amplified channels now supply near mesh refinement. Previously ChannelPieces
  returned nothing for amplified worlds: the narrow-stream refiner was wired
  only to the obsolete local hydrology spline representation.
- Shared hierarchy endpoints define descending bed/water profiles. Reach-scale
  monotone shelf/drop templates depend on slope and a synthetic lithology-based
  hardness estimate. Low-gradient small creeks are not forced to be turbulent.
- Compact cut-only bed primitives follow amplified channel segments. Overlapping
  beds combine as a minimum envelope, not a discontinuous nearest-reach winner.
  Provenance contributions retire smoothly at shared face boundaries. Existing
  broader terrain/valley shaping remains; this does not raise a bank causeway.
- Short procedural tributaries have a physical contributing-area/runoff bound
  rather than inheriting12% of a continental river's flow at every scale.
- Terrain and water use channel-aligned near vertices. Original grid boundaries
  and diagonals share their inserted edge points, preventing refinement T-junction
  cracks. Streaming/LOD scheduling, reanchoring and walking controls are unchanged.
- Valid reach-derived water potentials persist at dry corners and headwaters;
  they are NOT dry-ground elevations. A compact bed modifier's radius is no longer
  an artificial cutoff of water over an otherwise submerged valley. Actual drawn
  terrain determines its banks, with the accepted two-metre hidden overlap.
- Adjacent continuous river potentials share meshing vertices even across coarse
  channel-ID boundaries. Receiving lake/sea corners retain their fixed datum.
  Standing basins are never blended into each other or bent toward terrain.
- Existing water vertex colours carry river flow/turbulence to a single-pass
  surface: moving ripples, analytic sky/Fresnel reflection, sun glints and restrained
  turbulent highlights. No scene-copy/reflection pass or fluid simulation. Standing
  water retains its existing appearance and fixed-level path.

## Scope and limits

This is an adaptation using our existing CPU hierarchy and compact evaluator;
we have not imported the paper's entire authoring/routing or blend-flow system.
True detached waterfall sheets, spray, plunge-pool art, hydraulic backwater,
full sediment transport, braiding and detailed asymmetrical meander erosion
remain future work. Current steep drops are heightfield cascades, not overhangs.
The new water shader is approximate opaque water, not refraction or underwater art.

Coarse render triangles still approximate the fine potential field: separate
drainage domains may be visually bridged when their separating land is lost at
LOD. River datum selection itself still follows the nearest amplified reach;
its fine-scale transitions at close bends/junctions warrant owner review. The
bed envelope is continuous across the tested shared edges, not proof that every
river's surface/profile is physically valid. No scale-independent width retuning
or complete global morphology acceptance is implied.

No new global fine raster, serialized profile set or simulation tick is added.
Near geometry does more work than lake7: actual CLR205m/32 river tiles measured
roughly20-145ms without simultaneous compilation, with substantially more
channel vertices. These run in the bundled hidden worker; this is NOT a guarantee
of unchanged loading latency or FPS. Native evidence belongs in VALIDATION.md.

## References

- [Procedural Riverscapes paper](https://perso.liris.cnrs.fr/egalin/Articles/2019-riverscapes.pdf)
- [Authors' results video](https://www.youtube.com/watch?v=Xe2bjYaPMfY)
- [Hyper-amplification reference](https://github.com/Arches-Team/Real-Time-Hyper-Amplification-of-Planets)

The new river primitive formulas are project code inspired by published methods,
not copied paper illustrations/assets or a claimed ready-made Unity package.
