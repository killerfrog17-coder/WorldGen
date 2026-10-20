# Drainage-aware planetary amplification

`local-terrain-hyper-2` replaces the geographic-scaffold surface for completed
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

The root topology and inverse triangle matrices are stored in primitive struct
arrays, not an eagerly allocated graph of400k faces,600k edges and1.2million
incident-face reference objects. Root face/edge objects are realized only for
queries, with weak address caches and gradual expired-wrapper cleanup. Root
addresses must not hold strong face references that would reconnect the whole
planet. Live subdivision bundles retain their parent faces, so cached detail
cannot lose its ancestry when an unused coarse object expires. Root river
provenance is precomputed numerically and bound lazily, preserving the same
incoming/outgoing choice regardless of query order. Earth/Continents Plus probe
files are byte-identical to the eager representation; all492 LOD tiles and
concurrent/cold-cache checks pass. This is a representation change, not less
terrain detail or changed water/height rules.

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

The broad valley height field interpolates shared terminal-vertex attributes.
It must not switch its cut level to whichever river happens to be nearest:
that produced discontinuous walls between reaches with different water levels.
Nearest displaced channels still determine the actual wet footprint and level.
Dry lake provenance no longer disables a river's valley profile.

All queries use one global topological terminal depth, selected for a nominal
8 m physical spacing from the sphere area/base-face count. Actual edge spacing
varies with the irregular input triangles. This makes the terminal height mesh
conforming; stopping adjacent triangles independently by their longest edge did
not. Continuous interpolation applies inside these shared triangles. Distant render LOD reduces
mesh tessellation, not the height returned at a shared point. The existing tile planner,
near-first scheduling and hidden reanchor handoff are retained. Fine terminal
spacing is an implementation budget, not a promise of centimetre geomorphology.

Refinement now perturbs the parent face at the displaced midpoint, with separate
broad/fine gains conditioned on hill/mountain activity, sediment and plateaus.
This suppresses repeated random full-slope interpolation and fine spikes, not
the source mountain elevation. New perturbations are bounded by the incident
triangle altitude as well as edge length, including thin input triangles.
The map's **Relief** layer sits below Terrain and shares these landform controls.
Its metres describe a local detail envelope, not absolute elevation, measured
DEM roughness or the exact peak-to-valley range of a particular nearby view.

## Determinism and disposal

Vertex choices are keyed by stable edge/face identifiers. Shared edges, not tile
origins, own their choices. Lateral displacement is limited by **both** incident
triangles' barycentric safe regions (including their endpoint constraints) to
prevent inverted slivers. Scalar construction intermediates avoid quantized
vector projection errors; final midpoint directions are quantized at 1e-12.
Point location walks refined
neighbors, since a displaced child edge need not coincide with its old parent
boundary.

A bounded 32,768-bundle working set holds recent subdivisions. Child links are
weak; live descendants retain the necessary ancestry. Incident faces have
stable root/dyadic addresses so garbage collection cannot alter geometry or
lose a neighboring face. They do not strongly retain the whole connected trail
of visited terrain. Concurrent overlapping/cold-cache queries are regression
tested. This is a bound on retained cache entries, not a byte-perfect total
memory bound including active workers/meshes and their leases.

Fine queries keep their last terminal face for repeated controller queries and
nearby fine vertices instead of always descending again from the root. Unity
terrain workers also fill writable native MeshData vertex/index buffers and
bounds; the main thread applies them and binds render objects. This removes the
old per-tile main-thread managed conversion arrays and forced UploadMeshData
call. The existing two worker lanes, near-first planner and handoff are retained.
Applied meshes keep a bounded native CPU copy; this is not zero-memory upload.
Cancelled/obsolete work disposes un-applied native buffers. Native review tests
check the actual vertex/normal/color/lithology/index/bounds/water data and record
loading-frame percentiles, collections, mesh apply/bind and controller timings.
These changes require native measurements, not an assumption of hitch-free play.

The first native upload revision brought apply/bind below0.6ms but still had
28–35 collections and matching >100ms frame stalls per scene. Those stalls
cannot be called fixed by moving uploads. Permanent base faces now avoid about
1.2million unnecessary weak handles, and expired child-link wrappers are reused.
An explicit1.5ms incremental-collection experiment was **rejected**: even with
allocation-based triggering it produced538 collections/89s in the river scene,
versus35/39s before, and individual calls exceeded their requested slice during
unsliced work. Neither continuous nor threshold-triggered explicit collection
is shipped. Automatic incremental GC remains enabled; there is no disabled-
collector/unbounded-heap mode. Allocation reduction, not forced collection, is
the retained change. Native results determine the remaining stall limits.

-[Unity incremental collection modes](https://docs.unity.com/en-us/engine/6000.7/manual/analysis/performance-memory/managed-memory/incremental-garbage-collection)
-[CollectIncremental API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Scripting.GarbageCollector.CollectIncremental.html)

## Unity execution backend

The Windows player now bundles a self-contained local CoreCLR worker (about
80MB) for mesh **and scatter** generation. The exact same Core/Presentation
assemblies run there. Unity retains precise ground queries, rendering, native
MeshData preparation, the existing two work lanes/near-first policy and hidden
reanchor handoff. There is no alternate terrain formula or reduced detail.
Moving meshes alone was insufficient: scatter samples thousands of terrain
points per patch and continued to cause Unity GC fallback pauses.

The worker communicates only over authenticated loopback TCP. Requests carry
the exact frame basis (no second normalization), tile/stitching parameters or
global scatter address. Float arrays/instances retain their exact values.
Disconnected/cancelled requests cancel Core work. Windows launches it hidden
at below-normal priority using an owned process handle; world replacement/game
exit terminates that handle, with a parent-process watchdog as a second guard.
An absent packaged worker retains the in-process fallback; no such fallback is
used to establish the Windows measurements below.

Fresh worlds warm the worker by replaying their small, deterministic recipe and
verifying the generation-stage fingerprint. This costs about13s once in the
measured200k Continents Plus run, while setup/simulation can already be used.
Loaded binary worlds instead use their literal existing file, not regeneration.
Legacy JSON imports without a binary snapshot use the verified recipe path;
if it cannot reproduce the source fingerprint, exploration fails explicitly
rather than substituting different geography. Saving/reloading a binary world
provides the literal snapshot route. There is extra worker RAM, not a fine
world raster on disk. Full-world serialization inside Unity was rejected after
it exceeded the native near-ready test's30s startup allowance.

The build script publishes/packages the standalone runtime and stages matching
authoritative assemblies before IL2CPP compilation. No separate .NET install
is required to play. See VALIDATION for exact transport/native measurements.

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
