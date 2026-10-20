# Drainage-aware planetary amplification

`local-terrain-hyper-1` replaces the geographic-scaffold surface for completed
worlds. It is a **shared CPU adaptation**, not the original application's GPU
compute-shader pipeline or a claim of identical output/performance.

## Source and integration

The basis is Cortial, Peytavie, Galin and Guérin, *Real-Time Hyper-Amplification
of Planets* (2020), and the MIT-licensed Arches-Team reference implementation:

- [Paper/project](https://perso.liris.cnrs.fr/apeytavi/website/publication/hal-02967067/)
- [Released source](https://github.com/Arches-Team/Real-Time-Hyper-Amplification-of-Planets)
- Inspected revision: `b3341c336b2b3070def911fd0c5df196de2bfc64`.
- Relevant shader rules: `edgeSplit.comp`, `faceSplit_river.comp`, `profiles.comp`.
- License: `third_party/hyper-amplification/LICENSE`.

The standalone Qt/OpenGL application was inspected, not executed. Its published
GPU timings must not be presented as measurements of this integration.

## What is generated

At terrain preparation, Core builds shared typed edges/faces over the existing
spherical Delaunay triangles. Source heights, drainage receivers, discharge,
standing-water levels, moisture, geological age proxies, plateau/hill/mountain
and sediment descriptors seed this graph. No extra planet-wide fine raster is
stored, serialized, or added to historical simulation ticks.

On demand, edges split and faces produce four children. Both incident faces
share the same midpoint. River edges remain connected and descend; terrain
between rivers becomes a divide, river-to-upland edges produce valley slopes,
and internal edges can introduce subordinate tributaries/dry gullies. Lake
nodes inherit a constant surface, distinct from their bed. Land amplification
blends peak, age/moisture-dependent mountain, hill and plateau rules.

This adapts the source's crust-maximum input to our mean-elevation input and
retains physical slope/relief limits on horizontally compressed planets.
Geological age on imported Earth is a coarse relief-based proxy, not measured
geological data. This is procedural plausible terrain, not local Earth DEM
reconstruction or a hydraulic erosion simulation.

Final river distance/profile evaluation follows the **actual displaced channel
hierarchy**, not the old coarse straight/spline strip. Valley sides and channel
beds are sampled by the same function used by terrain meshes, collision queries,
scatter placement and water clipping. The valley profile only cuts; it does not
raise a river embankment through low terrain. Root confluences share water levels.
The saved hydrology order is source-first; cutting propagates forward, whereas
downstream water constraints propagate backward. Headwaters taper to zero depth.

Walking/scatter queries use the canonical hierarchy at an 8 m physical terminal
spacing with continuous interpolation inside its triangles. Distant render
queries may stop earlier in the same hierarchy. The existing tile planner,
near-first scheduling and hidden reanchor handoff are retained. Fine terminal
spacing is an implementation budget, not a promise of centimetre geomorphology.

## Determinism and disposal

Vertex choices are keyed by stable edge/face identifiers. Shared edges, not tile
origins, own their choices. Lateral displacement is limited by **both** incident
triangle altitudes to prevent inverted slivers. Point location walks refined
neighbors, since a displaced child edge need not coincide with its old parent
boundary.

A bounded 32,768-bundle working set holds recent subdivisions. Child links are
weak; live descendants retain the necessary ancestry. Incident faces have
stable root/dyadic addresses so garbage collection cannot alter geometry or
lose a neighboring face. They do not strongly retain the whole connected trail
of visited terrain. Concurrent overlapping/cold-cache queries are regression
tested. This is a bound on retained cache entries, not a byte-perfect total
memory bound including active workers/meshes and their leases.

The historical world and existing generation hashes are unchanged. Old histories
are not upgraded. Local preview geometry intentionally changes; permanent
structures must not rely on an unversioned preview surface.

## Review commands

```powershell
dotnet run --project tests/WorldGen.Core.Tests -c Release -- --hyper-review <world.world> <diagnostic-folder>
# Append --fresh-generation to include a complete regeneration timing.
```

The native diagnostic uses `--worldgen-local-terrain-check <world.world>
--worldgen-landform-review --worldgen-hyper-review --worldgen-output <folder>`.
It measures near-ready time separately from full distant coverage and captures
actual camera renders at mountain, hill, plateau, river and lake sites.

Limits: coastal/island specialization, fully realistic fluvial meanders,
braiding, waterfalls, deltas, glaciers, fjords, aquifers and regional geological
reconstruction are not implied by implementing this amplification foundation.
Placeholder materials/vegetation remain. See VALIDATION for measured results and
any outstanding review failures; test counts are not aesthetic certification.
