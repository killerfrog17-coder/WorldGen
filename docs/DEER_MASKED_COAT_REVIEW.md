# Anatomically masked deer coat trial — 23 October 2026

Owner authorized a texture-only experiment on the accepted deer. Same body,
features, UVs, skin, skeleton and motions; no antlers, new shape inference or
game integration. This is a first material trial for owner review, not a claim
of final realistic fur/eyes or production art approval.

Desktop **Deer - Masked Coat Review.lnk** opens the finished comparison on the
**rightmost monitor**. Header **New coat / Previous coat**, existing motion
buttons and Face/Whole deer controls. Starts in bind pose for inspection;
corrected Eating remains selectable. The owner asked to watch the process, so
a separate working review opened earlier and refreshed from completed bakes.
Neither prior reviews nor owner applications were closed. Final viewer bounds
1912,-8 to3848,1048 were verified. Capture was skipped after owner focused
another window. The working viewer can remain open behind the finished review.

## Material construction

Built-in **imagegen** generated one reusable neutral short-deer-fur swatch.
Original copied into ignored `artifacts/deer-masked-coat-20261023/fur-source.png`,
hash a0656dfc538a7c8e8d66268bcb12e1df3abd855e50d44cde993bd46392825bc2.
Exact prompt/tool/original path saved in `fur-provenance.json`. Prompt asks for
edge-to-edge short tawny deer fur, predominantly downward hairs, uniform diffuse
de-lit lighting and no anatomy/text/background. No paid endpoint/key or local
GPU inference was used. The owner was told The Forest could launch/continue.

The swatch provides restrained hair-scale variation. Material colours and masks
are controlled separately. Triplanar surface sampling gives directional body
fur, with shorter sampling on face/legs. Soft masks define belly, throat,
dorsal tint, muzzle, eyes/lids/brows, nose/nostrils/lip, inner ears, underside of
tail and four hoof regions. Eye/nose masks are not invented by the image model.

Surface anchors were picked from inspected old-coat orthographic side/front
guide pixels, raycast onto the actual mesh and checked from both sides/front.
The previous paint was a placement guide only: no previous albedo pixels are
sampled into the new coat. Landmark normals account for the left-handed
anatomical basis. Bone influence and surface-facing gates constrain the masks.
Head/body remain literally the same accepted mesh, including its asymmetry.

First inspection found overly round eyes and lost low-poly surface smoothing.
Refinement uses tapered almond eyelids, broader orbital/brow transitions and
restores the accepted geometry-only high-to-low normal bake. Fur relief is
combined with that normal; this is not new dense geometry or old colour paint.
Nose pores/roughness reduce its initial smooth glossy appearance. Texture paint
cannot correct limitations in the underlying eye/nose geometry or provide true
layered eyeball optics. Fur remains a surface approximation, not strand geometry.

Final maps: **2048² colour, roughness and tangent normal**, one ordinary PBR
material, metallic0. UV occupancy1991469 pixels/47.4803%; occupancy is not a
failed-paint percentage. Geometry-anchored masks colour all occupied texels;
island borders get16-pixel padding. Anatomical debug mask atlas is separate from
the render material. Final CPU bake11.531s, excluding swatch generation/setup/
inspection; earlier two bakes were revised. No claim all future species can be
painted in that time. This trial implements one coat, not multiple separately
generated fur banks or a runtime species recolour system.

## Validation and preservation

`native-validation.json` records Blender reload checks: accepted mesh positions,
topology, weights, UVs and rig exact; **all30 source actions** including key
coordinates, handles/interpolation exact. Source blend SHA256 unchanged:
b544ea0da599fa1253805478ee40f080316c23f8fd197cb499997765fc781522.
Three packed maps finite and present; actual coat comparison/motion controls
pass. Selected Bind/Idle/Walk/Run/Eating deformed poses finite. This does not
revalidate all contact/collision or claim improved motion. CPU body and three
facial views inspected. No Unity/game launch, profiling, save/settings edits,
model/rig/animation generation, extra antlers or shared environment changes.

The old material is preserved with a fake user so A/B works after reload. An
initial unpublished final save lost that unused material; it was corrected and
reloaded successfully before publishing the shortcut. The complete final blend
packs its maps; the working review uses external maps for live refresh. Existing
rig/motion rights/provenance still apply; do not label the entire asset CC0.
Media/models/evidence remain excluded from Git source backup.
