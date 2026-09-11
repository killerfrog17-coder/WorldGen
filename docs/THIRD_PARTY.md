# Dependencies and licenses

| Dependency | Version / pin | License / use |
|---|---|---|
| .NET SDK and runtime | SDK 10.0.401; browser runtime 10.0.12 | MIT, with runtime third-party notices; builds CLI, Blazor and worker |
| Microsoft.AspNetCore.Components.WebAssembly | 10.0.12 | MIT; browser component/runtime integration |
| Microsoft.AspNetCore.Components.WebAssembly.DevServer | 10.0.12 | MIT; development server only |
| Playwright | 1.58.2, package lock | Apache-2.0; development browser automation only |
| Unity Editor / Windows player | 6000.6.0f1 | Unity terms; existing project preserved |
| Unity URP | 17.6.0 | Unity package license; existing renderer |
| Unity Input System | 1.20.0 | Unity package license; existing runtime input |
| Existing Unity template/package dependencies | `unity/WorldGen.Unity/Packages/manifest.json` and `packages-lock.json` | Retained original packages and their bundled licenses/notices |

The browser uses an independently written small WebGL renderer; it adds no rendering dependency or CDN runtime. There are no generated art assets, AI asset tools, Blender or paid asset dependencies. Existing Unity navigation and tutorial packages were not added by this milestone.

World Orogen (`raguilar011095/planet_heightmap_generation`, GPL-3.0) was inspected at `cc2662b4edd52231c4f65d8765f3ef12cd82d9b7` as an architectural/behavioral reference. Its source clone lives outside this repository. No code, textures, shaders, calibrated constant tables or mechanical translations were incorporated. `OROGEN_REFERENCE_MAP.md` records preserved, modified, deferred and rejected concepts. This document does not assign a license to the owner's new code; the npm tooling package is private and UNLICENSED.

The .NET worker API integration was checked against [Microsoft's Web Worker documentation](https://learn.microsoft.com/en-us/aspnet/core/client-side/dotnet-on-webworkers?view=aspnetcore-10.0). The implementation uses `JSExport`/`JSImport`, a standalone worker runtime, and ordinary browser message passing. Reference-template copyright comments are retained in unmodified template support files.
