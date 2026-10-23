# Local deer animation comparison — 23 October 2026

Two actual local neural-animation trials are complete on the accepted deer.
UniMate v3 is the more promising result in this trial, but has not passed owner
gait review. MoCapAnything V2 ran successfully; its decoded rotations tip the
body, and a separate position-based reconstruction folds the torso. Both
MoCap variants are rejected. This is not a claim that the model cannot work
with other references, inputs or rigs.

## Review

Desktop **Deer - AI Motion Comparison.lnk** opens the separate
`artifacts/deer-motion-comparison-20261023/Deer - AI Motion Comparison.blend`.
The top-left selector chooses the motion family. **Raw AI / Cleaned** switches
the same selected take; **Pause / Play** and **Replay** control playback.
**Current CC0-derived** selects the existing native Walk/Run/Graze/Alert.
The right-hand **Motion** sidebar also has explicit buttons and status labels.
The viewport follows the deer so forward-moving takes stay visible.

These are two-second UniMate takes and a three-second video take. They replay
with an explicit end/start jump: they are not finished seamless game loops or
walk/run transitions. The viewer does not apply the prior procedural gaze,
live foot IK or terrain controller. Native baseline means the existing baked
CC0-derived clips, not pristine donor motions or a newly generated baseline.

The new review leaves the previous prototype/reviews and all game, Unity,
world, save and settings files intact. No existing Blender/user app was closed.
The owner explicitly closed The Forest before GPU inference. All owned
generation processes exited; the review GUI is the only task viewer left open.

## Trials and results

| Trial | Inputs and output | Outcome |
|---|---|---|
| UniMate official v3 | Same 33-joint deer; four prompts; three independent takes each; 60 frames at 30 Hz; seed 732100, guidance 4, batch 1 | 12 retained raw samples. Final heuristic gate rejects four: two feeding takes never lower the head, one walk and one run barely travel despite the forward prompt. Eight require review. |
| UniMate contact cleanup | Best non-rejected take for each prompt; offline stance anchors and three-joint damped IK, eased boundaries | Run and feeding corrections meet the numeric non-regression guard. Walk and alert corrections are retained for inspection but rejected by the guard. No automatic game admission. |
| MoCapAnything V2 neural rotations | Own deer reference + 90 frames, seconds 8–11 of CC0 mule-deer footage | Inference/checkpoint loading succeeds; raw deformation visibly tips the body and penetrates the ground. Rejected. |
| MoCap neural positions + offline swing reconstruction | Same predicted positions; exact bind lengths, smoothed directions; no new inference | Better upright legs, but torso/neck fold badly; maximum torso joint deviation is approximately 180°. Rejected, including cleanup. |

The ranking includes finite/bounded coordinates, large rotation discontinuities,
ground penetration/contact, planted-foot sliding, relative foot movement,
forward travel, feeding head lowering, torso folding, acceleration and a
candidate loop-boundary score. Thresholds are deer-standing/locomotion/feed
heuristics. They are not a universal anatomical/collision classifier.
`REVIEW` expressly does not mean natural, usable or accepted.

The initial scoring preferred nearly stationary “run” samples because they
had less acceleration. The corrected forward-travel gate avoids that failure.
The native surface inspection exposed the separate MoCap torso failure; joint
positions, finite weights and lower sliding scores alone did not establish
quality. No full mesh self-intersection/contact acceptance is claimed.

Cleanup leaves the root track and bone lengths unchanged. A five-frame median
contact mask, minimum four-frame stance, eased five-frame boundaries and
bounded three-joint damped least-squares corrections operate offline. Only
the correction rotations are lightly filtered. Original model features and
rejected takes/corrections are never deleted. Guard requirements include
non-rejected semantic/anatomical status, no material worsening of penetration,
rotation jumps, acceleration or aggregate score. There is no root-height lift
per frame to hide penetration.

