# Deer mesh and texture replacement — 23 October 2026

The owner approved the latest CC0-authored motion on the generated deer and
requested a sharper texture without Hunyuan's geographic output restrictions,
plus practical geometry/texture budgets. This work changes offline asset
preparation only. It does not add a fauna agent or import anything into Unity.

## Review and checks

Desktop **Deer - Game Asset Review.lnk** opens
`artifacts/deer-realvis-20261023/Deer - Game Asset Review v2.blend`.
Top-left **Idle / Walk / Run / Pause-Play** controls are visible; the right
Generated Deer panel adds mesh LOD comparison buttons. The same approved motion
automatically starts with Walk. Prior review files remain separate.

The successful 13-view RealVis paint totals **536.88 seconds**, peak **4554.3MiB
Torch allocation**; Marigold totals **54.03 seconds**, peak **1987MiB**. This
includes two CPU-decoded pilots and eleven faster FP16-safe GPU decodes, not the
entire research/setup/rejected-iteration elapsed time. There is no runtime AI.

The selected file passes exact rest rig/all123 evaluated bone-frame comparisons,
1–4 normalized skin influence checks, UV/material/packed image checks and selected
finite deformed poses for every mesh LOD. At4167 sampled source points the reduced
surface distance is at most **1.284mm**,95th percentile **0.561mm** on the1.65m
animal. Old Hunyuan packed-image hash is absent. Direct painted atlas coverage
is **97.58%**; unseen creases use nearby surface fill. Normal bake neutralizes
1893 reversed/invalid pixels out of1024² to avoid wrong thin-part ray hits.
Source contact sheet and final CPU front/rear renders inspected. These checks
do not establish runtime FPS, production topology, foot locking, linear Unity
skinning equivalence or final art approval. Some independent-view markings and
seams remain possible, particularly around the head/ears.

## Chosen route

Keep the accepted, locally generated **TripoSG shape**, and rebuild its game
meshes, UVs and material. Generating another random shape would discard a shape
already accepted and require fitting the approved rig again. The skeleton and
authored action channels remain literal; mesh reduction interpolates skin
weights and limits each vertex to four influences.

1. Weld identical old UV seam vertices, reduce the review sculpt from roughly
   50,000 to **13,999 triangles**, and make a new xatlas UV layout.
2. Render exact depth, silhouette and neutral anatomical colour guides at
   **1024²**: ten body views and three close-up head views. The guides contain
   no Hunyuan texture pixels or borrowed animal images.
3. Paint each view locally with **RealVisXL V5.0**, an SDXL photorealism fine-tune,
   plus the small SDXL depth ControlNet. Use the standard SDXL encoders and VAE;
   FP8 weight storage/FP16 computation fits the RTX 4060. These are independently
   generated guided views, not a jointly trained multiview texture network.
4. Estimate broad illumination with **Marigold IID lighting**, retaining source
   fur detail and chromaticity. Apply only a bounded, low-frequency luminance
   correction, normalized to the median brown-coat exposure. This is estimated
   de-lighting, not a measured physically exact albedo.
5. Bake a **2048² colour atlas** with depth visibility, background rejection and
   steep facing weights. Fuse broad body colour with a weighted median to reject
   isolated view-colour outliers while preserving
   fine detail from the best-facing view; prioritize close-up facial views.
   Fill unseen creases from nearby painted surfaces and pad island borders.
6. Bake a **1024² tangent normal map** from the higher-detail own shape, then
   discard the dense bake geometry from the review scene. Roughness is fixed
   matte and metallicity zero; do not claim generated complete PBR maps.
7. Prepare **6,000 / 2,200 / 700 triangle** offline LOD variants sharing the
   material, UV atlas and approved rig. Only one is visible in the viewer.

The game will eventually need proper import compression/mips, runtime LOD
thresholds, skinning checks and fauna rendering/behaviour. None is silently
implemented by an offline Blender file. In particular Blender's preserve-volume
skinning is not a promise of an identical linear-skinned Unity result.
The diffusion weights and source-view images are offline tools, not part of the
animal's runtime material or the game installation.

## Why this route

