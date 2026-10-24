# Fresh deer stage1 review — 23 October2026

Owner authorized a fresh one-candidate deer using the revised two-gate workflow,
explicitly trying GPU generation while The Forest runs. **Stage1 only is ready;
model/appearance approval is pending before any new rigging or animation.**
See [review/queue rules](FAUNA_ASSET_REVIEW_WORKFLOW.md). No Desktop item created.

Owner subsequently accepted the final corner correction and explicitly approved
continuing to Stage2 animation. The approved static source is
`Deer - Stage1 Eyelid and Antler Review v3.blend`; earlier pending-approval notes
below document review history rather than revoke that authorization.

## Review

### Eyelid anatomy and antlers

The owner rejected the remaining eye surround as fur with a pasted-on eyeball,
providing real deer references and approving a connected anatomical lid, bare
skin, thin-furred outer ring, lashes and brow whiskers. New separate review:
`Deer - Stage1 Eyelid and Antler Review v3.blend` (same launcher, choose this filename
with `--blend-name`). Prior published files stay unchanged. This is still static
Stage 1; no rigging approval has been given.

The previous colour rim was extremely narrow and did not follow exactly the
constructed aperture. Its surrounding surface still used body fur relief.
The correction adds one 24-segment ring per eye: a fuller upper lid and thinner
lower lid connected through the existing socket into the face. Three geometric
surface weights (bare lid, surround and inner margin) survive UV splitting and
barycentric texture baking. Bare skin replaces fur colour and fur height, has
subtle pores and separate roughness, with a darker inner margin. A subdued
short-fur ring blends into the rest of the face. The accepted eyeball mesh,
size, iris and glossy material are retained. Sixteen tapered upper lashes and
six brow whiskers use three triangles each. Lashes were moved outward after
the first pilot exposed buried tips; native attachment/clearance checks accompany
selected close-ups. There is no blinking animation or runtime hair system.

The owner then accepted the fur-ring colour but requested more prominent skin,
a more deer-like lid contour and longer brow whiskers. Face revision5 widens the
upper hood to15.2mm above the eye centre versus11.4mm, with a raised crest,
rounder upper aperture and shallower lower contour. The outer short-fur colour
is retained and bounded to avoid expanding it into a large pale spectacle.
Brow whiskers are1.55x the first eyelid revision's length. The first published
eyelid/antler review remains separate; these are local corrections, not a new
model/texture-generation pass.

The owner accepted revision5 with one final requested detail: the surrounding
skin should taper forward at the inner corner instead of following the eyeball
uniformly. Revision6 extends the bare lid6mm toward the muzzle at that corner,
blends its surrounding skin over8mm and drops the corner2mm, with a subtle
medial crease. The opening/globe and the rest of the accepted contour stay as
before. The local basis is mirrored anatomically, so the extension points toward
the nose on BOTH sides. This completes the requested conditional art correction;
no rigging has been performed in this task.

Review navigation now provides **Free orbit**. Plain middle-drag stops the
automatic orbit, unlocks the view and applies continuous quaternion rotation;
it has no axis-snap step. Shift/Ctrl-middle-drag keep Blender's pan/zoom bindings.
No Blender user preferences are saved. The earlier90-degree issue was not
independently reproduced; native Alt-axis snapping and auto-orbit interference
were possibilities, not a confirmed cause. The explicit review control avoids
both for plain middle-drag.

Final native reload verifies all8 coats/packed maps, both preview sexes/all4 rack
styles, finite local morphs, the small-delta/free-orbit/release path and the
5960 visible-triangle cap. Eyeball coordinates match the accepted review exactly;
neutral non-facial body distance0. Eye-hair root distance<=.245mm, sampled mid/
tip clearance>=4.495mm. Seven CPU angles rendered; selected close eye/front/
oblique and4 rack pilots inspected. The final review is open on the rightmost
monitor; the requested final corner correction is complete. Earlier published
reviews are preserved. No rig/animation, game launch or GPU inference was done.

Owner additionally requested antlers with a 50% chance when changing phenotype.
Four authored procedural paired shapes share packed 512px colour, roughness and
normal maps: Young spikes (44 triangles), Narrow forks (72), Woodland branches
(84), Wide crown (96). Shafts taper and branch at embedded joins; pedicles are
ray-fitted to the skull. Geometry is simple and deliberately economical, not a
scan or an AI-generated rack. No paid service or local GPU model is involved.

