# World Orogen alignment - version 3

The owner chose **independently written code; do not translate GPL source**. The supplied World Orogen project was inspected/executed in place as a behavioral reference. No GPL source, shaders, calibrated tables or assets are incorporated. This remains an independent implementation; matching every reference algorithm or identical geography for a seed is not claimed.

Reference location: C:/Users/kille/Desktop/planet_heightmap_generation-main/planet_heightmap_generation-main. The recorded JS hashes identify that snapshot. Older pinned-commit analysis is historical.

## Resolution correction

Inspection of the reference worker confirmed that coarse plate/continent construction stays fixed, but terrain/postprocessing/climate run on the requested detail mesh. The previous WorldGen v2 interpretation (simulate everything at 20k and interpolate a display mesh) was incorrect and has been removed.

V3 keeps exactly 20,000 coarse cells, projects plate/crust identities onto 5,000-2,560,000 fine cells, repairs connectivity and computes physics/boundaries/terrain/erosion/climate on that mesh. Detail changes actual coastlines, heights and climate, while World.CoarseHash stays fixed. Irregularity belongs to fine generation too.

## Requested behavior corrections

| Behavior | V3 implementation |
|---|---|
| Long loading without UI stalls | Core and mesh preparation on a Unity worker, bounded per-frame upload, visible progress and Cancel through presentation |
| Detail changes the world | Fine simulation arrays have exactly Recipe.Detail cells; no display resampler |
| Excess lakes/custom hydrology | Solver removed; controls, overlays and CSV drainage columns removed; only temporary erosion routing remains |
| Crisp low-detail surfaces | Separate dual-cell fans and per-cell colors; continuous Heightmap values |
| Faster zoom | Exponential distance/scale changes, supports normalized or raw wheel units |
| North stays north | Yaw and clamped pitch, no accumulated roll, poles stop at +/-89 degrees |
| Window and panel sizing | Resizable standalone window, F11/fullscreen button, draggable sidebar |
| Smooth longitude | Translation/wrap instances and shader clipping, no regeneration or reprojection on drag |
| Wireframe / Show plates / grid | Dual edges; plate borders in every layer and plate colors in Terrain; Off/15/30 grid with 30 default |
| Browser | Same settings/Core/Presentation in .NET worker, small UI summary and chunked rendering |

Plate frontier growth, continent budgets, mantle/torque heuristics, geological provinces, noise, oriented ridges, sculpting and two-season climate remain independently implemented, with their own coefficients. Fine projection uses connected weighted growth rather than reproducing reference source. Heights use integer physical metres, not the reference's normalized raw elevation/nonlinear conversion. Coarse pole closure is included inside exactly 20,000; the reference's 20,000 request adds one closure region.

Persistent hydrology and Map2 escape/dead-end behavior are deferred. Legacy model names do not enable another generation route. The 500-plate stress preset exceeds the reference UI's 120 limit. Climate is currently eager at all details; the reference defers it above 300k. Planet codes, plate painting, heightmap import, wind/current arrows and tiled 65k PNG export are still absent; recipe/result JSON, CSV and 16-bit PGM remain the export interfaces. These are outstanding parity gaps, not claims of completion.

## Reproducible comparison

```powershell
npm ci
node --experimental-vm-modules scripts/orogen-reference-check.mjs C:/Users/kille/Desktop/planet_heightmap_generation-main/planet_heightmap_generation-main
```

The probe evaluates the supplied reference in memory, maps its public Delaunator dependency to the pinned development package, and writes only to ignored `artifacts/orogen-reference`. It does not copy or modify the reference source. At seed 73421, N=20,000, P=80 and the reference defaults, the supplied reference produced 20,001 regions, 26.8037% land cells and 27 distinct climate codes including ocean. A further run at N=40,000 produced 40,001 regions, 26.7543% land cells and 25 climate codes: the reference recomputes world fields as detail changes, rather than only subdividing its coarse cells. The probe accepts optional detail as its third argument. This is a behavioral comparison, not an output-equivalence test. Native and cross-host WorldGen results are recorded separately in VALIDATION.md.

