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

## Streaming: restored original implementation

The owner rejected both geology-era streaming attempts. All LOD behaviour is
restored to commit20810b6 (the centered-footsteps checkpoint), not redesigned:
original planning/sampling, tile lifetime, job admission/completion, reanchoring,
analytic player grounding, view-distance control and distance fog. This deliberately
includes the original reanchor reload behaviour. No claim that those old limitations
have been solved is made.

The new terrain streaming partial class and Video LOD options are deleted.
No retained-chart/product cache, coverage root/mask, geometric morph,
rendered-triangle grounding or protective streaming fog remains. The only changes
to the baseline explorer/shader are the six rock-material attribute channels
and material blending. Scatter's ground-height lookup also matches20810b6.

## Checks

Focused tests: `--ground-only`, `--local-scatter-only`,
`--local-terrain-only`. Source comparisons against20810b6 verify the complete
navigation, debug controls, display settings and runtime review harness match;
explorer differences are restricted to lithology upload, and shader differences
to rock material attributes/shading.

The restored native review uses `--worldgen-ground-review` with
`--worldgen-local-terrain-check <snapshot.world> --worldgen-output <directory>`.
The removed geology/streaming-review flags no longer apply. Prior passing
cache/morph tests are historical evidence only, not owner acceptance.

Rock art remains the latest irregular asymmetric weathered ellipsoids with
welded seam normals, not the first deliberately squared placeholder. Lithology
still affects palette, grain, proportions and regional occurrence; there is no
prop collision.
