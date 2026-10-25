# Fauna asset review workflow

## Latest selected colour checkpoint - 25 October 2026

Eleven of122explicitly chosen bodies now have checked colour copies, adding
Bison4/Duck2/Pheasant3. Bison blends shaggy forequarters into short hindquarter
fur, five adult coat studies and three horn proportions on the same adult body.
Duck has six linked plumages with separate bill/feet/lid masks and actual-wing
speculum placement. Pheasant has five palettes, red male eye skin and a barred
tail; female/immature palettes explicitly retain the selected long adult tail.
Both birds reuse the new contour/flight feather sources. Exact built-in imagegen
prompts and source hashes live beside the PNGs in fauna-assets/textures.

Body geometry, skin weights, rest hierarchy and source action curves remain
exact in the native comparisons. Bison/Duck/Pheasant retain15/6/4actions
respectively; pheasant supplies Idle/Walk plus two demos, not flight/takeoff.
All mapped sources are packed into the review files. Existing1-4/skeleton/coat
controls pass. These are unapproved first appearances, not new motion approval.
Next colouring priority: selected freshwater fish1, Mouse1 and Squirrel3.

The owner is browsing the raw viewer opened on the rightmost monitor at Duiker.
Do not switch or alter it during inspection. The pending list contains49new
body choices plus the Aardvark repair. Five procedural insect guilds had already
been accepted without further review:176minus122explicit model selections is
54, but only49new choices actually remain. All176four-candidate sets are ready.

## Current finishing update - 25 October 2026

The raw phase is complete:176 review sets/704 candidates, full file/provenance
audit with zero pending/error entries. This is technical audition completion,
not natural-motion or owner visual approval. The idle generator exited cleanly;
its control is `complete`, and selected-body colouring continues independently.
Do not restart GPU generation simply because the overall finishing goal is active.

Eight colour passes are checked, adding WildGoat1 and Bear2. Goat has five coats,
backward scimitar horn sizes for male/female/young examples and two small beard
variants; maximum visible7114 triangles. Bear has six coat/pigment studies,
fitted eyes, furred ear interiors, muzzle/nose/pad regions and five small claws
on each existing paw; maximum6996 triangles. Both retain the chosen6000-triangle
body, original skin/rest hierarchy and all15 source actions exactly. Near-black
and cinnamon on this shoulder-humped bear are pigment studies; the plan's
distinct black-bear archetype is not created by recolouring it. No cub or other
age/season body proportions have been implemented.

Optional `align_eye_plane` in mesh-pinned guide picks places both sockets in a
common forward-depth plane using actual opposite-side surface rays. A guided
nasal pick can embed its camera in the same record, avoiding a maxY edge seed.
It is a cosmetic fit, not a repair of the selected skull or rig. Face camera
now uses bind face-forward when provided; Front body still checks world+Y.
Whole-body render framing includes visible appendages, and the goat's wider
review framing accommodates the largest horns. All eight native controls,
source preservation and palette/eligible-appendage options pass. Actual CPU
head/body/selected trait images were inspected; appearance remains owner pending.

The explicit paint recipe set now covers eight mammals. Shared short fur,
dense fine fur and coarse guard fur are reused. A new shag/wool source has been
generated and inspected for selected Bison4 next; it is not mapped to a body
yet. Its full prompt and provenance stay beside the project source PNG.

Six selected bodies now have checked first colour passes, adding WildBoar1 and
Mouflon1 (five coats each). Boar reuses the new shared coarse-guard source;
mouflon reuses the accepted short directional fur. Both have anatomy-gated
nose/ear/hoof and eye/lid materials. Boar includes a longitudinal juvenile
stripe pigment study; mouflon includes ram saddle, neck, belly and lower-leg
markings. Juvenile palettes are labelled studies on adult bodies, not complete
juvenile morphology. Body dimensions and source motion defects are retained.

