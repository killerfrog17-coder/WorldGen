# River incision correction —20October2026

## Failure

The local hydrology3 cross-section returned a prescribed bed/bank elevation
even when it was ABOVE the existing terrain. Its water levels were linear
between coarse control endpoints, while the actual path/ground was curved and
included cubic relief and fine noise. The resulting supporting walls were real
raised ground, not merely blue water shading. A prior test wrongly required
high dry banks even when the surrounding original ground was below the water.
That assertion has been replaced, not retained as acceptance evidence.

## Correction

Local hydrology4 /terrain7 introduces two distinct safeguards:

1. The entire river/valley cross-section is `min(original, carved envelope)`.
   It cannot raise land at the bed, bank or outer valley boundary. Standing
   lake/island prototypes remain separate; island terrain intentionally rises.
2. Before exploration, sample the actual uncarved terrain along the curved
   river path, including both bank sides. Lower the longitudinal profile to
   fit those constraints, check intermediate dips, and reconcile tributaries
   with shared node ceilings in a source-first drainage pass. Final sampled
   levels never rise downstream; each confluence has one shared endpoint.

The terrain sampler has an internal dry-patch path so conditioning does not
recursively sample its own carved channels. Patch plans are reused by source
region; up to four worker lanes perform this bounded preparation. Existing
point queries read compact profile arrays, rather than tracing upstream or
running erosion. Terrain mesh segments align with profile breakpoints.

`PreWaterElevationM` exposes the uncarved sample for diagnostics; it is not
coarse `BaseElevationM`. River profile lowering and preparation time are also
reported. No extra per-cell history work, save-format/history rule changes or
new saved raster. Around20MiB of additional managed profile data is retained
in the actual200k Earth diagnostic (total derived terrain263.9MiB).

## Cost / remaining limits

The preceding build's scaffold was1.4–1.7s CLR, with all terrain preparation
2.9–3.3s, about16–17% of17–21s full generation+preparation. Its native scaffold
alone was3.44s; those two runtimes must not be silently combined as a native
whole-generation benchmark.

New Earth CLR measurements:15.86s complete terrain preparation,11.84s of it
river conditioning. The largest measured old-profile correction was578.1m.
The updated full generated-world and native review evidence is in VALIDATION.md.

This corrects embankments; it is not a complete new river-routing solver.
Profiles remain bounded sampled approximations, not an analytic proof of
clearance at every infinitesimal point. Narrow channel geometry, long linear
reaches, valley width, specialist landforms and final water materials still
need visual refinement. Deep necessary incision can expose a canyon; no attempt
is made to disguise it by lifting terrain or to claim all canyons look natural.
Global scaffold spill conditioning and existing lake/shore prototypes are
unchanged by this focused correction.
