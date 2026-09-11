# Milestone validation evidence

Validation performed on Windows 10 x64 on 10 September 2026, using .NET SDK 10.0.401 / runtime 10.0.12, installed Chrome with Playwright 1.58.2, and Unity 6000.6.0f1 with the Windows Mono player backend. Logs, screenshots, exports and executables are local build artifacts rather than checked-in binary source.

## Executed checks

| Check | Result and evidence |
|---|---|
| Shared solution / .NET Standard libraries / CLI / Blazor / worker | Release builds succeeded, zero warnings and errors. `scripts/build.ps1` stages worker assets and Unity DLLs. |
| Core regression/property harness | **389,377 assertions passed**. Closed topology/Euler characteristic, actual sphere area, symmetric adjacency, positive cell areas, exact counts, connected microplates and parent affinities, contested eligibility, ghost identities, normalized province weights, protected bathymetry, receiver adjacency/order, lake surfaces/single outlets, terminal basins, contributing-area conservation and corpus hashes. |
| Synthetic hydrology | Nested bowls and merge saddles; common spill level; stable equal-height decisions; protected inland reservoirs; drainage-area conservation. A discovered equal-level lake outlet cycle was fixed by selecting the earliest original topological outlet. |
| Recipe / result formats | Complete recipe and verified result round trips, unsupported version rejection, incomplete recipe rejection, fresh/reapply/recipe/cache reconstruction equivalence. |
| CLI | Preview generation approximately **0.9 s** in the recorded final run; the 500-microplate corpus case approximately **1.9 s**. Times are observations, not performance guarantees. Complete JSON, CSV and 16-bit PGM exports generated. |
| Browser UI in real Chrome | Automatic generation, exact CLI stage hashes, repeat/reapply, recipe save/load/reconstruction, full result and PGM downloads, all 23 diagnostic layers, sea-level preview, surface picking, cancellation during active 500-plate computation, desktop and mobile layouts. `scripts/browser-check.mjs`. |
| Browser worker cross-host corpus | **All 9 stage hashes match CLI for all 8 recipes**, including 500 microplates. `scripts/worker-check.mjs`; `artifacts/browser-worker/*.hashes.json`. |
| Unity editor | Batch compilation and `WorldGenBatch.Validate`: all corpus stages match CLI; repeated generation / recipe round trips / verified snapshot serialization pass. `artifacts/unity-validation.log` and `artifacts/unity-editor/`. |
| Unity Windows player | Built successfully; launched at runtime with `--worldgen-check`; all corpus stages match CLI. `scripts/unity-check.ps1 -BuildPlayer -PlayerCheck`; `artifacts/player-check.log` and `artifacts/unity-player/`. |
| Unity render | Windows D3D11 / RTX 4060 offscreen camera render produced and visually inspected. `artifacts/unity-player.png`. The first hidden-window swap-chain capture was black; explicit render-to-texture capture fixed the inspection path. |
| Static web deployment | Release `dotnet publish` succeeded to `artifacts/web-publish/wwwroot`. `scripts/published-check.mjs` exercises HTTP static hosting and the mid-generation dirty-settings regression. |
| Final source review | Single pipeline, no authoritative host-specific fallback, named PRNG streams, explicit ordered reductions, all effective fields in the shared metadata, no deferred simulation systems, no copied GPL source, ignored generated outputs. Authored-source whitespace check passes. |

## Golden corpus

Each file under `tests/golden` includes the complete recipe, all expected stage hashes and metrics. The final hashes below identify the tested release algorithm:

| Recipe | Final SHA-256 |
|---|---|
| ordinary | `f09ec67e423644266b3e1def8e63273c6cc5b0bca8a76e6c786221c2273f0d4c` |
| connected | `6e4fb9ec0d67b80e8bb3c970f48e67511a712328645729933a278a89fbe6a203` |
| isolated | `eab21a3416021fa1acca730753b335aecf436b73fa23ad29255aa4c92df4e6c1` |
| escape-face | `0ee7a03927234a85d58628f7112bc924383af68aea6dccd0cd17b2a22d3d82c2` |
| dead-end | `1f55baefc20ac871b4d265bafa90c5091b8dd8ee056acbec039187bd82769871` |
| inland-sea | `a05c20582c3181c28be8133042ba91d6b71144ec0b93e7d64a5a61b66eb90d22` |
| glacial-basin | `7f5b8f74557165e8186a38537a262d2ef1811d2fdc8b4f89006150c289fcc4e0` |
| 500-microplates | `724a9d093e9af2da457e09c2b4fb1e2ca5602c35f45e41ff48e16ee432589ae3` |

## Visual inspection and limits

Browser desktop, parent-plate, water and mobile screenshots are in `artifacts/browser/`. Inspection prompted removal of default river clutter (rivers have a dedicated layer), relief-normal shading and a focus-outline fix. UI files remain available as a runtime menu in the built Unity player; the automated Unity capture inspects the **globe**, not the complete IMGUI interface. Manual end-user interaction with the Unity menu was not automated.

The worker is interpreted WebAssembly and is substantially slower than native generation: the 500-microplate case takes minutes in this build. One early 180-second test deadline expired during computation; extending the stress-test allowance allowed completion and matching hashes. Generation stayed off the UI thread, and cancellation was tested during computation. Complete JSON validation / mesh preparation / exports on the Blazor side can briefly block the UI after generation. AOT and typed transfer buffers are future performance work, not a second algorithm.

IL2CPP, Android/iOS/ARM, macOS/Linux Unity players, Firefox and Safari were not executed. Run the documented corpus before adding them to the supported parity claim. No physical-geology calibration or seasonal hydrology validation is claimed. Macro resolution limits narrow straits, small lakes and ridge spacing; continents/land coverage remain target tendencies with measured results. The fixed-range PGM clips values outside −12,000 to +16,000 m; complete JSON and CSV preserve the full field.

## Reproduce the static and rendering checks

```powershell
# After build/publish, in one shell:
python -m http.server 5081 --bind 127.0.0.1 --directory artifacts/web-publish/wwwroot
# In another shell:
node scripts/published-check.mjs

# A render-only screenshot from the built player (no visible helper window required):
$run = Start-Process artifacts/player/WorldGen.exe -WindowStyle Hidden -PassThru -ArgumentList '-screen-width 1440 -screen-height 1000 -screen-fullscreen 0 --worldgen-screenshot artifacts/unity-player.png -logFile artifacts/player-visual.log'
$run.WaitForExit()
```

Use absolute screenshot/log paths when launching from another working directory. Setup, all-host test commands and expected files are in `README.md` and `DETERMINISM.md`.