`add-selected-fauna-appendages.py` adds separately skinned, packed-texture
libraries: three boar tusk sizes and young/adult/mature ram horn curls plus
small-ewe/hornless choices. Repeated2 presses shuffle eligible options within
each coat; coat pigment never forces boar sex. Ram/ewe and juvenile restrictions
remain explicit. Only one pair renders. Maximum visible totals are6652 and7044
triangles, including the unchanged5992/6000-triangle selected bodies and476
facial triangles each. Root caps are buried along the sampled scalp normal.
These are simple prototype appendages, not measured anatomy or a growth system.

For lateral-eyed or oblique generated heads, frontal photographs can hide a
socket. Neutral `render-fauna-diagnostic.py --surface-only --focus X Y Z --head`
guides support both `--view side` and `--view left`. Optional per-copy
`face-guide-picks.json` binds inspected camera/pixel picks to the selected model
SHA; each eye is recast from its own side at the same rest-space height. An
explicit snout seed is supported for a long muzzle whose height-gated seed is
wrong. These records take precedence over the first four frontal/side defaults.
Save new guide images before fitting, then bake, fit face, add appendages, check
and index. Appearance is not approved by a finite or source-preservation check.

All six first passes preserve chosen geometry/skin/rest rig and source actions;
all appendage options, coats and review controls were reached in native checks.
CPU head and body/trait views were inspected. No new owner window was opened.
The finishing scripts currently support six explicit slugs, not every animal.

The preceding canid checkpoint covered Hare3/Rabbit1 and RedFox3/Wolf2.
Canids reuse the accepted directional-fur source, with five fox
coats (red, cross, silver-tipped dark, black, pale dryland) and six wolf coats
(grey, tawny, dark brown, black, ivory northern analogue, pale dryland).
The hypothetical palettes stay labelled. Colour and roughness are2K on canids,
normal detail512; fur swatches are shared rather than generated per coat.
Foot stockings, white tail tip and dark outer ears are fox-specific. Wolf uses
a restrained back/forehead/underside mask set. No sex-specific coat rule or
new ecology taxon is inferred. Ear/tail/age morphology remains future work.

Canid eye anchors use two independently inspected frontal guide rays, then
recast to equal rest-space height. A coarse skull's socket-depth asymmetry must
not become a false head axis: forward is muzzle minus eye midpoint. Shared
`fauna_paint_recipes.eye_frame` supplies both mask and facial-geometry axes;
otherwise a misplaced dark lid patch can remain below an adjusted eyeball.
The fox gets a vertical pupil design; wolf round, with scene-lit corneal gloss.
Canid facial pieces use476 triangles, total5969/5972 including the unchanged
5493/5496-triangle bodies. Selected source actions15 each are native-exact.
The raw fox's triangular cheek fragments and oblique skull, and angular wolf
ruff/cheek geometry are retained; first colour pass is not owner art approval.
Front/side heads, fox silver side/cross rear and wolf ivory side/black rear were
actually inspected after the final bake. The separate painted review index has
four checked passes; the owner window is not changed or reopened automatically.

Owner-selected bodies may be coloured now, alongside the remaining raw queue;
there is no all-roster completion gate. Their model choice authorizes colouring
even if motion is unapproved and will be replaced later. Needed shared sources
may also be generated now. The GPU raw producer uses sequential CPU offload
while the owner plays, retaining FP16/50 steps/depth8. These later instructions
supersede the historical ordering and resident-only statements below.

Hare3 and Rabbit1 have separate first colour passes under
`artifacts/fauna-painted-20261024`, with six/five palettes, 1K colour/roughness,
512 normal detail, fitted glossy eyes, surface-fitted bare lid rims and sparse
whiskers. The selected body geometry, skin, rest rig and all seven source actions
per animal are exact against raw. Extras add780 triangles to3420/3430-triangle
bodies. Whole front/side/rear and head front/side CPU views were inspected;
all palettes are reachable and outer lid fur changes with the body palette.
Morphology remains the chosen body: a snowshoe-like coat does not prove
snowshoe foot/anatomy suitability. Tiny raw face fragments and raw gait defects
are retained and are not cured by colouring. Appearance remains unapproved.

