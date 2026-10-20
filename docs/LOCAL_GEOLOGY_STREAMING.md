# Local geology and continuous terrain streaming

Owner-authorized19 October2026. This is the geology stage, not the subsequent
landform, local hydrology, seasonal ground or vegetation overhaul.

## Physical model

`local-geology-1` combines the existing synthetic six-family parent lithology
with soil burial, gravel content, weathering, actual rendered slope and two
planet-addressed metre-scale noise fields. Deep stone-free deposits can have
no boulders; exposed provinces contain clustered stones/boulders and occasional
larger outcrops. Fields are queried locally, not by a recurring world census.
The underlying `local-terrain-preview-3` height function is unchanged.

Six lithology mixture channels blend through the existing compact cell kernels.
They are derived descriptors, not new saved soil surveys, agronomic modifiers
or historical rules. Earth lithology is still synthetic, not measured geology.
`local-scatter-preview-3` gives rocks lithology-specific variants, joint-conditioned
physical sizes, broad outcrop proportions and rock palettes. Plants retain the
preceding placement rules. Loose-rock density is not a talus transport simulation.

Granite placeholders are relatively rounded, basalt blockier, sedimentary rock
layered and metamorphic forms sheared. Render-only PBR detail supplies grain,
bedding and joints. Default arrays contain the preceding eight substrate layers
plus six rock detail layers; custom eight-layer arrays still work through their
generic rock layer. UV4/UV5 carry six lithology weights. Existing art bindings
can replace these placeholders without changing placement.

Ground inspection and footstep material selection share the same exposed-rock
surface. Existing quiet, centered AI stone contacts are reused; no new synthetic
audio, wind or rain changes are part of this pass. The prototype still has no
prop collision/overhangs: large outcrops are scenery, not authoritative hills or
building sites. Actual connected cliff/valley shapes belong to the next landform
pass. Sizes/jointing are plausible illustrative priors, not reconstructed parks.

References informing the distinction:

- [West Virginia State Parks: Coopers Rock](https://wvstateparks.com/parks/coopers-rock-state-forest/park-history/): sandstone cliffs and boulders.
- [Texas Parks: Enchanted Rock guide](https://tpwd.texas.gov/publications/pwdpubs/media/pwd_br_p4507_0119r.pdf): granite domes and exfoliation.
- [NPS: granite weathering](https://www.nps.gov/places/geological-trail-station-1.htm): joints, exfoliation and weathered debris.

## Streaming: rollback baseline plus prepared frame handoff

The owner rejected both geology-era streaming attempts. Commit6a89867 restored
the20810b6 baseline while retaining rocks. The subsequent owner-authorized fix
targets only the destructive automatic chart reset, not a new LOD-quality scheme.
TerrainLod planning/sampling, mesh geometry, height queries, analytic grounding,
ordinary visible-tile replacement, view-distance control and distance fog remain
the rollback baseline.

At half the existing reanchor distance, one hidden replacement frame prepares
the normal requested tiles. It shares the existing two terrain/scatter worker
slots and single terrain upload per frame. Current terrain stays active until
the replacement covers the requested area and supplies required near detail and
edge stitching. Missing distance leaves are prepared ahead of repeated detail
refinements; overdue work prioritizes the replacement rather than refining the
chart about to retire. If preparation is late, the active chart is not cleared.

The handoff transfers the prepared plan and switches both frame roots in one
Update. Geographic position, height above ground, viewing direction, motion
state and footstep cadence are retained. Scatter batches update before that
frame renders. Retired terrain is disabled together and its meshes destroyed
two tiles per frame; the next preparation waits for retirement to finish.
This is bounded overlap memory, not a permanent fine-terrain cache.
Manual map teleports still perform an explicit reset.

For grounded travel, hidden scatter matrices are also prepared in approximately
1ms batches rather than reprojecting the whole forest at the switch. A terrain
revision or origin change invalidates that preparation. Adoption swaps arrays,
retains the ordinary geographic scatter plan and updates instanced batches
before rendering. Native review compares every prepared matrix to the unchanged
ordinary projection, rather than accepting a cheaper placement approximation.

No coverage roots/masks, geometric morphs, rendered-mesh grounding, protective
streaming fog or new Video LOD controls are introduced. Ordinary LOD transitions
and detail lag at extreme speed are not claimed solved; their masking is deferred.

## Checks

Focused tests: `--ground-only`, `--local-scatter-only`,
`--local-terrain-only`. TerrainLod/LocalTerrainNavigation, mesh geometry, terrain
shader and scatter VisibleGround stay at the rollback baseline. New tests cover
chart coordinates at poles/scales, complete versus missing coverage and required
near resolution. The normal ground/input review remains available.

An overlapping coarse ancestor near the player requires its full exact
replacement set before handoff, so the ordinary renderer can retire it without
occluding the finer surface. The native eye-clearance check takes the highest
of ALL overlapping active land meshes, not just the finest cached mesh.

The restored native review uses `--worldgen-ground-review` with
`--worldgen-local-terrain-check <snapshot.world> --worldgen-output <directory>`.
The removed geology/streaming-review flags no longer apply. Prior passing
cache/morph tests are historical evidence only, not owner acceptance.
The new --worldgen-reanchor-review exercises repeated flight and actual walking
crossings at0.1x, measures handoff/upload/frame timings, tests rendered eye
clearance and captures the same standing position before/after handoff.

Rock art remains the latest irregular asymmetric weathered ellipsoids with
welded seam normals, not the first deliberately squared placeholder. Lithology
still affects palette, grain, proportions and regional occurrence; there is no
prop collision.
