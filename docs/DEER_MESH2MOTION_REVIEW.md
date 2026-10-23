# Mesh2Motion deer review

23 October 2026. Offline Blender only. No game/Unity/world/save/settings changes,
paid services, signup or GPU inference. The owner's Forest session and existing
review windows were left running; the launcher avoids taking game focus.

## Owner rejection and current comparison

The owner rejected every replacement Mesh2Motion rig/skin variant. **Fitted +
weight cleanup** was least bad, still unacceptable. Do not promote these exports
to game assets. The owner also rejected every preceding UniMate result; its
heuristic REVIEW labels do not constitute art approval.

Desktop **Deer - Original Rig Motion Review** opens the separate file:
`artifacts/deer-own-rig-mesh2motion-20261023/Deer - Original Rig Motion Review.blend`.
It retains the existing 34-bone skeleton, rest geometry, weights, UV and paint.
Sixteen existing actions remain under **Motion → Previous deer clips**; thirteen
transferred clips are a separate bank. Header: **Bind pose**, **Idle**, **Walk**,
**Trot**, **Run**, **Pause / Play**, **Face**, **Whole deer**. It starts truly
unposed; clicking a clip starts playback. No live IK/gaze controller is present.

The old replacement-rig review is separate. Neither source nor open review files
were overwritten. Binary review artifacts are not backed up by source commits.

## Replacement-rig trial

Local `afk-rig@0.1.2` packages the actual Mesh2Motion engine, skeletons and
authored motions. It runs on CPU, not a hosted service or neural animation model.
The deer was fitted to its 56-bone horse skeleton; anatomical landmarks corrected
the generic box fit. Three rest-axis modes and a fourth tail/hip weight-cleanup
variant were tried. All were rejected by the owner.

GLB checks preserve world vertices within 0.07 micrometres, UV/index arrays and
embedded texture bytes exactly, and all 784 native rotation channels per export
exactly. These are provenance checks, not a good-skinning or natural-motion claim.
The raw/fitted projects, tool revisions, licenses and failed results remain in
`artifacts/deer-mesh2motion-20261023`.

## Face diagnosis and original-rig transfer

Same-camera CPU closeups compare both rigs in true bind and idle. The true bind
face is unchanged; the replacement idle visibly deforms the muzzle and cheek.
Pausing an animation retains its deformation. Static material values and world
normals also match. This isolates the added problem to the replacement rig,
weights and pose combination; it does not prove one individual weight alone is
the cause. No face sculpting, repainting or shape inference was performed.

The replacement transfer uses world-space rotations relative to donor bind axes,
converted to the deer frame, while retaining original target rest lengths. Joint
sides come from paired positions because old suffixes are inconsistent. Several
front joints in the earlier horse transfer were mapped one segment too far down
the leg; the corrected chain uses scapula/humerus/upper foreleg/lower foreleg/
ankle. Ears, jaw and muzzle inherit the head rigidly. Hip bob uses displacement
relative to donor rest. Quaternion signs are continuous; keys are linear. There
is no per-frame floor lift or live IK added to hide contact failures.

Original clips use 24 fps, transferred clips 30 fps. Donor import occurs after
setting that frame rate, preserving seconds. The earlier downloaded horse glTF
includes a Blender-readable skin; all its 2,352 channel times/values match the
package's animation-only file exactly. No donor mesh remains in the new review.

## Verification and limits

- Saved/reloaded geometry, UVs, skin weights, skeleton rest matrices and decoded
  texture pixels match the original asset exactly. Source SHA-256 remains
  `0f9aa11b8fee484ff569cffcf6d2a561e439caecb401bc3f8adf553824a2ade1`.
- All 29 action selections pass native slot/rate/range/finite-pose checks. True
  bind clears the action and selects REST. Every frame of all 29 actions has
  finite skinned geometry; facial child bones remain identity for transfers.
- Selected CPU idle/walk/run and four walk-phase renders were inspected. The
  transferred idle no longer shows the replacement-rig muzzle distortion.
- Contact is unfinished: transferred walk penetrates about 5.4 cm; feeding,
  resting and death can penetrate around 14–18 cm. These are failures, not
  accepted contact. No full intersection, foot-lock, natural-gait or loop approval.
- No animation crossfade/controller integration, game export/integration,
  gameplay tests or runtime FPS claim follows from this comparison.

Evidence: ignored `retarget-checks.json`, `pose-checks.json`, `viewer-checks.json`,
`motion-source-check.json`, and the bind/idle face closeups in the new root.
Builders/checkers/viewers are in `scripts/*mesh2motion*`, `scripts/*own-rig*`,
and `scripts/diagnose-deer-face.py`. No shared AI environment or global Blender
add-on was modified.

Mesh2Motion code and wrapper are MIT; bundled authored rig/motion art is CC0.
The deer retains its documented generated-asset terms; **the entire deer is not
relabeled CC0**. Exact npm integrity and file/license hashes are retained in
the old root's `tool-provenance.json`. References:
[Mesh2Motion](https://github.com/Mesh2Motion/mesh2motion-app),
[wrapper](https://www.npmjs.com/package/afk-rig),
[existing deer pipeline](DEER_GAME_ASSET_PIPELINE.md).
