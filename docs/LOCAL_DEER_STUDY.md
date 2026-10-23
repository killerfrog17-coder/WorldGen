# Local generated deer study

Owner requests a genuinely newly generated deer for Blender review, using free
local tools, without purchasing credits or substituting a downloaded animal.
No Unity integration, game launch, save or game-shortcut changes are part of this
study. Existing rejected Quaternius review is a separate earlier experiment.

## Route and provenance

1. Generate an isolated adult doe reference using the built-in imagegen tool.
   This image-generation step is hosted; **3D inference is local**. The reference
   prompt asks for photorealistic red-deer anatomy, no antlers, full standing body,
   four distinctly visible legs, near side-on view, transparent background and
   soft neutral illumination. It does not use a downloaded animal/reference.
2. TripoSG code: https://github.com/VAST-AI-Research/TripoSG
   revision `fc5c40990181e2a756c4e0b1c2f4d6b5202faf8c` (MIT).
   Model: https://huggingface.co/VAST-AI/TripoSG
   revision `2c1c516d22d58db486a058d98d31bb6177344e06` (MIT model card).
   Three weight SHA256s are recorded with the ignored study artifacts.
3. `scripts/generate-deer-local.py` composites the provided alpha onto a padded
   white square, then runs 50 image-conditioned steps, seed 731923, FP16, using
   CPU offload. It saves final latents before extraction for decoder-only retry.
   A 256-grid hierarchical SDF extraction uses the upstream scikit-image route
   with 4096-point decoder chunks. The optional `diso` CUDA extension is not
   installed: its import is stubbed to fail explicitly if accidentally invoked;
   the selected hierarchical route does not call it. No upstream files patched.
4. `scripts/prepare-local-deer-review.py` imports the generated GLB into Blender,
   stages a neutral inspection scene and retains the generated surface under a
   non-applied review decimator. Material preparation and animation must be
   identified separately from shape generation; TripoSG generates **shape only**.
   `scripts/local-deer-review-controls.py` loads an explicit inspection sidebar.

Weights/environment are in `E:/AI/WorldGen3D`, not in the game/build output or
source control. The isolated Python environment reads existing ComfyUI CUDA
packages through a .pth entry without changing that environment or its server.
Local overrides: diffusers 0.32.2, transformers 4.49.0, tokenizers 0.21.1,
huggingface_hub 0.29.3, accelerate 1.4.0, peft 0.14.0, omegaconf 2.3.0.
The existing Torch installation is 2.14.1+cu130. No hosted Tripo generation/API
request or payment is used. The Windows-user Tripo key is not needed here.

## Current study artifacts

`artifacts/deer-local-20261023/reference.png` is the original imagegen output.
Generated mesh, latents, model hashes, timings, inference provenance, CPU-rendered
inspection images and Blender file are stored beside it as produced. These are
ignored outputs; a source-code backup does not back up these assets or weights.

GPU inference is explicitly gated by `--infer`; without it the script imports
dependencies only. Wait for the owner's confirmation that the game is closed
before running inference. Do not close applications, kill/unload the shared
ComfyUI service, or change GPU settings. Blender review opens in low-cost Solid
view. Background inspection renders use Cycles CPU with four threads.

## Acceptance boundaries

Dependency import/preflight passing is not successful mesh generation. A valid
GLB is not proof of correct anatomy, animation-ready topology, good skinning or
game performance. Inspect all sides and leg separation before claiming a useful
deer. Generated topology may need cleanup/retopology. Preview loops, if later
added, are not an implemented game locomotion/blending controller.

## Completed review checkpoint, 23 October 2026 local

The generated source is `deer-raw.glb`, SHA256
`71a2217e32589e07223b19cf2fd4ac3ed6f5c761a5320609593b683745212f7e`:
113,360 vertices / 226,722 triangles, not watertight. Denoising was approximately
32 seconds on RTX4060. An export-wrapper return-type error occurred after
successful extraction; fixed and recovered from saved latents. The recorded
4.06-second JSON timing is **decoder-only recovery**, not total generation time.
No repeated denoising was needed. No CUDA diso build or background remover used.

`Generated Deer - Review v2.blend` is the owner review, opened on the primary
centre monitor. `Deer - Local AI Review.lnk` on the Desktop launches it with the
Generated Deer sidebar. The older CC0 study/shortcut and first local study remain
separate; no user-open Blender file was overwritten or closed. Default Material
Preview shows the actual surface shader; Solid is available to lower viewport
GPU cost. Heavy inference has exited; the owner may reopen their game.

The review uses 25,002 vertices / 50,000 triangles and an original asset-specific
27-bone skeleton. `scripts/rig-local-deer-study.py` fits measured landmarks,
weights at most four bones per vertex, authors IK-target gait studies, then bakes
them into ordinary FK actions: Idle 96 / Walk 48 / Run 24 frames at 24fps, plus
their repeated final endpoint. No downloaded mesh, skeleton or animal animation
was used in this local study. These are **authored approximate loops**, not AI
motion generation or calibrated deer gait. Belly/leg influence leakage was
corrected after inspecting initial poses. No production controller, turning,
speed matching, gameplay needs/AI, animation crossfades or terrain foot placement.

The coat reuses the newly generated photo as a simple mirrored planar projection
with procedural fallback, not a generated 360-degree texture. Back-of-head
projection is masked by facing/height to avoid an obviously duplicated face.
Seams, stretching, static-view bias, pose/skinning artifacts and anatomical
imperfections remain. Inspect all sides; do not call this final realistic art.

`scripts/check-local-deer-study.py` passes offline Blender checks across all
171 frames (including repeated endpoints): finite bounded vertices, normalized
one-to-four influences, baked/no live constraints and closed loops. Maximum
endpoint vertex differences are 0 / 0.0000002384 / 0.0000002980m. This is not
natural-motion or game/FPS acceptance. Selected CPU-rendered poses and opposite
side were inspected. The full game/world was never launched/tested/profiled.

All new scripts pass Python syntax compilation and source whitespace checks.
The previous rejected CC0 experiment's untracked scripts are not silently
included in this checkpoint. Models, Blender files, bitmaps and weights remain
ignored artifacts, separate from the source-code backup.

## Exact imagegen reference prompt

Built-in imagegen, `transparent_background=true`, no reference image:

> Create one photorealistic adult red deer doe (female, NO antlers), a clean anatomical reference for generating a riggable 3D game animal. Entire animal isolated on genuinely transparent background, no floor or ground shadow, no scenery, no text. Full body fills image with generous small margin, ears and all four hooves fully visible. Natural realistic adult deer anatomy: long slender legs with distinct joints and cloven hooves, muscular but lean brown torso, long neck, elongated tapered deer muzzle with black nose, modest dark eyes, two alert large ears, short tail and pale rump. Smooth short brown summer coat, pale underside, not cartoon, not toy, not fawn, no spots. Neutral relaxed standing pose, head aligned forward with body, camera nearly side-on but about 20 degrees around toward the front, at torso height, long lens/orthographic-like, allowing all four legs and hooves to be distinctly visible with clear empty space between them, far-side legs slightly offset from near-side legs. Four legs only, straight grounded stance. Soft even neutral studio illumination without dramatic shadows or bright highlights. One animal only, one view only, not a contact sheet. Practical production reference, believable proportions and continuous body surfaces.
