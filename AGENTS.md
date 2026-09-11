# WorldGen repository instructions

Read `WorldGen_OneShot_Prompt.md`, `docs/FIRST_BUILD_SPEC.md`, `docs/REFERENCE_ANALYSIS.md` and the implementation contracts in `docs/ALGORITHMS.md` before changing generation behavior. `docs/OROGEN_REFERENCE_MAP.md` records independent use of the pinned behavioral reference; never copy GPL reference code without the owner's explicit license decision.

`src/WorldGen.Core` is the sole authoritative generator and targets .NET Standard 2.1. `Serialization` and `Presentation` are shared adapters. CLI, .NET browser worker and Unity runtime call `WorldPipeline`; reapply/cache/edit changes must not create another generation route. Keep geological provenance and persistent hydrological fields inspectable.

The determinism contract is seed + complete recipe + algorithm version. Use project-owned named PRNG streams, explicit numerical quantization, ordered graph ties/reductions and stage hashes. Avoid System.Random, engine randomness, GPU decisions, wall time and unordered authoritative iteration. Version output-changing algorithms after the initial release. Golden updates must be intentional and reviewed; never regenerate expectations as part of a passing test run.

Current scope: sphere geometry, plate hierarchy, tectonic terrain, moderate erosion and persistent climate-neutral hydrology. Defer climate/seasonal discharge, ecology, populations, settlements, civilization, religion, history and playable local terrain. Preserve `ISeasonalRunoffProvider` as an extension boundary without fabricating season data.

Build: `powershell -ExecutionPolicy Bypass -File scripts/build.ps1`. This compiles the solution, stages worker assets, and copies shared .NET Standard DLLs into Unity. Test: `dotnet run --project tests/WorldGen.Core.Tests -c Release`. Verify CLI reconstruction: `dotnet run --project src/WorldGen.Cli -c Release -- --preset Preview --verify`.

Browser: `dotnet run --project src/WorldGen.Web -c Release --no-build --urls http://127.0.0.1:5080`; then `npm ci`, `node scripts/browser-check.mjs`, `node scripts/worker-check.mjs`. Restart the server after rebuilding fingerprinted assets. Unity is **6000.6.0f1**, URP **17.6.0**, under `unity/WorldGen.Unity`. Run `scripts/unity-check.ps1` and `scripts/unity-check.ps1 -BuildPlayer -PlayerCheck`. Do not run two editor processes against the same project.

Required review: matching all-stage hashes across fresh/reapply/serialized-recipe/cached reconstruction; connected microplates/parents; valid lake levels/outlets, protected inland seas, acyclic receivers and area conservation; settings parity between both faces; no generator code in JavaScript or Unity. `docs/VALIDATION.md` records available-host evidence and unverified targets. Keep generated caches, binary outputs and test screenshots out of source control.
