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

It opens on the rightmost monitor and starts the showcase automatically. The
first review was rejected for leg clipping and feeding/rest posture; the
correction below supersedes its motion and skin description.
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

## Owner-requested motion corrections — 24 October2026

Owner subsequently reports this candidate may look worse, with unrealistic
deformation. It is not accepted. The earlier Eating Correction deer was opened
on the rightmost monitor for the owner's comparison. Preserve both viewers and
isolate fit/weights/retarget errors before further heuristic corrections.

The separate corrected review is
`artifacts/fauna-deer-motion-corrections-20261023/Deer - Stage2 Animation Review.blend`.
Use the same launcher and controls, with that root. The first published review
and any open/unsaved owner review remain separate.

Walk/Trot/Run exposed two problems: source rotation changes were being applied
in world axes despite different fitted joint directions, and the new capsule
weights folded the fresh torso/limbs poorly. The correction transfers rotation
changes relative to each source parent through the fitted hierarchy. It uses
Blender CPU bone heat on a temporary solid 12mm voxel proxy, then maps weights
back onto the unchanged approved mesh. Ear/head/hoof rigid attachments are
retained; strongest four weights are normalized. The 29,968-vertex proxy is
discarded, not exported as the animal. Maximum proxy-to-body distance is36.50mm.

Sagittal leg-plane seeds followed by fixed-length IK keep the fore/aft stride
targets, stride height and foot rotations. Each hoof's lateral target follows
its neutral anatomical lane plus pelvis motion; knees/elbows stay in the same
side's plane too. The foreign rotation fit otherwise made the legs cross sideways
in Run. Outward clearance seeds reduced some intersection counts but visibly
splayed elbows in front view and were rejected. The foreleg seed retains12degrees
of elbow pitch; final angles come from the fixed-length solve, not fixed poses.
Solved limb directions also use a twist-free swing from their bind orientation.
This removes inherited axial roll from the fitted limb directions; contact still
needs checking on Blender's actual interpolation, not only the solver matrices.
After limb IK, a bounded body lift resolves any remaining rigid-toe penetration;
the builder rejects corrections above40mm. Contact dips between keys were caught
during the revisions. Corrected gaits now bake contact at eighth frames (240Hz
keys/30fps source); other corrected actions use quarter frames (120Hz). Native
gait checks also inspect sixteenth-frame positions between keys, as well as
keeping all knees/elbows/hooves on their respective anatomical sides. The native
floor tolerance stays8mm. These review keys have not undergone production curve
compression or Unity import acceptance.

`correct-fauna-deer-contact.py` then samples native deformation at1/32 and1/64
source-frame spacing and inserts bounded pelvis-location corrections only where
the interpolated skin dips below3mm. Joint rotations and stride timing remain
unchanged by that pass. Its report records added samples and maximum incremental
lift; this is a presentation/contact bake, not physical foot locking or proof of
zero foot sliding. The source solver's twist cleanup alone did not remove every
between-key dip, which is why this native pass is separate.

Eating lowers the stance100mm, pitches the body forward about4.6degrees, bends
supporting forelegs and reaches farther forward with a distributed neck curve.
The muzzle stays above the floor. Feeding transitions are1.10s instead of0.45s.
Sleep now has explicitly folded, separated lower legs and a supported lower
body. Get Up/Lie Down are rebuilt between that posture and standing. These are
authored corrections to the transferred motions, not unchanged source clips.

The diagnostic intersection audit freezes anatomical triangle masks from the
original skin and applies the same masks to both files. Reclassifying by each
candidate's weights initially gave misleading zero counts; those results were
discarded. The final audit samples complete gait cycles at half frames plus
selected quarter frames, and selected Eating/Sleep poses. It detects selected
non-neighbour leg/torso crossings, not all collisions or acceptable soft-tissue
contact. See `deformation-comparison.json` for the final per-frame counts.

Native checks again verify exact approved bind mesh, topology, UVs, packed active
maps and species shape keys; normalized top4 linear skin,16 actions,34 bones,
controls and additional between-key gait samples. Side and front CPU renders are inspected as well
as the contact diagnostics. Residual intersections and animation quality still
require owner review. No GPU inference, new model/texture, game/Unity launch,
FPS profiling, Desktop item or owner app closure occurred.

Reproduction uses `generate-fauna-deer-heat-weights.py`, then
`build-fauna-deer-stage2.py --retarget-mode local --corrected-poses --skin-file fitted-skin-heat.npz`,
`correct-fauna-deer-contact.py --replace-unopened`, the existing native checker,
and `audit-fauna-deer-deformation.py` for before/after
diagnostics. Source motion licensing and model/material provenance are unchanged.