Key 2 chooses a seeded male/female preview with equal probability, shows only
the male's profile-linked rack, and retains coat/ear/tail variation. Labels show
sex and rack. The initial view is antlerless. This is the requested preview rule,
not a new saved ecology sex/age/season model. All four racks remain stored in the
review library, but at most one is drawn. Visible geometry is **5900 triangles
without antlers, at most 5996 with them** in the first eyelid revision; its
hidden library totals6196 triangles. The wider revision5 aperture removes36
near-eye triangles, yielding5864 antlerless/5960 maximum visible/6160 including
all hidden racks. Only one rack is drawn at once.

### Owner-requested facial/species revision

The owner accepted the body and eyeball appearance, but requested a shorter,
broader nose patch, smoother eyelid/socket transitions, better ear painting and
smaller ears with species variation. Keep the first review separate. The revised
file is `Deer - Stage1 Face and Species Review.blend`; launch with the same
command below plus `--blend-name "Deer - Stage1 Face and Species Review.blend"`.
This remains Stage 1; the first feedback was **Needs changes**, not rig approval.

The revised set has eight base families, each with bounded seeded tint:

| Appearance family | Intent | Ear / tail scale before small seeded variation |
| --- | --- | --- |
| Tawny woodland | Plain warm coat | 0.80 / 1.00 |
| Dappled spotted | Broken flank markings | 0.86 / 0.90 |
| Grey upland | Muted grey-brown | 0.78 / 0.78 |
| Snow-pale | Hypothetical pale cold-environment variant | 0.76 / 0.72 |
| Dark closed-forest | Dark brown, dark tail tip | 0.82 / 0.86 |
| Light-striped thicket | Narrow pale flank stripes | 0.86 / 0.85 |
| Dark-banded understory | Narrow darker flank bands | 0.87 / 0.84 |
| Sandy dry-woodland | Sandy coat with larger ears | 1.00 / 1.05 |

