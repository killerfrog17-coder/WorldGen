# Fauna asset review workflow

## Current order — owner revision24 October2026

**This supersedes the older stage order documented below.** Generate one fresh
model, automatically rig/weight it and apply existing suitable authored motions
BEFORE painting, facial refinement, species morphs or extras. Show the raw result
in Blender on the rightmost monitor. Reuse an existing fur swatch with automatic
mapping so the model is readable. No manual sculpture/painting/pose repairs or
extended checks in this initial audition unless the owner subsequently requests
them. It is an early rejection gate, not a quality or production acceptance claim.

Once the owner accepts the model's deformation and movement, proceed to surface
painting, species variations and optional antlers/ear/tail features. Then recheck
that appearance work has not broken movement. Keep original references and rig/
motion provenance; a fresh generated mesh need not mean regenerating its image
reference or rig template. Explain which parts were reused.

The24October raw deer uses a new TripoSG shape from the existing reference,
seed732524,50steps/depth8, shared-GPU sequential offload. Generation122.59s;
automatic Blender assembly2.44s, excluding script preparation/launch.6000triangles,
existing34-bone rig and authored animations retained together; closest-surface
barycentric top4 skin transfer. Existing fur uses box mapping; no painting or
manual repairs. No donor animal mesh remains in the saved audition. This is NOT
new neural rig generation, validated natural movement or accepted deformation.

Raw review: `artifacts/fauna-deer-raw-motion-20261024/Deer - Raw Generated Motion Audition.blend`.
Opened RIGHTMOST, one follow-up sent; leave it untouched while the owner inspects.
Top bar Idle/Walk/Trot/Run/Eating/Pause; sidebar Sleep and other source clips.
No new Desktop item or game/Unity changes. GPU generation is finished.

## Historical workflow — superseded stage order

Owner revision,23 October2026: one generated candidate at a time. Review is in
Blender on the rightmost monitor. **Do not create Desktop shortcuts or other
Desktop-root review items.** Keep source in the repository and generated assets
inside ignored project artifacts. Existing Desktop items are not an instruction
to delete or move them.

## Stage 1 — model and appearance

Plan reference anatomy, surface materials, coat/marking variants and available
motion sources before generation. Reuse approved swatches where appropriate.
Generate a fresh shape, inspect it, reduce with a triangle budget, prepare UVs,
correct obvious anatomy/face defects and paint it. Material masks must work
before the final rig exists; do not require full skinning for each candidate.
Check side, front AND rear, plus facial close-ups. Reserve geometry for eyelids,
curved eyes and attachment transitions. Small animals can use lower budgets;
the deer target is at most6000 rendered triangles, not6000 quads.

Gallery controls: **1 model / 2 reproducible species-appearance variation /
3 side–face–orbit**. One-candidate galleries explicitly display1/1. Also provide
front-body/rear inspection buttons. Display model number, coat name and seed
clearly enough for remote/phone review. A variant preview is not automatically
an admitted saved ecology taxon or a complete seasonal/sexual morphology system.
Keep morphological traits local and within a species-family range: ear size,
tail dimensions/markings and coat pattern can vary without new body meshes.
Preserve attachments and accepted face/body regions; inspect trait extremes.
Patterns may explore plausible evolution beyond named modern species, but label
biological analogies and art hypotheses clearly. Do not infer ecological rules
from a palette alone.

For eyes, inspect visible lid anatomy as well as the globe. Darkening ordinary
fur is insufficient: tag actual connected lid skin, remove fur relief there and
blend a short-furred orbital ring into the face. Check lash/brow attachments and
sample midpoints/tips for burial. Keep accepted eyeball shape/material separate
from these adjustments. Optional antler libraries share small material maps;
only one selected rack renders. The deer preview uses the owner's50/50 male/
female antler rule, independently of coat variation; this does not add saved
population demographics.

Finish obvious corrections before presenting. Send one follow-up and open one
review. **Owner selection/approval is required before Stage 2.**

## Stage 2 — only selected geometry

Rig, fit weights, transfer suitable licensed authored motions, correct obvious
deformation/contact problems and prepare blended demonstrations. Multiple
candidate motion sets are useful only when genuinely ambiguous. Existing source
packs must retain their provenance/license; no paid services without permission.

Requested controls: **1 animation set / 2 appearance / 3 side–face–orbit /
4 full behaviour showcase versus focused locomotion loop**. Focused deer loop
is idle, walk, run, walk, idle with transitions. This second gallery is not
implemented merely by completing the current static Stage1 viewer.

## Asynchronous queue

After approximately two minutes without a reply, continue **Stage1 of the next
owner-queued animal**. Silence is not approval. Keep later completed candidates
waiting without additional follow-ups or windows. When the owner returns, pause
at a safe checkpoint, show waiting reviews and prioritize Stage2 of approved
models. Preserve each model/seed/set identity. Do not invent new queue entries.
Currently the owner has queued the deer redo only.

## Shared GPU trial

Owner now explicitly permits trying local generation while The Forest runs.
Do not close the game or ask again merely because it is running. The current
TripoSG wrapper has opt-in `--shared-gpu`: sequential CPU offload, device-wide
free-memory check/allocator reserve, temperature/VRAM telemetry and a sampling-
boundary thermal stop. It retains FP16/50steps/depth8; no intended quality
reduction. Offload trades transfer time for GPU residency. Report actual timing,
not the earlier32-second isolated denoise figure as a complete shared run.

If concurrent operation fails due actual resource pressure, stop the owned job
and ask the owner to close the game before the dependent retry. A checkpoint
reader/CPU-loading defect is not proof the game must close. Ordinary browser/
video use and CPU asset work can continue. Do not alter shared ComfyUI, GPU
clocks, driver timeout settings, game settings or owner applications. No automatic
Stage2 inference until model approval; no game/Unity integration in these reviews.
