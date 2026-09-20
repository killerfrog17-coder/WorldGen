# Implementation contracts - version 3

Read OROGEN_ALIGNMENT.md and the license decision before modifying algorithms. All generation is independently written C# in WorldGen.Core (.NET Standard 2.1). No GPL implementation is translated or shipped.

## Coarse structure and fine terrain

1. Build exactly 20,000 coarse Fibonacci/Delaunay cells with fixed irregularity .75 and the canonical-geometry PRNG stream. Stereographic triangulation uses the MIT DelaunatorSharp dependency; pole closure is counted inside 20,000.
2. Grow connected coarse plates from separated seeds, using weighted fronts and transported directional preference. Assign continents as whole-plate groups with area budgets and size variety. `ContinentCount` controls landmass identities; `ContinentalComplexCount` clusters those identities around separated complex anchors so one complex can contain several nearby continent units. Retain coarse geometry, plate/crust arrays and a coarse-only hash.
3. Build Recipe.Detail fine cells (5,000 to 2,560,000), with Recipe.Irregularity and a separate named geometry stream. Packed sorted edge keys create deterministic edges and ordered adjacency without millions of tree allocations.
4. Sample coarse identities through an independently chosen spatial perturbation. Connected weighted growth reconciles the desired fine labels, protects one seed per plate, fills every cell, and rebuilds memberships/areas/adjacency. This keeps every fine plate connected, including at minimum detail. It is an independent connectivity method, not the reference's exact smoothing implementation.
5. Recompute plate physics, connected superplates, merge any superplate enclave surrounded by one neighboring group regardless of crust type, then build boundaries, stress and graph distances on the fine mesh. Parent continental fraction is area-weighted after mixed-crust merges. Euler motion, density/drag, ridge/slab forces and mantle fields use independently selected coefficients. Map2 escape/dead-end collision contexts are inactive.
6. Compute coast/convergence/divergence distances, continental basement, provinces, oriented mountains, forelands, shelves, ridges, trenches/arcs, sphere noise, hotspots and volcanic trails on fine cells. Hotspot geographic seeds are independent of cell count.
7. Resolve the structural ocean topology before postprocessing: flood below-zero cells from every ocean-crust cell, retain stranded continental depressions as land without changing their elevations, then apply blended terrain warp, structural-coast-preserving smoothing, elevation-gated detail, hydraulic/glacial/thermal erosion, ridge sharpening, creep and deterministic continental hypsometry on the fine graph. Erosion uses temporary priority-flood receivers and contributing area; these are discarded. It creates no permanent lakes, river reaches, watershed system or protected inland seas.
8. Compute final ocean connectivity from every below-zero ocean-plate cell, allowing multiple disconnected ocean basins. Preserve disconnected below-zero terrain as land without changing its elevation; persistent hydrology remains deferred. Compute two seasonal climate fields and Koppen classes from that ocean mask and final fine terrain. Fixed advection operands and ocean evaporation are cached; synchronous moisture/shadow buffers are reused. Multiplication/division order is preserved, and the optimization must match the reviewed hashes. Heights are physical integer metres.

WorldPipeline is the only generation route, including reapply/cache reconstruction. Core cancellation checks occur in major loops, graph work and streaming hashing. Dependency triangulation and Array.Sort complete their current call before cancellation is observed; neither runs on Unity's UI thread.

## Presentation

SurfaceMesh emits Voronoi-style cell fans using adjacent triangle centers. Terrain, Satellite, Climate and categorical layers have constant color per cell. Heightmap interpolates physical heights at centers/corners. This fixes the former blurry center-to-center color interpolation. No higher-detail display mesh exists: Detail belongs to generation.

Shared geometry is yielded in bounded pieces of at most 8,192 dual edges. Wireframe follows dual cell edges. Plate outlines appear on all layers; plate colors replace Terrain colors only. Geographic grids accept Off, 15 or 30 degrees. Map projection unwraps/clips the longitude seam and fills pole strips; every fan has consistent outward orientation before projection.

Unity runs Core and shared presentation in a worker task. A bounded queue holds at most two prepared upload pieces. Update uploads pieces within a per-frame budget; no giant MeshCollider or synchronous normals rebuild occurs. A new root becomes visible only when all pieces succeed. Cancellation keeps the previous complete world/view and releases temporary meshes. Picking intersects the analytic sphere or map plane and queries the core graph.

Map center longitude translates cached geometry and its shared-mesh wrap copies; the shader clips the fixed map rectangle. Longitude dragging performs no cell loops or mesh reconstruction. Globe orientation composes yaw with clamped pitch and never accumulates roll. Zoom scales the distance above the globe surface exponentially.

The browser retains authoritative state in its .NET worker. Its UI receives a small summary, with C# mesh pieces streamed separately; JavaScript only transports data and renders it. New views upload one bounded piece per animation frame. Cancel terminates generation workers or interrupts a presentation request between pieces, retaining the preceding rendered view.

## Deferred scope

Persistent hydrology is disabled; the old solver has been removed from the source tree. Legacy empty model fields remain for source compatibility, without generated water bodies or receivers. ISeasonalRunoffProvider is only an extension boundary. Ecology, Map2 collision-context behavior, civilization/history and playable local terrain are outside this version.
