# WorldGen

History15 separates rare independent domestication/state origins from locally transmitted traditions. Productive foraging can remain a mixed livelihood, cultivation no longer receives guaranteed rounded progress, and sustained neighboring state experience is recorded in city articles. Early administrative reach depends more strongly on transport and institutions. Regional simulation indexes remain; older saves retain their rules unless explicitly updated.

The game shell opens to **Set Up World / Load World / Options / Exit**. Choose a preset (default **Continents plus**), generate, place humans, then **Start world** to run human expansion, regional cultures, cities and states. History now includes capabilities through modern communications and medicine; a saved technology limit can instead stop development at Medieval, Early Modern or Industrial. Completed plate, terrain, climate and ecological views become available during generation. Plates have their own view; dragging a flat map wraps longitude. Inspection is view-specific, with ecological details and linked historical records.

Numeric settings accept fixed values or optional minimum/maximum ranges with linear, lower-biased or higher-biased rolls. A fixed seed resolves them repeatably; saved worlds retain the selected complete recipe. Earth samples bundled measured relief and simulates climate/ecology afresh. Escape pauses the simulation and opens save/menu actions; the world library groups saves and supports loading/deleting them. Existing saves are preserved.

Current source models are **worldgen-3.32.0 / prehistory-6 / history-15 / calendar-2**. Named crop lineages have growing-season, moisture, soil and day-length constraints, local diffusion, and bounded selective breeding. City articles show actual local crop surveys. Food, institutions, funded administrative connections, leaders, campaigns, dependencies and provincial pressures influence state histories. Headlines omit routine administration without deleting the archive; population/area ranking shows its numeric values. See [historical model and limits](docs/HISTORY_SIMULATION.md) and [tested evidence](docs/VALIDATION.md). Histories1–14 retain their rules; an explicit future-model upgrade does not rewrite past events. This remains a procedural history foundation, not an archaeological reconstruction, detailed modern economy or local playable-city model.

In simulation, Space pauses and `<`/`>` change target time rate, from real time to1,000 years/s; a numeric value and time unit allow finer choices. The saved deterministic event queue processes different concerns at different times rather than treating playback frames as simulation ticks. Auto defaults to500/250/50/5 years/s through the historical phases; manual rates persist. The displayed date gains months, days, hours or minutes at slower rates. Observer Year0 is the first state. `+`/`-` adjust base-map opacity by25percentage points; Shift adjusts the historical layer. The three bottom panels collapse out of the map, and selecting a cell does not unfold details. The root shortcut launches the tested history15 player; its main-menu footer identifies the model. Taskbar pins can still reference an older executable. Dense histories can run far below the requested speed, and the date waits for completed work rather than inventing progress.

A C# planet generator with Unity and browser faces, a fixed **20,000-cell plate base** and detail-dependent terrain, globe/map views, and Terrain, Satellite, Climate and Heightmap layers.

Version 3.28 adds **Ecoregions** and **Biomes**, abiotic habitat diagnostics, a representative modern-native Earth life pack, constrained dispersal and inspectable food webs. Click a cell and expand/scroll its inspector. Existing saves remain unchanged; regenerate and save a new copy to add ecology. See [ecoregion model and limitations](docs/ECOREGIONS.md). Soil, flooding, groundwater, permafrost, fire and salinity are coarse habitat proxies, not resolved physical simulations; species presence and feeding opportunities are not population dynamics.

Version 3 corrects the former display-only detail interpretation with an independent implementation aligned more closely with World Orogen's pipeline and settings. The owner explicitly chose **no GPL source translation**. Version 3.18 added persistent hydrology, smaller hotspot edifices, synthetic ancient ranges and bundled Earth relief. Version 3.19 introduced fractional lakes; version 3.20 reduces excessive mountain belts and constrains actual lake storage with conservative water delivery and subgrid throughflow. This is not an identical-output port; [alignment and remaining differences](docs/OROGEN_ALIGNMENT.md) are documented.

## Run

Version3.22 revises four-season climate geography: coastal/monsoon moisture, warm-current influence, rain shadows, inland drying and seasonal temperature contrasts. The Hydrology panel's **Lake amount (%)** slider runs0-1000;0 disables lakes and100 is now20 times3.21's default parameter scale. New5% matches the old default forcing under identical climate.1000 means10 times the new default, not necessarily10 times lake area. Regenerate after changing it. Seasonal diagnostics, inspection and CSV retain all four quarters. See [validation and known regional errors](docs/VALIDATION.md); this remains an empirical climate model, not an atmospheric general circulation model.

Requirements: .NET SDK **10.0.401**, wasm-tools, Unity **6000.6.0f1** / URP **17.6.0** for Unity, and Chrome for the automated browser checks.

