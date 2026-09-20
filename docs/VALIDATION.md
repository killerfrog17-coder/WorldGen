# Version 3 validation evidence

Host: Windows x64, .NET SDK 10.0.401, Unity 6000.6.0f1 / URP 17.6.0 (Windows Mono player), Chrome / Playwright 1.58.2. Tests were run on 17 September 2026 local time. Schema 3 / worldgen-3.0.0 replaces v2's incorrect display-only detail interpretation.

## Recorded evidence

- Release solution and worker build passed. Shared Core/Serialization/Presentation DLLs and the MIT triangulation dependency were staged into Unity.
- Core suite: **5,117,170 assertions passed**, eight fixed v3 recipes. Topology and spherical area, connected plates/superplates, per-cell provenance, climate bounds, no persistent hydrology, strict serialization, cancellation, flat cell colors, overlays, map seam/pole coverage and immutable presentation passed.
- Fresh/repeat/reapply/serialized-recipe/cached reconstruction match all **10** stage hashes. Explicit CLI Preview --verify passed again on the final hash implementation (2,199 ms native generation in that run).
- CoarseHash stayed equal when detail changed from 20,000 to 5,000 and irregularity from .75 to .1. Actual fine terrain array length changed; over 500 low-detail sample heights differed by more than 25 m from nearest high-detail samples. This rejects the old display-resampling behavior.
- Default **204,000** native terrain: generation 13,207 ms; globe preparation 239 ms; map preparation 1,274 ms in a recorded run. World land area 38.0154%, height range -7,399 to 6,712 m. All terrain/climate arrays use the fine mesh.
- Maximum **2,560,000** native terrain: generation **165,146 ms**; globe 939 bounded pieces / 46,079,964 vertices in 3,127 ms; map 939 pieces / 46,110,822 vertices in 15,883 ms. Land area 37.6634%, height range -7,328 to 6,823 m. Run: `--detail 2560000 --summary --render-check`. A second run after the output-preserving climate optimization completed generation in **114,213 ms**, globe preparation in 3,484 ms and map preparation in 17,546 ms, with the identical final hash `088007b8ea35548a74361008a1459864f6dcd8b9654f5758abd2ef402812786a`. These verify CPU generation and presentation, not a maximum-detail browser session.
- Unity Windows player built successfully and matched every CLI stage for all eight v3 recipes. Evidence: artifacts/unity-v2/artifacts/player-check.log and unity-player/*.hashes.json. The folder name reuses an existing isolated Library cache; its contents are the newly built v3 player.
- A 20,000-cell Unity Terrain capture completed and was inspected: crisp distinct cell surfaces, no interpolated-color blur. The first capture exposed excessive grid height, subsequently corrected to follow terrain height.

- Unity editor validation passed all eight recipes, every CLI stage and recipe/snapshot round trips in the isolated project.
- The final Unity player passed all eight CLI hash sets after the climate and hash optimizations. The isolated player is artifacts/unity-v2/artifacts/player-final/WorldGen.exe. The same build is staged at **artifacts/player/WorldGen.exe**; all 325 copied files matched their source hashes, and launch/generation/map capture passed from that normal location.
- All eight Unity Globe/Map x Terrain/Satellite/Climate/Heightmap captures completed at 20k terrain and were refreshed after the final pole-grid correction. Visual inspection confirmed crisp cells and straight map meridians/parallels. Additional Terrain/Satellite captures with wireframe, plate borders and a 15-degree grid confirmed plate colors only replace Terrain colors. A regression assertion covers pole-grid alignment.
- 20k Unity interaction validation: complete regeneration and map presentation, cancellation during presentation **17.758 ms**, longest busy frame **19.4396 ms**, longitude frames **16.7282 ms** average. Window resize and north-up checks passed. The actual Unity window answered all **40** Windows message probes, maximum **48 ms**, with zero timeouts. Evidence: artifacts/unity-ui-v3/detail-20000.json and window-20000.json. The probe initially selected an unrelated helper window; filtering for the player window fixed that test defect.
- Unity **2,000,000-cell** interaction validation **passed** generation, cancellation during presentation, full regeneration, map presentation, longitude scrolling and resizing. Across **41,901** busy frames, the longest was **58.3871 ms**; cancellation took **17.7675 ms**; longitude frames averaged **16.7427 ms**. The north-up and resize checks passed. The actual player window answered all **1,359** Windows message probes, maximum **196 ms**, with **zero timeouts**. Peak process working set was **2,611,257,344 bytes**. Evidence: artifacts/unity-ui-v3/detail-2000000.json and window-2000000.json. An earlier run also completed, but the separate probe's Int32 memory counter overflowed above 2 GB; the final successful probe uses Int64 counters.
- Browser full UI suite passed generation/repeat/saved-recipe hashes, recipe/result/PGM exports and mobile overflow. Stricter view checks exposed a test defect: this Playwright version treats an asynchronous wait predicate's Promise as truthy before its result is known. The final check imports the view inspector once and uses a synchronous predicate for the requested projection/layer and completed revision. That final interaction suite **passed** all 23 diagnostic layers, all eight main views, longitude without a new mesh revision, wireframe/plates/grid options, actual cell inspection, mid-generation cancellation with the previous world retained, and the 390-pixel mobile layout.
- Chrome worker: all eight cases match all **10** stages on the final implementation, including the specialized canonical hash writer; recorded times **106,134-153,712 ms** with two worker pages. Packed byte buffers replaced JSON mesh transfer. Evidence: artifacts/browser-worker/*.hashes.json and v3-timings.json.

## Golden review

Candidates were explicitly generated into artifacts/v3-candidate-corpus, inspected for plate count, terrain size, height bounds, land coverage and the structural tests, then copied into tests/golden/v3. Passing tests never regenerate expectations. All corpus cases intentionally use 20k terrain for portable regression coverage; separate resolution tests cover coarse invariance and changing fine terrain, and the maximum-detail smoke exercises 2.56m. V1/V2 fixtures remain historical.

The ordinary final hash is `2ccfec96a725dbabdd174fd226fcb855d17ca3628dd1ff4b7865ed9e4af076fa`. The full set of stage hashes is in tests/golden/v3. Algorithm outputs are intentionally different from v2 because terrain runs at actual detail, the persistent hydrology stage and collision-context route are removed, and the stage hash format is chained.

## Practical limits

This is independently written C#, not a source translation or numerically identical World Orogen port. The reference comparison and outstanding parity gaps are in OROGEN_ALIGNMENT.md. In particular, climate is still eager at high detail; editing/import/planet codes and extended export modes are absent. Old recipes are explicitly rejected rather than silently reinterpreted.

Chrome uses interpreted .NET WebAssembly and remains slower than native Unity. Native 2.56m success does not establish browser memory/performance at that detail. Full JSON result export is expensive; the high-detail smoke deliberately uses --summary. IL2CPP, ARM/mobile, macOS/Linux players, Firefox and Safari remain unverified. Timing observations are not guarantees.

Unity cancellation is cooperative. Presentation and project-owned loops check it frequently; an in-flight triangulation or framework array sort finishes before observing cancellation. These operations run on the worker, so the loading UI remains available.

## Reproduce

Use README.md's build/core/CLI/browser commands. For an already-open Unity project use `scripts/unity-check.ps1 -Isolated` and `-Isolated -BuildPlayer -PlayerCheck -PlayerFolder player-final`. Never launch two editors against one project. Automated players use Start-Process -WindowStyle Hidden.

Player flags: --worldgen-detail N, --worldgen-view Globe|Map, --worldgen-layer Terrain|Satellite|Climate|Heightmap, --worldgen-wireframe, --worldgen-plates, --worldgen-grid 0|15|30, --worldgen-screenshot <absolute PNG path>. Run `scripts/unity-interaction-check.ps1 -Isolated -Detail 2000000` for the actual window-message probe. The player harness uses --worldgen-ui-check <absolute JSON path> and exercises presentation cancellation, complete regeneration, map scrolling and resizing while recording frame latency.

Generated binaries, logs, captures, candidate corpora and reference results are ignored artifacts. The supplied reference remains outside this repository and is never modified.
