# Rejected experimental boundary inflow, worldgen3.33

Status: **do not promote**. This isolated `experiment/monsoon-inflow-333`
worktree branches from1295a82549bac5028118673b05772cc745900b30. Main/released
worldgen3.32 is unchanged. The prototype demonstrates a missing mechanism,
not an accepted climate improvement. No GPL reference code was used, no learned
weight was changed and no Earth coordinate affects generation.

## Mechanism and units

The existing seasonal thermal-pressure field makes a nondivergent geostrophic
basin circulation. This trial separately adds down-thermal-pressure boundary
flow: `u=-r*grad(p)/(rho*(r*r+f*f+f0*f0))`, with `rho=1.2kg/m3`,
`f=.000146*sin(latitude)` in1/s, `r=1/(DragHours*3600)` in1/s, and
equatorial numerical regularizer `f0=.000015/s`. Pressure gradients use
neighbor least squares inPa/m on the existing roughly1800km smoothed
land/ocean thermal field. Only this ageostrophic term is added: no duplicate
geostrophic term and no second forcing by the background Hadley pressure.
One global scale bounds the additional physical speed at6m/s. Core's existing
wind vectors use5m/s per unit.

The new component belongs to ordinary wind, not the exact streamfunction
component. Existing finite-volume paired face fluxes therefore carry its
convergence into the existing finite-column moisture solver. Ocean evaporation,
condensation and limited recycling retain their former equations. It does not
grant latitude-dependent rain or an inland moisture floor.

`Recipe.BoundaryInflow` is an optional nullable profile (`DragHours`, `Strength`).
Null means fixed3.33 defaults24hours/unit strength, not an external mutable
configuration. Explicit profiles are validated, cloned and hashed; old algorithm
versions require null, whose new field is omitted from canonical hashes and is
optional in legacy recipe JSON. Versions3.33 and3.32 are separately identified;
`PreviousAlgorithm` plus version helpers preserve3.32's shape presets, continent
limit, terrain baseline, subcells, ecology, percentage controls and validation.
No saved world is regenerated during loading.

## Compatibility and mechanism checks

The `tests/WorldGen.ClimateExperiment` observer harness calls only WorldPipeline
for actual world generation. Its optional baseline DLL reference lets frozen
1295a82 output be compared with candidate output without changing expectations.
It is a test/diagnostic host, not another generator.

Reproduction uses the ordinary project references by default:

```powershell
dotnet run --project tests/WorldGen.ClimateExperiment -c Release -- mechanisms
dotnet run --project tests/WorldGen.ClimateExperiment -c Release -- trial Earth 73421 200000 worldgen-3.33.0 6 2 artifacts/monsoon/earth-6h-2
dotnet run --project tests/WorldGen.ClimateExperiment -c Release -- flow artifacts/monsoon/earth-6h-2/final.world artifacts/monsoon/earth-6h-2
```

For legacy comparison, build Core and Serialization from1295a82 into an isolated
directory, build this harness with `-p:CoreBin=<that-directory>`, and run its
`baseline <control-folder>` mode. Rebuild against the experimental source and
run `compat <control-folder>` without modifying the saved controls. This
compares actual frozen outputs rather than regenerating golden expectations.

Six3.32 controls at5000terrain cells cover Earth, one continent/50% land,
six continents/30% land, Donut,35continent Archipelago and an oceanless shape.
Every stage hash matches the frozen baseline across fresh, reapply, serialized
recipe and cached reconstruction:24comparisons passed. Baseline complete hashes:

| Control | Complete hash |
| --- | --- |
| Earth |f017231e9c26e69ebc81980ca6741289b19d5ffdc755adc3a23c541bcf776e71|
| One continent |fcfed9f2e67a4d6ee2a75ba15096bff8c77e99c46a30ff87b29097df9ecca2d2|
| Six continents |4a26d01db9a50966ab99b049027d804ee86049a3472d49f0d0767d509ca41c27|
| Donut |d277dff8d92bd81b7e94f780da75ed72fed417eb88e618812ee575c6b8b6279f|
| Archipelago |4f30c661d37c3963342892c2d9f9c3395b85ac2804e207229f549ebad0a28199|
| Oceanless |b540786d0396564ad347e55ef2671bef6d6b8cff677dbb45b93b9b78f736a918|