[StableGen](https://github.com/sakalond/StableGen) documents depth-guided SDXL
multiview projection as a practical way to texture existing meshes. We use our
own CPU raster/bake and isolated Diffusers scripts; no addon bundle was installed
and the shared ComfyUI environment/server was not changed.

[TRELLIS.2](https://github.com/microsoft/TRELLIS.2) can generate materials on an
existing shape. Its official setup requires Linux, a CUDA toolkit and at least
24GB VRAM. Community low-memory ports exist, but would add substantial setup to
an already accepted shape/rig. Native export dependencies also need a separate
license review; an MIT top-level repository alone is insufficient. Not run.

Base SDXL produced several good photo-like views but also some illustrated or
plastic views, despite the negative prompt. Their colours caused unacceptable
flat patches in the 3D bake; that batch is rejected. RealVisXL replaced those
paint models after body/head pilot inspection. Old rejected candidates remain
separate, not used to initialize the replacement texture.

## Sources and licenses

| Part | Source / terms |
|---|---|
| Shape | Our existing TripoSG inference; model/code MIT |
| Rig | Existing SkinTokens inference and geometric weight cleanup; model/code MIT |
| Motion | Quaternius [Ultimate Animated Animals](https://quaternius.com/packs/ultimateanimatedanimals.html), CC0; authored Idle/Walk/Gallop retargeted with bind-axis correction |
| Colour diffusion | [RealVisXL V5.0](https://huggingface.co/SG161222/RealVisXL_V5.0), OpenRAIL++ |
| Encoders/VAE base | [SDXL base 1.0](https://huggingface.co/stabilityai/stable-diffusion-xl-base-1.0), [OpenRAIL++ license](https://huggingface.co/stabilityai/stable-diffusion-xl-base-1.0/blob/main/LICENSE.md) |
| Shape guidance | [SDXL depth-small](https://huggingface.co/diffusers/controlnet-depth-sdxl-1.0-small), OpenRAIL++ |
| FP16-safe decoder | [madebyollin SDXL VAE fix](https://huggingface.co/madebyollin/sdxl-vae-fp16-fix), model card MIT, derived from SDXL VAE |
| Illumination estimate | [Marigold IID lighting v1-1](https://huggingface.co/prs-eth/marigold-iid-lighting-v1-1), OpenRAIL++; Diffusers code Apache 2 |

The OpenRAIL model licenses have use restrictions but no Hunyuan-style EU/UK/
South Korea exclusion on generated outputs. This animal/game use is different
from distributing model weights or hosting inference. These components do not
require publishing the game's source. Do not label the complete generated asset
CC0 merely because its animation source is CC0. No AnyTop motion or Hunyuan
texture output is admitted into this replacement asset.

Pinned weight revisions (download manifests also record file sizes/hashes):

| Model | Revision |
|---|---|
| SDXL base | `462165984030d82259a11f4367a4eed129e94a7b` |
| Depth small | `daf3835d036574dff7c158882e8e77b75b024ee5` |
| RealVisXL V5 | `ac93e0dda1f6d448cae19bbfab8c5e720a5e48bc` |
| FP16-safe VAE | `207b116dae70ace3637169f1ddd2434b91b3a8cd` |
| Marigold lighting | `08c3930bb641abf786ba44ce92547507ebefbc16` |

## Repeating the offline pipeline

Use the portable Blender 4.5.15 already installed under `artifacts/tools`.
CPU UV/raster/bake runs in `E:/AI/WorldGen3D/venv`; inference runs in the existing
`E:/AI/WorldGen3D/rig-venv` with Diffusers 0.35.2 and Accelerate 1.4.0. Do not
install into or restart the shared ComfyUI server.

- `setup-deer-sdxl.py`: selective public model downloads, pinned revisions/hashes;
  `--kind base`, `realvis`, `vae`, or `delight`, with separate output roots.
- Blender `prepare-deer-game-mesh.py prepare`: source accepted CC0-motion review,
  fresh `--root`, default 14k target. Clears the rejected material before guides.
- `deer-sdxl-texture-cpu.py prepare`: new UVs, smooth geometric normals and views.
- `paint-deer-sdxl.py`: local model roots, fresh asset root, `--fp8 --steps 36`,
  `--appearance-root` RealVisXL and optional `--fast-vae` fixed decoder. Seed base
  731940, per-view offset. Completed images are retained; latent resume checks
  its request manifest rather than silently reusing different settings.
- `delight-deer-views.py`: local Marigold model, asset root; final 768px/four-step
  inference, seed base 732000. CPU previews are separate diagnostic filenames.
- `deer-sdxl-texture-cpu.py bake --delighted`: reviewed albedo views to 2K atlas.
- Blender `prepare-deer-game-mesh.py assemble`: unpainted reduced source, same
  asset root; creates packed textures, normal bake, LODs and review scene.
- Blender `check-deer-game-asset.py`: compare literal rest rig/all123 evaluated
  bone frames with the approved motion review; inspect skin/UV/material budgets
  and sampled shape distance. Does not certify art or runtime performance.

Raw candidates, source/reference meshes, weights, hashes, timing records and
Blender review files are ignored artifacts, not backed up by the source commit.
No game executable, WorldGen launch shortcut, save, ecology or world generation changes.

## Setup failures retained as evidence

Manual attention slicing replaced native SDPA and was slow; it is disabled.
Original full-size GPU FP32 VAE decode failed before output publication. CPU
decode with checkpointed latents worked; the FP16-safe GPU decoder then reduced
that cost. Installed Torch/Transformers mapped-storage loading also caused native
Windows access violations on selected FP16-to-FP32 paths: owned model loaders
use non-mapped bytes where needed. The CLIP encoder's non-persistent position-ID
buffer must be materialized when constructing its parameters on a meta device.
No shared package files or GPU/server process were patched/killed to fix this.

Visual inspection remains necessary: independent views can disagree in markings,
eyes, outlines or fur direction. Foreground rejection prevents studio backdrop
bleed, but is not automatic semantic approval. This is a placeholder asset
pipeline, not animation-quality certification or a measured game FPS gain.
