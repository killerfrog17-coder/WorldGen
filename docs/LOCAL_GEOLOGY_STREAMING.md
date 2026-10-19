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

Automatic tangent-chart reanchoring no longer calls destructive teleport cleanup.
Resident coverage is retained and rigidly reprojected while replacement jobs run;
at most three transitional chart groups are kept, then disposed. Explicit map
teleports remain distinct. Loading first requests16-segment coarse coverage roots
before expensive near-detail products. At most two terrain/scatter jobs run and
one terrain mesh uploads per frame.

Replacement parent/child groups only switch when their required coverage is
resident. Vertex heights morph from the old visible triangle surface to the new
mesh using UV3 and a renderer property block. This is a geometric transition, not
transparent terrain rendering. Source/walking heights are unchanged. Once a
transition completes, no per-renderer property update is needed until replacement.
Skirts and coarse-edge stitching remain. There can still be a coarse landscape
when flight outruns mesh generation; this is preferable to clearing the world.

Protective distance haze contracts before missing coverage and reveals newly
loaded distance gradually. Atmospheric/weather haze still operate independently.
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
retained terrain during replacement, settled cleanup and unchanged source hashes.
Screenshots and timing evidence are kept locally, never committed as source.