CPU source path: `prepare-selected-fauna-paint.py`, then ray-picked landmarks
with `fit-selected-fauna-landmarks.py`, `bake-selected-fauna-coats.py`,
`finish-selected-fauna-face.py`, and `check-selected-fauna-paint.py`.
The first four scripts currently support hare/rabbit/red-fox/wolf/wild-boar/
mouflon, not every body family. Recipes and inspected eye picks must be extended
for new bodies. Run the appendage step after face fitting where applicable.
`index-painted-fauna.py` exposes only hash-matching checked passes in a separate
review index and records actual progress. It does not approve art or motions.
The numbered viewer retains1 motion bank,2 coat,3 side/face/orbit,4 loops,
individual clips, Pause and Show/Hide Skeleton. No new owner window is opened
while they browse raw candidates. Never overwrite an owner-open/approved file.

## Active roster workflow - 24 October 2026

The owner subsequently authorized the full overnight roster. Current scope is
188 entries:176 new visual animal/guild sets, one retained accepted deer, five
existing audio-only groups and six deferred entries. Each new visual set has
four raw candidates. Generate models, fresh independent rigs and suitable
CC0 motion auditions first; five simple microfauna groups use authorized own
procedural geometry/motion. Use fast resident generation, not low-memory mode.
Keep known retarget/motion omissions explicit: a ready audition is not natural
gait, collision-free skinning, game integration or owner art acceptance.

The owner browses the existing window with Next/Previous animal and reports
model numbers. Save choices in `docs/fauna/OWNER_SELECTIONS.json`; preserve model
and rig hashes. Leave their viewer untouched, with no unsolicited switches,
additional windows or review prompts. Retain1/2/3/4, side/face/orbit, Front/Rear,
Pause/Play, individual clips and explicit Show/Hide Skeleton. Raw2 changes only
placeholder colour; it does not imply painted species variants.

Finish the raw queue before generating new reusable texture sources. Plans are
in `docs/fauna/asset-plan.json`, `SPECIES_VARIATION_PLANS.json` and the editable
`FAUNA_ASSET_PLAN.csv`. Twenty-six material-source briefs include reusable and
conditional sources, not26 mandatory new images. Accepted-deer source inventory
records which maps really exist. Painting/finishing needs selection of that body;
silence is not approval. Selection of a raw model does not approve its motions.

Run `python scripts/audit-fauna-queue.py` for a full-scope file/provenance snapshot
in batch `queue-audit.json`. It checks all188 plans/table allocation, the accepted
deer digest, four candidates and native-check evidence per completed set,
retained generated-model/rig files, motion-source hashes/declarations and owner
choice hashes. Missing/rebuilding/unqualified sets stay pending. This is NOT a
visual/contact test or proof that the entire goal (including later texture work)
is complete. Native sampling and screenshots have their own limited scope.
The older horse bank's historical review was intentionally deleted during
cleanup; `retained-source-provenance.json` verifies its identical canonical
arrays, original pinned CC0 horse asset and licenses without changing source
bank hashes.

Older sections below describe historical checkpoints and superseded workflows.
Use the active instructions above and current owner messages for ongoing work.

## Current retained assets and cleanup - 24 October 2026

The owner accepts the finished deer comparison as good enough for a game
prototype, with known motion imperfections. The single retained asset is now
`artifacts/fauna-assets/deer/Deer - Prototype.blend` (75.1MiB), using the initially
displayed candidate1 rig because no preferred rig was named. It includes the
approved body, all eight packed coats, eyes, hair, four optional antler racks,
ear/tail keys, one generated rig and the existing authored motions/demos. The
reference/fur swatch and one rig prediction remain beside it. Reusable original
motion arrays and license/provenance are in `artifacts/fauna-assets/motion-bank/deer`.
This is visual prototype acceptance, not completed game spawning/AI integration.

