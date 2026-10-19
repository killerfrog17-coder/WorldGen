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

## Streaming

Owner feedback rejected the first streaming checkpoint: its coarse roots blocked
fine geometry, interpolated rendering did not match the player's grounding, and
recently seen detail could be discarded/rebuilt. The earlier tile-count/height-
query checks did not test those failures. That checkpoint is not visual acceptance.

The correction removes the coarse-root queue and blocking activation gate.
Fine products display as soon as resident, nearest first; old coarse coverage
masks only rectangles actually filled by finer meshes. Default sampling restores
the pre-geology128/64/32/16/8 scheme. High increases sampling4x(up to128), rather
than requiring users to increase quality to recover the former baseline.

Terrain within64m of the player never geomorphs; the transition ramps in to256m.
Grounded movement uses the resident local level0 triangle surface, matching what
is drawn, with the shared analytic sampler as the unloaded fallback. Walking no
longer stands on an invisible height while the displayed ground interpolates.
This is a controller/render correction, not a change to accepted terrain heights.
UV3 still supplies distant geometric transitions. Skirts/stitching remain; no
claim of universally invisible seams or a seamless spherical renderer is made.

At most two terrain/scatter jobs run and one terrain mesh uploads per frame.
Recent products retain their higher resolution even if a new request needs less;
neighbour-plan changes do not discard completed meshes. A bounded512-product
active-chart cache evicts only inactive products. Automatic chart reanchors keep
three prior frame caches; a nearby return restores the same mesh objects rather
than rebuilding the whole view. Old frames become hidden when replacements settle,
not destroyed immediately. Explicit map teleports still reset the local cache.
Revisits outside the bounded cache can legitimately regenerate fine detail.

Protective distance haze contracts before missing coverage and reveals newly
loaded distance gradually. Atmospheric/weather haze still operate independently.
Coverage tests include a complete set of finer descendants: a pending coarse LOD
does not justify hiding a region that is already fully rendered at higher detail.
Video settings persist distant landscape range20–500km, detail distance0.5–2x,
Low/Balanced/High mesh quality and transition duration0.2–2s. Physical planet size
caps the usable range; curvature and terrain still occlude distant ground.

This is an incremental tangent-chart prototype, not a seamless spherical terrain
engine or a guarantee that arbitrary flight speeds cannot outrun generation.
Its retained transition cache is bounded, not a permanent fine-terrain store.

## Checks

Focused tests: `--ground-only`, `--local-scatter-only`, `--local-terrain-only`.
Actual-world mesh checks: `--local-terrain-world <snapshot.world>`.
Native: add `--worldgen-geology-review --worldgen-streaming-review` to
`--worldgen-local-terrain-check <snapshot.world> --worldgen-output <directory>`.
This exercises rock scenes, actual gait/mono contacts, a70km fast-flight reanchor,
retained terrain during replacement, a zero-build cached return and unchanged
source hashes. It measures foot clearance against actual resident triangles while
walking/jogging/sprinting, including streaming at High/2x detail. Test-process
settings are explicit and never written to the owner's saved preferences.
Screenshots and timing evidence are kept locally, never committed as source.

Rock art correction removes the deliberately squared superellipsoid shape. All
families now use irregular asymmetric weathered ellipsoids, quieter bedding
contrast, higher silhouette detail and welded seam normals. Lithology still
affects palette, grain, proportions and regional occurrence; this remains
replaceable placeholder art with no prop collision.