UniMate's twelve takes took **322.83 seconds** of wrapper inference time.
The video-model retry with cached DINO features took **2.47 seconds**, with
about **1813 MiB** peak Torch allocation. That second figure excludes video
decode, segmentation, DINO feature generation, installation, CPU preparation
and review/export. It must not be represented as full video-pipeline time.
The UniMate sampler reports 441 MiB peak allocation, while an independent
NVIDIA observation during inference was about 1779 MiB in use; allocator
metrics do not represent total process/driver memory. Neither job approaches
the RTX 4060's 8 GB capacity. No paid endpoint, signup or API request was used.

## Coordinate and deformation checks

The game rig has a separate ground-level `MotionRoot`. Learned conditioning
uses a **copy** with the anatomical Pelvis as root and 33 joints. The accepted
native 34-bone rig/skin remains unchanged. UniMate rule-based semantic labels
were explicitly reviewed as bone segments (forelimbs are arm segments;
hindlimbs are leg segments); no hosted language-model annotation was used.
The generated FK decoder uses **tpos_offsets**, not raw export offsets.

The initial MoCap FBX extraction assumed centimetres and a Y-up source. Those
assumptions do not fit this Blender asset. `build-deer-mocap-input.py` replaces
the inference input with an evaluated native-rig export in **metres, Y-up,
+Z forward**, with consistent mesh, UV, weights, offsets, local rotations and
reference render. Non-root translation residual against the native Walk is
under 0.00000045 m. No Truebones character or animation was downloaded.
Reference embeddings correspond to native Walk frame 0, the same reference
pose consumed by the model. DINO image batches are limited to two; the
upstream batch-512 feature helper is not used on an 8 GB GPU.

The same accepted mesh, skin, UV and image pixels are used for the actual
comparison. Rest positions fit each learned skeleton to the native rig, then
global rotation deltas are expressed in its real bind axes. All **840**
experimental frames across 12 raw/clean actions were evaluated, including
finite skinned surfaces. Maximum joint-head replay discrepancy is below
**0.0000012 m**. Mesh/skin/UV digest and image-pixel hashes match the source;
the source `.blend` SHA remains unchanged. These establish correct transfer
and preservation, not correct neural motion. Ground penetration remains
visible and is reported rather than repaired indiscriminately.

Portable Blender 4.5.15 checks all **16** displayed actions, action slots,
frame ranges, 24/30 Hz settings, finite selected poses and non-static movement.
Selected CPU renders were inspected; the native GUI was opened and its
top-left controls verified visible in a desktop capture. No game launch,
gameplay tests or FPS profiling was performed. Blender emits its pre-existing
small shutdown “Not freed memory blocks” message after successful checks;
it is not a model/gait pass or a fatal export error.

## Sources, rights and versions

This comparison is **private research only**, separately stored from game
assets. Do not describe the whole comparison as CC0 or generally shippable.

