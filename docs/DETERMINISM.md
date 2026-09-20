# Determinism and versioning

Schema **3**, algorithm **worldgen-3.6.0**. The determinism key is seed + complete recipe + algorithm version, including Detail, Irregularity and continental-complex topology. Older recipes are rejected. View projection, longitude, wireframe, plate coloring, grid and window controls are presentation only.

`World.CoarseSurface` has exactly 20,000 cells; `CoarsePlate`, `CoarseContinent` and `CoarseHash` retain the independent coarse partition. Changing only detail or irregularity preserves `CoarseHash`. The final Surface and terrain/climate arrays have Recipe.Detail cells. Fine terrain is recomputed; no display interpolation changes the apparent resolution of a 20k simulation.

Owned named xorshift32 streams, stable IDs, sorted packed mesh edges, ordered adjacency/frontiers and stable graph ties govern generation. Vector operations quantize to 1e-9; scalar noise to 1e-6; physical heights, distances, areas and climate storage use integer fields. Owned fixed-iteration square root, sine and atan avoid runtime math discrepancies in authoritative calculations.

Stages: coarse-plates, geometry, plate-projection, plate-physics, superplates, boundaries, terrain, erosion, climate, complete. Each intermediate stage hashes its name, previous hash and its outputs. The chain starts with the complete recipe hash. The final hash covers all public world state except the stage-record list. This avoids repeatedly hashing a multi-million-cell mesh at every intermediate stage while retaining snapshot integrity.

SHA-256 uses ordinal field names, explicit type tags and ordered collections. Floating diagnostics quantize to 1e-6 and normalize signed zero. Specialized mesh-record writers preserve the exact canonical byte format while avoiding reflection/boxing; runtime layout guards reject newly added fields until these writers are updated. A buffered streaming writer checks cancellation while hashing; it does not allocate a second full serialized copy. Timings and presentation never enter authoritative hashes.

Fresh, repeat, reapply, recipe roundtrip and cached reconstruction all call WorldPipeline. Tests require every stage to match, not only the final result. The reviewed fixtures in tests/golden/v3 are generated explicitly outside passing tests. V1/V2 remain historical. V3 changes are intentional: fine simulation, ocean-connectivity topology without persistent hydrology, no collision-context route, and the new hash chain.

CLI, Unity Mono and Chrome Wasm must be compared. Their agreement does not establish untested IL2CPP, ARM, other operating systems or other browsers. See VALIDATION.md for actual evidence.
