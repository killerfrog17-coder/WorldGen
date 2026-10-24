# Approved fresh deer: animation review

23 October2026. Owner explicitly approved the finished Stage1 deer and requested
the animation phase. The separate review is
`artifacts/fauna-deer-stage2-20261023/Deer - Stage2 Animation Review.blend`.
Launch with `python scripts/open-deer-mesh2motion.py --root artifacts/fauna-deer-stage2-20261023 --stage2 --rightmost`.
No Desktop item, game/Unity change, owner app closure or GPU inference.

## Review controls

- **1:** motion set1/1 (one fitted authored bank).
- **2:** existing eight coat families, ear/tail traits and50% antlered male /
  antlerless female, with four profile-linked racks.
- **3:** side, face and orbit. Face view follows the animated head.
- **4:** broader behaviour showcase versus focused locomotion loop.
- Header **Pause / Play**, **Free orbit**, Front body/Rear; ordinary middle-drag
  stops auto-orbit and rotates continuously. Space uses Blender's playback toggle.
- Animal Review sidebar → Animation preview selects individual clips.

It opens on the rightmost monitor and starts the showcase automatically. Owner
is inspecting it; do not replace/restart the viewer while that review is underway.
The review is in place, not a fauna AI or movement-controller implementation.

## Model, rig and motion

The approved Stage1 model is preserved, including UVs, packed material images,
eyeballs/lids/hairs, antlers and all appearance shape-key geometry. There is no
stock animal mesh substitution. Bind coordinates, triangle topology, UV arrays,
packed active-material image bytes and shape-key coordinates match exactly in
native reload checks. Visible animal budget remains5864/5960 triangles without/
with the largest rack; hidden rack candidates are not all drawn together.

The existing accepted Eating Correction review supplies the34-bone hierarchy
and13 Mesh2Motion motion clips. Mesh2Motion's bundled authored motion art is CC0;
this does not relabel the generated model/materials or entire toolchain CC0.
See [accepted source/provenance](DEER_MESH2MOTION_REVIEW.md).

The source is converted to a proper, non-reflected canonical +Y frame. Joint
positions/rolls are fitted to the fresh animal; source Idle calibrates the neutral
pose. Motion uses world rotation changes relative to that calibration and fixed
target rest offsets. This is a retarget, not identical old pose matrices applied
to incompatible bone axes. The source mesh/rig/actions are read only and hashed.

Final skin weights are newly computed on the approved mesh: spine/neck fields,
nearest adjacent limb capsules, surface-edge diffusion with UV splits welded for
the calculation, then normalized strongest four. Eyes, hairs and antlers attach
rigidly to Head; skull/lid skin is rigid there too. Ears and tail use their fitted
bones; low hooves remain rigid. Standard linear armature skinning is used, not
Blender-only dual-quaternion or corrective-smooth deformation.

An initial warped nearest-surface transfer pulled torso patches with the legs;
that transfer was rejected. A later distance gate made muscle folds worse and
was removed. Neither failed candidate was shown as the final animation review.
The remaining first-pass shoulder/hip pinching and gait quality require owner
review; structural checks do not establish art acceptance.

The source13 are Death, Eating, Head But, Idle, Kick, Rear, Run, Sleep, Trot,
Turn Left, Turn Right, Walk and Get Up (`lay_to_idle`). Lie Down reverses Get Up.
Two additional actions are the blended demonstrations, for16 actions total.
There are no new neural animation takes. The broad showcase covers locomotion,
feeding, turns and short defensive actions; all other clips remain selectable.

## Contact and playback

Offline fixed-length IK corrects hoof surface penetration. Grazing uses a lower
internal neck pivot, a small lowered standing stance, planted supporting limbs
and a reachable forward head target, preserving the authored head orientation.
The muzzle stays ahead of the forefeet rather than dipping between them. The
accepted source eating correction is retained as input; this new fit requires
new contact corrections for its proportions.

Retargeted ankle channels initially swung about93degrees in one source frame.
Toe-off orientation changes are bounded and contact is rebaked at half-frame
spacing. Final files use ordinary bone keys at60Hz with30fps source/playback,
not runtime Python IK. Demonstrations blend quaternions over0.45s, preserve gait
phase through Walk/Trot/Run transitions, and blend back to Idle for repetition.
This does not implement world movement, terrain foot locking or animal behaviour.

## Validation and limits

Native reload checks pass exact approved bind art/materials/shape keys, finite
normalized skin (at most4 weights), linear modifiers, all16 action selections,
the species/view/mode controls and798 selected integer/half/quarter-frame poses.
Sampled grazing muzzle clearance is25.170–25.187mm. The worst sampled body
floor penetration is3.613mm in Walk; the showcase minimum is about0.48mm below
the reference plane. This is not zero penetration, a full collision audit or
continuous foot-slide acceptance. Floor corrections for lying/death clips are
presentation adjustments, not a physical falling simulation.

Selected CPU idle/walk/run/eating renders were inspected. All phase work stayed
on CPU while the owner could keep The Forest open. No Unity/game launch, FPS
profiling, paid endpoint, new image generation or local neural inference.
Natural gait, all intersections and final animation quality remain owner pending.

Evidence is ignored: `source-rig.json`, `fit-report.json`, `motion-fit-checks.json`,
`native-validation.json`, motion arrays, blend and CPU renders. Source scripts
are `prepare-fauna-deer-motion-source.py`, `fit-fauna-deer-skin.py`,
`build-fauna-deer-stage2.py`, `check-fauna-deer-stage2.py` and the Stage2 viewer.
Git source backups exclude these generated assets and model weights.