These are candidate appearance families, not eight new ecological species or
assertions that every pattern is an existing deer species. The [NPS mule-deer
profile](https://www.nps.gov/grca/learn/nature/mule-deer.htm) documents large ears,
a black tail tip and heat radiation from ears. [LA Zoo's bongo profile](https://lazoo.org/explore-your-zoo/our-animals/mammals/bongo/)
describes pale stripes breaking up the animal's outline in forest shadows.
Bongos are antelopes: applying that pattern rationale to a hypothetical deer is
an explicit analogy, not evidence of striped modern deer or simulated adaptation.
Pale-cold and dark-forest pairings are art hypotheses, not universal ecological
rules. Future speciation must select compatible traits rather than mix all of
them uniformly at random.

Hotkey 2 shuffles through all eight families before replenishing its bag and
applies modest profile-linked ear/tail changes. Ear range is 0.72-1.08 of the
original blades, default 0.80; tail range is 0.70-1.20. Local shape keys preserve
attachments and exclude face/torso. Antlers remain deferred. The body is shared;
variants do not duplicate mesh geometry. Maps are packed for offline review;
this does not implement runtime loading of eight full material sets per animal.

The nose is a small rounded trapezoid at the muzzle tip with paired nostrils,
rather than a patch extended up its bridge. Wider connected orbital geometry
and a middle lid ring soften the eye-to-skin join without replacing the accepted
eyeballs. Ear interiors follow the blade's own coordinates, preserve the furred
outer rim/back, and blend warm inner skin into pale short hair.

Revised native reload checks pass at **5738 rendered triangles**, eight packed
2K map sets and all review controls. Ear/tail extreme coordinates are finite,
displacement stays below 8.1 cm, and changed vertices remain confined to the
ear shells/rear centre. Neutral non-facial body coordinates match the original
reduced candidate exactly (nearest distance 0). Eye-anchor heights still match;
maximum mirrored nearest distance remains 5.09 mm around the fitted head.
Local morphs do not prove skinning or animated anatomy. Selected CPU renders
include side/front/rear/head/face, both stripe families and ear/tail extremes.
The revised review was opened on the rightmost monitor with one new follow-up;
the owner must still approve the face and variation. No other animal is queued.

`artifacts/fauna-deer-stage1-20261023/Deer - Stage1 Model Review.blend` opens via
`python scripts/open-deer-mesh2motion.py --root artifacts/fauna-deer-stage1-20261023 --stage1 --rightmost`.
No shortcut is needed or created. Prior files/windows remain separate.

Focus its viewport: **1 model1/1 / 2 seeded appearance / 3 side–face–orbit**.
Header Front body/Rear buttons permit alignment checks. Visible status gives
model, coat and phenotype seed. The original review has four packed2K colour/roughness/normal sets:
tawny summer, spotted russet, grey and pale winter; small bounded shader
hue/saturation/value changes extend the preview. These are appearance examples,
not saved generated ecology taxa, runtime seasonal evolution, sex dimorphism or
a proven universal animal generator. Antlers remain deferred; eight simple
whisker ribbons are included. No Stage2 controls/behaviour showcase are claimed.

## Shape and preparation

Fresh built-in imagegen reference, with exact prompts in ignored
`plan-and-prompts.json`. The first image turned the face toward the camera and
was rejected before3D inference; a targeted head/neck correction produced the
used side reference. Hash db0a77e5c856db81a4ebb581bd144d04b3ec691e35c07aa1a772d918782069ac.
Local TripoSG revision fc5c40990181e2a756c4e0b1c2f4d6b5202faf8c,
seed732123, FP16,50steps, ordinary depth8 hierarchical decoder. Raw mesh hash
10d05b275d03ccc667f6830d62c42654404a6e539aaa13b6d58d3371ac977208;
raw mesh/latents/provenance retained. No previous deer body substitution.

Offline alignment places +Y forward/Z up, normalizes body height1.65m and mirrors
neutral anatomy to remove lateral spine/neck lean. Reduced body reserves room
for eyes/face. Generated ear folds were unsuitable: local correction rebuilds
closed curved ear blades and the adjoining skull crown. Eye neighborhoods are
refined, shaped beds opened and lid rims stitched directly to the body boundary;
separate curved eyeballs use a small iris/horizontal-pupil texture and live
gloss. This includes authored geometric correction, not pristine one-shot output.
Source and corrected intermediate meshes remain separate.

Unrigged anatomical surface tags replace the old paint dependency on skin
weights. Reused accepted imagegen fur supplies detail; continuous position-based
head/neck masks avoid the previous sharp bone-weight coat boundary. Belly,
throat, muzzle, ear interior, tail underside and hoof regions remain explicit.
New atlas/normals belong to this mesh; no old-model normal map is applied.

## Shared GPU run and setup limits

Successful run **218.04s including loading, sampling and extraction**; sampling
approximately181s. Peak Torch allocated **412.8MiB**, not total driver/native/
game memory.106 NVML samples: maximum total device memory3808MiB, maximum68°C.
The Forest PID5144 remained present through generation and review. No game FPS,
latency or all-future-workload stability measurement was made. This demonstrates
one coexistence run, not identical numerical output versus isolated inference.
Sequential offload retains the same model/FP16/steps/resolution, with no intended
quality sacrifice.

Several earlier attempts failed while loading CPU checkpoints, before sampling.
Mapped safetensors access, Rust deserialization and later dtype-copy paths were
unstable under the old environment/available commit. The successful isolated
`artifacts/tools/triposg-review-venv` reads Torch2.8.0+cu128 from the task's D:
runtime and existing Diffusers0.32.2/Transformers4.49 dependencies. The owned reader
uses tensor header/offset validation and NumPy allocations, casting each requested
FP32 weight to FP16 before retaining it. This avoids keeping the5.36GiB FP32
transformer plus its half copy. Those combined changes succeeded; they do not
individually prove every earlier native crash's cause. Shared environments,
ComfyUI, driver settings, clocks and owner applications were not modified.

The wrapper's opt-in shared mode now conservatively uses device-wide NVML free
VRAM because WDDM CUDA free figures can differ, reserves512MiB, caps its allocator,
logs resources and stops at a sampling boundary if temperature reaches85°C.
Default non-shared invocation keeps its old5.8GiB guard. No game closure occurred.

This first setup/cleanup exceeded the optimistic happy-path estimate: reference
copied20:45 local, generated raw mesh20:59, final review saved about21:33. The
successful218s run excludes prior loader fixes, reference work, art correction,
painting and review construction. Do not advertise this whole pass as30minutes.

## Checks and remaining approval

The initial review had **5742 rendered triangles**, no high source mesh or armature in the scene.
Its reload checks passed finite geometry/UV, exact paired eye-anchor heights, four
packed map sets, model/appearance/view operators and all four coat selections.
Maximum post-fit body mirrored-vertex nearest distance5.09mm; original neutral
body was mirrored, but local lid fitting is not claimed perfectly symmetric.
CPU side/front/rear/head/face views inspected. Earlier floating lids, skin across
corneas, ear debris, bad crown closure and inconsistent face winding were
corrected locally. Native structural checks do not establish owner art approval,
natural motion, production retopology, skinning, collision or in-game performance.

One user follow-up was sent after opening the initial review; the owner requested
the facial/species corrections described above. State persists in ignored
`review-state.json`. No additional animal is queued. Silence never approves
rigging. The accepted earlier deer and its rig/actions/Eating correction remain
untouched. No Unity/world/save/settings/media/player-shortcut changes, no paid
Tripo API/key use, no new model-weight downloads or game launch. Shape tool/model
MIT and existing fur provenance retained; do not label all materials/toolchains
CC0. Git backs up source/docs, not generated models/media/evidence.
