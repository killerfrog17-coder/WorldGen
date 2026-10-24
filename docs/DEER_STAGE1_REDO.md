# Fresh deer stage1 review — 23 October2026

Owner authorized a fresh one-candidate deer using the revised two-gate workflow,
explicitly trying GPU generation while The Forest runs. **Stage1 only is ready;
model/appearance approval is pending before any new rigging or animation.**
See [review/queue rules](FAUNA_ASSET_REVIEW_WORKFLOW.md). No Desktop item created.

## Review

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