A3.33Earth control also passes all four reconstruction paths. Actual200kEarth
and two procedural before/after worlds retain identical raw coarse/fine
geometry, plates, elevations and ocean mask; subsequent climate/hydrology/ecology
may change as intended. An old full historical3.32Earth snapshot loads and
validates without rewriting its integrity.

Twelve focused mechanism assertions pass: down-gradient direction, zero-gradient
zero forcing, mirrored hemispheric mobility, seasonal-gradient reversal, finite
equatorial response, actual spherical thermal-coast reversal, bounded speed,
monthly finite-water residual below1%, nonnegative rain, oceanless zero-source
dryness, cancellation and zero-strength exact old climate arrays. The inspected
Earth/procedural trials have maximum monthly local-budget residuals below
0.1%. Passing these numerical checks does not establish geographic accuracy.

## Geographic results: rejection

Earth uses seed73421 and200k terrain cells. Values below are millimetres/year at
the same nearest terrain cells; all original ten learned weights are retained.

| Point |3.32 baseline|24h,strength1|6h,strength2|24h,strength4|
| --- | ---: | ---: | ---: | ---: |
| Delhi |102|78|258|75|
| Kolkata |159|163|786|174|
| Mumbai |367|540|823|586|
| Sahara interior |118|150|375|162|
| Australia interior |524|647|1389|682|
| Congo |1129|501|394|340|
| Amazon |1749|1460|839|1270|
| Paris |756|755|751|755|
| Berlin |556|554|555|554|
| North China plain |971|970|937|965|

The stronger6h/strength2 response finally reverses summer flow toward India,
but makes Australia excessively humid and Congo's interior a hot desert while
also drying western Amazonia. The independent screenshot critic reviewed the
actual paired PNGs and rejected promotion for these material holdout regressions.
The weaker cases also fail: India remains dry and Congo is worsened. More
strength with24hdrag mostly activates the global cap rather than solving India.

At peak northern summer, the6h/strength2 addition supplies northward2.56m/s at
Delhi,3.12at Kolkata,3.44at Mumbai and3.50over the Bay of Bengal. Their thermal
pressure gradients northward areâˆ’.283/âˆ’.273/âˆ’.249/âˆ’.206Pa/km. The unbounded
global maximum7.17m/s is scaled by.8364to satisfy the6m/s limit.24h/unit
strength supplies only.53/.72/.90/1.11m/s at those locations, while its
near-equatorial Congo response already exceeds5m/s. This is a real spatial
response difference, not a uniform rainfall multiplier.

Two200kprocedural holdouts were also generated and their actual climate maps
inspected. A one-continent/50% land seed42 retains a dry deep interior:
mean land rain333â†’328mm and arid-cell share62.6â†’63.6%; Af share halves.
A six-continent/30% land seed261015 becomes wetter (850â†’978mm), arid share
25.2â†’16.2%, yet Af share also roughly halves. These are redistribution effects,
not evidence that the Indian improvement generalizes.

## Deeper diagnosed forcing limitation

The lowest thermal-pressure cells are around37.6N/84.8E (âˆ’549.8Pa), north/east
of Thar. Nearby high cells at37.8N/88.0E and36.1N/85.4E retain equally deep
lows despite simulated June temperaturesâˆ’.9C andâˆ’11C. Thar's sampled low is
onlyâˆ’382.7Pa. The forcing is smoothed land/ocean geometry times season and
sine latitude, not an actual near-surface or column-heating calculation.
Adding friction to that field does not repair its heat-low placement or the
coupled Hadley/convection/recycling balance. Actual highland surface temperature
alone is not an adequate replacement for vertically integrated heating either.

The learned frontal-rainout multiplier79.873156 compounds this concern: a
structural circulation correction interacts with a very strongly fitted sink.
Do not blindly refit all coefficients to Delhi or compensate historical
settlement rules for the baseline's unrealistic Indian deserts. A future climate
pass needs physically consistent heat/pressure/moisture feedback and broad
holdouts before another generation version could be promoted.

Artifacts are outside source control at
`D:/WorldGenBuildArtifacts/experiments/monsoon-artifacts/`: frozen binaries,
legacy controls, `earth-*`, `one-*`, `six-*` saved worlds/reports and paired PNGs.
Diagnostic maps render actual barycentric-dual climate values; the compact test
palette groups C families together and D families together, so detailed class
comparisons use the complete report, not color alone. No assets or screenshots
are committed. This worktree is an explicitly authorized disposable experiment,
not a routine project backup.
