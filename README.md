# WorldGen

A deterministic, sphere-first C# planet laboratory with a Blazor WebAssembly browser, Unity runtime application, and headless CLI. One shared core generates the microplate hierarchy, tectonic terrain, erosion and persistent hydrology. The existing Unity 6000.6.0f1 / URP project is preserved.

## Run it

Requirements: .NET SDK **10.0.401**, its `wasm-tools` workload, and a modern WebGL browser. Unity validation additionally uses **6000.6.0f1** with Windows standalone support. Node is only needed for browser automation.

```powershell
dotnet workload install wasm-tools
powershell -ExecutionPolicy Bypass -File scripts/build.ps1
dotnet run --project src/WorldGen.Web -c Release --no-build --urls http://127.0.0.1:5080
```

Open **http://127.0.0.1:5080**. The initial Preview recipe generates automatically in a dedicated .NET worker. Drag the globe, scroll to zoom, click to inspect a cell, or choose a diagnostic layer. The left panel scrolls to all six setting groups and generation/file actions. Sea-level preview changes display/connectivity without changing the saved world. Larger recipes take longer in interpreted browser WebAssembly; Cancel terminates the worker immediately.

After `scripts/build.ps1`, open `unity/WorldGen.Unity` in the pinned editor and press Play in the existing SampleScene. A runtime bootstrap adds the globe and generation interface. It also runs in the built player. Recipe paths are editable; by default files use Unity's persistent-data directory. The runtime's **Next** preset button cycles the same presets as the browser. The shared recipe metadata supplies every effective field in both interfaces.

```powershell
# Fast headless generation, exports and reconstruction checks:
dotnet run --project src/WorldGen.Cli -c Release -- --preset Preview --verify --output artifacts/my-world
# Stress case:
dotnet run --project src/WorldGen.Cli -c Release -- --preset "500 microplates" --seed 500
# Saved recipe:
dotnet run --project src/WorldGen.Cli -c Release -- --recipe tests/golden/inland-sea.recipe.json
```

Outputs: complete `world.json`, `recipe.json`, `hashes.json`, per-cell `cells.csv`, and a 16-bit equirectangular `heightmap.pgm`. PGM is big-endian, with the documented linear mapping `height_m = -12000 + sample × 28000 / 65535`. Full JSON/CSV preserve exact integer-metre heights. Ctrl+C cancels CLI generation.

## What is included

- Irregular spherical macro cells with actual areas, great-circle distances and metric adjacency; 4–500 connected microplates; connected weighted parent growth with directional transport, size inequality and curvature.
- Parent affinity separate from active identity, genuinely contested independent actors, persistent ghost seams, density-aware local rotation and collision context.
- Continent identities, connected landmasses, shelves/sills, physical bathymetry, continuous terrain provinces, directional mountain relief, named forelands, rifts, trenches, arcs, volcanic chains, large igneous provinces and ancient orogens.
- Protected tectonic inland seas, moderate hydraulic/thermal/glacial shaping, retained elevation stages, final depression hierarchy, common lake levels and outlets, terminal basins, contributing area, watershed IDs and river reaches.
- Complete versioned recipes/results, named random streams, stage hashes and metrics, cancellation, presets, diagnostic layers, cell inspection and exports in both faces.

Climate-neutral river potential assumes uniform **1 mm/day runoff**. Seasonal discharge, climate, biomes/ecology, people, history and playable local terrain are not implemented.

## Validate and build the player

```powershell
dotnet run --project tests/WorldGen.Core.Tests -c Release
npm ci
# Requires the web server above:
node scripts/browser-check.mjs
node scripts/worker-check.mjs
# Editor cross-host validation:
powershell -ExecutionPolicy Bypass -File scripts/unity-check.ps1
# Build and test Windows player:
powershell -ExecutionPolicy Bypass -File scripts/unity-check.ps1 -BuildPlayer -PlayerCheck
```

The property/regression suite is a standalone executable test harness with a nonzero failure exit code; `dotnet test` is not its runner. Golden expected hashes and recipes are checked in under `tests/golden`. Browser automation uses installed Chrome by default; set `CHROME_PATH` for another Chromium executable. Generated binaries, screenshots, logs and results are under ignored `artifacts/`.

For a static release, first run `scripts/build.ps1`, then `dotnet publish src/WorldGen.Web -c Release -o artifacts/web-publish`. Serve its `wwwroot` over HTTP(S) with WebAssembly MIME support. The copied worker directory must ship with it. Restart a running development server after rebuilding because the asset manifest is fixed at startup.

## Project map and reading

| Path | Responsibility |
|---|---|
| `src/WorldGen.Core` | Authoritative .NET Standard 2.1 geometry, generation, data and hashes |
| `src/WorldGen.Serialization` | Strict recipes, verified snapshots, CSV and heightmap |
| `src/WorldGen.Presentation` | Shared diagnostic palette and completed-world mesh adapter |
| `src/WorldGen.Cli` | Headless generation and golden corpus exports |
| `src/WorldGen.Web` | Blazor controls and native WebGL inspection |
| `src/WorldGen.Web.Worker` | Separate .NET Wasm runtime invoking the same core |
| `tests/WorldGen.Core.Tests` | Synthetic, invariant, serialization and golden checks |
| `unity/WorldGen.Unity` | Preserved Unity project, runtime face and batch validation |

[Algorithms and limitations](docs/ALGORITHMS.md) · [Effective settings](docs/SETTINGS.md) · [Determinism and cross-host commands](docs/DETERMINISM.md) · [Validation evidence](docs/VALIDATION.md) · [Orogen reference map](docs/OROGEN_REFERENCE_MAP.md) · [Dependencies and licenses](docs/THIRD_PARTY.md).

The generator is a macro-scale geological synthesizer, not a physical plate-history solver. Preview cells limit small-channel and ridge resolution; connectedness and land coverage are targets with achieved metrics. Browser interpretation and complete JSON transfers are the main performance limitations. IL2CPP, mobile and other browser engines require additional corpus validation.
