# WorldGen

A C# planet generator with Unity and browser faces, a fixed **20,000-cell plate base** and detail-dependent terrain, globe/map views, and Terrain, Satellite, Climate and Heightmap layers.

Version 3 corrects the former display-only detail interpretation and removes persistent hydrology with an independent implementation aligned more closely with World Orogen's pipeline and settings. The owner explicitly chose **no GPL source translation**. This is not an identical-output port; [alignment and remaining differences](docs/OROGEN_ALIGNMENT.md) are documented.

## Run

Requirements: .NET SDK **10.0.401**, wasm-tools, Unity **6000.6.0f1** / URP **17.6.0** for Unity, and Chrome for the automated browser checks.

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build.ps1
dotnet run --project src/WorldGen.Web -c Release --no-build --urls http://127.0.0.1:5080
```

For Unity, open `unity/WorldGen.Unity` and press Play in SampleScene. The runtime menu also works in the standalone player. Use the four view tabs and Globe/Map buttons; drag to orbit/pan, scroll to zoom, and click to inspect. Detail and Irregularity change actual terrain and require regeneration. Use wireframe, Show plates and Off/15/30-degree grid controls. F11 toggles fullscreen; the window and sidebar are resizable. Map mode has a center-longitude slider. Recipe files default to Unity's persistent-data directory; the path is editable.

The tested Windows build is staged at `artifacts/player/WorldGen.exe` on this workspace. Generated player files are not tracked in Git.

The browser uses the same recipe metadata and a dedicated .NET worker. Open http://127.0.0.1:5080. Restart its server after rebuilding fingerprinted assets. Browser computation/JSON/display preparation is slower than the native CLI; Preview generates a 20k terrain mesh. Generation and presentation run in the worker; the UI receives a small summary and bounded mesh pieces.

```powershell
dotnet run --project src/WorldGen.Cli -c Release -- --preset Preview --verify --output artifacts/my-world
dotnet run --project src/WorldGen.Cli -c Release -- --preset "500 plates" --seed 500
dotnet run --project src/WorldGen.Cli -c Release -- --recipe tests/golden/v3/ordinary.recipe.json
```

Exports include complete world/recipe JSON, stage hashes, cell CSV, and a 16-bit equirectangular PGM. The Heightmap palette and PGM use `height_m = -5000 + sample × 11000 / 65535`; values outside the range clip in the image, while JSON/CSV preserve exact heights.

## Validate

```powershell
dotnet run --project tests/WorldGen.Core.Tests -c Release
npm ci
node scripts/browser-check.mjs
node scripts/worker-check.mjs
powershell -ExecutionPolicy Bypass -File scripts/unity-check.ps1
powershell -ExecutionPolicy Bypass -File scripts/unity-check.ps1 -BuildPlayer -PlayerCheck
```

Do not run a second editor against an open Unity project. Add `-Isolated` to either Unity check command to use a separate project copy under ignored artifacts. Use `-PlayerFolder player-final` when building the standalone player. After building, `scripts/unity-render-check.ps1 -Isolated` captures all eight main views at the default detail. `scripts/unity-interaction-check.ps1 -Isolated -Detail 2000000` tests loading/cancellation and actual Windows message handling. Current golden expectations are in `tests/golden/v3`; version-1/2 fixtures remain historical and are not silently overwritten. Tests never regenerate expectations. Binary outputs, screenshots, reference-probe results and logs stay under ignored artifacts.

## Architecture

- `WorldGen.Core`: sole authoritative .NET Standard 2.1 pipeline, geometry, plates, physics, terrain, erosion and two-season climate.
- `WorldGen.Presentation`: shared palettes, cell surfaces, globe/map geometry and cell inspection.
- `WorldGen.Serialization`: strict versioned recipes, integrity-checked snapshots, CSV/PGM.
- CLI, Unity runtime and .NET browser worker all invoke `WorldPipeline`.

Persistent lakes/rivers and custom Map2 features are deferred. Erosion uses temporary drainage calculations only. Ecology, civilization/history and local playable terrain remain deferred. Older schema-1/2 recipes/results are rejected explicitly.

Native maximum-detail smoke check: `dotnet run --project src/WorldGen.Cli -c Release -- --detail 2560000 --summary --render-check`. This generates the actual fine terrain and both presentation projections without writing a multi-gigabyte full snapshot.

[Algorithms](docs/ALGORITHMS.md) · [Settings](docs/SETTINGS.md) · [Determinism](docs/DETERMINISM.md) · [Validation](docs/VALIDATION.md) · [Dependencies](docs/THIRD_PARTY.md).

Codex command: codex resume --last --sandbox danger-full-access --ask-for-approval never