# Terrain boundary repair — October 31

The owner reported sky-blue chunk edges resembling distant streams. A three-tile mountain fixture reproduced an actual 4.296082 m crack: a large tile bordered two smaller tiles with different grid resolutions. The old planner reduced the entire large edge to the largest neighbouring sample spacing. Its finer neighbour followed a different spacing, leaving the two edge curves apart even after loading completed.

Boundary plans now retain separate local intervals for each neighbour. Uniform complete edges keep the compact four-number representation; mixed or partially covered edges carry bounded interval records. The builder applies the spacing for the particular section being generated. Neighbour identity also uses integer quadtree coordinates, avoiding missing adjacency from floating-point additions at non-binary world scales.

The repair does not change the world sampler, population, requested LOD, vertex budget or drawing distance. It changes presentation boundary interpolation. Different LODs can still shade terrain differently; this repair targets physical sky-visible openings, not every possible material/lighting transition. Transient fallback/reanchor behavior remains a separate existing mechanism.

Terrain worker transport is version 8 and carries the whole interval list. Cache keys include those intervals, snapshot stored keys, and avoid allocating arrays for repeated lookup keys. Worker and player must be packaged together. Existing saves regenerate render meshes without a save-format conversion.

Current checks: 814 local terrain assertions, including rotated/scaled mixed-neighbour geometry, interval transport, integer adjacency and cache isolation; 1,382 adaptive-detail checks; 6,241 streaming/cache/stitch checks; 5,990 fog-frontier checks. The original 4.296 m fixture now has a maximum shared-edge discrepancy of approximately 0.000001 m. Source world hashes remain unchanged. Native rendering and packaged-worker verification are recorded in the release manifest when complete.

The isolated native fixture uses the production mesh upload and terrain shader with a uniform diagnostic pigment, comparing legacy whole-edge stitching against the repaired geometry. It does not claim to sample every natural biome, water boundary or transient loading state.
