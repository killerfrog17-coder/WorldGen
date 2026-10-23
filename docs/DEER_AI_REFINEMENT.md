# Deer texture, rig and motion refinement — 23 October 2026

Owner accepts the generated shape but rejects single-image projection and
invented gait curves. This is a **private Blender pipeline evaluation**, not a
new in-game animal or an accepted redistributable animation library. No money,
subscriptions, API credits, game/world launches, profiling or game changes.

## Review

**Owner subsequently accepts the mesh/texture and rejects all three AnyTop
animations.** Keep those assets as a failed motion experiment; do not describe
them as usable locomotion. No further neural takes were generated.

New Desktop **Deer - Authored Motion Comparison.lnk** opens the separate
`Deer - Authored Motion Comparison.blend`. Top-left **Idle / Walk / Run /
Pause-Play** controls automatically start Walk. This keeps the exact accepted
AI mesh, UV texture and SkinTokens rig/weights, and uses **Quaternius CC0 authored
deer motion** as a diagnostic control. No downloaded deer geometry replaces ours.
Animation source: [Ultimate Animated Animals](https://quaternius.com/packs/ultimateanimatedanimals.html),
already downloaded GLB SHA256
`fabf360b22eaccccd056ee99d698a9562d15aa245821f96e4fbad6db3751604e`.

The old retarget applied local rotation deltas without fully converting source
and target bind axes. The control uses world deformation rotations with bind
correction. Comparing it changes BOTH motion source and rotation conversion;
it cannot establish that AnyTop alone was responsible. The same predicted skin
can still have anatomical defects. Root-only floor correction reaches 12.24cm
in Walk and 6.45cm in Run: this is evidence of proportion/contact mismatch, not
successful foot locking. No claim that this diagnostic is a finished game gait.
Source Idle/Walk have duplicate endpoint poses; Gallop has a unique last pose
and must retain it when looping. No handcrafted gait curves were added.

Offline checks compare unchanged rest vertices, UVs, skin and decoded texture
pixels, absence of donor geometry, finite bounded evaluated vertices over123
frames. Selected CPU poses inspected. They do not certify natural movement,
self-intersections, planted feet, blending, turns or runtime performance.
Scripts: `retarget-deer-authored-control.py`, `check-deer-authored-control.py`,
`inspect-deer-donor.py`. Evidence `authored-comparison-checks.json` is ignored.

Desktop **Deer - AI Refined Review.lnk** opens
`artifacts/deer-refine-20261023/Deer - AI Refined Review.blend` with
`scripts/local-deer-review-controls.py`. It automatically plays the first take.
**AI take 1 / 2 / 3** and **Pause / Play** are at the left of the top viewport
bar; selecting a take starts playback. Sidebar discovery is no longer required.
The generated neural takes are 120 frames at 20fps, not finished seamless loops
or deliberately selected walk/run/graze actions. They wrap at their endpoints.
The original generated-deer and older CC0 reviews remain separate.

The final viewer was opened on the centre/primary 1920×1080 display. Actual
top-bar controls and active playback were visually verified. Owner visual/gait
acceptance is pending. All inference processes exited; only review Blender and
the pre-existing shared service may remain. Nothing closes the owner's apps.

## Actual sources of the parts

| Part | Source |
|---|---|
| Shape | Existing newly inferred TripoSG deer, simplified to 50,000 triangles; no new shape generation or downloaded animal mesh |
| Colour texture | **Local Hunyuan3D-2 multiview painting**, using the same owned generated reference plus normal/position controls for six cameras |
| UV layout and bake | xatlas layout, CPU visibility/facing-weighted multiview bake; 2048² colour atlas with nearest painted 3D-surface fill for hidden creases |
| Roughness/metalness | Fixed matte roughness .88 and metalness 0; **not** generated PBR maps |
| Skeleton and initial skin | **Local SkinTokens** prediction, correct Blender Z-up input, five-beam configuration |
| Skin cleanup | Four-neighbour transfer, distance-based suppression of remote bone influences, normalized strongest four weights |
| Motion | **Local AnyTop learned diffusion**, conditioned on its Deer skeleton; three samples, seeds 731926–731928 |
| Retarget/cleanup | Anatomical correspondence to generated rig, fixed target bone lengths and local-rotation transfer, quiet facial parts, root contact correction |

No replacement scripted foot trajectories or downloaded deer animation were
substituted for the neural motion takes. AnyTop conditioning uses released Deer
rest/normalization metadata, which is covered by its separate research terms.
It is not our own training dataset or a text-to-action service.

## Licensing — do not turn this review into a shipping asset silently

- TripoSG code/model and SkinTokens code/model are MIT according to their
  respective repositories/model cards.
- Hunyuan3D-2 uses the **Tencent Hunyuan 3D 2.0 Community License**, not CC0/MIT.
  It includes territory restrictions and output/use conditions. This local US
  evaluation is not a claim that the texture is unrestricted for global game
  distribution. Review its actual license before adopting it for release.
  [License](https://github.com/Tencent-Hunyuan/Hunyuan3D-2/blob/main/LICENSE).
- **AnyTop code is MIT, but its released checkpoints/data are explicitly
  non-commercial research only**, derived from Truebones ZOO; no underlying
  dataset rights are granted. This preview demonstrates technical feasibility
  only. **Do not put these motion takes in the game, shared builds or a reusable
  production asset bank.** A suitable licensed motion source/model remains
  necessary for such use.
  [Checkpoint terms](https://huggingface.co/inbar2344/AnyTop/blob/main/LICENSE).

Cost-free execution is not the same as unrestricted redistribution. No Tripo
cloud key, paid service or account signup was used. One anonymous request to the
public SkinTokens demo was rejected because its declared 450-second GPU duration
exceeded the service maximum; there was no quota workaround or paid escalation.
The successful rigging run was local.

## Measured runs and what failed

### Painting

`scripts/deer-texture-cpu.py prepare` simplifies the existing raw mesh, creates
UVs and rasterizes normal/position controls without a compiled CUDA rasterizer.
`scripts/paint-deer-local.py` imports the upstream 2GP painting network and loads
the original Tencent paint weights. Native Diffusers CPU offload and VAE slicing
fit the six 512² camera images on RTX4060: **43.96 seconds, 4585.2 MiB peak Torch
allocation**, seed731924, 30 steps. These are this single offline run's values,
not game performance or universal timings. Lighting removal was not run because
the reference uses soft studio light; baked illumination can remain.

CPU bake directly covers **98.8005%** of occupied UV texels, filling the remainder
from nearby painted 3D surface and extending island borders by12px. UV mesh and
painted GLB vertex/face arrays compare exactly equal: painting does not change
geometry. The baked atlas is actual multiview synthesis, not the original photo
wrapped around the reverse side. Texture blur, seams, baked lighting and broad
colour inconsistency remain possible; no final art acceptance is claimed.

### Rigging

`scripts/deer-skin-ai.py` uses native Torch SDPA in place of an external
FlashAttention extension and forces Qwen's supported SDPA path. No upstream
files or shared Python packages are patched. An initial raw GLB/Y-up input
produced incomplete leg topology and was rejected; matching the upstream Blender
importer's Z-up coordinates produces **33 joints including four leg chains**.
Final run: **22.93 seconds, 3506.9 MiB peak Torch allocation**, seed731925,
five beams. The published 14GB default is not a hard universal requirement for
this particular input/configuration; this is not a blanket 8GB compatibility
claim for every asset.

Skin predictions are independent occupancies, not guaranteed normalized weights.
One forehoof had strong influence from a distant hind-leg bone. Nearest-point
transfer and normalization alone caused stretched spikes. A geometry-based
distance gate suppresses remote influences before selecting four weights.
Selected corrected poses no longer show those spikes, but this is not universal
intersection/anatomy acceptance or a general autorigging guarantee.

### Learned motion

`scripts/deer-motion-research.py` evaluates AnyTop's quadruped model with T5-base
joint-name conditioning. It does not train a model, use the previous scripted
gait or prompt a cloud service. Three six-second takes generated in **1.47 /
1.26 / 1.26 seconds**, excluding initial text-encoder/model loading. Fixed target
bone lengths, hierarchical rotation retargeting, hoof-end correspondence and
root-only floor correction are applied in
`scripts/prepare-deer-ai-motion-review.py`. Early retargeted poses had hoof
rotation/ground-penetration errors; these were corrected before selecting the
final review. These costs do not imply every animal can be fully rigged and
animated in a few seconds; integration and art review take longer.

Kirin was researched but code release remains pending. Puppeteer requires a
video/flow/depth/tracking optimization stack, with substantial compiled
dependencies; it was inspected, not run. AnyMoLe reports a48GB tested setup and
scene-specific training; not attempted. No claim these are working local options.

## Validation and limits

`scripts/check-deer-refined-review.py` passes **360 evaluated frames**: UVs
present, normalized1–4 influences,33-bone hierarchy, no live constraints, finite
bounded vertices. Final lowest-vertex ranges:

- Take1: -0.00000012 to0.00896m.
- Take2: -0.00000012 to0.02332m.
- Take3: -0.00000013 to0.02019m.

Those are mesh/floor diagnostics after root correction, not precise biomechanical
contact validation. They do not certify natural motion, no self-intersections,
no foot sliding, looping, turning/blending or usable game locomotion. CPU-rendered
front/back texture and selected motion poses were inspected. No Unity/native
game/world launched, gameplay AI added or runtime FPS measured. Python syntax
and source whitespace checks pass. Owner tests the visual/motion quality.

## Reproducibility

Ignored artifacts: `artifacts/deer-refine-20261023`. Environments/models:
`E:/AI/WorldGen3D`, isolated from the existing ComfyUI environment; .pth entries
read its Torch/CUDA packages without changing them. Models/images/Blender files
are not source-backed. `refinement-provenance.json` stores selected output hashes,
and per-stage JSONs store actual seeds, configurations, timings and failures.

Pinned sources and model revisions:

| Component | Code revision | Weight revision |
|---|---|---|
| Hunyuan3D-2GP | `f2456e036a86a4b1d9f58e2379fe7ab0fe9b68b0` | Tencent `9cd649ba6913f7a852e3286bad86bfa9a2d83dcf` |
| SkinTokens | `273b691d35989d71cd17ff2895fdc735097b92d1` | `79736cad0fd84de384d5eede659b4ebd24effe33` |
| AnyTop | `e780d1575ca0121f29bb53821b309cf564156a95` | `2358c7bc236516f60c50003e56940a3a823ea27d` |

Dependencies include the existing Torch2.14.1+cu130, isolated paint
Diffusers0.32.2/Transformers4.49, and rig/motion
Diffusers0.35.2/Transformers4.57.3/Hub0.36.0. No system CUDA Toolkit, WSL setup,
server kill/global unload or paid inference was necessary.
