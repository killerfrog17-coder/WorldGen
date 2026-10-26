# Human animation audition

The two accepted appearance samples now have an isolated Blender animation
comparison, before any game implementation. Six MPFB people and six UMA people
play identical downloaded CC0 clips. Native output lives in ignored
`artifacts/human-animation-preview/Human - Animation Comparison.blend`.

Run `scripts/open-human-animation-preview.py` with the local rig Python to open
one new review on the rightmost monitor. The visible Item sidebar has MPFB,
UMA, Compare, people A–F, 15 clips, Play/Pause, Restart, views and skeleton
visibility. Keys 1–6 switch method, person, view, comparison, lineup and clip;
Space toggles playback. The comparison initially plays Walk, MPFB left/UMA right.
Hidden characters do not appear in the review. All six actual body proportions
remain visible; characters are not resized to an identical height.

## Sources and retargeting

The free downloads, exact clip inventory, license evidence and checksums are in
`artifacts/human-animation-sources` and `HUMAN_ANIMATION_SOURCE_AUDIT.md`.
Quaternius supplies Idle, Walk, Run (Jog), Sprint, Crouch, Sit down, Sitting,
Stand up, Sword attack, Chop wood and Swim. KayKit supplies its alternate walk,
alternate run, Jump and Dig. Fifteen motion choices are not fifteen finished
gameplay systems. Poses are sampled at 30 Hz from original source hierarchies,
with continuous quaternion signs and linear keys. Source hashes/clip names are
stored on each native action.

MPFB uses its original 53-bone game-engine rigs and unchanged meshes, weights,
shape keys and rest matrices. The appearance scene's initial lowered-arm pose is
replaced by animation without changing the bind pose. Each variant receives its
own calibrated action for its native proportions.

UMA's earlier appearance export was static. `HumanBuilderDemoExport.ExportRigs`
rebuilds those same six recipes in the isolated downloaded UMA Unity project,
exports all original vertex influences, bone hierarchy, current bind transforms
and native humanoid mappings, and exits. Baked vertices must match the earlier
appearance snapshots within 0.1 mm before original weights can be attached.
No nearest-neighbour weights, neural auto-rig or guessed anatomical skeleton
is used. The full influence lists are retained. Original Unity helper bones
remain in the hierarchy. UMA's opposite left/right naming convention is resolved
from actual bind coordinates; trusting the labels produced visibly crossed arms
in an early diagnostic and was corrected before review delivery.

Retargeting calibrates limb directions between different bind poses, transfers
global source rotations into each target's local bone basis, and scales pelvis
displacement by leg length. It preserves the target's bone lengths. Quaternius
finger motion is mapped to native fingers. KayKit has no fingers, so those clips
retain the target hand's resting finger shape; custom tool grips remain future
work. This is rotation retargeting, not a contact IK solver.

## Scope and validation

The source appearance comparison is retained read-only. The native checker
requires its exact previously delivered SHA, verifies every MPFB mesh/weight/
shape/rest digest, packed maps, 12 rigs, 15 actions per rig and actual motion
source hashes. It checks finite evaluated skinned surfaces and gross bounds at
five phases of every clip, for 900 character/pose samples. These numerical checks
do not replace rendered inspection or owner acceptance. Render checks use the
actual textured and skinned meshes, including all six body variants.

This audition contains individual clips, not transitions, gameplay motion
matching, first-person arms, collisions or equipment interactions. Tools and
weapons are not fitted, and sit poses have no chair prop. Foot contact and grips
will need gameplay-specific fitting if a foundation is selected. Swimming hides
the studio floor so it does not conceal the character. No game project, player,
save or existing owner window is changed.

Reproduce with `build-human-animation-preview.py`, then
`check-human-animation-preview.py` and `render-human-animation-check.py` in
Blender background mode. `human_motion_common.py` reads the downloaded GLBs;
`human_animation_stage.py` supplies shared viewer/renderer staging.