Open with:
```
python scripts/open-deer-mesh2motion.py --root artifacts/fauna-assets/deer --four-rigs --blend-name "Deer - Prototype.blend" --rightmost
```
The viewer retains1/2/3/4, Pause/Play, individual clips and Show/Hide Skeleton;
the model/rig selector now correctly says1/1. Native reload, packed-map and
control checks pass. Original historical deer review paths below were deleted
at the owner's request; they are provenance history, not current assets.

Owner-requested cleanup removed257 explicit generated targets totaling98.00GiB
across three drives; `artifacts` fell from59.98 to18.34GiB. Builds now retain only
the last per local day across C/E, protecting selected/running builds; no default
latest-three exception. Eight builds remain, one from today. Superseded deer
experiments, unused texture/motion model downloads, generated test fixtures,
captures and logs are gone. Actual owner worlds/saves, source/docs, sounds/music,
running apps and shared AI installations were preserved. Both wildlife audition
banks/catalogues remain, with duplicate stereo masters/job caches removed.
Required dependency providers are retained, including both TRELLIS-named venvs
used by the current TripoSG loader; CPU import preflights pass after cleanup.

The separate Desktop-shortcut deletion command was rejected before execution by
automatic approval review, with only "blocked by policy" stated. Shortcuts remain;
the large artifact cleanup itself completed with zero skips/errors. Reports are
`artifacts/cleanup-report-20261024.json` and `checkpoint-retention.json`.

At that cleanup checkpoint the overnight queue was still a proposal. The owner
later authorized it; the active roster workflow above supersedes that hold.

## Same approved model, four new rigs - 24 October 2026

The owner rejected the preceding four-model audition and now requests a more
controlled comparison: the **same approved finished deer** with four fresh
SkinTokens skeleton/skin predictions. Source art is
`artifacts/fauna-deer-stage1-20261023/Deer - Stage1 Eyelid and Antler Review v3.blend`.
Use `export-approved-deer-rig-input.py` to export only neutral base body geometry;
all four runs consume the identical GLB. Seeds734101-734104, five beams, native
CUDA/SDPA, no low-memory offload or new model/texture generation. Output root:
`D:/WorldGenAI/deer-approved-four-rigs-20261024`.

`build-deer-four-rigs.py --approved-model <source.blend> --root <output>
--motion-root artifacts/fauna-deer-stage2-20261023` retains finished body,
UVs, packed maps, eyes, lashes/whiskers, ear/tail keys and four antler racks.
Generated joints and hierarchy stay in their original canonical coordinates;
fresh network sample weights use four-neighbour interpolation and normalized
strongest four influences. No old skin or skeleton is substituted. Separate
eyes/hairs/racks attach rigidly to each generated head bone. Existing authored
motion is transferred using the same automatic graph/geometry mapper on all four;
no manual pose/geometry/painting fixes or contact/IK cleanup. This tests stochastic
rigging differences, not four distinct rigging software packages.

The gallery switches **1 rig / 2 appearance / 3 side-face-orbit / 4 showcase versus
locomotion**. Coat, bounded ear/tail and50/50 antler traits change together across
all candidates so appearance remains comparable. Direct rig1-4 buttons, individual
clips, Pause/Play and explicit Show/Hide Skeleton remain available. Middle drag
uses continuous free orbit. Structural checks cover unchanged approved bind art,
source/packed-map identity, fresh joint positions/hierarchy, top4 weights and
controls. They do not certify natural deformation, gait or absence of clipping.
The owner selects the most passable result; silence is not approval.

Completed comparison opened RIGHTMOST with one review question sent; leave it
untouched while inspected. Fresh predictions have36/40/43/39 joints and measured
25.22/47.01/66.03/56.29seconds (model load through prediction/save),3506.9MiB peak
Torch allocation. Assembly including motion bake/save took6.45seconds. Native
checks pass for four distinct predictions, generated joint positions/hierarchy,
top4 skin, controls and unchanged approved bind geometry/UV/shape-key coordinates,
transforms and packed active maps; source blend hash unchanged. All eight coat
materials are retained. No visual naturalness/collision approval is implied.
All inference exited before review; no owner app closure, Desktop item or game
integration. File identity/evidence is in batch `approved-input.json`, prediction
JSON/NPZ, `gallery.json` and `structural-checks.json`.

