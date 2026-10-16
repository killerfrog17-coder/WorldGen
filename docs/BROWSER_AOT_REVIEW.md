# Optional compiled browser worker

The normal solution build is unchanged: the browser worker uses the .NET WebAssembly interpreter/Jiterpreter. The release-only `WorldGenWorkerAot=true` profile compiles the same managed worker ahead of time. It does not move generation into JavaScript, skip save checks, alter recipes, or change historical rules.

## Reproduce

With the .NET 10 `wasm-tools` workload installed:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/publish-worker-aot.ps1 `
  -OutputDirectory artifacts/browser-worker-aot `
  -BuildArtifactsDirectory D:\WorldGenBuildArtifacts\wasm-aot-current
```

Both directories are generated build products. The optional D-drive directory is a disposable compiler cache, **not** a project, music, model, or saved-world backup. The script does not replace the regular staged worker or the Unity player. Its publication manifest records the starting source revision, dirty-tree flag, actual compiled input DLL hashes, build duration and asset sizes. A first native compilation can take several minutes; ordinary development builds do not incur that cost.

Test without replacing the running app's assets:

```powershell
$env:WORLDGEN_WORKER_ROOT=(Resolve-Path artifacts/browser-worker-aot/wwwroot).Path
$env:WORLDGEN_WORKER_CHECK_OUTPUT='artifacts/browser-worker-aot-check'
node scripts/worker-check.mjs
node scripts/living-world-browser-check.mjs path/to/control.world artifacts/browser-aot-ui
```

The harness serves only that test browser's `/worker/_framework/` requests from the chosen publication. The app and worker wrapper still come from the normal local server. No second generator or server-side simulation is introduced. Clear `WORLDGEN_WORKER_ROOT` to test the normal worker again. Optional `WORLDGEN_WORKER_RECIPES` supplies a separate directory of complete recipes and independently established expected stage hashes; it never rewrites repository goldens.

The SDK requires trimming for AOT. The profile therefore explicitly roots the complete Core, Serialization, Presentation and DelaunatorSharp assemblies and retains IL for reflection fallbacks. This is essential for saved data contracts, canonical fields and atlas records. Do not remove these roots to reduce download size without a separate compatibility review. Trimming warnings are not proof of compatibility; actual import, continuation and fresh-generation checks are required before adopting a particular publication.

## Measured control, 16 October 2026

The independent experiment directly referenced the frozen `round-h-bin` assemblies, rather than rebuilding an evolving source tree. The same 19,675,034-byte Pangeae save contains 20,000 cells and an already-running history. Headless browser tests used the same local app server; these are local-host observations, not internet-download or high-detail guarantees.

| Phase | Native frozen H | Browser interpreted H | Browser compiled H |
| --- | ---: | ---: | ---: |
| Base64 decoding | not applicable |3.28s |0.10s |
| Gzip/data-contract deserialization |1.46s |57.84s |3.86s |
| Terrain integrity hash |0.71s |76.14s |1.90s |
| Historical geography |0.07s |0.46s |0.12s |
| Historical fingerprint/accounts |0.61s |22.35s |1.32s |
| First cached geometry |not part of native load |8.52s |0.97s |
| Import dispatched to usable world |not applicable |171.60s |10.64s |

Native full load/validation took 2.89 s. No historical event processing was hidden in these import phases. The dominant browser costs were data-contract decoding and canonical hashing under interpreted managed execution, not a recurring population census.

The compiled experiment passed actual browser atlas checks for resources, political influence, public priorities, local ancestry appearance, organization membership/finances, 1024×768 layout and read-only lore. Its world hash was `68bdfe014bdc59260913ed5071c3454811ce9e1fee10c1e17867f9f739edf0e6`; strict import also verified the stored historical fingerprint `eec5e61db94de32664f55cb7b5fc22ac4f804d1666cc8996c07c9d9dcfe561c8`.

An actual one-year continuation through the compiled worker, followed by exported snapshot validation in frozen native H, produced exactly the independent native continuation fingerprint `0bb29508faf48e825b907449bfd7679c1b98bd6d30099cbfb7c7721ba140faa1`. The 3,781,470-character initial browser chronicle also matched native text after normalizing Windows line endings: SHA256 `d2201759065077bf42c5f6cb21c80a3990c8979d1e7ef00525e3cb2175463509`.

The compiled native blob grew from 2.86 MiB to 17.97 MiB, but trimming reduced other framework files: total framework `.wasm` files were 27.44 MiB compiled versus 28.06 MiB interpreted. All framework gzip files totaled 12.57 MiB versus 14.06 MiB; this includes ancillary assets and is not an exact network-transfer measurement. Native compilation used roughly three minutes on this host. Artifacts are under `artifacts/history32/browser-h-pangeae-aot`, with the temporary native compiler cache under `D:\WorldGenBuildArtifacts\wasm-aot-h`.

The existing repository v3 goldens use unsupported `worldgen-3.22.0` recipes and are intentionally unchanged. Their rejection must not be reported as a passing current-generator AOT test. Current-version cross-host controls, where recorded, are derived and checked outside `tests/golden`; they do not silently replace reviewed expectations. Maximum-detail browser memory, Firefox, Safari, and exhaustive simulated histories remain unverified by this control.

## Actual L source publication and boundary regression

The real publication script subsequently built the captured L source, before the independent M/N historical experiments, in 177.13 s. Its Core input DLL SHA256 was `1cf150c11621be2b80c5369582832b851d606b3c7b7a29befcc1531f4dac8063`; do not identify that dirty-tree candidate solely by a later repository HEAD. Publication: `artifacts/history32/wasm-aot-l-publish`; disposable compiler cache: `D:\WorldGenBuildArtifacts\wasm-aot-l`.

Eleven independently generated native H current-version controls all matched the actual L compiled browser, across all 165 stage records. Cases cover high/sparse plates, continents, Earth, glaciation, interconnection searches, mostly land, mostly ocean and completely ocean. These current `worldgen-3.32.0` controls live only in ignored artifacts, not the older reviewed golden directory. Browser results and timings are under `artifacts/history32/browser-worker-aot-l-current-controls`.

The completely ocean case exposed a pre-existing presentation boundary bug: the accepted world has no denominator for `continental_below_zero_fraction` and `inland_below_zero_fraction`. Its summary emitted `NaN`, which strict browser JSON rejected. The old checker cleared its timeout before parsing, leaving a permanently unresolved promise. The display adapter now omits undefined diagnostics and explains their omission in a copied warning list; accepted world diagnostics and hashes are untouched. The checker rejects parse errors promptly rather than masking them or hanging. The actual all-ocean compiled-browser case then passed all 15 hashes in 19.94 s.

Actual latest atlas checks against the L2 finance-bearing save passed at `artifacts/history32/browser-l2-finance-aot-final`, including the new operating-budget page, compact resource layout, 1024×768 reader and read-only chronicle comparison. The final map screenshot explicitly waits for completed geometry; its recorded view is flat map, climate background, `loading=false`. Earlier captures made while a view was still loading are not default-map visual evidence.

## Primary runtime references

- [Microsoft: browser build tools and AOT](https://learn.microsoft.com/en-us/aspnet/core/blazor/webassembly-build-tools-and-aot?view=aspnetcore-10.0): CPU-intensive managed code can benefit from AOT, with publication and deployment tradeoffs.
- [.NET runtime browser SHA provider](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Security.Cryptography/src/System/Security/Cryptography/HashProviderDispenser.Browser.cs): synchronous browser SHA uses the managed provider. The measured hash time includes both canonical serialization and SHA processing; the experiment did not separately attribute every millisecond to cryptography.
- [.NET Jiterpreter design](https://github.com/dotnet/runtime/blob/main/docs/design/mono/jiterpreter.md): explains partial runtime compilation available to interpreted builds. AOT is not a change to the simulation algorithm.
