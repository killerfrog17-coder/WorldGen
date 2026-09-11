# Determinism contract, versions and cross-host validation

The current recipe schema is 1; the algorithm is `worldgen-1.0.0`. A complete recipe, seed and algorithm version determine authoritative output. Unsupported versions fail rather than silently invoking a new generator. Recipe loading requires all known fields exactly once and rejects unknown settings.

Randomness is project-owned xorshift32. Each stream starts from the seed, FNV-1a of a named stream, and a mixed stable entity ID. Mixing uses explicitly unchecked 32-bit integer overflow; random fractions use exactly 24 bits. Geometry, seeding, plate motion, frontier variation, continents, ghosts, volcanic chains and endorheic choice have distinct streams. No System.Random, Unity random state, JavaScript random state, wall clock or GPU results enter authoritative decisions.

Vectors quantize scalar components to 1e-9 on construction and dot products to the same scale. Owned fixed-iteration Newton square root and series atan/sine avoid runtime transcendental-library selection. Noise outputs quantize to 1e-6. Growth ranking uses integer millionths; metres, motion/stress units, permille province weights, graph distances and hydrology levels are integers; area and accumulation are signed 64-bit square metres. All graph ties have ordered IDs. Collections used for lookup are not enumerated to choose authoritative winners; frontiers, components, neighbors and reductions have explicit ordering.

Stage hashes serialize public instance fields in ordinal name order, with canonical primitive type tags and ordered arrays/lists. Generic type tags exclude runtime assembly identities (e.g. `mscorlib` versus `System.Private.CoreLib`). Floating diagnostics hash at 1e-6 precision; integer authoritative fields hash exactly. SHA-256 produces a stable digest after geometry, microplates, parents, continents, boundaries, terrain, erosion, hydrology and completion. Each digest covers the complete state available at that stage, including recipe, semantic records, warnings and accumulated metrics; the digest list itself is excluded. Wall-clock timings and presentation state are excluded.

`Reapply` and `Reconstruct` call the same complete pipeline. Cache reconstruction validates a result's integrity, then regenerates from its recipe. This deliberately prioritizes consistent output over incremental cache optimization. Changing render size, rotation, colors or preview sea level never enters this pipeline.

## Commands

Run from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build.ps1
dotnet run --project tests/WorldGen.Core.Tests -c Release
dotnet run --project src/WorldGen.Cli -c Release -- --preset Preview --verify
dotnet run --project src/WorldGen.Web -c Release --no-build --urls http://127.0.0.1:5080
# In another shell, while the web server is running:
npm ci
node scripts/browser-check.mjs
node scripts/worker-check.mjs
powershell -ExecutionPolicy Bypass -File scripts/unity-check.ps1
powershell -ExecutionPolicy Bypass -File scripts/unity-check.ps1 -BuildPlayer -PlayerCheck
```

`tests/golden/*.recipe.json` and `*.hashes.json` are the expected cross-host corpus. Worker tests compare **every stage**, not only final elevation. Unity editor tests compare reapply and JSON reconstruction with the same corpus. Player checks execute the built Windows runtime and compare its exported stage hashes. Outputs live under `artifacts/browser-worker`, `artifacts/unity-editor` and `artifacts/unity-player`.

Regenerating golden files is an explicit maintenance action, not part of tests:

```powershell
dotnet run --project src/WorldGen.Cli -c Release -- --corpus tests/golden
```

Review algorithm changes before updating this corpus; after release, output-changing edits require a new algorithm version. The corpus covers ordinary, connected, isolated, escape-face, dead-end, inland-sea, glacial-basin and 500-microplate recipes. Tests require the named special cases to actually contain their intended features.

Supported execution is validated against the exact hosts in `VALIDATION.md`. Matching x64 .NET, Unity Mono and Chrome Wasm does not establish untested IL2CPP, mobile ARM, console or all-browser parity. Treat those as additional targets requiring the same corpus rather than asserting universal floating-point equivalence.