On a fresh checkout, restore the versioned large game assets before building:
`python scripts/restore-game-assets.py`. See [asset recovery and checkpoint policy](docs/ASSET_VERSIONING.md)
for prerequisites, verification and recovery of older packaged assets.

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build.ps1
dotnet run --project src/WorldGen.Web -c Release --no-build --urls http://127.0.0.1:5080
```

For Unity, open `unity/WorldGen.Unity` and press Play in SampleScene. The standalone player uses the same main menu and world setup. Drag to orbit/pan, scroll to zoom and click to select; flat-map dragging wraps longitude. Generation settings require regeneration, while completed display layers are cached. In simulation, **View** contains base-map, historical-overlay, opacity, river and grid settings. The map remains visible while background simulation catches up. **Esc** opens save/menu actions; **Atlas** opens linked world, state, city, people, religion and ruler articles. Worlds and their saves are managed through the in-game library, not a typed file-path field.

The default Windows build location is `artifacts/player/WorldGen.exe`; [validation evidence](docs/VALIDATION.md) records the latest tested build when a separate folder is used to preserve an open player. Generated player files are not tracked in Git.

Player builds now use normal non-development options. Add `-DevelopmentPlayer` to `scripts/unity-check.ps1 -BuildPlayer` when a diagnostic development runtime is wanted; ordinary players do not carry the development watermark.

The browser uses the same recipe metadata and a dedicated .NET worker. Open http://127.0.0.1:5080. Restart its server after rebuilding fingerprinted assets. Browser computation/JSON/display preparation is slower than the native CLI; Preview generates a 20k terrain mesh. Generation and presentation run in the worker; the UI receives a small summary and bounded mesh pieces.

```powershell
dotnet run --project src/WorldGen.Cli -c Release -- --preset Preview --verify --output artifacts/my-world
dotnet run --project src/WorldGen.Cli -c Release -- --preset "500 plates" --seed 500
dotnet run --project src/WorldGen.Cli -c Release -- --recipe tests/golden/v3/ordinary.recipe.json
```

Exports include complete world/recipe JSON, stage hashes, cell CSV, and a 16-bit equirectangular PGM. The Heightmap palette and PGM use `height_m = -5000 + sample × 11000 / 65535`; values outside the range clip in the image, while JSON/CSV preserve exact heights.

Continents again use the original whole-plate land/sea allocation. Continental complexes and composition are removed. **Interconnectedness** (1–50) is the exact number of candidate plate assignments to score: narrow contacts and near connections score positively; broad continent fusion scores negatively. The best candidate proceeds through the terrain pipeline once. Increasing the value increases search effort, not the number or width of forced bridges. World types are deferred. One candidate preserves the original default allocation path. Land coverage again supports 0–100%; whole-plate budgets remain approximate. Recipes use `worldgen-3.22.0`; older algorithm versions are rejected.

Lake cells are painted only at **90% estimated water coverage**. Smaller lakes remain stored with coverage, area and volume in snapshots/cell CSV and inspection; river visibility is independent. These sub-cell estimates are constraints for future terrain realization, not a generated high-resolution lake shoreline. New climate and hydrology require regenerating a world in3.22; rebuild the matching historical source checkpoint with its dependency manifest for older versioned snapshots. Pre-manifest versions may additionally need the archived asset index.

```powershell
dotnet run --project src/WorldGen.Cli -c Release -- --continents 6 --interconnectedness 20 --verify
```

Tectonic terrain distinguishes collision plateaus, subduction cordilleras, segmented island arcs, back-arc basins, oblique ranges and age-progressive hotspot trails. Uplift is restricted to the appropriate plate side. These are process-inspired approximations, not an Earth-history simulation; see [geological comparisons and limits](docs/TECTONIC_REALISM.md).

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

- `WorldGen.Core`: sole authoritative .NET Standard 2.1 pipeline, geometry, plates, physics, terrain, erosion and four-season climate.
- `WorldGen.Presentation`: shared palettes, cell surfaces, globe/map geometry and cell inspection.
- `WorldGen.Serialization`: strict versioned recipes, integrity-checked snapshots, CSV/PGM.
- CLI, Unity runtime and .NET browser worker all invoke `WorldPipeline`.

Choose the **Earth** starting recipe for offline measured NOAA ETOPO2022 terrain, then Generate. Its 10-arcminute bundled elevation/bathymetry grid bypasses procedural sculpting; climate and drainage are simulated. Small features below the terrain/data resolution cannot be recovered. This is not an observed-climate benchmark or a reconstruction of Earth's plates. Dataset attribution and checksum are in `src/WorldGen.Core/Data/earth-etopo2022-10min.source.json`.

Use **Show rivers** for discharge-scaled river ribbons; visual widths are exaggerated for global-map readability. Lakes have explicit water surfaces and seasonal water budgets, with glacial provenance retained. Save/load world controls preserve full validated `.world` snapshots (not only recipes), using compressed binary storage. Recipe save/load and JSON export/import remain available. Saved worlds require the matching algorithm version. Compact imports are bounded to 256 MB compressed/4 GB decoded; legacy JSON imports to 250 million characters. Extremely large worlds can exceed these limits. CLI: `--save-world file.world --summary`, then `--world file.world --summary` to load without regeneration.

Custom Map2 features, ecology, civilization/history and local playable terrain remain deferred. Older schema/algorithm recipes/results are rejected explicitly. Hydrology is a coarse global model, not a groundwater, sediment-transport or daily flood simulator.

Native maximum-detail smoke check: `dotnet run --project src/WorldGen.Cli -c Release -- --detail 2560000 --summary --render-check`. This generates the actual fine terrain and both presentation projections without writing a multi-gigabyte full snapshot.

[Algorithms](docs/ALGORITHMS.md) · [Settings](docs/SETTINGS.md) · [Determinism](docs/DETERMINISM.md) · [Validation](docs/VALIDATION.md) · [Dependencies](docs/THIRD_PARTY.md).

Codex command: codex resume --last --sandbox danger-full-access --ask-for-approval never