## Four independent rigs — 24 October2026

Owner requested four fresh models with newly generated skeletons, superseding
template-rig reuse for this experiment. Review is open RIGHTMOST at
`D:/WorldGenAI/deer-four-rigs-20261024/Deer - Four Generated Rigs.blend`.
Owner is comparing; preserve this window and wait for selection or rejection.

Each model has a separate SkinTokens skeleton and dense sample skin prediction:
29/35/33/36 joints,21.91/23.58/22.84/24.41seconds respectively. Generated heads
and hierarchy are preserved under one uniform normalization and canonical axis
alignment. No old rig/skin is substituted. Automatic graph/geometry matching
handles differing limb/spine/head chain counts when transferring the existing
authored motions. Sample skin is interpolated from4 neighbours, strongest4
normalized;6000triangles/model and existing box-mapped fur. No paint, sculpt,
manual pose repair, IK or contact bake. Source animation terms are unchanged.

The viewer has **1 model / 2 colour-only species preview / 3 side-face-orbit /
4 showcase-locomotion**, direct model1-4 buttons, individual clips, Pause/Play
and Show/Hide Skeleton. Model changes preserve the selected clip/phase. Visible
skeleton enables bone overlays and In Front explicitly. Structural/control checks
verify4 distinct prediction hashes, unchanged generated joint heads/hierarchy,
normalized top4 skin, one displayed model and every requested control. These
checks do NOT establish natural motion, sound skinning or collision-free poses.

Owner requires fast loading rather than low-memory sequential offload. New
`generate-deer-local.py --resident-gpu` holds the pipeline on CUDA without CPU
offload hooks. Model2/3/4 generation measured48.67/48.59/48.86seconds including
loading, denoising and extraction. Candidate1's earlier model-CPU-offload run
crashed during extraction after saving latents;4.76second decoder recovery is
not a full-generation timing. The owner-requested change stopped the subsequent
low-memory batch; no further low-memory runs were used. No owner app closure or
game/Unity changes. All inference finished before opening the gallery.

Artifacts stay on D: for this batch, without new model/environment downloads or
Desktop items. Owner-authorized build retention removed5 obsolete checkpoints,
7.25GiB, retaining9 (daily/latest3/selected-running protection). Additional old
test logs/backups/rejected video-model cleanup was rejected before execution by
automatic review with only "blocked by policy"; those targets remain. No attempt
was made to evade that block. The generation models/tools and world snapshots
were preserved. See `artifacts/checkpoint-retention.json` and batch cleanup plan.

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

Owner found the first raw pass better but requested another generation. Candidate
B uses seed732525,113.89s generation, same automatic assembly, in the sibling
`fauna-deer-raw-motion-20261024-b` root. Its new raw viewer restores the requested
numbered controls in both the top bar and Animal Review sidebar:

- **1:** animation bank1/1.
- **2:** randomized fur-colour preview only; no painted patterns or extra anatomy
  are claimed for this unfinished raw pass.
- **3:** side, face and rotating view; ordinary middle drag stops automatic orbit.
- **4:** blended behaviour showcase versus locomotion loop. These are ordinary
  local-pose blends of the existing clips; original source actions stay intact.
- **Show/Hide Skeleton:** explicitly unhides the rig, enables bone overlays and
  draws bones in front of the body. Native GUI toggle/restore checks pass.

Individual animations and Pause/Play remain available. The owner has never used
Blender: say "the deer picture" instead of unexplained "3D viewport", and prefer
visible buttons over instructions to search Blender properties. Alt+H alone did
not solve the owner's visibility problem. Preserve older windows/files.

Collision, stretch and joint-flip diagnostics can inform future automatic
rejection, but this reroll has no added quality-rejection/repair gate and no art
acceptance claim. Smoothing is not a repair for wrong weights or joint placement.

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