- [UniMate code](https://github.com/Friedrich-M/UniMate), commit
  `b78c7807df7cf4231d4e67f8d0ece29c5e11025b`: MIT code; its
  [released checkpoints](https://huggingface.co/Linzhan/UniMate/blob/main/LICENSE)
  are CC BY-NC 4.0. Upstream source datasets retain their own terms. Its
  documented stance-contact cleanup informed this offline experiment.
- UniMate checkpoint revision
  `174fc755ec248d0c98f27759b7a95648e7f05905`, v3 step 150000,
  SHA256 `f991ddd87262c251f4681d18297a6163ab4f139daa28c86d5ef0a66771df7bc6`.
- [MoCapAnything code](https://github.com/phongdaot/MocapAnything), commit
  `c99fb22c2f6001958c73c197065322b98ca0b93b` and
  [V2 weights](https://huggingface.co/kehong/MoCapAnythingV2-weights): MIT;
  checkpoint revision `4b8c28e69aea9e39610264818d83153d59219bd6`.
  Upstream describes this release as a reimplementation, not the exact
  original paper code. Results here are not a benchmark reproduction.
- [BRIA RMBG-1.4](https://huggingface.co/briaai/RMBG-1.4) has separate BRIA
  terms; its freely available weights are used only for this non-commercial
  experiment. Do not infer commercial clearance from MoCap's MIT label.
- [DINOv2](https://huggingface.co/facebook/dinov2-large),
  [FLAN T5](https://huggingface.co/google/flan-t5-base) and the existing local
  T5-base provide feature/semantic conditioning. Full pinned download hashes
  and revisions are in ignored `model-inventory.json`.
- [Mule-deer foraging video](https://commons.wikimedia.org/wiki/File:Mule_Deer_foraging_at_Okanagan_Mountain_Provincial_Park.webm),
  Extemporalist, 9 February 2015, CC0. One real deer, browsing/looking around
  with small steps; it is not a clean sustained walking-cycle ground truth.
  Segmented masks were visually inspected. An initially downloaded NPS clip
  was rejected as input because its deer legs are cropped.
- Accepted mesh/paint/rig and authored-motion provenance remain in
  [DEER_GAME_ASSET_PIPELINE.md](DEER_GAME_ASSET_PIPELINE.md) and
  [DEER_BEHAVIOUR_PROTOTYPE.md](DEER_BEHAVIOUR_PROTOTYPE.md).

## Reproduction and evidence

All generated media, models, environments, scores, logs and `.blend` files are
ignored under `artifacts/deer-motion-comparison-20261023` or `artifacts/tools`.
Source checkpoints do not back up those large assets. Model inventory resumes
against the recorded revision, verifies existing hashes and writes atomically.

The isolated `artifacts/tools/motion-eval-venv` uses Python 3.11 with bpy 4.5.3,
NumPy 1.26.4, pandas 2.2.3, Motion, SciPy, Transformers, torchdiffeq,
torch-geometric, roma, OpenCV and imageio-ffmpeg. A `.pth` reads the existing
Torch/CUDA/transformer packages from the rig and Comfy environments; neither
shared environment nor server was modified. Do not install upstream training
requirements indiscriminately or assume this local `.pth` is portable.

Run scripts from the repository root with the isolated Python, except the
explicit native Blender scripts:

1. `setup-deer-motion-comparison.py --root <root> --download` downloads pinned
   inference files. Clone the two repos at the commits above separately.
2. Native Blender: `prepare-deer-motion-inputs.py --source <accepted blend>
   --root <root>`. Run UniMate `data_process.rig_preprocess` with rule annotation,
   review its anatomy labels and retain `unimate-rig/cond.npy` and preview.
3. `run-deer-unimate.py --root <root> --repo <UniMate repo> --preflight` is CPU
   only. After the owner closes the game, run without `--preflight`, using
   `--takes 3 --name pilot --guidance 4`. Existing output directories are refused.
4. `score-deer-unimate.py --root <root> --repo <UniMate repo> --samples
   <root>/unimate-samples/pilot` decodes and ranks all takes.
5. `build-deer-mocap-input.py --root <root> --repo <MoCap repo> --source
   <accepted blend>` prepares CPU-only native inputs. With the cited source
   video at `input/mule-deer-CC0.webm`, `run-deer-mocap.py --root <root> --repo
   <MoCap repo>` runs the video pipeline. `--reuse-features` retries model/decode
   without repeating segmentation/DINO; raw arrays are saved before BVH decode.
6. `reconstruct-deer-mocap-pose.py --root <root>` is the rejected alternative
   position decode. `clean-deer-motion.py --root <root>` writes independent
   raw/clean comparison arrays and the complete guard report.
7. `build-deer-motion-comparison.py --root <root> --source <accepted blend>
   [--render]` creates the separate review with native actions. Native portable
   Blender runs `check-deer-motion-comparison.py -- --root <root>`.
8. `open-deer-motion-comparison.py` opens the finished viewer on the primary
   monitor. Controls are supplied by `deer-motion-comparison-viewer.py`.

Key evidence: `candidate-scores.json`, `comparison-report.json`,
`mocap-native-input-check.json`, `native-comparison-checks.json`,
`viewer-control-checks.json`, raw features, masked-frame previews and selected
CPU renders. No result was automatically promoted into the game.
